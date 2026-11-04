using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using RevitApp = Autodesk.Revit.ApplicationServices.Application;

namespace HDTool
{
    /// <summary>
    /// Bộ lọc chọn đối tượng: Chỉ cho phép quét chọn Phụ Kiện Ống, Đường Ống và Phụ Kiện MEP
    /// </summary>
    public class MepSelectionFilter : ISelectionFilter
    {
        private bool _allowPipes = true;
        private bool _allowFittings = true;

        public MepSelectionFilter()
        {
            _allowPipes = true;
            _allowFittings = true;
        }

        public MepSelectionFilter(bool allowPipes, bool allowFittings)
        {
            _allowPipes = allowPipes;
            _allowFittings = allowFittings;
        }

        public bool AllowElement(Element elem)
        {
            if (elem == null || elem.Category == null) return false;
            ElementId catId = elem.Category.Id;

            if (_allowPipes && catId == new ElementId(BuiltInCategory.OST_PipeCurves))
            {
                return true;
            }

            if (_allowFittings && (catId == new ElementId(BuiltInCategory.OST_PipeFitting) ||
                                  catId == new ElementId(BuiltInCategory.OST_PipeAccessory)))
            {
                return true;
            }

            return false;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }

    /// <summary>
    /// Bộ lọc quét chọn thông minh: Cho phép quét chọn Pipe, Fitting và Viewport trên Sheet
    /// </summary>
    public class SheetOrMepSelectionFilter : ISelectionFilter
    {
        private bool _allowPipes;
        private bool _allowFittings;
        private bool _isSheet;

        public SheetOrMepSelectionFilter(bool allowPipes, bool allowFittings, bool isSheet)
        {
            _allowPipes = allowPipes;
            _allowFittings = allowFittings;
            _isSheet = isSheet;
        }

        public bool AllowElement(Element elem)
        {
            if (elem == null || elem.Category == null) return false;
            ElementId catId = elem.Category.Id;

            // Cho phép chọn Viewport khi đang ở trên Sheet
            if (_isSheet && catId == new ElementId(BuiltInCategory.OST_Viewports))
            {
                return true;
            }

            if (_allowPipes && catId == new ElementId(BuiltInCategory.OST_PipeCurves))
            {
                return true;
            }

            if (_allowFittings && (catId == new ElementId(BuiltInCategory.OST_PipeFitting) ||
                                  catId == new ElementId(BuiltInCategory.OST_PipeAccessory)))
            {
                return true;
            }

            return false;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }

    /// <summary>
    /// Tùy chọn ẩn hiện các cột trên bảng Schedule Revit khi đưa vào Sheet
    /// </summary>
    public class ScheduleColumnVisibilityOptions
    {
        public bool ShowName { get; set; }
        public bool ShowSize { get; set; }
        public bool ShowCount { get; set; }
        public bool ShowType { get; set; }
        public bool ShowArea { get; set; }
        public bool ShowPN { get; set; }
        public bool ShowLevel { get; set; }
        public bool ShowGrandTotal { get; set; }

        public ScheduleColumnVisibilityOptions()
        {
            ShowName = true;
            ShowSize = true;
            ShowCount = true;
            ShowType = true;
            ShowArea = true;
            ShowPN = false; // Mặc định ẩn PN trừ khi người dùng tích chọn
            ShowLevel = true;
            ShowGrandTotal = true;
        }
    }

    /// <summary>
    /// Định nghĩa cấu trúc một cột hiển thị trong Bảng Khối Lượng Excel trên Sheet
    /// </summary>
    public class ExcelTableColumn
    {
        public string Header { get; set; }
        public double WidthMm { get; set; }
        public HorizontalTextAlignment Align { get; set; }
        public Func<MepFittingItem, int, string> ValueGetter { get; set; }

        public ExcelTableColumn(string header, double widthMm, HorizontalTextAlignment align, Func<MepFittingItem, int, string> valueGetter)
        {
            Header = header;
            WidthMm = widthMm;
            Align = align;
            ValueGetter = valueGetter;
        }
    }

    /// <summary>
    /// Metadata lưu thông tin hình học và danh sách đối tượng của Bảng Khối Lượng Excel đã đặt trên Sheet
    /// phục vụ tính năng Soi Sáng Đối Tượng (Highlight) khi click vào bất kỳ dòng nào của bảng trên Sheet
    /// </summary>
    public class PlacedTableMetadata
    {
        public ElementId SheetId { get; set; }
        public ElementId DraftingViewId { get; set; }
        public ElementId ViewportId { get; set; }
        public double TotalWidthFt { get; set; }
        public double TotalHeightFt { get; set; }
        public double TitleHeightFt { get; set; }
        public double HeaderHeightFt { get; set; }
        public double RowHeightFt { get; set; }
        public List<MepFittingItem> Items { get; set; }

        public PlacedTableMetadata()
        {
            Items = new List<MepFittingItem>();
        }
    }

    /// <summary>
    /// Trình quản lý Highlight phát sáng đối tượng MEP chuyên nghiệp:
    /// Áp dụng Graphic Overrides màu sắc nổi bật (Đỏ tươi / Cam rực rỡ, LineWeight 5-7)
    /// trên ActiveView và tất cả các Model View (Floor Plan, 3D, v.v.) đang mở,
    /// đồng thời tự động Zoom và Selection trong Revit
    /// </summary>
    public static class MepHighlightManager
    {
        private static Dictionary<ElementId, HashSet<ElementId>> _overriddenByView = new Dictionary<ElementId, HashSet<ElementId>>();
        private static HashSet<ElementId> _allOverriddenElements = new HashSet<ElementId>();

        /// <summary>
        /// Xóa toàn bộ highlight override trên tất cả các view mô hình trong Document
        /// </summary>
        public static void ClearHighlights(Document doc)
        {
            if (doc == null) return;
            if (_overriddenByView.Count == 0 && _allOverriddenElements.Count == 0) return;

            try
            {
                using (Transaction tx = new Transaction(doc, "HDTOOL - Xóa Highlight MEP"))
                {
                    tx.Start();
                    OverrideGraphicSettings resetOgs = new OverrideGraphicSettings();

                    // 1. Reset theo từng View đã lưu
                    foreach (var kvp in _overriddenByView)
                    {
                        Autodesk.Revit.DB.View view = doc.GetElement(kvp.Key) as Autodesk.Revit.DB.View;
                        if (view != null && view.IsValidObject)
                        {
                            foreach (ElementId elId in kvp.Value)
                            {
                                try { view.SetElementOverrides(elId, resetOgs); } catch { }
                            }
                        }
                    }

                    // 2. Quét thêm toàn bộ open views nếu còn sót element trong _allOverriddenElements
                    if (_allOverriddenElements.Count > 0)
                    {
                        FilteredElementCollector vCol = new FilteredElementCollector(doc).OfClass(typeof(Autodesk.Revit.DB.View));
                        foreach (Autodesk.Revit.DB.View v in vCol.Cast<Autodesk.Revit.DB.View>())
                        {
                            if (v != null && !v.IsTemplate && !(v is ViewSheet) && !(v is ViewDrafting) && !(v is ViewSchedule))
                            {
                                foreach (ElementId elId in _allOverriddenElements)
                                {
                                    try { v.SetElementOverrides(elId, resetOgs); } catch { }
                                }
                            }
                        }
                    }

                    tx.Commit();
                }
            }
            catch { }
            finally
            {
                _overriddenByView.Clear();
                _allOverriddenElements.Clear();
            }
        }

        /// <summary>
        /// Khôi phục toàn bộ giao diện Revit về nguyên trạng ban đầu:
        /// Xóa highlight, bỏ cách ly Temporary View, xóa Selection và Refresh View
        /// </summary>
        public static void ResetAllVisualsAndHighlights(UIDocument uidoc)
        {
            if (uidoc == null) return;
            Document doc = uidoc.Document;
            if (doc == null) return;

            try
            {
                // 1. Xóa toàn bộ highlight overrides
                ClearHighlights(doc);

                // 2. Bỏ cách ly Temporary View Mode nếu đang bật trên active view
                try
                {
                    if (uidoc.ActiveView != null && uidoc.ActiveView.IsInTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate))
                    {
                        using (Transaction tx = new Transaction(doc, "HDTOOL - Bỏ Cách Ly View"))
                        {
                            tx.Start();
                            uidoc.ActiveView.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
                            tx.Commit();
                        }
                    }
                }
                catch { }

                // 3. Bỏ chọn các đối tượng (clear selection)
                try
                {
                    uidoc.Selection.SetElementIds(new List<ElementId>());
                }
                catch { }

                // 4. Refresh Active View để Revit vẽ lại giao diện sạch đẹp ngay lập tức
                try
                {
                    uidoc.RefreshActiveView();
                }
                catch { }
            }
            catch { }
        }

        /// <summary>
        /// Áp dụng highlight phát sáng màu nổi bật cho danh sách đối tượng
        /// trên ActiveView và tất cả các Model View (Floor Plan, 3D...) đang mở
        /// </summary>
        public static void HighlightElements(UIDocument uidoc, ICollection<ElementId> targetIds, Autodesk.Revit.DB.Color color = null, int lineWeight = 6, bool zoom = false)
        {
            if (uidoc == null) return;
            Document doc = uidoc.Document;
            if (doc == null) return;

            if (color == null)
            {
                color = new Autodesk.Revit.DB.Color(255, 0, 0); // Đỏ tươi mặc định
            }

            // 1. Tìm tất cả các View mô hình cần áp dụng (View hiện hành + các View mô hình đang mở)
            List<Autodesk.Revit.DB.View> viewsToOverride = new List<Autodesk.Revit.DB.View>();

            Autodesk.Revit.DB.View curView = uidoc.ActiveView;
            if (curView != null && !(curView is ViewSheet) && !(curView is ViewDrafting) && !(curView is ViewSchedule))
            {
                viewsToOverride.Add(curView);
            }

            try
            {
                IList<UIView> openViews = uidoc.GetOpenUIViews();
                foreach (UIView uiv in openViews)
                {
                    Autodesk.Revit.DB.View v = doc.GetElement(uiv.ViewId) as Autodesk.Revit.DB.View;
                    if (v != null && !(v is ViewSheet) && !(v is ViewDrafting) && !(v is ViewSchedule))
                    {
                        if (!viewsToOverride.Any(x => x.Id == v.Id))
                        {
                            viewsToOverride.Add(v);
                        }
                    }
                }
            }
            catch { }

            // 2. Thực hiện Transaction áp dụng OverrideGraphicSettings
            try
            {
                using (Transaction tx = new Transaction(doc, "HDTOOL - Highlight Đối Tượng MEP"))
                {
                    tx.Start();

                    OverrideGraphicSettings ogs = new OverrideGraphicSettings();
                    ogs.SetProjectionLineColor(color);
                    ogs.SetProjectionLineWeight(lineWeight);

                    try
                    {
                        FilteredElementCollector fpCol = new FilteredElementCollector(doc).OfClass(typeof(FillPatternElement));
                        FillPatternElement solidPattern = fpCol.Cast<FillPatternElement>().FirstOrDefault(x => 
                            x.Name.IndexOf("solid", StringComparison.OrdinalIgnoreCase) >= 0 || 
                            x.Name.IndexOf("đặc", StringComparison.OrdinalIgnoreCase) >= 0);
                        if (solidPattern != null)
                        {
                            ogs.SetSurfaceForegroundPatternId(solidPattern.Id);
                            ogs.SetSurfaceForegroundPatternColor(color);
                        }
                    }
                    catch { }

                    foreach (Autodesk.Revit.DB.View v in viewsToOverride)
                    {
                        if (v.IsValidObject)
                        {
                            HashSet<ElementId> existing;
                            if (_overriddenByView.TryGetValue(v.Id, out existing))
                            {
                                OverrideGraphicSettings resetOgs = new OverrideGraphicSettings();
                                foreach (ElementId oldId in existing)
                                {
                                    try { v.SetElementOverrides(oldId, resetOgs); } catch { }
                                }
                                existing.Clear();
                            }
                            else
                            {
                                existing = new HashSet<ElementId>();
                                _overriddenByView[v.Id] = existing;
                            }

                            if (targetIds != null && targetIds.Count > 0)
                            {
                                foreach (ElementId id in targetIds)
                                {
                                    try
                                    {
                                        v.SetElementOverrides(id, ogs);
                                        existing.Add(id);
                                        _allOverriddenElements.Add(id);
                                    }
                                    catch { }
                                }
                            }
                        }
                    }

                    tx.Commit();
                }
            }
            catch { }

            // 3. Selection
            try
            {
                if (targetIds != null && targetIds.Count > 0)
                {
                    uidoc.Selection.SetElementIds(targetIds);
                }
            }
            catch { }

            // 4. Zoom trên UIView mô hình
            try
            {
                IList<UIView> openViews = uidoc.GetOpenUIViews();
                foreach (UIView uiv in openViews)
                {
                    Autodesk.Revit.DB.View v = doc.GetElement(uiv.ViewId) as Autodesk.Revit.DB.View;
                    if (v != null && !(v is ViewSheet) && !(v is ViewDrafting) && !(v is ViewSchedule))
                    {
                        if (zoom && targetIds != null && targetIds.Count > 0)
                        {
                            XYZ bbMin = null;
                            XYZ bbMax = null;
                            foreach (ElementId tid in targetIds)
                            {
                                Element el = doc.GetElement(tid);
                                if (el != null)
                                {
                                    BoundingBoxXYZ b = el.get_BoundingBox(v);
                                    if (b != null)
                                    {
                                        if (bbMin == null) { bbMin = b.Min; bbMax = b.Max; }
                                        else
                                        {
                                            bbMin = new XYZ(Math.Min(bbMin.X, b.Min.X), Math.Min(bbMin.Y, b.Min.Y), Math.Min(bbMin.Z, b.Min.Z));
                                            bbMax = new XYZ(Math.Max(bbMax.X, b.Max.X), Math.Max(bbMax.Y, b.Max.Y), Math.Max(bbMax.Z, b.Max.Z));
                                        }
                                    }
                                }
                            }

                            if (bbMin != null && bbMax != null)
                            {
                                double dx = Math.Max(bbMax.X - bbMin.X, 2.0) * 0.2;
                                double dy = Math.Max(bbMax.Y - bbMin.Y, 2.0) * 0.2;
                                XYZ p1 = new XYZ(bbMin.X - dx, bbMin.Y - dy, bbMin.Z);
                                XYZ p2 = new XYZ(bbMax.X + dx, bbMax.Y + dy, bbMax.Z);
                                try { uiv.ZoomAndCenterRectangle(p1, p2); } catch { }
                            }
                        }
                    }
                }
            }
            catch { }

            try
            {
                uidoc.RefreshActiveView();
            }
            catch { }
        }
    }

