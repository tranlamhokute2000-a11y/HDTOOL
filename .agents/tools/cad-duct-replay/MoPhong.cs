using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;
using HDTool;

// Chạy lại đúng trình tự CadToDuctService.AnalyzeCadDuctNetwork ngoài Revit.
// Thêm / đổi bước trong AnalyzeCadDuctNetwork thì PHẢI sửa theo ở đây, nếu không kết quả mô phỏng sai lệch.
// Có CadDuct_scan.log trong thư mục dữ liệu -> dùng đúng nét + ghi chú + vùng loại trừ Revit đã đọc; không có -> dùng lines.txt / cache.txt trích từ DWG.
static class Pipeline
{
    const double F = 1.0 / 304.8;
    const double Elev = 3000.0;
    static BindingFlags SF = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;

    public static void Execute(string dir, Type svc, IList polys, IList lines, IList arcs, string name, Dictionary<string, int> entPerLayer)
    {
        Func<string, object[], object> call = delegate(string n, object[] a) { return svc.GetMethod(n, SF).Invoke(null, a); };
        string outDir = Path.Combine(dir, "ket-qua");
        Directory.CreateDirectory(outDir);

        CadDwgExtractorOffline.OfflineCache = Path.Combine(dir, "cache.txt");
        List<CadTextNote> notes; List<CadDamperItem> dampers; List<CadExclusionZone> excl; string diag;
        CadDwgExtractorOffline.ExtractTextAndDampersFromDwg(null, null, out notes, out dampers, out excl, out diag);

        string scanLog = Path.Combine(dir, "CadDuct_scan.log");
        bool fromScan = File.Exists(scanLog);
        if (fromScan)
        {
            LoadScanNotes(scanLog, notes, excl);
            LoadScan(scanLog, call, polys, lines, arcs);
        }
        Console.WriteLine("NGUON: {0} | ghi chu={1} vung loai tru={2}", fromScan ? "nhat ky quet Revit" : "DWG", notes.Count, excl.Count);

        var cadLines = (float[])call("FlattenLinework", new object[] { polys, lines });
        HashSet<int> consumed = new HashSet<int>();
        var fits = new List<CadDuctSegment>((List<CadDuctSegment>)call("PairConcentricArcsIntoElbows", new object[] { arcs, lines, Elev, null, consumed }));
        call("ReconstructClosedLoopsFromLines", new object[] { lines, polys, consumed });
        var rects = new List<CadDuctSegment>();
        foreach (object rp in polys)
        {
            Type t = rp.GetType();
            var c = (CadDuctSegment)call("ClassifyPolygonShape", new object[] { t.GetField("Points").GetValue(rp), (string)t.GetField("LayerName").GetValue(rp), Elev });
            if (c == null) continue;
            c.ColorRgb = (int)t.GetField("ColorRgb").GetValue(rp);
            var src = (int[])t.GetField("SourceLineIndices").GetValue(rp);
            if (src != null) foreach (int i in src) consumed.Add(i);
            if (c.ShapeKind == CadShapeKind.StraightDuct) rects.Add(c); else fits.Add(c);
        }

        var leaders = (HashSet<int>)call("FindLeaderLineIndices", new object[] { polys, lines, notes });
        var filtered = (IList)Activator.CreateInstance(lines.GetType());
        for (int i = 0; i < lines.Count; i++)
        {
            if (consumed.Contains(i) || leaders.Contains(i)) continue;
            var ln = (Line)lines[i].GetType().GetField("Line").GetValue(lines[i]);
            XYZ p0 = ln.GetEndPoint(0), p1 = ln.GetEndPoint(1);
            if ((bool)call("IsLineFullyInsideExclusionZones", new object[] { p0, p1, (p0 + p1) * 0.5, excl })) continue;
            filtered.Add(lines[i]);
        }
        foreach (var d in (List<CadDuctSegment>)call("MatchParallelLinesIntoDucts", new object[] { filtered, Elev }))
            if (!(bool)call("IsDuplicateSegment", new object[] { d, rects })) rects.Add(d);
        Console.WriteLine("DUONG-DAN: {0} net", leaders.Count);
        Dump("truoc loc", rects);

        object risers = call("DetectRiserSymbols", new object[] { polys, lines });
        int trimmed = (int)call("TrimDuctsAtRiserSymbols", new object[] { risers, rects });
        call("FilterFalsePositiveSegments", new object[] { rects, fits, lines, excl });
        Dump("sau loc", rects);
        Console.WriteLine("KHOI-PHUC: " + call("RecoverAnnotatedDucts", new object[] { polys, notes, rects, fits, Elev, null }));
        trimmed += (int)call("TrimDuctsAtRiserSymbols", new object[] { risers, rects });
        object stubs = call("RestoreRiserStubs", new object[] { risers, polys, rects, fits, Elev, null });
        Console.WriteLine("CAT-TAI-TRUC: {0} | ky hieu truc: {1} | khuc cut: {2}", trimmed, ((ICollection)risers).Count, ((ICollection)stubs).Count);

        call("OrientShortCollarRectanglesByTopology", new object[] { rects, fits, notes });
        var edges = (List<CadConnectionEdge>)call("BuildGeometricTopology", new object[] { rects, fits });
        call("NormalizeBranchesToTaps", new object[] { rects, fits, edges });
        call("LinkRiserStubs", new object[] { stubs, rects, edges });
        Console.WriteLine("BO-NOI-QUA-TRUC: " + call("RemoveLinksThroughRisers", new object[] { risers, rects, edges }));
        call("RebuildNeighborLinks", new object[] { rects, edges });

        CadToDuctService.MatchAndPropagateDuctSizes(rects, notes, 0);
        call("ConvertInlineReducers", new object[] { rects, fits, edges });
        call("MergeCollinearTapHosts", new object[] { rects, edges });
        call("AssignFallbackTapBevelDirections", new object[] { rects, edges });
        Console.WriteLine("ONG-DUNG: " + call("AttachRisers", new object[] { risers, notes, rects, edges, Elev, (Func<XYZ, bool>)delegate(XYZ p) { return true; } }));
        foreach (var d in rects) if (d.IsRiser) d.Length = 3300;

        var linked = new HashSet<CadDuctSegment>();
        foreach (var e in edges) if (e.BridgePolygon != null) linked.Add(e.BridgePolygon);
        fits.RemoveAll(delegate(CadDuctSegment q)
        {
            return !linked.Contains(q) && q.CenterPoint != null && rects.Exists(delegate(CadDuctSegment d) { return !d.IsRiser && d.ContainsPointXY(q.CenterPoint); });
        });
        Dump("cuoi", rects);

        int risersCount = rects.Count(delegate(CadDuctSegment d) { return d.IsRiser; });
        Console.WriteLine("KET QUA: {0} ong = {1} ngang + {2} truc dung | {3} phu kien | {4} lien ket", rects.Count, rects.Count - risersCount, risersCount, fits.Count, edges.Count);

        // Liên kết theo toạ độ (để so hồi quy, không phụ thuộc số thứ tự)
        var rows = new List<string>();
        foreach (var e in edges)
        {
            string a = Describe(e.SegmentA), b = Describe(e.SegmentB);
            if (e.Kind != CadConnectionKind.TakeoffTap && e.Kind != CadConnectionKind.RiserTap && string.CompareOrdinal(a, b) > 0) { string t = a; a = b; b = t; }
            string via = e.BridgePolygon == null ? "-" : string.Format("{0}@({1:0},{2:0}) W={3:0}/{4:0}", e.BridgePolygon.ShapeKind, e.BridgePolygon.CenterPoint.X / F, e.BridgePolygon.CenterPoint.Y / F, e.BridgePolygon.WidthMm, e.BridgePolygon.Width2Mm);
            rows.Add(string.Format("{0} {1} + {2} via {3}", e.Kind, a, b, via));
        }
        foreach (var d in rects) if (!edges.Exists(delegate(CadConnectionEdge e) { return e.SegmentA == d || e.SegmentB == d; })) rows.Add("CHUA-NOI " + Describe(d));
        rows.Sort(StringComparer.Ordinal);
        File.WriteAllLines(Path.Combine(outDir, "lienket.txt"), rows.ToArray(), System.Text.Encoding.UTF8);

        Draw(Path.Combine(outDir, "mat-bang.png"), cadLines, rects, fits, notes);
        var form = new CadDuctPreviewForm();
        form.SetData(rects, fits, edges, cadLines);
        form.StartPosition = System.Windows.Forms.FormStartPosition.Manual; form.Location = new System.Drawing.Point(10, 10); form.Opacity = 0.01; form.Show();
        var canvas = (CadDuctPreviewCanvas)typeof(CadDuctPreviewForm).GetField("_canvas", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(form);
        canvas.ResetView(false);
        System.Windows.Forms.Application.DoEvents();
        using (var bmp = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bmp, new System.Drawing.Rectangle(0, 0, form.Width, form.Height)); bmp.Save(Path.Combine(outDir, "xem-truoc-3d.png"), ImageFormat.Png); }
        form.Close();
    }

