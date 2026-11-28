using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;

namespace HDTool
{
    /// <summary>
    /// Ống trục đứng: ký hiệu CAD là khung chữ nhật trên layer ống gió có 2 đường chéo.
    /// Chiều cao ống bằng chiều cao 1 tầng (tầng đang chọn -> tầng kế trên).
    /// </summary>
    public static partial class CadToDuctService
    {
        private const double DefaultStoreyHeightMm = 3300.0;
        private const double MinStoreyHeightMm = 2000.0; // bỏ qua các Level phụ (cốt trần, cốt dầm...)

        private class RiserSymbol
        {
            public XYZ Center;
            public XYZ AxisU;
            public XYZ AxisV;
            public double SizeU; // feet
            public double SizeV; // feet
            public string LayerName;
            public int ColorRgb;
        }

        // ---------------------------------------------------------------- NHẬN DIỆN KÝ HIỆU

        private static List<RiserSymbol> DetectRiserSymbols(List<RawPolyShape> polys, List<RawLineShape> lines)
        {
            List<XYZ[]> segments = new List<XYZ[]>();
            foreach (RawLineShape l in lines) segments.Add(new XYZ[] { l.Line.GetEndPoint(0), l.Line.GetEndPoint(1) });
            foreach (RawPolyShape p in polys)
            {
                for (int i = 1; i < p.Points.Count; i++) segments.Add(new XYZ[] { p.Points[i - 1], p.Points[i] });
            }

            List<RiserSymbol> found = new List<RiserSymbol>();
            foreach (RawPolyShape rp in polys)
            {
                XYZ[] c;
                if (!TryGetRectangleCorners(rp.Points, out c)) continue;

                double sizeU = DistXY(c[0], c[1]), sizeV = DistXY(c[1], c[2]);
                double small = Math.Min(sizeU, sizeV) * FEET_TO_MM, big = Math.Max(sizeU, sizeV) * FEET_TO_MM;
                if (small < 100.0 || big > 2500.0 || big > small * 5.0) continue;

                XYZ center = new XYZ((c[0].X + c[2].X) * 0.5, (c[0].Y + c[2].Y) * 0.5, 0.0);
                if (found.Exists(delegate(RiserSymbol r) { return DistXY(r.Center, center) < 30.0 * MM_TO_FEET; })) continue;
                if (!HasCrossMark(c, center, segments)) continue;

                RiserSymbol symbol = new RiserSymbol();
                symbol.Center = center;
                symbol.AxisU = new XYZ(c[1].X - c[0].X, c[1].Y - c[0].Y, 0.0).Normalize();
                symbol.AxisV = new XYZ(c[2].X - c[1].X, c[2].Y - c[1].Y, 0.0).Normalize();
                symbol.SizeU = sizeU;
                symbol.SizeV = sizeV;
                symbol.LayerName = rp.LayerName;
                symbol.ColorRgb = rp.ColorRgb;
                found.Add(symbol);
            }
            return found;
        }

        private static bool TryGetRectangleCorners(List<XYZ> points, out XYZ[] corners)
        {
            corners = null;
            if (points == null || points.Count < 4) return false;
            double tol = 3.0 * MM_TO_FEET;
            List<XYZ> pts = new List<XYZ>();
            foreach (XYZ p in points)
            {
                if (pts.Count == 0 || DistXY(pts[pts.Count - 1], p) > tol) pts.Add(p);
            }
            if (pts.Count > 1 && DistXY(pts[0], pts[pts.Count - 1]) <= tol) pts.RemoveAt(pts.Count - 1);
            if (pts.Count != 4) return false;

            for (int i = 0; i < 4; i++)
            {
                XYZ a = pts[(i + 1) % 4] - pts[i], b = pts[(i + 2) % 4] - pts[(i + 1) % 4];
                double la = Math.Sqrt(a.X * a.X + a.Y * a.Y), lb = Math.Sqrt(b.X * b.X + b.Y * b.Y);
                if (la < tol || lb < tol || Math.Abs((a.X * b.X + a.Y * b.Y) / (la * lb)) > 0.02) return false;
            }
            corners = pts.ToArray();
            return true;
        }

