using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Color = System.Drawing.Color;
using Font = System.Drawing.Font;
using Point = System.Drawing.Point;

namespace HDTool
{
    public class UpdateSheetListPreviewForm : HDToolBaseForm
    {
        private Document _doc;
        private List<SheetUpdateItem> _allItems;
        private List<SheetUpdateItem> _filteredItems;
        private bool _isUpdatingCheck = false;
        private int _lastCheckedRowIndex = -1;
        private bool _lastTargetCheckState = true;

        public int UpdatedCount { get; private set; }
        public event Action<int> UpdatesApplied;

        // UI Controls
        private System.Windows.Forms.Panel pnlHeader;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblSubtitle;
        private PictureBox picLogo;

        private System.Windows.Forms.GroupBox grpFilter;
        private System.Windows.Forms.Label lblSearch;
        private System.Windows.Forms.TextBox txtSearch;
        private System.Windows.Forms.CheckBox chkOnlyChanges;
        private System.Windows.Forms.Button btnSelectChanges;
        private System.Windows.Forms.Button btnDeselectAll;

        private DataGridView dgvPreview;
        private DataGridViewCheckBoxColumn colCheck;
        private DataGridViewTextBoxColumn colNumber;
        private DataGridViewTextBoxColumn colCurrentName;
        private DataGridViewTextBoxColumn colNewName;
        private DataGridViewTextBoxColumn colRev;
        private DataGridViewTextBoxColumn colStatus;

        private System.Windows.Forms.Panel pnlBottom;
        private System.Windows.Forms.Label lblSummary;
        private System.Windows.Forms.Button btnApply;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.ToolTip toolTip;

        public UpdateSheetListPreviewForm(Document doc, List<SheetUpdateItem> items)
        {
            _doc = doc;
            _allItems = items ?? new List<SheetUpdateItem>();
            _filteredItems = new List<SheetUpdateItem>();
            UpdatedCount = 0;

            InitializeComponent();
            ApplyFilter();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.Text = "HDTOOL - Xem Trước & Xác Nhận Cập Nhật Bản Vẽ Từ Excel";
            this.Size = new Size(950, 680);
            this.MinimumSize = new Size(820, 520);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.TopMost = true;
            this.BackColor = Color.FromArgb(245, 247, 250);
            this.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

            toolTip = new System.Windows.Forms.ToolTip();

            // 1. Header Panel
            pnlHeader = new System.Windows.Forms.Panel();
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = 75;
            pnlHeader.BackColor = Color.FromArgb(24, 43, 73);

            lblTitle = new System.Windows.Forms.Label();
            lblTitle.Text = "HDTOOL - XÁC NHẬN CẬP NHẬT TÊN BẢN VẼ TỪ EXCEL VÀO REVIT";
            lblTitle.ForeColor = Color.White;
            lblTitle.Font = new Font("Segoe UI", 11.0f, FontStyle.Bold);
            lblTitle.Location = new Point(16, 14);
            lblTitle.AutoSize = true;

            lblSubtitle = new System.Windows.Forms.Label();
            lblSubtitle.Text = "Kiểm tra các thay đổi bên dưới trước khi ghi đè vào Revit. Tích chọn các bản vẽ muốn áp dụng.";
            lblSubtitle.ForeColor = Color.FromArgb(189, 195, 199);
            lblSubtitle.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblSubtitle.Location = new Point(16, 42);
            lblSubtitle.AutoSize = true;

            picLogo = new PictureBox();
            picLogo.Size = new Size(130, 45);
            picLogo.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            picLogo.Location = new Point(790, 15);
            picLogo.SizeMode = PictureBoxSizeMode.Zoom;
            picLogo.BackColor = Color.Transparent;
            try
            {
                using (Stream stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("hadomee_logo.png"))
                {
                    if (stream != null) picLogo.Image = Image.FromStream(stream);
                }
            }
            catch { }

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);
            pnlHeader.Controls.Add(picLogo);

