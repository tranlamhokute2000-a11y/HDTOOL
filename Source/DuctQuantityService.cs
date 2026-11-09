using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;

namespace HDTool
{
    /// <summary>
    /// Service xử lý bóc tách khối lượng Ống Gió & Phụ Kiện HVAC trong Revit
    /// Tính diện tích tôn m² chuẩn theo TCVN 5687:2010 / SMACNA,
    /// trích xuất hệ thống, phân loại cút, côn, lượn, van gió VCD/FD, miệng gió
    /// và hỗ trợ dynamic custom project parameter columns theo lệnh Sếp Hồ.
    /// </summary>
    public static class DuctQuantityService
    {
        public const double FeetToMeter = 0.3048;
        public const double SqFeetToSqMeter = 0.09290304;

        /// <summary>
        /// Thu thập và thống kê toàn bộ khối lượng Ống Gió & Phụ Kiện HVAC từ Revit Document
        /// </summary>
        public static List<DuctQuantityItem> CollectDuctItems(
            Document doc,
            Autodesk.Revit.DB.View activeView,
            int scopeOption, // 0 = Khung nhìn hiện hành, 1 = Vùng quét cục bộ, 2 = Toàn bộ dự án
            bool incDuct,
            bool incFitting,
            bool incDamper,
            bool incDiffuser,
            bool incFlex,
            ICollection<ElementId> customElementIds = null,
            string levelFilter = null,
            string systemFilter = null,
            string searchText = null,
            List<string> customParamNames = null,
            bool discreteDucts = false)
        {
            List<DuctQuantityItem> result = new List<DuctQuantityItem>();
            if (doc == null) return result;

            List<Element> elementsToProcess = new List<Element>();

            // 1. Lọc theo phạm vi
            if (scopeOption == 1 && customElementIds != null && customElementIds.Count > 0)
            {
                foreach (ElementId id in customElementIds)
                {
                    Element el = doc.GetElement(id);
                    if (el != null)
                    {
                        List<ElementId> extracted = DuctQuantityCommand.ExtractDuctIdsFromElement(doc, el);
                        if (extracted != null && extracted.Count > 0)
                        {
                            foreach (ElementId subId in extracted)
                            {
                                Element subEl = doc.GetElement(subId);
                                if (subEl != null && !elementsToProcess.Contains(subEl))
                                {
                                    elementsToProcess.Add(subEl);
                                }
                            }
                        }
                        else
                        {
                            if (!elementsToProcess.Contains(el)) elementsToProcess.Add(el);
                        }
                    }
                }
            }
            else if (scopeOption == 0 && activeView != null)
            {
                // Nếu là Sheet: Quét qua tất cả Viewports đặt trên Sheet
                if (activeView is ViewSheet)
                {
                    ViewSheet vs = activeView as ViewSheet;
                    ICollection<ElementId> vpIds = vs.GetAllViewports();
                    foreach (ElementId vpid in vpIds)
                    {
                        Viewport vp = doc.GetElement(vpid) as Viewport;
                        if (vp != null)
                        {
                            Autodesk.Revit.DB.View modelV = doc.GetElement(vp.ViewId) as Autodesk.Revit.DB.View;
                            if (modelV != null && !(modelV is ViewDrafting) && modelV.ViewType != ViewType.Legend)
                            {
                                CollectElementsFromView(doc, modelV, incDuct, incFitting, incDamper, incDiffuser, incFlex, elementsToProcess);
                            }
                        }
                    }
                }
                else
                {
                    CollectElementsFromView(doc, activeView, incDuct, incFitting, incDamper, incDiffuser, incFlex, elementsToProcess);
                }
            }
            else
            {
                // Toàn bộ dự án (All Document)
                CollectElementsFromProject(doc, incDuct, incFitting, incDamper, incDiffuser, incFlex, elementsToProcess);
            }

            // 2. Chuyển đổi và tính toán chi tiết từng Element sang DuctQuantityItem
            List<DuctQuantityItem> rawItems = new List<DuctQuantityItem>();

            foreach (Element elem in elementsToProcess)
            {
                if (elem == null) continue;
                DuctQuantityItem item = ProcessSingleElement(doc, elem, customParamNames);
                if (item != null)
                {
                    rawItems.Add(item);
                }
            }

            // 3. Áp dụng bộ lọc Tầng, Hệ Thống và Tìm Kiếm Text
            IEnumerable<DuctQuantityItem> filtered = rawItems;

            if (!string.IsNullOrEmpty(levelFilter) && levelFilter != "Tất Cả" && levelFilter != "All")
            {
                filtered = filtered.Where(x => string.Equals(x.LocationByLevel, levelFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(systemFilter) && systemFilter != "Tất Cả" && systemFilter != "All")
            {
                filtered = filtered.Where(x => string.Equals(x.SystemType, systemFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(searchText) && searchText != "Tìm tên, size...")
            {
                string kw = searchText.Trim().ToLowerInvariant();
                filtered = filtered.Where(x =>
                    (x.MergedName != null && x.MergedName.ToLowerInvariant().Contains(kw)) ||
                    (x.Size != null && x.Size.ToLowerInvariant().Contains(kw)) ||
                    (x.SystemType != null && x.SystemType.ToLowerInvariant().Contains(kw)) ||
                    (x.LocationByLevel != null && x.LocationByLevel.ToLowerInvariant().Contains(kw)) ||
                    (x.CategoryKind != null && x.CategoryKind.ToLowerInvariant().Contains(kw))
                );
            }

            // 4. Gom nhóm hoặc hiển thị rời rạc
            if (discreteDucts)
            {
                foreach (DuctQuantityItem it in filtered)
                {
                    it.TotalLengthM = it.LengthM;
                    it.TotalAreaM2 = it.AreaM2;
                    result.Add(it);
                }
            }
            else
            {
                // Gom nhóm các phần tử có cùng quy cách, tên, hệ thống, tầng
                Dictionary<string, DuctQuantityItem> grouped = new Dictionary<string, DuctQuantityItem>(StringComparer.OrdinalIgnoreCase);

                foreach (DuctQuantityItem it in filtered)
                {
                    string key = it.GetGroupingKey();
                    if (!grouped.ContainsKey(key))
                    {
                        DuctQuantityItem gItem = new DuctQuantityItem
                        {
                            MergedName = it.MergedName,
                            Size = it.Size,
                            Shape = it.Shape,
                            LengthM = it.LengthM,
                            TotalLengthM = it.LengthM,
                            AreaM2 = it.AreaM2,
                            TotalAreaM2 = it.AreaM2,
                            Count = it.Count,
                            Unit = it.Unit,
                            SystemType = it.SystemType,
                            Thickness = it.Thickness,
                            LocationByLevel = it.LocationByLevel,
                            CategoryKind = it.CategoryKind,
                            ElementIds = new List<ElementId>(it.ElementIds),
                            CustomParameters = new Dictionary<string, string>(it.CustomParameters, StringComparer.OrdinalIgnoreCase)
                        };
                        grouped[key] = gItem;
                    }
                    else
                    {
                        DuctQuantityItem existing = grouped[key];
                        existing.Count += it.Count;
                        existing.TotalLengthM += it.LengthM;
                        existing.TotalAreaM2 += it.AreaM2;
                        existing.ElementIds.AddRange(it.ElementIds);

                        // Giữ lại custom parameter nếu dòng trước chưa có
                        if (it.CustomParameters != null)
                        {
                            foreach (var kvp in it.CustomParameters)
                            {
                                if (!existing.CustomParameters.ContainsKey(kvp.Key) || string.IsNullOrEmpty(existing.CustomParameters[kvp.Key]))
                                {
                                    existing.CustomParameters[kvp.Key] = kvp.Value;
                                }
                            }
                        }
                    }
                }

                foreach (var kvp in grouped)
                {
                    result.Add(kvp.Value);
                }
            }

            return SortDuctQuantityItems(result);
        }

        private static void CollectElementsFromView(Document doc, Autodesk.Revit.DB.View view, bool incDuct, bool incFitting, bool incDamper, bool incDiffuser, bool incFlex, List<Element> targetList)
        {
            if (view == null || doc == null || view.IsTemplate) return;

            int countBefore = targetList.Count;

            if (incDuct)
            {
                var ducts = new FilteredElementCollector(doc, view.Id)
                    .OfCategory(BuiltInCategory.OST_DuctCurves)
                    .WhereElementIsNotElementType()
                    .ToElements();
                targetList.AddRange(ducts);
            }

            if (incFlex)
            {
                var flexDucts = new FilteredElementCollector(doc, view.Id)
                    .OfCategory(BuiltInCategory.OST_FlexDuctCurves)
                    .WhereElementIsNotElementType()
                    .ToElements();
                targetList.AddRange(flexDucts);
            }

            if (incFitting)
            {
                var fittings = new FilteredElementCollector(doc, view.Id)
                    .OfCategory(BuiltInCategory.OST_DuctFitting)
                    .WhereElementIsNotElementType()
                    .ToElements();
                targetList.AddRange(fittings);
            }

            if (incDamper)
            {
                var accessories = new FilteredElementCollector(doc, view.Id)
                    .OfCategory(BuiltInCategory.OST_DuctAccessory)
                    .WhereElementIsNotElementType()
                    .ToElements();
                targetList.AddRange(accessories);
            }

            if (incDiffuser)
            {
                var terminals = new FilteredElementCollector(doc, view.Id)
                    .OfCategory(BuiltInCategory.OST_DuctTerminal)
                    .WhereElementIsNotElementType()
                    .ToElements();
                targetList.AddRange(terminals);
            }

            // NẾU LÀ VIEW 3D MÀ COLLECTOR THEO VIEW TRẢ VỀ 0 PHẦN TỬ (DO BỘ LỌC HOẶC ĐẶC THÙ REVIT VIEW 3D):
            // Fallback siêu an toàn: Quét các phần tử trong document không bị ẩn trong view này
            if (view is View3D && targetList.Count == countBefore)
            {
                try
                {
                    BuiltInCategory[] bics = new BuiltInCategory[]
                    {
                        BuiltInCategory.OST_DuctCurves,
                        BuiltInCategory.OST_FlexDuctCurves,
                        BuiltInCategory.OST_DuctFitting,
                        BuiltInCategory.OST_DuctAccessory,
                        BuiltInCategory.OST_DuctTerminal
                    };

                    foreach (var bic in bics)
                    {
                        if (bic == BuiltInCategory.OST_DuctCurves && !incDuct) continue;
                        if (bic == BuiltInCategory.OST_FlexDuctCurves && !incFlex) continue;
                        if (bic == BuiltInCategory.OST_DuctFitting && !incFitting) continue;
                        if (bic == BuiltInCategory.OST_DuctAccessory && !incDamper) continue;
                        if (bic == BuiltInCategory.OST_DuctTerminal && !incDiffuser) continue;

                        var allInDoc = new FilteredElementCollector(doc)
                            .OfCategory(bic)
                            .WhereElementIsNotElementType()
                            .ToElements();

                        foreach (var el in allInDoc)
                        {
                            try
                            {
                                if (!el.IsHidden(view))
                                {
                                    targetList.Add(el);
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }
        }

        private static void CollectElementsFromProject(Document doc, bool incDuct, bool incFitting, bool incDamper, bool incDiffuser, bool incFlex, List<Element> targetList)
        {
            if (doc == null) return;

            if (incDuct)
            {
                var ducts = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_DuctCurves)
                    .WhereElementIsNotElementType()
                    .ToElements();
                targetList.AddRange(ducts);
            }

            if (incFlex)
            {
                var flexDucts = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_FlexDuctCurves)
                    .WhereElementIsNotElementType()
                    .ToElements();
                targetList.AddRange(flexDucts);
            }

            if (incFitting)
            {
                var fittings = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_DuctFitting)
                    .WhereElementIsNotElementType()
                    .ToElements();
                targetList.AddRange(fittings);
            }

            if (incDamper)
            {
                var accessories = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_DuctAccessory)
                    .WhereElementIsNotElementType()
                    .ToElements();
                targetList.AddRange(accessories);
            }

            if (incDiffuser)
            {
                var terminals = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_DuctTerminal)
                    .WhereElementIsNotElementType()
                    .ToElements();
                targetList.AddRange(terminals);
            }
        }

        /// <summary>
        /// Xử lý 1 phần tử Revit sang DuctQuantityItem với diện tích m² tôn chuẩn TCVN 5687:2010
        /// </summary>
        private static DuctQuantityItem ProcessSingleElement(Document doc, Element elem, List<string> customParamNames)
        {
            if (elem == null || elem.Category == null) return null;

            DuctQuantityItem item = new DuctQuantityItem();
            item.ElementIds.Add(elem.Id);

            Category cat = elem.Category;
            BuiltInCategory bic = (BuiltInCategory)cat.Id.IntegerValue;

            // 1. Tầng (Level)
            item.LocationByLevel = GetElementLevelName(doc, elem);

            // 2. Hệ thống (System Type / Name)
            item.SystemType = GetElementSystemName(elem);

            // 3. Phân loại theo Category
            if (bic == BuiltInCategory.OST_DuctCurves)
            {
                // --- ỐNG GIÓ THẲNG ---
                item.CategoryKind = "Ống Gió Thẳng";
                item.Unit = "đoạn";
                item.Count = 1;

                Duct duct = elem as Duct;
                double widthFt = 0, heightFt = 0, diameterFt = 0;
                double lengthFt = 0;

                Parameter pLen = elem.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
                if (pLen != null && pLen.HasValue) lengthFt = pLen.AsDouble();
                item.LengthM = Math.Round(lengthFt * FeetToMeter, 2);

                Parameter pWidth = elem.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM);
                Parameter pHeight = elem.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);
                Parameter pDiam = elem.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);

                if (pWidth != null && pWidth.HasValue) widthFt = pWidth.AsDouble();
                if (pHeight != null && pHeight.HasValue) heightFt = pHeight.AsDouble();
                if (pDiam != null && pDiam.HasValue) diameterFt = pDiam.AsDouble();

                if (widthFt > 0 && heightFt > 0)
                {
                    // Ống chữ nhật
                    item.Shape = "Chữ Nhật";
                    double wMm = Math.Round(widthFt * 304.8, 0);
                    double hMm = Math.Round(heightFt * 304.8, 0);
                    item.Size = string.Format("{0} x {1}", wMm, hMm);
                    item.MergedName = "Ống Gió Thẳng Chữ Nhật";

                    // Diện tích tôn m² theo TCVN 5687: Chu vi * Chiều dài
                    double perimeterM = 2.0 * ((wMm + hMm) / 1000.0);
                    item.AreaM2 = Math.Round(perimeterM * item.LengthM, 2);

                    // Độ dày tôn theo TCVN 5687:2010
                    double maxSide = Math.Max(wMm, hMm);
                    item.Thickness = CalculateSheetThicknessTCVN(maxSide);
                }
                else if (diameterFt > 0)
                {
                    // Ống tròn
                    item.Shape = "Tròn";
                    double dMm = Math.Round(diameterFt * 304.8, 0);
                    item.Size = string.Format("Ø{0}", dMm);
                    item.MergedName = "Ống Gió Tròn Xoắn";

                    // Diện tích tôn m² theo TCVN 5687: π * D * L
                    double perimeterM = Math.PI * (dMm / 1000.0);
                    item.AreaM2 = Math.Round(perimeterM * item.LengthM, 2);

                    item.Thickness = CalculateSheetThicknessRoundTCVN(dMm);
                }
                else
                {
                    item.Shape = "Chữ Nhật";
                    item.Size = GetParamString(elem, BuiltInParameter.RBS_CALCULATED_SIZE) ?? "Kích Thước Mặc Định";
                    item.MergedName = elem.Name ?? "Ống Gió Thẳng";
                    item.AreaM2 = Math.Round(item.LengthM * 1.5, 2);
                    item.Thickness = "0.75 mm";
                }
            }
            else if (bic == BuiltInCategory.OST_FlexDuctCurves)
            {
                // --- ỐNG GIÓ MỀM ---
                item.CategoryKind = "Ống Gió Mềm";
                item.Unit = "m";
                item.Count = 1;
                item.MergedName = "Ống Gió Mềm (Flex Duct)";

                Parameter pLen = elem.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
                double lengthFt = pLen != null && pLen.HasValue ? pLen.AsDouble() : 0;
                item.LengthM = Math.Round(lengthFt * FeetToMeter, 2);

                Parameter pDiam = elem.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);
                if (pDiam != null && pDiam.HasValue)
                {
                    double dMm = Math.Round(pDiam.AsDouble() * 304.8, 0);
                    item.Size = string.Format("Ø{0}", dMm);
                    item.AreaM2 = Math.Round(Math.PI * (dMm / 1000.0) * item.LengthM, 2);
                }
                else
                {
                    item.Size = "Ø200";
                    item.AreaM2 = Math.Round(item.LengthM * 0.63, 2);
                }
                item.Thickness = "Bảo Ôn 25mm";
            }
            else if (bic == BuiltInCategory.OST_DuctFitting)
            {
                // --- PHỤ KIỆN CÚT / CÔN / TÊ / LƯỢN (CHUYÊN SÂU THEO LỆNH SẾP HỒ) ---
                ProcessDuctFittingDetails(elem, item);
            }
            else if (bic == BuiltInCategory.OST_DuctAccessory)
            {
                // --- VAN GIÓ VCD / FD / NRD ---
                item.CategoryKind = "Van Gió";
                item.Unit = "cái";
                item.Count = 1;
                item.LengthM = 0;
                item.AreaM2 = 0; // Van gió tính theo số lượng cái/chiếc

                FamilyInstance fi = elem as FamilyInstance;
                string famName = fi != null && fi.Symbol != null ? fi.Symbol.Family.Name : elem.Name;
                item.MergedName = StandardizeAccessoryName(famName, elem.Name);
                item.Size = GetFittingSizeString(elem);
                item.Thickness = "-";
            }
            else if (bic == BuiltInCategory.OST_DuctTerminal)
            {
                // --- MIỆNG GIÓ (DIFFUSER / SLOT / LOUVER) ---
                item.CategoryKind = "Miệng Gió";
                item.Unit = "bộ";
                item.Count = 1;
                item.LengthM = 0;
                item.AreaM2 = 0;

                FamilyInstance fi = elem as FamilyInstance;
                string famName = fi != null && fi.Symbol != null ? fi.Symbol.Family.Name : elem.Name;
                item.MergedName = StandardizeTerminalName(famName, elem.Name);
                item.Size = GetFittingSizeString(elem);
                item.Thickness = "Nhôm Sơn Tĩnh Điện";
            }

            // 4. Trích xuất Custom Project Parameters
            if (customParamNames != null && customParamNames.Count > 0)
            {
                foreach (string pName in customParamNames)
                {
                    if (string.IsNullOrEmpty(pName)) continue;
                    string val = GetElementCustomParamValue(elem, pName);
                    item.CustomParameters[pName] = val;
                }
            }

            return item;
        }