        /// <summary>Cần đủ 1 đường chéo trọn + ít nhất 1 nửa đường chéo còn lại (đường chéo có thể vẽ thành 2 nửa từ tâm).</summary>
        private static bool HasCrossMark(XYZ[] c, XYZ center, List<XYZ[]> segments)
        {
            double tol = 8.0 * MM_TO_FEET;
            bool full02 = false, full13 = false;
            bool[] half = new bool[4];
            foreach (XYZ[] s in segments)
            {
                int a = MarkPointIndex(c, center, s[0], tol);
                if (a < 0) continue;
                int b = MarkPointIndex(c, center, s[1], tol);
                if (b < 0 || a == b) continue;

                int lo = Math.Min(a, b), hi = Math.Max(a, b);
                if (hi == 4) half[lo] = true;
                else if (lo == 0 && hi == 2) full02 = true;
                else if (lo == 1 && hi == 3) full13 = true;
            }
            if (half[0] && half[2]) full02 = true;
            if (half[1] && half[3]) full13 = true;
            return (full02 && (full13 || half[1] || half[3])) || (full13 && (full02 || half[0] || half[2]));
        }

        private static int MarkPointIndex(XYZ[] c, XYZ center, XYZ p, double tol)
        {
            for (int i = 0; i < 4; i++)
            {
                if (DistXY(c[i], p) <= tol) return i;
            }
            return DistXY(center, p) <= tol ? 4 : -1;
        }

        // ---------------------------------------------------------------- ỐNG VẼ XUYÊN VÀO KHUNG TRỤC

        /// <summary>
        /// Có bản vẽ kéo ống ngang tới mép xa của khung trục, gạch chéo nằm trong đầu ống -> cắt đầu ống về mặt gần của khung,
        /// nếu không cả đoạn ống bị coi là hình giả. Chạy lại nhiều lần không đổi kết quả.
        /// </summary>
        private static int TrimDuctsAtRiserSymbols(List<RiserSymbol> symbols, List<CadDuctSegment> ducts)
        {
            int trimmed = 0;
            double endTol = 60.0 * MM_TO_FEET;

            foreach (CadDuctSegment d in ducts)
            {
                if (d.IsRiser || d.ShapeKind != CadShapeKind.StraightDuct) continue;
                if (d.PolygonVertices == null || d.PolygonVertices.Count < 3) continue;

                foreach (RiserSymbol r in symbols)
                {
                    XYZ axis = new XYZ(d.EndPoint.X - d.StartPoint.X, d.EndPoint.Y - d.StartPoint.Y, 0.0);
                    double length = axis.GetLength();
                    if (length < 1e-6) break;
                    axis = axis.Normalize();
                    XYZ perp = new XYZ(-axis.Y, axis.X, 0.0);
                    if (DistXY(d.CenterPoint, r.Center) < 30.0 * MM_TO_FEET) continue; // chính là khung trục

                    bool alongU = Math.Abs(axis.DotProduct(r.AxisU)) > 0.97;
                    if (!alongU && Math.Abs(axis.DotProduct(r.AxisV)) <= 0.97) continue;
                    double depth = alongU ? r.SizeU : r.SizeV;
                    double across = alongU ? r.SizeV : r.SizeU;
                    double widthFeet = d.WidthMm * MM_TO_FEET;
                    if (across > widthFeet + endTol || across < widthFeet - 150.0 * MM_TO_FEET) continue;

                    XYZ rel = r.Center - d.StartPoint;
                    if (Math.Abs(rel.X * perp.X + rel.Y * perp.Y) > endTol) continue;
                    double t = rel.X * axis.X + rel.Y * axis.Y;

                    bool atEnd = Math.Abs(t + depth * 0.5 - length) <= endTol;
                    bool atStart = Math.Abs(t - depth * 0.5) <= endTol;
                    if (atEnd == atStart) continue;

                    double from = atStart ? t + depth * 0.5 : 0.0;
                    double to = atEnd ? t - depth * 0.5 : length;
                    if (to - from < 100.0 * MM_TO_FEET) continue;

                    XYZ start = d.StartPoint + axis * from;
                    XYZ end = d.StartPoint + axis * to;
                    double minSide = double.MaxValue, maxSide = double.MinValue;
                    foreach (XYZ p in d.PolygonVertices)
                    {
                        double side = (p.X - start.X) * perp.X + (p.Y - start.Y) * perp.Y;
                        minSide = Math.Min(minSide, side);
                        maxSide = Math.Max(maxSide, side);
                    }

                    d.StartPoint = start;
                    d.EndPoint = end;
                    d.CenterPoint = (start + end) * 0.5;
                    d.Direction = axis;
                    d.Length = (to - from) * FEET_TO_MM;
                    d.PolygonVertices = new List<XYZ>
                    {
                        start + perp * minSide, end + perp * minSide,
                        end + perp * maxSide, start + perp * maxSide
                    };
                    trimmed++;
                }
            }
            return trimmed;
        }

