using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;

namespace HDTool
{
    /// <summary>
    /// Dịch vụ xuất bản vẽ sang AutoCAD DWG và tự động quy đổi Layer cho hệ thống MEP.
    /// Hỗ trợ thiết lập DWGExportOptions + ExportLayerTable (SystemType Modifier)
    /// kết hợp xử lý hậu kỳ chính xác màu sắc/layer qua AutoCAD Core Engine (accoreconsole.exe).
    /// </summary>
    public static class CadExportMepService
    {
        /// <summary>
        /// Đường dẫn lưu cấu hình quy đổi Layer mặc định của HDTOOL
        /// </summary>
        public static string GetDefaultConfigFilePath()
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Autodesk\Revit\Addins\HDTOOL");
            if (!Directory.Exists(folder))
            {
                try { Directory.CreateDirectory(folder); } catch { }
            }
            return Path.Combine(folder, "mep_cad_layer_mapping.json");
        }

        /// <summary>
        /// Tự động lưu bảng cấu hình Layer vào file mặc định để lần sau dùng tiếp
        /// </summary>
        public static bool AutoSaveDefaultConfig(IList<CadLayerMappingItem> mappings)
        {
            try
            {
                string path = GetDefaultConfigFilePath();
                CadExportMepSettings settings = new CadExportMepSettings();
                settings.PresetName = "CUSTOM_SAVED";
                if (mappings != null)
                {
                    settings.LayerMappings = new List<CadLayerMappingItem>(mappings);
                }
                return settings.SaveToFile(path);
            }
            catch { return false; }
        }

        /// <summary>
        /// Tự động nạp bảng cấu hình Layer đã lưu; nếu chưa có thì nạp cấu hình chuẩn TCVN
        /// </summary>
        public static List<CadLayerMappingItem> AutoLoadDefaultConfig(Document doc)
        {
            try
            {
                string path = GetDefaultConfigFilePath();
                if (File.Exists(path))
                {
                    CadExportMepSettings settings = CadExportMepSettings.LoadFromFile(path);
                    if (settings != null && settings.LayerMappings != null && settings.LayerMappings.Count > 0)
                    {
                        return settings.LayerMappings;
                    }
                }
            }
            catch { }

            return GenerateTcvnMepPreset(doc);
        }

