using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using Autodesk.Revit.DB;
using Color = System.Drawing.Color;

namespace HDTool
{
    /// <summary>
    /// Nhóm danh mục MEP để lọc trên giao diện bảng quy đổi Layer
    /// </summary>
    public enum MepCategoryGroup
    {
        All = 0,
        Ducts = 1,
        Pipes = 2,
        Equipment = 3,
        Electrical = 4
    }

    /// <summary>
    /// Chế độ lựa chọn nguồn xuất bản vẽ sang CAD theo chỉ đạo của Sếp Hồ
    /// </summary>
    public enum CadExportScope
    {
        ActiveView = 0,     // View Đang Vẽ (Active View)
        Sheets = 1,         // Danh Sách Sheet (Bản Vẽ)
        ProjectViews = 2    // Danh Sách View Dự Án (Mặt bằng, Mặt cắt, 3D...)
    }

    /// <summary>
    /// Đối tượng đại diện cho một View hoặc Sheet cần xuất sang CAD
    /// </summary>
    public class CadExportTargetItem
    {
        public ElementId Id { get; set; }
        public string ItemNumber { get; set; }     // Số hiệu (VD: "P-01", "M-02" đối với Sheet) hoặc rỗng
        public string ItemName { get; set; }       // Tên Sheet hoặc Tên View
        public string CategoryOrType { get; set; } // "Sheet", "FloorPlan", "CeilingPlan", "ThreeD", "Section"...
        public bool IsSelected { get; set; }

        public CadExportTargetItem()
        {
            this.Id = ElementId.InvalidElementId;
            this.ItemNumber = string.Empty;
            this.ItemName = string.Empty;
            this.CategoryOrType = string.Empty;
            this.IsSelected = false;
        }

        public CadExportTargetItem(ElementId id, string itemNumber, string itemName, string catOrType, bool isSelected)
        {
            this.Id = id;
            this.ItemNumber = itemNumber != null ? itemNumber : string.Empty;
            this.ItemName = itemName != null ? itemName : string.Empty;
            this.CategoryOrType = catOrType != null ? catOrType : string.Empty;
            this.IsSelected = isSelected;
        }

        public string GetDisplayName()
        {
            if (!string.IsNullOrEmpty(this.ItemNumber))
            {
                return string.Format("[{0}] - {1}", this.ItemNumber, this.ItemName);
            }
            if (!string.IsNullOrEmpty(this.CategoryOrType) && !this.CategoryOrType.Equals("Sheet", StringComparison.OrdinalIgnoreCase))
            {
                return string.Format("({0}) {1}", this.CategoryOrType, this.ItemName);
            }
            return this.ItemName;
        }
    }

    /// <summary>
    /// Bản ghi quy đổi từ Hệ thống / Category trong Revit sang Layer AutoCAD
    /// </summary>
    public class CadLayerMappingItem
    {
        public bool IsEnabled { get; set; }
        public MepCategoryGroup CategoryGroup { get; set; }
        public string CategoryName { get; set; }
        public BuiltInCategory BuiltInCat { get; set; }
        public string RevitSystemName { get; set; }
        public string SystemClassification { get; set; }
        public string TargetCadLayerName { get; set; }
        public int ColorNumber { get; set; }
        public string Linetype { get; set; }
        public int Lineweight { get; set; } // in 1/100 mm (-1 for default, 25 for 0.25mm, 35 for 0.35mm, 50 for 0.50mm)

        public CadLayerMappingItem()
        {
            this.IsEnabled = true;
            this.CategoryGroup = MepCategoryGroup.All;
            this.CategoryName = string.Empty;
            this.BuiltInCat = BuiltInCategory.INVALID;
            this.RevitSystemName = "*";
            this.SystemClassification = string.Empty;
            this.TargetCadLayerName = string.Empty;
            this.ColorNumber = 7;
            this.Linetype = "Continuous";
            this.Lineweight = -1;
        }

        public CadLayerMappingItem(
            MepCategoryGroup group,
            string categoryName,
            BuiltInCategory builtInCat,
            string revitSystemName,
            string targetCadLayerName,
            int colorNumber,
            string linetype,
            int lineweight)
            : this(group, categoryName, builtInCat, revitSystemName, targetCadLayerName, colorNumber, linetype, lineweight, string.Empty)
        {
        }