        /// <summary>
        /// 2 ống mà giao tim rơi vào trong khung trục chỉ gặp nhau QUA trục đứng, không được nối thẳng bằng cút / chân rẽ.
        /// </summary>
        private static int RemoveLinksThroughRisers(List<RiserSymbol> symbols, List<CadDuctSegment> ducts, List<CadConnectionEdge> edges)
        {
            List<RiserSymbol> fed = symbols.FindAll(delegate(RiserSymbol r) { return FindRiserFeeder(r, ducts) != null; });
            if (fed.Count == 0) return 0;

            return edges.RemoveAll(delegate(CadConnectionEdge e)
            {
                if (e.Kind != CadConnectionKind.Elbow && e.Kind != CadConnectionKind.TakeoffTap) return false;
                XYZ junction = LineLineIntersectXY(e.SegmentA.StartPoint, GetSegmentAxisXY(e.SegmentA),
                    e.SegmentB.StartPoint, GetSegmentAxisXY(e.SegmentB), e.SegmentA.StartPoint.Z) ?? e.JunctionPoint;
                return junction != null && fed.Exists(delegate(RiserSymbol r) { return IsInsideRiserBox(r, junction, 30.0 * MM_TO_FEET); });
            });
        }

        private static bool IsInsideRiserBox(RiserSymbol r, XYZ p, double margin)
        {
            double du = (p.X - r.Center.X) * r.AxisU.X + (p.Y - r.Center.Y) * r.AxisU.Y;
            double dv = (p.X - r.Center.X) * r.AxisV.X + (p.Y - r.Center.Y) * r.AxisV.Y;
            return Math.Abs(du) <= r.SizeU * 0.5 + margin && Math.Abs(dv) <= r.SizeV * 0.5 + margin;
        }

        // ---------------------------------------------------------------- KHÚC ỐNG CỤT SÁT KHUNG TRỤC