        private static string CalculateSheetThicknessTCVN(double maxSideMm)
        {
            if (maxSideMm <= 450) return "0.58 mm";
            if (maxSideMm <= 1000) return "0.75 mm";
            if (maxSideMm <= 2000) return "1.00 mm";
            return "1.20 mm";
        }

        private static string CalculateSheetThicknessRoundTCVN(double diameterMm)
        {
            if (diameterMm <= 400) return "0.58 mm";
            if (diameterMm <= 800) return "0.75 mm";
            if (diameterMm <= 1250) return "1.00 mm";
            return "1.20 mm";
        }

        private static string CalculateFittingThickness(string sizeStr)
        {
            // Trích xuất số lớn nhất trong size
            if (string.IsNullOrEmpty(sizeStr)) return "0.75 mm";
            try
            {
                var nums = System.Text.RegularExpressions.Regex.Matches(sizeStr, "\\d+")
                    .Cast<System.Text.RegularExpressions.Match>()
                    .Select(m => double.Parse(m.Value))
                    .ToList();
                if (nums.Count > 0)
                {
                    double maxVal = nums.Max();
                    return CalculateSheetThicknessTCVN(maxVal);
                }
            }
            catch { }
            return "0.75 mm";
        }

        #region ====== XỬ LÝ PHỤ KIỆN ỐNG GIÓ CHUYÊN SÂU (THEO LỆNH SẾP HỒ) ======

