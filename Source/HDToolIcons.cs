using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Drawing.Color;
using Pen = System.Drawing.Pen;
using SolidBrush = System.Drawing.SolidBrush;
using Font = System.Drawing.Font;
using FontFamily = System.Drawing.FontFamily;
using DashStyle = System.Drawing.Drawing2D.DashStyle;

namespace HDTool
{
    /// <summary>
    /// Bộ icon nét kỹ thuật của HDTOOL (đồng bộ với PreviewStudio/HDTool_ThemeStudio.html):
    /// nét mực 1.6 đơn vị trên lưới 32 + đúng 1 điểm nhấn màu Accent. Vẽ vector bằng GDI+ nên sắc nét ở mọi DPI.
    /// </summary>
    public static class HDToolIcons
    {
        /// <summary>Màu nét mực trên nền sáng.</summary>
        public static Color InkLight { get { return Color.FromArgb(30, 53, 80); } }
        /// <summary>Màu nét mực trên nền tối (Revit giao diện Dark).</summary>
        public static Color InkDark { get { return Color.FromArgb(215, 227, 239); } }
        public static Color AccentLight { get { return Color.FromArgb(11, 120, 166); } }
        public static Color AccentDark { get { return Color.FromArgb(43, 184, 232); } }

        // ===================== ICON CHO RIBBON REVIT =====================
        /// <summary>
        /// Tạo ImageSource cho Ribbon (size = 16 hoặc 32 DIP). Ảnh được vẽ ở độ phân giải thật của màn hình
        /// và gắn DPI tương ứng nên Revit hiển thị sắc nét ở 125% / 150% / 200%.
        /// </summary>
        public static ImageSource Ribbon(string key, int size)
        {
            float scale = 1f;
            try
            {
                using (Graphics sg = Graphics.FromHwnd(IntPtr.Zero)) scale = Math.Max(1f, sg.DpiX / 96f);
            }
            catch { }

            bool dark = IsRevitDarkTheme();
            Color ink = dark ? InkDark : InkLight;
            Color acc = HDToolTheme.ToolColor(key); // điểm nhấn = màu nhận diện riêng của từng công cụ
            if (dark) acc = HDToolTheme.Mix(acc, Color.White, 0.2);

            int px = Math.Max(size, (int)Math.Round(size * scale));
            using (Bitmap bmp = new Bitmap(px, px, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                bmp.SetResolution(96f * px / size, 96f * px / size);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    Draw(g, key, new RectangleF(0, 0, px, px), ink, acc);
                }
                return ToImageSource(bmp);
            }
        }

        private static bool IsRevitDarkTheme()
        {
            try
            {
                return Autodesk.Revit.UI.UIThemeManager.CurrentTheme == Autodesk.Revit.UI.UITheme.Dark;
            }
            catch
            {
                return false;
            }
        }