        public CadLayerMappingItem(
            MepCategoryGroup group,
            string categoryName,
            BuiltInCategory builtInCat,
            string revitSystemName,
            string targetCadLayerName,
            int colorNumber,
            string linetype,
            int lineweight,
            string systemClassification)
        {
            this.IsEnabled = true;
            this.CategoryGroup = group;
            this.CategoryName = categoryName != null ? categoryName : string.Empty;
            this.BuiltInCat = builtInCat;
            this.RevitSystemName = revitSystemName != null ? revitSystemName : "*";
            this.TargetCadLayerName = targetCadLayerName != null ? targetCadLayerName : string.Empty;
            this.ColorNumber = (colorNumber > 0 && colorNumber <= 255) ? colorNumber : 7;
            this.Linetype = string.IsNullOrEmpty(linetype) ? "Continuous" : linetype;
            this.Lineweight = lineweight;
            this.SystemClassification = systemClassification != null ? systemClassification : string.Empty;
        }

        public CadLayerMappingItem Clone()
        {
            CadLayerMappingItem item = new CadLayerMappingItem();
            item.IsEnabled = this.IsEnabled;
            item.CategoryGroup = this.CategoryGroup;
            item.CategoryName = this.CategoryName;
            item.BuiltInCat = this.BuiltInCat;
            item.RevitSystemName = this.RevitSystemName;
            item.SystemClassification = this.SystemClassification;
            item.TargetCadLayerName = this.TargetCadLayerName;
            item.ColorNumber = this.ColorNumber;
            item.Linetype = this.Linetype;
            item.Lineweight = this.Lineweight;
            return item;
        }

        /// <summary>
        /// Lấy màu sắc System.Drawing.Color tương ứng với chỉ số màu AutoCAD (ACI 1-255)
        /// để hiển thị ô vuông màu trực quan trên DataGridView
        /// </summary>
        public Color GetColorRgb()
        {
            return AciToColor(this.ColorNumber);
        }

        /// <summary>
        /// Bảng tra cứu màu chuẩn AutoCAD Color Index (ACI 1-255)
        /// Tái hiện 100% chuẩn màu AutoCAD
        /// </summary>
        public static Color AciToColor(int aci)
        {
            return CadColorPickerDialog.GetAciRgb(aci);
        }
    }

    /// <summary>
    /// Cấu hình xuất CAD toàn diện kèm bảng quy đổi Layer
    /// </summary>
    public class CadExportMepSettings
    {
        public CadExportScope ExportScope { get; set; }
        public string TargetFolder { get; set; }
        public ACADVersion CadVersion { get; set; }
        public bool MergedViews { get; set; }
        public ExportUnit TargetUnit { get; set; }
        public bool SingleCombinedCad { get; set; }
        public string CombinedFileName { get; set; }
        public bool OpenFolderAfter { get; set; }
        public string PresetName { get; set; }
        public List<CadLayerMappingItem> LayerMappings { get; set; }

        public CadExportMepSettings()
        {
            this.ExportScope = CadExportScope.ActiveView;
            this.TargetFolder = string.Empty;
            this.CadVersion = ACADVersion.R2018;
            this.MergedViews = true;
            this.TargetUnit = ExportUnit.Millimeter;
            this.SingleCombinedCad = false;
            this.CombinedFileName = "HoSoBanVe_MEP.dwg";
            this.OpenFolderAfter = true;
            this.PresetName = "TCVN MEP";
            this.LayerMappings = new List<CadLayerMappingItem>();
        }

