using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace HDTool
{
    /// <summary>
    /// Service xử lý Tự Động Tạo & Căn Chỉnh Khung Crop View (Auto Crop Box)
    /// Tích hợp tính năng "TỰ ĐỘNG HIỂU Ý NGƯỜI DÙNG": Khi người dùng đã co kéo Crop View hướng về một cụm đối tượng,
    /// công cụ tự động lọc CHỈ ôm sát cụm đối tượng đó, TUYỆT ĐỐI KHÔNG DÃN TO RA CÁC ĐỐI TƯỢNG KHÁC NẰM NGOÀI!
    /// Chuẩn hóa 100% theo chỉ thị của Sếp Hồ (HadoMEE).
    /// </summary>
    public static class AutoCropService
    {
        private const double MM_TO_FEET = 1.0 / 304.8;

        /// <summary>
        /// Áp dụng Auto Crop cho danh sách các View cụ thể (Dùng cho nhiều Viewport trên Sheet hoặc View đơn)
        /// </summary>
        public static int ApplyAutoCropToViews(
            UIDocument uidoc,
            IList<Autodesk.Revit.DB.View> targetViews,
            out string resultMessage,
            double paddingMm = 150.0,
            bool ignoreLinks = true,
            bool mepPriorityOnly = true,
            bool onlyInsideCurrentCrop = true,
            bool fitSectionBox = false,
            bool setVisible = true,
            ICollection<ElementId> customSelection = null)
        {
            resultMessage = string.Empty;
            if (uidoc == null || uidoc.Document == null || targetViews == null || targetViews.Count == 0)
            {
                resultMessage = "Không có khung nhìn (View) hợp lệ nào để xử lý.";
                return 0;
            }

            Document doc = uidoc.Document;
            int successCount = 0;
            List<string> errors = new List<string>();

            try
            {
                using (Transaction tx = new Transaction(doc, "HDTOOL - Tự Động Fit Crop View"))
                {
                    tx.Start();

                    foreach (Autodesk.Revit.DB.View v in targetViews)
                    {
                        if (v == null || v.IsTemplate || v is ViewSheet) continue;

                        string singleErr;
                        bool ok = ProcessSingleViewCrop(
                            doc, v, paddingMm, ignoreLinks, mepPriorityOnly,
                            onlyInsideCurrentCrop, fitSectionBox, setVisible, customSelection, out singleErr
                        );

                        if (ok)
                        {
                            successCount++;
                        }
                        else if (!string.IsNullOrEmpty(singleErr))
                        {
                            errors.Add(string.Format("• {0}: {1}", v.Name, singleErr));
                        }
                    }

                    if (successCount > 0)
                    {
                        tx.Commit();
                    }
                    else
                    {
                        tx.RollBack();
                    }
                }

                try { uidoc.RefreshActiveView(); } catch { }

                if (successCount > 0)
                {
                    resultMessage = string.Format(
                        "✔ Đã tự động Fit Crop View ôm khít {0}/{1} khung nhìn (Padding {2} mm)!",
                        successCount, targetViews.Count, paddingMm
                    );
                    if (errors.Count > 0)
                    {
                        resultMessage += "\nLưu ý:\n" + string.Join("\n", errors);
                    }
                }
                else
                {
                    resultMessage = errors.Count > 0
                        ? "Không thể Fit Crop View:\n" + string.Join("\n", errors)
                        : "Không tìm thấy đối tượng mô hình phù hợp trong khung nhìn.";
                }

                return successCount;
            }
            catch (Exception ex)
            {
                resultMessage = "Lỗi hệ thống Revit khi Fit Crop View: " + ex.Message;
                return 0;
            }
        }

        /// <summary>
        /// Hàm chạy Auto Crop cho Active View hiện tại (tự động nhận diện Sheet hay View mô hình)
        /// </summary>
        public static bool ApplyAutoCrop(
            UIDocument uidoc,
            out string resultMessage,
            double paddingMm = 150.0,
            bool fitSectionBox = false,
            bool setVisible = true,
            ICollection<ElementId> customSelection = null,
            bool ignoreLinks = true,
            bool mepPriorityOnly = true,
            bool onlyInsideCurrentCrop = true)
        {
            resultMessage = string.Empty;
            if (uidoc == null || uidoc.Document == null)
            {
                resultMessage = "Không tìm thấy dự án Revit đang mở.";
                return false;
            }

            Document doc = uidoc.Document;
            Autodesk.Revit.DB.View activeView = uidoc.ActiveView;
            if (activeView == null)
            {
                resultMessage = "Không có View nào đang mở.";
                return false;
            }

            List<Autodesk.Revit.DB.View> targets = new List<Autodesk.Revit.DB.View>();

            if (activeView is ViewSheet)
            {
                ViewSheet sheet = activeView as ViewSheet;
                ICollection<ElementId> selIds = uidoc.Selection.GetElementIds();

                if (selIds != null && selIds.Count > 0)
                {
                    foreach (ElementId id in selIds)
                    {
                        Viewport vp = doc.GetElement(id) as Viewport;
                        if (vp != null)
                        {
                            Autodesk.Revit.DB.View v = doc.GetElement(vp.ViewId) as Autodesk.Revit.DB.View;
                            if (v != null && !(v is ViewDrafting) && v.ViewType != ViewType.Legend && !targets.Any(x => x.Id == v.Id))
                            {
                                targets.Add(v);
                            }
                        }
                    }
                }

                if (targets.Count == 0)
                {
                    targets = GetCropableViewsOnSheet(doc, sheet);
                }

                if (targets.Count == 0)
                {
                    resultMessage = "Không tìm thấy Viewport mô hình nào trên Sheet hiện tại để Crop.";
                    return false;
                }

                int cnt = ApplyAutoCropToViews(uidoc, targets, out resultMessage, paddingMm, ignoreLinks, mepPriorityOnly, onlyInsideCurrentCrop, fitSectionBox, setVisible, null);
                return cnt > 0;
            }
            else
            {
                targets.Add(activeView);
                ICollection<ElementId> sel = customSelection;
                if (sel == null || sel.Count == 0)
                {
                    sel = uidoc.Selection.GetElementIds();
                }

                int cnt = ApplyAutoCropToViews(uidoc, targets, out resultMessage, paddingMm, ignoreLinks, mepPriorityOnly, onlyInsideCurrentCrop, fitSectionBox, setVisible, sel);
                return cnt > 0;
            }
        }

        public static List<Autodesk.Revit.DB.View> GetCropableViewsOnSheet(Document doc, ViewSheet sheet)
        {
            List<Autodesk.Revit.DB.View> list = new List<Autodesk.Revit.DB.View>();
            if (doc == null || sheet == null) return list;

            ICollection<ElementId> vpIds = sheet.GetAllViewports();
            if (vpIds == null) return list;

            foreach (ElementId id in vpIds)
            {
                Viewport vp = doc.GetElement(id) as Viewport;
                if (vp != null)
                {
                    Autodesk.Revit.DB.View v = doc.GetElement(vp.ViewId) as Autodesk.Revit.DB.View;
                    if (v != null && !v.IsTemplate && v.ViewType != ViewType.Legend && v.ViewType != ViewType.Schedule && v.ViewType != ViewType.DraftingView)
                    {
                        if (!list.Any(x => x.Id == v.Id))
                        {
                            list.Add(v);
                        }
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// Xử lý thuật toán Tự Động Hiểu Ý Người Dùng + Vertex-Exact Geometry & gán CropBox cho 1 View
        /// </summary>
        private static bool ProcessSingleViewCrop(
            Document doc,
            Autodesk.Revit.DB.View targetView,
            double paddingMm,
            bool ignoreLinks,
            bool mepPriorityOnly,
            bool onlyInsideCurrentCrop,
            bool fitSectionBox,
            bool setVisible,
            ICollection<ElementId> customSelection,
            out string errorMsg)
        {
            errorMsg = string.Empty;

            try
            {
                // 1. Gỡ Scope Box nếu đang khóa CropBox
                try
                {
                    Parameter pScope = targetView.get_Parameter(BuiltInParameter.VIEWER_VOLUME_OF_INTEREST_CROP);
                    if (pScope != null && !pScope.IsReadOnly && pScope.AsElementId() != ElementId.InvalidElementId)
                    {
                        pScope.Set(ElementId.InvalidElementId);
                    }
                }
                catch { }

                // 2. Lấy CropBox hiện tại TRƯỚC KHI kích hoạt để biết ý định co kéo của người dùng
                BoundingBoxXYZ curCropBox = targetView.CropBox;
                bool wasCropActive = targetView.CropBoxActive;

                // Bật CropBoxActive trước khi chỉnh sửa
                try
                {
                    if (!targetView.CropBoxActive)
                    {
                        Parameter pCropActive = targetView.get_Parameter(BuiltInParameter.VIEWER_CROP_REGION);
                        if (pCropActive != null && pCropActive.IsReadOnly)
                        {
                            errorMsg = "Thông số 'Crop View' đang bị khóa bởi View Template!";
                            return false;
                        }
                        targetView.CropBoxActive = true;
                        doc.Regenerate();
                    }
                }
                catch (Exception exActive)
                {
                    errorMsg = "Không thể kích hoạt Crop View (bị khóa bởi View Template): " + exActive.Message;
                    return false;
                }

                // Xóa bỏ đường bao đa giác cũ nếu có
                try
                {
                    ViewCropRegionShapeManager crsm = targetView.GetCropRegionShapeManager();
                    if (crsm != null && crsm.ShapeSet)
                    {
                        crsm.RemoveCropRegionShape();
                        doc.Regenerate();
                    }
                }
                catch { }

                // Cập nhật lại curCropBox sau khi active nếu cần
                if (curCropBox == null) curCropBox = targetView.CropBox;

                // 3. Thu thập các đối tượng mô hình thực sự hiển thị trong View
                List<Element> elements = GetModelElementsInView(doc, targetView, customSelection, ignoreLinks, mepPriorityOnly);
                if (elements == null || elements.Count == 0)
                {
                    errorMsg = "Không tìm thấy đối tượng mô hình hiển thị nào trong View.";
                    return false;
                }

                // 4. Kiểm tra Section Box nếu là View3D
                BoundingBoxXYZ activeSectionBox = null;
                Transform secTrans = Transform.Identity;
                Transform secInvT = Transform.Identity;
                bool hasSectionBox = false;
                if (targetView is View3D)
                {
                    View3D v3d = targetView as View3D;
                    if (v3d.IsSectionBoxActive)
                    {
                        activeSectionBox = v3d.GetSectionBox();
                        if (activeSectionBox != null && activeSectionBox.Enabled)
                        {
                            hasSectionBox = true;
                            secTrans = activeSectionBox.Transform ?? Transform.Identity;
                            secInvT = secTrans.Inverse;
                        }
                    }
                }

                // 5. Lấy hệ tọa độ CropBox chuẩn của View
                Transform viewT = (curCropBox != null && curCropBox.Transform != null) ? curCropBox.Transform : Transform.Identity;
                if (viewT.BasisX.GetLength() < 0.001 || viewT.BasisY.GetLength() < 0.001)
                {
                    viewT = Transform.Identity;
                    XYZ rx = targetView.RightDirection.Normalize();
                    XYZ ry = targetView.UpDirection.Normalize();
                    XYZ rz = rx.CrossProduct(ry).Normalize();
                    viewT.BasisX = rx;
                    viewT.BasisY = ry;
                    viewT.BasisZ = rz;
                    viewT.Origin = targetView.Origin;
                }
                Transform invViewT = viewT.Inverse;

                // ===================================================================================
                // 6. THUẬT TOÁN "TỰ ĐỘNG HIỂU Ý NGƯỜI DÙNG" (SMART CROPPED-INTENT FILTER):
                // Nếu người dùng đã co kéo Crop View hướng về 1 trục cụ thể (CropBox đang active và có phạm vi hợp lệ):
                // CHỈ giữ lại các đối tượng ĐANG NẰM TRONG (HOẶC CHẠM) KHUNG CROP HIỆN TẠI!
                // TUYỆT ĐỐI LOẠI BỎ CÁC ĐỐI TƯỢNG NẰM HOÀN TOÀN NGOÀI CROP ĐỂ KHÔNG BỊ DÃN TO RA!
                // ===================================================================================
                bool filterByCurrentCrop = onlyInsideCurrentCrop && wasCropActive && curCropBox != null;
                double curXMin = double.MinValue, curXMax = double.MaxValue;
                double curYMin = double.MinValue, curYMax = double.MaxValue;

                if (filterByCurrentCrop)
                {
                    double spanX = Math.Abs(curCropBox.Max.X - curCropBox.Min.X);
                    double spanY = Math.Abs(curCropBox.Max.Y - curCropBox.Min.Y);
                    
                    // Cho phép dung sai mở rộng 20% hoặc tối thiểu 1.5 feet (~450mm) để bắt trọn đầu mút ống nếu người dùng kéo hơi sát
                    double tolX = Math.Max(1.5, spanX * 0.20);
                    double tolY = Math.Max(1.5, spanY * 0.20);
                    curXMin = curCropBox.Min.X - tolX;
                    curXMax = curCropBox.Max.X + tolX;
                    curYMin = curCropBox.Min.Y - tolY;
                    curYMax = curCropBox.Max.Y + tolY;

                    // Lọc danh sách phần tử
                    List<Element> insideElements = new List<Element>();
                    foreach (Element el in elements)
                    {
                        BoundingBoxXYZ b = el.get_BoundingBox(targetView) ?? el.get_BoundingBox(null);
                        if (b == null) continue;

                        Transform bT = b.Transform ?? Transform.Identity;
                        // Kiểm tra 8 đỉnh góc của phần tử xem có đỉnh nào chạm vùng Crop hiện tại không
                        XYZ[] cns = new XYZ[8]
                        {
                            bT.OfPoint(new XYZ(b.Min.X, b.Min.Y, b.Min.Z)),
                            bT.OfPoint(new XYZ(b.Max.X, b.Min.Y, b.Min.Z)),
                            bT.OfPoint(new XYZ(b.Min.X, b.Max.Y, b.Min.Z)),
                            bT.OfPoint(new XYZ(b.Max.X, b.Max.Y, b.Min.Z)),
                            bT.OfPoint(new XYZ(b.Min.X, b.Min.Y, b.Max.Z)),
                            bT.OfPoint(new XYZ(b.Max.X, b.Min.Y, b.Max.Z)),
                            bT.OfPoint(new XYZ(b.Min.X, b.Max.Y, b.Max.Z)),
                            bT.OfPoint(new XYZ(b.Max.X, b.Max.Y, b.Max.Z))
                        };

                        double elXMin = double.MaxValue, elXMax = double.MinValue;
                        double elYMin = double.MaxValue, elYMax = double.MinValue;
                        for (int i = 0; i < 8; i++)
                        {
                            XYZ pLoc = invViewT.OfPoint(cns[i]);
                            elXMin = Math.Min(elXMin, pLoc.X); elXMax = Math.Max(elXMax, pLoc.X);
                            elYMin = Math.Min(elYMin, pLoc.Y); elYMax = Math.Max(elYMax, pLoc.Y);
                        }

                        // Nếu đối tượng nằm hoàn toàn bên ngoài khung Crop hiện tại -> LOẠI BỎ!
                        if (elXMax < curXMin || elXMin > curXMax ||
                            elYMax < curYMin || elYMin > curYMax)
                        {
                            continue; // Bỏ qua đối tượng ở trục khác!
                        }

                        insideElements.Add(el);
                    }

                    // Nếu tìm thấy đối tượng trong vùng Crop hiện tại -> Dùng danh sách đã lọc!
                    if (insideElements.Count > 0)
                    {
                        elements = insideElements;
                    }
                }

                // 7. TRÍCH XUẤT TỌA ĐỘ HÌNH HỌC CHÍNH XÁC (VERTEX-EXACT GEOMETRY POINTS)
                Options geomOpt = new Options();
                geomOpt.ComputeReferences = false;
                geomOpt.IncludeNonVisibleObjects = false;
                try { geomOpt.View = targetView; } catch { geomOpt.DetailLevel = ViewDetailLevel.Medium; }

                double minX = double.MaxValue, maxX = double.MinValue;
                double minY = double.MaxValue, maxY = double.MinValue;
                double wcsMinX = double.MaxValue, wcsMaxX = double.MinValue;
                double wcsMinY = double.MaxValue, wcsMaxY = double.MinValue;
                double wcsMinZ = double.MaxValue, wcsMaxZ = double.MinValue;

                int validPointCount = 0;

                foreach (Element el in elements)
                {
                    List<XYZ> rawPoints = new List<XYZ>();
                    List<Tuple<XYZ, XYZ>> rawSegments = new List<Tuple<XYZ, XYZ>>();

                    ExtractAccurateGeometry(el, targetView, geomOpt, rawPoints, rawSegments);

                    if (hasSectionBox && activeSectionBox != null)
                    {
                        XYZ sMin = activeSectionBox.Min;
                        XYZ sMax = activeSectionBox.Max;

                        foreach (var seg in rawSegments)
                        {
                            XYZ p1Sec = secInvT.OfPoint(seg.Item1);
                            XYZ p2Sec = secInvT.OfPoint(seg.Item2);
                            XYZ clip1, clip2;
                            if (ClipSegmentLiangBarsky(p1Sec, p2Sec, sMin, sMax, out clip1, out clip2))
                            {
                                XYZ w1 = secTrans.OfPoint(clip1);
                                XYZ w2 = secTrans.OfPoint(clip2);
                                AccumulatePoint(w1, invViewT, ref minX, ref maxX, ref minY, ref maxY, ref wcsMinX, ref wcsMaxX, ref wcsMinY, ref wcsMaxY, ref wcsMinZ, ref wcsMaxZ, ref validPointCount);
                                AccumulatePoint(w2, invViewT, ref minX, ref maxX, ref minY, ref maxY, ref wcsMinX, ref wcsMaxX, ref wcsMinY, ref wcsMaxY, ref wcsMinZ, ref wcsMaxZ, ref validPointCount);
                            }
                        }

                        const double eps = 0.05;
                        foreach (XYZ pt in rawPoints)
                        {
                            XYZ pSec = secInvT.OfPoint(pt);
                            if (pSec.X >= sMin.X - eps && pSec.X <= sMax.X + eps &&
                                pSec.Y >= sMin.Y - eps && pSec.Y <= sMax.Y + eps &&
                                pSec.Z >= sMin.Z - eps && pSec.Z <= sMax.Z + eps)
                            {
                                AccumulatePoint(pt, invViewT, ref minX, ref maxX, ref minY, ref maxY, ref wcsMinX, ref wcsMaxX, ref wcsMinY, ref wcsMaxY, ref wcsMinZ, ref wcsMaxZ, ref validPointCount);
                            }
                        }
                    }
                    else
                    {
                        foreach (var seg in rawSegments)
                        {
                            AccumulatePoint(seg.Item1, invViewT, ref minX, ref maxX, ref minY, ref maxY, ref wcsMinX, ref wcsMaxX, ref wcsMinY, ref wcsMaxY, ref wcsMinZ, ref wcsMaxZ, ref validPointCount);
                            AccumulatePoint(seg.Item2, invViewT, ref minX, ref maxX, ref minY, ref maxY, ref wcsMinX, ref wcsMaxX, ref wcsMinY, ref wcsMaxY, ref wcsMinZ, ref wcsMaxZ, ref validPointCount);
                        }
                        foreach (XYZ pt in rawPoints)
                        {
                            AccumulatePoint(pt, invViewT, ref minX, ref maxX, ref minY, ref maxY, ref wcsMinX, ref wcsMaxX, ref wcsMinY, ref wcsMaxY, ref wcsMinZ, ref wcsMaxZ, ref validPointCount);
                        }
                    }
                }

                if (validPointCount == 0 || minX >= maxX || minY >= maxY)
                {
                    errorMsg = "Không tính được đường bao hình học của đối tượng trong phạm vi View.";
                    return false;
                }

                // 8. Mở rộng khoảng đệm viền ngoài (Margin / Padding mm)
                double padFt = Math.Max(5.0, paddingMm) * MM_TO_FEET;
                minX -= padFt;
                maxX += padFt;
                minY -= padFt;
                maxY += padFt;

                // Giữ nguyên giới hạn Z (chiều sâu) từ curCropBox để đảm bảo 100% không cắt xén mô hình theo phương sâu!
                double zMin = (curCropBox != null) ? curCropBox.Min.Z : -1000.0;
                double zMax = (curCropBox != null) ? curCropBox.Max.Z : 1000.0;
                if (zMin >= zMax)
                {
                    zMin = -1000.0;
                    zMax = 1000.0;
                }

                // 9. Áp dụng CropBox mới ôm sát khít cụm đối tượng được chọn/hướng tới
                BoundingBoxXYZ newCropBox = new BoundingBoxXYZ();
                newCropBox.Transform = viewT;
                newCropBox.Min = new XYZ(minX, minY, zMin);
                newCropBox.Max = new XYZ(maxX, maxY, zMax);

                targetView.CropBox = newCropBox;
                targetView.CropBoxActive = true;
                targetView.CropBoxVisible = setVisible;

                if (fitSectionBox && targetView is View3D && hasSectionBox)
                {
                    try
                    {
                        View3D v3d = targetView as View3D;
                        BoundingBoxXYZ secBox = new BoundingBoxXYZ();
                        secBox.Transform = Transform.Identity;
                        secBox.Min = new XYZ(wcsMinX - padFt, wcsMinY - padFt, wcsMinZ - padFt);
                        secBox.Max = new XYZ(wcsMaxX + padFt, wcsMaxY + padFt, wcsMaxZ + padFt);
                        v3d.SetSectionBox(secBox);
                    }
                    catch { }
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMsg = ex.Message;
                return false;
            }
        }

        private static void AccumulatePoint(
            XYZ ptWcs, Transform invViewT,
            ref double minX, ref double maxX,
            ref double minY, ref double maxY,
            ref double wMinX, ref double wMaxX,
            ref double wMinY, ref double wMaxY,
            ref double wMinZ, ref double wMaxZ,
            ref int count)
        {
            wMinX = Math.Min(wMinX, ptWcs.X);
            wMaxX = Math.Max(wMaxX, ptWcs.X);
            wMinY = Math.Min(wMinY, ptWcs.Y);
            wMaxY = Math.Max(wMaxY, ptWcs.Y);
            wMinZ = Math.Min(wMinZ, ptWcs.Z);
            wMaxZ = Math.Max(wMaxZ, ptWcs.Z);

            XYZ ptLocal = invViewT.OfPoint(ptWcs);
            minX = Math.Min(minX, ptLocal.X);
            maxX = Math.Max(maxX, ptLocal.X);
            minY = Math.Min(minY, ptLocal.Y);
            maxY = Math.Max(maxY, ptLocal.Y);
            count++;
        }

        private static void ExtractAccurateGeometry(
            Element el,
            Autodesk.Revit.DB.View targetView,
            Options geomOpt,
            List<XYZ> outPoints,
            List<Tuple<XYZ, XYZ>> outSegments)
        {
            bool gotGeom = false;
            try
            {
                GeometryElement gElem = el.get_Geometry(geomOpt);
                if (gElem != null)
                {
                    ExtractFromGeometryElement(gElem, outPoints, outSegments);
                    if (outPoints.Count > 0 || outSegments.Count > 0) gotGeom = true;
                }
            }
            catch { }

            if (!gotGeom && el is MEPCurve)
            {
                try
                {
                    LocationCurve lc = el.Location as LocationCurve;
                    if (lc != null && lc.Curve != null)
                    {
                        IList<XYZ> pts = lc.Curve.Tessellate();
                        double r = 0.25;
                        try
                        {
                            Parameter pDiam = el.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)
                                ?? el.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM)
                                ?? el.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM);
                            if (pDiam != null && pDiam.HasValue) r = pDiam.AsDouble() * 0.55;
                        }
                        catch { }

                        for (int i = 0; i < pts.Count - 1; i++)
                        {
                            outSegments.Add(new Tuple<XYZ, XYZ>(pts[i], pts[i + 1]));
                        }
                        foreach (XYZ p in pts)
                        {
                            outPoints.Add(p);
                            outPoints.Add(new XYZ(p.X + r, p.Y, p.Z));
                            outPoints.Add(new XYZ(p.X - r, p.Y, p.Z));
                            outPoints.Add(new XYZ(p.X, p.Y + r, p.Z));
                            outPoints.Add(new XYZ(p.X, p.Y - r, p.Z));
                            outPoints.Add(new XYZ(p.X, p.Y, p.Z + r));
                            outPoints.Add(new XYZ(p.X, p.Y, p.Z - r));
                        }
                        gotGeom = true;
                    }
                }
                catch { }
            }

            if (!gotGeom)
            {
                BoundingBoxXYZ bbox = el.get_BoundingBox(targetView) ?? el.get_BoundingBox(null);
                if (bbox != null)
                {
                    Transform bTrans = bbox.Transform ?? Transform.Identity;
                    XYZ bMin = bbox.Min;
                    XYZ bMax = bbox.Max;
                    if (Math.Abs(bMax.X - bMin.X) < 10000 && Math.Abs(bMax.Y - bMin.Y) < 10000 && Math.Abs(bMax.Z - bMin.Z) < 10000)
                    {
                        outPoints.Add(bTrans.OfPoint(new XYZ(bMin.X, bMin.Y, bMin.Z)));
                        outPoints.Add(bTrans.OfPoint(new XYZ(bMax.X, bMin.Y, bMin.Z)));
                        outPoints.Add(bTrans.OfPoint(new XYZ(bMin.X, bMax.Y, bMin.Z)));
                        outPoints.Add(bTrans.OfPoint(new XYZ(bMax.X, bMax.Y, bMin.Z)));
                        outPoints.Add(bTrans.OfPoint(new XYZ(bMin.X, bMin.Y, bMax.Z)));
                        outPoints.Add(bTrans.OfPoint(new XYZ(bMax.X, bMin.Y, bMax.Z)));
                        outPoints.Add(bTrans.OfPoint(new XYZ(bMin.X, bMax.Y, bMax.Z)));
                        outPoints.Add(bTrans.OfPoint(new XYZ(bMax.X, bMax.Y, bMax.Z)));
                    }
                }
            }
        }

        private static void ExtractFromGeometryElement(
            GeometryElement gElem,
            List<XYZ> outPoints,
            List<Tuple<XYZ, XYZ>> outSegments)
        {
            if (gElem == null) return;

            foreach (GeometryObject obj in gElem)
            {
                if (obj == null) continue;

                if (obj is Solid)
                {
                    Solid sol = obj as Solid;
                    if (sol.Volume > 1e-9 || sol.Edges.Size > 0)
                    {
                        foreach (Edge edge in sol.Edges)
                        {
                            try
                            {
                                IList<XYZ> ePts = edge.Tessellate();
                                for (int i = 0; i < ePts.Count - 1; i++)
                                {
                                    outSegments.Add(new Tuple<XYZ, XYZ>(ePts[i], ePts[i + 1]));
                                }
                            }
                            catch { }
                        }
                    }
                }
                else if (obj is Curve)
                {
                    try
                    {
                        Curve c = obj as Curve;
                        IList<XYZ> cPts = c.Tessellate();
                        for (int i = 0; i < cPts.Count - 1; i++)
                        {
                            outSegments.Add(new Tuple<XYZ, XYZ>(cPts[i], cPts[i + 1]));
                        }
                    }
                    catch { }
                }
                else if (obj is PolyLine)
                {
                    try
                    {
                        PolyLine pl = obj as PolyLine;
                        IList<XYZ> pPts = pl.GetCoordinates();
                        for (int i = 0; i < pPts.Count - 1; i++)
                        {
                            outSegments.Add(new Tuple<XYZ, XYZ>(pPts[i], pPts[i + 1]));
                        }
                    }
                    catch { }
                }
                else if (obj is Mesh)
                {
                    try
                    {
                        Mesh m = obj as Mesh;
                        foreach (XYZ v in m.Vertices) outPoints.Add(v);
                    }
                    catch { }
                }
                else if (obj is GeometryInstance)
                {
                    try
                    {
                        GeometryInstance gi = obj as GeometryInstance;
                        GeometryElement instGeom = gi.GetInstanceGeometry();
                        if (instGeom != null)
                        {
                            ExtractFromGeometryElement(instGeom, outPoints, outSegments);
                        }
                    }
                    catch { }
                }
            }
        }

        private static bool ClipSegmentLiangBarsky(XYZ p1, XYZ p2, XYZ bMin, XYZ bMax, out XYZ clip1, out XYZ clip2)
        {
            clip1 = p1;
            clip2 = p2;

            double t0 = 0.0;
            double t1 = 1.0;

            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            double dz = p2.Z - p1.Z;

            const double eps = 0.05;

            if (!ClipAxis(-dx, p1.X - (bMin.X - eps), ref t0, ref t1)) return false;
            if (!ClipAxis(dx, (bMax.X + eps) - p1.X, ref t0, ref t1)) return false;

            if (!ClipAxis(-dy, p1.Y - (bMin.Y - eps), ref t0, ref t1)) return false;
            if (!ClipAxis(dy, (bMax.Y + eps) - p1.Y, ref t0, ref t1)) return false;

            if (!ClipAxis(-dz, p1.Z - (bMin.Z - eps), ref t0, ref t1)) return false;
            if (!ClipAxis(dz, (bMax.Z + eps) - p1.Z, ref t0, ref t1)) return false;

            clip1 = new XYZ(p1.X + t0 * dx, p1.Y + t0 * dy, p1.Z + t0 * dz);
            clip2 = new XYZ(p1.X + t1 * dx, p1.Y + t1 * dy, p1.Z + t1 * dz);
            return true;
        }

        private static bool ClipAxis(double p, double q, ref double t0, ref double t1)
        {
            if (Math.Abs(p) < 1e-9)
            {
                return q >= 0.0;
            }
            double r = q / p;
            if (p < 0.0)
            {
                if (r > t1) return false;
                if (r > t0) t0 = r;
            }
            else
            {
                if (r < t0) return false;
                if (r < t1) t1 = r;
            }
            return true;
        }

        public static List<Element> GetModelElementsInView(
            Document doc,
            Autodesk.Revit.DB.View targetView,
            ICollection<ElementId> userSelection = null,
            bool ignoreLinks = true,
            bool mepPriorityOnly = true)
        {
            List<Element> list = new List<Element>();
            if (doc == null || targetView == null) return list;

            if (userSelection != null && userSelection.Count > 0)
            {
                foreach (ElementId id in userSelection)
                {
                    Element el = doc.GetElement(id);
                    if (el != null && IsValidModelElement(el, targetView, ignoreLinks, false))
                    {
                        list.Add(el);
                    }
                }
                if (list.Count > 0) return list;
            }

            FilteredElementCollector col = new FilteredElementCollector(doc, targetView.Id)
                .WhereElementIsNotElementType();

            List<Element> allValid = new List<Element>();
            List<Element> mepOnly = new List<Element>();

            foreach (Element el in col)
            {
                if (IsValidModelElement(el, targetView, ignoreLinks, false))
                {
                    allValid.Add(el);
                    if (IsMepElement(el))
                    {
                        mepOnly.Add(el);
                    }
                }
            }

            if (mepPriorityOnly && mepOnly.Count > 0)
            {
                return mepOnly;
            }

            return allValid;
        }

        private static bool IsMepElement(Element el)
        {
            if (el == null || el.Category == null) return false;
            int catId = el.Category.Id.IntegerValue;
            return catId == (int)BuiltInCategory.OST_PipeCurves ||
                   catId == (int)BuiltInCategory.OST_PipeFitting ||
                   catId == (int)BuiltInCategory.OST_PipeAccessory ||
                   catId == (int)BuiltInCategory.OST_FlexPipeCurves ||
                   catId == (int)BuiltInCategory.OST_PlumbingFixtures ||
                   catId == (int)BuiltInCategory.OST_DuctCurves ||
                   catId == (int)BuiltInCategory.OST_DuctFitting ||
                   catId == (int)BuiltInCategory.OST_DuctAccessory ||
                   catId == (int)BuiltInCategory.OST_DuctTerminal ||
                   catId == (int)BuiltInCategory.OST_FlexDuctCurves ||
                   catId == (int)BuiltInCategory.OST_MechanicalEquipment ||
                   catId == (int)BuiltInCategory.OST_CableTray ||
                   catId == (int)BuiltInCategory.OST_CableTrayFitting ||
                   catId == (int)BuiltInCategory.OST_Conduit ||
                   catId == (int)BuiltInCategory.OST_ConduitFitting ||
                   catId == (int)BuiltInCategory.OST_ElectricalEquipment ||
                   catId == (int)BuiltInCategory.OST_ElectricalFixtures ||
                   catId == (int)BuiltInCategory.OST_LightingFixtures ||
                   catId == (int)BuiltInCategory.OST_Sprinklers ||
                   catId == (int)BuiltInCategory.OST_PipeInsulations ||
                   catId == (int)BuiltInCategory.OST_DuctInsulations;
        }

        private static bool IsValidModelElement(Element el, Autodesk.Revit.DB.View targetView, bool ignoreLinks, bool mepOnly)
        {
            if (el == null) return false;
            if (el is Autodesk.Revit.DB.View) return false;
            if (el is Sketch) return false;
            if (el is Viewport) return false;

            if (ignoreLinks && (el is RevitLinkInstance || el is ImportInstance || el is PointCloudInstance))
            {
                return false;
            }

            try
            {
                if (el.IsHidden(targetView)) return false;
            }
            catch { }

            Category cat = el.Category;
            if (cat == null) return false;

            if (cat.CategoryType != CategoryType.Model) return false;

            int catId = cat.Id.IntegerValue;
            if (catId == (int)BuiltInCategory.OST_Grids ||
                catId == (int)BuiltInCategory.OST_Levels ||
                catId == (int)BuiltInCategory.OST_VolumeOfInterest || // Scope Box
                catId == (int)BuiltInCategory.OST_SectionBox ||
                catId == (int)BuiltInCategory.OST_Cameras ||
                catId == (int)BuiltInCategory.OST_Viewports ||
                catId == (int)BuiltInCategory.OST_TitleBlocks ||
                catId == (int)BuiltInCategory.OST_CLines ||
                catId == (int)BuiltInCategory.OST_SunStudy ||
                catId == (int)BuiltInCategory.OST_RvtLinks ||
                catId == (int)BuiltInCategory.OST_Rooms ||
                catId == (int)BuiltInCategory.OST_MEPSpaces ||
                catId == (int)BuiltInCategory.OST_HVAC_Zones ||
                catId == (int)BuiltInCategory.OST_Lines)
            {
                return false;
            }

            if (mepOnly && !IsMepElement(el)) return false;

            BoundingBoxXYZ bbox = el.get_BoundingBox(targetView) ?? el.get_BoundingBox(null);
            if (bbox == null) return false;

            return true;
        }
    }
}
