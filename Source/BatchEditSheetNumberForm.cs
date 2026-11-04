using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Color = System.Drawing.Color;
using Font = System.Drawing.Font;
using Point = System.Drawing.Point;

namespace HDTool
{
    /// <summary>
    /// Hộp thoại Sửa & Thay Thế Số Hiệu Hàng Loạt Với Các Tiền Tố Giống Nhau
    /// Hỗ trợ: Tìm & Thay thế Tiền tố, Thêm Tiền tố/Hậu tố, hoặc Đánh số thứ tự tự động theo Tiền tố
    /// với bảng Live Preview thời gian thực, tự động quét TẤT CẢ bản vẽ có cùng tiền tố trong toàn bộ dự án
    /// và kiểm tra trùng lặp an toàn 100%.
    /// </summary>
    public class BatchEditSheetNumberForm : Form
    {
        private List<SheetItem> _allSheets;
        private List<SheetItem> _selectedSheets;
        private List<SheetItem> _activeTargetSheets;
        private string _detectedCommonPrefix = string.Empty;

        // UI Controls
        private System.Windows.Forms.Panel pnlHeader;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblSubtitle;

        private System.Windows.Forms.GroupBox grpMode;
        private System.Windows.Forms.RadioButton rdoModeReplace;
        private System.Windows.Forms.RadioButton rdoModeAdd;
        private System.Windows.Forms.RadioButton rdoModeNumber;

        // Mode 1: Replace Prefix / Text
        private System.Windows.Forms.Panel pnlModeReplace;
        private System.Windows.Forms.Label lblFind;
        private System.Windows.Forms.TextBox txtFind;
        private System.Windows.Forms.Label lblReplace;
        private System.Windows.Forms.TextBox txtReplace;
        private System.Windows.Forms.CheckBox chkOnlyPrefix;
        private System.Windows.Forms.CheckBox chkMatchCase;

        // Mode 2: Add Prefix / Suffix
        private System.Windows.Forms.Panel pnlModeAdd;
        private System.Windows.Forms.Label lblAddPrefix;
        private System.Windows.Forms.TextBox txtAddPrefix;
        private System.Windows.Forms.Label lblAddSuffix;
        private System.Windows.Forms.TextBox txtAddSuffix;

        // Mode 3: Auto Number with Prefix
        private System.Windows.Forms.Panel pnlModeNumber;
        private System.Windows.Forms.Label lblNumPrefix;
        private System.Windows.Forms.TextBox txtNumPrefix;
        private System.Windows.Forms.Label lblNumStart;
        private System.Windows.Forms.NumericUpDown numStart;
        private System.Windows.Forms.Label lblNumFormat;
        private System.Windows.Forms.ComboBox cmbNumFormat;
        private System.Windows.Forms.Label lblNumStep;
        private System.Windows.Forms.NumericUpDown numStep;
        private System.Windows.Forms.Label lblNumSuffix;
        private System.Windows.Forms.TextBox txtNumSuffix;

        // Scope Selection
        private System.Windows.Forms.GroupBox grpScope;
        private System.Windows.Forms.RadioButton rdoScopeMatched;
        private System.Windows.Forms.RadioButton rdoScopeSelected;
        private System.Windows.Forms.RadioButton rdoScopeAll;

        // Live Preview Grid
        private System.Windows.Forms.GroupBox grpPreview;
        private DataGridView dgvPreview;
        private DataGridViewTextBoxColumn colPvStt;
        private DataGridViewTextBoxColumn colPvName;
        private DataGridViewTextBoxColumn colPvOld;
        private DataGridViewTextBoxColumn colPvNew;
        private DataGridViewTextBoxColumn colPvStatus;
        private System.Windows.Forms.Label lblPreviewStats;

        // Bottom Actions
        private System.Windows.Forms.Panel pnlBottom;
        private System.Windows.Forms.Button btnApply;
        private System.Windows.Forms.Button btnCancel;

        // Output Result: Item -> New SheetNumber
        public Dictionary<SheetItem, string> ModifiedNumbers { get; private set; }