    static string Describe(CadDuctSegment d)
    {
        return string.Format("[{0:0},{1:0} > {2:0},{3:0} {4:0}x{5:0}{6}]", d.StartPoint.X / F, d.StartPoint.Y / F, d.EndPoint.X / F, d.EndPoint.Y / F, d.WidthMm, d.HeightMm, d.IsRiser ? " TRUC" : "");
    }

    // Nét Revit đã đọc (POLY / LINE trong nhật ký quét). Các vòng dựng lại từ LINE nằm cuối danh sách POLY -> bỏ để pipeline tự dựng lại.
    static void LoadScan(string path, Func<string, object[], object> call, IList polys, IList lines, IList arcs)
    {
        Type lineType = lines.GetType().GetGenericArguments()[0];
        polys.Clear(); lines.Clear(); arcs.Clear();
        var logPolys = new List<Tuple<string, List<XYZ>>>();
        foreach (string row in File.ReadAllLines(path, System.Text.Encoding.UTF8))
        {
            var c = row.Split('|');
            if (c[0] == "POLY") logPolys.Add(Tuple.Create(c[1], c[2].Split(';').Select(Pt).ToList()));
            else if (c[0] == "LINE")
            {
                var q = c[2].Split(';').Select(Pt).ToList();
                object ln = Activator.CreateInstance(lineType, true);
                lineType.GetField("Line").SetValue(ln, Line.CreateBound(q[0], q[1]));
                lineType.GetField("LayerName").SetValue(ln, c[1]);
                lines.Add(ln);
            }
        }
        var trial = (IList)Activator.CreateInstance(polys.GetType());
        call("ReconstructClosedLoopsFromLines", new object[] { lines, trial, new HashSet<int>() });
        logPolys.RemoveRange(logPolys.Count - trial.Count, trial.Count);
        foreach (var lp in logPolys)
            foreach (var part in (List<List<XYZ>>)call("SplitPinchedPolyline", new object[] { lp.Item2 }))
                call("AddPolylinePart", new object[] { part, lp.Item1, 0, polys, lines });
    }

