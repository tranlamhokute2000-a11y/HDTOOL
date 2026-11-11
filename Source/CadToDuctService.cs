using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;

namespace HDTool
{
    /// <summary>
    /// Engine nhận diện HÌNH VẼ HÌNH HỌC (Geometric Shape Recognition) ống gió, côn thu, cút, chân rẽ
    /// từ bản vẽ CAD Link (không phụ thuộc vào Layer) và tự động dựng mô hình 3D Revit MEP.
    /// Tuân thủ 6 quy tắc chuyên môn của Sếp Hồ.
    /// </summary>
    public static class CadToDuctService
    {
        private const double MM_TO_FEET = 1.0 / 304.8;
        private const double FEET_TO_MM = 304.8;

        private static readonly string[] IgnoredLayerKeywords = new string[]
        {
            "AXIS", "GRID", "DIM", "DEFPOINTS", "TEXT", "TXT", "HATCH", "ANNO", "STAMP", "TITLE"
        };

        public static List<ImportInstance> GetCadLinksInView(Document doc, Autodesk.Revit.DB.View view)
        {
            List<ImportInstance> result = new List<ImportInstance>();
            if (doc == null) return result;

            FilteredElementCollector col = (view != null)
                ? new FilteredElementCollector(doc, view.Id)
                : new FilteredElementCollector(doc);

            foreach (Element e in col.OfClass(typeof(ImportInstance)))
            {
                ImportInstance imp = e as ImportInstance;
                if (imp != null && !imp.ViewSpecific) result.Add(imp);
            }

            if (result.Count == 0)
            {
                FilteredElementCollector allCol = new FilteredElementCollector(doc).OfClass(typeof(ImportInstance));
                foreach (Element e in allCol)
                {
                    ImportInstance imp = e as ImportInstance;
                    if (imp != null) result.Add(imp);
                }
            }

            return result;
        }

        public static string GetCadLinkDisplayName(Document doc, ImportInstance imp)
        {
            if (imp == null) return "Unknown CAD";
            if (imp.Category != null && !string.IsNullOrEmpty(imp.Category.Name)) return imp.Category.Name;
            ElementType et = doc != null ? doc.GetElement(imp.GetTypeId()) as ElementType : null;
            if (et != null && !string.IsNullOrEmpty(et.Name)) return et.Name;
            return imp.Name;
        }

        public static List<string> GetLayersFromCadLink(ImportInstance cadLink, Document doc)
        {
            HashSet<string> layers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (cadLink == null || doc == null) return new List<string>();

            Options opt = new Options();
            opt.ComputeReferences = false;
            opt.IncludeNonVisibleObjects = false;

            GeometryElement geomElem = cadLink.get_Geometry(opt);
            if (geomElem == null) return new List<string>();

            CollectLayerNamesRecursive(geomElem, doc, layers);

            List<string> sorted = layers.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            sorted.Sort(StringComparer.OrdinalIgnoreCase);
            return sorted;
        }

        private static void CollectLayerNamesRecursive(GeometryElement geomElem, Document doc, HashSet<string> layers)
        {
            if (geomElem == null) return;
            foreach (GeometryObject go in geomElem)
            {
                GeometryInstance gi = go as GeometryInstance;
                if (gi != null)
                {
                    GeometryElement sym = gi.GetSymbolGeometry();
                    if (sym != null) CollectLayerNamesRecursive(sym, doc, layers);
                }
                else
                {
                    string lay = GetLayerName(go, doc);
                    if (!string.IsNullOrEmpty(lay)) layers.Add(lay);
                }
            }
        }

        private static string GetLayerName(GeometryObject obj, Document doc)
        {
            if (obj == null || doc == null) return string.Empty;
            if (obj.GraphicsStyleId != ElementId.InvalidElementId)
            {
                GraphicsStyle gs = doc.GetElement(obj.GraphicsStyleId) as GraphicsStyle;
                if (gs != null && gs.GraphicsStyleCategory != null)
                {
                    string catName = gs.GraphicsStyleCategory.Name;
                    if (!string.IsNullOrEmpty(catName))
                    {
                        int slash = catName.IndexOf('/');
                        if (slash >= 0 && slash < catName.Length - 1) catName = catName.Substring(slash + 1);
                        return catName.Trim();
                    }
                }
            }
            return string.Empty;
        }

        // =========================================================================
        // ENGINE PHÂN TÍCH & NHẬN DIỆN HÌNH VẼ HÌNH HỌC (GEOMETRIC SHAPE RECOGNITION)
        // =========================================================================

        private class RawPolyShape
        {
            public List<XYZ> Points;
            public string LayerName;
        }

        private class RawLineShape
        {
            public Line Line;
            public string LayerName;
        }

        public static void AnalyzeCadDuctNetwork(
            Document doc,
            ImportInstance cadLink,
            double centerElevMm,
            string optionalLayerFilter,
            Outline pickBox,
            XYZ pickSeedPoint,
            double defaultHeightMm,
            out List<CadDuctSegment> outStraightDucts,
            out List<CadDuctSegment> outFittings,
            out List<CadConnectionEdge> outEdges,
            out List<CadDamperItem> outDampers)
        {
            outStraightDucts = new List<CadDuctSegment>();
            outFittings = new List<CadDuctSegment>();
            outEdges = new List<CadConnectionEdge>();
            outDampers = new List<CadDamperItem>();

            if (doc == null || cadLink == null) return;

            bool filterBySpecificLayer = !string.IsNullOrWhiteSpace(optionalLayerFilter)
                && !optionalLayerFilter.StartsWith("(");

            Options opt = new Options();
            opt.ComputeReferences = false;
            opt.IncludeNonVisibleObjects = true;

            GeometryElement rootGeom = cadLink.get_Geometry(opt);
            if (rootGeom == null) return;

            List<RawPolyShape> rawPolys = new List<RawPolyShape>();
            List<RawLineShape> rawLines = new List<RawLineShape>();

            Outline wideBox = null;
            if (pickBox != null)
            {
                double pad = 10000.0 * MM_TO_FEET;
                wideBox = new Outline(
                    new XYZ(pickBox.MinimumPoint.X - pad, pickBox.MinimumPoint.Y - pad, -1e6),
                    new XYZ(pickBox.MaximumPoint.X + pad, pickBox.MaximumPoint.Y + pad, 1e6));
            }
            else if (pickSeedPoint != null)
            {
                double pad = 80000.0 * MM_TO_FEET;
                wideBox = new Outline(
                    new XYZ(pickSeedPoint.X - pad, pickSeedPoint.Y - pad, -1e6),
                    new XYZ(pickSeedPoint.X + pad, pickSeedPoint.Y + pad, 1e6));
            }

            CollectGeometryRecursive(rootGeom, Transform.Identity, doc, filterBySpecificLayer ? optionalLayerFilter : null, rawPolys, rawLines);

            // Tái tạo các đa giác kín từ các đường Line nối nhau nếu bản vẽ dùng Line thay vì PolyLine
            ReconstructClosedLoopsFromLines(rawLines, rawPolys);

            List<CadDuctSegment> allRectangles = new List<CadDuctSegment>();
            List<CadDuctSegment> allFittings = new List<CadDuctSegment>();

            foreach (RawPolyShape rp in rawPolys)
            {
                if (wideBox != null && !PolyIntersectsOutlineXY(rp.Points, wideBox)) continue;

                CadDuctSegment classified = ClassifyPolygonShape(rp.Points, rp.LayerName, centerElevMm);
                if (classified == null) continue;

                if (classified.ShapeKind == CadShapeKind.StraightDuct)
                {
                    allRectangles.Add(classified);
                }
                else
                {
                    allFittings.Add(classified);
                }
            }

            // Fallback cho cặp nét song song rời
            if (rawLines.Count > 0)
            {
                List<RawLineShape> filteredLines = rawLines;
                if (wideBox != null)
                {
                    filteredLines = rawLines.Where(delegate(RawLineShape rl)
                    {
                        XYZ mid = (rl.Line.GetEndPoint(0) + rl.Line.GetEndPoint(1)) * 0.5;
                        return PointInOutlineXY(mid, wideBox);
                    }).ToList();
                }

                List<CadDuctSegment> linePairDucts = MatchParallelLinesIntoDucts(filteredLines, centerElevMm);
                foreach (CadDuctSegment ld in linePairDucts)
                {
                    if (!IsDuplicateSegment(ld, allRectangles))
                    {
                        allRectangles.Add(ld);
                    }
                }
            }

            if (allRectangles.Count == 0) return;

            // Xây dựng Topology kết nối
            List<CadConnectionEdge> allEdges = BuildGeometricTopology(allRectangles, allFittings);

            List<CadDuctSegment> selectedDucts;
            List<CadDuctSegment> selectedFittings;
            List<CadConnectionEdge> selectedEdges;

            if (pickSeedPoint != null)
            {
                ExtractConnectedChainFromPickPoint(
                    pickSeedPoint,
                    allRectangles,
                    allFittings,
                    allEdges,
                    out selectedDucts,
                    out selectedFittings,
                    out selectedEdges);
            }
            else if (pickBox != null)
            {
                selectedDucts = allRectangles.Where(delegate(CadDuctSegment d)
                {
                    return PointInOutlineXY(d.CenterPoint, pickBox)
                        || PointInOutlineXY(d.StartPoint, pickBox)
                        || PointInOutlineXY(d.EndPoint, pickBox)
                        || d.ContainsPointXY((pickBox.MinimumPoint + pickBox.MaximumPoint) * 0.5, 50.0 * MM_TO_FEET);
                }).ToList();

                HashSet<string> keepIds = new HashSet<string>(selectedDucts.Select(x => x.Id));
                selectedEdges = allEdges.Where(e => keepIds.Contains(e.SegmentA.Id) && keepIds.Contains(e.SegmentB.Id)).ToList();
                selectedFittings = selectedEdges.Where(e => e.BridgePolygon != null).Select(e => e.BridgePolygon).Distinct().ToList();
            }
            else
            {
                selectedDucts = allRectangles;
                selectedFittings = allFittings;
                selectedEdges = allEdges;
            }

            foreach (CadDuctSegment d in selectedDucts)
            {
                d.ConnectedNeighbors.Clear();
                d.Connections.Clear();
            }
            foreach (CadConnectionEdge edge in selectedEdges)
            {
                edge.SegmentA.Connections.Add(edge);
                edge.SegmentB.Connections.Add(edge);
                if (!edge.SegmentA.ConnectedNeighbors.Contains(edge.SegmentB)) edge.SegmentA.ConnectedNeighbors.Add(edge.SegmentB);
                if (!edge.SegmentB.ConnectedNeighbors.Contains(edge.SegmentA)) edge.SegmentB.ConnectedNeighbors.Add(edge.SegmentA);
            }

            List<CadTextNote> textNotes;
            List<CadDamperItem> dampers;
            CadDwgExtractor.ExtractTextAndDampersFromDwg(doc, cadLink, out textNotes, out dampers);

            MatchAndPropagateDuctSizes(selectedDucts, textNotes, defaultHeightMm);

            if (dampers != null && dampers.Count > 0)
            {
                double maxDampDist = 500.0 * MM_TO_FEET;
                foreach (CadDamperItem dp in dampers)
                {
                    foreach (CadDuctSegment d in selectedDucts)
                    {
                        if (d.DistanceToPointXY(dp.Position) <= maxDampDist)
                        {
                            dp.WidthMm = d.WidthMm;
                            dp.HeightMm = d.HeightMm;
                            dp.Direction = d.Direction;
                            outDampers.Add(dp);
                            break;
                        }
                    }
                }
            }

            outStraightDucts = selectedDucts;
            outFittings = selectedFittings;
            outEdges = selectedEdges;
        }