    /// <summary>
    /// Service xử lý quét, gộp thông minh các parameter tên phụ kiện phân mảnh,
    /// đồng bộ parameter dự án, tạo bảng ViewSchedule chuẩn và đặt vào Sheet
    /// </summary>
    public static class MepQuantityService
    {
        public static Dictionary<ElementId, PlacedTableMetadata> PlacedTablesBySheet = new Dictionary<ElementId, PlacedTableMetadata>();
        public static PlacedTableMetadata LastPlacedTable = null;
        public const string DefaultUnifiedParamName = "HD_Tên Phụ Kiện";
        public const string DefaultScopeParamName = "HD_Phạm Vi Bóc";

        /// <summary>
        /// Chuẩn hóa chuỗi tìm kiếm không dấu, chữ thường
        /// </summary>
        public static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            string normalized = text.Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder();
            foreach (char c in normalized)
            {
                UnicodeCategory uc = CharUnicodeInfo.GetUnicodeCategory(c);
                if (uc != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }
            return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        }

        /// <summary>
        /// Kiểm tra tên parameter có phải là biến thể của "Tên phụ kiện" hay không
        /// </summary>
        public static bool IsFittingNameParam(string paramName)
        {
            if (string.IsNullOrEmpty(paramName)) return false;
            string clean = RemoveDiacritics(paramName).Replace(" ", "").Replace("_", "").Replace("-", "");
            return clean.Contains("tenphukien") || clean.Contains("tenpk") || clean.Contains("tenphu") ||
                   clean.Contains("tenong") || clean.Contains("tenvattu") || clean.Contains("mota") ||
                   clean.Contains("description");
        }