            // 2. Filter Bar
            grpFilter = new System.Windows.Forms.GroupBox();
            grpFilter.Text = "Bộ Lọc Hiển Thị";
            grpFilter.Dock = DockStyle.Top;
            grpFilter.Height = 72;
            grpFilter.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            grpFilter.ForeColor = Color.FromArgb(44, 62, 80);
            grpFilter.Padding = new Padding(12, 5, 12, 8);

            lblSearch = new System.Windows.Forms.Label();
            lblSearch.Text = "Tìm kiếm:";
            lblSearch.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
            lblSearch.Location = new Point(15, 26);
            lblSearch.AutoSize = true;

            txtSearch = new System.Windows.Forms.TextBox();
            txtSearch.Location = new Point(78, 23);
            txtSearch.Size = new Size(200, 24);
            txtSearch.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
            txtSearch.TextChanged += (s, e) => ApplyFilter();
            toolTip.SetToolTip(txtSearch, "Nhập số hiệu hoặc tên bản vẽ");

            chkOnlyChanges = new System.Windows.Forms.CheckBox();
            chkOnlyChanges.Text = "Chỉ hiện các bản vẽ có thay đổi";
            chkOnlyChanges.Checked = true;
            chkOnlyChanges.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
            chkOnlyChanges.Location = new Point(300, 25);
            chkOnlyChanges.AutoSize = true;
            chkOnlyChanges.CheckedChanged += (s, e) => ApplyFilter();

            btnSelectChanges = new System.Windows.Forms.Button();
            btnSelectChanges.Text = "✓ Chọn Tất Cả Có Thay Đổi";
            btnSelectChanges.Location = new Point(540, 22);
            btnSelectChanges.Size = new Size(185, 26);
            btnSelectChanges.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            btnSelectChanges.Click += BtnSelectChanges_Click;

            btnDeselectAll = new System.Windows.Forms.Button();
            btnDeselectAll.Text = "✗ Bỏ Chọn Hết";
            btnDeselectAll.Location = new Point(735, 22);
            btnDeselectAll.Size = new Size(105, 26);
            btnDeselectAll.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            btnDeselectAll.Click += (s, e) => SetSelectionState(false);

            grpFilter.Controls.Add(lblSearch);
            grpFilter.Controls.Add(txtSearch);
            grpFilter.Controls.Add(chkOnlyChanges);
            grpFilter.Controls.Add(btnSelectChanges);
            grpFilter.Controls.Add(btnDeselectAll);

            // 3. DataGridView Setup
            dgvPreview = new DataGridView();
            dgvPreview.Dock = DockStyle.Fill;
            dgvPreview.BackgroundColor = Color.White;
            dgvPreview.BorderStyle = BorderStyle.None;
            dgvPreview.RowHeadersVisible = false;
            dgvPreview.AllowUserToAddRows = false;
            dgvPreview.AllowUserToDeleteRows = false;
            dgvPreview.AllowUserToResizeRows = false;
            dgvPreview.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvPreview.MultiSelect = true;
            dgvPreview.RowTemplate.Height = 27;
            dgvPreview.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 252);
            dgvPreview.DefaultCellStyle.SelectionBackColor = Color.FromArgb(214, 234, 248);
            dgvPreview.DefaultCellStyle.SelectionForeColor = Color.Black;
            dgvPreview.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(235, 240, 245);
            dgvPreview.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(44, 62, 80);
            dgvPreview.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            dgvPreview.ColumnHeadersHeight = 32;
            dgvPreview.EnableHeadersVisualStyles = false;

