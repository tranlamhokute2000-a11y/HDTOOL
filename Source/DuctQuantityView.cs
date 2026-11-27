using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace HDTool
{
    /// <summary>
    /// Giao diện v2 (Tối Cao Cấp) của cửa sổ Khối Lượng Ống Gió — chỉ dựng & sắp xếp control, KHÔNG chứa logic Revit.
    /// DuctQuantityForm gắn sự kiện vào các control công khai bên dưới. Mẫu duyệt: tab 02 của PreviewStudio/HDTool_GiaoDienMoi.html.
    /// Bố cục cố định theo tầng: Dải đầu 68 · Thanh bước 48 · Thanh công cụ 56 · Chip loại 46 · Nội dung (4 ô số liệu + thẻ bảng) · Thanh hành động 62.
    /// </summary>
    public class DuctQuantityView
    {
        // Tầng 1-2
        public HDToolHeader Header;
        public HDToolStepBar Steps;
        // Tầng 3: thanh công cụ
        public HDToolSegmented Scope;
        public HDToolButton BtnPick, BtnAppend, BtnRefresh, BtnLight, BtnLightOff, BtnZoom;
        public HDToolSearchBox Search;
        // Tầng 4: chip loại cấu kiện
        public HDToolChipToggle ChkDuct, ChkFitting, ChkDamper, ChkDiffuser, ChkFlex;
        // Nội dung
        public HDToolKpiCard KpiArea, KpiLength, KpiSections, KpiFittings;
        public HDToolCardPanel Card;
        public Label TableTitle, TableSub, LblLevel, LblSystem;
        public ComboBox CboLevel, CboSystem;
        public HDToolButton BtnAddCol, BtnRemoveCol;
        public DataGridView Grid;
        public HDToolEmptyState Empty;
        // Thanh hành động
        public HDToolSwitch SwAutoHighlight, SwLiveSync;
        public Label Info;
        public HDToolButton BtnCopy, BtnExcel, BtnPlace, BtnClose;
        public ToolTip Tips;

        private Form _form;
        private Panel _root, _toolbar, _chips, _actions, _tableHead, _sep;
        private Label _chipsLabel;

        public static readonly string[] StepNames = new string[] { "Chọn phạm vi", "Quét đối tượng", "Kiểm tra bảng", "Xuất Excel / Đặt vào Sheet" };

        public void Build(Form form, string viewName)
        {
            _form = form;
            Tips = new ToolTip();
            Tips.AutoPopDelay = 7000; Tips.InitialDelay = 400; Tips.ReshowDelay = 150; Tips.ShowAlways = true;

            _root = new Panel();
            _root.Dock = DockStyle.Fill;
            _root.BackColor = HDToolTheme.Bg;

            Header = new HDToolHeader();
            Header.Dock = DockStyle.None;
            Header.IconKey = "duct";
            Header.Title = "Khối Lượng Ống Gió & Phụ Kiện";
            Header.Subtitle = "Tính m² tôn theo TCVN 5687:2010 / SMACNA · phân loại cút, côn, lượn, van và miệng gió";
            Header.Chips = new string[] { "TCVN 5687:2010", Short(viewName, 26), "ESC đóng" };

            Steps = new HDToolStepBar();
            Steps.Steps = StepNames;
            Steps.Current = 1;

            // ---------- Thanh công cụ ----------
            _toolbar = MakeBar(true);
            Scope = new HDToolSegmented();
            Scope.Items = new string[] { "View hiện tại", "Vùng đã quét", "Toàn dự án" };
            BtnPick = MakeButton("Quét vùng mới", "pick", HDToolButtonKind.Primary);
            BtnAppend = MakeButton("Bóc thêm", "plus", HDToolButtonKind.Secondary);
            BtnRefresh = MakeButton("Quét lại", "refresh", HDToolButtonKind.Secondary);
            BtnLight = MakeButton("", "light", HDToolButtonKind.Ghost);
            BtnLightOff = MakeButton("", "lightoff", HDToolButtonKind.Ghost);
            BtnZoom = MakeButton("", "zoom", HDToolButtonKind.Ghost);
            _sep = new Panel(); _sep.BackColor = HDToolTheme.Line;
            Search = new HDToolSearchBox();
            Search.Placeholder = "Tìm tên, size, hệ thống, tầng…";
            Search.KeyHint = "";
            Tips.SetToolTip(BtnPick, "Trên Sheet: click chọn Viewport để lấy toàn bộ ống gió của View.\nTrong View mô hình: kéo quét một khung chữ nhật.");
            Tips.SetToolTip(BtnAppend, "Quét thêm vùng mới (hoặc thêm Viewport) và cộng dồn vào bảng");
            Tips.SetToolTip(BtnRefresh, "Đọc lại dữ liệu theo phạm vi đang chọn");
            Tips.SetToolTip(BtnLight, "Làm sáng toàn bộ đối tượng đã bóc trên bản vẽ Revit");
            Tips.SetToolTip(BtnLightOff, "Tắt làm sáng");
            Tips.SetToolTip(BtnZoom, "Phóng tới các đối tượng của dòng đang chọn");
            _toolbar.Controls.AddRange(new Control[] { Scope, BtnPick, BtnAppend, BtnRefresh, _sep, BtnLight, BtnLightOff, BtnZoom, Search });

            // ---------- Chip loại cấu kiện ----------
            _chips = MakeBar(false);
            _chipsLabel = MakeLabel("Loại cấu kiện", HDToolTheme.Text3, HDToolTheme.Caption);
            ChkDuct = MakeChip("Ống gió thẳng (m² tôn)", Color.FromArgb(37, 99, 235));
            ChkFitting = MakeChip("Cút, côn, tê, lượn", Color.FromArgb(124, 58, 237));
            ChkDamper = MakeChip("Van gió VCD / FD / NRD", Color.FromArgb(217, 119, 6));
            ChkDiffuser = MakeChip("Miệng gió", Color.FromArgb(22, 163, 74));
            ChkFlex = MakeChip("Ống gió mềm", Color.FromArgb(100, 116, 139));
            _chips.Controls.AddRange(new Control[] { _chipsLabel, ChkDuct, ChkFitting, ChkDamper, ChkDiffuser, ChkFlex });

            // ---------- 4 ô số liệu ----------
            KpiArea = MakeKpi("area", Color.FromArgb(234, 88, 12), "Tổng diện tích tôn", "m²");
            KpiLength = MakeKpi("ruler", Color.FromArgb(91, 140, 255), "Chiều dài ống thẳng", "m");
            KpiSections = MakeKpi("hash", Color.FromArgb(167, 139, 250), "Số đoạn ống", "đoạn");
            KpiFittings = MakeKpi("valve", Color.FromArgb(52, 211, 153), "Phụ kiện, van, miệng gió", "cái");

            // ---------- Thẻ bảng ----------
            Card = new HDToolCardPanel();
            _tableHead = new Panel();
            _tableHead.BackColor = HDToolTheme.Surface;
            _tableHead.Paint += (s, e) => { using (Pen p = new Pen(HDToolTheme.Line)) e.Graphics.DrawLine(p, 0, _tableHead.Height - 1, _tableHead.Width, _tableHead.Height - 1); };
            TableTitle = MakeLabel("Bảng khối lượng", HDToolTheme.Text, HDToolTheme.CardTitle);
            TableSub = MakeLabel("Đang tải…", HDToolTheme.Text3, HDToolTheme.Caption);
            LblLevel = MakeLabel("Tầng", HDToolTheme.Text3, HDToolTheme.Caption);
            LblSystem = MakeLabel("Hệ thống", HDToolTheme.Text3, HDToolTheme.Caption);
            CboLevel = new ComboBox(); CboLevel.DropDownStyle = ComboBoxStyle.DropDownList; CboLevel.Width = 140;
            CboSystem = new ComboBox(); CboSystem.DropDownStyle = ComboBoxStyle.DropDownList; CboSystem.Width = 170;
            BtnAddCol = MakeButton("Thêm cột", "col", HDToolButtonKind.Secondary);
            BtnRemoveCol = MakeButton("Xóa cột", "col", HDToolButtonKind.Ghost);
            Tips.SetToolTip(BtnAddCol, "Thêm cột tham số của dự án / hệ ống gió (Comments, Mark, Loại Bảo Ôn...)");
            Tips.SetToolTip(BtnRemoveCol, "Xóa một cột tham số đã thêm");
            _tableHead.Controls.AddRange(new Control[] { TableTitle, TableSub, LblLevel, CboLevel, LblSystem, CboSystem, BtnAddCol, BtnRemoveCol });

            Grid = new DataGridView();
            Grid.BorderStyle = BorderStyle.None;
            Grid.BackgroundColor = HDToolTheme.Surface;
            Grid.AllowUserToAddRows = false;
            Grid.RowHeadersVisible = false;
            Grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            Grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            Grid.ColumnHeadersHeight = 34;
            Grid.CellPainting += PaintCategoryPill;
            // Cột tên cấu kiện tự giãn lấp kín chiều rộng bảng (không để khoảng trống bên phải)
            Grid.ColumnAdded += (s, e) =>
            {
                if (string.Equals(e.Column.Name, "Name", StringComparison.OrdinalIgnoreCase))
                {
                    e.Column.MinimumWidth = 200;
                    e.Column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                }
            };

            Empty = new HDToolEmptyState();
            Empty.IconKey = "duct";
            Empty.Title = "Chưa có dữ liệu ống gió";
            Empty.Hint = "Chọn phạm vi ở thanh trên rồi bấm \"Quét vùng mới\" để bắt đầu bóc khối lượng.";
            Empty.ActionButton.Text = "Quét vùng mới";
            Empty.ActionButton.IconKey = "pick";
            Empty.Visible = false;

            Card.Controls.Add(Grid);
            Card.Controls.Add(Empty);
            Card.Controls.Add(_tableHead);

            // ---------- Thanh hành động ----------
            _actions = MakeBar(true);
            _actions.Paint += (s, e) => { using (Pen p = new Pen(HDToolTheme.Line)) e.Graphics.DrawLine(p, 0, 0, _actions.Width, 0); };
            SwAutoHighlight = new HDToolSwitch(); SwAutoHighlight.Text = "Tự làm sáng khi chọn dòng"; SwAutoHighlight.Checked = true;
            SwLiveSync = new HDToolSwitch(); SwLiveSync.Text = "Tự bóc khi chọn trên Revit"; SwLiveSync.Checked = false;
            Tips.SetToolTip(SwLiveSync, "Bật: chỉ cần chọn ống gió trên bản vẽ, bảng tự tính lại ngay");
            Info = MakeLabel("", HDToolTheme.Text2, HDToolTheme.Body);
            BtnCopy = MakeButton("Copy bảng", "copy", HDToolButtonKind.Secondary);
            BtnExcel = MakeButton("Xuất Excel", "xls", HDToolButtonKind.Secondary);
            BtnPlace = MakeButton("Đặt vào Sheet", "sheet", HDToolButtonKind.Primary);
            BtnClose = MakeButton("Đóng (ESC)", null, HDToolButtonKind.Ghost);
            Tips.SetToolTip(BtnPlace, "Tạo / cập nhật bảng khối lượng và đặt lên Sheet bản vẽ");
            _actions.Controls.AddRange(new Control[] { SwAutoHighlight, SwLiveSync, Info, BtnCopy, BtnExcel, BtnPlace, BtnClose });

            _root.Controls.AddRange(new Control[] { Header, Steps, _toolbar, _chips, KpiArea, KpiLength, KpiSections, KpiFittings, Card, _actions });
            _root.Resize += (s, e) => LayoutAll();
            form.Controls.Add(_root);
            form.Shown += (s, e) => LayoutAll();
        }

        // ---------- Cập nhật nội dung ----------
        /// <param name="hasAnyData">Có dữ liệu trước khi lọc (rows = 0 nhưng hasAnyData = true nghĩa là bộ lọc / ô tìm loại hết).</param>
        public void SetTotals(double areaM2, double lengthM, int sections, int fittings, int rows, bool hasAnyData)
        {
            KpiArea.Value = areaM2.ToString("#,##0.00");
            KpiLength.Value = lengthM.ToString("#,##0.00");
            KpiSections.Value = sections.ToString("#,##0");
            KpiFittings.Value = fittings.ToString("#,##0");
            TableSub.Text = !hasAnyData ? "Chưa có dữ liệu" : (rows == 0 ? "Không có dòng khớp bộ lọc / từ khóa" : rows.ToString("#,##0") + " dòng thống kê");
            Empty.Visible = !hasAnyData;
            Grid.Visible = hasAnyData;
            Steps.Current = !hasAnyData ? 1 : Math.Max(Steps.Current, 2);
            LayoutCard();
        }

        public void SetScope(int index, int scannedCount)
        {
            Scope.SetItemText(1, scannedCount > 0 ? "Vùng đã quét (" + scannedCount.ToString("#,##0") + ")" : "Vùng đã quét");
            if (Scope.SelectedIndex != index) Scope.SelectedIndex = index;
            LayoutToolbar();
        }

        // ---------- Bố cục ----------
        private float S()
        {
            ContainerControl cc = _form as ContainerControl;
            try
            {
                if (cc != null && cc.AutoScaleDimensions.Width > 0) return Math.Max(1f, cc.CurrentAutoScaleDimensions.Width / 96f);
            }
            catch { }
            return 1f;
        }
        private int P(float v) { return (int)Math.Round(v * S()); }

        public void LayoutAll()
        {
            if (_root == null || _root.ClientSize.Width < 50) return;
            int W = _root.ClientSize.Width, H = _root.ClientSize.Height, y = 0;
            Header.SetBounds(0, y, W, P(68)); y += P(68);
            Steps.SetBounds(0, y, W, P(48)); y += P(48);
            _toolbar.SetBounds(0, y, W, P(56)); y += P(56); LayoutToolbar();
            _chips.SetBounds(0, y, W, P(46)); y += P(46); LayoutChips();
            int aH = P(62);
            _actions.SetBounds(0, H - aH, W, aH); LayoutActions();

            int pad = P(14), gap = P(12), kH = P(78);
            int kW = Math.Max(80, (W - pad * 2 - gap * 3) / 4);
            HDToolKpiCard[] k = new HDToolKpiCard[] { KpiArea, KpiLength, KpiSections, KpiFittings };
            for (int i = 0; i < 4; i++) k[i].SetBounds(pad + i * (kW + gap), y + pad, kW, kH);
            int cy = y + pad + kH + gap;
            Card.SetBounds(pad, cy, W - pad * 2, Math.Max(P(120), H - aH - pad - cy));
            LayoutCard();
        }

        public void LayoutToolbar()
        {
            if (_toolbar == null) return;
            int h = _toolbar.Height, x = P(16), bh = P(34), gap = P(8);
            int yy = (h - bh) / 2;
            Scope.SetBounds(x, yy, Scope.PreferredWidth, bh); x = Scope.Right + P(12);
            foreach (HDToolButton b in new HDToolButton[] { BtnPick, BtnAppend, BtnRefresh })
            {
                int w = b.GetPreferredSize(Size.Empty).Width;
                b.SetBounds(x, yy, w, bh); x += w + gap;
            }
            _sep.SetBounds(x + P(2), yy + P(5), 1, bh - P(10)); x += P(12);
            foreach (HDToolButton b in new HDToolButton[] { BtnLight, BtnLightOff, BtnZoom })
            {
                b.SetBounds(x, yy, bh, bh); x += bh + P(4);
            }
            int sw = Math.Max(P(160), Math.Min(P(300), _toolbar.Width - x - P(32)));
            Search.SetBounds(_toolbar.Width - P(16) - sw, yy, sw, bh);
        }

        private void LayoutChips()
        {
            int h = _chips.Height, x = P(16), ch = P(30);
            Size ls = TextRenderer.MeasureText(_chipsLabel.Text, _chipsLabel.Font);
            _chipsLabel.SetBounds(x, 0, ls.Width + P(4), h); x += ls.Width + P(14);
            foreach (HDToolChipToggle c in new HDToolChipToggle[] { ChkDuct, ChkFitting, ChkDamper, ChkDiffuser, ChkFlex })
            {
                int w = c.PreferredWidth;
                c.SetBounds(x, (h - ch) / 2, w, ch); x += w + P(8);
            }
        }

        private void LayoutCard()
        {
            if (Card == null || Card.Width < 50) return;
            int W = Card.ClientSize.Width, H = Card.ClientSize.Height, hh = P(52);
            _tableHead.SetBounds(1, 1, W - 2, hh);
            Size ts = TextRenderer.MeasureText(TableTitle.Text, TableTitle.Font);
            TableTitle.SetBounds(P(16), 0, ts.Width + P(4), hh);
            int rx = _tableHead.Width - P(14), bh = P(32), yy = (hh - bh) / 2;
            int w;
            w = BtnRemoveCol.GetPreferredSize(Size.Empty).Width; BtnRemoveCol.SetBounds(rx - w, yy, w, bh); rx -= w + P(6);
            w = BtnAddCol.GetPreferredSize(Size.Empty).Width; BtnAddCol.SetBounds(rx - w, yy, w, bh); rx -= w + P(16);
            CboSystem.Width = P(170); CboSystem.Location = new Point(rx - CboSystem.Width, (hh - CboSystem.Height) / 2); rx = CboSystem.Left - P(6);
            w = TextRenderer.MeasureText(LblSystem.Text, LblSystem.Font).Width + P(4); LblSystem.SetBounds(rx - w, 0, w, hh); rx -= w + P(14);
            CboLevel.Width = P(140); CboLevel.Location = new Point(rx - CboLevel.Width, (hh - CboLevel.Height) / 2); rx = CboLevel.Left - P(6);
            w = TextRenderer.MeasureText(LblLevel.Text, LblLevel.Font).Width + P(4); LblLevel.SetBounds(rx - w, 0, w, hh); rx -= w + P(14);
            int subX = TableTitle.Right + P(10);
            TableSub.SetBounds(subX, 0, Math.Max(10, rx - subX), hh);
            Grid.SetBounds(1, hh + 1, W - 2, Math.Max(10, H - hh - 2));
            Empty.SetBounds(1, hh + 1, W - 2, Math.Max(10, H - hh - 2));
        }

        private void LayoutActions()
        {
            int h = _actions.Height, rx = _actions.Width - P(16), bh = P(34), gap = P(8);
            int w = BtnClose.GetPreferredSize(Size.Empty).Width; BtnClose.SetBounds(rx - w, (h - bh) / 2, w, bh); rx -= w + gap;
            int ph = P(40); w = BtnPlace.GetPreferredSize(Size.Empty).Width + P(12); BtnPlace.SetBounds(rx - w, (h - ph) / 2, w, ph); rx -= w + gap;
            w = BtnExcel.GetPreferredSize(Size.Empty).Width; BtnExcel.SetBounds(rx - w, (h - bh) / 2, w, bh); rx -= w + gap;
            w = BtnCopy.GetPreferredSize(Size.Empty).Width; BtnCopy.SetBounds(rx - w, (h - bh) / 2, w, bh); rx -= w + P(16);
            int x = P(16), sh = P(30);
            SwAutoHighlight.SetBounds(x, (h - sh) / 2, SwAutoHighlight.PreferredWidth, sh); x = SwAutoHighlight.Right + P(14);
            SwLiveSync.SetBounds(x, (h - sh) / 2, SwLiveSync.PreferredWidth, sh); x = SwLiveSync.Right + P(18);
            Info.SetBounds(x, 0, Math.Max(10, rx - x), h);
        }

        // ---------- Nhãn loại cấu kiện trong bảng (cột "Category") ----------
        public static Color KindColor(string kind)
        {
            string k = (kind ?? "").ToLowerInvariant();
            if (k.Contains("thẳng")) return Color.FromArgb(37, 99, 235);
            if (k.Contains("mềm")) return Color.FromArgb(100, 116, 139);
            if (k.Contains("van")) return Color.FromArgb(217, 119, 6);
            if (k.Contains("miệng")) return Color.FromArgb(22, 163, 74);
            return Color.FromArgb(124, 58, 237);
        }

        private void PaintCategoryPill(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.Handled || e.RowIndex < 0 || e.ColumnIndex < 0) return;
            DataGridView g = (DataGridView)sender;
            if (!string.Equals(g.Columns[e.ColumnIndex].Name, "Category", StringComparison.OrdinalIgnoreCase)) return;
            string val = e.FormattedValue as string;
            if (string.IsNullOrEmpty(val)) return;
            e.Paint(e.CellBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.ContentForeground);
            Color c = KindColor(val);
            Graphics gr = e.Graphics;
            SmoothingMode old = gr.SmoothingMode;
            gr.SmoothingMode = SmoothingMode.AntiAlias;
            Font f = HDToolTheme.Caption;
            Size ts = TextRenderer.MeasureText(gr, val, f, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            int ph = Math.Min(e.CellBounds.Height - 8, ts.Height + 6);
            int pw = Math.Min(e.CellBounds.Width - 10, ts.Width + 26);
            Rectangle r = new Rectangle(e.CellBounds.X + 6, e.CellBounds.Y + (e.CellBounds.Height - ph) / 2, pw, ph);
            Color back = e.CellStyle != null ? e.CellStyle.BackColor : HDToolTheme.Surface;
            using (GraphicsPath p = HDToolTheme.RoundRect(r, HDToolTheme.IsPremium ? ph / 2f : 0f))
            using (SolidBrush b = new SolidBrush(HDToolTheme.Mix(back, c, 0.22))) gr.FillPath(b, p);
            int d = Math.Max(5, ph / 3);
            Color tc = HDToolTheme.IsDark ? HDToolTheme.Mix(c, Color.White, 0.3) : c;
            using (SolidBrush db = new SolidBrush(tc)) gr.FillEllipse(db, r.X + 9, r.Y + (ph - d) / 2, d, d);
            TextRenderer.DrawText(gr, val, f, new Rectangle(r.X + 9 + d + 5, r.Y, r.Width - d - 16, r.Height), tc,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            gr.SmoothingMode = old;
            e.Handled = true;
        }

        // ---------- Tiện ích tạo control ----------
        private static Panel MakeBar(bool surface)
        {
            Panel p = new Panel();
            p.BackColor = surface ? HDToolTheme.Surface : HDToolTheme.Bg;
            if (surface) p.Paint += (s, e) => { Panel pp = (Panel)s; using (Pen pen = new Pen(HDToolTheme.Line)) e.Graphics.DrawLine(pen, 0, pp.Height - 1, pp.Width, pp.Height - 1); };
            return p;
        }

        private static HDToolButton MakeButton(string text, string icon, HDToolButtonKind kind)
        {
            HDToolButton b = new HDToolButton();
            b.Text = text;
            b.IconKey = icon;
            b.Kind = kind;
            return b;
        }

        private static Label MakeLabel(string text, Color fore, Font font)
        {
            Label l = new Label();
            l.Text = text;
            l.ForeColor = fore;
            l.BackColor = Color.Transparent;
            l.Font = font;
            l.AutoSize = false;
            l.AutoEllipsis = true;
            l.UseMnemonic = false;
            l.TextAlign = ContentAlignment.MiddleLeft;
            return l;
        }

        private static HDToolChipToggle MakeChip(string text, Color c)
        {
            HDToolChipToggle t = new HDToolChipToggle();
            t.Text = text;
            t.ChipColor = c;
            t.Checked = true;
            return t;
        }

        private static HDToolKpiCard MakeKpi(string icon, Color c, string label, string unit)
        {
            HDToolKpiCard k = new HDToolKpiCard();
            k.IconKey = icon;
            k.KpiColor = c;
            k.Label = label;
            k.Unit = unit;
            k.Value = "—";
            return k;
        }

        private static string Short(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= n ? s : s.Substring(0, n - 1) + "…";
        }
    }
}
