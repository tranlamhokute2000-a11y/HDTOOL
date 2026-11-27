using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Color = System.Drawing.Color;
using Point = System.Drawing.Point;

namespace HDTool
{
    public class HDToolMainForm : HDToolBaseForm
    {
        private UIDocument _uidoc;
        private Document _doc;
        private Autodesk.Revit.DB.View _activeView;

        // UI Controls (giao diện Mac: cột trái chọn nhóm + danh sách, mẫu PreviewStudio/HDTool_GiaoDienMac.html)
        private HDToolSidebar sideNav;
        private System.Windows.Forms.Panel pnlSide;
        private System.Windows.Forms.Panel pnlContent;
        private System.Windows.Forms.Panel pnlFoot;
        private Label lblTip;
        private HDToolButton btnClose;
        private HDToolSearchBox txtSearch;
        private Label lblHeading;
        private Label lblSub;
        private Label lblEmpty;
        private string _selGroup = "all";
        private readonly List<System.Windows.Forms.Control> _flow = new List<System.Windows.Forms.Control>();
        private readonly Dictionary<System.Windows.Forms.Control, int> _gapAfter = new Dictionary<System.Windows.Forms.Control, int>();
        private readonly List<KeyValuePair<HDToolSectionLabel, HDToolCardGrid>> _groups = new List<KeyValuePair<HDToolSectionLabel, HDToolCardGrid>>();
        private readonly List<string> _groupKeys = new List<string>();
        private readonly List<HDToolToolCard> _cards = new List<HDToolToolCard>();
        private readonly Dictionary<string, EventHandler> _actions = new Dictionary<string, EventHandler>();

        public HDToolMainForm(UIDocument uidoc)
        {
            _uidoc = uidoc;
            _doc = uidoc != null ? uidoc.Document : null;
            _activeView = uidoc != null ? uidoc.ActiveView : null;

            InitializeComponent();
        }