            // Bật DoubleBuffered
            try
            {
                typeof(DataGridView).GetProperty("DoubleBuffered",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(dgvPreview, true, null);
            }
            catch { }

            colCheck = new DataGridViewCheckBoxColumn();
            colCheck.Name = "colCheck";
            colCheck.HeaderText = "Chọn";
            colCheck.Width = 50;
            colCheck.Frozen = true;
            dgvPreview.Columns.Add(colCheck);

            colNumber = new DataGridViewTextBoxColumn();
            colNumber.Name = "colNumber";
            colNumber.HeaderText = "Số Hiệu (Sheet Number)";
            colNumber.Width = 140;
            colNumber.ReadOnly = true;
            colNumber.DefaultCellStyle.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            colNumber.DefaultCellStyle.ForeColor = Color.FromArgb(41, 128, 185);
            dgvPreview.Columns.Add(colNumber);

            colCurrentName = new DataGridViewTextBoxColumn();
            colCurrentName.Name = "colCurrentName";
            colCurrentName.HeaderText = "Tên Hiện Tại (Trong Revit)";
            colCurrentName.Width = 240;
            colCurrentName.ReadOnly = true;
            colCurrentName.DefaultCellStyle.ForeColor = Color.FromArgb(120, 120, 120);
            dgvPreview.Columns.Add(colCurrentName);

            colNewName = new DataGridViewTextBoxColumn();
            colNewName.Name = "colNewName";
            colNewName.HeaderText = "Tên Mới (Từ Excel)";
            colNewName.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colNewName.ReadOnly = true;
            colNewName.DefaultCellStyle.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            colNewName.DefaultCellStyle.ForeColor = Color.FromArgb(39, 174, 96);
            dgvPreview.Columns.Add(colNewName);

            colRev = new DataGridViewTextBoxColumn();
            colRev.Name = "colRev";
            colRev.HeaderText = "Phiên Bản (Rev)";
            colRev.Width = 100;
            colRev.ReadOnly = true;
            colRev.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvPreview.Columns.Add(colRev);

            colStatus = new DataGridViewTextBoxColumn();
            colStatus.Name = "colStatus";
            colStatus.HeaderText = "Trạng Thái / Thay Đổi";
            colStatus.Width = 160;
            colStatus.ReadOnly = true;
            colStatus.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvPreview.Columns.Add(colStatus);

            dgvPreview.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (dgvPreview.IsCurrentCellDirty && dgvPreview.CurrentCellAddress.X == colCheck.Index)
                {
                    if ((System.Windows.Forms.Control.ModifierKeys & Keys.Shift) == Keys.Shift)
                    {
                        dgvPreview.CancelEdit();
                        return;
                    }
                    dgvPreview.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };
            dgvPreview.CellContentClick += DgvPreview_CellContentClick;
            dgvPreview.CellMouseDown += DgvPreview_CellMouseDown;
            dgvPreview.KeyDown += DgvPreview_KeyDown;
            dgvPreview.ColumnHeaderMouseClick += (s, e) =>
            {
                if (e.ColumnIndex == colCheck.Index)
                {
                    bool allSelected = _filteredItems.All(delegate(SheetUpdateItem x) { return x.IsSelected; });
                    SetSelectionState(!allSelected);
                }
            };

            // Setup Context Menu for dgvPreview
            ContextMenuStrip cmsPreview = new ContextMenuStrip();
            cmsPreview.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

            ToolStripMenuItem miCheck = new ToolStripMenuItem("✔ Tích chọn các dòng đang bôi đen (Phím Cách)");
            miCheck.Click += (s, e) => SetSelectedRowsCheckState(true);

            ToolStripMenuItem miUncheck = new ToolStripMenuItem("✖ Bỏ chọn các dòng đang bôi đen");
            miUncheck.Click += (s, e) => SetSelectedRowsCheckState(false);

            ToolStripMenuItem miInvert = new ToolStripMenuItem("◫ Đảo chọn các dòng đang bôi đen");
            miInvert.Click += (s, e) =>
            {
                if (dgvPreview.SelectedRows.Count == 0) return;
                _isUpdatingCheck = true;
                try
                {
                    foreach (DataGridViewRow row in dgvPreview.SelectedRows)
                    {
                        bool cur = Convert.ToBoolean(row.Cells[colCheck.Index].Value);
                        row.Cells[colCheck.Index].Value = !cur;
                        SheetUpdateItem item = row.Tag as SheetUpdateItem;
                        if (item != null) item.IsSelected = !cur;
                    }
                }
                finally { _isUpdatingCheck = false; }
                UpdateSummaryLabel();
                dgvPreview.InvalidateColumn(colCheck.Index);
            };

            cmsPreview.Items.Add(miCheck);
            cmsPreview.Items.Add(miUncheck);
            cmsPreview.Items.Add(miInvert);
            dgvPreview.ContextMenuStrip = cmsPreview;

            // 4. Bottom Panel
            pnlBottom = new System.Windows.Forms.Panel();
            pnlBottom.Dock = DockStyle.Bottom;
            pnlBottom.Height = 85;
            pnlBottom.BackColor = Color.FromArgb(240, 243, 246);

            lblSummary = new System.Windows.Forms.Label();
            lblSummary.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            lblSummary.ForeColor = Color.FromArgb(24, 43, 73);
            lblSummary.Location = new Point(16, 12);
            lblSummary.Size = new Size(600, 22);

            btnApply = new System.Windows.Forms.Button();
            btnApply.Text = "🚀  CẬP NHẬT VÀO REVIT (ÁP DỤNG NGAY)";
            btnApply.Font = new Font("Segoe UI", 10.0f, FontStyle.Bold);
            btnApply.BackColor = Color.FromArgb(211, 84, 0); // Orange Accent
            btnApply.ForeColor = Color.White;
            btnApply.FlatStyle = FlatStyle.Flat;
            btnApply.FlatAppearance.BorderSize = 0;
            btnApply.Location = new Point(16, 38);
            btnApply.Size = new Size(380, 40);
            btnApply.Click += BtnApply_Click;
            toolTip.SetToolTip(btnApply, "Ghi đè tên bản vẽ mới vào các ViewSheet đang được tích chọn trong Revit");

            btnCancel = new System.Windows.Forms.Button();
            btnCancel.Text = "Hủy Bỏ";
            btnCancel.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
            btnCancel.Location = new Point(815, 38);
            btnCancel.Size = new Size(110, 38);
            btnCancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
            this.CancelButton = btnCancel;

            pnlBottom.Controls.Add(lblSummary);
            pnlBottom.Controls.Add(btnApply);
            pnlBottom.Controls.Add(btnCancel);

            // Gắn vào Form
            this.Controls.Add(dgvPreview);
            this.Controls.Add(grpFilter);
            this.Controls.Add(pnlHeader);
            this.Controls.Add(pnlBottom);

            this.ResumeLayout(false);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void ApplyFilter()
        {
            string keyword = (txtSearch.Text ?? string.Empty).Trim().ToLowerInvariant();
            bool onlyChanges = chkOnlyChanges.Checked;

            _filteredItems.Clear();
            foreach (SheetUpdateItem item in _allItems)
            {
                if (onlyChanges && item.Status != SheetUpdateStatus.Changed) continue;

                if (!string.IsNullOrEmpty(keyword))
                {
                    bool matchNum = (item.SheetNumber ?? string.Empty).ToLowerInvariant().Contains(keyword);
                    bool matchCurName = (item.CurrentSheetName ?? string.Empty).ToLowerInvariant().Contains(keyword);
                    bool matchNewName = (item.NewSheetName ?? string.Empty).ToLowerInvariant().Contains(keyword);
                    if (!matchNum && !matchCurName && !matchNewName) continue;
                }

                _filteredItems.Add(item);
            }

            RefreshGrid();
        }

        private void RefreshGrid()
        {
            _isUpdatingCheck = true;
            dgvPreview.Rows.Clear();

            foreach (SheetUpdateItem item in _filteredItems)
            {
                int rowIdx = dgvPreview.Rows.Add();
                DataGridViewRow row = dgvPreview.Rows[rowIdx];
                row.Tag = item;

                row.Cells["colCheck"].Value = item.IsSelected;
                row.Cells["colNumber"].Value = item.SheetNumber;
                row.Cells["colCurrentName"].Value = item.CurrentSheetName;
                row.Cells["colNewName"].Value = item.NewSheetName;

                // Revision info
                if (!string.IsNullOrEmpty(item.NewRevision) && item.NewRevision != item.CurrentRevision)
                {
                    row.Cells["colRev"].Value = string.Format("{0} ➔ {1}", item.CurrentRevision, item.NewRevision);
                }
                else
                {
                    row.Cells["colRev"].Value = item.CurrentRevision;
                }

                row.Cells["colStatus"].Value = item.Note;

                // Màu sắc hàng theo trạng thái
                if (item.Status == SheetUpdateStatus.Changed)
                {
                    row.Cells["colStatus"].Style.ForeColor = Color.FromArgb(39, 174, 96);
                    row.Cells["colStatus"].Style.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                    row.Cells["colNewName"].Style.BackColor = Color.FromArgb(234, 250, 234);
                }
                else if (item.Status == SheetUpdateStatus.NotFound)
                {
                    row.Cells["colStatus"].Style.ForeColor = Color.FromArgb(192, 57, 43);
                    row.Cells["colStatus"].Style.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
                    row.DefaultCellStyle.ForeColor = Color.FromArgb(160, 160, 160);
                }
                else
                {
                    row.Cells["colStatus"].Style.ForeColor = Color.FromArgb(127, 140, 141);
                }
            }

            _isUpdatingCheck = false;
            UpdateSummaryLabel();
        }

        private void UpdateSummaryLabel()
        {
            int changedCount = _allItems.Count(delegate(SheetUpdateItem x) { return x.Status == SheetUpdateStatus.Changed; });
            int selectedCount = _allItems.Count(delegate(SheetUpdateItem x) { return x.IsSelected && x.Status == SheetUpdateStatus.Changed; });
            int notFoundCount = _allItems.Count(delegate(SheetUpdateItem x) { return x.Status == SheetUpdateStatus.NotFound; });
            int unchangedCount = _allItems.Count(delegate(SheetUpdateItem x) { return x.Status == SheetUpdateStatus.Unchanged; });

            lblSummary.Text = string.Format("📊 Thống kê: {0} bản vẽ có thay đổi ({1} được chọn cập nhật) | {2} giữ nguyên | {3} không tìm thấy",
                changedCount, selectedCount, unchangedCount, notFoundCount);

            btnApply.Enabled = (selectedCount > 0);
            if (selectedCount > 0)
            {
                btnApply.Text = string.Format("🚀  CẬP NHẬT {0} BẢN VẼ VÀO REVIT (ÁP DỤNG NGAY)", selectedCount);
                btnApply.BackColor = Color.FromArgb(211, 84, 0);
            }
            else
            {
                btnApply.Text = "🚀  CẬP NHẬT VÀO REVIT (Vui lòng chọn ít nhất 1 bản vẽ)";
                btnApply.BackColor = Color.FromArgb(180, 180, 180);
            }
        }

        private void DgvPreview_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (_isUpdatingCheck || e.RowIndex < 0 || e.ColumnIndex != dgvPreview.Columns["colCheck"].Index) return;

            dgvPreview.CommitEdit(DataGridViewDataErrorContexts.Commit);
            DataGridViewRow row = dgvPreview.Rows[e.RowIndex];
            SheetUpdateItem item = row.Tag as SheetUpdateItem;
            if (item != null)
            {
                bool isChecked = Convert.ToBoolean(row.Cells["colCheck"].Value);
                item.IsSelected = isChecked;
                UpdateSummaryLabel();

                _lastCheckedRowIndex = e.RowIndex;
                _lastTargetCheckState = isChecked;
            }
        }

