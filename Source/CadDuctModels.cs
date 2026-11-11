using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace HDTool
{
    public enum CadShapeKind
    {
        StraightDuct,   // Hình chữ nhật kín 4 đỉnh hoặc cặp nét song song
        Transition,     // Hình thang Côn thu (W1 -> W2)
        TakeoffTap,     // Hình thang vát 45 độ Chân rẽ (Tap vào sườn ống chính)
        Elbow,          // Cút góc chuyển hướng (90 / 45 độ)
        TeeBranch       // Chạc ba / chạc chia nhánh (Y / T)
    }

    public enum CadConnectionKind
    {
        CollinearDirect, // Nối tiếp thẳng hàng cùng kích thước
        Transition,      // Côn thu đồng tâm (W1 -> W2)
        Elbow,           // Cút góc chuyển hướng
        TakeoffTap,      // Chân rẽ Tap đâm sườn ống chính
        TeeBranch        // Chạc 3 chia nhánh
    }

    /// <summary>
    /// Kết nối hình học giữa 2 đoạn ống gió đã nhận diện
    /// </summary>
    public class CadConnectionEdge
    {
        public CadDuctSegment SegmentA { get; set; }
        public CadDuctSegment SegmentB { get; set; }
        public CadConnectionKind Kind { get; set; }
        public CadDuctSegment BridgePolygon { get; set; }
        public XYZ JunctionPoint { get; set; }

        public CadConnectionEdge(CadDuctSegment a, CadDuctSegment b, CadConnectionKind kind, CadDuctSegment bridge = null, XYZ junction = null)
        {
            SegmentA = a;
            SegmentB = b;
            Kind = kind;
            BridgePolygon = bridge;
            JunctionPoint = junction;
        }
    }

    /// <summary>
    /// Model dữ liệu một đoạn ống gió hoặc phụ kiện nhận diện bằng HÌNH VẼ HÌNH HỌC từ bản vẽ CAD
    /// </summary>
    public class CadDuctSegment
    {
        public string Id { get; set; }
        public CadShapeKind ShapeKind { get; set; }

        public XYZ StartPoint { get; set; }
        public XYZ EndPoint { get; set; }
        public XYZ CenterPoint { get; set; }
        public XYZ Direction { get; set; }
        public double Length { get; set; }

        public double WidthMm { get; set; }   // Bề rộng tại StartPoint (hoặc toàn đoạn nếu là ống thẳng)
        public double Width2Mm { get; set; }  // Bề rộng tại EndPoint (nếu là Côn thu Transition)
        public double HeightMm { get; set; }
        public double CenterElevationMm { get; set; }

        public string LayerName { get; set; }
        public string TextLabel { get; set; }
        public bool HasDirectText { get; set; }
        public string SizeSource { get; set; }

        // Lưu toàn bộ các đỉnh đa giác 2D/3D để kiểm tra Pick trực tiếp và giao cắt hình học
        public List<XYZ> PolygonVertices { get; set; }

        public ElementId CreatedDuctId { get; set; }

        // Kết nối mạng lưới đồ thị hình học (Geometric Graph Topology)
        public List<CadDuctSegment> ConnectedNeighbors { get; set; }
        public List<CadConnectionEdge> Connections { get; set; }
        public bool IsProcessed { get; set; }

        public CadDuctSegment()
        {
            Id = Guid.NewGuid().ToString("N").Substring(0, 8);
            ShapeKind = CadShapeKind.StraightDuct;
            ConnectedNeighbors = new List<CadDuctSegment>();
            Connections = new List<CadConnectionEdge>();
            PolygonVertices = new List<XYZ>();
            WidthMm = 300;
            Width2Mm = 300;
            HeightMm = 300;
            CenterElevationMm = 3200;
            TextLabel = string.Empty;
            SizeSource = "Mặc định / Lan truyền";
            HasDirectText = false;
            IsProcessed = false;
        }

        public double DistanceToPointXY(XYZ pt)
        {
            if (pt == null || StartPoint == null || EndPoint == null) return double.MaxValue;
            XYZ p = new XYZ(pt.X, pt.Y, 0);
            XYZ a = new XYZ(StartPoint.X, StartPoint.Y, 0);
            XYZ b = new XYZ(EndPoint.X, EndPoint.Y, 0);
            XYZ v = b - a;
            double len = v.GetLength();
            if (len < 0.0001) return a.DistanceTo(p);
            XYZ u = v.Normalize();
            double t = (p - a).DotProduct(u);
            if (t <= 0) return a.DistanceTo(p);
            if (t >= len) return b.DistanceTo(p);
            XYZ proj = a + u * t;
            return proj.DistanceTo(p);
        }

        public double DistanceToPoint(XYZ pt)
        {
            return DistanceToPointXY(pt);
        }

        /// <summary>
        /// Kiểm tra điểm có nằm bên trong đa giác 2D hình vẽ ống gió (hoặc rất sát biên) không
        /// </summary>
        public bool ContainsPointXY(XYZ pt, double marginFeet = 0.0)
        {
            if (PolygonVertices != null && PolygonVertices.Count >= 3)
            {
                if (IsPointInPolygonXY(pt, PolygonVertices)) return true;
                // Kiểm tra gần cạnh đa giác
                if (marginFeet > 0)
                {
                    for (int i = 0; i < PolygonVertices.Count; i++)
                    {
                        XYZ v1 = PolygonVertices[i];
                        XYZ v2 = PolygonVertices[(i + 1) % PolygonVertices.Count];
                        if (DistPointToSegmentXY(pt, v1, v2) <= marginFeet) return true;
                    }
                }
            }
            // Fallback kiểm tra khoảng cách tới trục tim <= nửa bề rộng + margin
            double halfW = (Math.Max(WidthMm, Width2Mm) * 0.5 / 304.8) + marginFeet;
            return DistanceToPointXY(pt) <= halfW;
        }

        public static bool IsPointInPolygonXY(XYZ p, List<XYZ> poly)
        {
            bool inside = false;
            int n = poly.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double xi = poly[i].X, yi = poly[i].Y;
                double xj = poly[j].X, yj = poly[j].Y;
                bool intersect = ((yi > p.Y) != (yj > p.Y)) &&
                    (p.X < (xj - xi) * (p.Y - yi) / ((yj - yi) == 0 ? 1e-9 : (yj - yi)) + xi);
                if (intersect) inside = !inside;
            }
            return inside;
        }

        public static double DistPointToSegmentXY(XYZ pt, XYZ a, XYZ b)
        {
            double ax = a.X, ay = a.Y, bx = b.X, by = b.Y, px = pt.X, py = pt.Y;
            double dx = bx - ax, dy = by - ay;
            double len2 = dx * dx + dy * dy;
            if (len2 < 1e-10) return Math.Sqrt((px - ax) * (px - ax) + (py - ay) * (py - ay));
            double t = ((px - ax) * dx + (py - ay) * dy) / len2;
            if (t < 0) t = 0;
            else if (t > 1) t = 1;
            double cx = ax + t * dx, cy = ay + t * dy;
            return Math.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
        }
    }

    /// <summary>
    /// Ghi chú Text kích thước nhận diện được từ bản vẽ CAD (WxH)
    /// </summary>
    public class CadTextNote
    {
        public string RawText { get; set; }
        public double WidthMm { get; set; }
        public double HeightMm { get; set; }
        public XYZ Position { get; set; }
        public string LayerName { get; set; }

        public CadTextNote(string text, double w, double h, XYZ pos, string layer)
        {
            RawText = text;
            WidthMm = w;
            HeightMm = h;
            Position = pos;
            LayerName = layer ?? string.Empty;
        }
    }

    /// <summary>
    /// Ký hiệu van gió VCD / MFD / NRD nhận diện trên tuyến ống
    /// </summary>
    public class CadDamperItem
    {
        public string TypeName { get; set; } // "VCD", "MFD", "NRD"
        public XYZ Position { get; set; }
        public double WidthMm { get; set; }
        public double HeightMm { get; set; }
        public XYZ Direction { get; set; }

        public CadDamperItem(string typeName, XYZ pos, double w, double h, XYZ dir)
        {
            TypeName = typeName;
            Position = pos;
            WidthMm = w;
            HeightMm = h;
            Direction = dir;
        }
    }

    /// <summary>
    /// Cặp nét thẳng song song CAD đại diện cho 2 biên của ống gió
    /// </summary>
    public class CadParallelPair
    {
        public Line Line1 { get; set; }
        public Line Line2 { get; set; }
        public double DistanceMm { get; set; }
        public XYZ MidStart { get; set; }
        public XYZ MidEnd { get; set; }
        public XYZ Direction { get; set; }
        public string LayerName { get; set; }
    }
}