        /// <summary>
        /// Khúc ống chữ nhật dính liền cạnh khung trục đứng chắc chắn là ống đấu vào trục -> giữ lại dù bộ lọc thiết bị đã loại.
        /// </summary>
        private static List<CadDuctSegment> RestoreRiserStubs(
            List<RiserSymbol> symbols,
            List<RawPolyShape> polys,
            List<CadDuctSegment> ducts,
            List<CadDuctSegment> fittings,
            double centerElevMm,
            Outline limitBox)
        {
            List<CadDuctSegment> stubs = new List<CadDuctSegment>();
            double zFeet = centerElevMm * MM_TO_FEET;

            foreach (RiserSymbol r in symbols)
            {
                // Chính khung trục không được dựng thành ống ngang
                Predicate<CadDuctSegment> isBoxItself = delegate(CadDuctSegment d)
                {
                    return d.CenterPoint != null && DistXY(d.CenterPoint, r.Center) < 30.0 * MM_TO_FEET;
                };
                ducts.RemoveAll(isBoxItself);
                if (fittings != null) fittings.RemoveAll(isBoxItself);

                foreach (RawPolyShape rp in polys)
                {
                    if (limitBox != null && !PolyIntersectsOutlineXY(rp.Points, limitBox)) continue;
                    XYZ[] c;
                    if (!TryGetRectangleCorners(rp.Points, out c)) continue;
                    XYZ center = new XYZ((c[0].X + c[2].X) * 0.5, (c[0].Y + c[2].Y) * 0.5, 0.0);
                    if (DistXY(center, r.Center) < 30.0 * MM_TO_FEET) continue;

                    for (int side = 0; side < 4; side++)
                    {
                        XYZ n = side < 2 ? r.AxisU : r.AxisV;          // hướng đi ra khỏi cạnh khung
                        if (side % 2 == 1) n = n.Negate();
                        XYZ t = side < 2 ? r.AxisV : r.AxisU;
                        double halfN = (side < 2 ? r.SizeU : r.SizeV) * 0.5;
                        double sideLen = side < 2 ? r.SizeV : r.SizeU;

                        double minN = double.MaxValue, maxN = double.MinValue, minT = double.MaxValue, maxT = double.MinValue;
                        bool aligned = true;
                        for (int i = 0; i < 4; i++)
                        {
                            XYZ rel = c[i] - r.Center;
                            double pn = rel.X * n.X + rel.Y * n.Y, pt = rel.X * t.X + rel.Y * t.Y;
                            minN = Math.Min(minN, pn); maxN = Math.Max(maxN, pn);
                            minT = Math.Min(minT, pt); maxT = Math.Max(maxT, pt);
                            XYZ edge = c[(i + 1) % 4] - c[i];
                            double len = Math.Sqrt(edge.X * edge.X + edge.Y * edge.Y);
                            double dot = Math.Abs((edge.X * n.X + edge.Y * n.Y) / len);
                            if (dot > 0.02 && dot < 0.98) aligned = false;
                        }
                        if (!aligned) continue;

                        double width = maxT - minT, length = maxN - minN;
                        if (Math.Abs(minN - halfN) > 30.0 * MM_TO_FEET) continue;                 // phải dính vào cạnh
                        if (Math.Abs((minT + maxT) * 0.5) > 60.0 * MM_TO_FEET) continue;          // đồng trục
                        if (width > sideLen + 10.0 * MM_TO_FEET || width < sideLen - 150.0 * MM_TO_FEET) continue;
                        if (width < 100.0 * MM_TO_FEET || length < 80.0 * MM_TO_FEET) continue;
                        if (ducts.Exists(delegate(CadDuctSegment d) { return d.ContainsPointXY(center, 10.0 * MM_TO_FEET); })) continue;

                        XYZ mid = r.Center + t * ((minT + maxT) * 0.5);
                        CadDuctSegment stub = new CadDuctSegment();
                        stub.ShapeKind = CadShapeKind.StraightDuct;
                        XYZ near = mid + n * minN, far = mid + n * maxN;
                        stub.StartPoint = new XYZ(far.X, far.Y, zFeet);
                        stub.EndPoint = new XYZ(near.X, near.Y, zFeet);
                        stub.CenterPoint = (stub.StartPoint + stub.EndPoint) * 0.5;
                        stub.Direction = n.Negate();
                        stub.Length = length * FEET_TO_MM;
                        stub.WidthMm = SnapStandardDuctWidthMm(width * FEET_TO_MM);
                        stub.Width2Mm = stub.WidthMm;
                        stub.CenterElevationMm = centerElevMm;
                        stub.LayerName = rp.LayerName;
                        stub.ColorRgb = rp.ColorRgb;
                        stub.PolygonVertices = c.Select(delegate(XYZ p) { return new XYZ(p.X, p.Y, zFeet); }).ToList();
                        if (fittings != null)
                        {
                            fittings.RemoveAll(delegate(CadDuctSegment f) { return f.CenterPoint != null && DistXY(f.CenterPoint, center) < 30.0 * MM_TO_FEET; });
                        }
                        ducts.Add(stub);
                        stubs.Add(stub);
                        break;
                    }
                }
            }
            return stubs;
        }