        private static ImageSource ToImageSource(Bitmap bitmap)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Position = 0;
                BitmapImage img = new BitmapImage();
                img.BeginInit();
                img.StreamSource = ms;
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.EndInit();
                img.Freeze();
                return img;
            }
        }

        // ===================== VẼ VECTOR TRÊN LƯỚI 32 =====================
        private class Ctx
        {
            public Graphics G;
            public float S, X, Y;
            public Color Ink, Acc;
            public float Px;
            public PointF P(float x, float y) { return new PointF(X + x * S, Y + y * S); }
            public float W(float w) { return Math.Max(1f, w * S); }
        }

        private static Pen MakePen(Ctx c, Color col, float w)
        {
            Pen p = new Pen(col, c.W(w));
            p.LineJoin = LineJoin.Miter;
            p.StartCap = LineCap.Flat;
            p.EndCap = LineCap.Flat;
            return p;
        }

        private static void Line(Ctx c, Color col, float w, float x1, float y1, float x2, float y2)
        {
            using (Pen p = MakePen(c, col, w)) c.G.DrawLine(p, c.P(x1, y1), c.P(x2, y2));
        }

        private static void Dashed(Ctx c, Color col, float w, float[] dash, float x1, float y1, float x2, float y2)
        {
            using (Pen p = MakePen(c, col, w))
            {
                float pw = c.W(w);
                float[] d = new float[dash.Length];
                for (int i = 0; i < dash.Length; i++) d[i] = Math.Max(0.5f, dash[i] * c.S / pw);
                p.DashPattern = d;
                c.G.DrawLine(p, c.P(x1, y1), c.P(x2, y2));
            }
        }

        private static void Poly(Ctx c, Color col, float w, bool closed, params float[] xy)
        {
            PointF[] pts = new PointF[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = c.P(xy[i * 2], xy[i * 2 + 1]);
            using (Pen p = MakePen(c, col, w))
            {
                if (closed) c.G.DrawPolygon(p, pts);
                else c.G.DrawLines(p, pts);
            }
        }

        private static void Fill(Ctx c, Color col, params float[] xy)
        {
            PointF[] pts = new PointF[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = c.P(xy[i * 2], xy[i * 2 + 1]);
            using (SolidBrush b = new SolidBrush(col)) c.G.FillPolygon(b, pts);
        }

        private static void Rect(Ctx c, Color col, float w, float x, float y, float rw, float rh)
        {
            using (Pen p = MakePen(c, col, w))
            {
                PointF a = c.P(x, y);
                c.G.DrawRectangle(p, a.X, a.Y, rw * c.S, rh * c.S);
            }
        }

        private static void FillRect(Ctx c, Color col, float x, float y, float rw, float rh)
        {
            using (SolidBrush b = new SolidBrush(col))
            {
                PointF a = c.P(x, y);
                c.G.FillRectangle(b, a.X, a.Y, rw * c.S, rh * c.S);
            }
        }

        private static void Circle(Ctx c, Color col, float w, float cx, float cy, float r)
        {
            using (Pen p = MakePen(c, col, w))
            {
                PointF a = c.P(cx - r, cy - r);
                c.G.DrawEllipse(p, a.X, a.Y, 2 * r * c.S, 2 * r * c.S);
            }
        }

        private static void Arc(Ctx c, Color col, float w, float cx, float cy, float r, float start, float sweep)
        {
            using (Pen p = MakePen(c, col, w))
            {
                PointF a = c.P(cx - r, cy - r);
                c.G.DrawArc(p, a.X, a.Y, 2 * r * c.S, 2 * r * c.S, start, sweep);
            }
        }

        private static void Txt(Ctx c, Color col, string s, float size, float cx, float cy)
        {
            if (c.Px < 20) return; // icon 16px: bỏ chữ để không nhòe
            float fs = Math.Max(5f, size * c.S);
            using (Font f = new Font("Consolas", fs, FontStyle.Bold, GraphicsUnit.Pixel))
            using (SolidBrush b = new SolidBrush(col))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                c.G.DrawString(s, f, b, c.P(cx, cy), sf);
            }
        }

        /// <summary>Vẽ icon theo khóa vào vùng r (vuông). Khóa: align, grid, level, crop, pipe, duct, cad2duct, sheetlist, sheetsync, layers, family, update, menu.</summary>
        public static void Draw(Graphics g, string key, RectangleF r, Color ink, Color acc)
        {
            float side = Math.Min(r.Width, r.Height);
            Ctx c = new Ctx();
            c.G = g;
            // Icon nhỏ (nút bấm) không có điểm nhấn: nét cùng màu với chữ của nút
            c.S = side / 32f;
            c.X = r.X + (r.Width - side) / 2f;
            c.Y = r.Y + (r.Height - side) / 2f;
            c.Ink = ink;
            c.Acc = acc;
            c.Px = side;
            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            try
            {
                switch ((key ?? "").ToLowerInvariant())
                {
                    case "align": DrawAlign(c); break;
                    case "grid": DrawGrid(c); break;
                    case "level": DrawLevel(c); break;
                    case "crop": DrawCrop(c); break;
                    case "pipe": DrawPipe(c); break;
                    case "duct": DrawDuct(c); break;
                    case "cad2duct": DrawCadToDuct(c); break;
                    case "sheetlist": DrawSheetList(c); break;
                    case "sheetsync": DrawSheetSync(c); break;
                    case "layers": DrawLayers(c); break;
                    case "family": DrawFamily(c); break;
                    case "update": DrawUpdate(c); break;
                    case "pick": DrawPick(c); break;
                    case "plus": DrawPlus(c); break;
                    case "refresh": DrawRefresh(c); break;
                    case "light": DrawLight(c, false); break;
                    case "lightoff": DrawLight(c, true); break;
                    case "zoom": DrawZoom(c); break;
                    case "col": DrawCol(c); break;
                    case "copy": DrawCopy(c); break;
                    case "xls": DrawXls(c); break;
                    case "sheet": DrawSheet(c); break;
                    case "area": DrawArea(c); break;
                    case "ruler": DrawRuler(c); break;
                    case "hash": DrawHash(c); break;
                    case "valve": DrawValve(c); break;
                    default: DrawMenu(c); break;
                }
            }
            finally
            {
                g.SmoothingMode = old;
            }
        }

        private static void DrawAlign(Ctx c)
        {
            Circle(c, c.Ink, 1.6f, 9, 6, 3.2f);
            Circle(c, c.Ink, 1.6f, 21, 6, 3.2f);
            Line(c, c.Ink, 1.6f, 9, 9.2f, 9, 27);
            Line(c, c.Ink, 1.6f, 21, 9.2f, 21, 27);
            Dashed(c, c.Ink, 1.6f, new float[] { 3, 2 }, 3, 19, 29, 19);
            Fill(c, c.Acc, 27, 15.5f, 30, 19, 24, 19);
            Line(c, c.Acc, 1.4f, 5, 23, 27, 23);
            Line(c, c.Acc, 1.4f, 5, 21.5f, 5, 24.5f);
            Line(c, c.Acc, 1.4f, 27, 21.5f, 27, 24.5f);
        }

        private static void DrawGrid(Ctx c)
        {
            Circle(c, c.Ink, 1.6f, 10, 6, 3.4f);
            Circle(c, c.Ink, 1.6f, 22, 6, 3.4f);
            Dashed(c, c.Ink, 1.6f, new float[] { 5, 2, 1.5f, 2 }, 10, 9.4f, 10, 29);
            Dashed(c, c.Ink, 1.6f, new float[] { 5, 2, 1.5f, 2 }, 22, 9.4f, 22, 29);
            Line(c, c.Acc, 1.5f, 4, 24, 28, 24);
            Line(c, c.Acc, 1.5f, 4, 22, 4, 26);
            Line(c, c.Acc, 1.5f, 28, 22, 28, 26);
        }

        private static void DrawLevel(Ctx c)
        {
            Fill(c, c.Ink, 3, 10, 8, 6.5f, 8, 13.5f);
            Line(c, c.Ink, 1.6f, 8, 10, 29, 10);
            Fill(c, c.Acc, 3, 22, 8, 18.5f, 8, 25.5f);
            Line(c, c.Acc, 1.6f, 8, 22, 29, 22);
            Line(c, c.Ink, 1.2f, 24, 11.5f, 24, 20.5f);
            Line(c, c.Ink, 1.2f, 22, 11.5f, 26, 11.5f);
            Line(c, c.Ink, 1.2f, 22, 20.5f, 26, 20.5f);
        }

        private static void DrawCrop(Ctx c)
        {
            Rect(c, c.Ink, 1.6f, 11, 11, 10, 10);
            Poly(c, c.Acc, 2f, false, 4, 10, 4, 4, 10, 4);
            Poly(c, c.Acc, 2f, false, 22, 4, 28, 4, 28, 10);
            Poly(c, c.Acc, 2f, false, 28, 22, 28, 28, 22, 28);
            Poly(c, c.Acc, 2f, false, 10, 28, 4, 28, 4, 22);
            using (Pen p = MakePen(c, c.Ink, 1f))
            {
                p.DashPattern = new float[] { 2f, 2f };
                PointF a = c.P(7, 7);
                c.G.DrawRectangle(p, a.X, a.Y, 18 * c.S, 18 * c.S);
            }
        }

        private static void DrawPipe(Ctx c)
        {
            Line(c, c.Ink, 1.6f, 3, 9, 15, 9);
            Arc(c, c.Ink, 1.6f, 15, 15, 6, 270, 90);
            Line(c, c.Ink, 1.6f, 21, 15, 21, 29);
            Line(c, c.Ink, 1.6f, 3, 15, 15, 15);
            Line(c, c.Ink, 1.6f, 15, 15, 15, 29);
            Line(c, c.Ink, 1.6f, 3, 7, 3, 17);
            Line(c, c.Ink, 1.6f, 13, 29, 23, 29);
            Poly(c, c.Acc, 1.8f, false, 30, 4, 23, 4, 26.5f, 8, 23, 12, 30, 12);
        }

        private static void DrawDuct(Ctx c)
        {
            Poly(c, c.Ink, 1.6f, true, 3, 12, 12, 7, 29, 7, 20, 12);
            Poly(c, c.Ink, 1.6f, false, 3, 12, 3, 24, 20, 24, 20, 12);
            Poly(c, c.Ink, 1.6f, false, 20, 24, 29, 19, 29, 7);
            Line(c, c.Ink, 1f, 8, 12, 8, 24);
            Line(c, c.Ink, 1f, 14, 12, 14, 24);
            if (c.Px >= 20) Txt(c, c.Acc, "m²", 7.5f, 16, 28.6f);
            else Line(c, c.Acc, 2f, 3, 28, 20, 28);
        }

        private static void DrawCadToDuct(Ctx c)
        {
            Dashed(c, c.Ink, 1.4f, new float[] { 2.5f, 1.8f }, 3, 7, 14, 7);
            Dashed(c, c.Ink, 1.4f, new float[] { 2.5f, 1.8f }, 3, 13, 14, 13);
            Poly(c, c.Acc, 1.6f, false, 8, 16, 8, 21, 14, 21);
            Poly(c, c.Acc, 1.6f, false, 12, 19, 14, 21, 12, 23);
            Poly(c, c.Ink, 1.6f, true, 16, 20, 21, 17, 30, 17, 25, 20);
            Poly(c, c.Ink, 1.6f, false, 16, 20, 16, 28, 25, 28, 25, 20);
            Poly(c, c.Ink, 1.6f, false, 25, 28, 30, 25, 30, 17);
            Rect(c, c.Acc, 1.4f, 17, 3, 11, 8);
            Txt(c, c.Acc, "DWG", 5.4f, 22.5f, 7.1f);
        }

        private static void DrawSheetBase(Ctx c, bool thirdLine)
        {
            Rect(c, c.Ink, 1.6f, 4, 3, 16, 26);
            Line(c, c.Ink, 1.2f, 4, 23, 20, 23);
            Line(c, c.Ink, 1.2f, 12, 23, 12, 29);
            Line(c, c.Ink, 1.2f, 8, 8, 16, 8);
            Line(c, c.Ink, 1.2f, 8, 12, 16, 12);
            if (thirdLine) Line(c, c.Ink, 1.2f, 8, 16, 13, 16);
        }

        private static void DrawSheetList(Ctx c)
        {
            DrawSheetBase(c, true);
            Line(c, c.Acc, 1.8f, 20, 15, 29, 15);
            Poly(c, c.Acc, 1.8f, false, 25, 11, 29, 15, 25, 19);
        }

        private static void DrawSheetSync(Ctx c)
        {
            DrawSheetBase(c, false);
            Arc(c, c.Acc, 1.7f, 24, 19, 6, 300, 150);
            Arc(c, c.Acc, 1.7f, 24, 19, 6, 120, 150);
            Poly(c, c.Acc, 1.7f, false, 28.5f, 11.5f, 28.5f, 15.3f, 24.7f, 15.3f);
            Poly(c, c.Acc, 1.7f, false, 19.5f, 26.5f, 19.5f, 22.7f, 23.3f, 22.7f);
        }

        private static void DrawLayers(Ctx c)
        {
            Poly(c, c.Ink, 1.6f, true, 16, 4, 29, 10, 16, 16, 3, 10);
            Poly(c, c.Ink, 1.6f, false, 3, 16, 16, 22, 29, 16);
            Poly(c, c.Acc, 1.8f, false, 3, 22, 16, 28, 29, 22);
        }

        private static void DrawFamily(Ctx c)
        {
            Fill(c, Color.FromArgb(90, c.Acc), 16, 17, 28, 10, 28, 22, 16, 29);
            Poly(c, c.Ink, 1.6f, true, 16, 3, 28, 10, 28, 22, 16, 29, 4, 22, 4, 10);
            Poly(c, c.Ink, 1.6f, false, 4, 10, 16, 17, 28, 10);
            Line(c, c.Ink, 1.6f, 16, 17, 16, 29);
            Line(c, c.Acc, 1.8f, 16, 17, 28, 10);
        }

        private static void DrawUpdate(Ctx c)
        {
            Arc(c, c.Ink, 1.6f, 16, 16, 11, 0, 315);
            Poly(c, c.Ink, 1.6f, false, 24, 3, 24, 8.5f, 18.5f, 8.5f);
            Line(c, c.Acc, 1.9f, 16, 10, 16, 20);
            Poly(c, c.Acc, 1.9f, false, 11.5f, 16, 16, 20.5f, 20.5f, 16);
        }

        // ---------- Icon nhỏ cho nút bấm (vẽ trên lưới 32, nét dày hơn để rõ ở 16px) ----------
        private static void DrawPick(Ctx c)
        {
            Circle(c, c.Ink, 2.8f, 16, 16, 10);
            Line(c, c.Ink, 2.8f, 16, 2, 16, 10); Line(c, c.Ink, 2.8f, 16, 22, 16, 30);
            Line(c, c.Ink, 2.8f, 2, 16, 10, 16); Line(c, c.Ink, 2.8f, 22, 16, 30, 16);
        }

        private static void DrawPlus(Ctx c)
        {
            Line(c, c.Ink, 3.2f, 16, 6, 16, 26); Line(c, c.Ink, 3.2f, 6, 16, 26, 16);
        }

        private static void DrawRefresh(Ctx c)
        {
            Arc(c, c.Ink, 2.8f, 16, 16, 11, 300, 280);
            Poly(c, c.Ink, 2.8f, false, 25, 3, 25, 9.5f, 18.5f, 9.5f);
        }

        private static void DrawLight(Ctx c, bool off)
        {
            Poly(c, c.Ink, 2.6f, false, 11, 22, 11, 18.5f, 8, 14, 8, 11);
            Arc(c, c.Ink, 2.6f, 16, 11, 8, 180, 180);
            Poly(c, c.Ink, 2.6f, false, 24, 11, 24, 14, 21, 18.5f, 21, 22);
            Line(c, c.Ink, 2.6f, 11, 22, 21, 22); Line(c, c.Ink, 2.6f, 12, 27, 20, 27);
            if (off) Line(c, c.Ink, 2.6f, 4, 4, 28, 28);
        }

        private static void DrawZoom(Ctx c)
        {
            Circle(c, c.Ink, 2.8f, 14, 14, 9);
            Line(c, c.Ink, 3f, 21, 21, 28, 28);
            Line(c, c.Ink, 2.4f, 10, 14, 18, 14); Line(c, c.Ink, 2.4f, 14, 10, 14, 18);
        }

        private static void DrawCol(Ctx c)
        {
            Rect(c, c.Ink, 2.6f, 4, 6, 24, 20);
            Line(c, c.Ink, 2.4f, 12, 6, 12, 26); Line(c, c.Ink, 2.4f, 20, 6, 20, 26);
        }

        private static void DrawCopy(Ctx c)
        {
            Rect(c, c.Ink, 2.6f, 10, 10, 17, 18);
            Poly(c, c.Ink, 2.6f, false, 6, 22, 6, 5, 21, 5);
        }

        private static void DrawXls(Ctx c)
        {
            Poly(c, c.Ink, 2.6f, true, 6, 3, 20, 3, 26, 9, 26, 29, 6, 29);
            Line(c, c.Ink, 2.6f, 11, 15, 21, 24); Line(c, c.Ink, 2.6f, 21, 15, 11, 24);
        }

        private static void DrawSheet(Ctx c)
        {
            Rect(c, c.Ink, 2.6f, 3, 6, 26, 20);
            Poly(c, c.Ink, 2.4f, false, 19, 26, 19, 19, 29, 19);
        }

        private static void DrawArea(Ctx c)
        {
            Poly(c, c.Ink, 2.4f, true, 4, 10, 16, 4, 28, 10, 16, 16);
            Poly(c, c.Ink, 2.4f, false, 4, 10, 4, 22, 16, 28, 28, 22, 28, 10);
            Line(c, c.Ink, 2.4f, 16, 16, 16, 28);
        }

        private static void DrawRuler(Ctx c)
        {
            Poly(c, c.Ink, 2.4f, true, 4, 22, 22, 4, 28, 10, 10, 28);
            Line(c, c.Ink, 2f, 9, 17, 12, 20); Line(c, c.Ink, 2f, 13, 13, 16, 16); Line(c, c.Ink, 2f, 17, 9, 20, 12);
        }

        private static void DrawHash(Ctx c)
        {
            Line(c, c.Ink, 2.6f, 6, 12, 28, 12); Line(c, c.Ink, 2.6f, 4, 20, 26, 20);
            Line(c, c.Ink, 2.6f, 13, 4, 10, 28); Line(c, c.Ink, 2.6f, 22, 4, 19, 28);
        }

        private static void DrawValve(Ctx c)
        {
            Poly(c, c.Ink, 2.4f, true, 4, 10, 4, 24, 16, 17);
            Poly(c, c.Ink, 2.4f, true, 28, 10, 28, 24, 16, 17);
            Line(c, c.Ink, 2.4f, 16, 17, 16, 7); Line(c, c.Ink, 2.4f, 11, 7, 21, 7);
        }

        private static void DrawMenu(Ctx c)
        {
            Rect(c, c.Ink, 1.6f, 4, 4, 10, 10);
            Rect(c, c.Ink, 1.6f, 18, 18, 10, 10);
            Rect(c, c.Ink, 1.6f, 4, 18, 10, 10);
            FillRect(c, c.Acc, 18, 4, 10, 10);
        }
    }
}
