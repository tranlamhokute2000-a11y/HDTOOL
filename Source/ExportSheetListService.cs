using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Windows.Forms;
using Autodesk.Revit.DB;

namespace HDTool
{
    /// <summary>
    /// Xử lý logic trích xuất danh sách bản vẽ từ Revit Document và xuất ra Excel, CSV, Clipboard
    /// </summary>
    public static class ExportSheetListService
    {
        [SuppressUnmanagedCodeSecurity]
        private static class NativeMethods
        {
            [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
            public static extern int StrCmpLogicalW(string psz1, string psz2);
        }

        public static int NativeCompare(string x, string y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            try
            {
                return NativeMethods.StrCmpLogicalW(x, y);
            }
            catch
            {
                return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
            }
        }

        public class NaturalStringComparer : IComparer<string>
        {
            public int Compare(string x, string y)
            {
                return NativeCompare(x, y);
            }
        }

        /// <summary>
        /// Lấy toàn bộ danh sách Sheet trong Document và sắp xếp theo thứ tự tự nhiên của Sheet Number
        /// </summary>
        public static List<SheetItem> GetSheetsFromDocument(Document doc)
        {
            List<SheetItem> list = new List<SheetItem>();
            if (doc == null) return list;

            try
            {
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                IList<Element> sheetElements = collector.OfClass(typeof(ViewSheet))
                                                        .WhereElementIsNotElementType()
                                                        .ToElements();

                foreach (Element elem in sheetElements)
                {
                    ViewSheet vs = elem as ViewSheet;
                    if (vs == null || vs.IsTemplate) continue;

                    SheetItem item = new SheetItem();
                    item.IsSelected = true;
                    item.SheetId = vs.Id;
                    item.SheetNumber = vs.SheetNumber ?? string.Empty;
                    item.OriginalSheetNumber = item.SheetNumber;
                    item.SheetName = vs.Name ?? string.Empty;
                    item.OriginalSheetName = item.SheetName;
                    item.GroupTitle = string.Empty;
                    item.IsPlaceholder = vs.IsPlaceholder;

                    // Lấy các tham số chuẩn nếu có
                    Parameter pRev = vs.get_Parameter(BuiltInParameter.SHEET_CURRENT_REVISION);
                    if (pRev != null && pRev.HasValue) item.Revision = pRev.AsString() ?? string.Empty;

                    Parameter pDate = vs.get_Parameter(BuiltInParameter.SHEET_ISSUE_DATE);
                    if (pDate != null && pDate.HasValue) item.IssueDate = pDate.AsString() ?? string.Empty;

                    Parameter pDrawn = vs.get_Parameter(BuiltInParameter.SHEET_DRAWN_BY);
                    if (pDrawn != null && pDrawn.HasValue) item.DrawnBy = pDrawn.AsString() ?? string.Empty;

                    Parameter pAppr = vs.get_Parameter(BuiltInParameter.SHEET_APPROVED_BY);
                    if (pAppr != null && pAppr.HasValue) item.ApprovedBy = pAppr.AsString() ?? string.Empty;

                    Parameter pScale = vs.get_Parameter(BuiltInParameter.SHEET_SCALE);
                    if (pScale != null && pScale.HasValue) item.Scale = pScale.AsString() ?? string.Empty;

                    list.Add(item);
                }
            }
            catch { }

            // Sắp xếp tự nhiên theo Sheet Number
            NaturalStringComparer comparer = new NaturalStringComparer();
            list.Sort(delegate(SheetItem a, SheetItem b)
            {
                int cmp = comparer.Compare(a.SheetNumber, b.SheetNumber);
                if (cmp != 0) return cmp;
                return comparer.Compare(a.SheetName, b.SheetName);
            });

            return list;
        }