        /// <summary>
        /// Giữa khúc cụt vào trục và ống ngang thường là cút vẽ trên layer khác hoặc có van chắn -> nối bằng cút tại giao 2 tim ống.
        /// </summary>
        private static void LinkRiserStubs(List<CadDuctSegment> stubs, List<CadDuctSegment> ducts, List<CadConnectionEdge> edges)
        {
            foreach (CadDuctSegment stub in stubs)
            {
                if (!ducts.Contains(stub)) continue;
                if (edges.Exists(delegate(CadConnectionEdge e) { return e.SegmentA == stub || e.SegmentB == stub; })) continue;

                XYZ farEnd = stub.StartPoint; // đầu quay ra xa khung trục
                CadDuctSegment best = null;
                XYZ bestCorner = null;
                double bestGap = double.MaxValue;
                foreach (CadDuctSegment d in ducts)
                {
                    if (d == stub || Math.Abs(d.WidthMm - stub.WidthMm) > 60.0) continue;
                    XYZ axis = GetSegmentAxisXY(d);
                    if (Math.Abs(axis.DotProduct(stub.Direction)) > 0.2) continue;

                    XYZ corner = LineLineIntersectXY(stub.StartPoint, stub.Direction, d.StartPoint, axis, stub.StartPoint.Z);
                    if (corner == null) continue;
                    double reach = Math.Max(400.0, stub.WidthMm * 2.0) * MM_TO_FEET;
                    double fromStub = DistXY(corner, farEnd);
                    double fromDuct = Math.Min(DistXY(corner, d.StartPoint), DistXY(corner, d.EndPoint));
                    if (fromStub > reach || fromDuct > reach) continue;
                    // Góc cút phải nằm phía sau đầu khúc cụt (không nằm về phía khung trục)
                    if ((corner - farEnd).DotProduct(stub.Direction) > 50.0 * MM_TO_FEET) continue;

                    if (fromStub + fromDuct < bestGap)
                    {
                        bestGap = fromStub + fromDuct;
                        best = d;
                        bestCorner = corner;
                    }
                }
                if (best != null) edges.Add(new CadConnectionEdge(stub, best, CadConnectionKind.Elbow, null, bestCorner));
            }
        }

        // ---------------------------------------------------------------- TẠO ỐNG TRỤC ĐỨNG

