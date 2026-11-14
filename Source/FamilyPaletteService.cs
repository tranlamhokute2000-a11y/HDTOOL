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
                // 1. Đếm số lượng thực thể FamilyInstance đang được đặt trong mô hình theo từng TypeId (chạy 1 pass O(N) siêu nhanh)
                Dictionary<ElementId, int> instanceCounts = new Dictionary<ElementId, int>();
                try
                {
                    FilteredElementCollector instanceCol = new FilteredElementCollector(doc)
                        .OfClass(typeof(FamilyInstance))
                        .WhereElementIsNotElementType();

                    foreach (Element e in instanceCol)
                    {
                        ElementId typeId = e.GetTypeId();
                        if (typeId != null && typeId != ElementId.InvalidElementId)
                        {
                            int c;
                            if (instanceCounts.TryGetValue(typeId, out c))
                            {
                                instanceCounts[typeId] = c + 1;
                            }
                            else
                            {
                                instanceCounts[typeId] = 1;
                            }
                        }
                    }
                }
                catch { }

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
                    BuiltInCategory bic = (cat != null) ? (BuiltInCategory)cat.Id.IntegerValue : BuiltInCategory.INVALID;
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
                bic = (BuiltInCategory)cat.Id.IntegerValue;
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
}