        private static string ReadVersion()
        {
            try
            {
                using (System.IO.Stream s = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("version.txt"))
                {
                    if (s == null) return "";
                    using (System.IO.StreamReader r = new System.IO.StreamReader(s)) return r.ReadToEnd().Trim();
                }
            }
            catch { return ""; }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            string ver = ReadVersion();
            this.Text = "HDTOOL — Trung Tâm Công Cụ" + (string.IsNullOrEmpty(ver) ? "" : " · Version " + ver);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = true;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.TopMost = true;
            this.BackColor = HDToolTheme.Bg;
            this.Font = HDToolTheme.Body;
            this.ClientSize = new Size(860, 640);
            this.MinimumSize = new Size(640, 460);

            // CHÂN TRANG: mẹo + nút Đóng
            pnlFoot = new System.Windows.Forms.Panel();
            pnlFoot.Dock = DockStyle.Bottom;
            pnlFoot.Height = 48;
            pnlFoot.BackColor = HDToolTheme.Header;
            pnlFoot.Paint += (s, e) => { using (Pen p = new Pen(HDToolTheme.Line)) e.Graphics.DrawLine(p, 0, 0, pnlFoot.Width, 0); };
            lblTip = new Label();
            lblTip.Text = "● Sẵn sàng · Phím 1–9 mở nhanh · Ctrl+F tìm · Enter mở kết quả đầu tiên · vẫn thao tác trên bản vẽ được";
            lblTip.ForeColor = HDToolTheme.Text3;
            lblTip.BackColor = Color.Transparent;
            lblTip.AutoSize = false;
            lblTip.AutoEllipsis = true;
            lblTip.UseMnemonic = false;
            lblTip.TextAlign = ContentAlignment.MiddleLeft;
            btnClose = new HDToolButton();
            btnClose.Text = "Đóng (ESC)";
            btnClose.Kind = HDToolButtonKind.Secondary;
            btnClose.Size = new Size(110, 32);
            btnClose.Click += (s, e) => this.Close();
            this.CancelButton = btnClose;
            pnlFoot.Controls.Add(lblTip);
            pnlFoot.Controls.Add(btnClose);
            pnlFoot.Resize += (s, e) => LayoutFoot();

            // CỘT TRÁI: ô tìm kiếm + danh mục nhóm
            pnlSide = new System.Windows.Forms.Panel();
            pnlSide.Dock = DockStyle.Left;
            pnlSide.Width = 220;
            pnlSide.BackColor = HDToolTheme.Surface2;
            pnlSide.Padding = new Padding(10, 12, 11, 0);
            txtSearch = new HDToolSearchBox();
            txtSearch.Placeholder = "Tìm công cụ…";
            txtSearch.KeyHint = "";
            txtSearch.Dock = DockStyle.Top;
            txtSearch.Height = 34;
            txtSearch.Box.TextChanged += (s, e) => ApplyFilter();
            txtSearch.Box.KeyDown += SearchBox_KeyDown;
            sideNav = new HDToolSidebar();
            sideNav.Dock = DockStyle.Fill;
            sideNav.ItemClick += SideNav_ItemClick;
            pnlSide.Controls.Add(sideNav);
            pnlSide.Controls.Add(txtSearch);
            pnlSide.Paint += (s, e) => { using (Pen p = new Pen(HDToolTheme.Line)) e.Graphics.DrawLine(p, pnlSide.Width - 1, 0, pnlSide.Width - 1, pnlSide.Height); };

            // NỘI DUNG: tiêu đề nhóm + danh sách công cụ (cuộn được)
            pnlContent = new System.Windows.Forms.Panel();
            pnlContent.Dock = DockStyle.Fill;
            pnlContent.AutoScroll = true;
            pnlContent.BackColor = HDToolTheme.Bg;
            pnlContent.Padding = new Padding(24, 18, 24, 18);

            lblHeading = new Label();
            lblHeading.Font = new Font("Segoe UI", 15f, FontStyle.Bold);
            lblHeading.ForeColor = HDToolTheme.Text;
            lblHeading.BackColor = Color.Transparent;
            lblHeading.AutoSize = false;
            lblHeading.AutoEllipsis = true;
            lblHeading.UseMnemonic = false;
            lblHeading.Height = 34;
            lblHeading.Text = "Tất cả công cụ";
            AddFlow(lblHeading, 0);

            lblSub = new Label();
            lblSub.ForeColor = HDToolTheme.Text3;
            lblSub.BackColor = Color.Transparent;
            lblSub.AutoSize = false;
            lblSub.AutoEllipsis = true;
            lblSub.UseMnemonic = false;
            lblSub.Height = 24;
            lblSub.Text = "Đang mở: " + (_activeView != null
                ? _activeView.Name + " · " + _activeView.ViewType.ToString() + (_activeView.Scale > 0 ? " · 1:" + _activeView.Scale : "")
                : "Chưa mở View");
            AddFlow(lblSub, 12);

            sideNav.Add("all", "Tất cả", "menu", "");
            sideNav.SelectedKey = "all";
            sideNav.AddHeader("NHÓM CÔNG CỤ");

            // Các nhóm công cụ (màu nhận diện lấy theo HDToolTheme.ToolColor)
            AddGroup("Căn chỉnh đối tượng", "grid", new string[][] {
                new string[] { "grid", "Căn Chỉnh Grids", "Căn đầu trục theo mm tới mép Crop View hoặc Sheet", "truc luoi grids can chinh" },
                new string[] { "level", "Căn Chỉnh Level", "Căn 2 đầu cao độ trên Mặt cắt / Mặt đứng / Sheet", "cao do level can chinh mat cat" },
                new string[] { "crop", "Tự Động Crop View", "Khung Crop ôm khít đối tượng · Plan / Section / 3D", "crop view khung cat tu dong" },
                new string[] { "align", "Tự Nhận Theo View", "Plan / Sheet → Grids · Mặt cắt / Mặt đứng → Level", "tu nhan view grids level smart" }
            });
            AddGroup("Khối lượng MEP", "duct", new string[][] {
                new string[] { "pipe", "Thống Kê Ống Nước", "Ống & phụ kiện · soi sáng mô hình · đưa vào Sheet", "ong nuoc pipe thong ke phu kien" },
                new string[] { "duct", "Khối Lượng Ống Gió", "m² tôn theo TCVN 5687:2010 · cút, côn, van, miệng gió", "ong gio duct khoi luong ton m2 hvac" },
                new string[] { "cad2duct", "Dựng Ống Gió Từ CAD", "Nhận diện tuyến, size WxH · dựng mô hình 3D", "cad dwg dung ong gio link" }
            });
            AddGroup("Bản vẽ & Sheet", "sheetlist", new string[][] {
                new string[] { "sheetlist", "Xuất Danh Mục Bản Vẽ", "Excel / CSV · sửa số hiệu & tên trực tiếp", "danh muc ban ve sheet xuat excel" },
                new string[] { "sheetsync", "Cập Nhật Danh Mục", "Đối chiếu file Excel → cập nhật Sheet trong Revit", "cap nhat danh muc sheet excel" },
                new string[] { "layers", "Xuất CAD Quy Đổi Layer", "DWG · Layer & màu ACI theo hệ thống MEP", "xuat cad dwg layer aci" }
            });
            AddGroup("Thư viện & hệ thống", "family", new string[][] {
                new string[] { "family", "Tổng Hợp Family 3D", "Xem trước 3D · tìm nhanh · đặt vào bản vẽ", "family thu vien 3d dat" },
                new string[] { "update", "Cập Nhật Tool", "Nạp phiên bản mới không cần tắt Revit", "cap nhat tool hot reload phien ban" }
            });

            lblEmpty = new Label();
            lblEmpty.Text = "Không tìm thấy công cụ phù hợp. Thử từ khóa khác.";
            lblEmpty.ForeColor = HDToolTheme.Text3;
            lblEmpty.BackColor = Color.Transparent;
            lblEmpty.TextAlign = ContentAlignment.MiddleCenter;
            lblEmpty.UseMnemonic = false;
            lblEmpty.Height = 60;
            lblEmpty.Visible = false;
            AddFlow(lblEmpty, 0);

            // Dùng gần đây (cuối cột trái)
            BuildRecent();

            _actions["grid"] = BtnGrids_Click;
            _actions["level"] = BtnLevel_Click;
            _actions["crop"] = BtnAutoCrop_Click;
            _actions["align"] = BtnSmart_Click;
            _actions["pipe"] = BtnMepQuantity_Click;
            _actions["duct"] = BtnDuctQuantity_Click;
            _actions["cad2duct"] = BtnCadToDuct_Click;
            _actions["sheetlist"] = BtnSheetList_Click;
            _actions["sheetsync"] = BtnUpdateSheetList_Click;
            _actions["layers"] = BtnExportCad_Click;
            _actions["family"] = BtnFamilyPalette_Click;
            _actions["update"] = BtnReloadTool_Click;

            pnlContent.Resize += (s, e) => LayoutFlow();

            this.Controls.Add(pnlContent);
            this.Controls.Add(pnlSide);
            this.Controls.Add(pnlFoot);

            this.ResumeLayout(false);
            this.Shown += (s, e) => { LayoutFlow(); LayoutFoot(); };
        }