        /// <summary>
        /// Xử lý chi tiết Fitting ống gió: Phân biệt Cút theo góc độ thực tế & R150,
        /// Côn thu trích xuất Chiều dài L (khoảng cách 2 đầu kích thước),
        /// Lượn Z có Chiều dài L và Độ chênh cao Delta H, tính diện tích m² chuẩn TCVN.
        /// </summary>
        private static void ProcessDuctFittingDetails(Element elem, DuctQuantityItem item)
        {
            item.CategoryKind = "Phụ Kiện Ống Gió";
            item.Unit = "cái";
            item.Count = 1;
            item.LengthM = 0;

            FamilyInstance fi = elem as FamilyInstance;
            string famName = fi != null && fi.Symbol != null ? fi.Symbol.Family.Name : elem.Name;
            string typeName = fi != null && fi.Symbol != null ? fi.Symbol.Name : "";
            string combined = (famName + " " + typeName + " " + elem.Name).ToLowerInvariant();

            List<Connector> conns = GetFittingConnectors(elem);

            if (combined.Contains("elbow") || combined.Contains("cút") || combined.Contains("co ") || combined.Contains("bends"))
            {
                // 1. CÚT ỐNG GIÓ (ELBOW)
                double angleDeg = GetFittingAngleDegrees(elem, conns);
                string baseSize = GetFittingCleanBaseSize(elem, conns);
                double ductWidth = GetFirstNumberFromSize(baseSize);
                double radiusMm = GetFittingBendRadiusMm(elem, ductWidth);

                bool hasVane = combined.Contains("vane") || combined.Contains("cánh");
                if (hasVane)
                {
                    item.MergedName = string.Format("Cút {0}° Có Cánh Hướng Dòng (R{1})", angleDeg, radiusMm);
                }
                else
                {
                    item.MergedName = string.Format("Cút {0}° Tôn Mạ Kẽm (R{1})", angleDeg, radiusMm);
                }

                item.Size = string.Format("{0} ({1}°, R{2})", baseSize, angleDeg, radiusMm);
                item.LengthM = 0;
                item.AreaM2 = CalculateElbowAreaM2(elem, angleDeg, radiusMm, baseSize);
                item.Thickness = CalculateFittingThickness(baseSize);
            }
            else if (combined.Contains("transition") || combined.Contains("côn") || combined.Contains("thu") || combined.Contains("reducer"))
            {
                // 2. CÔN THU CHUYỂN TIẾT DIỆN (TRANSITION)
                double lenMm = GetTransitionLengthMm(elem, conns);
                double lenM = Math.Round(lenMm / 1000.0, 2);
                item.LengthM = lenM;

                bool isEccentric = combined.Contains("eccentric") || combined.Contains("lệch");
                item.MergedName = isEccentric ? "Côn Thu Lệch Tâm" : "Côn Thu Chuyển Tiết Diện";

                string baseSize = GetFittingCleanBaseSize(elem, conns);
                item.Size = string.Format("{0} (L={1})", baseSize, lenMm);
                item.AreaM2 = CalculateTransitionAreaM2(elem, lenMm, baseSize);
                item.Thickness = CalculateFittingThickness(baseSize);
            }
            else if (combined.Contains("offset") || combined.Contains("lượn"))
            {
                // 3. LƯỢN ỐNG GIÓ CHỮ Z (OFFSET)
                double lenMm, offsetMm;
                GetOffsetDetails(elem, conns, out lenMm, out offsetMm);
                double lenM = Math.Round(lenMm / 1000.0, 2);
                item.LengthM = lenM;

                item.MergedName = "Lượn Ống Gió Chữ Z";
                string baseSize = GetFittingCleanBaseSize(elem, conns);
                item.Size = string.Format("{0} (L={1}, Chênh H={2})", baseSize, lenMm, offsetMm);
                item.AreaM2 = CalculateOffsetAreaM2(elem, lenMm, offsetMm, baseSize);
                item.Thickness = CalculateFittingThickness(baseSize);
            }
            else if (combined.Contains("tee") || combined.Contains("tê"))
            {
                // 4. TÊ ỐNG GIÓ (TEE)
                item.MergedName = combined.Contains("thu") ? "Tê Thu Ống Gió" : "Tê Đều Ống Gió";
                string baseSize = GetFittingCleanBaseSize(elem, conns);
                item.Size = baseSize;
                item.LengthM = 0;
                item.AreaM2 = CalculateTeeAreaM2(elem, baseSize);
                item.Thickness = CalculateFittingThickness(baseSize);
            }
            else if (combined.Contains("cap") || combined.Contains("bịt") || combined.Contains("end"))
            {
                // 5. NẮP BỊT (END CAP)
                item.MergedName = "Nắp Bịt Đầu Ống Gió (End Cap)";
                string baseSize = GetFittingCleanBaseSize(elem, conns);
                item.Size = baseSize;
                item.LengthM = 0;
                item.AreaM2 = 0.10;
                item.Thickness = CalculateFittingThickness(baseSize);
            }
            else
            {
                // Phụ kiện khác
                item.MergedName = !string.IsNullOrEmpty(famName) ? famName : "Phụ Kiện Ống Gió";
                string baseSize = GetFittingCleanBaseSize(elem, conns);
                item.Size = baseSize;
                item.LengthM = 0;
                item.AreaM2 = 0.25;
                item.Thickness = CalculateFittingThickness(baseSize);
            }
        }

        private static List<Connector> GetFittingConnectors(Element elem)
        {
            List<Connector> conns = new List<Connector>();
            if (elem == null) return conns;

            FamilyInstance fi = elem as FamilyInstance;
            if (fi != null && fi.MEPModel != null && fi.MEPModel.ConnectorManager != null)
            {
                try
                {
                    foreach (Connector c in fi.MEPModel.ConnectorManager.Connectors)
                    {
                        if (c != null && c.ConnectorType == ConnectorType.End)
                        {
                            conns.Add(c);
                        }
                    }
                }
                catch { }
            }
            return conns;
        }

        private static void GetConnectorSize(Connector conn, out double widthMm, out double heightMm, out double diamMm)
        {
            widthMm = 0;
            heightMm = 0;
            diamMm = 0;
            if (conn == null) return;

            try
            {
                if (conn.Shape == ConnectorProfileType.Rectangular)
                {
                    widthMm = Math.Round(conn.Width * 304.8, 0);
                    heightMm = Math.Round(conn.Height * 304.8, 0);
                }
                else if (conn.Shape == ConnectorProfileType.Round)
                {
                    diamMm = Math.Round(conn.Radius * 2.0 * 304.8, 0);
                }
            }
            catch { }
        }

        private static double GetFittingAngleDegrees(Element elem, List<Connector> conns)
        {
            // 1. Thử parameter lookup
            string[] angleParamNames = new string[] { "Angle", "Góc", "Fitting Angle", "Duct Angle", "Angle 1", "Góc Uốn", "Bend Angle" };
            foreach (string name in angleParamNames)
            {
                Parameter p = elem.LookupParameter(name);
                if (p == null)
                {
                    FamilyInstance fi = elem as FamilyInstance;
                    if (fi != null && fi.Symbol != null) p = fi.Symbol.LookupParameter(name);
                }
                if (p != null && p.HasValue && p.StorageType == StorageType.Double)
                {
                    double val = p.AsDouble();
                    double deg = val * 180.0 / Math.PI;
                    if (deg > 180.0) deg = val;
                    if (deg > 5.0 && deg <= 180.0)
                    {
                        return NormalizeAngleDegrees(deg);
                    }
                }
            }

            // 3. Phân tích vector giữa 2 connector
            if (conns != null && conns.Count >= 2)
            {
                try
                {
                    XYZ dir1 = conns[0].CoordinateSystem.BasisZ;
                    XYZ dir2 = conns[1].CoordinateSystem.BasisZ;
                    double dot = Math.Max(-1.0, Math.Min(1.0, dir1.DotProduct(dir2)));
                    double angleDeg = Math.Acos(-dot) * 180.0 / Math.PI;
                    if (angleDeg > 5.0 && angleDeg < 175.0)
                    {
                        return NormalizeAngleDegrees(angleDeg);
                    }
                }
                catch { }
            }

            // 4. Tìm trong Type Name hoặc Family Name
            FamilyInstance inst = elem as FamilyInstance;
            string fullName = (elem.Name + " " + (inst != null && inst.Symbol != null ? inst.Symbol.Family.Name : "")).ToLowerInvariant();
            if (fullName.Contains("45")) return 45.0;
            if (fullName.Contains("60")) return 60.0;
            if (fullName.Contains("30")) return 30.0;
            if (fullName.Contains("90")) return 90.0;

            return 90.0; // Fallback chuẩn
        }

        private static double NormalizeAngleDegrees(double deg)
        {
            if (Math.Abs(deg - 90.0) <= 4.0) return 90.0;
            if (Math.Abs(deg - 45.0) <= 3.0) return 45.0;
            if (Math.Abs(deg - 60.0) <= 3.0) return 60.0;
            if (Math.Abs(deg - 30.0) <= 3.0) return 30.0;
            if (Math.Abs(deg - 22.5) <= 2.0) return 22.5;
            if (Math.Abs(deg - 15.0) <= 2.0) return 15.0;
            return Math.Round(deg, 0);
        }

        private static double GetFittingBendRadiusMm(Element elem, double ductWidthMm)
        {
            string[] radiusParamNames = new string[] 
            { 
                "Bend Radius", "Inside Radius", "Throat Radius", "Radius", 
                "Bán Kính Cong", "Bán Kính Trong", "Bán Kính", "R", "Throat" 
            };

            foreach (string name in radiusParamNames)
            {
                Parameter p = elem.LookupParameter(name);
                if (p == null)
                {
                    FamilyInstance fi = elem as FamilyInstance;
                    if (fi != null && fi.Symbol != null) p = fi.Symbol.LookupParameter(name);
                }
                if (p != null && p.HasValue && p.StorageType == StorageType.Double)
                {
                    double rFt = p.AsDouble();
                    if (rFt > 0.05)
                    {
                        return Math.Round(rFt * 304.8, 0);
                    }
                }
            }

            Parameter pClr = elem.LookupParameter("Centerline Radius") ?? elem.LookupParameter("Center Line Radius");
            if (pClr != null && pClr.HasValue && pClr.StorageType == StorageType.Double)
            {
                double clrMm = pClr.AsDouble() * 304.8;
                double rThroat = clrMm - (ductWidthMm / 2.0);
                if (rThroat > 10) return Math.Round(rThroat, 0);
            }

            // Tiêu chuẩn HVAC SMACNA / TCVN 5687: R150 (khớp ghi chú bản vẽ Sếp gửi)
            return 150.0;
        }

