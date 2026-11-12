using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace HDTool
{
    /// <summary>
    /// Event Handler giao tiếp an toàn với Revit UI Thread cho chức năng CHECK VAR PLUMBING
    /// Tuyệt đối không bao giờ làm đơ hoặc treo luồng giao diện Revit
    /// </summary>
    public class CheckVarPlumbingExternalEventHandler : IExternalEventHandler
    {
        public Action<UIApplication> ActionToRun { get; set; }

        public void Execute(UIApplication app)
        {
            try
            {
                if (ActionToRun != null)
                {
                    ActionToRun(app);
                }
            }
            catch { }
        }

        public string GetName()
        {
            return "HDTOOL Check Var Plumbing Event Handler";
        }
    }

    /// <summary>
    /// Cửa sổ HUD Modeless tương tác soi sáng, đối soát khối lượng trực tiếp trên Sheet:
    /// "CHECK VAR PLUMBING" - Tốc độ tức thì, không bao giờ đơ bản vẽ, nhận diện vĩnh viễn khi mở lại!
    /// </summary>
    public class CheckVarPlumbingForm : HDToolBaseForm
    {
        private UIDocument _uidoc;
        private ExternalEvent _extEvent;
        private CheckVarPlumbingExternalEventHandler _handler;

        private ViewSheet _currentSheet;
        private Viewport _tableViewport;
        private Autodesk.Revit.DB.View _tableDrafting;
        private PlacedTableMetadata _metadata;

        // UI Controls
        private System.Windows.Forms.Panel pnlHeader;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblSubtitle;
        private System.Windows.Forms.Panel pnlToolbar;
        private System.Windows.Forms.Button btnPickOnSheet;
        private System.Windows.Forms.Button btnHighlightAll;
        private System.Windows.Forms.Button btnClearHighlight;
        private System.Windows.Forms.Button btnReload;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.TextBox txtSearch;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.DataGridView dgvItems;
        private System.Windows.Forms.Panel pnlFooter;

        private List<MepFittingItem> _displayItems = new List<MepFittingItem>();

        public CheckVarPlumbingForm(UIDocument uidoc, ExternalEvent extEvent, CheckVarPlumbingExternalEventHandler handler)
        {
            _uidoc = uidoc;
            _extEvent = extEvent;
            _handler = handler;

            InitializeUi();
            LoadActiveSheetData();
        }

        private void InitializeUi()
        {
            this.Text = "HDTOOL - CHECK VAR PLUMBING (Soi Sáng & Đối Soát Khối Lượng Trên Sheet)";
            this.Size = new System.Drawing.Size(900, 560);
            this.MinimumSize = new System.Drawing.Size(640, 380);
            this.StartPosition = FormStartPosition.Manual;
            this.TopMost = true;
            this.FormBorderStyle = FormBorderStyle.SizableToolWindow;
            this.BackColor = System.Drawing.Color.FromArgb(248, 250, 252);

            // Bắt phím ESC để đóng tool ngay lập tức
            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    this.Close();
                }
            };

            // Khi đóng Form (ESC hoặc nút Đóng): BỎ HIGHLIGHT TOÀN BỘ ĐỐI TƯỢNG NGAY ĐI
            this.FormClosing += (s, e) =>
            {
                if (_handler != null && _extEvent != null)
                {
                    _handler.ActionToRun = (app) =>
                    {
                        try
                        {
                            UIDocument uidoc = app.ActiveUIDocument != null ? app.ActiveUIDocument : _uidoc;
                            MepHighlightManager.ResetAllVisualsAndHighlights(uidoc);
                        }
                        catch { }
                    };
                    _extEvent.Raise();
                }
                else if (_uidoc != null)
                {
                    MepHighlightManager.ResetAllVisualsAndHighlights(_uidoc);
                }
            };

            // Đặt vị trí cửa sổ ở góc trên bên phải màn hình làm việc
            try
            {
                var workArea = Screen.PrimaryScreen.WorkingArea;
                int posX = Math.Max(workArea.Right - 920, 50);
                int posY = Math.Max(workArea.Top + 80, 50);
                this.Location = new System.Drawing.Point(posX, posY);
            }
            catch { }

            // 1. HEADER PANEL
            pnlHeader = new System.Windows.Forms.Panel();
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = 58;
            pnlHeader.BackColor = System.Drawing.Color.FromArgb(15, 23, 42); // Navy Dark Slate

            lblTitle = new System.Windows.Forms.Label();
            lblTitle.Text = "CHECK VAR PLUMBING - SOI SÁNG ĐỐI SOÁT KHỐI LƯỢNG";
            lblTitle.Location = new System.Drawing.Point(14, 8);
            lblTitle.AutoSize = true;
            lblTitle.Font = new System.Drawing.Font("Segoe UI", 11.5f, FontStyle.Bold);
            lblTitle.ForeColor = System.Drawing.Color.FromArgb(245, 158, 11); // Amber Gold

            lblSubtitle = new System.Windows.Forms.Label();
            lblSubtitle.Text = "Click chọn dòng trong bảng (hoặc mũi tên Lên/Xuống) để đối tượng sáng đỏ trên Revit! Bấm ESC để đóng và bỏ highlight ngay lập tức.";
            lblSubtitle.Location = new System.Drawing.Point(15, 33);
            lblSubtitle.AutoSize = true;
            lblSubtitle.Font = new System.Drawing.Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblSubtitle.ForeColor = System.Drawing.Color.FromArgb(203, 213, 225);

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);

            // 2. TOOLBAR PANEL
            pnlToolbar = new System.Windows.Forms.Panel();
            pnlToolbar.Dock = DockStyle.Top;
            pnlToolbar.Height = 44;
            pnlToolbar.BackColor = System.Drawing.Color.FromArgb(241, 245, 249);
            pnlToolbar.Padding = new Padding(8, 6, 8, 6);

            btnPickOnSheet = new System.Windows.Forms.Button();
            btnPickOnSheet.Text = "Pick Trên Sheet";
            btnPickOnSheet.Location = new System.Drawing.Point(10, 6);
            btnPickOnSheet.Size = new System.Drawing.Size(150, 32);
            btnPickOnSheet.BackColor = System.Drawing.Color.FromArgb(2, 132, 199); // Sky Blue
            btnPickOnSheet.ForeColor = System.Drawing.Color.White;
            btnPickOnSheet.FlatStyle = FlatStyle.Flat;
            btnPickOnSheet.FlatAppearance.BorderSize = 0;
            btnPickOnSheet.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnPickOnSheet.Cursor = Cursors.Hand;
            btnPickOnSheet.Click += BtnPickOnSheet_Click;

            btnHighlightAll = new System.Windows.Forms.Button();
            btnHighlightAll.Text = "Sáng Toàn Bảng";
            btnHighlightAll.Location = new System.Drawing.Point(168, 6);
            btnHighlightAll.Size = new System.Drawing.Size(135, 32);
            btnHighlightAll.BackColor = System.Drawing.Color.FromArgb(217, 119, 6); // Amber
            btnHighlightAll.ForeColor = System.Drawing.Color.White;
            btnHighlightAll.FlatStyle = FlatStyle.Flat;
            btnHighlightAll.FlatAppearance.BorderSize = 0;
            btnHighlightAll.Font = new System.Drawing.Font("Segoe UI", 8.5f, FontStyle.Bold);
            btnHighlightAll.Cursor = Cursors.Hand;
            btnHighlightAll.Click += BtnHighlightAll_Click;

            btnClearHighlight = new System.Windows.Forms.Button();
            btnClearHighlight.Text = "Tắt Sáng";
            btnClearHighlight.Location = new System.Drawing.Point(310, 6);
            btnClearHighlight.Size = new System.Drawing.Size(95, 32);
            btnClearHighlight.BackColor = System.Drawing.Color.FromArgb(100, 116, 139); // Slate Gray
            btnClearHighlight.ForeColor = System.Drawing.Color.White;
            btnClearHighlight.FlatStyle = FlatStyle.Flat;
            btnClearHighlight.FlatAppearance.BorderSize = 0;
            btnClearHighlight.Font = new System.Drawing.Font("Segoe UI", 8.5f, FontStyle.Bold);
            btnClearHighlight.Cursor = Cursors.Hand;
            btnClearHighlight.Click += BtnClearHighlight_Click;

            btnReload = new System.Windows.Forms.Button();
            btnReload.Text = "Nạp Lại";
            btnReload.Location = new System.Drawing.Point(412, 6);
            btnReload.Size = new System.Drawing.Size(95, 32);
            btnReload.BackColor = System.Drawing.Color.FromArgb(16, 149, 193);
            btnReload.ForeColor = System.Drawing.Color.White;
            btnReload.FlatStyle = FlatStyle.Flat;
            btnReload.FlatAppearance.BorderSize = 0;
            btnReload.Font = new System.Drawing.Font("Segoe UI", 8.5f, FontStyle.Bold);
            btnReload.Cursor = Cursors.Hand;
            btnReload.Click += (s, e) => LoadActiveSheetData();

            txtSearch = new System.Windows.Forms.TextBox();
            txtSearch.Location = new System.Drawing.Point(518, 10);
            txtSearch.Size = new System.Drawing.Size(220, 24);
            txtSearch.Font = new System.Drawing.Font("Segoe UI", 9.0f);
            txtSearch.Text = "Tìm nhanh quy cách...";
            txtSearch.ForeColor = System.Drawing.Color.Gray;
            txtSearch.Enter += (s, e) => { if (txtSearch.Text.Contains("Tìm")) { txtSearch.Text = ""; txtSearch.ForeColor = System.Drawing.Color.Black; } };
            txtSearch.Leave += (s, e) => { if (string.IsNullOrWhiteSpace(txtSearch.Text)) { txtSearch.Text = "Tìm nhanh quy cách..."; txtSearch.ForeColor = System.Drawing.Color.Gray; } };
            txtSearch.TextChanged += (s, e) => FilterItems();

            pnlToolbar.Controls.Add(btnPickOnSheet);
            pnlToolbar.Controls.Add(btnHighlightAll);
            pnlToolbar.Controls.Add(btnClearHighlight);
            pnlToolbar.Controls.Add(btnReload);
            pnlToolbar.Controls.Add(txtSearch);

            btnClose = new System.Windows.Forms.Button();
            btnClose.Text = "Đóng (ESC)";
            btnClose.Location = new System.Drawing.Point(750, 6);
            btnClose.Size = new System.Drawing.Size(100, 32);
            btnClose.BackColor = System.Drawing.Color.FromArgb(226, 232, 240);
            btnClose.ForeColor = System.Drawing.Color.FromArgb(51, 65, 85);
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.FlatAppearance.BorderSize = 1;
            btnClose.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(203, 213, 225);
            btnClose.Font = new System.Drawing.Font("Segoe UI", 8.5f, FontStyle.Bold);
            btnClose.Cursor = Cursors.Hand;
            btnClose.Click += (s, e) => { this.Close(); };
            pnlToolbar.Controls.Add(btnClose);
            this.CancelButton = btnClose;

            // 3. DATAGRIDVIEW
            dgvItems = new System.Windows.Forms.DataGridView();
            dgvItems.Dock = DockStyle.Fill;
            dgvItems.BackgroundColor = System.Drawing.Color.White;
            dgvItems.BorderStyle = BorderStyle.None;
            dgvItems.AllowUserToAddRows = false;
            dgvItems.AllowUserToDeleteRows = false;
            dgvItems.ReadOnly = true;
            dgvItems.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvItems.MultiSelect = false;
            dgvItems.RowHeadersVisible = false;
            dgvItems.EnableHeadersVisualStyles = false;
            dgvItems.ColumnHeadersHeight = 32;
            dgvItems.RowTemplate.Height = 26;
            dgvItems.Font = new System.Drawing.Font("Segoe UI", 9.0f);

            // Header Style
            dgvItems.ColumnHeadersDefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(30, 41, 59);
            dgvItems.ColumnHeadersDefaultCellStyle.ForeColor = System.Drawing.Color.White;
            dgvItems.ColumnHeadersDefaultCellStyle.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Bold);
            dgvItems.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            // Selection Style
            dgvItems.DefaultCellStyle.SelectionBackColor = System.Drawing.Color.FromArgb(239, 68, 68); // Red-500
            dgvItems.DefaultCellStyle.SelectionForeColor = System.Drawing.Color.White;

            BuildColumns();

            dgvItems.SelectionChanged += DgvItems_SelectionChanged;

            // 4. FOOTER STATUS PANEL
            pnlFooter = new System.Windows.Forms.Panel();
            pnlFooter.Dock = DockStyle.Bottom;
            pnlFooter.Height = 32;
            pnlFooter.BackColor = System.Drawing.Color.FromArgb(241, 245, 249);
            pnlFooter.BorderStyle = BorderStyle.FixedSingle;

            lblStatus = new System.Windows.Forms.Label();
            lblStatus.Text = "CHECK VAR sẵn sàng. Click bất kỳ dòng nào để soi sáng ngay tức thì!";
            lblStatus.Location = new System.Drawing.Point(10, 6);
            lblStatus.AutoSize = true;
            lblStatus.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Bold);
            lblStatus.ForeColor = System.Drawing.Color.FromArgb(2, 132, 199);

            pnlFooter.Controls.Add(lblStatus);

            this.Controls.Add(dgvItems);
            this.Controls.Add(pnlFooter);
            this.Controls.Add(pnlToolbar);
            this.Controls.Add(pnlHeader);
        }

        private void BuildColumns()
        {
            dgvItems.Columns.Clear();

            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "STT", Width = 45, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Tên Phụ Kiện / Đường Ống", Width = 230, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleLeft, Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Bold) } });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Phân Loại", Width = 90, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Size", Width = 95, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Bold) } });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "SL", Width = 50, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Bold) } });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Chiều Dài (m)", Width = 95, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Bold) } });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Type", Width = 85, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleLeft } });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Khu Vực", Width = 95, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "PN", Width = 55, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Tầng", Width = 80, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Số ID", Width = 60, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, ForeColor = System.Drawing.Color.Gray } });
        }

        public void LoadActiveSheetData()
        {
            if (_uidoc == null) return;
            Document doc = _uidoc.Document;
            if (doc == null) return;

            Autodesk.Revit.DB.View curView = _uidoc.ActiveView;
            _currentSheet = curView as ViewSheet;

            if (_currentSheet == null)
            {
                // Thử tìm Sheet mở gần nhất
                try
                {
                    var openViews = _uidoc.GetOpenUIViews();
                    foreach (var uiv in openViews)
                    {
                        var v = doc.GetElement(uiv.ViewId) as ViewSheet;
                        if (v != null) { _currentSheet = v; break; }
                    }
                }
                catch { }
            }

            if (_currentSheet == null)
            {
                lblTitle.Text = "CHECK VAR PLUMBING - (Chưa mở bản vẽ Sheet)";
                lblStatus.ForeColor = System.Drawing.Color.FromArgb(220, 38, 38);
                lblStatus.Text = "Vui lòng mở bản vẽ Sheet có chứa Bảng Khối Lượng và bấm 'Nạp Lại'!";
                dgvItems.Rows.Clear();
                return;
            }

            lblTitle.Text = string.Format("CHECK VAR PLUMBING - SHEET: {0} - {1}", _currentSheet.SheetNumber, _currentSheet.Name);

            // Tìm và tải metadata của bảng
            _metadata = MepQuantityService.ResolvePlacedTableMetadataForSheet(doc, _currentSheet, out _tableViewport, out _tableDrafting);

            if (_metadata == null || _metadata.Items == null || _metadata.Items.Count == 0)
            {
                lblStatus.ForeColor = System.Drawing.Color.FromArgb(234, 88, 12);
                lblStatus.Text = "Không tìm thấy Bảng Khối Lượng HDTOOL trên Sheet này. Hãy đặt bảng lên Sheet trước!";
                dgvItems.Rows.Clear();
                return;
            }

            FilterItems();

            lblStatus.ForeColor = System.Drawing.Color.FromArgb(16, 185, 129);
            lblStatus.Text = string.Format("Đã tải {0} dòng khối lượng. Click vào dòng bất kỳ để SOI SÁNG TRÊN REVIT!", _metadata.Items.Count);
        }

        private void FilterItems()
        {
            if (_metadata == null || _metadata.Items == null) return;

            string query = "";
            if (txtSearch != null && !string.IsNullOrWhiteSpace(txtSearch.Text) && !txtSearch.Text.Contains("Tìm"))
            {
                query = MepQuantityService.RemoveDiacritics(txtSearch.Text.Trim());
            }

            _displayItems = new List<MepFittingItem>();
            foreach (var it in _metadata.Items)
            {
                if (!string.IsNullOrEmpty(query))
                {
                    string target = MepQuantityService.RemoveDiacritics(string.Format("{0} {1} {2} {3} {4} {5}",
                        it.MergedName, it.Size, it.TypeName, it.LocationByArea, it.LocationByLevel, it.SystemType));
                    if (!target.Contains(query)) continue;
                }
                _displayItems.Add(it);
            }

            dgvItems.Rows.Clear();
            for (int i = 0; i < _displayItems.Count; i++)
            {
                var it = _displayItems[i];
                int rowIdx = dgvItems.Rows.Add(
                    it.STT > 0 ? it.STT : (i + 1),
                    it.MergedName,
                    it.CategoryKind,
                    it.Size,
                    it.Count,
                    it.LengthM > 0 ? it.LengthM.ToString("0.00") : "-",
                    it.TypeName,
                    it.LocationByArea,
                    it.PN,
                    it.LocationByLevel,
                    it.ElementIds != null ? it.ElementIds.Count : 0
                );

                if (it.CategoryKind == "Đường Ống")
                {
                    dgvItems.Rows[rowIdx].Cells[1].Style.ForeColor = System.Drawing.Color.FromArgb(2, 132, 199);
                    dgvItems.Rows[rowIdx].Cells[2].Style.ForeColor = System.Drawing.Color.FromArgb(2, 132, 199);
                    dgvItems.Rows[rowIdx].Cells[5].Style.ForeColor = System.Drawing.Color.FromArgb(2, 132, 199);
                }
                else
                {
                    dgvItems.Rows[rowIdx].Cells[1].Style.ForeColor = System.Drawing.Color.FromArgb(124, 58, 237);
                    dgvItems.Rows[rowIdx].Cells[2].Style.ForeColor = System.Drawing.Color.FromArgb(124, 58, 237);
                }

                dgvItems.Rows[rowIdx].Tag = it;
            }
        }

        private void DgvItems_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvItems.SelectedRows.Count == 0) return;
            DataGridViewRow row = dgvItems.SelectedRows[0];
            MepFittingItem it = row.Tag as MepFittingItem;
            if (it == null || it.ElementIds == null || it.ElementIds.Count == 0) return;

            string info = string.Format("{0} [{1}] (SL: {2}{3})",
                it.MergedName, it.Size, it.Count, it.LengthM > 0 ? (" | " + it.LengthM.ToString("0.00") + "m") : "");

            lblStatus.ForeColor = System.Drawing.Color.FromArgb(239, 68, 68); // Red
            lblStatus.Text = string.Format("Đang CHECK VAR: {0} -> SÁNG ĐỎ TRÊN REVIT!", info);

            TriggerHighlight(it.ElementIds, false);
        }

        private void BtnHighlightAll_Click(object sender, EventArgs e)
        {
            if (_metadata == null || _metadata.Items == null || _metadata.Items.Count == 0) return;

            List<ElementId> allIds = new List<ElementId>();
            foreach (var it in _metadata.Items)
            {
                if (it.ElementIds != null) allIds.AddRange(it.ElementIds);
            }

            if (allIds.Count > 0)
            {
                lblStatus.ForeColor = System.Drawing.Color.FromArgb(217, 119, 6);
                lblStatus.Text = string.Format("Đang làm sáng TOÀN BỘ {0} đối tượng trên bản vẽ Revit!", allIds.Count);
                TriggerHighlight(allIds, false);
            }
        }

        private void BtnClearHighlight_Click(object sender, EventArgs e)
        {
            if (_handler != null && _extEvent != null)
            {
                _handler.ActionToRun = (app) =>
                {
                    try
                    {
                        Document doc = app.ActiveUIDocument != null ? app.ActiveUIDocument.Document : _uidoc.Document;
                        if (doc != null)
                        {
                            MepHighlightManager.ClearHighlights(doc);
                            if (app.ActiveUIDocument != null)
                            {
                                app.ActiveUIDocument.Selection.SetElementIds(new List<ElementId>());
                                app.ActiveUIDocument.RefreshActiveView();
                            }
                        }
                    }
                    catch { }
                };
                _extEvent.Raise();
            }

            lblStatus.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            lblStatus.Text = "Đã tắt toàn bộ chế độ soi sáng Highlight.";
        }

        private void BtnPickOnSheet_Click(object sender, EventArgs e)
        {
            if (_currentSheet == null || _tableViewport == null || _metadata == null || _metadata.Items == null || _metadata.Items.Count == 0)
            {
                MessageBox.Show("Vui lòng mở một bản vẽ Sheet có chứa Bảng Khối Lượng HDTOOL!", "HDTOOL", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Thu nhỏ nhẹ cửa sổ để Sếp nhìn rõ bản vẽ Sheet
            this.WindowState = FormWindowState.Minimized;

            if (_handler != null && _extEvent != null)
            {
                _handler.ActionToRun = (app) =>
                {
                    try
                    {
                        UIDocument uidoc = app.ActiveUIDocument;
                        if (uidoc == null) return;

                        // Thực hiện Pick 1 điểm an toàn trên Sheet (không while(true) vô tận)
                        XYZ pt = uidoc.Selection.PickPoint(
                            ObjectSnapTypes.None,
                            "HDTOOL CHECK VAR: Click vào dòng bất kỳ của bảng trên Sheet..."
                        );

                        // Tính toán dòng
                        int targetRow = MepQuantityService.CalculateRowIndexFromPickedPoint(pt, _tableViewport, _metadata);

                        SafeInvoke(() =>
                        {
                            this.WindowState = FormWindowState.Normal;
                            this.BringToFront();
                            this.Activate();

                            if (targetRow >= 0 && targetRow < dgvItems.Rows.Count)
                            {
                                dgvItems.ClearSelection();
                                dgvItems.Rows[targetRow].Selected = true;
                                dgvItems.FirstDisplayedScrollingRowIndex = targetRow;
                            }
                            else if (targetRow == -1)
                            {
                                // Click Tiêu đề -> Sáng toàn bảng
                                BtnHighlightAll_Click(null, null);
                            }
                        });
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        // Sếp bấm ESC hoặc hủy
                        SafeInvoke(() =>
                        {
                            this.WindowState = FormWindowState.Normal;
                            this.BringToFront();
                        });
                    }
                    catch (Exception ex)
                    {
                        SafeInvoke(() =>
                        {
                            this.WindowState = FormWindowState.Normal;
                            this.BringToFront();
                            lblStatus.Text = "Lỗi pick: " + ex.Message;
                        });
                    }
                };
                _extEvent.Raise();
            }
        }

        private void TriggerHighlight(ICollection<ElementId> ids, bool zoom)
        {
            if (ids == null || ids.Count == 0) return;

            if (_handler != null && _extEvent != null)
            {
                _handler.ActionToRun = (app) =>
                {
                    try
                    {
                        UIDocument uidoc = app.ActiveUIDocument != null ? app.ActiveUIDocument : _uidoc;
                        if (uidoc != null)
                        {
                            MepHighlightManager.HighlightElements(uidoc, ids, new Autodesk.Revit.DB.Color(255, 0, 0), 7, zoom);
                        }
                    }
                    catch { }
                };
                _extEvent.Raise();
            }
            else
            {
                try
                {
                    if (_uidoc != null)
                    {
                        MepHighlightManager.HighlightElements(_uidoc, ids, new Autodesk.Revit.DB.Color(255, 0, 0), 7, zoom);
                    }
                }
                catch { }
            }
        }

        public void SafeInvoke(Action action)
        {
            if (action == null || this.IsDisposed) return;
            try
            {
                if (this.IsHandleCreated && this.InvokeRequired)
                {
                    this.BeginInvoke(action);
                }
                else
                {
                    action();
                }
            }
            catch { }
        }
    }
}

