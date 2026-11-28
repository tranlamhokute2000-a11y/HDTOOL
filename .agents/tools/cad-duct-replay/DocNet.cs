using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Autodesk.Revit.DB;
using HDTool;

static class Run
{
    const double F = 1.0 / 304.8;
    static string Dir;
    static Type Svc, TPoly, TLine, TArc;
    static Dictionary<string, List<string[]>> Blocks = new Dictionary<string, List<string[]>>(StringComparer.OrdinalIgnoreCase);
    static Dictionary<string, double[]> Base = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
    static Dictionary<string, int> LayerColor = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    static IList polys, lines, arcs;

    static double D(string s) { return double.Parse(s, CultureInfo.InvariantCulture); }
    static BindingFlags SF = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;
    static object Call(string name, params object[] a) { return Svc.GetMethod(name, SF).Invoke(null, a); }

    static void Set(object o, string field, object v) { o.GetType().GetField(field).SetValue(o, v); }

    struct M { public double a, b, c, d, tx, ty;
        public double[] P(double x, double y) { return new double[] { a * x + b * y + tx, c * x + d * y + ty }; }
        public M Mul(M o) { M r; r.a = a * o.a + b * o.c; r.b = a * o.b + b * o.d; r.c = c * o.a + d * o.c; r.d = c * o.b + d * o.d; r.tx = a * o.tx + b * o.ty + tx; r.ty = c * o.tx + d * o.ty + ty; return r; } }

    static XYZ X(M m, double x, double y) { double[] p = m.P(x, y); return new XYZ(p[0] * F, p[1] * F, 0); }

    static void AddLine(XYZ p0, XYZ p1, string layer, int color)
    {
        if (p0.DistanceTo(p1) <= 50 * F) return;
        object r = Activator.CreateInstance(TLine, true);
        Set(r, "Line", Line.CreateBound(p0, p1)); Set(r, "LayerName", layer); Set(r, "ColorRgb", color);
        lines.Add(r);
    }

    static void AddPolyPoints(List<XYZ> pts, string layer, int color)
    {
        if (pts.Count < 2) return;
        if (pts.Count >= 4 && pts[0].DistanceTo(pts[pts.Count - 1]) < 15 * F)
        {
            object r = Activator.CreateInstance(TPoly, true);
            Set(r, "Points", pts); Set(r, "LayerName", layer); Set(r, "ColorRgb", color);
            polys.Add(r);
        }
        else if (!(bool)Call("IsAirflowArrowPolygon", pts))
        {
            for (int i = 0; i + 1 < pts.Count; i++) AddLine(pts[i], pts[i + 1], layer, color);
        }
    }

    static void AddArc(XYZ c, XYZ p0, XYZ pm, XYZ p1, List<XYZ> samples, double spanDeg, string layer, int color)
    {
        double r = (c.DistanceTo(p0) + c.DistanceTo(p1) + c.DistanceTo(pm)) / 3.0;
        if (spanDeg <= 15 || spanDeg >= 210 || r < 30 * F || r > 25000 * F) return;
        if (Math.Abs(c.DistanceTo(p0) - c.DistanceTo(pm)) > 15 * F || Math.Abs(c.DistanceTo(p1) - c.DistanceTo(pm)) > 15 * F) return;
        object a = Activator.CreateInstance(TArc, true);
        Set(a, "Center", c); Set(a, "P0", p0); Set(a, "P1", p1); Set(a, "PMid", pm); Set(a, "RadiusFeet", r); Set(a, "SpanDeg", spanDeg);
        Set(a, "SamplePoints", samples); Set(a, "LayerName", layer); Set(a, "ColorRgb", color);
        arcs.Add(a);
    }