        private static double GetTransitionLengthMm(Element elem, List<Connector> conns)
        {
            if (conns != null && conns.Count >= 2)
            {
                try
                {
                    double distFt = conns[0].Origin.DistanceTo(conns[1].Origin);
                    double distMm = Math.Round(distFt * 304.8, 0);
                    if (distMm >= 50 && distMm <= 3000)
                    {
                        return distMm;
                    }
                }
                catch { }
            }

            string[] lenParamNames = new string[] { "Duct Length", "Length", "Chiều Dài", "L", "Transition Length", "Fitting Length" };
            foreach (string name in lenParamNames)
            {
                Parameter p = elem.LookupParameter(name);
                if (p == null)
                {
                    FamilyInstance fi = elem as FamilyInstance;
                    if (fi != null && fi.Symbol != null) p = fi.Symbol.LookupParameter(name);
                }
                if (p != null && p.HasValue && p.StorageType == StorageType.Double)
                {
                    double valMm = Math.Round(p.AsDouble() * 304.8, 0);
                    if (valMm >= 50) return valMm;
                }
            }

            return 200.0; // Fallback chuẩn 200mm theo hình ảnh thực tế Sếp gửi
        }

        private static void GetOffsetDetails(Element elem, List<Connector> conns, out double lengthMm, out double offsetMm)
        {
            lengthMm = 600.0;
            offsetMm = 200.0;

            string[] offParamNames = new string[] { "Duct Offset", "Offset", "Chênh Cao", "Độ Chênh", "Độ Lệch", "Offset Height", "Offset Distance", "H" };
            foreach (string name in offParamNames)
            {
                Parameter p = elem.LookupParameter(name);
                if (p == null)
                {
                    FamilyInstance fi = elem as FamilyInstance;
                    if (fi != null && fi.Symbol != null) p = fi.Symbol.LookupParameter(name);
                }
                if (p != null && p.HasValue && p.StorageType == StorageType.Double)
                {
                    double valMm = Math.Round(Math.Abs(p.AsDouble() * 304.8), 0);
                    if (valMm > 5) { offsetMm = valMm; break; }
                }
            }

            string[] lenParamNames = new string[] { "Duct Length", "Length", "Chiều Dài", "L", "Offset Length" };
            foreach (string name in lenParamNames)
            {
                Parameter p = elem.LookupParameter(name);
                if (p == null)
                {
                    FamilyInstance fi = elem as FamilyInstance;
                    if (fi != null && fi.Symbol != null) p = fi.Symbol.LookupParameter(name);
                }
                if (p != null && p.HasValue && p.StorageType == StorageType.Double)
                {
                    double valMm = Math.Round(p.AsDouble() * 304.8, 0);
                    if (valMm >= 50) { lengthMm = valMm; break; }
                }
            }

            if (conns != null && conns.Count >= 2)
            {
                try
                {
                    XYZ p1 = conns[0].Origin;
                    XYZ p2 = conns[1].Origin;
                    XYZ diff = p2 - p1;

                    double deltaZMm = Math.Round(Math.Abs(diff.Z) * 304.8, 0);
                    if (deltaZMm > 5)
                    {
                        offsetMm = deltaZMm;
                    }

                    double distTotalMm = Math.Round(p1.DistanceTo(p2) * 304.8, 0);
                    if (distTotalMm > offsetMm)
                    {
                        double axisLenMm = Math.Round(Math.Sqrt(Math.Max(0, distTotalMm * distTotalMm - offsetMm * offsetMm)), 0);
                        if (axisLenMm >= 50) lengthMm = axisLenMm;
                        else lengthMm = distTotalMm;
                    }
                    else if (distTotalMm >= 50)
                    {
                        lengthMm = distTotalMm;
                    }
                }
                catch { }
            }
        }

        private static string GetFittingCleanBaseSize(Element elem, List<Connector> conns)
        {
            if (conns != null && conns.Count >= 2)
            {
                try
                {
                    double w1, h1, d1, w2, h2, d2;
                    GetConnectorSize(conns[0], out w1, out h1, out d1);
                    GetConnectorSize(conns[1], out w2, out h2, out d2);

                    string s1 = d1 > 0 ? string.Format("Ø{0}", d1) : (w1 > 0 && h1 > 0 ? string.Format("{0}x{1}", w1, h1) : "");
                    string s2 = d2 > 0 ? string.Format("Ø{0}", d2) : (w2 > 0 && h2 > 0 ? string.Format("{0}x{1}", w2, h2) : "");

                    if (!string.IsNullOrEmpty(s1) && !string.IsNullOrEmpty(s2))
                    {
                        if (string.Equals(s1, s2, StringComparison.OrdinalIgnoreCase))
                        {
                            return s1;
                        }
                        return string.Format("{0} - {1}", s1, s2);
                    }
                    if (!string.IsNullOrEmpty(s1)) return s1;
                    if (!string.IsNullOrEmpty(s2)) return s2;
                }
                catch { }
            }

            string raw = GetFittingSizeString(elem);
            if (!string.IsNullOrEmpty(raw) && raw != "-")
            {
                string cleaned = raw.Replace("mm", "").Replace("MM", "");
                cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s*([xX*×\-])\s*", "$1");
                return cleaned.Trim();
            }

            return "Kích Thước Mặc Định";
        }

        private static double GetFirstNumberFromSize(string sizeStr)
        {
            if (string.IsNullOrEmpty(sizeStr)) return 250.0;
            var m = System.Text.RegularExpressions.Regex.Match(sizeStr, @"\d+");
            if (m.Success)
            {
                double v;
                if (double.TryParse(m.Value, out v)) return v;
            }
            return 250.0;
        }

        private static double CalculateElbowAreaM2(Element elem, double angleDeg, double radiusMm, string sizeStr)
        {
            Parameter pArea = elem.get_Parameter(BuiltInParameter.RBS_CURVE_SURFACE_AREA);
            if (pArea != null && pArea.HasValue && pArea.AsDouble() > 0)
            {
                return Math.Round(pArea.AsDouble() * SqFeetToSqMeter, 2);
            }

            try
            {
                var nums = System.Text.RegularExpressions.Regex.Matches(sizeStr ?? "", @"\d+")
                    .Cast<System.Text.RegularExpressions.Match>()
                    .Select(m => double.Parse(m.Value))
                    .ToList();

                if (nums.Count >= 2)
                {
                    double w = nums[0] / 1000.0;
                    double h = nums[1] / 1000.0;
                    double r = Math.Max(50.0, radiusMm) / 1000.0;

                    double angleRad = (Math.Max(5.0, angleDeg) * Math.PI) / 180.0;
                    double curveLen = (r + w / 2.0) * angleRad;
                    double perim = 2.0 * (w + h);
                    double area = perim * curveLen + perim * 0.05;
                    return Math.Max(0.05, Math.Round(area, 2));
                }
            }
            catch { }

            return 0.35;
        }

        private static double CalculateTransitionAreaM2(Element elem, double lenMm, string sizeStr)
        {
            Parameter pArea = elem.get_Parameter(BuiltInParameter.RBS_CURVE_SURFACE_AREA);
            if (pArea != null && pArea.HasValue && pArea.AsDouble() > 0)
            {
                return Math.Round(pArea.AsDouble() * SqFeetToSqMeter, 2);
            }

            try
            {
                var nums = System.Text.RegularExpressions.Regex.Matches(sizeStr ?? "", @"\d+")
                    .Cast<System.Text.RegularExpressions.Match>()
                    .Select(m => double.Parse(m.Value))
                    .ToList();

                if (nums.Count >= 2)
                {
                    double w1 = nums[0] / 1000.0;
                    double h1 = nums[1] / 1000.0;
                    double w2 = (nums.Count >= 4) ? nums[2] / 1000.0 : w1;
                    double h2 = (nums.Count >= 4) ? nums[3] / 1000.0 : h1;

                    double p1 = 2.0 * (w1 + h1);
                    double p2 = 2.0 * (w2 + h2);
                    double lM = Math.Max(50.0, lenMm) / 1000.0;

                    double dW = Math.Abs(w1 - w2) / 2.0;
                    double dH = Math.Abs(h1 - h2) / 2.0;
                    double slant = Math.Sqrt(lM * lM + dW * dW + dH * dH);

                    double area = ((p1 + p2) / 2.0) * slant + ((p1 + p2) / 2.0) * 0.05;
                    return Math.Max(0.05, Math.Round(area, 2));
                }
            }
            catch { }

            return 0.30;
        }

        private static double CalculateOffsetAreaM2(Element elem, double lenMm, double offsetMm, string sizeStr)
        {
            Parameter pArea = elem.get_Parameter(BuiltInParameter.RBS_CURVE_SURFACE_AREA);
            if (pArea != null && pArea.HasValue && pArea.AsDouble() > 0)
            {
                return Math.Round(pArea.AsDouble() * SqFeetToSqMeter, 2);
            }

            try
            {
                var nums = System.Text.RegularExpressions.Regex.Matches(sizeStr ?? "", @"\d+")
                    .Cast<System.Text.RegularExpressions.Match>()
                    .Select(m => double.Parse(m.Value))
                    .ToList();

                if (nums.Count >= 2)
                {
                    double w = nums[0] / 1000.0;
                    double h = nums[1] / 1000.0;
                    double perim = 2.0 * (w + h);

                    double lM = Math.Max(50.0, lenMm) / 1000.0;
                    double offM = Math.Max(0.0, offsetMm) / 1000.0;
                    double pathLen = Math.Sqrt(lM * lM + offM * offM);

                    double area = perim * pathLen + perim * 0.05;
                    return Math.Max(0.10, Math.Round(area, 2));
                }
            }
            catch { }

            return 0.45;
        }

        private static double CalculateTeeAreaM2(Element elem, string sizeStr)
        {
            Parameter pArea = elem.get_Parameter(BuiltInParameter.RBS_CURVE_SURFACE_AREA);
            if (pArea != null && pArea.HasValue && pArea.AsDouble() > 0)
            {
                return Math.Round(pArea.AsDouble() * SqFeetToSqMeter, 2);
            }

            try
            {
                var nums = System.Text.RegularExpressions.Regex.Matches(sizeStr ?? "", @"\d+")
                    .Cast<System.Text.RegularExpressions.Match>()
                    .Select(m => double.Parse(m.Value))
                    .ToList();

                if (nums.Count >= 2)
                {
                    double w = nums[0] / 1000.0;
                    double h = nums[1] / 1000.0;
                    double perim = 2.0 * (w + h);
                    return Math.Max(0.15, Math.Round(perim * 0.60, 2));
                }
            }
            catch { }

            return 0.35;
        }