    // Ghi chú + vùng loại trừ đúng như Revit đã đọc (toạ độ Revit có thể lệch DWG nên không dùng bản trích từ DWG)
    static void LoadScanNotes(string path, List<CadTextNote> notes, List<CadExclusionZone> excl)
    {
        notes.Clear(); excl.Clear();
        foreach (string row in File.ReadAllLines(path, System.Text.Encoding.UTF8))
        {
            var c = row.Split('|');
            if (c[0] == "NOTE" && c.Length >= 3)
            {
                var m = System.Text.RegularExpressions.Regex.Match(c[1], @"(\d+)\s*[xX]\s*(\d+)");
                if (!m.Success) continue;
                var n = new CadTextNote(c[1], double.Parse(m.Groups[1].Value), double.Parse(m.Groups[2].Value), Pt(c[2]), "");
                if (c.Length >= 4 && c[3].StartsWith("LDR=")) { n.HasLeaderArrow = true; n.LeaderArrowPoint = Pt(c[3].Substring(4)); }
                notes.Add(n);
            }
            else if (c[0] == "ZONE" && c.Length >= 5)
            {
                var z = new CadExclusionZone();
                z.Kind = (CadExclusionKind)Enum.Parse(typeof(CadExclusionKind), c[1]);
                z.BlockName = c[2]; z.LayerName = c[3]; z.TagText = "";
                foreach (var p in c[4].Split(';')) if (p.Length > 0) z.Corners.Add(Pt(p));
                if (z.Corners.Count == 0) continue;
                z.Center = new XYZ(z.Corners.Average(delegate(XYZ q) { return q.X; }), z.Corners.Average(delegate(XYZ q) { return q.Y; }), 0);
                z.ComputeBoundsFromCorners();
                excl.Add(z);
            }
        }
    }