        /// <summary>Chạy sau khi đã gán kích thước ống ngang, để trục đứng lan truyền được kích thước từ ống đấu vào.</summary>
        private static int AttachRisers(
            List<RiserSymbol> symbols,
            List<CadTextNote> notes,
            List<CadDuctSegment> ducts,
            List<CadConnectionEdge> edges,
            double centerElevMm,
            Func<XYZ, bool> allowStandalone)
        {
            double zFeet = centerElevMm * MM_TO_FEET;
            List<CadDuctSegment> horizontals = new List<CadDuctSegment>(ducts);
            int added = 0;

            foreach (RiserSymbol r in symbols)
            {
                CadDuctSegment feeder = FindRiserFeeder(r, horizontals);
                bool noteWidthOnU = true;
                CadTextNote note = FindRiserSizeNote(r, notes, out noteWidthOnU);

                // Chỉ có hình chéo, không ghi chú khớp cỡ và không ống nào đấu vào -> chưa đủ chắc là trục đứng
                if (feeder == null && (note == null || allowStandalone == null || !allowStandalone(r.Center))) continue;

                // Phương bề rộng: vuông góc ống đấu vào; không có ống thì theo chiều khớp số W của ghi chú
                XYZ widthAxis;
                bool widthOnU;
                if (feeder != null)
                {
                    widthOnU = Math.Abs(GetSegmentAxisXY(feeder).DotProduct(r.AxisU)) < 0.5;
                }
                else widthOnU = noteWidthOnU;
                widthAxis = widthOnU ? r.AxisU : r.AxisV;
                double boxAlongWidthMm = (widthOnU ? r.SizeU : r.SizeV) * FEET_TO_MM;
                double boxAcrossMm = (widthOnU ? r.SizeV : r.SizeU) * FEET_TO_MM;

                double w, h;
                string source;
                if (note != null)
                {
                    bool direct = Math.Abs(boxAlongWidthMm - note.WidthMm) <= Math.Abs(boxAlongWidthMm - note.HeightMm);
                    w = direct ? note.WidthMm : note.HeightMm;
                    h = direct ? note.HeightMm : note.WidthMm;
                    source = "Trục đứng - ghi chú: " + note.RawText;
                }
                else if (feeder.WidthMm > 0 && feeder.HeightMm > 0)
                {
                    w = feeder.WidthMm;
                    h = feeder.HeightMm;
                    source = string.Format("Trục đứng - lan truyền từ ống đấu vào {0}x{1}", (int)w, (int)h);
                }
                else
                {
                    w = SnapStandardDuctWidthMm(boxAlongWidthMm);
                    h = SnapStandardDuctWidthMm(boxAcrossMm);
                    source = "Trục đứng - đo theo khung CAD";
                }

                CadDuctSegment riser = new CadDuctSegment();
                riser.ShapeKind = CadShapeKind.StraightDuct;
                riser.IsRiser = true;
                riser.RiserWidthAxis = widthAxis;
                riser.CenterPoint = new XYZ(r.Center.X, r.Center.Y, zFeet);
                riser.StartPoint = riser.CenterPoint;
                riser.EndPoint = riser.CenterPoint;
                riser.Direction = widthAxis;
                riser.Length = DefaultStoreyHeightMm;
                riser.WidthMm = w;
                riser.Width2Mm = w;
                riser.HeightMm = h;
                riser.CenterElevationMm = centerElevMm;
                riser.LayerName = r.LayerName;
                riser.ColorRgb = r.ColorRgb;
                riser.SizeSource = source;
                riser.TextLabel = string.Format("{0}x{1} (Trục đứng)", (int)w, (int)h);
                riser.HasDirectText = note != null;
                riser.PolygonVertices = BuildRiserFootprint(riser);
                ducts.Add(riser);
                added++;

                if (feeder != null) edges.Add(new CadConnectionEdge(riser, feeder, CadConnectionKind.RiserTap, null, riser.CenterPoint));
            }

            if (added > 0) RebuildNeighborLinks(ducts, edges);
            return added;
        }

        /// <summary>Khung mặt bằng của trục đứng theo đúng W x H hiện tại (dùng cho hình xem trước).</summary>
        public static List<XYZ> BuildRiserFootprint(CadDuctSegment riser)
        {
            XYZ u = riser.RiserWidthAxis ?? XYZ.BasisX;
            XYZ v = new XYZ(-u.Y, u.X, 0.0);
            XYZ du = u * (riser.WidthMm * 0.5 * MM_TO_FEET), dv = v * (riser.HeightMm * 0.5 * MM_TO_FEET);
            XYZ c = riser.CenterPoint;
            return new List<XYZ> { c - du - dv, c + du - dv, c + du + dv, c - du + dv };
        }