        /// <summary>
        /// Lấy giá trị chuỗi an toàn từ Parameter
        /// </summary>
        private static string GetParamStringValue(Parameter p)
        {
            if (p == null || !p.HasValue) return string.Empty;
            switch (p.StorageType)
            {
                case StorageType.String:
                    string s = p.AsString();
                    if (string.IsNullOrEmpty(s)) s = p.AsValueString();
                    return s ?? string.Empty;
                case StorageType.ElementId:
                    return p.AsValueString() ?? string.Empty;
                case StorageType.Double:
                    return p.AsValueString() ?? p.AsDouble().ToString(CultureInfo.InvariantCulture);
                case StorageType.Integer:
                    return p.AsValueString() ?? p.AsInteger().ToString(CultureInfo.InvariantCulture);
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Tìm parameter theo danh sách tên ưu tiên (Target nào đứng trước thì được ưu tiên tuyệt đối trước)
        /// </summary>
        private static string FindParamValue(Element elem, Element typeElem, string[] targetNames)
        {
            if (targetNames == null || targetNames.Length == 0) return string.Empty;

            // 1. Duyệt theo thứ tự ưu tiên tuyệt đối của targetNames trên Instance (elem) trước
            if (elem != null)
            {
                foreach (string target in targetNames)
                {
                    if (string.IsNullOrEmpty(target)) continue;

                    // Thử LookupParameter trực tiếp (nhanh và chính xác nhất)
                    try
                    {
                        Parameter pDirect = elem.LookupParameter(target);
                        if (pDirect != null && pDirect.HasValue)
                        {
                            // Tránh lấy nhầm diện tích hình học (StorageType.Double với đơn vị m²) khi tìm Area/Location by Area
                            if (target.IndexOf("Area", StringComparison.OrdinalIgnoreCase) >= 0 && pDirect.StorageType == StorageType.Double)
                            {
                                // Bỏ qua vì đây là diện tích hình học m², không phải tên khu vực!
                            }
                            else
                            {
                                string v = GetParamStringValue(pDirect);
                                if (!string.IsNullOrEmpty(v) && !string.IsNullOrEmpty(v.Trim()))
                                {
                                    return v.Trim();
                                }
                            }
                        }
                    }
                    catch { }

                    // Quét qua danh sách parameters của elem để tìm case-insensitive hoặc không dấu
                    foreach (Parameter p in elem.Parameters)
                    {
                        if (p != null && p.Definition != null && !string.IsNullOrEmpty(p.Definition.Name))
                        {
                            string pName = p.Definition.Name.Trim();
                            if (pName.Equals(target, StringComparison.OrdinalIgnoreCase) ||
                                RemoveDiacritics(pName) == RemoveDiacritics(target))
                            {
                                if (target.IndexOf("Area", StringComparison.OrdinalIgnoreCase) >= 0 && p.StorageType == StorageType.Double)
                                {
                                    continue;
                                }

                                string v = GetParamStringValue(p);
                                if (!string.IsNullOrEmpty(v) && !string.IsNullOrEmpty(v.Trim()))
                                {
                                    return v.Trim();
                                }
                            }
                        }
                    }
                }
            }

            // 2. Nếu Instance không có, duyệt theo thứ tự ưu tiên trên Type (typeElem)
            if (typeElem != null)
            {
                foreach (string target in targetNames)
                {
                    if (string.IsNullOrEmpty(target)) continue;

                    try
                    {
                        Parameter pDirect = typeElem.LookupParameter(target);
                        if (pDirect != null && pDirect.HasValue)
                        {
                            if (target.IndexOf("Area", StringComparison.OrdinalIgnoreCase) >= 0 && pDirect.StorageType == StorageType.Double)
                            {
                                // Bỏ qua
                            }
                            else
                            {
                                string v = GetParamStringValue(pDirect);
                                if (!string.IsNullOrEmpty(v) && !string.IsNullOrEmpty(v.Trim()))
                                {
                                    return v.Trim();
                                }
                            }
                        }
                    }
                    catch { }

                    foreach (Parameter p in typeElem.Parameters)
                    {
                        if (p != null && p.Definition != null && !string.IsNullOrEmpty(p.Definition.Name))
                        {
                            string pName = p.Definition.Name.Trim();
                            if (pName.Equals(target, StringComparison.OrdinalIgnoreCase) ||
                                RemoveDiacritics(pName) == RemoveDiacritics(target))
                            {
                                if (target.IndexOf("Area", StringComparison.OrdinalIgnoreCase) >= 0 && p.StorageType == StorageType.Double)
                                {
                                    continue;
                                }

                                string v = GetParamStringValue(p);
                                if (!string.IsNullOrEmpty(v) && !string.IsNullOrEmpty(v.Trim()))
                                {
                                    return v.Trim();
                                }
                            }
                        }
                    }
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Trích xuất tên phụ kiện gộp từ 3 (hoặc nhiều) parameters khác nhau của cùng 1 đối tượng
        /// </summary>
        public static void ExtractFittingName(Element elem, Element typeElem, out string mergedName, out string sourceParamName)
        {
            mergedName = string.Empty;
            sourceParamName = string.Empty;

            List<Parameter> candidateParams = new List<Parameter>();

            // 1. Quét Instance parameters
            if (elem != null)
            {
                foreach (Parameter p in elem.Parameters)
                {
                    if (p != null && p.Definition != null && !string.IsNullOrEmpty(p.Definition.Name))
                    {
                        if (IsFittingNameParam(p.Definition.Name))
                        {
                            candidateParams.Add(p);
                        }
                    }
                }
            }

            // 2. Quét Type parameters
            if (typeElem != null)
            {
                foreach (Parameter p in typeElem.Parameters)
                {
                    if (p != null && p.Definition != null && !string.IsNullOrEmpty(p.Definition.Name))
                    {
                        if (IsFittingNameParam(p.Definition.Name))
                        {
                            candidateParams.Add(p);
                        }
                    }
                }
            }

            // 3. Tìm parameter đầu tiên có giá trị không rỗng
            foreach (Parameter cp in candidateParams)
            {
                string val = GetParamStringValue(cp);
                if (!string.IsNullOrEmpty(val) && !string.IsNullOrEmpty(val.Trim()))
                {
                    mergedName = val.Trim();
                    sourceParamName = cp.Definition.Name;
                    return;
                }
            }

            // 4. Fallback 1: Thử tìm trong Type Name hoặc Family Name
            if (typeElem != null)
            {
                FamilySymbol sym = typeElem as FamilySymbol;
                if (sym != null)
                {
                    if (!string.IsNullOrEmpty(sym.Name))
                    {
                        mergedName = sym.Name;
                        sourceParamName = "Type Name";
                        return;
                    }
                    if (sym.Family != null && !string.IsNullOrEmpty(sym.Family.Name))
                    {
                        mergedName = sym.Family.Name;
                        sourceParamName = "Family Name";
                        return;
                    }
                }
                else if (!string.IsNullOrEmpty(typeElem.Name))
                {
                    mergedName = typeElem.Name;
                    sourceParamName = "Type Name";
                    return;
                }
            }

            // 5. Fallback 2: Tên Element
            if (!string.IsNullOrEmpty(elem.Name))
            {
                mergedName = elem.Name;
                sourceParamName = "Element Name";
                return;
            }

            mergedName = "Phụ Kiện Chưa Đặt Tên";
            sourceParamName = "None";
        }

        /// <summary>
        /// Chuẩn hóa tên đường ống ngắn gọn, chuyên nghiệp:
        /// Loại bỏ các chuỗi rườm rà "tiêu chuẩn ISO...", chỉ giữ lại Tên Ống + Kích Thước (VD: "Ống uPVC D110")
        /// </summary>
        public static string FormatCleanPipeName(string rawName, string size, string typeName)
        {
            // 1. Xác định chủng loại vật liệu ống (uPVC, HDPE, PPR, Thép...)
            string material = "uPVC";
            string combined = ((rawName ?? "") + " " + (typeName ?? "")).ToUpperInvariant();

            if (combined.Contains("UPVC") || combined.Contains("U.PVC") || combined.Contains("PVC"))
            {
                material = "uPVC";
            }
            else if (combined.Contains("HDPE"))
            {
                material = "HDPE";
            }
            else if (combined.Contains("PPR"))
            {
                material = "PPR";
            }
            else if (combined.Contains("THEP") || combined.Contains("STEEL") || combined.Contains("GI"))
            {
                material = "Thép";
            }
            else if (!string.IsNullOrEmpty(rawName) && !rawName.Contains("tiêu chuẩn") && !rawName.Contains("ISO"))
            {
                material = rawName.Trim();
            }

            // 2. Trích xuất kích thước đường kính ống (VD: 110ø -> D110)
            string cleanSize = "";
            if (!string.IsNullOrEmpty(size) && size != "-")
            {
                string s = size.Replace("ø", "").Replace("Ø", "").Replace("mm", "").Trim();
                if (s.Contains("-"))
                {
                    s = s.Split('-')[0].Trim();
                }

                if (!string.IsNullOrEmpty(s))
                {
                    if (s.StartsWith("D", StringComparison.OrdinalIgnoreCase) ||
                        s.StartsWith("DN", StringComparison.OrdinalIgnoreCase))
                    {
                        cleanSize = s.ToUpperInvariant();
                    }
                    else
                    {
                        cleanSize = "D" + s;
                    }
                }
            }

            // 3. Kết hợp tên hoàn chỉnh
            if (!string.IsNullOrEmpty(cleanSize))
            {
                return string.Format("Ống {0} {1}", material, cleanSize);
            }
            return string.Format("Ống {0}", material);
        }

        /// <summary>
        /// Trích xuất kích thước Size của ống hoặc phụ kiện
        /// </summary>
        public static string ExtractSize(Element elem, Element typeElem)
        {
            // Thử BuiltInParameter trước
            if (elem != null)
            {
                Parameter pSize = elem.get_Parameter(BuiltInParameter.RBS_CALCULATED_SIZE);
                if (pSize != null && pSize.HasValue)
                {
                    string s = pSize.AsString();
                    if (!string.IsNullOrEmpty(s)) return s.Trim();
                }
            }

            string[] sizeNames = new string[] { "Size", "Kích Thước", "Kích cỡ", "Đường Kính", "Diameter", "Size_Sch" };
            string found = FindParamValue(elem, typeElem, sizeNames);
            if (!string.IsNullOrEmpty(found)) return found;

            // Nếu là ống Pipe
            Pipe pipe = elem as Pipe;
            if (pipe != null)
            {
                Parameter pDia = pipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
                if (pDia != null && pDia.HasValue)
                {
                    return pDia.AsValueString();
                }
            }

            return "-";
        }

        /// <summary>
        /// Trích xuất thông tin Khu vực (Location by Area)
        /// Ưu tiên tuyệt đối parameter dự án "Location by Area", loại bỏ hoàn toàn việc nhầm với diện tích hình học (m²)
        /// </summary>
        public static string ExtractArea(Element elem, Element typeElem)
        {
            // BƯỚC 1: Ưu tiên cao nhất: Lấy đúng tham số "Location by Area"
            string[] exactAreaNames = new string[] { "Location by Area", "Location By Area" };
            string found = FindParamValue(elem, typeElem, exactAreaNames);
            if (!string.IsNullOrEmpty(found)) return found;

            // BƯỚC 2: Thử các tham số tiếng Việt nếu có
            string[] viAreaNames = new string[] { "Khu Vực", "Khu vuc", "Vị Trí Khu Vực", "Vi tri khu vuc" };
            string foundVi = FindParamValue(elem, typeElem, viAreaNames);
            if (!string.IsNullOrEmpty(foundVi)) return foundVi;

            return "-";
        }

        /// <summary>
        /// Trích xuất thông tin Tầng (Location by Level)
        /// Ưu tiên tuyệt đối parameter dự án "Location by Level" trùng khớp 100% với thuộc tính trong Revit
        /// </summary>
        public static string ExtractLevel(Element elem, Element typeElem)
        {
            // BƯỚC 1: Ưu tiên cao nhất: Lấy đúng tham số "Location by Level" (như trong bảng Properties của Revit)
            string[] exactLvlNames = new string[] { "Location by Level", "Location By Level" };
            string found = FindParamValue(elem, typeElem, exactLvlNames);
            if (!string.IsNullOrEmpty(found)) return found;

            // BƯỚC 2: Thử các tham số tiếng Việt khác nếu có
            string[] viLvlNames = new string[] { "Level_Location", "Tầng", "Tang", "Cao Độ", "Cao do" };
            string foundVi = FindParamValue(elem, typeElem, viLvlNames);
            if (!string.IsNullOrEmpty(foundVi)) return foundVi;

            // BƯỚC 3: Fallback cuối cùng nếu không có tham số tùy chỉnh: Lấy Level gốc của Revit
            if (elem != null)
            {
                Parameter pLvl = elem.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM);
                if (pLvl != null && pLvl.HasValue)
                {
                    string s = pLvl.AsValueString();
                    if (!string.IsNullOrEmpty(s)) return s;
                }

                Parameter pRefLvl = elem.get_Parameter(BuiltInParameter.RBS_START_LEVEL_PARAM);
                if (pRefLvl != null && pRefLvl.HasValue)
                {
                    string s = pRefLvl.AsValueString();
                    if (!string.IsNullOrEmpty(s)) return s;
                }

                Parameter pDirectLvl = elem.LookupParameter("Level");
                if (pDirectLvl != null && pDirectLvl.HasValue)
                {
                    string s = GetParamStringValue(pDirectLvl);
                    if (!string.IsNullOrEmpty(s)) return s;
                }
            }

            return "-";
        }

        /// <summary>
        /// Trích xuất thông tin Áp lực (PN)
        /// </summary>
        public static string ExtractPN(Element elem, Element typeElem)
        {
            string[] pnNames = new string[] { "PN", "Áp Lực", "Ap Luc", "Pressure", "Pressure Rating" };
            string found = FindParamValue(elem, typeElem, pnNames);
            return string.IsNullOrEmpty(found) ? "" : found;
        }

        /// <summary>
        /// Trích xuất thông tin Phân loại Hệ Thống (System Type)
        /// </summary>
        public static string ExtractSystemType(Element elem)
        {
            if (elem == null) return "-";

            Parameter pSys = elem.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM);
            if (pSys != null && pSys.HasValue)
            {
                string s = pSys.AsValueString();
                if (!string.IsNullOrEmpty(s)) return s;
            }

            Parameter pSysClass = elem.get_Parameter(BuiltInParameter.RBS_SYSTEM_CLASSIFICATION_PARAM);
            if (pSysClass != null && pSysClass.HasValue)
            {
                string s = pSysClass.AsString();
                if (!string.IsNullOrEmpty(s)) return s;
            }

            MEPCurve mepCurve = elem as MEPCurve;
            if (mepCurve != null && mepCurve.MEPSystem != null)
            {
                return mepCurve.MEPSystem.Name ?? "-";
            }

            return "-";
        }

        /// <summary>
        /// Quét tất cả các đối tượng phụ kiện ống & đường ống trong View, Document hoặc Vùng Quét Chọn Cục Bộ,
        /// tự động gộp tên và nhóm lại theo từng dòng hoàn chỉnh
        /// </summary>
                /// <summary>
        /// Quét toàn bộ ElementId ống nước & phụ kiện thuộc một View mô hình
        /// </summary>
        public static List<ElementId> GetModelElementIdsFromView(Document doc, Autodesk.Revit.DB.View targetView, bool incPipes, bool incFittings)
        {
            List<ElementId> ids = new List<ElementId>();
            if (doc == null || targetView == null) return ids;

            if (incPipes)
            {
                try
                {
                    var pCol = new FilteredElementCollector(doc, targetView.Id)
                        .OfCategory(BuiltInCategory.OST_PipeCurves)
                        .WhereElementIsNotElementType()
                        .ToElementIds();
                    ids.AddRange(pCol);
                }
                catch { }
            }

            if (incFittings)
            {
                try
                {
                    var fCol = new FilteredElementCollector(doc, targetView.Id)
                        .OfCategory(BuiltInCategory.OST_PipeFitting)
                        .WhereElementIsNotElementType()
                        .ToElementIds();
                    ids.AddRange(fCol);

                    var aCol = new FilteredElementCollector(doc, targetView.Id)
                        .OfCategory(BuiltInCategory.OST_PipeAccessory)
                        .WhereElementIsNotElementType()
                        .ToElementIds();
                    ids.AddRange(aCol);
                }
                catch { }
            }

            return ids.Distinct().ToList();
        }

        public static List<MepFittingItem> ScanAndGroupElements(
            Document doc,
            Autodesk.Revit.DB.View activeView,
            bool currentViewOnly,
            bool includeFittings,
            bool includePipes,
            string filterArea = null,
            string filterLevel = null,
            string filterSystem = null,
            string searchText = null,
            ICollection<ElementId> customElementIds = null,
            bool discretePipes = true)
        {
            List<MepFittingItem> result = new List<MepFittingItem>();
            if (doc == null) return result;

            if (customElementIds != null && customElementIds.Count == 0)
            {
                return result; // Quét vùng cục bộ nhưng không có đối tượng nào
            }

            Dictionary<string, MepFittingItem> groupDict = new Dictionary<string, MepFittingItem>(StringComparer.OrdinalIgnoreCase);

            // A. NẾU LÀ QUÉT VÙNG CỤC BỘ: Duyệt trực tiếp 100% qua danh sách ElementId đã quét chọn
            if (customElementIds != null && customElementIds.Count > 0)
            {
                ElementId catPipeFitting = new ElementId(BuiltInCategory.OST_PipeFitting);
                ElementId catPipeCurves = new ElementId(BuiltInCategory.OST_PipeCurves);
                ElementId catPipeAccessory = new ElementId(BuiltInCategory.OST_PipeAccessory);

                foreach (ElementId id in customElementIds)
                {
                    Element el = doc.GetElement(id);
                    if (el == null || el.Category == null) continue;
                    ElementId catId = el.Category.Id;

                    if (catId == catPipeFitting && includeFittings)
                    {
                        ProcessFittingElement(doc, el, groupDict, "Phụ Kiện Ống");
                    }
                    else if (catId == catPipeCurves && includePipes)
                    {
                        ProcessPipeElement(doc, el, groupDict, discretePipes);
                    }
                    else if (catId == catPipeAccessory && includeFittings)
                    {
                        ProcessFittingElement(doc, el, groupDict, "Phụ Kiện Van");
                    }
                }
            }
            else
            {
                // B. QUÃ‰T THEO VIEW HOáº¶C TOÃ€N Bá»˜ Dá»° ÃN
                // Tá»± Ä‘á»™ng nháº­n diá»‡n náº¿u ActiveView lÃ  Sheet: QuÃ©t gom toÃ n bá»™ Model Viewports trÃªn Sheet Ä‘Ã³
                List<Autodesk.Revit.DB.View> viewsToScan = new List<Autodesk.Revit.DB.View>();
                bool isSheetScan = currentViewOnly && activeView is ViewSheet;

                if (isSheetScan)
                {
                    ViewSheet vs = activeView as ViewSheet;
                    ICollection<ElementId> vpIds = vs.GetAllViewports();
                    foreach (ElementId vpid in vpIds)
                    {
                        Viewport vp = doc.GetElement(vpid) as Viewport;
                        if (vp != null)
                        {
                            Autodesk.Revit.DB.View childView = doc.GetElement(vp.ViewId) as Autodesk.Revit.DB.View;
                            if (childView != null &&
                                childView.ViewType != ViewType.Legend &&
                                childView.ViewType != ViewType.DraftingView &&
                                childView.ViewType != ViewType.Schedule &&
                                childView.ViewType != ViewType.CostReport)
                            {
                                viewsToScan.Add(childView);
                            }
                        }
                    }
                }
                else if (currentViewOnly && activeView != null)
                {
                    viewsToScan.Add(activeView);
                }

                // 1. Phá»¥ Kiá»‡n á»ng & Van (Pipe Fittings & Accessories)
                if (includeFittings)
                {
                    if (currentViewOnly)
                    {
                        foreach (Autodesk.Revit.DB.View targetV in viewsToScan)
                        {
                            FilteredElementCollector fittingCol = new FilteredElementCollector(doc, targetV.Id);
                            IList<Element> fittings = fittingCol
                                .OfCategory(BuiltInCategory.OST_PipeFitting)
                                .WhereElementIsNotElementType()
                                .ToElements();

                            foreach (Element el in fittings)
                            {
                                ProcessFittingElement(doc, el, groupDict, "Phá»¥ Kiá»‡n á»ng");
                            }

                            FilteredElementCollector accCol = new FilteredElementCollector(doc, targetV.Id);
                            IList<Element> accessories = accCol
                                .OfCategory(BuiltInCategory.OST_PipeAccessory)
                                .WhereElementIsNotElementType()
                                .ToElements();

                            foreach (Element el in accessories)
                            {
                                ProcessFittingElement(doc, el, groupDict, "Phá»¥ Kiá»‡n Van");
                            }
                        }
                    }
                    else
                    {
                        FilteredElementCollector fittingCol = new FilteredElementCollector(doc);
                        IList<Element> fittings = fittingCol
                            .OfCategory(BuiltInCategory.OST_PipeFitting)
                            .WhereElementIsNotElementType()
                            .ToElements();

                        foreach (Element el in fittings)
                        {
                            ProcessFittingElement(doc, el, groupDict, "Phá»¥ Kiá»‡n á»ng");
                        }

                        FilteredElementCollector accCol = new FilteredElementCollector(doc);
                        IList<Element> accessories = accCol
                            .OfCategory(BuiltInCategory.OST_PipeAccessory)
                            .WhereElementIsNotElementType()
                            .ToElements();

                        foreach (Element el in accessories)
                            ProcessFittingElement(doc, el, groupDict, "Phá»¥ Kiá»‡n Van");
                    }
                }

                // 2. ÄÆ°á»ng á»ng (Pipes)
                if (includePipes)
                {
                    if (currentViewOnly)
                    {
                        foreach (Autodesk.Revit.DB.View targetV in viewsToScan)
                        {
                            FilteredElementCollector pipeCol = new FilteredElementCollector(doc, targetV.Id);
                            IList<Element> pipes = pipeCol
                                .OfCategory(BuiltInCategory.OST_PipeCurves)
                                .WhereElementIsNotElementType()
                                .ToElements();

                            foreach (Element el in pipes)
                            {
                                ProcessPipeElement(doc, el, groupDict, discretePipes);
                            }
                        }
                    }
                    else
                    {
                        FilteredElementCollector pipeCol = new FilteredElementCollector(doc);
                        IList<Element> pipes = pipeCol
                            .OfCategory(BuiltInCategory.OST_PipeCurves)
                            .WhereElementIsNotElementType()
                            .ToElements();

                        foreach (Element el in pipes)
                        {
                            ProcessPipeElement(doc, el, groupDict, discretePipes);
                        }
                    }
                }
            }

            // 3. Lọc theo các tiêu chí giao diện
            string searchClean = RemoveDiacritics(searchText ?? "");
            foreach (MepFittingItem item in groupDict.Values)
            {
                if (!string.IsNullOrEmpty(filterArea) && filterArea != "Tất cả" &&
                    !item.LocationByArea.Equals(filterArea, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(filterLevel) && filterLevel != "Tất cả" &&
                    !item.LocationByLevel.Equals(filterLevel, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(filterSystem) && filterSystem != "Tất cả" &&
                    !item.SystemType.Equals(filterSystem, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(searchClean))
                {
                    string target = RemoveDiacritics(string.Format("{0} {1} {2} {3} {4} {5}",
                        item.MergedName, item.Size, item.TypeName, item.LocationByArea, item.LocationByLevel, item.SystemType));
                    if (!target.Contains(searchClean))
                    {
                        continue;
                    }
                }

                result.Add(item);
            }

            // 4. Sắp xếp: Tên Phụ Kiện -> Size -> Tầng
            result = result
                .OrderBy(x => x.CategoryKind)
                .ThenBy(x => x.MergedName)
                .ThenBy(x => x.Size)
                .ThenBy(x => x.LocationByLevel)
                .ToList();

            // 5. Đánh số STT liên tục
            for (int i = 0; i < result.Count; i++)
            {
                result[i].STT = i + 1;
            }

            return result;
        }

        private static void ProcessFittingElement(Document doc, Element el, Dictionary<string, MepFittingItem> groupDict, string categoryKind)
        {
            if (el == null) return;
            Element typeEl = doc.GetElement(el.GetTypeId());

            string mergedName;
            string sourceParam;
            ExtractFittingName(el, typeEl, out mergedName, out sourceParam);

            string size = ExtractSize(el, typeEl);
            string area = ExtractArea(el, typeEl);
            string level = ExtractLevel(el, typeEl);
            string pn = ExtractPN(el, typeEl);
            string systemType = ExtractSystemType(el);
            string typeName = (typeEl != null && !string.IsNullOrEmpty(typeEl.Name)) ? typeEl.Name : "Standard";

            MepFittingItem proto = new MepFittingItem();
            proto.MergedName = mergedName;
            proto.SourceParamName = sourceParam;
            proto.Size = size;
            proto.TypeName = typeName;
            proto.LocationByArea = area;
            proto.PN = pn;
            proto.LocationByLevel = level;
            proto.SystemType = systemType;
            proto.CategoryKind = categoryKind;

            string key = proto.GetGroupingKey();
            MepFittingItem existing;
            if (!groupDict.TryGetValue(key, out existing))
            {
                existing = proto;
                existing.Count = 1;
                existing.ElementIds.Add(el.Id);
                groupDict[key] = existing;
            }
            else
            {
                existing.Count++;
                existing.ElementIds.Add(el.Id);
            }
        }

        private static void ProcessPipeElement(Document doc, Element el, Dictionary<string, MepFittingItem> groupDict, bool discretePipes)
        {
            if (el == null) return;
            Element typeEl = doc.GetElement(el.GetTypeId());

            string pipeName;
            string sourceParam;
            ExtractFittingName(el, typeEl, out pipeName, out sourceParam);
            if (string.IsNullOrEmpty(pipeName) || pipeName.Equals("Phụ Kiện Chưa Đặt Tên", StringComparison.OrdinalIgnoreCase))
            {
                pipeName = (typeEl != null && !string.IsNullOrEmpty(typeEl.Name)) ? typeEl.Name : "Ống nước";
                sourceParam = "Pipe Type";
            }

            string size = ExtractSize(el, typeEl);
            string area = ExtractArea(el, typeEl);
            string level = ExtractLevel(el, typeEl);
            string pn = ExtractPN(el, typeEl);
            string systemType = ExtractSystemType(el);

            string pipeTypeName = (typeEl != null && !string.IsNullOrEmpty(typeEl.Name)) ? typeEl.Name : "Standard";

            // Chuẩn hóa tên đường ống ngắn gọn, bỏ tiêu chuẩn rườm rà (VD: "Ống uPVC D110")
            string cleanPipeName = FormatCleanPipeName(pipeName, size, pipeTypeName);

            double lengthM = 0.0;
            Parameter pLen = el.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
            if (pLen != null && pLen.HasValue)
            {
                lengthM = Math.Round(pLen.AsDouble() * 0.3048, 2);
            }

            MepFittingItem proto = new MepFittingItem();
            proto.MergedName = cleanPipeName;
            proto.SourceParamName = sourceParam;
            proto.Size = size;
            proto.TypeName = pipeTypeName;
            proto.LocationByArea = area;
            proto.PN = pn;
            proto.LocationByLevel = level;
            proto.SystemType = systemType;
            proto.CategoryKind = "Đường Ống";

            string key;
            if (discretePipes)
            {
                // Theo lệnh Sếp Hồ: Tách rời rạc theo chiều dài (không SUM mét dài)
                // Các đoạn có chiều dài khác nhau sẽ tách ra từng dòng riêng biệt!
                key = proto.GetGroupingKey() + "|L:" + lengthM.ToString("0.00");
                MepFittingItem existing;
                if (!groupDict.TryGetValue(key, out existing))
                {
                    existing = proto;
                    existing.Count = 1;
                    existing.LengthM = lengthM; // Chiều dài thực tế của đoạn đó
                    existing.TotalLengthM = lengthM;
                    existing.ElementIds.Add(el.Id);
                    groupDict[key] = existing;
                }
                else
                {
                    existing.Count++;
                    existing.LengthM = lengthM; // Giữ nguyên chiều dài đoạn (không sum vào LengthM)
                    existing.TotalLengthM = Math.Round(existing.TotalLengthM + lengthM, 2);
                    existing.ElementIds.Add(el.Id);
                }
            }
            else
            {
                // Chế độ cũ: Gom tổng mét dài (SUM)
                key = proto.GetGroupingKey();
                MepFittingItem existing;
                if (!groupDict.TryGetValue(key, out existing))
                {
                    existing = proto;
                    existing.Count = 1;
                    existing.LengthM = lengthM;
                    existing.TotalLengthM = lengthM;
                    existing.ElementIds.Add(el.Id);
                    groupDict[key] = existing;
                }
                else
                {
                    existing.Count++;
                    existing.LengthM = Math.Round(existing.LengthM + lengthM, 2);
                    existing.TotalLengthM = existing.LengthM;
                    existing.ElementIds.Add(el.Id);
                }
            }
        }

        /// <summary>
        /// Đảm bảo Project Parameter tùy chỉnh tồn tại và được bind vào Category Pipe & Pipe Fitting
        /// </summary>
        public static bool EnsureCustomProjectParameter(RevitApp app, Document doc, string paramName, string description = "HDTOOL Parameter")
        {
            if (app == null || doc == null || string.IsNullOrEmpty(paramName)) return false;

            // 1. Kiểm tra xem parameter đã được bind chưa
            BindingMap map = doc.ParameterBindings;
            DefinitionBindingMapIterator it = map.ForwardIterator();
            while (it.MoveNext())
            {
                if (it.Key != null && it.Key.Name.Equals(paramName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // 2. Tạo Shared Parameter file tạm thời
            string tempFile = Path.Combine(Path.GetTempPath(), "hdtool_param_" + Guid.NewGuid().ToString("N") + ".txt");
            string origShared = null;
            try
            {
                origShared = app.SharedParametersFilename;
            }
            catch { }

            try
            {
                Guid pGuid = Guid.NewGuid();
                string content = "# This is a Revit shared parameter file.\r\n" +
                                 "*META\tVERSION\tMINVERSION\r\n2\t1\r\n" +
                                 "*GROUP\tID\tNAME\r\n1\tHDTOOL\r\n" +
                                 "*PARAM\tGUID\tNAME\tDATATYPE\tDATACATEGORY\tGROUP\tVISIBLE\tDESCRIPTION\tUSERMODIFIABLE\tHIDEWHENNOVALUE\r\n" +
                                 "PARAM\t" + pGuid.ToString() + "\t" + paramName + "\tTEXT\t\t1\t1\t" + description + "\t1\t0\r\n";

                File.WriteAllText(tempFile, content, Encoding.UTF8);

                app.SharedParametersFilename = tempFile;
                DefinitionFile defFile = app.OpenSharedParameterFile();
                if (defFile == null) return false;

                DefinitionGroup defGroup = defFile.Groups.get_Item("HDTOOL");
                if (defGroup == null) return false;

                Definition def = defGroup.Definitions.get_Item(paramName);
                if (def == null) return false;

                CategorySet catSet = app.Create.NewCategorySet();
                Category catFitting = doc.Settings.Categories.get_Item(BuiltInCategory.OST_PipeFitting);
                if (catFitting != null) catSet.Insert(catFitting);
                Category catPipe = doc.Settings.Categories.get_Item(BuiltInCategory.OST_PipeCurves);
                if (catPipe != null) catSet.Insert(catPipe);
                Category catAccessory = doc.Settings.Categories.get_Item(BuiltInCategory.OST_PipeAccessory);
                if (catAccessory != null) catSet.Insert(catAccessory);

                InstanceBinding binding = app.Create.NewInstanceBinding(catSet);
                return doc.ParameterBindings.Insert(def, binding, GroupTypeId.IdentityData);
            }
            catch
            {
                return false;
            }
            finally
            {
                try
                {
                    if (origShared != null) app.SharedParametersFilename = origShared;
                }
                catch { }

                try
                {
                    if (File.Exists(tempFile)) File.Delete(tempFile);
                }
                catch { }
            }
        }

        /// <summary>
        /// Đảm bảo Project Parameter thống nhất "HD_Tên Phụ Kiện" tồn tại trong Document
        /// </summary>
        public static bool EnsureUnifiedProjectParameter(RevitApp app, Document doc, string paramName = DefaultUnifiedParamName)
        {
            return EnsureCustomProjectParameter(app, doc, paramName, "HDTOOL Unified Fitting Name");
        }

        /// <summary>
        /// Đồng bộ giá trị tên gộp vào parameter "HD_Tên Phụ Kiện" cho tất cả phụ kiện & ống trong dự án hoặc vùng chọn
        /// Giữ nguyên 100% các parameter gốc!
        /// </summary>
        public static bool SyncUnifiedParameterValues(
            RevitApp app,
            Document doc,
            bool currentViewOnly,
            Autodesk.Revit.DB.View activeView,
            out int updatedCount,
            string paramName = DefaultUnifiedParamName,
            ICollection<ElementId> customElementIds = null)
        {
            updatedCount = 0;
            if (doc == null) return false;

            // 1. Đảm bảo parameter tồn tại
            using (Transaction txInit = new Transaction(doc, "HDTOOL - Khởi Tạo Tham Số Thống Kê Gộp"))
            {
                txInit.Start();
                EnsureUnifiedProjectParameter(app, doc, paramName);
                txInit.Commit();
            }

            // 2. Quét và gán giá trị
            using (Transaction tx = new Transaction(doc, "HDTOOL - Đồng Bộ Tên Phụ Kiện Gộp"))
            {
                tx.Start();

                if (customElementIds != null && customElementIds.Count > 0)
                {
                    foreach (ElementId id in customElementIds)
                    {
                        Element el = doc.GetElement(id);
                        if (el == null) continue;
                        Element typeEl = doc.GetElement(el.GetTypeId());

                        string mergedName;
                        string sourceParam;
                        ExtractFittingName(el, typeEl, out mergedName, out sourceParam);

                        if (el.Category != null && el.Category.Id == new ElementId(BuiltInCategory.OST_PipeCurves))
                        {
                            string size = ExtractSize(el, typeEl);
                            string typeName = (typeEl != null && !string.IsNullOrEmpty(typeEl.Name)) ? typeEl.Name : "Standard";
                            mergedName = FormatCleanPipeName(mergedName, size, typeName);
                        }
                        else
                        {
                            if (string.IsNullOrEmpty(mergedName) || mergedName.Equals("Phụ Kiện Chưa Đặt Tên", StringComparison.OrdinalIgnoreCase))
                            {
                                mergedName = (typeEl != null && !string.IsNullOrEmpty(typeEl.Name)) ? typeEl.Name : "Phụ kiện";
                            }
                        }

                        Parameter pTarget = el.LookupParameter(paramName);
                        if (pTarget != null && !pTarget.IsReadOnly)
                        {
                            pTarget.Set(mergedName);
                            updatedCount++;
                        }
                    }
                }
                else
                {
                    FilteredElementCollector fittingCol = currentViewOnly && activeView != null
                        ? new FilteredElementCollector(doc, activeView.Id)
                        : new FilteredElementCollector(doc);

                    IList<Element> fittings = fittingCol
                        .OfCategory(BuiltInCategory.OST_PipeFitting)
                        .WhereElementIsNotElementType()
                        .ToElements();

                    foreach (Element el in fittings)
                    {
                        if (el == null) continue;
                        Element typeEl = doc.GetElement(el.GetTypeId());

                        string mergedName;
                        string sourceParam;
                        ExtractFittingName(el, typeEl, out mergedName, out sourceParam);

                        Parameter pTarget = el.LookupParameter(paramName);
                        if (pTarget != null && !pTarget.IsReadOnly)
                        {
                            pTarget.Set(mergedName);
                            updatedCount++;
                        }
                    }

                    // Cập nhật cho cả đường ống
                    FilteredElementCollector pipeCol = currentViewOnly && activeView != null
                        ? new FilteredElementCollector(doc, activeView.Id)
                        : new FilteredElementCollector(doc);

                    IList<Element> pipes = pipeCol
                        .OfCategory(BuiltInCategory.OST_PipeCurves)
                        .WhereElementIsNotElementType()
                        .ToElements();

                    foreach (Element el in pipes)
                    {
                        if (el == null) continue;
                        Element typeEl = doc.GetElement(el.GetTypeId());
                        string rawName;
                        string sourceParam;
                        ExtractFittingName(el, typeEl, out rawName, out sourceParam);
                        string size = ExtractSize(el, typeEl);
                        string typeName = (typeEl != null && !string.IsNullOrEmpty(typeEl.Name)) ? typeEl.Name : "Standard";
                        string pipeName = FormatCleanPipeName(rawName, size, typeName);

                        Parameter pTarget = el.LookupParameter(paramName);
                        if (pTarget != null && !pTarget.IsReadOnly)
                        {
                            pTarget.Set(pipeName);
                            updatedCount++;
                        }
                    }
                }

                tx.Commit();
            }

            return true;
        }

        /// <summary>
        /// Tạo bảng ViewSchedule chuẩn trong Revit với 1 cột Tên Phụ Kiện duy nhất (đã gộp),
        /// cấu hình Grouping & Grand Total, và đặt ngay ngắn lên Sheet được chọn hoặc tạo sẵn để kéo thả
        /// </summary>
        public static bool CreateScheduleAndPlaceOnSheet(
            RevitApp app,
            Document doc,
            string scheduleName,
            ViewSheet targetSheet,
            XYZ placementPoint,
            out ViewSchedule createdSchedule,
            out string resultMessage,
            string unifiedParamName = DefaultUnifiedParamName,
            ICollection<ElementId> customElementIds = null,
            ScheduleColumnVisibilityOptions colOptions = null)
        {
            createdSchedule = null;
            resultMessage = string.Empty;

            if (doc == null)
            {
                resultMessage = "Tài liệu Revit không hợp lệ.";
                return false;
            }

            // 1. Đồng bộ parameter trước để đảm bảo Schedule có đủ dữ liệu
            int dummyCount;
            SyncUnifiedParameterValues(app, doc, false, null, out dummyCount, unifiedParamName, customElementIds);

            // 2. Nếu là bóc cục bộ: đảm bảo tham số HD_Phạm Vi Bóc tồn tại và gán nhãn
            bool isLocal = (customElementIds != null && customElementIds.Count > 0);
            if (isLocal)
            {
                using (Transaction txInit = new Transaction(doc, "HDTOOL - Khởi Tạo Tham Số Phạm Vi Bóc"))
                {
                    txInit.Start();
                    EnsureCustomProjectParameter(app, doc, DefaultScopeParamName, "HDTOOL Scope Filter");
                    txInit.Commit();
                }

                using (Transaction txScope = new Transaction(doc, "HDTOOL - Gán Nhãn Bóc Cục Bộ"))
                {
                    txScope.Start();
                    HashSet<ElementId> localSet = new HashSet<ElementId>(customElementIds);

                    FilteredElementCollector allFittings = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_PipeFitting)
                        .WhereElementIsNotElementType();

                    foreach (Element el in allFittings)
                    {
                        Parameter p = el.LookupParameter(DefaultScopeParamName);
                        if (p != null && !p.IsReadOnly)
                        {
                            p.Set(localSet.Contains(el.Id) ? "Cục Bộ" : string.Empty);
                        }
                    }

                    FilteredElementCollector allPipes = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_PipeCurves)
                        .WhereElementIsNotElementType();

                    foreach (Element el in allPipes)
                    {
                        Parameter p = el.LookupParameter(DefaultScopeParamName);
                        if (p != null && !p.IsReadOnly)
                        {
                            p.Set(localSet.Contains(el.Id) ? "Cục Bộ" : string.Empty);
                        }
                    }

                    FilteredElementCollector allAccessories = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_PipeAccessory)
                        .WhereElementIsNotElementType();

                    foreach (Element el in allAccessories)
                    {
                        Parameter p = el.LookupParameter(DefaultScopeParamName);
                        if (p != null && !p.IsReadOnly)
                        {
                            p.Set(localSet.Contains(el.Id) ? "Cục Bộ" : string.Empty);
                        }
                    }

                    txScope.Commit();
                }
            }

            using (Transaction tx = new Transaction(doc, "HDTOOL - Tạo Bảng Schedule Khối Lượng Phụ Kiện"))
            {
                tx.Start();

                try
                {
                    // 3. Tìm hoặc tạo ViewSchedule
                    ViewSchedule schedule = null;
                    FilteredElementCollector schedCol = new FilteredElementCollector(doc).OfClass(typeof(ViewSchedule));
                    foreach (ViewSchedule vs in schedCol.Cast<ViewSchedule>())
                    {
                        if (vs.Name.Equals(scheduleName, StringComparison.OrdinalIgnoreCase))
                        {
                            schedule = vs;
                            break;
                        }
                    }

                    if (schedule == null)
                    {
                        schedule = ViewSchedule.CreateSchedule(doc, new ElementId(BuiltInCategory.OST_PipeFitting));
                        try { schedule.Name = scheduleName; } catch { }
                    }

                    ScheduleDefinition def = schedule.Definition;

                    // Xóa các field cũ nếu đang tái sử dụng
                    IList<ScheduleFieldId> curFieldIds = def.GetFieldOrder();
                    for (int i = curFieldIds.Count - 1; i >= 0; i--)
                    {
                        try { def.RemoveField(curFieldIds[i]); } catch { }
                    }

                    // 4. Thêm các trường hiển thị (Scheduled Fields)
                    IList<SchedulableField> availableFields = def.GetSchedulableFields();
                    if (colOptions == null) colOptions = new ScheduleColumnVisibilityOptions();

                    // Field 1: Tên Phụ Kiện Gộp
                    SchedulableField sfName = FindSchedulableField(doc, availableFields, unifiedParamName);
                    ScheduleField fName = null;
                    if (sfName != null)
                    {
                        fName = def.AddField(sfName);
                        fName.ColumnHeading = "Tên Phụ Kiện";
                        fName.IsHidden = !colOptions.ShowName;
                    }

                    // Field 2: Size
                    SchedulableField sfSize = FindSchedulableField(doc, availableFields, "Size", BuiltInParameter.RBS_CALCULATED_SIZE);
                    ScheduleField fSize = null;
                    if (sfSize != null)
                    {
                        fSize = def.AddField(sfSize);
                        fSize.ColumnHeading = "Size";
                        fSize.IsHidden = !colOptions.ShowSize;
                    }

                    // Field 3: Count
                    SchedulableField sfCount = availableFields.FirstOrDefault(f => f.FieldType == ScheduleFieldType.Count);
                    ScheduleField fCount = null;
                    if (sfCount != null)
                    {
                        fCount = def.AddField(sfCount);
                        fCount.ColumnHeading = "Count";
                        fCount.DisplayType = ScheduleFieldDisplayType.Totals;
                        fCount.IsHidden = !colOptions.ShowCount;
                    }

                    // Field 4: Type
                    SchedulableField sfType = FindSchedulableField(doc, availableFields, "Type", BuiltInParameter.ELEM_TYPE_PARAM);
                    if (sfType != null)
                    {
                        ScheduleField fType = def.AddField(sfType);
                        fType.ColumnHeading = "Type";
                        fType.IsHidden = !colOptions.ShowType;
                    }

                    // Field 5: Location by Area
                    SchedulableField sfArea = FindSchedulableField(doc, availableFields, "Location by Area", "Location By Area", "Khu Vực");
                    if (sfArea != null)
                    {
                        ScheduleField fArea = def.AddField(sfArea);
                        fArea.ColumnHeading = "Location by Area";
                        fArea.IsHidden = !colOptions.ShowArea;
                    }

                    // Field 6: PN
                    SchedulableField sfPN = FindSchedulableField(doc, availableFields, "PN", "Áp Lực");
                    if (sfPN != null)
                    {
                        ScheduleField fPN = def.AddField(sfPN);
                        fPN.ColumnHeading = "PN";
                        fPN.IsHidden = !colOptions.ShowPN;
                    }

                    // Field 7: Location by Level
                    SchedulableField sfLevel = FindSchedulableField(doc, availableFields, "Location by Level", "Location By Level", "Level_Location", "Level", BuiltInParameter.SCHEDULE_LEVEL_PARAM);
                    if (sfLevel != null)
                    {
                        ScheduleField fLevel = def.AddField(sfLevel);
                        fLevel.ColumnHeading = "Location by Level";
                        fLevel.IsHidden = !colOptions.ShowLevel;
                    }

                    // Bộ lọc nếu là Bóc Cục Bộ
                    def.ClearFilters();
                    if (isLocal)
                    {
                        SchedulableField sfScope = FindSchedulableField(doc, availableFields, DefaultScopeParamName);
                        if (sfScope != null)
                        {
                            ScheduleField fScope = def.AddField(sfScope);
                            fScope.IsHidden = true;
                            ScheduleFilter filter = new ScheduleFilter(fScope.FieldId, ScheduleFilterType.Equal, "Cục Bộ");
                            def.AddFilter(filter);
                        }
                    }

                    // 5. Cấu hình Sorting / Grouping & Grand Total
                    def.ClearSortGroupFields();

                    if (fName != null)
                    {
                        ScheduleSortGroupField sortName = new ScheduleSortGroupField(fName.FieldId);
                        sortName.ShowHeader = false;
                        sortName.ShowFooter = false;
                        sortName.ShowBlankLine = false;
                        def.AddSortGroupField(sortName);
                    }

                    if (fSize != null)
                    {
                        ScheduleSortGroupField sortSize = new ScheduleSortGroupField(fSize.FieldId);
                        sortSize.ShowHeader = false;
                        sortSize.ShowFooter = false;
                        def.AddSortGroupField(sortSize);
                    }

                    def.IsItemized = false; // Gom các dòng giống nhau thành 1 dòng và đếm Count
                    def.ShowGrandTotal = colOptions.ShowGrandTotal;
                    def.ShowGrandTotalTitle = colOptions.ShowGrandTotal;
                    def.GrandTotalTitle = "Grand total";

                    createdSchedule = schedule;

                    // 6. Đặt lên Sheet nếu có yêu cầu
                    if (targetSheet != null)
                    {
                        // Kiểm tra xem Schedule đã có instance trên Sheet này chưa
                        ScheduleSheetInstance existingInst = null;
                        FilteredElementCollector ssiCol = new FilteredElementCollector(doc, targetSheet.Id)
                            .OfClass(typeof(ScheduleSheetInstance));

                        foreach (ScheduleSheetInstance inst in ssiCol.Cast<ScheduleSheetInstance>())
                        {
                            if (inst.ScheduleId == schedule.Id)
                            {
                                existingInst = inst;
                                break;
                            }
                        }

                        if (existingInst == null)
                        {
                            XYZ pt = placementPoint ?? new XYZ(1.2, 1.2, 0);
                            ScheduleSheetInstance.Create(doc, targetSheet.Id, schedule.Id, pt);
                            resultMessage = string.Format("✔ Đã tạo bảng Schedule '{0}' và đặt thành công lên Sheet {1} ({2})!\nBạn cũng có thể mở Project Browser và kéo thả (Drag & Drop) bảng này vào bất kỳ Sheet nào khác.",
                                schedule.Name, targetSheet.SheetNumber, targetSheet.Name);
                        }
                        else
                        {
                            if (placementPoint != null)
                            {
                                existingInst.Point = placementPoint;
                            }
                            resultMessage = string.Format("✔ Bảng Schedule '{0}' trên Sheet {1} ({2}) đã được cập nhật chuẩn xác!",
                                schedule.Name, targetSheet.SheetNumber, targetSheet.Name);
                        }
                    }
                    else
                    {
                        resultMessage = string.Format("✔ Đã tạo/cập nhật bảng Schedule '{0}' thành công trong mục Schedules của Revit!\nBây giờ bạn chỉ cần mở Sheet bất kỳ và KÉO THẢ (Drag & Drop) bảng này vào bản vẽ.", schedule.Name);
                    }

                    tx.Commit();
                    return true;
                }
                catch (Exception ex)
                {
                    tx.RollBack();
                    resultMessage = "Lỗi tạo bảng Schedule: " + ex.Message;
                    return false;
                }
            }
        }

        private static SchedulableField FindSchedulableField(Document doc, IList<SchedulableField> fields, params object[] nameOrBip)
        {
            if (fields == null) return null;

            foreach (object target in nameOrBip)
            {
                if (target is BuiltInParameter)
                {
                    BuiltInParameter bip = (BuiltInParameter)target;
                    ElementId bipId = new ElementId(bip);
                    foreach (SchedulableField sf in fields)
                    {
                        if (sf.ParameterId == bipId) return sf;
                    }
                }
                else if (target is string)
                {
                    string name = (string)target;
                    foreach (SchedulableField sf in fields)
                    {
                        try
                        {
                            string fName = sf.GetName(doc);
                            if (fName.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                                RemoveDiacritics(fName) == RemoveDiacritics(name))
                            {
                                return sf;
                            }
                        }
                        catch { }
                    }
                }
            }
            return null;
        }

                /// <summary>
        /// Lấy hoặc tự động tạo Viewport Type "No Title" (Không Tiêu Đề) để đường viền Viewport ôm sát rạt, không bị thanh Title Line kéo dài
        /// </summary>
        public static ElementId GetOrCreateNoTitleViewportType(Document doc)
        {
            if (doc == null) return ElementId.InvalidElementId;

            FilteredElementCollector vpTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(ElementType))
                .OfCategory(BuiltInCategory.OST_Viewports);

            ElementType fallbackType = null;
            foreach (ElementType et in vpTypes.Cast<ElementType>())
            {
                if (fallbackType == null) fallbackType = et;

                string name = et.Name.ToLowerInvariant();
                if (name.Contains("no title") || name.Contains("không tiêu đề") || name.Contains("khong tieu de") || name.Contains("none"))
                {
                    return et.Id;
                }

                Parameter pShow = et.get_Parameter(BuiltInParameter.VIEWPORT_ATTR_SHOW_LABEL);
                if (pShow != null && pShow.HasValue && pShow.AsInteger() == 0)
                {
                    return et.Id;
                }
            }

            if (fallbackType != null)
            {
                try
                {
                    ElementType newType = fallbackType.Duplicate("HDTOOL - No Title");
                    if (newType != null)
                    {
                        Parameter pShow = newType.get_Parameter(BuiltInParameter.VIEWPORT_ATTR_SHOW_LABEL);
                        if (pShow != null && !pShow.IsReadOnly)
                        {
                            pShow.Set(0);
                        }

                        Parameter pExt = newType.get_Parameter(BuiltInParameter.VIEWPORT_ATTR_SHOW_EXTENSION_LINE);
                        if (pExt != null && !pExt.IsReadOnly)
                        {
                            pExt.Set(0);
                        }

                        return newType.Id;
                    }
                }
                catch { }
                return fallbackType.Id;
            }

            return ElementId.InvalidElementId;
        }

        /// <summary>
        /// Lấy hoặc tạo mới View dạng Legend (Legends) theo lệnh Sếp Hồ:
        /// Ưu tiên tìm Legend cùng tên -> Duplicate từ Legend có sẵn -> Tạo ViewFamily.Legend -> Fallback Drafting
        /// </summary>
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

            // 2. Tìm bất kỳ Legend View nào có sẵn trong dự án để Duplicate (chuẩn 100% Revit API)
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

            // 3. Thử tạo mới bằng ViewDrafting.Create với ViewFamily.Legend
            ViewFamilyType legendVft = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(x => x.ViewFamily == ViewFamily.Legend);

            if (legendVft != null)
            {
                try
                {
                    ViewDrafting legDraft = ViewDrafting.Create(doc, legendVft.Id);
                    if (legDraft != null)
                    {
                        try { legDraft.Name = viewName; } catch { }
                        try { legDraft.Scale = 1; } catch { }
                        return legDraft;
                    }
                }
                catch { }
            }

            // 4. Fallback sang ViewDrafting nếu dự án chưa có Legend nào và Revit từ chối tạo Legend trống
            FilteredElementCollector draftCol = new FilteredElementCollector(doc).OfClass(typeof(ViewDrafting));
            foreach (ViewDrafting vd in draftCol.Cast<ViewDrafting>())
            {
                if (vd.Name.Equals(viewName, StringComparison.OrdinalIgnoreCase))
                {
                    return vd;
                }
            }

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
            catch
            {
                try
                {
                    TextNoteOptions opts = new TextNoteOptions(typeId);
                    opts.HorizontalAlignment = hAlign;
                    opts.VerticalAlignment = vAlign;
                    TextNote.Create(doc, view.Id, pt, text, opts);
                }
                catch { }
            }
        }

        /// <summary>
        /// Tạo hoặc lấy kiểu chữ chuyên dụng cho Bảng Khối Lượng HDTOOL với kích thước chuẩn mm trên giấy
        /// </summary>
        public static TextNoteType GetOrCreateTableTextType(Document doc, double textSizeMm, bool isBold)
        {
            if (doc == null) return null;
            string typeName = string.Format("HDTOOL_Table_{0:0.0}mm{1}", textSizeMm, isBold ? "_Bold" : "");
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

        /// <summary>
        /// Lấy tỷ lệ thực tế mà Bảng Khối Lượng đã được vẽ
        /// </summary>
        public static int GetTableDrawnScale(ViewDrafting vd)
        {
            if (vd == null) return 1;
            Parameter p = vd.get_Parameter(BuiltInParameter.VIEW_DESCRIPTION);
            if (p != null && p.HasValue)
            {
                string val = p.AsString();
                if (!string.IsNullOrEmpty(val) && val.StartsWith("HD_TABLE_SCALE:"))
                {
                    int s;
                    if (int.TryParse(val.Substring("HD_TABLE_SCALE:".Length), out s) && s > 0)
                    {
                        return s;
                    }
                }
            }
            return 1;
        }

        /// <summary>
        /// Ghi nhớ tỷ lệ thực tế mà Bảng Khối Lượng đã vẽ vào tham số View
        /// </summary>
        public static void SetTableDrawnScale(ViewDrafting vd, int scale)
        {
            if (vd == null || scale <= 0) return;
            try
            {
                Parameter p = vd.get_Parameter(BuiltInParameter.VIEW_DESCRIPTION);
                if (p != null && !p.IsReadOnly)
                {
                    p.Set("HD_TABLE_SCALE:" + scale);
                }
            }
            catch { }
        }

        /// <summary>
        /// Kiểm tra xem một ViewDrafting có phải là Bảng Khối Lượng HDTOOL không
        /// </summary>
        public static bool IsHDToolTable(ViewDrafting vd)
        {
            if (vd == null) return false;
            string n = vd.Name ?? "";
            if (n.StartsWith("HD_BẢNG KHỐI LƯỢNG", StringComparison.OrdinalIgnoreCase) ||
                n.StartsWith("HD_BANG KHOI LUONG", StringComparison.OrdinalIgnoreCase) ||
                n.IndexOf("KLPK", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
            Parameter p = vd.get_Parameter(BuiltInParameter.VIEW_DESCRIPTION);
            if (p != null && p.HasValue && p.AsString().Contains("HD_TABLE_SCALE:"))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// CĂN KHỚP TỶ LỆ TỰ ĐỘNG: Co dãn toàn bộ đường kẻ ô và chữ trong Bảng Khối Lượng theo tỷ lệ mới,
        /// đảm bảo chữ và ô kẻ luôn khớp khít 100%, không bị đè chữ hay tràn ô.
        /// </summary>
        public static bool RescaleDraftingViewTable(Document doc, ViewDrafting draftingView, int newScale)
        {
            if (doc == null || draftingView == null || newScale <= 0) return false;
            int oldScale = GetTableDrawnScale(draftingView);
            if (oldScale <= 0) oldScale = 1;
            if (oldScale == newScale) return true;

            double ratio = (double)newScale / (double)oldScale;

            FilteredElementCollector col = new FilteredElementCollector(doc, draftingView.Id)
                .WhereElementIsNotElementType();

            List<DetailCurve> curves = new List<DetailCurve>();
            List<TextNote> textNotes = new List<TextNote>();

            foreach (Element el in col)
            {
                if (el is DetailCurve) curves.Add(el as DetailCurve);
                else if (el is TextNote) textNotes.Add(el as TextNote);
            }

            // 1. Co dãn lại các đường kẻ khung (Detail Curves)
            List<Line> newLines = new List<Line>();
            List<ElementId> oldCurveIds = new List<ElementId>();

            foreach (DetailCurve dc in curves)
            {
                Line l = dc.GeometryCurve as Line;
                if (l != null)
                {
                    XYZ p1 = l.GetEndPoint(0);
                    XYZ p2 = l.GetEndPoint(1);
                    XYZ p1_new = new XYZ(p1.X * ratio, p1.Y * ratio, 0);
                    XYZ p2_new = new XYZ(p2.X * ratio, p2.Y * ratio, 0);
                    if (p1_new.DistanceTo(p2_new) > 0.001)
                    {
                        newLines.Add(Line.CreateBound(p1_new, p2_new));
                        oldCurveIds.Add(dc.Id);
                    }
                }
            }

            if (oldCurveIds.Count > 0)
            {
                doc.Delete(oldCurveIds);
                foreach (Line l in newLines)
                {
                    try { doc.Create.NewDetailCurve(draftingView, l); } catch { }
                }
            }

            // 2. Di chuyển và dãn bề rộng các ô TextNote
            foreach (TextNote tn in textNotes)
            {
                try
                {
                    XYZ currentCoord = tn.Coord;
                    XYZ targetCoord = new XYZ(currentCoord.X * ratio, currentCoord.Y * ratio, 0);
                    XYZ translation = targetCoord - currentCoord;

                    if (translation.GetLength() > 0.0001)
                    {
                        ElementTransformUtils.MoveElement(doc, tn.Id, translation);
                    }

                    double newWidth = tn.Width * ratio;
                    if (newWidth > 0.001)
                    {
                        try { tn.Width = newWidth; } catch { }
                    }
                }
                catch { }
            }

            SetTableDrawnScale(draftingView, newScale);
            return true;
        }

        /// <summary>
        /// Tự động quét và căn khớp tỷ lệ tất cả các Bảng Khối Lượng trên Sheet hoặc View hiện tại
        /// </summary>
        public static bool CheckAndRescaleAllTables(Document doc, Autodesk.Revit.DB.View activeView)
        {
            if (doc == null || doc.IsReadOnly) return false;
            bool anyUpdated = false;

            try
            {
                ViewSheet sheet = activeView as ViewSheet;
                if (sheet != null)
                {
                    FilteredElementCollector vpCol = new FilteredElementCollector(doc, sheet.Id).OfClass(typeof(Viewport));
                    foreach (Viewport vp in vpCol.Cast<Viewport>())
                    {
                        Autodesk.Revit.DB.View v = doc.GetElement(vp.ViewId) as Autodesk.Revit.DB.View;
                        if (v is ViewDrafting && IsHDToolTable(v as ViewDrafting))
                        {
                            ViewDrafting vd = v as ViewDrafting;
                            int currentScale = vd.Scale;
                            int drawnScale = GetTableDrawnScale(vd);
                            if (currentScale > 0 && currentScale != drawnScale)
                            {
                                using (Transaction tx = new Transaction(doc, "HDTOOL - Khớp Tỷ Lệ Bảng Khối Lượng"))
                                {
                                    tx.Start();
                                    RescaleDraftingViewTable(doc, vd, currentScale);
                                    tx.Commit();
                                    anyUpdated = true;
                                }
                            }
                        }
                    }
                }
                else if (activeView is ViewDrafting && IsHDToolTable(activeView as ViewDrafting))
                {
                    ViewDrafting vd = activeView as ViewDrafting;
                    int currentScale = vd.Scale;
                    int drawnScale = GetTableDrawnScale(vd);
                    if (currentScale > 0 && currentScale != drawnScale)
                    {
                        using (Transaction tx = new Transaction(doc, "HDTOOL - Khớp Tỷ Lệ Bảng Khối Lượng"))
                        {
                            tx.Start();
                            RescaleDraftingViewTable(doc, vd, currentScale);
                            tx.Commit();
                            anyUpdated = true;
                        }
                    }
                }
            }
            catch { }

            return anyUpdated;
        }

        /// <summary>
        /// Tạo hoặc cập nhật Bảng Khối Lượng dạng Excel (Drafting View Table) chuẩn 100% như Add-in,
        /// gồm CẢ Đường Ống (m) + Phụ Kiện (cái), đầy đủ STT, Tên, Size, Count, Length, Type, Area, Level, Hệ Thống,
        /// kẻ khung viền kỹ thuật sắc nét, và đặt lên Sheet như 1 Viewport có thể di chuyển hoặc tự động cập nhật lại.
        /// </summary>
        public static bool CreateOrUpdateExcelTableOnSheet(
            RevitApp app,
            Document doc,
            string tableName,
            ViewSheet targetSheet,
            XYZ placementPoint,
            List<MepFittingItem> items,
            ScheduleColumnVisibilityOptions colOptions,
            out Autodesk.Revit.DB.View createdDraftingView,
            out Viewport createdViewport,
            out string resultMessage,
            List<ExcelTableColumn> dynamicColumns = null,
            bool showGrandTotal = true)
        {
            createdDraftingView = null;
            createdViewport = null;
            resultMessage = string.Empty;

            if (doc == null || items == null || items.Count == 0)
            {
                resultMessage = "Không có dữ liệu khối lượng để tạo bảng.";
                return false;
            }

            char[] forbiddenChars = new char[] { '\\', ':', '{', '}', '[', ']', '|', ';', '<', '>', '?', '*' };
            string cleanTitle = !string.IsNullOrWhiteSpace(tableName) ? tableName.Trim() : "BANG THONG KE KHOI LUONG MEP";
            foreach (char c in forbiddenChars)
            {
                cleanTitle = cleanTitle.Replace(c, '_');
            }
            string viewName = "HD_BẢNG KHỐI LƯỢNG - " + cleanTitle.Trim();
            if (viewName.Length > 90) viewName = viewName.Substring(0, 90);

            try
            {
                using (Transaction tx = new Transaction(doc, "HDTOOL - Đặt Bảng Khối Lượng Excel Lên Sheet"))
                {
                    tx.Start();

                    // 1. TẠO HOẶC LẤY VIEW DẠNG LEGEND (LEGENDS) THEO LỆNH SẾP HỒ
                    Autodesk.Revit.DB.View tableView = GetOrCreateTableLegendView(doc, viewName);
                    if (tableView == null)
                    {
                        resultMessage = "Không thể tạo hoặc nhân bản View Legend trong dự án Revit.";
                        tx.RollBack();
                        return false;
                    }

                    // TẨY SẠCH 100% CÁC ĐỐI TƯỢNG CŨ TRONG VIEW ĐỂ VIEWPORT KHÔNG BỊ PHÌNH TO
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

                    int viewScale = tableView.Scale;
                    if (viewScale <= 0) viewScale = 1;
                    double scaleFactor = (double)viewScale;

                    if (colOptions == null) colOptions = new ScheduleColumnVisibilityOptions();

                    List<ExcelTableColumn> columns = dynamicColumns;
                    if (columns == null || columns.Count == 0)
                    {
                        columns = new List<ExcelTableColumn>();
                        columns.Add(new ExcelTableColumn("STT", 14.0, HorizontalTextAlignment.Center, (it, idx) => idx.ToString()));
                        if (colOptions.ShowName) columns.Add(new ExcelTableColumn("Tên Phụ Kiện / Đường Ống", 65.0, HorizontalTextAlignment.Left, (it, idx) => it.MergedName));
                        if (colOptions.ShowType) columns.Add(new ExcelTableColumn("Phân Loại", 28.0, HorizontalTextAlignment.Center, (it, idx) => it.CategoryKind));
                        if (colOptions.ShowSize) columns.Add(new ExcelTableColumn("Kích Thước", 35.0, HorizontalTextAlignment.Center, (it, idx) => it.Size));
                        if (colOptions.ShowCount) columns.Add(new ExcelTableColumn("Số Lượng", 22.0, HorizontalTextAlignment.Right, (it, idx) => it.LengthM > 0 && it.Count == 0 ? "-" : it.Count.ToString()));
                        columns.Add(new ExcelTableColumn("Chiều Dài (m)", 26.0, HorizontalTextAlignment.Right, (it, idx) => it.LengthM > 0 ? string.Format("{0:0.00}", it.LengthM) : "-"));
                        if (colOptions.ShowType) columns.Add(new ExcelTableColumn("Type", 38.0, HorizontalTextAlignment.Left, (it, idx) => it.TypeName));
                        if (colOptions.ShowArea) columns.Add(new ExcelTableColumn("Location by Area", 40.0, HorizontalTextAlignment.Center, (it, idx) => it.LocationByArea));
                        if (colOptions.ShowPN) columns.Add(new ExcelTableColumn("PN", 18.0, HorizontalTextAlignment.Center, (it, idx) => it.PN));
                        if (colOptions.ShowLevel) columns.Add(new ExcelTableColumn("Location by Level", 40.0, HorizontalTextAlignment.Center, (it, idx) => it.LocationByLevel));
                        columns.Add(new ExcelTableColumn("Hệ Thống", 45.0, HorizontalTextAlignment.Left, (it, idx) => it.SystemType));
                    }

                    // Tự động căn chỉnh độ rộng cột
                    foreach (ExcelTableColumn col in columns)
                    {
                        int maxChars = (col.Header != null) ? col.Header.Length : 0;
                        for (int r = 0; r < items.Count; r++)
                        {
                            string val = col.ValueGetter(items[r], r + 1) ?? "";
                            if (val.Length > maxChars) maxChars = val.Length;
                        }
                        double autoNeededMm = (maxChars * 1.85) + 6.0;
                        if (autoNeededMm > col.WidthMm)
                        {
                            col.WidthMm = Math.Ceiling(autoNeededMm);
                        }
                    }

                    const double MM_TO_FT = 1.0 / 304.8;
                    double sMM_TO_FT = MM_TO_FT * scaleFactor;

                    const double TITLE_H_MM = 11.0;
                    const double HEADER_H_MM = 9.0;
                    const double ROW_H_MM = 7.0;
                    const double SUMMARY_H_MM = 8.5;
                    const double PADDING_X_MM = 2.0;

                    double totalWidthMm = 0;
                    foreach (ExcelTableColumn col in columns) totalWidthMm += col.WidthMm;

                    double titleHFt = TITLE_H_MM * sMM_TO_FT;
                    double headerHFt = HEADER_H_MM * sMM_TO_FT;
                    double rowHFt = ROW_H_MM * sMM_TO_FT;
                    bool hasTotal = showGrandTotal && (colOptions == null || colOptions.ShowGrandTotal);
                    double summaryHFt = hasTotal ? (SUMMARY_H_MM * sMM_TO_FT) : 0;
                    double totalWidthFt = totalWidthMm * sMM_TO_FT;
                    double totalHFt = titleHFt + headerHFt + (items.Count * rowHFt) + summaryHFt;

                    // 5. VẼ LƯỚI KẺ KHUNG (DETAIL CURVES)
                    SafeDrawDetailLine(doc, tableView, new XYZ(0, 0, 0), new XYZ(totalWidthFt, 0, 0));
                    SafeDrawDetailLine(doc, tableView, new XYZ(0, -titleHFt, 0), new XYZ(totalWidthFt, -titleHFt, 0));

                    double headerBottomY = -titleHFt - headerHFt;
                    SafeDrawDetailLine(doc, tableView, new XYZ(0, headerBottomY, 0), new XYZ(totalWidthFt, headerBottomY, 0));

                    for (int r = 1; r <= items.Count; r++)
                    {
                        double rowY = headerBottomY - (r * rowHFt);
                        SafeDrawDetailLine(doc, tableView, new XYZ(0, rowY, 0), new XYZ(totalWidthFt, rowY, 0));
                    }

                    if (hasTotal)
                    {
                        double bottomY = -totalHFt;
                        SafeDrawDetailLine(doc, tableView, new XYZ(0, bottomY, 0), new XYZ(totalWidthFt, bottomY, 0));
                    }

                    SafeDrawDetailLine(doc, tableView, new XYZ(0, 0, 0), new XYZ(0, -totalHFt, 0));
                    SafeDrawDetailLine(doc, tableView, new XYZ(totalWidthFt, 0, 0), new XYZ(totalWidthFt, -totalHFt, 0));

                    double curX = 0;
                    for (int c = 0; c < columns.Count - 1; c++)
                    {
                        curX += columns[c].WidthMm * sMM_TO_FT;
                        double bottomColY = hasTotal ? (-totalHFt + summaryHFt) : -totalHFt;
                        SafeDrawDetailLine(doc, tableView, new XYZ(curX, -titleHFt, 0), new XYZ(curX, bottomColY, 0));
                    }

                    // 6. GHI NỘI DUNG TEXT VÀO CÁC Ô
                    double paddingXFt = PADDING_X_MM * sMM_TO_FT;

                    SafeDrawTextNote(doc, tableView, titleTextType.Id, new XYZ(totalWidthFt / 2.0, -titleHFt / 2.0, 0), cleanTitle, HorizontalTextAlignment.Center, VerticalTextAlignment.Middle, totalWidthFt - (paddingXFt * 2));

                    double colLeft = 0;
                    for (int c = 0; c < columns.Count; c++)
                    {
                        ExcelTableColumn col = columns[c];
                        double colWidthFt = col.WidthMm * sMM_TO_FT;
                        double colCenter = colLeft + (colWidthFt / 2.0);
                        double headerMidY = -titleHFt - (headerHFt / 2.0);
                        double cellWidthFt = Math.Max(colWidthFt - (paddingXFt * 1.5), 0.005);

                        SafeDrawTextNote(doc, tableView, headerTextType.Id, new XYZ(colCenter, headerMidY, 0), col.Header.ToUpper(), HorizontalTextAlignment.Center, VerticalTextAlignment.Middle, cellWidthFt);

                        colLeft += colWidthFt;
                    }

                    for (int r = 0; r < items.Count; r++)
                    {
                        MepFittingItem it = items[r];
                        double rowMidY = headerBottomY - (r * rowHFt) - (rowHFt / 2.0);
                        colLeft = 0;

                        for (int c = 0; c < columns.Count; c++)
                        {
                            ExcelTableColumn col = columns[c];
                            double colWidthFt = col.WidthMm * sMM_TO_FT;
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

                    if (hasTotal)
                    {
                        double summaryMidY = -totalHFt + (summaryHFt / 2.0);
                        int totalCount = items.Sum(x => x.Count);
                        double totalLength = Math.Round(items.Sum(x => x.LengthM), 2);

                        string sumLabel;
                        if (totalCount > 0 && totalLength > 0)
                        {
                            sumLabel = string.Format("TỔNG CỘNG: {0} cái  |  {1:0.00} m ống  |  {2} nhóm quy cách", totalCount, totalLength, items.Count);
                        }
                        else if (totalCount > 0)
                        {
                            sumLabel = string.Format("TỔNG CỘNG: {0} cái phụ kiện  |  {1} nhóm quy cách", totalCount, items.Count);
                        }
                        else
                        {
                            sumLabel = string.Format("TỔNG CỘNG: {0:0.00} m đường ống  |  {1} nhóm quy cách", totalLength, items.Count);
                        }
                        SafeDrawTextNote(doc, tableView, headerTextType.Id, new XYZ(paddingXFt, summaryMidY, 0), sumLabel, HorizontalTextAlignment.Left, VerticalTextAlignment.Middle, totalWidthFt - (paddingXFt * 2));
                    }

                    // 7. THIẾT LẬP CROP BOX ÔM SÁT RẠT BẢNG (FIX TRIỆT ĐỂ KHUNG TO KHỔNG LỒ)
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

                    createdDraftingView = tableView;

                    // 8. ĐẶT HOẶC CẬP NHẬT VIEWPORT TRÊN SHEET VỪA VẶN, CHUẨN XÁC
                    Viewport vp = null;
                    if (targetSheet != null)
                    {
                        XYZ centerPt;
                        if (placementPoint != null)
                        {
                            // Người dùng click chuột: góc trên bên trái bảng trùng khớp với điểm click
                            centerPt = new XYZ(placementPoint.X + (totalWidthFt / 2.0), placementPoint.Y - (totalHFt / 2.0), 0);
                        }
                        else
                        {
                            // Tự động tìm khung tên (Titleblock) để đặt vào góc trên bên phải bản vẽ gọn gàng
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
                                    double marginX = 20.0 / 304.8;
                                    double marginY = 20.0 / 304.8;
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
                            if (Viewport.CanAddViewToSheet(doc, targetSheet.Id, tableView.Id))
                            {
                                vp = Viewport.Create(doc, targetSheet.Id, tableView.Id, centerPt);
                            }
                        }
                        else
                        {
                            try { vp.SetBoxCenter(centerPt); } catch { }
                        }

                        // ĐỔI VIEWPORT TYPE SANG "NO TITLE" ĐỂ TRIỆT TIÊU THANH EXTENSION LINE KÉO DÀI
                        if (vp != null)
                        {
                            ElementId noTitleId = GetOrCreateNoTitleViewportType(doc);
                            if (noTitleId != null && noTitleId != ElementId.InvalidElementId)
                            {
                                try { vp.ChangeTypeId(noTitleId); } catch { }
                            }

                            try { vp.LabelLineLength = 0; } catch { }
                            try { vp.LabelOffset = new XYZ(0, -totalHFt, 0); } catch { }
                            try { vp.SetBoxCenter(centerPt); } catch { }
                        }

                        createdViewport = vp;
                    }

                    PlacedTableMetadata meta = new PlacedTableMetadata
                    {
                        SheetId = targetSheet != null ? targetSheet.Id : null,
                        DraftingViewId = tableView.Id,
                        ViewportId = vp != null ? vp.Id : null,
                        TotalWidthFt = totalWidthFt,
                        TotalHeightFt = totalHFt,
                        TitleHeightFt = titleHFt,
                        HeaderHeightFt = headerHFt,
                        RowHeightFt = rowHFt,
                        Items = new List<MepFittingItem>(items)
                    };

                    // Lưu Metadata vĩnh viễn vào Parameter Comments (KHÔNG TẠO TextNote ở -500, -500 để Viewport không bị to)
                    SaveTableMetadataToDraftingView(doc, tableView, meta);

                    tx.Commit();

                    if (targetSheet != null)
                    {
                        PlacedTablesBySheet[targetSheet.Id] = meta;
                    }
                    LastPlacedTable = meta;
                }

                resultMessage = string.Format("✅ Đã tạo bảng khối lượng dạng LEGENDS thành công trên Sheet '{0}'!\n\n📋 Gồm {1} nhóm quy cách, khung nhìn ôm sát rạt vừa vặn 100%, không bị to và không bị đẩy ra xa.",
                    targetSheet != null ? targetSheet.Name : "Project Browser (Legends)", items.Count);
                return true;
            }
            catch (Exception ex)
            {
                resultMessage = "Lỗi tạo bảng khối lượng: " + ex.Message;
                return false;
            }
        }

        // =========================================================================
        // METADATA PERSISTENCE & CHECK VAR PLUMBING RECOVERY
        // =========================================================================

        public const string TableMetadataPrefix = "[HDTOOL_METADATA_V1]";

        public static string SerializePlacedTableMetadata(PlacedTableMetadata meta)
        {
            if (meta == null) return string.Empty;
            StringBuilder sb = new StringBuilder();
            sb.Append(TableMetadataPrefix).Append("\n");
            sb.AppendFormat(CultureInfo.InvariantCulture, "{0:0.0000}|{1:0.0000}|{2:0.0000}|{3:0.0000}|{4:0.0000}\n",
                meta.TotalWidthFt, meta.TotalHeightFt, meta.TitleHeightFt, meta.HeaderHeightFt, meta.RowHeightFt);

            if (meta.Items != null)
            {
                foreach (var it in meta.Items)
                {
                    string idList = (it.ElementIds != null && it.ElementIds.Count > 0)
                        ? string.Join(",", it.ElementIds.Select(x => x.ToString()))
                        : "";

                    sb.AppendFormat(CultureInfo.InvariantCulture, "{0}|{1}|{2}|{3}|{4:0.00}|{5:0.00}|{6}|{7}|{8}|{9}|{10}|{11}|{12}\n",
                        it.STT,
                        (it.MergedName ?? "").Replace("|", "_").Replace("\n", " "),
                        (it.Size ?? "").Replace("|", "_"),
                        it.Count,
                        it.LengthM,
                        it.TotalLengthM,
                        (it.TypeName ?? "").Replace("|", "_"),
                        (it.LocationByArea ?? "").Replace("|", "_"),
                        (it.PN ?? "").Replace("|", "_"),
                        (it.LocationByLevel ?? "").Replace("|", "_"),
                        (it.SystemType ?? "").Replace("|", "_"),
                        (it.CategoryKind ?? "").Replace("|", "_"),
                        idList
                    );
                }
            }
            return sb.ToString();
        }

        public static PlacedTableMetadata DeserializePlacedTableMetadata(string rawText, ElementId sheetId, ElementId draftingViewId, ElementId viewportId)
        {
            if (string.IsNullOrEmpty(rawText) || !rawText.Contains(TableMetadataPrefix)) return null;

            int idx = rawText.IndexOf(TableMetadataPrefix);
            string payload = rawText.Substring(idx + TableMetadataPrefix.Length).Trim();
            string[] lines = payload.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) return null;

            PlacedTableMetadata meta = new PlacedTableMetadata();
            meta.SheetId = sheetId;
            meta.DraftingViewId = draftingViewId;
            meta.ViewportId = viewportId;

            string[] dims = lines[0].Split('|');
            if (dims.Length >= 5)
            {
                double w, h, th, hh, rh;
                if (double.TryParse(dims[0], NumberStyles.Any, CultureInfo.InvariantCulture, out w)) meta.TotalWidthFt = w;
                if (double.TryParse(dims[1], NumberStyles.Any, CultureInfo.InvariantCulture, out h)) meta.TotalHeightFt = h;
                if (double.TryParse(dims[2], NumberStyles.Any, CultureInfo.InvariantCulture, out th)) meta.TitleHeightFt = th;
                if (double.TryParse(dims[3], NumberStyles.Any, CultureInfo.InvariantCulture, out hh)) meta.HeaderHeightFt = hh;
                if (double.TryParse(dims[4], NumberStyles.Any, CultureInfo.InvariantCulture, out rh)) meta.RowHeightFt = rh;
            }

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;
                string[] p = line.Split('|');
                if (p.Length < 12) continue;

                MepFittingItem it = new MepFittingItem();
                int stt, count;
                double len, totLen;
                if (int.TryParse(p[0], out stt)) it.STT = stt;
                it.MergedName = p[1];
                it.Size = p[2];
                if (int.TryParse(p[3], out count)) it.Count = count;
                if (double.TryParse(p[4], NumberStyles.Any, CultureInfo.InvariantCulture, out len)) it.LengthM = len;
                if (double.TryParse(p[5], NumberStyles.Any, CultureInfo.InvariantCulture, out totLen)) it.TotalLengthM = totLen;
                it.TypeName = p[6];
                it.LocationByArea = p[7];
                it.PN = p[8];
                it.LocationByLevel = p[9];
                it.SystemType = p[10];
                it.CategoryKind = p[11];

                if (p.Length >= 13 && !string.IsNullOrEmpty(p[12]))
                {
                    string[] idParts = p[12].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string idStr in idParts)
                    {
                        long idVal;
                        if (long.TryParse(idStr.Trim(), out idVal))
                        {
                            it.ElementIds.Add(new ElementId(idVal));
                        }
                    }
                }

                meta.Items.Add(it);
            }

            return meta;
        }

        public static void SaveTableMetadataToDraftingView(Document doc, Autodesk.Revit.DB.View vd, PlacedTableMetadata meta)
        {
            if (doc == null || vd == null || meta == null) return;
            try
            {
                string serialized = SerializePlacedTableMetadata(meta);
                if (string.IsNullOrEmpty(serialized)) return;

                // Xóa bỏ tất cả TextNote metadata cũ nếu có để tránh phình to Viewport
                try
                {
                    FilteredElementCollector tnCol = new FilteredElementCollector(doc, vd.Id).OfClass(typeof(TextNote));
                    List<ElementId> tnDel = new List<ElementId>();
                    foreach (TextNote tn in tnCol.Cast<TextNote>())
                    {
                        if (tn.Text != null && tn.Text.StartsWith(TableMetadataPrefix))
                        {
                            tnDel.Add(tn.Id);
                        }
                    }
                    if (tnDel.Count > 0) doc.Delete(tnDel);
                }
                catch { }

                // Lưu an toàn tuyệt đối vào Parameter Comments / VIEW_DESCRIPTION của View (không sinh ra đối tượng hình học)
                try
                {
                    Parameter pComments = vd.LookupParameter("Comments");
                    if (pComments != null && !pComments.IsReadOnly)
                    {
                        pComments.Set(serialized);
                    }
                    else
                    {
                        Parameter pDesc = vd.get_Parameter(BuiltInParameter.VIEW_DESCRIPTION);
                        if (pDesc != null && !pDesc.IsReadOnly)
                        {
                            pDesc.Set(serialized);
                        }
                    }
                }
                catch { }
            }
            catch { }
        }

        public static PlacedTableMetadata LoadTableMetadataFromDraftingView(Document doc, Autodesk.Revit.DB.View vd, ViewSheet sheet = null, Viewport vp = null)
        {
            if (doc == null || vd == null) return null;
            try
            {
                ElementId sheetId = sheet != null ? sheet.Id : null;
                ElementId vpId = vp != null ? vp.Id : null;

                // 1. Kiểm tra TextNote ẩn bên trong vd
                FilteredElementCollector tnCol = new FilteredElementCollector(doc, vd.Id).OfClass(typeof(TextNote));
                foreach (TextNote tn in tnCol.Cast<TextNote>())
                {
                    if (tn.Text != null && tn.Text.Contains(TableMetadataPrefix))
                    {
                        PlacedTableMetadata meta = DeserializePlacedTableMetadata(tn.Text, sheetId, vd.Id, vpId);
                        if (meta != null && meta.Items.Count > 0) return meta;
                    }
                }

                // 2. Kiểm tra Parameter Comments / VIEW_DESCRIPTION
                Parameter pComments = vd.LookupParameter("Comments");
                if (pComments != null && pComments.HasValue && pComments.AsString() != null && pComments.AsString().Contains(TableMetadataPrefix))
                {
                    PlacedTableMetadata meta = DeserializePlacedTableMetadata(pComments.AsString(), sheetId, vd.Id, vpId);
                    if (meta != null && meta.Items.Count > 0) return meta;
                }

                Parameter pDesc = vd.get_Parameter(BuiltInParameter.VIEW_DESCRIPTION);
                if (pDesc != null && pDesc.HasValue && pDesc.AsString() != null && pDesc.AsString().Contains(TableMetadataPrefix))
                {
                    PlacedTableMetadata meta = DeserializePlacedTableMetadata(pDesc.AsString(), sheetId, vd.Id, vpId);
                    if (meta != null && meta.Items.Count > 0) return meta;
                }
            }
            catch { }
            return null;
        }

        public static void FindTableViewportOnSheet(Document doc, ViewSheet sheet, out Viewport tableVp, out Autodesk.Revit.DB.View tableDrafting)
        {
            tableVp = null;
            tableDrafting = null;
            if (doc == null || sheet == null) return;

            FilteredElementCollector vpCol = new FilteredElementCollector(doc, sheet.Id).OfClass(typeof(Viewport));
            List<Viewport> draftVps = new List<Viewport>();

            foreach (Viewport v in vpCol.Cast<Viewport>())
            {
                View vRef = doc.GetElement(v.ViewId) as View;
                if (vRef is ViewDrafting || vRef.ViewType == ViewType.Legend)
                {
                    Autodesk.Revit.DB.View vd = vRef;
                    draftVps.Add(v);

                    string n = vd.Name ?? "";
                    if (n.IndexOf("BẢNG KHỐI LƯỢNG", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("HD_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("BangKhoiLuong", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("[HDTOOL]", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        tableVp = v;
                        tableDrafting = vd;
                        return;
                    }

                    // Kiểm tra text bên trong drafting view
                    FilteredElementCollector tnCol = new FilteredElementCollector(doc, vd.Id).OfClass(typeof(TextNote));
                    foreach (TextNote tn in tnCol.Cast<TextNote>())
                    {
                        string txt = tn.Text ?? "";
                        if (txt.IndexOf("TỔNG CỘNG", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            txt.IndexOf("Tên Phụ Kiện", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            txt.IndexOf("KHỐI LƯỢNG", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            txt.IndexOf("KLPK", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            tableVp = v;
                            tableDrafting = vd;
                            return;
                        }
                    }
                }
            }

            if (tableVp == null && draftVps.Count > 0)
            {
                tableVp = draftVps[0];
                tableDrafting = doc.GetElement(tableVp.ViewId) as Autodesk.Revit.DB.View;
            }
        }

        public static PlacedTableMetadata RecoverMetadataFromSheetModelViews(Document doc, ViewSheet sheet, Viewport tableVp, Autodesk.Revit.DB.View tableDrafting)
        {
            if (doc == null || sheet == null || tableDrafting == null) return null;
            try
            {
                FilteredElementCollector vpCol = new FilteredElementCollector(doc, sheet.Id).OfClass(typeof(Viewport));
                List<Autodesk.Revit.DB.View> modelViews = new List<Autodesk.Revit.DB.View>();

                foreach (Viewport vp in vpCol.Cast<Viewport>())
                {
                    if (tableVp != null && vp.Id == tableVp.Id) continue;
                    Autodesk.Revit.DB.View v = doc.GetElement(vp.ViewId) as Autodesk.Revit.DB.View;
                    if (v != null && !(v is ViewDrafting) && !(v is ViewSchedule) && !(v is ViewSheet))
                    {
                        modelViews.Add(v);
                    }
                }

                List<MepFittingItem> scannedItems = null;
                if (modelViews.Count > 0)
                {
                    scannedItems = ScanAndGroupElements(doc, modelViews[0], currentViewOnly: true, includeFittings: true, includePipes: true, discretePipes: true);
                }

                if (scannedItems == null || scannedItems.Count == 0)
                {
                    scannedItems = ScanAndGroupElements(doc, null, currentViewOnly: false, includeFittings: true, includePipes: true, discretePipes: true);
                }

                if (scannedItems != null && scannedItems.Count > 0)
                {
                    PlacedTableMetadata meta = new PlacedTableMetadata
                    {
                        SheetId = sheet.Id,
                        DraftingViewId = tableDrafting.Id,
                        ViewportId = tableVp != null ? tableVp.Id : null,
                        Items = scannedItems
                    };
                    return meta;
                }
            }
            catch { }
            return null;
        }

        public static PlacedTableMetadata ResolvePlacedTableMetadataForSheet(Document doc, ViewSheet sheet, out Viewport tableVp, out Autodesk.Revit.DB.View tableDrafting)
        {
            tableVp = null;
            tableDrafting = null;
            if (doc == null || sheet == null) return null;

            if (PlacedTablesBySheet.ContainsKey(sheet.Id))
            {
                var m = PlacedTablesBySheet[sheet.Id];
                if (m != null && m.Items != null && m.Items.Count > 0)
                {
                    if (m.ViewportId != null) tableVp = doc.GetElement(m.ViewportId) as Viewport;
                    if (m.DraftingViewId != null) tableDrafting = doc.GetElement(m.DraftingViewId) as Autodesk.Revit.DB.View;
                    return m;
                }
            }

            FindTableViewportOnSheet(doc, sheet, out tableVp, out tableDrafting);
            if (tableDrafting == null) return null;

            PlacedTableMetadata persistentMeta = LoadTableMetadataFromDraftingView(doc, tableDrafting, sheet, tableVp);
            if (persistentMeta != null && persistentMeta.Items.Count > 0)
            {
                PlacedTablesBySheet[sheet.Id] = persistentMeta;
                LastPlacedTable = persistentMeta;
                return persistentMeta;
            }

            PlacedTableMetadata recoveredMeta = RecoverMetadataFromSheetModelViews(doc, sheet, tableVp, tableDrafting);
            if (recoveredMeta != null && recoveredMeta.Items.Count > 0)
            {
                PlacedTablesBySheet[sheet.Id] = recoveredMeta;
                LastPlacedTable = recoveredMeta;
                try
                {
                    using (Transaction tx = new Transaction(doc, "HDTOOL - Lưu Metadata Bảng Khối Lượng"))
                    {
                        tx.Start();
                        SaveTableMetadataToDraftingView(doc, tableDrafting, recoveredMeta);
                        tx.Commit();
                    }
                }
                catch { }
                return recoveredMeta;
            }

            return null;
        }

        public static int CalculateRowIndexFromPickedPoint(XYZ pickedPt, Viewport tableVp, PlacedTableMetadata meta)
        {
            if (pickedPt == null || tableVp == null || meta == null || meta.Items == null || meta.Items.Count == 0) return -2;

            const double MM_TO_FT = 1.0 / 304.8;
            const double TITLE_H_MM = 10.0;
            const double HEADER_H_MM = 8.0;
            const double ROW_H_MM = 6.5;
            const double SUMMARY_H_MM = 8.0;

            double titleHFt = meta.TitleHeightFt > 0 ? meta.TitleHeightFt : (TITLE_H_MM * MM_TO_FT);
            double headerHFt = meta.HeaderHeightFt > 0 ? meta.HeaderHeightFt : (HEADER_H_MM * MM_TO_FT);
            double rowHFt = meta.RowHeightFt > 0 ? meta.RowHeightFt : (ROW_H_MM * MM_TO_FT);
            double summaryHFt = SUMMARY_H_MM * MM_TO_FT;

            XYZ center = tableVp.GetBoxCenter();
            Outline box = tableVp.GetBoxOutline();

            double contentHFt = titleHFt + headerHFt + (meta.Items.Count * rowHFt) + summaryHFt;
            if (box != null)
            {
                double boxH = box.MaximumPoint.Y - box.MinimumPoint.Y;
                if (boxH > contentHFt) contentHFt = boxH;
            }

            double sheetTopY = center.Y + (contentHFt / 2.0);
            double relY = sheetTopY - pickedPt.Y;
            double ratio = relY / contentHFt;

            if (relY <= (titleHFt + headerHFt) || ratio <= 0.12)
            {
                return -1; // Click Tiêu đề -> Sáng toàn bảng
            }
            if (relY >= (contentHFt - summaryHFt) || ratio >= 0.88)
            {
                return -1; // Click dòng Tổng cộng -> Sáng toàn bảng
            }

            double availableH = contentHFt - (titleHFt + headerHFt) - summaryHFt;
            double dataRelY = relY - (titleHFt + headerHFt);
            int rowIdx = (int)((dataRelY / availableH) * meta.Items.Count);

            if (rowIdx < 0) rowIdx = 0;
            if (rowIdx >= meta.Items.Count) rowIdx = meta.Items.Count - 1;

            return rowIdx;
        }

        /// <summary>
        /// Chức năng tương tác độc quyền: CHECK VAR PLUMBING
        /// Nhận diện vĩnh viễn bảng khối lượng trên Sheet khi mở lại bản vẽ và soi sáng an toàn, không đơ
        /// </summary>
        public static bool PickAndHighlightFromSheetTable(UIDocument uidoc, out string resultMessage)
        {
            resultMessage = string.Empty;
            if (uidoc == null) return false;

            Document doc = uidoc.Document;
            Autodesk.Revit.DB.View curView = uidoc.ActiveView;
            ViewSheet sheet = curView as ViewSheet;

            if (sheet == null)
            {
                resultMessage = "Vui lòng mở một bản vẽ Sheet trên Revit có chứa Bảng Khối Lượng trước khi dùng tính năng này!";
                return false;
            }

            Viewport tableVp;
            Autodesk.Revit.DB.View tableDrafting;
            PlacedTableMetadata meta = ResolvePlacedTableMetadataForSheet(doc, sheet, out tableVp, out tableDrafting);

            if (meta == null || meta.Items == null || meta.Items.Count == 0 || tableVp == null)
            {
                resultMessage = string.Format("Không tìm thấy Bảng Khối Lượng HDTOOL nào trên Sheet '{0}' ({1}).\nVui lòng đặt bảng khối lượng lên Sheet trước khi soi sáng!",
                    sheet.SheetNumber, sheet.Name);
                return false;
            }

            List<MepFittingItem> items = meta.Items;

            try
            {
                XYZ pickedPt = uidoc.Selection.PickPoint(
                    ObjectSnapTypes.None,
                    "HDTOOL CHECK VAR: Click vào dòng bất kỳ của bảng trên Sheet để LÀM SÁNG ĐỐI TƯỢNG..."
                );

                int rowIdx = CalculateRowIndexFromPickedPoint(pickedPt, tableVp, meta);
                if (rowIdx == -1)
                {
                    List<ElementId> allIds = new List<ElementId>();
                    foreach (var it in items)
                    {
                        if (it.ElementIds != null) allIds.AddRange(it.ElementIds);
                    }
                    if (allIds.Count > 0)
                    {
                        MepHighlightManager.HighlightElements(uidoc, allIds, new Autodesk.Revit.DB.Color(255, 0, 0), 7, false);
                        resultMessage = string.Format("✔ Đã làm sáng TOÀN BỘ {0} đối tượng của bảng trên Sheet!", allIds.Count);
                        return true;
                    }
                }
                else if (rowIdx >= 0 && rowIdx < items.Count)
                {
                    MepFittingItem it = items[rowIdx];
                    if (it.ElementIds != null && it.ElementIds.Count > 0)
                    {
                        MepHighlightManager.HighlightElements(uidoc, it.ElementIds, new Autodesk.Revit.DB.Color(255, 0, 0), 7, false);
                        resultMessage = string.Format("✔ Đã soi sáng: {0} [{1}] ({2} đối tượng) trên mô hình Revit!",
                            it.MergedName, it.Size, it.ElementIds.Count);
                        return true;
                    }
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                resultMessage = "Đã hủy thao tác.";
                return true;
            }
            catch (Exception ex)
            {
                resultMessage = "Lỗi soi sáng: " + ex.Message;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Xuất dữ liệu bảng khối lượng ra định dạng CSV UTF-8
        /// </summary>
        public static bool ExportToCsv(string filePath, IList<MepFittingItem> items)
        {
            if (string.IsNullOrEmpty(filePath) || items == null) return false;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("STT,Tên Phụ Kiện,Size,Count,Length(m),Type,Location by Area,PN,Location by Level,Hệ Thống,Chủng Loại,Param Nguồn");

            foreach (MepFittingItem item in items)
            {
                sb.AppendLine(string.Format("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\",\"{6}\",\"{7}\",\"{8}\",\"{9}\",\"{10}\",\"{11}\"",
                    item.STT,
                    EscapeCsv(item.MergedName),
                    EscapeCsv(item.Size),
                    item.Count,
                    item.LengthM,
                    EscapeCsv(item.TypeName),
                    EscapeCsv(item.LocationByArea),
                    EscapeCsv(item.PN),
                    EscapeCsv(item.LocationByLevel),
                    EscapeCsv(item.SystemType),
                    EscapeCsv(item.CategoryKind),
                    EscapeCsv(item.SourceParamName)
                ));
            }

            File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(true));
            return true;
        }

        /// <summary>
        /// Sao chép bảng dữ liệu vào Clipboard dạng bảng Tab-Delimited để dán trực tiếp vào Excel
        /// </summary>
        public static bool CopyToClipboard(IList<MepFittingItem> items)
        {
            if (items == null || items.Count == 0) return false;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("STT\tTên Phụ Kiện\tSize\tCount\tLength (m)\tType\tLocation by Area\tPN\tLocation by Level\tHệ Thống\tChủng Loại\tParam Nguồn");

            foreach (MepFittingItem item in items)
            {
                sb.AppendLine(string.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}\t{10}\t{11}",
                    item.STT,
                    item.MergedName,
                    item.Size,
                    item.Count,
                    item.LengthM,
                    item.TypeName,
                    item.LocationByArea,
                    item.PN,
                    item.LocationByLevel,
                    item.SystemType,
                    item.CategoryKind,
                    item.SourceParamName
                ));
            }

            try
            {
                System.Windows.Forms.Clipboard.SetText(sb.ToString());
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string EscapeCsv(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\"", "\"\"");
        }

        /// <summary>
        /// Lấy tất cả các bảng ViewSchedule liên quan đến MEP trong dự án
        /// </summary>
        public static List<ViewSchedule> GetAllMepSchedules(Document doc)
        {
            List<ViewSchedule> mepSchedules = new List<ViewSchedule>();
            if (doc == null) return mepSchedules;

            try
            {
                FilteredElementCollector schedCol = new FilteredElementCollector(doc).OfClass(typeof(ViewSchedule));
                foreach (ViewSchedule vs in schedCol.Cast<ViewSchedule>())
                {
                    if (vs.IsTemplate) continue;
                    if (vs.Definition == null) continue;

                    ElementId catId = vs.Definition.CategoryId;
                    if (catId == new ElementId(BuiltInCategory.OST_PipeCurves) ||
                        catId == new ElementId(BuiltInCategory.OST_PipeFitting) ||
                        catId == new ElementId(BuiltInCategory.OST_PipeAccessory) ||
                        catId == new ElementId(BuiltInCategory.OST_DuctCurves) ||
                        catId == new ElementId(BuiltInCategory.OST_DuctFitting) ||
                        catId == ElementId.InvalidElementId) // Multi-category
                    {
                        mepSchedules.Add(vs);
                    }
                }
            }
            catch { }

            return mepSchedules;
        }

        /// <summary>
        /// Lấy danh sách các tham số thực tế từ Bảng Khối Lượng Schedule trong Revit (Scheduled Fields)
        /// và toàn bộ các tham số khả dụng trong dự án Revit (Schedulable Fields / Available Fields - 79+ items)
        /// Ưu tiên chuẩn xác theo Bảng Schedule trên Sheet hoặc ViewSchedule đang mở theo yêu cầu của Sếp Hồ
        /// </summary>
        public static List<string> GetRevitScheduleFields(Document doc, out List<string> allSchedulableFields, out string scheduleNameFound, ViewSchedule specificSchedule = null)
        {
            List<string> scheduledFields = new List<string>();
            SortedSet<string> schedulableFields = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            scheduleNameFound = string.Empty;

            if (doc == null)
            {
                allSchedulableFields = new List<string>(schedulableFields);
                return scheduledFields;
            }

            try
            {
                List<ViewSchedule> mepSchedules = GetAllMepSchedules(doc);
                ViewSchedule targetVs = specificSchedule;

                // 1. Nếu đang mở trực tiếp ViewSchedule
                ViewSchedule vsActive = doc.ActiveView as ViewSchedule;
                if (targetVs == null && vsActive != null && !vsActive.IsTemplate)
                {
                    targetVs = vsActive;
                }

                // 2. Nếu đang mở ViewSheet: Tìm bảng ScheduleSheetInstance được đặt trên Sheet này!
                ViewSheet vsheet = doc.ActiveView as ViewSheet;
                if (targetVs == null && vsheet != null)
                {
                    try
                    {
                        FilteredElementCollector ssiCol = new FilteredElementCollector(doc, vsheet.Id).OfClass(typeof(ScheduleSheetInstance));
                        foreach (ScheduleSheetInstance ssi in ssiCol.Cast<ScheduleSheetInstance>())
                        {
                            if (ssi.ScheduleId != null && ssi.ScheduleId != ElementId.InvalidElementId)
                            {
                                ViewSchedule vs = doc.GetElement(ssi.ScheduleId) as ViewSchedule;
                                if (vs != null && !vs.IsTemplate && vs.Definition != null)
                                {
                                    targetVs = vs;
                                    break;
                                }
                            }
                        }
                    }
                    catch { }
                }

                // 3. Tìm bảng Schedule có tên chứa từ khóa "Khối Lượng" hoặc "Thoát Nước" hoặc "Cấp Nước"
                if (targetVs == null && mepSchedules.Count > 0)
                {
                    targetVs = mepSchedules.FirstOrDefault(x =>
                        x.Name.IndexOf("Khối Lượng", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        x.Name.IndexOf("Khoi Luong", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        x.Name.IndexOf("Thoát Nước", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        x.Name.IndexOf("Thoat Nuoc", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        x.Name.IndexOf("Cấp Nước", StringComparison.OrdinalIgnoreCase) >= 0);

                    if (targetVs == null)
                    {
                        targetVs = mepSchedules[0];
                    }
                }

                // 4. Trích xuất CHÍNH XÁC 1:1 các Scheduled Fields từ targetVs
                if (targetVs != null && targetVs.Definition != null)
                {
                    scheduleNameFound = targetVs.Name;
                    ScheduleDefinition def = targetVs.Definition;

                    IList<ScheduleFieldId> fieldOrder = def.GetFieldOrder();
                    foreach (ScheduleFieldId fid in fieldOrder)
                    {
                        ScheduleField f = def.GetField(fid);
                        if (f != null)
                        {
                            string fName = f.GetName();
                            if (!string.IsNullOrEmpty(fName))
                            {
                                fName = fName.Trim();
                                if (!scheduledFields.Contains(fName, StringComparer.OrdinalIgnoreCase))
                                {
                                    scheduledFields.Add(fName);
                                }
                            }
                        }
                    }

                    // Trích xuất toàn bộ Available Fields (79 items)
                    try
                    {
                        IList<SchedulableField> availFields = def.GetSchedulableFields();
                        foreach (SchedulableField sf in availFields)
                        {
                            string sfName = sf.GetName(doc);
                            if (!string.IsNullOrEmpty(sfName))
                            {
                                schedulableFields.Add(sfName.Trim());
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }

            allSchedulableFields = new List<string>(schedulableFields);
            return scheduledFields;
        }
    }
}