        /// <summary>
        /// Xuất cấu hình ra chuỗi JSON chuẩn thuần .NET 4.8 / C# 5
        /// </summary>
        public string ToJson()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"PresetName\": \"{0}\",", EscapeJson(this.PresetName)));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"ExportScope\": \"{0}\",", this.ExportScope.ToString()));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"CadVersion\": \"{0}\",", this.CadVersion.ToString()));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"MergedViews\": {0},", this.MergedViews ? "true" : "false"));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"SingleCombinedCad\": {0},", this.SingleCombinedCad ? "true" : "false"));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"CombinedFileName\": \"{0}\",", EscapeJson(this.CombinedFileName)));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"OpenFolderAfter\": {0},", this.OpenFolderAfter ? "true" : "false"));
            sb.AppendLine("  \"LayerMappings\": [");

            if (this.LayerMappings != null)
            {
                for (int i = 0; i < this.LayerMappings.Count; i++)
                {
                    CadLayerMappingItem item = this.LayerMappings[i];
                    sb.AppendLine("    {");
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "      \"IsEnabled\": {0},", item.IsEnabled ? "true" : "false"));
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "      \"CategoryGroup\": \"{0}\",", item.CategoryGroup.ToString()));
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "      \"CategoryName\": \"{0}\",", EscapeJson(item.CategoryName)));
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "      \"BuiltInCat\": \"{0}\",", item.BuiltInCat.ToString()));
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "      \"RevitSystemName\": \"{0}\",", EscapeJson(item.RevitSystemName)));
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "      \"SystemClassification\": \"{0}\",", EscapeJson(item.SystemClassification)));
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "      \"TargetCadLayerName\": \"{0}\",", EscapeJson(item.TargetCadLayerName)));
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "      \"ColorNumber\": {0},", item.ColorNumber));
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "      \"Linetype\": \"{0}\",", EscapeJson(item.Linetype)));
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "      \"Lineweight\": {0}", item.Lineweight));
                    sb.Append("    }");
                    if (i < this.LayerMappings.Count - 1) sb.Append(",");
                    sb.AppendLine();
                }
            }

            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        public bool SaveToFile(string filePath)
        {
            string error;
            return SaveToFile(filePath, out error);
        }

        public bool SaveToFile(string filePath, out string error)
        {
            error = string.Empty;
            try
            {
                string dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(filePath, this.ToJson(), Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Nạp cấu hình từ file JSON (overload tiện lợi)
        /// </summary>
        public static CadExportMepSettings LoadFromFile(string filePath)
        {
            string error;
            return LoadFromFile(filePath, out error);
        }

        /// <summary>
        /// Nạp cấu hình từ file JSON
        /// </summary>
        public static CadExportMepSettings LoadFromFile(string filePath, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                error = "File cấu hình không tồn tại.";
                return null;
            }

            try
            {
                string json = File.ReadAllText(filePath, Encoding.UTF8);
                return FromJson(json, out error);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return null;
            }
        }

        /// <summary>
        /// Bộ giải mã JSON nhẹ, độc lập 100% không phụ thuộc thư viện ngoài (chuẩn C# 5)
        /// </summary>
        public static CadExportMepSettings FromJson(string json, out string error)
        {
            error = string.Empty;
            CadExportMepSettings settings = new CadExportMepSettings();
            if (string.IsNullOrEmpty(json)) return settings;

            try
            {
                string pName = ExtractJsonString(json, "PresetName");
                settings.PresetName = pName != null ? pName : "Custom";

                string scopeStr = ExtractJsonString(json, "ExportScope");
                if (!string.IsNullOrEmpty(scopeStr))
                {
                    try { settings.ExportScope = (CadExportScope)Enum.Parse(typeof(CadExportScope), scopeStr, true); } catch { }
                }

                string openStr = ExtractJsonToken(json, "OpenFolderAfter");
                if (!string.IsNullOrEmpty(openStr))
                {
                    settings.OpenFolderAfter = openStr.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
                }

                string cadVerStr = ExtractJsonString(json, "CadVersion");
                if (!string.IsNullOrEmpty(cadVerStr))
                {
                    try { settings.CadVersion = (ACADVersion)Enum.Parse(typeof(ACADVersion), cadVerStr, true); } catch { }
                }

                string mergedStr = ExtractJsonToken(json, "MergedViews");
                if (!string.IsNullOrEmpty(mergedStr))
                {
                    settings.MergedViews = mergedStr.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
                }

                string singleStr = ExtractJsonToken(json, "SingleCombinedCad");
                if (!string.IsNullOrEmpty(singleStr))
                {
                    settings.SingleCombinedCad = singleStr.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
                }

                string combName = ExtractJsonString(json, "CombinedFileName");
                settings.CombinedFileName = combName != null ? combName : "HoSoBanVe_MEP.dwg";

                // Phân tích mảng LayerMappings
                int arrStart = json.IndexOf("\"LayerMappings\"", StringComparison.OrdinalIgnoreCase);
                if (arrStart >= 0)
                {
                    int openBracket = json.IndexOf('[', arrStart);
                    int closeBracket = json.LastIndexOf(']');
                    if (openBracket >= 0 && closeBracket > openBracket)
                    {
                        string arrayBody = json.Substring(openBracket + 1, closeBracket - openBracket - 1);
                        string[] itemBlocks = arrayBody.Split(new string[] { "}," }, StringSplitOptions.RemoveEmptyEntries);

                        for (int i = 0; i < itemBlocks.Length; i++)
                        {
                            string b = itemBlocks[i];
                            CadLayerMappingItem item = new CadLayerMappingItem();

                            string enabledStr = ExtractJsonToken(b, "IsEnabled");
                            if (!string.IsNullOrEmpty(enabledStr)) item.IsEnabled = enabledStr.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);

                            string groupStr = ExtractJsonString(b, "CategoryGroup");
                            if (!string.IsNullOrEmpty(groupStr))
                            {
                                try { item.CategoryGroup = (MepCategoryGroup)Enum.Parse(typeof(MepCategoryGroup), groupStr, true); } catch { }
                            }

                            string catName = ExtractJsonString(b, "CategoryName");
                            item.CategoryName = catName != null ? catName : string.Empty;

                            string catStr = ExtractJsonString(b, "BuiltInCat");
                            if (!string.IsNullOrEmpty(catStr))
                            {
                                try { item.BuiltInCat = (BuiltInCategory)Enum.Parse(typeof(BuiltInCategory), catStr, true); } catch { }
                            }

                            string sysName = ExtractJsonString(b, "RevitSystemName");
                            item.RevitSystemName = sysName != null ? sysName : "*";
                            string sysClass = ExtractJsonString(b, "SystemClassification");
                            item.SystemClassification = sysClass != null ? sysClass : string.Empty;
                            string targetLay = ExtractJsonString(b, "TargetCadLayerName");
                            item.TargetCadLayerName = targetLay != null ? targetLay : string.Empty;

                            string colStr = ExtractJsonToken(b, "ColorNumber");
                            int colVal;
                            if (int.TryParse(colStr, out colVal)) item.ColorNumber = colVal;

                            string ltStr = ExtractJsonString(b, "Linetype");
                            item.Linetype = ltStr != null ? ltStr : "Continuous";

                            string lwStr = ExtractJsonToken(b, "Lineweight");
                            int lwVal;
                            if (int.TryParse(lwStr, out lwVal)) item.Lineweight = lwVal;

                            settings.LayerMappings.Add(item);
                        }
                    }
                }

                return settings;
            }
            catch (Exception ex)
            {
                error = "Lỗi phân tích JSON: " + ex.Message;
                return settings;
            }
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
        }

        private static string ExtractJsonString(string json, string key)
        {
            string pattern = "\"" + key + "\"";
            int idx = json.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            int colon = json.IndexOf(':', idx + pattern.Length);
            if (colon < 0) return null;
            int startQuote = json.IndexOf('\"', colon + 1);
            if (startQuote < 0) return null;
            int endQuote = json.IndexOf('\"', startQuote + 1);
            if (endQuote < 0) return null;
            return json.Substring(startQuote + 1, endQuote - startQuote - 1).Replace("\\\"", "\"").Replace("\\\\", "\\");
        }

        private static string ExtractJsonToken(string json, string key)
        {
            string pattern = "\"" + key + "\"";
            int idx = json.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            int colon = json.IndexOf(':', idx + pattern.Length);
            if (colon < 0) return null;
            int end = json.IndexOfAny(new char[] { ',', '}', '\r', '\n' }, colon + 1);
            if (end < 0) end = json.Length;
            return json.Substring(colon + 1, end - colon - 1).Trim();
        }
    }
}