        /// <summary>
        /// Xuất danh mục bản vẽ ra file CSV chuẩn UTF-8 có BOM (Excel mở trực tiếp không lỗi tiếng Việt)
        /// </summary>
        public static void ExportToCsv(string filePath, IList<SheetItem> sheets)
        {
            if (string.IsNullOrEmpty(filePath) || sheets == null) return;

            StringBuilder sb = new StringBuilder();

            // Header (7 cột chuẩn)
            sb.AppendLine("STT,Số Hiệu Bản Vẽ,Tên Bản Vẽ,Phiên Bản,Ngày Phát Hành,Người Vẽ,Tỷ Lệ");

            int index = 1;
            foreach (SheetItem s in sheets)
            {
                sb.AppendLine(string.Format("{0},{1},{2},{3},{4},{5},{6}",
                    index++,
                    EscapeCsv(s.SheetNumber),
                    EscapeCsv(s.SheetName),
                    EscapeCsv(s.Revision),
                    EscapeCsv(s.IssueDate),
                    EscapeCsv(s.DrawnBy),
                    EscapeCsv(s.Scale)
                ));
            }

            // Ghi file với UTF-8 BOM
            File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(true));
        }

        /// <summary>
        /// Xuất danh mục bản vẽ ra định dạng Excel XML Spreadsheet (định dạng bảng biểu đẹp, có màu sắc, viền khung và mở trực tiếp trong Excel)
        /// </summary>
        public static void ExportToExcelXml(string filePath, IList<SheetItem> sheets, string projectName)
        {
            if (string.IsNullOrEmpty(filePath) || sheets == null) return;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<?mso-application progid=\"Excel.Sheet\"?>");
            sb.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\"");
            sb.AppendLine(" xmlns:o=\"urn:schemas-microsoft-com:office:office\"");
            sb.AppendLine(" xmlns:x=\"urn:schemas-microsoft-com:office:excel\"");
            sb.AppendLine(" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\"");
            sb.AppendLine(" xmlns:html=\"http://www.w3.org/TR/REC-html40\">");

            // Styles
            sb.AppendLine(" <Styles>");
            sb.AppendLine("  <Style ss:ID=\"Default\" ss:Name=\"Normal\">");
            sb.AppendLine("   <Alignment ss:Vertical=\"Center\"/>");
            sb.AppendLine("   <Font ss:FontName=\"Segoe UI\" ss:Size=\"10\" ss:Color=\"#000000\"/>");
            sb.AppendLine("  </Style>");

            // Style: Title Project
            sb.AppendLine("  <Style ss:ID=\"TitleProject\">");
            sb.AppendLine("   <Alignment ss:Horizontal=\"Center\" ss:Vertical=\"Center\"/>");
            sb.AppendLine("   <Font ss:FontName=\"Segoe UI\" ss:Size=\"15\" ss:Bold=\"1\" ss:Color=\"#182B49\"/>");
            sb.AppendLine("  </Style>");

            // Style: Subtitle
            sb.AppendLine("  <Style ss:ID=\"Subtitle\">");
            sb.AppendLine("   <Alignment ss:Horizontal=\"Center\" ss:Vertical=\"Center\"/>");
            sb.AppendLine("   <Font ss:FontName=\"Segoe UI\" ss:Size=\"10\" ss:Italic=\"1\" ss:Color=\"#7F8C8D\"/>");
            sb.AppendLine("  </Style>");

            // Style: Header Columns
            sb.AppendLine("  <Style ss:ID=\"HeaderCol\">");
            sb.AppendLine("   <Alignment ss:Horizontal=\"Center\" ss:Vertical=\"Center\" ss:WrapText=\"1\"/>");
            sb.AppendLine("   <Borders>");
            sb.AppendLine("    <Border ss:Position=\"Bottom\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#000000\"/>");
            sb.AppendLine("    <Border ss:Position=\"Left\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#000000\"/>");
            sb.AppendLine("    <Border ss:Position=\"Right\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#000000\"/>");
            sb.AppendLine("    <Border ss:Position=\"Top\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#000000\"/>");
            sb.AppendLine("   </Borders>");
            sb.AppendLine("   <Font ss:FontName=\"Segoe UI\" ss:Size=\"10\" ss:Bold=\"1\" ss:Color=\"#FFFFFF\"/>");
            sb.AppendLine("   <Interior ss:Color=\"#182B49\" ss:Pattern=\"Solid\"/>");
            sb.AppendLine("  </Style>");

