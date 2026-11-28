using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Color = System.Drawing.Color;
using Font = System.Drawing.Font;
using Point = System.Drawing.Point;

namespace HDTool
{
    /// <summary>
    /// Dịch vụ tổng hợp Family trong dự án Revit, kết xuất ảnh 3D Isometric Preview
    /// và hỗ trợ kéo thả / đặt Family vào bản vẽ để tiếp tục vẽ theo chỉ đạo Sếp Hồ
    /// </summary>
    public static class FamilyPaletteService
    {
        private static readonly System.Drawing.Size DefaultPreviewSize = new System.Drawing.Size(128, 128);

        /// <summary>
        /// Thu thập toàn bộ danh sách Family và các Type trong dự án Revit siêu tốc (metadata only, không load trước ảnh để chống đơ giật lag 100%)
        /// </summary>
        public static List<FamilyGroupItem> GetProjectFamilies(Document doc, FamilyPaletteFilter filter)
        {
            List<FamilyGroupItem> result = new List<FamilyGroupItem>();
            if (doc == null) return result;

            if (filter == null)
            {
                filter = new FamilyPaletteFilter();
            }

            try
            {
                // Chỉ tổng hợp những gì đã load vào dự án, KHÔNG đếm số lượng trên bản vẽ (lệnh Sếp Hồ)
                Dictionary<ElementId, int> instanceCounts = new Dictionary<ElementId, int>();

                // 2. Thu thập toàn bộ FamilySymbol (Type) trong dự án
                FilteredElementCollector symbolCol = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .WhereElementIsElementType();

                Dictionary<string, FamilyGroupItem> groupsByName = new Dictionary<string, FamilyGroupItem>(StringComparer.OrdinalIgnoreCase);

                foreach (Element e in symbolCol)
                {
                    FamilySymbol symbol = e as FamilySymbol;
                    if (symbol == null || symbol.Family == null) continue;

                    Family fam = symbol.Family;
                    string famName = fam.Name;
                    if (string.IsNullOrEmpty(famName)) continue;

                    string typeName = symbol.Name;
                    Category cat = symbol.Category;
                    string catName = (cat != null) ? cat.Name : "Khác";
                    BuiltInCategory bic = (cat != null) ? (BuiltInCategory)(int)cat.Id.Value : BuiltInCategory.INVALID;
                    FamilyCategoryGroup catGroup = ResolveCategoryGroup(cat);

                    // Lọc theo Category Group nếu người dùng chỉ định
                    if (filter.SelectedGroup != FamilyCategoryGroup.All && catGroup != filter.SelectedGroup)
                    {
                        continue;
                    }

                    int count = 0;
                    instanceCounts.TryGetValue(symbol.Id, out count);

                    // CHÚ Ý TỐI ƯU CỦA SẾP HỒ:
                    // Tuyệt đối KHÔNG gọi symbol.GetPreviewImage() ở đây để triệt tiêu 100% hiện tượng đóng băng/đơ máy!
                    // Ảnh preview 3D sẽ được nạp theo cơ chế On-Demand (Lazy Loading) khi kỹ sư click vào dòng.
                    Bitmap previewBmp = null;

                    FamilyTypeItem typeItem = new FamilyTypeItem(
                        symbol.Id,
                        typeName,
                        famName,
                        catName,
                        bic,
                        catGroup,
                        symbol.IsActive,
                        count,
                        previewBmp
                    );

                    FamilyGroupItem group;
                    if (!groupsByName.TryGetValue(famName, out group))
                    {
                        group = new FamilyGroupItem(
                            famName,
                            catName,
                            bic,
                            catGroup,
                            fam.Id
                        );
                        group.RepresentativePreview = null;
                        groupsByName[famName] = group;
                    }

                    group.Types.Add(typeItem);
                    group.TotalInstances += count;
                }

                // 3. Lọc từ khóa tìm kiếm & điều kiện số lượng
                string searchKeyword = filter.SearchKeyword != null ? filter.SearchKeyword.Trim() : string.Empty;
                string cleanSearch = RemoveDiacritics(searchKeyword).ToUpperInvariant();

                foreach (KeyValuePair<string, FamilyGroupItem> kvp in groupsByName)
                {
                    FamilyGroupItem g = kvp.Value;

                    if (filter.OnlyWithInstances && g.TotalInstances <= 0)
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(cleanSearch))
                    {
                        string cleanFam = RemoveDiacritics(g.FamilyName).ToUpperInvariant();
                        string cleanCat = RemoveDiacritics(g.CategoryName).ToUpperInvariant();

                        bool match = cleanFam.Contains(cleanSearch) || cleanCat.Contains(cleanSearch);
                        if (!match && g.Types != null)
                        {
                            for (int i = 0; i < g.Types.Count; i++)
                            {
                                string cleanType = RemoveDiacritics(g.Types[i].TypeName).ToUpperInvariant();
                                if (cleanType.Contains(cleanSearch))
                                {
                                    match = true;
                                    break;
                                }
                            }
                        }

                        if (!match) continue;
                    }

                    // Sắp xếp các Type bên trong Family theo tên
                    if (g.Types != null && g.Types.Count > 1)
                    {
                        g.Types.Sort(delegate(FamilyTypeItem a, FamilyTypeItem b)
                        {
                            return StringComparer.OrdinalIgnoreCase.Compare(a.TypeName, b.TypeName);
                        });
                    }

                    result.Add(g);
                }

                // 4. Sắp xếp danh sách kết quả theo Category Group rồi đến Tên Family
                result.Sort(delegate(FamilyGroupItem a, FamilyGroupItem b)
                {
                    int c = ((int)a.CategoryGroup).CompareTo((int)b.CategoryGroup);
                    if (c != 0) return c;
                    return StringComparer.OrdinalIgnoreCase.Compare(a.FamilyName, b.FamilyName);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Lỗi tổng hợp Family: " + ex.Message);
            }

            return result;
        }

        /// <summary>
        /// Nạp ảnh 3D Isometric Preview chất lượng cao theo yêu cầu (On-Demand / Lazy Loading) cho đúng Type đang chọn
        /// </summary>
        public static Bitmap GetSymbolPreviewImage(Document doc, ElementId symbolId, System.Drawing.Size size, string familyName, string categoryName)
        {
            if (doc == null || symbolId == null || symbolId == ElementId.InvalidElementId)
            {
                return CreatePlaceholderPreview(familyName, categoryName, size.Width, size.Height);
            }

            try
            {
                FamilySymbol symbol = doc.GetElement(symbolId) as FamilySymbol;
                if (symbol != null)
                {
                    Bitmap bmp = symbol.GetPreviewImage(size);
                    if (bmp != null) return bmp;
                }
            }
            catch { }

            return CreatePlaceholderPreview(familyName, categoryName, size.Width, size.Height);
        }

        /// <summary>
        /// Lấy hình 3D của Type cho khung xem trước xoay được: tam giác (9 số / tam giác) + đoạn thẳng (6 số / đoạn), đơn vị feet.
        /// Thứ tự: hình của FamilySymbol -> 1 đối tượng đã đặt -> giao dịch TẠM (kích hoạt Type, đặt thử 1 đối tượng
        /// theo kiểu đặt của Family: điểm / Level / đường / mặt tường / tường chủ / view) rồi RollBack — dự án không đổi.
        /// BẮT BUỘC gọi trong API context (ExternalEvent). Không lấy được gì thì cả 2 mảng = null.
        /// </summary>
        public static void GetSymbolGeometry(Document doc, ElementId symbolId, int maxTriangles, out float[] triangles, out float[] lines)
        {
            triangles = null;
            lines = null;
            if (doc == null || symbolId == null || symbolId == ElementId.InvalidElementId) return;
            List<float> tri = new List<float>(9 * 2048);
            List<float> seg = new List<float>(6 * 512);
            try
            {
                FamilySymbol symbol = doc.GetElement(symbolId) as FamilySymbol;
                if (symbol == null) return;

                CollectGeometry(symbol.get_Geometry(ModelOptions()), tri, seg, maxTriangles);

                if (tri.Count == 0)
                {
                    Element inst = new FilteredElementCollector(doc)
                        .WherePasses(new FamilyInstanceFilter(doc, symbolId))
                        .FirstElement();
                    if (inst != null)
                    {
                        tri.Clear(); seg.Clear();
                        CollectGeometry(inst.get_Geometry(ModelOptions()), tri, seg, maxTriangles);
                        if (tri.Count == 0 && inst.OwnerViewId != ElementId.InvalidElementId)
                        {
                            View ov = doc.GetElement(inst.OwnerViewId) as View;
                            if (ov != null) CollectGeometry(inst.get_Geometry(ViewOptions(ov)), tri, seg, maxTriangles);
                        }
                    }
                }

                if (tri.Count == 0 && !doc.IsReadOnly && !doc.IsModifiable)
                {
                    List<float> t2 = new List<float>(9 * 2048);
                    List<float> s2 = new List<float>(6 * 512);
                    GetGeometryByTempPlacement(doc, symbol, maxTriangles, t2, s2);
                    if (t2.Count > 0 || s2.Count > seg.Count) { tri = t2; seg = s2; }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Lỗi lấy hình 3D: " + ex.Message);
            }
            if (tri.Count > 0) triangles = tri.ToArray();
            if (seg.Count > 0) lines = seg.ToArray();
        }

        private static Options ModelOptions()
        {
            Options opt = new Options();
            opt.DetailLevel = ViewDetailLevel.Fine;
            opt.ComputeReferences = false;
            opt.IncludeNonVisibleObjects = false;
            return opt;
        }

        private static Options ViewOptions(View v)
        {
            Options opt = new Options();
            opt.View = v;
            opt.ComputeReferences = false;
            return opt;
        }

        /// <summary>Giao dịch tạm: kích hoạt Type / đặt thử 1 đối tượng, đọc hình rồi RollBack (không ghi vào dự án, không vào Undo).</summary>
        private static void GetGeometryByTempPlacement(Document doc, FamilySymbol symbol, int maxTriangles, List<float> tri, List<float> seg)
        {
            using (Transaction t = new Transaction(doc, "HDTOOL - Xem trước 3D (tạm)"))
            {
                try
                {
                    FailureHandlingOptions fho = t.GetFailureHandlingOptions();
                    fho.SetFailuresPreprocessor(new HDToolSilentFailures());
                    fho.SetClearAfterRollback(true);
                    fho.SetForcedModalHandling(false);
                    t.SetFailureHandlingOptions(fho);
                    if (t.Start() != TransactionStatus.Started) return;

                    if (!symbol.IsActive) symbol.Activate();
                    doc.Regenerate();
                    CollectGeometry(symbol.get_Geometry(ModelOptions()), tri, seg, maxTriangles);

                    if (tri.Count == 0)
                    {
                        View view;
                        FamilyInstance fi = PlaceTempInstance(doc, symbol, out view);
                        if (fi != null)
                        {
                            doc.Regenerate();
                            tri.Clear(); seg.Clear();
                            CollectGeometry(fi.get_Geometry(ModelOptions()), tri, seg, maxTriangles);
                            if (tri.Count == 0 && seg.Count == 0 && view != null)
                            {
                                CollectGeometry(fi.get_Geometry(ViewOptions(view)), tri, seg, maxTriangles);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Đặt tạm để xem 3D lỗi: " + ex.Message);
                }
                finally
                {
                    try { if (t.GetStatus() == TransactionStatus.Started) t.RollBack(); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine("RollBack xem 3D lỗi: " + ex.Message); }
                }
            }
        }

        /// <summary>Thử lần lượt các cách đặt theo kiểu đặt của Family; trả về đối tượng tạm đầu tiên đặt được.</summary>
        private static FamilyInstance PlaceTempInstance(Document doc, FamilySymbol symbol, out View view)
        {
            view = null;
            Autodesk.Revit.DB.Structure.StructuralType ns = Autodesk.Revit.DB.Structure.StructuralType.NonStructural;
            Level level = null;
            foreach (Element e in new FilteredElementCollector(doc).OfClass(typeof(Level)))
            {
                Level l = (Level)e;
                if (level == null || l.Elevation < level.Elevation) level = l;
            }
            double z = level != null ? level.Elevation : 0.0;
            XYZ p0 = new XYZ(0, 0, z);
            Line line = Line.CreateBound(p0, new XYZ(6, 0, z));

            FamilyPlacementType pt = FamilyPlacementType.Invalid;
            try { pt = symbol.Family.FamilyPlacementType; } catch { pt = FamilyPlacementType.Invalid; }

            FamilyInstance fi = null;

            // 2D (chú thích / chi tiết): đặt trong 1 view mặt bằng
            if (pt == FamilyPlacementType.ViewBased || pt == FamilyPlacementType.CurveBasedDetail)
            {
                View pv = FindPlanView(doc);
                view = pv;
                if (pv != null)
                {
                    if (pt == FamilyPlacementType.CurveBasedDetail) fi = TryPlace(delegate { return doc.Create.NewFamilyInstance(line, symbol, pv); });
                    if (fi == null) fi = TryPlace(delegate { return doc.Create.NewFamilyInstance(p0, symbol, pv); });
                }
                return fi;
            }

            if (pt == FamilyPlacementType.Adaptive)
            {
                fi = TryPlace(delegate
                {
                    FamilyInstance a = AdaptiveComponentInstanceUtils.CreateAdaptiveComponentInstance(doc, symbol);
                    IList<ElementId> ids = AdaptiveComponentInstanceUtils.GetInstancePlacementPointElementRefIds(a);
                    for (int i = 0; i < ids.Count; i++)
                    {
                        ReferencePoint rp = doc.GetElement(ids[i]) as ReferencePoint;
                        if (rp != null) rp.Position = new XYZ(4.0 * i, (i % 2) * 2.0, z);
                    }
                    return a;
                });
                if (fi != null) return fi;
            }

            if (pt == FamilyPlacementType.CurveBased || pt == FamilyPlacementType.CurveDrivenStructural)
            {
                if (level != null) fi = TryPlace(delegate { return doc.Create.NewFamilyInstance(line, symbol, level, ns); });
                if (fi != null) return fi;
            }

            if (level != null) fi = TryPlace(delegate { return doc.Create.NewFamilyInstance(p0, symbol, level, ns); });
            if (fi == null) fi = TryPlace(delegate { return doc.Create.NewFamilyInstance(p0, symbol, ns); });
            if (fi != null) return fi;

            // Family gắn tường / gắn mặt / mặt phẳng làm việc: dựng tường tạm làm chủ
            if (level != null)
            {
                Wall wall = null;
                try { wall = Wall.Create(doc, Line.CreateBound(new XYZ(-10, 0, z), new XYZ(10, 0, z)), level.Id, false); doc.Regenerate(); }
                catch { wall = null; }
                if (wall != null)
                {
                    XYZ mid = new XYZ(0, 0, z + 3.0);
                    fi = TryPlace(delegate { return doc.Create.NewFamilyInstance(mid, symbol, wall, level, ns); });
                    if (fi == null)
                    {
                        fi = TryPlace(delegate
                        {
                            IList<Reference> faces = HostObjectUtils.GetSideFaces(wall, ShellLayerType.Exterior);
                            if (faces == null || faces.Count == 0) return null;
                            Face face = wall.GetGeometryObjectFromReference(faces[0]) as Face;
                            if (face == null) return null;
                            IntersectionResult ir = face.Project(mid);
                            XYZ loc = ir != null ? ir.XYZPoint : mid;
                            return doc.Create.NewFamilyInstance(faces[0], loc, XYZ.BasisX, symbol);
                        });
                    }
                }
            }
            return fi;
        }

        private static FamilyInstance TryPlace(Func<FamilyInstance> place)
        {
            try { return place(); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Đặt tạm không được: " + ex.Message);
                return null;
            }
        }

        private static View FindPlanView(Document doc)
        {
            View av = doc.ActiveView;
            if (av != null && !av.IsTemplate && (av is ViewPlan || av is ViewSection || av is ViewDrafting)) return av;
            foreach (Element e in new FilteredElementCollector(doc).OfClass(typeof(ViewPlan)))
            {
                View v = (View)e;
                if (!v.IsTemplate) return v;
            }
            return null;
        }

        private static void CollectGeometry(GeometryElement geo, List<float> tri, List<float> seg, int maxTriangles)
        {
            if (geo == null) return;
            foreach (GeometryObject obj in geo)
            {
                if (tri.Count / 9 >= maxTriangles) return;

                Solid solid = obj as Solid;
                if (solid != null)
                {
                    if (solid.Faces.Size == 0) continue;
                    foreach (Face face in solid.Faces)
                    {
                        Mesh fm = null;
                        try { fm = face.Triangulate(); } catch { fm = null; }
                        AddMesh(fm, tri, maxTriangles);
                        if (tri.Count / 9 >= maxTriangles) return;
                    }
                    continue;
                }

                Mesh mesh = obj as Mesh;
                if (mesh != null)
                {
                    AddMesh(mesh, tri, maxTriangles);
                    continue;
                }

                Curve curve = obj as Curve;
                if (curve != null)
                {
                    try { AddPolyline(curve.Tessellate(), seg); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Tessellate lỗi: " + ex.Message); }
                    continue;
                }

                PolyLine pl = obj as PolyLine;
                if (pl != null)
                {
                    try { AddPolyline(pl.GetCoordinates(), seg); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("PolyLine lỗi: " + ex.Message); }
                    continue;
                }

                GeometryInstance gi = obj as GeometryInstance;
                if (gi != null)
                {
                    CollectGeometry(gi.GetInstanceGeometry(), tri, seg, maxTriangles);
                }
            }
        }

        private static void AddPolyline(IList<XYZ> pts, List<float> seg)
        {
            if (pts == null) return;
            for (int i = 1; i < pts.Count; i++)
            {
                if (seg.Count / 6 >= 40000) return;
                seg.Add((float)pts[i - 1].X); seg.Add((float)pts[i - 1].Y); seg.Add((float)pts[i - 1].Z);
                seg.Add((float)pts[i].X); seg.Add((float)pts[i].Y); seg.Add((float)pts[i].Z);
            }
        }

        private static void AddMesh(Mesh mesh, List<float> buf, int maxTriangles)
        {
            if (mesh == null) return;
            int n = mesh.NumTriangles;
            for (int i = 0; i < n; i++)
            {
                if (buf.Count / 9 >= maxTriangles) return;
                MeshTriangle tri = mesh.get_Triangle(i);
                for (int k = 0; k < 3; k++)
                {
                    XYZ p = tri.get_Vertex(k);
                    buf.Add((float)p.X);
                    buf.Add((float)p.Y);
                    buf.Add((float)p.Z);
                }
            }
        }

        /// <summary>
        /// Kích hoạt lệnh đặt Family vào Revit View (tương đương kéo thả từ Project Browser)
        /// Người dùng nhìn thấy preview của Family theo con trỏ chuột và click để đặt trực tiếp
        /// </summary>
        public static bool PlaceFamilySymbol(UIApplication uiapp, ElementId symbolId, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (uiapp == null || uiapp.ActiveUIDocument == null)
            {
                errorMessage = "Không có dự án Revit nào đang hoạt động.";
                return false;
            }

            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            if (symbolId == null || symbolId == ElementId.InvalidElementId)
            {
                errorMessage = "FamilySymbol Id không hợp lệ.";
                return false;
            }

            FamilySymbol symbol = doc.GetElement(symbolId) as FamilySymbol;
            if (symbol == null)
            {
                errorMessage = "Không tìm thấy FamilySymbol trong dự án.";
                return false;
            }

            try
            {
                // Bắt buộc kích hoạt FamilySymbol nếu chưa active trước khi đặt vào view
                if (!symbol.IsActive)
                {
                    using (Transaction t = new Transaction(doc, "Kích Hoạt Family Symbol"))
                    {
                        t.Start();
                        symbol.Activate();
                        doc.Regenerate();
                        t.Commit();
                    }
                }

                // Gọi lệnh tương tác đặt Family của Revit UI (chế độ Placement trực quan)
                uidoc.PromptForFamilyInstancePlacement(symbol);
                return true;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // Người dùng ấn phím ESC hoặc Modify để thoát đặt Family -> xem như hoàn thành bình thường
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Lỗi khi đặt Family vào bản vẽ: " + ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Phân loại Category Revit thành các nhóm chuyên ngành MEP / Kiến trúc / Kết cấu
        /// </summary>
        public static FamilyCategoryGroup ResolveCategoryGroup(Category cat)
        {
            if (cat == null) return FamilyCategoryGroup.Other;

            BuiltInCategory bic = BuiltInCategory.INVALID;
            try
            {
                bic = (BuiltInCategory)(int)cat.Id.Value;
            }
            catch { }

            // Ống gió & Miệng gió
            if (bic == BuiltInCategory.OST_DuctCurves ||
                bic == BuiltInCategory.OST_DuctTerminal ||
                bic == BuiltInCategory.OST_DuctAccessory ||
                bic == BuiltInCategory.OST_FlexDuctCurves)
            {
                return FamilyCategoryGroup.Ducts;
            }

            // Phụ kiện ống gió
            if (bic == BuiltInCategory.OST_DuctFitting)
            {
                return FamilyCategoryGroup.DuctFittings;
            }

            // Ống nước & Van
            if (bic == BuiltInCategory.OST_PipeCurves ||
                bic == BuiltInCategory.OST_PipeAccessory ||
                bic == BuiltInCategory.OST_FlexPipeCurves)
            {
                return FamilyCategoryGroup.Pipes;
            }

            // Phụ kiện ống nước
            if (bic == BuiltInCategory.OST_PipeFitting)
            {
                return FamilyCategoryGroup.PipeFittings;
            }

            // Thiết bị cơ điện
            if (bic == BuiltInCategory.OST_MechanicalEquipment)
            {
                return FamilyCategoryGroup.MechanicalEquipment;
            }

            // Điện & Chiếu sáng
            if (bic == BuiltInCategory.OST_ElectricalEquipment ||
                bic == BuiltInCategory.OST_ElectricalFixtures ||
                bic == BuiltInCategory.OST_LightingFixtures ||
                bic == BuiltInCategory.OST_LightingDevices ||
                bic == BuiltInCategory.OST_CableTray ||
                bic == BuiltInCategory.OST_Conduit)
            {
                return FamilyCategoryGroup.Electrical;
            }

            // Thiết bị vệ sinh & PCCC
            if (bic == BuiltInCategory.OST_PlumbingFixtures ||
                bic == BuiltInCategory.OST_Sprinklers ||
                bic == BuiltInCategory.OST_FireAlarmDevices)
            {
                return FamilyCategoryGroup.Plumbing;
            }

            // Kiến trúc
            if (bic == BuiltInCategory.OST_Doors ||
                bic == BuiltInCategory.OST_Windows ||
                bic == BuiltInCategory.OST_Furniture ||
                bic == BuiltInCategory.OST_FurnitureSystems ||
                bic == BuiltInCategory.OST_Walls ||
                bic == BuiltInCategory.OST_Ceilings ||
                bic == BuiltInCategory.OST_Roofs ||
                bic == BuiltInCategory.OST_Casework)
            {
                return FamilyCategoryGroup.Architecture;
            }

            // Kết cấu
            if (bic == BuiltInCategory.OST_StructuralColumns ||
                bic == BuiltInCategory.OST_StructuralFraming ||
                bic == BuiltInCategory.OST_StructuralFoundation ||
                bic == BuiltInCategory.OST_Floors)
            {
                return FamilyCategoryGroup.Structure;
            }

            // Phân loại theo tên nếu BuiltInCategory chưa bao quát
            string nameUpper = cat.Name != null ? cat.Name.ToUpperInvariant() : string.Empty;
            if (nameUpper.Contains("DUCT") || nameUpper.Contains("GIÓ") || nameUpper.Contains("AIR")) return FamilyCategoryGroup.Ducts;
            if (nameUpper.Contains("PIPE") || nameUpper.Contains("NƯỚC")) return FamilyCategoryGroup.Pipes;
            if (nameUpper.Contains("MECH") || nameUpper.Contains("EQUIP") || nameUpper.Contains("THIẾT BỊ")) return FamilyCategoryGroup.MechanicalEquipment;
            if (nameUpper.Contains("ELEC") || nameUpper.Contains("LIGHT") || nameUpper.Contains("ĐIỆN")) return FamilyCategoryGroup.Electrical;

            return FamilyCategoryGroup.Other;
        }

        /// <summary>
        /// Tạo ảnh 3D Isometric Preview chất lượng cao thay thế khi Family không trích được ảnh từ Revit
        /// </summary>
        public static Bitmap CreatePlaceholderPreview(string familyName, string categoryName, int width, int height)
        {
            if (width <= 0) width = 128;
            if (height <= 0) height = 128;

            Bitmap bmp = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.FromArgb(15, 23, 42)); // Slate dark #0f172a

                float scale = width / 128.0f;

                // Vẽ khối lập phương 3D Isometric nổi bật
                PointF center = new PointF(width / 2.0f, height / 2.0f - 8f * scale);
                float size = 32f * scale;

                PointF topCenter = new PointF(center.X, center.Y - size * 0.5f);
                PointF topRight = new PointF(center.X + size * 0.866f, center.Y - size * 0.25f);
                PointF topRightBottom = new PointF(center.X + size * 0.866f, center.Y + size * 0.75f);
                PointF bottomCenter = new PointF(center.X, center.Y + size * 1.0f);
                PointF topLeftBottom = new PointF(center.X - size * 0.866f, center.Y + size * 0.75f);
                PointF topLeft = new PointF(center.X - size * 0.866f, center.Y - size * 0.25f);

                // Mặt trên (Top Face) - Xanh ngọc Cyan
                PointF[] topFace = new PointF[] { center, topRight, topCenter, topLeft };
                using (SolidBrush topBrush = new SolidBrush(Color.FromArgb(56, 189, 248)))
                {
                    g.FillPolygon(topBrush, topFace);
                }

                // Mặt phải (Right Face) - Xanh dương đậm
                PointF[] rightFace = new PointF[] { center, topRight, topRightBottom, bottomCenter };
                using (SolidBrush rightBrush = new SolidBrush(Color.FromArgb(2, 132, 199)))
                {
                    g.FillPolygon(rightBrush, rightFace);
                }

                // Mặt trái (Left Face) - Xanh lục bảo
                PointF[] leftFace = new PointF[] { center, topLeft, topLeftBottom, bottomCenter };
                using (SolidBrush leftBrush = new SolidBrush(Color.FromArgb(14, 165, 233)))
                {
                    g.FillPolygon(leftBrush, leftFace);
                }

                // Viền khối 3D
                using (Pen borderPen = new Pen(Color.FromArgb(224, 242, 254), 1.2f * scale))
                {
                    g.DrawPolygon(borderPen, topFace);
                    g.DrawPolygon(borderPen, rightFace);
                    g.DrawPolygon(borderPen, leftFace);
                }

                // Nhãn Category ở chân card
                string label = categoryName;
                if (label.Length > 16) label = label.Substring(0, 16) + "..";

                using (Font font = new Font("Segoe UI", 8.0f * scale, FontStyle.Bold))
                using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(148, 163, 184)))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    RectangleF rect = new RectangleF(2f, height - 24f * scale, width - 4f, 20f * scale);
                    g.DrawString(label, font, textBrush, rect, sf);
                }
            }
            return bmp;
        }

        /// <summary>
        /// Loại bỏ dấu tiếng Việt để tìm kiếm không dấu chính xác 100%
        /// </summary>
        public static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            string normalizedString = text.Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < normalizedString.Length; i++)
            {
                char c = normalizedString[i];
                UnicodeCategory uc = CharUnicodeInfo.GetUnicodeCategory(c);
                if (uc != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }

            string res = sb.ToString().Normalize(NormalizationForm.FormC);
            res = res.Replace('Đ', 'D').Replace('đ', 'd');
            return res;
        }
    }

    /// <summary>Bỏ qua cảnh báo khi đặt tạm Family để xem trước 3D (giao dịch luôn RollBack).</summary>
    internal class HDToolSilentFailures : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            try { failuresAccessor.DeleteAllWarnings(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Bỏ cảnh báo lỗi: " + ex.Message); }
            return FailureProcessingResult.Continue;
        }
    }
}