        private static void CollectGeometryRecursive(
            GeometryElement geomElem,
            Transform currentTransform,
            Document doc,
            string requiredLayer,
            List<RawPolyShape> outPolys,
            List<RawLineShape> outLines)
        {
            if (geomElem == null) return;

            foreach (GeometryObject obj in geomElem)
            {
                GeometryInstance gInst = obj as GeometryInstance;
                if (gInst != null)
                {
                    Transform nextTransform = currentTransform.Multiply(gInst.Transform);
                    GeometryElement symGeom = gInst.GetSymbolGeometry();
                    if (symGeom != null)
                    {
                        CollectGeometryRecursive(symGeom, nextTransform, doc, requiredLayer, outPolys, outLines);
                    }
                    continue;
                }

                string lay = GetLayerName(obj, doc);
                if (!string.IsNullOrEmpty(requiredLayer))
                {
                    if (!string.Equals(lay, requiredLayer.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                }
                else if (IsIgnoredUtilityLayer(lay))
                {
                    continue;
                }

                PolyLine poly = obj as PolyLine;
                if (poly != null)
                {
                    IList<XYZ> rawPts = poly.GetCoordinates();
                    if (rawPts != null && rawPts.Count >= 2)
                    {
                        List<XYZ> wcsPts = new List<XYZ>(rawPts.Count);
                        for (int i = 0; i < rawPts.Count; i++)
                        {
                            XYZ p = currentTransform.OfPoint(rawPts[i]);
                            wcsPts.Add(new XYZ(p.X, p.Y, 0.0));
                        }

                        if (wcsPts.Count >= 4 && wcsPts[0].DistanceTo(wcsPts[wcsPts.Count - 1]) < 10.0 * MM_TO_FEET)
                        {
                            RawPolyShape rps = new RawPolyShape();
                            rps.Points = wcsPts;
                            rps.LayerName = lay;
                            outPolys.Add(rps);
                        }
                        else
                        {
                            for (int i = 0; i < wcsPts.Count - 1; i++)
                            {
                                if (wcsPts[i].DistanceTo(wcsPts[i + 1]) > 50.0 * MM_TO_FEET)
                                {
                                    try
                                    {
                                        RawLineShape rls = new RawLineShape();
                                        rls.Line = Line.CreateBound(wcsPts[i], wcsPts[i + 1]);
                                        rls.LayerName = lay;
                                        outLines.Add(rls);
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                    continue;
                }

                Line line = obj as Line;
                if (line != null && line.Length > 50.0 * MM_TO_FEET)
                {
                    try
                    {
                        XYZ p0 = currentTransform.OfPoint(line.GetEndPoint(0));
                        XYZ p1 = currentTransform.OfPoint(line.GetEndPoint(1));
                        p0 = new XYZ(p0.X, p0.Y, 0.0);
                        p1 = new XYZ(p1.X, p1.Y, 0.0);
                        if (p0.DistanceTo(p1) > 50.0 * MM_TO_FEET)
                        {
                            RawLineShape rls = new RawLineShape();
                            rls.Line = Line.CreateBound(p0, p1);
                            rls.LayerName = lay;
                            outLines.Add(rls);
                        }
                    }
                    catch { }
                }
            }
        }

        private static void ReconstructClosedLoopsFromLines(List<RawLineShape> lines, List<RawPolyShape> outPolys)
        {
            if (lines == null || lines.Count < 4) return;
            double tol = 6.0 * MM_TO_FEET;
            int n = lines.Count;
            bool[] used = new bool[n];

            // Tái tạo các vòng lặp 4 cạnh (Rectangle / Trapezoid)
            for (int i = 0; i < n; i++)
            {
                if (used[i]) continue;
                Line l0 = lines[i].Line;
                XYZ curEnd = l0.GetEndPoint(1);

                for (int j = 0; j < n; j++)
                {
                    if (j == i || used[j]) continue;
                    Line l1 = lines[j].Line;
                    XYZ nextEnd1 = null;
                    if (l1.GetEndPoint(0).DistanceTo(curEnd) < tol) nextEnd1 = l1.GetEndPoint(1);
                    else if (l1.GetEndPoint(1).DistanceTo(curEnd) < tol) nextEnd1 = l1.GetEndPoint(0);
                    if (nextEnd1 == null) continue;

                    for (int k = 0; k < n; k++)
                    {
                        if (k == i || k == j || used[k]) continue;
                        Line l2 = lines[k].Line;
                        XYZ nextEnd2 = null;
                        if (l2.GetEndPoint(0).DistanceTo(nextEnd1) < tol) nextEnd2 = l2.GetEndPoint(1);
                        else if (l2.GetEndPoint(1).DistanceTo(nextEnd1) < tol) nextEnd2 = l2.GetEndPoint(0);
                        if (nextEnd2 == null) continue;

                        for (int m = 0; m < n; m++)
                        {
                            if (m == i || m == j || m == k || used[m]) continue;
                            Line l3 = lines[m].Line;
                            XYZ finalEnd = null;
                            if (l3.GetEndPoint(0).DistanceTo(nextEnd2) < tol) finalEnd = l3.GetEndPoint(1);
                            else if (l3.GetEndPoint(1).DistanceTo(nextEnd2) < tol) finalEnd = l3.GetEndPoint(0);
                            if (finalEnd == null) continue;

                            if (finalEnd.DistanceTo(l0.GetEndPoint(0)) < tol)
                            {
                                // Tìm thấy vòng kín 4 cạnh!
                                used[i] = true; used[j] = true; used[k] = true; used[m] = true;
                                RawPolyShape rps = new RawPolyShape();
                                rps.Points = new List<XYZ>() { l0.GetEndPoint(0), curEnd, nextEnd1, nextEnd2, l0.GetEndPoint(0) };
                                rps.LayerName = lines[i].LayerName;
                                outPolys.Add(rps);
                                break;
                            }
                        }
                        if (used[i]) break;
                    }
                    if (used[i]) break;
                }
            }
        }

        private static bool IsIgnoredUtilityLayer(string lay)
        {
            if (string.IsNullOrEmpty(lay)) return false;
            string upper = lay.ToUpperInvariant();
            foreach (string kw in IgnoredLayerKeywords)
            {
                if (upper.Contains(kw)) return true;
            }
            return false;
        }

        private static CadDuctSegment ClassifyPolygonShape(List<XYZ> rawPts, string layerName, double centerElevMm)
        {
            List<XYZ> verts = SimplifyPolygonVertices(rawPts);
            if (verts.Count < 4) return null;

            double zFeet = centerElevMm * MM_TO_FEET;

            if (verts.Count == 4)
            {
                XYZ p0 = verts[0], p1 = verts[1], p2 = verts[2], p3 = verts[3];
                XYZ e0 = p1 - p0, e1 = p2 - p1, e2 = p3 - p2, e3 = p0 - p3;
                double l0 = e0.GetLength() * FEET_TO_MM;
                double l1 = e1.GetLength() * FEET_TO_MM;
                double l2 = e2.GetLength() * FEET_TO_MM;
                double l3 = e3.GetLength() * FEET_TO_MM;

                if (l0 < 50 || l1 < 50 || l2 < 50 || l3 < 50) return null;

                XYZ u0 = e0.Normalize(), u1 = e1.Normalize(), u2 = e2.Normalize(), u3 = e3.Normalize();

                // 1. KIỂM TRA HÌNH CHỮ NHẬT (RECTANGLE -> STRAIGHT DUCT)
                bool adjPerp = Math.Abs(u0.DotProduct(u1)) < 0.08
                            && Math.Abs(u1.DotProduct(u2)) < 0.08
                            && Math.Abs(u2.DotProduct(u3)) < 0.08;
                bool oppEqual = Math.Abs(l0 - l2) <= Math.Max(12.0, 0.04 * Math.Max(l0, l2))
                             && Math.Abs(l1 - l3) <= Math.Max(12.0, 0.04 * Math.Max(l1, l3));

                if (adjPerp && oppEqual)
                {
                    double wAvg02 = (l0 + l2) * 0.5;
                    double wAvg13 = (l1 + l3) * 0.5;

                    double minSide = Math.Min(wAvg02, wAvg13);
                    double maxSide = Math.Max(wAvg02, wAvg13);

                    if (minSide < 100.0 || minSide > 3500.0 || maxSide < 120.0) return null;

                    if (Math.Abs(maxSide - minSide) < 15.0 && maxSide < 450.0)
                    {
                        string upperLay = (layerName ?? "").ToUpperInvariant();
                        if (upperLay.Contains("GRILLE") || upperLay.Contains("BOX")) return null;
                    }

                    XYZ startMid, endMid;
                    double widthMm;

                    if (wAvg02 <= wAvg13)
                    {
                        startMid = (p0 + p1) * 0.5;
                        endMid = (p2 + p3) * 0.5;
                        widthMm = wAvg02;
                    }
                    else
                    {
                        startMid = (p1 + p2) * 0.5;
                        endMid = (p3 + p0) * 0.5;
                        widthMm = wAvg13;
                    }

                    widthMm = SnapStandardDuctWidthMm(widthMm);

                    CadDuctSegment duct = new CadDuctSegment();
                    duct.ShapeKind = CadShapeKind.StraightDuct;
                    duct.StartPoint = new XYZ(startMid.X, startMid.Y, zFeet);
                    duct.EndPoint = new XYZ(endMid.X, endMid.Y, zFeet);
                    duct.CenterPoint = (duct.StartPoint + duct.EndPoint) * 0.5;
                    XYZ diff = duct.EndPoint - duct.StartPoint;
                    if (diff.GetLength() < 1e-5) return null;
                    duct.Direction = diff.Normalize();
                    duct.Length = diff.GetLength() * FEET_TO_MM;
                    duct.WidthMm = widthMm;
                    duct.Width2Mm = widthMm;
                    duct.CenterElevationMm = centerElevMm;
                    duct.LayerName = layerName;
                    duct.PolygonVertices = verts;
                    return duct;
                }

                // 2. KIỂM TRA HÌNH THANG (TRAPEZOID -> CÔN THU TRANSITION HOẶC CHÂN RẼ TAKEOFF)
                bool pair02Parallel = Math.Abs(u0.DotProduct(u2)) > 0.96;
                bool pair13Parallel = Math.Abs(u1.DotProduct(u3)) > 0.96;

                if (pair02Parallel ^ pair13Parallel)
                {
                    XYZ a1, a2, b1, b2;
                    double wa, wb;
                    XYZ baseDir;

                    if (pair02Parallel)
                    {
                        a1 = p0; a2 = p1; wa = l0;
                        b1 = p2; b2 = p3; wb = l2;
                        baseDir = u0;
                    }
                    else
                    {
                        a1 = p1; a2 = p2; wa = l1;
                        b1 = p3; b2 = p0; wb = l3;
                        baseDir = u1;
                    }

                    if (wa >= 100 && wb >= 100 && Math.Max(wa, wb) <= 3500 && Math.Abs(wa - wb) >= 15.0)
                    {
                        XYZ midA = (a1 + a2) * 0.5;
                        XYZ midB = (b1 + b2) * 0.5;
                        double heightMm = LineToLineDistance(a1, baseDir, b1, baseDir) * FEET_TO_MM;

                        if (heightMm >= 50.0 && heightMm <= 2500.0)
                        {
                            CadDuctSegment trap = new CadDuctSegment();
                            trap.ShapeKind = CadShapeKind.Transition;
                            if (wa >= wb)
                            {
                                trap.StartPoint = new XYZ(midA.X, midA.Y, zFeet);
                                trap.EndPoint = new XYZ(midB.X, midB.Y, zFeet);
                                trap.WidthMm = SnapStandardDuctWidthMm(wa);
                                trap.Width2Mm = SnapStandardDuctWidthMm(wb);
                            }
                            else
                            {
                                trap.StartPoint = new XYZ(midB.X, midB.Y, zFeet);
                                trap.EndPoint = new XYZ(midA.X, midA.Y, zFeet);
                                trap.WidthMm = SnapStandardDuctWidthMm(wb);
                                trap.Width2Mm = SnapStandardDuctWidthMm(wa);
                            }
                            trap.CenterPoint = (trap.StartPoint + trap.EndPoint) * 0.5;
                            XYZ dir = trap.EndPoint - trap.StartPoint;
                            trap.Direction = dir.GetLength() > 1e-5 ? dir.Normalize() : XYZ.BasisX;
                            trap.Length = dir.GetLength() * FEET_TO_MM;
                            trap.CenterElevationMm = centerElevMm;
                            trap.LayerName = layerName;
                            trap.PolygonVertices = verts;
                            return trap;
                        }
                    }
                }

                // 3. KIỂM TRA CÚT 4 ĐỈNH VÁT CHÉO (MITERED ELBOW)
                for (int i = 0; i < 4; i++)
                {
                    int j = (i + 2) % 4;
                    XYZ vi = verts[(i + 1) % 4] - verts[i];
                    XYZ vj = verts[(j + 1) % 4] - verts[j];
                    double lenI = vi.GetLength() * FEET_TO_MM;
                    double lenJ = vj.GetLength() * FEET_TO_MM;
                    if (lenI >= 100 && lenI <= 3500 && Math.Abs(lenI - lenJ) <= 30.0)
                    {
                        double dot = Math.Abs(vi.Normalize().DotProduct(vj.Normalize()));
                        if (dot < 0.75)
                        {
                            XYZ midI = (verts[i] + verts[(i + 1) % 4]) * 0.5;
                            XYZ midJ = (verts[j] + verts[(j + 1) % 4]) * 0.5;
                            CadDuctSegment elb = new CadDuctSegment();
                            elb.ShapeKind = CadShapeKind.Elbow;
                            elb.StartPoint = new XYZ(midI.X, midI.Y, zFeet);
                            elb.EndPoint = new XYZ(midJ.X, midJ.Y, zFeet);
                            elb.CenterPoint = ComputeCentroidXY(verts, zFeet);
                            elb.WidthMm = SnapStandardDuctWidthMm((lenI + lenJ) * 0.5);
                            elb.Width2Mm = elb.WidthMm;
                            elb.CenterElevationMm = centerElevMm;
                            elb.LayerName = layerName;
                            elb.PolygonVertices = verts;
                            return elb;
                        }
                    }
                }
            }
            else if (verts.Count >= 5 && verts.Count <= 24)
            {
                BoundingBoxXYZ bb = ComputeBoundingBoxXY(verts);
                double spanX = (bb.Max.X - bb.Min.X) * FEET_TO_MM;
                double spanY = (bb.Max.Y - bb.Min.Y) * FEET_TO_MM;
                if (spanX >= 100 && spanY >= 100 && spanX <= 6000 && spanY <= 6000)
                {
                    CadDuctSegment tee = new CadDuctSegment();
                    tee.ShapeKind = CadShapeKind.TeeBranch;
                    tee.CenterPoint = ComputeCentroidXY(verts, zFeet);
                    tee.StartPoint = tee.CenterPoint;
                    tee.EndPoint = tee.CenterPoint;
                    tee.CenterElevationMm = centerElevMm;
                    tee.LayerName = layerName;
                    tee.PolygonVertices = verts;
                    return tee;
                }
            }

            return null;
        }

        private static List<XYZ> SimplifyPolygonVertices(List<XYZ> rawPts)
        {
            List<XYZ> clean = new List<XYZ>();
            double mergeTol = 2.0 * MM_TO_FEET;

            foreach (XYZ p in rawPts)
            {
                if (clean.Count == 0 || clean[clean.Count - 1].DistanceTo(p) > mergeTol)
                {
                    clean.Add(p);
                }
            }

            if (clean.Count > 1 && clean[0].DistanceTo(clean[clean.Count - 1]) <= mergeTol)
            {
                clean.RemoveAt(clean.Count - 1);
            }

            if (clean.Count < 4) return clean;

            List<XYZ> result = new List<XYZ>();
            int n = clean.Count;
            for (int i = 0; i < n; i++)
            {
                XYZ prev = clean[(i - 1 + n) % n];
                XYZ curr = clean[i];
                XYZ next = clean[(i + 1) % n];
                XYZ v1 = (curr - prev);
                XYZ v2 = (next - curr);
                if (v1.GetLength() < mergeTol || v2.GetLength() < mergeTol) continue;
                double dot = v1.Normalize().DotProduct(v2.Normalize());
                if (dot < 0.998)
                {
                    result.Add(curr);
                }
            }

            return result;
        }

        private static double SnapStandardDuctWidthMm(double rawMm)
        {
            double round25 = Math.Round(rawMm / 25.0) * 25.0;
            if (Math.Abs(rawMm - round25) <= 6.0) return round25;
            return Math.Round(rawMm);
        }

        private static List<CadDuctSegment> MatchParallelLinesIntoDucts(List<RawLineShape> lines, double centerElevMm)
        {
            List<CadDuctSegment> ducts = new List<CadDuctSegment>();
            int n = lines.Count;
            bool[] used = new bool[n];

            double minGapFeet = 100.0 * MM_TO_FEET;
            double maxGapFeet = 3500.0 * MM_TO_FEET;
            double zFeet = centerElevMm * MM_TO_FEET;

            for (int i = 0; i < n; i++)
            {
                if (used[i]) continue;
                Line l1 = lines[i].Line;
                XYZ p1Start = l1.GetEndPoint(0);
                XYZ p1End = l1.GetEndPoint(1);
                XYZ dir1 = (p1End - p1Start).Normalize();

                int bestPair = -1;
                double bestDist = double.MaxValue;
                double bestOvlStart = 0, bestOvlEnd = 0;

                for (int j = i + 1; j < n; j++)
                {
                    if (used[j]) continue;
                    Line l2 = lines[j].Line;
                    XYZ p2Start = l2.GetEndPoint(0);
                    XYZ p2End = l2.GetEndPoint(1);
                    XYZ dir2 = (p2End - p2Start).Normalize();

                    double dot = Math.Abs(dir1.DotProduct(dir2));
                    if (dot < 0.985) continue;

                    double dist = LineToLineDistance(p1Start, dir1, p2Start, dir2);
                    if (dist >= minGapFeet && dist <= maxGapFeet)
                    {
                        double a1 = p1Start.DotProduct(dir1);
                        double a2 = p1End.DotProduct(dir1);
                        double b1 = p2Start.DotProduct(dir1);
                        double b2 = p2End.DotProduct(dir1);

                        double minA = Math.Min(a1, a2), maxA = Math.Max(a1, a2);
                        double minB = Math.Min(b1, b2), maxB = Math.Max(b1, b2);
                        double ovlStart = Math.Max(minA, minB);
                        double ovlEnd = Math.Min(maxA, maxB);
                        double overlap = ovlEnd - ovlStart;

                        if (overlap > 120.0 * MM_TO_FEET && dist < bestDist)
                        {
                            bestDist = dist;
                            bestPair = j;
                            bestOvlStart = ovlStart;
                            bestOvlEnd = ovlEnd;
                        }
                    }
                }

                if (bestPair != -1)
                {
                    used[i] = true;
                    used[bestPair] = true;

                    Line l2 = lines[bestPair].Line;
                    XYZ p2Start = l2.GetEndPoint(0);

                    XYZ perp = new XYZ(-dir1.Y, dir1.X, 0.0).Normalize();
                    double perp1 = p1Start.DotProduct(perp);
                    double perp2 = p2Start.DotProduct(perp);
                    double perpMid = (perp1 + perp2) * 0.5;

                    XYZ midStart = dir1 * bestOvlStart + perp * perpMid;
                    XYZ midEnd = dir1 * bestOvlEnd + perp * perpMid;

                    midStart = new XYZ(midStart.X, midStart.Y, zFeet);
                    midEnd = new XYZ(midEnd.X, midEnd.Y, zFeet);

                    double widthMm = SnapStandardDuctWidthMm(bestDist * FEET_TO_MM);
                    if (widthMm < 100) widthMm = 100;

                    CadDuctSegment seg = new CadDuctSegment();
                    seg.ShapeKind = CadShapeKind.StraightDuct;
                    seg.StartPoint = midStart;
                    seg.EndPoint = midEnd;
                    seg.CenterPoint = (midStart + midEnd) * 0.5;
                    seg.Direction = (midEnd - midStart).Normalize();
                    seg.Length = midStart.DistanceTo(midEnd) * FEET_TO_MM;
                    seg.WidthMm = widthMm;
                    seg.Width2Mm = widthMm;
                    seg.CenterElevationMm = centerElevMm;
                    seg.LayerName = lines[i].LayerName;

                    if (seg.Length > 100.0)
                    {
                        ducts.Add(seg);
                    }
                }
            }

            return ducts;
        }

        private static bool IsDuplicateSegment(CadDuctSegment candidate, List<CadDuctSegment> existing)
        {
            double centerTol = 50.0 * MM_TO_FEET;
            foreach (CadDuctSegment e in existing)
            {
                if (e.CenterPoint.DistanceTo(candidate.CenterPoint) < centerTol
                    && Math.Abs(e.WidthMm - candidate.WidthMm) < 50.0)
                {
                    return true;
                }
            }
            return false;
        }

        private static List<CadConnectionEdge> BuildGeometricTopology(
            List<CadDuctSegment> ducts,
            List<CadDuctSegment> fittings)
        {
            List<CadConnectionEdge> edges = new List<CadConnectionEdge>();
            HashSet<string> addedPairs = new HashSet<string>();

            double touchTolFeet = 90.0 * MM_TO_FEET;

            foreach (CadDuctSegment fit in fittings)
            {
                if (fit.ShapeKind == CadShapeKind.Transition)
                {
                    CadDuctSegment mainSideDuct = null;
                    CadDuctSegment smallEndDuct = null;
                    CadDuctSegment largeEndDuct = null;

                    foreach (CadDuctSegment d in ducts)
                    {
                        if (d.StartPoint.DistanceTo(fit.EndPoint) <= touchTolFeet || d.EndPoint.DistanceTo(fit.EndPoint) <= touchTolFeet)
                        {
                            if (Math.Abs(d.WidthMm - fit.Width2Mm) <= 60.0)
                            {
                                smallEndDuct = d;
                            }
                        }

                        if (d.StartPoint.DistanceTo(fit.StartPoint) <= touchTolFeet || d.EndPoint.DistanceTo(fit.StartPoint) <= touchTolFeet)
                        {
                            if (Math.Abs(d.WidthMm - fit.WidthMm) <= 60.0)
                            {
                                largeEndDuct = d;
                            }
                        }

                        double distToAxis = d.DistanceToPointXY(fit.StartPoint);
                        double halfMainW = d.WidthMm * 0.5 * MM_TO_FEET;
                        if (Math.Abs(distToAxis - halfMainW) <= touchTolFeet
                            && d.WidthMm >= fit.Width2Mm + 20.0
                            && Math.Abs(d.Direction.DotProduct(fit.Direction)) < 0.25)
                        {
                            mainSideDuct = d;
                        }
                    }

                    if (mainSideDuct != null && smallEndDuct != null && mainSideDuct != smallEndDuct)
                    {
                        fit.ShapeKind = CadShapeKind.TakeoffTap;
                        XYZ tapPt = ProjectPointOnSegmentXY(smallEndDuct.CenterPoint, mainSideDuct.StartPoint, mainSideDuct.EndPoint, mainSideDuct.CenterElevationMm * MM_TO_FEET);
                        AddEdgeUnique(edges, addedPairs, mainSideDuct, smallEndDuct, CadConnectionKind.TakeoffTap, fit, tapPt);
                        continue;
                    }

                    if (largeEndDuct != null && smallEndDuct != null && largeEndDuct != smallEndDuct)
                    {
                        AddEdgeUnique(edges, addedPairs, largeEndDuct, smallEndDuct, CadConnectionKind.Transition, fit, fit.CenterPoint);
                        continue;
                    }
                }
                else if (fit.ShapeKind == CadShapeKind.Elbow)
                {
                    List<CadDuctSegment> touching = FindDuctsTouchingPolygon(fit, ducts, touchTolFeet);
                    if (touching.Count >= 2)
                    {
                        for (int i = 0; i < touching.Count; i++)
                        {
                            for (int j = i + 1; j < touching.Count; j++)
                            {
                                CadDuctSegment d1 = touching[i];
                                CadDuctSegment d2 = touching[j];
                                if (Math.Abs(d1.Direction.DotProduct(d2.Direction)) < 0.85)
                                {
                                    XYZ corner = LineLineIntersectXY(d1.StartPoint, d1.Direction, d2.StartPoint, d2.Direction, d1.StartPoint.Z);
                                    AddEdgeUnique(edges, addedPairs, d1, d2, CadConnectionKind.Elbow, fit, corner);
                                }
                            }
                        }
                    }
                }
                else if (fit.ShapeKind == CadShapeKind.TeeBranch)
                {
                    List<CadDuctSegment> touching = FindDuctsTouchingPolygon(fit, ducts, touchTolFeet);
                    if (touching.Count >= 2)
                    {
                        touching.Sort((a, b) => b.WidthMm.CompareTo(a.WidthMm));
                        CadDuctSegment mainDuct = touching[0];
                        for (int i = 1; i < touching.Count; i++)
                        {
                            CadDuctSegment br = touching[i];
                            XYZ junc = LineLineIntersectXY(mainDuct.StartPoint, mainDuct.Direction, br.StartPoint, br.Direction, mainDuct.StartPoint.Z);
                            if (junc == null) junc = fit.CenterPoint;
                            AddEdgeUnique(edges, addedPairs, mainDuct, br, CadConnectionKind.TeeBranch, fit, junc);
                        }
                    }
                }
            }

            double directEndTolFeet = 150.0 * MM_TO_FEET;
            double elbowGapTolFeet = 600.0 * MM_TO_FEET;

            for (int i = 0; i < ducts.Count; i++)
            {
                for (int j = i + 1; j < ducts.Count; j++)
                {
                    CadDuctSegment d1 = ducts[i];
                    CadDuctSegment d2 = ducts[j];

                    string pairKey = MakePairKey(d1.Id, d2.Id);
                    if (addedPairs.Contains(pairKey)) continue;

                    double dot = Math.Abs(d1.Direction.DotProduct(d2.Direction));
                    double minEndDist = MinEndpointDistanceXY(d1, d2);

                    if (dot > 0.96)
                    {
                        double perpGap = LineToLineDistance(d1.StartPoint, d1.Direction, d2.StartPoint, d2.Direction);
                        if (minEndDist <= directEndTolFeet && perpGap <= 100.0 * MM_TO_FEET)
                        {
                            if (Math.Abs(d1.WidthMm - d2.WidthMm) >= 15.0)
                            {
                                AddEdgeUnique(edges, addedPairs, d1, d2, CadConnectionKind.Transition, null, GetClosestEndpointsMid(d1, d2));
                            }
                            else
                            {
                                AddEdgeUnique(edges, addedPairs, d1, d2, CadConnectionKind.CollinearDirect, null, GetClosestEndpointsMid(d1, d2));
                            }
                        }
                    }
                    else if (dot < 0.25)
                    {
                        XYZ inter = LineLineIntersectXY(d1.StartPoint, d1.Direction, d2.StartPoint, d2.Direction, d1.StartPoint.Z);
                        if (inter == null) continue;

                        double d1DistToBody = d1.DistanceToPointXY(inter);
                        double d2DistToBody = d2.DistanceToPointXY(inter);

                        double minD2ToInter = Math.Min(d2.StartPoint.DistanceTo(inter), d2.EndPoint.DistanceTo(inter));
                        double minD1ToInter = Math.Min(d1.StartPoint.DistanceTo(inter), d1.EndPoint.DistanceTo(inter));

                        if (d1DistToBody < 50.0 * MM_TO_FEET && d1.WidthMm > d2.WidthMm + 20.0
                            && minD2ToInter <= (d1.WidthMm * 0.5 + 350.0) * MM_TO_FEET
                            && d1.StartPoint.DistanceTo(inter) > 120.0 * MM_TO_FEET
                            && d1.EndPoint.DistanceTo(inter) > 120.0 * MM_TO_FEET)
                        {
                            AddEdgeUnique(edges, addedPairs, d1, d2, CadConnectionKind.TakeoffTap, null, inter);
                        }
                        else if (d2DistToBody < 50.0 * MM_TO_FEET && d2.WidthMm > d1.WidthMm + 20.0
                            && minD1ToInter <= (d2.WidthMm * 0.5 + 350.0) * MM_TO_FEET
                            && d2.StartPoint.DistanceTo(inter) > 120.0 * MM_TO_FEET
                            && d2.EndPoint.DistanceTo(inter) > 120.0 * MM_TO_FEET)
                        {
                            AddEdgeUnique(edges, addedPairs, d2, d1, CadConnectionKind.TakeoffTap, null, inter);
                        }
                        else if (minD1ToInter <= elbowGapTolFeet && minD2ToInter <= elbowGapTolFeet)
                        {
                            AddEdgeUnique(edges, addedPairs, d1, d2, CadConnectionKind.Elbow, null, inter);
                        }
                    }
                }
            }

            return edges;
        }

        private static List<CadDuctSegment> FindDuctsTouchingPolygon(CadDuctSegment fit, List<CadDuctSegment> ducts, double tolFeet)
        {
            List<CadDuctSegment> res = new List<CadDuctSegment>();
            if (fit.PolygonVertices == null || fit.PolygonVertices.Count < 3) return res;

            foreach (CadDuctSegment d in ducts)
            {
                bool sInside = fit.ContainsPointXY(d.StartPoint, tolFeet);
                bool eInside = fit.ContainsPointXY(d.EndPoint, tolFeet);
                if (sInside || eInside)
                {
                    res.Add(d);
                }
            }
            return res;
        }

        private static void ExtractConnectedChainFromPickPoint(
            XYZ pickPt,
            List<CadDuctSegment> allDucts,
            List<CadDuctSegment> allFittings,
            List<CadConnectionEdge> allEdges,
            out List<CadDuctSegment> chainDucts,
            out List<CadDuctSegment> chainFittings,
            out List<CadConnectionEdge> chainEdges)
        {
            chainDucts = new List<CadDuctSegment>();
            chainFittings = new List<CadDuctSegment>();
            chainEdges = new List<CadConnectionEdge>();

            CadDuctSegment seed = null;
            double bestDist = double.MaxValue;
            double maxPickDist = 1500.0 * MM_TO_FEET;

            foreach (CadDuctSegment d in allDucts)
            {
                if (d.ContainsPointXY(pickPt, 30.0 * MM_TO_FEET))
                {
                    seed = d;
                    break;
                }
                double dist = d.DistanceToPointXY(pickPt);
                if (dist < bestDist && dist <= maxPickDist)
                {
                    bestDist = dist;
                    seed = d;
                }
            }

            if (seed == null) return;

            HashSet<string> visitedIds = new HashSet<string>();
            Queue<CadDuctSegment> q = new Queue<CadDuctSegment>();
            visitedIds.Add(seed.Id);
            q.Enqueue(seed);
            chainDucts.Add(seed);

            while (q.Count > 0)
            {
                CadDuctSegment cur = q.Dequeue();
                foreach (CadConnectionEdge edge in allEdges)
                {
                    CadDuctSegment next = null;
                    if (edge.SegmentA == cur) next = edge.SegmentB;
                    else if (edge.SegmentB == cur) next = edge.SegmentA;

                    if (next != null && !visitedIds.Contains(next.Id))
                    {
                        visitedIds.Add(next.Id);
                        q.Enqueue(next);
                        chainDucts.Add(next);
                    }
                }
            }

            foreach (CadConnectionEdge edge in allEdges)
            {
                if (visitedIds.Contains(edge.SegmentA.Id) && visitedIds.Contains(edge.SegmentB.Id))
                {
                    chainEdges.Add(edge);
                    if (edge.BridgePolygon != null && !chainFittings.Contains(edge.BridgePolygon))
                    {
                        chainFittings.Add(edge.BridgePolygon);
                    }
                }
            }
        }

        public static void MatchAndPropagateDuctSizes(List<CadDuctSegment> ducts, List<CadTextNote> textNotes, double defaultHeightMm = 300)
        {
            if (ducts == null || ducts.Count == 0) return;

            double maxMatchRadiusFeet = 1800.0 * MM_TO_FEET;

            foreach (CadDuctSegment duct in ducts)
            {
                CadTextNote bestText = null;
                double bestScore = double.MaxValue;

                if (textNotes != null)
                {
                    foreach (CadTextNote note in textNotes)
                    {
                        double distToAxis = duct.DistanceToPointXY(note.Position);
                        bool insidePoly = duct.ContainsPointXY(note.Position, 150.0 * MM_TO_FEET);
                        if (distToAxis > maxMatchRadiusFeet && !insidePoly) continue;

                        bool wMatches = Math.Abs(duct.WidthMm - note.WidthMm) <= 60.0 || Math.Abs(duct.WidthMm - note.HeightMm) <= 60.0;
                        if (!wMatches) continue;

                        double score = insidePoly ? distToAxis * 0.1 : distToAxis;
                        if (score < bestScore)
                        {
                            bestScore = score;
                            bestText = note;
                        }
                    }
                }

                if (bestText != null)
                {
                    if (Math.Abs(duct.WidthMm - bestText.HeightMm) < Math.Abs(duct.WidthMm - bestText.WidthMm))
                    {
                        duct.HeightMm = bestText.WidthMm;
                    }
                    else
                    {
                        duct.HeightMm = bestText.HeightMm;
                    }
                    duct.TextLabel = bestText.RawText;
                    duct.SizeSource = "Text CAD: " + bestText.RawText;
                    duct.HasDirectText = true;
                }
                else
                {
                    duct.HeightMm = defaultHeightMm;
                    duct.SizeSource = string.Format("Mặc định (H={0})", (int)defaultHeightMm);
                }
            }

            Queue<CadDuctSegment> queue = new Queue<CadDuctSegment>();
            foreach (CadDuctSegment d in ducts)
            {
                if (d.HasDirectText) queue.Enqueue(d);
            }

            while (queue.Count > 0)
            {
                CadDuctSegment current = queue.Dequeue();
                foreach (CadDuctSegment neighbor in current.ConnectedNeighbors)
                {
                    if (!neighbor.HasDirectText)
                    {
                        neighbor.HeightMm = current.HeightMm;
                        neighbor.TextLabel = string.Format("{0}x{1} (Lan truyền)", (int)neighbor.WidthMm, (int)neighbor.HeightMm);
                        neighbor.SizeSource = string.Format("Kế thừa H={0} từ đoạn #{1}", (int)current.HeightMm, current.Id.Substring(0, 4));
                        neighbor.HasDirectText = true;
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        public static int BuildDuctsInRevit(
            Document doc,
            List<CadDuctSegment> ducts,
            List<CadConnectionEdge> edges,
            ElementId systemTypeId,
            ElementId ductTypeId,
            ElementId levelId,
            bool useConcentricTransition,
            bool useTakeoffTap,
            out string summaryReport)
        {
            int createdDuctCount = 0;
            int createdTransitionCount = 0;
            int createdElbowCount = 0;
            int createdTakeoffCount = 0;
            int createdTeeCount = 0;

            if (doc == null || ducts == null || ducts.Count == 0)
            {
                summaryReport = "Không có đoạn ống gió nào được chọn để dựng.";
                return 0;
            }

            Level targetLevel = doc.GetElement(levelId) as Level;
            double levelElevFeet = (targetLevel != null) ? targetLevel.Elevation : 0.0;

            Dictionary<string, XYZ> adjustedStarts = new Dictionary<string, XYZ>();
            Dictionary<string, XYZ> adjustedEnds = new Dictionary<string, XYZ>();

            foreach (CadDuctSegment d in ducts)
            {
                double absZ = levelElevFeet + d.CenterElevationMm * MM_TO_FEET;
                adjustedStarts[d.Id] = new XYZ(d.StartPoint.X, d.StartPoint.Y, absZ);
                adjustedEnds[d.Id] = new XYZ(d.EndPoint.X, d.EndPoint.Y, absZ);
            }

            if (edges != null)
            {
                foreach (CadConnectionEdge edge in edges)
                {
                    if (edge.Kind == CadConnectionKind.Transition)
                    {
                        AlignTransitionEndpoints(edge.SegmentA, edge.SegmentB, adjustedStarts, adjustedEnds);
                    }
                    else if (edge.Kind == CadConnectionKind.TakeoffTap)
                    {
                        AdjustTapBranchEndpoint(edge.SegmentA, edge.SegmentB, adjustedStarts, adjustedEnds);
                    }
                    else if (edge.Kind == CadConnectionKind.Elbow && edge.JunctionPoint != null)
                    {
                        AdjustElbowEndpoints(edge.SegmentA, edge.SegmentB, edge.JunctionPoint, adjustedStarts, adjustedEnds);
                    }
                }
            }

            using (Transaction tx = new Transaction(doc, "HDTOOL - Dựng Ống Gió 3D Từ Hình Vẽ CAD"))
            {
                tx.Start();

                Dictionary<string, Duct> ductMap = new Dictionary<string, Duct>();

                foreach (CadDuctSegment seg in ducts)
                {
                    try
                    {
                        XYZ pt1 = adjustedStarts[seg.Id];
                        XYZ pt2 = adjustedEnds[seg.Id];
                        if (pt1.DistanceTo(pt2) < 40.0 * MM_TO_FEET) continue;

                        Duct duct = Duct.Create(doc, systemTypeId, ductTypeId, levelId, pt1, pt2);
                        if (duct != null)
                        {
                            Parameter pW = duct.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM);
                            if (pW != null && !pW.IsReadOnly) pW.Set(seg.WidthMm * MM_TO_FEET);

                            Parameter pH = duct.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);
                            if (pH != null && !pH.IsReadOnly) pH.Set(seg.HeightMm * MM_TO_FEET);

                            seg.CreatedDuctId = duct.Id;
                            ductMap[seg.Id] = duct;
                            createdDuctCount++;
                        }
                    }
                    catch { }
                }

                doc.Regenerate();

                if (edges != null)
                {
                    List<CadConnectionEdge> teeEdges = edges.Where(e => e.Kind == CadConnectionKind.TeeBranch).ToList();
                    HashSet<string> handledTeeIds = new HashSet<string>();

                    foreach (var grp in teeEdges.GroupBy(e => e.SegmentA.Id))
                    {
                        List<CadConnectionEdge> list = grp.ToList();
                        Duct mainDuct;
                        if (list.Count >= 2 && ductMap.TryGetValue(grp.Key, out mainDuct))
                        {
                            Duct b1, b2;
                            if (ductMap.TryGetValue(list[0].SegmentB.Id, out b1) && ductMap.TryGetValue(list[1].SegmentB.Id, out b2))
                            {
                                bool ok = TryConnectTeeOrDoubleTap(doc, mainDuct, b1, b2, useTakeoffTap, ref createdTeeCount, ref createdTakeoffCount);
                                if (ok)
                                {
                                    handledTeeIds.Add(list[0].SegmentB.Id);
                                    handledTeeIds.Add(list[1].SegmentB.Id);
                                }
                            }
                        }
                    }

                    foreach (CadConnectionEdge edge in edges)
                    {
                        Duct d1, d2;
                        if (!ductMap.TryGetValue(edge.SegmentA.Id, out d1) || !ductMap.TryGetValue(edge.SegmentB.Id, out d2)) continue;

                        if (edge.Kind == CadConnectionKind.TeeBranch && handledTeeIds.Contains(edge.SegmentB.Id)) continue;

                        try
                        {
                            if (edge.Kind == CadConnectionKind.TakeoffTap || edge.Kind == CadConnectionKind.TeeBranch)
                            {
                                Connector cBranch = FindClosestConnectorToSegment(d2, d1);
                                if (cBranch != null && !cBranch.IsConnected)
                                {
                                    if (useTakeoffTap)
                                    {
                                        FamilyInstance tap = doc.Create.NewTakeoffFitting(cBranch, d1);
                                        if (tap != null) createdTakeoffCount++;
                                    }
                                    else
                                    {
                                        Connector cMain = FindClosestConnectorPair(d1, d2, true);
                                        if (cMain != null)
                                        {
                                            FamilyInstance elb = doc.Create.NewElbowFitting(cMain, cBranch);
                                            if (elb != null) createdElbowCount++;
                                        }
                                    }
                                }
                            }
                            else if (edge.Kind == CadConnectionKind.Transition)
                            {
                                Connector c1, c2;
                                FindBestFacingConnectors(d1, d2, out c1, out c2);
                                if (c1 != null && c2 != null && !c1.IsConnected && !c2.IsConnected)
                                {
                                    FamilyInstance trans = doc.Create.NewTransitionFitting(c1, c2);
                                    if (trans != null) createdTransitionCount++;
                                }
                            }
                            else if (edge.Kind == CadConnectionKind.Elbow)
                            {
                                Connector c1, c2;
                                FindBestFacingConnectors(d1, d2, out c1, out c2);
                                if (c1 != null && c2 != null && !c1.IsConnected && !c2.IsConnected)
                                {
                                    FamilyInstance elb = doc.Create.NewElbowFitting(c1, c2);
                                    if (elb != null) createdElbowCount++;
                                }
                            }
                            else if (edge.Kind == CadConnectionKind.CollinearDirect)
                            {
                                Connector c1, c2;
                                FindBestFacingConnectors(d1, d2, out c1, out c2);
                                if (c1 != null && c2 != null && !c1.IsConnected && !c2.IsConnected)
                                {
                                    c1.ConnectTo(c2);
                                }
                            }
                        }
                        catch { }
                    }
                }

                tx.Commit();
            }

            summaryReport = string.Format(
                "✔ Dựng thành công: {0} ống gió thẳng | {1} côn thu đồng tâm | {2} cút chuyển hướng | {3} chân rẽ Tap{4}!",
                createdDuctCount,
                createdTransitionCount,
                createdElbowCount,
                createdTakeoffCount,
                createdTeeCount > 0 ? string.Format(" | {0} chạc chia Tee", createdTeeCount) : "");

            return createdDuctCount;
        }

        private static bool TryConnectTeeOrDoubleTap(
            Document doc,
            Duct mainDuct,
            Duct branch1,
            Duct branch2,
            bool preferTap,
            ref int teeCount,
            ref int tapCount)
        {
            try
            {
                Connector cB1 = FindClosestConnectorToSegment(branch1, mainDuct);
                Connector cB2 = FindClosestConnectorToSegment(branch2, mainDuct);
                Connector cMain = FindClosestConnectorToSegment(mainDuct, branch1);

                if (cB1 != null && cB2 != null && cMain != null && !cB1.IsConnected && !cB2.IsConnected && !cMain.IsConnected)
                {
                    if (!preferTap)
                    {
                        try
                        {
                            FamilyInstance tee = doc.Create.NewTeeFitting(cB1, cB2, cMain);
                            if (tee != null) { teeCount++; return true; }
                        }
                        catch { }
                    }

                    LocationCurve lcMain = mainDuct.Location as LocationCurve;
                    if (lcMain != null)
                    {
                        Line line = lcMain.Curve as Line;
                        if (line != null)
                        {
                            XYZ p0 = line.GetEndPoint(0);
                            XYZ p1 = line.GetEndPoint(1);
                            XYZ bMid = (cB1.Origin + cB2.Origin) * 0.5;
                            XYZ u = (p1 - p0).Normalize();

                            double d0 = p0.DistanceTo(bMid);
                            double d1 = p1.DistanceTo(bMid);
                            double branchHalfW = (branch1.Width * 0.5) + (100.0 * MM_TO_FEET);

                            if (d1 <= d0)
                            {
                                XYZ newEnd = bMid + u * branchHalfW;
                                lcMain.Curve = Line.CreateBound(p0, new XYZ(newEnd.X, newEnd.Y, p1.Z));
                            }
                            else
                            {
                                XYZ newStart = bMid - u * branchHalfW;
                                lcMain.Curve = Line.CreateBound(new XYZ(newStart.X, newStart.Y, p0.Z), p1);
                            }
                            doc.Regenerate();
                        }
                    }

                    try { FamilyInstance t1 = doc.Create.NewTakeoffFitting(cB1, mainDuct); if (t1 != null) tapCount++; } catch { }
                    try { FamilyInstance t2 = doc.Create.NewTakeoffFitting(cB2, mainDuct); if (t2 != null) tapCount++; } catch { }
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static void AlignTransitionEndpoints(
            CadDuctSegment d1,
            CadDuctSegment d2,
            Dictionary<string, XYZ> starts,
            Dictionary<string, XYZ> ends)
        {
            try
            {
                XYZ s1 = starts[d1.Id], e1 = ends[d1.Id];
                XYZ s2 = starts[d2.Id], e2 = ends[d2.Id];

                XYZ axis = (e1 - s1).Normalize();
                double latDiff = LineToLineDistance(s1, axis, s2, axis);
                if (latDiff <= 120.0 * MM_TO_FEET)
                {
                    XYZ s2Proj = s1 + axis * (s2 - s1).DotProduct(axis);
                    XYZ e2Proj = s1 + axis * (e2 - s1).DotProduct(axis);
                    starts[d2.Id] = new XYZ(s2Proj.X, s2Proj.Y, s2.Z);
                    ends[d2.Id] = new XYZ(e2Proj.X, e2Proj.Y, e2.Z);
                }
            }
            catch { }
        }

        private static void AdjustTapBranchEndpoint(
            CadDuctSegment mainDuct,
            CadDuctSegment branchDuct,
            Dictionary<string, XYZ> starts,
            Dictionary<string, XYZ> ends)
        {
            try
            {
                XYZ ms = starts[mainDuct.Id], me = ends[mainDuct.Id];
                XYZ bs = starts[branchDuct.Id], be = ends[branchDuct.Id];

                XYZ mainDir = (me - ms).Normalize();
                XYZ projStart = ms + mainDir * (bs - ms).DotProduct(mainDir);
                XYZ projEnd = ms + mainDir * (be - ms).DotProduct(mainDir);

                double dStart = bs.DistanceTo(projStart);
                double dEnd = be.DistanceTo(projEnd);

                double halfWFeet = (mainDuct.WidthMm * 0.5 + 15.0) * MM_TO_FEET;

                if (dStart <= dEnd)
                {
                    XYZ outDir = (bs - projStart);
                    if (outDir.GetLength() > 1e-5)
                    {
                        outDir = outDir.Normalize();
                        XYZ snappedStart = projStart + outDir * halfWFeet;
                        if (snappedStart.DistanceTo(be) > 60.0 * MM_TO_FEET)
                            starts[branchDuct.Id] = new XYZ(snappedStart.X, snappedStart.Y, bs.Z);
                    }
                }
                else
                {
                    XYZ outDir = (be - projEnd);
                    if (outDir.GetLength() > 1e-5)
                    {
                        outDir = outDir.Normalize();
                        XYZ snappedEnd = projEnd + outDir * halfWFeet;
                        if (snappedEnd.DistanceTo(bs) > 60.0 * MM_TO_FEET)
                            ends[branchDuct.Id] = new XYZ(snappedEnd.X, snappedEnd.Y, be.Z);
                    }
                }
            }
            catch { }
        }

        private static void AdjustElbowEndpoints(
            CadDuctSegment d1,
            CadDuctSegment d2,
            XYZ cornerPt,
            Dictionary<string, XYZ> starts,
            Dictionary<string, XYZ> ends)
        {
            try
            {
                ClampEndpointBeforeCorner(d1, cornerPt, starts, ends);
                ClampEndpointBeforeCorner(d2, cornerPt, starts, ends);
            }
            catch { }
        }

        private static void ClampEndpointBeforeCorner(
            CadDuctSegment d,
            XYZ cornerPt,
            Dictionary<string, XYZ> starts,
            Dictionary<string, XYZ> ends)
        {
            XYZ s = starts[d.Id], e = ends[d.Id];
            XYZ cornerZ = new XYZ(cornerPt.X, cornerPt.Y, s.Z);
            double ds = s.DistanceTo(cornerZ);
            double de = e.DistanceTo(cornerZ);
            double minClear = (d.WidthMm * 0.5 + 30.0) * MM_TO_FEET;

            if (ds <= de && ds < minClear && de > minClear + 50.0 * MM_TO_FEET)
            {
                XYZ dirAway = (e - s).Normalize();
                starts[d.Id] = cornerZ + dirAway * minClear;
            }
            else if (de < ds && de < minClear && ds > minClear + 50.0 * MM_TO_FEET)
            {
                XYZ dirAway = (s - e).Normalize();
                ends[d.Id] = cornerZ + dirAway * minClear;
            }
        }

        private static void FindBestFacingConnectors(Duct d1, Duct d2, out Connector c1, out Connector c2)
        {
            c1 = null;
            c2 = null;
            double bestDist = double.MaxValue;
            foreach (Connector a in d1.ConnectorManager.Connectors)
            {
                if (a.IsConnected) continue;
                foreach (Connector b in d2.ConnectorManager.Connectors)
                {
                    if (b.IsConnected) continue;
                    double dist = a.Origin.DistanceTo(b.Origin);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        c1 = a;
                        c2 = b;
                    }
                }
            }
        }

        private static Connector FindClosestConnectorToSegment(Duct branch, Duct main)
        {
            LocationCurve lcMain = main.Location as LocationCurve;
            if (lcMain == null) return null;
            Curve mainCurve = lcMain.Curve;

            Connector best = null;
            double bestDist = double.MaxValue;
            foreach (Connector c in branch.ConnectorManager.Connectors)
            {
                if (c.IsConnected) continue;
                double d = mainCurve.Distance(c.Origin);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = c;
                }
            }
            return best;
        }

        private static Connector FindClosestConnectorPair(Duct d1, Duct d2, bool returnFirst)
        {
            Connector c1, c2;
            FindBestFacingConnectors(d1, d2, out c1, out c2);
            return returnFirst ? c1 : c2;
        }

        private static void AddEdgeUnique(
            List<CadConnectionEdge> edges,
            HashSet<string> addedPairs,
            CadDuctSegment a,
            CadDuctSegment b,
            CadConnectionKind kind,
            CadDuctSegment bridge,
            XYZ junction)
        {
            string key = MakePairKey(a.Id, b.Id);
            if (addedPairs.Contains(key)) return;
            addedPairs.Add(key);
            edges.Add(new CadConnectionEdge(a, b, kind, bridge, junction));
        }

        private static string MakePairKey(string id1, string id2)
        {
            return string.CompareOrdinal(id1, id2) < 0 ? (id1 + "_" + id2) : (id2 + "_" + id1);
        }

        private static double MinEndpointDistanceXY(CadDuctSegment d1, CadDuctSegment d2)
        {
            double d11 = DistXY(d1.StartPoint, d2.StartPoint);
            double d12 = DistXY(d1.StartPoint, d2.EndPoint);
            double d21 = DistXY(d1.EndPoint, d2.StartPoint);
            double d22 = DistXY(d1.EndPoint, d2.EndPoint);
            return Math.Min(Math.Min(d11, d12), Math.Min(d21, d22));
        }

        private static XYZ GetClosestEndpointsMid(CadDuctSegment d1, CadDuctSegment d2)
        {
            XYZ[] p1 = new XYZ[] { d1.StartPoint, d1.EndPoint };
            XYZ[] p2 = new XYZ[] { d2.StartPoint, d2.EndPoint };
            double best = double.MaxValue;
            XYZ mid = d1.EndPoint;
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    double d = DistXY(p1[i], p2[j]);
                    if (d < best) { best = d; mid = (p1[i] + p2[j]) * 0.5; }
                }
            }
            return mid;
        }

        private static double DistXY(XYZ a, XYZ b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static XYZ ProjectPointOnSegmentXY(XYZ pt, XYZ a, XYZ b, double z)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double len2 = dx * dx + dy * dy;
            if (len2 < 1e-10) return new XYZ(a.X, a.Y, z);
            double t = ((pt.X - a.X) * dx + (pt.Y - a.Y) * dy) / len2;
            if (t < 0) t = 0;
            else if (t > 1) t = 1;
            return new XYZ(a.X + t * dx, a.Y + t * dy, z);
        }

        private static XYZ LineLineIntersectXY(XYZ p1, XYZ dir1, XYZ p2, XYZ dir2, double z)
        {
            double a1 = dir1.X, b1 = -dir2.X, c1 = p2.X - p1.X;
            double a2 = dir1.Y, b2 = -dir2.Y, c2 = p2.Y - p1.Y;
            double det = a1 * b2 - a2 * b1;
            if (Math.Abs(det) < 1e-6) return null;
            double t = (c1 * b2 - c2 * b1) / det;
            return new XYZ(p1.X + t * dir1.X, p1.Y + t * dir1.Y, z);
        }

        private static double LineToLineDistance(XYZ p1, XYZ dir1, XYZ p2, XYZ dir2)
        {
            XYZ v = new XYZ(p2.X - p1.X, p2.Y - p1.Y, 0.0);
            XYZ norm = new XYZ(-dir1.Y, dir1.X, 0.0);
            if (norm.GetLength() < 0.0001) return v.GetLength();
            norm = norm.Normalize();
            return Math.Abs(v.DotProduct(norm));
        }

        private static XYZ ComputeCentroidXY(List<XYZ> pts, double z)
        {
            double sx = 0, sy = 0;
            for (int i = 0; i < pts.Count; i++) { sx += pts[i].X; sy += pts[i].Y; }
            return new XYZ(sx / pts.Count, sy / pts.Count, z);
        }

        private static BoundingBoxXYZ ComputeBoundingBoxXY(List<XYZ> pts)
        {
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            foreach (XYZ p in pts)
            {
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }
            BoundingBoxXYZ bb = new BoundingBoxXYZ();
            bb.Min = new XYZ(minX, minY, -1000);
            bb.Max = new XYZ(maxX, maxY, 1000);
            return bb;
        }

        private static bool PointInOutlineXY(XYZ pt, Outline box)
        {
            return pt.X >= box.MinimumPoint.X && pt.X <= box.MaximumPoint.X
                && pt.Y >= box.MinimumPoint.Y && pt.Y <= box.MaximumPoint.Y;
        }

        private static bool PolyIntersectsOutlineXY(List<XYZ> pts, Outline box)
        {
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            foreach (XYZ p in pts)
            {
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }
            return !(maxX < box.MinimumPoint.X || minX > box.MaximumPoint.X
                  || maxY < box.MinimumPoint.Y || minY > box.MaximumPoint.Y);
        }
    }
}