            // Style: Data Center
            sb.AppendLine("  <Style ss:ID=\"DataCenter\">");
            sb.AppendLine("   <Alignment ss:Horizontal=\"Center\" ss:Vertical=\"Center\"/>");
            sb.AppendLine("   <Borders>");
            sb.AppendLine("    <Border ss:Position=\"Bottom\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#D0D0D0\"/>");
            sb.AppendLine("    <Border ss:Position=\"Left\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#D0D0D0\"/>");
            sb.AppendLine("    <Border ss:Position=\"Right\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#D0D0D0\"/>");
            sb.AppendLine("    <Border ss:Position=\"Top\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#D0D0D0\"/>");
            sb.AppendLine("   </Borders>");
            sb.AppendLine("   <Font ss:FontName=\"Segoe UI\" ss:Size=\"10\"/>");
            sb.AppendLine("  </Style>");

            // Style: Data Center Bold (for Sheet Number)
            sb.AppendLine("  <Style ss:ID=\"DataCenterBold\">");
            sb.AppendLine("   <Alignment ss:Horizontal=\"Center\" ss:Vertical=\"Center\"/>");
            sb.AppendLine("   <Borders>");
            sb.AppendLine("    <Border ss:Position=\"Bottom\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#D0D0D0\"/>");
            sb.AppendLine("    <Border ss:Position=\"Left\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#D0D0D0\"/>");
            sb.AppendLine("    <Border ss:Position=\"Right\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#D0D0D0\"/>");
            sb.AppendLine("    <Border ss:Position=\"Top\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#D0D0D0\"/>");
            sb.AppendLine("   </Borders>");
            sb.AppendLine("   <Font ss:FontName=\"Segoe UI\" ss:Size=\"10\" ss:Bold=\"1\" ss:Color=\"#2980B9\"/>");
            sb.AppendLine("  </Style>");

            // Style: Data Left
            sb.AppendLine("  <Style ss:ID=\"DataLeft\">");
            sb.AppendLine("   <Alignment ss:Horizontal=\"Left\" ss:Vertical=\"Center\"/>");
            sb.AppendLine("   <Borders>");
            sb.AppendLine("    <Border ss:Position=\"Bottom\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#D0D0D0\"/>");
            sb.AppendLine("    <Border ss:Position=\"Left\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#D0D0D0\"/>");
            sb.AppendLine("    <Border ss:Position=\"Right\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#D0D0D0\"/>");
            sb.AppendLine("    <Border ss:Position=\"Top\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#D0D0D0\"/>");
            sb.AppendLine("   </Borders>");
            sb.AppendLine("   <Font ss:FontName=\"Segoe UI\" ss:Size=\"10\"/>");
            sb.AppendLine("  </Style>");

            sb.AppendLine(" </Styles>");

            // Worksheet
            sb.AppendLine(" <Worksheet ss:Name=\"Danh Mục Bản Vẽ\">");
            sb.AppendLine("  <Table>");
            sb.AppendLine("   <Column ss:Width=\"45\"/>");   // STT
            sb.AppendLine("   <Column ss:Width=\"130\"/>");  // Số hiệu bản vẽ
            sb.AppendLine("   <Column ss:Width=\"280\"/>");  // Tên bản vẽ
            sb.AppendLine("   <Column ss:Width=\"80\"/>");   // Phiên bản
            sb.AppendLine("   <Column ss:Width=\"110\"/>");  // Ngày phát hành
            sb.AppendLine("   <Column ss:Width=\"100\"/>");  // Người vẽ
            sb.AppendLine("   <Column ss:Width=\"85\"/>");   // Tỷ lệ

            // Title Row 1 (7 cột -> MergeAcross = 6)
            sb.AppendLine("   <Row ss:Height=\"30\">");
            sb.AppendLine(string.Format("    <Cell ss:MergeAcross=\"6\" ss:StyleID=\"TitleProject\"><Data ss:Type=\"String\">DANH MỤC HỒ SƠ BẢN VẼ: {0}</Data></Cell>",
                EscapeXml(projectName ?? "DỰ ÁN")));
            sb.AppendLine("   </Row>");