        private static CadDuctSegment FindRiserFeeder(RiserSymbol r, List<CadDuctSegment> ducts)
        {
            double tol = 60.0 * MM_TO_FEET;
            CadDuctSegment best = null;
            foreach (CadDuctSegment d in ducts)
            {
                if (d.IsRiser || DistXY(d.StartPoint, d.EndPoint) < 1e-6) continue;
                XYZ[] ends = new XYZ[] { d.StartPoint, d.EndPoint };
                for (int i = 0; i < 2; i++)
                {
                    XYZ into = ends[i] - ends[1 - i];
                    into = new XYZ(into.X, into.Y, 0.0).Normalize(); // hướng từ thân ống đi vào đầu mút
                    XYZ rel = ends[i] - r.Center;
                    double pu = rel.X * r.AxisU.X + rel.Y * r.AxisU.Y, pv = rel.X * r.AxisV.X + rel.Y * r.AxisV.Y;

                    bool alongU = Math.Abs(into.DotProduct(r.AxisU)) > 0.97;
                    if (!alongU && Math.Abs(into.DotProduct(r.AxisV)) <= 0.97) continue;
                    double p = alongU ? pu : pv, q = alongU ? pv : pu;
                    double halfP = (alongU ? r.SizeU : r.SizeV) * 0.5, halfQ = (alongU ? r.SizeV : r.SizeU) * 0.5;
                    double towards = into.DotProduct(alongU ? r.AxisU : r.AxisV);

                    if (Math.Abs(q) > halfQ || Math.Abs(Math.Abs(p) - halfP) > tol || p * towards >= 0) continue;
                    if (best == null || d.WidthMm > best.WidthMm) best = d;
                }
            }
            return best;
        }

        /// <summary>Ghi chú kích thước trùng đúng 2 cạnh khung (VD "KT: 450x1000 mm" cho khung 450x1000).</summary>
        private static CadTextNote FindRiserSizeNote(RiserSymbol r, List<CadTextNote> notes, out bool widthOnU)
        {
            widthOnU = true;
            if (notes == null) return null;
            double u = r.SizeU * FEET_TO_MM, v = r.SizeV * FEET_TO_MM;
            CadTextNote best = null;
            double bestDist = double.MaxValue;
            foreach (CadTextNote n in notes)
            {
                if (n == null || n.WidthMm <= 0 || n.HeightMm <= 0 || n.Position == null) continue;
                bool onU = Math.Abs(u - n.WidthMm) <= 25.0 && Math.Abs(v - n.HeightMm) <= 25.0;
                bool onV = Math.Abs(v - n.WidthMm) <= 25.0 && Math.Abs(u - n.HeightMm) <= 25.0;
                if (!onU && !onV) continue;

                double dist = DistXY(n.HasLeaderArrow && n.LeaderArrowPoint != null ? n.LeaderArrowPoint : n.Position, r.Center);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = n;
                    widthOnU = onU;
                }
            }
            return best;
        }

        // ---------------------------------------------------------------- CHIỀU CAO TẦNG & DỰNG TRONG REVIT

        /// <summary>Chiều cao 1 tầng: từ Level đang chọn tới Level kế trên. Gọi trong ngữ cảnh Revit API.</summary>
        public static double GetStoreyHeightMm(Document doc, ElementId levelId, out bool fromLevels)
        {
            fromLevels = false;
            Level level = doc.GetElement(levelId) as Level;
            if (level == null) return DefaultStoreyHeightMm;

            double best = double.MaxValue;
            foreach (Element e in new FilteredElementCollector(doc).OfClass(typeof(Level)))
            {
                Level other = e as Level;
                if (other == null) continue;
                double riseMm = (other.Elevation - level.Elevation) * FEET_TO_MM;
                if (riseMm >= MinStoreyHeightMm && riseMm < best) best = riseMm;
            }
            if (best == double.MaxValue) return DefaultStoreyHeightMm;
            fromLevels = true;
            return best;
        }

