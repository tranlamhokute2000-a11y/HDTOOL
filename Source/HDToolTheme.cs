using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace HDTool
{
    /// <summary>Mac = Mac Sáng (mặc định, Sếp Hồ chốt 2026-11-27), Premium = Tối Cao Cấp, Drafting = Bản Vẽ Sáng, Blueprint = Kỹ thuật tối.</summary>
    public enum HDToolThemeMode { Premium, Drafting, Blueprint, Mac }

    public enum HDToolButtonKind { Primary, Secondary, Ghost, Danger }

    /// <summary>
    /// Bộ màu, font và bộ "khoác áo" giao diện dùng chung cho toàn bộ Form HDTOOL.
    /// Nguồn: PreviewStudio/HDTool_GiaoDienMoi.html (Sếp Hồ chốt phong cách Tối Cao Cấp).
    /// HDToolBaseForm tự gọi ApplyTo(form) khi Form tải, nên mọi Form kế thừa đều đổi giao diện đồng bộ.
    /// </summary>
    public static class HDToolTheme
    {
        public static HDToolThemeMode Mode = HDToolThemeMode.Mac;
        public static bool IsMac { get { return Mode == HDToolThemeMode.Mac; } }
        public static bool IsDark { get { return Mode != HDToolThemeMode.Drafting && Mode != HDToolThemeMode.Mac; } }
        // IsPremium = kiểu hiện đại bo góc (Tối Cao Cấp hoặc Mac Sáng)
        public static bool IsPremium { get { return Mode == HDToolThemeMode.Premium || Mode == HDToolThemeMode.Mac; } }

        private static Color Pick(Color premium, Color blueprint, Color drafting)
        {
            return Mode == HDToolThemeMode.Premium ? premium : (Mode == HDToolThemeMode.Blueprint ? blueprint : drafting);
        }
        private static Color C(int r, int g, int b) { return Color.FromArgb(r, g, b); }
        private static Color Pm(Color mac, Color premium, Color blueprint, Color drafting)
        {
            return Mode == HDToolThemeMode.Mac ? mac : Pick(premium, blueprint, drafting);
        }

        // ===================== MÀU (TOKEN) =====================
        public static Color Bg           { get { return Pm(C(245, 245, 247), C(14, 16, 22), C(13, 21, 32), C(237, 241, 245)); } }
        public static Color Surface      { get { return Pm(C(255, 255, 255), C(22, 26, 35), C(18, 28, 41), C(255, 255, 255)); } }
        public static Color Surface2     { get { return Pm(C(242, 242, 245), C(27, 32, 48), C(23, 36, 51), C(245, 247, 250)); } }
        public static Color SurfaceHover { get { return Pm(C(236, 236, 240), C(30, 36, 52), C(26, 40, 57), C(240, 244, 249)); } }
        public static Color Header       { get { return Pm(C(236, 236, 238), C(31, 37, 87), C(10, 17, 26), C(16, 35, 58)); } }
        public static Color HeaderGrad1  { get { return Pm(C(240, 240, 242), C(20, 26, 51), C(10, 17, 26), C(16, 35, 58)); } }
        public static Color HeaderGrad2  { get { return Pm(C(236, 236, 238), C(31, 37, 87), C(10, 17, 26), C(16, 35, 58)); } }
        public static Color HeaderGrad3  { get { return Pm(C(232, 232, 236), C(58, 31, 99), C(10, 17, 26), C(16, 35, 58)); } }
        public static Color HeaderText   { get { return Pm(C(29, 29, 31), C(255, 255, 255), C(232, 241, 250), C(242, 246, 250)); } }
        public static Color HeaderSub    { get { return Pm(C(110, 110, 115), C(184, 188, 210), C(157, 176, 196), C(157, 176, 196)); } }
        public static Color HeaderLine   { get { return Pm(C(214, 214, 219), C(70, 74, 120), C(47, 72, 98), C(44, 70, 100)); } }
        public static Color HeaderAccent { get { return Pm(C(0, 122, 255), C(125, 163, 255), C(43, 184, 232), C(43, 184, 232)); } }
        public static Color Line         { get { return Pm(C(224, 224, 229), C(38, 44, 61), C(34, 52, 74), C(211, 219, 228)); } }
        public static Color LineStrong   { get { return Pm(C(200, 200, 206), C(53, 60, 82), C(47, 72, 98), C(183, 195, 208)); } }
        public static Color Text         { get { return Pm(C(29, 29, 31), C(236, 238, 245), C(227, 235, 243), C(14, 28, 43)); } }
        public static Color Text2        { get { return Pm(C(72, 72, 78), C(170, 177, 197), C(157, 176, 196), C(70, 88, 107)); } }
        public static Color Text3        { get { return Pm(C(134, 134, 139), C(113, 122, 147), C(102, 121, 143), C(122, 137, 153)); } }
        public static Color Accent       { get { return Pm(C(0, 122, 255), C(91, 140, 255), C(43, 184, 232), C(11, 120, 166)); } }
        public static Color AccentHover  { get { return Pm(C(30, 138, 255), C(125, 163, 255), C(82, 200, 240), C(10, 141, 194)); } }
        public static Color AccentDown   { get { return Pm(C(0, 100, 215), C(70, 115, 230), C(30, 150, 192), C(8, 96, 133)); } }
        public static Color AccentInk    { get { return Pm(C(255, 255, 255), C(10, 15, 31), C(4, 32, 44), C(255, 255, 255)); } }
        public static Color AccentSoft   { get { return Pm(C(229, 240, 255), C(32, 43, 68), C(19, 58, 82), C(217, 238, 247)); } }
        public static Color Ok           { get { return Pm(C(40, 167, 69), C(52, 211, 153), C(61, 214, 140), C(30, 158, 98)); } }
        public static Color Warn         { get { return Pm(C(214, 122, 0), C(251, 191, 36), C(242, 179, 61), C(183, 121, 31)); } }
        public static Color Err          { get { return Pm(C(230, 50, 40), C(248, 113, 113), C(242, 102, 94), C(200, 66, 59)); } }
        public static Color OkSoft       { get { return Pm(C(230, 247, 235), C(26, 50, 50), C(18, 52, 40), C(226, 244, 235)); } }
        public static Color WarnSoft     { get { return Pm(C(255, 244, 226), C(52, 47, 35), C(56, 44, 18), C(252, 243, 224)); } }
        public static Color ErrSoft      { get { return Pm(C(255, 235, 233), C(51, 37, 45), C(60, 26, 26), C(251, 231, 229)); } }
        public static Color RowAlt       { get { return Pm(C(248, 248, 250), C(24, 28, 39), C(21, 33, 50), C(246, 249, 252)); } }
        public static Color Input        { get { return Pm(C(255, 255, 255), C(18, 22, 32), C(15, 24, 35), C(255, 255, 255)); } }
        public static Color GridMinor    { get { return Pm(Color.FromArgb(0, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), Color.FromArgb(13, 43, 184, 232), Color.FromArgb(10, 11, 120, 166)); } }
        public static Color GridMajor    { get { return Pm(Color.FromArgb(0, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), Color.FromArgb(26, 43, 184, 232), Color.FromArgb(20, 11, 120, 166)); } }

        /// <summary>Bán kính bo góc (px @ 96 DPI). Kiểu Kỹ thuật dùng góc vuông.</summary>
        public static int Radius { get { return IsPremium ? 10 : 0; } }
        public static int RadiusSm { get { return IsPremium ? 7 : 0; } }

        // Kích thước bố cục (px @ 96 DPI, tự nhân theo DPI qua AutoScaleMode.Dpi)
        public const int HeaderHeight = 58;
        public const int ToolbarHeight = 46;
        public const int StatusHeight = 52;
        public const int RowHeight = 28;
        public const int ColumnHeaderHeight = 32;

        // ===================== FONT: chỉ Segoe UI (chữ) và Consolas (số, mã, kích thước) =====================
        public static readonly Font Hero       = new Font("Segoe UI", 12.5f, FontStyle.Bold);
        public static readonly Font Title      = new Font("Segoe UI", 12f, FontStyle.Bold);
        public static readonly Font CardTitle  = new Font("Segoe UI", 10f, FontStyle.Bold);
        public static readonly Font Body       = new Font("Segoe UI", 9f, FontStyle.Regular);
        public static readonly Font BodyBold   = new Font("Segoe UI", 9f, FontStyle.Bold);
        public static readonly Font Caption    = new Font("Segoe UI", 8.5f, FontStyle.Regular);
        public static readonly Font LabelFont  = new Font("Segoe UI", 8f, FontStyle.Bold);
        public static readonly Font GridHeader = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        public static readonly Font Mono       = new Font("Consolas", 9f, FontStyle.Regular);
        public static readonly Font MonoSmall  = new Font("Consolas", 8f, FontStyle.Regular);
        public static readonly Font Readout    = new Font("Consolas", 16f, FontStyle.Regular);

        // ===================== MÀU NHẬN DIỆN TỪNG CÔNG CỤ (dùng chung Ribbon, Menu, đầu cửa sổ) =====================
        public static Color ToolColor(string iconKey)
        {
            switch ((iconKey ?? "").ToLowerInvariant())
            {
                case "grid": return C(37, 99, 235);
                case "level": return C(14, 165, 233);
                case "crop": return C(124, 58, 237);
                case "align": return C(99, 102, 241);
                case "pipe": return C(8, 145, 178);
                case "duct": return C(234, 88, 12);
                case "cad2duct": return C(219, 39, 119);
                case "sheetlist": return C(22, 163, 74);
                case "sheetsync": return C(5, 150, 105);
                case "layers": return C(217, 119, 6);
                case "family": return C(192, 38, 211);
                case "update": return C(100, 116, 139);
                default: return C(91, 140, 255);
            }
        }

        // ===================== MÀU HỆ THỐNG ỐNG GIÓ =====================
        public static Color SystemColor(string code)
        {
            switch ((code ?? "").ToUpperInvariant())
            {
                case "SA": return Pick(C(91, 140, 255), C(43, 184, 232), C(11, 120, 166));
                case "RA": return Pick(C(52, 211, 153), C(61, 214, 140), C(30, 158, 98));
                case "EA": return Pick(C(251, 191, 36), C(242, 179, 61), C(183, 121, 31));
                case "OA": return Pick(C(167, 139, 250), C(180, 140, 242), C(122, 79, 201));
                default: return Text2;
            }
        }

        private static readonly Regex RxSA = new Regex(@"(^|[^A-Z])SA([^A-Z]|$)|SUPPLY|CẤP", RegexOptions.CultureInvariant);
        private static readonly Regex RxRA = new Regex(@"(^|[^A-Z])RA([^A-Z]|$)|RETURN|HỒI", RegexOptions.CultureInvariant);
        private static readonly Regex RxEA = new Regex(@"(^|[^A-Z])(EA|SEA|KEA)([^A-Z]|$)|EXHAUST|THẢI|HÚT", RegexOptions.CultureInvariant);
        private static readonly Regex RxOA = new Regex(@"(^|[^A-Z])(OA|FA)([^A-Z]|$)|OUTSIDE|OUTDOOR|FRESH|TƯƠI", RegexOptions.CultureInvariant);

        /// <summary>Nhận mã hệ gió (SA/RA/EA/OA) từ tên hệ thống trong Revit; trả về chuỗi rỗng nếu không nhận được.</summary>
        public static string SystemCodeFromName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            string u = name.ToUpperInvariant();
            if (RxEA.IsMatch(u)) return "EA";
            if (RxOA.IsMatch(u)) return "OA";
            if (RxRA.IsMatch(u)) return "RA";
            if (RxSA.IsMatch(u)) return "SA";
            return "";
        }

        // ===================== TIỆN ÍCH MÀU =====================
        public static double Lum(Color c)
        {
            return (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;
        }

        public static Color Mix(Color a, Color b, double t)
        {
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        private enum Role { Keep, Dark, Bg, Surface, Surface2, Accent, Ok, Warn, Err, AccentSoft, OkSoft, WarnSoft, ErrSoft }

        private static Role HueRole(Color c, bool soft)
        {
            float h = c.GetHue();
            if (h < 25f || h >= 330f) return soft ? Role.ErrSoft : Role.Err;
            if (h < 70f) return soft ? Role.WarnSoft : Role.Warn;
            if (h < 165f) return soft ? Role.OkSoft : Role.Ok;
            return soft ? Role.AccentSoft : Role.Accent;
        }

        private static bool IsToken(Color c)
        {
            int v = c.ToArgb();
            Color[] tokens = new Color[] { Bg, Surface, Surface2, SurfaceHover, Header, Accent, AccentHover, AccentSoft, Ok, Warn, Err, OkSoft, WarnSoft, ErrSoft, RowAlt, Input, Line, LineStrong };
            foreach (Color t in tokens) if (t.ToArgb() == v) return true;
            return false;
        }

        private static bool IsForeToken(Color c)
        {
            int v = c.ToArgb();
            Color[] tokens = new Color[] { Text, Text2, Text3, Accent, AccentInk, Ok, Warn, Err, HeaderText, HeaderSub, HeaderAccent };
            foreach (Color t in tokens) if (t.ToArgb() == v) return true;
            return false;
        }

        /// <summary>Màu xám đặt tên (Color.Gray...) được code dùng làm chữ gợi ý và so sánh bằng "==" -> không đổi.</summary>
        private static bool IsNamedGray(Color c)
        {
            if (!c.IsKnownColor) return false;
            KnownColor k = c.ToKnownColor();
            return k == KnownColor.Gray || k == KnownColor.DarkGray || k == KnownColor.DimGray || k == KnownColor.Silver ||
                   k == KnownColor.GrayText || k == KnownColor.LightGray;
        }

        private static Role ClassifyBack(Color c)
        {
            if (c.A == 0 || IsToken(c)) return Role.Keep;
            double lum = Lum(c);
            float sat = c.GetSaturation();
            if (lum < 0.30) return Role.Dark;
            if (sat > 0.35f && lum < 0.80) return HueRole(c, false);
            if (sat > 0.35f && lum < 0.97) return HueRole(c, true);
            if (lum >= 0.97) return Role.Surface;
            if (lum >= 0.90) return Role.Bg;
            return Role.Surface2;
        }

        private static Color RoleColor(Role r, Color original)
        {
            switch (r)
            {
                case Role.Dark: return Header;
                case Role.Bg: return Bg;
                case Role.Surface: return Surface;
                case Role.Surface2: return Surface2;
                case Role.Accent: return Accent;
                case Role.Ok: return Ok;
                case Role.Warn: return Warn;
                case Role.Err: return Err;
                case Role.AccentSoft: return AccentSoft;
                case Role.OkSoft: return OkSoft;
                case Role.WarnSoft: return WarnSoft;
                case Role.ErrSoft: return ErrSoft;
                default: return original;
            }
        }

        /// <summary>Đổi màu chữ cũ sang token. onHeader = chữ nằm trên dải tiêu đề.</summary>
        private static Color MapFore(Color c, bool onHeader, Color effectiveBack)
        {
            if (c.A == 0 || IsForeToken(c)) return c;
            double lum = Lum(c);
            float sat = c.GetSaturation();
            if (onHeader)
            {
                if (sat > 0.40f && lum > 0.15 && lum < 0.85) return HeaderAccent;
                return lum > 0.75 ? HeaderText : HeaderSub;
            }
            if (lum > 0.85)
            {
                // Chữ trắng: giữ trắng nếu nền tối/đậm màu, ngược lại đổi sang chữ thường
                return Lum(effectiveBack) > 0.6 ? Text : (IsDark ? Text : c);
            }
            if (sat > 0.40f && lum > 0.12 && lum < 0.85) return RoleColor(HueRole(c, false), c);
            if (lum < 0.30) return Text;
            if (lum < 0.55) return Text2;
            return Text3;
        }

        // ===================== NHẬN BIẾT THUỘC TÍNH ĐƯỢC ĐẶT TƯỜNG MINH =====================
        private static readonly MethodInfo MiShouldBack = typeof(Control).GetMethod("ShouldSerializeBackColor", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly MethodInfo MiShouldFore = typeof(Control).GetMethod("ShouldSerializeForeColor", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly PropertyInfo PiDoubleBuffered = typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);

        private static bool IsSet(MethodInfo mi, Control c)
        {
            try { return mi != null && (bool)mi.Invoke(c, null); }
            catch { return true; }
        }

        public static void SetDoubleBuffered(Control c)
        {
            try { if (PiDoubleBuffered != null) PiDoubleBuffered.SetValue(c, true, null); }
            catch { }
        }

        private static readonly ConditionalWeakTable<Control, object> Done = new ConditionalWeakTable<Control, object>();
        private static readonly ConditionalWeakTable<Control, object> HeaderPanels = new ConditionalWeakTable<Control, object>();
        private static readonly object Marker = new object();

        private static bool IsOnHeader(Control c)
        {
            object o;
            for (Control p = c.Parent; p != null; p = p.Parent)
            {
                if (HeaderPanels.TryGetValue(p, out o)) return true;
                if (p is Form) break;
            }
            return false;
        }

        // ===================== ÁP GIAO DIỆN CHO FORM =====================
        /// <summary>
        /// Khoác giao diện cho toàn bộ Form: chỉ đổi những màu được đặt tường minh (và nền mặc định của ô nhập/bảng khi dùng nền tối),
        /// giữ nguyên bố cục, sự kiện và logic. Control thêm sau (động) cũng tự được áp qua sự kiện ControlAdded.
        /// </summary>
        public static void ApplyTo(Form form)
        {
            if (form == null || form.IsDisposed) return;
            try
            {
                object o;
                if (Done.TryGetValue(form, out o)) return;
                Done.Add(form, Marker);

                Color oldBack = form.BackColor;
                Role role = ClassifyBack(oldBack);
                form.BackColor = (role == Role.Keep || role == Role.Dark) ? Bg : RoleColor(role, oldBack);
                if (IsSet(MiShouldFore, form) || IsDark) form.ForeColor = Text;
                if (form.ContextMenuStrip != null) StyleToolStrip(form.ContextMenuStrip);
                if (IsDark) HDToolNative.UseDarkTitleBar(form);

                foreach (Control child in form.Controls) SafeSkin(child, oldBack);
                form.ControlAdded += OnControlAdded;
            }
            catch { }
        }

        /// <summary>Áp giao diện cho một nhánh control bất kỳ (dùng khi dựng control động ngoài Form chính).</summary>
        public static void ApplyToControl(Control c)
        {
            if (c == null) return;
            Color parentBack = c.Parent != null ? c.Parent.BackColor : Bg;
            Skin(c, parentBack);
        }

        private static void OnControlAdded(object sender, ControlEventArgs e)
        {
            try
            {
                Control parent = sender as Control;
                Skin(e.Control, parent != null ? parent.BackColor : Bg);
            }
            catch { }
        }

        /// <summary>Lỗi khi khoác áo từng control (để chẩn đoán; không làm dừng việc khoác áo các control khác).</summary>
        public static readonly System.Collections.Generic.List<string> SkinErrors = new System.Collections.Generic.List<string>();

        private static void SafeSkin(Control c, Color oldParentBack)
        {
            try { Skin(c, oldParentBack); }
            catch (Exception ex)
            {
                if (SkinErrors.Count < 50) SkinErrors.Add((c != null ? c.GetType().Name + " '" + c.Name + "'" : "?") + ": " + ex.GetType().Name + " " + ex.Message);
            }
        }

        private static void Skin(Control c, Color oldParentBack)
        {
            if (c == null || c.IsDisposed) return;
            object o;
            if (Done.TryGetValue(c, out o)) return;
            Done.Add(c, Marker);

            bool backSet = IsSet(MiShouldBack, c);
            bool foreSet = IsSet(MiShouldFore, c);
            Color oldBack = backSet ? c.BackColor : oldParentBack;
            Color oldFore = c.ForeColor;

            if (c.ContextMenuStrip != null) StyleToolStrip(c.ContextMenuStrip);

            if (c is HDToolPaintedControl)
            {
                // Control tự vẽ theo token, không cần đổi màu
            }
            else if (c is DataGridView)
            {
                StyleGrid((DataGridView)c);
            }
            else if (c is Button)
            {
                Button b = (Button)c;
                StyleButton(b, ClassifyButton(b, oldBack, backSet));
                if (IsDark) b.BackColorChanged += OnButtonBackChanged;
            }
            else if (c is CheckBox || c is RadioButton)
            {
                ButtonBase bb = (ButtonBase)c;
                bool isToggle = (c is CheckBox && ((CheckBox)c).Appearance == Appearance.Button) ||
                                (c is RadioButton && ((RadioButton)c).Appearance == Appearance.Button);
                if (isToggle)
                {
                    bb.FlatStyle = FlatStyle.Flat;
                    bb.UseVisualStyleBackColor = false;
                    bb.BackColor = Surface;
                    bb.ForeColor = Text;
                    bb.FlatAppearance.BorderColor = LineStrong;
                    bb.FlatAppearance.CheckedBackColor = AccentSoft;
                    bb.FlatAppearance.MouseOverBackColor = AccentSoft;
                }
                else
                {
                    SkinTextual(c, backSet, foreSet, oldBack, oldFore, oldParentBack);
                    if (IsDark && !foreSet) c.ForeColor = IsOnHeader(c) ? HeaderText : Text;
                }
                if (IsDark) c.ForeColorChanged += OnForeChanged;
            }
            else if (c is TextBoxBase)
            {
                TextBoxBase tb = (TextBoxBase)c;
                if (tb.BorderStyle == BorderStyle.Fixed3D) tb.BorderStyle = BorderStyle.FixedSingle;
                if ((backSet && Lum(oldBack) > 0.6) || (IsDark && !backSet)) tb.BackColor = tb.ReadOnly ? Surface2 : Input;
                if ((foreSet && Lum(oldFore) < 0.30) || (IsDark && !foreSet)) tb.ForeColor = Text; // giữ chữ xám gợi ý (Color.Gray)
                if (IsDark)
                {
                    tb.ForeColorChanged += OnForeChanged;
                    HDToolNative.DarkScrollbars(tb);
                }
            }
            else if (c is UpDownBase)
            {
                UpDownBase ud = (UpDownBase)c;
                ud.BorderStyle = BorderStyle.FixedSingle;
                if ((backSet && Lum(oldBack) > 0.6) || (IsDark && !backSet)) ud.BackColor = Input;
                if ((foreSet && Lum(oldFore) < 0.30) || (IsDark && !foreSet)) ud.ForeColor = Text;
            }
            else if (c is ComboBox)
            {
                ComboBox cb = (ComboBox)c;
                cb.FlatStyle = IsDark ? FlatStyle.Standard : FlatStyle.Flat; // nền tối: kiểu chuẩn + chủ đề DarkMode_CFD của Windows đẹp nhất
                if (!backSet || Lum(oldBack) > 0.6) cb.BackColor = Input;
                if (!foreSet || Lum(oldFore) < 0.30) cb.ForeColor = Text;
                if (IsDark) HDToolNative.DarkComboBox(cb);
            }
            else if (c is ListBox || c is ListView || c is TreeView)
            {
                if (c is ListBox) ((ListBox)c).BorderStyle = BorderStyle.FixedSingle;
                if (c is ListView) ((ListView)c).BorderStyle = BorderStyle.FixedSingle;
                if (c is TreeView)
                {
                    ((TreeView)c).BorderStyle = BorderStyle.FixedSingle;
                    ((TreeView)c).LineColor = LineStrong;
                }
                if (!backSet || Lum(oldBack) > 0.6) c.BackColor = Surface;
                if (!foreSet || Lum(oldFore) < 0.30) c.ForeColor = Text;
                if (IsDark) HDToolNative.DarkScrollbars(c);
            }
            else if (c is ToolStrip)
            {
                StyleToolStrip((ToolStrip)c);
            }
            else if (c is Label)
            {
                SkinTextual(c, backSet, foreSet, oldBack, oldFore, oldParentBack);
                if (IsDark && !foreSet) c.ForeColor = IsOnHeader(c) ? HeaderText : Text;
                if (IsDark) c.ForeColorChanged += OnForeChanged;
            }
            else if (c is PictureBox || c is ProgressBar || c is WebBrowser)
            {
                // Giữ nguyên (logo HadoMEE, ảnh xem trước 3D, thanh tiến trình hệ thống)
            }
            else if (c is SplitContainer)
            {
                SplitContainer sc = (SplitContainer)c;
                Color panelOld = oldBack;
                sc.BackColor = Line;
                SkinContainer(sc.Panel1, panelOld);
                SkinContainer(sc.Panel2, panelOld);
                return;
            }
            else if (c is TabControl)
            {
                TabControl tc = (TabControl)c;
                if (IsDark) StyleTabControl(tc);
                foreach (TabPage tp in tc.TabPages)
                {
                    Color tpOld = IsSet(MiShouldBack, tp) ? tp.BackColor : Color.White;
                    tp.UseVisualStyleBackColor = false;
                    SkinContainerColors(tp, tpOld, true);
                    if (IsDark && tp.BackColor.ToArgb() == Color.White.ToArgb()) tp.BackColor = Bg;
                    if (IsDark) tp.ForeColor = Text;
                    MarkAndRecurse(tp, tpOld);
                }
                c.ControlAdded += OnControlAdded;
                return;
            }
            else if (c is GroupBox)
            {
                GroupBox gb = (GroupBox)c;
                if (foreSet || IsDark) gb.ForeColor = IsOnHeader(gb) ? HeaderSub : Text2;
                if (backSet) SkinContainerColors(gb, oldBack, false);
            }
            else
            {
                // Panel, TableLayoutPanel, FlowLayoutPanel, UserControl... (vùng chứa)
                if (backSet) SkinContainerColors(c, oldBack, true);
                if (foreSet) c.ForeColor = MapFore(oldFore, IsOnHeader(c), Effective(c));
                else if (IsDark) c.ForeColor = Text;
                ScrollableControl scr = c as ScrollableControl;
                if (IsDark && scr != null && scr.AutoScroll) HDToolNative.DarkScrollbars(c);
            }

            foreach (Control child in c.Controls) SafeSkin(child, oldBack);
            c.ControlAdded += OnControlAdded;
        }

        private static void SkinContainer(Control panel, Color inheritedOld)
        {
            object o;
            if (Done.TryGetValue(panel, out o)) return;
            Done.Add(panel, Marker);
            Color old = IsSet(MiShouldBack, panel) ? panel.BackColor : inheritedOld;
            SkinContainerColors(panel, old, true);
            if (IsDark && !IsSet(MiShouldBack, panel)) panel.BackColor = Bg;
            foreach (Control child in panel.Controls) SafeSkin(child, old);
            panel.ControlAdded += OnControlAdded;
        }

        private static void MarkAndRecurse(Control c, Color oldBack)
        {
            object o;
            if (!Done.TryGetValue(c, out o)) Done.Add(c, Marker);
            foreach (Control child in c.Controls) SafeSkin(child, oldBack);
            c.ControlAdded += OnControlAdded;
        }

        private static Color Effective(Control c)
        {
            Control p = c;
            while (p != null)
            {
                if (p.BackColor.A > 0) return p.BackColor;
                p = p.Parent;
            }
            return Bg;
        }

        private static void SkinContainerColors(Control c, Color oldBack, bool allowGrid)
        {
            Role role = ClassifyBack(oldBack);
            if (role == Role.Keep) return;
            c.BackColor = RoleColor(role, oldBack);

            Panel p = c as Panel;
            if (p != null && p.BorderStyle != BorderStyle.None)
            {
                p.BorderStyle = BorderStyle.None;
                p.Paint += PaintThinBorder;
            }

            if (role == Role.Dark)
            {
                bool headerLike = (c.Dock == DockStyle.Top || c.Top <= 4) && c.Height >= 36 && c.Height <= 130;
                if (headerLike)
                {
                    object o;
                    if (!HeaderPanels.TryGetValue(c, out o)) HeaderPanels.Add(c, Marker);
                    SetDoubleBuffered(c);
                    c.Paint += PaintHeaderBand;
                    c.Resize += delegate { c.Invalidate(); };
                }
            }
            else if (role == Role.Bg && allowGrid && !IsPremium)
            {
                ScrollableControl sc = c as ScrollableControl;
                if (sc == null || !sc.AutoScroll)
                {
                    SetDoubleBuffered(c);
                    c.Paint += PaintBlueprintGrid;
                }
            }
            else if ((role == Role.Surface2) && c.Dock == DockStyle.Bottom)
            {
                c.Paint += PaintTopLine;
            }
        }

        private static void SkinTextual(Control c, bool backSet, bool foreSet, Color oldBack, Color oldFore, Color oldParentBack)
        {
            bool onHeader = IsOnHeader(c);
            if (backSet)
            {
                if (oldBack.ToArgb() == oldParentBack.ToArgb() || oldBack.A == 0)
                {
                    try { c.BackColor = Color.Transparent; } catch { }
                }
                else
                {
                    Role r = ClassifyBack(oldBack);
                    if (r != Role.Keep) c.BackColor = RoleColor(r, oldBack);
                }
            }
            else if (onHeader && IsPremium)
            {
                // Dải tiêu đề vẽ chuyển màu: nhãn trong suốt để thấy nền chuyển màu
                try { c.BackColor = Color.Transparent; } catch { }
            }
            if (foreSet)
            {
                c.ForeColor = MapFore(oldFore, onHeader, Effective(c));
            }
        }

        // ---------- Màu đổi lúc chạy (code cũ tự đặt màu khi bấm nút / cập nhật trạng thái) ----------
        [ThreadStatic] private static bool _adjusting;

        private static void OnForeChanged(object sender, EventArgs e)
        {
            if (_adjusting) return;
            Control c = sender as Control;
            if (c == null) return;
            Color f = c.ForeColor;
            if (IsForeToken(f) || IsNamedGray(f)) return;
            Color mapped = MapFore(f, IsOnHeader(c), Effective(c));
            if (mapped.ToArgb() == f.ToArgb()) return;
            try { _adjusting = true; c.ForeColor = mapped; }
            finally { _adjusting = false; }
        }

        private static void OnButtonBackChanged(object sender, EventArgs e)
        {
            if (_adjusting) return;
            Button b = sender as Button;
            if (b == null || IsToken(b.BackColor)) return;
            try { _adjusting = true; StyleButton(b, ClassifyButton(b, b.BackColor, true)); }
            finally { _adjusting = false; }
        }

        // ===================== TAB (nền tối) =====================
        private static void StyleTabControl(TabControl tc)
        {
            try
            {
                tc.DrawMode = TabDrawMode.OwnerDrawFixed;
                tc.DrawItem += delegate(object s, DrawItemEventArgs e)
                {
                    TabControl t = (TabControl)s;
                    if (e.Index < 0 || e.Index >= t.TabPages.Count) return;
                    bool sel = e.Index == t.SelectedIndex;
                    Rectangle r = e.Bounds;
                    using (SolidBrush bg = new SolidBrush(sel ? Surface2 : Surface)) e.Graphics.FillRectangle(bg, r);
                    if (sel)
                    {
                        using (SolidBrush acc = new SolidBrush(Accent)) e.Graphics.FillRectangle(acc, r.X, r.Bottom - 3, r.Width, 3);
                    }
                    TextRenderer.DrawText(e.Graphics, t.TabPages[e.Index].Text, sel ? BodyBold : Body, r, sel ? Text : Text2,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                };
            }
            catch { }
        }

        // ===================== NÚT BẤM =====================
        private static HDToolButtonKind ClassifyButton(Button b, Color oldBack, bool backSet)
        {
            string t = (b.Text ?? "").Trim().ToLowerInvariant();
            if (!backSet) return HDToolButtonKind.Secondary;
            float sat = oldBack.GetSaturation();
            double lum = Lum(oldBack);
            float hue = oldBack.GetHue();
            bool reddish = sat > 0.35f && lum < 0.85 && (hue < 20f || hue >= 335f);
            if (reddish || (t.StartsWith("xóa") && sat > 0.35f)) return HDToolButtonKind.Danger;
            if (t.StartsWith("đóng") || t.StartsWith("hủy")) return HDToolButtonKind.Secondary;
            if (sat > 0.30f && lum > 0.08 && lum < 0.85) return HDToolButtonKind.Primary;
            return HDToolButtonKind.Secondary;
        }

        /// <summary>Kiểu nút chuẩn: Primary (nền Accent), Secondary (viền), Ghost (phẳng), Danger (viền đỏ).</summary>
        public static void StyleButton(Button b, HDToolButtonKind kind)
        {
            if (b == null) return;
            bool prev = _adjusting;
            _adjusting = true;
            try
            {
                b.FlatStyle = FlatStyle.Flat;
                b.UseVisualStyleBackColor = false;
                b.Cursor = Cursors.Hand;
                b.FlatAppearance.BorderSize = 1;
                switch (kind)
                {
                    case HDToolButtonKind.Primary:
                        b.BackColor = Accent;
                        b.ForeColor = AccentInk;
                        b.FlatAppearance.BorderColor = Accent;
                        b.FlatAppearance.MouseOverBackColor = AccentHover;
                        b.FlatAppearance.MouseDownBackColor = AccentDown;
                        break;
                    case HDToolButtonKind.Danger:
                        b.BackColor = Surface;
                        b.ForeColor = Err;
                        b.FlatAppearance.BorderColor = Err;
                        b.FlatAppearance.MouseOverBackColor = ErrSoft;
                        b.FlatAppearance.MouseDownBackColor = Mix(ErrSoft, Err, 0.2);
                        break;
                    case HDToolButtonKind.Ghost:
                        b.BackColor = Surface2;
                        b.ForeColor = Text2;
                        b.FlatAppearance.BorderSize = 0;
                        b.FlatAppearance.MouseOverBackColor = AccentSoft;
                        b.FlatAppearance.MouseDownBackColor = Mix(AccentSoft, Accent, 0.15);
                        break;
                    default:
                        b.BackColor = Surface2;
                        b.ForeColor = Text;
                        b.FlatAppearance.BorderColor = LineStrong;
                        b.FlatAppearance.MouseOverBackColor = AccentSoft;
                        b.FlatAppearance.MouseDownBackColor = Mix(AccentSoft, Accent, 0.15);
                        break;
                }
            }
            finally { _adjusting = prev; }
        }

        // ===================== BẢNG DỮ LIỆU =====================
        private static readonly Regex RxMonoColumn = new Regex(
            @"stt|size|quy cách|kích thước|số hiệu|sheet ?number|chiều dài|length|diện tích|area|số lượng|quantity|cao độ|elevation|offset|độ dày|thickness|\(m\)|\(mm\)|\bmm\b|m²|aci",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>Kiểu bảng: tiêu đề nền nhạt + vạch nhấn, dòng 28px xen màu, số liệu Consolas, chip mã hệ thống.</summary>
        public static void StyleGrid(DataGridView g)
        {
            if (g == null) return;
            g.EnableHeadersVisualStyles = false;
            g.BackgroundColor = Surface;
            g.GridColor = Line;
            if (g.BorderStyle == BorderStyle.Fixed3D) g.BorderStyle = BorderStyle.FixedSingle;
            if (IsPremium) g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            g.ColumnHeadersBorderStyle = IsPremium ? DataGridViewHeaderBorderStyle.None : DataGridViewHeaderBorderStyle.Single;

            DataGridViewCellStyle h = g.ColumnHeadersDefaultCellStyle;
            h.BackColor = Surface2;
            h.ForeColor = Text2;
            h.SelectionBackColor = Surface2;
            h.SelectionForeColor = Text2;
            h.Font = GridHeader;
            h.WrapMode = DataGridViewTriState.False;
            if (g.ColumnHeadersHeightSizeMode != DataGridViewColumnHeadersHeightSizeMode.AutoSize && g.ColumnHeadersHeight < ColumnHeaderHeight)
            {
                g.ColumnHeadersHeight = ColumnHeaderHeight;
            }

            DataGridViewCellStyle d = g.DefaultCellStyle;
            d.BackColor = Surface;
            d.ForeColor = Text;
            d.SelectionBackColor = AccentSoft;
            d.SelectionForeColor = Text;
            g.AlternatingRowsDefaultCellStyle.BackColor = RowAlt;
            g.AlternatingRowsDefaultCellStyle.SelectionBackColor = AccentSoft;
            g.AlternatingRowsDefaultCellStyle.SelectionForeColor = Text;

            g.RowHeadersDefaultCellStyle.BackColor = Surface2;
            g.RowHeadersDefaultCellStyle.ForeColor = Text2;
            g.RowHeadersDefaultCellStyle.SelectionBackColor = AccentSoft;

            if (g.RowTemplate.Height < RowHeight) g.RowTemplate.Height = RowHeight;
            try
            {
                foreach (DataGridViewRow r in g.Rows)
                {
                    if (r.Height < RowHeight) r.Height = RowHeight;
                }
            }
            catch { }

            foreach (DataGridViewColumn col in g.Columns) StyleColumn(col);
            g.ColumnAdded += delegate(object s, DataGridViewColumnEventArgs e) { StyleColumn(e.Column); };
            g.RowsAdded += delegate(object s, DataGridViewRowsAddedEventArgs e)
            {
                DataGridView dg = (DataGridView)s;
                try
                {
                    for (int i = e.RowIndex; i < e.RowIndex + e.RowCount && i < dg.Rows.Count; i++)
                    {
                        if (dg.Rows[i].Height < RowHeight) dg.Rows[i].Height = RowHeight;
                    }
                }
                catch { }
            };
            if (IsDark) g.CellFormatting += AdaptCellColors;
            g.Paint += PaintGridHeaderAccent;
            g.CellPainting += PaintSystemChip;
            SetDoubleBuffered(g);
            if (IsDark) HDToolNative.DarkScrollbars(g);
        }

        /// <summary>Nền tối: màu ô do code cũ đặt (nền sáng, chữ đậm) được chuyển sang màu tối tương ứng khi vẽ — dữ liệu không đổi.</summary>
        private static void AdaptCellColors(object sender, DataGridViewCellFormattingEventArgs e)
        {
            try
            {
                DataGridViewCellStyle st = e.CellStyle;
                if (st == null) return;
                if (st.BackColor.A > 0 && !IsToken(st.BackColor) && Lum(st.BackColor) > 0.5)
                {
                    Role r = ClassifyBack(st.BackColor);
                    if (r == Role.Surface || r == Role.Bg || r == Role.Surface2) st.BackColor = (e.RowIndex % 2 == 1) ? RowAlt : Surface;
                    else if (r != Role.Keep && r != Role.Dark) st.BackColor = RoleColor(r, st.BackColor);
                }
                if (st.ForeColor.A > 0 && !IsForeToken(st.ForeColor) && !IsNamedGray(st.ForeColor))
                {
                    st.ForeColor = MapFore(st.ForeColor, false, st.BackColor);
                }
                if (st.SelectionBackColor.A > 0 && !IsToken(st.SelectionBackColor) && Lum(st.SelectionBackColor) > 0.5)
                {
                    st.SelectionBackColor = AccentSoft;
                    st.SelectionForeColor = Text;
                }
            }
            catch { }
        }

        private static void StyleColumn(DataGridViewColumn col)
        {
            if (col == null) return;
            try
            {
                DataGridViewContentAlignment a = col.DefaultCellStyle.Alignment;
                bool right = a == DataGridViewContentAlignment.MiddleRight || a == DataGridViewContentAlignment.TopRight || a == DataGridViewContentAlignment.BottomRight;
                string key = (col.Name ?? "") + " " + (col.HeaderText ?? "");
                if (right || RxMonoColumn.IsMatch(key))
                {
                    if (col is DataGridViewTextBoxColumn || col is DataGridViewComboBoxColumn)
                    {
                        col.DefaultCellStyle.Font = Mono;
                    }
                }
                col.HeaderCell.Style.Padding = new Padding(6, 0, 6, 0);
            }
            catch { }
        }

        private static bool IsSystemColumn(DataGridViewColumn col)
        {
            if (col == null) return false;
            string n = (col.Name ?? "").ToLowerInvariant();
            string h = (col.HeaderText ?? "").ToLowerInvariant();
            return n == "system" || n == "systemname" || n == "colsystem" || h == "hệ thống" || h == "hệ" || h == "system";
        }

        private static void PaintSystemChip(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.Handled || e.RowIndex < 0 || e.ColumnIndex < 0) return;
            DataGridView g = (DataGridView)sender;
            DataGridViewColumn col = g.Columns[e.ColumnIndex];
            if (!IsSystemColumn(col)) return;
            string val = e.FormattedValue as string;
            if (string.IsNullOrEmpty(val)) return;

            e.Paint(e.CellBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.ContentForeground);
            Color sc = SystemColor(SystemCodeFromName(val));
            Size ts = TextRenderer.MeasureText(e.Graphics, val, MonoSmall, Size.Empty, TextFormatFlags.NoPadding);
            int chipH = Math.Min(e.CellBounds.Height - 8, ts.Height + 6);
            int chipW = Math.Min(e.CellBounds.Width - 10, ts.Width + 14);
            Rectangle chip = new Rectangle(e.CellBounds.X + 5, e.CellBounds.Y + (e.CellBounds.Height - chipH) / 2, chipW, chipH);
            SmoothingMode old = e.Graphics.SmoothingMode;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (IsPremium)
            {
                using (GraphicsPath gp = RoundRect(chip, 5))
                using (SolidBrush b = new SolidBrush(sc)) e.Graphics.FillPath(b, gp);
                TextRenderer.DrawText(e.Graphics, val, MonoSmall, chip, Lum(sc) > 0.55 ? C(10, 15, 31) : Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            }
            else
            {
                using (Pen p = new Pen(sc, 1f)) e.Graphics.DrawRectangle(p, chip);
                TextRenderer.DrawText(e.Graphics, val, MonoSmall, chip, sc,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            }
            e.Graphics.SmoothingMode = old;
            e.Handled = true;
        }

        // ===================== MENU / TOOLSTRIP =====================
        public static void StyleToolStrip(ToolStrip ts)
        {
            if (ts == null) return;
            try
            {
                ts.RenderMode = ToolStripRenderMode.Professional;
                ts.Renderer = new HDToolStripRenderer();
                if (!(ts is ToolStripDropDown)) ts.BackColor = Surface2;
                ts.ForeColor = Text;
            }
            catch { }
        }

        private class HDToolStripRenderer : ToolStripProfessionalRenderer
        {
            public HDToolStripRenderer() : base(new HDToolColorTable()) { RoundedEdges = false; }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                if (IsDark && (e.TextColor.ToArgb() == SystemColors.ControlText.ToArgb() || Lum(e.TextColor) < 0.3)) e.TextColor = e.Item.Enabled ? Text : Text3;
                base.OnRenderItemText(e);
            }

            protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
            {
                if (IsDark) e.ArrowColor = Text2;
                base.OnRenderArrow(e);
            }
        }

        private class HDToolColorTable : ProfessionalColorTable
        {
            public override Color MenuItemSelected { get { return AccentSoft; } }
            public override Color MenuItemBorder { get { return Accent; } }
            public override Color MenuBorder { get { return LineStrong; } }
            public override Color MenuItemSelectedGradientBegin { get { return AccentSoft; } }
            public override Color MenuItemSelectedGradientEnd { get { return AccentSoft; } }
            public override Color MenuItemPressedGradientBegin { get { return AccentSoft; } }
            public override Color MenuItemPressedGradientEnd { get { return AccentSoft; } }
            public override Color ToolStripDropDownBackground { get { return Surface; } }
            public override Color ImageMarginGradientBegin { get { return Surface2; } }
            public override Color ImageMarginGradientMiddle { get { return Surface2; } }
            public override Color ImageMarginGradientEnd { get { return Surface2; } }
            public override Color SeparatorDark { get { return Line; } }
            public override Color SeparatorLight { get { return Surface; } }
            public override Color ToolStripBorder { get { return Line; } }
            public override Color ToolStripGradientBegin { get { return Surface2; } }
            public override Color ToolStripGradientMiddle { get { return Surface2; } }
            public override Color ToolStripGradientEnd { get { return Surface2; } }
            public override Color ButtonSelectedHighlight { get { return AccentSoft; } }
            public override Color ButtonSelectedGradientBegin { get { return AccentSoft; } }
            public override Color ButtonSelectedGradientEnd { get { return AccentSoft; } }
            public override Color ButtonSelectedBorder { get { return Accent; } }
            public override Color CheckBackground { get { return AccentSoft; } }
            public override Color CheckSelectedBackground { get { return AccentSoft; } }
            public override Color CheckPressedBackground { get { return AccentSoft; } }
        }

        // ===================== HÀM VẼ DÙNG CHUNG =====================
        /// <summary>Hình chữ nhật bo góc (radius tính theo px thực).</summary>
        public static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            GraphicsPath gp = new GraphicsPath();
            if (radius <= 0.5f)
            {
                gp.AddRectangle(r);
                return gp;
            }
            float d = Math.Min(radius * 2f, Math.Min(r.Width, r.Height));
            gp.AddArc(r.X, r.Y, d, d, 180, 90);
            gp.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            gp.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            gp.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            gp.CloseFigure();
            return gp;
        }

        /// <summary>Nền chuyển màu của dải tiêu đề (Tối Cao Cấp: xanh đêm -> tím).</summary>
        public static void FillHeaderGradient(Graphics g, Rectangle r)
        {
            if (r.Width <= 0 || r.Height <= 0) return;
            using (LinearGradientBrush b = new LinearGradientBrush(r, HeaderGrad1, HeaderGrad3, LinearGradientMode.Horizontal))
            {
                ColorBlend cb = new ColorBlend(3);
                cb.Colors = new Color[] { HeaderGrad1, HeaderGrad2, HeaderGrad3 };
                cb.Positions = new float[] { 0f, 0.52f, 1f };
                b.InterpolationColors = cb;
                g.FillRectangle(b, r);
            }
        }

        /// <summary>Lưới blueprint: ô nhỏ 10px, ô lớn 50px (tự nhân theo DPI). Không vẽ ở Tối Cao Cấp.</summary>
        public static void DrawBlueprintGrid(Graphics g, Rectangle r, float dpiScale)
        {
            if (GridMinor.A == 0 && GridMajor.A == 0) return;
            int minor = Math.Max(6, (int)Math.Round(10 * dpiScale));
            int major = minor * 5;
            using (Pen pMinor = new Pen(GridMinor, 1f))
            using (Pen pMajor = new Pen(GridMajor, 1f))
            {
                for (int x = r.Left - (r.Left % minor); x < r.Right; x += minor)
                {
                    g.DrawLine((x % major == 0) ? pMajor : pMinor, x, r.Top, x, r.Bottom);
                }
                for (int y = r.Top - (r.Top % minor); y < r.Bottom; y += minor)
                {
                    g.DrawLine((y % major == 0) ? pMajor : pMinor, r.Left, y, r.Right, y);
                }
            }
        }

        public static float DpiScale(Control c)
        {
            try
            {
                using (Graphics g = c.CreateGraphics()) return g.DpiX / 96f;
            }
            catch { return 1f; }
        }

        private static void PaintBlueprintGrid(object sender, PaintEventArgs e)
        {
            Control c = sender as Control;
            if (c == null) return;
            DrawBlueprintGrid(e.Graphics, e.ClipRectangle, e.Graphics.DpiX / 96f);
        }

        /// <summary>Dải tiêu đề của Form cũ: Tối Cao Cấp = nền chuyển màu; kiểu khác = vạch nhấn trái.</summary>
        private static void PaintHeaderBand(object sender, PaintEventArgs e)
        {
            Control c = sender as Control;
            if (c == null) return;
            float s = e.Graphics.DpiX / 96f;
            if (IsPremium)
            {
                FillHeaderGradient(e.Graphics, c.ClientRectangle);
            }
            else
            {
                using (SolidBrush b = new SolidBrush(HeaderAccent))
                {
                    e.Graphics.FillRectangle(b, 0, 0, Math.Max(3, (int)Math.Round(4 * s)), c.Height);
                }
            }
            using (Pen p = new Pen(HeaderLine))
            {
                e.Graphics.DrawLine(p, 0, c.Height - 1, c.Width, c.Height - 1);
            }
        }

        private static void PaintThinBorder(object sender, PaintEventArgs e)
        {
            Control c = sender as Control;
            if (c == null) return;
            using (Pen p = new Pen(Line))
            {
                e.Graphics.DrawRectangle(p, 0, 0, c.Width - 1, c.Height - 1);
            }
        }

        private static void PaintTopLine(object sender, PaintEventArgs e)
        {
            Control c = sender as Control;
            if (c == null) return;
            using (Pen p = new Pen(Line))
            {
                e.Graphics.DrawLine(p, 0, 0, c.Width, 0);
            }
        }

        private static void PaintGridHeaderAccent(object sender, PaintEventArgs e)
        {
            DataGridView g = sender as DataGridView;
            if (g == null || !g.ColumnHeadersVisible) return;
            float s = e.Graphics.DpiX / 96f;
            int h = IsPremium ? 1 : Math.Max(2, (int)Math.Round(2 * s));
            int y = g.ColumnHeadersHeight - h;
            int right = 0;
            try
            {
                Rectangle disp = g.DisplayRectangle;
                right = disp.Right;
                int lastVisible = -1;
                foreach (DataGridViewColumn col in g.Columns)
                {
                    if (col.Visible && col.Displayed) lastVisible = Math.Max(lastVisible, col.Index);
                }
                if (lastVisible >= 0)
                {
                    Rectangle r = g.GetColumnDisplayRectangle(lastVisible, false);
                    right = Math.Min(disp.Right, r.Right);
                }
            }
            catch { right = g.Width; }
            using (SolidBrush b = new SolidBrush(IsPremium ? LineStrong : Accent))
            {
                e.Graphics.FillRectangle(b, 0, y, right, h);
            }
        }
    }

    /// <summary>Gọi API Windows cho nền tối: thanh tiêu đề tối (Windows 10 20H1+ / 11) và thanh cuộn tối.</summary>
    public static class HDToolNative
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubIdList);

        private static int ColorRef(Color c) { return c.R | (c.G << 8) | (c.B << 16); }

        public static void UseDarkTitleBar(Form f)
        {
            if (f == null) return;
            Action apply = delegate
            {
                try
                {
                    int on = 1;
                    if (DwmSetWindowAttribute(f.Handle, 20, ref on, 4) != 0) DwmSetWindowAttribute(f.Handle, 19, ref on, 4);
                    int cap = ColorRef(HDToolTheme.Surface);
                    DwmSetWindowAttribute(f.Handle, 35, ref cap, 4); // màu thanh tiêu đề (Windows 11)
                    int border = ColorRef(HDToolTheme.LineStrong);
                    DwmSetWindowAttribute(f.Handle, 34, ref border, 4); // màu viền cửa sổ (Windows 11)
                }
                catch { }
            };
            if (f.IsHandleCreated) apply();
            else f.HandleCreated += delegate { apply(); };
        }

        public static void DarkScrollbars(Control c)
        {
            if (c == null) return;
            Action apply = delegate
            {
                try
                {
                    SetWindowTheme(c.Handle, "DarkMode_Explorer", null);
                    foreach (Control child in c.Controls)
                    {
                        if (child is ScrollBar) SetWindowTheme(child.Handle, "DarkMode_Explorer", null);
                    }
                }
                catch { }
            };
            if (c.IsHandleCreated) apply();
            else c.HandleCreated += delegate { apply(); };
        }

        public static void DarkComboBox(ComboBox cb)
        {
            if (cb == null) return;
            Action apply = delegate { try { SetWindowTheme(cb.Handle, "DarkMode_CFD", null); } catch { } };
            if (cb.IsHandleCreated) apply();
            else cb.HandleCreated += delegate { apply(); };
        }
    }

    /// <summary>Đánh dấu control tự vẽ theo token HDToolTheme (bộ khoác áo không đổi màu của nó).</summary>
    public interface HDToolPaintedControl { }
}