            // Subtitle Row 2
            sb.AppendLine("   <Row ss:Height=\"20\">");
            sb.AppendLine(string.Format("    <Cell ss:MergeAcross=\"6\" ss:StyleID=\"Subtitle\"><Data ss:Type=\"String\">Thời gian xuất: {0} | Tổng cộng: {1} bản vẽ</Data></Cell>",
                DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"), sheets.Count));
            sb.AppendLine("   </Row>");

            // Empty row
            sb.AppendLine("   <Row ss:Height=\"10\"></Row>");

            // Header Row (7 cột)
            sb.AppendLine("   <Row ss:Height=\"28\">");
            sb.AppendLine("    <Cell ss:StyleID=\"HeaderCol\"><Data ss:Type=\"String\">STT</Data></Cell>");
            sb.AppendLine("    <Cell ss:StyleID=\"HeaderCol\"><Data ss:Type=\"String\">SỐ HIỆU BẢN VẼ</Data></Cell>");
            sb.AppendLine("    <Cell ss:StyleID=\"HeaderCol\"><Data ss:Type=\"String\">TÊN BẢN VẼ</Data></Cell>");
            sb.AppendLine("    <Cell ss:StyleID=\"HeaderCol\"><Data ss:Type=\"String\">PHIÊN BẢN</Data></Cell>");
            sb.AppendLine("    <Cell ss:StyleID=\"HeaderCol\"><Data ss:Type=\"String\">NGÀY PHÁT HÀNH</Data></Cell>");
            sb.AppendLine("    <Cell ss:StyleID=\"HeaderCol\"><Data ss:Type=\"String\">NGƯỜI VẼ</Data></Cell>");
            sb.AppendLine("    <Cell ss:StyleID=\"HeaderCol\"><Data ss:Type=\"String\">TỶ LỆ</Data></Cell>");
            sb.AppendLine("   </Row>");

            // Data Rows
            int stt = 1;
            foreach (SheetItem item in sheets)
            {
                sb.AppendLine("   <Row ss:Height=\"22\">");
                sb.AppendLine(string.Format("    <Cell ss:StyleID=\"DataCenter\"><Data ss:Type=\"Number\">{0}</Data></Cell>", stt++));
                sb.AppendLine(string.Format("    <Cell ss:StyleID=\"DataCenterBold\"><Data ss:Type=\"String\">{0}</Data></Cell>", EscapeXml(item.SheetNumber)));
                sb.AppendLine(string.Format("    <Cell ss:StyleID=\"DataLeft\"><Data ss:Type=\"String\">{0}</Data></Cell>", EscapeXml(item.SheetName)));
                sb.AppendLine(string.Format("    <Cell ss:StyleID=\"DataCenter\"><Data ss:Type=\"String\">{0}</Data></Cell>", EscapeXml(item.Revision)));
                sb.AppendLine(string.Format("    <Cell ss:StyleID=\"DataCenter\"><Data ss:Type=\"String\">{0}</Data></Cell>", EscapeXml(item.IssueDate)));
                sb.AppendLine(string.Format("    <Cell ss:StyleID=\"DataCenter\"><Data ss:Type=\"String\">{0}</Data></Cell>", EscapeXml(item.DrawnBy)));
                sb.AppendLine(string.Format("    <Cell ss:StyleID=\"DataCenter\"><Data ss:Type=\"String\">{0}</Data></Cell>", EscapeXml(item.Scale)));
                sb.AppendLine("   </Row>");
            }

            sb.AppendLine("  </Table>");
            sb.AppendLine(" </Worksheet>");
            sb.AppendLine("</Workbook>");

            File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(true));
        }

