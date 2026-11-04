using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace HDTool
{
    /// <summary>
    /// Xử lý hình học và logic điều chỉnh khoảng cách Level so với khung Crop View (Hỗ trợ Đơn View và Đa View)
    /// </summary>
    public static class LevelCropService
    {
        private const double MM_PER_FOOT = 304.8;

        /// <summary>
        /// Kiểm tra một View có phải là View hiển thị được Level (Section, Elevation, Detail) không
        /// </summary>
        public static bool IsViewApplicable(Autodesk.Revit.DB.View view)
        {
            if (view == null || view.IsTemplate) return false;

            return (view.ViewType == ViewType.Elevation ||
                    view.ViewType == ViewType.Section ||
                    view.ViewType == ViewType.Detail);
        }

        /// <summary>
        /// Kiểm tra xem ngữ cảnh hiện tại (View đơn hoặc Sheet) có hợp lệ để chạy Add-in không
        /// </summary>
        public static bool IsValidContext(Autodesk.Revit.DB.View activeView, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (activeView == null)
            {
                errorMessage = "Không tìm thấy View đang mở.";
                return false;
            }

            if (activeView.IsTemplate)
            {
                errorMessage = "View hiện tại là View Template, vui lòng mở một View hoặc Sheet thực tế.";
                return false;
            }

            // Hợp lệ nếu là Sheet (Trang in chứa nhiều View) hoặc View trực tiếp (Section, Elevation)
            if (activeView is ViewSheet || IsViewApplicable(activeView))
            {
                return true;
            }

            errorMessage = "Lệnh áp dụng cho View Mặt cắt (Section), Mặt đứng (Elevation), Callout hoặc Trang in (Sheet) chứa các View này.";
            return false;
        }

        /// <summary>
        /// Tự động tìm kiếm tất cả các View liên quan trong màn hình làm việc hiện tại:
        /// - Nếu đang ở Sheet (như H12, H14 trong ảnh): Lấy tất cả View trên Sheet đó
        /// - Nếu đang mở nhiều View dạng Tile Windows / Tabs: Lấy tất cả View đang mở
        /// - Nếu ở View đơn: Lấy View đó
        /// </summary>
        public static List<Autodesk.Revit.DB.View> GetApplicableViewsInContext(UIDocument uidoc)
        {
            List<Autodesk.Revit.DB.View> result = new List<Autodesk.Revit.DB.View>();
            if (uidoc == null) return result;

            Document doc = uidoc.Document;
            Autodesk.Revit.DB.View activeView = uidoc.ActiveView;

            // 1. Nếu đang mở ViewSheet (Trang in chứa các khung nhìn như H12, H14 trong ảnh)
            ViewSheet sheet = activeView as ViewSheet;
            if (sheet != null)
            {
                ISet<ElementId> placedViewIds = sheet.GetAllPlacedViews();
                foreach (ElementId vId in placedViewIds)
                {
                    Autodesk.Revit.DB.View v = doc.GetElement(vId) as Autodesk.Revit.DB.View;
                    if (v != null && IsViewApplicable(v))
                    {
                        result.Add(v);
                    }
                }

                if (result.Count > 0) return result;
            }

            // 2. Nếu đang mở nhiều View trên màn hình làm việc (Tile Windows / Open Tabs)
            try
            {
                IList<UIView> openUIViews = uidoc.GetOpenUIViews();
                if (openUIViews != null && openUIViews.Count > 0)
                {
                    foreach (UIView uiv in openUIViews)
                    {
                        Autodesk.Revit.DB.View v = doc.GetElement(uiv.ViewId) as Autodesk.Revit.DB.View;
                        if (v == null) continue;

                        ViewSheet openSheet = v as ViewSheet;
                        if (openSheet != null)
                        {
                            foreach (ElementId vId in openSheet.GetAllPlacedViews())
                            {
                                Autodesk.Revit.DB.View placedV = doc.GetElement(vId) as Autodesk.Revit.DB.View;
                                if (placedV != null && IsViewApplicable(placedV) && !result.Any(x => x.Id == placedV.Id))
                                {
                                    result.Add(placedV);
                                }
                            }
                        }
                        else if (IsViewApplicable(v) && !result.Any(x => x.Id == v.Id))
                        {
                            result.Add(v);
                        }
                    }
                }
            }
            catch
            {
                // Bỏ qua nếu không lấy được open views
            }

            // 3. Nếu danh sách vẫn rỗng, thử thêm ActiveView nếu phù hợp
            if (result.Count == 0 && IsViewApplicable(activeView))
            {
                result.Add(activeView);
            }

            return result;
        }

        /// <summary>
        /// Lấy toàn bộ Level đang hiển thị trong View cụ thể
        /// </summary>
        public static List<Level> GetAllLevelsInView(Document doc, Autodesk.Revit.DB.View view)
        {
            if (doc == null || view == null) return new List<Level>();

            Dictionary<ElementId, Level> resultDict = new Dictionary<ElementId, Level>();

            try
            {
                FilteredElementCollector viewCollector = new FilteredElementCollector(doc, view.Id);
                IList<Element> viewLevels = viewCollector.OfCategory(BuiltInCategory.OST_Levels)
                                                         .WhereElementIsNotElementType()
                                                         .ToElements();
                foreach (Element e in viewLevels)
                {
                    Level l = e as Level;
                    if (l != null) resultDict[l.Id] = l;
                }
            }
            catch { }

            try
            {
                FilteredElementCollector docCollector = new FilteredElementCollector(doc);
                IList<Element> allDocLevels = docCollector.OfCategory(BuiltInCategory.OST_Levels)
                                                          .WhereElementIsNotElementType()
                                                          .ToElements();
                foreach (Element e in allDocLevels)
                {
                    Level l = e as Level;
                    if (l == null || resultDict.ContainsKey(l.Id)) continue;
                    try
                    {
                        if (l.CanBeVisibleInView(view) && !l.IsHidden(view))
                        {
                            resultDict[l.Id] = l;
                        }
                    }
                    catch { }
                }
            }
            catch { }

            return resultDict.Values.ToList();
        }

        /// <summary>
        /// Bật hoặc tắt hiển thị bong bóng đầu trục / level (Bubble / Symbol) trong View cụ thể một cách an toàn
        /// </summary>
        public static void SetBubbleVisibility(DatumPlane datum, DatumEnds end, Autodesk.Revit.DB.View view, bool show)
        {
            if (datum == null || view == null) return;
            try
            {
                if (show)
                {
                    if (!datum.IsBubbleVisibleInView(end, view))
                    {
                        datum.ShowBubbleInView(end, view);
                    }
                }
                else
                {
                    if (datum.IsBubbleVisibleInView(end, view))
                    {
                        datum.HideBubbleInView(end, view);
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Hàm nội bộ thực hiện căn chỉnh tất cả Level trong một View cụ thể kèm bật/tắt Symbol độc lập Trái / Phải
        /// </summary>
        public static int AdjustViewLevelsInternal(Document doc, Autodesk.Revit.DB.View view,
            double offsetLeftMm, double offsetRightMm, bool make2D,
            bool showSymbolLeft, bool showSymbolRight)
        {
            if (doc == null || view == null) return 0;

            // 1. Đảm bảo CropBox được bật
            try
            {
                if (!view.CropBoxActive)
                {
                    view.CropBoxActive = true;
                }
            }
            catch { }

            BoundingBoxXYZ cropBox = view.CropBox;
            if (cropBox == null) return 0;

            Transform tView = cropBox.Transform;
            Transform tInv = tView.Inverse;

            double defaultCropLeft = Math.Min(cropBox.Min.X, cropBox.Max.X);
            double defaultCropRight = Math.Max(cropBox.Min.X, cropBox.Max.X);

            double offsetLeftFeet = offsetLeftMm / MM_PER_FOOT;
            double offsetRightFeet = offsetRightMm / MM_PER_FOOT;

            // Thử lấy polygon biên Crop nếu có
            IList<CurveLoop> cropLoops = null;
            try
            {
                ViewCropRegionShapeManager shapeManager = view.GetCropRegionShapeManager();
                if (shapeManager != null && shapeManager.CanHaveShape && shapeManager.ShapeSet)
                {
                    cropLoops = shapeManager.GetCropShape();
                }
            }
            catch
            {
                cropLoops = null;
            }

            List<Level> levels = GetAllLevelsInView(doc, view);
            if (levels.Count == 0) return 0;

            int count = 0;

            foreach (Level level in levels)
            {
                try
                {
                    if (make2D)
                    {
                        try
                        {
                            if (level.GetDatumExtentTypeInView(DatumEnds.End0, view) == DatumExtentType.Model)
                            {
                                level.SetDatumExtentType(DatumEnds.End0, view, DatumExtentType.ViewSpecific);
                            }
                            if (level.GetDatumExtentTypeInView(DatumEnds.End1, view) == DatumExtentType.Model)
                            {
                                level.SetDatumExtentType(DatumEnds.End1, view, DatumExtentType.ViewSpecific);
                            }
                        }
                        catch { }
                    }

                    DatumExtentType extentType = make2D ? DatumExtentType.ViewSpecific : DatumExtentType.Model;
                    IList<Curve> curves = level.GetCurvesInView(extentType, view);
                    if (curves == null || curves.Count == 0)
                    {
                        curves = level.GetCurvesInView(DatumExtentType.Model, view);
                    }
                    if (curves == null || curves.Count == 0) continue;

                    Curve origCurve = curves[0];
                    XYZ p0 = origCurve.GetEndPoint(0);
                    XYZ p1 = origCurve.GetEndPoint(1);
                    XYZ origDir = (p1 - p0).Normalize();

                    XYZ p0_v = tInv.OfPoint(p0);
                    XYZ p1_v = tInv.OfPoint(p1);
                    double yLevel = (p0_v.Y + p1_v.Y) / 2.0;

                    double cropLeft = defaultCropLeft;
                    double cropRight = defaultCropRight;

                    if (cropLoops != null && cropLoops.Count > 0)
                    {
                        List<double> xIntersects = FindCropIntersectionsAtY(cropLoops, yLevel);
                        if (xIntersects.Count >= 2)
                        {
                            cropLeft = xIntersects.Min();
                            cropRight = xIntersects.Max();
                        }
                    }

                    double targetLeft_v = cropLeft - offsetLeftFeet;
                    double targetRight_v = cropRight + offsetRightFeet;

                    if (targetRight_v <= targetLeft_v + (10.0 / MM_PER_FOOT))
                    {
                        targetRight_v = targetLeft_v + (10.0 / MM_PER_FOOT);
                    }

                    XYZ dir_v = tInv.OfVector(origDir);
                    double dx = dir_v.X;
                    if (Math.Abs(dx) < 1e-7) continue;

                    double s_left = (targetLeft_v - p0_v.X) / dx;
                    double s_right = (targetRight_v - p0_v.X) / dx;

                    XYZ ptLeft_3d = p0 + s_left * origDir;
                    XYZ ptRight_3d = p0 + s_right * origDir;

                    Curve newCurve;
                    if (s_left < s_right)
                    {
                        newCurve = Line.CreateBound(ptLeft_3d, ptRight_3d);
                    }
                    else
                    {
                        newCurve = Line.CreateBound(ptRight_3d, ptLeft_3d);
                    }

                    try
                    {
                        level.SetCurveInView(extentType, view, newCurve);
                        count++;
                    }
                    catch
                    {
                        // Fallback sang 2D ViewSpecific nếu Model không cho phép hoặc bị khóa 3D
                        try
                        {
                            level.SetDatumExtentType(DatumEnds.End0, view, DatumExtentType.ViewSpecific);
                            level.SetDatumExtentType(DatumEnds.End1, view, DatumExtentType.ViewSpecific);
                            level.SetCurveInView(DatumExtentType.ViewSpecific, view, newCurve);
                            count++;
                        }
                        catch { }
                    }

                    // Thiết lập hiển thị bong bóng đầu tầng (Bubble / Symbol) độc lập cho 2 bên Trái / Phải
                    try
                    {
                        DatumExtentType extentCheck = make2D ? DatumExtentType.ViewSpecific : DatumExtentType.Model;
                        IList<Curve> curCurves = level.GetCurvesInView(extentCheck, view);
                        if (curCurves == null || curCurves.Count == 0) curCurves = level.GetCurvesInView(DatumExtentType.ViewSpecific, view);
                        if (curCurves == null || curCurves.Count == 0) curCurves = level.GetCurvesInView(DatumExtentType.Model, view);
                        if (curCurves != null && curCurves.Count > 0)
                        {
                            Curve c = curCurves[0];
                            XYZ pt0 = c.GetEndPoint(0);
                            XYZ pt1 = c.GetEndPoint(1);
                            XYZ pt0_v = tInv.OfPoint(pt0);
                            XYZ pt1_v = tInv.OfPoint(pt1);

                            if (pt0_v.X <= pt1_v.X)
                            {
                                SetBubbleVisibility(level, DatumEnds.End0, view, showSymbolLeft);
                                SetBubbleVisibility(level, DatumEnds.End1, view, showSymbolRight);
                            }
                            else
                            {
                                SetBubbleVisibility(level, DatumEnds.End0, view, showSymbolRight);
                                SetBubbleVisibility(level, DatumEnds.End1, view, showSymbolLeft);
                            }
                        }
                    }
                    catch { }
                }
                catch { }
            }

            return count;
        }

        /// <summary>
        /// Hàm nội bộ thực hiện căn chỉnh tất cả Level trong một View cụ thể (Overload mặc định)
        /// </summary>
        public static int AdjustViewLevelsInternal(Document doc, Autodesk.Revit.DB.View view, double offsetLeftMm, double offsetRightMm, bool make2D)
        {
            return AdjustViewLevelsInternal(doc, view, offsetLeftMm, offsetRightMm, make2D, false, true);
        }

        /// <summary>
        /// Đồng loạt căn chỉnh khoảng cách Level cho NHIỀU VIEW trong cùng 1 Transaction kèm bật/tắt Symbol độc lập
        /// </summary>
        public static int AdjustMultipleViews(Document doc, IList<Autodesk.Revit.DB.View> views,
            double offsetLeftMm, double offsetRightMm, bool make2D,
            bool showSymbolLeft, bool showSymbolRight,
            out string summaryMessage)
        {
            summaryMessage = string.Empty;

            if (doc == null || views == null || views.Count == 0)
            {
                summaryMessage = "Không có View nào được chọn.";
                return 0;
            }

            int totalLevels = 0;
            List<string> successViews = new List<string>();

            using (Transaction trans = new Transaction(doc, "Đồng loạt căn chỉnh Level cho nhiều View"))
            {
                trans.Start();

                foreach (Autodesk.Revit.DB.View v in views)
                {
                    try
                    {
                        int count = AdjustViewLevelsInternal(doc, v, offsetLeftMm, offsetRightMm, make2D, showSymbolLeft, showSymbolRight);
                        if (count > 0)
                        {
                            totalLevels += count;
                            successViews.Add(string.Format("{0} ({1} Level)", v.Name, count));
                        }
                    }
                    catch { }
                }

                trans.Commit();
            }

            if (totalLevels > 0)
            {
                summaryMessage = string.Format("ĐÃ ĐIỀU CHỈNH THÀNH CÔNG:\n- Tổng số: {0} Level\n- Trong {1} View: {2}",
                    totalLevels,
                    successViews.Count,
                    string.Join(", ", successViews.ToArray()));
            }
            else
            {
                summaryMessage = "Không tìm thấy Level nào cần điều chỉnh trong các View đã chọn.";
            }

            return totalLevels;
        }

        /// <summary>
        /// Đồng loạt căn chỉnh khoảng cách Level cho NHIỀU VIEW (Overload mặc định)
        /// </summary>
        public static int AdjustMultipleViews(Document doc, IList<Autodesk.Revit.DB.View> views, double offsetLeftMm, double offsetRightMm, bool make2D, out string summaryMessage)
        {
            return AdjustMultipleViews(doc, views, offsetLeftMm, offsetRightMm, make2D, false, true, out summaryMessage);
        }

        /// <summary>
        /// Thực hiện điều chỉnh khoảng cách của danh sách Level trong một View đơn kèm bật/tắt Symbol độc lập
        /// </summary>
        public static int AdjustLevels(Document doc, Autodesk.Revit.DB.View view, IList<Level> levels,
            double offsetLeftMm, double offsetRightMm, bool make2D,
            bool showSymbolLeft, bool showSymbolRight,
            out string resultMessage)
        {
            resultMessage = string.Empty;

            if (doc == null || view == null)
            {
                resultMessage = "Dữ liệu Document hoặc View không hợp lệ.";
                return 0;
            }

            if (levels == null || levels.Count == 0)
            {
                resultMessage = "Không có Level nào được chọn.";
                return 0;
            }

            int successCount = 0;

            using (Transaction trans = new Transaction(doc, "Điều chỉnh khoảng cách Level so với Crop View"))
            {
                trans.Start();

                try
                {
                    try
                    {
                        if (!view.CropBoxActive)
                        {
                            view.CropBoxActive = true;
                        }
                    }
                    catch { }

                    BoundingBoxXYZ cropBox = view.CropBox;
                    if (cropBox == null)
                    {
                        trans.RollBack();
                        resultMessage = "Không thể lấy thông tin khung Crop View của View hiện tại.";
                        return 0;
                    }

                    Transform tView = cropBox.Transform;
                    Transform tInv = tView.Inverse;

                    double defaultCropLeft = Math.Min(cropBox.Min.X, cropBox.Max.X);
                    double defaultCropRight = Math.Max(cropBox.Min.X, cropBox.Max.X);

                    double offsetLeftFeet = offsetLeftMm / MM_PER_FOOT;
                    double offsetRightFeet = offsetRightMm / MM_PER_FOOT;

                    IList<CurveLoop> cropLoops = null;
                    try
                    {
                        ViewCropRegionShapeManager shapeManager = view.GetCropRegionShapeManager();
                        if (shapeManager != null && shapeManager.CanHaveShape && shapeManager.ShapeSet)
                        {
                            cropLoops = shapeManager.GetCropShape();
                        }
                    }
                    catch
                    {
                        cropLoops = null;
                    }

                    foreach (Level level in levels)
                    {
                        try
                        {
                            if (make2D)
                            {
                                try
                                {
                                    if (level.GetDatumExtentTypeInView(DatumEnds.End0, view) == DatumExtentType.Model)
                                    {
                                        level.SetDatumExtentType(DatumEnds.End0, view, DatumExtentType.ViewSpecific);
                                    }
                                    if (level.GetDatumExtentTypeInView(DatumEnds.End1, view) == DatumExtentType.Model)
                                    {
                                        level.SetDatumExtentType(DatumEnds.End1, view, DatumExtentType.ViewSpecific);
                                    }
                                }
                                catch { }
                            }

                            DatumExtentType extentType = make2D ? DatumExtentType.ViewSpecific : DatumExtentType.Model;
                            IList<Curve> curves = level.GetCurvesInView(extentType, view);

                            if (curves == null || curves.Count == 0)
                            {
                                curves = level.GetCurvesInView(DatumExtentType.Model, view);
                            }

                            if (curves == null || curves.Count == 0) continue;

                            Curve origCurve = curves[0];
                            XYZ p0 = origCurve.GetEndPoint(0);
                            XYZ p1 = origCurve.GetEndPoint(1);
                            XYZ origDir = (p1 - p0).Normalize();

                            XYZ p0_v = tInv.OfPoint(p0);
                            XYZ p1_v = tInv.OfPoint(p1);
                            double yLevel = (p0_v.Y + p1_v.Y) / 2.0;

                            double cropLeft = defaultCropLeft;
                            double cropRight = defaultCropRight;

                            if (cropLoops != null && cropLoops.Count > 0)
                            {
                                List<double> xIntersects = FindCropIntersectionsAtY(cropLoops, yLevel);
                                if (xIntersects.Count >= 2)
                                {
                                    cropLeft = xIntersects.Min();
                                    cropRight = xIntersects.Max();
                                }
                            }

                            double targetLeft_v = cropLeft - offsetLeftFeet;
                            double targetRight_v = cropRight + offsetRightFeet;

                            if (targetRight_v <= targetLeft_v + (10.0 / MM_PER_FOOT))
                            {
                                targetRight_v = targetLeft_v + (10.0 / MM_PER_FOOT);
                            }

                            XYZ dir_v = tInv.OfVector(origDir);
                            double dx = dir_v.X;
                            if (Math.Abs(dx) < 1e-7) continue;

                            double s_left = (targetLeft_v - p0_v.X) / dx;
                            double s_right = (targetRight_v - p0_v.X) / dx;

                            XYZ ptLeft_3d = p0 + s_left * origDir;
                            XYZ ptRight_3d = p0 + s_right * origDir;

                            Curve newCurve;
                            if (s_left < s_right)
                            {
                                newCurve = Line.CreateBound(ptLeft_3d, ptRight_3d);
                            }
                            else
                            {
                                newCurve = Line.CreateBound(ptRight_3d, ptLeft_3d);
                            }

                            try
                            {
                                level.SetCurveInView(extentType, view, newCurve);
                                successCount++;
                            }
                            catch
                            {
                                // Fallback sang 2D ViewSpecific nếu Model không cho phép hoặc bị khóa 3D
                                try
                                {
                                    level.SetDatumExtentType(DatumEnds.End0, view, DatumExtentType.ViewSpecific);
                                    level.SetDatumExtentType(DatumEnds.End1, view, DatumExtentType.ViewSpecific);
                                    level.SetCurveInView(DatumExtentType.ViewSpecific, view, newCurve);
                                    successCount++;
                                }
                                catch { }
                            }

                            // Thiết lập hiển thị bong bóng đầu tầng (Bubble / Symbol) độc lập cho 2 bên Trái / Phải
                            try
                            {
                                DatumExtentType extentCheck = make2D ? DatumExtentType.ViewSpecific : DatumExtentType.Model;
                                IList<Curve> curCurves = level.GetCurvesInView(extentCheck, view);
                                if (curCurves == null || curCurves.Count == 0) curCurves = level.GetCurvesInView(DatumExtentType.ViewSpecific, view);
                                if (curCurves == null || curCurves.Count == 0) curCurves = level.GetCurvesInView(DatumExtentType.Model, view);
                                if (curCurves != null && curCurves.Count > 0)
                                {
                                    Curve c = curCurves[0];
                                    XYZ pt0 = c.GetEndPoint(0);
                                    XYZ pt1 = c.GetEndPoint(1);
                                    XYZ pt0_v = tInv.OfPoint(pt0);
                                    XYZ pt1_v = tInv.OfPoint(pt1);

                                    if (pt0_v.X <= pt1_v.X)
                                    {
                                        SetBubbleVisibility(level, DatumEnds.End0, view, showSymbolLeft);
                                        SetBubbleVisibility(level, DatumEnds.End1, view, showSymbolRight);
                                    }
                                    else
                                    {
                                        SetBubbleVisibility(level, DatumEnds.End0, view, showSymbolRight);
                                        SetBubbleVisibility(level, DatumEnds.End1, view, showSymbolLeft);
                                    }
                                }
                            }
                            catch { }
                        }
                        catch { }
                    }

                    trans.Commit();
                    resultMessage = string.Format("Đã điều chỉnh thành công {0} Level!", successCount);
                    return successCount;
                }
                catch (Exception ex)
                {
                    trans.RollBack();
                    resultMessage = "Đã xảy ra lỗi: " + ex.Message;
                    return 0;
                }
            }
        }

        /// <summary>
        /// Thực hiện điều chỉnh khoảng cách của danh sách Level trong một View đơn (Overload mặc định)
        /// </summary>
        public static int AdjustLevels(Document doc, Autodesk.Revit.DB.View view, IList<Level> levels,
            double offsetLeftMm, double offsetRightMm, bool make2D, out string resultMessage)
        {
            return AdjustLevels(doc, view, levels, offsetLeftMm, offsetRightMm, make2D, false, true, out resultMessage);
        }

        private static List<double> FindCropIntersectionsAtY(IList<CurveLoop> loops, double yLevel)
        {
            List<double> intersections = new List<double>();
            if (loops == null) return intersections;

            foreach (CurveLoop loop in loops)
            {
                foreach (Curve c in loop)
                {
                    XYZ start = c.GetEndPoint(0);
                    XYZ end = c.GetEndPoint(1);

                    double minY = Math.Min(start.Y, end.Y);
                    double maxY = Math.Max(start.Y, end.Y);

                    if (yLevel >= minY - 1e-6 && yLevel <= maxY + 1e-6)
                    {
                        double dy = end.Y - start.Y;
                        if (Math.Abs(dy) > 1e-7)
                        {
                            double fraction = (yLevel - start.Y) / dy;
                            if (fraction >= -1e-6 && fraction <= 1.0 + 1e-6)
                            {
                                double x = start.X + fraction * (end.X - start.X);
                                intersections.Add(x);
                            }
                        }
                        else
                        {
                            intersections.Add(start.X);
                            intersections.Add(end.X);
                        }
                    }
                }
            }

            return intersections;
        }
    }
}