        /// <summary>Ống đứng chạy từ cốt tầng lên tầng kế; nới thêm nếu ống ngang đấu vào nằm sát hoặc ngoài khoảng đó.</summary>
        private static void GetRiserEndPoints(CadDuctSegment riser, double levelElevFeet, double storeyMm, out XYZ bottom, out XYZ top)
        {
            double branchMm = riser.CenterElevationMm;
            double clearMm = 300.0;
            foreach (CadConnectionEdge e in riser.Connections)
            {
                if (e.Kind == CadConnectionKind.RiserTap && e.SegmentB.HeightMm > 0) clearMm = Math.Max(clearMm, e.SegmentB.HeightMm + 150.0);
            }
            double bottomMm = Math.Min(0.0, branchMm - clearMm);
            double topMm = Math.Max(storeyMm, branchMm + clearMm);
            bottom = new XYZ(riser.CenterPoint.X, riser.CenterPoint.Y, levelElevFeet + bottomMm * MM_TO_FEET);
            top = new XYZ(riser.CenterPoint.X, riser.CenterPoint.Y, levelElevFeet + topMm * MM_TO_FEET);
        }

        /// <summary>Kéo / cắt đầu ống ngang tới đúng mặt ống đứng để Revit gắn được chân rẽ.</summary>
        private static void TrimFeedersToRiserFace(List<CadConnectionEdge> edges, Dictionary<string, XYZ> starts, Dictionary<string, XYZ> ends)
        {
            foreach (CadConnectionEdge e in edges)
            {
                if (e.Kind != CadConnectionKind.RiserTap) continue;
                CadDuctSegment riser = e.SegmentA, feeder = e.SegmentB;
                XYZ s, t;
                if (!starts.TryGetValue(feeder.Id, out s) || !ends.TryGetValue(feeder.Id, out t)) continue;

                bool startIsNear = DistXY(s, riser.CenterPoint) < DistXY(t, riser.CenterPoint);
                XYZ near = startIsNear ? s : t, other = startIsNear ? t : s;
                XYZ into = new XYZ(near.X - other.X, near.Y - other.Y, 0.0);
                if (into.GetLength() < 1e-6) continue;
                into = into.Normalize();

                XYZ u = riser.RiserWidthAxis ?? XYZ.BasisX;
                double depthMm = Math.Abs(into.DotProduct(u)) > 0.7 ? riser.WidthMm : riser.HeightMm;
                double alongToCenter = (riser.CenterPoint.X - other.X) * into.X + (riser.CenterPoint.Y - other.Y) * into.Y;
                double newLen = alongToCenter - depthMm * 0.5 * MM_TO_FEET;
                if (newLen < 100.0 * MM_TO_FEET) continue;

                XYZ face = new XYZ(other.X + into.X * newLen, other.Y + into.Y * newLen, near.Z);
                if (startIsNear) starts[feeder.Id] = face;
                else ends[feeder.Id] = face;
            }
        }

        /// <summary>Xoay ống đứng để cạnh W nằm đúng phương như trên mặt bằng CAD.</summary>
        private static void AlignRiserSection(Document doc, Duct duct, CadDuctSegment seg)
        {
            doc.Regenerate();
            Connector first = duct.ConnectorManager.Connectors.Cast<Connector>().FirstOrDefault();
            if (first == null || seg.RiserWidthAxis == null) return;

            XYZ current = first.CoordinateSystem.BasisX;
            double angle = Math.Atan2(seg.RiserWidthAxis.Y, seg.RiserWidthAxis.X) - Math.Atan2(current.Y, current.X);
            while (angle > Math.PI / 2) angle -= Math.PI;   // tiết diện chữ nhật đối xứng 180 độ
            while (angle <= -Math.PI / 2) angle += Math.PI;
            if (Math.Abs(angle) < 0.002) return;

            XYZ basePoint = new XYZ(seg.CenterPoint.X, seg.CenterPoint.Y, 0.0);
            ElementTransformUtils.RotateElement(doc, duct.Id, Line.CreateBound(basePoint, basePoint + XYZ.BasisZ), angle);
        }
    }
}