        #endregion

        #region ====== SẮP XẾP BẢNG KHỐI LƯỢNG (THEO LỆNH SẾP HỒ: HẾT ỐNG GIÓ THẲNG TỪ TO ĐẾN NHỎ RỒI MỚI ĐẾN PHỤ KIỆN) ======

        /// <summary>
        /// Sắp xếp bảng khối lượng ống gió chuẩn chỉ theo lệnh Sếp Hồ:
        /// 1. Ống Gió Thẳng (kích thước từ TO ĐẾN NHỎ theo diện tích tiết diện)
        /// 2. Ống Gió Mềm
        /// 3. Phụ Kiện (Cút từ to đến nhỏ, Côn từ to đến nhỏ, Tê, Lượn Z)
        /// 4. Van Gió (VCD, FD, NRD)
        /// 5. Miệng Gió (Diffuser, Slot, Louver)
        /// Đánh lại STT tuần tự 1, 2, 3...
        /// </summary>
        public static List<DuctQuantityItem> SortDuctQuantityItems(IEnumerable<DuctQuantityItem> items)
        {
            if (items == null) return new List<DuctQuantityItem>();

            var sorted = items
                .OrderBy(x => GetCategorySortPriority(x.CategoryKind, x.MergedName))
                .ThenByDescending(x => GetSizeCrossSectionAreaMm2(x.Size))
                .ThenByDescending(x => GetMaxWidthMm(x.Size))
                .ThenByDescending(x => GetMaxHeightMm(x.Size))
                .ThenByDescending(x => x.TotalLengthM)
                .ThenBy(x => x.MergedName)
                .ToList();

            for (int i = 0; i < sorted.Count; i++)
            {
                sorted[i].STT = i + 1;
            }

            return sorted;
        }

        public static int GetCategorySortPriority(string categoryKind, string mergedName)
        {
            string cat = (categoryKind ?? "").Trim().ToLowerInvariant();
            string name = (mergedName ?? "").Trim().ToLowerInvariant();

            // 1. Ống Gió Thẳng luôn đứng đầu bảng
            if (cat.Contains("thẳng") || cat.Contains("duct curves") || (cat.Contains("ống gió") && !cat.Contains("mềm") && !cat.Contains("phụ kiện")))
            {
                return 10;
            }

            // 2. Ống Gió Mềm
            if (cat.Contains("mềm") || cat.Contains("flex"))
            {
                return 20;
            }

            // 3. Phụ Kiện Ống Gió
            if (cat.Contains("phụ kiện") || cat.Contains("fitting"))
            {
                if (name.Contains("cút") || name.Contains("co ") || name.Contains("elbow")) return 31;
                if (name.Contains("côn") || name.Contains("thu") || name.Contains("transition")) return 32;
                if (name.Contains("tê") || name.Contains("tee")) return 33;
                if (name.Contains("lượn") || name.Contains("offset") || name.Contains("chữ z")) return 34;
                return 35;
            }

            // 4. Van Gió
            if (cat.Contains("van") || cat.Contains("accessory") || cat.Contains("vcd") || cat.Contains("fd"))
            {
                return 40;
            }

            // 5. Miệng Gió
            if (cat.Contains("miệng") || cat.Contains("terminal") || cat.Contains("diffuser") || cat.Contains("louver"))
            {
                return 50;
            }

            return 60;
        }

        public static double GetSizeCrossSectionAreaMm2(string sizeStr)
        {
            if (string.IsNullOrEmpty(sizeStr)) return 0;

            try
            {
                var matchRound = System.Text.RegularExpressions.Regex.Match(sizeStr, @"[ØødD]\s*(\d+)");
                if (matchRound.Success)
                {
                    double d = double.Parse(matchRound.Groups[1].Value);
                    return Math.PI * d * d / 4.0;
                }

                var matches = System.Text.RegularExpressions.Regex.Matches(sizeStr, @"(\d+)\s*[xX*×]\s*(\d+)");
                if (matches.Count > 0)
                {
                    double maxArea = 0;
                    foreach (System.Text.RegularExpressions.Match m in matches)
                    {
                        double w = double.Parse(m.Groups[1].Value);
                        double h = double.Parse(m.Groups[2].Value);
                        double a = w * h;
                        if (a > maxArea) maxArea = a;
                    }
                    return maxArea;
                }

                var nums = System.Text.RegularExpressions.Regex.Matches(sizeStr, @"\d+")
                    .Cast<System.Text.RegularExpressions.Match>()
                    .Select(m => double.Parse(m.Value))
                    .ToList();

                if (nums.Count >= 2)
                {
                    return nums[0] * nums[1];
                }
                else if (nums.Count == 1)
                {
                    return nums[0];
                }
            }
            catch { }

            return 0;
        }

        public static double GetMaxWidthMm(string sizeStr)
        {
            if (string.IsNullOrEmpty(sizeStr)) return 0;
            try
            {
                var matches = System.Text.RegularExpressions.Regex.Matches(sizeStr, @"(\d+)\s*[xX*×]\s*(\d+)");
                if (matches.Count > 0)
                {
                    double maxW = 0;
                    foreach (System.Text.RegularExpressions.Match m in matches)
                    {
                        double w = double.Parse(m.Groups[1].Value);
                        if (w > maxW) maxW = w;
                    }
                    return maxW;
                }
            }
            catch { }
            return 0;
        }

        public static double GetMaxHeightMm(string sizeStr)
        {
            if (string.IsNullOrEmpty(sizeStr)) return 0;
            try
            {
                var matches = System.Text.RegularExpressions.Regex.Matches(sizeStr, @"(\d+)\s*[xX*×]\s*(\d+)");
                if (matches.Count > 0)
                {
                    double maxH = 0;
                    foreach (System.Text.RegularExpressions.Match m in matches)
                    {
                        double h = double.Parse(m.Groups[2].Value);
                        if (h > maxH) maxH = h;
                    }
                    return maxH;
                }
            }
            catch { }
            return 0;
        }

        #endregion

        private static string StandardizeAccessoryName(string famName, string elemName)
        {
            string s = (famName + " " + elemName).ToLowerInvariant();
            if (s.Contains("fire") || s.Contains("fd") || s.Contains("cháy")) return "Van Dập Lửa Chống Cháy (FD)";
            if (s.Contains("smoke") || s.Contains("khói")) return "Van Ngăn Khói / Hút Khói (SD/FSD)";
            if (s.Contains("vcd") || s.Contains("volume") || s.Contains("tay gạt")) return "Van Gió Điều Chỉnh Lưu Lượng (VCD)";
            if (s.Contains("nrd") || s.Contains("non-return") || s.Contains("1 chiều")) return "Van Gió 1 Chiều (NRD)";

            return !string.IsNullOrEmpty(famName) ? famName : "Van Gió HVAC";
        }

        private static string StandardizeTerminalName(string famName, string elemName)
        {
            string s = (famName + " " + elemName).ToLowerInvariant();
            if (s.Contains("linear") || s.Contains("slot")) return "Miệng Gió Hồi Khe Nan (Linear Slot)";
            if (s.Contains("louver") || s.Contains("ngoài trời")) return "Cửa Gió Nan Nan Thời Tiết (Louver)";
            if (s.Contains("round") || s.Contains("tròn")) return "Miệng Gió Tròn Khuếch Tán";
            if (s.Contains("diffuser") || s.Contains("khuếch tán") || s.Contains("4 way")) return "Miệng Gió Khuếch Tán 4 Hướng";

            return !string.IsNullOrEmpty(famName) ? famName : "Miệng Gió";
        }

        private static string GetFittingSizeString(Element elem)
        {
            Parameter pSize = elem.get_Parameter(BuiltInParameter.RBS_CALCULATED_SIZE);
            if (pSize != null && pSize.HasValue && !string.IsNullOrWhiteSpace(pSize.AsString()))
            {
                return pSize.AsString().Trim();
            }

            Parameter pSizeCustom = elem.LookupParameter("Size") ?? elem.LookupParameter("Kích Thước") ?? elem.LookupParameter("Duct Size");
            if (pSizeCustom != null && pSizeCustom.HasValue)
            {
                string s = pSizeCustom.AsString();
                if (!string.IsNullOrEmpty(s)) return s.Trim();
            }

            return "-";
        }

        private static string GetElementLevelName(Document doc, Element elem)
        {
            Parameter pLevel = elem.get_Parameter(BuiltInParameter.RBS_START_LEVEL_PARAM) ??
                               elem.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM) ??
                               elem.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM);
            if (pLevel != null && pLevel.HasValue)
            {
                ElementId lid = pLevel.AsElementId();
                if (lid != null && lid != ElementId.InvalidElementId)
                {
                    Element lvl = doc.GetElement(lid);
                    if (lvl != null) return lvl.Name;
                }
                string s = pLevel.AsString();
                if (!string.IsNullOrEmpty(s)) return s;
            }

            Parameter pLvlCustom = elem.LookupParameter("Level") ?? elem.LookupParameter("Tầng") ?? elem.LookupParameter("Cao Độ");
            if (pLvlCustom != null && pLvlCustom.HasValue)
            {
                if (pLvlCustom.StorageType == StorageType.ElementId)
                {
                    Element lvl = doc.GetElement(pLvlCustom.AsElementId());
                    if (lvl != null) return lvl.Name;
                }
                string s = pLvlCustom.AsString();
                if (!string.IsNullOrEmpty(s)) return s;
            }