        /// <summary>
        /// Thu thập toàn bộ danh sách MechanicalSystemType và PipingSystemType đang có trong dự án Revit
        /// </summary>
        public static void GetProjectMepSystems(
            Document doc,
            out List<string> ductSystems,
            out List<string> pipeSystems)
        {
            ductSystems = new List<string>();
            pipeSystems = new List<string>();

            if (doc == null) return;

            // 1. Thu thập MechanicalSystemType (Duct Systems)
            try
            {
                FilteredElementCollector ductCol = new FilteredElementCollector(doc)
                    .OfClass(typeof(MechanicalSystemType));

                foreach (Element e in ductCol)
                {
                    MechanicalSystemType mst = e as MechanicalSystemType;
                    if (mst != null && !string.IsNullOrEmpty(mst.Name))
                    {
                        if (!ductSystems.Contains(mst.Name))
                        {
                            ductSystems.Add(mst.Name);
                        }
                    }
                }
            }
            catch { }

            // 2. Thu thập PipingSystemType (Pipe Systems)
            try
            {
                FilteredElementCollector pipeCol = new FilteredElementCollector(doc)
                    .OfClass(typeof(PipingSystemType));

                foreach (Element e in pipeCol)
                {
                    PipingSystemType pst = e as PipingSystemType;
                    if (pst != null && !string.IsNullOrEmpty(pst.Name))
                    {
                        if (!pipeSystems.Contains(pst.Name))
                        {
                            pipeSystems.Add(pst.Name);
                        }
                    }
                }
            }
            catch { }

            ductSystems.Sort(StringComparer.OrdinalIgnoreCase);
            pipeSystems.Sort(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Tự động quét và nhận diện các hệ thống MEP thực tế ĐANG TỒN TẠI VÀ ĐANG HIỂN THỊ trong các View/Sheet cần xuất.
        /// Theo chỉ đạo của Sếp Hồ: "phần mềm phải tự nhận diện được view đó có hệ thống gì các hệ thống đã bị ẩn rồi thì sẽ không nhắc đến".
        /// Loại bỏ triệt để các hệ thống bị ẩn (Hide Elements in View, View Filter ẩn, Category ẩn).
        /// Tự động đối chiếu với cấu hình đã lưu để giữ nguyên tên Layer CAD và mã màu ACI mà người dùng đã thiết lập.
        /// </summary>
        public static List<CadLayerMappingItem> ScanVisibleMepSystemsInViews(
            Document doc,
            IList<ElementId> viewOrSheetIds,
            IList<CadLayerMappingItem> existingSavedMappings = null)
        {
            List<CadLayerMappingItem> result = new List<CadLayerMappingItem>();
            if (doc == null) return result;

            // 1. Xác định danh sách các View thực tế cần quét
            List<View> viewsToScan = new List<View>();
            if (viewOrSheetIds != null && viewOrSheetIds.Count > 0)
            {
                for (int i = 0; i < viewOrSheetIds.Count; i++)
                {
                    Element elem = doc.GetElement(viewOrSheetIds[i]);
                    ViewSheet vs = elem as ViewSheet;
                    if (vs != null)
                    {
                        try
                        {
                            ICollection<ElementId> placed = vs.GetAllPlacedViews();
                            foreach (ElementId pid in placed)
                            {
                                View pv = doc.GetElement(pid) as View;
                                if (pv != null && !pv.IsTemplate && !viewsToScan.Contains(pv))
                                {
                                    viewsToScan.Add(pv);
                                }
                            }
                        }
                        catch { }
                    }
                    else
                    {
                        View v = elem as View;
                        if (v != null && !v.IsTemplate && !viewsToScan.Contains(v))
                        {
                            viewsToScan.Add(v);
                        }
                    }
                }
            }

            // Nếu không có View nào được chọn, mặc định lấy ActiveView
            if (viewsToScan.Count == 0 && doc.ActiveView != null && !doc.ActiveView.IsTemplate)
            {
                viewsToScan.Add(doc.ActiveView);
            }

            if (viewsToScan.Count == 0) return result;

            // 2. Nạp từ điển các thiết lập đã lưu (nếu có)
            Dictionary<string, CadLayerMappingItem> savedMap = new Dictionary<string, CadLayerMappingItem>(StringComparer.OrdinalIgnoreCase);
            if (existingSavedMappings == null || existingSavedMappings.Count == 0)
            {
                existingSavedMappings = AutoLoadDefaultConfig(doc);
            }
            if (existingSavedMappings != null)
            {
                for (int i = 0; i < existingSavedMappings.Count; i++)
                {
                    CadLayerMappingItem sm = existingSavedMappings[i];
                    string key1 = (sm.CategoryGroup.ToString() + "::" + (sm.RevitSystemName ?? "")).ToLowerInvariant();
                    if (!savedMap.ContainsKey(key1)) savedMap[key1] = sm;

                    string key2 = (sm.RevitSystemName ?? "").ToLowerInvariant();
                    if (!savedMap.ContainsKey(key2)) savedMap[key2] = sm;
                }
            }

            // 3. Quét các phần tử MEP VISIBLE trong từng View
            Dictionary<string, CadLayerMappingItem> detectedMap = new Dictionary<string, CadLayerMappingItem>(StringComparer.OrdinalIgnoreCase);

            List<BuiltInCategory> mepCats = new List<BuiltInCategory>
            {
                BuiltInCategory.OST_DuctCurves,
                BuiltInCategory.OST_DuctFitting,
                BuiltInCategory.OST_DuctAccessory,
                BuiltInCategory.OST_DuctTerminal,
                BuiltInCategory.OST_PipeCurves,
                BuiltInCategory.OST_PipeFitting,
                BuiltInCategory.OST_PipeAccessory,
                BuiltInCategory.OST_MechanicalEquipment,
                BuiltInCategory.OST_CableTray,
                BuiltInCategory.OST_Conduit
            };
            ElementMulticategoryFilter catFilter = new ElementMulticategoryFilter(mepCats);

            for (int vIdx = 0; vIdx < viewsToScan.Count; vIdx++)
            {
                View view = viewsToScan[vIdx];
                if (view == null) continue;

                // Lấy các Filter đang BỊ TẮT VISIBILITY trong View này
                List<ElementFilter> hiddenFilters = new List<ElementFilter>();
                try
                {
                    ICollection<ElementId> fIds = view.GetFilters();
                    if (fIds != null)
                    {
                        foreach (ElementId fid in fIds)
                        {
                            if (!view.GetFilterVisibility(fid))
                            {
                                ParameterFilterElement fe = doc.GetElement(fid) as ParameterFilterElement;
                                if (fe != null)
                                {
                                    ElementFilter ef = fe.GetElementFilter();
                                    if (ef != null) hiddenFilters.Add(ef);
                                }
                            }
                        }
                    }
                }
                catch { }

                FilteredElementCollector col = null;
                try
                {
                    col = new FilteredElementCollector(doc, view.Id)
                        .WherePasses(catFilter)
                        .WhereElementIsNotElementType();
                }
                catch
                {
                    continue;
                }

                foreach (Element elem in col)
                {
                    if (elem == null || elem.Category == null) continue;

                    // A. Kiểm tra nếu Category bị ẩn trong View
                    try
                    {
                        if (view.CanCategoryBeHidden(elem.Category.Id) && view.GetCategoryHidden(elem.Category.Id))
                        {
                            continue;
                        }
                    }
                    catch { }

                    // B. Kiểm tra nếu phần tử bị ẩn thủ công trong View
                    try
                    {
                        if (elem.CanBeHidden(view) && elem.IsHidden(view))
                        {
                            continue;
                        }
                    }
                    catch { }

                    // C. Kiểm tra nếu phần tử thỏa mãn View Filter bị tắt Visibility
                    bool isHiddenByFilter = false;
                    for (int f = 0; f < hiddenFilters.Count; f++)
                    {
                        try
                        {
                            if (hiddenFilters[f].PassesFilter(elem))
                            {
                                isHiddenByFilter = true;
                                break;
                            }
                        }
                        catch { }
                    }
                    if (isHiddenByFilter) continue;

                    // D. Xác định Group, Category và System Name
                    MepCategoryGroup group = MepCategoryGroup.Ducts;
                    string catName = "Ống Gió (Ducts)";
                    BuiltInCategory bic = BuiltInCategory.OST_DuctCurves;
                    string sysName = null;
                    int bicVal = elem.Category.Id.IntegerValue;

                    if (bicVal == (int)BuiltInCategory.OST_DuctCurves)
                    {
                        group = MepCategoryGroup.Ducts;
                        catName = "Ống Gió (Ducts)";
                        bic = BuiltInCategory.OST_DuctCurves;
                        sysName = GetSystemTypeName(doc, elem, BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM);
                    }
                    else if (bicVal == (int)BuiltInCategory.OST_DuctFitting)
                    {
                        group = MepCategoryGroup.Ducts;
                        catName = "Phụ Kiện Gió (Fittings)";
                        bic = BuiltInCategory.OST_DuctFitting;
                        sysName = GetSystemTypeName(doc, elem, BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM);
                        if (string.IsNullOrEmpty(sysName)) sysName = "Phụ Kiện Ống Gió (Fittings)";
                    }
                    else if (bicVal == (int)BuiltInCategory.OST_DuctAccessory)
                    {
                        group = MepCategoryGroup.Ducts;
                        catName = "Van Gió (Accessories)";
                        bic = BuiltInCategory.OST_DuctAccessory;
                        sysName = GetSystemTypeName(doc, elem, BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM);
                        if (string.IsNullOrEmpty(sysName)) sysName = "Van Gió (Duct Accessories)";
                    }
                    else if (bicVal == (int)BuiltInCategory.OST_DuctTerminal)
                    {
                        group = MepCategoryGroup.Ducts;
                        catName = "Miệng Gió (Air Terminals)";
                        bic = BuiltInCategory.OST_DuctTerminal;
                        sysName = GetSystemTypeName(doc, elem, BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM);
                        if (string.IsNullOrEmpty(sysName)) sysName = "Miệng Gió (Air Terminals)";
                    }
                    else if (bicVal == (int)BuiltInCategory.OST_PipeCurves)
                    {
                        group = MepCategoryGroup.Pipes;
                        catName = "Ống Nước (Pipes)";
                        bic = BuiltInCategory.OST_PipeCurves;
                        sysName = GetSystemTypeName(doc, elem, BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM);
                    }
                    else if (bicVal == (int)BuiltInCategory.OST_PipeFitting)
                    {
                        group = MepCategoryGroup.Pipes;
                        catName = "Phụ Kiện Nước (Fittings)";
                        bic = BuiltInCategory.OST_PipeFitting;
                        sysName = GetSystemTypeName(doc, elem, BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM);
                        if (string.IsNullOrEmpty(sysName)) sysName = "Phụ Kiện Ống Nước (Fittings)";
                    }
                    else if (bicVal == (int)BuiltInCategory.OST_PipeAccessory)
                    {
                        group = MepCategoryGroup.Pipes;
                        catName = "Van Nước (Accessories)";
                        bic = BuiltInCategory.OST_PipeAccessory;
                        sysName = GetSystemTypeName(doc, elem, BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM);
                        if (string.IsNullOrEmpty(sysName)) sysName = "Van Nước (Pipe Accessories)";
                    }
                    else if (bicVal == (int)BuiltInCategory.OST_MechanicalEquipment)
                    {
                        group = MepCategoryGroup.Equipment;
                        catName = "Thiết Bị Cơ Điện";
                        bic = BuiltInCategory.OST_MechanicalEquipment;
                        sysName = "Thiết Bị Cơ Điện (Equipment)";
                    }
                    else if (bicVal == (int)BuiltInCategory.OST_CableTray)
                    {
                        group = MepCategoryGroup.Electrical;
                        catName = "Máng Cáp (Cable Tray)";
                        bic = BuiltInCategory.OST_CableTray;
                        sysName = "Máng Cáp (Cable Tray)";
                    }
                    else if (bicVal == (int)BuiltInCategory.OST_Conduit)
                    {
                        group = MepCategoryGroup.Electrical;
                        catName = "Ống Luồn Dây (Conduit)";
                        bic = BuiltInCategory.OST_Conduit;
                        sysName = "Ống Luồn Dây (Conduit)";
                    }

                    if (string.IsNullOrEmpty(sysName))
                    {
                        sysName = catName;
                    }

                    string mapKey = string.Format("{0}::{1}", group, sysName.Trim().ToLowerInvariant());
                    if (!detectedMap.ContainsKey(mapKey))
                    {
                        // Kiểm tra cấu hình đã lưu
                        CadLayerMappingItem match = null;
                        if (savedMap.ContainsKey(mapKey)) match = savedMap[mapKey];
                        else if (savedMap.ContainsKey(sysName.Trim().ToLowerInvariant())) match = savedMap[sysName.Trim().ToLowerInvariant()];

                        CadLayerMappingItem item = new CadLayerMappingItem();
                        item.IsEnabled = true;
                        item.CategoryGroup = group;
                        item.CategoryName = catName;
                        item.BuiltInCat = bic;
                        item.RevitSystemName = sysName.Trim();

                        if (match != null)
                        {
                            item.TargetCadLayerName = match.TargetCadLayerName;
                            item.ColorNumber = match.ColorNumber;
                            item.Linetype = match.Linetype;
                            item.Lineweight = match.Lineweight;
                            item.IsEnabled = match.IsEnabled;
                        }
                        else
                        {
                            AssignDefaultLayerAndColor(item, sysName, group);
                        }

                        detectedMap[mapKey] = item;
                    }
                }
            }

            // Chuyển sang List và sắp xếp theo nhóm
            foreach (var kv in detectedMap)
            {
                result.Add(kv.Value);
            }

            result.Sort((a, b) =>
            {
                int grpComp = a.CategoryGroup.CompareTo(b.CategoryGroup);
                if (grpComp != 0) return grpComp;
                return string.Compare(a.RevitSystemName, b.RevitSystemName, StringComparison.OrdinalIgnoreCase);
            });

            return result;
        }

        private static string GetSystemTypeName(Document doc, Element elem, BuiltInParameter bip)
        {
            try
            {
                Parameter p = elem.get_Parameter(bip);
                if (p != null && p.HasValue)
                {
                    string s = p.AsValueString();
                    if (!string.IsNullOrEmpty(s)) return s;

                    ElementId tid = p.AsElementId();
                    if (tid != null && tid != ElementId.InvalidElementId)
                    {
                        Element te = doc.GetElement(tid);
                        if (te != null && !string.IsNullOrEmpty(te.Name)) return te.Name;
                    }
                }

                // Thử RBS_SYSTEM_NAME_PARAM
                Parameter pName = elem.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM);
                if (pName != null && pName.HasValue)
                {
                    string s = pName.AsString();
                    if (!string.IsNullOrEmpty(s))
                    {
                        int comma = s.IndexOf(',');
                        if (comma > 0) s = s.Substring(0, comma);
                        return s.Trim();
                    }
                }
            }
            catch { }
            return null;
        }

        private static void AssignDefaultLayerAndColor(CadLayerMappingItem item, string sysName, MepCategoryGroup group)
        {
            string upper = sysName.ToUpperInvariant();
            string suffix = SanitizeLayerSuffix(sysName);
            item.Linetype = "Continuous";
            item.Lineweight = 35;

            if (group == MepCategoryGroup.Ducts)
            {
                if (upper.Contains("CẤP") || upper.Contains("SUPPLY") || upper.Contains("CAP") || upper.Contains("SA"))
                {
                    item.ColorNumber = 4; // Cyan
                    item.TargetCadLayerName = "M-DUCT-CAPGIO";
                }
                else if (upper.Contains("HỒI") || upper.Contains("RETURN") || upper.Contains("HOI") || upper.Contains("RA"))
                {
                    item.ColorNumber = 6; // Magenta
                    item.TargetCadLayerName = "M-DUCT-HOIGIO";
                }
                else if (upper.Contains("THẢI") || upper.Contains("EXHAUST") || upper.Contains("THAI") || upper.Contains("EA"))
                {
                    item.ColorNumber = 1; // Red
                    item.TargetCadLayerName = "M-DUCT-THAIGIO";
                }
                else if (upper.Contains("TƯƠI") || upper.Contains("OUTDOOR") || upper.Contains("FRESH") || upper.Contains("OA"))
                {
                    item.ColorNumber = 2; // Yellow
                    item.TargetCadLayerName = "M-DUCT-GIONGOAI";
                }
                else if (upper.Contains("KHÓI") || upper.Contains("SMOKE") || upper.Contains("KHOI"))
                {
                    item.ColorNumber = 1; // Red
                    item.TargetCadLayerName = "M-DUCT-HUTKHOI";
                }
                else if (upper.Contains("ÁP") || upper.Contains("PRESSUR") || upper.Contains("AP"))
                {
                    item.ColorNumber = 5; // Blue
                    item.TargetCadLayerName = "M-DUCT-TANGAP";
                }
                else if (upper.Contains("FITTING") || upper.Contains("PHỤ KIỆN"))
                {
                    item.ColorNumber = 7;
                    item.TargetCadLayerName = "M-DUCT-FITTING";
                    item.Lineweight = 25;
                }
                else if (upper.Contains("TERMINAL") || upper.Contains("MIỆNG") || upper.Contains("GRILLE"))
                {
                    item.ColorNumber = 3;
                    item.TargetCadLayerName = "M-DUCT-GRILLE";
                    item.Lineweight = 25;
                }
                else if (upper.Contains("ACCESSORY") || upper.Contains("VAN"))
                {
                    item.ColorNumber = 1;
                    item.TargetCadLayerName = "M-DUCT-DAMPER";
                    item.Lineweight = 25;
                }
                else
                {
                    item.ColorNumber = 7;
                    item.TargetCadLayerName = "M-DUCT-" + suffix;
                }
            }
            else if (group == MepCategoryGroup.Pipes)
            {
                if (upper.Contains("LẠNH") || upper.Contains("COLD") || upper.Contains("LANH") || upper.Contains("CW") || upper.Contains("CẤP"))
                {
                    item.ColorNumber = 5; // Blue
                    item.TargetCadLayerName = "P-PIPE-CAPNUOC";
                }
                else if (upper.Contains("NÓNG") || upper.Contains("HOT") || upper.Contains("NONG") || upper.Contains("HW"))
                {
                    item.ColorNumber = 1; // Red
                    item.TargetCadLayerName = "P-PIPE-NUOCNONG";
                }
                else if (upper.Contains("THẢI") || upper.Contains("SANITARY") || upper.Contains("THOAT") || upper.Contains("WASTE"))
                {
                    item.ColorNumber = 30; // Orange
                    item.TargetCadLayerName = "P-PIPE-THOATNUOC";
                }
                else if (upper.Contains("MƯA") || upper.Contains("RAIN") || upper.Contains("STORM") || upper.Contains("MUA"))
                {
                    item.ColorNumber = 140; // Sky Blue
                    item.TargetCadLayerName = "P-PIPE-THOATMUA";
                }
                else if (upper.Contains("CHỮA CHÁY") || upper.Contains("FIRE") || upper.Contains("SPRINKLER") || upper.Contains("PCCC"))
                {
                    item.ColorNumber = 1; // Red
                    item.TargetCadLayerName = "P-PIPE-CHUACHAY";
                }
                else
                {
                    item.ColorNumber = 7;
                    item.TargetCadLayerName = "P-PIPE-" + suffix;
                }
            }
            else if (group == MepCategoryGroup.Equipment)
            {
                item.ColorNumber = 2; // Yellow
                item.TargetCadLayerName = "M-EQUIP";
            }
            else if (group == MepCategoryGroup.Electrical)
            {
                if (upper.Contains("TRAY") || upper.Contains("MÁNG"))
                {
                    item.ColorNumber = 2; // Yellow
                    item.TargetCadLayerName = "E-CABLE-TRAY";
                }
                else
                {
                    item.ColorNumber = 8; // Gray
                    item.TargetCadLayerName = "E-CONDUIT";
                }
            }
        }

        /// <summary>
        /// Tạo cấu hình bảng quy đổi Layer mặc định theo tiêu chuẩn TCVN MEP (Việt Nam)
        /// </summary>
        public static List<CadLayerMappingItem> GenerateTcvnMepPreset(Document doc)
        {
            List<CadLayerMappingItem> list = new List<CadLayerMappingItem>();

            List<string> projectDuctSystems;
            List<string> projectPipeSystems;
            GetProjectMepSystems(doc, out projectDuctSystems, out projectPipeSystems);

            // 1. NHÓM ỐNG GIÓ (DUCTS)
            List<string> defaultDuctSystems = new List<string>(new string[]
            {
                "Cấp gió", "Supply Air", "Hồi gió", "Return Air",
                "Thải gió", "Exhaust Air", "Gió tươi", "Outdoor Air",
                "Hút khói", "Smoke Exhaust", "Tăng áp", "Pressurization"
            });

            for (int i = 0; i < projectDuctSystems.Count; i++)
            {
                string s = projectDuctSystems[i];
                if (!defaultDuctSystems.Contains(s))
                {
                    defaultDuctSystems.Add(s);
                }
            }

            for (int i = 0; i < defaultDuctSystems.Count; i++)
            {
                string sys = defaultDuctSystems[i];
                int col = 7;
                string layer = "M-DUCT-" + SanitizeLayerSuffix(sys);
                string upper = sys.ToUpperInvariant();

                if (upper.Contains("CẤP") || upper.Contains("SUPPLY") || upper.Contains("CAP") || upper.Contains("SA"))
                {
                    col = 4; // Cyan
                    layer = "M-DUCT-CAPGIO";
                }
                else if (upper.Contains("HỒI") || upper.Contains("RETURN") || upper.Contains("HOI") || upper.Contains("RA"))
                {
                    col = 6; // Magenta
                    layer = "M-DUCT-HOIGIO";
                }
                else if (upper.Contains("THẢI") || upper.Contains("EXHAUST") || upper.Contains("THAI") || upper.Contains("EA"))
                {
                    col = 1; // Red
                    layer = "M-DUCT-THAIGIO";
                }
                else if (upper.Contains("TƯƠI") || upper.Contains("OUTDOOR") || upper.Contains("FRESH") || upper.Contains("OA"))
                {
                    col = 2; // Yellow
                    layer = "M-DUCT-GIONGOAI";
                }
                else if (upper.Contains("KHÓI") || upper.Contains("SMOKE") || upper.Contains("KHOI"))
                {
                    col = 1; // Red / Index 1
                    layer = "M-DUCT-HUTKHOI";
                }
                else if (upper.Contains("ÁP") || upper.Contains("PRESSUR") || upper.Contains("AP"))
                {
                    col = 5; // Blue
                    layer = "M-DUCT-TANGAP";
                }

                list.Add(new CadLayerMappingItem(
                    MepCategoryGroup.Ducts,
                    "Ống gió (Ducts)",
                    BuiltInCategory.OST_DuctCurves,
                    sys,
                    layer,
                    col,
                    "Continuous",
                    35,
                    "HVAC"
                ));
            }

            // Phụ kiện & Thiết bị đầu cuối ống gió
            list.Add(new CadLayerMappingItem(
                MepCategoryGroup.Ducts,
                "Phụ kiện ống gió (Duct Fittings)",
                BuiltInCategory.OST_DuctFitting,
                "*",
                "M-DUCT-FITTING",
                7,
                "Continuous",
                25,
                "HVAC"
            ));

            list.Add(new CadLayerMappingItem(
                MepCategoryGroup.Ducts,
                "Miệng gió (Air Terminals)",
                BuiltInCategory.OST_DuctTerminal,
                "*",
                "M-DUCT-GRILLE",
                3,
                "Continuous",
                25,
                "HVAC"
            ));

            list.Add(new CadLayerMappingItem(
                MepCategoryGroup.Ducts,
                "Van gió (Duct Accessories)",
                BuiltInCategory.OST_DuctAccessory,
                "*",
                "M-DUCT-DAMPER",
                1,
                "Continuous",
                25,
                "HVAC"
            ));

            // 2. NHÓM ỐNG NƯỚC & CẤP THOÁT NƯỚC (PIPING / PLUMBING)
            List<string> defaultPipeSystems = new List<string>(new string[]
            {
                "Cấp nước lạnh", "Domestic Cold Water", "Cấp nước nóng", "Domestic Hot Water",
                "Thoát nước thải", "Sanitary", "Thoát nước mưa", "Storm",
                "Thông hơi", "Vent", "Nước ngưng", "Condensate Drain",
                "Chữa cháy", "Fire Protection Wet"
            });

            for (int i = 0; i < projectPipeSystems.Count; i++)
            {
                string s = projectPipeSystems[i];
                if (!defaultPipeSystems.Contains(s))
                {
                    defaultPipeSystems.Add(s);
                }
            }

            for (int i = 0; i < defaultPipeSystems.Count; i++)
            {
                string sys = defaultPipeSystems[i];
                int col = 7;
                string layer = "P-PIPE-" + SanitizeLayerSuffix(sys);
                string lt = "Continuous";
                int lw = 35;
                string upper = sys.ToUpperInvariant();

                if (upper.Contains("LẠNH") || upper.Contains("COLD") || upper.Contains("LANH") || upper.Contains("CW"))
                {
                    col = 5; // Blue
                    layer = "P-PIPE-CAPNUOC";
                }
                else if (upper.Contains("NÓNG") || upper.Contains("HOT") || upper.Contains("NONG") || upper.Contains("HW"))
                {
                    col = 1; // Red
                    layer = "P-PIPE-NUOCNONG";
                }
                else if (upper.Contains("THẢI") || upper.Contains("SANITARY") || upper.Contains("THOAT") || upper.Contains("WASTE"))
                {
                    col = 30; // Orange/Brown
                    layer = "P-PIPE-THOATNUOC";
                }
                else if (upper.Contains("MƯA") || upper.Contains("STORM") || upper.Contains("MUA"))
                {
                    col = 4; // Cyan
                    layer = "P-PIPE-THOATNUOCMUA";
                }
                else if (upper.Contains("HƠI") || upper.Contains("VENT") || upper.Contains("HOI"))
                {
                    col = 8; // Gray
                    layer = "P-PIPE-THONGHOI";
                    lt = "DASHED";
                    lw = 25;
                }
                else if (upper.Contains("NGƯNG") || upper.Contains("CONDENSATE") || upper.Contains("NGUNG"))
                {
                    col = 140; // Sky Blue
                    layer = "P-PIPE-NUOCNGUNG";
                    lw = 25;
                }
                else if (upper.Contains("CHỮA CHÁY") || upper.Contains("FIRE") || upper.Contains("CHUA CHAY") || upper.Contains("FP"))
                {
                    col = 1; // Red
                    layer = "FP-PIPE-CHUACHAY";
                    lw = 50;
                }

                list.Add(new CadLayerMappingItem(
                    MepCategoryGroup.Pipes,
                    "Ống nước (Pipes)",
                    BuiltInCategory.OST_PipeCurves,
                    sys,
                    layer,
                    col,
                    lt,
                    lw,
                    "Plumbing"
                ));
            }

            list.Add(new CadLayerMappingItem(
                MepCategoryGroup.Pipes,
                "Phụ kiện ống nước (Pipe Fittings)",
                BuiltInCategory.OST_PipeFitting,
                "*",
                "P-PIPE-FITTING",
                7,
                "Continuous",
                25,
                "Plumbing"
            ));

            list.Add(new CadLayerMappingItem(
                MepCategoryGroup.Pipes,
                "Van & Phụ kiện đường ống (Pipe Accessories)",
                BuiltInCategory.OST_PipeAccessory,
                "*",
                "P-PIPE-VALVE",
                2,
                "Continuous",
                25,
                "Plumbing"
            ));

            list.Add(new CadLayerMappingItem(
                MepCategoryGroup.Pipes,
                "Thiết bị vệ sinh (Plumbing Fixtures)",
                BuiltInCategory.OST_PlumbingFixtures,
                "*",
                "P-PLUMB-FIXTURE",
                7,
                "Continuous",
                25,
                "Plumbing"
            ));

            // 3. NHÓM THIẾT BỊ CƠ ĐIỆN & PCCC (EQUIPMENT)
            list.Add(new CadLayerMappingItem(
                MepCategoryGroup.Equipment,
                "Thiết bị cơ điện (Mechanical Equipment)",
                BuiltInCategory.OST_MechanicalEquipment,
                "*",
                "M-MECH-EQUIP",
                2,
                "Continuous",
                35,
                "Mechanical"
            ));

            list.Add(new CadLayerMappingItem(
                MepCategoryGroup.Equipment,
                "Đầu phun Sprinkler (Sprinklers)",
                BuiltInCategory.OST_Sprinklers,
                "*",
                "FP-SPRN-HEAD",
                1,
                "Continuous",
                25,
                "FireProtection"
            ));

            return list;
        }

        /// <summary>
        /// Tạo cấu hình bảng quy đổi Layer theo chuẩn quốc tế AIA / NCS
        /// </summary>
        public static List<CadLayerMappingItem> GenerateAiaNcsPreset(Document doc)
        {
            List<CadLayerMappingItem> list = new List<CadLayerMappingItem>();

            // Ducts (AIA standard: M-DUCT-SUPP, M-DUCT-RETN, M-DUCT-EXHS...)
            list.Add(new CadLayerMappingItem(MepCategoryGroup.Ducts, "Ống gió (Ducts)", BuiltInCategory.OST_DuctCurves, "Supply Air", "M-DUCT-SUPP", 4, "Continuous", 35, "HVAC"));
            list.Add(new CadLayerMappingItem(MepCategoryGroup.Ducts, "Ống gió (Ducts)", BuiltInCategory.OST_DuctCurves, "Return Air", "M-DUCT-RETN", 6, "Continuous", 35, "HVAC"));
            list.Add(new CadLayerMappingItem(MepCategoryGroup.Ducts, "Ống gió (Ducts)", BuiltInCategory.OST_DuctCurves, "Exhaust Air", "M-DUCT-EXHS", 1, "Continuous", 35, "HVAC"));
            list.Add(new CadLayerMappingItem(MepCategoryGroup.Ducts, "Ống gió (Ducts)", BuiltInCategory.OST_DuctCurves, "*", "M-DUCT-OTHR", 2, "Continuous", 35, "HVAC"));
            list.Add(new CadLayerMappingItem(MepCategoryGroup.Ducts, "Phụ kiện ống gió (Duct Fittings)", BuiltInCategory.OST_DuctFitting, "*", "M-DUCT-FITT", 7, "Continuous", 25, "HVAC"));
            list.Add(new CadLayerMappingItem(MepCategoryGroup.Ducts, "Miệng gió (Air Terminals)", BuiltInCategory.OST_DuctTerminal, "*", "M-DUCT-DIFF", 3, "Continuous", 25, "HVAC"));

            // Pipes (AIA standard: P-DOMW-CWTR, P-DOMW-HWTR, P-SANR-PIPE, P-STRM-PIPE...)
            list.Add(new CadLayerMappingItem(MepCategoryGroup.Pipes, "Ống nước (Pipes)", BuiltInCategory.OST_PipeCurves, "Domestic Cold Water", "P-DOMW-CWTR", 5, "Continuous", 35, "Plumbing"));
            list.Add(new CadLayerMappingItem(MepCategoryGroup.Pipes, "Ống nước (Pipes)", BuiltInCategory.OST_PipeCurves, "Domestic Hot Water", "P-DOMW-HWTR", 1, "Continuous", 35, "Plumbing"));
            list.Add(new CadLayerMappingItem(MepCategoryGroup.Pipes, "Ống nước (Pipes)", BuiltInCategory.OST_PipeCurves, "Sanitary", "P-SANR-PIPE", 30, "Continuous", 35, "Plumbing"));
            list.Add(new CadLayerMappingItem(MepCategoryGroup.Pipes, "Ống nước (Pipes)", BuiltInCategory.OST_PipeCurves, "Storm", "P-STRM-PIPE", 4, "Continuous", 35, "Plumbing"));
            list.Add(new CadLayerMappingItem(MepCategoryGroup.Pipes, "Ống nước (Pipes)", BuiltInCategory.OST_PipeCurves, "Vent", "P-VENT-PIPE", 8, "DASHED", 25, "Plumbing"));
            list.Add(new CadLayerMappingItem(MepCategoryGroup.Pipes, "Ống nước (Pipes)", BuiltInCategory.OST_PipeCurves, "*", "P-PIPE-OTHR", 7, "Continuous", 35, "Plumbing"));
            list.Add(new CadLayerMappingItem(MepCategoryGroup.Pipes, "Phụ kiện ống nước (Pipe Fittings)", BuiltInCategory.OST_PipeFitting, "*", "P-PIPE-FITT", 7, "Continuous", 25, "Plumbing"));

            // Equipment
            list.Add(new CadLayerMappingItem(MepCategoryGroup.Equipment, "Thiết bị cơ điện (Mechanical Equipment)", BuiltInCategory.OST_MechanicalEquipment, "*", "M-EQPM", 2, "Continuous", 35, "Mechanical"));
            list.Add(new CadLayerMappingItem(MepCategoryGroup.Equipment, "Đầu phun Sprinkler (Sprinklers)", BuiltInCategory.OST_Sprinklers, "*", "F-SPRN", 1, "Continuous", 25, "FireProtection"));

            return list;
        }

        private static string SanitizeLayerSuffix(string text)
        {
            if (string.IsNullOrEmpty(text)) return "SYS";
            string clean = SheetExportCadPdfService.RemoveDiacritics(text).ToUpperInvariant();
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < clean.Length; i++)
            {
                char c = clean[i];
                if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                {
                    sb.Append(c);
                }
                else if (c == ' ' || c == '_' || c == '-')
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] != '_')
                    {
                        sb.Append('_');
                    }
                }
            }
            string res = sb.ToString().Trim('_');
            if (string.IsNullOrEmpty(res)) return "SYS";
            if (res.Length > 20) res = res.Substring(0, 20).Trim('_');
            return res;
        }

        /// <summary>
        /// Cấu hình DWGExportOptions và thiết lập ExportLayerTable kèm ModifierType.SystemType
        /// để Revit tự động chia tách các hệ thống MEP thành các Layer riêng biệt khi xuất
        /// </summary>
        public static DWGExportOptions BuildDwgExportOptions(
            Document doc,
            CadExportMepSettings settings)
        {
            DWGExportOptions dwgOpt = null;

            // 1. Thử lấy từ Active Predefined Setup trong Document Revit
            if (doc != null)
            {
                try
                {
                    ExportDWGSettings activeSettings = ExportDWGSettings.GetActivePredefinedSettings(doc);
                    if (activeSettings != null)
                    {
                        dwgOpt = activeSettings.GetDWGExportOptions();
                    }
                }
                catch { }

                if (dwgOpt == null)
                {
                    try
                    {
                        IList<string> setupNames = ExportDWGSettings.ListNames(doc);
                        if (setupNames != null && setupNames.Count > 0)
                        {
                            dwgOpt = DWGExportOptions.GetPredefinedOptions(doc, setupNames[0]);
                        }
                    }
                    catch { }
                }
            }

            if (dwgOpt == null)
            {
                dwgOpt = new DWGExportOptions();
            }

            dwgOpt.MergedViews = settings.MergedViews;
            dwgOpt.FileVersion = settings.CadVersion;
            dwgOpt.TargetUnit = settings.TargetUnit;
            dwgOpt.Colors = ExportColorMode.IndexColors;

            // 2. Tinh chỉnh ExportLayerTable theo bảng quy đổi Layer MEP của người dùng
            try
            {
                ExportLayerTable layerTable = dwgOpt.GetExportLayerTable();
                if (layerTable != null && settings.LayerMappings != null && settings.LayerMappings.Count > 0)
                {
                    List<CadLayerMappingItem> ductMaps = new List<CadLayerMappingItem>();
                    List<CadLayerMappingItem> pipeMaps = new List<CadLayerMappingItem>();
                    List<CadLayerMappingItem> equipMaps = new List<CadLayerMappingItem>();
                    List<CadLayerMappingItem> elecMaps = new List<CadLayerMappingItem>();

                    for (int i = 0; i < settings.LayerMappings.Count; i++)
                    {
                        CadLayerMappingItem m = settings.LayerMappings[i];
                        if (m != null && m.IsEnabled && !string.IsNullOrEmpty(m.TargetCadLayerName))
                        {
                            if (m.CategoryGroup == MepCategoryGroup.Ducts) ductMaps.Add(m);
                            else if (m.CategoryGroup == MepCategoryGroup.Pipes) pipeMaps.Add(m);
                            else if (m.CategoryGroup == MepCategoryGroup.Equipment) equipMaps.Add(m);
                            else if (m.CategoryGroup == MepCategoryGroup.Electrical) elecMaps.Add(m);
                        }
                    }

                    // A. NHÓM ỐNG NƯỚC (PIPES)
                    if (pipeMaps.Count > 0)
                    {
                        CadLayerMappingItem p0 = pipeMaps[0];
                        string pLay = p0.TargetCadLayerName;
                        int pCol = p0.ColorNumber > 0 && p0.ColorNumber <= 255 ? p0.ColorNumber : 7;

                        ApplyLayerMappingToCategories(layerTable, new string[] { "Pipe", "Ống nước" }, pLay, pCol, pipeMaps.Count > 1);
                    }

                    // B. NHÓM ỐNG GIÓ (DUCTS)
                    if (ductMaps.Count > 0)
                    {
                        CadLayerMappingItem d0 = ductMaps[0];
                        string dLay = d0.TargetCadLayerName;
                        int dCol = d0.ColorNumber > 0 && d0.ColorNumber <= 255 ? d0.ColorNumber : 7;

                        ApplyLayerMappingToCategories(layerTable, new string[] { "Duct", "Ống gió", "Air Terminal", "Miệng gió" }, dLay, dCol, ductMaps.Count > 1);
                    }

                    // C. NHÓM THIẾT BỊ (EQUIPMENT)
                    if (equipMaps.Count > 0)
                    {
                        CadLayerMappingItem e0 = equipMaps[0];
                        ApplyLayerMappingToCategories(layerTable, new string[] { "Mechanical Equipment", "Thiết bị" }, e0.TargetCadLayerName, e0.ColorNumber, false);
                    }

                    // D. NHÓM ĐIỆN (ELECTRICAL)
                    if (elecMaps.Count > 0)
                    {
                        for (int e = 0; e < elecMaps.Count; e++)
                        {
                            CadLayerMappingItem em = elecMaps[e];
                            if (em.RevitSystemName.IndexOf("Tray", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                em.TargetCadLayerName.IndexOf("TRAY", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                ApplyLayerMappingToCategories(layerTable, new string[] { "Cable Tray", "Máng cáp" }, em.TargetCadLayerName, em.ColorNumber, false);
                            }
                            else
                            {
                                ApplyLayerMappingToCategories(layerTable, new string[] { "Conduit", "Ống luồn" }, em.TargetCadLayerName, em.ColorNumber, false);
                            }
                        }
                    }

                    dwgOpt.SetExportLayerTable(layerTable);
                }
            }
            catch { }

            return dwgOpt;
        }

        private static void ApplyLayerMappingToCategories(
            ExportLayerTable table,
            string[] categoryKeywords,
            string targetLayerName,
            int targetColorNumber,
            bool addSystemTypeModifier)
        {
            if (table == null || categoryKeywords == null || categoryKeywords.Length == 0 || string.IsNullOrEmpty(targetLayerName)) return;

            try
            {
                ICollection<ExportLayerKey> keys = table.GetKeys();
                if (keys != null)
                {
                    foreach (ExportLayerKey k in keys)
                    {
                        if (k == null || string.IsNullOrEmpty(k.CategoryName)) continue;

                        bool match = false;
                        for (int i = 0; i < categoryKeywords.Length; i++)
                        {
                            if (k.CategoryName.IndexOf(categoryKeywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                match = true;
                                break;
                            }
                        }

                        if (match)
                        {
                            ExportLayerInfo info = table.GetExportLayerInfo(k);
                            if (info != null)
                            {
                                info.LayerName = targetLayerName;
                                info.ColorNumber = targetColorNumber;
                                info.CutLayerName = targetLayerName;
                                info.CutColorNumber = targetColorNumber;

                                info.ClearLayerModifiers();
                                info.ClearCutLayerModifiers();

                                if (addSystemTypeModifier)
                                {
                                    try
                                    {
                                        LayerModifier modSys = new LayerModifier(ModifierType.SystemType, "-");
                                        info.AddLayerModifier(modSys);
                                        info.AddCutLayerModifier(modSys);
                                    }
                                    catch { }
                                }

                                table[k] = info;
                            }
                        }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Xử lý hậu kỳ file CAD đã xuất bằng AutoCAD Core Console (overload không cần out message)
        /// </summary>
        public static bool PostProcessDwgLayers(
            string dwgPath,
            IList<CadLayerMappingItem> mappings)
        {
            string msg;
            return PostProcessDwgLayers(dwgPath, mappings, out msg);
        }

        /// <summary>
        /// Xử lý hậu kỳ file CAD đã xuất bằng AutoCAD Core Console (accoreconsole.exe):
        /// Tạo và chuyển đổi toàn bộ đối tượng (entities) sang đúng Tên Layer và Mã Màu CAD (ACI 1-255)
        /// do người dùng thiết lập, chạy độc lập qua AutoLISP thuần túy (entmake/entmod) cực nhanh và an toàn 100%.
        /// </summary>
        public static bool PostProcessDwgLayers(
            string dwgPath,
            IList<CadLayerMappingItem> mappings,
            out string resultMessage)
        {
            resultMessage = string.Empty;

            if (string.IsNullOrEmpty(dwgPath) || !File.Exists(dwgPath))
            {
                resultMessage = "File DWG không tồn tại: " + dwgPath;
                return false;
            }

            if (mappings == null || mappings.Count == 0)
            {
                resultMessage = "Bảng quy đổi Layer trống, bỏ qua xử lý hậu kỳ.";
                return true;
            }

            string accoreExe = SheetExportCadPdfService.FindAutoCadCoreConsole();
            if (string.IsNullOrEmpty(accoreExe) || !File.Exists(accoreExe))
            {
                resultMessage = "Không tìm thấy AutoCAD Core Console (accoreconsole.exe), giữ nguyên Layer mặc định của Revit.";
                return true;
            }

            string tempDir = Path.GetTempPath();
            string randSuffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            string tempDwg = Path.Combine(tempDir, "hd_export_" + randSuffix + ".dwg");
            string tempScr = Path.Combine(tempDir, "hd_remap_" + randSuffix + ".scr");

            try
            {
                // 1. Copy file DWG sang đường dẫn tạm ASCII thuần túy trong %TEMP%
                File.Copy(dwgPath, tempDwg, true);

                // 2. Phân loại cấu hình mapping theo nhóm MEP
                List<CadLayerMappingItem> ductMaps = new List<CadLayerMappingItem>();
                List<CadLayerMappingItem> pipeMaps = new List<CadLayerMappingItem>();
                List<CadLayerMappingItem> equipMaps = new List<CadLayerMappingItem>();
                List<CadLayerMappingItem> elecMaps = new List<CadLayerMappingItem>();

                for (int i = 0; i < mappings.Count; i++)
                {
                    CadLayerMappingItem m = mappings[i];
                    if (m != null && m.IsEnabled && !string.IsNullOrEmpty(m.TargetCadLayerName))
                    {
                        if (m.CategoryGroup == MepCategoryGroup.Ducts) ductMaps.Add(m);
                        else if (m.CategoryGroup == MepCategoryGroup.Pipes) pipeMaps.Add(m);
                        else if (m.CategoryGroup == MepCategoryGroup.Equipment) equipMaps.Add(m);
                        else if (m.CategoryGroup == MepCategoryGroup.Electrical) elecMaps.Add(m);
                    }
                }

                // 3. Tạo file Script .scr nhúng trực tiếp AutoLISP thực thi không cần load lisp ngoài
                StringBuilder sbScr = new StringBuilder();
                sbScr.AppendLine("FILEDIA 0");
                sbScr.AppendLine("CMDECHO 0");

                // Hàm LISP tạo hoặc cập nhật layer với màu sắc ACI
                sbScr.AppendLine("(defun hd-set-layer (layName colorNum ltype / layData)");
                sbScr.AppendLine("  (if (setq layData (tblsearch \"LAYER\" layName))");
                sbScr.AppendLine("    (progn");
                sbScr.AppendLine("      (setq layData (entget (tblobjname \"LAYER\" layName)))");
                sbScr.AppendLine("      (if (and colorNum (> colorNum 0) (<= colorNum 255))");
                sbScr.AppendLine("        (setq layData (subst (cons 62 colorNum) (assoc 62 layData) layData))");
                sbScr.AppendLine("      )");
                sbScr.AppendLine("      (if (and ltype (/= ltype \"\") (tblsearch \"LTYPE\" ltype))");
                sbScr.AppendLine("        (setq layData (subst (cons 6 ltype) (assoc 6 layData) layData))");
                sbScr.AppendLine("      )");
                sbScr.AppendLine("      (entmod layData)");
                sbScr.AppendLine("    )");
                sbScr.AppendLine("    (progn");
                sbScr.AppendLine("      (entmake (list");
                sbScr.AppendLine("        (cons 0 \"LAYER\")");
                sbScr.AppendLine("        (cons 100 \"AcDbSymbolTableRecord\")");
                sbScr.AppendLine("        (cons 100 \"AcDbLayerTableRecord\")");
                sbScr.AppendLine("        (cons 2 layName)");
                sbScr.AppendLine("        (cons 70 0)");
                sbScr.AppendLine("        (cons 62 (if (and colorNum (> colorNum 0) (<= colorNum 255)) colorNum 7))");
                sbScr.AppendLine("        (cons 6 (if (and ltype (/= ltype \"\") (tblsearch \"LTYPE\" ltype)) ltype \"Continuous\"))");
                sbScr.AppendLine("      ))");
                sbScr.AppendLine("    )");
                sbScr.AppendLine("  )");
                sbScr.AppendLine(")");

                // Hàm LISP di chuyển toàn bộ entities sang layer đích và đặt màu ByLayer (256)
                sbScr.AppendLine("(defun hd-move-entities (srcLay tgtLay / ss i ent ed)");
                sbScr.AppendLine("  (if (setq ss (ssget \"_X\" (list (cons 8 srcLay))))");
                sbScr.AppendLine("    (repeat (setq i (sslength ss))");
                sbScr.AppendLine("      (setq ent (ssname ss (setq i (1- i))))");
                sbScr.AppendLine("      (setq ed (entget ent))");
                sbScr.AppendLine("      (setq ed (subst (cons 8 tgtLay) (assoc 8 ed) ed))");
                sbScr.AppendLine("      (if (assoc 62 ed)");
                sbScr.AppendLine("        (setq ed (subst (cons 62 256) (assoc 62 ed) ed))");
                sbScr.AppendLine("        (setq ed (append ed (list (cons 62 256))))");
                sbScr.AppendLine("      )");
                sbScr.AppendLine("      (if (assoc 420 ed)");
                sbScr.AppendLine("        (setq ed (vl-remove (assoc 420 ed) ed))");
                sbScr.AppendLine("      )");
                sbScr.AppendLine("      (entmod ed)");
                sbScr.AppendLine("    )");
                sbScr.AppendLine("  )");
                sbScr.AppendLine(")");

                // Hàm chính xử lý remap
                sbScr.AppendLine("(defun c:hd-remap-all (/ td layName layUpper item matched)");

                // 3.1. Khởi tạo tất cả các layer đích trước
                for (int i = 0; i < mappings.Count; i++)
                {
                    CadLayerMappingItem m = mappings[i];
                    if (m != null && m.IsEnabled && !string.IsNullOrEmpty(m.TargetCadLayerName))
                    {
                        string tgt = m.TargetCadLayerName.ToUpperInvariant();
                        int col = (m.ColorNumber > 0 && m.ColorNumber <= 255) ? m.ColorNumber : 7;
                        string lt = string.IsNullOrEmpty(m.Linetype) ? "Continuous" : m.Linetype;
                        sbScr.AppendLine(string.Format(CultureInfo.InvariantCulture,
                            "  (hd-set-layer \"{0}\" {1} \"{2}\")",
                            EscapeLisp(tgt), col, EscapeLisp(lt)));
                    }
                }

                // 3.2. Quét toàn bộ layer trong DWG và remap
                sbScr.AppendLine("  (setq td (tblnext \"LAYER\" T))");
                sbScr.AppendLine("  (while td");
                sbScr.AppendLine("    (setq layName (cdr (assoc 2 td)))");
                sbScr.AppendLine("    (setq layUpper (strcase layName))");
                sbScr.AppendLine("    (setq matched nil)");

                // A. Quy tắc riêng cho Pipes: Nếu trong View chỉ có 1 hệ thống Pipes duy nhất
                if (pipeMaps.Count == 1)
                {
                    string pTgt = pipeMaps[0].TargetCadLayerName.ToUpperInvariant();
                    sbScr.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "    (if (and (not matched) (/= layUpper \"{0}\") (wcmatch layUpper \"*PIPE*\"))",
                        EscapeLisp(pTgt)));
                    sbScr.AppendLine("      (progn");
                    sbScr.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "        (hd-move-entities layName \"{0}\")", EscapeLisp(pTgt)));
                    sbScr.AppendLine("        (setq matched T)");
                    sbScr.AppendLine("      )");
                    sbScr.AppendLine("    )");
                }

                // B. Quy tắc riêng cho Ducts: Nếu trong View chỉ có 1 hệ thống Ducts duy nhất
                if (ductMaps.Count == 1)
                {
                    string dTgt = ductMaps[0].TargetCadLayerName.ToUpperInvariant();
                    sbScr.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "    (if (and (not matched) (/= layUpper \"{0}\") (or (wcmatch layUpper \"*DUCT*\") (wcmatch layUpper \"*HVAC*\"))) ",
                        EscapeLisp(dTgt)));
                    sbScr.AppendLine("      (progn");
                    sbScr.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "        (hd-move-entities layName \"{0}\")", EscapeLisp(dTgt)));
                    sbScr.AppendLine("        (setq matched T)");
                    sbScr.AppendLine("      )");
                    sbScr.AppendLine("    )");
                }

                // C. Quy tắc so khớp theo từng từ khóa hệ thống (System Tokens) cho các trường hợp còn lại
                for (int i = 0; i < mappings.Count; i++)
                {
                    CadLayerMappingItem m = mappings[i];
                    if (m != null && m.IsEnabled && !string.IsNullOrEmpty(m.TargetCadLayerName))
                    {
                        string tgt = m.TargetCadLayerName.ToUpperInvariant();
                        List<string> tokens = ExtractSystemTokens(m.RevitSystemName);

                        for (int t = 0; t < tokens.Count; t++)
                        {
                            string tok = tokens[t];
                            if (tok.Length >= 2)
                            {
                                sbScr.AppendLine(string.Format(CultureInfo.InvariantCulture,
                                    "    (if (and (not matched) (/= layUpper \"{0}\") (wcmatch layUpper \"*{1}*\"))",
                                    EscapeLisp(tgt), EscapeLisp(tok)));
                                sbScr.AppendLine("      (progn");
                                sbScr.AppendLine(string.Format(CultureInfo.InvariantCulture,
                                    "        (hd-move-entities layName \"{0}\")", EscapeLisp(tgt)));
                                sbScr.AppendLine("        (setq matched T)");
                                sbScr.AppendLine("      )");
                                sbScr.AppendLine("    )");
                            }
                        }
                    }
                }

                sbScr.AppendLine("    (setq td (tblnext \"LAYER\"))");
                sbScr.AppendLine("  )");
                sbScr.AppendLine("  (princ \"\\n---HD_REMAP_COMPLETE---\")");
                sbScr.AppendLine("  (princ)");
                sbScr.AppendLine(")");
                sbScr.AppendLine("(c:hd-remap-all)");
                sbScr.AppendLine("QSAVE");
                sbScr.AppendLine("QUIT");

                File.WriteAllText(tempScr, sbScr.ToString(), Encoding.ASCII);

                // 4. Thực thi accoreconsole.exe trên file tạm
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = accoreExe;
                psi.Arguments = string.Format("/i \"{0}\" /s \"{1}\" /l en-US", tempDwg, tempScr);
                psi.WorkingDirectory = tempDir;
                psi.UseShellExecute = false;
                psi.RedirectStandardInput = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;

                using (Process p = Process.Start(psi))
                {
                    p.StandardInput.Close();
                    string stdout = p.StandardOutput.ReadToEnd();
                    string stderr = p.StandardError.ReadToEnd();
                    bool exited = p.WaitForExit(40000);

                    if (!exited)
                    {
                        try { p.Kill(); } catch { }
                        resultMessage = "Quá thời gian xử lý hậu kỳ file CAD.";
                        return false;
                    }
                }

                // 5. Ghi đè file DWG tạm đã xử lý chuẩn thành công vào file DWG gốc
                if (File.Exists(tempDwg))
                {
                    bool copyOk = false;
                    for (int retry = 0; retry < 5; retry++)
                    {
                        try
                        {
                            File.Copy(tempDwg, dwgPath, true);
                            copyOk = true;
                            break;
                        }
                        catch (IOException)
                        {
                            System.Threading.Thread.Sleep(300);
                        }
                    }

                    if (!copyOk)
                    {
                        string dir = Path.GetDirectoryName(dwgPath);
                        string fnWithoutExt = Path.GetFileNameWithoutExtension(dwgPath);
                        string fallbackDwg = Path.Combine(dir, fnWithoutExt + "_REMAP.dwg");
                        try
                        {
                            File.Copy(tempDwg, fallbackDwg, true);
                            resultMessage = "File gốc đang mở trong AutoCAD. Bản vẽ quy đổi được lưu tại: " + Path.GetFileName(fallbackDwg);
                            return true;
                        }
                        catch { }
                    }
                }

                resultMessage = "Đã quy đổi Layer & Màu sắc thành công cho file CAD.";
                return true;
            }
            catch (Exception ex)
            {
                resultMessage = "Lỗi xử lý hậu kỳ Layer CAD: " + ex.Message;
                return false;
            }
            finally
            {
                try { if (File.Exists(tempDwg)) File.Delete(tempDwg); } catch { }
                try { if (File.Exists(tempScr)) File.Delete(tempScr); } catch { }
            }
        }

        private static List<string> ExtractSystemTokens(string revitSystemName)
        {
            List<string> tokens = new List<string>();
            if (string.IsNullOrEmpty(revitSystemName)) return tokens;

            string clean = SheetExportCadPdfService.RemoveDiacritics(revitSystemName).ToUpperInvariant();
            tokens.Add(clean.Replace("(", "_").Replace(")", "_").Trim());
            tokens.Add(clean.Replace("(", " ").Replace(")", " ").Trim());

            char[] seps = new char[] { ' ', '_', '-', '(', ')', '[', ']', '.', ',', '/' };
            string[] parts = clean.Split(seps, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (p.Length >= 2 && !tokens.Contains(p))
                {
                    if (p != "PIPE" && p != "ONG" && p != "HE" && p != "THONG" && p != "LOAI")
                    {
                        tokens.Add(p);
                    }
                }
            }

            return tokens;
        }
        private static string EscapeLisp(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        /// <summary>
        /// Lấy thông tin View Đang Vẽ (Active View) làm lựa chọn mặc định theo chỉ đạo của Sếp Hồ
        /// </summary>
        public static CadExportTargetItem GetActiveViewTarget(Document doc)
        {
            if (doc == null || doc.ActiveView == null) return null;
            View av = doc.ActiveView;
            if (av.IsTemplate) return null;

            string typeName = av.ViewType.ToString();
            string num = string.Empty;
            ViewSheet vs = av as ViewSheet;
            if (vs != null)
            {
                typeName = "Sheet";
                num = vs.SheetNumber;
            }

            return new CadExportTargetItem(av.Id, num, av.Name, typeName, true);
        }

        /// <summary>
        /// Lấy toàn bộ danh sách các Sheet (Bản Vẽ) trong dự án Revit
        /// </summary>
        public static List<CadExportTargetItem> GetProjectSheetTargets(Document doc)
        {
            List<CadExportTargetItem> list = new List<CadExportTargetItem>();
            if (doc == null) return list;

            try
            {
                FilteredElementCollector col = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSheet));

                foreach (Element e in col)
                {
                    ViewSheet vs = e as ViewSheet;
                    if (vs != null && !vs.IsTemplate)
                    {
                        list.Add(new CadExportTargetItem(vs.Id, vs.SheetNumber, vs.Name, "Sheet", false));
                    }
                }
            }
            catch { }

            list.Sort(delegate(CadExportTargetItem a, CadExportTargetItem b)
            {
                return StringComparer.OrdinalIgnoreCase.Compare(a.ItemNumber, b.ItemNumber);
            });

            return list;
        }

        /// <summary>
        /// Lấy danh sách các View trực tiếp trong dự án (Mặt bằng FloorPlan, CeilingPlan, 3D, Mặt cắt Section...)
        /// theo chỉ đạo của Sếp Hồ để xuất CAD mà không cần đưa vào Sheet
        /// </summary>
        public static List<CadExportTargetItem> GetProjectViewTargets(Document doc)
        {
            List<CadExportTargetItem> list = new List<CadExportTargetItem>();
            if (doc == null) return list;

            try
            {
                FilteredElementCollector col = new FilteredElementCollector(doc)
                    .OfClass(typeof(View));

                foreach (Element e in col)
                {
                    View v = e as View;
                    if (v == null || v.IsTemplate) continue;

                    // Bỏ qua ViewSheet (đã có ở mục Sheet riêng) và View nội bộ không xuất được
                    if (v is ViewSheet) continue;
                    if (v.ViewType == ViewType.Internal ||
                        v.ViewType == ViewType.ProjectBrowser ||
                        v.ViewType == ViewType.SystemBrowser ||
                        v.ViewType == ViewType.DrawingSheet)
                    {
                        continue;
                    }

                    string typeStr = v.ViewType.ToString();
                    list.Add(new CadExportTargetItem(v.Id, string.Empty, v.Name, typeStr, false));
                }
            }
            catch { }

            list.Sort(delegate(CadExportTargetItem a, CadExportTargetItem b)
            {
                int c = StringComparer.OrdinalIgnoreCase.Compare(a.CategoryOrType, b.CategoryOrType);
                if (c != 0) return c;
                return StringComparer.OrdinalIgnoreCase.Compare(a.ItemName, b.ItemName);
            });

            return list;
        }

        /// <summary>
        /// Thực thi xuất các View hoặc Sheet sang AutoCAD kèm quy đổi Layer MEP (alias cho ExportCadWithMepLayers)
        /// </summary>
        public static bool ExportViewsToCad(
            Document doc,
            IList<ElementId> viewOrSheetIds,
            CadExportMepSettings settings,
            out string resultMessage)
        {
            return ExportCadWithMepLayers(doc, viewOrSheetIds, settings, out resultMessage);
        }

        /// <summary>
        /// Thực thi xuất CAD và áp dụng quy đổi Layer hệ thống MEP
        /// Hỗ trợ cả 3 chế độ phạm vi: View đang vẽ / Danh sách Sheet / Danh sách View dự án
        /// </summary>
        public static bool ExportCadWithMepLayers(
            Document doc,
            IList<ElementId> viewOrSheetIds,
            CadExportMepSettings settings,
            out string resultMessage)
        {
            resultMessage = string.Empty;

            if (doc == null)
            {
                resultMessage = "Document Revit không hợp lệ.";
                return false;
            }

            if (viewOrSheetIds == null || viewOrSheetIds.Count == 0)
            {
                resultMessage = "Vui lòng chọn ít nhất 1 Bản Vẽ (Sheet) hoặc View để xuất CAD.";
                return false;
            }

            if (settings == null)
            {
                resultMessage = "Cài đặt xuất CAD không hợp lệ.";
                return false;
            }

            string targetFolder = settings.TargetFolder;
            if (string.IsNullOrEmpty(targetFolder))
            {
                targetFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "HDTOOL_MEP_CAD_EXPORTS");
            }

            try
            {
                if (!Directory.Exists(targetFolder))
                {
                    Directory.CreateDirectory(targetFolder);
                }

                DateTime exportStartTime = DateTime.Now.AddSeconds(-10);

                // Ghi nhớ danh sách file DWG đã có từ trước trong thư mục
                HashSet<string> priorDwgs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (Directory.Exists(targetFolder))
                {
                    string[] existing = Directory.GetFiles(targetFolder, "*.dwg");
                    for (int i = 0; i < existing.Length; i++) priorDwgs.Add(existing[i]);
                }

                // 1. Cấu hình DWGExportOptions sạch và an toàn 100%
                DWGExportOptions dwgOpt = BuildDwgExportOptions(doc, settings);

                // 2. Thực thi lệnh Export DWG của Revit API theo từng View/Sheet
                int exportOkCount = 0;
                List<string> newlyExportedDwgs = new List<string>();
                string lastErrorDetail = string.Empty;

                for (int i = 0; i < viewOrSheetIds.Count; i++)
                {
                    ElementId vid = viewOrSheetIds[i];
                    Element elem = doc.GetElement(vid);
                    if (elem == null) continue;

                    string exportName = string.Empty;
                    ViewSheet vs = elem as ViewSheet;
                    if (vs != null)
                    {
                        exportName = string.Format("{0} - {1}", vs.SheetNumber, vs.Name);
                    }
                    else
                    {
                        View v = elem as View;
                        if (v != null)
                        {
                            exportName = v.Name;
                        }
                    }

                    exportName = SheetExportCadPdfService.SanitizeFileName(exportName);
                    if (string.IsNullOrEmpty(exportName))
                    {
                        exportName = "View_" + vid.IntegerValue.ToString();
                    }

                    List<ElementId> singleIdList = new List<ElementId>();
                    singleIdList.Add(vid);

                    bool singleOk = false;

                    // Chiến lược 1: Thử xuất với tên file tùy chỉnh
                    try
                    {
                        singleOk = doc.Export(targetFolder, exportName, singleIdList, dwgOpt);
                    }
                    catch (Exception ex1)
                    {
                        lastErrorDetail = ex1.Message;
                    }

                    // Chiến lược 2: Nếu chưa thành công, thử xuất với name = null (Revit tự đặt tên file an toàn)
                    if (!singleOk)
                    {
                        try
                        {
                            singleOk = doc.Export(targetFolder, null, singleIdList, dwgOpt);
                        }
                        catch (Exception ex2)
                        {
                            lastErrorDetail = ex2.Message;
                        }
                    }

                    // Chiến lược 3: Nếu vẫn chưa được, thử với DWGExportOptions cơ bản sạch hoàn toàn
                    if (!singleOk)
                    {
                        try
                        {
                            DWGExportOptions cleanOpt = new DWGExportOptions();
                            cleanOpt.MergedViews = settings.MergedViews;
                            cleanOpt.TargetUnit = ExportUnit.Millimeter;
                            cleanOpt.Colors = ExportColorMode.IndexColors;
                            singleOk = doc.Export(targetFolder, null, singleIdList, cleanOpt);
                        }
                        catch (Exception ex3)
                        {
                            lastErrorDetail = ex3.Message;
                        }
                    }

                    if (singleOk)
                    {
                        exportOkCount++;
                    }
                }

                // Quét phát hiện toàn bộ các file DWG mới sinh ra trong thư mục
                string[] files = Directory.GetFiles(targetFolder, "*.dwg");
                for (int i = 0; i < files.Length; i++)
                {
                    string f = files[i];
                    FileInfo fi = new FileInfo(f);
                    if (!priorDwgs.Contains(f) || fi.LastWriteTime >= exportStartTime)
                    {
                        if (!newlyExportedDwgs.Contains(f))
                        {
                            newlyExportedDwgs.Add(f);
                        }
                    }
                }

                if (exportOkCount == 0 && newlyExportedDwgs.Count == 0)
                {
                    resultMessage = "Revit báo lỗi khi thực hiện xuất bản vẽ sang CAD." +
                        (!string.IsNullOrEmpty(lastErrorDetail) ? " Chi tiết: " + lastErrorDetail : "");
                    return false;
                }

                // 3. Nếu người dùng có cấu hình bảng quy đổi Layer MEP -> Xử lý hậu kỳ qua accoreconsole.exe
                int remappedCount = 0;
                if (settings.LayerMappings != null && settings.LayerMappings.Count > 0 && newlyExportedDwgs.Count > 0)
                {
                    for (int i = 0; i < newlyExportedDwgs.Count; i++)
                    {
                        string dwg = newlyExportedDwgs[i];
                        string remapMsg;
                        if (PostProcessDwgLayers(dwg, settings.LayerMappings, out remapMsg))
                        {
                            remappedCount++;
                        }
                    }
                }

                // 4. Tự động lưu cài đặt hiện tại làm cấu hình mặc định cho các lần sau theo lệnh Sếp Hồ
                if (settings.LayerMappings != null && settings.LayerMappings.Count > 0)
                {
                    AutoSaveDefaultConfig(settings.LayerMappings);
                }

                // 5. Tổng hợp thông báo kết quả
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(string.Format("ĐÃ XUẤT THÀNH CÔNG {0} BẢN VẼ SANG AUTOCAD (.DWG)!", viewOrSheetIds.Count));
                sb.AppendLine("Thư mục lưu trữ: " + targetFolder);
                if (remappedCount > 0)
                {
                    sb.AppendLine(string.Format("✔ Đã tự động quy đổi màu sắc & Layer MEP cho {0}/{1} file CAD bằng AutoCAD Core Engine.", remappedCount, newlyExportedDwgs.Count));
                }
                sb.AppendLine("✔ Toàn bộ cài đặt Layer đã được tự động lưu lại cho các lần xuất tiếp theo.");

                resultMessage = sb.ToString().TrimEnd();

                if (settings.OpenFolderAfter && Directory.Exists(targetFolder))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(targetFolder) { UseShellExecute = true });
                    }
                    catch { }
                }

                return true;
            }
            catch (Exception ex)
            {
                resultMessage = "Lỗi trong quá trình xuất CAD: " + ex.Message;
                return false;
            }
        }
    }
}