        private void DgvPreview_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0) return;

            // Xử lý chuột phải: giữ hoặc chọn dòng để mở Context Menu
            if (e.Button == MouseButtons.Right)
            {
                if (!dgvPreview.Rows[e.RowIndex].Selected)
                {
                    dgvPreview.ClearSelection();
                    dgvPreview.Rows[e.RowIndex].Selected = true;
                    if (e.ColumnIndex >= 0)
                    {
                        try
                        {
                            dgvPreview.CurrentCell = dgvPreview.Rows[e.RowIndex].Cells[e.ColumnIndex];
                        }
                        catch { }
                    }
                }
                return;
            }

            // Xử lý chuột trái
            if (e.Button == MouseButtons.Left)
            {
                if (e.ColumnIndex == colCheck.Index)
                {
                    if ((System.Windows.Forms.Control.ModifierKeys & Keys.Shift) == Keys.Shift)
                    {
                        HandleShiftClickCheck(e.RowIndex);
                    }
                    else
                    {
                        _lastCheckedRowIndex = e.RowIndex;
                        _lastTargetCheckState = Convert.ToBoolean(dgvPreview.Rows[e.RowIndex].Cells[colCheck.Index].Value);
                    }
                }
            }
        }

        private void HandleShiftClickCheck(int targetRowIndex)
        {
            if (targetRowIndex < 0 || targetRowIndex >= dgvPreview.Rows.Count) return;

            int fromIndex = _lastCheckedRowIndex;
            if (fromIndex < 0 || fromIndex >= dgvPreview.Rows.Count)
            {
                fromIndex = (dgvPreview.CurrentRow != null && dgvPreview.CurrentRow.Index >= 0)
                    ? dgvPreview.CurrentRow.Index
                    : 0;
            }

            int start = Math.Min(fromIndex, targetRowIndex);
            int end = Math.Max(fromIndex, targetRowIndex);

            bool targetState;
            if (_lastCheckedRowIndex >= 0 && _lastCheckedRowIndex < dgvPreview.Rows.Count)
            {
                targetState = _lastTargetCheckState;

                // Nếu tất cả các dòng trong dải đã có cùng trạng thái targetState rồi, người dùng bấm tiếp để ĐẢO (toggle)
                bool allAlreadySame = true;
                for (int i = start; i <= end; i++)
                {
                    if (Convert.ToBoolean(dgvPreview.Rows[i].Cells[colCheck.Index].Value) != targetState)
                    {
                        allAlreadySame = false;
                        break;
                    }
                }
                if (allAlreadySame)
                {
                    targetState = !targetState;
                    _lastTargetCheckState = targetState;
                }
            }
            else
            {
                bool curVal = Convert.ToBoolean(dgvPreview.Rows[targetRowIndex].Cells[colCheck.Index].Value);
                targetState = !curVal;
                _lastTargetCheckState = targetState;
            }

            SetRangeChecked(start, end, targetState);
            _lastCheckedRowIndex = fromIndex;

            this.BeginInvoke((MethodInvoker)delegate
            {
                SetRangeChecked(start, end, targetState);
                _lastCheckedRowIndex = fromIndex;
            });
        }

        private void SetRangeChecked(int startRow, int endRow, bool checkState)
        {
            if (startRow < 0) startRow = 0;
            if (endRow >= dgvPreview.Rows.Count) endRow = dgvPreview.Rows.Count - 1;
            if (startRow > endRow)
            {
                int temp = startRow;
                startRow = endRow;
                endRow = temp;
            }

            _isUpdatingCheck = true;
            try
            {
                for (int i = startRow; i <= endRow; i++)
                {
                    DataGridViewRow row = dgvPreview.Rows[i];
                    row.Cells[colCheck.Index].Value = checkState;
                    SheetUpdateItem item = row.Tag as SheetUpdateItem;
                    if (item != null)
                    {
                        item.IsSelected = checkState;
                    }
                }
            }
            finally
            {
                _isUpdatingCheck = false;
            }

            UpdateSummaryLabel();
            dgvPreview.InvalidateColumn(colCheck.Index);
        }

        private void DgvPreview_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space && !dgvPreview.IsCurrentCellInEditMode)
            {
                if (dgvPreview.SelectedRows.Count > 0)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;

                    bool anyUnchecked = false;
                    foreach (DataGridViewRow row in dgvPreview.SelectedRows)
                    {
                        if (!Convert.ToBoolean(row.Cells[colCheck.Index].Value))
                        {
                            anyUnchecked = true;
                            break;
                        }
                    }

                    bool newState = anyUnchecked;
                    SetSelectedRowsCheckState(newState);
                }
            }
        }

        private void SetSelectedRowsCheckState(bool checkState)
        {
            if (dgvPreview.SelectedRows.Count == 0) return;

            _isUpdatingCheck = true;
            try
            {
                foreach (DataGridViewRow row in dgvPreview.SelectedRows)
                {
                    row.Cells[colCheck.Index].Value = checkState;
                    SheetUpdateItem item = row.Tag as SheetUpdateItem;
                    if (item != null)
                    {
                        item.IsSelected = checkState;
                    }
                }
            }
            finally
            {
                _isUpdatingCheck = false;
            }

            UpdateSummaryLabel();
            dgvPreview.InvalidateColumn(colCheck.Index);
        }

        private void BtnSelectChanges_Click(object sender, EventArgs e)
        {
            foreach (SheetUpdateItem item in _allItems)
            {
                if (item.Status == SheetUpdateStatus.Changed)
                {
                    item.IsSelected = true;
                }
            }
            RefreshGrid();
        }

        private void SetSelectionState(bool selected)
        {
            foreach (SheetUpdateItem item in _allItems)
            {
                item.IsSelected = selected;
            }
            RefreshGrid();
        }

        private void BtnApply_Click(object sender, EventArgs e)
        {
            int selectedCount = _allItems.Count(delegate(SheetUpdateItem x) { return x.IsSelected && x.Status == SheetUpdateStatus.Changed; });
            if (selectedCount == 0)
            {
                MessageBox.Show("Vui lòng tích chọn ít nhất 1 bản vẽ có thay đổi để cập nhật vào Revit.",
                    "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult confirm = MessageBox.Show(
                string.Format("Bạn có chắc chắn muốn cập nhật tên và thông tin cho {0} bản vẽ đã chọn trong Revit không?", selectedCount),
                "Xác Nhận Cập Nhật",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            HDToolModelessHost.Run(uiapp =>
            {
                try
                {
                    Document targetDoc = (uiapp != null && uiapp.ActiveUIDocument != null) ? uiapp.ActiveUIDocument.Document : _doc;
                    string report;
                    int count = UpdateSheetListService.ApplyUpdatesToDocument(targetDoc, _allItems, out report);
                    this.UpdatedCount = count;

                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        if (count > 0)
                        {
                            MessageBox.Show(
                                "CẬP NHẬT THÀNH CÔNG!\n\n" + report,
                                "HDTOOL - Cập Nhật Bản Vẽ Thành Công",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);

                            if (UpdatesApplied != null) UpdatesApplied(count);
                            this.DialogResult = DialogResult.OK;
                            this.Close();
                        }
                        else
                        {
                            MessageBox.Show(
                                "Không có bản vẽ nào được cập nhật.\n" + report,
                                "Thông Báo",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                        }
                    });
                }
                catch (Exception ex)
                {
                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        MessageBox.Show("Lỗi cập nhật: " + ex.Message, "HDTOOL Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    });
                }
            });
        }
    }
}