    static string OnlyLayer;
    static int Skipped;
    static bool IsTerminal(List<string[]> ents)
    {
        if (ents.Count < 3 || ents.Count > 5) return false;
        var shapes = new List<IList<XYZ>>();
        foreach (var e in ents)
        {
            if (e[0] == "L") shapes.Add(new XYZ[] { new XYZ(D(e[2]) * F, D(e[3]) * F, 0), new XYZ(D(e[4]) * F, D(e[5]) * F, 0) });
            else if (e[0] == "P") { var pts = e[3].Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(delegate(string s) { var q = s.Split(','); return new XYZ(D(q[0]) * F, D(q[1]) * F, 0); }).ToList(); if (e[2] == "1") pts.Add(pts[0]); shapes.Add(pts); }
            else return false;
        }
        return (bool)Call("IsCrossHairTerminalSymbol", shapes);
    }
    static HashSet<string> Used = new HashSet<string>();
    static Dictionary<string, int> EntPerLayer = new Dictionary<string, int>();

    static void Walk(string block, M m, int depth, string blockLayer)
    {
        List<string[]> ents;
        if (depth > 12 || !Blocks.TryGetValue(block, out ents)) return;
        foreach (string[] e in ents)
        {
            string layer = e[1];
            if (e[0] == "I")
            {
                string name = e[2];
                if ((bool)Call("IsNonDuctBlockOrLayer", layer, name)) continue;
                double ix = D(e[3]), iy = D(e[4]), sx = D(e[5]), sy = D(e[6]), rot = D(e[7]); double nzs = D(e[8]);
                double[] b; if (!Base.TryGetValue(name, out b)) b = new double[] { 0, 0 };
                M t; double cs = Math.Cos(rot), sn = Math.Sin(rot);
                t.a = cs * sx; t.b = -sn * sy; t.c = sn * sx; t.d = cs * sy; t.tx = ix - (t.a * b[0] + t.b * b[1]); t.ty = iy - (t.c * b[0] + t.d * b[1]);
                if (nzs < 0) { t.a = -t.a; t.b = -t.b; t.tx = -t.tx; }
                List<string[]> kids; if (Blocks.TryGetValue(name, out kids) && IsTerminal(kids)) { Skipped++; continue; }
                Walk(name, m.Mul(t), depth + 1, layer == "0" ? blockLayer : layer);
                continue;
            }
            if (layer == "0" && blockLayer != null) layer = blockLayer;
            int n; EntPerLayer.TryGetValue(layer, out n); EntPerLayer[layer] = n + 1;
            if (OnlyLayer == "auto") { if (!(bool)Call("IsDuctLayerName", layer)) continue; Used.Add(layer); }
            else if (OnlyLayer != null) { if (!OnlyLayer.Split(';').Any(delegate(string k) { return layer.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0; })) continue; }
            else if ((bool)Call("IsIgnoredUtilityLayer", layer)) continue;
            int color; LayerColor.TryGetValue(layer, out color);

            if (e[0] == "L") AddLine(X(m, D(e[2]), D(e[3])), X(m, D(e[4]), D(e[5])), layer, color);
            else if (e[0] == "P")
            {
                string[] vs = e[3].Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                List<double[]> v = vs.Select(delegate(string s) { string[] q = s.Split(','); return new double[] { D(q[0]), D(q[1]), D(q[2]) }; }).ToList();
                if (e[2] == "1" && v.Count > 0) v.Add(new double[] { v[0][0], v[0][1], 0 });
                List<XYZ> pts = new List<XYZ>();
                for (int i = 0; i < v.Count; i++)
                {
                    pts.Add(X(m, v[i][0], v[i][1]));
                    if (i + 1 < v.Count && Math.Abs(v[i][2]) > 1e-4)
                    {
                        double bu = v[i][2], x0 = v[i][0], y0 = v[i][1], x1 = v[i + 1][0], y1 = v[i + 1][1];
                        double th = 4 * Math.Atan(bu), ch = Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
                        if (ch < 1e-6) continue;
                        double off = ch / 2 / Math.Tan(th / 2), cx = (x0 + x1) / 2 - off * (y1 - y0) / ch, cy = (y0 + y1) / 2 + off * (x1 - x0) / ch;
                        double a0 = Math.Atan2(y0 - cy, x0 - cx), rr = Math.Sqrt((x0 - cx) * (x0 - cx) + (y0 - cy) * (y0 - cy));
                        for (int k = 1; k < 8; k++) pts.Add(X(m, cx + rr * Math.Cos(a0 + th * k / 8), cy + rr * Math.Sin(a0 + th * k / 8)));
                    }
                }
                AddPolyPoints(pts, layer, color);
            }
            else if (e[0] == "A")
            {
                double cx = D(e[2]), cy = D(e[3]), r = D(e[4]), a0 = D(e[5]), a1 = D(e[6]); bool neg = e[7] == "-1";
                double span = a1 - a0; while (span <= 0) span += 2 * Math.PI;
                Func<double, XYZ> at = delegate(double t) { double a = a0 + span * t; double x = cx + r * Math.Cos(a), y = cy + r * Math.Sin(a); return X(m, neg ? -x : x, y); };
                AddArc(X(m, neg ? -cx : cx, cy), at(0), at(0.5), at(1), Enumerable.Range(0, 9).Select(delegate(int k) { return at(k / 8.0); }).ToList(), span * 180 / Math.PI, layer, color);
            }
        }
    }

