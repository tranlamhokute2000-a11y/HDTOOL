using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace HDTool
{
    // =====================================================================================
    // CONTROL GIAO DIỆN v2 (Tối Cao Cấp) — tự vẽ theo token HDToolTheme, tự co giãn theo DPI.
    // Mẫu duyệt: PreviewStudio/HDTool_GiaoDienMoi.html
    // =====================================================================================

    internal static class HDDraw
    {
        public const TextFormatFlags One = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;

        public static float Scale(Graphics g) { return g.DpiX / 96f; }
        public static int S(Graphics g, float v) { return (int)Math.Round(v * g.DpiX / 96f); }

        public static void ClearParent(Graphics g, Control c)
        {
            Color back = c.Parent != null ? c.Parent.BackColor : HDToolTheme.Bg;
            if (back.A == 0) back = HDToolTheme.Bg;
            g.Clear(back);
        }

        public static void FillRound(Graphics g, Rectangle r, int radius, Color fill)
        {
            using (GraphicsPath p = HDToolTheme.RoundRect(r, radius))
            using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, p);
        }

        public static void StrokeRound(Graphics g, Rectangle r, int radius, Color line, float width)
        {
            using (GraphicsPath p = HDToolTheme.RoundRect(new RectangleF(r.X + width / 2f, r.Y + width / 2f, r.Width - width, r.Height - width), radius))
            using (Pen pen = new Pen(line, width)) g.DrawPath(pen, p);
        }

        public static Size Measure(Graphics g, string t, Font f)
        {
            return TextRenderer.MeasureText(g, t ?? "", f, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }
    }

    /// <summary>Dải đầu cửa sổ: nền chuyển màu, ô icon công cụ, tiêu đề, mô tả, các chip bên phải (vd. "v110", "ESC đóng").</summary>
    public class HDToolHeader : Control, HDToolPaintedControl
    {
        public string IconKey = "menu";
        public string Title = "";
        public string Subtitle = "";
        public string[] Chips = new string[] { "ESC đóng" };

        public HDToolHeader()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            Dock = DockStyle.Top;
            Height = 68;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (HDToolTheme.IsPremium) HDToolTheme.FillHeaderGradient(g, ClientRectangle);
            else
            {
                g.Clear(HDToolTheme.Header);
                using (SolidBrush b = new SolidBrush(HDToolTheme.HeaderAccent)) g.FillRectangle(b, 0, 0, HDDraw.S(g, 4), Height);
            }
            using (Pen p = new Pen(HDToolTheme.HeaderLine)) g.DrawLine(p, 0, Height - 1, Width, Height - 1);

            int pad = HDDraw.S(g, 18);
            int tile = HDDraw.S(g, 42);
            Rectangle tr = new Rectangle(pad, (Height - tile) / 2, tile, tile);
            HDDraw.FillRound(g, tr, HDDraw.S(g, HDToolTheme.RadiusSm), Color.FromArgb(40, 255, 255, 255));
            HDDraw.StrokeRound(g, tr, HDDraw.S(g, HDToolTheme.RadiusSm), Color.FromArgb(70, 255, 255, 255), 1f);
            int ic = HDDraw.S(g, 26);
            HDToolIcons.Draw(g, IconKey, new RectangleF(tr.X + (tile - ic) / 2f, tr.Y + (tile - ic) / 2f, ic, ic), Color.White, Color.White);

            // Chip bên phải
            int right = Width - pad;
            if (Chips != null)
            {
                for (int i = Chips.Length - 1; i >= 0; i--)
                {
                    string t = Chips[i];
                    if (string.IsNullOrEmpty(t)) continue;
                    Size ts = HDDraw.Measure(g, t, HDToolTheme.Caption);
                    int h = ts.Height + HDDraw.S(g, 8), w = ts.Width + HDDraw.S(g, 20);
                    Rectangle cr = new Rectangle(right - w, (Height - h) / 2, w, h);
                    HDDraw.FillRound(g, cr, h / 2, Color.FromArgb(34, 255, 255, 255));
                    HDDraw.StrokeRound(g, cr, h / 2, Color.FromArgb(70, 255, 255, 255), 1f);
                    TextRenderer.DrawText(g, t, HDToolTheme.Caption, cr, HDToolTheme.HeaderText, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | HDDraw.One);
                    right = cr.Left - HDDraw.S(g, 8);
                }
            }

            int x = tr.Right + HDDraw.S(g, 14);
            int w2 = Math.Max(20, right - x - HDDraw.S(g, 8));
            Size t1 = HDDraw.Measure(g, "Ag", HDToolTheme.Hero);
            Size t2 = HDDraw.Measure(g, "Ag", HDToolTheme.Body);
            int total = t1.Height + (string.IsNullOrEmpty(Subtitle) ? 0 : t2.Height + HDDraw.S(g, 3));
            int y = (Height - total) / 2;
            TextRenderer.DrawText(g, Title ?? "", HDToolTheme.Hero, new Rectangle(x, y, w2, t1.Height), HDToolTheme.HeaderText, HDDraw.One);
            if (!string.IsNullOrEmpty(Subtitle))
                TextRenderer.DrawText(g, Subtitle, HDToolTheme.Body, new Rectangle(x, y + t1.Height + HDDraw.S(g, 3), w2, t2.Height), HDToolTheme.HeaderSub, HDDraw.One);
        }
    }

    /// <summary>Nhãn nhóm: chấm màu + CHỮ IN HOA + đường kẻ mảnh.</summary>
    public class HDToolSectionLabel : Control, HDToolPaintedControl
    {
        private Color _dot = Color.Empty;
        public Color DotColor { get { return _dot.IsEmpty ? HDToolTheme.Accent : _dot; } set { _dot = value; Invalidate(); } }

        public HDToolSectionLabel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            Height = 30;
        }

        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            HDDraw.ClearParent(g, this);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int d = HDDraw.S(g, 8), cy = Height / 2;
            using (SolidBrush b = new SolidBrush(DotColor))
            {
                if (HDToolTheme.IsPremium) g.FillEllipse(b, 0, cy - d / 2, d, d);
                else g.FillRectangle(b, 0, cy - d / 2, d, d);
            }
            string caps = (Text ?? "").ToUpperInvariant();
            Size ts = HDDraw.Measure(g, caps, HDToolTheme.LabelFont);
            int tx = d + HDDraw.S(g, 8);
            TextRenderer.DrawText(g, caps, HDToolTheme.LabelFont, new Point(tx, cy - ts.Height / 2), HDToolTheme.Text3, HDDraw.One);
            int lx = tx + ts.Width + HDDraw.S(g, 10);
            if (lx < Width) using (Pen p = new Pen(HDToolTheme.Line)) g.DrawLine(p, lx, cy, Width - 1, cy);
        }
    }

    /// <summary>Nút bo góc tự vẽ (Primary / Secondary / Ghost / Danger), có icon tùy chọn.</summary>
    public class HDToolButton : Button, HDToolPaintedControl
    {
        private HDToolButtonKind _kind = HDToolButtonKind.Secondary;
        private bool _hover, _down;
        public string IconKey = null;

        public HDToolButtonKind Kind { get { return _kind; } set { _kind = value; Invalidate(); } }

        public HDToolButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Font = HDToolTheme.Body;
            Height = 34;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

        public override Size GetPreferredSize(Size proposedSize)
        {
            using (Graphics g = CreateGraphics())
            {
                Size ts = HDDraw.Measure(g, Text, Font);
                int w = ts.Width + HDDraw.S(g, 32) + (string.IsNullOrEmpty(IconKey) ? 0 : HDDraw.S(g, 24));
                return new Size(w, Math.Max(Height, ts.Height + HDDraw.S(g, 16)));
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            HDDraw.ClearParent(g, this);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color fill, line, fore;
            switch (_kind)
            {
                case HDToolButtonKind.Primary:
                    fill = _down ? HDToolTheme.AccentDown : (_hover ? HDToolTheme.AccentHover : HDToolTheme.Accent);
                    line = fill; fore = HDToolTheme.AccentInk; break;
                case HDToolButtonKind.Danger:
                    fill = _hover ? HDToolTheme.ErrSoft : HDToolTheme.Surface;
                    line = HDToolTheme.Err; fore = HDToolTheme.Err; break;
                case HDToolButtonKind.Ghost:
                    fill = _hover ? HDToolTheme.AccentSoft : (Parent != null ? Parent.BackColor : HDToolTheme.Surface);
                    line = fill; fore = _hover ? HDToolTheme.Text : HDToolTheme.Text2; break;
                default:
                    fill = _down ? HDToolTheme.AccentSoft : (_hover ? HDToolTheme.SurfaceHover : HDToolTheme.Surface2);
                    line = _hover ? HDToolTheme.Accent : HDToolTheme.LineStrong; fore = HDToolTheme.Text; break;
            }
            if (!Enabled) { fill = HDToolTheme.Surface2; line = HDToolTheme.Line; fore = HDToolTheme.Text3; }
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            int rad = HDDraw.S(g, HDToolTheme.RadiusSm);
            HDDraw.FillRound(g, r, rad, fill);
            HDDraw.StrokeRound(g, new Rectangle(0, 0, Width, Height), rad, line, 1f);
            if (Focused && ShowFocusCues) HDDraw.StrokeRound(g, new Rectangle(1, 1, Width - 2, Height - 2), rad, Color.FromArgb(120, HDToolTheme.Accent), 2f);

            Font f = _kind == HDToolButtonKind.Primary ? new Font(Font, FontStyle.Bold) : Font;
            try
            {
                Size ts = HDDraw.Measure(g, Text, f);
                int icon = string.IsNullOrEmpty(IconKey) ? 0 : HDDraw.S(g, 16);
                int gap = icon > 0 ? HDDraw.S(g, 8) : 0;
                int total = icon + gap + ts.Width;
                int x = Math.Max(HDDraw.S(g, 8), (Width - total) / 2);
                if (icon > 0) HDToolIcons.Draw(g, IconKey, new RectangleF(x, (Height - icon) / 2f, icon, icon), fore, fore);
                TextRenderer.DrawText(g, Text, f, new Rectangle(x + icon + gap, 0, Math.Max(1, Width - x - icon - gap - HDDraw.S(g, 6)), Height), fore,
                    TextFormatFlags.VerticalCenter | HDDraw.One);
            }
            finally { if (!ReferenceEquals(f, Font)) f.Dispose(); }
        }
    }

    /// <summary>Thẻ công cụ trong Menu: ô icon màu nhận diện, tên, mô tả, phím tắt; rê chuột nổi viền màu công cụ.</summary>
    public class HDToolToolCard : Control, HDToolPaintedControl
    {
        public string IconKey = "menu";
        public string Title = "";
        public string Description = "";
        public string Shortcut = "";
        public string Keywords = "";
        private bool _hover;

        public HDToolToolCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable, true);
            Height = 76;
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        public Color ToolColor { get { return HDToolTheme.ToolColor(IconKey); } }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { OnClick(EventArgs.Empty); e.Handled = true; return; }
            base.OnKeyDown(e);
        }

        /// <summary>Kiểu hàng danh sách (Mac): nằm trong HDToolCardGrid.ListStyle, không tự vẽ khung thẻ.</summary>
        public bool RowStyle;
        /// <summary>Hàng đầu tiên đang hiện trong nhóm (không vẽ đường kẻ phía trên).</summary>
        public bool FirstRow;

        private void PaintRow(Graphics g)
        {
            HDDraw.ClearParent(g, this);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            bool hot = _hover || Focused;
            Color tc = ToolColor;
            int pad = HDDraw.S(g, 12);
            int tile = HDDraw.S(g, 30);
            if (!FirstRow)
            {
                using (Pen p = new Pen(HDToolTheme.Line)) g.DrawLine(p, pad + tile + HDDraw.S(g, 12), 0, Width - pad, 0);
            }
            if (hot)
            {
                HDDraw.FillRound(g, new Rectangle(HDDraw.S(g, 4), HDDraw.S(g, 2), Width - HDDraw.S(g, 8), Height - HDDraw.S(g, 4)),
                    HDDraw.S(g, 7), HDToolTheme.SurfaceHover);
            }
            Rectangle tr = new Rectangle(pad, (Height - tile) / 2, tile, tile);
            HDDraw.FillRound(g, tr, HDDraw.S(g, 7), tc);
            int ic = HDDraw.S(g, 20);
            HDToolIcons.Draw(g, IconKey, new RectangleF(tr.X + (tile - ic) / 2f, tr.Y + (tile - ic) / 2f, ic, ic), Color.White, Color.White);

            int x = tr.Right + HDDraw.S(g, 12);
            int right = Width - pad;
            Size ch = HDDraw.Measure(g, "›", HDToolTheme.Title);
            TextRenderer.DrawText(g, "›", HDToolTheme.Title, new Rectangle(right - ch.Width, 0, ch.Width, Height), HDToolTheme.Text3,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            right -= ch.Width + HDDraw.S(g, 10);
            if (!string.IsNullOrEmpty(Shortcut))
            {
                Size ks = HDDraw.Measure(g, Shortcut, HDToolTheme.MonoSmall);
                Rectangle kr = new Rectangle(right - ks.Width - HDDraw.S(g, 10), (Height - ks.Height - HDDraw.S(g, 4)) / 2, ks.Width + HDDraw.S(g, 10), ks.Height + HDDraw.S(g, 4));
                HDDraw.StrokeRound(g, kr, HDDraw.S(g, 4), HDToolTheme.LineStrong, 1f);
                TextRenderer.DrawText(g, Shortcut, HDToolTheme.MonoSmall, kr, HDToolTheme.Text3, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | HDDraw.One);
                right = kr.Left - HDDraw.S(g, 10);
            }
            int w = Math.Max(10, right - x);
            Size t1 = HDDraw.Measure(g, "Ag", HDToolTheme.BodyBold);
            Size t2 = HDDraw.Measure(g, "Ag", HDToolTheme.Caption);
            int top = (Height - t1.Height - t2.Height - HDDraw.S(g, 2)) / 2;
            TextRenderer.DrawText(g, Title, HDToolTheme.BodyBold, new Rectangle(x, top, w, t1.Height), HDToolTheme.Text, HDDraw.One);
            TextRenderer.DrawText(g, Description, HDToolTheme.Caption, new Rectangle(x, top + t1.Height + HDDraw.S(g, 2), w, t2.Height), HDToolTheme.Text3, HDDraw.One);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            if (RowStyle) { PaintRow(g); return; }
            HDDraw.ClearParent(g, this);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            bool hot = _hover || Focused;
            Color tc = ToolColor;
            int rad = HDDraw.S(g, HDToolTheme.Radius);
            int lift = hot && HDToolTheme.IsPremium ? HDDraw.S(g, 2) : 0;
            Rectangle card = new Rectangle(0, HDDraw.S(g, 2) - lift, Width - 1, Height - 1 - HDDraw.S(g, 4));

            if (HDToolTheme.IsPremium)
            {
                Rectangle sh = card; sh.Offset(0, HDDraw.S(g, hot ? 4 : 2));
                HDDraw.FillRound(g, sh, rad, Color.FromArgb(hot ? 90 : 55, 0, 0, 0));
            }
            HDDraw.FillRound(g, card, rad, hot ? HDToolTheme.SurfaceHover : HDToolTheme.Surface);
            HDDraw.StrokeRound(g, new Rectangle(card.X, card.Y, card.Width + 1, card.Height + 1), rad, hot ? HDToolTheme.Mix(HDToolTheme.Line, tc, 0.6) : HDToolTheme.Line, 1f);

            int pad = HDDraw.S(g, 12);
            int tile = HDDraw.S(g, 40);
            Rectangle tr = new Rectangle(card.X + pad, card.Y + pad, tile, tile);
            HDDraw.FillRound(g, tr, HDDraw.S(g, HDToolTheme.RadiusSm), HDToolTheme.Mix(HDToolTheme.Surface, tc, hot ? 0.30 : 0.20));
            int ic = HDDraw.S(g, 24);
            HDToolIcons.Draw(g, IconKey, new RectangleF(tr.X + (tile - ic) / 2f, tr.Y + (tile - ic) / 2f, ic, ic), HDToolTheme.Mix(tc, Color.White, 0.25), HDToolTheme.Mix(tc, Color.White, 0.25));

            int x = tr.Right + HDDraw.S(g, 12);
            int right = card.Right - pad;
            if (!string.IsNullOrEmpty(Shortcut))
            {
                Size ks = HDDraw.Measure(g, Shortcut, HDToolTheme.MonoSmall);
                Rectangle kr = new Rectangle(right - ks.Width - HDDraw.S(g, 10), card.Y + HDDraw.S(g, 10), ks.Width + HDDraw.S(g, 10), ks.Height + HDDraw.S(g, 4));
                HDDraw.FillRound(g, kr, HDDraw.S(g, 4), HDToolTheme.Surface2);
                HDDraw.StrokeRound(g, kr, HDDraw.S(g, 4), HDToolTheme.Line, 1f);
                TextRenderer.DrawText(g, Shortcut, HDToolTheme.MonoSmall, kr, HDToolTheme.Text3, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | HDDraw.One);
                right = kr.Left - HDDraw.S(g, 6);
            }
            int w = Math.Max(10, right - x);
            Size t1 = HDDraw.Measure(g, "Ag", HDToolTheme.CardTitle);
            TextRenderer.DrawText(g, Title, HDToolTheme.CardTitle, new Rectangle(x, tr.Y - HDDraw.S(g, 1), w, t1.Height), HDToolTheme.Text, HDDraw.One);
            int dy = tr.Y + t1.Height + HDDraw.S(g, 2);
            int dw = Math.Max(10, card.Right - pad - x);
            TextRenderer.DrawText(g, Description, HDToolTheme.Caption, new Rectangle(x, dy, dw, card.Bottom - dy - HDDraw.S(g, 6)), HDToolTheme.Text3,
                TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }
    }

    /// <summary>Lưới thẻ tự co giãn: xếp các control con thành N cột đều nhau, chỉ tính control đang hiện.</summary>
    public class HDToolCardGrid : Panel, HDToolPaintedControl
    {
        public int Columns = 2;
        public int Gap = 10;

        public HDToolCardGrid()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            BackColor = HDToolTheme.Bg;
        }

        /// <summary>Sắp xếp lại và trả về chiều cao cần thiết.</summary>
        /// <summary>Kiểu danh sách Mac: 1 cột, khung trắng bo góc, các hàng HDToolToolCard.RowStyle.</summary>
        public bool ListStyle;

        protected override void OnPaint(PaintEventArgs e)
        {
            if (!ListStyle) { base.OnPaint(e); return; }
            Graphics g = e.Graphics;
            HDDraw.ClearParent(g, this);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            HDDraw.FillRound(g, r, HDDraw.S(g, 10), HDToolTheme.Surface);
            HDDraw.StrokeRound(g, new Rectangle(0, 0, Width, Height), HDDraw.S(g, 10), HDToolTheme.Line, 1f);
        }

        public int Relayout(int width)
        {
            float s = DeviceScale();
            if (ListStyle)
            {
                BackColor = HDToolTheme.Surface;
                int pv = (int)Math.Round(6 * s), ly = pv;
                bool first = true;
                foreach (Control c in Controls)
                {
                    if (!c.Visible) continue;
                    HDToolToolCard card = c as HDToolToolCard;
                    if (card != null) { card.FirstRow = first; card.Invalidate(); }
                    first = false;
                    c.SetBounds(1, ly, Math.Max(60, width - 2), c.Height);
                    ly += c.Height;
                }
                Height = first ? 0 : ly + pv;
                Invalidate();
                return Height;
            }
            int gap = (int)Math.Round(Gap * s);
            int colW = Math.Max(60, (width - gap * (Columns - 1)) / Columns);
            int i = 0, y = 0, rowH = 0;
            foreach (Control c in Controls)
            {
                if (!c.Visible) continue;
                int col = i % Columns;
                if (col == 0 && i > 0) { y += rowH + gap; rowH = 0; }
                c.SetBounds(col * (colW + gap), y, colW, c.Height);
                rowH = Math.Max(rowH, c.Height);
                i++;
            }
            int h = i == 0 ? 0 : y + rowH;
            Height = h;
            return h;
        }

        public int VisibleCount
        {
            get { int n = 0; foreach (Control c in Controls) if (c.Visible) n++; return n; }
        }

        private float DeviceScale()
        {
            try { using (Graphics g = CreateGraphics()) return g.DpiX / 96f; } catch { return 1f; }
        }
    }

    /// <summary>Ô tìm kiếm bo góc: icon kính lúp, chữ gợi ý, phím tắt bên phải; dùng TextBox thật bên trong.</summary>
    public class HDToolSearchBox : Panel, HDToolPaintedControl
    {

        public readonly TextBox Box = new TextBox();
        private readonly Label _hint = new Label();
        private string _placeholder = "Tìm kiếm…";
        public string KeyHint = "Ctrl+F";

        public string Placeholder
        {
            get { return _placeholder; }
            set { _placeholder = value; _hint.Text = value; }
        }

        public HDToolSearchBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            Height = 38;
            Box.BorderStyle = BorderStyle.None;
            Box.BackColor = HDToolTheme.Input;
            Box.ForeColor = HDToolTheme.Text;
            Box.Font = new Font("Segoe UI", 10f);
            // Nhãn gợi ý tự vẽ (không phụ thuộc EM_SETCUEBANNER / visual styles của Revit)
            _hint.Text = _placeholder;
            _hint.Font = Box.Font;
            _hint.ForeColor = HDToolTheme.Text3;
            _hint.BackColor = HDToolTheme.Input;
            _hint.AutoSize = false;
            _hint.AutoEllipsis = true;
            _hint.TextAlign = ContentAlignment.MiddleLeft;
            _hint.UseMnemonic = false;
            _hint.Cursor = Cursors.IBeam;
            _hint.Click += delegate { Box.Focus(); };
            Controls.Add(_hint);
            Controls.Add(Box);
            _hint.BringToFront();
            Box.TextChanged += delegate { _hint.Visible = Box.Text.Length == 0; };
            Box.GotFocus += delegate { Invalidate(); };
            Box.LostFocus += delegate { Invalidate(); };
            Cursor = Cursors.IBeam;
            Click += delegate { Box.Focus(); };
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            float s = DeviceDpiScale();
            int left = (int)Math.Round(38 * s);
            int right = (int)Math.Round((string.IsNullOrEmpty(KeyHint) ? 12 : 70) * s);
            int h = Box.PreferredHeight;
            Box.SetBounds(left, (Height - h) / 2, Math.Max(20, Width - left - right), h);
            _hint.SetBounds(Box.Left, Box.Top, Box.Width, Box.Height);
        }

        private float DeviceDpiScale()
        {
            try { using (Graphics g = CreateGraphics()) return g.DpiX / 96f; } catch { return 1f; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            HDDraw.ClearParent(g, this);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int rad = HDDraw.S(g, HDToolTheme.RadiusSm);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            HDDraw.FillRound(g, r, rad, HDToolTheme.Input);
            bool foc = Box.Focused;
            HDDraw.StrokeRound(g, new Rectangle(0, 0, Width, Height), rad, foc ? HDToolTheme.Accent : HDToolTheme.LineStrong, foc ? 2f : 1f);
            // Kính lúp
            float s = HDDraw.Scale(g);
            using (Pen p = new Pen(HDToolTheme.Text3, Math.Max(1.4f, 1.6f * s)))
            {
                float cx = 18 * s, cy = Height / 2f - 1 * s, rr = 6 * s;
                g.DrawEllipse(p, cx - rr, cy - rr, rr * 2, rr * 2);
                g.DrawLine(p, cx + rr * 0.72f, cy + rr * 0.72f, cx + rr * 1.5f, cy + rr * 1.5f);
            }
            if (!string.IsNullOrEmpty(KeyHint))
            {
                Size ks = HDDraw.Measure(g, KeyHint, HDToolTheme.MonoSmall);
                Rectangle kr = new Rectangle(Width - ks.Width - HDDraw.S(g, 22), (Height - ks.Height - HDDraw.S(g, 6)) / 2, ks.Width + HDDraw.S(g, 12), ks.Height + HDDraw.S(g, 6));
                HDDraw.FillRound(g, kr, HDDraw.S(g, 4), HDToolTheme.Surface2);
                HDDraw.StrokeRound(g, kr, HDDraw.S(g, 4), HDToolTheme.Line, 1f);
                TextRenderer.DrawText(g, KeyHint, HDToolTheme.MonoSmall, kr, HDToolTheme.Text3, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | HDDraw.One);
            }
        }
    }

    /// <summary>Thanh thông tin bo góc nền nhấn nhạt: nhãn + giá trị đậm + chi tiết Consolas.</summary>
    public class HDToolInfoBar : Control, HDToolPaintedControl
    {
        public string Label = "Đang mở:";
        public string Value = "";
        public string Detail = "";

        public HDToolInfoBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            Height = 40;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            HDDraw.ClearParent(g, this);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            HDDraw.FillRound(g, new Rectangle(0, 0, Width - 1, Height - 1), HDDraw.S(g, HDToolTheme.Radius), HDToolTheme.AccentSoft);
            int x = HDDraw.S(g, 14);
            Size a = HDDraw.Measure(g, Label, HDToolTheme.Body);
            TextRenderer.DrawText(g, Label, HDToolTheme.Body, new Rectangle(x, 0, a.Width + 2, Height), HDToolTheme.Text2, TextFormatFlags.VerticalCenter | HDDraw.One);
            x += a.Width + HDDraw.S(g, 8);
            Size b = HDDraw.Measure(g, Value, HDToolTheme.BodyBold);
            int maxV = Math.Max(20, Width - x - HDDraw.S(g, 140));
            int vw = Math.Min(b.Width + 2, maxV);
            TextRenderer.DrawText(g, Value, HDToolTheme.BodyBold, new Rectangle(x, 0, vw, Height), HDToolTheme.Text, TextFormatFlags.VerticalCenter | HDDraw.One);
            x += vw + HDDraw.S(g, 10);
            TextRenderer.DrawText(g, Detail, HDToolTheme.Mono, new Rectangle(x, 0, Math.Max(10, Width - x - HDDraw.S(g, 12)), Height), HDToolTheme.Text2, TextFormatFlags.VerticalCenter | HDDraw.One);
        }
    }

    /// <summary>Nút viên thuốc (dùng gần đây): chấm màu công cụ + tên.</summary>
    public class HDToolPill : Control, HDToolPaintedControl
    {
        public string IconKey = "menu";
        private bool _hover;

        public HDToolPill()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            Height = 32;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        public int PreferredWidth
        {
            get
            {
                using (Graphics g = CreateGraphics())
                {
                    return HDDraw.Measure(g, Text, HDToolTheme.Body).Width + HDDraw.S(g, 40);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            HDDraw.ClearParent(g, this);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color tc = HDToolTheme.ToolColor(IconKey);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            int rad = HDToolTheme.IsPremium ? Height / 2 : 0;
            HDDraw.FillRound(g, r, rad, _hover ? HDToolTheme.SurfaceHover : HDToolTheme.Surface);
            HDDraw.StrokeRound(g, new Rectangle(0, 0, Width, Height), rad, _hover ? tc : HDToolTheme.Line, 1f);
            int d = HDDraw.S(g, 8), x = HDDraw.S(g, 14);
            using (SolidBrush b = new SolidBrush(tc)) g.FillEllipse(b, x, (Height - d) / 2, d, d);
            TextRenderer.DrawText(g, Text, HDToolTheme.Body, new Rectangle(x + d + HDDraw.S(g, 8), 0, Width - x - d - HDDraw.S(g, 14), Height), HDToolTheme.Text, TextFormatFlags.VerticalCenter | HDDraw.One);
        }
    }

    /// <summary>Thanh các bước thao tác: bước xong (vòng xanh ✓), bước hiện tại (vòng nhấn), bước chưa tới (vòng xám).</summary>
    public class HDToolStepBar : Control, HDToolPaintedControl
    {
        private string[] _steps = new string[0];
        private int _current;

        public string[] Steps { get { return _steps; } set { _steps = value ?? new string[0]; Invalidate(); } }
        /// <summary>Chỉ số bước hiện tại (các bước trước đó coi như đã xong). Bằng Steps.Length = tất cả đã xong.</summary>
        public int Current { get { return _current; } set { if (_current != value) { _current = value; Invalidate(); } } }

        public HDToolStepBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            Height = 48;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(HDToolTheme.Surface);
            using (Pen p = new Pen(HDToolTheme.Line)) g.DrawLine(p, 0, Height - 1, Width, Height - 1);
            if (_steps.Length == 0) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int pad = HDDraw.S(g, 18), d = HDDraw.S(g, 24), cy = Height / 2;
            // Đo tổng chiều rộng chữ để chia đều phần đường nối
            int[] tw = new int[_steps.Length];
            int textTotal = 0;
            for (int i = 0; i < _steps.Length; i++)
            {
                Font f = i == _current ? HDToolTheme.BodyBold : HDToolTheme.Body;
                tw[i] = HDDraw.Measure(g, _steps[i], f).Width;
                textTotal += d + HDDraw.S(g, 8) + tw[i];
            }
            int lineTotal = Math.Max(0, Width - pad * 2 - textTotal);
            int seg = _steps.Length > 1 ? lineTotal / (_steps.Length - 1) : 0;
            int x = pad;
            for (int i = 0; i < _steps.Length; i++)
            {
                bool done = i < _current, cur = i == _current;
                Rectangle cr = new Rectangle(x, cy - d / 2, d, d);
                Color ring = done ? HDToolTheme.Ok : (cur ? HDToolTheme.Accent : HDToolTheme.LineStrong);
                if (cur)
                {
                    Rectangle halo = cr; halo.Inflate(HDDraw.S(g, 4), HDDraw.S(g, 4));
                    using (SolidBrush hb = new SolidBrush(HDToolTheme.AccentSoft)) g.FillEllipse(hb, halo);
                }
                using (SolidBrush b = new SolidBrush(done || cur ? ring : HDToolTheme.Surface)) g.FillEllipse(b, cr);
                using (Pen p = new Pen(ring, Math.Max(1.5f, 2f * HDDraw.Scale(g)))) g.DrawEllipse(p, cr);
                if (done)
                {
                    using (Pen ck = new Pen(Color.White, Math.Max(1.6f, 2f * HDDraw.Scale(g))))
                    {
                        float s = d / 24f;
                        g.DrawLines(ck, new PointF[] { new PointF(cr.X + 7 * s, cr.Y + 12.5f * s), new PointF(cr.X + 10.5f * s, cr.Y + 16 * s), new PointF(cr.X + 17 * s, cr.Y + 8.5f * s) });
                    }
                }
                else
                {
                    TextRenderer.DrawText(g, (i + 1).ToString(), HDToolTheme.MonoSmall, cr, cur ? HDToolTheme.AccentInk : HDToolTheme.Text3,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | HDDraw.One);
                }
                int tx = cr.Right + HDDraw.S(g, 8);
                Font tf = cur ? HDToolTheme.BodyBold : HDToolTheme.Body;
                TextRenderer.DrawText(g, _steps[i], tf, new Rectangle(tx, 0, tw[i] + 4, Height), cur ? HDToolTheme.Text : (done ? HDToolTheme.Text2 : HDToolTheme.Text3),
                    TextFormatFlags.VerticalCenter | HDDraw.One);
                x = tx + tw[i];
                if (i < _steps.Length - 1)
                {
                    int l1 = x + HDDraw.S(g, 10), l2 = x + seg - HDDraw.S(g, 10);
                    if (l2 > l1)
                        using (Pen lp = new Pen(done ? HDToolTheme.Ok : HDToolTheme.Line, Math.Max(1.5f, 2f * HDDraw.Scale(g)))) g.DrawLine(lp, l1, cy, l2, cy);
                    x += seg;
                }
            }
        }
    }

    /// <summary>Nút chọn dạng phân đoạn (một trong nhiều lựa chọn).</summary>
    public class HDToolSegmented : Control, HDToolPaintedControl
    {
        private string[] _items = new string[0];
        private int _selected;
        private int _hover = -1;
        public event EventHandler SelectedIndexChanged;

        public string[] Items { get { return _items; } set { _items = value ?? new string[0]; Invalidate(); } }
        public int SelectedIndex
        {
            get { return _selected; }
            set { if (_selected == value) return; _selected = value; Invalidate(); if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty); }
        }
        public void SetItemText(int i, string t) { if (i >= 0 && i < _items.Length && _items[i] != t) { _items[i] = t; Invalidate(); } }

        public HDToolSegmented()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            Height = 34;
            Cursor = Cursors.Hand;
        }

        private Rectangle[] ItemRects(Graphics g)
        {
            Rectangle[] r = new Rectangle[_items.Length];
            int pad = HDDraw.S(g, 3), x = pad;
            for (int i = 0; i < _items.Length; i++)
            {
                int w = HDDraw.Measure(g, _items[i], HDToolTheme.BodyBold).Width + HDDraw.S(g, 24);
                r[i] = new Rectangle(x, pad, w, Height - pad * 2);
                x += w;
            }
            return r;
        }

        public int PreferredWidth
        {
            get
            {
                using (Graphics g = CreateGraphics())
                {
                    Rectangle[] r = ItemRects(g);
                    return r.Length == 0 ? 40 : r[r.Length - 1].Right + HDDraw.S(g, 3);
                }
            }
        }

        private int HitTest(Point p)
        {
            using (Graphics g = CreateGraphics())
            {
                Rectangle[] r = ItemRects(g);
                for (int i = 0; i < r.Length; i++) if (r[i].Contains(p)) return i;
            }
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e) { int h = HitTest(e.Location); if (h != _hover) { _hover = h; Invalidate(); } base.OnMouseMove(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = -1; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseClick(MouseEventArgs e) { int h = HitTest(e.Location); if (h >= 0) SelectedIndex = h; base.OnMouseClick(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            HDDraw.ClearParent(g, this);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int rad = HDDraw.S(g, HDToolTheme.RadiusSm);
            HDDraw.FillRound(g, new Rectangle(0, 0, Width - 1, Height - 1), rad, HDToolTheme.Surface2);
            HDDraw.StrokeRound(g, new Rectangle(0, 0, Width, Height), rad, HDToolTheme.Line, 1f);
            Rectangle[] r = ItemRects(g);
            for (int i = 0; i < r.Length; i++)
            {
                bool sel = i == _selected;
                if (sel)
                {
                    HDDraw.FillRound(g, r[i], Math.Max(0, rad - HDDraw.S(g, 2)), HDToolTheme.AccentSoft);
                    HDDraw.StrokeRound(g, r[i], Math.Max(0, rad - HDDraw.S(g, 2)), HDToolTheme.Mix(HDToolTheme.AccentSoft, HDToolTheme.Accent, 0.5), 1f);
                }
                else if (i == _hover) HDDraw.FillRound(g, r[i], Math.Max(0, rad - HDDraw.S(g, 2)), HDToolTheme.SurfaceHover);
                TextRenderer.DrawText(g, _items[i], sel ? HDToolTheme.BodyBold : HDToolTheme.Body, r[i], sel ? HDToolTheme.Text : HDToolTheme.Text2,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | HDDraw.One);
            }
        }
    }

    /// <summary>Chip bật/tắt (vẫn là CheckBox nên giữ nguyên Checked / CheckedChanged): viên thuốc có chấm màu.</summary>
    public class HDToolChipToggle : CheckBox, HDToolPaintedControl
    {
        private bool _hover;
        public Color ChipColor = Color.Empty;

        public HDToolChipToggle()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Height = 30;
            AutoSize = false;
        }

        public int PreferredWidth
        {
            get { using (Graphics g = CreateGraphics()) return HDDraw.Measure(g, Text, HDToolTheme.Body).Width + HDDraw.S(g, 46); }
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            HDDraw.ClearParent(g, this);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color c = ChipColor.IsEmpty ? HDToolTheme.Accent : ChipColor;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            int rad = HDToolTheme.IsPremium ? Height / 2 : 0;
            Color fill = Checked ? HDToolTheme.Mix(HDToolTheme.Surface, c, 0.22) : (_hover ? HDToolTheme.SurfaceHover : HDToolTheme.Surface);
            HDDraw.FillRound(g, r, rad, fill);
            HDDraw.StrokeRound(g, new Rectangle(0, 0, Width, Height), rad, Checked ? HDToolTheme.Mix(HDToolTheme.Line, c, 0.7) : HDToolTheme.Line, 1f);
            int d = HDDraw.S(g, 14), x = HDDraw.S(g, 10);
            Rectangle box = new Rectangle(x, (Height - d) / 2, d, d);
            if (Checked)
            {
                using (SolidBrush b = new SolidBrush(c)) g.FillEllipse(b, box);
                using (Pen ck = new Pen(Color.White, Math.Max(1.4f, 1.8f * HDDraw.Scale(g))))
                {
                    float s = d / 14f;
                    g.DrawLines(ck, new PointF[] { new PointF(box.X + 3.5f * s, box.Y + 7.2f * s), new PointF(box.X + 6 * s, box.Y + 9.6f * s), new PointF(box.X + 10.5f * s, box.Y + 4.6f * s) });
                }
            }
            else using (Pen p = new Pen(HDToolTheme.LineStrong, Math.Max(1.2f, 1.5f * HDDraw.Scale(g)))) g.DrawEllipse(p, box);
            TextRenderer.DrawText(g, Text, HDToolTheme.Body, new Rectangle(box.Right + HDDraw.S(g, 8), 0, Width - box.Right - HDDraw.S(g, 12), Height),
                Checked ? HDToolTheme.Text : HDToolTheme.Text3, TextFormatFlags.VerticalCenter | HDDraw.One);
        }
    }

    /// <summary>Công tắc bật/tắt (vẫn là CheckBox).</summary>
    public class HDToolSwitch : CheckBox, HDToolPaintedControl
    {
        public HDToolSwitch()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Height = 30;
            AutoSize = false;
        }

        public int PreferredWidth
        {
            get { using (Graphics g = CreateGraphics()) return HDDraw.Measure(g, Text, HDToolTheme.Body).Width + HDDraw.S(g, 52); }
        }

        protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            HDDraw.ClearParent(g, this);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int w = HDDraw.S(g, 34), h = HDDraw.S(g, 18);
            Rectangle tr = new Rectangle(0, (Height - h) / 2, w, h);
            HDDraw.FillRound(g, tr, h / 2, Checked ? HDToolTheme.Accent : HDToolTheme.LineStrong);
            int k = h - HDDraw.S(g, 4);
            Rectangle knob = new Rectangle(Checked ? tr.Right - k - HDDraw.S(g, 2) : tr.X + HDDraw.S(g, 2), tr.Y + HDDraw.S(g, 2), k, k);
            using (SolidBrush b = new SolidBrush(Color.White)) g.FillEllipse(b, knob);
            TextRenderer.DrawText(g, Text, HDToolTheme.Body, new Rectangle(tr.Right + HDDraw.S(g, 10), 0, Width - tr.Right - HDDraw.S(g, 10), Height),
                Checked ? HDToolTheme.Text : HDToolTheme.Text2, TextFormatFlags.VerticalCenter | HDDraw.One);
        }
    }

    /// <summary>Ô số liệu: vòng icon màu + nhãn + giá trị lớn (Consolas) + đơn vị.</summary>
    public class HDToolKpiCard : Control, HDToolPaintedControl
    {
        public string IconKey = "area";
        public Color KpiColor = Color.Empty;
        public string Label = "";
        private string _value = "—";
        public string Unit = "";
        public string Value { get { return _value; } set { _value = value; Invalidate(); } }

        public HDToolKpiCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            Height = 76;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            HDDraw.ClearParent(g, this);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color c = KpiColor.IsEmpty ? HDToolTheme.Accent : KpiColor;
            int rad = HDDraw.S(g, HDToolTheme.Radius);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            HDDraw.FillRound(g, r, rad, HDToolTheme.Surface);
            HDDraw.StrokeRound(g, new Rectangle(0, 0, Width, Height), rad, HDToolTheme.Line, 1f);
            int pad = HDDraw.S(g, 14), d = HDDraw.S(g, 42);
            Rectangle cr = new Rectangle(pad, (Height - d) / 2, d, d);
            using (SolidBrush b = new SolidBrush(HDToolTheme.Mix(HDToolTheme.Surface, c, 0.22))) g.FillEllipse(b, cr);
            int ic = HDDraw.S(g, 22);
            HDToolIcons.Draw(g, IconKey, new RectangleF(cr.X + (d - ic) / 2f, cr.Y + (d - ic) / 2f, ic, ic), HDToolTheme.Mix(c, Color.White, 0.2), c);
            int x = cr.Right + HDDraw.S(g, 12), w = Math.Max(10, Width - x - pad);
            Size ls = HDDraw.Measure(g, "Ag", HDToolTheme.Caption);
            Size vs = HDDraw.Measure(g, _value ?? "", HDToolTheme.Readout);
            int top = (Height - ls.Height - vs.Height) / 2;
            TextRenderer.DrawText(g, Label, HDToolTheme.Caption, new Rectangle(x, top, w, ls.Height), HDToolTheme.Text3, HDDraw.One);
            int vw = Math.Min(vs.Width, w);
            TextRenderer.DrawText(g, _value ?? "", HDToolTheme.Readout, new Rectangle(x, top + ls.Height, vw + 2, vs.Height), HDToolTheme.Text, HDDraw.One);
            if (!string.IsNullOrEmpty(Unit) && vw + HDDraw.S(g, 6) < w)
            {
                Size us = HDDraw.Measure(g, Unit, HDToolTheme.Mono);
                TextRenderer.DrawText(g, Unit, HDToolTheme.Mono, new Rectangle(x + vw + HDDraw.S(g, 4), top + ls.Height + vs.Height - us.Height - HDDraw.S(g, 3), w - vw, us.Height + 2),
                    HDToolTheme.Mix(c, Color.White, 0.15), HDDraw.One);
            }
        }
    }

    /// <summary>Khung thẻ bo góc (nền Surface, viền mảnh) chứa control con; Padding mặc định 1px để viền không bị che.</summary>
    public class HDToolCardPanel : Panel, HDToolPaintedControl
    {
        public HDToolCardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            Padding = new Padding(1);
            BackColor = HDToolTheme.Surface;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            HDDraw.ClearParent(g, this);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int rad = HDDraw.S(g, HDToolTheme.Radius);
            HDDraw.FillRound(g, new Rectangle(0, 0, Width - 1, Height - 1), rad, HDToolTheme.Surface);
            HDDraw.StrokeRound(g, new Rectangle(0, 0, Width, Height), rad, HDToolTheme.Line, 1f);
        }
    }

    /// <summary>Màn hình "chưa có dữ liệu": icon lớn, tiêu đề, hướng dẫn và một nút hành động.</summary>
    public class HDToolEmptyState : Panel, HDToolPaintedControl
    {
        public string IconKey = "menu";
        public string Title = "";
        public string Hint = "";
        public readonly HDToolButton ActionButton = new HDToolButton();

        public HDToolEmptyState()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = HDToolTheme.Surface;
            ActionButton.Kind = HDToolButtonKind.Primary;
            ActionButton.Size = new Size(170, 38);
            Controls.Add(ActionButton);
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            float s = 1f;
            try { using (Graphics g = CreateGraphics()) s = g.DpiX / 96f; } catch { }
            // Dưới dòng hướng dẫn (vị trí khớp với OnPaint: vòng icon 84, tiêu đề, 2 dòng hướng dẫn)
            ActionButton.Location = new Point((Width - ActionButton.Width) / 2, Height / 2 + (int)Math.Round(88 * s));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(HDToolTheme.Surface);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color c = HDToolTheme.ToolColor(IconKey);
            int d = HDDraw.S(g, 84);
            int cy = Height / 2 - HDDraw.S(g, 60);
            Rectangle cr = new Rectangle((Width - d) / 2, cy - d / 2, d, d);
            using (SolidBrush b = new SolidBrush(HDToolTheme.Mix(HDToolTheme.Surface, c, 0.18))) g.FillEllipse(b, cr);
            int ic = HDDraw.S(g, 44);
            HDToolIcons.Draw(g, IconKey, new RectangleF(cr.X + (d - ic) / 2f, cr.Y + (d - ic) / 2f, ic, ic), c, c);
            Size ts = HDDraw.Measure(g, Title, HDToolTheme.Title);
            int y = cr.Bottom + HDDraw.S(g, 12);
            TextRenderer.DrawText(g, Title, HDToolTheme.Title, new Rectangle(0, y, Width, ts.Height + 2), HDToolTheme.Text, TextFormatFlags.HorizontalCenter | HDDraw.One);
            y += ts.Height + HDDraw.S(g, 6);
            TextRenderer.DrawText(g, Hint, HDToolTheme.Body, new Rectangle(HDDraw.S(g, 20), y, Width - HDDraw.S(g, 40), HDDraw.S(g, 40)), HDToolTheme.Text2,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }
    }

    /// <summary>Lưu danh sách công cụ dùng gần đây (%APPDATA%\HDTOOL\recent_tools.txt).</summary>
    public static class HDToolRecent
    {
        private static string FilePath
        {
            get { return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HDTOOL", "recent_tools.txt"); }
        }

        public static List<string> Load()
        {
            List<string> list = new List<string>();
            try
            {
                if (System.IO.File.Exists(FilePath))
                {
                    foreach (string l in System.IO.File.ReadAllLines(FilePath))
                    {
                        string t = l.Trim();
                        if (t.Length > 0 && !list.Contains(t)) list.Add(t);
                    }
                }
            }
            catch { }
            return list;
        }

        public static void Touch(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            try
            {
                List<string> list = Load();
                list.Remove(key);
                list.Insert(0, key);
                if (list.Count > 6) list.RemoveRange(6, list.Count - 6);
                string dir = System.IO.Path.GetDirectoryName(FilePath);
                if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllLines(FilePath, list.ToArray());
            }
            catch { }
        }
    }

    /// <summary>Cột trái kiểu Mac: mục chọn nhóm (ô icon màu + tên + số lượng) và tiêu đề mục nhỏ in hoa.</summary>
    public class HDToolSidebar : Control, HDToolPaintedControl
    {
        public class Item
        {
            public string Key = "";
            public string Text = "";
            public string IconKey = "";
            public string Count = "";
            public bool Header;
            public Rectangle Bounds;
        }

        public readonly List<Item> Items = new List<Item>();
        public string SelectedKey = "";
        public event EventHandler<HDToolSidebarEventArgs> ItemClick;
        private Item _hover;

        public HDToolSidebar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = HDToolTheme.Surface2;
        }

        public Item Add(string key, string text, string iconKey, string count)
        {
            Item it = new Item(); it.Key = key; it.Text = text; it.IconKey = iconKey ?? ""; it.Count = count ?? "";
            Items.Add(it); Invalidate(); return it;
        }

        public void AddHeader(string text)
        {
            Item it = new Item(); it.Text = text; it.Header = true;
            Items.Add(it); Invalidate();
        }

        private Item HitTest(Point p)
        {
            foreach (Item it in Items) if (!it.Header && it.Bounds.Contains(p)) return it;
            return null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Item h = HitTest(e.Location);
            if (h != _hover) { _hover = h; Cursor = h != null ? Cursors.Hand : Cursors.Default; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = null; Invalidate(); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left) return;
            Item it = HitTest(e.Location);
            if (it != null && ItemClick != null) ItemClick(this, new HDToolSidebarEventArgs(it));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(HDToolTheme.Surface2);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int y = HDDraw.S(g, 4);
            int pad = HDDraw.S(g, 8);
            int rowH = HDDraw.S(g, 32);
            int hdrH = HDDraw.S(g, 30);
            int tile = HDDraw.S(g, 22);
            foreach (Item it in Items)
            {
                if (it.Header)
                {
                    it.Bounds = new Rectangle(pad, y, Width - pad * 2, hdrH);
                    TextRenderer.DrawText(g, it.Text, HDToolTheme.LabelFont, new Rectangle(pad + HDDraw.S(g, 8), y + HDDraw.S(g, 12), Width - pad * 3, hdrH - HDDraw.S(g, 12)),
                        HDToolTheme.Text3, HDDraw.One);
                    y += hdrH;
                    continue;
                }
                it.Bounds = new Rectangle(pad, y, Width - pad * 2, rowH);
                bool sel = it.Key == SelectedKey;
                if (sel) HDDraw.FillRound(g, it.Bounds, HDDraw.S(g, 7), HDToolTheme.Accent);
                else if (it == _hover) HDDraw.FillRound(g, it.Bounds, HDDraw.S(g, 7), HDToolTheme.SurfaceHover);

                int x = it.Bounds.X + HDDraw.S(g, 8);
                if (it.IconKey.Length > 0)
                {
                    Rectangle tr = new Rectangle(x, y + (rowH - tile) / 2, tile, tile);
                    Color tc = it.IconKey == "menu" ? HDToolTheme.Text3 : HDToolTheme.ToolColor(it.IconKey);
                    HDDraw.FillRound(g, tr, HDDraw.S(g, 6), tc);
                    int ic = HDDraw.S(g, 15);
                    HDToolIcons.Draw(g, it.IconKey, new RectangleF(tr.X + (tile - ic) / 2f, tr.Y + (tile - ic) / 2f, ic, ic), Color.White, Color.White);
                    x = tr.Right + HDDraw.S(g, 9);
                }
                int right = it.Bounds.Right - HDDraw.S(g, 8);
                if (it.Count.Length > 0)
                {
                    Size cs = HDDraw.Measure(g, it.Count, HDToolTheme.Caption);
                    TextRenderer.DrawText(g, it.Count, HDToolTheme.Caption, new Rectangle(right - cs.Width, y, cs.Width, rowH),
                        sel ? HDToolTheme.Mix(HDToolTheme.AccentInk, HDToolTheme.Accent, 0.2) : HDToolTheme.Text3, TextFormatFlags.VerticalCenter | HDDraw.One);
                    right -= cs.Width + HDDraw.S(g, 6);
                }
                TextRenderer.DrawText(g, it.Text, HDToolTheme.Body, new Rectangle(x, y, Math.Max(10, right - x), rowH),
                    sel ? HDToolTheme.AccentInk : HDToolTheme.Text, TextFormatFlags.VerticalCenter | HDDraw.One);
                y += rowH + HDDraw.S(g, 2);
            }
            using (Pen p = new Pen(HDToolTheme.Line)) g.DrawLine(p, Width - 1, 0, Width - 1, Height);
        }
    }

    public class HDToolSidebarEventArgs : EventArgs
    {
        public readonly HDToolSidebar.Item Item;
        public HDToolSidebarEventArgs(HDToolSidebar.Item item) { Item = item; }
    }
}
