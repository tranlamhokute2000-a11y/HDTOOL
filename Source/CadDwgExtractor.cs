using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using Microsoft.Win32;

namespace HDTool
{
    /// <summary>
    /// Bộ trích xuất Text kích thước (WxH) và Block van gió (VCD/MFD/NRD) từ file DWG gốc của CAD Link
    /// Tự động xử lý đường dẫn tuyệt đối/tương đối/Import CAD, chuyển OCS->WCS, tâm Text/MText/Attrib/Block,
    /// đơn vị $INSUNITS, và loại trừ ghi chú miệng gió (SAG/EAG/PAG...) / tiêu âm (ATT).
    /// </summary>
    public static class CadDwgExtractor
    {
        private const double MM_TO_FEET = 1.0 / 304.8;
        private const double FEET_TO_MM = 304.8;
        private const int ACCORE_TIMEOUT_MS = 60000;

        private static string _lastDiagnostic = string.Empty;

        /// <summary>
        /// Thông tin chẩn đoán lần trích xuất DWG gần nhất (đường dẫn, accoreconsole, số lượng text, lỗi nếu có)
        /// </summary>
        public static string LastDiagnostic
        {
            get { return _lastDiagnostic; }
        }

        private static readonly Regex DuctSizeRegex = new Regex(@"(\d{2,4})\s*[xX\*×]\s*(\d{2,4})", RegexOptions.Compiled);
        private static readonly Regex GrilleIgnoreRegex = new Regex(
            @"\b(SAG|EAG|RAG|PAG|OAG|FAG|KAG|GRILLE|DIFFUSER|LOUVER|REGISTER|OBD)\b|\b\d+\s*Nos\b|\bc\s*/\s*w\s*OBD\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex AttenuatorIgnoreRegex = new Regex(
            @"\bATT\b|\d{2,4}\s*[xX\*×]\s*\d{2,4}\s*[xX\*×]\s*\d{2,4}",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Lấy đường dẫn file DWG từ ImportInstance trong Revit (hỗ trợ Absolute, Relative, ExternalResource, Import CAD và fallback tìm quanh dự án)
        /// </summary>
        public static string GetDwgFilePath(Document doc, ImportInstance cadLink)
        {
            if (doc == null || cadLink == null) return null;

            string docDir = null;
            try
            {
                if (!string.IsNullOrEmpty(doc.PathName))
                {
                    docDir = Path.GetDirectoryName(doc.PathName);
                }
            }
            catch { }

            List<string> rawCandidates = new List<string>();
            List<string> fileNames = new List<string>();

            ElementId typeId = cadLink.GetTypeId();
            CADLinkType linkType = null;
            if (typeId != ElementId.InvalidElementId)
            {
                try { linkType = doc.GetElement(typeId) as CADLinkType; } catch { }
            }

            // 1. Thử lấy từ ExternalFileReference (chỉ gọi khi thực sự là ExternalFileReference, tránh ném ngoại lệ với Import CAD)
            if (typeId != ElementId.InvalidElementId)
            {
                try
                {
                    if (ExternalFileUtils.IsExternalFileReference(doc, typeId))
                    {
                        ExternalFileReference extRef = ExternalFileUtils.GetExternalFileReference(doc, typeId);
                        if (extRef != null)
                        {
                            try
                            {
                                ModelPath absMp = extRef.GetAbsolutePath();
                                if (absMp != null)
                                {
                                    string absPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(absMp);
                                    AddPathCandidate(absPath, docDir, rawCandidates, fileNames);
                                }
                            }
                            catch { }

                            try
                            {
                                ModelPath relMp = extRef.GetPath();
                                if (relMp != null)
                                {
                                    string relPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(relMp);
                                    AddPathCandidate(relPath, docDir, rawCandidates, fileNames);
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }

                // 2. Thử ExternalResourceReference (Autodesk Docs / Desktop Connector / Cloud Link)
                if (linkType != null)
                {
                    try
                    {
                        if (linkType.RefersToExternalResourceReferences())
                        {
                            IDictionary<ExternalResourceType, ExternalResourceReference> refs =
                                linkType.GetExternalResourceReferences();
                            if (refs != null)
                            {
                                foreach (KeyValuePair<ExternalResourceType, ExternalResourceReference> kv in refs)
                                {
                                    if (kv.Value != null)
                                    {
                                        AddPathCandidate(kv.Value.InSessionPath, docDir, rawCandidates, fileNames);
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }
            }

            // 3. Lấy tên file ứng viên từ tham số và tên Type / Category (cần thiết cho Import CAD hoặc file đã chuyển thư mục)
            try
            {
                Parameter pName = cadLink.get_Parameter(BuiltInParameter.IMPORT_SYMBOL_NAME);
                if (pName != null)
                {
                    AddPathCandidate(pName.AsString(), docDir, rawCandidates, fileNames);
                }
            }
            catch { }

            if (linkType != null)
            {
                try { AddPathCandidate(linkType.Name, docDir, rawCandidates, fileNames); } catch { }
                try
                {
                    Parameter pTypeName = linkType.get_Parameter(BuiltInParameter.SYMBOL_NAME_PARAM);
                    if (pTypeName != null) AddPathCandidate(pTypeName.AsString(), docDir, rawCandidates, fileNames);
                }
                catch { }
            }

            if (cadLink.Category != null && !string.IsNullOrEmpty(cadLink.Category.Name))
            {
                AddPathCandidate(cadLink.Category.Name, docDir, rawCandidates, fileNames);
            }

            // Kiểm tra trực tiếp các đường dẫn đã có
            for (int i = 0; i < rawCandidates.Count; i++)
            {
                string cand = rawCandidates[i];
                try
                {
                    if (!string.IsNullOrEmpty(cand) && File.Exists(cand))
                    {
                        return Path.GetFullPath(cand);
                    }
                }
                catch { }
            }

            if (fileNames.Count == 0) return null;

            // 4. Fallback: Tìm theo tên file trong thư mục chứa .rvt (cùng cấp, thư mục con sâu <= 3, thư mục cha sâu <= 2)
            if (!string.IsNullOrEmpty(docDir) && Directory.Exists(docDir))
            {
                string foundNearDoc = FindDwgInFolderTree(docDir, fileNames, 3, 250, 150);
                if (!string.IsNullOrEmpty(foundNearDoc)) return foundNearDoc;

                try
                {
                    DirectoryInfo parent = Directory.GetParent(docDir);
                    if (parent != null && parent.Exists)
                    {
                        string foundInParent = FindDwgInFolderTree(parent.FullName, fileNames, 2, 180, 120);
                        if (!string.IsNullOrEmpty(foundInParent)) return foundInParent;
                    }
                }
                catch { }
            }

            // 5. Fallback: Kiểm tra thư mục của các CADLinkType khác trong cùng Document
            try
            {
                FilteredElementCollector col = new FilteredElementCollector(doc).OfClass(typeof(CADLinkType));
                foreach (Element el in col)
                {
                    if (el.Id == typeId) continue;
                    if (ExternalFileUtils.IsExternalFileReference(doc, el.Id))
                    {
                        ExternalFileReference otherRef = ExternalFileUtils.GetExternalFileReference(doc, el.Id);
                        if (otherRef != null)
                        {
                            ModelPath mp = otherRef.GetAbsolutePath() ?? otherRef.GetPath();
                            if (mp != null)
                            {
                                string otherPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(mp);
                                if (!string.IsNullOrEmpty(otherPath))
                                {
                                    string otherDir = Path.GetDirectoryName(otherPath);
                                    if (!string.IsNullOrEmpty(otherDir) && Directory.Exists(otherDir))
                                    {
                                        string foundSibling = FindDwgInFolderTree(otherDir, fileNames, 2, 100, 80);
                                        if (!string.IsNullOrEmpty(foundSibling)) return foundSibling;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            // 6. Fallback cuối khi dự án chưa lưu (doc.PathName rỗng) hoặc Import CAD: quét nhanh Desktop / Documents (sâu <= 2, giới hạn 150ms)
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (!string.IsNullOrEmpty(desktop) && Directory.Exists(desktop))
                {
                    string foundDesktop = FindDwgInFolderTree(desktop, fileNames, 2, 160, 150);
                    if (!string.IsNullOrEmpty(foundDesktop)) return foundDesktop;
                }

                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (!string.IsNullOrEmpty(docs) && Directory.Exists(docs))
                {
                    string foundDocs = FindDwgInFolderTree(docs, fileNames, 2, 120, 120);
                    if (!string.IsNullOrEmpty(foundDocs)) return foundDocs;
                }
            }
            catch { }

            return null;
        }

        private static void AddPathCandidate(string raw, string docDir, List<string> rawCandidates, List<string> fileNames)
        {
            if (string.IsNullOrEmpty(raw)) return;
            string trimmed = raw.Trim();
            if (trimmed.Length == 0) return;

            if (Path.IsPathRooted(trimmed))
            {
                if (!rawCandidates.Contains(trimmed)) rawCandidates.Add(trimmed);
            }
            else if (!string.IsNullOrEmpty(docDir))
            {
                try
                {
                    string combined = Path.GetFullPath(Path.Combine(docDir, trimmed));
                    if (!rawCandidates.Contains(combined)) rawCandidates.Add(combined);
                }
                catch { }
            }

            // Trích tên file .dwg (loại bỏ hậu tố "(1)", "(2)" do Revit thêm khi import trùng tên)
            try
            {
                string fname = Path.GetFileName(trimmed);
                if (!string.IsNullOrEmpty(fname))
                {
                    fname = Regex.Replace(fname, @"\s*\(\d+\)\s*$", string.Empty);
                    if (fname.IndexOf(".dwg", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        int dwgIdx = fname.IndexOf(".dwg", StringComparison.OrdinalIgnoreCase);
                        fname = fname.Substring(0, dwgIdx + 4);
                    }
                    else if (fname.IndexOf('.') < 0)
                    {
                        fname = fname + ".dwg";
                    }

                    if (!string.IsNullOrEmpty(fname) && fname.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!fileNames.Contains(fname)) fileNames.Add(fname);
                    }
                }
            }
            catch { }
        }

        private static string FindDwgInFolderTree(string rootDir, List<string> fileNames, int maxDepth, int maxDirs, int maxMs)
        {
            if (string.IsNullOrEmpty(rootDir) || !Directory.Exists(rootDir) || fileNames == null || fileNames.Count == 0)
                return null;

            Stopwatch sw = Stopwatch.StartNew();
            Queue<KeyValuePair<string, int>> queue = new Queue<KeyValuePair<string, int>>();
            queue.Enqueue(new KeyValuePair<string, int>(rootDir, 0));
            int visitedDirs = 0;

            while (queue.Count > 0 && visitedDirs < maxDirs && sw.ElapsedMilliseconds < maxMs)
            {
                KeyValuePair<string, int> item = queue.Dequeue();
                string dir = item.Key;
                int depth = item.Value;
                visitedDirs++;

                for (int i = 0; i < fileNames.Count; i++)
                {
                    string candidate = Path.Combine(dir, fileNames[i]);
                    try
                    {
                        if (File.Exists(candidate)) return Path.GetFullPath(candidate);
                    }
                    catch { }
                }

                if (depth >= maxDepth) continue;

                try
                {
                    string[] subDirs = Directory.GetDirectories(dir);
                    for (int i = 0; i < subDirs.Length; i++)
                    {
                        string sub = subDirs[i];
                        string folderName = Path.GetFileName(sub);
                        if (string.IsNullOrEmpty(folderName)) continue;
                        if (folderName.StartsWith(".") ||
                            string.Equals(folderName, "node_modules", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(folderName, "bin", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(folderName, "obj", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(folderName, "Build", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(folderName, "Installer", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(folderName, "Windows", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(folderName, "AppData", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                        queue.Enqueue(new KeyValuePair<string, int>(sub, depth + 1));
                    }
                }
                catch { }
            }

            return null;
        }

        /// <summary>
        /// Tìm đường dẫn accoreconsole.exe có sẵn trên máy (ưu tiên AutoCAD chính thức trước DWG TrueView)
        /// </summary>
        public static string FindAcCoreConsolePath()
        {
            string[] candidates = new string[]
            {
                @"C:\Program Files\Autodesk\AutoCAD 2027\accoreconsole.exe",
                @"C:\Program Files\Autodesk\AutoCAD 2026\accoreconsole.exe",
                @"C:\Program Files\Autodesk\AutoCAD 2025\accoreconsole.exe",
                @"C:\Program Files\Autodesk\AutoCAD 2024\accoreconsole.exe",
                @"C:\Program Files\Autodesk\AutoCAD 2023\accoreconsole.exe",
                @"C:\Program Files\Autodesk\AutoCAD 2022\accoreconsole.exe",
                @"C:\Program Files\Autodesk\AutoCAD 2021\accoreconsole.exe",
                @"C:\Program Files\Autodesk\DWG TrueView 2027 - English\accoreconsole.exe",
                @"C:\Program Files\Autodesk\DWG TrueView 2026 - English\accoreconsole.exe",
                @"C:\Program Files\Autodesk\DWG TrueView 2025 - English\accoreconsole.exe",
                @"C:\Program Files\Autodesk\DWG TrueView 2024 - English\accoreconsole.exe"
            };

            foreach (string p in candidates)
            {
                if (File.Exists(p)) return p;
            }

            // Tra cứu Registry nếu AutoCAD cài ở ổ đĩa / thư mục khác
            try
            {
                using (RegistryKey baseKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Autodesk\AutoCAD"))
                {
                    if (baseKey != null)
                    {
                        string[] relNames = baseKey.GetSubKeyNames();
                        Array.Sort(relNames);
                        Array.Reverse(relNames);
                        foreach (string rel in relNames)
                        {
                            using (RegistryKey relKey = baseKey.OpenSubKey(rel))
                            {
                                if (relKey == null) continue;
                                foreach (string prod in relKey.GetSubKeyNames())
                                {
                                    using (RegistryKey prodKey = relKey.OpenSubKey(prod))
                                    {
                                        if (prodKey == null) continue;
                                        object loc = prodKey.GetValue("AcadLocation");
                                        if (loc != null)
                                        {
                                            string exe = Path.Combine(loc.ToString(), "accoreconsole.exe");
                                            if (File.Exists(exe)) return exe;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Trích xuất toàn bộ Text kích thước WxH và Block van gió từ file DWG (giữ nguyên chữ ký public tương thích)
        /// </summary>
        public static void ExtractTextAndDampersFromDwg(
            Document doc,
            ImportInstance cadLink,
            out List<CadTextNote> outTexts,
            out List<CadDamperItem> outDampers)
        {
            List<CadExclusionZone> exclusions;
            string diag;
            ExtractTextAndDampersFromDwg(doc, cadLink, out outTexts, out outDampers, out exclusions, out diag);
        }

        /// <summary>
        /// Trích xuất Text kích thước WxH và Block van gió từ file DWG kèm thông tin chẩn đoán chi tiết
        /// </summary>
        public static void ExtractTextAndDampersFromDwg(
            Document doc,
            ImportInstance cadLink,
            out List<CadTextNote> outTexts,
            out List<CadDamperItem> outDampers,
            out string diagnostic)
        {
            List<CadExclusionZone> exclusions;
            ExtractTextAndDampersFromDwg(doc, cadLink, out outTexts, out outDampers, out exclusions, out diagnostic);
        }

        /// <summary>
        /// Trích xuất Text WxH, Van gió và các Vùng loại trừ (Grille, RiserDrop, IAQ AnnotationBox, Damper, Attenuator, Fan, Hatch) từ DWG
        /// </summary>
        public static void ExtractTextAndDampersFromDwg(
            Document doc,
            ImportInstance cadLink,
            out List<CadTextNote> outTexts,
            out List<CadDamperItem> outDampers,
            out List<CadExclusionZone> outExclusions,
            out string diagnostic)
        {
            outTexts = new List<CadTextNote>();
            outDampers = new List<CadDamperItem>();
            outExclusions = new List<CadExclusionZone>();
            diagnostic = string.Empty;
            _lastDiagnostic = string.Empty;

            if (doc == null || cadLink == null)
            {
                diagnostic = "Không có Document hoặc CAD Link hợp lệ.";
                _lastDiagnostic = diagnostic;
                return;
            }

            string dwgPath = GetDwgFilePath(doc, cadLink);
            if (string.IsNullOrEmpty(dwgPath) || !File.Exists(dwgPath))
            {
                string cadTitle = string.Empty;
                try
                {
                    Parameter pName = cadLink.get_Parameter(BuiltInParameter.IMPORT_SYMBOL_NAME);
                    if (pName != null) cadTitle = pName.AsString();
                    if (string.IsNullOrEmpty(cadTitle) && cadLink.Category != null) cadTitle = cadLink.Category.Name;
                }
                catch { }
                diagnostic = string.Format("Không tìm thấy file DWG gốc '{0}' trên ổ đĩa (có thể CAD được Import hoặc đã chuyển thư mục).", cadTitle ?? "");
                _lastDiagnostic = diagnostic;
                return;
            }

            string cacheFile = GetCacheFilePath(dwgPath);
            string extractStatus = "CacheHit";
            if (!File.Exists(cacheFile) || new FileInfo(cacheFile).Length == 0)
            {
                string accore = FindAcCoreConsolePath();
                if (string.IsNullOrEmpty(accore))
                {
                    diagnostic = "Không tìm thấy AutoCAD accoreconsole.exe để đọc Text từ DWG.";
                    _lastDiagnostic = diagnostic;
                    return;
                }

                extractStatus = RunAcCoreExtraction(accore, dwgPath, cacheFile);
            }

            if (!File.Exists(cacheFile) || new FileInfo(cacheFile).Length == 0)
            {
                diagnostic = string.Format("Trích xuất DWG không tạo được dữ liệu ({0}) cho file '{1}'.", extractStatus, Path.GetFileName(dwgPath));
                _lastDiagnostic = diagnostic;
                return;
            }

            Transform totalXform = GetSymbolToRevitTransform(cadLink);

            List<CadTextNote> rawTexts = new List<CadTextNote>();
            List<CadDamperItem> rawDampers = new List<CadDamperItem>();
            double minCadX = double.MaxValue, minCadY = double.MaxValue;
            double maxCadX = double.MinValue, maxCadY = double.MinValue;
            bool hasExtents = false;
            int insUnits = 4; // Mặc định Millimeters
            int totalRawTextLines = 0;
            int filteredGrilleCount = 0;
            int filteredAttCount = 0;

            try
            {
                string[] lines = File.ReadAllLines(cacheFile, Encoding.UTF8);
                List<RawTextEntry> parsedTextEntries = new List<RawTextEntry>();
                List<RawAnyTextEntry> parsedAnyTexts = new List<RawAnyTextEntry>();
                List<RawLeaderEntry> parsedLeaders = new List<RawLeaderEntry>();
                List<RawDamperEntry> parsedDamperEntries = new List<RawDamperEntry>();
                Dictionary<string, RawBlockDef> blockDefs = new Dictionary<string, RawBlockDef>(StringComparer.OrdinalIgnoreCase);
                List<RawBlkBoxEntry> parsedBlkBoxes = new List<RawBlkBoxEntry>();
                List<RawHatchEntry> parsedHatches = new List<RawHatchEntry>();

                foreach (string line in lines)
                {
                    if (string.IsNullOrEmpty(line)) continue;
                    string[] parts = line.Split('|');
                    string kind = parts[0];

                    if (kind == "VARS" && parts.Length >= 3)
                    {
                        if (parts[1] == "INSUNITS")
                        {
                            int parsedUnits;
                            if (int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedUnits))
                            {
                                insUnits = parsedUnits;
                            }
                        }
                        continue;
                    }
                    if (kind == "EXT" && parts.Length >= 5)
                    {
                        double ex1, ey1, ex2, ey2;
                        if (double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out ex1) &&
                            double.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out ey1) &&
                            double.TryParse(parts[3], NumberStyles.Any, CultureInfo.InvariantCulture, out ex2) &&
                            double.TryParse(parts[4], NumberStyles.Any, CultureInfo.InvariantCulture, out ey2))
                        {
                            minCadX = ex1; minCadY = ey1; maxCadX = ex2; maxCadY = ey2;
                            hasExtents = (maxCadX > minCadX && maxCadY > minCadY);
                        }
                        continue;
                    }
                    if (kind == "BDEF" && parts.Length >= 16)
                    {
                        RawBlockDef bdef = ParseBlockDefLine(parts);
                        if (bdef != null && !string.IsNullOrEmpty(bdef.Name))
                        {
                            blockDefs[bdef.Name] = bdef;
                        }
                        continue;
                    }
                    if (kind == "BLKX" && parts.Length >= 4)
                    {
                        // BLKX|layer|bname|x1,y1;x2,y2;x3,y3;x4,y4|attribText
                        RawBlkBoxEntry be = new RawBlkBoxEntry();
                        be.Layer = parts[1];
                        be.BlockName = parts[2];
                        be.CornersCad = ParsePointListCad(parts[3]);
                        be.AttribText = (parts.Length >= 5) ? parts[4] : string.Empty;
                        if (be.CornersCad.Count >= 3)
                        {
                            parsedBlkBoxes.Add(be);
                        }
                        continue;
                    }
                    if (kind == "HTX" && parts.Length >= 4)
                    {
                        // HTX|layer|pattern|x1,y1;x2,y2;...
                        RawHatchEntry he = new RawHatchEntry();
                        he.Layer = parts[1];
                        he.Pattern = parts[2];
                        he.PolygonCad = ParsePointListCad(parts[3]);
                        if (he.PolygonCad.Count >= 3)
                        {
                            parsedHatches.Add(he);
                        }
                        continue;
                    }

                    if (kind == "LDR" && parts.Length >= 6)
                    {
                        double ax, ay, tx, ty;
                        if (double.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out ax) &&
                            double.TryParse(parts[3], NumberStyles.Any, CultureInfo.InvariantCulture, out ay) &&
                            double.TryParse(parts[4], NumberStyles.Any, CultureInfo.InvariantCulture, out tx) &&
                            double.TryParse(parts[5], NumberStyles.Any, CultureInfo.InvariantCulture, out ty))
                        {
                            RawLeaderEntry le = new RawLeaderEntry();
                            le.Layer = parts[1];
                            le.ArrowX = ax;
                            le.ArrowY = ay;
                            le.TailX = tx;
                            le.TailY = ty;
                            le.AnnoHandle = (parts.Length >= 7) ? (parts[6] ?? string.Empty).Trim() : string.Empty;
                            parsedLeaders.Add(le);
                        }
                        continue;
                    }

                    if (parts.Length < 5) continue;

                    string layer = parts[1];
                    string content = parts[2];
                    double xCad, yCad;

                    if (!double.TryParse(parts[3], NumberStyles.Any, CultureInfo.InvariantCulture, out xCad) ||
                        !double.TryParse(parts[4], NumberStyles.Any, CultureInfo.InvariantCulture, out yCad))
                    {
                        continue;
                    }

                    if (kind == "TXT")
                    {
                        totalRawTextLines++;
                        string trimmedContent = (content ?? string.Empty).Trim();
                        if (string.IsNullOrEmpty(trimmedContent)) continue;

                        double tMinX = xCad, tMinY = yCad, tMaxX = xCad, tMaxY = yCad;
                        if (parts.Length >= 9)
                        {
                            double pMinX, pMinY, pMaxX, pMaxY;
                            if (double.TryParse(parts[5], NumberStyles.Any, CultureInfo.InvariantCulture, out pMinX) &&
                                double.TryParse(parts[6], NumberStyles.Any, CultureInfo.InvariantCulture, out pMinY) &&
                                double.TryParse(parts[7], NumberStyles.Any, CultureInfo.InvariantCulture, out pMaxX) &&
                                double.TryParse(parts[8], NumberStyles.Any, CultureInfo.InvariantCulture, out pMaxY))
                            {
                                tMinX = Math.Min(pMinX, pMaxX);
                                tMaxX = Math.Max(pMinX, pMaxX);
                                tMinY = Math.Min(pMinY, pMaxY);
                                tMaxY = Math.Max(pMinY, pMaxY);
                            }
                        }
                        string txtHandle = (parts.Length >= 10) ? (parts[9] ?? string.Empty).Trim() : string.Empty;

                        RawAnyTextEntry anyTxt = new RawAnyTextEntry();
                        anyTxt.X = xCad;
                        anyTxt.Y = yCad;
                        anyTxt.MinX = tMinX;
                        anyTxt.MinY = tMinY;
                        anyTxt.MaxX = tMaxX;
                        anyTxt.MaxY = tMaxY;
                        anyTxt.Handle = txtHandle;
                        anyTxt.Layer = layer;
                        anyTxt.TextEntryIndex = -1;

                        if (GrilleIgnoreRegex.IsMatch(trimmedContent))
                        {
                            filteredGrilleCount++;
                            parsedAnyTexts.Add(anyTxt);
                            continue;
                        }
                        if (AttenuatorIgnoreRegex.IsMatch(trimmedContent))
                        {
                            filteredAttCount++;
                            parsedAnyTexts.Add(anyTxt);
                            continue;
                        }

                        Match m = DuctSizeRegex.Match(trimmedContent);
                        if (m.Success)
                        {
                            double w = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                            double h = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                            if (w >= 50.0 && w <= 6000.0 && h >= 50.0 && h <= 6000.0)
                            {
                                RawTextEntry entry = new RawTextEntry();
                                entry.Content = trimmedContent;
                                entry.WidthMm = w;
                                entry.HeightMm = h;
                                entry.X = xCad;
                                entry.Y = yCad;
                                entry.MinX = tMinX;
                                entry.MinY = tMinY;
                                entry.MaxX = tMaxX;
                                entry.MaxY = tMaxY;
                                entry.Handle = txtHandle;
                                entry.Layer = layer;
                                anyTxt.TextEntryIndex = parsedTextEntries.Count;
                                parsedTextEntries.Add(entry);
                            }
                        }
                        parsedAnyTexts.Add(anyTxt);
                    }
                    else if (kind == "BLK")
                    {
                        string upper = (content ?? string.Empty).ToUpperInvariant();
                        string vanType = null;
                        if (upper.Contains("VCD")) vanType = "VCD";
                        else if (upper.Contains("MFD")) vanType = "MFD";
                        else if (upper.Contains("NRD")) vanType = "NRD";

                        if (vanType != null)
                        {
                            RawDamperEntry de = new RawDamperEntry();
                            de.TypeName = vanType;
                            de.X = xCad;
                            de.Y = yCad;
                            parsedDamperEntries.Add(de);
                        }
                    }
                }

                double unitToMm = ResolveCadUnitToMmFactor(cadLink, insUnits, hasExtents, minCadX, minCadY, maxCadX, maxCadY);

                double minMmX = minCadX * unitToMm;
                double minMmY = minCadY * unitToMm;
                double maxMmX = maxCadX * unitToMm;
                double maxMmY = maxCadY * unitToMm;

                for (int i = 0; i < parsedTextEntries.Count; i++)
                {
                    RawTextEntry e = parsedTextEntries[i];
                    XYZ ptFeet = new XYZ(e.X * unitToMm * MM_TO_FEET, e.Y * unitToMm * MM_TO_FEET, 0.0);
                    rawTexts.Add(new CadTextNote(e.Content, e.WidthMm, e.HeightMm, ptFeet, e.Layer));
                }

                // Liên kết mũi tên QLEADER / MULTILEADER với Text kích thước tại đuôi leader (tránh hút nhầm leader của ATT:/Grille/Van)
                if (parsedLeaders.Count > 0 && parsedTextEntries.Count > 0)
                {
                    double maxTailDistCad = 650.0 / Math.Max(1e-6, unitToMm);
                    Dictionary<string, int> handleToTextIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < parsedTextEntries.Count; i++)
                    {
                        if (!string.IsNullOrEmpty(parsedTextEntries[i].Handle) && !handleToTextIdx.ContainsKey(parsedTextEntries[i].Handle))
                        {
                            handleToTextIdx[parsedTextEntries[i].Handle] = i;
                        }
                    }

                    bool[] primaryLeaderAssigned = new bool[parsedTextEntries.Count];
                    for (int li = 0; li < parsedLeaders.Count; li++)
                    {
                        RawLeaderEntry le = parsedLeaders[li];
                        int targetTxtIdx = -1;

                        if (!string.IsNullOrEmpty(le.AnnoHandle))
                        {
                            int hIdx;
                            if (handleToTextIdx.TryGetValue(le.AnnoHandle, out hIdx))
                            {
                                targetTxtIdx = hIdx;
                            }
                            else
                            {
                                // Associative handle trỏ tới entity không phải text kích thước ống (vd MTEXT tiêu âm/van)
                                continue;
                            }
                        }

                        if (targetTxtIdx < 0)
                        {
                            int bestAnyIdx = -1;
                            double bestDist = maxTailDistCad;
                            for (int ai = 0; ai < parsedAnyTexts.Count; ai++)
                            {
                                RawAnyTextEntry any = parsedAnyTexts[ai];
                                double d = DistancePointToBox2D(le.TailX, le.TailY, any.MinX, any.MinY, any.MaxX, any.MaxY);
                                if (d < bestDist)
                                {
                                    bestDist = d;
                                    bestAnyIdx = ai;
                                }
                            }
                            if (bestAnyIdx >= 0 && parsedAnyTexts[bestAnyIdx].TextEntryIndex >= 0)
                            {
                                targetTxtIdx = parsedAnyTexts[bestAnyIdx].TextEntryIndex;
                            }
                        }

                        if (targetTxtIdx < 0 || targetTxtIdx >= rawTexts.Count) continue;

                        XYZ arrowFeet = new XYZ(le.ArrowX * unitToMm * MM_TO_FEET, le.ArrowY * unitToMm * MM_TO_FEET, 0.0);
                        if (!primaryLeaderAssigned[targetTxtIdx])
                        {
                            rawTexts[targetTxtIdx].LeaderArrowPoint = arrowFeet;
                            rawTexts[targetTxtIdx].HasLeaderArrow = true;
                            primaryLeaderAssigned[targetTxtIdx] = true;
                        }
                        else
                        {
                            RawTextEntry src = parsedTextEntries[targetTxtIdx];
                            CadTextNote extra = new CadTextNote(src.Content, src.WidthMm, src.HeightMm, rawTexts[targetTxtIdx].Position, src.Layer);
                            extra.LeaderArrowPoint = arrowFeet;
                            extra.HasLeaderArrow = true;
                            rawTexts.Add(extra);
                        }
                    }
                }

                // Kiểm tra độ lệch Center-to-Center vs Origin-to-Origin trong Revit Symbol Geometry
                XYZ symbolOffsetFeet = ComputeSymbolOffsetFeet(cadLink, hasExtents, minMmX, minMmY, maxMmX, maxMmY);

                foreach (CadTextNote t in rawTexts)
                {
                    XYZ symPt = t.Position + symbolOffsetFeet;
                    t.Position = totalXform.OfPoint(symPt);
                    if (t.HasLeaderArrow && t.LeaderArrowPoint != null)
                    {
                        t.LeaderArrowPoint = totalXform.OfPoint(t.LeaderArrowPoint + symbolOffsetFeet);
                    }
                    outTexts.Add(t);
                }

                // Chuyển đổi BLKX -> CadExclusionZone + phát hiện thêm van gió từ ATTRIB/ATTDEF (vd MFD/OBD/NRD/FD trong *U...)
                HashSet<string> damperDedupKeys = new HashSet<string>();
                for (int i = 0; i < parsedDamperEntries.Count; i++)
                {
                    RawDamperEntry d = parsedDamperEntries[i];
                    XYZ ptFeet = new XYZ(d.X * unitToMm * MM_TO_FEET, d.Y * unitToMm * MM_TO_FEET, 0.0);
                    XYZ wcsPt = totalXform.OfPoint(ptFeet + symbolOffsetFeet);
                    rawDampers.Add(new CadDamperItem(d.TypeName, wcsPt, 0, 0, XYZ.BasisX));
                    damperDedupKeys.Add(BuildPointKey(d.TypeName, wcsPt));
                }

                for (int i = 0; i < parsedBlkBoxes.Count; i++)
                {
                    RawBlkBoxEntry be = parsedBlkBoxes[i];
                    RawBlockDef bdef = null;
                    if (!string.IsNullOrEmpty(be.BlockName)) blockDefs.TryGetValue(be.BlockName, out bdef);

                    double edge0Mm = Dist2D(be.CornersCad[0], be.CornersCad[1]) * unitToMm;
                    double edge1Mm = Dist2D(be.CornersCad[1], be.CornersCad[2]) * unitToMm;
                    double maxDimMm = Math.Max(edge0Mm, edge1Mm);
                    if (maxDimMm < 30.0) continue;

                    string tagText;
                    string damperType;
                    CadExclusionKind exKind = ClassifyBlockExclusion(bdef, be.Layer, be.BlockName, be.AttribText, edge0Mm, edge1Mm, out tagText, out damperType);
                    if (exKind == CadExclusionKind.Unknown) continue;

                    CadExclusionZone zone = new CadExclusionZone();
                    zone.Kind = exKind;
                    zone.BlockName = be.BlockName ?? string.Empty;
                    zone.LayerName = be.Layer ?? string.Empty;
                    zone.TagText = tagText ?? string.Empty;

                    double sx = 0, sy = 0;
                    for (int c = 0; c < be.CornersCad.Count; c++)
                    {
                        XYZ localFeet = new XYZ(be.CornersCad[c].X * unitToMm * MM_TO_FEET, be.CornersCad[c].Y * unitToMm * MM_TO_FEET, 0.0);
                        XYZ wcsCorner = totalXform.OfPoint(localFeet + symbolOffsetFeet);
                        zone.Corners.Add(wcsCorner);
                        sx += wcsCorner.X;
                        sy += wcsCorner.Y;
                    }
                    zone.Center = new XYZ(sx / zone.Corners.Count, sy / zone.Corners.Count, 0.0);
                    zone.ComputeBoundsFromCorners();
                    outExclusions.Add(zone);

                    if (exKind == CadExclusionKind.Damper && !string.IsNullOrEmpty(damperType))
                    {
                        string dKey = BuildPointKey(damperType, zone.Center);
                        if (!damperDedupKeys.Contains(dKey))
                        {
                            damperDedupKeys.Add(dKey);
                            XYZ dir = XYZ.BasisX;
                            double wMm = edge0Mm, hMm = edge1Mm;
                            if (zone.Corners.Count >= 3)
                            {
                                XYZ v01 = zone.Corners[1] - zone.Corners[0];
                                XYZ v12 = zone.Corners[2] - zone.Corners[1];
                                if (v01.GetLength() >= v12.GetLength() && v01.GetLength() > 1e-6)
                                {
                                    dir = new XYZ(v01.X, v01.Y, 0.0).Normalize();
                                    wMm = v01.GetLength() * FEET_TO_MM;
                                    hMm = v12.GetLength() * FEET_TO_MM;
                                }
                                else if (v12.GetLength() > 1e-6)
                                {
                                    dir = new XYZ(v12.X, v12.Y, 0.0).Normalize();
                                    wMm = v12.GetLength() * FEET_TO_MM;
                                    hMm = v01.GetLength() * FEET_TO_MM;
                                }
                            }
                            rawDampers.Add(new CadDamperItem(damperType, zone.Center, wMm, hMm, dir));
                        }
                    }
                }

                for (int i = 0; i < parsedHatches.Count; i++)
                {
                    RawHatchEntry he = parsedHatches[i];
                    List<XYZ> wcsPoly = new List<XYZ>(he.PolygonCad.Count);
                    double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
                    double sx = 0, sy = 0;
                    for (int p = 0; p < he.PolygonCad.Count; p++)
                    {
                        XYZ localFeet = new XYZ(he.PolygonCad[p].X * unitToMm * MM_TO_FEET, he.PolygonCad[p].Y * unitToMm * MM_TO_FEET, 0.0);
                        XYZ wcsPt = totalXform.OfPoint(localFeet + symbolOffsetFeet);
                        wcsPoly.Add(wcsPt);
                        if (wcsPt.X < minX) minX = wcsPt.X;
                        if (wcsPt.Y < minY) minY = wcsPt.Y;
                        if (wcsPt.X > maxX) maxX = wcsPt.X;
                        if (wcsPt.Y > maxY) maxY = wcsPt.Y;
                        sx += wcsPt.X;
                        sy += wcsPt.Y;
                    }
                    double spanXmm = (maxX - minX) * FEET_TO_MM;
                    double spanYmm = (maxY - minY) * FEET_TO_MM;
                    double minDim = Math.Min(spanXmm, spanYmm);
                    double maxDim = Math.Max(spanXmm, spanYmm);
                    // Bỏ qua hatch quá nhỏ (<50mm) hoặc hatch nền toàn công trình (>15000mm hoặc minDim > 4500mm)
                    if (minDim < 50.0 || minDim > 4500.0 || maxDim > 15000.0) continue;

                    CadExclusionZone hz = new CadExclusionZone();
                    hz.Kind = CadExclusionKind.PlenumHatch;
                    hz.BlockName = string.Empty;
                    hz.LayerName = he.Layer ?? string.Empty;
                    hz.TagText = he.Pattern ?? string.Empty;
                    hz.Corners = wcsPoly;
                    hz.Center = new XYZ(sx / wcsPoly.Count, sy / wcsPoly.Count, 0.0);
                    hz.ComputeBoundsFromCorners();
                    outExclusions.Add(hz);
                }

                for (int i = 0; i < rawDampers.Count; i++)
                {
                    outDampers.Add(rawDampers[i]);
                }

                diagnostic = string.Format(
                    "DWG '{0}' ({1}): {2} text kích thước ống gió (lọc bỏ {3} miệng gió, {4} tiêu âm ATT; tổng {5} text CAD; {6} van gió; {7} vùng loại trừ).",
                    Path.GetFileName(dwgPath),
                    extractStatus,
                    outTexts.Count,
                    filteredGrilleCount,
                    filteredAttCount,
                    totalRawTextLines,
                    outDampers.Count,
                    outExclusions.Count);
                _lastDiagnostic = diagnostic;
            }
            catch (Exception ex)
            {
                diagnostic = string.Format("Lỗi đọc cache DWG '{0}': {1}", Path.GetFileName(dwgPath), ex.Message);
                _lastDiagnostic = diagnostic;
            }
        }

        private static double DistancePointToBox2D(double px, double py, double minX, double minY, double maxX, double maxY)
        {
            double dx = 0.0;
            if (px < minX) dx = minX - px;
            else if (px > maxX) dx = px - maxX;

            double dy = 0.0;
            if (py < minY) dy = minY - py;
            else if (py > maxY) dy = py - maxY;

            return Math.Sqrt(dx * dx + dy * dy);
        }

        private class RawTextEntry
        {
            public string Content;
            public double WidthMm;
            public double HeightMm;
            public double X;
            public double Y;
            public double MinX;
            public double MinY;
            public double MaxX;
            public double MaxY;
            public string Handle;
            public string Layer;
        }

        private class RawAnyTextEntry
        {
            public double X;
            public double Y;
            public double MinX;
            public double MinY;
            public double MaxX;
            public double MaxY;
            public string Handle;
            public string Layer;
            public int TextEntryIndex;
        }

        private class RawLeaderEntry
        {
            public string Layer;
            public double ArrowX;
            public double ArrowY;
            public double TailX;
            public double TailY;
            public string AnnoHandle;
        }

        private class RawDamperEntry
        {
            public string TypeName;
            public double X;
            public double Y;
        }

        private class CadPt2D
        {
            public double X;
            public double Y;
            public CadPt2D(double x, double y) { X = x; Y = y; }
        }

        private class RawBlockDef
        {
            public string Name;
            public int LineCount;
            public int DiagCount;
            public int ArcCount;
            public int CircleCount;
            public int LwCount;
            public int R4Count;
            public int SolidCount;
            public int HatchCount;
            public string AttTags;
            public string SubLayers;
        }

        private class RawBlkBoxEntry
        {
            public string Layer;
            public string BlockName;
            public List<CadPt2D> CornersCad;
            public string AttribText;
        }

        private class RawHatchEntry
        {
            public string Layer;
            public string Pattern;
            public List<CadPt2D> PolygonCad;
        }

        private static readonly Regex DamperTagRegex = new Regex(
            @"\b(VCD|MFD|NRD|OBD|FD|DAMPER|FIRE_DAMPER|NORETURN)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex AttenuatorBlockRegex = new Regex(
            @"(^|[\s_\-])ATT([\s_\-]|$)|ATTEN|SILENCER",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex FanEquipBlockRegex = new Regex(
            @"\b(FAN|AXIAL|CENTRIFUGAL|FLEX|AHU|PAU|FCU|ERV|HRV|CHILLER|BLOWER|EQUIP)\b|AXIAL[\-_]?FAN|M[\-_]?FLEX",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex DuctFittingNameRegex = new Regex(
            @"MEPG[\-_]?DUCT|CÚT|CHÂN\s*RẼ|ỐNG\s*GIÓ|\b(ELBOW|TEE|WYE|TAP|REDUCER|TRANSITION|OFFSET|BEND)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex SensorTagRegex = new Regex(
            @"\b(IAQ|CO2|CO|RH|TEMP|SENSOR|THERMOSTAT)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static RawBlockDef ParseBlockDefLine(string[] parts)
        {
            // BDEF|name|xmin|ymin|xmax|ymax|lineCnt|diagCnt|arcCnt|circleCnt|lwCnt|r4Cnt|solidCnt|hatchCnt|attTags|subLayers
            RawBlockDef def = new RawBlockDef();
            def.Name = parts[1];
            int.TryParse(parts[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out def.LineCount);
            int.TryParse(parts[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out def.DiagCount);
            int.TryParse(parts[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out def.ArcCount);
            int.TryParse(parts[9], NumberStyles.Integer, CultureInfo.InvariantCulture, out def.CircleCount);
            int.TryParse(parts[10], NumberStyles.Integer, CultureInfo.InvariantCulture, out def.LwCount);
            int.TryParse(parts[11], NumberStyles.Integer, CultureInfo.InvariantCulture, out def.R4Count);
            int.TryParse(parts[12], NumberStyles.Integer, CultureInfo.InvariantCulture, out def.SolidCount);
            int.TryParse(parts[13], NumberStyles.Integer, CultureInfo.InvariantCulture, out def.HatchCount);
            def.AttTags = (parts.Length >= 15) ? (parts[14] ?? string.Empty) : string.Empty;
            def.SubLayers = (parts.Length >= 16) ? (parts[15] ?? string.Empty) : string.Empty;
            return def;
        }

        private static List<CadPt2D> ParsePointListCad(string encoded)
        {
            List<CadPt2D> pts = new List<CadPt2D>();
            if (string.IsNullOrEmpty(encoded)) return pts;
            string[] tokens = encoded.Split(';');
            for (int i = 0; i < tokens.Length; i++)
            {
                string tok = tokens[i];
                if (string.IsNullOrEmpty(tok)) continue;
                int comma = tok.IndexOf(',');
                if (comma <= 0 || comma >= tok.Length - 1) continue;
                double x, y;
                if (double.TryParse(tok.Substring(0, comma), NumberStyles.Any, CultureInfo.InvariantCulture, out x) &&
                    double.TryParse(tok.Substring(comma + 1), NumberStyles.Any, CultureInfo.InvariantCulture, out y))
                {
                    pts.Add(new CadPt2D(x, y));
                }
            }
            return pts;
        }

        private static double Dist2D(CadPt2D a, CadPt2D b)
        {
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static string BuildPointKey(string tag, XYZ pt)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}|{1:F1}|{2:F1}",
                tag,
                pt.X,
                pt.Y);
        }

        private static CadExclusionKind ClassifyBlockExclusion(
            RawBlockDef def,
            string insertLayer,
            string blockName,
            string attribText,
            double edge0Mm,
            double edge1Mm,
            out string tagText,
            out string damperType)
        {
            string bUp = (blockName ?? string.Empty).ToUpperInvariant();
            string lUp = (insertLayer ?? string.Empty).ToUpperInvariant();
            string aUp = (attribText ?? string.Empty).Trim().ToUpperInvariant();
            string defAttUp = (def != null && def.AttTags != null) ? def.AttTags.ToUpperInvariant() : string.Empty;
            string subLyrsUp = (def != null && def.SubLayers != null) ? def.SubLayers.ToUpperInvariant() : string.Empty;
            double maxDim = Math.Max(edge0Mm, edge1Mm);

            tagText = !string.IsNullOrEmpty(aUp) ? attribText.Trim() : ((def != null) ? def.AttTags : string.Empty);
            damperType = null;

            if (bUp.Contains("ARROW"))
            {
                return CadExclusionKind.ArrowSymbol;
            }

            if (AttenuatorBlockRegex.IsMatch(bUp) ||
                (def != null && def.HatchCount >= 1 && def.LwCount >= 3))
            {
                return CadExclusionKind.Attenuator;
            }

            string combinedDamper = bUp + " " + aUp + " " + defAttUp;
            if (DamperTagRegex.IsMatch(combinedDamper))
            {
                if (combinedDamper.Contains("MFD")) damperType = "MFD";
                else if (combinedDamper.Contains("NRD") || combinedDamper.Contains("NORETURN")) damperType = "NRD";
                else if (combinedDamper.Contains("OBD")) damperType = "OBD";
                else if (combinedDamper.Contains("VCD")) damperType = "VCD";
                else damperType = "FD";
                return CadExclusionKind.Damper;
            }

            if (bUp.Contains("TXT-BOX") || bUp.Contains("TEXT-BOX") || bUp.Contains("CALLOUT") || bUp.Contains("TAG-BOX") ||
                (SensorTagRegex.IsMatch(aUp) && maxDim <= 1000.0) ||
                ((lUp.Contains("TXT") || lUp.Contains("ANNO") || lUp.Contains("GENERAL-35") || lUp.Contains("LABEL") || lUp.Contains("NOTE")) &&
                 def != null && (def.R4Count >= 1 || def.LineCount == 4) && maxDim <= 1200.0))
            {
                return CadExclusionKind.AnnotationBox;
            }

            if (DuctFittingNameRegex.IsMatch(bUp))
            {
                return CadExclusionKind.Unknown;
            }

            if (FanEquipBlockRegex.IsMatch(bUp) ||
                (def != null && def.LineCount >= 12 && (def.ArcCount >= 2 || def.LineCount >= 15)))
            {
                return CadExclusionKind.FanEquipment;
            }

            if (def != null && (def.R4Count >= 1 || def.LineCount >= 4) && def.DiagCount >= 1 && def.SolidCount >= 1)
            {
                return CadExclusionKind.RiserDrop;
            }

            if (lUp.Contains("GRILLE") || lUp.Contains("DIFF") || lUp.Contains("LOUVER") || lUp.Contains("REGISTER") ||
                bUp.Contains("GRILLE") || bUp.Contains("DIFF") || bUp.Contains("OAD-OAG") ||
                subLyrsUp.Contains("GRILLE") ||
                (def != null && (def.R4Count >= 1 || def.LineCount >= 4) && def.DiagCount >= 1))
            {
                return CadExclusionKind.Grille;
            }

            return CadExclusionKind.Unknown;
        }

        private static Transform GetSymbolToRevitTransform(ImportInstance cadLink)
        {
            try
            {
                Options opt = new Options();
                opt.IncludeNonVisibleObjects = true;
                GeometryElement gElem = cadLink.get_Geometry(opt);
                if (gElem != null)
                {
                    foreach (GeometryObject go in gElem)
                    {
                        GeometryInstance gi = go as GeometryInstance;
                        if (gi != null)
                        {
                            return gi.Transform;
                        }
                    }
                }
            }
            catch { }
            return cadLink.GetTotalTransform();
        }

        private static double ResolveCadUnitToMmFactor(
            ImportInstance cadLink,
            int insUnits,
            bool hasExtents,
            double minCadX, double minCadY, double maxCadX, double maxCadY)
        {
            if (insUnits == 4) return 1.0;     // Millimeters
            if (insUnits == 5) return 10.0;    // Centimeters
            if (insUnits == 6) return 1000.0;  // Meters
            if (insUnits == 1) return 25.4;    // Inches
            if (insUnits == 2) return 304.8;   // Feet

            // INSUNITS = 0 (Unitless) hoặc khác: suy luận từ tỷ lệ bounding box giữa Revit Symbol và CAD Extents
            if (hasExtents && cadLink != null)
            {
                try
                {
                    double cadSpan = Math.Max(maxCadX - minCadX, maxCadY - minCadY);
                    if (cadSpan > 1e-3)
                    {
                        Options opt = new Options();
                        opt.IncludeNonVisibleObjects = true;
                        GeometryElement gElem = cadLink.get_Geometry(opt);
                        if (gElem != null)
                        {
                            foreach (GeometryObject go in gElem)
                            {
                                GeometryInstance gi = go as GeometryInstance;
                                if (gi != null)
                                {
                                    GeometryElement sym = gi.GetSymbolGeometry();
                                    if (sym != null)
                                    {
                                        BoundingBoxXYZ bb = sym.GetBoundingBox();
                                        if (bb != null)
                                        {
                                            double symSpanMm = Math.Max(bb.Max.X - bb.Min.X, bb.Max.Y - bb.Min.Y) * FEET_TO_MM;
                                            double ratio = symSpanMm / cadSpan;
                                            double[] known = new double[] { 1.0, 10.0, 1000.0, 25.4, 304.8 };
                                            double best = 1.0;
                                            double bestErr = double.MaxValue;
                                            for (int i = 0; i < known.Length; i++)
                                            {
                                                double err = Math.Abs(ratio - known[i]) / known[i];
                                                if (err < bestErr)
                                                {
                                                    bestErr = err;
                                                    best = known[i];
                                                }
                                            }
                                            if (bestErr < 0.25) return best;
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            return 1.0;
        }

        private static XYZ ComputeSymbolOffsetFeet(
            ImportInstance cadLink,
            bool hasCadExt,
            double minCadMmX, double minCadMmY, double maxCadMmX, double maxCadMmY)
        {
            if (!hasCadExt || cadLink == null) return XYZ.Zero;
            try
            {
                Options opt = new Options();
                opt.IncludeNonVisibleObjects = true;
                GeometryElement gElem = cadLink.get_Geometry(opt);
                if (gElem == null) return XYZ.Zero;

                foreach (GeometryObject go in gElem)
                {
                    GeometryInstance gi = go as GeometryInstance;
                    if (gi != null)
                    {
                        GeometryElement sym = gi.GetSymbolGeometry();
                        if (sym != null)
                        {
                            BoundingBoxXYZ bb = sym.GetBoundingBox();
                            if (bb != null)
                            {
                                XYZ symCenter = (bb.Min + bb.Max) * 0.5;
                                XYZ cadCenter = new XYZ(
                                    ((minCadMmX + maxCadMmX) * 0.5) * MM_TO_FEET,
                                    ((minCadMmY + maxCadMmY) * 0.5) * MM_TO_FEET,
                                    0.0);

                                // Nếu gần Origin-to-Origin (< 500mm) thì không cần bù
                                double diffXY = Math.Sqrt(
                                    Math.Pow(symCenter.X - cadCenter.X, 2) +
                                    Math.Pow(symCenter.Y - cadCenter.Y, 2));

                                if (diffXY > 500.0 * MM_TO_FEET)
                                {
                                    return new XYZ(symCenter.X - cadCenter.X, symCenter.Y - cadCenter.Y, 0.0);
                                }
                            }
                        }
                        break;
                    }
                }
            }
            catch { }
            return XYZ.Zero;
        }

        private static bool IsPureAscii(string text)
        {
            if (string.IsNullOrEmpty(text)) return true;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] > 127) return false;
            }
            return true;
        }

        private static string GetAsciiSafeWorkFolder()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "HDTOOL_CadCache");
            if (IsPureAscii(tempDir))
            {
                try
                {
                    if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);
                    return tempDir;
                }
                catch { }
            }

            // AutoLISP (open ...) fail nếu đường dẫn chứa ký tự Unicode tiếng Việt -> fallback sang CommonApplicationData thuần ASCII
            try
            {
                string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                if (!string.IsNullOrEmpty(progData) && IsPureAscii(progData))
                {
                    string asciiDir = Path.Combine(progData, "HDTOOL_CadCache");
                    if (!Directory.Exists(asciiDir)) Directory.CreateDirectory(asciiDir);
                    return asciiDir;
                }
            }
            catch { }

            try
            {
                if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);
            }
            catch { }
            return tempDir;
        }

        private static string GetCacheFilePath(string dwgPath)
        {
            string folder = GetAsciiSafeWorkFolder();
            try
            {
                FileInfo fi = new FileInfo(dwgPath);
                string key = string.Format("{0}_{1}_{2}_v6", fi.FullName.ToLowerInvariant(), fi.Length, fi.LastWriteTimeUtc.Ticks);
                using (MD5 md5 = MD5.Create())
                {
                    byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(key));
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
                    return Path.Combine(folder, "dwg_" + sb.ToString() + ".txt");
                }
            }
            catch
            {
                return Path.Combine(folder, "hdtool_dwg_cache_v6.txt");
            }
        }

        private static string RunAcCoreExtraction(string accoreExe, string dwgPath, string cacheOutputFile)
        {
            string tempDwg = null;
            string scriptPath = null;
            string tempOutFile = null;
            string workFolder = GetAsciiSafeWorkFolder();

            try
            {
                // Copy sang file tạm tên ASCII để tránh khóa file của Revit hoặc lỗi đường dẫn Unicode
                string uid = Guid.NewGuid().ToString("N");
                tempDwg = Path.Combine(workFolder, "hdtool_src_" + uid + ".dwg");
                File.Copy(dwgPath, tempDwg, true);

                scriptPath = Path.Combine(workFolder, "hdtool_ex_" + uid + ".scr");
                tempOutFile = Path.Combine(workFolder, "hdtool_out_" + uid + ".tmp");
                string outPathLisp = tempOutFile.Replace('\\', '/');

                string lispTemplate =
@"(setvar ""FILEDIA"" 0)
(setvar ""CMDDIA"" 0)
(defun hd-clean (s)
  (if (not s) """"
    (vl-string-translate ""\r\n\t|"" ""    "" s)
  )
)
(defun hd-strip-mtext (s / res i len ch next)
  (if (not s) """"
    (progn
      (setq res """" i 1 len (strlen s))
      (while (<= i len)
        (setq ch (substr s i 1))
        (cond
          ((= ch ""{"" ) nil)
          ((= ch ""}"" ) nil)
          ((= ch ""\\"" )
            (if (< i len)
              (progn
                (setq next (substr s (1+ i) 1))
                (cond
                  ((or (= next ""P"") (= next ""p""))
                    (setq res (strcat res "" "") i (1+ i))
                  )
                  ((or (= next ""\\"") (= next ""{"") (= next ""}""))
                    (setq res (strcat res next) i (1+ i))
                  )
                  ((wcmatch next ""A,C,F,H,Q,T,W,f,p,S"")
                    (while (and (< i len) (/= (substr s i 1) "";"")) (setq i (1+ i)))
                  )
                  (T (setq i (1+ i)))
                )
              )
            )
          )
          (T (setq res (strcat res ch)))
        )
        (setq i (1+ i))
      )
      res
    )
  )
)
(defun hd-get-long-text (ed / res pair)
  (setq res """")
  (foreach pair ed
    (if (or (= (car pair) 3) (= (car pair) 1))
      (setq res (strcat res (cdr pair)))
    )
  )
  res
)
(defun hd-text-center-ocs (ed / typ p10 p11 h72 v73 tb cx cy ang dir ux uy vx vy w h att71 hx hy)
  (setq typ (cdr (assoc 0 ed)))
  (setq p10 (cdr (assoc 10 ed)) p11 (cdr (assoc 11 ed)))
  (cond
    ((or (= typ ""TEXT"") (= typ ""ATTRIB""))
      (setq h72 (cond ((cdr (assoc 72 ed))) (0)) v73 (cond ((cdr (assoc 73 ed))) (0)))
      (if (and p10 p11 (or (/= h72 0) (/= v73 0)) (< (abs (car p10)) 1e-6) (< (abs (cadr p10)) 1e-6))
        p11
        (progn
          (setq tb (textbox ed))
          (if (and tb p10)
            (progn
              (setq cx (* 0.5 (+ (caar tb) (caadr tb))) cy (* 0.5 (+ (cadar tb) (cadadr tb))))
              (setq ang (cond ((cdr (assoc 50 ed))) (0.0)))
              (list (+ (car p10) (- (* cx (cos ang)) (* cy (sin ang))))
                    (+ (cadr p10) (+ (* cx (sin ang)) (* cy (cos ang))))
                    (cond ((caddr p10)) (0.0)))
            )
            p10
          )
        )
      )
    )
    ((= typ ""MTEXT"")
      (if p10
        (progn
          (setq w (cond ((cdr (assoc 42 ed))) (0.0)) h (cond ((cdr (assoc 43 ed))) (0.0)))
          (setq att71 (cond ((cdr (assoc 71 ed))) (1)))
          (if (and (> w 1e-4) (> h 1e-4))
            (progn
              (setq dir (cdr (assoc 11 ed)))
              (if (and dir (> (+ (* (car dir) (car dir)) (* (cadr dir) (cadr dir))) 1e-8))
                (progn
                  (setq ux (car dir) uy (cadr dir))
                  (setq cx (sqrt (+ (* ux ux) (* uy uy))))
                  (setq ux (/ ux cx) uy (/ uy cx))
                )
                (progn
                  (setq ang (cond ((cdr (assoc 50 ed))) (0.0)))
                  (setq ux (cos ang) uy (sin ang))
                )
              )
              (setq vx (- uy) vy ux)
              (setq hx (cond ((member att71 '(1 4 7)) (* 0.5 w)) ((member att71 '(3 6 9)) (* -0.5 w)) (T 0.0)))
              (setq hy (cond ((member att71 '(1 2 3)) (* -0.5 h)) ((member att71 '(7 8 9)) (* 0.5 h)) (T 0.0)))
              (list (+ (car p10) (* hx ux) (* hy vx)) (+ (cadr p10) (* hx uy) (* hy vy)) (cond ((caddr p10)) (0.0)))
            )
            p10
          )
        )
        nil
      )
    )
    (T p10)
  )
)
(defun hd-is-diag (x1 y1 x2 y2 / dx dy len)
  (setq dx (abs (- x2 x1)) dy (abs (- y2 y1)))
  (if (and (> dx 5.0) (> dy 5.0))
    (progn
      (setq len (sqrt (+ (* dx dx) (* dy dy))))
      (and (> dx (* 0.15 len)) (> dy (* 0.15 len)))
    )
    nil
  )
)
(defun hd-acc-pt (bx x y)
  (if (null bx)
    (list x y x y)
    (list (min (car bx) x) (min (cadr bx) y) (max (caddr bx) x) (max (cadddr bx) y))
  )
)
(defun hd-add-uniq (lst s)
  (if (and s (/= s """") (not (member s lst)))
    (cons s lst)
    lst
  )
)
(defun hd-join (lst sep / res itm)
  (setq res """")
  (foreach itm (reverse lst)
    (setq res (if (= res """") itm (strcat res sep itm)))
  )
  res
)
(defun hd-xform-pt (pt ox oy sx sy ang / px py c s)
  (setq px (* (car pt) sx) py (* (cadr pt) sy))
  (setq c (cos ang) s (sin ang))
  (list (+ ox (- (* px c) (* py s))) (+ oy (+ (* px s) (* py c))) 0.0)
)
(defun hd-build-blk-cache (f / blk bn bbase sub subed subtyp txt pt items subins
                              bx lcnt dcnt acnt ccnt lwcnt r4cnt scnt hcnt atts lyrs
                              p10 p11 lpts pair lx ly prevp firstp plflg vent ved lastv vpts
                              rad a1 a2 amid tag s1 s2 inEdge)
  (setq *hd-blk-map* nil)
  (setq *hd-bdef-box* nil)
  (setq blk (tblnext ""BLOCK"" T))
  (while blk
    (setq bn (cdr (assoc 2 blk)))
    (if (and bn (not (wcmatch (strcase bn) ""*PAPER_SPACE*,*MODEL_SPACE*"")))
      (progn
        (setq bbase (cond ((cdr (assoc 10 blk))) ('(0.0 0.0 0.0))))
        (setq sub (cdr (assoc -2 blk)) items nil subins nil
              bx nil lcnt 0 dcnt 0 acnt 0 ccnt 0 lwcnt 0 r4cnt 0 scnt 0 hcnt 0 atts nil lyrs nil)
        (while sub
          (setq subed (entget sub) subtyp (cdr (assoc 0 subed)))
          (setq lyrs (hd-add-uniq lyrs (hd-clean (cdr (assoc 8 subed)))))
          (cond
            ((= subtyp ""LINE"")
              (setq lcnt (1+ lcnt))
              (setq p10 (cdr (assoc 10 subed)) p11 (cdr (assoc 11 subed)))
              (if (and p10 p11)
                (progn
                  (setq lx (- (car p10) (car bbase)) ly (- (cadr p10) (cadr bbase)))
                  (setq bx (hd-acc-pt bx lx ly))
                  (setq p10 (list lx ly)
                        lx (- (car p11) (car bbase)) ly (- (cadr p11) (cadr bbase)))
                  (setq bx (hd-acc-pt bx lx ly))
                  (if (hd-is-diag (car p10) (cadr p10) lx ly) (setq dcnt (1+ dcnt)))
                )
              )
            )
            ((= subtyp ""LWPOLYLINE"")
              (setq lwcnt (1+ lwcnt) lpts nil plflg (cond ((cdr (assoc 70 subed))) (0)))
              (foreach pair subed
                (if (= (car pair) 10)
                  (progn
                    (setq lx (- (cadr pair) (car bbase)) ly (- (caddr pair) (cadr bbase)))
                    (setq bx (hd-acc-pt bx lx ly))
                    (setq lpts (cons (list lx ly) lpts))
                  )
                )
              )
              (setq lpts (reverse lpts))
              (if (and (= (logand plflg 1) 1) (= (length lpts) 4)) (setq r4cnt (1+ r4cnt)))
              (if (>= (length lpts) 2)
                (progn
                  (setq prevp (car lpts) firstp (car lpts))
                  (foreach p11 (cdr lpts)
                    (if (hd-is-diag (car prevp) (cadr prevp) (car p11) (cadr p11)) (setq dcnt (1+ dcnt)))
                    (setq prevp p11)
                  )
                  (if (and (= (logand plflg 1) 1) (hd-is-diag (car prevp) (cadr prevp) (car firstp) (cadr firstp)))
                    (setq dcnt (1+ dcnt))
                  )
                )
              )
            )
            ((= subtyp ""POLYLINE"")
              (setq lwcnt (1+ lwcnt) vpts nil plflg (cond ((cdr (assoc 70 subed))) (0)))
              (setq vent (entnext sub) lastv sub)
              (while (and vent (/= (cdr (assoc 0 (setq ved (entget vent)))) ""SEQEND""))
                (if (= (cdr (assoc 0 ved)) ""VERTEX"")
                  (progn
                    (setq p10 (cdr (assoc 10 ved)))
                    (if p10
                      (progn
                        (setq lx (- (car p10) (car bbase)) ly (- (cadr p10) (cadr bbase)))
                        (setq bx (hd-acc-pt bx lx ly))
                        (setq vpts (cons (list lx ly) vpts))
                      )
                    )
                  )
                )
                (setq lastv vent vent (entnext vent))
              )
              (if (and (= (logand plflg 1) 1) (= (length vpts) 4)) (setq r4cnt (1+ r4cnt)))
              (setq sub (if vent vent lastv))
            )
            ((= subtyp ""CIRCLE"")
              (setq ccnt (1+ ccnt) p10 (cdr (assoc 10 subed)) rad (cond ((cdr (assoc 40 subed))) (0.0)))
              (if p10
                (progn
                  (setq lx (- (car p10) (car bbase)) ly (- (cadr p10) (cadr bbase)))
                  (setq bx (hd-acc-pt bx (- lx rad) (- ly rad)))
                  (setq bx (hd-acc-pt bx (+ lx rad) (+ ly rad)))
                )
              )
            )
            ((= subtyp ""ARC"")
              (setq acnt (1+ acnt) p10 (cdr (assoc 10 subed)) rad (cond ((cdr (assoc 40 subed))) (0.0))
                    a1 (cond ((cdr (assoc 50 subed))) (0.0)) a2 (cond ((cdr (assoc 51 subed))) (0.0)))
              (if p10
                (progn
                  (setq lx (- (car p10) (car bbase)) ly (- (cadr p10) (cadr bbase)))
                  (setq amid (* 0.5 (+ a1 a2)))
                  (if (< a2 a1) (setq amid (+ amid pi)))
                  (setq bx (hd-acc-pt bx (+ lx (* rad (cos a1))) (+ ly (* rad (sin a1)))))
                  (setq bx (hd-acc-pt bx (+ lx (* rad (cos amid))) (+ ly (* rad (sin amid)))))
                  (setq bx (hd-acc-pt bx (+ lx (* rad (cos a2))) (+ ly (* rad (sin a2)))))
                )
              )
            )
            ((or (= subtyp ""SOLID"") (= subtyp ""TRACE""))
              (setq scnt (1+ scnt))
              (foreach pair subed
                (if (and (member (car pair) '(10 11 12 13)) (listp (cdr pair)))
                  (setq bx (hd-acc-pt bx (- (cadr pair) (car bbase)) (- (caddr pair) (cadr bbase))))
                )
              )
            )
            ((= subtyp ""HATCH"")
              (setq hcnt (1+ hcnt) inEdge nil)
              (foreach pair subed
                (cond
                  ((= (car pair) 92) (setq inEdge T))
                  ((= (car pair) 75) (setq inEdge nil))
                  ((and inEdge (or (= (car pair) 10) (= (car pair) 11)) (listp (cdr pair)))
                    (setq bx (hd-acc-pt bx (- (cadr pair) (car bbase)) (- (caddr pair) (cadr bbase))))
                  )
                )
              )
            )
            ((= subtyp ""ATTDEF"")
              (setq s1 (hd-clean (cdr (assoc 1 subed))) s2 (hd-clean (cdr (assoc 2 subed))))
              (if (and s1 (/= s1 """") (<= (strlen s1) 30)) (setq atts (hd-add-uniq atts s1)))
              (if (and s2 (/= s2 """") (<= (strlen s2) 30)) (setq atts (hd-add-uniq atts s2)))
            )
            ((or (= subtyp ""TEXT"") (= subtyp ""MTEXT""))
              (setq txt (hd-get-long-text subed))
              (if (= subtyp ""MTEXT"") (setq txt (hd-strip-mtext txt)))
              (setq pt (hd-text-center-ocs subed))
              (if (and txt (/= txt """") pt)
                (progn
                  (setq items (cons (list (cdr (assoc 8 subed)) txt (- (car pt) (car bbase)) (- (cadr pt) (cadr bbase))) items))
                  (if (<= (strlen txt) 30) (setq atts (hd-add-uniq atts (hd-clean txt))))
                )
              )
            )
            ((= subtyp ""MULTILEADER"")
              (setq txt (cdr (assoc 304 subed)))
              (setq pt (cond ((cdr (assoc 12 subed))) ((cdr (assoc 10 subed)))))
              (if (and txt (/= txt """") pt)
                (setq items (cons (list (cdr (assoc 8 subed)) (hd-strip-mtext txt) (- (car pt) (car bbase)) (- (cadr pt) (cadr bbase))) items))
              )
            )
            ((= subtyp ""INSERT"")
              (setq pt (cdr (assoc 10 subed)))
              (if pt
                (setq subins (cons (list
                  (cdr (assoc 2 subed))
                  (cdr (assoc 8 subed))
                  (- (car pt) (car bbase))
                  (- (cadr pt) (cadr bbase))
                  (cond ((cdr (assoc 41 subed))) (1.0))
                  (cond ((cdr (assoc 42 subed))) (1.0))
                  (cond ((cdr (assoc 50 subed))) (0.0))) subins))
              )
            )
          )
          (setq sub (entnext sub))
        )
        (if bx
          (progn
            (setq *hd-bdef-box* (cons (list bn (car bx) (cadr bx) (caddr bx) (cadddr bx)) *hd-bdef-box*))
            (write-line (strcat ""BDEF|"" (hd-clean bn) ""|""
              (rtos (car bx) 2 2) ""|"" (rtos (cadr bx) 2 2) ""|""
              (rtos (caddr bx) 2 2) ""|"" (rtos (cadddr bx) 2 2) ""|""
              (itoa lcnt) ""|"" (itoa dcnt) ""|"" (itoa acnt) ""|"" (itoa ccnt) ""|""
              (itoa lwcnt) ""|"" (itoa r4cnt) ""|"" (itoa scnt) ""|"" (itoa hcnt) ""|""
              (vl-string-translate "";"" "" "" (hd-join atts "","")) ""|""
              (vl-string-translate "";"" "" "" (hd-join lyrs "",""))) f)
          )
        )
        (if (or items subins)
          (setq *hd-blk-map* (cons (list bn items subins) *hd-blk-map*))
        )
      )
    )
    (setq blk (tblnext ""BLOCK""))
  )
)
(defun hd-emit-blkx (f topEnt lay bn ox oy sx sy ang attStr / bbe xmin ymin xmax ymax p1 p2 p3 p4)
  (if (setq bbe (assoc bn *hd-bdef-box*))
    (progn
      (setq xmin (nth 1 bbe) ymin (nth 2 bbe) xmax (nth 3 bbe) ymax (nth 4 bbe))
      (setq p1 (trans (hd-xform-pt (list xmin ymin 0.0) ox oy sx sy ang) topEnt 0))
      (setq p2 (trans (hd-xform-pt (list xmax ymin 0.0) ox oy sx sy ang) topEnt 0))
      (setq p3 (trans (hd-xform-pt (list xmax ymax 0.0) ox oy sx sy ang) topEnt 0))
      (setq p4 (trans (hd-xform-pt (list xmin ymax 0.0) ox oy sx sy ang) topEnt 0))
      (write-line (strcat ""BLKX|"" (hd-clean lay) ""|"" (hd-clean bn) ""|""
        (rtos (car p1) 2 2) "","" (rtos (cadr p1) 2 2) "";""
        (rtos (car p2) 2 2) "","" (rtos (cadr p2) 2 2) "";""
        (rtos (car p3) 2 2) "","" (rtos (cadr p3) 2 2) "";""
        (rtos (car p4) 2 2) "","" (rtos (cadr p4) 2 2) ""|""
        (vl-string-translate "";"" "" "" (hd-clean attStr))) f)
    )
  )
)
(defun hd-emit-blk (f topEnt bn ox oy sx sy ang depth / entry itm sub lp wpt sbname nsx nsy nang)
  (if (<= depth 5)
    (if (setq entry (assoc bn *hd-blk-map*))
      (progn
        (foreach itm (cadr entry)
          (setq lp (hd-xform-pt (list (caddr itm) (cadddr itm) 0.0) ox oy sx sy ang))
          (setq wpt (trans lp topEnt 0))
          (write-line (strcat ""TXT|"" (hd-clean (car itm)) ""|"" (hd-clean (cadr itm)) ""|"" (rtos (car wpt) 2 4) ""|"" (rtos (cadr wpt) 2 4)) f)
        )
        (foreach sub (caddr entry)
          (setq sbname (car sub))
          (setq lp (hd-xform-pt (list (nth 2 sub) (nth 3 sub) 0.0) ox oy sx sy ang))
          (setq wpt (trans lp topEnt 0))
          (setq nsx (* sx (nth 4 sub)) nsy (* sy (nth 5 sub))
                nang (if (< (* sx sy) 0.0) (- ang (nth 6 sub)) (+ ang (nth 6 sub))))
          (write-line (strcat ""BLK|"" (hd-clean (cadr sub)) ""|"" (hd-clean sbname) ""|"" (rtos (car wpt) 2 4) ""|"" (rtos (cadr wpt) 2 4)) f)
          (hd-emit-blkx f topEnt (cadr sub) sbname (car lp) (cadr lp) nsx nsy nang """")
          (hd-emit-blk f topEnt sbname (car lp) (cadr lp) nsx nsy nang (1+ depth))
        )
      )
    )
  )
)
(defun hd-emit-hatch (f ent ed lay / pat inEdge isPoly pts bx pair wpt s)
  (setq pat (cond ((cdr (assoc 2 ed))) ("""")))
  (setq isPoly (= (logand (cond ((cdr (assoc 92 ed))) (0)) 2) 2))
  (setq inEdge nil pts nil bx nil)
  (foreach pair ed
    (cond
      ((= (car pair) 92) (setq inEdge T))
      ((= (car pair) 75) (setq inEdge nil))
      ((and inEdge (or (= (car pair) 10) (= (car pair) 11)) (listp (cdr pair)))
        (setq wpt (trans (list (cadr pair) (caddr pair) 0.0) ent 0))
        (setq bx (hd-acc-pt bx (car wpt) (cadr wpt)))
        (if (and isPoly (= (car pair) 10))
          (setq pts (cons wpt pts))
        )
      )
    )
  )
  (if (and isPoly (>= (length pts) 3) (<= (length pts) 24))
    (progn
      (setq s """")
      (foreach wpt (reverse pts)
        (setq s (if (= s """")
          (strcat (rtos (car wpt) 2 2) "","" (rtos (cadr wpt) 2 2))
          (strcat s "";"" (rtos (car wpt) 2 2) "","" (rtos (cadr wpt) 2 2))))
      )
      (write-line (strcat ""HTX|"" (hd-clean lay) ""|"" (hd-clean pat) ""|"" s) f)
    )
    (if bx
      (write-line (strcat ""HTX|"" (hd-clean lay) ""|"" (hd-clean pat) ""|""
        (rtos (car bx) 2 2) "","" (rtos (cadr bx) 2 2) "";""
        (rtos (caddr bx) 2 2) "","" (rtos (cadr bx) 2 2) "";""
        (rtos (caddr bx) 2 2) "","" (rtos (cadddr bx) 2 2) "";""
        (rtos (car bx) 2 2) "","" (rtos (cadddr bx) 2 2)) f)
    )
  )
)
(defun hd-text-bbox-wcs (ed typ wpt / tb hw hh ang dir ux uy d rx ry)
  (setq rx 0.0 ry 0.0)
  (cond
    ((or (= typ ""TEXT"") (= typ ""ATTRIB""))
      (setq tb (textbox ed))
      (if tb
        (progn
          (setq hw (* 0.5 (- (caadr tb) (caar tb))) hh (* 0.5 (- (cadadr tb) (cadar tb))))
          (setq ang (cond ((cdr (assoc 50 ed))) (0.0)))
          (setq rx (+ (* (abs hw) (abs (cos ang))) (* (abs hh) (abs (sin ang)))))
          (setq ry (+ (* (abs hw) (abs (sin ang))) (* (abs hh) (abs (cos ang)))))
        )
      )
    )
    ((= typ ""MTEXT"")
      (setq hw (* 0.5 (cond ((cdr (assoc 42 ed))) (0.0))) hh (* 0.5 (cond ((cdr (assoc 43 ed))) (0.0))))
      (setq dir (cdr (assoc 11 ed)))
      (if (and dir (> (+ (* (car dir) (car dir)) (* (cadr dir) (cadr dir))) 1e-8))
        (progn
          (setq ux (car dir) uy (cadr dir) d (sqrt (+ (* ux ux) (* uy uy))))
          (setq ux (/ ux d) uy (/ uy d))
          (setq rx (+ (* (abs hw) (abs ux)) (* (abs hh) (abs uy))))
          (setq ry (+ (* (abs hw) (abs uy)) (* (abs hh) (abs ux))))
        )
        (progn
          (setq ang (cond ((cdr (assoc 50 ed))) (0.0)))
          (setq rx (+ (* (abs hw) (abs (cos ang))) (* (abs hh) (abs (sin ang)))))
          (setq ry (+ (* (abs hw) (abs (sin ang))) (* (abs hh) (abs (cos ang)))))
        )
      )
    )
  )
  (list (- (car wpt) rx) (- (cadr wpt) ry) (+ (car wpt) rx) (+ (cadr wpt) ry))
)
(defun c:HDEX (/ f ss i ent ed typ txt pt wpt bname lay extMin extMax att atted atttxt attpt p10 p11 insunits sx sy ang attList tbb hnd lpts pair arrowPt tailPt annoEnt annoH inLdr ldrTip)
  (setq f (open ""__HD_OUT_PATH__"" ""w""))
  (if f
    (progn
      (setq insunits (cond ((getvar ""INSUNITS"")) (4)))
      (write-line (strcat ""VARS|INSUNITS|"" (itoa insunits)) f)
      (setq extMin (getvar ""EXTMIN"") extMax (getvar ""EXTMAX""))
      (if (and extMin extMax)
        (write-line (strcat ""EXT|"" (rtos (car extMin) 2 4) ""|"" (rtos (cadr extMin) 2 4) ""|"" (rtos (car extMax) 2 4) ""|"" (rtos (cadr extMax) 2 4)) f)
      )
      (hd-build-blk-cache f)
      (if (setq ss (ssget ""X"" '((0 . ""TEXT,MTEXT,LEADER,MULTILEADER,DIMENSION,INSERT,HATCH"") (410 . ""Model""))))
        (progn
          (setq i 0)
          (while (< i (sslength ss))
            (setq ent (ssname ss i) ed (entget ent) typ (cdr (assoc 0 ed)) lay (cdr (assoc 8 ed)))
            (cond
              ((= typ ""TEXT"")
                (setq txt (cdr (assoc 1 ed)) pt (hd-text-center-ocs ed))
                (if (and txt pt)
                  (progn
                    (setq wpt (trans pt ent 0) tbb (hd-text-bbox-wcs ed ""TEXT"" wpt) hnd (cond ((cdr (assoc 5 ed))) ("""")))
                    (write-line (strcat ""TXT|"" (hd-clean lay) ""|"" (hd-clean txt) ""|"" (rtos (car wpt) 2 4) ""|"" (rtos (cadr wpt) 2 4)
                      ""|"" (rtos (nth 0 tbb) 2 4) ""|"" (rtos (nth 1 tbb) 2 4) ""|"" (rtos (nth 2 tbb) 2 4) ""|"" (rtos (nth 3 tbb) 2 4) ""|"" (hd-clean hnd)) f)
                  )
                )
              )
              ((= typ ""MTEXT"")
                (setq txt (hd-get-long-text ed) pt (hd-text-center-ocs ed))
                (if (and txt pt)
                  (progn
                    (setq tbb (hd-text-bbox-wcs ed ""MTEXT"" pt) hnd (cond ((cdr (assoc 5 ed))) ("""")))
                    (write-line (strcat ""TXT|"" (hd-clean lay) ""|"" (hd-clean (hd-strip-mtext txt)) ""|"" (rtos (car pt) 2 4) ""|"" (rtos (cadr pt) 2 4)
                      ""|"" (rtos (nth 0 tbb) 2 4) ""|"" (rtos (nth 1 tbb) 2 4) ""|"" (rtos (nth 2 tbb) 2 4) ""|"" (rtos (nth 3 tbb) 2 4) ""|"" (hd-clean hnd)) f)
                  )
                )
              )
              ((= typ ""LEADER"")
                (setq lpts nil)
                (foreach pair ed
                  (if (and (= (car pair) 10) (listp (cdr pair)))
                    (setq lpts (cons (cdr pair) lpts))
                  )
                )
                (setq lpts (reverse lpts))
                (if (>= (length lpts) 2)
                  (progn
                    (setq arrowPt (car lpts) tailPt (car (reverse lpts)))
                    (setq annoEnt (cdr (assoc 340 ed)) annoH """")
                    (if (and annoEnt (setq hnd (cdr (assoc 5 (entget annoEnt))))) (setq annoH hnd))
                    (write-line (strcat ""LDR|"" (hd-clean lay) ""|"" (rtos (car arrowPt) 2 4) ""|"" (rtos (cadr arrowPt) 2 4)
                      ""|"" (rtos (car tailPt) 2 4) ""|"" (rtos (cadr tailPt) 2 4) ""|"" (hd-clean annoH)) f)
                  )
                )
              )
              ((= typ ""MULTILEADER"")
                (setq txt (cdr (assoc 304 ed)))
                (setq pt (cond ((cdr (assoc 12 ed))) ((cdr (assoc 10 ed)))))
                (if (and txt (/= txt """") pt)
                  (progn
                    (write-line (strcat ""TXT|"" (hd-clean lay) ""|"" (hd-clean (hd-strip-mtext txt)) ""|"" (rtos (car pt) 2 4) ""|"" (rtos (cadr pt) 2 4)) f)
                    (setq inLdr nil ldrTip nil)
                    (foreach pair ed
                      (cond
                        ((and (or (= (car pair) 302) (= (car pair) 304)) (wcmatch (strcase (cdr pair)) ""*LEADER_LINE*"")) (setq inLdr T))
                        ((and inLdr (= (car pair) 10) (listp (cdr pair)) (null ldrTip))
                          (setq ldrTip (cdr pair))
                        )
                      )
                    )
                    (if ldrTip
                      (write-line (strcat ""LDR|"" (hd-clean lay) ""|"" (rtos (car ldrTip) 2 4) ""|"" (rtos (cadr ldrTip) 2 4)
                        ""|"" (rtos (car pt) 2 4) ""|"" (rtos (cadr pt) 2 4) ""|"") f)
                    )
                  )
                )
              )
              ((= typ ""DIMENSION"")
                (setq txt (cdr (assoc 1 ed)) p11 (cdr (assoc 11 ed)))
                (if (and txt (/= txt """") (/= txt ""<>"") p11)
                  (progn
                    (setq wpt (trans p11 ent 0))
                    (write-line (strcat ""TXT|"" (hd-clean lay) ""|"" (hd-clean (hd-strip-mtext txt)) ""|"" (rtos (car wpt) 2 4) ""|"" (rtos (cadr wpt) 2 4)) f)
                  )
                )
              )
              ((= typ ""HATCH"")
                (hd-emit-hatch f ent ed lay)
              )
              ((= typ ""INSERT"")
                (setq bname (cdr (assoc 2 ed)) p10 (cdr (assoc 10 ed)))
                (if (and bname p10)
                  (progn
                    (setq wpt (trans p10 ent 0) attList nil)
                    (write-line (strcat ""BLK|"" (hd-clean lay) ""|"" (hd-clean bname) ""|"" (rtos (car wpt) 2 4) ""|"" (rtos (cadr wpt) 2 4)) f)
                    (if (= 1 (cdr (assoc 66 ed)))
                      (progn
                        (setq att (entnext ent))
                        (while (and att (/= (cdr (assoc 0 (setq atted (entget att)))) ""SEQEND""))
                          (if (= (cdr (assoc 0 atted)) ""ATTRIB"")
                            (progn
                              (setq atttxt (cdr (assoc 1 atted)) attpt (hd-text-center-ocs atted))
                              (if (and atttxt (/= atttxt """"))
                                (progn
                                  (setq attList (hd-add-uniq attList (hd-clean atttxt)))
                                  (if attpt
                                    (progn
                                      (setq wpt (trans attpt att 0))
                                      (write-line (strcat ""TXT|"" (hd-clean (cdr (assoc 8 atted))) ""|"" (hd-clean atttxt) ""|"" (rtos (car wpt) 2 4) ""|"" (rtos (cadr wpt) 2 4)) f)
                                    )
                                  )
                                )
                              )
                            )
                          )
                          (setq att (entnext att))
                        )
                      )
                    )
                    (setq sx (cond ((cdr (assoc 41 ed))) (1.0)))
                    (setq sy (cond ((cdr (assoc 42 ed))) (1.0)))
                    (setq ang (cond ((cdr (assoc 50 ed))) (0.0)))
                    (hd-emit-blkx f ent lay bname (car p10) (cadr p10) sx sy ang (hd-join attList "",""))
                    (hd-emit-blk f ent bname (car p10) (cadr p10) sx sy ang 1)
                  )
                )
              )
            )
            (setq i (1+ i))
          )
        )
      )
      (close f)
    )
  )
  (princ)
)
(c:HDEX)
_.QUIT
_Y
";
                string lisp = lispTemplate.Replace("__HD_OUT_PATH__", outPathLisp);

                File.WriteAllText(scriptPath, lisp, Encoding.ASCII);

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = accoreExe;
                psi.Arguments = string.Format("/i \"{0}\" /s \"{1}\" /l en-US", tempDwg, scriptPath);
                psi.WorkingDirectory = workFolder;
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                psi.RedirectStandardInput = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;

                Stopwatch sw = Stopwatch.StartNew();
                using (Process p = Process.Start(psi))
                {
                    p.StandardInput.Close();
                    p.OutputDataReceived += delegate { };
                    p.ErrorDataReceived += delegate { };
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();

                    bool exited = p.WaitForExit(ACCORE_TIMEOUT_MS);
                    sw.Stop();

                    if (!exited)
                    {
                        try { p.Kill(); } catch { }
                        if (File.Exists(tempOutFile) && new FileInfo(tempOutFile).Length > 0)
                        {
                            File.Copy(tempOutFile, cacheOutputFile, true);
                            return string.Format("TimeoutAfterOutput:{0}ms", sw.ElapsedMilliseconds);
                        }
                        return string.Format("Timeout:{0}ms", sw.ElapsedMilliseconds);
                    }

                    if (File.Exists(tempOutFile) && new FileInfo(tempOutFile).Length > 0)
                    {
                        File.Copy(tempOutFile, cacheOutputFile, true);
                        return string.Format("FreshExtract:{0}ms", sw.ElapsedMilliseconds);
                    }
                    return string.Format("Exit{0}_EmptyOutput:{1}ms", p.ExitCode, sw.ElapsedMilliseconds);
                }
            }
            catch (Exception ex)
            {
                return "Error:" + ex.Message;
            }
            finally
            {
                try { if (!string.IsNullOrEmpty(scriptPath) && File.Exists(scriptPath)) File.Delete(scriptPath); } catch { }
                try { if (!string.IsNullOrEmpty(tempDwg) && File.Exists(tempDwg)) File.Delete(tempDwg); } catch { }
                try { if (!string.IsNullOrEmpty(tempOutFile) && File.Exists(tempOutFile)) File.Delete(tempOutFile); } catch { }
            }
        }
    }
}