        public BatchEditSheetNumberForm(List<SheetItem> allSheets, List<SheetItem> selectedSheets)
        {
            _allSheets = allSheets ?? new List<SheetItem>();
            _selectedSheets = selectedSheets ?? new List<SheetItem>();
            if (_selectedSheets.Count == 0)
            {
                _selectedSheets = new List<SheetItem>(_allSheets);
            }
            _activeTargetSheets = new List<SheetItem>();
            ModifiedNumbers = new Dictionary<SheetItem, string>();

            InitializeComponent();
            AutoDetectAndSetCommonPrefix();
            UpdateLivePreview();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.Text = "HDTOOL - Sửa & Thay Thế Số Hiệu Bản Vẽ Hàng Loạt";
            this.Size = new Size(960, 700);
            this.MinimumSize = new Size(900, 620);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(245, 247, 250);
            this.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

            // 1. Header Panel
            pnlHeader = new System.Windows.Forms.Panel();
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = 65;
            pnlHeader.BackColor = Color.FromArgb(24, 43, 73); // Dark Navy

            lblTitle = new System.Windows.Forms.Label();
            lblTitle.Text = "✏  SỬA & THAY THẾ SỐ HIỆU BẢN VẼ HÀNG LOẠT (BATCH EDIT PREFIX)";
            lblTitle.ForeColor = Color.White;
            lblTitle.Font = new Font("Segoe UI", 11.0f, FontStyle.Bold);
            lblTitle.Location = new Point(16, 12);
            lblTitle.AutoSize = true;

            lblSubtitle = new System.Windows.Forms.Label();
            lblSubtitle.Text = "Tự động nhận diện tiền tố chung, tìm & thay thế trên TẤT CẢ bản vẽ cùng tiền tố trong toàn bộ dự án";
            lblSubtitle.ForeColor = Color.FromArgb(189, 195, 199);
            lblSubtitle.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblSubtitle.Location = new Point(16, 38);
            lblSubtitle.AutoSize = true;

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);

            // 2. Mode Selector Group
            grpMode = new System.Windows.Forms.GroupBox();
            grpMode.Text = "Chọn Chế Độ Thay Đổi";
            grpMode.Location = new Point(14, 75);
            grpMode.Size = new Size(430, 54);
            grpMode.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            grpMode.ForeColor = Color.FromArgb(24, 43, 73);

            rdoModeReplace = new System.Windows.Forms.RadioButton();
            rdoModeReplace.Text = "1. Thay Thế Tiền Tố";
            rdoModeReplace.Location = new Point(10, 20);
            rdoModeReplace.Size = new Size(145, 22);
            rdoModeReplace.Checked = true;
            rdoModeReplace.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            rdoModeReplace.CheckedChanged += Mode_CheckedChanged;

            rdoModeAdd = new System.Windows.Forms.RadioButton();
            rdoModeAdd.Text = "2. Thêm Tiền Tố/Hậu Tố";
            rdoModeAdd.Location = new Point(160, 20);
            rdoModeAdd.Size = new Size(150, 22);
            rdoModeAdd.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            rdoModeAdd.CheckedChanged += Mode_CheckedChanged;

            rdoModeNumber = new System.Windows.Forms.RadioButton();
            rdoModeNumber.Text = "3. Đánh Số TĐ";
            rdoModeNumber.Location = new Point(315, 20);
            rdoModeNumber.Size = new Size(110, 22);
            rdoModeNumber.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            rdoModeNumber.CheckedChanged += Mode_CheckedChanged;

            grpMode.Controls.Add(rdoModeReplace);
            grpMode.Controls.Add(rdoModeAdd);
            grpMode.Controls.Add(rdoModeNumber);

            // Scope Selector Group: 3 options, rdoScopeMatched is DEFAULT
            grpScope = new System.Windows.Forms.GroupBox();
            grpScope.Text = "Phạm Vi Áp Dụng";
            grpScope.Location = new Point(452, 75);
            grpScope.Size = new Size(490, 54);
            grpScope.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            grpScope.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            grpScope.ForeColor = Color.FromArgb(24, 43, 73);

            rdoScopeMatched = new System.Windows.Forms.RadioButton();
            rdoScopeMatched.Text = "⭐ Cùng tiền tố (0 bản vẽ)";
            rdoScopeMatched.Location = new Point(10, 20);
            rdoScopeMatched.Size = new Size(225, 22);
            rdoScopeMatched.Checked = true;
            rdoScopeMatched.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            rdoScopeMatched.ForeColor = Color.FromArgb(24, 106, 59); // Green
            rdoScopeMatched.CheckedChanged += Scope_CheckedChanged;

            rdoScopeSelected = new System.Windows.Forms.RadioButton();
            rdoScopeSelected.Text = string.Format("Đang chọn ({0})", _selectedSheets.Count);
            rdoScopeSelected.Location = new Point(240, 20);
            rdoScopeSelected.Size = new Size(135, 22);
            rdoScopeSelected.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            rdoScopeSelected.CheckedChanged += Scope_CheckedChanged;

            rdoScopeAll = new System.Windows.Forms.RadioButton();
            rdoScopeAll.Text = string.Format("Tất cả ({0})", _allSheets.Count);
            rdoScopeAll.Location = new Point(380, 20);
            rdoScopeAll.Size = new Size(100, 22);
            rdoScopeAll.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            rdoScopeAll.CheckedChanged += Scope_CheckedChanged;

            grpScope.Controls.Add(rdoScopeMatched);
            grpScope.Controls.Add(rdoScopeSelected);
            grpScope.Controls.Add(rdoScopeAll);