        // ---------- Dựng các khối ----------
        private void AddFlow(System.Windows.Forms.Control c, int gapAfter)
        {
            pnlContent.Controls.Add(c);
            _flow.Add(c);
            _gapAfter[c] = gapAfter;
        }

        private void AddGroup(string title, string colorKey, string[][] tools)
        {
            HDToolSectionLabel sec = new HDToolSectionLabel();
            sec.Text = title;
            sec.DotColor = HDToolTheme.ToolColor(colorKey);
            sec.Height = 28;
            AddFlow(sec, 2);

            HDToolCardGrid grid = new HDToolCardGrid();
            grid.ListStyle = true;
            grid.Columns = 1;
            foreach (string[] t in tools)
            {
                HDToolToolCard card = new HDToolToolCard();
                card.RowStyle = true;
                card.IconKey = t[0];
                card.Title = t[1];
                card.Description = t[2];
                card.Keywords = t[3];
                card.Height = 54;
                if (_cards.Count < 9) card.Shortcut = (_cards.Count + 1).ToString();
                card.Click += Card_Click;
                _cards.Add(card);
                grid.Controls.Add(card);
            }
            AddFlow(grid, 18);
            _groups.Add(new KeyValuePair<HDToolSectionLabel, HDToolCardGrid>(sec, grid));
            _groupKeys.Add(colorKey);
            sideNav.Add(colorKey, title, colorKey, tools.Length.ToString());
        }

