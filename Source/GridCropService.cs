using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace HDTool
{
    /// <summary>
    /// Xử lý hình học và logic căn chỉnh lưới trục (Grids) theo khung Crop View trên mặt bằng
    /// </summary>
    public static class GridCropService
    {
        private const double MM_PER_FOOT = 304.8;

        /// <summary>
        /// Kiểm tra xem View có phải là Mặt bằng (FloorPlan, CeilingPlan, EngineeringPlan, AreaPlan) không
        /// </summary>
        public static bool IsPlanView(Autodesk.Revit.DB.View view)
        {
            if (view == null || view.IsTemplate) return false;

            return (view.ViewType == ViewType.FloorPlan ||
                    view.ViewType == ViewType.CeilingPlan ||
                    view.ViewType == ViewType.EngineeringPlan ||
                    view.ViewType == ViewType.AreaPlan);
        }

        /// <summary>
        /// Kiểm tra xem View có phải là Mặt đứng (Elevation), Mặt cắt (Section) hoặc Chi tiết (Detail) không
        /// </summary>
        public static bool IsElevationOrSectionView(Autodesk.Revit.DB.View view)
        {
            if (view == null || view.IsTemplate) return false;

            return (view.ViewType == ViewType.Elevation ||
                    view.ViewType == ViewType.Section ||
                    view.ViewType == ViewType.Detail);
        }

        /// <summary>
        /// Kiểm tra xem View có phù hợp để căn chỉnh Grids không (Mặt bằng, Mặt đứng, Mặt cắt)
        /// </summary>
        public static bool IsViewApplicableForGrids(Autodesk.Revit.DB.View view)
        {
            if (view == null || view.IsTemplate) return false;
            return IsPlanView(view) || IsElevationOrSectionView(view);
        }

        /// <summary>
        /// Kiểm tra ngữ cảnh hiện tại có phù hợp để căn chỉnh Grid không (Mặt bằng, Mặt đứng, Mặt cắt hoặc Sheet chứa các View này)
        /// </summary>
        public static bool IsValidContextForGrids(Autodesk.Revit.DB.View activeView, out string errorMessage)
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

            if (activeView is ViewSheet || IsViewApplicableForGrids(activeView))
            {
                return true;
            }

            errorMessage = "Lệnh áp dụng cho Mặt bằng, Mặt đứng, Mặt cắt hoặc Trang in (Sheet) chứa các View này.";
            return false;
        }

        /// <summary>
        /// Lấy tất cả các View áp dụng cho căn Grids trong ngữ cảnh hiện tại (Trang in hoặc View đang mở)
        /// </summary>
        public static List<Autodesk.Revit.DB.View> GetApplicableViewsForGrids(UIDocument uidoc)
        {
            List<Autodesk.Revit.DB.View> result = new List<Autodesk.Revit.DB.View>();
            if (uidoc == null) return result;

            Document doc = uidoc.Document;
            Autodesk.Revit.DB.View activeView = uidoc.ActiveView;

            ViewSheet sheet = activeView as ViewSheet;
            if (sheet != null)
            {
                ISet<ElementId> placedViewIds = sheet.GetAllPlacedViews();
                foreach (ElementId vId in placedViewIds)
                {
                    Autodesk.Revit.DB.View v = doc.GetElement(vId) as Autodesk.Revit.DB.View;
                    if (v != null && IsViewApplicableForGrids(v))
                    {
                        List<Grid> grids = GetAllGridsInView(doc, v);
                        if (grids != null && grids.Count > 0)
                        {
                            result.Add(v);
                        }
                    }
                }
                if (result.Count > 0) return result;
            }

            if (IsViewApplicableForGrids(activeView))
            {
                result.Add(activeView);
            }

            return result;
        }

        /// <summary>
        /// Lấy tất cả các View Mặt bằng/Mặt đứng áp dụng cho Grid (giữ tên tương thích ngược)
        /// </summary>
        public static List<Autodesk.Revit.DB.View> GetApplicablePlanViews(UIDocument uidoc)
        {
            return GetApplicableViewsForGrids(uidoc);
        }

        /// <summary>
        /// Lấy tất cả Grids hiển thị trong View cụ thể
        /// </summary>
        public static List<Grid> GetAllGridsInView(Document doc, Autodesk.Revit.DB.View view)
        {
            if (doc == null || view == null) return new List<Grid>();

            Dictionary<ElementId, Grid> resultDict = new Dictionary<ElementId, Grid>();

            // 1. Lấy các Grid đang trong view collector
            try
            {
                FilteredElementCollector viewCollector = new FilteredElementCollector(doc, view.Id);
                IList<Element> viewGrids = viewCollector.OfCategory(BuiltInCategory.OST_Grids)
                                                        .WhereElementIsNotElementType()
                                                        .ToElements();
                foreach (Element e in viewGrids)
                {
                    Grid g = e as Grid;
                    if (g != null)
                    {
                        resultDict[g.Id] = g;
                    }
                }
            }
            catch { }

            // 2. Quét toàn bộ Grid trong Document để tự động tìm các Grid có đường tim đi qua Crop View
            // (kể cả khi 2 đầu Grid đang bị kéo ra ngoài vùng Crop View)
            try
            {
                FilteredElementCollector docCollector = new FilteredElementCollector(doc);
                IList<Element> allDocGrids = docCollector.OfCategory(BuiltInCategory.OST_Grids)
                                                         .WhereElementIsNotElementType()
                                                         .ToElements();

                BoundingBoxXYZ cropBox = view.CropBox;
                if (cropBox != null)
                {
                    Transform tInv = cropBox.Transform.Inverse;

                    double cropLeft = Math.Min(cropBox.Min.X, cropBox.Max.X);
                    double cropRight = Math.Max(cropBox.Min.X, cropBox.Max.X);
                    double cropBottom = Math.Min(cropBox.Min.Y, cropBox.Max.Y);
                    double cropTop = Math.Max(cropBox.Min.Y, cropBox.Max.Y);

                    double cropW = cropRight - cropLeft;
                    double cropH = cropTop - cropBottom;

                    // Margin mở rộng khoảng 5 mét (16.4 ft) hoặc 30% kích thước Crop để bao trọn cả các trục biên
                    double marginX = Math.Max(16.4, cropW * 0.3);
                    double marginY = Math.Max(16.4, cropH * 0.3);

                    foreach (Element e in allDocGrids)
                    {
                        Grid g = e as Grid;
                        if (g == null || resultDict.ContainsKey(g.Id)) continue;

                        try
                        {
                            if (!g.CanBeVisibleInView(view) || g.IsHidden(view))
                            {
                                continue;
                            }
                        }
                        catch
                        {
                            continue;
                        }

                        try
                        {
                            Curve c = null;
                            try
                            {
                                IList<Curve> curves = g.GetCurvesInView(DatumExtentType.ViewSpecific, view);
                                if (curves != null && curves.Count > 0) c = curves[0];
                            }
                            catch { }

                            if (c == null)
                            {
                                try
                                {
                                    IList<Curve> curves = g.GetCurvesInView(DatumExtentType.Model, view);
                                    if (curves != null && curves.Count > 0) c = curves[0];
                                }
                                catch { }
                            }

                            if (c == null && IsPlanView(view))
                            {
                                try { c = g.Curve; } catch { }
                            }

                            if (c == null) continue;

                            Line line = c as Line;
                            if (line == null)
                            {
                                resultDict[g.Id] = g;
                                continue;
                            }

                            XYZ p0 = line.GetEndPoint(0);
                            XYZ p1 = line.GetEndPoint(1);
                            XYZ origDir = (p1 - p0).Normalize();

                            XYZ p0_v = tInv.OfPoint(p0);
                            XYZ dir_v = tInv.OfVector(origDir).Normalize();

                            double absX = Math.Abs(dir_v.X);
                            double absY = Math.Abs(dir_v.Y);

                            bool intersectsCrop = false;

                            if (absX >= absY)
                            {
                                double yGrid = p0_v.Y;
                                if (yGrid >= (cropBottom - marginY) && yGrid <= (cropTop + marginY))
                                {
                                    intersectsCrop = true;
                                }
                            }
                            else
                            {
                                double xGrid = p0_v.X;
                                if (xGrid >= (cropLeft - marginX) && xGrid <= (cropRight + marginX))
                                {
                                    intersectsCrop = true;
                                }
                            }

                            if (!intersectsCrop)
                            {
                                double c1 = -dir_v.Y * (cropLeft - marginX - p0_v.X) + dir_v.X * (cropBottom - marginY - p0_v.Y);
                                double c2 = -dir_v.Y * (cropRight + marginX - p0_v.X) + dir_v.X * (cropBottom - marginY - p0_v.Y);
                                double c3 = -dir_v.Y * (cropRight + marginX - p0_v.X) + dir_v.X * (cropTop + marginY - p0_v.Y);
                                double c4 = -dir_v.Y * (cropLeft - marginX - p0_v.X) + dir_v.X * (cropTop + marginY - p0_v.Y);

                                double minC = Math.Min(Math.Min(c1, c2), Math.Min(c3, c4));
                                double maxC = Math.Max(Math.Max(c1, c2), Math.Max(c3, c4));

                                if (minC <= 0 && maxC >= 0)
                                {
                                    intersectsCrop = true;
                                }
                            }

                            if (intersectsCrop)
                            {
                                resultDict[g.Id] = g;
                            }
                        }
                        catch { }
                    }
                }
                else
                {
                    foreach (Element e in allDocGrids)
                    {
                        Grid g = e as Grid;
                        if (g != null && !resultDict.ContainsKey(g.Id))
                        {
                            try
                            {
                                if (g.CanBeVisibleInView(view) && !g.IsHidden(view))
                                {
                                    resultDict[g.Id] = g;
                                }
                            }
                            catch { }
                        }
                    }
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
        /// Căn chỉnh tất cả các Grid trong View theo khung Crop View kèm tùy chọn bật/tắt Symbol 4 hướng
        /// </summary>
        public static int AdjustAllGridsInView(Document doc, Autodesk.Revit.DB.View view,
            double offsetTopMm, double offsetBottomMm, double offsetLeftMm, double offsetRightMm,
            bool make2D,
            bool showSymbolTop, bool showSymbolBottom, bool showSymbolLeft, bool showSymbolRight,
            out string resultMessage)
        {
            List<Grid> allGrids = GetAllGridsInView(doc, view);
            return AdjustGrids(doc, view, allGrids, offsetTopMm, offsetBottomMm, offsetLeftMm, offsetRightMm,
                make2D, showSymbolTop, showSymbolBottom, showSymbolLeft, showSymbolRight, out resultMessage);
        }

        /// <summary>
        /// Căn chỉnh tất cả các Grid trong View (Overload mặc định bật cả 4 hướng)
        /// </summary>
        public static int AdjustAllGridsInView(Document doc, Autodesk.Revit.DB.View view,
            double offsetTopMm, double offsetBottomMm, double offsetLeftMm, double offsetRightMm,
            bool make2D, out string resultMessage)
        {
            return AdjustAllGridsInView(doc, view, offsetTopMm, offsetBottomMm, offsetLeftMm, offsetRightMm,
                make2D, true, true, true, true, out resultMessage);
        }

        /// <summary>
        /// Căn chỉnh danh sách Grid cụ thể trong View theo khung Crop View kèm tùy chọn bật/tắt Symbol 4 hướng
        /// </summary>
        public static int AdjustGrids(Document doc, Autodesk.Revit.DB.View view, IList<Grid> grids,
            double offsetTopMm, double offsetBottomMm, double offsetLeftMm, double offsetRightMm,
            bool make2D,
            bool showSymbolTop, bool showSymbolBottom, bool showSymbolLeft, bool showSymbolRight,
            out string resultMessage)
        {
            resultMessage = string.Empty;

            if (doc == null || view == null)
            {
                resultMessage = "Document hoặc View không hợp lệ.";
                return 0;
            }

            if (grids == null || grids.Count == 0)
            {
                resultMessage = "Không tìm thấy Grid nào cần căn chỉnh.";
                return 0;
            }

            int successCount = 0;

            using (Transaction trans = new Transaction(doc, "Căn chỉnh lưới trục (Grids) theo Crop View"))
            {
                trans.Start();

                try
                {
                    // 1. Đảm bảo CropBox được bật
                    if (!view.CropBoxActive)
                    {
                        view.CropBoxActive = true;
                    }

                    BoundingBoxXYZ cropBox = view.CropBox;
                    if (cropBox == null)
                    {
                        trans.RollBack();
                        resultMessage = "Không thể lấy thông tin khung Crop View của View hiện tại.";
                        return 0;
                    }

                    Transform tView = cropBox.Transform;
                    Transform tInv = tView.Inverse;

                    double cropLeft = Math.Min(cropBox.Min.X, cropBox.Max.X);
                    double cropRight = Math.Max(cropBox.Min.X, cropBox.Max.X);
                    double cropBottom = Math.Min(cropBox.Min.Y, cropBox.Max.Y);
                    double cropTop = Math.Max(cropBox.Min.Y, cropBox.Max.Y);

                    double offsetTopFeet = offsetTopMm / MM_PER_FOOT;
                    double offsetBottomFeet = offsetBottomMm / MM_PER_FOOT;
                    double offsetLeftFeet = offsetLeftMm / MM_PER_FOOT;
                    double offsetRightFeet = offsetRightMm / MM_PER_FOOT;

                    successCount = AdjustGridsInternal(doc, view, grids,
                        offsetTopFeet, offsetBottomFeet, offsetLeftFeet, offsetRightFeet,
                        make2D, showSymbolTop, showSymbolBottom, showSymbolLeft, showSymbolRight);

                    trans.Commit();
                    resultMessage = string.Format("Đã căn chỉnh thành công {0} Grid theo khung Crop View!", successCount);
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
        /// Căn chỉnh danh sách Grid (Overload mặc định bật cả 4 hướng)
        /// </summary>
        public static int AdjustGrids(Document doc, Autodesk.Revit.DB.View view, IList<Grid> grids,
            double offsetTopMm, double offsetBottomMm, double offsetLeftMm, double offsetRightMm,
            bool make2D, out string resultMessage)
        {
            return AdjustGrids(doc, view, grids, offsetTopMm, offsetBottomMm, offsetLeftMm, offsetRightMm,
                make2D, true, true, true, true, out resultMessage);
        }

        /// <summary>
        /// Đồng loạt căn chỉnh Grids cho nhiều Mặt bằng kèm tùy chọn bật/tắt Symbol 4 hướng
        /// </summary>
        public static int AdjustMultiplePlanViews(Document doc, IList<Autodesk.Revit.DB.View> planViews,
            double offsetTopMm, double offsetBottomMm, double offsetLeftMm, double offsetRightMm,
            bool make2D,
            bool showSymbolTop, bool showSymbolBottom, bool showSymbolLeft, bool showSymbolRight,
            out string summaryMessage)
        {
            summaryMessage = string.Empty;

            if (doc == null || planViews == null || planViews.Count == 0)
            {
                summaryMessage = "Không có View nào được chọn.";
                return 0;
            }

            int totalGrids = 0;
            List<string> updatedViews = new List<string>();

            double offsetTopFeet = offsetTopMm / MM_PER_FOOT;
            double offsetBottomFeet = offsetBottomMm / MM_PER_FOOT;
            double offsetLeftFeet = offsetLeftMm / MM_PER_FOOT;
            double offsetRightFeet = offsetRightMm / MM_PER_FOOT;

            using (Transaction trans = new Transaction(doc, "Đồng loạt căn chỉnh Grids cho các View"))
            {
                trans.Start();

                foreach (Autodesk.Revit.DB.View v in planViews)
                {
                    try
                    {
                        List<Grid> grids = GetAllGridsInView(doc, v);
                        if (grids.Count == 0) continue;

                        int count = AdjustGridsInternal(doc, v, grids,
                            offsetTopFeet, offsetBottomFeet, offsetLeftFeet, offsetRightFeet,
                            make2D, showSymbolTop, showSymbolBottom, showSymbolLeft, showSymbolRight);
                        if (count > 0)
                        {
                            totalGrids += count;
                            updatedViews.Add(string.Format("{0} ({1} Grid)", v.Name, count));
                        }
                    }
                    catch { }
                }

                trans.Commit();
            }

            if (totalGrids > 0)
            {
                summaryMessage = string.Format("ĐÃ CĂN CHỈNH THÀNH CÔNG:\n- Tổng số: {0} Grid\n- Trong {1} View: {2}",
                    totalGrids, updatedViews.Count, string.Join(", ", updatedViews.ToArray()));
            }
            else
            {
                summaryMessage = "Không tìm thấy Grid nào cần căn chỉnh trong các View đã chọn.";
            }

            return totalGrids;
        }

        /// <summary>
        /// Đồng loạt căn chỉnh Grids cho nhiều Mặt bằng (Overload mặc định bật cả 4 hướng)
        /// </summary>
        public static int AdjustMultiplePlanViews(Document doc, IList<Autodesk.Revit.DB.View> planViews,
            double offsetTopMm, double offsetBottomMm, double offsetLeftMm, double offsetRightMm,
            bool make2D, out string summaryMessage)
        {
            return AdjustMultiplePlanViews(doc, planViews, offsetTopMm, offsetBottomMm, offsetLeftMm, offsetRightMm,
                make2D, true, true, true, true, out summaryMessage);
        }

        /// <summary>
        /// Đồng loạt căn chỉnh Grids cho nhiều View (Mặt bằng, Mặt đứng, Mặt cắt)
        /// </summary>
        public static int AdjustMultipleViews(Document doc, IList<Autodesk.Revit.DB.View> views,
            double offsetTopMm, double offsetBottomMm, double offsetLeftMm, double offsetRightMm,
            bool make2D,
            bool showSymbolTop, bool showSymbolBottom, bool showSymbolLeft, bool showSymbolRight,
            out string summaryMessage)
        {
            return AdjustMultiplePlanViews(doc, views, offsetTopMm, offsetBottomMm, offsetLeftMm, offsetRightMm,
                make2D, showSymbolTop, showSymbolBottom, showSymbolLeft, showSymbolRight, out summaryMessage);
        }

        private static int AdjustGridsInternal(Document doc, Autodesk.Revit.DB.View view, IList<Grid> grids,
            double offsetTopFeet, double offsetBottomFeet, double offsetLeftFeet, double offsetRightFeet,
            bool make2D,
            bool showSymbolTop, bool showSymbolBottom, bool showSymbolLeft, bool showSymbolRight)
        {
            if (!view.CropBoxActive)
            {
                view.CropBoxActive = true;
            }

            BoundingBoxXYZ cropBox = view.CropBox;
            if (cropBox == null) return 0;

            Transform tView = cropBox.Transform;
            Transform tInv = tView.Inverse;

            double cropLeft = Math.Min(cropBox.Min.X, cropBox.Max.X);
            double cropRight = Math.Max(cropBox.Min.X, cropBox.Max.X);
            double cropBottom = Math.Min(cropBox.Min.Y, cropBox.Max.Y);
            double cropTop = Math.Max(cropBox.Min.Y, cropBox.Max.Y);

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

            int successCount = 0;

            foreach (Grid grid in grids)
            {
                try
                {
                    if (make2D)
                    {
                        try
                        {
                            if (grid.GetDatumExtentTypeInView(DatumEnds.End0, view) == DatumExtentType.Model)
                            {
                                grid.SetDatumExtentType(DatumEnds.End0, view, DatumExtentType.ViewSpecific);
                            }
                            if (grid.GetDatumExtentTypeInView(DatumEnds.End1, view) == DatumExtentType.Model)
                            {
                                grid.SetDatumExtentType(DatumEnds.End1, view, DatumExtentType.ViewSpecific);
                            }
                        }
                        catch { }
                    }

                    DatumExtentType extentType = make2D ? DatumExtentType.ViewSpecific : DatumExtentType.Model;
                    IList<Curve> curves = grid.GetCurvesInView(extentType, view);
                    if (curves == null || curves.Count == 0)
                    {
                        curves = grid.GetCurvesInView(DatumExtentType.Model, view);
                    }
                    if ((curves == null || curves.Count == 0) && IsPlanView(view))
                    {
                        try { curves = new List<Curve> { grid.Curve }; } catch { }
                    }
                    if (curves == null || curves.Count == 0) continue;

                    Curve origCurve = curves[0];
                    Line origLine = origCurve as Line;
                    if (origLine == null) continue;

                    XYZ p0 = origLine.GetEndPoint(0);
                    XYZ p1 = origLine.GetEndPoint(1);
                    XYZ origDir = (p1 - p0).Normalize();

                    XYZ p0_v = tInv.OfPoint(p0);
                    XYZ p1_v = tInv.OfPoint(p1);
                    XYZ dir_v = tInv.OfVector(origDir).Normalize();

                    double absX = Math.Abs(dir_v.X);
                    double absY = Math.Abs(dir_v.Y);

                    double s_start = 0.0;
                    double s_end = 0.0;

                    if (absX >= absY)
                    {
                        if (Math.Abs(dir_v.X) < 1e-6) continue;

                        double yGrid = (p0_v.Y + p1_v.Y) / 2.0;
                        double cLeft = cropLeft;
                        double cRight = cropRight;

                        if (cropLoops != null && cropLoops.Count > 0)
                        {
                            List<double> xIntersects = FindCropIntersectionsAtY(cropLoops, yGrid);
                            if (xIntersects.Count >= 2)
                            {
                                cLeft = xIntersects.Min();
                                cRight = xIntersects.Max();
                            }
                        }

                        double targetLeft_v = cLeft - offsetLeftFeet;
                        double targetRight_v = cRight + offsetRightFeet;

                        if (targetRight_v <= targetLeft_v + (10.0 / MM_PER_FOOT))
                        {
                            targetRight_v = targetLeft_v + (10.0 / MM_PER_FOOT);
                        }

                        s_start = (targetLeft_v - p0_v.X) / dir_v.X;
                        s_end = (targetRight_v - p0_v.X) / dir_v.X;
                    }
                    else
                    {
                        if (Math.Abs(dir_v.Y) < 1e-6) continue;

                        double xGrid = (p0_v.X + p1_v.X) / 2.0;
                        double cBottom = cropBottom;
                        double cTop = cropTop;

                        if (cropLoops != null && cropLoops.Count > 0)
                        {
                            List<double> yIntersects = FindCropIntersectionsAtX(cropLoops, xGrid);
                            if (yIntersects.Count >= 2)
                            {
                                cBottom = yIntersects.Min();
                                cTop = yIntersects.Max();
                            }
                        }

                        double targetBottom_v = cBottom - offsetBottomFeet;
                        double targetTop_v = cTop + offsetTopFeet;

                        if (targetTop_v <= targetBottom_v + (10.0 / MM_PER_FOOT))
                        {
                            targetTop_v = targetBottom_v + (10.0 / MM_PER_FOOT);
                        }

                        s_start = (targetBottom_v - p0_v.Y) / dir_v.Y;
                        s_end = (targetTop_v - p0_v.Y) / dir_v.Y;
                    }

                    XYZ ptStart_3d = p0 + s_start * origDir;
                    XYZ ptEnd_3d = p0 + s_end * origDir;

                    Curve newCurve;
                    if (s_start < s_end)
                    {
                        newCurve = Line.CreateBound(ptStart_3d, ptEnd_3d);
                    }
                    else
                    {
                        newCurve = Line.CreateBound(ptEnd_3d, ptStart_3d);
                    }

                    try
                    {
                        grid.SetCurveInView(extentType, view, newCurve);
                        successCount++;
                    }
                    catch
                    {
                        // Fallback 1: ViewSpecific
                        try
                        {
                            grid.SetDatumExtentType(DatumEnds.End0, view, DatumExtentType.ViewSpecific);
                            grid.SetDatumExtentType(DatumEnds.End1, view, DatumExtentType.ViewSpecific);
                            grid.SetCurveInView(DatumExtentType.ViewSpecific, view, newCurve);
                            successCount++;
                        }
                        catch
                        {
                            // Fallback 2: Model
                            try
                            {
                                grid.SetDatumExtentType(DatumEnds.End0, view, DatumExtentType.Model);
                                grid.SetDatumExtentType(DatumEnds.End1, view, DatumExtentType.Model);
                                grid.SetCurveInView(DatumExtentType.Model, view, newCurve);
                                successCount++;
                            }
                            catch { }
                        }
                    }

                    // Thiết lập hiển thị bong bóng trục (Bubble / Symbol) theo 4 hướng độc lập
                    try
                    {
                        DatumExtentType extentCheck = make2D ? DatumExtentType.ViewSpecific : DatumExtentType.Model;
                        IList<Curve> curCurves = grid.GetCurvesInView(extentCheck, view);
                        if (curCurves == null || curCurves.Count == 0) curCurves = grid.GetCurvesInView(DatumExtentType.ViewSpecific, view);
                        if (curCurves == null || curCurves.Count == 0) curCurves = grid.GetCurvesInView(DatumExtentType.Model, view);
                        if (curCurves != null && curCurves.Count > 0)
                        {
                            Curve c = curCurves[0];
                            XYZ pt0 = c.GetEndPoint(0);
                            XYZ pt1 = c.GetEndPoint(1);
                            XYZ pt0_v = tInv.OfPoint(pt0);
                            XYZ pt1_v = tInv.OfPoint(pt1);

                            if (absX >= absY)
                            {
                                // Trục ngang (Horizontal Grid): 2 đầu là Trái (Left) và Phải (Right)
                                if (pt0_v.X <= pt1_v.X)
                                {
                                    SetBubbleVisibility(grid, DatumEnds.End0, view, showSymbolLeft);
                                    SetBubbleVisibility(grid, DatumEnds.End1, view, showSymbolRight);
                                }
                                else
                                {
                                    SetBubbleVisibility(grid, DatumEnds.End0, view, showSymbolRight);
                                    SetBubbleVisibility(grid, DatumEnds.End1, view, showSymbolLeft);
                                }
                            }
                            else
                            {
                                // Trục dọc (Vertical Grid): 2 đầu là Dưới (Bottom) và Trên (Top)
                                if (pt0_v.Y <= pt1_v.Y)
                                {
                                    SetBubbleVisibility(grid, DatumEnds.End0, view, showSymbolBottom);
                                    SetBubbleVisibility(grid, DatumEnds.End1, view, showSymbolTop);
                                }
                                else
                                {
                                    SetBubbleVisibility(grid, DatumEnds.End0, view, showSymbolTop);
                                    SetBubbleVisibility(grid, DatumEnds.End1, view, showSymbolBottom);
                                }
                            }
                        }
                    }
                    catch { }
                }
                catch { }
            }

            return successCount;
        }

        private static List<double> FindCropIntersectionsAtY(IList<CurveLoop> loops, double yVal)
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

                    if (yVal >= minY - 1e-6 && yVal <= maxY + 1e-6)
                    {
                        double dy = end.Y - start.Y;
                        if (Math.Abs(dy) > 1e-7)
                        {
                            double fraction = (yVal - start.Y) / dy;
                            if (fraction >= -1e-6 && fraction <= 1.0 + 1e-6)
                            {
                                intersections.Add(start.X + fraction * (end.X - start.X));
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

        private static List<double> FindCropIntersectionsAtX(IList<CurveLoop> loops, double xVal)
        {
            List<double> intersections = new List<double>();
            if (loops == null) return intersections;

            foreach (CurveLoop loop in loops)
            {
                foreach (Curve c in loop)
                {
                    XYZ start = c.GetEndPoint(0);
                    XYZ end = c.GetEndPoint(1);

                    double minX = Math.Min(start.X, end.X);
                    double maxX = Math.Max(start.X, end.X);

                    if (xVal >= minX - 1e-6 && xVal <= maxX + 1e-6)
                    {
                        double dx = end.X - start.X;
                        if (Math.Abs(dx) > 1e-7)
                        {
                            double fraction = (xVal - start.X) / dx;
                            if (fraction >= -1e-6 && fraction <= 1.0 + 1e-6)
                            {
                                intersections.Add(start.Y + fraction * (end.Y - start.Y));
                            }
                        }
                        else
                        {
                            intersections.Add(start.Y);
                            intersections.Add(end.Y);
                        }
                    }
                }
            }
            return intersections;
        }
    }
}