            // Container for Mode Inputs (Height: 76)
            // Mode 1: Replace inputs
            pnlModeReplace = new System.Windows.Forms.Panel();
            pnlModeReplace.Location = new Point(14, 134);
            pnlModeReplace.Size = new Size(928, 76);
            pnlModeReplace.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            pnlModeReplace.BackColor = Color.FromArgb(238, 242, 246);
            pnlModeReplace.BorderStyle = BorderStyle.FixedSingle;

            lblFind = new System.Windows.Forms.Label();
            lblFind.Text = "Tiền tố cũ / Chuỗi tìm kiếm:";
            lblFind.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            lblFind.ForeColor = Color.FromArgb(44, 62, 80);
            lblFind.Location = new Point(12, 12);
            lblFind.AutoSize = true;

            txtFind = new System.Windows.Forms.TextBox();
            txtFind.Location = new Point(180, 9);
            txtFind.Size = new Size(240, 23);
            txtFind.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            txtFind.TextChanged += (s, e) => UpdateLivePreview();
            txtFind.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    txtReplace.Focus();
                    txtReplace.SelectAll();
                }
            };

            lblReplace = new System.Windows.Forms.Label();
            lblReplace.Text = "Thay thế bằng (Tiền tố mới):";
            lblReplace.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            lblReplace.ForeColor = Color.FromArgb(24, 106, 59);
            lblReplace.Location = new Point(435, 12);
            lblReplace.AutoSize = true;

            txtReplace = new System.Windows.Forms.TextBox();
            txtReplace.Location = new Point(605, 9);
            txtReplace.Size = new Size(310, 23);
            txtReplace.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtReplace.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            txtReplace.ForeColor = Color.FromArgb(24, 106, 59);
            txtReplace.TextChanged += (s, e) => UpdateLivePreview();
            txtReplace.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    if (btnApply.Enabled)
                    {
                        btnApply.PerformClick();
                    }
                }
            };

            chkOnlyPrefix = new System.Windows.Forms.CheckBox();
            chkOnlyPrefix.Text = "Chỉ thay thế nếu nằm ở ĐẦU số hiệu (Chuẩn Tiền tố Prefix)";
            chkOnlyPrefix.Location = new Point(180, 42);
            chkOnlyPrefix.Size = new Size(390, 22);
            chkOnlyPrefix.Checked = true;
            chkOnlyPrefix.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            chkOnlyPrefix.CheckedChanged += (s, e) => UpdateLivePreview();

            chkMatchCase = new System.Windows.Forms.CheckBox();
            chkMatchCase.Text = "Phân biệt chữ hoa / thường";
            chkMatchCase.Location = new Point(580, 42);
            chkMatchCase.Size = new Size(220, 22);
            chkMatchCase.Checked = false;
            chkMatchCase.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            chkMatchCase.CheckedChanged += (s, e) => UpdateLivePreview();

            pnlModeReplace.Controls.Add(lblFind);
            pnlModeReplace.Controls.Add(txtFind);
            pnlModeReplace.Controls.Add(lblReplace);
            pnlModeReplace.Controls.Add(txtReplace);
            pnlModeReplace.Controls.Add(chkOnlyPrefix);
            pnlModeReplace.Controls.Add(chkMatchCase);

            // Mode 2: Add inputs
            pnlModeAdd = new System.Windows.Forms.Panel();
            pnlModeAdd.Location = new Point(14, 134);
            pnlModeAdd.Size = new Size(928, 76);
            pnlModeAdd.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            pnlModeAdd.BackColor = Color.FromArgb(238, 242, 246);
            pnlModeAdd.BorderStyle = BorderStyle.FixedSingle;
            pnlModeAdd.Visible = false;

            lblAddPrefix = new System.Windows.Forms.Label();
            lblAddPrefix.Text = "Thêm Tiền tố vào trước (Prefix):";
            lblAddPrefix.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            lblAddPrefix.ForeColor = Color.FromArgb(41, 128, 185);
            lblAddPrefix.Location = new Point(12, 18);
            lblAddPrefix.AutoSize = true;

            txtAddPrefix = new System.Windows.Forms.TextBox();
            txtAddPrefix.Location = new Point(205, 15);
            txtAddPrefix.Size = new Size(210, 23);
            txtAddPrefix.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            txtAddPrefix.TextChanged += (s, e) => UpdateLivePreview();

            lblAddSuffix = new System.Windows.Forms.Label();
            lblAddSuffix.Text = "Thêm Hậu tố vào sau (Suffix):";
            lblAddSuffix.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            lblAddSuffix.ForeColor = Color.FromArgb(142, 68, 173);
            lblAddSuffix.Location = new Point(430, 18);
            lblAddSuffix.AutoSize = true;

            txtAddSuffix = new System.Windows.Forms.TextBox();
            txtAddSuffix.Location = new Point(600, 15);
            txtAddSuffix.Size = new Size(210, 23);
            txtAddSuffix.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            txtAddSuffix.TextChanged += (s, e) => UpdateLivePreview();

            System.Windows.Forms.Label lblAddHint = new System.Windows.Forms.Label();
            lblAddHint.Text = "💡 Ví dụ: Thêm tiền tố \"PCCC-\" hoặc thêm hậu tố \"-REV1\" vào tất cả các số hiệu đang chọn.";
            lblAddHint.Location = new Point(12, 48);
            lblAddHint.AutoSize = true;
            lblAddHint.ForeColor = Color.FromArgb(100, 110, 120);
            lblAddHint.Font = new Font("Segoe UI", 8.25f, FontStyle.Italic);

            pnlModeAdd.Controls.Add(lblAddPrefix);
            pnlModeAdd.Controls.Add(txtAddPrefix);
            pnlModeAdd.Controls.Add(lblAddSuffix);
            pnlModeAdd.Controls.Add(txtAddSuffix);
            pnlModeAdd.Controls.Add(lblAddHint);

            // Mode 3: Numbering inputs
            pnlModeNumber = new System.Windows.Forms.Panel();
            pnlModeNumber.Location = new Point(14, 134);
            pnlModeNumber.Size = new Size(928, 76);
            pnlModeNumber.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            pnlModeNumber.BackColor = Color.FromArgb(238, 242, 246);
            pnlModeNumber.BorderStyle = BorderStyle.FixedSingle;
            pnlModeNumber.Visible = false;

            lblNumPrefix = new System.Windows.Forms.Label();
            lblNumPrefix.Text = "Tiền tố:";
            lblNumPrefix.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            lblNumPrefix.Location = new Point(10, 15);
            lblNumPrefix.AutoSize = true;

            txtNumPrefix = new System.Windows.Forms.TextBox();
            txtNumPrefix.Location = new Point(65, 12);
            txtNumPrefix.Size = new Size(190, 23);
            txtNumPrefix.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            txtNumPrefix.TextChanged += (s, e) => UpdateLivePreview();

            lblNumStart = new System.Windows.Forms.Label();
            lblNumStart.Text = "Bắt đầu:";
            lblNumStart.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            lblNumStart.Location = new Point(265, 15);
            lblNumStart.AutoSize = true;

            numStart = new System.Windows.Forms.NumericUpDown();
            numStart.Location = new Point(320, 12);
            numStart.Size = new Size(60, 23);
            numStart.Minimum = 0;
            numStart.Maximum = 99999;
            numStart.Value = 1;
            numStart.ValueChanged += (s, e) => UpdateLivePreview();

            lblNumFormat = new System.Windows.Forms.Label();
            lblNumFormat.Text = "Định dạng:";
            lblNumFormat.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            lblNumFormat.Location = new Point(390, 15);
            lblNumFormat.AutoSize = true;

            cmbNumFormat = new System.Windows.Forms.ComboBox();
            cmbNumFormat.Location = new Point(460, 12);
            cmbNumFormat.Size = new Size(110, 23);
            cmbNumFormat.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbNumFormat.Items.AddRange(new object[] { "01, 02...", "001, 002...", "1, 2, 3...", "0001, 0002..." });
            cmbNumFormat.SelectedIndex = 0;
            cmbNumFormat.SelectedIndexChanged += (s, e) => UpdateLivePreview();

            lblNumStep = new System.Windows.Forms.Label();
            lblNumStep.Text = "Bước nhảy:";
            lblNumStep.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            lblNumStep.Location = new Point(580, 15);
            lblNumStep.AutoSize = true;

            numStep = new System.Windows.Forms.NumericUpDown();
            numStep.Location = new Point(650, 12);
            numStep.Size = new Size(45, 23);
            numStep.Minimum = 1;
            numStep.Maximum = 100;
            numStep.Value = 1;
            numStep.ValueChanged += (s, e) => UpdateLivePreview();

            lblNumSuffix = new System.Windows.Forms.Label();
            lblNumSuffix.Text = "Hậu tố:";
            lblNumSuffix.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            lblNumSuffix.Location = new Point(705, 15);
            lblNumSuffix.AutoSize = true;

            txtNumSuffix = new System.Windows.Forms.TextBox();
            txtNumSuffix.Location = new Point(755, 12);
            txtNumSuffix.Size = new Size(120, 23);
            txtNumSuffix.TextChanged += (s, e) => UpdateLivePreview();

            System.Windows.Forms.Label lblNumHint = new System.Windows.Forms.Label();
            lblNumHint.Text = "💡 Ví dụ: Tiền tố \"HDM/CB/B1/T1.\" ➔ Đánh số tự động: HDM/CB/B1/T1.01, HDM/CB/B1/T1.02...";
            lblNumHint.Location = new Point(10, 48);
            lblNumHint.AutoSize = true;
            lblNumHint.ForeColor = Color.FromArgb(100, 110, 120);
            lblNumHint.Font = new Font("Segoe UI", 8.25f, FontStyle.Italic);

            pnlModeNumber.Controls.Add(lblNumPrefix);
            pnlModeNumber.Controls.Add(txtNumPrefix);
            pnlModeNumber.Controls.Add(lblNumStart);
            pnlModeNumber.Controls.Add(numStart);
            pnlModeNumber.Controls.Add(lblNumFormat);
            pnlModeNumber.Controls.Add(cmbNumFormat);
            pnlModeNumber.Controls.Add(lblNumStep);
            pnlModeNumber.Controls.Add(numStep);
            pnlModeNumber.Controls.Add(lblNumSuffix);
            pnlModeNumber.Controls.Add(txtNumSuffix);
            pnlModeNumber.Controls.Add(lblNumHint);

            // 3. Live Preview GroupBox & DataGridView
            grpPreview = new System.Windows.Forms.GroupBox();
            grpPreview.Text = "Xem Trước Thay Đổi Thời Gian Thực (Live Preview)";
            grpPreview.Location = new Point(14, 218);
            grpPreview.Size = new Size(928, 415);
            grpPreview.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            grpPreview.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            grpPreview.ForeColor = Color.FromArgb(24, 43, 73);

            dgvPreview = new DataGridView();
            dgvPreview.Dock = DockStyle.Fill;
            dgvPreview.BackgroundColor = Color.White;
            dgvPreview.BorderStyle = BorderStyle.None;
            dgvPreview.RowHeadersVisible = false;
            dgvPreview.AllowUserToAddRows = false;
            dgvPreview.AllowUserToDeleteRows = false;
            dgvPreview.AllowUserToResizeRows = false;
            dgvPreview.ReadOnly = true;
            dgvPreview.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvPreview.RowTemplate.Height = 26;
            dgvPreview.ColumnHeadersHeight = 28;
            dgvPreview.EnableHeadersVisualStyles = false;
            dgvPreview.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(235, 240, 245);
            dgvPreview.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(44, 62, 80);
            dgvPreview.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);

            colPvStt = new DataGridViewTextBoxColumn();
            colPvStt.HeaderText = "STT";
            colPvStt.Width = 45;
            colPvStt.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvPreview.Columns.Add(colPvStt);

            colPvName = new DataGridViewTextBoxColumn();
            colPvName.HeaderText = "Tên Bản Vẽ (Sheet Name)";
            colPvName.Width = 280;
            colPvName.DefaultCellStyle.ForeColor = Color.FromArgb(80, 80, 80);
            dgvPreview.Columns.Add(colPvName);

            colPvOld = new DataGridViewTextBoxColumn();
            colPvOld.HeaderText = "Số Hiệu Hiện Tại (Cũ)";
            colPvOld.Width = 230;
            colPvOld.DefaultCellStyle.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
            colPvOld.DefaultCellStyle.ForeColor = Color.FromArgb(100, 100, 100);
            dgvPreview.Columns.Add(colPvOld);

            colPvNew = new DataGridViewTextBoxColumn();
            colPvNew.HeaderText = "Số Hiệu Mới (Xem Trước) ➔";
            colPvNew.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colPvNew.DefaultCellStyle.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            dgvPreview.Columns.Add(colPvNew);

            colPvStatus = new DataGridViewTextBoxColumn();
            colPvStatus.HeaderText = "Trạng Thái";
            colPvStatus.Width = 130;
            colPvStatus.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvPreview.Columns.Add(colPvStatus);

            lblPreviewStats = new System.Windows.Forms.Label();
            lblPreviewStats.Dock = DockStyle.Bottom;
            lblPreviewStats.Height = 26;
            lblPreviewStats.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            lblPreviewStats.ForeColor = Color.FromArgb(41, 128, 185);
            lblPreviewStats.Text = "Thống kê: Đang tính toán...";
            lblPreviewStats.TextAlign = ContentAlignment.MiddleLeft;
            lblPreviewStats.Padding = new Padding(8, 0, 0, 0);

            grpPreview.Controls.Add(dgvPreview);
            grpPreview.Controls.Add(lblPreviewStats);

            // 4. Bottom Action Panel
            pnlBottom = new System.Windows.Forms.Panel();
            pnlBottom.Dock = DockStyle.Bottom;
            pnlBottom.Height = 58;
            pnlBottom.BackColor = Color.FromArgb(240, 243, 246);

            btnApply = new System.Windows.Forms.Button();
            btnApply.Text = "✔  ÁP DỤNG THAY ĐỔI";
            btnApply.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            btnApply.BackColor = Color.FromArgb(39, 174, 96); // Green
            btnApply.ForeColor = Color.White;
            btnApply.FlatStyle = FlatStyle.Flat;
            btnApply.FlatAppearance.BorderSize = 0;
            btnApply.Cursor = Cursors.Hand;
            btnApply.Size = new Size(320, 40);
            btnApply.Click += BtnApply_Click;

            btnCancel = new System.Windows.Forms.Button();
            btnCancel.Text = "❌  HỦY BỎ";
            btnCancel.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
            btnCancel.BackColor = Color.FromArgb(149, 165, 166);
            btnCancel.ForeColor = Color.White;
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Cursor = Cursors.Hand;
            btnCancel.Size = new Size(110, 40);
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
            this.CancelButton = btnCancel;

            pnlBottom.Resize += (s, e) =>
            {
                btnApply.Left = Math.Max(10, (pnlBottom.Width - btnApply.Width - btnCancel.Width - 15) / 2);
                btnApply.Top = 9;
                btnCancel.Left = btnApply.Right + 15;
                btnCancel.Top = 9;
            };

            pnlBottom.Controls.Add(btnApply);
            pnlBottom.Controls.Add(btnCancel);

            // Attach Controls to Form
            this.Controls.Add(grpPreview);
            this.Controls.Add(pnlModeReplace);
            this.Controls.Add(pnlModeAdd);
            this.Controls.Add(pnlModeNumber);
            this.Controls.Add(grpScope);
            this.Controls.Add(grpMode);
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

        private void AutoDetectAndSetCommonPrefix()
        {
            List<string> numbers = null;
            if (_selectedSheets != null && _selectedSheets.Count > 0)
            {
                numbers = _selectedSheets
                    .Select(delegate(SheetItem s) { return s.SheetNumber ?? string.Empty; })
                    .Where(delegate(string s) { return !string.IsNullOrEmpty(s); })
                    .ToList();
            }
            else if (_allSheets != null && _allSheets.Count > 0)
            {
                numbers = _allSheets
                    .Select(delegate(SheetItem s) { return s.SheetNumber ?? string.Empty; })
                    .Where(delegate(string s) { return !string.IsNullOrEmpty(s); })
                    .ToList();
            }

            _detectedCommonPrefix = FindLongestCommonPrefix(numbers);
            if (!string.IsNullOrEmpty(_detectedCommonPrefix))
            {
                txtFind.Text = _detectedCommonPrefix;
                txtReplace.Text = _detectedCommonPrefix;
                txtNumPrefix.Text = _detectedCommonPrefix;
            }
        }

        public static string FindLongestCommonPrefix(List<string> strings)
        {
            if (strings == null || strings.Count == 0) return string.Empty;
            if (strings.Count == 1)
            {
                string s = strings[0];
                int lastDelim = s.LastIndexOfAny(new char[] { '/', '-', '_', '.' });
                if (lastDelim >= 0) return s.Substring(0, lastDelim + 1);
                return CleanPrefix(s);
            }

            string first = strings[0];
            int minLen = first.Length;
            for (int i = 1; i < strings.Count; i++)
            {
                if (strings[i].Length < minLen) minLen = strings[i].Length;
            }

            int prefixLen = 0;
            for (int i = 0; i < minLen; i++)
            {
                char c = first[i];
                bool allMatch = true;
                for (int j = 1; j < strings.Count; j++)
                {
                    if (strings[j][i] != c)
                    {
                        allMatch = false;
                        break;
                    }
                }
                if (!allMatch) break;
                prefixLen++;
            }

            if (prefixLen == 0) return string.Empty;
            string rawPrefix = first.Substring(0, prefixLen);
            return CleanPrefix(rawPrefix);
        }

        public static string CleanPrefix(string prefix)
        {
            if (string.IsNullOrEmpty(prefix)) return string.Empty;

            // 1. Nếu có ký tự phân cách chuẩn (/, -, _, .):
            int lastDelim = prefix.LastIndexOfAny(new char[] { '/', '-', '_', '.' });
            if (lastDelim >= 0 && lastDelim < prefix.Length - 1)
            {
                bool allDigitsAfterDelim = true;
                for (int i = lastDelim + 1; i < prefix.Length; i++)
                {
                    if (!char.IsDigit(prefix[i]))
                    {
                        allDigitsAfterDelim = false;
                        break;
                    }
                }
                if (allDigitsAfterDelim)
                {
                    return prefix.Substring(0, lastDelim + 1);
                }
            }

            // 2. Nếu kết thúc bằng số thì cắt bỏ phần số cuối để lấy phần chữ tiền tố (ví dụ "A0" -> "A")
            string trimmed = prefix.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
            if (!string.IsNullOrEmpty(trimmed))
            {
                return trimmed;
            }

            return prefix;
        }

        private void Mode_CheckedChanged(object sender, EventArgs e)
        {
            pnlModeReplace.Visible = rdoModeReplace.Checked;
            pnlModeAdd.Visible = rdoModeAdd.Checked;
            pnlModeNumber.Visible = rdoModeNumber.Checked;

            UpdateLivePreview();
        }

        private void Scope_CheckedChanged(object sender, EventArgs e)
        {
            UpdateLivePreview();
        }

        private List<SheetItem> GetMatchedSheets()
        {
            if (_allSheets == null || _allSheets.Count == 0) return new List<SheetItem>();

            string pattern = string.Empty;
            if (rdoModeReplace.Checked)
            {
                pattern = txtFind != null ? txtFind.Text : string.Empty;
            }
            else if (rdoModeNumber.Checked)
            {
                pattern = txtNumPrefix != null ? txtNumPrefix.Text : string.Empty;
            }
            else // rdoModeAdd
            {
                pattern = _detectedCommonPrefix ?? string.Empty;
            }

            if (string.IsNullOrEmpty(pattern))
            {
                // Nếu chưa có tiền tố, tạm hiển thị danh sách bản vẽ đang chọn
                return new List<SheetItem>(_selectedSheets);
            }

            StringComparison comp = (chkMatchCase != null && chkMatchCase.Checked)
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

            bool onlyPrefix = (chkOnlyPrefix == null || chkOnlyPrefix.Checked);

            List<SheetItem> matched = new List<SheetItem>();
            foreach (SheetItem s in _allSheets)
            {
                string num = s.SheetNumber ?? string.Empty;
                if (onlyPrefix)
                {
                    if (num.StartsWith(pattern, comp))
                    {
                        matched.Add(s);
                    }
                }
                else
                {
                    if (num.IndexOf(pattern, comp) >= 0)
                    {
                        matched.Add(s);
                    }
                }
            }

            return matched;
        }

        private void UpdateLivePreview()
        {
            if (dgvPreview == null) return;

            List<SheetItem> matchedSheets = GetMatchedSheets();
            if (rdoScopeMatched != null)
            {
                rdoScopeMatched.Text = string.Format("⭐ Cùng tiền tố ({0} bản vẽ)", matchedSheets.Count);
            }

            if (rdoScopeMatched != null && rdoScopeMatched.Checked)
            {
                _activeTargetSheets = matchedSheets;
            }
            else if (rdoScopeSelected != null && rdoScopeSelected.Checked)
            {
                _activeTargetSheets = _selectedSheets;
            }
            else if (rdoScopeAll != null && rdoScopeAll.Checked)
            {
                _activeTargetSheets = _allSheets;
            }
            else
            {
                _activeTargetSheets = matchedSheets;
            }

            dgvPreview.Rows.Clear();

            if (_activeTargetSheets == null || _activeTargetSheets.Count == 0)
            {
                btnApply.Enabled = false;
                btnApply.Text = "✔  ÁP DỤNG THAY ĐỔI (0 BẢN VẼ)";
                btnApply.BackColor = Color.FromArgb(160, 170, 180);
                lblPreviewStats.ForeColor = Color.FromArgb(192, 57, 43);
                lblPreviewStats.Text = string.IsNullOrEmpty(txtFind.Text)
                    ? "Vui lòng nhập tiền tố cần tìm kiếm để quét tất cả bản vẽ cùng tiền tố trong toàn bộ dự án."
                    : string.Format("Không tìm thấy bản vẽ nào khớp với tiền tố \"{0}\" trong dự án.", txtFind.Text);
                return;
            }

            int changedCount = 0;
            int duplicateCount = 0;

            // Bộ nhớ kiểm tra trùng số hiệu
            HashSet<string> generatedNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Tập hợp các số hiệu hiện tại của các bản vẽ KHÔNG nằm trong danh sách đang sửa
            HashSet<string> existingExternalNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (SheetItem s in _allSheets)
            {
                if (!_activeTargetSheets.Contains(s) && !string.IsNullOrEmpty(s.SheetNumber))
                {
                    existingExternalNumbers.Add(s.SheetNumber);
                }
            }

            int stt = 1;
            int autoNum = (int)numStart.Value;
            int step = (int)numStep.Value;

            foreach (SheetItem item in _activeTargetSheets)
            {
                string oldNum = item.SheetNumber ?? string.Empty;
                string newNum = ComputeNewNumber(oldNum, autoNum);
                autoNum += step;

                bool isChanged = !string.Equals(oldNum, newNum, StringComparison.Ordinal);
                if (isChanged) changedCount++;

                string statusText = "Giữ nguyên";
                Color statusColor = Color.FromArgb(120, 120, 120);

                if (isChanged)
                {
                    statusText = "Đổi số hiệu";
                    statusColor = Color.FromArgb(24, 106, 59); // Green
                }

                if (string.IsNullOrEmpty(newNum))
                {
                    statusText = "⚠ Trống!";
                    statusColor = Color.Red;
                }
                else if (generatedNumbers.Contains(newNum) || existingExternalNumbers.Contains(newNum))
                {
                    statusText = "⚠ Trùng số hiệu!";
                    statusColor = Color.Red;
                    duplicateCount++;
                }
                else
                {
                    generatedNumbers.Add(newNum);
                }

                int rowIdx = dgvPreview.Rows.Add(
                    stt++,
                    item.SheetName,
                    oldNum,
                    newNum,
                    statusText
                );
                DataGridViewRow row = dgvPreview.Rows[rowIdx];
                row.Tag = new KeyValuePair<SheetItem, string>(item, newNum);

                if (isChanged)
                {
                    row.Cells[colPvNew.Index].Style.BackColor = Color.FromArgb(234, 250, 234);
                    row.Cells[colPvNew.Index].Style.ForeColor = Color.FromArgb(24, 106, 59);
                }

                row.Cells[colPvStatus.Index].Style.ForeColor = statusColor;
                row.Cells[colPvStatus.Index].Style.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            }

            // Cập nhật thống kê và nút bấm
            btnApply.Text = string.Format("✔  ÁP DỤNG THAY ĐỔI ({0} BẢN VẼ)", changedCount);

            if (duplicateCount > 0)
            {
                lblPreviewStats.ForeColor = Color.Red;
                lblPreviewStats.Text = string.Format("⚠ CẢNH BÁO: Phát hiện {0} số hiệu bị trùng lặp! Vui lòng điều chỉnh lại để tránh xung đột Revit.", duplicateCount);
                btnApply.Enabled = false;
                btnApply.BackColor = Color.FromArgb(160, 170, 180);
            }
            else
            {
                lblPreviewStats.ForeColor = Color.FromArgb(41, 128, 185);
                lblPreviewStats.Text = string.Format("Thống kê: Sẽ thay đổi {0} / {1} bản vẽ. Không có trùng lặp, an toàn 100%.", changedCount, _activeTargetSheets.Count);
                btnApply.Enabled = (changedCount > 0);
                btnApply.BackColor = (changedCount > 0) ? Color.FromArgb(39, 174, 96) : Color.FromArgb(160, 170, 180);
            }
        }

        private string ComputeNewNumber(string oldNumber, int autoNum)
        {
            if (oldNumber == null) oldNumber = string.Empty;

            if (rdoModeReplace.Checked)
            {
                string find = txtFind.Text;
                string repl = txtReplace.Text;

                if (string.IsNullOrEmpty(find)) return oldNumber;

                StringComparison comp = chkMatchCase.Checked ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

                if (chkOnlyPrefix.Checked)
                {
                    if (oldNumber.StartsWith(find, comp))
                    {
                        return repl + oldNumber.Substring(find.Length);
                    }
                    return oldNumber;
                }
                else
                {
                    if (chkMatchCase.Checked)
                    {
                        return oldNumber.Replace(find, repl);
                    }
                    else
                    {
                        // Case-insensitive replace
                        int pos = oldNumber.IndexOf(find, StringComparison.OrdinalIgnoreCase);
                        if (pos >= 0)
                        {
                            return oldNumber.Substring(0, pos) + repl + oldNumber.Substring(pos + find.Length);
                        }
                        return oldNumber;
                    }
                }
            }
            else if (rdoModeAdd.Checked)
            {
                string p = txtAddPrefix.Text ?? string.Empty;
                string s = txtAddSuffix.Text ?? string.Empty;
                return p + oldNumber + s;
            }
            else if (rdoModeNumber.Checked)
            {
                string p = txtNumPrefix.Text ?? string.Empty;
                string s = txtNumSuffix.Text ?? string.Empty;

                string numStr = autoNum.ToString();
                int idx = cmbNumFormat.SelectedIndex;
                if (idx == 0) // 01, 02
                {
                    numStr = autoNum.ToString("D2");
                }
                else if (idx == 1) // 001, 002
                {
                    numStr = autoNum.ToString("D3");
                }
                else if (idx == 2) // 1, 2
                {
                    numStr = autoNum.ToString();
                }
                else if (idx == 3) // 0001, 0002
                {
                    numStr = autoNum.ToString("D4");
                }

                return p + numStr + s;
            }

            return oldNumber;
        }

        private void BtnApply_Click(object sender, EventArgs e)
        {
            ModifiedNumbers.Clear();
            foreach (DataGridViewRow row in dgvPreview.Rows)
            {
                if (row.Tag is KeyValuePair<SheetItem, string>)
                {
                    KeyValuePair<SheetItem, string> kvp = (KeyValuePair<SheetItem, string>)row.Tag;
                    ModifiedNumbers[kvp.Key] = kvp.Value;
                }
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
