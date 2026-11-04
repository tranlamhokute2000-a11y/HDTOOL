using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace HDTool
{
    /// <summary>
    /// Cấu hình lưu trữ trạng thái hiển thị của từng cột trên bảng:
    /// Thứ tự hiển thị (DisplayIndex), độ rộng (Width), trạng thái ẩn/hiện (Visible)
    /// </summary>
    public class MepColumnConfig
    {
        public int DisplayIndex { get; set; }
        public int Width { get; set; }
        public bool Visible { get; set; }

        public MepColumnConfig()
        {
            Visible = true;
            Width = 100;
            DisplayIndex = -1;
        }

        public MepColumnConfig(int displayIndex, int width, bool visible)
        {
            DisplayIndex = displayIndex;
            Width = width;
            Visible = visible;
        }
    }

    /// <summary>
    /// Trình quản lý ghi nhớ tùy chọn người dùng (User Preferences Persistence) theo lệnh Sếp Hồ:
    /// Tự động nhớ 100% tất cả các tác vụ/tùy chọn (chế độ bóc, cách đo đoạn ống, highlight,
    /// các cột hiển thị, thứ tự cột kéo thả, định dạng bảng và cách đặt vào Sheet...)
    /// Hoàn toàn độc lập, thuần .NET, không phụ thuộc thư viện ngoài (tương thích 100% cả Revit 2024 .NET 4.8 và Revit 2025+ .NET 8)
    /// </summary>
    public class MepUserSettings
    {
        private static MepUserSettings _instance;
        private static readonly object _lock = new object();

        // 1. Chế độ bóc khối lượng: 0 = Tất cả (All), 1 = Ống nước (PipesOnly), 2 = Phụ kiện (FittingsOnly)
        public int FilterMode { get; set; }

        // 2. Chế độ đo đoạn ống: true = Tách rời rạc (Không SUM), false = Gom mét dài (SUM)
        public bool DiscretePipes { get; set; }

        // 3. Tùy chọn tự động Highlight đối tượng trên Revit: true / false
        public bool AutoHighlight { get; set; }

        // 4. Phạm vi quét: 0 = Khung nhìn hiện hành, 1 = Vùng quét cục bộ, 2 = Toàn bộ dự án
        public int ScopeOption { get; set; }

        // 5. Cửa sổ Đặt Bảng Vào Sheet:
        // Định dạng bảng: 0 = Bảng kẻ khung Excel chuẩn 100%, 1 = Bảng ViewSchedule Revit
        public int SheetTableFormat { get; set; }

        // Cách đặt bảng: 0 = Đặt trực tiếp vào Sheet mong muốn, 1 = Click chuột chọn điểm, 2 = Tạo vào Project Browser
        public int SheetPlacementMode { get; set; }

        // Số hiệu Sheet đã chọn lần trước (VD: "C101.TN.4F.KRL.1")
        public string LastSelectedSheetNumber { get; set; }

        // Tên bảng Schedule tạo gần nhất
        public string LastScheduleName { get; set; }

        // 6. Danh sách các cột Parameter dự án tùy chỉnh đã thêm
        public List<string> CustomParamColumns { get; set; }

        // 7. Cấu hình chi tiết từng cột: Thứ tự (DisplayIndex), Độ rộng (Width), Ẩn/Hiện (Visible)
        public Dictionary<string, MepColumnConfig> Columns { get; set; }

        public MepUserSettings()
        {
            FilterMode = 0; // Mặc định: Tất cả
            DiscretePipes = true; // Mặc định: Tách rời rạc theo lệnh Sếp Hồ
            AutoHighlight = true; // Mặc định: Bật tự động highlight
            ScopeOption = 0; // Mặc định: Khung nhìn hiện hành
            SheetTableFormat = 0; // Mặc định: Bảng kẻ khung Excel chuẩn 100%
            SheetPlacementMode = 1; // Mặc định: Click chuột chọn điểm đặt trên Sheet
            LastSelectedSheetNumber = string.Empty;
            LastScheduleName = string.Empty;
            CustomParamColumns = new List<string>();
            Columns = new Dictionary<string, MepColumnConfig>(StringComparer.OrdinalIgnoreCase);
        }

        private static string GetSettingsFilePath()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string hdFolder = Path.Combine(appData, "HDTool");
                if (!Directory.Exists(hdFolder))
                {
                    Directory.CreateDirectory(hdFolder);
                }
                return Path.Combine(hdFolder, "hdtool_mep_user_settings.json");
            }
            catch
            {
                return "hdtool_mep_user_settings.json";
            }
        }

        public static MepUserSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = Load();
                        }
                    }
                }
                return _instance;
            }
        }

        public void Save()
        {
            try
            {
                string path = GetSettingsFilePath();
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"FilterMode\": {0},", FilterMode));
                sb.AppendLine(string.Format("  \"DiscretePipes\": {0},", DiscretePipes ? "true" : "false"));
                sb.AppendLine(string.Format("  \"AutoHighlight\": {0},", AutoHighlight ? "true" : "false"));
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"ScopeOption\": {0},", ScopeOption));
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"SheetTableFormat\": {0},", SheetTableFormat));
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"SheetPlacementMode\": {0},", SheetPlacementMode));
                sb.AppendLine(string.Format("  \"LastSelectedSheetNumber\": \"{0}\",", EscapeJson(LastSelectedSheetNumber ?? "")));
                sb.AppendLine(string.Format("  \"LastScheduleName\": \"{0}\",", EscapeJson(LastScheduleName ?? "")));

                // CustomParamColumns
                sb.Append("  \"CustomParamColumns\": [");
                if (CustomParamColumns != null && CustomParamColumns.Count > 0)
                {
                    for (int i = 0; i < CustomParamColumns.Count; i++)
                    {
                        sb.Append(string.Format("\"{0}\"", EscapeJson(CustomParamColumns[i])));
                        if (i < CustomParamColumns.Count - 1) sb.Append(", ");
                    }
                }
                sb.AppendLine("],");

                // Columns
                sb.AppendLine("  \"Columns\": {");
                if (Columns != null && Columns.Count > 0)
                {
                    int cIdx = 0;
                    int cTotal = Columns.Count;
                    foreach (var kvp in Columns)
                    {
                        cIdx++;
                        string comma = (cIdx < cTotal) ? "," : "";
                        sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                            "    \"{0}\": {{ \"DisplayIndex\": {1}, \"Width\": {2}, \"Visible\": {3} }}{4}",
                            EscapeJson(kvp.Key), kvp.Value.DisplayIndex, kvp.Value.Width, kvp.Value.Visible ? "true" : "false", comma));
                    }
                }
                sb.AppendLine("  }");
                sb.AppendLine("}");

                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        public static MepUserSettings Load()
        {
            MepUserSettings settings = new MepUserSettings();
            try
            {
                string path = GetSettingsFilePath();
                if (!File.Exists(path)) return settings;

                string json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json)) return settings;

                // Parse FilterMode
                Match mFilter = Regex.Match(json, "\"FilterMode\"\\s*:\\s*(\\d+)");
                if (mFilter.Success) settings.FilterMode = int.Parse(mFilter.Groups[1].Value);

                // Parse DiscretePipes
                Match mDiscrete = Regex.Match(json, "\"DiscretePipes\"\\s*:\\s*(true|false)", RegexOptions.IgnoreCase);
                if (mDiscrete.Success) settings.DiscretePipes = bool.Parse(mDiscrete.Groups[1].Value);

                // Parse AutoHighlight
                Match mHighlight = Regex.Match(json, "\"AutoHighlight\"\\s*:\\s*(true|false)", RegexOptions.IgnoreCase);
                if (mHighlight.Success) settings.AutoHighlight = bool.Parse(mHighlight.Groups[1].Value);

                // Parse ScopeOption
                Match mScope = Regex.Match(json, "\"ScopeOption\"\\s*:\\s*(\\d+)");
                if (mScope.Success) settings.ScopeOption = int.Parse(mScope.Groups[1].Value);

                // Parse SheetTableFormat
                Match mFormat = Regex.Match(json, "\"SheetTableFormat\"\\s*:\\s*(\\d+)");
                if (mFormat.Success) settings.SheetTableFormat = int.Parse(mFormat.Groups[1].Value);

                // Parse SheetPlacementMode
                Match mPlacement = Regex.Match(json, "\"SheetPlacementMode\"\\s*:\\s*(\\d+)");
                if (mPlacement.Success) settings.SheetPlacementMode = int.Parse(mPlacement.Groups[1].Value);

                // Parse LastSelectedSheetNumber
                Match mSheet = Regex.Match(json, "\"LastSelectedSheetNumber\"\\s*:\\s*\"([^\"]*)\"");
                if (mSheet.Success) settings.LastSelectedSheetNumber = UnescapeJson(mSheet.Groups[1].Value);

                // Parse LastScheduleName
                Match mSched = Regex.Match(json, "\"LastScheduleName\"\\s*:\\s*\"([^\"]*)\"");
                if (mSched.Success) settings.LastScheduleName = UnescapeJson(mSched.Groups[1].Value);

                // Parse CustomParamColumns
                Match mCustom = Regex.Match(json, "\"CustomParamColumns\"\\s*:\\s*\\[([^\\]]*)\\]");
                if (mCustom.Success)
                {
                    string arrayBody = mCustom.Groups[1].Value;
                    MatchCollection matches = Regex.Matches(arrayBody, "\"([^\"]*)\"");
                    foreach (Match m in matches)
                    {
                        string pName = UnescapeJson(m.Groups[1].Value);
                        if (!string.IsNullOrEmpty(pName) && !settings.CustomParamColumns.Contains(pName))
                        {
                            settings.CustomParamColumns.Add(pName);
                        }
                    }
                }

                // Parse Columns dictionary
                Match mCols = Regex.Match(json, "\"Columns\"\\s*:\\s*\\{([\\s\\S]*?)\\}");
                if (mCols.Success)
                {
                    string colsBody = mCols.Groups[1].Value;
                    MatchCollection colMatches = Regex.Matches(colsBody, "\"([^\"]+)\"\\s*:\\s*\\{\\s*\"DisplayIndex\"\\s*:\\s*(-?\\d+)\\s*,\\s*\"Width\"\\s*:\\s*(\\d+)\\s*,\\s*\"Visible\"\\s*:\\s*(true|false)");
                    foreach (Match cm in colMatches)
                    {
                        string colName = UnescapeJson(cm.Groups[1].Value);
                        int dIdx = int.Parse(cm.Groups[2].Value);
                        int w = int.Parse(cm.Groups[3].Value);
                        bool v = bool.Parse(cm.Groups[4].Value);
                        settings.Columns[colName] = new MepColumnConfig(dIdx, w, v);
                    }
                }
            }
            catch { }

            return settings;
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private static string UnescapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\\"", "\"").Replace("\\\\", "\\");
        }
    }
}