        private void BuildRecent()
        {
            List<string> keys = HDToolRecent.Load();
            bool header = false;
            int n = 0;
            foreach (string k in keys)
            {
                HDToolToolCard c = _cards.Find(x => x.IconKey == k);
                if (c == null) continue;
                if (!header) { sideNav.AddHeader("GẦN ĐÂY"); header = true; }
                sideNav.Add("recent:" + k, c.Title, k, "");
                if (++n >= 3) break;
            }
        }

        private void SideNav_ItemClick(object sender, HDToolSidebarEventArgs e)
        {
            string key = e.Item.Key;
            if (key.StartsWith("recent:"))
            {
                HDToolToolCard c = _cards.Find(x => x.IconKey == key.Substring(7));
                if (c != null) Card_Click(c, EventArgs.Empty);
                return;
            }
            _selGroup = key;
            sideNav.SelectedKey = key;
            sideNav.Invalidate();
            pnlContent.AutoScrollPosition = new Point(0, 0);
            if (txtSearch.Box.Text.Length > 0) txtSearch.Box.Text = ""; // tự gọi ApplyFilter
            else ApplyFilter();
        }

        private float UiScale()
        {
            return this.AutoScaleDimensions.Width > 0 ? Math.Max(1f, this.CurrentAutoScaleDimensions.Width / this.AutoScaleDimensions.Width) : 1f;
        }

        private void LayoutFlow()
        {
            if (pnlContent == null) return;
            float sc = UiScale();
            pnlContent.SuspendLayout();
            Point scroll = pnlContent.AutoScrollPosition;
            int x = pnlContent.Padding.Left;
            int w = Math.Max(120, pnlContent.ClientSize.Width - pnlContent.Padding.Horizontal);
            int y = pnlContent.Padding.Top + scroll.Y;
            foreach (System.Windows.Forms.Control c in _flow)
            {
                if (!c.Visible) continue;
                HDToolCardGrid grid = c as HDToolCardGrid;
                if (grid != null) grid.Relayout(w);
                c.SetBounds(x, y, w, c.Height);
                y += c.Height + (int)Math.Round(_gapAfter[c] * sc);
            }
            pnlContent.ResumeLayout(true);
        }

        private void LayoutFoot()
        {
            if (pnlFoot == null) return;
            int pad = (int)Math.Round(16 * UiScale());
            btnClose.Location = new Point(pnlFoot.ClientSize.Width - btnClose.Width - pad, (pnlFoot.ClientSize.Height - btnClose.Height) / 2);
            lblTip.SetBounds(pad, 0, Math.Max(40, btnClose.Left - pad * 2), pnlFoot.ClientSize.Height);
        }

        // ---------- Tìm kiếm & phím tắt ----------
        private static string Norm(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            string d = s.ToLowerInvariant().Replace('đ', 'd').Normalize(System.Text.NormalizationForm.FormD);
            System.Text.StringBuilder sb = new System.Text.StringBuilder(d.Length);
            foreach (char ch in d)
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(ch);
            }
            return sb.ToString();
        }

