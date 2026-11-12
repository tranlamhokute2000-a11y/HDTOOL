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
            int unusedColor;
            return GetLayerNameAndColor(obj, doc, out unusedColor);
        }

        private static string GetLayerNameAndColor(GeometryObject obj, Document doc, out int colorRgb)
        {
            colorRgb = -1;
            if (obj == null || doc == null) return string.Empty;
            if (obj.GraphicsStyleId != ElementId.InvalidElementId)
            {
                GraphicsStyle gs = doc.GetElement(obj.GraphicsStyleId) as GraphicsStyle;
                if (gs != null && gs.GraphicsStyleCategory != null)
                {
                    try
                    {
                        Color col = gs.GraphicsStyleCategory.LineColor;
                        if (col != null && col.IsValid)
                        {
                            colorRgb = (((int)col.Red) << 16) | (((int)col.Green) << 8) | ((int)col.Blue);
                        }
                    }
                    catch { }

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
            public int ColorRgb;
            public int[] SourceLineIndices;
        }

        private class RawLineShape
        {
            public Line Line;
            public string LayerName;
            public int ColorRgb;
        }

        private class RawArcShape
        {
            public XYZ Center;
            public XYZ P0;
            public XYZ P1;
            public XYZ PMid;
            public double RadiusFeet;
            public double SpanDeg;
            public List<XYZ> SamplePoints;
            public string LayerName;
            public int ColorRgb;
        }

        private class ArcPairCandidate
        {
            public int InnerIdx;
            public int OuterIdx;
            public bool SameDir;
            public int CapLineIdx0;
            public int CapLineIdx1;
            public double Score;
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

            List<CadTextNote> textNotes;
            List<CadDamperItem> dampers;
            List<CadExclusionZone> exclusions;
            string extractorDiag;
            CadDwgExtractor.ExtractTextAndDampersFromDwg(doc, cadLink, out textNotes, out dampers, out exclusions, out extractorDiag);

            bool filterBySpecificLayer = !string.IsNullOrWhiteSpace(optionalLayerFilter)
                && !optionalLayerFilter.StartsWith("(");

            Options opt = new Options();
            opt.ComputeReferences = false;
            opt.IncludeNonVisibleObjects = true;

            GeometryElement rootGeom = cadLink.get_Geometry(opt);
            if (rootGeom == null) return;

            List<RawPolyShape> rawPolys = new List<RawPolyShape>();
            List<RawLineShape> rawLines = new List<RawLineShape>();
            List<RawArcShape> rawArcs = new List<RawArcShape>();

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
                double pad = 120000.0 * MM_TO_FEET;
                wideBox = new Outline(
                    new XYZ(pickSeedPoint.X - pad, pickSeedPoint.Y - pad, -1e6),
                    new XYZ(pickSeedPoint.X + pad, pickSeedPoint.Y + pad, 1e6));
            }

            CollectGeometryRecursive(rootGeom, Transform.Identity, doc, filterBySpecificLayer ? optionalLayerFilter : null, rawPolys, rawLines, rawArcs);

            HashSet<int> consumedLineIndices = new HashSet<int>();

            // Ghép các cặp Arc đồng tâm thành Cút cong (Curved Elbow) và đánh dấu nét chốt đầu cút để không biến thành ống thẳng giả
            List<CadDuctSegment> arcElbows = PairConcentricArcsIntoElbows(rawArcs, rawLines, centerElevMm, wideBox, consumedLineIndices);

            // Tái tạo các đa giác kín từ các đường Line nối nhau nếu bản vẽ dùng Line thay vì PolyLine
            ReconstructClosedLoopsFromLines(rawLines, rawPolys, consumedLineIndices);

            List<CadDuctSegment> allRectangles = new List<CadDuctSegment>();
            List<CadDuctSegment> allFittings = new List<CadDuctSegment>();

            foreach (CadDuctSegment ae in arcElbows)
            {
                if (!IsDuplicateSegment(ae, allFittings))
                {
                    allFittings.Add(ae);
                }
            }

            foreach (RawPolyShape rp in rawPolys)
            {
                if (wideBox != null && !PolyIntersectsOutlineXY(rp.Points, wideBox)) continue;

                CadDuctSegment classified = ClassifyPolygonShape(rp.Points, rp.LayerName, centerElevMm);
                if (classified == null) continue;
                classified.ColorRgb = rp.ColorRgb;

                if (rp.SourceLineIndices != null)
                {
                    for (int sIdx = 0; sIdx < rp.SourceLineIndices.Length; sIdx++)
                    {
                        consumedLineIndices.Add(rp.SourceLineIndices[sIdx]);
                    }
                }

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
                List<RawLineShape> filteredLines = new List<RawLineShape>();
                for (int i = 0; i < rawLines.Count; i++)
                {
                    if (consumedLineIndices.Contains(i)) continue;
                    RawLineShape rl = rawLines[i];
                    XYZ p0 = rl.Line.GetEndPoint(0);
                    XYZ p1 = rl.Line.GetEndPoint(1);
                    XYZ mid = (p0 + p1) * 0.5;
                    if (wideBox != null && !PointInOutlineXY(mid, wideBox)) continue;
                    if (IsLineFullyInsideExclusionZones(p0, p1, mid, exclusions)) continue;
                    filteredLines.Add(rl);
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

            // Loại bỏ triệt để các hình hộp giả ống (miệng gió/trục đứng gạch chéo, khung hộp chữ IAQ, tiêu âm/quạt/hatch phòng máy, van ngang)
            FilterFalsePositiveSegments(allRectangles, allFittings, rawLines, exclusions);

            if (allRectangles.Count == 0) return;

            // Sửa hướng trục cho các đoạn ống cổ ngắn (Length < Width, VD 647x2200 hoặc 911x2000) dựa vào mặt tiếp xúc phụ kiện/ống và mũi tên Leader
            OrientShortCollarRectanglesByTopology(allRectangles, allFittings, textNotes);

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
                        || d.ContainsPointXY((pickBox.MinimumPoint + pickBox.MaximumPoint) * 0.5, 50.0 * MM_TO_FEET)
                        || (d.PolygonVertices != null && d.PolygonVertices.Count >= 3 && PolyIntersectsOutlineXY(d.PolygonVertices, pickBox));
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

            MatchAndPropagateDuctSizes(selectedDucts, textNotes, defaultHeightMm);

            // Gộp các đoạn ống thẳng cùng kích thước nối tiếp nhau qua quần ống gió / chạc chia Y thành 1 ống chính xuyên suốt để gắn Chân rẽ sạch sẽ không đụng mối nối
            MergeCollinearTapHosts(selectedDucts, selectedEdges);
            AssignFallbackTapBevelDirections(selectedDucts, selectedEdges);

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
            List<RawLineShape> outLines,
            List<RawArcShape> outArcs)
        {
            if (geomElem == null) return;

            foreach (GeometryObject obj in geomElem)
            {
                GeometryInstance gInst = obj as GeometryInstance;
                if (gInst != null)
                {
                    string instLayer = GetLayerName(gInst, doc);
                    string symName = string.Empty;
                    SymbolGeometryId sgId = gInst.GetSymbolGeometryId();
                    if (sgId != null && sgId.IsValidObject && doc != null
                        && sgId.SymbolId != null && sgId.SymbolId != ElementId.InvalidElementId)
                    {
                        Element symElem = doc.GetElement(sgId.SymbolId);
                        if (symElem != null) symName = symElem.Name;
                    }
                    if (IsNonDuctBlockOrLayer(instLayer, symName))
                    {
                        continue;
                    }

                    Transform nextTransform = currentTransform.Multiply(gInst.Transform);
                    GeometryElement symGeom = gInst.GetSymbolGeometry();
                    if (symGeom != null)
                    {
                        CollectGeometryRecursive(symGeom, nextTransform, doc, requiredLayer, outPolys, outLines, outArcs);
                    }
                    continue;
                }

                int layColor;
                string lay = GetLayerNameAndColor(obj, doc, out layColor);
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

                        if (wcsPts.Count >= 4 && wcsPts[0].DistanceTo(wcsPts[wcsPts.Count - 1]) < 15.0 * MM_TO_FEET)
                        {
                            RawPolyShape rps = new RawPolyShape();
                            rps.Points = wcsPts;
                            rps.LayerName = lay;
                            rps.ColorRgb = layColor;
                            outPolys.Add(rps);
                        }
                        else if (!IsAirflowArrowPolygon(wcsPts))
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
                                        rls.ColorRgb = layColor;
                                        outLines.Add(rls);
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                    continue;
                }

                Arc arc = obj as Arc;
                if (arc != null && arc.IsBound)
                {
                    try
                    {
                        XYZ normalWcs = currentTransform.OfVector(arc.Normal);
                        if (normalWcs.GetLength() > 1e-6 && Math.Abs(normalWcs.Normalize().Z) > 0.7)
                        {
                            double spanRad = arc.Radius > 1e-6 ? (arc.Length / arc.Radius) : 0.0;
                            double spanDeg = spanRad * (180.0 / Math.PI);
                            if (spanDeg > 15.0 && spanDeg < 210.0)
                            {
                                XYZ c = currentTransform.OfPoint(arc.Center);
                                XYZ p0 = currentTransform.OfPoint(arc.GetEndPoint(0));
                                XYZ p1 = currentTransform.OfPoint(arc.GetEndPoint(1));
                                XYZ pMid = currentTransform.OfPoint(arc.Evaluate(0.5, true));

                                c = new XYZ(c.X, c.Y, 0.0);
                                p0 = new XYZ(p0.X, p0.Y, 0.0);
                                p1 = new XYZ(p1.X, p1.Y, 0.0);
                                pMid = new XYZ(pMid.X, pMid.Y, 0.0);

                                double r0 = c.DistanceTo(p0);
                                double r1 = c.DistanceTo(p1);
                                double rM = c.DistanceTo(pMid);
                                double rAvg = (r0 + r1 + rM) / 3.0;

                                if (rAvg >= 30.0 * MM_TO_FEET && rAvg <= 25000.0 * MM_TO_FEET
                                    && Math.Abs(r0 - rM) <= 15.0 * MM_TO_FEET
                                    && Math.Abs(r1 - rM) <= 15.0 * MM_TO_FEET)
                                {
                                    RawArcShape ras = new RawArcShape();
                                    ras.Center = c;
                                    ras.P0 = p0;
                                    ras.P1 = p1;
                                    ras.PMid = pMid;
                                    ras.RadiusFeet = rAvg;
                                    ras.SpanDeg = spanDeg;
                                    ras.LayerName = lay;
                                    ras.ColorRgb = layColor;
                                    ras.SamplePoints = new List<XYZ>(9);
                                    for (int s = 0; s <= 8; s++)
                                    {
                                        double normT = s / 8.0;
                                        XYZ sp = currentTransform.OfPoint(arc.Evaluate(normT, true));
                                        ras.SamplePoints.Add(new XYZ(sp.X, sp.Y, 0.0));
                                    }
                                    outArcs.Add(ras);
                                }
                            }
                        }
                    }
                    catch { }
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
                            rls.ColorRgb = layColor;
                            outLines.Add(rls);
                        }
                    }
                    catch { }
                }
            }
        }

        private static List<CadDuctSegment> PairConcentricArcsIntoElbows(
            List<RawArcShape> rawArcs,
            List<RawLineShape> rawLines,
            double centerElevMm,
            Outline wideBox,
            HashSet<int> consumedLineIndices)
        {
            List<CadDuctSegment> elbows = new List<CadDuctSegment>();
            if (rawArcs == null || rawArcs.Count < 2) return elbows;

            List<RawArcShape> activeArcs = rawArcs;
            if (wideBox != null)
            {
                activeArcs = new List<RawArcShape>();
                for (int i = 0; i < rawArcs.Count; i++)
                {
                    if (PolyIntersectsOutlineXY(rawArcs[i].SamplePoints, wideBox))
                    {
                        activeArcs.Add(rawArcs[i]);
                    }
                }
            }
            if (activeArcs.Count < 2) return elbows;

            double centerTolFeet = 10.0 * MM_TO_FEET;
            double capTolFeet = 25.0 * MM_TO_FEET;
            double zFeet = centerElevMm * MM_TO_FEET;

            List<ArcPairCandidate> candidates = new List<ArcPairCandidate>();
            int count = activeArcs.Count;

            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    RawArcShape a = activeArcs[i];
                    RawArcShape b = activeArcs[j];

                    double cDist = DistXY(a.Center, b.Center);
                    if (cDist >= centerTolFeet) continue;

                    double diffMm = Math.Abs(a.RadiusFeet - b.RadiusFeet) * FEET_TO_MM;
                    if (diffMm < 100.0 || diffMm > 3500.0) continue;

                    if (a.SpanDeg <= 15.0 || b.SpanDeg <= 15.0) continue;
                    if (Math.Abs(a.SpanDeg - b.SpanDeg) > 15.0) continue;

                    XYZ va = a.PMid - a.Center;
                    XYZ vb = b.PMid - b.Center;
                    if (va.GetLength() < 1e-6 || vb.GetLength() < 1e-6) continue;
                    if (va.Normalize().DotProduct(vb.Normalize()) < 0.85) continue;

                    int innerIdx = (a.RadiusFeet <= b.RadiusFeet) ? i : j;
                    int outerIdx = (innerIdx == i) ? j : i;
                    RawArcShape inner = activeArcs[innerIdx];
                    RawArcShape outer = activeArcs[outerIdx];

                    XYZ uI0 = (inner.P0 - inner.Center);
                    XYZ uI1 = (inner.P1 - inner.Center);
                    XYZ uO0 = (outer.P0 - outer.Center);
                    XYZ uO1 = (outer.P1 - outer.Center);
                    if (uI0.GetLength() < 1e-6 || uI1.GetLength() < 1e-6 || uO0.GetLength() < 1e-6 || uO1.GetLength() < 1e-6) continue;

                    uI0 = uI0.Normalize();
                    uI1 = uI1.Normalize();
                    uO0 = uO0.Normalize();
                    uO1 = uO1.Normalize();

                    bool sameDir = (uI0.DotProduct(uO0) >= uI0.DotProduct(uO1));
                    XYZ outerStart = sameDir ? outer.P0 : outer.P1;
                    XYZ outerEnd = sameDir ? outer.P1 : outer.P0;
                    XYZ uOStart = sameDir ? uO0 : uO1;
                    XYZ uOEnd = sameDir ? uO1 : uO0;

                    if (uI0.DotProduct(uOStart) < 0.85 || uI1.DotProduct(uOEnd) < 0.85) continue;

                    int cap0 = FindMatchingEndCapLine(inner.P0, outerStart, rawLines, capTolFeet);
                    int cap1 = FindMatchingEndCapLine(inner.P1, outerEnd, rawLines, capTolFeet);
                    int capMatches = (cap0 >= 0 ? 1 : 0) + (cap1 >= 0 ? 1 : 0);

                    double score = diffMm + cDist * FEET_TO_MM * 10.0;
                    if (capMatches == 2) score -= 100000.0;
                    else if (capMatches == 1) score -= 40000.0;
                    if (string.Equals(inner.LayerName, outer.LayerName, StringComparison.OrdinalIgnoreCase)) score -= 5000.0;

                    ArcPairCandidate cand = new ArcPairCandidate();
                    cand.InnerIdx = innerIdx;
                    cand.OuterIdx = outerIdx;
                    cand.SameDir = sameDir;
                    cand.CapLineIdx0 = cap0;
                    cand.CapLineIdx1 = cap1;
                    cand.Score = score;
                    candidates.Add(cand);
                }
            }

            candidates.Sort(delegate(ArcPairCandidate x, ArcPairCandidate y)
            {
                return x.Score.CompareTo(y.Score);
            });

            bool[] usedArc = new bool[count];
            foreach (ArcPairCandidate cand in candidates)
            {
                if (usedArc[cand.InnerIdx] || usedArc[cand.OuterIdx]) continue;
                usedArc[cand.InnerIdx] = true;
                usedArc[cand.OuterIdx] = true;

                if (cand.CapLineIdx0 >= 0 && consumedLineIndices != null) consumedLineIndices.Add(cand.CapLineIdx0);
                if (cand.CapLineIdx1 >= 0 && consumedLineIndices != null) consumedLineIndices.Add(cand.CapLineIdx1);

                RawArcShape inner = activeArcs[cand.InnerIdx];
                RawArcShape outer = activeArcs[cand.OuterIdx];

                List<XYZ> polyVerts = new List<XYZ>(outer.SamplePoints.Count + inner.SamplePoints.Count);
                if (cand.SameDir)
                {
                    for (int k = 0; k < outer.SamplePoints.Count; k++) polyVerts.Add(outer.SamplePoints[k]);
                }
                else
                {
                    for (int k = outer.SamplePoints.Count - 1; k >= 0; k--) polyVerts.Add(outer.SamplePoints[k]);
                }
                for (int k = inner.SamplePoints.Count - 1; k >= 0; k--)
                {
                    polyVerts.Add(inner.SamplePoints[k]);
                }

                XYZ outerStart = cand.SameDir ? outer.P0 : outer.P1;
                XYZ outerEnd = cand.SameDir ? outer.P1 : outer.P0;
                XYZ capStartMid = (inner.P0 + outerStart) * 0.5;
                XYZ capEndMid = (inner.P1 + outerEnd) * 0.5;
                XYZ midBody = (inner.PMid + outer.PMid) * 0.5;

                double widthMm = SnapStandardDuctWidthMm(Math.Abs(outer.RadiusFeet - inner.RadiusFeet) * FEET_TO_MM);
                double avgRadius = (inner.RadiusFeet + outer.RadiusFeet) * 0.5;
                double avgSpanRad = ((inner.SpanDeg + outer.SpanDeg) * 0.5) * (Math.PI / 180.0);

                CadDuctSegment elb = new CadDuctSegment();
                elb.ShapeKind = CadShapeKind.Elbow;
                elb.StartPoint = new XYZ(capStartMid.X, capStartMid.Y, zFeet);
                elb.EndPoint = new XYZ(capEndMid.X, capEndMid.Y, zFeet);
                elb.CenterPoint = new XYZ(midBody.X, midBody.Y, zFeet);
                XYZ chord = elb.EndPoint - elb.StartPoint;
                elb.Direction = chord.GetLength() > 1e-5 ? chord.Normalize() : XYZ.BasisX;
                elb.Length = avgRadius * avgSpanRad * FEET_TO_MM;
                elb.WidthMm = widthMm;
                elb.Width2Mm = widthMm;
                elb.HeightMm = 0;
                elb.CenterElevationMm = centerElevMm;
                elb.LayerName = !string.IsNullOrEmpty(inner.LayerName) ? inner.LayerName : outer.LayerName;
                elb.ColorRgb = inner.ColorRgb >= 0 ? inner.ColorRgb : outer.ColorRgb;
                elb.PolygonVertices = polyVerts;

                if (wideBox == null || PolyIntersectsOutlineXY(elb.PolygonVertices, wideBox))
                {
                    elbows.Add(elb);
                }
            }

            return elbows;
        }

        private static int FindMatchingEndCapLine(XYZ a, XYZ b, List<RawLineShape> rawLines, double tolFeet)
        {
            if (rawLines == null || rawLines.Count == 0) return -1;
            for (int i = 0; i < rawLines.Count; i++)
            {
                Line ln = rawLines[i].Line;
                XYZ l0 = ln.GetEndPoint(0);
                XYZ l1 = ln.GetEndPoint(1);
                if ((DistXY(l0, a) <= tolFeet && DistXY(l1, b) <= tolFeet)
                    || (DistXY(l0, b) <= tolFeet && DistXY(l1, a) <= tolFeet))
                {
                    return i;
                }
            }
            return -1;
        }

        private static void ReconstructClosedLoopsFromLines(List<RawLineShape> lines, List<RawPolyShape> outPolys, HashSet<int> consumedLineIndices)
        {
            if (lines == null || lines.Count < 4) return;
            double tol = 6.0 * MM_TO_FEET;
            int n = lines.Count;
            bool[] used = new bool[n];
            if (consumedLineIndices != null)
            {
                foreach (int ci in consumedLineIndices)
                {
                    if (ci >= 0 && ci < n) used[ci] = true;
                }
            }

            // Tiền tính tọa độ đầu mút & danh sách láng giềng để tránh vòng lặp O(L^4) trên bản vẽ nhiều nét Line
            XYZ[] sPts = new XYZ[n];
            XYZ[] ePts = new XYZ[n];
            List<int>[] adj = new List<int>[n];
            for (int i = 0; i < n; i++)
            {
                sPts[i] = lines[i].Line.GetEndPoint(0);
                ePts[i] = lines[i].Line.GetEndPoint(1);
                adj[i] = new List<int>();
            }

            for (int i = 0; i < n; i++)
            {
                if (used[i]) continue;
                XYZ si = sPts[i], ei = ePts[i];
                for (int j = i + 1; j < n; j++)
                {
                    if (used[j]) continue;
                    XYZ sj = sPts[j], ej = ePts[j];
                    if (si.DistanceTo(sj) < tol || si.DistanceTo(ej) < tol ||
                        ei.DistanceTo(sj) < tol || ei.DistanceTo(ej) < tol)
                    {
                        adj[i].Add(j);
                        adj[j].Add(i);
                    }
                }
            }

            // Tái tạo các vòng lặp 4 cạnh (Rectangle / Trapezoid)
            for (int i = 0; i < n; i++)
            {
                if (used[i]) continue;
                XYZ start0 = sPts[i];
                XYZ curEnd = ePts[i];

                List<int> adjI = adj[i];
                for (int ji = 0; ji < adjI.Count; ji++)
                {
                    int j = adjI[ji];
                    if (j == i || used[j]) continue;
                    XYZ nextEnd1 = null;
                    if (sPts[j].DistanceTo(curEnd) < tol) nextEnd1 = ePts[j];
                    else if (ePts[j].DistanceTo(curEnd) < tol) nextEnd1 = sPts[j];
                    if (nextEnd1 == null) continue;

                    List<int> adjJ = adj[j];
                    for (int ki = 0; ki < adjJ.Count; ki++)
                    {
                        int k = adjJ[ki];
                        if (k == i || k == j || used[k]) continue;
                        XYZ nextEnd2 = null;
                        if (sPts[k].DistanceTo(nextEnd1) < tol) nextEnd2 = ePts[k];
                        else if (ePts[k].DistanceTo(nextEnd1) < tol) nextEnd2 = sPts[k];
                        if (nextEnd2 == null) continue;

                        List<int> adjK = adj[k];
                        for (int mi = 0; mi < adjK.Count; mi++)
                        {
                            int m = adjK[mi];
                            if (m == i || m == j || m == k || used[m]) continue;
                            XYZ finalEnd = null;
                            if (sPts[m].DistanceTo(nextEnd2) < tol) finalEnd = ePts[m];
                            else if (ePts[m].DistanceTo(nextEnd2) < tol) finalEnd = sPts[m];
                            if (finalEnd == null) continue;

                            if (finalEnd.DistanceTo(start0) < tol)
                            {
                                // Tìm thấy vòng kín 4 cạnh!
                                used[i] = true; used[j] = true; used[k] = true; used[m] = true;
                                RawPolyShape rps = new RawPolyShape();
                                rps.Points = new List<XYZ>() { start0, curEnd, nextEnd1, nextEnd2, start0 };
                                rps.LayerName = lines[i].LayerName;
                                rps.ColorRgb = lines[i].ColorRgb;
                                rps.SourceLineIndices = new int[] { i, j, k, m };
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

                            // Nhận diện góc vát bất đối xứng của Chân rẽ Shoe-Tap: hướng từ tâm cổ nhánh (EndPoint) về phía tâm đáy loe rộng (StartPoint) dọc theo phương đáy
                            XYZ baseAxisXY = new XYZ(baseDir.X, baseDir.Y, 0.0);
                            if (baseAxisXY.GetLength() > 1e-6 && trap.WidthMm >= trap.Width2Mm + 20.0)
                            {
                                baseAxisXY = baseAxisXY.Normalize();
                                XYZ wideToNarrow = new XYZ(trap.StartPoint.X - trap.EndPoint.X, trap.StartPoint.Y - trap.EndPoint.Y, 0.0);
                                double flareAlongBase = wideToNarrow.DotProduct(baseAxisXY);
                                if (Math.Abs(flareAlongBase) >= 20.0 * MM_TO_FEET)
                                {
                                    trap.TapBevelDirection = (flareAlongBase >= 0.0) ? baseAxisXY : baseAxisXY.Negate();
                                }
                            }
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
            else if (verts.Count >= 5 && verts.Count <= 500)
            {
                BoundingBoxXYZ bb = ComputeBoundingBoxXY(verts);
                double spanX = (bb.Max.X - bb.Min.X) * FEET_TO_MM;
                double spanY = (bb.Max.Y - bb.Min.Y) * FEET_TO_MM;
                if (spanX >= 80 && spanY >= 80 && spanX <= 12000 && spanY <= 12000)
                {
                    CadDuctSegment polyFit = new CadDuctSegment();
                    polyFit.ShapeKind = CadShapeKind.TeeBranch; // Sẽ phân định Elbow (2 ống) hay TeeBranch (3+ ống) trong BuildGeometricTopology
                    polyFit.CenterPoint = ComputeCentroidXY(verts, zFeet);
                    polyFit.StartPoint = polyFit.CenterPoint;
                    polyFit.EndPoint = polyFit.CenterPoint;
                    polyFit.WidthMm = 0;
                    polyFit.Width2Mm = 0;
                    polyFit.HeightMm = 0;
                    polyFit.CenterElevationMm = centerElevMm;
                    polyFit.LayerName = layerName;
                    polyFit.PolygonVertices = verts;
                    return polyFit;
                }
            }

            return null;
        }

        private static List<XYZ> SimplifyPolygonVertices(List<XYZ> rawPts)
        {
            List<XYZ> clean = new List<XYZ>();
            if (rawPts == null) return clean;
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

            // Chỉ loại bỏ các đỉnh thẳng hàng trên cạnh thẳng, giữ nguyên các đỉnh tạo đường cong (Cút cong)
            double sagittaTol = 1.0 * MM_TO_FEET;
            List<XYZ> result = new List<XYZ>();
            int n = clean.Count;
            for (int i = 0; i < n; i++)
            {
                XYZ prev = (result.Count > 0) ? result[result.Count - 1] : clean[(i - 1 + n) % n];
                XYZ curr = clean[i];
                XYZ next = clean[(i + 1) % n];
                XYZ v1 = (curr - prev);
                XYZ v2 = (next - curr);
                if (v1.GetLength() < mergeTol || v2.GetLength() < mergeTol) continue;
                double dot = v1.Normalize().DotProduct(v2.Normalize());
                double perpDist = CadDuctSegment.DistPointToSegmentXY(curr, prev, next);
                if (dot < 0.99985 || perpDist > sagittaTol)
                {
                    result.Add(curr);
                }
            }

            if (result.Count >= 4)
            {
                XYZ pLast = result[result.Count - 1];
                XYZ pFirst = result[0];
                XYZ pSecond = result[1];
                XYZ v1 = pFirst - pLast;
                XYZ v2 = pSecond - pFirst;
                if (v1.GetLength() > mergeTol && v2.GetLength() > mergeTol)
                {
                    double dot0 = v1.Normalize().DotProduct(v2.Normalize());
                    double perp0 = CadDuctSegment.DistPointToSegmentXY(pFirst, pLast, pSecond);
                    if (dot0 >= 0.99985 && perp0 <= sagittaTol)
                    {
                        result.RemoveAt(0);
                    }
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
            if (n < 2) return ducts;
            bool[] used = new bool[n];

            LineSpatialGrid lineGrid = BuildLineSpatialGrid(lines);

            double minGapFeet = 100.0 * MM_TO_FEET;
            double maxGapFeet = 3500.0 * MM_TO_FEET;
            double zFeet = centerElevMm * MM_TO_FEET;

            for (int i = 0; i < n; i++)
            {
                if (used[i]) continue;
                Line l1 = lines[i].Line;
                XYZ p1Start = l1.GetEndPoint(0);
                XYZ p1End = l1.GetEndPoint(1);
                XYZ vec1 = new XYZ(p1End.X - p1Start.X, p1End.Y - p1Start.Y, 0.0);
                if (vec1.GetLength() < 1e-6) continue;
                XYZ dir1 = vec1.Normalize();
                XYZ perp1Dir = new XYZ(-dir1.Y, dir1.X, 0.0);

                int bestPair = -1;
                double bestDist = double.MaxValue;
                double bestOvlStart = 0, bestOvlEnd = 0;

                for (int j = i + 1; j < n; j++)
                {
                    if (used[j]) continue;
                    Line l2 = lines[j].Line;
                    XYZ p2Start = l2.GetEndPoint(0);
                    XYZ p2End = l2.GetEndPoint(1);
                    XYZ vec2 = new XYZ(p2End.X - p2Start.X, p2End.Y - p2Start.Y, 0.0);
                    if (vec2.GetLength() < 1e-6) continue;
                    XYZ dir2 = vec2.Normalize();

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
                            double perpA = p1Start.DotProduct(perp1Dir);
                            double perpB = p2Start.DotProduct(perp1Dir);
                            if (HasDenseIntermediateParallelLines(lineGrid, dir1, perp1Dir, perpA, perpB, ovlStart, ovlEnd, i, j))
                            {
                                continue;
                            }

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
                    double halfW = bestDist * 0.5;

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
                    seg.ColorRgb = lines[i].ColorRgb >= 0 ? lines[i].ColorRgb : lines[bestPair].ColorRgb;

                    XYZ c0 = dir1 * bestOvlStart + perp * (perpMid - halfW);
                    XYZ c1 = dir1 * bestOvlEnd + perp * (perpMid - halfW);
                    XYZ c2 = dir1 * bestOvlEnd + perp * (perpMid + halfW);
                    XYZ c3 = dir1 * bestOvlStart + perp * (perpMid + halfW);
                    seg.PolygonVertices = new List<XYZ>(4)
                    {
                        new XYZ(c0.X, c0.Y, 0.0),
                        new XYZ(c1.X, c1.Y, 0.0),
                        new XYZ(c2.X, c2.Y, 0.0),
                        new XYZ(c3.X, c3.Y, 0.0)
                    };

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

        private static bool IsNonDuctBlockOrLayer(string layerName, string symbolName)
        {
            string lay = string.IsNullOrEmpty(layerName) ? string.Empty : layerName.ToUpperInvariant();
            string sym = string.IsNullOrEmpty(symbolName) ? string.Empty : symbolName.ToUpperInvariant();

            string blockToken = sym;
            if (blockToken.EndsWith(".DWG", StringComparison.Ordinal))
            {
                blockToken = string.Empty;
            }
            else
            {
                int dwgDot = blockToken.LastIndexOf(".DWG.", StringComparison.Ordinal);
                if (dwgDot >= 0)
                {
                    blockToken = blockToken.Substring(dwgDot + 5);
                }
            }

            if (lay.IndexOf("GRILLE", StringComparison.Ordinal) >= 0
                || lay.IndexOf("DIFFUSER", StringComparison.Ordinal) >= 0
                || lay.IndexOf("TXT-BOX", StringComparison.Ordinal) >= 0
                || lay.IndexOf("AXIAL-FAN", StringComparison.Ordinal) >= 0
                || lay.IndexOf("M-FLEX", StringComparison.Ordinal) >= 0
                || lay.IndexOf("FLOW-ARROW", StringComparison.Ordinal) >= 0)
            {
                return true;
            }

            if (!string.IsNullOrEmpty(blockToken) && blockToken.IndexOf("DUCT", StringComparison.Ordinal) < 0)
            {
                if (blockToken.IndexOf("GRILLE", StringComparison.Ordinal) >= 0
                    || blockToken.IndexOf("DIFFUSER", StringComparison.Ordinal) >= 0
                    || blockToken.IndexOf("LOUVER", StringComparison.Ordinal) >= 0
                    || blockToken.IndexOf("TXT-BOX", StringComparison.Ordinal) >= 0
                    || blockToken.IndexOf("AXIAL-FAN", StringComparison.Ordinal) >= 0
                    || blockToken.IndexOf("FLOW-ARROW", StringComparison.Ordinal) >= 0
                    || blockToken.IndexOf("ATTENUATOR", StringComparison.Ordinal) >= 0
                    || blockToken.IndexOf("SILENCER", StringComparison.Ordinal) >= 0
                    || blockToken == "ATT"
                    || blockToken == "MFD"
                    || blockToken == "OBD"
                    || blockToken == "NRD"
                    || blockToken == "KNM")
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsLineFullyInsideExclusionZones(XYZ p0, XYZ p1, XYZ mid, List<CadExclusionZone> exclusions)
        {
            if (exclusions == null || exclusions.Count == 0) return false;
            double margin = 45.0 * MM_TO_FEET;
            for (int i = 0; i < exclusions.Count; i++)
            {
                CadExclusionZone z = exclusions[i];
                if (z == null || z.Kind == CadExclusionKind.ArrowSymbol) continue;
                if (z.Kind == CadExclusionKind.Unknown && z.GetAreaFeet2() > 400.0) continue;
                if (z.ContainsPointXY(mid, margin)
                    && z.ContainsPointXY(p0, margin)
                    && z.ContainsPointXY(p1, margin))
                {
                    return true;
                }
            }
            return false;
        }

        private struct LineGridItem
        {
            public int Index;
            public double P0X;
            public double P0Y;
            public double P1X;
            public double P1Y;
            public double MinX;
            public double MinY;
            public double MaxX;
            public double MaxY;
            public double DirX;
            public double DirY;
            public double LenFeet;
        }

        private sealed class LineSpatialGrid
        {
            public double CellSizeFeet;
            public LineGridItem[] Items;
            public Dictionary<long, List<int>> Cells;
            public List<int> LongItems;
        }

        private static LineSpatialGrid BuildLineSpatialGrid(List<RawLineShape> rawLines)
        {
            LineSpatialGrid grid = new LineSpatialGrid();
            grid.CellSizeFeet = 2000.0 * MM_TO_FEET;
            grid.Cells = new Dictionary<long, List<int>>();
            grid.LongItems = new List<int>();
            int count = rawLines != null ? rawLines.Count : 0;
            grid.Items = new LineGridItem[count];

            double invCell = 1.0 / grid.CellSizeFeet;
            for (int i = 0; i < count; i++)
            {
                Line ln = rawLines[i].Line;
                XYZ p0 = ln.GetEndPoint(0);
                XYZ p1 = ln.GetEndPoint(1);
                double dx = p1.X - p0.X;
                double dy = p1.Y - p0.Y;
                double len = Math.Sqrt(dx * dx + dy * dy);
                LineGridItem item = new LineGridItem();
                item.Index = i;
                item.P0X = p0.X;
                item.P0Y = p0.Y;
                item.P1X = p1.X;
                item.P1Y = p1.Y;
                item.MinX = Math.Min(p0.X, p1.X);
                item.MinY = Math.Min(p0.Y, p1.Y);
                item.MaxX = Math.Max(p0.X, p1.X);
                item.MaxY = Math.Max(p0.Y, p1.Y);
                item.LenFeet = len;
                if (len > 1e-6)
                {
                    item.DirX = dx / len;
                    item.DirY = dy / len;
                }
                grid.Items[i] = item;

                if (len < 1e-6) continue;

                int cMinX = (int)Math.Floor(item.MinX * invCell);
                int cMaxX = (int)Math.Floor(item.MaxX * invCell);
                int cMinY = (int)Math.Floor(item.MinY * invCell);
                int cMaxY = (int)Math.Floor(item.MaxY * invCell);

                if ((cMaxX - cMinX) > 20 || (cMaxY - cMinY) > 20)
                {
                    grid.LongItems.Add(i);
                    continue;
                }

                for (int cx = cMinX; cx <= cMaxX; cx++)
                {
                    for (int cy = cMinY; cy <= cMaxY; cy++)
                    {
                        long key = (((long)cx) << 32) ^ ((uint)cy);
                        List<int> bucket;
                        if (!grid.Cells.TryGetValue(key, out bucket))
                        {
                            bucket = new List<int>(4);
                            grid.Cells[key] = bucket;
                        }
                        bucket.Add(i);
                    }
                }
            }

            return grid;
        }

        private static List<int> QueryLineGrid(LineSpatialGrid grid, double minX, double minY, double maxX, double maxY, int[] stamp, int stampId)
        {
            List<int> hits = new List<int>();
            if (grid == null || grid.Items.Length == 0) return hits;

            double invCell = 1.0 / grid.CellSizeFeet;
            int cMinX = (int)Math.Floor(minX * invCell);
            int cMaxX = (int)Math.Floor(maxX * invCell);
            int cMinY = (int)Math.Floor(minY * invCell);
            int cMaxY = (int)Math.Floor(maxY * invCell);

            for (int cx = cMinX; cx <= cMaxX; cx++)
            {
                for (int cy = cMinY; cy <= cMaxY; cy++)
                {
                    long key = (((long)cx) << 32) ^ ((uint)cy);
                    List<int> bucket;
                    if (!grid.Cells.TryGetValue(key, out bucket)) continue;
                    for (int k = 0; k < bucket.Count; k++)
                    {
                        int idx = bucket[k];
                        if (stamp[idx] == stampId) continue;
                        stamp[idx] = stampId;
                        LineGridItem it = grid.Items[idx];
                        if (it.MaxX < minX || it.MinX > maxX || it.MaxY < minY || it.MinY > maxY) continue;
                        hits.Add(idx);
                    }
                }
            }

            for (int k = 0; k < grid.LongItems.Count; k++)
            {
                int idx = grid.LongItems[k];
                if (stamp[idx] == stampId) continue;
                stamp[idx] = stampId;
                LineGridItem it = grid.Items[idx];
                if (it.MaxX < minX || it.MinX > maxX || it.MaxY < minY || it.MinY > maxY) continue;
                hits.Add(idx);
            }

            return hits;
        }

        private static bool HasDenseIntermediateParallelLines(
            LineSpatialGrid grid,
            XYZ dir1,
            XYZ perp,
            double perpA,
            double perpB,
            double ovlStart,
            double ovlEnd,
            int idxA,
            int idxB)
        {
            if (grid == null || grid.Items.Length < 4) return false;

            double minPerp = Math.Min(perpA, perpB) + 15.0 * MM_TO_FEET;
            double maxPerp = Math.Max(perpA, perpB) - 15.0 * MM_TO_FEET;
            if (maxPerp <= minPerp) return false;

            XYZ pMidA = dir1 * ovlStart + perp * minPerp;
            XYZ pMidB = dir1 * ovlEnd + perp * minPerp;
            XYZ pMidC = dir1 * ovlStart + perp * maxPerp;
            XYZ pMidD = dir1 * ovlEnd + perp * maxPerp;

            double qMinX = Math.Min(Math.Min(pMidA.X, pMidB.X), Math.Min(pMidC.X, pMidD.X)) - 0.1;
            double qMaxX = Math.Max(Math.Max(pMidA.X, pMidB.X), Math.Max(pMidC.X, pMidD.X)) + 0.1;
            double qMinY = Math.Min(Math.Min(pMidA.Y, pMidB.Y), Math.Min(pMidC.Y, pMidD.Y)) - 0.1;
            double qMaxY = Math.Max(Math.Max(pMidA.Y, pMidB.Y), Math.Max(pMidC.Y, pMidD.Y)) + 0.1;

            int[] stamp = new int[grid.Items.Length];
            List<int> candidates = QueryLineGrid(grid, qMinX, qMinY, qMaxX, qMaxY, stamp, 1);

            List<double> offsets = new List<double>();
            double minOverlap = 80.0 * MM_TO_FEET;

            for (int k = 0; k < candidates.Count; k++)
            {
                int idx = candidates[k];
                if (idx == idxA || idx == idxB) continue;
                LineGridItem it = grid.Items[idx];
                if (it.LenFeet < minOverlap) continue;

                double dot = Math.Abs(it.DirX * dir1.X + it.DirY * dir1.Y);
                if (dot < 0.96) continue;

                double lp0 = it.P0X * perp.X + it.P0Y * perp.Y;
                double lp1 = it.P1X * perp.X + it.P1Y * perp.Y;
                double lp = (lp0 + lp1) * 0.5;
                if (lp <= minPerp || lp >= maxPerp) continue;

                double la0 = it.P0X * dir1.X + it.P0Y * dir1.Y;
                double la1 = it.P1X * dir1.X + it.P1Y * dir1.Y;
                double lMin = Math.Min(la0, la1);
                double lMax = Math.Max(la0, la1);
                if (Math.Min(ovlEnd, lMax) - Math.Max(ovlStart, lMin) < minOverlap) continue;

                bool dupOffset = false;
                for (int m = 0; m < offsets.Count; m++)
                {
                    if (Math.Abs(offsets[m] - lp) < 15.0 * MM_TO_FEET)
                    {
                        dupOffset = true;
                        break;
                    }
                }
                if (!dupOffset)
                {
                    offsets.Add(lp);
                    if (offsets.Count >= 3) return true;
                }
            }

            if (offsets.Count == 2)
            {
                double innerGapMm = Math.Abs(offsets[1] - offsets[0]) * FEET_TO_MM;
                if (innerGapMm < 140.0) return true;
            }

            return false;
        }

        private static void OrientShortCollarRectanglesByTopology(
            List<CadDuctSegment> straightDucts,
            List<CadDuctSegment> fittings,
            List<CadTextNote> textNotes)
        {
            if (straightDucts == null || straightDucts.Count == 0) return;

            int dc = straightDucts.Count;
            DuctAabb2D[] ductBoxes = new DuctAabb2D[dc];
            for (int i = 0; i < dc; i++)
            {
                ductBoxes[i] = ComputeDuctSegmentAabb(straightDucts[i]);
            }

            int fc = (fittings != null) ? fittings.Count : 0;
            DuctAabb2D[] fitBoxes = new DuctAabb2D[fc];
            for (int i = 0; i < fc; i++)
            {
                fitBoxes[i] = ComputeDuctSegmentAabb(fittings[i]);
            }

            double padFeet = 65.0 * MM_TO_FEET;

            for (int i = 0; i < dc; i++)
            {
                CadDuctSegment d = straightDucts[i];
                if (d == null || d.ShapeKind != CadShapeKind.StraightDuct) continue;
                if (d.PolygonVertices == null || d.PolygonVertices.Count != 4) continue;

                XYZ p0 = d.PolygonVertices[0];
                XYZ p1 = d.PolygonVertices[1];
                XYZ p2 = d.PolygonVertices[2];
                XYZ p3 = d.PolygonVertices[3];

                double l0 = DistXY(p0, p1) * FEET_TO_MM;
                double l1 = DistXY(p1, p2) * FEET_TO_MM;
                double l2 = DistXY(p2, p3) * FEET_TO_MM;
                double l3 = DistXY(p3, p0) * FEET_TO_MM;

                double w02 = (l0 + l2) * 0.5;
                double w13 = (l1 + l3) * 0.5;
                double minSide = Math.Min(w02, w13);
                double maxSide = Math.Max(w02, w13);

                if (maxSide <= minSide * 1.02 || maxSide > 3500.0 || minSide < 100.0) continue;

                bool curIs02 = (w02 <= w13);
                XYZ curF1a = curIs02 ? p0 : p1, curF1b = curIs02 ? p1 : p2;
                XYZ curF2a = curIs02 ? p2 : p3, curF2b = curIs02 ? p3 : p0;
                double curRawW = curIs02 ? w02 : w13;

                XYZ altF1a = curIs02 ? p1 : p0, altF1b = curIs02 ? p2 : p1;
                XYZ altF2a = curIs02 ? p3 : p2, altF2b = curIs02 ? p0 : p3;
                double altRawW = curIs02 ? w13 : w02;

                double scoreCur = ScoreShortCollarOrientation(
                    d, i, ductBoxes[i], curF1a, curF1b, curF2a, curF2b, curRawW,
                    straightDucts, ductBoxes, fittings, fitBoxes, textNotes, padFeet);

                double scoreAlt = ScoreShortCollarOrientation(
                    d, i, ductBoxes[i], altF1a, altF1b, altF2a, altF2b, altRawW,
                    straightDucts, ductBoxes, fittings, fitBoxes, textNotes, padFeet);

                if (scoreAlt >= scoreCur + 15.0)
                {
                    double zFeet = d.StartPoint.Z;
                    XYZ m1 = (altF1a + altF1b) * 0.5;
                    XYZ m2 = (altF2a + altF2b) * 0.5;
                    XYZ newStart = new XYZ(m1.X, m1.Y, zFeet);
                    XYZ newEnd = new XYZ(m2.X, m2.Y, zFeet);
                    XYZ diff = newEnd - newStart;
                    if (diff.GetLength() > 1e-5)
                    {
                        double snapW = SnapStandardDuctWidthMm(altRawW);
                        d.StartPoint = newStart;
                        d.EndPoint = newEnd;
                        d.CenterPoint = (newStart + newEnd) * 0.5;
                        d.Direction = diff.Normalize();
                        d.Length = diff.GetLength() * FEET_TO_MM;
                        d.WidthMm = snapW;
                        d.Width2Mm = snapW;
                    }
                }
            }
        }

        private static double ScoreShortCollarOrientation(
            CadDuctSegment self,
            int selfIdx,
            DuctAabb2D selfBox,
            XYZ f1a, XYZ f1b,
            XYZ f2a, XYZ f2b,
            double candidateRawWidthMm,
            List<CadDuctSegment> ducts,
            DuctAabb2D[] ductBoxes,
            List<CadDuctSegment> fittings,
            DuctAabb2D[] fitBoxes,
            List<CadTextNote> textNotes,
            double padFeet)
        {
            double score = 0.0;
            XYZ[] fa = new XYZ[] { f1a, f2a };
            XYZ[] fb = new XYZ[] { f1b, f2b };

            for (int fiFace = 0; fiFace < 2; fiFace++)
            {
                XYZ pA = fa[fiFace];
                XYZ pB = fb[fiFace];
                double fLenMm = DistXY(pA, pB) * FEET_TO_MM;
                if (fLenMm < 80.0) continue;

                XYZ fVec = new XYZ(pB.X - pA.X, pB.Y - pA.Y, 0.0);
                XYZ fDir = fVec.Normalize();
                XYZ fMid = new XYZ((pA.X + pB.X) * 0.5, (pA.Y + pB.Y) * 0.5, 0.0);
                XYZ outVec = new XYZ(fMid.X - self.CenterPoint.X, fMid.Y - self.CenterPoint.Y, 0.0);
                if (outVec.GetLength() < 1e-6) continue;
                XYZ outDir = outVec.Normalize();

                double bestFaceBonus = 0.0;

                if (fittings != null)
                {
                    for (int k = 0; k < fittings.Count; k++)
                    {
                        CadDuctSegment fit = fittings[k];
                        if (fit == null || fit.PolygonVertices == null || fit.PolygonVertices.Count < 3) continue;
                        DuctAabb2D kb = fitBoxes[k];
                        if (kb.MaxX + padFeet < selfBox.MinX || kb.MinX - padFeet > selfBox.MaxX ||
                            kb.MaxY + padFeet < selfBox.MinY || kb.MinY - padFeet > selfBox.MaxY) continue;
                        if (!AreDuctSystemsCompatible(self, fit)) continue;

                        if ((fit.CenterPoint.X - fMid.X) * outDir.X + (fit.CenterPoint.Y - fMid.Y) * outDir.Y <= 5.0 * MM_TO_FEET)
                            continue;

                        double edgeBonus = MaxFlushEdgeContactScore(pA, pB, fDir, fLenMm, fit.PolygonVertices, 20.0);
                        if (edgeBonus > bestFaceBonus) bestFaceBonus = edgeBonus;
                    }
                }

                for (int k = 0; k < ducts.Count; k++)
                {
                    if (k == selfIdx) continue;
                    CadDuctSegment other = ducts[k];
                    if (other == null || other.PolygonVertices == null || other.PolygonVertices.Count < 4) continue;
                    DuctAabb2D kb = ductBoxes[k];
                    if (kb.MaxX + padFeet < selfBox.MinX || kb.MinX - padFeet > selfBox.MaxX ||
                        kb.MaxY + padFeet < selfBox.MinY || kb.MinY - padFeet > selfBox.MaxY) continue;
                    if (!AreDuctSystemsCompatible(self, other)) continue;

                    if ((other.CenterPoint.X - fMid.X) * outDir.X + (other.CenterPoint.Y - fMid.Y) * outDir.Y <= 5.0 * MM_TO_FEET)
                        continue;

                    double edgeBonus = MaxFlushEdgeContactScore(pA, pB, fDir, fLenMm, other.PolygonVertices, 15.0);
                    if (edgeBonus > bestFaceBonus) bestFaceBonus = edgeBonus;
                }

                score += bestFaceBonus;
            }

            if (textNotes != null)
            {
                double snapW = SnapStandardDuctWidthMm(candidateRawWidthMm);
                for (int t = 0; t < textNotes.Count; t++)
                {
                    CadTextNote note = textNotes[t];
                    if (note == null || note.WidthMm <= 0 || note.HeightMm <= 0) continue;
                    if (!IsNoteSystemCompatibleWithDuct(self, note)) continue;

                    bool hits = (note.HasLeaderArrow && note.LeaderArrowPoint != null && self.ContainsPointXY(note.LeaderArrowPoint, 120.0 * MM_TO_FEET))
                             || (note.Position != null && self.ContainsPointXY(note.Position, 20.0 * MM_TO_FEET));
                    if (hits && Math.Abs(note.WidthMm - snapW) <= 60.0)
                    {
                        score += 15.0;
                        break;
                    }
                }
            }

            return score;
        }

        private static double MaxFlushEdgeContactScore(
            XYZ pA, XYZ pB, XYZ fDir, double fLenMm,
            List<XYZ> poly, double baseBonus)
        {
            double tA = 0.0;
            double tB = (pB.X - pA.X) * fDir.X + (pB.Y - pA.Y) * fDir.Y;
            double fMin = Math.Min(tA, tB);
            double fMax = Math.Max(tA, tB);
            double perpTol = 55.0 * MM_TO_FEET;
            double best = 0.0;

            int n = poly.Count;
            for (int e = 0; e < n; e++)
            {
                XYZ ea = poly[e];
                XYZ eb = poly[(e + 1) % n];
                XYZ eVec = new XYZ(eb.X - ea.X, eb.Y - ea.Y, 0.0);
                double eLenFeet = eVec.GetLength();
                double eLenMm = eLenFeet * FEET_TO_MM;
                if (eLenMm < 80.0) continue;

                XYZ eDir = eVec / eLenFeet;
                if (Math.Abs(eDir.DotProduct(fDir)) <= 0.94) continue;

                double perpDist = Math.Abs(((ea.X - pA.X) * (-fDir.Y)) + ((ea.Y - pA.Y) * fDir.X));
                if (perpDist > perpTol) continue;

                double ta = (ea.X - pA.X) * fDir.X + (ea.Y - pA.Y) * fDir.Y;
                double tb = (eb.X - pA.X) * fDir.X + (eb.Y - pA.Y) * fDir.Y;
                double ovMin = Math.Max(fMin, Math.Min(ta, tb));
                double ovMax = Math.Min(fMax, Math.Max(ta, tb));
                if (ovMax <= ovMin) continue;

                double overlapMm = (ovMax - ovMin) * FEET_TO_MM;
                if (overlapMm >= Math.Min(eLenMm, fLenMm) * 0.65 && overlapMm >= fLenMm * 0.55)
                {
                    double cand = baseBonus + (Math.Abs(eLenMm - fLenMm) <= 80.0 ? 10.0 : 0.0);
                    if (cand > best) best = cand;
                }
            }
            return best;
        }

        private static void FilterFalsePositiveSegments(
            List<CadDuctSegment> straightDucts,
            List<CadDuctSegment> fittings,
            List<RawLineShape> rawLines,
            List<CadExclusionZone> exclusions)
        {
            LineSpatialGrid lineGrid = BuildLineSpatialGrid(rawLines);
            int[] stamp = new int[lineGrid.Items.Length];
            int stampCounter = 0;
            List<CadExclusionZone> arrowZones = null;
            if (exclusions != null)
            {
                for (int zi = 0; zi < exclusions.Count; zi++)
                {
                    if (exclusions[zi] != null && exclusions[zi].Kind == CadExclusionKind.ArrowSymbol)
                    {
                        if (arrowZones == null) arrowZones = new List<CadExclusionZone>();
                        arrowZones.Add(exclusions[zi]);
                    }
                }
            }

            if (straightDucts != null && straightDucts.Count > 0)
            {
                List<CadDuctSegment> keptDucts = new List<CadDuctSegment>(straightDucts.Count);
                for (int i = 0; i < straightDucts.Count; i++)
                {
                    CadDuctSegment seg = straightDucts[i];
                    if (seg == null) continue;
                    if (IsSegmentMatchedByExclusionZones(seg, exclusions)) continue;
                    if (HasInternalFalseGeometryLines(seg, lineGrid, stamp, ref stampCounter, arrowZones)) continue;
                    keptDucts.Add(seg);
                }

                FilterNestedSubRectangles(keptDucts);
                FilterTransverseDamperOrFlangeStrips(keptDucts, fittings);

                straightDucts.Clear();
                straightDucts.AddRange(keptDucts);
            }

            if (fittings != null && fittings.Count > 0 && exclusions != null && exclusions.Count > 0)
            {
                List<CadDuctSegment> keptFittings = new List<CadDuctSegment>(fittings.Count);
                for (int i = 0; i < fittings.Count; i++)
                {
                    CadDuctSegment fit = fittings[i];
                    if (fit == null) continue;
                    if (IsSegmentMatchedByExclusionZones(fit, exclusions)) continue;
                    keptFittings.Add(fit);
                }
                fittings.Clear();
                fittings.AddRange(keptFittings);
            }
        }

        private static List<XYZ> BuildSegmentTestCornersXY(CadDuctSegment seg)
        {
            if (seg.PolygonVertices != null && seg.PolygonVertices.Count >= 3)
            {
                return seg.PolygonVertices;
            }

            XYZ dir = seg.Direction;
            if (dir == null || dir.GetLength() < 1e-6) dir = XYZ.BasisX;
            dir = new XYZ(dir.X, dir.Y, 0.0);
            if (dir.GetLength() < 1e-6) dir = XYZ.BasisX;
            dir = dir.Normalize();

            XYZ perp = new XYZ(-dir.Y, dir.X, 0.0);
            double halfW = Math.Max(seg.WidthMm, 50.0) * 0.5 * MM_TO_FEET;
            XYZ s = seg.StartPoint;
            XYZ e = seg.EndPoint;

            return new List<XYZ>(4)
            {
                new XYZ(s.X - perp.X * halfW, s.Y - perp.Y * halfW, 0.0),
                new XYZ(e.X - perp.X * halfW, e.Y - perp.Y * halfW, 0.0),
                new XYZ(e.X + perp.X * halfW, e.Y + perp.Y * halfW, 0.0),
                new XYZ(s.X + perp.X * halfW, s.Y + perp.Y * halfW, 0.0)
            };
        }

        private static double ComputeSegmentAreaFeet2(CadDuctSegment seg)
        {
            if (seg.PolygonVertices != null && seg.PolygonVertices.Count >= 3)
            {
                double sum = 0.0;
                int m = seg.PolygonVertices.Count;
                for (int i = 0; i < m; i++)
                {
                    XYZ a = seg.PolygonVertices[i];
                    XYZ b = seg.PolygonVertices[(i + 1) % m];
                    sum += (a.X * b.Y - b.X * a.Y);
                }
                double area = Math.Abs(sum) * 0.5;
                if (area > 1e-6) return area;
            }
            return Math.Max(seg.Length, 10.0) * MM_TO_FEET * Math.Max(seg.WidthMm, 10.0) * MM_TO_FEET;
        }

        private static bool IsSegmentMatchedByExclusionZones(CadDuctSegment seg, List<CadExclusionZone> exclusions)
        {
            if (seg == null || exclusions == null || exclusions.Count == 0) return false;

            List<XYZ> corners = BuildSegmentTestCornersXY(seg);
            double segMinX = seg.CenterPoint.X, segMinY = seg.CenterPoint.Y;
            double segMaxX = seg.CenterPoint.X, segMaxY = seg.CenterPoint.Y;
            for (int i = 0; i < corners.Count; i++)
            {
                XYZ c = corners[i];
                if (c.X < segMinX) segMinX = c.X;
                if (c.Y < segMinY) segMinY = c.Y;
                if (c.X > segMaxX) segMaxX = c.X;
                if (c.Y > segMaxY) segMaxY = c.Y;
            }

            double segArea = ComputeSegmentAreaFeet2(seg);
            double baseMargin = 45.0 * MM_TO_FEET;
            double equipMargin = 65.0 * MM_TO_FEET;

            List<CadExclusionZone> nearbyZones = null;

            for (int zi = 0; zi < exclusions.Count; zi++)
            {
                CadExclusionZone zone = exclusions[zi];
                if (zone == null || zone.Kind == CadExclusionKind.ArrowSymbol) continue;

                double zoneArea = zone.GetAreaFeet2();
                if (zone.Kind == CadExclusionKind.Unknown && zoneArea > 400.0) continue;

                bool isEquip = zone.Kind == CadExclusionKind.Attenuator
                    || zone.Kind == CadExclusionKind.FanEquipment
                    || zone.Kind == CadExclusionKind.PlenumHatch
                    || zone.Kind == CadExclusionKind.RiserDrop;

                double margin = isEquip ? equipMargin : baseMargin;
                if (!zone.OverlapsBoxXY(segMinX, segMinY, segMaxX, segMaxY, margin)) continue;

                if (nearbyZones == null) nearbyZones = new List<CadExclusionZone>(4);
                nearbyZones.Add(zone);

                bool centerIn = zone.ContainsPointXY(seg.CenterPoint, margin);
                if (!centerIn) continue;

                int insideCount = 0;
                for (int ci = 0; ci < corners.Count; ci++)
                {
                    if (zone.ContainsPointXY(corners[ci], margin)) insideCount++;
                }

                bool areaOkAll = zoneArea <= 1e-6 || segArea <= zoneArea * 1.45;
                bool areaOkMost = zoneArea <= 1e-6 || segArea <= zoneArea * 1.25;

                if (insideCount == corners.Count && areaOkAll) return true;
                if (corners.Count >= 4 && insideCount * 4 >= corners.Count * 3 && areaOkMost) return true;

                if (isEquip
                    && zone.ContainsPointXY(seg.StartPoint, margin)
                    && zone.ContainsPointXY(seg.EndPoint, margin)
                    && (zoneArea <= 1e-6 || segArea <= zoneArea * 1.5))
                {
                    return true;
                }
            }

            // Tổ hợp nhiều vùng loại trừ liền kề (VD: côn chia quần Trouser Piece gồm nhiều mảng ANSI37 hatch cạnh nhau)
            if (nearbyZones != null && nearbyZones.Count >= 2 && seg.Length <= 5000.0)
            {
                bool centerCovered = false;
                for (int zi = 0; zi < nearbyZones.Count; zi++)
                {
                    CadExclusionZone z = nearbyZones[zi];
                    if (z.Kind == CadExclusionKind.AnnotationBox) continue;
                    if (z.ContainsPointXY(seg.CenterPoint, equipMargin))
                    {
                        centerCovered = true;
                        break;
                    }
                }
                if (centerCovered)
                {
                    int coveredCorners = 0;
                    for (int ci = 0; ci < corners.Count; ci++)
                    {
                        XYZ pt = corners[ci];
                        for (int zi = 0; zi < nearbyZones.Count; zi++)
                        {
                            CadExclusionZone z = nearbyZones[zi];
                            if (z.Kind == CadExclusionKind.AnnotationBox) continue;
                            if (z.ContainsPointXY(pt, equipMargin))
                            {
                                coveredCorners++;
                                break;
                            }
                        }
                    }
                    if (coveredCorners == corners.Count) return true;
                }
            }

            return false;
        }

        private static bool IsPointInsideAnyArrowZone(double x, double y, List<CadExclusionZone> arrowZones)
        {
            if (arrowZones == null || arrowZones.Count == 0) return false;
            XYZ pt = new XYZ(x, y, 0.0);
            double tolFeet = 25.0 * MM_TO_FEET;
            for (int i = 0; i < arrowZones.Count; i++)
            {
                if (arrowZones[i].ContainsPointXY(pt, tolFeet)) return true;
            }
            return false;
        }

        private static bool IsAirflowArrowPolygon(IList<XYZ> pts)
        {
            if (pts == null || pts.Count < 6) return false;
            List<XYZ> v = new List<XYZ>(pts.Count);
            for (int i = 0; i < pts.Count; i++)
            {
                XYZ p = pts[i];
                if (p == null) continue;
                if (v.Count == 0 || DistXY(v[v.Count - 1], p) > 2.0 * MM_TO_FEET) v.Add(new XYZ(p.X, p.Y, 0.0));
            }
            if (v.Count >= 2 && DistXY(v[0], v[v.Count - 1]) <= 2.0 * MM_TO_FEET) v.RemoveAt(v.Count - 1);
            if (v.Count < 6 || v.Count > 12) return false;

            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            for (int i = 0; i < v.Count; i++)
            {
                if (v[i].X < minX) minX = v[i].X;
                if (v[i].Y < minY) minY = v[i].Y;
                if (v[i].X > maxX) maxX = v[i].X;
                if (v[i].Y > maxY) maxY = v[i].Y;
            }
            double dxMm = (maxX - minX) * FEET_TO_MM;
            double dyMm = (maxY - minY) * FEET_TO_MM;
            double longMm = Math.Max(dxMm, dyMm);
            double shortMm = Math.Min(dxMm, dyMm);
            if (longMm < 180.0 || longMm > 1100.0 || shortMm < 60.0 || shortMm > 450.0) return false;

            // Thử các trục ứng viên: trục dài AABB chính (X/Y) và phương cạnh dài nhất (bắt cả mũi tên xoay góc bất kỳ)
            XYZ bestEdgeAxis = (dxMm >= dyMm) ? XYZ.BasisX : XYZ.BasisY;
            double bestEdgeLen = 0.0;
            for (int i = 0; i < v.Count; i++)
            {
                XYZ a = v[i], b = v[(i + 1) % v.Count];
                double d = DistXY(a, b);
                if (d > bestEdgeLen)
                {
                    bestEdgeLen = d;
                    XYZ dv = new XYZ(b.X - a.X, b.Y - a.Y, 0.0);
                    if (dv.GetLength() > 1e-6) bestEdgeAxis = dv.Normalize();
                }
            }
            XYZ[] candidateAxes = new XYZ[]
            {
                (dxMm >= dyMm) ? XYZ.BasisX : XYZ.BasisY,
                bestEdgeAxis
            };

            double[] us = new double[v.Count];
            double[] vs = new double[v.Count];
            for (int axIdx = 0; axIdx < candidateAxes.Length; axIdx++)
            {
                XYZ axis = candidateAxes[axIdx];
                if (axIdx > 0 && Math.Abs(axis.DotProduct(candidateAxes[0])) > 0.999) continue;
                XYZ perp = new XYZ(-axis.Y, axis.X, 0.0);

                double minU = double.MaxValue, maxU = double.MinValue;
                double minV = double.MaxValue, maxV = double.MinValue;
                for (int i = 0; i < v.Count; i++)
                {
                    us[i] = v[i].X * axis.X + v[i].Y * axis.Y;
                    vs[i] = v[i].X * perp.X + v[i].Y * perp.Y;
                    if (us[i] < minU) minU = us[i];
                    if (us[i] > maxU) maxU = us[i];
                    if (vs[i] < minV) minV = vs[i];
                    if (vs[i] > maxV) maxV = vs[i];
                }
                double spanU = maxU - minU;
                double spanV = maxV - minV;
                if (spanU < 180.0 * MM_TO_FEET || spanV < 60.0 * MM_TO_FEET || spanU / spanV < 1.3) continue;

                // Mũi tên rỗng chuẩn (7-8 đỉnh): 1 đầu nhọn hội tụ về 1 đỉnh duy nhất, đầu đối diện có thân hẹp hơn độ mở cánh mũi tên
                for (int dirSign = -1; dirSign <= 1; dirSign += 2)
                {
                    double tipU = (dirSign > 0) ? maxU : minU;
                    double tailU = (dirSign > 0) ? minU : maxU;
                    int tipIdx = -1, nearTipCount = 0;
                    for (int i = 0; i < v.Count; i++)
                    {
                        if (Math.Abs(us[i] - tipU) <= 25.0 * MM_TO_FEET)
                        {
                            nearTipCount++;
                            tipIdx = i;
                        }
                    }
                    if (nearTipCount == 1 && tipIdx >= 0)
                    {
                        int prevIdx = (tipIdx - 1 + v.Count) % v.Count;
                        int nextIdx = (tipIdx + 1) % v.Count;
                        double l1 = DistXY(v[tipIdx], v[prevIdx]);
                        double l2 = DistXY(v[tipIdx], v[nextIdx]);
                        double dv1 = vs[prevIdx] - vs[tipIdx];
                        double dv2 = vs[nextIdx] - vs[tipIdx];
                        if (l1 >= 100.0 * MM_TO_FEET && l2 >= 100.0 * MM_TO_FEET
                            && Math.Abs(l1 - l2) <= 45.0 * MM_TO_FEET
                            && dv1 * dv2 < -1e-6)
                        {
                            double tailMinV = double.MaxValue, tailMaxV = double.MinValue;
                            for (int i = 0; i < v.Count; i++)
                            {
                                if (Math.Abs(us[i] - tailU) <= spanU * 0.25)
                                {
                                    if (vs[i] < tailMinV) tailMinV = vs[i];
                                    if (vs[i] > tailMaxV) tailMaxV = vs[i];
                                }
                            }
                            if (tailMaxV >= tailMinV && (tailMaxV - tailMinV) <= spanV * 0.72)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        private static bool TryMatchArrowHeadPair(
            LineGridItem a,
            LineGridItem b,
            XYZ dir,
            XYZ perp,
            double halfWidthFeet,
            out double arrowMinLong,
            out double arrowMaxLong,
            out double arrowMidLat)
        {
            arrowMinLong = 0; arrowMaxLong = 0; arrowMidLat = 0;
            if (Math.Abs(a.LenFeet - b.LenFeet) > 40.0 * MM_TO_FEET) return false;
            if (a.LenFeet > 550.0 * MM_TO_FEET || b.LenFeet > 550.0 * MM_TO_FEET) return false;

            XYZ[] pa = new XYZ[] { new XYZ(a.P0X, a.P0Y, 0), new XYZ(a.P1X, a.P1Y, 0) };
            XYZ[] pb = new XYZ[] { new XYZ(b.P0X, b.P0Y, 0), new XYZ(b.P1X, b.P1Y, 0) };
            double bestTipDist = 28.0 * MM_TO_FEET;
            XYZ tipPt = null;
            XYZ wingA = null, wingB = null;
            for (int ia = 0; ia < 2; ia++)
            {
                for (int ib = 0; ib < 2; ib++)
                {
                    double d = pa[ia].DistanceTo(pb[ib]);
                    if (d <= bestTipDist)
                    {
                        bestTipDist = d;
                        tipPt = (pa[ia] + pb[ib]) * 0.5;
                        wingA = pa[1 - ia];
                        wingB = pb[1 - ib];
                    }
                }
            }
            if (tipPt == null || wingA == null || wingB == null) return false;

            double duA = (wingA - tipPt).DotProduct(dir);
            double duB = (wingB - tipPt).DotProduct(dir);
            double dvA = (wingA - tipPt).DotProduct(perp);
            double dvB = (wingB - tipPt).DotProduct(perp);

            // Hai cạnh mũi tên phải hướng cùng phía dọc trục ống và đối xứng 2 bên tâm đầu mũi tên, không kéo dài chạm hết 2 thành ống
            if (duA * duB <= 0.0 || Math.Abs(duA - duB) > 45.0 * MM_TO_FEET) return false;
            if (dvA * dvB >= 0.0) return false;
            double totalLatSpan = Math.Abs(dvA - dvB);
            if (totalLatSpan > halfWidthFeet * 1.55) return false;

            double tipU = tipPt.X * dir.X + tipPt.Y * dir.Y;
            double wingAU = wingA.X * dir.X + wingA.Y * dir.Y;
            double wingBU = wingB.X * dir.X + wingB.Y * dir.Y;
            arrowMinLong = Math.Min(tipU, Math.Min(wingAU, wingBU));
            arrowMaxLong = Math.Max(tipU, Math.Max(wingAU, wingBU));
            arrowMidLat = tipPt.X * perp.X + tipPt.Y * perp.Y;
            return true;
        }

        private static bool HasInternalFalseGeometryLines(
            CadDuctSegment seg,
            LineSpatialGrid lineGrid,
            int[] stamp,
            ref int stampCounter,
            List<CadExclusionZone> arrowZones)
        {
            if (seg == null || seg.ShapeKind != CadShapeKind.StraightDuct) return false;
            if (lineGrid == null || lineGrid.Items.Length == 0) return false;

            DuctAabb2D box = ComputeDuctSegmentAabb(seg);
            double pad = 35.0 * MM_TO_FEET;
            stampCounter++;
            List<int> nearby = QueryLineGrid(lineGrid, box.MinX - pad, box.MinY - pad, box.MaxX + pad, box.MaxY + pad, stamp, stampCounter);
            if (nearby.Count == 0) return false;

            XYZ dir = seg.Direction;
            if (dir == null || dir.GetLength() < 1e-6) dir = XYZ.BasisX;
            dir = new XYZ(dir.X, dir.Y, 0.0);
            if (dir.GetLength() < 1e-6) dir = XYZ.BasisX;
            dir = dir.Normalize();
            XYZ perp = new XYZ(-dir.Y, dir.X, 0.0);

            double minDimMm = Math.Min(seg.WidthMm, seg.Length);
            double minDiagLenFeet = Math.Max(75.0, minDimMm * 0.42) * MM_TO_FEET;
            double rectDiagFeet = Math.Sqrt(seg.WidthMm * seg.WidthMm + seg.Length * seg.Length) * MM_TO_FEET;
            double insideMarginFeet = 30.0 * MM_TO_FEET;

            double halfWidthFeet = Math.Max(seg.WidthMm, 50.0) * 0.5 * MM_TO_FEET;
            double halfLengthFeet = Math.Max(seg.Length, 50.0) * 0.5 * MM_TO_FEET;
            double centerLat = seg.CenterPoint.X * perp.X + seg.CenterPoint.Y * perp.Y;
            double centerLong = seg.CenterPoint.X * dir.X + seg.CenterPoint.Y * dir.Y;
            double minInternalParallelLenFeet = Math.Max(60.0, seg.Length * 0.25) * MM_TO_FEET;

            List<LineGridItem> matchedDiags = null;
            List<LineGridItem> matchedParallels = null;

            for (int k = 0; k < nearby.Count; k++)
            {
                LineGridItem it = lineGrid.Items[nearby[k]];
                if (it.LenFeet < 40.0 * MM_TO_FEET) continue;
                if (IsPointInsideAnyArrowZone((it.P0X + it.P1X) * 0.5, (it.P0Y + it.P1Y) * 0.5, arrowZones)) continue;

                double dot = Math.Abs(it.DirX * dir.X + it.DirY * dir.Y);

                // 1. Kiểm tra gạch chéo X / V / cánh van chéo NRD nằm bên trong hình chữ nhật (sẽ loại trừ mũi tên chỉ hướng gió V sau vòng lặp)
                if (dot > 0.05 && dot < 0.997 && it.LenFeet >= minDiagLenFeet)
                {
                    double lp0Diag = it.P0X * perp.X + it.P0Y * perp.Y;
                    double lp1Diag = it.P1X * perp.X + it.P1Y * perp.Y;
                    double latSpan = Math.Abs(lp1Diag - lp0Diag);
                    if (latSpan >= Math.Max(40.0 * MM_TO_FEET, halfWidthFeet * 0.45))
                    {
                        XYZ p0 = new XYZ(it.P0X, it.P0Y, 0.0);
                        XYZ p1 = new XYZ(it.P1X, it.P1Y, 0.0);
                        if (seg.ContainsPointXY(p0, insideMarginFeet) && seg.ContainsPointXY(p1, insideMarginFeet))
                        {
                            if (matchedDiags == null) matchedDiags = new List<LineGridItem>(4);
                            matchedDiags.Add(it);
                        }
                    }
                    continue;
                }

                // 2. Kiểm tra nhiều nét song song bên trong (cửa gió nan knm / nhiều vách tiêu âm ATT / nét hatch song song)
                if (dot >= 0.96 && it.LenFeet >= minInternalParallelLenFeet && halfWidthFeet > 35.0 * MM_TO_FEET)
                {
                    double lp0 = it.P0X * perp.X + it.P0Y * perp.Y;
                    double lp1 = it.P1X * perp.X + it.P1Y * perp.Y;
                    double relLat = ((lp0 + lp1) * 0.5) - centerLat;
                    if (Math.Abs(relLat) <= halfWidthFeet - 25.0 * MM_TO_FEET)
                    {
                        double la0 = (it.P0X * dir.X + it.P0Y * dir.Y) - centerLong;
                        double la1 = (it.P1X * dir.X + it.P1Y * dir.Y) - centerLong;
                        double lMin = Math.Min(la0, la1);
                        double lMax = Math.Max(la0, la1);
                        double ovl = Math.Min(lMax, halfLengthFeet + 15.0 * MM_TO_FEET) - Math.Max(lMin, -halfLengthFeet - 15.0 * MM_TO_FEET);
                        if (ovl >= minInternalParallelLenFeet)
                        {
                            if (matchedParallels == null) matchedParallels = new List<LineGridItem>(4);
                            matchedParallels.Add(it);
                        }
                    }
                }
            }

            // Nhận diện & bỏ qua các cặp nét đầu mũi tên chỉ hướng gió V (< hoặc >) bên trong ống gió
            List<double> arrowRangesMinU = null;
            List<double> arrowRangesMaxU = null;
            List<double> arrowMidsV = null;
            if (matchedDiags != null && matchedDiags.Count > 0)
            {
                bool[] isArrowDiag = new bool[matchedDiags.Count];
                for (int i = 0; i < matchedDiags.Count; i++)
                {
                    if (isArrowDiag[i]) continue;
                    for (int j = i + 1; j < matchedDiags.Count; j++)
                    {
                        if (isArrowDiag[j]) continue;
                        double minU, maxU, midV;
                        if (TryMatchArrowHeadPair(matchedDiags[i], matchedDiags[j], dir, perp, halfWidthFeet, out minU, out maxU, out midV))
                        {
                            isArrowDiag[i] = true;
                            isArrowDiag[j] = true;
                            if (arrowRangesMinU == null)
                            {
                                arrowRangesMinU = new List<double>();
                                arrowRangesMaxU = new List<double>();
                                arrowMidsV = new List<double>();
                            }
                            arrowRangesMinU.Add(minU);
                            arrowRangesMaxU.Add(maxU);
                            arrowMidsV.Add(midV);
                            break;
                        }
                    }
                }

                int remainingDiags = 0;
                for (int i = 0; i < matchedDiags.Count; i++)
                {
                    if (isArrowDiag[i]) continue;
                    LineGridItem d = matchedDiags[i];
                    double dLat = Math.Abs((d.P1X * perp.X + d.P1Y * perp.Y) - (d.P0X * perp.X + d.P0Y * perp.Y));
                    if (d.LenFeet >= rectDiagFeet * 0.65 && dLat >= halfWidthFeet * 1.15) return true;
                    remainingDiags++;
                    if (remainingDiags >= 2) return true;
                }
            }

            if (matchedParallels != null && matchedParallels.Count > 0)
            {
                List<double> parallelOffsets = new List<double>(4);
                for (int i = 0; i < matchedParallels.Count; i++)
                {
                    LineGridItem it = matchedParallels[i];
                    // Bỏ qua nét thân ngắn (<= 550mm) thuộc mũi tên hướng gió đã khớp ở bước trên
                    if (arrowRangesMinU != null && it.LenFeet <= 550.0 * MM_TO_FEET)
                    {
                        double mu = ((it.P0X + it.P1X) * 0.5) * dir.X + ((it.P0Y + it.P1Y) * 0.5) * dir.Y;
                        double mv = ((it.P0X + it.P1X) * 0.5) * perp.X + ((it.P0Y + it.P1Y) * 0.5) * perp.Y;
                        bool partOfArrowStem = false;
                        for (int ai = 0; ai < arrowRangesMinU.Count; ai++)
                        {
                            if (mu >= arrowRangesMinU[ai] - 600.0 * MM_TO_FEET
                                && mu <= arrowRangesMaxU[ai] + 600.0 * MM_TO_FEET
                                && Math.Abs(mv - arrowMidsV[ai]) <= 120.0 * MM_TO_FEET)
                            {
                                partOfArrowStem = true;
                                break;
                            }
                        }
                        if (partOfArrowStem) continue;
                    }

                    double lp0 = it.P0X * perp.X + it.P0Y * perp.Y;
                    double lp1 = it.P1X * perp.X + it.P1Y * perp.Y;
                    double relLat = ((lp0 + lp1) * 0.5) - centerLat;
                    bool dup = false;
                    for (int m = 0; m < parallelOffsets.Count; m++)
                    {
                        if (Math.Abs(parallelOffsets[m] - relLat) < 15.0 * MM_TO_FEET)
                        {
                            dup = true;
                            break;
                        }
                    }
                    if (!dup)
                    {
                        parallelOffsets.Add(relLat);
                        if (parallelOffsets.Count >= 3) return true;
                    }
                }

                if (parallelOffsets.Count == 2)
                {
                    double innerGapMm = Math.Abs(parallelOffsets[1] - parallelOffsets[0]) * FEET_TO_MM;
                    if (innerGapMm < 140.0) return true;
                }
            }

            return false;
        }

        private static void FilterNestedSubRectangles(List<CadDuctSegment> straightDucts)
        {
            int n = straightDucts.Count;
            if (n < 3) return;

            DuctAabb2D[] boxes = new DuctAabb2D[n];
            double[] areas = new double[n];
            int[] order = new int[n];
            for (int i = 0; i < n; i++)
            {
                boxes[i] = ComputeDuctSegmentAabb(straightDucts[i]);
                areas[i] = ComputeSegmentAreaFeet2(straightDucts[i]);
                order[i] = i;
            }
            Array.Sort(order, (a, b) => boxes[a].MinX.CompareTo(boxes[b].MinX));

            bool[] reject = new bool[n];
            double margin = 35.0 * MM_TO_FEET;

            for (int oi = 0; oi < n; oi++)
            {
                int i = order[oi];
                CadDuctSegment outer = straightDucts[i];
                if (outer.Length > 3500.0 || outer.WidthMm > 3500.0) continue;

                DuctAabb2D ob = boxes[i];
                double outerArea = areas[i];
                List<int> children = null;

                for (int oj = 0; oj < n; oj++)
                {
                    int j = order[oj];
                    if (i == j) continue;
                    DuctAabb2D ib = boxes[j];
                    if (ib.MinX > ob.MaxX + margin) break;
                    if (ib.MaxX < ob.MinX - margin) continue;
                    if (ib.MinY < ob.MinY - margin || ib.MaxY > ob.MaxY + margin) continue;
                    if (ib.MinX < ob.MinX - margin || ib.MaxX > ob.MaxX + margin) continue;
                    if (areas[j] >= outerArea * 0.65) continue;

                    CadDuctSegment inner = straightDucts[j];
                    if (outer.ContainsPointXY(inner.CenterPoint, margin)
                        && outer.ContainsPointXY(inner.StartPoint, margin)
                        && outer.ContainsPointXY(inner.EndPoint, margin))
                    {
                        if (children == null) children = new List<int>(4);
                        children.Add(j);
                    }
                }

                if (children != null && children.Count >= 2)
                {
                    // Đếm các hình con thực sự tách biệt (không phải trùng nhau) -> tiêu âm ATT có 4-5 vách tách biệt nằm cạnh nhau
                    int distinctCount = 0;
                    for (int a = 0; a < children.Count; a++)
                    {
                        bool overlapsPrev = false;
                        CadDuctSegment ca = straightDucts[children[a]];
                        for (int b = 0; b < a; b++)
                        {
                            CadDuctSegment cb = straightDucts[children[b]];
                            if (ca.CenterPoint.DistanceTo(cb.CenterPoint) < 50.0 * MM_TO_FEET)
                            {
                                overlapsPrev = true;
                                break;
                            }
                        }
                        if (!overlapsPrev) distinctCount++;
                    }

                    if (distinctCount >= 2)
                    {
                        reject[i] = true;
                        for (int c = 0; c < children.Count; c++)
                        {
                            reject[children[c]] = true;
                        }
                    }
                }
            }

            List<CadDuctSegment> kept = new List<CadDuctSegment>(n);
            for (int i = 0; i < n; i++)
            {
                if (!reject[i]) kept.Add(straightDucts[i]);
            }
            if (kept.Count < n)
            {
                straightDucts.Clear();
                straightDucts.AddRange(kept);
            }
        }

        private static void FilterTransverseDamperOrFlangeStrips(List<CadDuctSegment> straightDucts, List<CadDuctSegment> fittings)
        {
            int n = straightDucts.Count;
            if (n < 2) return;

            DuctAabb2D[] boxes = new DuctAabb2D[n];
            int[] order = new int[n];
            for (int i = 0; i < n; i++)
            {
                boxes[i] = ComputeDuctSegmentAabb(straightDucts[i]);
                order[i] = i;
            }
            Array.Sort(order, (a, b) => boxes[a].MinX.CompareTo(boxes[b].MinX));

            bool[] reject = new bool[n];
            double searchPad = 350.0 * MM_TO_FEET;
            double touchTol = 250.0 * MM_TO_FEET;

            for (int i = 0; i < n; i++)
            {
                CadDuctSegment s = straightDucts[i];
                if (s.WidthMm > 230.0 || s.Length < 400.0 || s.Length < s.WidthMm * 2.2) continue;

                XYZ sDir = s.Direction != null && s.Direction.GetLength() > 1e-6 ? s.Direction.Normalize() : XYZ.BasisX;
                DuctAabb2D sb = boxes[i];

                // Tìm ống chính vuông góc rộng hơn mà dải mỏng này vắt ngang qua (van MFD/NRD/OBD hoặc cổ bích miệng gió cuối nhánh)
                int hostIdx = -1;
                for (int oj = 0; oj < n; oj++)
                {
                    int j = order[oj];
                    if (j == i) continue;
                    DuctAabb2D mb = boxes[j];
                    if (mb.MinX > sb.MaxX + searchPad) break;
                    if (mb.MaxX < sb.MinX - searchPad) continue;
                    if (mb.MaxY < sb.MinY - searchPad || mb.MinY > sb.MaxY + searchPad) continue;

                    CadDuctSegment main = straightDucts[j];
                    if (!AreDuctSystemsCompatible(s, main)) continue;
                    if (main.WidthMm < 300.0 || main.WidthMm < s.WidthMm * 2.0) continue;
                    if (s.Length > main.WidthMm + 350.0 || main.WidthMm < s.Length - 260.0) continue;

                    XYZ mDir = main.Direction != null && main.Direction.GetLength() > 1e-6 ? main.Direction.Normalize() : XYZ.BasisX;
                    if (Math.Abs(sDir.DotProduct(mDir)) > 0.25) continue;

                    XYZ mPerp = new XYZ(-mDir.Y, mDir.X, 0.0);
                    double latCenter = Math.Abs((s.CenterPoint - main.CenterPoint).DotProduct(mPerp));
                    double maxCenterLat = Math.Max(180.0, main.WidthMm * 0.25) * MM_TO_FEET;
                    if (latCenter > maxCenterLat) continue;

                    double latStart = Math.Abs((s.StartPoint - main.CenterPoint).DotProduct(mPerp));
                    double latEnd = Math.Abs((s.EndPoint - main.CenterPoint).DotProduct(mPerp));
                    double maxEndLat = (main.WidthMm * 0.5 + 250.0) * MM_TO_FEET;
                    if (latStart > maxEndLat || latEnd > maxEndLat) continue;

                    double mainHalfLen = main.Length * 0.5 * MM_TO_FEET;
                    double longProj = Math.Abs((s.CenterPoint - main.CenterPoint).DotProduct(mDir));
                    if (longProj <= mainHalfLen + 250.0 * MM_TO_FEET)
                    {
                        hostIdx = j;
                        break;
                    }
                }

                if (hostIdx < 0) continue;

                // Bảo vệ: nếu đầu/cuối của s có nối với ống hoặc phụ kiện khác (không phải hostIdx) thì s là nhánh thật
                bool hasExternalConnection = false;
                for (int oj = 0; oj < n; oj++)
                {
                    int k = order[oj];
                    if (k == i || k == hostIdx) continue;
                    DuctAabb2D kb = boxes[k];
                    if (kb.MinX > sb.MaxX + touchTol) break;
                    if (kb.MaxX < sb.MinX - touchTol) continue;
                    if (kb.MaxY < sb.MinY - touchTol || kb.MinY > sb.MaxY + touchTol) continue;

                    CadDuctSegment other = straightDucts[k];
                    if (other.DistanceToPointXY(s.StartPoint) <= touchTol || other.DistanceToPointXY(s.EndPoint) <= touchTol)
                    {
                        hasExternalConnection = true;
                        break;
                    }
                }

                if (!hasExternalConnection && fittings != null)
                {
                    for (int fi = 0; fi < fittings.Count; fi++)
                    {
                        CadDuctSegment fit = fittings[fi];
                        if (fit.DistanceToPointXY(s.StartPoint) <= touchTol || fit.DistanceToPointXY(s.EndPoint) <= touchTol)
                        {
                            hasExternalConnection = true;
                            break;
                        }
                    }
                }

                if (!hasExternalConnection)
                {
                    reject[i] = true;
                }
            }

            List<CadDuctSegment> kept = new List<CadDuctSegment>(n);
            for (int i = 0; i < n; i++)
            {
                if (!reject[i]) kept.Add(straightDucts[i]);
            }
            if (kept.Count < n)
            {
                straightDucts.Clear();
                straightDucts.AddRange(kept);
            }
        }

        private struct DuctAabb2D
        {
            public double MinX;
            public double MinY;
            public double MaxX;
            public double MaxY;
        }

        private static DuctAabb2D ComputeDuctSegmentAabb(CadDuctSegment d)
        {
            DuctAabb2D box = new DuctAabb2D();
            box.MinX = double.MaxValue;
            box.MinY = double.MaxValue;
            box.MaxX = double.MinValue;
            box.MaxY = double.MinValue;

            if (d == null) return box;

            if (d.StartPoint != null)
            {
                if (d.StartPoint.X < box.MinX) box.MinX = d.StartPoint.X;
                if (d.StartPoint.Y < box.MinY) box.MinY = d.StartPoint.Y;
                if (d.StartPoint.X > box.MaxX) box.MaxX = d.StartPoint.X;
                if (d.StartPoint.Y > box.MaxY) box.MaxY = d.StartPoint.Y;
            }
            if (d.EndPoint != null)
            {
                if (d.EndPoint.X < box.MinX) box.MinX = d.EndPoint.X;
                if (d.EndPoint.Y < box.MinY) box.MinY = d.EndPoint.Y;
                if (d.EndPoint.X > box.MaxX) box.MaxX = d.EndPoint.X;
                if (d.EndPoint.Y > box.MaxY) box.MaxY = d.EndPoint.Y;
            }
            if (d.PolygonVertices != null && d.PolygonVertices.Count > 0)
            {
                for (int i = 0; i < d.PolygonVertices.Count; i++)
                {
                    XYZ p = d.PolygonVertices[i];
                    if (p.X < box.MinX) box.MinX = p.X;
                    if (p.Y < box.MinY) box.MinY = p.Y;
                    if (p.X > box.MaxX) box.MaxX = p.X;
                    if (p.Y > box.MaxY) box.MaxY = p.Y;
                }
            }
            else
            {
                double halfW = Math.Max(d.WidthMm, d.Width2Mm) * 0.5 * MM_TO_FEET;
                box.MinX -= halfW;
                box.MinY -= halfW;
                box.MaxX += halfW;
                box.MaxY += halfW;
            }
            return box;
        }

        private class ImplicitElbowCandidate
        {
            public int Idx1;
            public int Idx2;
            public CadDuctSegment D1;
            public CadDuctSegment D2;
            public XYZ End1;
            public XYZ End2;
            public XYZ Inter;
            public double TotalDist;
        }

        private static long MakePairCode(int i, int j)
        {
            int a = (i < j) ? i : j;
            int b = (i < j) ? j : i;
            return ((long)a << 32) | (uint)b;
        }

        private static bool IsTokenMatchIgnoreCase(string s, int start, int len, string targetUpper)
        {
            if (len != targetUpper.Length) return false;
            for (int i = 0; i < len; i++)
            {
                char c = s[start + i];
                if (c >= 'a' && c <= 'z') c = (char)(c - 32);
                if (c != targetUpper[i]) return false;
            }
            return true;
        }

        private static string ExtractHvacSystemCode(string layerName)
        {
            if (string.IsNullOrEmpty(layerName)) return string.Empty;
            int n = layerName.Length;
            int i = 0;
            while (i < n)
            {
                while (i < n)
                {
                    char c = layerName[i];
                    if (c == '-' || c == '_' || c == ' ' || c == '/' || c == '.' || c == '\\' || c == ':') i++;
                    else break;
                }
                if (i >= n) break;
                int start = i;
                while (i < n)
                {
                    char c = layerName[i];
                    if (c == '-' || c == '_' || c == ' ' || c == '/' || c == '.' || c == '\\' || c == ':') break;
                    i++;
                }
                int len = i - start;
                if (len == 1)
                {
                    char c = layerName[start];
                    if (c >= 'a' && c <= 'z') c = (char)(c - 32);
                    if (c == 'E') return "E";
                    if (c == 'O') return "O";
                    if (c == 'S') return "S";
                    if (c == 'R') return "R";
                    if (c == 'K') return "K";
                }
                else if (len >= 2 && len <= 7)
                {
                    if (IsTokenMatchIgnoreCase(layerName, start, len, "EA") ||
                        IsTokenMatchIgnoreCase(layerName, start, len, "EAD") ||
                        IsTokenMatchIgnoreCase(layerName, start, len, "EXH") ||
                        IsTokenMatchIgnoreCase(layerName, start, len, "EXHAUST") ||
                        IsTokenMatchIgnoreCase(layerName, start, len, "EF")) return "E";
                    if (IsTokenMatchIgnoreCase(layerName, start, len, "OA") ||
                        IsTokenMatchIgnoreCase(layerName, start, len, "OAD") ||
                        IsTokenMatchIgnoreCase(layerName, start, len, "FA") ||
                        IsTokenMatchIgnoreCase(layerName, start, len, "FAD") ||
                        IsTokenMatchIgnoreCase(layerName, start, len, "OUTDOOR") ||
                        IsTokenMatchIgnoreCase(layerName, start, len, "FRESH")) return "O";
                    if (IsTokenMatchIgnoreCase(layerName, start, len, "SA") ||
                        IsTokenMatchIgnoreCase(layerName, start, len, "SAD") ||
                        IsTokenMatchIgnoreCase(layerName, start, len, "SUP") ||
                        IsTokenMatchIgnoreCase(layerName, start, len, "SUPPLY") ||
                        IsTokenMatchIgnoreCase(layerName, start, len, "SF")) return "S";
                    if (IsTokenMatchIgnoreCase(layerName, start, len, "RA") ||
                        IsTokenMatchIgnoreCase(layerName, start, len, "RAD") ||
                        IsTokenMatchIgnoreCase(layerName, start, len, "RET") ||
                        IsTokenMatchIgnoreCase(layerName, start, len, "RETURN")) return "R";
                    if (IsTokenMatchIgnoreCase(layerName, start, len, "SE") ||
                        IsTokenMatchIgnoreCase(layerName, start, len, "SMK") ||
                        IsTokenMatchIgnoreCase(layerName, start, len, "SMOKE") ||
                        IsTokenMatchIgnoreCase(layerName, start, len, "PRESS") ||
                        IsTokenMatchIgnoreCase(layerName, start, len, "FUME")) return "K";
                }
            }
            return string.Empty;
        }

        private static bool AreColorsDistinctlyDifferent(int rgbA, int rgbB, bool sameKnownSystem)
        {
            if (rgbA < 0 || rgbB < 0) return false;

            int rA = (rgbA >> 16) & 0xFF;
            int gA = (rgbA >> 8) & 0xFF;
            int bA = rgbA & 0xFF;

            int rB = (rgbB >> 16) & 0xFF;
            int gB = (rgbB >> 8) & 0xFF;
            int bB = rgbB & 0xFF;

            int satA = Math.Max(rA, Math.Max(gA, bA)) - Math.Min(rA, Math.Min(gA, bA));
            int satB = Math.Max(rB, Math.Max(gB, bB)) - Math.Min(rB, Math.Min(gB, bB));
            if (satA < 35 && satB < 35) return false;

            int manhattan = Math.Abs(rA - rB) + Math.Abs(gA - gB) + Math.Abs(bA - bB);
            int threshold = sameKnownSystem ? 240 : 180;
            return manhattan >= threshold;
        }

        private static bool AreDuctSystemsCompatible(CadDuctSegment a, CadDuctSegment b)
        {
            if (a == null || b == null) return true;

            string sysA = ExtractHvacSystemCode(a.LayerName);
            string sysB = ExtractHvacSystemCode(b.LayerName);
            if (!string.IsNullOrEmpty(sysA) && !string.IsNullOrEmpty(sysB) &&
                !string.Equals(sysA, sysB, StringComparison.Ordinal))
            {
                return false;
            }

            bool sameKnownSystem = !string.IsNullOrEmpty(sysA) && string.Equals(sysA, sysB, StringComparison.Ordinal);
            if (AreColorsDistinctlyDifferent(a.ColorRgb, b.ColorRgb, sameKnownSystem))
            {
                return false;
            }

            return true;
        }

        private static bool DoesSegmentPierceThroughOther(CadDuctSegment seg, CadDuctSegment host)
        {
            if (seg == null || host == null || seg.Direction == null || host.Direction == null) return false;
            if (Math.Abs(seg.Direction.DotProduct(host.Direction)) >= 0.85) return false;

            XYZ hDir = new XYZ(host.Direction.X, host.Direction.Y, 0.0);
            if (hDir.GetLength() < 1e-6) return false;
            hDir = hDir.Normalize();
            XYZ hPerp = new XYZ(-hDir.Y, hDir.X, 0.0);

            double latS = (seg.StartPoint - host.CenterPoint).DotProduct(hPerp);
            double latE = (seg.EndPoint - host.CenterPoint).DotProduct(hPerp);
            double minLat = Math.Min(latS, latE);
            double maxLat = Math.Max(latS, latE);
            double halfHostFeet = (host.WidthMm * 0.5 + 60.0) * MM_TO_FEET;

            return (minLat < -halfHostFeet && maxLat > halfHostFeet);
        }

        private static bool DoSegmentsCrossThroughEachOtherXY(CadDuctSegment a, CadDuctSegment b)
        {
            return DoesSegmentPierceThroughOther(a, b) && DoesSegmentPierceThroughOther(b, a);
        }

        private static bool CanConnectDuctPair(
            CadDuctSegment a,
            CadDuctSegment b,
            HashSet<long> forbiddenPairCodes,
            Dictionary<string, int> ductIdxMap)
        {
            if (a == null || b == null || a == b) return false;
            if (!AreDuctSystemsCompatible(a, b)) return false;

            if (forbiddenPairCodes != null && ductIdxMap != null)
            {
                int idxA, idxB;
                if (ductIdxMap.TryGetValue(a.Id, out idxA) && ductIdxMap.TryGetValue(b.Id, out idxB))
                {
                    if (forbiddenPairCodes.Contains(MakePairCode(idxA, idxB))) return false;
                }
            }

            if (DoSegmentsCrossThroughEachOtherXY(a, b)) return false;
            return true;
        }

        private static HashSet<long> DetectOverpassUnderpassCrossings(
            List<CadDuctSegment> ducts,
            DuctAabb2D[] endBounds,
            DuctAabb2D[] fullBounds,
            Dictionary<string, int> ductIdxMap,
            HashSet<long> addedPairCodes,
            List<CadConnectionEdge> edges)
        {
            HashSet<long> forbiddenPairCodes = new HashSet<long>();
            int n = (ducts != null) ? ducts.Count : 0;
            if (n < 3) return forbiddenPairCodes;

            double minGapFeet = 150.0 * MM_TO_FEET;
            double maxGapFeet = 3800.0 * MM_TO_FEET;
            double searchReach = maxGapFeet + 600.0 * MM_TO_FEET;

            int[] endOrder = new int[n];
            int[] fullOrder = new int[n];
            for (int i = 0; i < n; i++)
            {
                endOrder[i] = i;
                fullOrder[i] = i;
            }
            Array.Sort(endOrder, delegate(int a, int b) { return endBounds[a].MinX.CompareTo(endBounds[b].MinX); });
            Array.Sort(fullOrder, delegate(int a, int b) { return fullBounds[a].MinX.CompareTo(fullBounds[b].MinX); });

            for (int oi = 0; oi < n; oi++)
            {
                int i = endOrder[oi];
                CadDuctSegment u1 = ducts[i];
                DuctAabb2D b1 = endBounds[i];
                double limitX = b1.MaxX + searchReach;

                for (int oj = oi + 1; oj < n; oj++)
                {
                    int j = endOrder[oj];
                    DuctAabb2D b2 = endBounds[j];
                    if (b2.MinX > limitX) break;
                    if (b1.MaxY + searchReach < b2.MinY || b2.MaxY + searchReach < b1.MinY)
                    {
                        continue;
                    }

                    CadDuctSegment u2 = ducts[j];
                    if (!AreDuctSystemsCompatible(u1, u2)) continue;

                    double colDot = Math.Abs(u1.Direction.DotProduct(u2.Direction));
                    if (colDot < 0.94) continue;

                    double wDiff = Math.Abs(u1.WidthMm - u2.WidthMm);
                    if (wDiff > 150.0) continue;

                    double perpGap = LineToLineDistance(u1.StartPoint, u1.Direction, u2.StartPoint, u2.Direction);
                    if (perpGap > 120.0 * MM_TO_FEET) continue;

                    XYZ f1, far1, f2, far2;
                    double gapDist = FindClosestEndpointPairXY(u1, u2, out f1, out far1, out f2, out far2);
                    if (gapDist < minGapFeet || gapDist > maxGapFeet) continue;

                    XYZ out1 = new XYZ(f1.X - far1.X, f1.Y - far1.Y, 0.0);
                    XYZ out2 = new XYZ(f2.X - far2.X, f2.Y - far2.Y, 0.0);
                    XYZ f1ToF2 = new XYZ(f2.X - f1.X, f2.Y - f1.Y, 0.0);
                    if (out1.GetLength() < 1e-6 || out2.GetLength() < 1e-6 || f1ToF2.GetLength() < 1e-6) continue;
                    out1 = out1.Normalize();
                    out2 = out2.Normalize();
                    XYZ fDir = f1ToF2.Normalize();
                    if (out1.DotProduct(fDir) < 0.7 || out2.DotProduct(fDir) > -0.7) continue;

                    double padM = 250.0 * MM_TO_FEET;
                    double gMinX = Math.Min(f1.X, f2.X) - padM;
                    double gMaxX = Math.Max(f1.X, f2.X) + padM;
                    double gMinY = Math.Min(f1.Y, f2.Y) - padM;
                    double gMaxY = Math.Max(f1.Y, f2.Y) + padM;

                    for (int ok = 0; ok < n; ok++)
                    {
                        int k = fullOrder[ok];
                        DuctAabb2D mb = fullBounds[k];
                        if (mb.MinX > gMaxX) break;
                        if (k == i || k == j) continue;
                        if (mb.MaxX < gMinX || mb.MaxY < gMinY || mb.MinY > gMaxY) continue;

                        CadDuctSegment m = ducts[k];
                        if (Math.Abs(m.Direction.DotProduct(u1.Direction)) >= 0.45) continue;
                        if (gapDist < m.WidthMm * 0.65 * MM_TO_FEET || gapDist > (m.WidthMm + 600.0) * MM_TO_FEET) continue;

                        XYZ inter = LineLineIntersectXY(m.StartPoint, m.Direction, u1.StartPoint, u1.Direction, m.StartPoint.Z);
                        if (inter == null) continue;

                        XYZ mDir = new XYZ(m.Direction.X, m.Direction.Y, 0.0);
                        if (mDir.GetLength() < 1e-6) continue;
                        mDir = mDir.Normalize();
                        XYZ mPerp = new XYZ(-mDir.Y, mDir.X, 0.0);

                        double pS = (m.StartPoint - m.CenterPoint).DotProduct(mDir);
                        double pE = (m.EndPoint - m.CenterPoint).DotProduct(mDir);
                        double pInter = (inter - m.CenterPoint).DotProduct(mDir);
                        double marginM = (Math.Max(u1.WidthMm, u2.WidthMm) * 0.5 + 30.0) * MM_TO_FEET;
                        if (pInter < Math.Min(pS, pE) + marginM || pInter > Math.Max(pS, pE) - marginM) continue;

                        double latF1 = (f1 - m.CenterPoint).DotProduct(mPerp);
                        double latF2 = (f2 - m.CenterPoint).DotProduct(mPerp);
                        if (latF1 * latF2 >= 0.0) continue;

                        double minWallClear = m.WidthMm * 0.35 * MM_TO_FEET;
                        if (Math.Abs(latF1) < minWallClear || Math.Abs(latF2) < minWallClear) continue;

                        bool sameSys = AreDuctSystemsCompatible(u1, m) && AreDuctSystemsCompatible(u2, m);
                        if (sameSys && Math.Min(u1.WidthMm, u2.WidthMm) <= m.WidthMm + 50.0) continue;

                        forbiddenPairCodes.Add(MakePairCode(i, k));
                        forbiddenPairCodes.Add(MakePairCode(j, k));

                        CadConnectionKind bridgeKind = (wDiff >= 15.0)
                            ? CadConnectionKind.Transition
                            : CadConnectionKind.CollinearDirect;
                        AddEdgeUniqueByIndex(edges, addedPairCodes, ductIdxMap, u1, u2, bridgeKind, null, (f1 + f2) * 0.5, forbiddenPairCodes);
                    }
                }
            }

            return forbiddenPairCodes;
        }

        private static void AddEdgeUniqueByIndex(
            List<CadConnectionEdge> edges,
            HashSet<long> addedPairCodes,
            Dictionary<string, int> ductIdxMap,
            CadDuctSegment a,
            CadDuctSegment b,
            CadConnectionKind kind,
            CadDuctSegment bridge,
            XYZ junction)
        {
            AddEdgeUniqueByIndex(edges, addedPairCodes, ductIdxMap, a, b, kind, bridge, junction, null);
        }

        private static void AddEdgeUniqueByIndex(
            List<CadConnectionEdge> edges,
            HashSet<long> addedPairCodes,
            Dictionary<string, int> ductIdxMap,
            CadDuctSegment a,
            CadDuctSegment b,
            CadConnectionKind kind,
            CadDuctSegment bridge,
            XYZ junction,
            HashSet<long> forbiddenPairCodes)
        {
            int idxA, idxB;
            if (!ductIdxMap.TryGetValue(a.Id, out idxA) || !ductIdxMap.TryGetValue(b.Id, out idxB)) return;
            if (!CanConnectDuctPair(a, b, forbiddenPairCodes, ductIdxMap)) return;
            long code = MakePairCode(idxA, idxB);
            if (addedPairCodes.Contains(code)) return;
            addedPairCodes.Add(code);
            edges.Add(new CadConnectionEdge(a, b, kind, bridge, junction));
        }

        private static List<CadDuctSegment> FilterTouchingDuctsForFitting(CadDuctSegment fit, List<CadDuctSegment> touching)
        {
            if (touching == null || touching.Count == 0) return touching;

            List<CadDuctSegment> result = new List<CadDuctSegment>(touching.Count);
            bool fitHasIdentity = fit != null && (!string.IsNullOrEmpty(ExtractHvacSystemCode(fit.LayerName)) || fit.ColorRgb >= 0);
            if (fitHasIdentity)
            {
                for (int i = 0; i < touching.Count; i++)
                {
                    if (AreDuctSystemsCompatible(fit, touching[i]))
                    {
                        result.Add(touching[i]);
                    }
                }
                if (result.Count == 0)
                {
                    result.AddRange(touching);
                }
            }
            else
            {
                result.AddRange(touching);
            }

            if (result.Count <= 1) return result;

            bool allMutual = true;
            for (int i = 0; i < result.Count && allMutual; i++)
            {
                for (int j = i + 1; j < result.Count; j++)
                {
                    if (!AreDuctSystemsCompatible(result[i], result[j]))
                    {
                        allMutual = false;
                        break;
                    }
                }
            }
            if (allMutual) return result;

            List<CadDuctSegment> bestCluster = result;
            double bestScore = -1.0;
            for (int i = 0; i < result.Count; i++)
            {
                CadDuctSegment anchor = result[i];
                List<CadDuctSegment> cluster = new List<CadDuctSegment>();
                double widthSum = 0.0;
                for (int j = 0; j < result.Count; j++)
                {
                    if (AreDuctSystemsCompatible(anchor, result[j]))
                    {
                        cluster.Add(result[j]);
                        widthSum += result[j].WidthMm;
                    }
                }
                double score = cluster.Count * 100000.0 + widthSum;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestCluster = cluster;
                }
            }
            return bestCluster;
        }

        private static List<CadConnectionEdge> BuildGeometricTopology(
            List<CadDuctSegment> ducts,
            List<CadDuctSegment> fittings)
        {
            List<CadConnectionEdge> edges = new List<CadConnectionEdge>();
            HashSet<long> addedPairCodes = new HashSet<long>();
            int ductCount = (ducts != null) ? ducts.Count : 0;
            Dictionary<string, int> ductIdxMap = new Dictionary<string, int>(ductCount);
            DuctAabb2D[] endBounds = new DuctAabb2D[ductCount];
            DuctAabb2D[] fullBounds = new DuctAabb2D[ductCount];

            for (int i = 0; i < ductCount; i++)
            {
                CadDuctSegment d = ducts[i];
                ductIdxMap[d.Id] = i;

                DuctAabb2D eb = new DuctAabb2D();
                eb.MinX = Math.Min(d.StartPoint.X, d.EndPoint.X);
                eb.MinY = Math.Min(d.StartPoint.Y, d.EndPoint.Y);
                eb.MaxX = Math.Max(d.StartPoint.X, d.EndPoint.X);
                eb.MaxY = Math.Max(d.StartPoint.Y, d.EndPoint.Y);
                endBounds[i] = eb;

                fullBounds[i] = ComputeDuctSegmentAabb(d);
            }

            // BƯỚC 0: Phát hiện các giao cắt khác cốt (chui dưới / vượt trên bị ngắt ở 2 vách ống băng ngang) để nối thẳng liền mạch & cấm rẽ nhầm
            HashSet<long> forbiddenCrossingPairs = DetectOverpassUnderpassCrossings(
                ducts, endBounds, fullBounds, ductIdxMap, addedPairCodes, edges);

            double baseTouchTolFeet = 120.0 * MM_TO_FEET;

            // BƯỚC 1: Kết nối qua các hình phụ kiện nhận diện được (Transition, Curved/Mitered Elbow, TeeBranch)
            foreach (CadDuctSegment fit in fittings)
            {
                if (fit.ShapeKind == CadShapeKind.Transition)
                {
                    CadDuctSegment mainSideDuct = null;
                    CadDuctSegment smallEndDuct = null;
                    CadDuctSegment largeEndDuct = null;

                    double fsx = fit.StartPoint.X, fsy = fit.StartPoint.Y;
                    double fex = fit.EndPoint.X, fey = fit.EndPoint.Y;
                    double fMinX = Math.Min(fsx, fex), fMaxX = Math.Max(fsx, fex);
                    double fMinY = Math.Min(fsy, fey), fMaxY = Math.Max(fsy, fey);

                    for (int i = 0; i < ductCount; i++)
                    {
                        CadDuctSegment d = ducts[i];
                        if (!AreDuctSystemsCompatible(fit, d)) continue;
                        double margin = (d.WidthMm * 0.5 * MM_TO_FEET) + baseTouchTolFeet;
                        DuctAabb2D eb = endBounds[i];
                        if (fMaxX + margin < eb.MinX || fMinX - margin > eb.MaxX ||
                            fMaxY + margin < eb.MinY || fMinY - margin > eb.MaxY)
                        {
                            continue;
                        }

                        if (d.StartPoint.DistanceTo(fit.EndPoint) <= baseTouchTolFeet || d.EndPoint.DistanceTo(fit.EndPoint) <= baseTouchTolFeet)
                        {
                            if (Math.Abs(d.WidthMm - fit.Width2Mm) <= 60.0)
                            {
                                smallEndDuct = d;
                            }
                        }

                        if (d.StartPoint.DistanceTo(fit.StartPoint) <= baseTouchTolFeet || d.EndPoint.DistanceTo(fit.StartPoint) <= baseTouchTolFeet)
                        {
                            if (Math.Abs(d.WidthMm - fit.WidthMm) <= 60.0)
                            {
                                largeEndDuct = d;
                            }
                        }

                        double distToAxis = d.DistanceToPointXY(fit.StartPoint);
                        double halfMainW = d.WidthMm * 0.5 * MM_TO_FEET;
                        bool alignedWithMainWall = (fit.TapBevelDirection != null)
                            ? (Math.Abs(d.Direction.DotProduct(fit.TapBevelDirection)) >= 0.92 && Math.Abs(d.Direction.DotProduct(fit.Direction)) < 0.65)
                            : (Math.Abs(d.Direction.DotProduct(fit.Direction)) < 0.60);
                        if (Math.Abs(distToAxis - halfMainW) <= baseTouchTolFeet
                            && d.WidthMm >= fit.Width2Mm + 20.0
                            && alignedWithMainWall)
                        {
                            mainSideDuct = d;
                        }
                    }

                    if (mainSideDuct != null && smallEndDuct != null && mainSideDuct != smallEndDuct
                        && CanConnectDuctPair(mainSideDuct, smallEndDuct, forbiddenCrossingPairs, ductIdxMap))
                    {
                        fit.ShapeKind = CadShapeKind.TakeoffTap;
                        XYZ tapPt = ProjectPointOnSegmentXY(smallEndDuct.CenterPoint, mainSideDuct.StartPoint, mainSideDuct.EndPoint, mainSideDuct.CenterElevationMm * MM_TO_FEET);
                        AddEdgeUniqueByIndex(edges, addedPairCodes, ductIdxMap, mainSideDuct, smallEndDuct, CadConnectionKind.TakeoffTap, fit, tapPt, forbiddenCrossingPairs);
                        continue;
                    }

                    if (largeEndDuct != null && smallEndDuct != null && largeEndDuct != smallEndDuct
                        && CanConnectDuctPair(largeEndDuct, smallEndDuct, forbiddenCrossingPairs, ductIdxMap))
                    {
                        AddEdgeUniqueByIndex(edges, addedPairCodes, ductIdxMap, largeEndDuct, smallEndDuct, CadConnectionKind.Transition, fit, fit.CenterPoint, forbiddenCrossingPairs);
                        continue;
                    }
                }
                else if (fit.ShapeKind == CadShapeKind.Elbow)
                {
                    List<CadDuctSegment> touching = FilterTouchingDuctsForFitting(fit, FindElbowTouchingDucts(fit, ducts, baseTouchTolFeet));
                    if (touching.Count < 2 && fit.WidthMm > 0)
                    {
                        double expandedTol = Math.Min(350.0, Math.Max(150.0, fit.WidthMm * 0.18)) * MM_TO_FEET;
                        touching = FilterTouchingDuctsForFitting(fit, FindElbowTouchingDucts(fit, ducts, expandedTol));
                    }
                    if (touching.Count >= 2)
                    {
                        for (int i = 0; i < touching.Count; i++)
                        {
                            for (int j = i + 1; j < touching.Count; j++)
                            {
                                CadDuctSegment d1 = touching[i];
                                CadDuctSegment d2 = touching[j];
                                if (Math.Abs(d1.Direction.DotProduct(d2.Direction)) < 0.85
                                    && CanConnectDuctPair(d1, d2, forbiddenCrossingPairs, ductIdxMap))
                                {
                                    XYZ corner = LineLineIntersectXY(d1.StartPoint, d1.Direction, d2.StartPoint, d2.Direction, d1.StartPoint.Z);
                                    if (corner == null) corner = fit.CenterPoint;
                                    AddEdgeUniqueByIndex(edges, addedPairCodes, ductIdxMap, d1, d2, CadConnectionKind.Elbow, fit, corner, forbiddenCrossingPairs);
                                }
                            }
                        }
                    }
                }
                else if (fit.ShapeKind == CadShapeKind.TeeBranch)
                {
                    List<CadDuctSegment> touching = FilterTouchingDuctsForFitting(fit, FindDuctsTouchingPolygon(fit, ducts, baseTouchTolFeet));
                    if (touching.Count < 2)
                    {
                        touching = FilterTouchingDuctsForFitting(fit, FindDuctsTouchingPolygon(fit, ducts, 250.0 * MM_TO_FEET));
                    }

                    if (touching.Count == 1)
                    {
                        // Trường hợp ống chính chạy xuyên suốt qua cổ chân rẽ (chỉ có 1 nhánh chạm đa giác phụ kiện)
                        CadDuctSegment brOnly = touching[0];
                        CadDuctSegment contMain = FindContinuousMainDuctPassingPolygon(fit, brOnly, ducts, forbiddenCrossingPairs, ductIdxMap);
                        if (contMain != null)
                        {
                            XYZ juncTap = LineLineIntersectXY(contMain.StartPoint, contMain.Direction, brOnly.StartPoint, brOnly.Direction, contMain.StartPoint.Z);
                            if (juncTap == null) juncTap = fit.CenterPoint;
                            fit.ShapeKind = CadShapeKind.TakeoffTap;
                            AddEdgeUniqueByIndex(edges, addedPairCodes, ductIdxMap, contMain, brOnly, CadConnectionKind.TakeoffTap, fit, juncTap, forbiddenCrossingPairs);
                        }
                    }
                    else if (touching.Count == 2)
                    {
                        CadDuctSegment d1 = touching[0];
                        CadDuctSegment d2 = touching[1];
                        if (CanConnectDuctPair(d1, d2, forbiddenCrossingPairs, ductIdxMap))
                        {
                            double dot = Math.Abs(d1.Direction.DotProduct(d2.Direction));
                            double wDiff = Math.Abs(d1.WidthMm - d2.WidthMm);

                            if (dot < 0.85 && wDiff <= 100.0)
                            {
                                // Đa giác cong / đa giác 5..250 đỉnh chạm ĐÚNG 2 ống không thẳng hàng cùng bề rộng -> Cút chuyển hướng (Elbow)!
                                fit.ShapeKind = CadShapeKind.Elbow;
                                fit.WidthMm = SnapStandardDuctWidthMm((d1.WidthMm + d2.WidthMm) * 0.5);
                                fit.Width2Mm = fit.WidthMm;
                                XYZ corner = LineLineIntersectXY(d1.StartPoint, d1.Direction, d2.StartPoint, d2.Direction, d1.StartPoint.Z);
                                if (corner == null) corner = fit.CenterPoint;
                                AddEdgeUniqueByIndex(edges, addedPairCodes, ductIdxMap, d1, d2, CadConnectionKind.Elbow, fit, corner, forbiddenCrossingPairs);
                            }
                            else if (dot >= 0.85 && wDiff >= 15.0)
                            {
                                fit.ShapeKind = CadShapeKind.Transition;
                                AddEdgeUniqueByIndex(edges, addedPairCodes, ductIdxMap, d1, d2, CadConnectionKind.Transition, fit, fit.CenterPoint, forbiddenCrossingPairs);
                            }
                            else if (dot >= 0.85)
                            {
                                AddEdgeUniqueByIndex(edges, addedPairCodes, ductIdxMap, d1, d2, CadConnectionKind.CollinearDirect, fit, fit.CenterPoint, forbiddenCrossingPairs);
                            }
                            else
                            {
                                CadDuctSegment mainD = (d1.WidthMm >= d2.WidthMm) ? d1 : d2;
                                CadDuctSegment brD = (mainD == d1) ? d2 : d1;
                                XYZ junc = LineLineIntersectXY(mainD.StartPoint, mainD.Direction, brD.StartPoint, brD.Direction, mainD.StartPoint.Z);
                                if (junc == null) junc = fit.CenterPoint;
                                fit.ShapeKind = CadShapeKind.TakeoffTap;
                                AddEdgeUniqueByIndex(edges, addedPairCodes, ductIdxMap, mainD, brD, CadConnectionKind.TakeoffTap, fit, junc, forbiddenCrossingPairs);
                            }
                        }
                    }
                    else if (touching.Count >= 3)
                    {
                        // Phân tách thông minh kiểu kỹ sư BIM: tìm 2 ống chính thẳng hàng -> nối liền mạch (CollinearDirect/Transition) + nhánh phụ đâm Chân rẽ (TakeoffTap), không ép vẽ Y gió!
                        CadDuctSegment mainA = null, mainB = null;
                        double bestPairScore = -1.0;
                        for (int ti = 0; ti < touching.Count; ti++)
                        {
                            for (int tj = ti + 1; tj < touching.Count; tj++)
                            {
                                CadDuctSegment da = touching[ti];
                                CadDuctSegment db = touching[tj];
                                if (!CanConnectDuctPair(da, db, forbiddenCrossingPairs, ductIdxMap)) continue;
                                double colDot = Math.Abs(da.Direction.DotProduct(db.Direction));
                                if (colDot >= 0.85)
                                {
                                    double score = da.WidthMm + db.WidthMm + colDot * 100.0;
                                    if (score > bestPairScore)
                                    {
                                        bestPairScore = score;
                                        mainA = da;
                                        mainB = db;
                                    }
                                }
                            }
                        }

                        if (mainA != null && mainB != null)
                        {
                            CadDuctSegment mainPrimary = (mainA.WidthMm > mainB.WidthMm || (Math.Abs(mainA.WidthMm - mainB.WidthMm) < 1.0 && mainA.Length >= mainB.Length))
                                ? mainA : mainB;
                            CadDuctSegment mainSecondary = (mainPrimary == mainA) ? mainB : mainA;

                            if (Math.Abs(mainPrimary.WidthMm - mainSecondary.WidthMm) >= 15.0)
                            {
                                AddEdgeUniqueByIndex(edges, addedPairCodes, ductIdxMap, mainPrimary, mainSecondary, CadConnectionKind.Transition, fit, GetClosestEndpointsMid(mainPrimary, mainSecondary), forbiddenCrossingPairs);
                            }
                            else
                            {
                                AddEdgeUniqueByIndex(edges, addedPairCodes, ductIdxMap, mainPrimary, mainSecondary, CadConnectionKind.CollinearDirect, fit, GetClosestEndpointsMid(mainPrimary, mainSecondary), forbiddenCrossingPairs);
                            }

                            fit.ShapeKind = CadShapeKind.TakeoffTap;
                            for (int ti = 0; ti < touching.Count; ti++)
                            {
                                CadDuctSegment br = touching[ti];
                                if (br == mainPrimary || br == mainSecondary) continue;
                                if (!CanConnectDuctPair(mainPrimary, br, forbiddenCrossingPairs, ductIdxMap)) continue;
                                XYZ tapPt = LineLineIntersectXY(mainPrimary.StartPoint, mainPrimary.Direction, br.StartPoint, br.Direction, mainPrimary.StartPoint.Z);
                                if (tapPt == null) tapPt = fit.CenterPoint;
                                AddEdgeUniqueByIndex(edges, addedPairCodes, ductIdxMap, mainPrimary, br, CadConnectionKind.TakeoffTap, fit, tapPt, forbiddenCrossingPairs);
                            }
                        }
                        else
                        {
                            // Chạc chia / chữ Y xiên góc không có cặp thẳng hàng rõ rệt: giữ ống lớn nhất làm ống chính kéo dài qua nút giao + gắn các nhánh còn lại bằng Chân rẽ (TakeoffTap), không vẽ Y gió!
                            fit.ShapeKind = CadShapeKind.TakeoffTap;
                            touching.Sort(delegate(CadDuctSegment a, CadDuctSegment b)
                            {
                                int cmp = b.WidthMm.CompareTo(a.WidthMm);
                                return (cmp != 0) ? cmp : b.Length.CompareTo(a.Length);
                            });
                            CadDuctSegment mainDuct = touching[0];
                            for (int i = 1; i < touching.Count; i++)
                            {
                                CadDuctSegment br = touching[i];
                                if (!CanConnectDuctPair(mainDuct, br, forbiddenCrossingPairs, ductIdxMap)) continue;
                                XYZ junc = LineLineIntersectXY(mainDuct.StartPoint, mainDuct.Direction, br.StartPoint, br.Direction, mainDuct.StartPoint.Z);
                                if (junc == null) junc = fit.CenterPoint;
                                AddEdgeUniqueByIndex(edges, addedPairCodes, ductIdxMap, mainDuct, br, CadConnectionKind.TakeoffTap, fit, junc, forbiddenCrossingPairs);
                            }
                        }
                    }
                }
            }

            // BƯỚC 2: Nối thẳng hàng (CollinearDirect / Transition) & Chân rẽ trực tiếp (TakeoffTap)
            double directEndTolFeet = 180.0 * MM_TO_FEET;

            for (int i = 0; i < ductCount; i++)
            {
                CadDuctSegment d1 = ducts[i];
                DuctAabb2D b1 = endBounds[i];
                double pad1 = (d1.WidthMm * 0.5 + 450.0) * MM_TO_FEET;

                for (int j = i + 1; j < ductCount; j++)
                {
                    CadDuctSegment d2 = ducts[j];
                    DuctAabb2D b2 = endBounds[j];
                    double maxPad = pad1 + (d2.WidthMm * 0.5 * MM_TO_FEET);
                    if (b1.MaxX + maxPad < b2.MinX || b2.MaxX + maxPad < b1.MinX ||
                        b1.MaxY + maxPad < b2.MinY || b2.MaxY + maxPad < b1.MinY)
                    {
                        continue;
                    }

                    long pairCode = MakePairCode(i, j);
                    if (addedPairCodes.Contains(pairCode)) continue;
                    if (!CanConnectDuctPair(d1, d2, forbiddenCrossingPairs, ductIdxMap)) continue;

                    double dot = Math.Abs(d1.Direction.DotProduct(d2.Direction));

                    if (dot > 0.96)
                    {
                        double minEndDist = MinEndpointDistanceXY(d1, d2);
                        if (minEndDist <= directEndTolFeet)
                        {
                            double perpGap = LineToLineDistance(d1.StartPoint, d1.Direction, d2.StartPoint, d2.Direction);
                            double wDiff = Math.Abs(d1.WidthMm - d2.WidthMm);
                            double maxPerpTol = (wDiff >= 15.0)
                                ? Math.Max(120.0, wDiff * 0.55 + 60.0) * MM_TO_FEET
                                : 100.0 * MM_TO_FEET;

                            if (perpGap <= maxPerpTol)
                            {
                                if (wDiff >= 15.0)
                                {
                                    AddEdgeUniqueByIndex(edges, addedPairCodes, ductIdxMap, d1, d2, CadConnectionKind.Transition, null, GetClosestEndpointsMid(d1, d2), forbiddenCrossingPairs);
                                }
                                else
                                {
                                    AddEdgeUniqueByIndex(edges, addedPairCodes, ductIdxMap, d1, d2, CadConnectionKind.CollinearDirect, null, GetClosestEndpointsMid(d1, d2), forbiddenCrossingPairs);
                                }
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
                            AddEdgeUniqueByIndex(edges, addedPairCodes, ductIdxMap, d1, d2, CadConnectionKind.TakeoffTap, null, inter, forbiddenCrossingPairs);
                        }
                        else if (d2DistToBody < 50.0 * MM_TO_FEET && d2.WidthMm > d1.WidthMm + 20.0
                            && minD1ToInter <= (d2.WidthMm * 0.5 + 350.0) * MM_TO_FEET
                            && d2.StartPoint.DistanceTo(inter) > 120.0 * MM_TO_FEET
                            && d2.EndPoint.DistanceTo(inter) > 120.0 * MM_TO_FEET)
                        {
                            AddEdgeUniqueByIndex(edges, addedPairCodes, ductIdxMap, d2, d1, CadConnectionKind.TakeoffTap, null, inter, forbiddenCrossingPairs);
                        }
                    }
                }
            }

            // BƯỚC 2.5: Bắt các ngã ba giật cấp mở (không có đa giác kín bọc nút giao): 2 ống chính thẳng hàng + nhánh phụ kẹp giữa -> Transition/Collinear + TakeoffTap
            ConnectOpenSteppedThreeWayJunctions(ducts, endBounds, ductIdxMap, addedPairCodes, edges, forbiddenCrossingPairs);

            // BƯỚC 3: Kết nối Cút ngầm định (tự co giãn ngưỡng theo bề rộng ống để cút cong 1900x400 tới ~4.6m vẫn nối chính xác, tránh nối nhầm qua ống trung gian)
            List<ImplicitElbowCandidate> elbowCandidates = new List<ImplicitElbowCandidate>();
            for (int i = 0; i < ductCount; i++)
            {
                CadDuctSegment d1 = ducts[i];
                DuctAabb2D b1 = endBounds[i];

                for (int j = i + 1; j < ductCount; j++)
                {
                    CadDuctSegment d2 = ducts[j];
                    double dot = Math.Abs(d1.Direction.DotProduct(d2.Direction));
                    if (dot >= 0.78) continue;

                    double widthDiff = Math.Abs(d1.WidthMm - d2.WidthMm);
                    if (widthDiff > 100.0 && dot >= 0.25) continue;

                    double elbowGapTolFeet = (widthDiff <= 100.0)
                        ? ((Math.Max(d1.WidthMm, d2.WidthMm) * 1.8 + 1200.0) * MM_TO_FEET)
                        : (600.0 * MM_TO_FEET);

                    DuctAabb2D b2 = endBounds[j];
                    double reach = elbowGapTolFeet * 2.0;
                    if (b1.MaxX + reach < b2.MinX || b2.MaxX + reach < b1.MinX ||
                        b1.MaxY + reach < b2.MinY || b2.MaxY + reach < b1.MinY)
                    {
                        continue;
                    }

                    long pairCode = MakePairCode(i, j);
                    if (addedPairCodes.Contains(pairCode)) continue;
                    if (!CanConnectDuctPair(d1, d2, forbiddenCrossingPairs, ductIdxMap)) continue;

                    XYZ inter = LineLineIntersectXY(d1.StartPoint, d1.Direction, d2.StartPoint, d2.Direction, d1.StartPoint.Z);
                    if (inter == null) continue;

                    double ds1 = DistXY(d1.StartPoint, inter);
                    double de1 = DistXY(d1.EndPoint, inter);
                    XYZ end1 = (ds1 <= de1) ? d1.StartPoint : d1.EndPoint;
                    XYZ far1 = (ds1 <= de1) ? d1.EndPoint : d1.StartPoint;
                    double minD1ToInter = Math.Min(ds1, de1);

                    double ds2 = DistXY(d2.StartPoint, inter);
                    double de2 = DistXY(d2.EndPoint, inter);
                    XYZ end2 = (ds2 <= de2) ? d2.StartPoint : d2.EndPoint;
                    XYZ far2 = (ds2 <= de2) ? d2.EndPoint : d2.StartPoint;
                    double minD2ToInter = Math.Min(ds2, de2);

                    if (minD1ToInter > elbowGapTolFeet || minD2ToInter > elbowGapTolFeet) continue;

                    XYZ vec1 = end1 - far1;
                    XYZ vec2 = end2 - far2;
                    if (vec1.GetLength() < 1e-6 || vec2.GetLength() < 1e-6) continue;
                    XYZ out1 = new XYZ(vec1.X, vec1.Y, 0.0).Normalize();
                    XYZ out2 = new XYZ(vec2.X, vec2.Y, 0.0).Normalize();
                    XYZ toInter1 = new XYZ(inter.X - end1.X, inter.Y - end1.Y, 0.0);
                    XYZ toInter2 = new XYZ(inter.X - end2.X, inter.Y - end2.Y, 0.0);

                    if (toInter1.DotProduct(out1) < -(d1.WidthMm * 0.6) * MM_TO_FEET) continue;
                    if (toInter2.DotProduct(out2) < -(d2.WidthMm * 0.6) * MM_TO_FEET) continue;

                    if (HasIntermediateDuctBlocking(d1, end1, d2, end2, inter, ducts, fullBounds)) continue;

                    ImplicitElbowCandidate cand = new ImplicitElbowCandidate();
                    cand.Idx1 = i;
                    cand.Idx2 = j;
                    cand.D1 = d1;
                    cand.D2 = d2;
                    cand.End1 = end1;
                    cand.End2 = end2;
                    cand.Inter = inter;
                    cand.TotalDist = minD1ToInter + minD2ToInter;
                    elbowCandidates.Add(cand);
                }
            }

            elbowCandidates.Sort(delegate(ImplicitElbowCandidate a, ImplicitElbowCandidate b)
            {
                return a.TotalDist.CompareTo(b.TotalDist);
            });

            foreach (ImplicitElbowCandidate cand in elbowCandidates)
            {
                long pairCode = MakePairCode(cand.Idx1, cand.Idx2);
                if (addedPairCodes.Contains(pairCode)) continue;
                if (IsEndpointAlreadyConnected(cand.D1, cand.End1, edges)) continue;
                if (IsEndpointAlreadyConnected(cand.D2, cand.End2, edges)) continue;

                AddEdgeUniqueByIndex(edges, addedPairCodes, ductIdxMap, cand.D1, cand.D2, CadConnectionKind.Elbow, null, cand.Inter, forbiddenCrossingPairs);
            }

            return edges;
        }

        private static bool HasIntermediateDuctBlocking(
            CadDuctSegment d1, XYZ end1,
            CadDuctSegment d2, XYZ end2,
            XYZ inter,
            List<CadDuctSegment> ducts,
            DuctAabb2D[] fullBounds)
        {
            double[] fracs = new double[] { 0.2, 0.4, 0.6, 0.8, 1.0 };
            double tolFeet = 15.0 * MM_TO_FEET;
            double minRefWidth = Math.Min(d1.WidthMm, d2.WidthMm);

            double qMinX = Math.Min(inter.X, Math.Min(end1.X, end2.X)) - tolFeet;
            double qMaxX = Math.Max(inter.X, Math.Max(end1.X, end2.X)) + tolFeet;
            double qMinY = Math.Min(inter.Y, Math.Min(end1.Y, end2.Y)) - tolFeet;
            double qMaxY = Math.Max(inter.Y, Math.Max(end1.Y, end2.Y)) + tolFeet;

            for (int k = 0; k < ducts.Count; k++)
            {
                CadDuctSegment other = ducts[k];
                if (other == d1 || other == d2) continue;

                DuctAabb2D ob = fullBounds[k];
                if (ob.MaxX < qMinX || ob.MinX > qMaxX || ob.MaxY < qMinY || ob.MinY > qMaxY) continue;

                bool collinear1 = Math.Abs(other.Direction.DotProduct(d1.Direction)) > 0.85;
                bool collinear2 = Math.Abs(other.Direction.DotProduct(d2.Direction)) > 0.85;
                bool comparableWidth = other.WidthMm >= minRefWidth * 0.45;
                if (!collinear1 && !collinear2 && !comparableWidth) continue;

                for (int f = 0; f < fracs.Length; f++)
                {
                    double t = fracs[f];
                    XYZ q1 = end1 + (inter - end1) * t;
                    if ((collinear1 || comparableWidth) && other.ContainsPointXY(q1, tolFeet)) return true;

                    XYZ q2 = end2 + (inter - end2) * t;
                    if ((collinear2 || comparableWidth) && other.ContainsPointXY(q2, tolFeet)) return true;
                }
            }
            return false;
        }

        private static bool IsEndpointAlreadyConnected(CadDuctSegment d, XYZ endpoint, List<CadConnectionEdge> edges)
        {
            XYZ farEnd = (DistXY(endpoint, d.StartPoint) < 1e-4) ? d.EndPoint : d.StartPoint;
            foreach (CadConnectionEdge edge in edges)
            {
                if (edge.Kind == CadConnectionKind.TakeoffTap && edge.SegmentA == d) continue;
                if (edge.SegmentA != d && edge.SegmentB != d) continue;

                XYZ refPt = edge.JunctionPoint;
                if (refPt == null)
                {
                    CadDuctSegment other = (edge.SegmentA == d) ? edge.SegmentB : edge.SegmentA;
                    refPt = other.CenterPoint;
                }
                if (refPt == null) continue;

                double dEnd = DistXY(endpoint, refPt);
                double dFar = DistXY(farEnd, refPt);
                if (dEnd < dFar) return true;
            }
            return false;
        }

        private static CadDuctSegment FindContinuousMainDuctPassingPolygon(
            CadDuctSegment fit,
            CadDuctSegment brOnly,
            List<CadDuctSegment> ducts,
            HashSet<long> forbiddenCrossingPairs,
            Dictionary<string, int> ductIdxMap)
        {
            if (fit == null || brOnly == null || ducts == null) return null;
            if (fit.PolygonVertices == null || fit.PolygonVertices.Count < 3) return null;

            double pad = 250.0 * MM_TO_FEET;
            double fMinX = double.MaxValue, fMinY = double.MaxValue;
            double fMaxX = double.MinValue, fMaxY = double.MinValue;
            for (int i = 0; i < fit.PolygonVertices.Count; i++)
            {
                XYZ p = fit.PolygonVertices[i];
                if (p.X < fMinX) fMinX = p.X;
                if (p.Y < fMinY) fMinY = p.Y;
                if (p.X > fMaxX) fMaxX = p.X;
                if (p.Y > fMaxY) fMaxY = p.Y;
            }

            CadDuctSegment bestMain = null;
            double bestScore = double.MaxValue;

            for (int i = 0; i < ducts.Count; i++)
            {
                CadDuctSegment m = ducts[i];
                if (m == brOnly) continue;
                if (!AreDuctSystemsCompatible(fit, m)) continue;
                if (!CanConnectDuctPair(m, brOnly, forbiddenCrossingPairs, ductIdxMap)) continue;
                if (m.WidthMm < brOnly.WidthMm - 30.0) continue;
                if (Math.Abs(m.Direction.DotProduct(brOnly.Direction)) >= 0.75) continue;

                DuctAabb2D mb = ComputeDuctSegmentAabb(m);
                if (mb.MaxX < fMinX - pad || mb.MinX > fMaxX + pad || mb.MaxY < fMinY - pad || mb.MinY > fMaxY + pad)
                {
                    continue;
                }

                double dist = m.DistanceToPointXY(fit.CenterPoint);
                double maxDist = (m.WidthMm * 0.5 + 350.0) * MM_TO_FEET;
                if (dist > maxDist) continue;

                XYZ inter = LineLineIntersectXY(m.StartPoint, m.Direction, brOnly.StartPoint, brOnly.Direction, m.StartPoint.Z);
                if (inter == null) continue;
                if (DistXY(m.StartPoint, inter) <= 120.0 * MM_TO_FEET || DistXY(m.EndPoint, inter) <= 120.0 * MM_TO_FEET) continue;

                if (dist < bestScore)
                {
                    bestScore = dist;
                    bestMain = m;
                }
            }

            return bestMain;
        }

        private static void ConnectOpenSteppedThreeWayJunctions(
            List<CadDuctSegment> ducts,
            DuctAabb2D[] endBounds,
            Dictionary<string, int> ductIdxMap,
            HashSet<long> addedPairCodes,
            List<CadConnectionEdge> edges,
            HashSet<long> forbiddenCrossingPairs)
        {
            int n = ducts.Count;
            if (n < 3) return;

            double maxGapFeet = 2600.0 * MM_TO_FEET;
            double searchReach = maxGapFeet + 500.0 * MM_TO_FEET;

            for (int i = 0; i < n; i++)
            {
                CadDuctSegment d1 = ducts[i];
                DuctAabb2D b1 = endBounds[i];

                for (int j = i + 1; j < n; j++)
                {
                    CadDuctSegment d2 = ducts[j];
                    DuctAabb2D b2 = endBounds[j];
                    if (b1.MaxX + searchReach < b2.MinX || b2.MaxX + searchReach < b1.MinX ||
                        b1.MaxY + searchReach < b2.MinY || b2.MaxY + searchReach < b1.MinY)
                    {
                        continue;
                    }

                    long pairCode = MakePairCode(i, j);
                    if (addedPairCodes.Contains(pairCode)) continue;
                    if (!CanConnectDuctPair(d1, d2, forbiddenCrossingPairs, ductIdxMap)) continue;

                    double colDot = Math.Abs(d1.Direction.DotProduct(d2.Direction));
                    if (colDot < 0.94) continue;

                    double wDiff = Math.Abs(d1.WidthMm - d2.WidthMm);
                    double perpGap = LineToLineDistance(d1.StartPoint, d1.Direction, d2.StartPoint, d2.Direction);
                    double maxPerp = (wDiff * 0.65 + 150.0) * MM_TO_FEET;
                    if (perpGap > maxPerp) continue;

                    XYZ f1, far1, f2, far2;
                    double gapDist = FindClosestEndpointPairXY(d1, d2, out f1, out far1, out f2, out far2);
                    if (gapDist < 100.0 * MM_TO_FEET || gapDist > maxGapFeet) continue;

                    if (IsEndpointAlreadyConnected(d1, f1, edges) || IsEndpointAlreadyConnected(d2, f2, edges)) continue;

                    XYZ out1 = new XYZ(f1.X - far1.X, f1.Y - far1.Y, 0.0);
                    XYZ out2 = new XYZ(f2.X - far2.X, f2.Y - far2.Y, 0.0);
                    XYZ f1ToF2 = new XYZ(f2.X - f1.X, f2.Y - f1.Y, 0.0);
                    if (out1.GetLength() < 1e-6 || out2.GetLength() < 1e-6 || f1ToF2.GetLength() < 1e-6) continue;
                    out1 = out1.Normalize();
                    out2 = out2.Normalize();
                    XYZ fDir = f1ToF2.Normalize();
                    if (out1.DotProduct(fDir) < 0.7 || out2.DotProduct(fDir) > -0.7) continue;

                    XYZ gapMid = (f1 + f2) * 0.5;
                    double maxBrLat = (Math.Max(d1.WidthMm, d2.WidthMm) * 0.5 + 800.0) * MM_TO_FEET;
                    double longMargin = 200.0 * MM_TO_FEET;

                    List<CadDuctSegment> middleBranches = null;
                    for (int k = 0; k < n; k++)
                    {
                        if (k == i || k == j) continue;
                        CadDuctSegment br = ducts[k];
                        if (Math.Abs(br.Direction.DotProduct(d1.Direction)) >= 0.55) continue;

                        double dStart = DistXY(br.StartPoint, gapMid);
                        double dEnd = DistXY(br.EndPoint, gapMid);
                        XYZ brTip = (dStart <= dEnd) ? br.StartPoint : br.EndPoint;
                        if (DistXY(brTip, gapMid) > maxBrLat + gapDist * 0.6) continue;
                        if (IsEndpointAlreadyConnected(br, brTip, edges)) continue;

                        XYZ axis = new XYZ(d1.Direction.X, d1.Direction.Y, 0.0).Normalize();
                        XYZ perpAxis = new XYZ(-axis.Y, axis.X, 0.0);
                        double p1 = f1.X * axis.X + f1.Y * axis.Y;
                        double p2 = f2.X * axis.X + f2.Y * axis.Y;
                        double pb = brTip.X * axis.X + brTip.Y * axis.Y;
                        double minP = Math.Min(p1, p2) - longMargin;
                        double maxP = Math.Max(p1, p2) + longMargin;
                        if (pb < minP || pb > maxP) continue;

                        double midLat = gapMid.X * perpAxis.X + gapMid.Y * perpAxis.Y;
                        double bLat = brTip.X * perpAxis.X + brTip.Y * perpAxis.Y;
                        if (Math.Abs(bLat - midLat) > maxBrLat) continue;

                        if (!CanConnectDuctPair(d1, br, forbiddenCrossingPairs, ductIdxMap)
                            || !CanConnectDuctPair(d2, br, forbiddenCrossingPairs, ductIdxMap)) continue;

                        if (middleBranches == null) middleBranches = new List<CadDuctSegment>(2);
                        middleBranches.Add(br);
                    }

                    if (middleBranches != null && middleBranches.Count > 0)
                    {
                        CadDuctSegment mainPrimary = (d1.WidthMm > d2.WidthMm || (Math.Abs(d1.WidthMm - d2.WidthMm) < 1.0 && d1.Length >= d2.Length))
                            ? d1 : d2;
                        CadDuctSegment mainSecondary = (mainPrimary == d1) ? d2 : d1;

                        if (wDiff >= 15.0)
                        {
                            AddEdgeUniqueByIndex(edges, addedPairCodes, ductIdxMap, mainPrimary, mainSecondary, CadConnectionKind.Transition, null, gapMid, forbiddenCrossingPairs);
                        }
                        else
                        {
                            AddEdgeUniqueByIndex(edges, addedPairCodes, ductIdxMap, mainPrimary, mainSecondary, CadConnectionKind.CollinearDirect, null, gapMid, forbiddenCrossingPairs);
                        }

                        for (int bi = 0; bi < middleBranches.Count; bi++)
                        {
                            CadDuctSegment br = middleBranches[bi];
                            XYZ tapPt = LineLineIntersectXY(mainPrimary.StartPoint, mainPrimary.Direction, br.StartPoint, br.Direction, mainPrimary.StartPoint.Z);
                            if (tapPt == null) tapPt = gapMid;
                            AddEdgeUniqueByIndex(edges, addedPairCodes, ductIdxMap, mainPrimary, br, CadConnectionKind.TakeoffTap, null, tapPt, forbiddenCrossingPairs);
                        }
                    }
                }
            }
        }

        private static double FindClosestEndpointPairXY(
            CadDuctSegment d1, CadDuctSegment d2,
            out XYZ face1, out XYZ far1,
            out XYZ face2, out XYZ far2)
        {
            XYZ[] p1 = new XYZ[] { d1.StartPoint, d1.EndPoint };
            XYZ[] p2 = new XYZ[] { d2.StartPoint, d2.EndPoint };
            double best = double.MaxValue;
            int bi = 0, bj = 0;
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    double dist = DistXY(p1[i], p2[j]);
                    if (dist < best) { best = dist; bi = i; bj = j; }
                }
            }
            face1 = p1[bi];
            far1 = p1[1 - bi];
            face2 = p2[bj];
            far2 = p2[1 - bj];
            return best;
        }

        private static List<CadDuctSegment> FindElbowTouchingDucts(CadDuctSegment fit, List<CadDuctSegment> ducts, double tolFeet)
        {
            List<CadDuctSegment> raw = FindDuctsTouchingPolygon(fit, ducts, tolFeet);
            if (fit.WidthMm <= 0) return raw;

            List<CadDuctSegment> filtered = new List<CadDuctSegment>();
            foreach (CadDuctSegment d in raw)
            {
                if (Math.Abs(d.WidthMm - fit.WidthMm) <= 120.0)
                {
                    filtered.Add(d);
                }
            }
            return filtered;
        }

        private static List<CadDuctSegment> FindDuctsTouchingPolygon(CadDuctSegment fit, List<CadDuctSegment> ducts, double tolFeet)
        {
            List<CadDuctSegment> res = new List<CadDuctSegment>();
            if (fit.PolygonVertices == null || fit.PolygonVertices.Count < 3) return res;

            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            for (int i = 0; i < fit.PolygonVertices.Count; i++)
            {
                XYZ p = fit.PolygonVertices[i];
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }
            double extMinX = minX - tolFeet, extMaxX = maxX + tolFeet;
            double extMinY = minY - tolFeet, extMaxY = maxY + tolFeet;

            foreach (CadDuctSegment d in ducts)
            {
                XYZ sp = d.StartPoint;
                XYZ ep = d.EndPoint;
                bool spInBox = sp.X >= extMinX && sp.X <= extMaxX && sp.Y >= extMinY && sp.Y <= extMaxY;
                bool epInBox = ep.X >= extMinX && ep.X <= extMaxX && ep.Y >= extMinY && ep.Y <= extMaxY;
                if (!spInBox && !epInBox) continue;

                bool sInside = spInBox && fit.ContainsPointXY(sp, tolFeet);
                bool eInside = epInBox && fit.ContainsPointXY(ep, tolFeet);
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

            CadDuctSegment insideSeed = null;
            CadDuctSegment nearestSeed = null;
            double bestInsideDist = double.MaxValue;
            double bestDist = double.MaxValue;
            double maxPickDist = 2500.0 * MM_TO_FEET;

            foreach (CadDuctSegment d in allDucts)
            {
                double dist = d.DistanceToPointXY(pickPt);
                if (d.ContainsPointXY(pickPt, 30.0 * MM_TO_FEET))
                {
                    if (dist < bestInsideDist)
                    {
                        bestInsideDist = dist;
                        insideSeed = d;
                    }
                }
                else if (dist < bestDist && dist <= maxPickDist)
                {
                    bestDist = dist;
                    nearestSeed = d;
                }
            }

            CadDuctSegment seed = insideSeed;

            // Nếu click không nằm trong đa giác ống thẳng -> ưu tiên kiểm tra click trúng Cút cong / Phụ kiện trước khi fallback về ống gần nhất
            if (seed == null && allFittings != null)
            {
                foreach (CadDuctSegment fit in allFittings)
                {
                    if (fit.ContainsPointXY(pickPt, 50.0 * MM_TO_FEET))
                    {
                        foreach (CadConnectionEdge edge in allEdges)
                        {
                            if (edge.BridgePolygon == fit)
                            {
                                seed = edge.SegmentA;
                                break;
                            }
                        }
                        if (seed != null) break;
                    }
                }
            }

            if (seed == null) seed = nearestSeed;
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

                    if (next != null && !visitedIds.Contains(next.Id) && AreDuctSystemsCompatible(cur, next))
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

        private class TextMatchCandidate
        {
            public int DuctIndex;
            public int NoteIndex;
            public double Score;
        }

        private class LocalTextSeedCandidate
        {
            public int DuctIndex;
            public int NoteIndex;
            public double DistFeet;
        }

        private static bool IsNonDuctAnnotationNote(string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText)) return false;
            string upper = rawText.ToUpperInvariant();

            // Bỏ qua ghi chú miệng gió (SAG/EAG/RAG/OAG/FAG/PAG/SLG...), louver hoặc tiêu âm (ATT)
            if (System.Text.RegularExpressions.Regex.IsMatch(
                    upper,
                    @"(?:^|[^A-Z0-9])(?:SAG|EAG|RAG|OAG|FAG|PAG|SLG|RLG|ELG|GRILLE|DIFFUSER|LOUVER|ATTENUATOR|ATT)(?:$|[^A-Z0-9])"))
            {
                return true;
            }

            // Dạng đếm số lượng miệng gió (vd: "31Nos 300x600") nếu không mang ký hiệu ống gió (EAD/SAD/RAD/OAD/FAD/PAD...)
            if (System.Text.RegularExpressions.Regex.IsMatch(upper, @"(?:^|[^A-Z0-9])\d+\s*NOS(?:$|[^A-Z0-9])")
                && !System.Text.RegularExpressions.Regex.IsMatch(upper, @"(?:^|[^A-Z0-9])(?:SAD|EAD|RAD|OAD|FAD|PAD|EXD|FSD|SPD|TEAD|TSAD)(?:$|[^A-Z0-9])"))
            {
                return true;
            }

            return false;
        }

        private static bool HasEiInsulationKeyword(string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText)) return false;
            return System.Text.RegularExpressions.Regex.IsMatch(
                rawText.ToUpperInvariant(),
                @"(?:^|[^A-Z0-9])EI\s*\d+");
        }

        private static bool IsNoteWidthCompatible(double geomW, CadTextNote note, bool allowEiTolerance, bool requireEiKeyword)
        {
            if (note == null || note.WidthMm <= 0 || note.HeightMm <= 0) return false;
            if (geomW <= 0) return true;

            double wDiff = Math.Abs(geomW - note.WidthMm);
            if (!allowEiTolerance)
            {
                // Quy tắc chuẩn HVAC: số TRƯỚC 'x' (note.WidthMm) luôn là W, tuyệt đối không đảo H thành W
                return wDiff <= 60.0;
            }

            bool hasEi = HasEiInsulationKeyword(note.RawText);
            if (requireEiKeyword && !hasEi) return false;

            double maxWiderTol = hasEi ? 300.0 : 250.0;
            double minWidthRatio = hasEi ? 0.35 : 0.45;
            return geomW >= note.WidthMm - 80.0
                && geomW <= note.WidthMm + maxWiderTol
                && note.WidthMm >= geomW * minWidthRatio;
        }

        private static void AssignDirectTextMatch(
            CadDuctSegment duct,
            CadTextNote note,
            double origGeomW,
            HashSet<string> resolvedIds)
        {
            // QUY TẮC BẢN VẼ HVAC: Số đầu tiên (note.WidthMm) LUÔN là W, số thứ hai (note.HeightMm) LUÔN là H!
            duct.WidthMm = note.WidthMm;
            duct.Width2Mm = note.WidthMm;
            duct.HeightMm = note.HeightMm;
            duct.TextLabel = note.RawText;
            string prefix = note.HasLeaderArrow ? "Leader CAD" : "Text CAD";
            if (origGeomW > 0 && Math.Abs(origGeomW - note.WidthMm) > 60.0)
            {
                duct.SizeSource = string.Format("{0} (EI/Bọc): {1} [Hình học ~{2}mm]",
                    prefix, note.RawText, (int)Math.Round(origGeomW));
            }
            else
            {
                duct.SizeSource = prefix + ": " + note.RawText;
            }
            duct.HasDirectText = true;
            resolvedIds.Add(duct.Id);
        }

        private static double DistanceToDuctPolygonXY(XYZ pt, CadDuctSegment duct)
        {
            if (pt == null || duct == null) return double.MaxValue;
            if (duct.ContainsPointXY(pt, 0.0)) return 0.0;
            if (duct.PolygonVertices != null && duct.PolygonVertices.Count >= 3)
            {
                double minDist = double.MaxValue;
                int n = duct.PolygonVertices.Count;
                for (int i = 0; i < n; i++)
                {
                    double d = CadDuctSegment.DistPointToSegmentXY(pt, duct.PolygonVertices[i], duct.PolygonVertices[(i + 1) % n]);
                    if (d < minDist) minDist = d;
                }
                return minDist;
            }
            double halfW = Math.Max(duct.WidthMm, duct.Width2Mm) * 0.5 * MM_TO_FEET;
            return Math.Max(0.0, duct.DistanceToPointXY(pt) - halfW);
        }

        private static void RunLeaderArrowMatchTier(
            List<CadDuctSegment> ducts,
            DuctAabb2D[] ductBounds,
            List<CadTextNote> usableNotes,
            double[] geomWidths,
            HashSet<string> resolvedIds,
            bool[] claimedNotes,
            bool insideOnly,
            bool allowEiTolerance,
            double maxTipRadiusFeet)
        {
            if (usableNotes == null || usableNotes.Count == 0) return;

            List<TextMatchCandidate> candidates = new List<TextMatchCandidate>();
            double insideMargin = 80.0 * MM_TO_FEET;
            double boxPad = insideOnly ? insideMargin : maxTipRadiusFeet;

            for (int i = 0; i < ducts.Count; i++)
            {
                CadDuctSegment duct = ducts[i];
                if (resolvedIds.Contains(duct.Id)) continue;

                DuctAabb2D b = ductBounds[i];
                double geomW = geomWidths[i];

                for (int t = 0; t < usableNotes.Count; t++)
                {
                    if (claimedNotes[t]) continue;
                    CadTextNote note = usableNotes[t];
                    if (!note.HasLeaderArrow || note.LeaderArrowPoint == null) continue;
                    if (!IsNoteSystemCompatibleWithDuct(duct, note)) continue;

                    XYZ tip = note.LeaderArrowPoint;
                    if (tip.X < b.MinX - boxPad || tip.X > b.MaxX + boxPad ||
                        tip.Y < b.MinY - boxPad || tip.Y > b.MaxY + boxPad)
                    {
                        continue;
                    }

                    bool allowEiHere = allowEiTolerance && HasEiInsulationKeyword(note.RawText);
                    if (!IsNoteWidthCompatible(geomW, note, allowEiHere, false))
                    {
                        continue;
                    }

                    bool tipInside = duct.ContainsPointXY(tip, insideMargin);
                    if (insideOnly && !tipInside) continue;

                    double edgeDist = tipInside ? 0.0 : DistanceToDuctPolygonXY(tip, duct);
                    if (!tipInside && edgeDist > maxTipRadiusFeet) continue;

                    double distToAxis = duct.DistanceToPointXY(tip);
                    double wDiff = Math.Abs(geomW - note.WidthMm);
                    double score = (tipInside ? 0.0 : 2000.0)
                                 + edgeDist * 100.0
                                 + distToAxis
                                 + (wDiff > 60.0 ? 5000.0 : 0.0)
                                 + (wDiff * MM_TO_FEET * 15.0);

                    TextMatchCandidate cand = new TextMatchCandidate();
                    cand.DuctIndex = i;
                    cand.NoteIndex = t;
                    cand.Score = score;
                    candidates.Add(cand);
                }
            }

            if (candidates.Count == 0) return;

            candidates.Sort(delegate(TextMatchCandidate a, TextMatchCandidate b)
            {
                return a.Score.CompareTo(b.Score);
            });

            foreach (TextMatchCandidate cand in candidates)
            {
                CadDuctSegment duct = ducts[cand.DuctIndex];
                if (resolvedIds.Contains(duct.Id) || claimedNotes[cand.NoteIndex]) continue;

                claimedNotes[cand.NoteIndex] = true;
                AssignDirectTextMatch(duct, usableNotes[cand.NoteIndex], geomWidths[cand.DuctIndex], resolvedIds);
            }
        }

        private static bool IsNoteSystemCompatibleWithDuct(CadDuctSegment duct, CadTextNote note)
        {
            if (duct == null || note == null) return true;
            string ductSys = ExtractHvacSystemCode(duct.LayerName);
            if (string.IsNullOrEmpty(ductSys)) return true;

            string noteTextSys = ExtractHvacSystemCode(note.RawText);
            string noteLayerSys = ExtractHvacSystemCode(note.LayerName);
            string noteSys = !string.IsNullOrEmpty(noteTextSys) ? noteTextSys : noteLayerSys;
            if (string.IsNullOrEmpty(noteSys)) return true;

            return string.Equals(ductSys, noteSys, StringComparison.Ordinal);
        }

        private static void RunDirectTextMatchTier(
            List<CadDuctSegment> ducts,
            DuctAabb2D[] ductBounds,
            List<CadTextNote> usableNotes,
            double[] geomWidths,
            HashSet<string> resolvedIds,
            bool[] claimedNotes,
            bool insideOnly,
            bool allowEiTolerance,
            bool requireEiOutside,
            double maxMatchRadiusFeet)
        {
            if (usableNotes == null || usableNotes.Count == 0) return;

            List<TextMatchCandidate> candidates = new List<TextMatchCandidate>();
            double strictInsideMargin = 30.0 * MM_TO_FEET;
            double nearPolyMargin = 150.0 * MM_TO_FEET;
            double boxPad = insideOnly ? (60.0 * MM_TO_FEET) : maxMatchRadiusFeet;

            for (int i = 0; i < ducts.Count; i++)
            {
                CadDuctSegment duct = ducts[i];
                if (resolvedIds.Contains(duct.Id)) continue;

                DuctAabb2D b = ductBounds[i];
                double geomW = geomWidths[i];

                for (int t = 0; t < usableNotes.Count; t++)
                {
                    if (claimedNotes[t]) continue;
                    CadTextNote note = usableNotes[t];
                    if (note.Position == null) continue;
                    if (!IsNoteSystemCompatibleWithDuct(duct, note)) continue;

                    double nx = note.Position.X;
                    double ny = note.Position.Y;
                    if (nx < b.MinX - boxPad || nx > b.MaxX + boxPad ||
                        ny < b.MinY - boxPad || ny > b.MaxY + boxPad)
                    {
                        continue;
                    }

                    if (!IsNoteWidthCompatible(geomW, note, allowEiTolerance, requireEiOutside && !insideOnly))
                    {
                        continue;
                    }

                    bool strictInside = duct.ContainsPointXY(note.Position, strictInsideMargin);
                    if (insideOnly && !strictInside) continue;

                    double distToAxis = duct.DistanceToPointXY(note.Position);
                    bool nearPoly = strictInside || duct.ContainsPointXY(note.Position, nearPolyMargin);
                    if (!nearPoly && distToAxis > maxMatchRadiusFeet) continue;

                    double wDiff = Math.Abs(geomW - note.WidthMm);
                    double score = (strictInside ? 0.0 : (nearPoly ? 2000.0 : 10000.0))
                                 + distToAxis
                                 + (wDiff * MM_TO_FEET * 10.0);

                    TextMatchCandidate cand = new TextMatchCandidate();
                    cand.DuctIndex = i;
                    cand.NoteIndex = t;
                    cand.Score = score;
                    candidates.Add(cand);
                }
            }

            if (candidates.Count == 0) return;

            candidates.Sort(delegate(TextMatchCandidate a, TextMatchCandidate b)
            {
                return a.Score.CompareTo(b.Score);
            });

            foreach (TextMatchCandidate cand in candidates)
            {
                CadDuctSegment duct = ducts[cand.DuctIndex];
                if (resolvedIds.Contains(duct.Id) || claimedNotes[cand.NoteIndex]) continue;

                claimedNotes[cand.NoteIndex] = true;
                AssignDirectTextMatch(duct, usableNotes[cand.NoteIndex], geomWidths[cand.DuctIndex], resolvedIds);
            }
        }

        public static void MatchAndPropagateDuctSizes(List<CadDuctSegment> ducts, List<CadTextNote> textNotes, double defaultHeightMm = 0)
        {
            if (ducts == null || ducts.Count == 0) return;

            int ductCount = ducts.Count;
            Dictionary<string, int> indexMap = new Dictionary<string, int>(ductCount);
            Dictionary<string, double> geomWidthMap = new Dictionary<string, double>(ductCount);
            double[] geomWidths = new double[ductCount];
            DuctAabb2D[] ductBounds = new DuctAabb2D[ductCount];

            for (int i = 0; i < ductCount; i++)
            {
                CadDuctSegment d = ducts[i];
                indexMap[d.Id] = i + 1;
                geomWidths[i] = d.WidthMm;
                geomWidthMap[d.Id] = d.WidthMm;
                ductBounds[i] = ComputeDuctSegmentAabb(d);

                d.HasDirectText = false;
                d.HeightMm = 0;
                d.TextLabel = string.Empty;
                d.SizeSource = "Chưa có ghi chú";
            }

            List<CadTextNote> usableNotes = new List<CadTextNote>();
            if (textNotes != null)
            {
                foreach (CadTextNote note in textNotes)
                {
                    if (note == null || note.WidthMm <= 0 || note.HeightMm <= 0 || note.Position == null) continue;
                    if (IsNonDuctAnnotationNote(note.RawText)) continue;
                    usableNotes.Add(note);
                }
            }

            bool[] claimedNotes = new bool[usableNotes.Count];
            HashSet<string> resolvedIds = new HashSet<string>();
            double maxMatchRadiusFeet = 2500.0 * MM_TO_FEET;

            // PASS 1-L1: Mũi tên QLEADER / MULTILEADER trỏ trực tiếp vào trong đa giác ống với W khớp chuẩn (<= 60mm)
            RunLeaderArrowMatchTier(ducts, ductBounds, usableNotes, geomWidths, resolvedIds, claimedNotes, true, false, 350.0 * MM_TO_FEET);

            // PASS 1-L2: Mũi tên QLEADER / MULTILEADER trỏ sát biên ống (<= 350mm) hoặc ống bọc EI60
            RunLeaderArrowMatchTier(ducts, ductBounds, usableNotes, geomWidths, resolvedIds, claimedNotes, false, true, 350.0 * MM_TO_FEET);

            // PASS 1A: Text nằm TRONG đa giác ống với bề rộng khớp chuẩn (|W_cad - W_text| <= 60mm)
            RunDirectTextMatchTier(ducts, ductBounds, usableNotes, geomWidths, resolvedIds, claimedNotes, true, false, false, maxMatchRadiusFeet);

            // PASS 1B: Text nằm TRONG đa giác ống bọc cách nhiệt EI60 / vẽ rộng hơn kích thước thật (VD EAD 350x300: EI60 trong ống vẽ 500mm)
            RunDirectTextMatchTier(ducts, ductBounds, usableNotes, geomWidths, resolvedIds, claimedNotes, true, true, false, maxMatchRadiusFeet);

            // PASS 1C: Text nằm ngoài đa giác trong bán kính 2500mm với W khớp chuẩn (<= 60mm)
            RunDirectTextMatchTier(ducts, ductBounds, usableNotes, geomWidths, resolvedIds, claimedNotes, false, false, false, maxMatchRadiusFeet);

            // PASS 2A: Lan truyền BFS cùng bề rộng qua Cút/Nối thẳng trước khi thử leader EI60 ngoài ống
            RunBfsPropagationPass(ducts, indexMap, geomWidthMap, resolvedIds, true, true, null);

            // PASS 1D: Leader Text EI60 ngoài đa giác (<= 2500mm) cho nhánh ống bọc EI60 chưa được gán ở bước trên
            RunDirectTextMatchTier(ducts, ductBounds, usableNotes, geomWidths, resolvedIds, claimedNotes, false, true, true, maxMatchRadiusFeet);

            // PASS 2 (đầy đủ): Lan truyền đồ thị BFS qua toàn bộ ConnectedNeighbors (Cút cong, Côn thu, Tap, Tee)
            PropagateBfsFromResolved(ducts, indexMap, geomWidthMap, resolvedIds, null);

            // PASS 3A: Với các cụm rời chưa có nhãn -> nếu có Text chưa dùng (<= 6000mm) khớp W nằm gần hơn mọi tuyến cùng W đã biết, ưu tiên lấy Text của chính cụm đó
            if (usableNotes.Count > 0 && resolvedIds.Count < ductCount)
            {
                PropagateCloserLocalUnclaimedTexts(ducts, ductBounds, usableNotes, geomWidths, geomWidthMap, indexMap, resolvedIds, claimedNotes);
            }

            // PASS 3B: Thuật toán lan truyền theo tuyến gần nhất (ưu tiên cùng W) cho các đoạn đứt đoạn còn thiếu
            List<int> labeledIndices = new List<int>();
            for (int i = 0; i < ductCount; i++)
            {
                if (resolvedIds.Contains(ducts[i].Id) && ducts[i].HeightMm > 0)
                {
                    labeledIndices.Add(i);
                }
            }

            if (labeledIndices.Count > 0 && resolvedIds.Count < ductCount)
            {
                double smallEiGapFeet = 1500.0 * MM_TO_FEET;
                for (int i = 0; i < ductCount; i++)
                {
                    CadDuctSegment duct = ducts[i];
                    if (resolvedIds.Contains(duct.Id)) continue;

                    DuctAabb2D db = ductBounds[i];
                    double geomW = geomWidths[i];

                    CadDuctSegment bestMatchWidth = null;
                    double bestMatchDist = double.MaxValue;
                    double bestMatchDist2 = double.MaxValue;
                    double bestMatchGeomW = 0;

                    CadDuctSegment bestEiGeomMatch = null;
                    double bestEiDist = smallEiGapFeet;
                    double bestEiDist2 = bestEiDist * bestEiDist;

                    CadDuctSegment bestOverall = null;
                    double bestOverallDist = double.MaxValue;
                    double bestOverallDist2 = double.MaxValue;

                    for (int li = 0; li < labeledIndices.Count; li++)
                    {
                        int srcIdx = labeledIndices[li];
                        CadDuctSegment src = ducts[srcIdx];
                        if (!AreDuctSystemsCompatible(duct, src)) continue;
                        DuctAabb2D sb = ductBounds[srcIdx];

                        double dx = 0.0;
                        if (db.MaxX < sb.MinX) dx = sb.MinX - db.MaxX;
                        else if (sb.MaxX < db.MinX) dx = db.MinX - sb.MaxX;

                        double dy = 0.0;
                        if (db.MaxY < sb.MinY) dy = sb.MinY - db.MaxY;
                        else if (sb.MaxY < db.MinY) dy = db.MinY - sb.MaxY;

                        double minBoxDist2 = dx * dx + dy * dy;
                        bool exactWMatch = Math.Abs(duct.WidthMm - src.WidthMm) <= 60.0;
                        bool sameEiCadGeom = !exactWMatch
                            && Math.Abs(geomW - geomWidths[srcIdx]) <= 60.0
                            && Math.Abs(src.WidthMm - geomWidths[srcIdx]) > 60.0;

                        if (minBoxDist2 >= bestOverallDist2
                            && (!exactWMatch || minBoxDist2 >= bestMatchDist2)
                            && (!sameEiCadGeom || minBoxDist2 >= bestEiDist2))
                        {
                            continue;
                        }

                        double dist = SegmentSegmentDistanceXY(duct.StartPoint, duct.EndPoint, src.StartPoint, src.EndPoint);
                        double dist2 = dist * dist;

                        if (exactWMatch && dist < bestMatchDist)
                        {
                            bestMatchDist = dist;
                            bestMatchDist2 = dist2;
                            bestMatchWidth = src;
                            bestMatchGeomW = geomWidths[srcIdx];
                        }
                        if (sameEiCadGeom && dist < bestEiDist)
                        {
                            bestEiDist = dist;
                            bestEiDist2 = dist2;
                            bestEiGeomMatch = src;
                        }
                        if (dist < bestOverallDist)
                        {
                            bestOverallDist = dist;
                            bestOverallDist2 = dist2;
                            bestOverall = src;
                        }
                    }

                    CadDuctSegment chosen = (bestMatchWidth != null)
                        ? bestMatchWidth
                        : ((bestEiGeomMatch != null) ? bestEiGeomMatch : bestOverall);

                    if (chosen != null)
                    {
                        bool inheritEiCorrectedWidth = (chosen == bestEiGeomMatch)
                            || (chosen == bestMatchWidth && Math.Abs(chosen.WidthMm - bestMatchGeomW) > 60.0 && bestMatchDist <= smallEiGapFeet);

                        if (duct.WidthMm <= 0 || inheritEiCorrectedWidth)
                        {
                            duct.WidthMm = chosen.WidthMm;
                            duct.Width2Mm = chosen.Width2Mm;
                        }
                        duct.HeightMm = chosen.HeightMm;
                        duct.TextLabel = string.Format("{0}x{1} (Lan truyền)", (int)duct.WidthMm, (int)duct.HeightMm);
                        duct.SizeSource = string.Format("Lan truyền từ tuyến gần nhất (#{0}: {1}x{2})",
                            indexMap[chosen.Id], (int)chosen.WidthMm, (int)chosen.HeightMm);
                        resolvedIds.Add(duct.Id);
                    }
                }
            }
            else if (usableNotes.Count > 0 && resolvedIds.Count < ductCount)
            {
                // PASS 3C: Toàn bản vẽ chưa khớp được đoạn nào trong 2500mm -> tìm Text gần nhất khớp WidthMm rồi lan truyền BFS
                FindAndApplyBestNearestText(ducts, ductBounds, usableNotes, geomWidthMap, indexMap, resolvedIds, true, 6000.0 * MM_TO_FEET);
                FindAndApplyBestNearestText(ducts, ductBounds, usableNotes, geomWidthMap, indexMap, resolvedIds, true, double.MaxValue);
                FindAndApplyBestNearestText(ducts, ductBounds, usableNotes, geomWidthMap, indexMap, resolvedIds, false, double.MaxValue);
            }

            // PASS 3D: Chỉ khi hoàn toàn không có bất kỳ Text nào trên bản vẽ
            foreach (CadDuctSegment duct in ducts)
            {
                if (resolvedIds.Contains(duct.Id)) continue;
                if (defaultHeightMm > 0)
                {
                    duct.HeightMm = defaultHeightMm;
                    duct.SizeSource = string.Format("Mặc định (H={0})", (int)defaultHeightMm);
                }
                else
                {
                    duct.HeightMm = 0;
                    duct.TextLabel = string.Empty;
                    duct.SizeSource = "Chưa có ghi chú";
                }
            }
        }

        private static void PropagateCloserLocalUnclaimedTexts(
            List<CadDuctSegment> ducts,
            DuctAabb2D[] ductBounds,
            List<CadTextNote> usableNotes,
            double[] geomWidths,
            Dictionary<string, double> geomWidthMap,
            Dictionary<string, int> indexMap,
            HashSet<string> resolvedIds,
            bool[] claimedNotes)
        {
            double maxLocalNoteFeet = 6000.0 * MM_TO_FEET;
            List<LocalTextSeedCandidate> seeds = new List<LocalTextSeedCandidate>();

            for (int i = 0; i < ducts.Count; i++)
            {
                CadDuctSegment duct = ducts[i];
                if (resolvedIds.Contains(duct.Id)) continue;

                DuctAabb2D db = ductBounds[i];
                double geomW = geomWidths[i];

                int bestNoteIdx = -1;
                double bestNoteDist = maxLocalNoteFeet;

                for (int t = 0; t < usableNotes.Count; t++)
                {
                    if (claimedNotes[t]) continue;
                    CadTextNote note = usableNotes[t];
                    if (note.Position == null) continue;
                    if (!IsNoteSystemCompatibleWithDuct(duct, note)) continue;

                    double nx = note.Position.X, ny = note.Position.Y;
                    if (nx < db.MinX - bestNoteDist || nx > db.MaxX + bestNoteDist ||
                        ny < db.MinY - bestNoteDist || ny > db.MaxY + bestNoteDist)
                    {
                        continue;
                    }

                    if (!IsNoteWidthCompatible(geomW, note, true, true) && !IsNoteWidthCompatible(geomW, note, false, false))
                    {
                        continue;
                    }

                    double dist = duct.DistanceToPointXY(note.Position);
                    if (dist < bestNoteDist)
                    {
                        bestNoteDist = dist;
                        bestNoteIdx = t;
                    }
                }

                if (bestNoteIdx < 0) continue;

                // Kiểm tra xem có tuyến đã biết nhãn cùng bề rộng nào gần hơn Text này không
                double nearestSameWidthDuctDist = double.MaxValue;
                double nearestDist2 = double.MaxValue;
                for (int s = 0; s < ducts.Count; s++)
                {
                    if (!resolvedIds.Contains(ducts[s].Id) || ducts[s].HeightMm <= 0) continue;
                    if (!AreDuctSystemsCompatible(duct, ducts[s])) continue;
                    if (Math.Abs(geomW - geomWidths[s]) > 60.0 && Math.Abs(duct.WidthMm - ducts[s].WidthMm) > 60.0) continue;

                    DuctAabb2D sb = ductBounds[s];
                    double dx = 0.0;
                    if (db.MaxX < sb.MinX) dx = sb.MinX - db.MaxX;
                    else if (sb.MaxX < db.MinX) dx = db.MinX - sb.MaxX;
                    double dy = 0.0;
                    if (db.MaxY < sb.MinY) dy = sb.MinY - db.MaxY;
                    else if (sb.MaxY < db.MinY) dy = db.MinY - sb.MaxY;
                    if (dx * dx + dy * dy >= nearestDist2) continue;

                    double sd = SegmentSegmentDistanceXY(duct.StartPoint, duct.EndPoint, ducts[s].StartPoint, ducts[s].EndPoint);
                    if (sd < nearestSameWidthDuctDist)
                    {
                        nearestSameWidthDuctDist = sd;
                        nearestDist2 = sd * sd;
                    }
                }

                if (bestNoteDist < nearestSameWidthDuctDist)
                {
                    LocalTextSeedCandidate cand = new LocalTextSeedCandidate();
                    cand.DuctIndex = i;
                    cand.NoteIndex = bestNoteIdx;
                    cand.DistFeet = bestNoteDist;
                    seeds.Add(cand);
                }
            }

            if (seeds.Count == 0) return;

            seeds.Sort(delegate(LocalTextSeedCandidate a, LocalTextSeedCandidate b)
            {
                return a.DistFeet.CompareTo(b.DistFeet);
            });

            foreach (LocalTextSeedCandidate seed in seeds)
            {
                CadDuctSegment duct = ducts[seed.DuctIndex];
                if (resolvedIds.Contains(duct.Id) || claimedNotes[seed.NoteIndex]) continue;

                claimedNotes[seed.NoteIndex] = true;
                CadTextNote note = usableNotes[seed.NoteIndex];
                AssignDirectTextMatch(duct, note, geomWidths[seed.DuctIndex], resolvedIds);
                duct.HasDirectText = false;
                duct.SizeSource = string.Format("Lan truyền từ Text gần nhất ({0})", note.RawText);

                List<CadDuctSegment> singleSeed = new List<CadDuctSegment>(1);
                singleSeed.Add(duct);
                PropagateBfsFromResolved(ducts, indexMap, geomWidthMap, resolvedIds, singleSeed);
            }
        }

        private static void FindAndApplyBestNearestText(
            List<CadDuctSegment> ducts,
            DuctAabb2D[] ductBounds,
            List<CadTextNote> usableNotes,
            Dictionary<string, double> geomWidthMap,
            Dictionary<string, int> indexMap,
            HashSet<string> resolvedIds,
            bool requireWidthMatch,
            double maxDistFeet)
        {
            if (usableNotes == null || usableNotes.Count == 0) return;

            List<LocalTextSeedCandidate> candidates = new List<LocalTextSeedCandidate>();
            bool hasDistLimit = maxDistFeet < 1e12;

            for (int i = 0; i < ducts.Count; i++)
            {
                CadDuctSegment d = ducts[i];
                if (resolvedIds.Contains(d.Id)) continue;

                DuctAabb2D db = ductBounds[i];
                double geomW = geomWidthMap[d.Id];
                int bestNoteIdx = -1;
                double bestDist = maxDistFeet;

                for (int t = 0; t < usableNotes.Count; t++)
                {
                    CadTextNote note = usableNotes[t];
                    if (note.WidthMm <= 0 || note.HeightMm <= 0 || note.Position == null) continue;
                    if (!IsNoteSystemCompatibleWithDuct(d, note)) continue;
                    if (requireWidthMatch && Math.Abs(geomW - note.WidthMm) > 60.0) continue;

                    double limit = hasDistLimit ? bestDist : double.MaxValue;
                    if (limit < 1e12)
                    {
                        if (note.Position.X < db.MinX - limit || note.Position.X > db.MaxX + limit ||
                            note.Position.Y < db.MinY - limit || note.Position.Y > db.MaxY + limit)
                        {
                            continue;
                        }
                    }

                    double dist = d.DistanceToPointXY(note.Position);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestNoteIdx = t;
                    }
                }

                if (bestNoteIdx >= 0)
                {
                    LocalTextSeedCandidate cand = new LocalTextSeedCandidate();
                    cand.DuctIndex = i;
                    cand.NoteIndex = bestNoteIdx;
                    cand.DistFeet = bestDist;
                    candidates.Add(cand);
                }
            }

            if (candidates.Count == 0) return;

            candidates.Sort(delegate(LocalTextSeedCandidate a, LocalTextSeedCandidate b)
            {
                return a.DistFeet.CompareTo(b.DistFeet);
            });

            foreach (LocalTextSeedCandidate cand in candidates)
            {
                CadDuctSegment bestDuct = ducts[cand.DuctIndex];
                if (resolvedIds.Contains(bestDuct.Id)) continue;

                ApplyNearestTextNote(bestDuct, usableNotes[cand.NoteIndex], requireWidthMatch);
                resolvedIds.Add(bestDuct.Id);

                List<CadDuctSegment> singleSeed = new List<CadDuctSegment>(1);
                singleSeed.Add(bestDuct);
                PropagateBfsFromResolved(ducts, indexMap, geomWidthMap, resolvedIds, singleSeed);
            }
        }

        private static CadConnectionEdge FindConnectionEdge(CadDuctSegment a, CadDuctSegment b)
        {
            if (a == null || b == null || a.Connections == null) return null;
            for (int i = 0; i < a.Connections.Count; i++)
            {
                CadConnectionEdge edge = a.Connections[i];
                if ((edge.SegmentA == a && edge.SegmentB == b) || (edge.SegmentA == b && edge.SegmentB == a))
                {
                    return edge;
                }
            }
            return null;
        }

        private static void PropagateBfsFromResolved(
            List<CadDuctSegment> ducts,
            Dictionary<string, int> indexMap,
            Dictionary<string, double> geomWidthMap,
            HashSet<string> resolvedIds,
            List<CadDuctSegment> initialSeeds)
        {
            RunBfsPropagationPass(ducts, indexMap, geomWidthMap, resolvedIds, true, true, initialSeeds);
            RunBfsPropagationPass(ducts, indexMap, geomWidthMap, resolvedIds, true, false, initialSeeds);
            RunBfsPropagationPass(ducts, indexMap, geomWidthMap, resolvedIds, false, false, initialSeeds);
        }

        private static void RunBfsPropagationPass(
            List<CadDuctSegment> ducts,
            Dictionary<string, int> indexMap,
            Dictionary<string, double> geomWidthMap,
            HashSet<string> resolvedIds,
            bool sameWidthOnly,
            bool samePathContinuationOnly,
            List<CadDuctSegment> initialSeeds)
        {
            Queue<CadDuctSegment> queue = new Queue<CadDuctSegment>();
            if (initialSeeds != null && initialSeeds.Count > 0)
            {
                for (int i = 0; i < initialSeeds.Count; i++)
                {
                    CadDuctSegment s = initialSeeds[i];
                    if (resolvedIds.Contains(s.Id) && s.HeightMm > 0) queue.Enqueue(s);
                }
            }
            else
            {
                for (int i = 0; i < ducts.Count; i++)
                {
                    CadDuctSegment d = ducts[i];
                    if (resolvedIds.Contains(d.Id) && d.HeightMm > 0) queue.Enqueue(d);
                }
            }

            while (queue.Count > 0)
            {
                CadDuctSegment current = queue.Dequeue();
                double currGeomW;
                if (!geomWidthMap.TryGetValue(current.Id, out currGeomW)) currGeomW = current.WidthMm;

                for (int ni = 0; ni < current.ConnectedNeighbors.Count; ni++)
                {
                    CadDuctSegment neighbor = current.ConnectedNeighbors[ni];
                    if (resolvedIds.Contains(neighbor.Id)) continue;
                    if (!AreDuctSystemsCompatible(current, neighbor)) continue;

                    double neighGeomW;
                    if (!geomWidthMap.TryGetValue(neighbor.Id, out neighGeomW)) neighGeomW = neighbor.WidthMm;

                    CadConnectionEdge edge = FindConnectionEdge(current, neighbor);
                    if (samePathContinuationOnly && edge != null
                        && edge.Kind != CadConnectionKind.CollinearDirect
                        && edge.Kind != CadConnectionKind.Elbow)
                    {
                        continue;
                    }

                    bool geomSameWidth = neighGeomW <= 0 || Math.Abs(currGeomW - neighGeomW) <= 60.0;
                    bool currentSameWidth = neighbor.WidthMm <= 0 || Math.Abs(current.WidthMm - neighbor.WidthMm) <= 60.0;

                    if (sameWidthOnly && !geomSameWidth && !currentSameWidth) continue;

                    bool isSameRunContinuation = (edge == null || edge.Kind == CadConnectionKind.CollinearDirect || edge.Kind == CadConnectionKind.Elbow)
                        && geomSameWidth;

                    if (neighbor.WidthMm <= 0 || (isSameRunContinuation && current.WidthMm > 0))
                    {
                        neighbor.WidthMm = current.WidthMm;
                        neighbor.Width2Mm = current.WidthMm;
                    }
                    neighbor.HeightMm = current.HeightMm;
                    neighbor.TextLabel = string.Format("{0}x{1} (Lan truyền)", (int)neighbor.WidthMm, (int)neighbor.HeightMm);
                    neighbor.SizeSource = string.Format("Lan truyền từ tuyến gần nhất (#{0}: {1}x{2})",
                        indexMap[current.Id], (int)current.WidthMm, (int)current.HeightMm);
                    resolvedIds.Add(neighbor.Id);
                    queue.Enqueue(neighbor);
                }
            }
        }

        private static void ApplyNearestTextNote(CadDuctSegment duct, CadTextNote note, bool widthMatched)
        {
            // QUY TẮC BẢN VẼ HVAC: Số đầu tiên (note.WidthMm) LUÔN là W, số thứ hai (note.HeightMm) LUÔN là H!
            if (widthMatched || duct.WidthMm <= 0)
            {
                duct.WidthMm = note.WidthMm;
                duct.Width2Mm = note.WidthMm;
            }
            duct.HeightMm = note.HeightMm;
            duct.TextLabel = string.Format("{0}x{1} (Text gần nhất)", (int)duct.WidthMm, (int)duct.HeightMm);
            duct.SizeSource = string.Format("Lan truyền từ Text gần nhất ({0})", note.RawText);
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

            int validCount = 0;
            int skippedInvalidSizeCount = 0;
            foreach (CadDuctSegment d in ducts)
            {
                if (d.WidthMm > 0 && d.HeightMm > 0) validCount++;
                else skippedInvalidSizeCount++;
            }

            if (validCount == 0)
            {
                summaryReport = "⚠ Không thể dựng: tất cả các đoạn ống đều chưa có kích thước WxH hợp lệ (Height <= 0). Vui lòng nhập WxH trên bảng xem trước.";
                return 0;
            }

            // Đảm bảo các tuyến ống nối tiếp qua Quần ống gió / Chạc Y đã được gộp thành ống xuyên suốt trước khi tính đầu mút BIM
            if (edges != null && edges.Count > 0)
            {
                MergeCollinearTapHosts(ducts, edges);
                AssignFallbackTapBevelDirections(ducts, edges);
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
                PrepareBimJunctionEndpoints(ducts, edges, useConcentricTransition, adjustedStarts, adjustedEnds);
            }

            using (TransactionGroup tg = new TransactionGroup(doc, "HDTOOL - Dựng Ống Gió 3D Từ Hình Vẽ CAD"))
            {
                tg.Start();

                Dictionary<string, Duct> ductMap = new Dictionary<string, Duct>();

                // PHA 1: Tạo toàn bộ đoạn ống gió thẳng
                using (Transaction txDucts = new Transaction(doc, "HDTOOL - Tạo Ống Gió Thẳng"))
                {
                    AttachSilentFailureHandling(txDucts);
                    txDucts.Start();

                    EnsureDuctTypeRoutingPreferences(doc, ductTypeId, useTakeoffTap);

                    foreach (CadDuctSegment seg in ducts)
                    {
                        if (seg.WidthMm <= 0 || seg.HeightMm <= 0) continue;

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
                    txDucts.Commit();
                }

                if (edges != null && edges.Count > 0)
                {
                    // PHA 2: Nối đầu-đối-đầu TRƯỚC (Transition, CollinearDirect, Elbow) để cố định hình học đầu mút ống trước khi gắn Tap
                    using (Transaction txEndFits = new Transaction(doc, "HDTOOL - Nối Côn & Cút"))
                    {
                        AttachSilentFailureHandling(txEndFits);
                        txEndFits.Start();

                        foreach (CadConnectionEdge edge in edges)
                        {
                            if (edge.Kind != CadConnectionKind.Transition
                                && edge.Kind != CadConnectionKind.Elbow
                                && edge.Kind != CadConnectionKind.CollinearDirect)
                            {
                                continue;
                            }

                            Duct d1, d2;
                            if (!ductMap.TryGetValue(edge.SegmentA.Id, out d1) || !ductMap.TryGetValue(edge.SegmentB.Id, out d2)) continue;
                            if (!d1.IsValidObject || !d2.IsValidObject) continue;

                            try
                            {
                                Connector c1, c2;
                                FindBestFacingConnectors(d1, d2, out c1, out c2);
                                if (c1 == null || c2 == null || c1.IsConnected || c2.IsConnected) continue;

                                if (edge.Kind == CadConnectionKind.Transition)
                                {
                                    if (c1.CoordinateSystem.BasisZ.DotProduct(c2.CoordinateSystem.BasisZ) <= -0.92
                                        && c1.Origin.DistanceTo(c2.Origin) <= 2500.0 * MM_TO_FEET)
                                    {
                                        FamilyInstance trans = doc.Create.NewTransitionFitting(c1, c2);
                                        if (trans != null) createdTransitionCount++;
                                    }
                                }
                                else if (edge.Kind == CadConnectionKind.Elbow)
                                {
                                    double zDot = Math.Abs(c1.CoordinateSystem.BasisZ.DotProduct(c2.CoordinateSystem.BasisZ));
                                    if (zDot < 0.96)
                                    {
                                        FamilyInstance elb = doc.Create.NewElbowFitting(c1, c2);
                                        if (elb != null) createdElbowCount++;
                                    }
                                }
                                else if (edge.Kind == CadConnectionKind.CollinearDirect)
                                {
                                    if (c1.CoordinateSystem.BasisZ.DotProduct(c2.CoordinateSystem.BasisZ) <= -0.92)
                                    {
                                        if (Math.Abs(d1.Width - d2.Width) > 0.01 || Math.Abs(d1.Height - d2.Height) > 0.01)
                                        {
                                            FamilyInstance trans = doc.Create.NewTransitionFitting(c1, c2);
                                            if (trans != null) createdTransitionCount++;
                                        }
                                        else if (c1.Origin.DistanceTo(c2.Origin) <= 8.0 * MM_TO_FEET)
                                        {
                                            c1.ConnectTo(c2);
                                        }
                                        else if (c1.Origin.DistanceTo(c2.Origin) <= 1500.0 * MM_TO_FEET)
                                        {
                                            FamilyInstance union = doc.Create.NewTransitionFitting(c1, c2);
                                            if (union != null) createdTransitionCount++;
                                        }
                                    }
                                }
                            }
                            catch { }
                        }

                        doc.Regenerate();
                        txEndFits.Commit();
                    }

                    // PHA 3: Gắn toàn bộ Chân rẽ TakeoffTap / Tee (cô lập từng Tap trong Transaction riêng để 1 Tap lỗi không kéo sập cả mạng & tự động xoay đúng chiều vát 45° theo CAD)
                    if (useTakeoffTap)
                    {
                        List<CadConnectionEdge> teeEdges = edges.Where(e => e.Kind == CadConnectionKind.TeeBranch).ToList();
                        HashSet<string> handledTeeIds = new HashSet<string>();

                        foreach (var grp in teeEdges.GroupBy(e => e.SegmentA.Id))
                        {
                            List<CadConnectionEdge> list = grp.ToList();
                            Duct mainDuct;
                            if (list.Count >= 2 && ductMap.TryGetValue(grp.Key, out mainDuct) && mainDuct.IsValidObject)
                            {
                                Duct b1, b2;
                                if (ductMap.TryGetValue(list[0].SegmentB.Id, out b1) && b1.IsValidObject
                                    && ductMap.TryGetValue(list[1].SegmentB.Id, out b2) && b2.IsValidObject)
                                {
                                    using (Transaction txTeeTap = new Transaction(doc, "HDTOOL - Gắn Chân Rẽ Đôi"))
                                    {
                                        AttachSilentFailureHandling(txTeeTap);
                                        txTeeTap.Start();
                                        bool ok = TryConnectTeeOrDoubleTap(doc, mainDuct, b1, b2, useTakeoffTap, ref createdTeeCount, ref createdTakeoffCount);
                                        if (ok)
                                        {
                                            txTeeTap.Commit();
                                            handledTeeIds.Add(list[0].SegmentB.Id);
                                            handledTeeIds.Add(list[1].SegmentB.Id);
                                        }
                                        else
                                        {
                                            txTeeTap.RollBack();
                                        }
                                    }
                                }
                            }
                        }

                        foreach (CadConnectionEdge edge in edges)
                        {
                            if (edge.Kind != CadConnectionKind.TakeoffTap && edge.Kind != CadConnectionKind.TeeBranch) continue;
                            if (edge.Kind == CadConnectionKind.TeeBranch && handledTeeIds.Contains(edge.SegmentB.Id)) continue;

                            Duct d1, d2;
                            if (!ductMap.TryGetValue(edge.SegmentA.Id, out d1) || !ductMap.TryGetValue(edge.SegmentB.Id, out d2)) continue;
                            if (!d1.IsValidObject || !d2.IsValidObject) continue;

                            using (Transaction txOneTap = new Transaction(doc, "HDTOOL - Gắn Chân Rẽ Tap"))
                            {
                                AttachSilentFailureHandling(txOneTap);
                                txOneTap.Start();
                                bool committed = false;
                                try
                                {
                                    Connector cBranch = FindClosestConnectorToSegment(d2, d1);
                                    if (cBranch != null && !cBranch.IsConnected)
                                    {
                                        if (TryEnsureTapHostCoversBranch(doc, cBranch, d1, d2))
                                        {
                                            FamilyInstance tap = doc.Create.NewTakeoffFitting(cBranch, d1);
                                            if (tap != null)
                                            {
                                                XYZ expectedBevel = edge.TapBevelDirection ?? (edge.BridgePolygon != null ? edge.BridgePolygon.TapBevelDirection : null);
                                                if (expectedBevel != null)
                                                {
                                                    EnsureTakeoffTapBevelOrientation(doc, tap, cBranch, d1, d2, expectedBevel);
                                                }
                                                createdTakeoffCount++;
                                                committed = (txOneTap.Commit() == TransactionStatus.Committed);
                                            }
                                        }
                                    }
                                }
                                catch { }
                                if (!committed && txOneTap.HasStarted())
                                {
                                    try { txOneTap.RollBack(); } catch { }
                                }
                            }
                        }
                    }
                    else
                    {
                        using (Transaction txTaps = new Transaction(doc, "HDTOOL - Gắn Chạc Tee"))
                        {
                            AttachSilentFailureHandling(txTaps);
                            txTaps.Start();

                            List<CadConnectionEdge> teeEdges = edges.Where(e => e.Kind == CadConnectionKind.TeeBranch).ToList();
                            HashSet<string> handledTeeIds = new HashSet<string>();

                            foreach (var grp in teeEdges.GroupBy(e => e.SegmentA.Id))
                            {
                                List<CadConnectionEdge> list = grp.ToList();
                                Duct mainDuct;
                                if (list.Count >= 2 && ductMap.TryGetValue(grp.Key, out mainDuct) && mainDuct.IsValidObject)
                                {
                                    Duct b1, b2;
                                    if (ductMap.TryGetValue(list[0].SegmentB.Id, out b1) && b1.IsValidObject
                                        && ductMap.TryGetValue(list[1].SegmentB.Id, out b2) && b2.IsValidObject)
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
                                if (edge.Kind != CadConnectionKind.TakeoffTap && edge.Kind != CadConnectionKind.TeeBranch) continue;
                                if (edge.Kind == CadConnectionKind.TeeBranch && handledTeeIds.Contains(edge.SegmentB.Id)) continue;

                                Duct d1, d2;
                                if (!ductMap.TryGetValue(edge.SegmentA.Id, out d1) || !ductMap.TryGetValue(edge.SegmentB.Id, out d2)) continue;
                                if (!d1.IsValidObject || !d2.IsValidObject) continue;

                                try
                                {
                                    Connector cBranch = FindClosestConnectorToSegment(d2, d1);
                                    if (cBranch != null && !cBranch.IsConnected)
                                    {
                                        Connector cMain = FindClosestConnectorPair(d1, d2, true);
                                        if (cMain != null && !cMain.IsConnected)
                                        {
                                            FamilyInstance elb = doc.Create.NewElbowFitting(cMain, cBranch);
                                            if (elb != null) createdElbowCount++;
                                        }
                                    }
                                }
                                catch { }
                            }

                            txTaps.Commit();
                        }
                    }
                }

                tg.Assimilate();
            }

            summaryReport = string.Format(
                "✔ Dựng thành công: {0} ống gió thẳng | {1} côn thu đồng tâm | {2} cút chuyển hướng | {3} chân rẽ Tap{4}{5}!",
                createdDuctCount,
                createdTransitionCount,
                createdElbowCount,
                createdTakeoffCount,
                createdTeeCount > 0 ? string.Format(" | {0} chạc chia Tee", createdTeeCount) : "",
                skippedInvalidSizeCount > 0 ? string.Format(" | Bỏ qua {0} đoạn thiếu WxH", skippedInvalidSizeCount) : "");

            return createdDuctCount;
        }

        private static void AttachSilentFailureHandling(Transaction tx)
        {
            if (tx == null) return;
            FailureHandlingOptions opts = tx.GetFailureHandlingOptions();
            opts.SetFailuresPreprocessor(new SilentFittingFailuresPreprocessor());
            opts.SetClearAfterRollback(true);
            tx.SetFailureHandlingOptions(opts);
        }

        private class SilentFittingFailuresPreprocessor : IFailuresPreprocessor
        {
            public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
            {
                if (failuresAccessor == null) return FailureProcessingResult.Continue;
                IList<FailureMessageAccessor> msgs = failuresAccessor.GetFailureMessages();
                if (msgs == null || msgs.Count == 0) return FailureProcessingResult.Continue;

                for (int i = 0; i < msgs.Count; i++)
                {
                    FailureMessageAccessor fma = msgs[i];
                    FailureSeverity sev = fma.GetSeverity();
                    if (sev == FailureSeverity.Warning)
                    {
                        failuresAccessor.DeleteWarning(fma);
                    }
                    else if (sev == FailureSeverity.Error)
                    {
                        // Không bao giờ cho phép ResolveFailure tự xóa ống gió hoặc hiện hộp thoại lỗi; RollBack an toàn transaction cục bộ
                        return FailureProcessingResult.ProceedWithRollBack;
                    }
                }

                return FailureProcessingResult.Continue;
            }
        }

        private static double MeasureTapBevelOffsetAlong(FamilyInstance tap, XYZ branchOrigin, XYZ uDir)
        {
            if (tap == null || branchOrigin == null || uDir == null) return 0.0;
            double minT = double.MaxValue;
            double maxT = double.MinValue;
            try
            {
                Options opt = new Options();
                opt.DetailLevel = ViewDetailLevel.Fine;
                opt.IncludeNonVisibleObjects = false;
                GeometryElement ge = tap.get_Geometry(opt);
                if (ge != null)
                {
                    AccumulateTapSolidProjectionRange(ge, Transform.Identity, branchOrigin, uDir, ref minT, ref maxT);
                }
            }
            catch { }

            if (minT > maxT)
            {
                try
                {
                    BoundingBoxXYZ bb = tap.get_BoundingBox(null);
                    if (bb != null && bb.Min != null && bb.Max != null)
                    {
                        XYZ[] corners = new XYZ[]
                        {
                            new XYZ(bb.Min.X, bb.Min.Y, bb.Min.Z),
                            new XYZ(bb.Max.X, bb.Min.Y, bb.Min.Z),
                            new XYZ(bb.Min.X, bb.Max.Y, bb.Min.Z),
                            new XYZ(bb.Max.X, bb.Max.Y, bb.Min.Z)
                        };
                        for (int i = 0; i < corners.Length; i++)
                        {
                            double t = (corners[i].X - branchOrigin.X) * uDir.X + (corners[i].Y - branchOrigin.Y) * uDir.Y;
                            if (t < minT) minT = t;
                            if (t > maxT) maxT = t;
                        }
                    }
                }
                catch { }
            }

            if (minT > maxT) return 0.0;
            return (minT + maxT) * 0.5;
        }

        private static void AccumulateTapSolidProjectionRange(
            GeometryElement ge,
            Transform xform,
            XYZ branchOrigin,
            XYZ uDir,
            ref double minT,
            ref double maxT)
        {
            if (ge == null) return;
            foreach (GeometryObject obj in ge)
            {
                GeometryInstance gi = obj as GeometryInstance;
                if (gi != null)
                {
                    GeometryElement sub = gi.GetInstanceGeometry();
                    if (sub != null)
                    {
                        AccumulateTapSolidProjectionRange(sub, xform, branchOrigin, uDir, ref minT, ref maxT);
                    }
                    continue;
                }

                Solid solid = obj as Solid;
                if (solid != null && solid.Volume > 1e-8 && solid.Edges != null)
                {
                    foreach (Edge edge in solid.Edges)
                    {
                        IList<XYZ> pts = edge.Tessellate();
                        if (pts == null) continue;
                        for (int i = 0; i < pts.Count; i++)
                        {
                            XYZ w = xform.OfPoint(pts[i]);
                            double t = (w.X - branchOrigin.X) * uDir.X + (w.Y - branchOrigin.Y) * uDir.Y;
                            if (t < minT) minT = t;
                            if (t > maxT) maxT = t;
                        }
                    }
                }
            }
        }

        private static bool IsTapConnectedToDuct(FamilyInstance tap, Duct duct)
        {
            if (tap == null || !tap.IsValidObject || duct == null || !duct.IsValidObject) return false;
            try
            {
                MEPModel mep = tap.MEPModel;
                if (mep == null || mep.ConnectorManager == null) return false;
                foreach (Connector c in mep.ConnectorManager.Connectors)
                {
                    if (!c.IsConnected) continue;
                    foreach (Connector r in c.AllRefs)
                    {
                        if (r != null && r.Owner != null && r.Owner.Id == duct.Id) return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private static void EnsureTakeoffTapBevelOrientation(
            Document doc,
            FamilyInstance tap,
            Connector cBranch,
            Duct mainDuct,
            Duct branchDuct,
            XYZ expectedBevelDir)
        {
            if (doc == null || tap == null || !tap.IsValidObject || cBranch == null || expectedBevelDir == null) return;
            XYZ uBevel = new XYZ(expectedBevelDir.X, expectedBevelDir.Y, 0.0);
            if (uBevel.GetLength() < 1e-6) return;
            uBevel = uBevel.Normalize();

            try { doc.Regenerate(); } catch { return; }

            XYZ branchOrigin = cBranch.Origin;
            double initialOffset = MeasureTapBevelOffsetAlong(tap, branchOrigin, uBevel);
            double tol = 10.0 * MM_TO_FEET;
            if (initialOffset >= tol) return;
            if (initialOffset > -tol) return; // Chân rẽ vuông góc không có vát 45°

            // Thử 1: flipHand()
            if (tap.CanFlipHand)
            {
                try
                {
                    tap.flipHand();
                    doc.Regenerate();
                    if (MeasureTapBevelOffsetAlong(tap, branchOrigin, uBevel) >= tol) return;
                    tap.flipHand();
                    doc.Regenerate();
                }
                catch { }
            }

            // Thử 2: flipFacing()
            if (tap.CanFlipFacing)
            {
                try
                {
                    tap.flipFacing();
                    doc.Regenerate();
                    if (MeasureTapBevelOffsetAlong(tap, branchOrigin, uBevel) >= tol) return;
                    tap.flipFacing();
                    doc.Regenerate();
                }
                catch { }
            }

            // Thử 3: Kết hợp flipHand() + flipFacing()
            if (tap.CanFlipHand && tap.CanFlipFacing)
            {
                try
                {
                    tap.flipHand();
                    tap.flipFacing();
                    doc.Regenerate();
                    if (MeasureTapBevelOffsetAlong(tap, branchOrigin, uBevel) >= tol) return;
                    tap.flipHand();
                    tap.flipFacing();
                    doc.Regenerate();
                }
                catch { }
            }

            // Thử 4: Xoay 180° quanh trục nhánh nếu family Tap khóa cả flipHand/flipFacing
            try
            {
                XYZ bAxisDir = cBranch.CoordinateSystem.BasisZ;
                if (bAxisDir != null && bAxisDir.GetLength() > 1e-6)
                {
                    using (SubTransaction st = new SubTransaction(doc))
                    {
                        st.Start();
                        Line rotAxis = Line.CreateUnbound(branchOrigin, bAxisDir.Normalize());
                        ElementTransformUtils.RotateElement(doc, tap.Id, rotAxis, Math.PI);
                        doc.Regenerate();
                        if (tap.IsValidObject
                            && MeasureTapBevelOffsetAlong(tap, branchOrigin, uBevel) >= tol
                            && IsTapConnectedToDuct(tap, branchDuct)
                            && IsTapConnectedToDuct(tap, mainDuct))
                        {
                            st.Commit();
                            return;
                        }
                        st.RollBack();
                    }
                }
            }
            catch { }
        }

        private static XYZ GetSegmentAxisXY(CadDuctSegment seg)
        {
            if (seg == null || seg.StartPoint == null || seg.EndPoint == null) return XYZ.BasisX;
            XYZ v = new XYZ(seg.EndPoint.X - seg.StartPoint.X, seg.EndPoint.Y - seg.StartPoint.Y, 0.0);
            if (v.GetLength() < 1e-6)
            {
                if (seg.Direction != null)
                {
                    XYZ d = new XYZ(seg.Direction.X, seg.Direction.Y, 0.0);
                    if (d.GetLength() > 1e-6) return d.Normalize();
                }
                return XYZ.BasisX;
            }
            return v.Normalize();
        }

        private static void MergeCollinearTapHosts(List<CadDuctSegment> ducts, List<CadConnectionEdge> edges)
        {
            if (ducts == null || ducts.Count < 2 || edges == null || edges.Count == 0) return;

            bool anyMerged = false;
            bool changed;
            do
            {
                changed = false;
                for (int ei = 0; ei < edges.Count; ei++)
                {
                    CadConnectionEdge edge = edges[ei];
                    if (edge == null || edge.Kind != CadConnectionKind.CollinearDirect) continue;
                    CadDuctSegment a = edge.SegmentA;
                    CadDuctSegment b = edge.SegmentB;
                    if (a == null || b == null || a == b) continue;
                    if (a.ShapeKind != CadShapeKind.StraightDuct || b.ShapeKind != CadShapeKind.StraightDuct) continue;
                    if (!AreDuctSystemsCompatible(a, b)) continue;
                    if (Math.Abs(a.WidthMm - b.WidthMm) >= 15.0) continue;
                    if (a.HeightMm > 0 && b.HeightMm > 0 && Math.Abs(a.HeightMm - b.HeightMm) >= 15.0) continue;
                    if (Math.Abs(a.CenterElevationMm - b.CenterElevationMm) >= 15.0) continue;

                    XYZ uA = GetSegmentAxisXY(a);
                    XYZ uB = GetSegmentAxisXY(b);
                    if (Math.Abs(uA.DotProduct(uB)) < 0.96) continue;

                    double perpDist = LineToLineDistance(a.StartPoint, uA, b.StartPoint, uA);
                    if (perpDist > 45.0 * MM_TO_FEET) continue;

                    XYZ f1, far1, f2, far2;
                    double gapDist = FindClosestEndpointPairXY(a, b, out f1, out far1, out f2, out far2);
                    if (gapDist > 3000.0 * MM_TO_FEET) continue;

                    // Chỉ gộp khi mối nối là Quần ống gió / Chạc Y / Ngã 3 có Chân rẽ (BridgePolygon != null hoặc có TakeoffTap gần khớp nối)
                    bool hasTapNearGap = (edge.BridgePolygon != null);
                    XYZ gapRef = edge.JunctionPoint ?? ((f1 + f2) * 0.5);
                    if (!hasTapNearGap)
                    {
                        for (int ti = 0; ti < edges.Count; ti++)
                        {
                            CadConnectionEdge te = edges[ti];
                            if (te == null || (te.Kind != CadConnectionKind.TakeoffTap && te.Kind != CadConnectionKind.TeeBranch)) continue;
                            if (te.SegmentA == a || te.SegmentA == b)
                            {
                                XYZ jp = te.JunctionPoint ?? te.SegmentB.CenterPoint;
                                if (jp != null && DistXY(jp, gapRef) <= 2500.0 * MM_TO_FEET)
                                {
                                    hasTapNearGap = true;
                                    break;
                                }
                            }
                        }
                    }
                    if (!hasTapNearGap) continue;

                    CadDuctSegment keeper = (a.HasDirectText && !b.HasDirectText) ? a
                        : ((b.HasDirectText && !a.HasDirectText) ? b : ((a.Length >= b.Length) ? a : b));
                    CadDuctSegment victim = (keeper == a) ? b : a;

                    XYZ dirK = GetSegmentAxisXY(keeper);
                    double zK = keeper.StartPoint.Z;
                    XYZ originK = keeper.StartPoint;
                    XYZ[] candPts = new XYZ[] { keeper.StartPoint, keeper.EndPoint, victim.StartPoint, victim.EndPoint };
                    double minT = double.MaxValue, maxT = double.MinValue;
                    for (int pi = 0; pi < candPts.Length; pi++)
                    {
                        double t = (candPts[pi].X - originK.X) * dirK.X + (candPts[pi].Y - originK.Y) * dirK.Y;
                        if (t < minT) minT = t;
                        if (t > maxT) maxT = t;
                    }
                    XYZ newStart = new XYZ(originK.X + dirK.X * minT, originK.Y + dirK.Y * minT, zK);
                    XYZ newEnd = new XYZ(originK.X + dirK.X * maxT, originK.Y + dirK.Y * maxT, zK);
                    keeper.StartPoint = newStart;
                    keeper.EndPoint = newEnd;
                    keeper.CenterPoint = (newStart + newEnd) * 0.5;
                    keeper.Direction = dirK;
                    keeper.Length = newStart.DistanceTo(newEnd);
                    keeper.HasDirectText = keeper.HasDirectText || victim.HasDirectText;
                    if (keeper.HeightMm <= 0 && victim.HeightMm > 0)
                    {
                        keeper.HeightMm = victim.HeightMm;
                        keeper.TextLabel = victim.TextLabel;
                        keeper.SizeSource = victim.SizeSource;
                    }

                    // Thay thế toàn bộ tham chiếu victim -> keeper trong danh sách edges
                    for (int k = edges.Count - 1; k >= 0; k--)
                    {
                        CadConnectionEdge ce = edges[k];
                        if (ce == null) { edges.RemoveAt(k); continue; }
                        if (ce.SegmentA == victim) ce.SegmentA = keeper;
                        if (ce.SegmentB == victim) ce.SegmentB = keeper;
                        if (ce.SegmentA == ce.SegmentB)
                        {
                            edges.RemoveAt(k);
                        }
                    }

                    // Khử trùng lặp cặp (SegmentA, SegmentB) sau khi gộp
                    HashSet<string> seenPairs = new HashSet<string>();
                    for (int k = 0; k < edges.Count; )
                    {
                        CadConnectionEdge ce = edges[k];
                        string id1 = ce.SegmentA.Id;
                        string id2 = ce.SegmentB.Id;
                        string pk = string.CompareOrdinal(id1, id2) <= 0 ? (id1 + "|" + id2) : (id2 + "|" + id1);
                        if (!seenPairs.Add(pk))
                        {
                            edges.RemoveAt(k);
                        }
                        else
                        {
                            if (ce.Kind == CadConnectionKind.TakeoffTap && ce.SegmentA == keeper)
                            {
                                XYZ tapJunc = LineLineIntersectXY(keeper.StartPoint, keeper.Direction, ce.SegmentB.StartPoint, ce.SegmentB.Direction, keeper.StartPoint.Z);
                                if (tapJunc != null) ce.JunctionPoint = tapJunc;
                            }
                            k++;
                        }
                    }

                    ducts.Remove(victim);
                    anyMerged = true;
                    changed = true;
                    break;
                }
            } while (changed);

            if (anyMerged)
            {
                foreach (CadDuctSegment d in ducts)
                {
                    if (d.ConnectedNeighbors == null) d.ConnectedNeighbors = new List<CadDuctSegment>();
                    else d.ConnectedNeighbors.Clear();
                    if (d.Connections == null) d.Connections = new List<CadConnectionEdge>();
                    else d.Connections.Clear();
                }
                foreach (CadConnectionEdge edge in edges)
                {
                    if (edge == null || edge.SegmentA == null || edge.SegmentB == null) continue;
                    if (!edge.SegmentA.Connections.Contains(edge)) edge.SegmentA.Connections.Add(edge);
                    if (!edge.SegmentB.Connections.Contains(edge)) edge.SegmentB.Connections.Add(edge);
                    if (!edge.SegmentA.ConnectedNeighbors.Contains(edge.SegmentB)) edge.SegmentA.ConnectedNeighbors.Add(edge.SegmentB);
                    if (!edge.SegmentB.ConnectedNeighbors.Contains(edge.SegmentA)) edge.SegmentB.ConnectedNeighbors.Add(edge.SegmentA);
                }
            }
        }

        private static void AssignFallbackTapBevelDirections(List<CadDuctSegment> ducts, List<CadConnectionEdge> edges)
        {
            if (edges == null || edges.Count == 0) return;

            for (int i = 0; i < edges.Count; i++)
            {
                CadConnectionEdge edge = edges[i];
                if (edge == null || edge.Kind != CadConnectionKind.TakeoffTap || edge.SegmentA == null || edge.SegmentB == null) continue;

                XYZ mainDir = GetSegmentAxisXY(edge.SegmentA);

                // 1. Ưu tiên tuyệt đối hướng vát được trích xuất trực tiếp từ hình thang Shoe-Tap CAD
                XYZ rawBevel = edge.TapBevelDirection ?? (edge.BridgePolygon != null ? edge.BridgePolygon.TapBevelDirection : null);
                if (rawBevel != null)
                {
                    double dot = rawBevel.X * mainDir.X + rawBevel.Y * mainDir.Y;
                    if (Math.Abs(dot) > 0.25)
                    {
                        edge.TapBevelDirection = (dot >= 0.0) ? mainDir : mainDir.Negate();
                        continue;
                    }
                }

                // 2. Fallback cho Quần ống gió / Ngã 3 không có hình thang Shoe-Tap: hướng về phía nguồn ống lớn hơn trên trục thân chính
                XYZ junc = edge.JunctionPoint ?? edge.SegmentB.CenterPoint;
                if (junc == null) continue;
                double jT = (junc.X - edge.SegmentA.StartPoint.X) * mainDir.X + (junc.Y - edge.SegmentA.StartPoint.Y) * mainDir.Y;

                double posUpstreamScore = 0.0;
                double negUpstreamScore = 0.0;
                double mainArea = edge.SegmentA.WidthMm * Math.Max(edge.SegmentA.HeightMm, 100.0);

                for (int k = 0; k < edges.Count; k++)
                {
                    CadConnectionEdge ne = edges[k];
                    if (ne == null || ne == edge) continue;
                    CadDuctSegment mate = (ne.SegmentA == edge.SegmentA) ? ne.SegmentB : ((ne.SegmentB == edge.SegmentA) ? ne.SegmentA : null);
                    if (mate == null || mate == edge.SegmentB) continue;

                    double mateArea = mate.WidthMm * Math.Max(mate.HeightMm, 100.0);
                    if (mateArea <= mainArea * 1.05) continue;

                    XYZ mp = ne.JunctionPoint ?? mate.CenterPoint;
                    if (mp == null) continue;
                    double mT = (mp.X - edge.SegmentA.StartPoint.X) * mainDir.X + (mp.Y - edge.SegmentA.StartPoint.Y) * mainDir.Y;
                    double dt = mT - jT;
                    if (Math.Abs(dt) < 30.0 * MM_TO_FEET) continue;
                    if (dt > 0.0 && mateArea > posUpstreamScore) posUpstreamScore = mateArea;
                    else if (dt < 0.0 && mateArea > negUpstreamScore) negUpstreamScore = mateArea;
                }

                if (posUpstreamScore > 0.0 && negUpstreamScore <= 0.0)
                {
                    edge.TapBevelDirection = mainDir;
                }
                else if (negUpstreamScore > 0.0 && posUpstreamScore <= 0.0)
                {
                    edge.TapBevelDirection = mainDir.Negate();
                }
            }
        }

        private static bool IsEndConnectorConnected(Duct duct, XYZ endPt)
        {
            if (duct == null || endPt == null) return true;
            foreach (Connector c in duct.ConnectorManager.Connectors)
            {
                if (c.ConnectorType == ConnectorType.End && c.Origin.DistanceTo(endPt) < 15.0 * MM_TO_FEET)
                {
                    return c.IsConnected;
                }
            }
            return false;
        }

        private static bool TryEnsureTapHostCoversBranch(Document doc, Connector cBranch, Duct mainDuct, Duct branchDuct)
        {
            if (doc == null || cBranch == null || mainDuct == null || branchDuct == null) return false;
            LocationCurve lcMain = mainDuct.Location as LocationCurve;
            if (lcMain == null) return false;
            Line line = lcMain.Curve as Line;
            if (line == null) return false;

            XYZ p0 = line.GetEndPoint(0);
            XYZ p1 = line.GetEndPoint(1);
            double mainLen = p0.DistanceTo(p1);
            if (mainLen < 40.0 * MM_TO_FEET) return false;
            XYZ u = (p1 - p0).Normalize();

            XYZ bAxis = cBranch.CoordinateSystem.BasisZ;
            double cosAng = Math.Abs(u.DotProduct(bAxis));
            if (cosAng >= 0.92) return false;

            double t = (cBranch.Origin - p0).DotProduct(u);
            XYZ proj = p0 + u * t;
            double latDist = cBranch.Origin.DistanceTo(proj);
            double maxLat = mainDuct.Width * 0.5 + branchDuct.Width * 0.75 + 600.0 * MM_TO_FEET;
            if (latDist > maxLat) return false;

            double sinAng = Math.Sqrt(Math.Max(0.25, 1.0 - cosAng * cosAng));
            double bevelSafety = Math.Max(160.0 * MM_TO_FEET, branchDuct.Width * 0.28);
            double margin = branchDuct.Width * 0.5 / sinAng + bevelSafety;
            double maxSelfExtend = 4500.0 * MM_TO_FEET;

            XYZ newP0 = p0;
            XYZ newP1 = p1;
            bool changed = false;

            if (t < margin)
            {
                if (!IsEndConnectorConnected(mainDuct, p0) && (margin - t) <= maxSelfExtend)
                {
                    newP0 = p0 + u * (t - margin);
                    changed = true;
                }
                else if (t < 0.02)
                {
                    return false;
                }
            }

            if (t > mainLen - margin)
            {
                if (!IsEndConnectorConnected(mainDuct, p1) && (t + margin - mainLen) <= maxSelfExtend)
                {
                    newP1 = p0 + u * (t + margin);
                    changed = true;
                }
                else if (t > mainLen - 0.02)
                {
                    return false;
                }
            }

            if (changed)
            {
                try
                {
                    lcMain.Curve = Line.CreateBound(new XYZ(newP0.X, newP0.Y, p0.Z), new XYZ(newP1.X, newP1.Y, p1.Z));
                    doc.Regenerate();
                }
                catch { return false; }
            }

            return true;
        }

        private static void EnsureDuctTypeRoutingPreferences(Document doc, ElementId ductTypeId, bool useTakeoffTap)
        {
            if (doc == null || ductTypeId == null || ductTypeId == ElementId.InvalidElementId) return;
            try
            {
                DuctType dt = doc.GetElement(ductTypeId) as DuctType;
                if (dt == null) return;
                RoutingPreferenceManager rpm = dt.RoutingPreferenceManager;
                if (rpm == null) return;

                // Ưu tiên tuyệt đối các Rule/Family đã được cấu hình sẵn trên DuctType của dự án
                bool needElbow = rpm.GetNumberOfRules(RoutingPreferenceRuleGroupType.Elbows) == 0;
                bool needTrans = rpm.GetNumberOfRules(RoutingPreferenceRuleGroupType.Transitions) == 0;
                bool hasTapRule = false;
                bool hasTeeRule = false;

                int juncCount = rpm.GetNumberOfRules(RoutingPreferenceRuleGroupType.Junctions);
                for (int i = 0; i < juncCount; i++)
                {
                    RoutingPreferenceRule r = rpm.GetRule(RoutingPreferenceRuleGroupType.Junctions, i);
                    if (r == null) continue;
                    PartType pt = GetFittingSymbolPartType(doc, r.MEPPartId);
                    if (pt == PartType.TapAdjustable || pt == PartType.TapPerpendicular) hasTapRule = true;
                    else if (pt == PartType.Tee || pt == PartType.Wye) hasTeeRule = true;
                }

                bool needTapRule = useTakeoffTap && !hasTapRule;
                bool needTeeRule = !useTakeoffTap && !hasTeeRule && juncCount == 0;

                if (needElbow || needTrans || needTapRule || needTeeRule)
                {
                    FilteredElementCollector fitCol = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_DuctFitting)
                        .OfClass(typeof(FamilySymbol));

                    List<FamilySymbol> allSymbols = new List<FamilySymbol>();
                    foreach (Element e in fitCol)
                    {
                        FamilySymbol fs = e as FamilySymbol;
                        if (fs != null) allSymbols.Add(fs);
                    }

                    if (needElbow)
                    {
                        FamilySymbol elbowSym = PickBestExistingFittingSymbol(allSymbols, PartType.Elbow, PartType.Undefined);
                        if (elbowSym != null)
                        {
                            if (!elbowSym.IsActive) elbowSym.Activate();
                            rpm.AddRule(RoutingPreferenceRuleGroupType.Elbows, new RoutingPreferenceRule(elbowSym.Id, "HDTOOL Existing Elbow"), 0);
                        }
                    }

                    if (needTrans)
                    {
                        FamilySymbol transSym = PickBestExistingFittingSymbol(allSymbols, PartType.Transition, PartType.Undefined);
                        if (transSym != null)
                        {
                            if (!transSym.IsActive) transSym.Activate();
                            rpm.AddRule(RoutingPreferenceRuleGroupType.Transitions, new RoutingPreferenceRule(transSym.Id, "HDTOOL Existing Transition"), 0);
                        }
                    }

                    if (needTapRule)
                    {
                        FamilySymbol tapSym = PickBestExistingFittingSymbol(allSymbols, PartType.TapAdjustable, PartType.TapPerpendicular);
                        if (tapSym != null)
                        {
                            if (!tapSym.IsActive) tapSym.Activate();
                            rpm.AddRule(RoutingPreferenceRuleGroupType.Junctions, new RoutingPreferenceRule(tapSym.Id, "HDTOOL Existing Tap"), 0);
                        }
                    }

                    if (needTeeRule)
                    {
                        FamilySymbol teeSym = PickBestExistingFittingSymbol(allSymbols, PartType.Tee, PartType.Wye);
                        if (teeSym != null)
                        {
                            if (!teeSym.IsActive) teeSym.Activate();
                            rpm.AddRule(RoutingPreferenceRuleGroupType.Junctions, new RoutingPreferenceRule(teeSym.Id, "HDTOOL Existing Tee"), 0);
                        }
                    }
                }

                try
                {
                    PreferredJunctionType desired = useTakeoffTap ? PreferredJunctionType.Tap : PreferredJunctionType.Tee;
                    if (rpm.PreferredJunctionType != desired)
                    {
                        rpm.PreferredJunctionType = desired;
                    }
                }
                catch { }
            }
            catch { }
        }

        private static PartType GetFittingSymbolPartType(Document doc, ElementId symId)
        {
            if (doc == null || symId == null || symId == ElementId.InvalidElementId) return PartType.Undefined;
            FamilySymbol fs = doc.GetElement(symId) as FamilySymbol;
            if (fs == null || fs.Family == null) return PartType.Undefined;
            Parameter p = fs.Family.get_Parameter(BuiltInParameter.FAMILY_CONTENT_PART_TYPE);
            if (p != null && p.HasValue)
            {
                return (PartType)p.AsInteger();
            }
            return PartType.Undefined;
        }

        private static FamilySymbol PickBestExistingFittingSymbol(List<FamilySymbol> symbols, PartType primaryType, PartType fallbackType)
        {
            FamilySymbol best = null;
            int bestScore = int.MinValue;

            for (int i = 0; i < symbols.Count; i++)
            {
                FamilySymbol fs = symbols[i];
                if (fs.Family == null) continue;
                Parameter p = fs.Family.get_Parameter(BuiltInParameter.FAMILY_CONTENT_PART_TYPE);
                if (p == null || !p.HasValue) continue;
                PartType pt = (PartType)p.AsInteger();
                if (pt != primaryType && (fallbackType == PartType.Undefined || pt != fallbackType)) continue;

                int score = (pt == primaryType) ? 100 : 40;
                if (fs.IsActive) score += 50;

                string full = (fs.Family.Name + " " + fs.Name).ToUpperInvariant();
                if (full.Contains("RECT") || full.Contains("CHU NHAT") || full.Contains("CHỮ NHẬT")) score += 80;
                if (full.Contains("SHOE") || full.Contains("BEVEL") || full.Contains("45") || full.Contains("TAP")) score += 20;
                if (full.Contains("ROUND") || full.Contains("TRÒN") || full.Contains("OVAL")) score -= 120;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = fs;
                }
            }

            return best;
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
                            XYZ u = (p1 - p0).Normalize();
                            double mainLen = p0.DistanceTo(p1);
                            double t1 = (cB1.Origin - p0).DotProduct(u);
                            double t2 = (cB2.Origin - p0).DotProduct(u);
                            double tMin = Math.Min(t1, t2);
                            double tMax = Math.Max(t1, t2);
                            double margin = Math.Max(branch1.Width, branch2.Width) * 0.5 + (120.0 * MM_TO_FEET);

                            XYZ newP0 = p0;
                            XYZ newP1 = p1;
                            bool changed = false;
                            if (tMin < margin)
                            {
                                newP0 = p0 + u * (tMin - margin);
                                changed = true;
                            }
                            if (tMax > mainLen - margin)
                            {
                                newP1 = p0 + u * (tMax + margin);
                                changed = true;
                            }
                            if (changed)
                            {
                                lcMain.Curve = Line.CreateBound(new XYZ(newP0.X, newP0.Y, p0.Z), new XYZ(newP1.X, newP1.Y, p1.Z));
                                doc.Regenerate();
                            }
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

        private static void PrepareBimJunctionEndpoints(
            List<CadDuctSegment> ducts,
            List<CadConnectionEdge> edges,
            bool useConcentricTransition,
            Dictionary<string, XYZ> starts,
            Dictionary<string, XYZ> ends)
        {
            if (edges == null || edges.Count == 0) return;

            // 1. Kéo dài ống chính qua các vị trí chân rẽ Tap (để chân rẽ luôn đâm gọn vào thân ống chính liên tục)
            ExtendMainDuctsForTaps(edges, starts, ends);

            // 2. Căn đồng tâm & điều chỉnh khe hở côn thu / nối thẳng (ưu tiên giữ trục ống chính lớn hơn cố định)
            AlignAndSpaceTransitionAndCollinearEdges(edges, useConcentricTransition, starts, ends);

            // 3. Căn lại đầu nhánh Tap dừng sát thành ngoài ống chính theo trục mới nhất
            foreach (CadConnectionEdge edge in edges)
            {
                if (edge.Kind == CadConnectionKind.TakeoffTap)
                {
                    AdjustTapBranchEndpoint(edge.SegmentA, edge.SegmentB, starts, ends);
                }
            }

            // 4. Tính lại giao điểm Cút theo tọa độ mới nhất (sau khi căn đồng tâm côn thu nếu có) và cắt mép trước góc
            foreach (CadConnectionEdge edge in edges)
            {
                if (edge.Kind == CadConnectionKind.Elbow)
                {
                    XYZ s1 = starts[edge.SegmentA.Id], e1 = ends[edge.SegmentA.Id];
                    XYZ s2 = starts[edge.SegmentB.Id], e2 = ends[edge.SegmentB.Id];
                    XYZ d1 = new XYZ(e1.X - s1.X, e1.Y - s1.Y, 0.0);
                    XYZ d2 = new XYZ(e2.X - s2.X, e2.Y - s2.Y, 0.0);
                    XYZ corner = null;
                    if (d1.GetLength() > 1e-6 && d2.GetLength() > 1e-6)
                    {
                        corner = LineLineIntersectXY(s1, d1.Normalize(), s2, d2.Normalize(), s1.Z);
                    }
                    if (corner == null) corner = edge.JunctionPoint;
                    if (corner != null)
                    {
                        AdjustElbowEndpoints(edge.SegmentA, edge.SegmentB, corner, starts, ends);
                    }
                }
            }
        }

        private static void ExtendMainDuctsForTaps(
            List<CadConnectionEdge> edges,
            Dictionary<string, XYZ> starts,
            Dictionary<string, XYZ> ends)
        {
            Dictionary<string, List<CadConnectionEdge>> tapsByMain = new Dictionary<string, List<CadConnectionEdge>>();
            for (int i = 0; i < edges.Count; i++)
            {
                CadConnectionEdge edge = edges[i];
                if (edge.Kind != CadConnectionKind.TakeoffTap || edge.SegmentA == null || edge.SegmentB == null) continue;
                List<CadConnectionEdge> list;
                if (!tapsByMain.TryGetValue(edge.SegmentA.Id, out list))
                {
                    list = new List<CadConnectionEdge>();
                    tapsByMain[edge.SegmentA.Id] = list;
                }
                list.Add(edge);
            }

            double defaultMaxExtendFeet = 3000.0 * MM_TO_FEET;
            double maxPolygonBridgeExtendFeet = 7500.0 * MM_TO_FEET;

            foreach (KeyValuePair<string, List<CadConnectionEdge>> kv in tapsByMain)
            {
                List<CadConnectionEdge> tapList = kv.Value;
                if (tapList.Count == 0) continue;
                CadDuctSegment main = tapList[0].SegmentA;
                XYZ ms, me;
                if (!starts.TryGetValue(main.Id, out ms) || !ends.TryGetValue(main.Id, out me)) continue;

                XYZ vec = new XYZ(me.X - ms.X, me.Y - ms.Y, 0.0);
                double mainLen = vec.GetLength();
                if (mainLen < 1e-6) continue;
                XYZ u = vec.Normalize();

                double minReqT = 0.0;
                double maxReqT = mainLen;

                for (int i = 0; i < tapList.Count; i++)
                {
                    CadConnectionEdge tapEdge = tapList[i];
                    CadDuctSegment br = tapEdge.SegmentB;
                    XYZ bs, be;
                    if (!starts.TryGetValue(br.Id, out bs) || !ends.TryGetValue(br.Id, out be)) continue;

                    XYZ bVec = new XYZ(be.X - bs.X, be.Y - bs.Y, 0.0);
                    XYZ tapPt = null;
                    double cosAng = 0.0;
                    if (bVec.GetLength() > 1e-6)
                    {
                        XYZ bu = bVec.Normalize();
                        cosAng = Math.Abs(u.DotProduct(bu));
                        tapPt = LineLineIntersectXY(ms, u, bs, bu, ms.Z);
                    }
                    if (tapPt == null) tapPt = tapEdge.JunctionPoint;
                    if (tapPt == null) tapPt = br.CenterPoint;

                    double allowedExtend = defaultMaxExtendFeet;
                    if (tapEdge.BridgePolygon != null && tapEdge.BridgePolygon.PolygonVertices != null && tapEdge.BridgePolygon.PolygonVertices.Count >= 3)
                    {
                        DuctAabb2D fb = ComputeDuctSegmentAabb(tapEdge.BridgePolygon);
                        double fitSpan = (fb.MaxX - fb.MinX) + (fb.MaxY - fb.MinY);
                        allowedExtend = Math.Min(maxPolygonBridgeExtendFeet, Math.Max(4500.0 * MM_TO_FEET, fitSpan + (br.WidthMm + 600.0) * MM_TO_FEET));
                    }

                    double sinAng = Math.Sqrt(Math.Max(0.25, 1.0 - cosAng * cosAng));
                    double t = (tapPt.X - ms.X) * u.X + (tapPt.Y - ms.Y) * u.Y;
                    double margin = (br.WidthMm * 0.5 / sinAng + 100.0) * MM_TO_FEET;
                    double needLow = t - margin;
                    double needHigh = t + margin;

                    if (needLow < minReqT && (0.0 - needLow) <= allowedExtend)
                    {
                        minReqT = needLow;
                    }
                    if (needHigh > maxReqT && (needHigh - mainLen) <= allowedExtend)
                    {
                        maxReqT = needHigh;
                    }
                }

                if (minReqT < 0.0)
                {
                    XYZ newStart = ms + u * minReqT;
                    starts[main.Id] = new XYZ(newStart.X, newStart.Y, ms.Z);
                }
                if (maxReqT > mainLen)
                {
                    XYZ newEnd = ms + u * maxReqT;
                    ends[main.Id] = new XYZ(newEnd.X, newEnd.Y, me.Z);
                }
            }
        }

        private static void AlignAndSpaceTransitionAndCollinearEdges(
            List<CadConnectionEdge> edges,
            bool useConcentricTransition,
            Dictionary<string, XYZ> starts,
            Dictionary<string, XYZ> ends)
        {
            List<CadConnectionEdge> axialEdges = new List<CadConnectionEdge>();
            for (int i = 0; i < edges.Count; i++)
            {
                CadConnectionEdge e = edges[i];
                if (e.Kind == CadConnectionKind.Transition || e.Kind == CadConnectionKind.CollinearDirect)
                {
                    axialEdges.Add(e);
                }
            }

            // Xử lý theo bề rộng ống lớn giảm dần để ống chính lớn làm chuẩn trục cố định
            axialEdges.Sort(delegate(CadConnectionEdge a, CadConnectionEdge b)
            {
                double wa = Math.Max(a.SegmentA.WidthMm, a.SegmentB.WidthMm);
                double wb = Math.Max(b.SegmentA.WidthMm, b.SegmentB.WidthMm);
                return wb.CompareTo(wa);
            });

            for (int i = 0; i < axialEdges.Count; i++)
            {
                CadConnectionEdge edge = axialEdges[i];
                CadDuctSegment d1 = edge.SegmentA;
                CadDuctSegment d2 = edge.SegmentB;

                CadDuctSegment anchor = (d1.WidthMm > d2.WidthMm || (Math.Abs(d1.WidthMm - d2.WidthMm) < 1.0 && d1.Length >= d2.Length)) ? d1 : d2;
                CadDuctSegment mate = (anchor == d1) ? d2 : d1;

                try
                {
                    XYZ aS = starts[anchor.Id], aE = ends[anchor.Id];
                    XYZ mS = starts[mate.Id], mE = ends[mate.Id];

                    XYZ aVec = new XYZ(aE.X - aS.X, aE.Y - aS.Y, 0.0);
                    if (aVec.GetLength() < 1e-6) continue;
                    XYZ axis = aVec.Normalize();

                    double latDiff = LineToLineDistance(aS, axis, mS, axis);
                    double maxAlignTol = (edge.Kind == CadConnectionKind.Transition)
                        ? (useConcentricTransition
                            ? Math.Max(220.0, Math.Abs(anchor.WidthMm - mate.WidthMm) * 0.65 + 80.0) * MM_TO_FEET
                            : 120.0 * MM_TO_FEET)
                        : (100.0 * MM_TO_FEET);

                    if (latDiff <= maxAlignTol)
                    {
                        XYZ mSProj = aS + axis * (mS - aS).DotProduct(axis);
                        XYZ mEProj = aS + axis * (mE - aS).DotProduct(axis);
                        mS = new XYZ(mSProj.X, mSProj.Y, mS.Z);
                        mE = new XYZ(mEProj.X, mEProj.Y, mE.Z);
                        starts[mate.Id] = mS;
                        ends[mate.Id] = mE;
                    }

                    // Chọn 2 đầu đối diện gần nhau nhất
                    XYZ[] aPts = new XYZ[] { aS, aE };
                    XYZ[] mPts = new XYZ[] { mS, mE };
                    double bestDist = double.MaxValue;
                    int aiBest = 0, miBest = 0;
                    for (int ai = 0; ai < 2; ai++)
                    {
                        for (int mi = 0; mi < 2; mi++)
                        {
                            double d = DistXY(aPts[ai], mPts[mi]);
                            if (d < bestDist) { bestDist = d; aiBest = ai; miBest = mi; }
                        }
                    }

                    XYZ aFace = aPts[aiBest];
                    XYZ aFar = aPts[1 - aiBest];
                    XYZ mFace = mPts[miBest];
                    XYZ mFar = mPts[1 - miBest];

                    XYZ outFromAnchor = new XYZ(aFace.X - aFar.X, aFace.Y - aFar.Y, 0.0);
                    if (outFromAnchor.GetLength() < 1e-6) continue;
                    outFromAnchor = outFromAnchor.Normalize();

                    double signedGap = (mFace.X - aFace.X) * outFromAnchor.X + (mFace.Y - aFace.Y) * outFromAnchor.Y;

                    bool isDimTransition = (edge.Kind == CadConnectionKind.Transition)
                        || (Math.Abs(anchor.WidthMm - mate.WidthMm) >= 15.0)
                        || (anchor.HeightMm > 0 && mate.HeightMm > 0 && Math.Abs(anchor.HeightMm - mate.HeightMm) >= 15.0);

                    if (isDimTransition)
                    {
                        double dimDelta = Math.Max(Math.Abs(anchor.WidthMm - mate.WidthMm), Math.Abs(anchor.HeightMm - mate.HeightMm));
                        double targetGap = Math.Max(150.0, Math.Min(350.0, dimDelta * 0.8)) * MM_TO_FEET;
                        if (signedGap < 80.0 * MM_TO_FEET || signedGap > 650.0 * MM_TO_FEET)
                        {
                            XYZ desiredMFace = aFace + outFromAnchor * targetGap;
                            double newMateLen = DistXY(desiredMFace, mFar);
                            double minMateLen = 120.0 * MM_TO_FEET;

                            if (newMateLen >= minMateLen)
                            {
                                XYZ fixedMFace = new XYZ(desiredMFace.X, desiredMFace.Y, mFace.Z);
                                if (miBest == 0) starts[mate.Id] = fixedMFace;
                                else ends[mate.Id] = fixedMFace;
                            }
                            else
                            {
                                XYZ toMateDir = new XYZ(mFar.X - aFace.X, mFar.Y - aFace.Y, 0.0);
                                if (toMateDir.GetLength() > minMateLen + 80.0 * MM_TO_FEET)
                                {
                                    XYZ clampedMFace = mFar - outFromAnchor * minMateLen;
                                    XYZ fixedMFace = new XYZ(clampedMFace.X, clampedMFace.Y, mFace.Z);
                                    if (miBest == 0) starts[mate.Id] = fixedMFace;
                                    else ends[mate.Id] = fixedMFace;
                                }
                            }
                        }
                    }
                    else if (edge.Kind == CadConnectionKind.CollinearDirect)
                    {
                        if (bestDist > 1.0 * MM_TO_FEET && bestDist <= 2500.0 * MM_TO_FEET)
                        {
                            XYZ fixedMFace = new XYZ(aFace.X, aFace.Y, mFace.Z);
                            if (DistXY(fixedMFace, mFar) >= 120.0 * MM_TO_FEET)
                            {
                                if (miBest == 0) starts[mate.Id] = fixedMFace;
                                else ends[mate.Id] = fixedMFace;
                            }
                        }
                    }
                }
                catch { }
            }
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

                XYZ mVec = new XYZ(me.X - ms.X, me.Y - ms.Y, 0.0);
                XYZ bVec = new XYZ(be.X - bs.X, be.Y - bs.Y, 0.0);
                if (mVec.GetLength() < 1e-6 || bVec.GetLength() < 1e-6) return;
                XYZ mainDir = mVec.Normalize();
                XYZ bDir = bVec.Normalize();

                XYZ tapAnchor = null;
                if (Math.Abs(mainDir.DotProduct(bDir)) < 0.92)
                {
                    tapAnchor = LineLineIntersectXY(ms, mainDir, bs, bDir, bs.Z);
                }

                XYZ projStart = (tapAnchor != null) ? tapAnchor : (ms + mainDir * (bs - ms).DotProduct(mainDir));
                XYZ projEnd = (tapAnchor != null) ? tapAnchor : (ms + mainDir * (be - ms).DotProduct(mainDir));

                double dStart = DistXY(bs, projStart);
                double dEnd = DistXY(be, projEnd);

                double cosAng = Math.Abs(mainDir.DotProduct(bDir));
                double sinAng = Math.Sqrt(Math.Max(0.25, 1.0 - cosAng * cosAng));
                double halfWFeet = ((mainDuct.WidthMm * 0.5) / sinAng + 25.0) * MM_TO_FEET;

                if (dStart <= dEnd)
                {
                    XYZ outDir = (tapAnchor != null)
                        ? new XYZ(be.X - tapAnchor.X, be.Y - tapAnchor.Y, 0.0)
                        : new XYZ(bs.X - projStart.X, bs.Y - projStart.Y, 0.0);
                    if (outDir.GetLength() > 1e-5)
                    {
                        outDir = outDir.Normalize();
                        XYZ snappedStart = projStart + outDir * halfWFeet;
                        if (DistXY(snappedStart, be) > 60.0 * MM_TO_FEET)
                            starts[branchDuct.Id] = new XYZ(snappedStart.X, snappedStart.Y, bs.Z);
                    }
                }
                else
                {
                    XYZ outDir = (tapAnchor != null)
                        ? new XYZ(bs.X - tapAnchor.X, bs.Y - tapAnchor.Y, 0.0)
                        : new XYZ(be.X - projEnd.X, be.Y - projEnd.Y, 0.0);
                    if (outDir.GetLength() > 1e-5)
                    {
                        outDir = outDir.Normalize();
                        XYZ snappedEnd = projEnd + outDir * halfWFeet;
                        if (DistXY(snappedEnd, bs) > 60.0 * MM_TO_FEET)
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
            double pullTol = minClear + 300.0 * MM_TO_FEET;

            if (ds <= de && (ds < minClear || ds <= pullTol) && de > minClear + 50.0 * MM_TO_FEET)
            {
                XYZ dirAway = (e - s).Normalize();
                starts[d.Id] = cornerZ + dirAway * minClear;
            }
            else if (de < ds && (de < minClear || de <= pullTol) && ds > minClear + 50.0 * MM_TO_FEET)
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

        private static double SegmentSegmentDistanceXY(XYZ a1, XYZ a2, XYZ b1, XYZ b2)
        {
            if (a1 == null || a2 == null || b1 == null || b2 == null) return double.MaxValue;
            if (SegmentsIntersectXY(a1, a2, b1, b2)) return 0.0;
            double d1 = CadDuctSegment.DistPointToSegmentXY(a1, b1, b2);
            double d2 = CadDuctSegment.DistPointToSegmentXY(a2, b1, b2);
            double d3 = CadDuctSegment.DistPointToSegmentXY(b1, a1, a2);
            double d4 = CadDuctSegment.DistPointToSegmentXY(b2, a1, a2);
            return Math.Min(Math.Min(d1, d2), Math.Min(d3, d4));
        }

        private static bool SegmentsIntersectXY(XYZ p1, XYZ q1, XYZ p2, XYZ q2)
        {
            int o1 = OrientationXY(p1, q1, p2);
            int o2 = OrientationXY(p1, q1, q2);
            int o3 = OrientationXY(p2, q2, p1);
            int o4 = OrientationXY(p2, q2, q1);

            if (o1 != o2 && o3 != o4) return true;
            if (o1 == 0 && OnSegmentXY(p1, p2, q1)) return true;
            if (o2 == 0 && OnSegmentXY(p1, q2, q1)) return true;
            if (o3 == 0 && OnSegmentXY(p2, p1, q2)) return true;
            if (o4 == 0 && OnSegmentXY(p2, q1, q2)) return true;
            return false;
        }

        private static int OrientationXY(XYZ p, XYZ q, XYZ r)
        {
            double val = (q.Y - p.Y) * (r.X - q.X) - (q.X - p.X) * (r.Y - q.Y);
            if (Math.Abs(val) < 1e-9) return 0;
            return (val > 0) ? 1 : 2;
        }

        private static bool OnSegmentXY(XYZ p, XYZ q, XYZ r)
        {
            return q.X <= Math.Max(p.X, r.X) + 1e-9 && q.X >= Math.Min(p.X, r.X) - 1e-9
                && q.Y <= Math.Max(p.Y, r.Y) + 1e-9 && q.Y >= Math.Min(p.Y, r.Y) - 1e-9;
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
