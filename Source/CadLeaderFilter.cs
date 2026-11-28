using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace HDTool
{
    /// <summary>
    /// Đường dẫn ghi chú (leader) vẽ bằng LINE trên layer ống: 2 đường dẫn song song cạnh nhau dễ bị ghép thành 2 mép một ống giả.
    /// Nhận ra chúng từ đầu mũi tên rồi loại khỏi bước ghép cặp nét song song.
    /// </summary>
    public static partial class CadToDuctService
    {
        private const double LeaderJoinTolMm = 5.0;
        private const int LeaderMaxSegments = 5;

        private class LeaderSeed
        {
            public XYZ Tip;
            public XYZ Axis; // từ đỉnh mũi tên đi về phía đuôi (hướng nét đường dẫn đi ra); null nếu chỉ biết điểm mũi tên
        }

        /// <summary>Chỉ số các LINE thuộc đường dẫn: chuỗi nét nối tiếp bắt đầu từ đầu mũi tên.</summary>
        private static HashSet<int> FindLeaderLineIndices(List<RawPolyShape> polys, List<RawLineShape> lines, List<CadTextNote> notes)
        {
            HashSet<int> leader = new HashSet<int>();
            if (lines == null || lines.Count == 0) return leader;

            List<LeaderSeed> seeds = new List<LeaderSeed>();
            if (polys != null)
            {
                foreach (RawPolyShape p in polys)
                {
                    LeaderSeed s = TryGetArrowHead(p.Points);
                    if (s != null) seeds.Add(s);
                }
            }
            if (notes != null)
            {
                foreach (CadTextNote n in notes)
                {
                    if (n != null && n.HasLeaderArrow && n.LeaderArrowPoint != null)
                        seeds.Add(new LeaderSeed { Tip = n.LeaderArrowPoint, Axis = null });
                }
            }
            if (seeds.Count == 0) return leader;

            double tol = LeaderJoinTolMm * MM_TO_FEET;
            foreach (LeaderSeed seed in seeds)
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    if (leader.Contains(i)) continue;
                    XYZ a = lines[i].Line.GetEndPoint(0), b = lines[i].Line.GetEndPoint(1);
                    bool atA = DistXY(a, seed.Tip) <= tol, atB = DistXY(b, seed.Tip) <= tol;
                    if (!atA && !atB) continue;

                    XYZ far = atA ? b : a;
                    if (seed.Axis != null)
                    {
                        XYZ dir = new XYZ(far.X - seed.Tip.X, far.Y - seed.Tip.Y, 0.0);
                        if (dir.GetLength() < 1e-6 || dir.Normalize().DotProduct(seed.Axis) < 0.9) continue; // nét không đi ra từ đuôi mũi tên
                    }
                    FollowLeaderChain(i, far, lines, leader, tol);
                }
            }
            return leader;
        }

        /// <summary>Đi tiếp qua các góc gấp: đầu nét chỉ nối đúng 1 nét khác thì nét đó cũng thuộc đường dẫn.</summary>
        private static void FollowLeaderChain(int first, XYZ end, List<RawLineShape> lines, HashSet<int> leader, double tol)
        {
            leader.Add(first);
            XYZ cursor = end;
            for (int step = 1; step < LeaderMaxSegments; step++)
            {
                int next = -1, touching = 0;
                for (int j = 0; j < lines.Count; j++)
                {
                    if (leader.Contains(j)) continue;
                    XYZ a = lines[j].Line.GetEndPoint(0), b = lines[j].Line.GetEndPoint(1);
                    if (DistXY(a, cursor) <= tol || DistXY(b, cursor) <= tol)
                    {
                        touching++;
                        next = j;
                    }
                }
                if (touching != 1) return; // hết đường hoặc gặp chỗ rẽ nhiều nét: dừng, không lan sang hình khác
                XYZ na = lines[next].Line.GetEndPoint(0), nb = lines[next].Line.GetEndPoint(1);
                leader.Add(next);
                cursor = DistXY(na, cursor) <= tol ? nb : na;
            }
        }

        /// <summary>Đầu mũi tên: tam giác kín nhỏ (cạnh dài nhất &lt;= 800mm), đáy ngắn rõ rệt so với 2 cạnh bên.</summary>
        private static LeaderSeed TryGetArrowHead(List<XYZ> points)
        {
            if (points == null || points.Count < 3) return null;
            double tol = 2.0 * MM_TO_FEET;
            List<XYZ> v = new List<XYZ>();
            foreach (XYZ p in points)
            {
                if (v.Count == 0 || DistXY(v[v.Count - 1], p) > tol) v.Add(p);
            }
            if (v.Count > 1 && DistXY(v[0], v[v.Count - 1]) <= tol) v.RemoveAt(v.Count - 1);
            if (v.Count != 3) return null;

            double[] side = new double[3];
            for (int i = 0; i < 3; i++) side[i] = DistXY(v[(i + 1) % 3], v[(i + 2) % 3]); // cạnh đối diện đỉnh i
            int tipIdx = 0;
            for (int i = 1; i < 3; i++)
            {
                if (side[i] < side[tipIdx]) tipIdx = i;
            }
            double longest = Math.Max(side[0], Math.Max(side[1], side[2]));
            if (longest * FEET_TO_MM > 800.0 || side[tipIdx] > longest * 0.6 || side[tipIdx] * FEET_TO_MM < 10.0) return null;

            XYZ tip = v[tipIdx];
            XYZ baseMid = (v[(tipIdx + 1) % 3] + v[(tipIdx + 2) % 3]) * 0.5;
            XYZ axis = new XYZ(baseMid.X - tip.X, baseMid.Y - tip.Y, 0.0);
            if (axis.GetLength() < 1e-6) return null;
            return new LeaderSeed { Tip = tip, Axis = axis.Normalize() };
        }
    }
}