        /// <summary>Lọc theo nhóm chọn ở cột trái; khi gõ tìm kiếm thì tìm trên tất cả nhóm.</summary>
        private void ApplyFilter()
        {
            string q = Norm(txtSearch.Box.Text.Trim());
            if (q.Length > 0 && _selGroup != "all")
            {
                _selGroup = "all";
                sideNav.SelectedKey = "all";
                sideNav.Invalidate();
            }
            bool any = false;
            string heading = "Tất cả công cụ";
            for (int i = 0; i < _groups.Count; i++)
            {
                KeyValuePair<HDToolSectionLabel, HDToolCardGrid> g = _groups[i];
                bool inGroup = _selGroup == "all" || _selGroup == _groupKeys[i];
                if (inGroup && _selGroup != "all") heading = g.Key.Text;
                int shown = 0;
                foreach (System.Windows.Forms.Control c in g.Value.Controls)
                {
                    HDToolToolCard card = (HDToolToolCard)c;
                    bool ok = inGroup && (q.Length == 0 || Norm(card.Title + " " + card.Description + " " + card.Keywords).Contains(q));
                    card.Visible = ok;
                    if (ok) shown++;
                }
                g.Key.Visible = shown > 0;
                g.Value.Visible = shown > 0;
                if (shown > 0) any = true;
            }
            lblHeading.Text = q.Length > 0 ? "Kết quả tìm kiếm" : heading;
            lblEmpty.Visible = !any;
            LayoutFlow();
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                HDToolToolCard first = _cards.Find(c => c.Visible && c.Parent != null && c.Parent.Visible);
                if (first != null) Card_Click(first, EventArgs.Empty);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        /// <summary>Gõ chữ ở bất kỳ đâu trong Menu -> tự nhảy vào ô tìm kiếm.</summary>
        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            if (txtSearch != null && !txtSearch.Box.Focused && (char.IsLetter(e.KeyChar) || e.KeyChar == ' '))
            {
                txtSearch.Box.Focus();
                txtSearch.Box.AppendText(e.KeyChar.ToString());
                txtSearch.Box.SelectionStart = txtSearch.Box.TextLength;
                e.Handled = true;
                return;
            }
            base.OnKeyPress(e);
        }

        private void Card_Click(object sender, EventArgs e)
        {
            HDToolToolCard card = sender as HDToolToolCard;
            if (card == null) return;
            EventHandler h;
            if (!_actions.TryGetValue(card.IconKey, out h)) return;
            HDToolRecent.Touch(card.IconKey);
            h(card, EventArgs.Empty);
        }

        /// <summary>Mở Khối Lượng Ống Gió bằng đúng lệnh trên Ribbon (PostCommand) để dùng chung ExternalEvent của lệnh gốc.</summary>
        private void BtnDuctQuantity_Click(object sender, EventArgs e)
        {
            PostRibbonCommand("Khối Lượng MEP", "cmdHDToolDuctQuantity", "Khối Lượng Ống Gió");
        }

        private void PostRibbonCommand(string panelName, string buttonName, string friendlyName)
        {
            HDToolModelessHost.EnsureInitialized();
            HDToolModelessHost.Run(app =>
            {
                RevitCommandId id = null;
                try { id = RevitCommandId.LookupCommandId("CustomCtrl_%CustomCtrl_%HDTOOL%" + panelName + "%" + buttonName); }
                catch { }
                if (id != null && app.CanPostCommand(id))
                {
                    app.PostCommand(id);
                }
                else
                {
                    TaskDialog.Show("HDTOOL", "Vui lòng mở \"" + friendlyName + "\" từ thanh Ribbon HDTOOL > " + panelName + ".");
                }
            });
            this.Close();
        }

        private void BtnFamilyPalette_Click(object sender, EventArgs e)
        {
            try
            {
                HDToolModelessHost.EnsureInitialized();
                FamilyPaletteForm form = new FamilyPaletteForm(_uidoc);
                OpenChildModeless(form);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("HDTOOL PALETTE FAMILY", "Lỗi mở Palette Family 3D: " + ex.Message);
            }
        }

