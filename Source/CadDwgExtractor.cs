using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;

namespace HDTool
{
    /// <summary>
    /// Bộ trích xuất Text kích thước (WxH) và Block van gió (VCD/MFD) từ file DWG gốc của CAD Link
    /// Tự động bắt tọa độ ModelSpace và biến đổi về hệ tọa độ Revit WCS (Feet)
    /// </summary>
    public static class CadDwgExtractor
    {
        private const double MM_TO_FEET = 1.0 / 304.8;
        private const double FEET_TO_MM = 304.8;

        /// <summary>
        /// Lấy đường dẫn file DWG từ ImportInstance trong Revit
        /// </summary>
        public static string GetDwgFilePath(Document doc, ImportInstance cadLink)
        {
            if (doc == null || cadLink == null) return null;
            try
            {
                ElementId typeId = cadLink.GetTypeId();
                if (typeId != ElementId.InvalidElementId)
                {
                    CADLinkType linkType = doc.GetElement(typeId) as CADLinkType;
                    if (linkType != null)
                    {
                        ExternalFileReference extRef = ExternalFileUtils.GetExternalFileReference(doc, typeId);
                        if (extRef != null)
                        {
                            ModelPath mp = extRef.GetPath();
                            if (mp != null)
                            {
                                string p = ModelPathUtils.ConvertModelPathToUserVisiblePath(mp);
                                if (!string.IsNullOrEmpty(p) && File.Exists(p)) return p;
                            }
                        }
                    }
                }

                Parameter pName = cadLink.get_Parameter(BuiltInParameter.IMPORT_SYMBOL_NAME);
                if (pName != null)
                {
                    string raw = pName.AsString();
                    if (!string.IsNullOrEmpty(raw) && File.Exists(raw)) return raw;
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Tìm đường dẫn accoreconsole.exe có sẵn trên máy
        /// </summary>
        public static string FindAcCoreConsolePath()
        {
            string[] candidates = new string[]
            {
                @"C:\Program Files\Autodesk\AutoCAD 2027\accoreconsole.exe",
                @"C:\Program Files\Autodesk\AutoCAD 2026\accoreconsole.exe",
                @"C:\Program Files\Autodesk\AutoCAD 2025\accoreconsole.exe",
                @"C:\Program Files\Autodesk\AutoCAD 2024\accoreconsole.exe",
                @"C:\Program Files\Autodesk\DWG TrueView 2027 - English\accoreconsole.exe",
                @"C:\Program Files\Autodesk\DWG TrueView 2026 - English\accoreconsole.exe",
                @"C:\Program Files\Autodesk\DWG TrueView 2025 - English\accoreconsole.exe"
            };

            foreach (string p in candidates)
            {
                if (File.Exists(p)) return p;
            }
            return null;
        }

        /// <summary>
        /// Trích xuất toàn bộ Text kích thước WxH và Block van gió từ file DWG
        /// </summary>
        public static void ExtractTextAndDampersFromDwg(
            Document doc,
            ImportInstance cadLink,
            out List<CadTextNote> outTexts,
            out List<CadDamperItem> outDampers)
        {
            outTexts = new List<CadTextNote>();
            outDampers = new List<CadDamperItem>();

            if (doc == null || cadLink == null) return;

            string dwgPath = GetDwgFilePath(doc, cadLink);
            if (string.IsNullOrEmpty(dwgPath) || !File.Exists(dwgPath)) return;

            string cacheFile = GetCacheFilePath(dwgPath);
            if (!File.Exists(cacheFile) || new FileInfo(cacheFile).Length == 0)
            {
                string accore = FindAcCoreConsolePath();
                if (!string.IsNullOrEmpty(accore))
                {
                    RunAcCoreExtraction(accore, dwgPath, cacheFile);
                }
            }

            if (!File.Exists(cacheFile)) return;

            Transform totalXform = cadLink.GetTotalTransform();
            Regex sizeRegex = new Regex(@"(\d{2,4})\s*[xX\*]\s*(\d{2,4})", RegexOptions.IgnoreCase);

            List<CadTextNote> rawTexts = new List<CadTextNote>();
            List<CadDamperItem> rawDampers = new List<CadDamperItem>();
            double minCadX = double.MaxValue, minCadY = double.MaxValue;
            double maxCadX = double.MinValue, maxCadY = double.MinValue;
            bool hasExtents = false;

            try
            {
                string[] lines = File.ReadAllLines(cacheFile, Encoding.UTF8);
                foreach (string line in lines)
                {
                    if (string.IsNullOrEmpty(line)) continue;
                    string[] parts = line.Split('|');
                    if (parts[0] == "EXT" && parts.Length >= 5)
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

                    if (parts.Length < 5) continue;

                    string kind = parts[0];
                    string layer = parts[1];
                    string content = parts[2];
                    double xCad, yCad;

                    if (!double.TryParse(parts[3], NumberStyles.Any, CultureInfo.InvariantCulture, out xCad) ||
                        !double.TryParse(parts[4], NumberStyles.Any, CultureInfo.InvariantCulture, out yCad))
                    {
                        continue;
                    }

                    XYZ ptFeet = new XYZ(xCad * MM_TO_FEET, yCad * MM_TO_FEET, 0.0);

                    if (kind == "TXT")
                    {
                        Match m = sizeRegex.Match(content);
                        if (m.Success)
                        {
                            double w = double.Parse(m.Groups[1].Value);
                            double h = double.Parse(m.Groups[2].Value);
                            rawTexts.Add(new CadTextNote(content.Trim(), w, h, ptFeet, layer));
                        }
                    }
                    else if (kind == "BLK")
                    {
                        string upper = content.ToUpperInvariant();
                        string vanType = null;
                        if (upper.Contains("VCD")) vanType = "VCD";
                        else if (upper.Contains("MFD")) vanType = "MFD";
                        else if (upper.Contains("NRD")) vanType = "NRD";

                        if (vanType != null)
                        {
                            rawDampers.Add(new CadDamperItem(vanType, ptFeet, 0, 0, XYZ.BasisX));
                        }
                    }
                }

                // Kiểm tra độ lệch Center-to-Center vs Origin-to-Origin trong Revit Symbol Geometry
                XYZ symbolOffsetFeet = ComputeSymbolOffsetFeet(cadLink, hasExtents, minCadX, minCadY, maxCadX, maxCadY);

                foreach (CadTextNote t in rawTexts)
                {
                    XYZ symPt = t.Position + symbolOffsetFeet;
                    t.Position = totalXform.OfPoint(symPt);
                    outTexts.Add(t);
                }

                foreach (CadDamperItem d in rawDampers)
                {
                    XYZ symPt = d.Position + symbolOffsetFeet;
                    d.Position = totalXform.OfPoint(symPt);
                    outDampers.Add(d);
                }
            }
            catch { }
        }

        private static XYZ ComputeSymbolOffsetFeet(
            ImportInstance cadLink,
            bool hasCadExt,
            double minCadX, double minCadY, double maxCadX, double maxCadY)
        {
            if (!hasCadExt || cadLink == null) return XYZ.Zero;
            try
            {
                Options opt = new Options();
                opt.IncludeNonVisibleObjects = false;
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
                                    ((minCadX + maxCadX) * 0.5) * MM_TO_FEET,
                                    ((minCadY + maxCadY) * 0.5) * MM_TO_FEET,
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

        private static string GetCacheFilePath(string dwgPath)
        {
            try
            {
                FileInfo fi = new FileInfo(dwgPath);
                string key = string.Format("{0}_{1}_{2}_v2", fi.FullName.ToLowerInvariant(), fi.Length, fi.LastWriteTimeUtc.Ticks);
                using (MD5 md5 = MD5.Create())
                {
                    byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(key));
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
                    string folder = Path.Combine(Path.GetTempPath(), "HDTOOL_CadCache");
                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                    return Path.Combine(folder, "dwg_" + sb.ToString() + ".txt");
                }
            }
            catch
            {
                return Path.Combine(Path.GetTempPath(), "hdtool_dwg_cache.txt");
            }
        }

        private static void RunAcCoreExtraction(string accoreExe, string dwgPath, string cacheOutputFile)
        {
            string tempDwg = null;
            string scriptPath = null;
            try
            {
                // Copy sang file tạm tên ASCII để tránh khóa file của Revit hoặc lỗi Unicode path
                tempDwg = Path.Combine(Path.GetTempPath(), "hdtool_src_" + Guid.NewGuid().ToString("N") + ".dwg");
                File.Copy(dwgPath, tempDwg, true);

                scriptPath = Path.Combine(Path.GetTempPath(), "hdtool_ex_" + Guid.NewGuid().ToString("N") + ".scr");
                string outPathLisp = cacheOutputFile.Replace('\\', '/');

                string lisp = string.Format(CultureInfo.InvariantCulture,
@"(defun hd-clean (s)
  (if (not s) """"
    (progn (setq s (vl-string-translate ""\r\n\t|"" ""    "" s)) s)
  )
)
(defun hd-strip-mtext (s / res i ch next)
  (setq res """" i 1)
  (while (<= i (strlen s))
    (setq ch (substr s i 1))
    (cond
      ((= ch ""{"" ) nil)
      ((= ch ""}"" ) nil)
      ((= ch ""\\"" )
        (if (< i (strlen s))
          (progn
            (setq next (substr s (1+ i) 1))
            (if (or (= next ""P"") (= next ""p""))
              (progn (setq res (strcat res "" "")) (setq i (1+ i)))
              (if (wcmatch next ""A,C,F,H,Q,T,W,f,p"")
                (progn
                  (while (and (< i (strlen s)) (/= (substr s i 1) "";"")) (setq i (1+ i)))
                )
                (setq i (1+ i))
              )
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
(defun c:HDEX (/ f ss i ent ed typ txt pt bname lay extMin extMax)
  (setq f (open ""{0}"" ""w""))
  (if f
    (progn
      (setq extMin (getvar ""EXTMIN"") extMax (getvar ""EXTMAX""))
      (if (and extMin extMax)
        (write-line (strcat ""EXT|"" (rtos (car extMin) 2 4) ""|"" (rtos (cadr extMin) 2 4) ""|"" (rtos (car extMax) 2 4) ""|"" (rtos (cadr extMax) 2 4)) f)
      )
      (if (setq ss (ssget ""X"" '((0 . ""TEXT,MTEXT,INSERT"") (410 . ""Model""))))
        (progn
          (setq i 0)
          (while (< i (sslength ss))
            (setq ent (ssname ss i) ed (entget ent) typ (cdr (assoc 0 ed)))
            (setq lay (cdr (assoc 8 ed)) pt (cdr (assoc 10 ed)))
            (cond
              ((= typ ""TEXT"")
                (setq txt (cdr (assoc 1 ed)))
                (if (and txt pt) (write-line (strcat ""TXT|"" (hd-clean lay) ""|"" (hd-clean txt) ""|"" (rtos (car pt) 2 4) ""|"" (rtos (cadr pt) 2 4)) f))
              )
              ((= typ ""MTEXT"")
                (setq txt (strcat (cond ((cdr (assoc 3 ed))) ("""")) (cond ((cdr (assoc 1 ed))) (""""))))
                (if (and txt pt) (write-line (strcat ""TXT|"" (hd-clean lay) ""|"" (hd-clean (hd-strip-mtext txt)) ""|"" (rtos (car pt) 2 4) ""|"" (rtos (cadr pt) 2 4)) f))
              )
              ((= typ ""INSERT"")
                (setq bname (cdr (assoc 2 ed)))
                (if (and bname pt) (write-line (strcat ""BLK|"" (hd-clean lay) ""|"" (hd-clean bname) ""|"" (rtos (car pt) 2 4) ""|"" (rtos (cadr pt) 2 4)) f))
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
_.QUIT _Y
", outPathLisp);

                File.WriteAllText(scriptPath, lisp, Encoding.ASCII);

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = accoreExe;
                psi.Arguments = string.Format("/i \"{0}\" /s \"{1}\"", tempDwg, scriptPath);
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.WindowStyle = ProcessWindowStyle.Hidden;

                using (Process p = Process.Start(psi))
                {
                    p.WaitForExit(20000);
                }
            }
            catch { }
            finally
            {
                try { if (!string.IsNullOrEmpty(scriptPath) && File.Exists(scriptPath)) File.Delete(scriptPath); } catch { }
                try { if (!string.IsNullOrEmpty(tempDwg) && File.Exists(tempDwg)) File.Delete(tempDwg); } catch { }
            }
        }
    }
}