            return "Tầng Mặc Định";
        }

        private static string GetElementSystemName(Element elem)
        {
            Parameter pSys = elem.get_Parameter(BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM) ??
                             elem.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM) ??
                             elem.get_Parameter(BuiltInParameter.RBS_SYSTEM_CLASSIFICATION_PARAM);
            if (pSys != null && pSys.HasValue)
            {
                if (pSys.StorageType == StorageType.ElementId)
                {
                    Element sysEl = elem.Document.GetElement(pSys.AsElementId());
                    if (sysEl != null) return sysEl.Name;
                }
                string s = pSys.AsString();
                if (!string.IsNullOrEmpty(s)) return s;
            }

            Parameter pSysCustom = elem.LookupParameter("System Type") ?? elem.LookupParameter("System Classification") ?? elem.LookupParameter("Hệ Thống");
            if (pSysCustom != null && pSysCustom.HasValue)
            {
                string s = pSysCustom.AsString();
                if (!string.IsNullOrEmpty(s)) return s;
            }

            return "Cấp Gió (SA)";
        }

        public static string GetElementCustomParamValue(Element elem, string paramName)
        {
            if (elem == null || string.IsNullOrEmpty(paramName)) return string.Empty;

            Parameter p = elem.LookupParameter(paramName);
            if (p == null)
            {
                // Thử tìm trong Type (Symbol) nếu có
                FamilyInstance fi = elem as FamilyInstance;
                if (fi != null && fi.Symbol != null)
                {
                    p = fi.Symbol.LookupParameter(paramName);
                }
            }

            if (p == null || !p.HasValue) return string.Empty;

            switch (p.StorageType)
            {
                case StorageType.String:
                    return p.AsString() ?? string.Empty;
                case StorageType.Double:
                    return Math.Round(p.AsDouble(), 2).ToString(CultureInfo.InvariantCulture);
                case StorageType.Integer:
                    return p.AsInteger().ToString();
                case StorageType.ElementId:
                    ElementId id = p.AsElementId();
                    if (id != null && id != ElementId.InvalidElementId)
                    {
                        Element refEl = elem.Document.GetElement(id);
                        if (refEl != null) return refEl.Name;
                    }
                    return id.ToString();
                default:
                    return p.AsValueString() ?? string.Empty;
            }
        }

        private static string GetParamString(Element elem, BuiltInParameter bip)
        {
            Parameter p = elem.get_Parameter(bip);
            if (p != null && p.HasValue)
            {
                return p.AsString() ?? p.AsValueString();
            }
            return null;
        }

        /// <summary>
        /// Trích xuất danh sách tất cả các tham số dự án khả dụng cho Category HVAC (Project / Shared / Family Params)
        /// </summary>
        public static List<string> GetAvailableDuctProjectParameters(Document doc, ICollection<ElementId> sampleElementIds = null)
        {
            SortedSet<string> set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            // Bổ sung các parameter chuẩn phổ biến nhất của MEP / HVAC
            set.Add("Comments");
            set.Add("Mark");
            set.Add("Loại Bảo Ôn");
            set.Add("Độ Dày Bảo Ôn");
            set.Add("Độ Dày Tôn");
            set.Add("Vật Liệu Tôn");
            set.Add("Mã Hiệu Phụ Kiện");
            set.Add("Nhà Cung Cấp");
            set.Add("Ghi Chú Nghiệm Thu");
            set.Add("Cost");
            set.Add("Phase Created");
            set.Add("Type Comments");
            set.Add("Type Mark");
            set.Add("Description");
            set.Add("Manufacturer");

            if (doc != null)
            {
                List<Element> samples = new List<Element>();
                if (sampleElementIds != null && sampleElementIds.Count > 0)
                {
                    foreach (ElementId id in sampleElementIds.Take(25))
                    {
                        Element el = doc.GetElement(id);
                        if (el != null) samples.Add(el);
                    }
                }
                else
                {
                    // Lấy ngẫu nhiên vài phần tử đại diện
                    var ducts = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_DuctCurves).WhereElementIsNotElementType().Take(5);
                    var fittings = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_DuctFitting).WhereElementIsNotElementType().Take(5);
                    var accessories = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_DuctAccessory).WhereElementIsNotElementType().Take(5);
                    var terminals = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_DuctTerminal).WhereElementIsNotElementType().Take(5);
                    samples.AddRange(ducts);
                    samples.AddRange(fittings);
                    samples.AddRange(accessories);
                    samples.AddRange(terminals);
                }

                foreach (Element el in samples)
                {
                    foreach (Parameter p in el.Parameters)
                    {
                        if (p == null || p.Definition == null) continue;
                        string name = p.Definition.Name;
                        if (string.IsNullOrWhiteSpace(name)) continue;

                        // Bỏ qua các parameter nội bộ hệ thống Revit khó hiểu
                        if (name.StartsWith("ELEM_") || name.StartsWith("SYMBOL_") || name.StartsWith("UNIFORMAT_") || name.StartsWith("RBS_"))
                            continue;

                        set.Add(name);
                    }
                }
            }

            return set.ToList();
        }

        /// <summary>
        /// Soi sáng đối tượng 2 chiều trên mô hình Revit (Màu cam rực rỡ + Zoom to Fit)
        /// </summary>
        public static void HighlightElements(UIDocument uidoc, ICollection<ElementId> targetIds, Autodesk.Revit.DB.Color color = null, int lineWeight = 6, bool zoom = false)
        {
            if (uidoc == null || targetIds == null || targetIds.Count == 0) return;
            Document doc = uidoc.Document;
            Autodesk.Revit.DB.View activeV = uidoc.ActiveView;
            if (activeV == null) return;

            if (color == null)
            {
                color = new Autodesk.Revit.DB.Color(245, 158, 11); // Amber / Orange nổi bật
            }

            try
            {
                using (Transaction t = new Transaction(doc, "HDTOOL - Highlight Khối Lượng Ống Gió"))
                {
                    t.Start();
                    OverrideGraphicSettings ogs = new OverrideGraphicSettings();
                    ogs.SetProjectionLineColor(color);
                    ogs.SetProjectionLineWeight(lineWeight);

                    foreach (ElementId id in targetIds)
                    {
                        try
                        {
                            activeV.SetElementOverrides(id, ogs);
                        }
                        catch { }
                    }
                    t.Commit();
                }

                if (zoom)
                {
                    uidoc.Selection.SetElementIds(targetIds.ToList());
                    uidoc.ShowElements(targetIds.ToList());
                }
            }
            catch { }
        }

        /// <summary>
        /// Xóa highlight trên khung nhìn hiện tại
        /// </summary>
        public static void ClearHighlight(UIDocument uidoc, ICollection<ElementId> targetIds)
        {
            if (uidoc == null || targetIds == null || targetIds.Count == 0) return;
            Document doc = uidoc.Document;
            Autodesk.Revit.DB.View activeV = uidoc.ActiveView;
            if (activeV == null) return;

            try
            {
                using (Transaction t = new Transaction(doc, "HDTOOL - Clear Highlight Ống Gió"))
                {
                    t.Start();
                    OverrideGraphicSettings emptyOgs = new OverrideGraphicSettings();
                    foreach (ElementId id in targetIds)
                    {
                        try
                        {
                            activeV.SetElementOverrides(id, emptyOgs);
                        }
                        catch { }
                    }
                    t.Commit();
                }
            }
            catch { }
        }

        /// <summary>
        /// Xuất dữ liệu bảng khối lượng ống gió ra file Excel (.xlsx / .csv)
        /// </summary>
        public static string ExportToCsv(List<DuctQuantityItem> items, List<string> customParams, string filePath = null)
        {
            if (items == null || items.Count == 0) return null;
            items = SortDuctQuantityItems(items);

            if (string.IsNullOrEmpty(filePath))
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "HDTool_Exports");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                filePath = Path.Combine(folder, string.Format("Bang_Khoi_Luong_Ong_Gio_HVAC_{0}.csv", DateTime.Now.ToString("yyyyMMdd_HHmmss")));
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("HDTOOL - BANG THONG KE KHOI LUONG ONG GIO & PHU KIEN HVAC (TCVN 5687:2010)");
            sb.AppendLine("DOANH NGHIEP: HadoMEE ENGINEERING - KHAC GHI: TAT CA VI HA DO THAN YEU");
            sb.AppendLine(string.Format("Ngay xuat: {0} | Nguoi thuc hien: Sep Ho", DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")));
            sb.AppendLine();

            // Header columns
            List<string> headers = new List<string> {
                "STT", "Ten Cau Kien / Phu Kien", "Quy Cach (Size)", "Chieu Dai (m)", "So Luong", "Dien Tich Ton (m2)",
                "He Thong", "Do Day Ton (mm)", "Tang / Cao Do", "Chung Loai"
            };

            if (customParams != null && customParams.Count > 0)
            {
                foreach (string cp in customParams)
                {
                    headers.Add(string.Format("\"{0}\"", cp.Replace("\"", "\"\"")));
                }
            }

            sb.AppendLine(string.Join(",", headers));

            double totalArea = 0;
            double totalLen = 0;
            int totalCount = 0;

            foreach (var it in items)
            {
                totalArea += it.TotalAreaM2;
                totalLen += it.TotalLengthM;
                totalCount += it.Count;

                List<string> row = new List<string>
                {
                    it.STT.ToString(),
                    string.Format("\"{0}\"", (it.MergedName ?? "").Replace("\"", "\"\"")),
                    string.Format("\"{0}\"", (it.Size ?? "").Replace("\"", "\"\"")),
                    it.TotalLengthM > 0 ? it.TotalLengthM.ToString("0.00", CultureInfo.InvariantCulture) : "-",
                    string.Format("{0} {1}", it.Count, it.Unit),
                    it.TotalAreaM2 > 0 ? it.TotalAreaM2.ToString("0.00", CultureInfo.InvariantCulture) : "-",
                    string.Format("\"{0}\"", (it.SystemType ?? "").Replace("\"", "\"\"")),
                    string.Format("\"{0}\"", (it.Thickness ?? "").Replace("\"", "\"\"")),
                    string.Format("\"{0}\"", (it.LocationByLevel ?? "").Replace("\"", "\"\"")),
                    string.Format("\"{0}\"", (it.CategoryKind ?? "").Replace("\"", "\"\""))
                };

                if (customParams != null && customParams.Count > 0)
                {
                    foreach (string cp in customParams)
                    {
                        string val = "";
                        if (it.CustomParameters != null && it.CustomParameters.ContainsKey(cp))
                        {
                            val = it.CustomParameters[cp] ?? "";
                        }
                        row.Add(string.Format("\"{0}\"", val.Replace("\"", "\"\"")));
                    }
                }

                sb.AppendLine(string.Join(",", row));
            }

            sb.AppendLine();
            sb.AppendLine(string.Format("TONG CONG DIEN TICH TON:,,,{0:0.00} m,,{1:0.00} m2,,,,", totalLen, totalArea));

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            return filePath;
        }

        #region ====== ĐẶT BẢNG KHỐI LƯỢNG ỐNG GIÓ LÊN SHEET (LEGEND VIEW & VIEWPORT) ======

        private const double MM_TO_FT = 1.0 / 304.8;

        /// <summary>
        /// Tạo hoặc cập nhật Bảng Khối Lượng Ống Gió & Phụ Kiện HVAC (TCVN 5687:2010)
        /// dưới dạng Legend View chuẩn và đặt vào Viewport trên Sheet mong muốn.
        /// </summary>
        public static bool CreateOrUpdateDuctTableOnSheet(
            Document doc,
            string tableName,
            ViewSheet targetSheet,
            XYZ placementPoint,
            List<DuctQuantityItem> items,
            List<string> customParams,
            out Autodesk.Revit.DB.View createdView,
            out Viewport createdViewport,
            out string resultMessage)
        {
            createdView = null;
            createdViewport = null;
            resultMessage = string.Empty;

            if (doc == null || items == null || items.Count == 0)
            {
                resultMessage = "Không có dữ liệu khối lượng ống gió để tạo bảng.";
                return false;
            }

            items = SortDuctQuantityItems(items);

            char[] forbiddenChars = new char[] { '\\', ':', '{', '}', '[', ']', '|', ';', '<', '>', '?', '*' };
            string cleanTitle = !string.IsNullOrWhiteSpace(tableName) ? tableName.Trim() : "BANG THONG KE KHOI LUONG ONG GIO HVAC";
            foreach (char c in forbiddenChars)
            {
                cleanTitle = cleanTitle.Replace(c, '_');
            }
            string viewName = "HD_BẢNG KHỐI LƯỢNG ỐNG GIÓ - " + cleanTitle;
            if (viewName.Length > 90) viewName = viewName.Substring(0, 90);

            try
            {
                using (Transaction tx = new Transaction(doc, "HDTOOL - Đặt Bảng Khối Lượng Ống Gió Vào Sheet"))
                {
                    tx.Start();

                    // 1. Tạo hoặc lấy Legend View để có thể dùng trên nhiều Sheet
                    Autodesk.Revit.DB.View tableView = GetOrCreateTableLegendView(doc, viewName);
                    if (tableView == null)
                    {
                        resultMessage = "Không thể tạo hoặc nhân bản View Legend trong dự án Revit.";
                        tx.RollBack();
                        return false;
                    }

                    // Tẩy sạch đối tượng cũ trong View để cập nhật mới 100%
                    FilteredElementCollector oldCol = new FilteredElementCollector(doc, tableView.Id)
                        .WhereElementIsNotElementType();
                    List<ElementId> toDel = new List<ElementId>();
                    foreach (Element el in oldCol)
                    {
                        if (el is DetailCurve || el is TextNote || el is Dimension || el is FilledRegion || el is IndependentTag)
                        {
                            toDel.Add(el.Id);
                        }
                    }
                    if (toDel.Count > 0)
                    {
                        doc.Delete(toDel);
                    }

                    // 2. Lấy kiểu chữ TextNoteType
                    TextNoteType textType = GetOrCreateTableTextType(doc, 2.0, false);
                    TextNoteType headerTextType = GetOrCreateTableTextType(doc, 2.2, true);
                    TextNoteType titleTextType = GetOrCreateTableTextType(doc, 2.8, true);

                    if (textType == null)
                    {
                        FilteredElementCollector tntCol = new FilteredElementCollector(doc).OfClass(typeof(TextNoteType));
                        textType = tntCol.Cast<TextNoteType>().FirstOrDefault();
                    }
                    if (headerTextType == null) headerTextType = textType;
                    if (titleTextType == null) titleTextType = headerTextType;

                    if (textType == null)
                    {
                        resultMessage = "Không tìm thấy kiểu chữ (TextNoteType) nào trong tài liệu Revit.";
                        tx.RollBack();
                        return false;
                    }

                    // 3. Cấu hình các cột hiển thị trong bảng
                    List<DuctTableColDef> cols = new List<DuctTableColDef>();
                    cols.Add(new DuctTableColDef("STT", 14.0, HorizontalTextAlignment.Center, (it, idx) => idx.ToString()));
                    cols.Add(new DuctTableColDef("Tên Cấu Kiện / Phụ Kiện", 60.0, HorizontalTextAlignment.Left, (it, idx) => it.MergedName));
                    cols.Add(new DuctTableColDef("Quy Cách (Size)", 32.0, HorizontalTextAlignment.Center, (it, idx) => it.Size));
                    cols.Add(new DuctTableColDef("Chiều Dài (m)", 24.0, HorizontalTextAlignment.Right, (it, idx) => it.TotalLengthM > 0 ? it.TotalLengthM.ToString("0.00") : "-"));
                    cols.Add(new DuctTableColDef("Số Lượng", 22.0, HorizontalTextAlignment.Center, (it, idx) => string.Format("{0} {1}", it.Count, it.Unit)));
                    cols.Add(new DuctTableColDef("Diện Tích (m²)", 26.0, HorizontalTextAlignment.Right, (it, idx) => it.TotalAreaM2 > 0 ? it.TotalAreaM2.ToString("0.00") : "-"));
                    cols.Add(new DuctTableColDef("Hệ Thống", 32.0, HorizontalTextAlignment.Left, (it, idx) => it.SystemType));
                    cols.Add(new DuctTableColDef("Độ Dày Tôn", 22.0, HorizontalTextAlignment.Center, (it, idx) => it.Thickness));
                    cols.Add(new DuctTableColDef("Tầng", 22.0, HorizontalTextAlignment.Center, (it, idx) => it.LocationByLevel));
                    cols.Add(new DuctTableColDef("Chủng Loại", 26.0, HorizontalTextAlignment.Center, (it, idx) => it.CategoryKind));

                    if (customParams != null && customParams.Count > 0)
                    {
                        foreach (string cp in customParams)
                        {
                            string pName = cp;
                            cols.Add(new DuctTableColDef(pName, 30.0, HorizontalTextAlignment.Left, (it, idx) => {
                                if (it.CustomParameters != null && it.CustomParameters.ContainsKey(pName))
                                {
                                    return it.CustomParameters[pName] ?? "";
                                }
                                return "";
                            }));
                        }
                    }

                    // 4. Kích thước bảng (mm chuyển sang feet)
                    double titleHFt = 12.0 * MM_TO_FT;
                    double headerHFt = 8.5 * MM_TO_FT;
                    double rowHFt = 6.8 * MM_TO_FT;
                    double summaryHFt = 8.0 * MM_TO_FT;

                    double totalWidthMm = cols.Sum(c => c.WidthMm);
                    double totalWidthFt = totalWidthMm * MM_TO_FT;
                    double totalHFt = titleHFt + headerHFt + (items.Count * rowHFt) + summaryHFt;

                    // 5. VẼ CÁC ĐƯỜNG KẺ BẢNG (GRID LINES)
                    // Đường ngang đỉnh bảng
                    SafeDrawDetailLine(doc, tableView, new XYZ(0, 0, 0), new XYZ(totalWidthFt, 0, 0));
                    // Đường ngang dưới tiêu đề
                    SafeDrawDetailLine(doc, tableView, new XYZ(0, -titleHFt, 0), new XYZ(totalWidthFt, -titleHFt, 0));

                    double headerBottomY = -titleHFt - headerHFt;
                    SafeDrawDetailLine(doc, tableView, new XYZ(0, headerBottomY, 0), new XYZ(totalWidthFt, headerBottomY, 0));

                    for (int r = 1; r <= items.Count; r++)
                    {
                        double rowY = headerBottomY - (r * rowHFt);
                        SafeDrawDetailLine(doc, tableView, new XYZ(0, rowY, 0), new XYZ(totalWidthFt, rowY, 0));
                    }

                    // Đường ngang đáy bảng
                    SafeDrawDetailLine(doc, tableView, new XYZ(0, -totalHFt, 0), new XYZ(totalWidthFt, -totalHFt, 0));

                    // Đường dọc bao ngoài
                    SafeDrawDetailLine(doc, tableView, new XYZ(0, 0, 0), new XYZ(0, -totalHFt, 0));
                    SafeDrawDetailLine(doc, tableView, new XYZ(totalWidthFt, 0, 0), new XYZ(totalWidthFt, -totalHFt, 0));

                    // Đường dọc phân cột
                    double curX = 0;
                    for (int c = 0; c < cols.Count - 1; c++)
                    {
                        curX += cols[c].WidthMm * MM_TO_FT;
                        double bottomColY = -totalHFt + summaryHFt;
                        SafeDrawDetailLine(doc, tableView, new XYZ(curX, -titleHFt, 0), new XYZ(curX, bottomColY, 0));
                    }

                    // 6. GHI NỘI DUNG TEXT VÀO CÁC Ô
                    double paddingXFt = 1.5 * MM_TO_FT;

                    // Tiêu đề bảng
                    string fullTitle = string.Format("HDTOOL — {0}", cleanTitle.ToUpper());
                    SafeDrawTextNote(doc, tableView, titleTextType.Id, new XYZ(totalWidthFt / 2.0, -titleHFt / 2.0, 0), fullTitle, HorizontalTextAlignment.Center, VerticalTextAlignment.Middle, totalWidthFt - (paddingXFt * 2));

                    // Header cột
                    double colLeft = 0;
                    for (int c = 0; c < cols.Count; c++)
                    {
                        DuctTableColDef col = cols[c];
                        double colWidthFt = col.WidthMm * MM_TO_FT;
                        double colCenter = colLeft + (colWidthFt / 2.0);
                        double headerMidY = -titleHFt - (headerHFt / 2.0);
                        double cellWidthFt = Math.Max(colWidthFt - (paddingXFt * 1.5), 0.005);

                        SafeDrawTextNote(doc, tableView, headerTextType.Id, new XYZ(colCenter, headerMidY, 0), col.Header.ToUpper(), HorizontalTextAlignment.Center, VerticalTextAlignment.Middle, cellWidthFt);

                        colLeft += colWidthFt;
                    }

                    // Dữ liệu từng dòng
                    double totalArea = 0;
                    double totalLen = 0;
                    int totalCount = 0;

                    for (int r = 0; r < items.Count; r++)
                    {
                        DuctQuantityItem it = items[r];
                        totalArea += it.TotalAreaM2;
                        totalLen += it.TotalLengthM;
                        totalCount += it.Count;

                        double rowMidY = headerBottomY - (r * rowHFt) - (rowHFt / 2.0);
                        colLeft = 0;

                        for (int c = 0; c < cols.Count; c++)
                        {
                            DuctTableColDef col = cols[c];
                            double colWidthFt = col.WidthMm * MM_TO_FT;
                            string val = col.ValueGetter(it, r + 1) ?? string.Empty;

                            if (!string.IsNullOrEmpty(val))
                            {
                                double cellWidthFt = Math.Max(colWidthFt - (paddingXFt * 1.5), 0.005);
                                double posX = colLeft + (colWidthFt / 2.0);
                                if (col.Align == HorizontalTextAlignment.Left)
                                {
                                    posX = colLeft + paddingXFt;
                                }
                                else if (col.Align == HorizontalTextAlignment.Right)
                                {
                                    posX = colLeft + colWidthFt - paddingXFt;
                                }

                                SafeDrawTextNote(doc, tableView, textType.Id, new XYZ(posX, rowMidY, 0), val, col.Align, VerticalTextAlignment.Middle, cellWidthFt);
                            }

                            colLeft += colWidthFt;
                        }
                    }

                    // Dòng tổng kết
                    string summaryText = string.Format("📊 TỔNG CỘNG: {0:0.00} m² tôn mạ kẽm  |  {1:0.00} mét dài ống  |  {2} phụ kiện, van, miệng gió  |  HadoMEE", totalArea, totalLen, totalCount);
                    double summaryMidY = -totalHFt + (summaryHFt / 2.0);
                    SafeDrawTextNote(doc, tableView, headerTextType.Id, new XYZ(totalWidthFt / 2.0, summaryMidY, 0), summaryText, HorizontalTextAlignment.Center, VerticalTextAlignment.Middle, totalWidthFt - (paddingXFt * 2));

                    // 7. CẤU HÌNH CROP BOX ÔM KHÍT BẢNG
                    try
                    {
                        tableView.CropBoxActive = true;
                        tableView.CropBoxVisible = false;

                        BoundingBoxXYZ crop = new BoundingBoxXYZ();
                        crop.Min = new XYZ(-0.02, -totalHFt - 0.02, 0);
                        crop.Max = new XYZ(totalWidthFt + 0.02, 0.02, 0);
                        tableView.CropBox = crop;
                    }
                    catch { }

                    createdView = tableView;

                    // 8. ĐẶT HOẶC CẬP NHẬT VIEWPORT TRÊN SHEET
                    Viewport vp = null;
                    if (targetSheet != null)
                    {
                        XYZ centerPt;
                        if (placementPoint != null)
                        {
                            centerPt = new XYZ(placementPoint.X + (totalWidthFt / 2.0), placementPoint.Y - (totalHFt / 2.0), 0);
                        }
                        else
                        {
                            // Tự động tìm khung tên bản vẽ (TitleBlock) để căn vào góc trên bên phải
                            FamilyInstance tb = new FilteredElementCollector(doc, targetSheet.Id)
                                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                                .WhereElementIsNotElementType()
                                .Cast<FamilyInstance>()
                                .FirstOrDefault();

                            if (tb != null)
                            {
                                BoundingBoxXYZ tbBox = tb.get_BoundingBox(targetSheet);
                                if (tbBox != null)
                                {
                                    double marginX = 20.0 * MM_TO_FT;
                                    double marginY = 20.0 * MM_TO_FT;
                                    double targetRightX = tbBox.Max.X - marginX;
                                    double targetTopY = tbBox.Max.Y - marginY;
                                    centerPt = new XYZ(targetRightX - (totalWidthFt / 2.0), targetTopY - (totalHFt / 2.0), 0);
                                }
                                else
                                {
                                    centerPt = new XYZ(totalWidthFt / 2.0 + 0.1, 1.8 - (totalHFt / 2.0), 0);
                                }
                            }
                            else
                            {
                                centerPt = new XYZ(totalWidthFt / 2.0 + 0.1, 1.8 - (totalHFt / 2.0), 0);
                            }
                        }

                        FilteredElementCollector vpCol = new FilteredElementCollector(doc, targetSheet.Id)
                            .OfClass(typeof(Viewport));

                        foreach (Viewport v in vpCol.Cast<Viewport>())
                        {
                            if (v.ViewId == tableView.Id)
                            {
                                vp = v;
                                break;
                            }
                        }

                        if (vp == null)
                        {
                            vp = Viewport.Create(doc, targetSheet.Id, tableView.Id, centerPt);
                            resultMessage = string.Format("✅ Đã tạo bảng '{0}' và đặt thành công lên Sheet {1} ({2})!",
                                cleanTitle, targetSheet.SheetNumber, targetSheet.Name);
                        }
                        else
                        {
                            vp.SetBoxCenter(centerPt);
                            resultMessage = string.Format("✅ Đã cập nhật thành công Bảng Khối Lượng '{0}' trên Sheet {1} ({2})!",
                                cleanTitle, targetSheet.SheetNumber, targetSheet.Name);
                        }
                        createdViewport = vp;
                    }
                    else
                    {
                        resultMessage = string.Format("✅ Đã tạo bảng '{0}' trong danh sách Legend Views! Sếp có thể kéo thả vào bất kỳ Sheet nào.", cleanTitle);
                    }

                    tx.Commit();
                    return true;
                }
            }
            catch (Exception ex)
            {
                resultMessage = "Lỗi tạo bảng lên Sheet: " + ex.Message;
                return false;
            }
        }

        public static Autodesk.Revit.DB.View GetOrCreateTableLegendView(Document doc, string viewName)
        {
            if (doc == null || string.IsNullOrWhiteSpace(viewName)) return null;

            FilteredElementCollector allViews = new FilteredElementCollector(doc).OfClass(typeof(Autodesk.Revit.DB.View));

            // 1. Tìm Legend View đã có cùng tên để tái sử dụng
            foreach (Autodesk.Revit.DB.View v in allViews.Cast<Autodesk.Revit.DB.View>())
            {
                if (!v.IsTemplate && v.ViewType == ViewType.Legend && v.Name.Equals(viewName, StringComparison.OrdinalIgnoreCase))
                {
                    return v;
                }
            }

            // 2. Tìm bất kỳ Legend View nào có sẵn trong dự án để Duplicate
            Autodesk.Revit.DB.View anyLegend = allViews.Cast<Autodesk.Revit.DB.View>()
                .FirstOrDefault(v => !v.IsTemplate && v.ViewType == ViewType.Legend);

            if (anyLegend != null)
            {
                try
                {
                    ElementId dupId = anyLegend.Duplicate(ViewDuplicateOption.Duplicate);
                    Autodesk.Revit.DB.View dupView = doc.GetElement(dupId) as Autodesk.Revit.DB.View;
                    if (dupView != null)
                    {
                        try { dupView.Name = viewName; } catch { }
                        try { dupView.Scale = 1; } catch { }
                        return dupView;
                    }
                }
                catch { }
            }

            // 3. Fallback sang ViewDrafting nếu dự án chưa có Legend
            ViewFamilyType draftVft = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(x => x.ViewFamily == ViewFamily.Drafting);

            if (draftVft != null)
            {
                ViewDrafting fallbackDraft = ViewDrafting.Create(doc, draftVft.Id);
                try { fallbackDraft.Name = viewName; } catch { }
                try { fallbackDraft.Scale = 1; } catch { }
                return fallbackDraft;
            }

            return null;
        }

        private static void SafeDrawDetailLine(Document doc, Autodesk.Revit.DB.View view, XYZ pt1, XYZ pt2)
        {
            if (doc == null || view == null || pt1 == null || pt2 == null) return;
            if (pt1.DistanceTo(pt2) < 0.001) return;
            try
            {
                Line l = Line.CreateBound(pt1, pt2);
                doc.Create.NewDetailCurve(view, l);
            }
            catch { }
        }

        private static void SafeDrawTextNote(Document doc, Autodesk.Revit.DB.View view, ElementId typeId, XYZ pt, string text, HorizontalTextAlignment hAlign, VerticalTextAlignment vAlign, double widthFt = 0)
        {
            if (doc == null || view == null || typeId == null || pt == null || string.IsNullOrEmpty(text)) return;
            try
            {
                TextNoteOptions opts = new TextNoteOptions(typeId);
                opts.HorizontalAlignment = hAlign;
                opts.VerticalAlignment = vAlign;
                if (widthFt > 0.001)
                {
                    TextNote.Create(doc, view.Id, pt, widthFt, text, opts);
                }
                else
                {
                    TextNote.Create(doc, view.Id, pt, text, opts);
                }
            }
            catch { }
        }

        public static TextNoteType GetOrCreateTableTextType(Document doc, double textSizeMm, bool isBold)
        {
            if (doc == null) return null;
            string typeName = string.Format("HDTOOL_DuctTable_{0:0.0}mm{1}", textSizeMm, isBold ? "_Bold" : "");
            FilteredElementCollector col = new FilteredElementCollector(doc).OfClass(typeof(TextNoteType));
            foreach (TextNoteType tnt in col.Cast<TextNoteType>())
            {
                if (tnt.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase))
                {
                    return tnt;
                }
            }

            TextNoteType defaultType = col.Cast<TextNoteType>().FirstOrDefault();
            if (defaultType == null) return null;

            try
            {
                TextNoteType newType = defaultType.Duplicate(typeName) as TextNoteType;
                if (newType != null)
                {
                    double sizeFt = (textSizeMm / 304.8);
                    Parameter pSize = newType.get_Parameter(BuiltInParameter.TEXT_SIZE);
                    if (pSize != null && !pSize.IsReadOnly) pSize.Set(sizeFt);

                    Parameter pBold = newType.get_Parameter(BuiltInParameter.TEXT_STYLE_BOLD);
                    if (pBold != null && !pBold.IsReadOnly) pBold.Set(isBold ? 1 : 0);

                    Parameter pWidth = newType.get_Parameter(BuiltInParameter.TEXT_WIDTH_SCALE);
                    if (pWidth != null && !pWidth.IsReadOnly) pWidth.Set(0.88);

                    Parameter pBack = newType.get_Parameter(BuiltInParameter.TEXT_BACKGROUND);
                    if (pBack != null && !pBack.IsReadOnly) pBack.Set(1); // 1 = Transparent

                    return newType;
                }
            }
            catch { }

            return defaultType;
        }

        public class DuctTableColDef
        {
            public string Header { get; set; }
            public double WidthMm { get; set; }
            public HorizontalTextAlignment Align { get; set; }
            public Func<DuctQuantityItem, int, string> ValueGetter { get; set; }

            public DuctTableColDef(string header, double widthMm, HorizontalTextAlignment align, Func<DuctQuantityItem, int, string> valueGetter)
            {
                Header = header;
                WidthMm = widthMm;
                Align = align;
                ValueGetter = valueGetter;
            }
        }

        #endregion
    }
}