        private void BtnExportCad_Click(object sender, EventArgs e)
        {
            try
            {
                HDToolModelessHost.EnsureInitialized();
                ExportCadOptionsForm form = (_uidoc != null) ? new ExportCadOptionsForm(_uidoc) : new ExportCadOptionsForm(_doc);
                OpenChildModeless(form);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("HDTOOL XUẤT CAD", "Lỗi mở giao diện Xuất CAD & Quy Đổi Layer MEP: " + ex.Message);
            }
        }

        private void BtnCadToDuct_Click(object sender, EventArgs e)
        {
            try
            {
                HDToolModelessHost.EnsureInitialized();
                CadToDuctForm form = new CadToDuctForm(_uidoc);
                OpenChildModeless(form);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("HDTOOL", "Lỗi mở Dựng Ống Gió Từ CAD: " + ex.Message);
            }
        }

        private void OpenChildModeless(System.Windows.Forms.Form childForm)
        {
            HDToolModelessHost.EnsureInitialized();
            IntPtr mainHwnd = IntPtr.Zero;
            try { mainHwnd = _uidoc.Application.MainWindowHandle; } catch { }
            this.Close();
            HDToolModelessHost.ShowModeless(childForm, mainHwnd);
        }

        private void BtnAutoCrop_Click(object sender, EventArgs e)
        {
            try
            {
                HDToolModelessHost.EnsureInitialized();
                AutoCropForm form = new AutoCropForm(_uidoc, HDToolModelessHost.ExEvent, HDToolModelessHost.Handler);
                OpenChildModeless(form);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("HDTOOL CROP VIEW", "Lỗi mở giao diện Tự Động Crop View: " + ex.Message);
            }
        }

