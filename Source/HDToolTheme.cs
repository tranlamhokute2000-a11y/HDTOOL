using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace HDTool
{
    public enum HDToolThemeMode { Blueprint, Drafting }

    public enum HDToolButtonKind { Primary, Secondary, Ghost, Danger }

    /// <summary>
    /// Bộ màu, font và bộ "khoác áo" giao diện kỹ thuật dùng chung cho toàn bộ Form HDTOOL.
    /// Nguồn: PreviewStudio/HDTool_ThemeStudio.html (Sếp Hồ chốt chế độ Bản Vẽ Sáng).
    /// HDToolBaseForm tự gọi ApplyTo(form) khi Form tải, nên mọi Form kế thừa đều đổi giao diện đồng bộ.
    /// </summary>
    public static class HDToolTheme
    {
        public static HDToolThemeMode Mode = HDToolThemeMode.Drafting;
        private static bool Dark { get { return Mode == HDToolThemeMode.Blueprint; } }

        // ===================== MÀU (TOKEN) =====================
        public static Color Bg           { get { return Dark ? Color.FromArgb(13, 21, 32) : Color.FromArgb(237, 241, 245); } }
        public static Color Surface      { get { return Dark ? Color.FromArgb(18, 28, 41) : Color.FromArgb(255, 255, 255); } }
        public static Color Surface2     { get { return Dark ? Color.FromArgb(23, 36, 51) : Color.FromArgb(245, 247, 250); } }
        public static Color Header       { get { return Dark ? Color.FromArgb(10, 17, 26) : Color.FromArgb(16, 35, 58); } }
        public static Color HeaderText   { get { return Dark ? Color.FromArgb(232, 241, 250) : Color.FromArgb(242, 246, 250); } }
        public static Color HeaderSub    { get { return Color.FromArgb(157, 176, 196); } }
        public static Color HeaderLine   { get { return Dark ? Color.FromArgb(47, 72, 98) : Color.FromArgb(44, 70, 100); } }
        public static Color HeaderAccent { get { return Color.FromArgb(43, 184, 232); } }
        public static Color Line         { get { return Dark ? Color.FromArgb(34, 52, 74) : Color.FromArgb(211, 219, 228); } }
        public static Color LineStrong   { get { return Dark ? Color.FromArgb(47, 72, 98) : Color.FromArgb(183, 195, 208); } }
        public static Color Text         { get { return Dark ? Color.FromArgb(227, 235, 243) : Color.FromArgb(14, 28, 43); } }
        public static Color Text2        { get { return Dark ? Color.FromArgb(157, 176, 196) : Color.FromArgb(70, 88, 107); } }
        public static Color Text3        { get { return Dark ? Color.FromArgb(102, 121, 143) : Color.FromArgb(122, 137, 153); } }
        public static Color Accent       { get { return Dark ? Color.FromArgb(43, 184, 232) : Color.FromArgb(11, 120, 166); } }
        public static Color AccentHover  { get { return Dark ? Color.FromArgb(82, 200, 240) : Color.FromArgb(10, 141, 194); } }
        public static Color AccentDown   { get { return Dark ? Color.FromArgb(30, 150, 192) : Color.FromArgb(8, 96, 133); } }
        public static Color AccentInk    { get { return Dark ? Color.FromArgb(4, 32, 44) : Color.FromArgb(255, 255, 255); } }
        public static Color AccentSoft   { get { return Dark ? Color.FromArgb(19, 58, 82) : Color.FromArgb(217, 238, 247); } }
        public static Color Ok           { get { return Dark ? Color.FromArgb(61, 214, 140) : Color.FromArgb(30, 158, 98); } }
        public static Color Warn         { get { return Dark ? Color.FromArgb(242, 179, 61) : Color.FromArgb(183, 121, 31); } }
        public static Color Err          { get { return Dark ? Color.FromArgb(242, 102, 94) : Color.FromArgb(200, 66, 59); } }
        public static Color OkSoft       { get { return Dark ? Color.FromArgb(18, 52, 40) : Color.FromArgb(226, 244, 235); } }
        public static Color WarnSoft     { get { return Dark ? Color.FromArgb(56, 44, 18) : Color.FromArgb(252, 243, 224); } }
        public static Color ErrSoft      { get { return Dark ? Color.FromArgb(60, 26, 26) : Color.FromArgb(251, 231, 229); } }
        public static Color RowAlt       { get { return Dark ? Color.FromArgb(21, 33, 50) : Color.FromArgb(246, 249, 252); } }
        public static Color Input        { get { return Dark ? Color.FromArgb(15, 24, 35) : Color.FromArgb(255, 255, 255); } }
        public static Color GridMinor    { get { return Dark ? Color.FromArgb(13, 43, 184, 232) : Color.FromArgb(10, 11, 120, 166); } }
        public static Color GridMajor    { get { return Dark ? Color.FromArgb(26, 43, 184, 232) : Color.FromArgb(20, 11, 120, 166); } }

        // Kích thước bố cục 4 tầng (px @ 96 DPI, tự nhân theo DPI qua AutoScaleMode.Dpi)
        public const int HeaderHeight = 58;
        public const int ToolbarHeight = 46;
        public const int StatusHeight = 52;
        public const int RowHeight = 26;
        public const int ColumnHeaderHeight = 30;

        // ===================== FONT: chỉ Segoe UI (chữ) và Consolas (số, mã, kích thước) =====================
        public static readonly Font Title      = new Font("Segoe UI", 12f, FontStyle.Bold);
        public static readonly Font Body       = new Font("Segoe UI", 9f, FontStyle.Regular);
        public static readonly Font BodyBold   = new Font("Segoe UI", 9f, FontStyle.Bold);
        public static readonly Font Caption    = new Font("Segoe UI", 8.5f, FontStyle.Regular);
        public static readonly Font LabelFont  = new Font("Segoe UI", 8f, FontStyle.Bold);
        public static readonly Font GridHeader = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        public static readonly Font Mono       = new Font("Consolas", 9f, FontStyle.Regular);
        public static readonly Font MonoSmall  = new Font("Consolas", 8f, FontStyle.Regular);
        public static readonly Font Readout    = new Font("Consolas", 16f, FontStyle.Regular);

        // ===================== MÀU HỆ THỐNG ỐNG GIÓ =====================
        public static Color SystemColor(string code)
        {
            switch ((code ?? "").ToUpperInvariant())
            {
                case "SA": return Dark ? Color.FromArgb(43, 184, 232) : Color.FromArgb(11, 120, 166);
                case "RA": return Dark ? Color.FromArgb(61, 214, 140) : Color.FromArgb(30, 158, 98);
                case "EA": return Dark ? Color.FromArgb(242, 179, 61) : Color.FromArgb(183, 121, 31);
                case "OA": return Dark ? Color.FromArgb(180, 140, 242) : Color.FromArgb(122, 79, 201);
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
        private static double Lum(Color c)
        {
            return (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;
        }

        private static Color Mix(Color a, Color b, double t)
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
            Color[] tokens = new Color[] { Bg, Surface, Surface2, Header, Accent, AccentSoft, Ok, Warn, Err, OkSoft, WarnSoft, ErrSoft, RowAlt, Input };
            foreach (Color t in tokens) if (t.ToArgb() == v) return true;
            return false;
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

        /// <summary>Đổi màu chữ cũ sang token tương ứng (nền sáng).</summary>
        private static Color MapFore(Color c, Color effectiveBack)
        {
            if (c.A == 0) return c;
            double lum = Lum(c);
            float sat = c.GetSaturation();
            bool backDark = Lum(effectiveBack) < 0.45;
            if (backDark)
            {
                if (sat > 0.40f && lum > 0.15 && lum < 0.85) return HeaderAccent;
                return lum > 0.75 ? HeaderText : HeaderSub;
            }
            if (lum > 0.85) return Lum(effectiveBack) > 0.6 ? Text : c; // chữ trắng trên nền đã thành sáng -> chữ mực
            if (sat > 0.40f && lum > 0.12 && lum < 0.85)
            {
                Role r = HueRole(c, false);
                return RoleColor(r, c);
            }
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

        private static void SetDoubleBuffered(Control c)
        {
            try { if (PiDoubleBuffered != null) PiDoubleBuffered.SetValue(c, true, null); }
            catch { }
        }

        private static readonly ConditionalWeakTable<Control, object> Done = new ConditionalWeakTable<Control, object>();
        private static readonly object Marker = new object();

        // ===================== ÁP GIAO DIỆN CHO FORM =====================
        /// <summary>
        /// Khoác giao diện kỹ thuật cho toàn bộ Form: chỉ đổi những màu được đặt tường minh, giữ nguyên bố cục, sự kiện và logic.
        /// Control được thêm sau (động) cũng tự được áp qua sự kiện ControlAdded.
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
                if (IsSet(MiShouldFore, form)) form.ForeColor = Text;
                if (form.ContextMenuStrip != null) StyleToolStrip(form.ContextMenuStrip);

                foreach (Control child in form.Controls) Skin(child, oldBack);
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
                // Control tự vẽ theo token (Header, Status, ToolRow...), không cần đổi màu
            }
            else if (c is DataGridView)
            {
                StyleGrid((DataGridView)c);
            }
            else if (c is Button)
            {
                Button b = (Button)c;
                StyleButton(b, ClassifyButton(b, oldBack, backSet));
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
                }
            }
            else if (c is TextBoxBase)
            {
                TextBoxBase tb = (TextBoxBase)c;
                if (tb.BorderStyle == BorderStyle.Fixed3D) tb.BorderStyle = BorderStyle.FixedSingle;
                if (backSet && Lum(oldBack) > 0.6) tb.BackColor = tb.ReadOnly ? Surface2 : Input;
                if (foreSet && Lum(oldFore) < 0.30) tb.ForeColor = Text; // giữ nguyên chữ xám placeholder (Color.Gray)
            }
            else if (c is UpDownBase)
            {
                UpDownBase ud = (UpDownBase)c;
                ud.BorderStyle = BorderStyle.FixedSingle;
                if (backSet && Lum(oldBack) > 0.6) ud.BackColor = Input;
                if (foreSet && Lum(oldFore) < 0.30) ud.ForeColor = Text;
            }
            else if (c is ComboBox)
            {
                ComboBox cb = (ComboBox)c;
                cb.FlatStyle = FlatStyle.Flat;
                if (!backSet || Lum(oldBack) > 0.6) cb.BackColor = Input;
                if (!foreSet || Lum(oldFore) < 0.30) cb.ForeColor = Text;
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
            }
            else if (c is ToolStrip)
            {
                StyleToolStrip((ToolStrip)c);
            }
            else if (c is Label)
            {
                SkinTextual(c, backSet, foreSet, oldBack, oldFore, oldParentBack);
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
                foreach (TabPage tp in ((TabControl)c).TabPages)
                {
                    Color tpOld = IsSet(MiShouldBack, tp) ? tp.BackColor : Color.White;
                    tp.UseVisualStyleBackColor = false;
                    SkinContainerColors(tp, tpOld, true);
                    MarkAndRecurse(tp, tpOld);
                }
                c.ControlAdded += OnControlAdded;
                return;
            }
            else if (c is GroupBox)
            {
                GroupBox gb = (GroupBox)c;
                if (foreSet) gb.ForeColor = Lum(Effective(gb)) < 0.45 ? HeaderSub : Text2;
                if (backSet) SkinContainerColors(gb, oldBack, false);
            }
            else
            {
                // Panel, TableLayoutPanel, FlowLayoutPanel, UserControl... (vùng chứa)
                if (backSet) SkinContainerColors(c, oldBack, true);
                if (foreSet) c.ForeColor = MapFore(oldFore, Effective(c));
            }

            foreach (Control child in c.Controls) Skin(child, oldBack);
            c.ControlAdded += OnControlAdded;
        }

        private static void SkinContainer(Control panel, Color inheritedOld)
        {
            object o;
            if (Done.TryGetValue(panel, out o)) return;
            Done.Add(panel, Marker);
            Color old = IsSet(MiShouldBack, panel) ? panel.BackColor : inheritedOld;
            SkinContainerColors(panel, old, true);
            foreach (Control child in panel.Controls) Skin(child, old);
            panel.ControlAdded += OnControlAdded;
        }

        private static void MarkAndRecurse(Control c, Color oldBack)
        {
            object o;
            if (!Done.TryGetValue(c, out o)) Done.Add(c, Marker);
            foreach (Control child in c.Controls) Skin(child, oldBack);
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
                    SetDoubleBuffered(c);
                    c.Paint += PaintHeaderStripe;
                }
            }
            else if (role == Role.Bg && allowGrid)
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
            if (foreSet)
            {
                c.ForeColor = MapFore(oldFore, Effective(c));
            }
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
                    b.BackColor = Surface;
                    b.ForeColor = Text;
                    b.FlatAppearance.BorderColor = LineStrong;
                    b.FlatAppearance.MouseOverBackColor = AccentSoft;
                    b.FlatAppearance.MouseDownBackColor = Mix(AccentSoft, Accent, 0.15);
                    break;
            }
        }

        // ===================== BẢNG DỮ LIỆU =====================
        private static readonly Regex RxMonoColumn = new Regex(
            @"stt|size|quy cách|kích thước|số hiệu|sheet ?number|chiều dài|length|diện tích|area|số lượng|quantity|cao độ|elevation|offset|độ dày|thickness|\(m\)|\(mm\)|\bmm\b|m²|aci",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>Kiểu bảng kỹ thuật: tiêu đề nền nhạt + vạch nhấn, dòng 26px xen màu, số liệu Consolas, chip mã hệ thống.</summary>
        public static void StyleGrid(DataGridView g)
        {
            if (g == null) return;
            g.EnableHeadersVisualStyles = false;
            g.BackgroundColor = Surface;
            g.GridColor = Line;
            if (g.BorderStyle == BorderStyle.Fixed3D) g.BorderStyle = BorderStyle.FixedSingle;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;

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
            g.Paint += PaintGridHeaderAccent;
            g.CellPainting += PaintSystemChip;
            SetDoubleBuffered(g);
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
                col.HeaderCell.Style.Padding = new Padding(4, 0, 4, 0);
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
            int chipH = Math.Min(e.CellBounds.Height - 6, ts.Height + 6);
            int chipW = Math.Min(e.CellBounds.Width - 10, ts.Width + 12);
            Rectangle chip = new Rectangle(e.CellBounds.X + 5, e.CellBounds.Y + (e.CellBounds.Height - chipH) / 2, chipW, chipH);
            using (Pen p = new Pen(sc, 1f))
            {
                e.Graphics.DrawRectangle(p, chip);
            }
            TextRenderer.DrawText(e.Graphics, val, MonoSmall, chip, sc,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            e.Handled = true;
        }

        // ===================== MENU / TOOLSTRIP =====================
        public static void StyleToolStrip(ToolStrip ts)
        {
            if (ts == null) return;
            try
            {
                ts.RenderMode = ToolStripRenderMode.Professional;
                ts.Renderer = new ToolStripProfessionalRenderer(new HDToolColorTable());
                if (!(ts is ToolStripDropDown)) ts.BackColor = Surface2;
                ts.ForeColor = Text;
            }
            catch { }
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
        }

        // ===================== HÀM VẼ DÙNG CHUNG =====================
        /// <summary>Lưới blueprint: ô nhỏ 10px, ô lớn 50px (tự nhân theo DPI).</summary>
        public static void DrawBlueprintGrid(Graphics g, Rectangle r, float dpiScale)
        {
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

        /// <summary>Góc ngắm kỹ thuật (2 góc chéo) cho khung thông tin.</summary>
        public static void DrawCornerTicks(Graphics g, Rectangle r, float dpiScale)
        {
            int len = (int)Math.Round(10 * dpiScale);
            float w = Math.Max(2f, 2f * dpiScale);
            using (Pen p = new Pen(Accent, w))
            {
                p.Alignment = PenAlignment.Inset;
                float o = w / 2f;
                g.DrawLine(p, r.Left + o, r.Top + o, r.Left + len, r.Top + o);
                g.DrawLine(p, r.Left + o, r.Top + o, r.Left + o, r.Top + len);
                g.DrawLine(p, r.Right - o - 1, r.Bottom - o - 1, r.Right - len - 1, r.Bottom - o - 1);
                g.DrawLine(p, r.Right - o - 1, r.Bottom - o - 1, r.Right - o - 1, r.Bottom - len - 1);
            }
        }

        private static void PaintBlueprintGrid(object sender, PaintEventArgs e)
        {
            Control c = sender as Control;
            if (c == null) return;
            DrawBlueprintGrid(e.Graphics, e.ClipRectangle, e.Graphics.DpiX / 96f);
        }

        private static void PaintHeaderStripe(object sender, PaintEventArgs e)
        {
            Control c = sender as Control;
            if (c == null) return;
            float s = e.Graphics.DpiX / 96f;
            using (SolidBrush b = new SolidBrush(HeaderAccent))
            {
                e.Graphics.FillRectangle(b, 0, 0, Math.Max(3, (int)Math.Round(4 * s)), c.Height);
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
            int h = Math.Max(2, (int)Math.Round(2 * s));
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
            using (SolidBrush b = new SolidBrush(Accent))
            {
                e.Graphics.FillRectangle(b, 0, y, right, h);
            }
        }
    }

    /// <summary>Đánh dấu control tự vẽ theo token HDToolTheme (bộ khoác áo không đổi màu của nó).</summary>
    public interface HDToolPaintedControl { }

    // =====================================================================================
    // CÁC CONTROL KỸ THUẬT DÙNG CHUNG (tự vẽ, tự co giãn theo DPI)
    // =====================================================================================

    /// <summary>Dải tiêu đề 58px: vạch nhấn trái, mã công cụ HDT-xx, tiêu đề, mô tả, nhắc phím ESC.</summary>
    public class HDToolHeader : Control, HDToolPaintedControl
    {
        public string Code = "HDT";
        public string Title = "";
        public string Subtitle = "";
        public bool ShowEscHint = true;

        public HDToolHeader()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            Dock = DockStyle.Top;
            Height = HDToolTheme.HeaderHeight;
            BackColor = HDToolTheme.Header;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            float s = g.DpiX / 96f;
            g.Clear(HDToolTheme.Header);
            using (SolidBrush b = new SolidBrush(HDToolTheme.HeaderAccent))
            {
                g.FillRectangle(b, 0, 0, Math.Max(3, (int)Math.Round(4 * s)), Height);
            }
            using (Pen p = new Pen(HDToolTheme.HeaderLine))
            {
                g.DrawLine(p, 0, Height - 1, Width, Height - 1);
            }

            int x = (int)Math.Round(20 * s);
            int right = Width - (int)Math.Round(16 * s);

            // Nhắc phím ESC (bên phải)
            if (ShowEscHint)
            {
                string esc = "ESC · ĐÓNG";
                Size es = TextRenderer.MeasureText(g, esc, HDToolTheme.MonoSmall, Size.Empty, TextFormatFlags.NoPadding);
                Rectangle er = new Rectangle(right - es.Width - (int)(12 * s), (Height - es.Height - (int)(8 * s)) / 2, es.Width + (int)(12 * s), es.Height + (int)(8 * s));
                using (Pen p = new Pen(HDToolTheme.HeaderLine)) g.DrawRectangle(p, er);
                TextRenderer.DrawText(g, esc, HDToolTheme.MonoSmall, er, HDToolTheme.HeaderSub, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                right = er.Left - (int)Math.Round(12 * s);
            }

            // Mã công cụ
            if (!string.IsNullOrEmpty(Code))
            {
                Size cs = TextRenderer.MeasureText(g, Code, HDToolTheme.MonoSmall, Size.Empty, TextFormatFlags.NoPadding);
                Rectangle cr = new Rectangle(x, (Height - cs.Height - (int)(8 * s)) / 2, cs.Width + (int)(12 * s), cs.Height + (int)(8 * s));
                using (Pen p = new Pen(HDToolTheme.HeaderAccent)) g.DrawRectangle(p, cr);
                TextRenderer.DrawText(g, Code, HDToolTheme.MonoSmall, cr, HDToolTheme.HeaderAccent, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                x = cr.Right + (int)Math.Round(14 * s);
            }

            int w = Math.Max(10, right - x);
            Size ts = TextRenderer.MeasureText(g, "Ag", HDToolTheme.Title, Size.Empty, TextFormatFlags.NoPadding);
            Size ss = TextRenderer.MeasureText(g, "Ag", HDToolTheme.Caption, Size.Empty, TextFormatFlags.NoPadding);
            int total = ts.Height + (string.IsNullOrEmpty(Subtitle) ? 0 : ss.Height + (int)(2 * s));
            int y = (Height - total) / 2;
            TextRenderer.DrawText(g, Title ?? "", HDToolTheme.Title, new Rectangle(x, y, w, ts.Height), HDToolTheme.HeaderText,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            if (!string.IsNullOrEmpty(Subtitle))
            {
                TextRenderer.DrawText(g, Subtitle, HDToolTheme.Caption, new Rectangle(x, y + ts.Height + (int)(2 * s), w, ss.Height), HDToolTheme.HeaderSub,
                    TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            }
        }
    }

    /// <summary>Nhãn mục kỹ thuật: "01  TIÊU ĐỀ  ───┼────┼───".</summary>
    public class HDToolSectionLabel : Control, HDToolPaintedControl
    {
        public string Number = "01";

        public HDToolSectionLabel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = 28;
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            float s = g.DpiX / 96f;
            string caps = (Text ?? "").ToUpperInvariant();
            Size ns = TextRenderer.MeasureText(g, Number, HDToolTheme.MonoSmall, Size.Empty, TextFormatFlags.NoPadding);
            Size ts = TextRenderer.MeasureText(g, caps, HDToolTheme.LabelFont, Size.Empty, TextFormatFlags.NoPadding);
            int cy = Height / 2;
            TextRenderer.DrawText(g, Number, HDToolTheme.MonoSmall, new Point(0, cy - ns.Height / 2), HDToolTheme.Accent, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            int tx = ns.Width + (int)Math.Round(8 * s);
            TextRenderer.DrawText(g, caps, HDToolTheme.LabelFont, new Point(tx, cy - ts.Height / 2), HDToolTheme.Text2, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            int lx = tx + ts.Width + (int)Math.Round(10 * s);
            if (lx < Width - 4)
            {
                using (Pen p = new Pen(HDToolTheme.Line))
                using (Pen pt = new Pen(HDToolTheme.LineStrong))
                {
                    g.DrawLine(p, lx, cy, Width - 1, cy);
                    int step = (int)Math.Round(25 * s);
                    int tick = (int)Math.Round(3 * s);
                    for (int x = lx; x < Width; x += step) g.DrawLine(pt, x, cy - tick, x, cy);
                }
            }
        }
    }

    /// <summary>Thanh trạng thái kiểu dòng lệnh CAD: ô trạng thái + chữ Consolas.</summary>
    public class HDToolStatusText : Control, HDToolPaintedControl
    {
        private Color _dot = Color.Empty;
        public Color DotColor { get { return _dot.IsEmpty ? HDToolTheme.Ok : _dot; } set { _dot = value; Invalidate(); } }

        public HDToolStatusText()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            float s = g.DpiX / 96f;
            int d = (int)Math.Round(8 * s);
            using (SolidBrush b = new SolidBrush(DotColor))
            {
                g.FillRectangle(b, 0, (Height - d) / 2, d, d);
            }
            int x = d + (int)Math.Round(10 * s);
            string prefix = "▶ ";
            Size ps = TextRenderer.MeasureText(g, prefix, HDToolTheme.Mono, Size.Empty, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, prefix, HDToolTheme.Mono, new Rectangle(x, 0, ps.Width, Height), HDToolTheme.Accent, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, Text ?? "", HDToolTheme.Mono, new Rectangle(x + ps.Width, 0, Math.Max(1, Width - x - ps.Width), Height), HDToolTheme.Text2,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }
    }

    /// <summary>Khung có góc ngắm (corner tick) và viền 1px.</summary>
    public class HDToolTickPanel : Panel, HDToolPaintedControl
    {
        public HDToolTickPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = HDToolTheme.Surface;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen p = new Pen(HDToolTheme.Line))
            {
                e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            }
            HDToolTheme.DrawCornerTicks(e.Graphics, ClientRectangle, e.Graphics.DpiX / 96f);
        }
    }

    /// <summary>Dòng công cụ trong Menu: icon + mã HDT-xx + tên + mô tả + mũi tên; rê chuột có vạch nhấn.</summary>
    public class HDToolToolRow : Control, HDToolPaintedControl
    {
        public string IconKey = "menu";
        public string Code = "";
        public string Title = "";
        public string Description = "";
        public bool ShowBottomLine = true;
        private bool _hover;

        public HDToolToolRow()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable, true);
            Height = 46;
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                OnClick(EventArgs.Empty);
                e.Handled = true;
                return;
            }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            float s = g.DpiX / 96f;
            bool hot = _hover || Focused;
            g.Clear(hot ? HDToolTheme.AccentSoft : HDToolTheme.Surface);
            if (hot)
            {
                using (SolidBrush b = new SolidBrush(HDToolTheme.Accent)) g.FillRectangle(b, 0, 0, Math.Max(2, (int)Math.Round(3 * s)), Height);
            }
            if (ShowBottomLine)
            {
                using (Pen p = new Pen(HDToolTheme.Line)) g.DrawLine(p, 0, Height - 1, Width, Height - 1);
            }

            int pad = (int)Math.Round(12 * s);
            int icon = (int)Math.Round(24 * s);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            HDToolIcons.Draw(g, IconKey, new RectangleF(pad, (Height - icon) / 2f, icon, icon), HDToolIcons.InkLight, HDToolTheme.Accent);
            g.SmoothingMode = SmoothingMode.Default;

            int codeX = pad + icon + (int)Math.Round(10 * s);
            int codeW = (int)Math.Round(64 * s);
            TextRenderer.DrawText(g, Code, HDToolTheme.MonoSmall, new Rectangle(codeX, 0, codeW, Height), HDToolTheme.Accent, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

            int chev = (int)Math.Round(14 * s);
            int textX = codeX + codeW + (int)Math.Round(10 * s);
            int textW = Math.Max(10, Width - textX - chev - pad * 2);
            Size t1 = TextRenderer.MeasureText(g, "Ag", HDToolTheme.BodyBold, Size.Empty, TextFormatFlags.NoPadding);
            Size t2 = TextRenderer.MeasureText(g, "Ag", HDToolTheme.Caption, Size.Empty, TextFormatFlags.NoPadding);
            int y = (Height - t1.Height - t2.Height - (int)(1 * s)) / 2;
            TextRenderer.DrawText(g, Title, HDToolTheme.BodyBold, new Rectangle(textX, y, textW, t1.Height), HDToolTheme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            TextRenderer.DrawText(g, Description, HDToolTheme.Caption, new Rectangle(textX, y + t1.Height + (int)(1 * s), textW, t2.Height), HDToolTheme.Text3, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

            float cx = Width - pad - chev / 2f, cyy = Height / 2f, cw = chev * 0.25f;
            using (Pen p = new Pen(hot ? HDToolTheme.Accent : HDToolTheme.Text3, Math.Max(1.2f, 1.6f * s)))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawLines(p, new PointF[] { new PointF(cx - cw, cyy - cw * 2), new PointF(cx + cw, cyy), new PointF(cx - cw, cyy + cw * 2) });
            }
        }
    }
}