    [STAThread]
    static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object s, ResolveEventArgs e) { string f = Path.Combine("C:/Program Files/Autodesk/Revit 2024", new AssemblyName(e.Name).Name + ".dll"); return File.Exists(f) ? Assembly.LoadFrom(f) : null; };
        Assembly.LoadFrom("C:/Program Files/Autodesk/Revit 2024/RevitAPI.dll");
        return Go(args);
    }

    static int Go(string[] args)
    {
        Dir = args[0];
        OnlyLayer = args.Length > 1 && args[1] != "*" ? args[1] : null;
        Svc = typeof(CadDuctSegment).Assembly.GetType("HDTool.CadToDuctService");
        TPoly = Svc.GetNestedType("RawPolyShape", BindingFlags.NonPublic);
        TLine = Svc.GetNestedType("RawLineShape", BindingFlags.NonPublic);
        TArc = Svc.GetNestedType("RawArcShape", BindingFlags.NonPublic);
        string cur = null;
        int[] aci = { 0, 0xFF0000, 0xFFFF00, 0x00FF00, 0x00FFFF, 0x0000FF, 0xFF00FF, 0xFFFFFF, 0x808080, 0xC0C0C0 };
        foreach (string raw in File.ReadAllLines(Path.Combine(Dir, "lines.txt"), Encoding.Default))
        {
            string[] p = raw.Split('|');
            if (p[0] == "Y") { int c = int.Parse(p[2]); LayerColor[p[1]] = c < 10 ? aci[c] : (c * 104729) & 0xFFFFFF; }
            else if (p[0] == "B") { cur = string.Join("|", p, 1, p.Length - 3); p = new string[] { "B", cur, p[p.Length - 2], p[p.Length - 1] }; Blocks[cur] = new List<string[]>(); Base[cur] = new double[] { D(p[2]), D(p[3]) }; }
            else if (cur != null) { if (p[0] == "I" && p.Length > 9) { var q = new List<string> { "I", p[1], string.Join("|", p, 2, p.Length - 8) }; for (int k = p.Length - 6; k < p.Length; k++) q.Add(p[k]); p = q.ToArray(); } Blocks[cur].Add(p); }
        }
        polys = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TPoly));
        lines = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TLine));
        arcs = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TArc));
        M id; id.a = 1; id.d = 1; id.b = id.c = id.tx = id.ty = 0;
        Walk("*MODEL", id, 0, null);
        Console.WriteLine("raw polys={0} lines={1} arcs={2}", polys.Count, lines.Count, arcs.Count);
        foreach (string u in Used) Console.WriteLine("AUTO layer: " + u);
        Console.WriteLine("skipped terminal symbols: " + Skipped);
        Pipeline.Execute(Dir, Svc, polys, lines, arcs, args.Length > 2 ? args[2] : "out", EntPerLayer);
        return 0;
    }
}