        /// <summary>
        /// Sao chép danh mục bản vẽ vào Clipboard dạng văn bản phân tách bằng phím Tab (Dán ngay vào Excel, Word, AutoCAD)
        /// </summary>
        public static void ExportToClipboard(IList<SheetItem> sheets)
        {
            if (sheets == null || sheets.Count == 0) return;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("STT\tSố Hiệu Bản Vẽ\tTên Bản Vẽ\tPhiên Bản\tNgày Phát Hành\tNgười Vẽ\tTỷ Lệ");

            int stt = 1;
            foreach (SheetItem s in sheets)
            {
                sb.AppendLine(string.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}",
                    stt++,
                    s.SheetNumber,
                    s.SheetName,
                    s.Revision,
                    s.IssueDate,
                    s.DrawnBy,
                    s.Scale));
            }

            Clipboard.SetText(sb.ToString());
        }

        /// <summary>
        /// Tự động nhận diện Đầu Mục Lớn (BẢN VẼ ĐIỀU HÒA, BẢN VẼ ĐIỆN...) dựa vào tên hoặc số hiệu bản vẽ
        /// </summary>
        public static string DetectDisciplineGroupTitle(SheetItem sheet)
        {
            if (sheet == null) return string.Empty;
            string num = (sheet.SheetNumber ?? string.Empty).Trim().ToUpper();
            string name = (sheet.SheetName ?? string.Empty).Trim().ToUpper();

            // 1. Kiểm tra từ khóa trong tên bản vẽ
            if (name.Contains("ĐIỀU HÒA") || name.Contains("THÔNG GIÓ") || name.Contains("HVAC") || name.Contains("LÀM MÁT"))
            {
                return "BẢN VẼ ĐIỀU HÒA";
            }
            if (name.Contains("ĐIỆN") || name.Contains("CHIẾU SÁNG") || name.Contains("ĐỘNG LỰC") || name.Contains("CHỐNG SÉT") || name.Contains("ELECTRICAL"))
            {
                return "BẢN VẼ ĐIỆN";
            }
            if (name.Contains("CẤP THOÁT NƯỚC") || name.Contains("CẤP NƯỚC") || name.Contains("THOÁT NƯỚC") || name.Contains("PLUMBING"))
            {
                return "BẢN VẼ CẤP THOÁT NƯỚC";
            }
            if (name.Contains("PCCC") || name.Contains("CHỮA CHÁY") || name.Contains("BÁO CHÁY") || name.Contains("FIRE"))
            {
                return "BẢN VẼ PHÒNG CHÁY CHỮA CHÁY";
            }
            if (name.Contains("KIẾN TRÚC") || name.Contains("MẶT BẰNG TỔNG THỂ") || name.Contains("MẶT ĐỨNG") || name.Contains("MẶT CẮT"))
            {
                if (num.StartsWith("A") || num.StartsWith("KT")) return "BẢN VẼ KIẾN TRÚC";
            }
            if (name.Contains("KẾT CẤU") || name.Contains("MÓNG") || name.Contains("CỘT") || name.Contains("DẦM"))
            {
                return "BẢN VẼ KẾT CẤU";
            }

            // 2. Kiểm tra tiền tố số hiệu bản vẽ
            if (num.StartsWith("M-") || num.StartsWith("M") || num.StartsWith("ME-M"))
            {
                return "BẢN VẼ ĐIỀU HÒA";
            }
            if (num.StartsWith("E-") || num.StartsWith("E") || num.StartsWith("EL-") || num.StartsWith("ME-E"))
            {
                return "BẢN VẼ ĐIỆN";
            }
            if (num.StartsWith("P-") || num.StartsWith("P") || num.StartsWith("PL-") || num.StartsWith("ME-P"))
            {
                return "BẢN VẼ CẤP THOÁT NƯỚC";
            }
            if (num.StartsWith("FP-") || num.StartsWith("FP") || num.StartsWith("PC-") || num.StartsWith("PCCC"))
            {
                return "BẢN VẼ PHÒNG CHÁY CHỮA CHÁY";
            }
            if (num.StartsWith("A-") || num.StartsWith("A") || num.StartsWith("KT-") || num.StartsWith("AR-"))
            {
                return "BẢN VẼ KIẾN TRÚC";
            }
            if (num.StartsWith("S-") || num.StartsWith("S") || num.StartsWith("KC-") || num.StartsWith("ST-"))
            {
                return "BẢN VẼ KẾT CẤU";
            }

            return string.Empty;
        }

        private static string EscapeCsv(string field)
        {
            if (string.IsNullOrEmpty(field)) return "\"\"";
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }
            return "\"" + field + "\"";
        }

        private static string EscapeXml(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("&", "&amp;")
                       .Replace("<", "&lt;")
                       .Replace(">", "&gt;")
                       .Replace("\"", "&quot;")
                       .Replace("'", "&apos;");
        }
    }
}