    static XYZ Pt(string t)
    {
        var a = t.Split(',');
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        return new XYZ(double.Parse(a[0], inv) * F, double.Parse(a[1], inv) * F, 0);
    }

    static void Dump(string title, List<CadDuctSegment> ds)
    {
        Console.WriteLine("--- {0}: {1}", title, ds.Count);
        int k = 0;
        foreach (var d in ds)
        {
            k++;
            Console.WriteLine("#{0} S=({1:0},{2:0}) E=({3:0},{4:0}) L={5:0} W={6:0} H={7:0}{8} [{9}]", k,
                d.StartPoint.X / F, d.StartPoint.Y / F, d.EndPoint.X / F, d.EndPoint.Y / F, d.IsRiser ? d.Length : d.StartPoint.DistanceTo(d.EndPoint) / F,
                d.WidthMm, d.HeightMm, d.IsRiser ? " TRUC" : "", d.SizeSource);
        }
    }

    static void Draw(string path, float[] cad, List<CadDuctSegment> rects, List<CadDuctSegment> fits, List<CadTextNote> notes)
    {
        var xs = new List<double>(); var ys = new List<double>();
        foreach (var d in rects.Concat(fits)) { xs.Add(d.CenterPoint.X); ys.Add(d.CenterPoint.Y); }
        if (xs.Count == 0) return;
        double x0 = xs.Min() - 5, x1 = xs.Max() + 5, y0 = ys.Min() - 5, y1 = ys.Max() + 5;
        double sc = 1900 / (x1 - x0); int W = 1900, H = Math.Max(200, (int)((y1 - y0) * sc));
        using (var bmp = new Bitmap(W, H))
        using (var g = Graphics.FromImage(bmp))
        using (var font = new Font("Segoe UI", 8f))
        {
            g.Clear(System.Drawing.Color.White);
            Func<double, double, PointF> P = delegate(double x, double y) { return new PointF((float)((x - x0) * sc), (float)((y1 - y) * sc)); };
            for (int i = 0; i + 3 < cad.Length; i += 4) g.DrawLine(Pens.Gray, P(cad[i], cad[i + 1]), P(cad[i + 2], cad[i + 3]));
            foreach (var f in fits) if (f.PolygonVertices != null && f.PolygonVertices.Count > 2) g.FillPolygon(new SolidBrush(System.Drawing.Color.FromArgb(110, 0, 150, 0)), f.PolygonVertices.Select(delegate(XYZ p) { return P(p.X, p.Y); }).ToArray());
            int k = 0;
            foreach (var d in rects)
            {
                k++;
                if (d.PolygonVertices != null && d.PolygonVertices.Count > 2) g.FillPolygon(new SolidBrush(System.Drawing.Color.FromArgb(90, 40, 40, 255)), d.PolygonVertices.Select(delegate(XYZ p) { return P(p.X, p.Y); }).ToArray());
                g.DrawLine(Pens.Blue, P(d.StartPoint.X, d.StartPoint.Y), P(d.EndPoint.X, d.EndPoint.Y));
                var m = P((d.StartPoint.X + d.EndPoint.X) / 2, (d.StartPoint.Y + d.EndPoint.Y) / 2);
                g.DrawString(string.Format("#{0} {1:0}x{2:0}", k, d.WidthMm, d.HeightMm), font, Brushes.Black, m.X + 2, m.Y - 14);
            }
            foreach (var n in notes) g.DrawString(n.RawText, font, Brushes.Red, P(n.Position.X, n.Position.Y));
            bmp.Save(path, ImageFormat.Png);
        }
    }
}
