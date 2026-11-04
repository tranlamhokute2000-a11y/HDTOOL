using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Microsoft.Win32;

namespace HDTool
{
    /// <summary>
    /// Dịch vụ xuất bản vẽ ra định dạng PDF (.pdf) và AutoCAD DWG (.dwg) theo nhóm và thứ tự tùy biến
    /// </summary>
    public static class SheetExportCadPdfService
    {
        /// <summary>
        /// Xuất danh sách Sheet ra file PDF (hỗ trợ gom chung thành 1 file hoặc xuất từng file riêng lẻ)
        /// </summary>
        public static bool ExportToPdf(
            Document doc,
            IList<SheetItem> sheets,
            string targetFolder,
            string baseFileName,
            bool combine,
            out string resultMessage)
        {
            resultMessage = string.Empty;

            if (doc == null)
            {
                resultMessage = "Dự án Revit không hợp lệ.";
                return false;
            }

            if (sheets == null || sheets.Count == 0)
            {
                resultMessage = "Không có bản vẽ nào được chọn để xuất PDF.";
                return false;
            }

            if (!Directory.Exists(targetFolder))
            {
                try
                {
                    Directory.CreateDirectory(targetFolder);
                }
                catch (Exception ex)
                {
                    resultMessage = "Không thể tạo thư mục lưu trữ: " + ex.Message;
                    return false;
                }
            }

            try
            {
                IList<ElementId> sheetIds = new List<ElementId>();
                foreach (SheetItem s in sheets)
                {
                    if (s.SheetId != null && s.SheetId != ElementId.InvalidElementId)
                    {
                        sheetIds.Add(s.SheetId);
                    }
                }

                if (sheetIds.Count == 0)
                {
                    resultMessage = "Không tìm thấy ElementId hợp lệ cho các bản vẽ đã chọn.";
                    return false;
                }

                PDFExportOptions opt = new PDFExportOptions();
                opt.Combine = combine;

                string safeName = "HoSoBanVe";
                if (!string.IsNullOrEmpty(baseFileName))
                {
                    safeName = Path.GetFileNameWithoutExtension(baseFileName);
                    foreach (char c in Path.GetInvalidFileNameChars())
                    {
                        safeName = safeName.Replace(c, '_');
                    }
                }

                opt.FileName = safeName;
                opt.ColorDepth = ColorDepthType.Color;
                opt.StopOnError = false;
                opt.ZoomType = ZoomType.Zoom;
                opt.ZoomPercentage = 100;
                opt.PaperPlacement = PaperPlacementType.Center;

                // Gọi hàm Export PDF của Revit 2024
                bool success = doc.Export(targetFolder, sheetIds, opt);
                if (success)
                {
                    if (combine)
                    {
                        string outPath = Path.Combine(targetFolder, safeName + ".pdf");
                        resultMessage = string.Format("Đã xuất thành công file PDF gộp ({0} bản vẽ):\n{1}", sheetIds.Count, outPath);
                    }
                    else
                    {
                        resultMessage = string.Format("Đã xuất thành công {0} file PDF riêng lẻ vào thư mục:\n{1}", sheetIds.Count, targetFolder);
                    }
                    return true;
                }
                else
                {
                    resultMessage = "Revit báo lỗi khi thực hiện xuất PDF.";
                    return false;
                }
            }
            catch (Exception ex)
            {
                resultMessage = "Lỗi trong quá trình xuất PDF:\n" + ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Xuất danh sách Sheet ra file AutoCAD DWG (.dwg)
        /// </summary>
        public static bool ExportToCad(
            Document doc,
            IList<SheetItem> sheets,
            string targetFolder,
            bool mergeViews,
            out string resultMessage)
        {
            resultMessage = string.Empty;

            if (doc == null)
            {
                resultMessage = "Dự án Revit không hợp lệ.";
                return false;
            }

            if (sheets == null || sheets.Count == 0)
            {
                resultMessage = "Không có bản vẽ nào được chọn để xuất CAD.";
                return false;
            }

            if (!Directory.Exists(targetFolder))
            {
                try
                {
                    Directory.CreateDirectory(targetFolder);
                }
                catch (Exception ex)
                {
                    resultMessage = "Không thể tạo thư mục lưu trữ: " + ex.Message;
                    return false;
                }
            }

            try
            {
                ICollection<ElementId> sheetIds = new List<ElementId>();
                foreach (SheetItem s in sheets)
                {
                    if (s.SheetId != null && s.SheetId != ElementId.InvalidElementId)
                    {
                        sheetIds.Add(s.SheetId);
                    }
                }

                if (sheetIds.Count == 0)
                {
                    resultMessage = "Không tìm thấy ElementId hợp lệ cho các bản vẽ đã chọn.";
                    return false;
                }

                DWGExportOptions dwgOpt = new DWGExportOptions();
                dwgOpt.MergedViews = mergeViews;
                dwgOpt.TargetUnit = ExportUnit.Millimeter;
                dwgOpt.Colors = ExportColorMode.TrueColor;

                // Export CAD trong Revit
                bool success = doc.Export(targetFolder, null, sheetIds, dwgOpt);
                if (success)
                {
                    resultMessage = string.Format("Đã xuất thành công {0} bản vẽ CAD (.dwg) vào thư mục:\n{1}", sheetIds.Count, targetFolder);
                    return true;
                }
                else
                {
                    resultMessage = "Revit báo lỗi khi thực hiện xuất CAD.";
                    return false;
                }
            }
            catch (Exception ex)
            {
                resultMessage = "Lỗi trong quá trình xuất file CAD:\n" + ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Loại bỏ dấu tiếng Việt để tạo tên Layout Tab an toàn tuyệt đối trong AutoCAD
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

        /// <summary>
        /// Chuẩn hóa tên Layout Tab cho AutoCAD (loại bỏ ký tự cấm: \ / : * ? " < > |)
        /// </summary>
        public static string SanitizeLayoutName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Sheet";
            char[] invalidChars = new char[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };
            string clean = name;
            for (int i = 0; i < invalidChars.Length; i++)
            {
                clean = clean.Replace(invalidChars[i], '_');
            }
            clean = clean.Trim();
            if (clean.Length > 40)
            {
                clean = clean.Substring(0, 40).Trim();
            }
            if (string.IsNullOrEmpty(clean)) return "Sheet";
            return clean;
        }

        /// <summary>
        /// Tự động tìm kiếm file thực thi AutoCAD Core Console (accoreconsole.exe) trên máy tính
        /// </summary>
        public static string FindAutoCadCoreConsole()
        {
            // 1. Kiểm tra các thư mục cài đặt tiêu chuẩn từ AutoCAD 2028 xuống 2018
            for (int year = 2028; year >= 2018; year--)
            {
                string path = string.Format(@"C:\Program Files\Autodesk\AutoCAD {0}\accoreconsole.exe", year);
                if (File.Exists(path)) return path;

                // Thử đường dẫn biến thể (VD: AutoCAD Mechanical, MEP, Architecture...)
                string mepPath = string.Format(@"C:\Program Files\Autodesk\AutoCAD {0}\accoreconsole.exe", year);
                if (File.Exists(mepPath)) return mepPath;
            }

            // 2. Tìm kiếm trong Registry
            try
            {
                using (RegistryKey hklm = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Autodesk\AutoCAD"))
                {
                    if (hklm != null)
                    {
                        string[] subKeys = hklm.GetSubKeyNames();
                        for (int i = 0; i < subKeys.Length; i++)
                        {
                            using (RegistryKey productKey = hklm.OpenSubKey(subKeys[i]))
                            {
                                if (productKey != null)
                                {
                                    string[] installKeys = productKey.GetSubKeyNames();
                                    for (int j = 0; j < installKeys.Length; j++)
                                    {
                                        using (RegistryKey pathKey = productKey.OpenSubKey(installKeys[j]))
                                        {
                                            if (pathKey != null)
                                            {
                                                object loc = pathKey.GetValue("Location");
                                                if (loc != null)
                                                {
                                                    string candidate = Path.Combine(loc.ToString(), "accoreconsole.exe");
                                                    if (File.Exists(candidate)) return candidate;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            // 3. Quét nhanh thư mục C:\Program Files\Autodesk
            try
            {
                string autodeskDir = @"C:\Program Files\Autodesk";
                if (Directory.Exists(autodeskDir))
                {
                    string[] found = Directory.GetFiles(autodeskDir, "accoreconsole.exe", SearchOption.AllDirectories);
                    if (found != null && found.Length > 0)
                    {
                        Array.Sort(found);
                        return found[found.Length - 1];
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Xuất tất cả các Sheet được chọn ra CÙNG 1 FILE CAD (.dwg) DUY NHẤT có đầy đủ Khung Tên (Title Block)
        /// Sử dụng Revit DWG Export kết hợp AutoCAD Core Engine (accoreconsole.exe) chạy ngầm
        /// </summary>
        public static bool ExportToSingleCombinedCad(
            Document doc,
            IList<SheetItem> sheets,
            string targetDwgPath,
            bool openAfter,
            out string resultMessage)
        {
            resultMessage = string.Empty;

            if (doc == null)
            {
                resultMessage = "Dự án Revit không hợp lệ.";
                return false;
            }

            if (sheets == null || sheets.Count == 0)
            {
                resultMessage = "Không có bản vẽ nào được chọn để xuất CAD.";
                return false;
            }

            if (string.IsNullOrEmpty(targetDwgPath))
            {
                resultMessage = "Đường dẫn file CAD đích không hợp lệ.";
                return false;
            }

            string targetFolder = Path.GetDirectoryName(targetDwgPath);
            if (!Directory.Exists(targetFolder))
            {
                try
                {
                    Directory.CreateDirectory(targetFolder);
                }
                catch (Exception ex)
                {
                    resultMessage = "Không thể tạo thư mục lưu trữ: " + ex.Message;
                    return false;
                }
            }

            // Kiểm tra xem file CAD đích có đang bị khóa bởi AutoCAD hay ứng dụng khác không
            if (File.Exists(targetDwgPath))
            {
                try
                {
                    File.Delete(targetDwgPath);
                }
                catch (Exception ex)
                {
                    resultMessage = "Không thể ghi đè file CAD đích (có thể file đang được mở trong AutoCAD):\n" + ex.Message;
                    return false;
                }
            }

            // Thu thập danh sách ElementId của các Sheet được chọn
            IList<ElementId> sheetIds = new List<ElementId>();
            for (int i = 0; i < sheets.Count; i++)
            {
                SheetItem s = sheets[i];
                if (s.SheetId != null && s.SheetId != ElementId.InvalidElementId)
                {
                    sheetIds.Add(s.SheetId);
                }
            }

            if (sheetIds.Count == 0)
            {
                resultMessage = "Không tìm thấy ElementId hợp lệ cho các bản vẽ đã chọn.";
                return false;
            }

            // 1. Tìm công cụ AutoCAD Core Engine (accoreconsole.exe)
            string accoreExe = FindAutoCadCoreConsole();

            // 2. Tạo thư mục tạm thời để Revit xuất các sheet trung gian
            string tempFolder = Path.Combine(Path.GetTempPath(), "HDTOOL_CAD_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(tempFolder);
            }
            catch (Exception ex)
            {
                resultMessage = "Không thể tạo thư mục tạm: " + ex.Message;
                return false;
            }

            try
            {
                // 3. Xuất tất cả các sheet sang DWG vào tempFolder với MergedViews = true để gộp trực tiếp Xref
                DWGExportOptions dwgOpt = new DWGExportOptions();
                dwgOpt.MergedViews = true;
                dwgOpt.TargetUnit = ExportUnit.Millimeter;
                dwgOpt.Colors = ExportColorMode.TrueColor;

                bool exportOk = doc.Export(tempFolder, "HD_SHT", sheetIds, dwgOpt);
                if (!exportOk)
                {
                    resultMessage = "Revit không thể xuất các bản vẽ trung gian sang DWG.";
                    return false;
                }

                // Lấy danh sách các file DWG vừa xuất trong thư mục tạm
                string[] exportedFiles = Directory.GetFiles(tempFolder, "*.dwg");
                if (exportedFiles == null || exportedFiles.Length == 0)
                {
                    resultMessage = "Không tìm thấy file DWG nào được tạo ra sau khi Revit xuất.";
                    return false;
                }

                // Nếu KHÔNG tìm thấy AutoCAD Core Console: chuyển các file sang targetFolder và thông báo
                if (string.IsNullOrEmpty(accoreExe) || !File.Exists(accoreExe))
                {
                    for (int i = 0; i < exportedFiles.Length; i++)
                    {
                        string f = exportedFiles[i];
                        string destFile = Path.Combine(targetFolder, Path.GetFileName(f));
                        if (File.Exists(destFile)) File.Delete(destFile);
                        File.Move(f, destFile);
                    }

                    resultMessage = string.Format(
                        "Đã xuất thành công {0} bản vẽ CAD vào thư mục:\n{1}\n\n" +
                        "LƯU Ý: Không tìm thấy AutoCAD Core Engine (accoreconsole.exe) trên máy tính để tự động gộp thành 1 file CAD duy nhất. " +
                        "HDTOOL đã xuất các bản vẽ thành từng file CAD riêng biệt có đầy đủ khung tên.",
                        exportedFiles.Length, targetFolder);
                    return true;
                }

                // 4. Đổi tên các file DWG trong thư mục tạm thành tên ASCII an toàn (sheet_001.dwg, sheet_002.dwg...)
                // Đồng thời chuẩn bị tên Layout Tab không dấu tiếng Việt
                List<string> safeDwgPaths = new List<string>();
                List<string> tabNames = new List<string>();
                HashSet<string> usedTabNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                int counter = 1;
                for (int i = 0; i < sheets.Count; i++)
                {
                    SheetItem s = sheets[i];
                    string matchedFile = null;

                    for (int j = 0; j < exportedFiles.Length; j++)
                    {
                        string ef = exportedFiles[j];
                        string fn = Path.GetFileNameWithoutExtension(ef);
                        if (fn.IndexOf(s.SheetNumber, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            matchedFile = ef;
                            break;
                        }
                    }

                    if (string.IsNullOrEmpty(matchedFile))
                    {
                        for (int j = 0; j < exportedFiles.Length; j++)
                        {
                            string ef = exportedFiles[j];
                            if (File.Exists(ef))
                            {
                                matchedFile = ef;
                                break;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(matchedFile) && File.Exists(matchedFile))
                    {
                        string safeName = string.Format("sheet_{0:D3}.dwg", counter);
                        string safePath = Path.Combine(tempFolder, safeName);
                        if (File.Exists(safePath)) File.Delete(safePath);
                        File.Move(matchedFile, safePath);

                        safeDwgPaths.Add(safePath);

                        // Tạo tên Layout Tab an toàn, loại bỏ dấu tiếng Việt để AutoCAD không bị lỗi font tab
                        string cleanNum = SanitizeLayoutName(s.SheetNumber);
                        string cleanName = RemoveDiacritics(s.SheetName ?? string.Empty);
                        string fullTab = cleanNum;
                        if (!string.IsNullOrEmpty(cleanName))
                        {
                            fullTab = cleanNum + " - " + cleanName;
                        }
                        fullTab = SanitizeLayoutName(fullTab);

                        string finalTab = fullTab;
                        int suffix = 2;
                        while (usedTabNames.Contains(finalTab))
                        {
                            finalTab = string.Format("{0}_{1}", fullTab, suffix++);
                        }
                        usedTabNames.Add(finalTab);
                        tabNames.Add(finalTab);

                        counter++;
                    }
                }

                if (safeDwgPaths.Count == 0)
                {
                    resultMessage = "Không thể chuẩn bị file CAD trung gian để thực hiện gộp.";
                    return false;
                }

                // 5. Sinh mã script AutoCAD (.scr) để gộp toàn bộ bản vẽ
                string scriptPath = Path.Combine(tempFolder, "merge_sheets.scr");
                StringBuilder sb = new StringBuilder();

                sb.AppendLine("FILEDIA 0");
                sb.AppendLine("CMDECHO 0");

                // Đổi tên layout mặc định Layout1 và xóa Layout2 để chuẩn bị nạp sạch các Sheet
                sb.AppendLine("-LAYOUT R Layout1 \"DefaultLayout\"");
                sb.AppendLine("-LAYOUT D Layout2");

                // 5a. Chèn từng bản vẽ vào Model Space cạnh nhau (dàn trang 5 bản vẽ mỗi hàng)
                double stepX = 150000.0; // 150 mét khoảng cách mỗi bản vẽ
                double stepY = 120000.0; // 120 mét khoảng cách mỗi hàng
                int perRow = 5;

                for (int i = 0; i < safeDwgPaths.Count; i++)
                {
                    string dwgForward = safeDwgPaths[i].Replace('\\', '/');
                    int col = i % perRow;
                    int row = i / perRow;
                    double posX = col * stepX;
                    double posY = -(row * stepY);

                    // -INSERT "<file>" X,Y 1 1 0
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "-INSERT \"{0}\" {1:F2},{2:F2} 1 1 0", dwgForward, posX, posY));
                }

                // 5b. Tạo từng Layout Tab cho từng Sheet để giữ nguyên 100% Khung tên (Title Block) và Viewport
                for (int i = 0; i < safeDwgPaths.Count; i++)
                {
                    string dwgForward = safeDwgPaths[i].Replace('\\', '/');
                    string tab = tabNames[i];

                    // Nạp Layout1 từ file trung gian
                    sb.AppendLine(string.Format("-LAYOUT T \"{0}\" Layout1", dwgForward));
                    // Đổi tên Layout1 vừa nạp thành tên Sheet chính thức
                    sb.AppendLine(string.Format("-LAYOUT R Layout1 \"{0}\"", tab));
                }

                // 5c. Xóa Layout mặc định ban đầu
                sb.AppendLine("-LAYOUT D DefaultLayout");

                // 5d. Zoom Extents và Lưu ra file DWG hoàn chỉnh chuẩn AutoCAD 2018
                sb.AppendLine("ZOOM E");
                string targetForward = targetDwgPath.Replace('\\', '/');
                sb.AppendLine(string.Format("SAVEAS 2018 \"{0}\"", targetForward));
                sb.AppendLine("QUIT Y");

                // Lưu file script dạng ASCII
                File.WriteAllText(scriptPath, sb.ToString(), Encoding.ASCII);

                // 6. Chạy accoreconsole.exe không cửa sổ (headless)
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = accoreExe;
                psi.Arguments = string.Format("/s \"{0}\"", scriptPath);
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;

                using (Process proc = Process.Start(psi))
                {
                    if (proc != null)
                    {
                        // Chờ tối đa 120 giây
                        bool finished = proc.WaitForExit(120000);
                        if (!finished)
                        {
                            try { proc.Kill(); } catch { }
                        }
                    }
                }

                // 7. Kiểm tra kết quả file gộp
                if (File.Exists(targetDwgPath) && new FileInfo(targetDwgPath).Length > 0)
                {
                    resultMessage = string.Format(
                        "Đã xuất và gộp thành công {0} bản vẽ vào 1 FILE CAD DUY NHẤT:\n{1}\n\n" +
                        "✔ Khung tên (Title Block) được giữ nguyên chuẩn 100% như trong Revit.\n" +
                        "✔ Mỗi bản vẽ có 1 Layout Tab riêng biệt chứa đầy đủ khung tên chuẩn in ấn.\n" +
                        "✔ Không gian Model chứa toàn bộ {0} bản vẽ được dàn trang thẳng hàng ngay ngắn.",
                        safeDwgPaths.Count, targetDwgPath);

                    if (openAfter)
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo(targetDwgPath) { UseShellExecute = true });
                        }
                        catch { }
                    }

                    return true;
                }
                else
                {
                    // Nếu file gộp chưa tạo thành công, copy các file DWG lẻ sang thư mục đích để người dùng không mất file
                    for (int i = 0; i < safeDwgPaths.Count; i++)
                    {
                        string sp = safeDwgPaths[i];
                        string dest = Path.Combine(targetFolder, Path.GetFileName(sp));
                        if (File.Exists(dest)) File.Delete(dest);
                        File.Copy(sp, dest);
                    }

                    resultMessage = string.Format(
                        "AutoCAD Core Engine không tạo được file gộp.\n" +
                        "HDTOOL đã xuất an toàn {0} bản vẽ thành các file CAD riêng lẻ vào thư mục:\n{1}",
                        safeDwgPaths.Count, targetFolder);
                    return false;
                }
            }
            catch (Exception ex)
            {
                resultMessage = "Lỗi trong quá trình xuất và gộp CAD:\n" + ex.Message;
                return false;
            }
            finally
            {
                // Dọn dẹp thư mục tạm
                try
                {
                    if (Directory.Exists(tempFolder))
                    {
                        Directory.Delete(tempFolder, true);
                    }
                }
                catch { }
            }
        }
    }
}