        private void BtnMepQuantity_Click(object sender, EventArgs e)
        {
            try
            {
                // ExternalEvent.Create chỉ hợp lệ trong API context -> tạo bên trong ExternalEvent dùng chung,
                // sau đó quay về luồng UI để mở Form (không gọi Create trực tiếp từ sự kiện Click của Form Modeless)
                HDToolModelessHost.EnsureInitialized();
                HDToolModelessHost.Run(app =>
                {
                    MepInspectorExternalEventHandler handler = new MepInspectorExternalEventHandler();
                    ExternalEvent extEvent = ExternalEvent.Create(handler);
                    if (this.IsDisposed || !this.IsHandleCreated) return;
                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        try
                        {
                            MepQuantityInspectorForm form = new MepQuantityInspectorForm(_uidoc, extEvent, handler);
                            OpenChildModeless(form);
                        }
                        catch (Exception ex2)
                        {
                            TaskDialog.Show("HDTOOL MEP", "Lỗi mở giao diện Thống kê khối lượng MEP: " + ex2.Message);
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                TaskDialog.Show("HDTOOL MEP", "Lỗi mở giao diện Thống kê khối lượng MEP: " + ex.Message);
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                this.Close();
                return true;
            }
            if (keyData == (Keys.Control | Keys.F) && txtSearch != null)
            {
                txtSearch.Box.Focus();
                txtSearch.Box.SelectAll();
                return true;
            }
            // Phím 1–9 mở nhanh công cụ (khi ô tìm kiếm đang trống)
            Keys k = keyData & Keys.KeyCode;
            bool plain = (keyData & (Keys.Control | Keys.Alt | Keys.Shift)) == Keys.None;
            if (plain && txtSearch != null && txtSearch.Box.Text.Length == 0 &&
                ((k >= Keys.D1 && k <= Keys.D9) || (k >= Keys.NumPad1 && k <= Keys.NumPad9)))
            {
                string digit = (k >= Keys.NumPad1 ? (int)(k - Keys.NumPad0) : (int)(k - Keys.D0)).ToString();
                HDToolToolCard card = _cards.Find(c => c.Shortcut == digit);
                if (card != null)
                {
                    Card_Click(card, EventArgs.Empty);
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void BtnReloadTool_Click(object sender, EventArgs e)
        {
            try
            {
                UpdateToolForm form = new UpdateToolForm(_uidoc.Application);
                OpenChildModeless(form);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("HDTOOL", "Lỗi mở cập nhật tool: " + ex.Message);
            }
        }

        private void BtnUpdateSheetList_Click(object sender, EventArgs e)
        {
            try
            {
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.Title = "Chọn File Excel hoặc CSV Đã Đổi Tên Bản Vẽ";
                ofd.Filter = "Tất cả file bảng tính (*.xls;*.xlsx;*.csv)|*.xls;*.xlsx;*.csv|" +
                             "Excel 97-2003 / XML (*.xls)|*.xls|" +
                             "Excel Workbook (*.xlsx)|*.xlsx|" +
                             "File CSV chuẩn UTF-8 (*.csv)|*.csv|" +
                             "Tất cả các file (*.*)|*.*";
                ofd.FilterIndex = 1;
                ofd.RestoreDirectory = true;

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    List<List<string>> rawRows = UpdateSheetListService.ReadRowsFromFile(ofd.FileName);
                    if (rawRows != null && rawRows.Count > 0)
                    {
                        List<SheetUpdateItem> items = UpdateSheetListService.MatchAndCompare(_doc, rawRows);
                        if (items.Count > 0)
                        {
                            UpdateSheetListPreviewForm preview = new UpdateSheetListPreviewForm(_doc, items);
                            OpenChildModeless(preview);
                            return;
                        }
                        else
                        {
                            TaskDialog.Show("Thông Báo", "Không tìm thấy số hiệu bản vẽ nào trong file khớp với dự án Revit.");
                        }
                    }
                    else
                    {
                        TaskDialog.Show("Thông Báo", "File đã chọn không có dữ liệu bảng tính hợp lệ.");
                    }
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Lỗi Cập Nhật", "Lỗi: " + ex.Message);
            }
            this.Close();
        }

        private void BtnSheetList_Click(object sender, EventArgs e)
        {
            try
            {
                ExportSheetListForm form = new ExportSheetListForm(_doc);
                OpenChildModeless(form);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("HDTOOL", "Lỗi mở Danh mục bản vẽ: " + ex.Message);
            }
        }

        private void BtnGrids_Click(object sender, EventArgs e)
        {
            try
            {
                HDToolModelessHost.EnsureInitialized();
                GridCropForm form = new GridCropForm(_uidoc, HDToolModelessHost.ExEvent, HDToolModelessHost.Handler);
                OpenChildModeless(form);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("HDTOOL", "Lỗi mở Căn Grids: " + ex.Message);
            }
        }

        private void BtnLevel_Click(object sender, EventArgs e)
        {
            try
            {
                LevelCropForm form = new LevelCropForm(_uidoc, new List<Level>());
                OpenChildModeless(form);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("HDTOOL", "Lỗi mở Căn Level: " + ex.Message);
            }
        }

        private void BtnSmart_Click(object sender, EventArgs e)
        {
            try
            {
                if (GridCropService.IsPlanView(_activeView) || _activeView is ViewSheet)
                {
                    HDToolModelessHost.EnsureInitialized();
                    GridCropForm form = new GridCropForm(_uidoc, HDToolModelessHost.ExEvent, HDToolModelessHost.Handler);
                    OpenChildModeless(form);
                    return;
                }

                string errorMsg;
                if (LevelCropService.IsValidContext(_activeView, out errorMsg))
                {
                    LevelCropForm form = new LevelCropForm(_uidoc, new List<Level>());
                    OpenChildModeless(form);
                    return;
                }

                TaskDialog.Show("HDTOOL", errorMsg);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("HDTOOL", "Lỗi: " + ex.Message);
            }
            this.Close();
        }
    }
}
