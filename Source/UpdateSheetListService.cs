using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using Autodesk.Revit.DB;

namespace HDTool
{
    /// <summary>
    /// Xử lý việc đọc danh sách bản vẽ từ Excel (.xls, .xlsx, .csv) hoặc Clipboard,
    /// so sánh với dự án Revit hiện tại và cập nhật lại Sheet Name / thông tin bản vẽ.
    /// </summary>
    public static class UpdateSheetListService
    {
        /// <summary>
        /// Đọc các hàng dữ liệu thô từ file Excel (.xls, .xlsx) hoặc CSV
        /// </summary>
        public static List<List<string>> ReadRowsFromFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                throw new FileNotFoundException("Không tìm thấy file nguồn: " + filePath);
            }

            string ext = Path.GetExtension(filePath).ToLowerInvariant();

            if (ext == ".xlsx")
            {
                return ParseXlsx(filePath);
            }
            else if (ext == ".csv")
            {
                return ParseCsv(filePath);
            }
            else if (ext == ".xls")
            {
                // Kiểm tra xem là SpreadsheetML (XML) hay CSV/Text
                if (IsXmlFile(filePath))
                {
                    return ParseSpreadsheetXml(filePath);
                }
                else
                {
                    return ParseCsv(filePath);
                }
            }
            else
            {
                // Mặc định thử đọc theo định dạng văn bản/CSV
                return ParseCsv(filePath);
            }
        }

        /// <summary>
        /// Đọc các hàng dữ liệu từ nội dung Clipboard (phân tách bởi dòng mới và tab/dấu phẩy)
        /// </summary>
        public static List<List<string>> ReadRowsFromClipboard(string clipboardText)
        {
            List<List<string>> rows = new List<List<string>>();
            if (string.IsNullOrEmpty(clipboardText)) return rows;

            string[] lines = clipboardText.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                char delimiter = line.Contains("\t") ? '\t' : (line.Contains(",") ? ',' : ';');
                List<string> row = ParseCsvLine(line, delimiter);
                if (row.Count > 0 && row.Any(delegate(string v) { return !string.IsNullOrEmpty(v); }))
                {
                    rows.Add(row);
                }
            }
            return rows;
        }

        private static bool IsXmlFile(string filePath)
        {
            try
            {
                using (StreamReader sr = new StreamReader(filePath, Encoding.UTF8))
                {
                    char[] buffer = new char[256];
                    int read = sr.Read(buffer, 0, buffer.Length);
                    string header = new string(buffer, 0, read).Trim();
                    return header.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) ||
                           header.StartsWith("<Workbook", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                return false;
            }
        }

        #region 1. Parser SpreadsheetML XML (.xls)
        private static List<List<string>> ParseSpreadsheetXml(string filePath)
        {
            List<List<string>> rows = new List<List<string>>();
            XmlDocument xdoc = new XmlDocument();
            xdoc.Load(filePath);

            XmlNodeList rowNodes = xdoc.GetElementsByTagName("Row");

            foreach (XmlNode rowNode in rowNodes)
            {
                List<string> rowValues = new List<string>();
                int colIndex = 0;

                foreach (XmlNode child in rowNode.ChildNodes)
                {
                    if (child.Name == "Cell" || child.LocalName == "Cell")
                    {
                        // Kiểm tra ss:Index nếu có ô trống bị nhảy cách
                        XmlAttribute attrIndex = child.Attributes["ss:Index"] ?? child.Attributes["Index"];
                        if (attrIndex != null)
                        {
                            int explicitIndex;
                            if (int.TryParse(attrIndex.Value, out explicitIndex) && explicitIndex > 0)
                            {
                                while (colIndex < explicitIndex - 1)
                                {
                                    rowValues.Add(string.Empty);
                                    colIndex++;
                                }
                            }
                        }

                        XmlNode dataNode = null;
                        foreach (XmlNode n in child.ChildNodes)
                        {
                            if (n.Name == "Data" || n.LocalName == "Data")
                            {
                                dataNode = n;
                                break;
                            }
                        }

                        string val = dataNode != null ? (dataNode.InnerText ?? string.Empty) : string.Empty;
                        rowValues.Add(val.Trim());
                        colIndex++;
                    }
                }

                if (rowValues.Any(delegate(string v) { return !string.IsNullOrEmpty(v); }))
                {
                    rows.Add(rowValues);
                }
            }

            return rows;
        }
        #endregion

        #region 2. Parser OpenXML (.xlsx)
        private static List<List<string>> ParseXlsx(string filePath)
        {
            List<List<string>> rows = new List<List<string>>();

            using (ZipArchive archive = ZipFile.OpenRead(filePath))
            {
                // Bước 1: Đọc bảng chuỗi chia sẻ (sharedStrings.xml)
                List<string> sharedStrings = new List<string>();
                ZipArchiveEntry sstEntry = archive.GetEntry("xl/sharedStrings.xml");
                if (sstEntry != null)
                {
                    using (Stream s = sstEntry.Open())
                    {
                        XmlDocument xdoc = new XmlDocument();
                        xdoc.Load(s);
                        XmlNodeList siNodes = xdoc.GetElementsByTagName("si");
                        foreach (XmlNode si in siNodes)
                        {
                            StringBuilder sb = new StringBuilder();
                            XmlNodeList tNodes = si.SelectNodes(".//t");
                            if (tNodes != null && tNodes.Count > 0)
                            {
                                foreach (XmlNode t in tNodes) sb.Append(t.InnerText);
                            }
                            else
                            {
                                foreach (XmlNode child in si.ChildNodes)
                                {
                                    if (child.LocalName == "t") sb.Append(child.InnerText);
                                    else if (child.LocalName == "r")
                                    {
                                        foreach (XmlNode rChild in child.ChildNodes)
                                        {
                                            if (rChild.LocalName == "t") sb.Append(rChild.InnerText);
                                        }
                                    }
                                }
                            }
                            sharedStrings.Add(sb.ToString());
                        }
                    }
                }

                // Bước 2: Đọc file sheet đầu tiên (sheet1.xml)
                ZipArchiveEntry sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml");
                if (sheetEntry == null)
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (entry.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) &&
                            entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                        {
                            sheetEntry = entry;
                            break;
                        }
                    }
                }

                if (sheetEntry != null)
                {
                    using (Stream s = sheetEntry.Open())
                    {
                        XmlDocument xdoc = new XmlDocument();
                        xdoc.Load(s);
                        XmlNodeList rowNodes = xdoc.GetElementsByTagName("row");
                        foreach (XmlNode rNode in rowNodes)
                        {
                            Dictionary<int, string> colDict = new Dictionary<int, string>();
                            int maxCol = -1;

                            foreach (XmlNode cNode in rNode.ChildNodes)
                            {
                                if (cNode.LocalName == "c")
                                {
                                    string cellRef = cNode.Attributes["r"] != null ? cNode.Attributes["r"].Value : string.Empty;
                                    int colIdx = ParseColumnIndexFromRef(cellRef);
                                    if (colIdx < 0) continue;
                                    if (colIdx > maxCol) maxCol = colIdx;

                                    string cellType = cNode.Attributes["t"] != null ? cNode.Attributes["t"].Value : string.Empty;
                                    string val = string.Empty;

                                    XmlNode valNode = null;
                                    XmlNode isNode = null;
                                    foreach (XmlNode cn in cNode.ChildNodes)
                                    {
                                        if (cn.LocalName == "v") valNode = cn;
                                        else if (cn.LocalName == "is") isNode = cn;
                                    }

                                    if (cellType == "s" && valNode != null)
                                    {
                                        int sstIdx;
                                        if (int.TryParse(valNode.InnerText, out sstIdx) && sstIdx >= 0 && sstIdx < sharedStrings.Count)
                                        {
                                            val = sharedStrings[sstIdx];
                                        }
                                    }
                                    else if (cellType == "inlineStr" && isNode != null)
                                    {
                                        val = isNode.InnerText ?? string.Empty;
                                    }
                                    else if (valNode != null)
                                    {
                                        val = valNode.InnerText ?? string.Empty;
                                    }

                                    colDict[colIdx] = val;
                                }
                            }

                            if (maxCol >= 0)
                            {
                                List<string> row = new List<string>();
                                for (int i = 0; i <= maxCol; i++)
                                {
                                    string cv;
                                    row.Add(colDict.TryGetValue(i, out cv) ? cv : string.Empty);
                                }
                                if (row.Any(delegate(string v) { return !string.IsNullOrEmpty(v); }))
                                {
                                    rows.Add(row);
                                }
                            }
                        }
                    }
                }
            }

            return rows;
        }

        private static int ParseColumnIndexFromRef(string cellRef)
        {
            if (string.IsNullOrEmpty(cellRef)) return -1;
            int index = 0;
            int letterCount = 0;
            for (int i = 0; i < cellRef.Length; i++)
            {
                char c = char.ToUpperInvariant(cellRef[i]);
                if (c >= 'A' && c <= 'Z')
                {
                    index = index * 26 + (c - 'A' + 1);
                    letterCount++;
                }
                else break;
            }
            return letterCount > 0 ? index - 1 : -1;
        }
        #endregion

        #region 3. Parser CSV / Text
        private static List<List<string>> ParseCsv(string filePath)
        {
            List<List<string>> rows = new List<List<string>>();
            string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);

            char delimiter = ',';
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line.Contains("\t")) delimiter = '\t';
                else if (line.Contains(";") && !line.Contains(",")) delimiter = ';';
                else delimiter = ',';
                break;
            }

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                List<string> row = ParseCsvLine(line, delimiter);
                if (row.Count > 0 && row.Any(delegate(string v) { return !string.IsNullOrEmpty(v); }))
                {
                    rows.Add(row);
                }
            }
            return rows;
        }

        private static List<string> ParseCsvLine(string line, char delimiter)
        {
            List<string> fields = new List<string>();
            if (line == null) return fields;

            StringBuilder cur = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '\"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '\"')
                    {
                        cur.Append('\"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == delimiter && !inQuotes)
                {
                    fields.Add(cur.ToString().Trim());
                    cur.Length = 0;
                }
                else
                {
                    cur.Append(c);
                }
            }
            fields.Add(cur.ToString().Trim());
            return fields;
        }
        #endregion

        #region 4. So Sánh và Ánh Xạ với Revit ViewSheets
        /// <summary>
        /// Phân tích bảng dữ liệu từ Excel/CSV và so khớp với toàn bộ Sheet trong Document
        /// </summary>
        public static List<SheetUpdateItem> MatchAndCompare(Document doc, List<List<string>> rawRows)
        {
            List<SheetUpdateItem> result = new List<SheetUpdateItem>();
            if (doc == null || rawRows == null || rawRows.Count == 0) return result;

            // Lấy toàn bộ ViewSheet trong Document và đưa vào từ điển theo SheetNumber (không phân biệt chữ hoa/thường)
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            IList<Element> sheetElements = collector.OfClass(typeof(ViewSheet))
                                                    .WhereElementIsNotElementType()
                                                    .ToElements();

            Dictionary<string, ViewSheet> sheetDict = new Dictionary<string, ViewSheet>(StringComparer.OrdinalIgnoreCase);
            foreach (Element elem in sheetElements)
            {
                ViewSheet vs = elem as ViewSheet;
                if (vs == null || vs.IsTemplate) continue;
                string num = (vs.SheetNumber ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(num) && !sheetDict.ContainsKey(num))
                {
                    sheetDict.Add(num, vs);
                }
            }

            // Tìm hàng tiêu đề (Header row)
            int numCol = -1;
            int nameCol = -1;
            int revCol = -1;
            int dateCol = -1;
            int drawnCol = -1;
            int headerRowIndex = -1;

            for (int r = 0; r < Math.Min(rawRows.Count, 15); r++)
            {
                List<string> row = rawRows[r];
                int cNum = -1;
                int cName = -1;

                for (int c = 0; c < row.Count; c++)
                {
                    string h = (row[c] ?? string.Empty).Trim().ToUpperInvariant();
                    if (h.Contains("SỐ HIỆU") || h.Contains("SO HIEU") || h.Contains("SHEET NUMBER") ||
                        h.Contains("SHEET NO") || h == "SỐ BẢN VẼ" || h == "MÃ BẢN VẼ" || h == "NUMBER")
                    {
                        cNum = c;
                    }
                    else if (h.Contains("TÊN BẢN VẼ") || h.Contains("TEN BAN VE") || h.Contains("SHEET NAME") ||
                             h == "TÊN" || h == "NAME" || h == "TÊN BẢN")
                    {
                        cName = c;
                    }
                    else if (h.Contains("PHIÊN BẢN") || h.Contains("REVISION") || h.Contains("REV"))
                    {
                        revCol = c;
                    }
                    else if (h.Contains("NGÀY") || h.Contains("DATE") || h.Contains("ISSUE DATE"))
                    {
                        dateCol = c;
                    }
                    else if (h.Contains("NGƯỜI VẼ") || h.Contains("DRAWN") || h.Contains("DRAWN BY"))
                    {
                        drawnCol = c;
                    }
                }

                if (cNum >= 0 && cName >= 0)
                {
                    numCol = cNum;
                    nameCol = cName;
                    headerRowIndex = r;
                    break;
                }
            }

            // Nếu không tìm thấy hàng tiêu đề rõ ràng, thử nhận diện cột chứa SheetNumber bằng cách so khớp dữ liệu thực tế
            if (numCol < 0 || nameCol < 0)
            {
                int bestCol = -1;
                int maxMatches = 0;

                for (int c = 0; c < 5; c++)
                {
                    int matches = 0;
                    for (int r = 0; r < rawRows.Count; r++)
                    {
                        if (rawRows[r].Count > c)
                        {
                            string val = rawRows[r][c].Trim();
                            if (sheetDict.ContainsKey(val)) matches++;
                        }
                    }
                    if (matches > maxMatches)
                    {
                        maxMatches = matches;
                        bestCol = c;
                    }
                }

                if (bestCol >= 0 && maxMatches > 0)
                {
                    numCol = bestCol;
                    nameCol = bestCol + 1;
                    headerRowIndex = -1; // bắt đầu duyệt từ hàng 0
                }
                else
                {
                    // Fallback mặc định: cột 1 là số hiệu, cột 2 là tên bản vẽ (bỏ qua cột 0 là STT)
                    numCol = 1;
                    nameCol = 2;
                    headerRowIndex = 0;
                }
            }

            // Duyệt từng hàng dữ liệu sau hàng header
            int startRow = headerRowIndex + 1;
            HashSet<string> processedNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int r = startRow; r < rawRows.Count; r++)
            {
                List<string> row = rawRows[r];
                string sheetNum = row.Count > numCol ? row[numCol].Trim() : string.Empty;
                string newName = row.Count > nameCol ? row[nameCol].Trim() : string.Empty;

                if (string.IsNullOrEmpty(sheetNum)) continue;
                if (processedNumbers.Contains(sheetNum)) continue; // Tránh trùng lặp
                processedNumbers.Add(sheetNum);

                ViewSheet vs;
                if (sheetDict.TryGetValue(sheetNum, out vs))
                {
                    SheetUpdateItem item = new SheetUpdateItem();
                    item.SheetId = vs.Id;
                    item.SheetNumber = vs.SheetNumber;
                    item.CurrentSheetName = vs.Name ?? string.Empty;
                    item.NewSheetName = newName;

                    // Lấy các tham số phụ nếu có trong file
                    Parameter pRev = vs.get_Parameter(BuiltInParameter.SHEET_CURRENT_REVISION);
                    item.CurrentRevision = (pRev != null && pRev.HasValue) ? (pRev.AsString() ?? string.Empty) : string.Empty;
                    item.NewRevision = (revCol >= 0 && row.Count > revCol) ? row[revCol].Trim() : item.CurrentRevision;

                    Parameter pDate = vs.get_Parameter(BuiltInParameter.SHEET_ISSUE_DATE);
                    item.CurrentIssueDate = (pDate != null && pDate.HasValue) ? (pDate.AsString() ?? string.Empty) : string.Empty;
                    item.NewIssueDate = (dateCol >= 0 && row.Count > dateCol) ? row[dateCol].Trim() : item.CurrentIssueDate;

                    Parameter pDrawn = vs.get_Parameter(BuiltInParameter.SHEET_DRAWN_BY);
                    item.CurrentDrawnBy = (pDrawn != null && pDrawn.HasValue) ? (pDrawn.AsString() ?? string.Empty) : string.Empty;
                    item.NewDrawnBy = (drawnCol >= 0 && row.Count > drawnCol) ? row[drawnCol].Trim() : item.CurrentDrawnBy;

                    bool nameChanged = !string.IsNullOrEmpty(newName) && !string.Equals(item.CurrentSheetName.Trim(), newName.Trim(), StringComparison.Ordinal);
                    bool revChanged = !string.IsNullOrEmpty(item.NewRevision) && !string.Equals(item.CurrentRevision, item.NewRevision, StringComparison.Ordinal);
                    bool dateChanged = !string.IsNullOrEmpty(item.NewIssueDate) && !string.Equals(item.CurrentIssueDate, item.NewIssueDate, StringComparison.Ordinal);
                    bool drawnChanged = !string.IsNullOrEmpty(item.NewDrawnBy) && !string.Equals(item.CurrentDrawnBy, item.NewDrawnBy, StringComparison.Ordinal);

                    if (nameChanged || revChanged || dateChanged || drawnChanged)
                    {
                        item.Status = SheetUpdateStatus.Changed;
                        item.IsSelected = true;

                        List<string> changes = new List<string>();
                        if (nameChanged) changes.Add("Đổi tên bản vẽ");
                        if (revChanged) changes.Add("Đổi Rev");
                        if (dateChanged) changes.Add("Đổi ngày");
                        if (drawnChanged) changes.Add("Đổi người vẽ");
                        item.Note = string.Join(", ", changes.ToArray());
                    }
                    else
                    {
                        item.Status = SheetUpdateStatus.Unchanged;
                        item.IsSelected = false;
                        item.Note = "Giữ nguyên (không đổi)";
                    }

                    result.Add(item);
                }
                else
                {
                    SheetUpdateItem item = new SheetUpdateItem();
                    item.SheetNumber = sheetNum;
                    item.CurrentSheetName = "(Không có)";
                    item.NewSheetName = newName;
                    item.Status = SheetUpdateStatus.NotFound;
                    item.IsSelected = false;
                    item.Note = "Không tìm thấy trong Revit";
                    result.Add(item);
                }
            }

            // Sắp xếp danh sách: Ưu tiên bản vẽ có thay đổi lên trước
            result.Sort(delegate(SheetUpdateItem a, SheetUpdateItem b)
            {
                if (a.Status == SheetUpdateStatus.Changed && b.Status != SheetUpdateStatus.Changed) return -1;
                if (a.Status != SheetUpdateStatus.Changed && b.Status == SheetUpdateStatus.Changed) return 1;
                return ExportSheetListService.NativeCompare(a.SheetNumber, b.SheetNumber);
            });

            return result;
        }
        #endregion

        #region 5. Áp Dụng Thay Đổi Vào Revit Document
        /// <summary>
        /// Thực thi cập nhật các ViewSheet trong Revit Document
        /// </summary>
        public static int ApplyUpdatesToDocument(Document doc, IList<SheetUpdateItem> itemsToApply, out string report)
        {
            report = string.Empty;
            if (doc == null || itemsToApply == null || itemsToApply.Count == 0) return 0;

            int successCount = 0;
            List<string> updatedDetails = new List<string>();
            List<string> errorDetails = new List<string>();

            using (Transaction trans = new Transaction(doc, "HDTOOL - Cập Nhật Danh Mục Bản Vẽ Từ Excel"))
            {
                trans.Start();

                foreach (SheetUpdateItem item in itemsToApply)
                {
                    if (!item.IsSelected || item.Status != SheetUpdateStatus.Changed) continue;
                    if (item.SheetId == null || item.SheetId == ElementId.InvalidElementId) continue;

                    try
                    {
                        Element elem = doc.GetElement(item.SheetId);
                        ViewSheet vs = elem as ViewSheet;
                        if (vs == null) continue;

                        bool changed = false;

                        // 1. Cập nhật Sheet Name
                        if (!string.IsNullOrEmpty(item.NewSheetName) &&
                            !string.Equals(vs.Name, item.NewSheetName, StringComparison.Ordinal))
                        {
                            string oldName = vs.Name;
                            string sanitizedName = SanitizeSheetName(item.NewSheetName);
                            vs.Name = sanitizedName;
                            updatedDetails.Add(string.Format("• [{0}]: \"{1}\" ➔ \"{2}\"", vs.SheetNumber, oldName, sanitizedName));
                            changed = true;
                        }

                        // 2. Cập nhật Revision
                        if (!string.IsNullOrEmpty(item.NewRevision))
                        {
                            Parameter p = vs.get_Parameter(BuiltInParameter.SHEET_CURRENT_REVISION);
                            if (p != null && !p.IsReadOnly && p.AsString() != item.NewRevision)
                            {
                                p.Set(item.NewRevision);
                                changed = true;
                            }
                        }

                        // 3. Cập nhật Issue Date
                        if (!string.IsNullOrEmpty(item.NewIssueDate))
                        {
                            Parameter p = vs.get_Parameter(BuiltInParameter.SHEET_ISSUE_DATE);
                            if (p != null && !p.IsReadOnly && p.AsString() != item.NewIssueDate)
                            {
                                p.Set(item.NewIssueDate);
                                changed = true;
                            }
                        }

                        // 4. Cập nhật Drawn By
                        if (!string.IsNullOrEmpty(item.NewDrawnBy))
                        {
                            Parameter p = vs.get_Parameter(BuiltInParameter.SHEET_DRAWN_BY);
                            if (p != null && !p.IsReadOnly && p.AsString() != item.NewDrawnBy)
                            {
                                p.Set(item.NewDrawnBy);
                                changed = true;
                            }
                        }

                        if (changed) successCount++;
                    }
                    catch (Exception ex)
                    {
                        errorDetails.Add(string.Format("• [{0}]: Lỗi ({1})", item.SheetNumber, ex.Message));
                    }
                }

                trans.Commit();
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(string.Format("Đã cập nhật thành công {0} bản vẽ trong Revit.", successCount));
            if (updatedDetails.Count > 0)
            {
                sb.AppendLine("\nChi tiết thay đổi:");
                foreach (string d in updatedDetails.Take(25)) sb.AppendLine(d);
                if (updatedDetails.Count > 25)
                {
                    sb.AppendLine(string.Format("... và {0} bản vẽ khác.", updatedDetails.Count - 25));
                }
            }
            if (errorDetails.Count > 0)
            {
                sb.AppendLine("\nCác bản vẽ gặp lỗi:");
                foreach (string err in errorDetails) sb.AppendLine(err);
            }

            report = sb.ToString();
            return successCount;
        }

        public static string SanitizeSheetName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unnamed";
            // Ký tự cấm trong tên view/sheet Revit: \ : { } [ ] | ; < > ? ` ~
            char[] invalidChars = new char[] { '\\', ':', '{', '}', '[', ']', '|', ';', '<', '>', '?', '`', '~' };
            string result = name;
            foreach (char c in invalidChars)
            {
                result = result.Replace(c, '-');
            }
            return result.Trim();
        }
        #endregion
    }
}
