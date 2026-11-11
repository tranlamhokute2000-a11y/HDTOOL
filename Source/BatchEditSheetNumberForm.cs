using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Color = System.Drawing.Color;
using Font = System.Drawing.Font;
using Point = System.Drawing.Point;

namespace HDTool
{
    /// <summary>
    /// Hộp thoại Sửa & Thay Thế Số Hiệu Hàng Loạt Với Các Tiền Tố Giống Nhau
    /// Hỗ trợ tính năng Excel AutoFill Drag Handle: Quét chọn rồi kéo chuột tự động tăng dần số đuôi
    /// giống hệt tính năng trong bảng Microsoft Excel theo lệnh của Sếp Hồ (HadoMEE).
    /// </summary>
    public class BatchEditSheetNumberForm : Form
    {
        private List<SheetItem> _allSheets;
        private List<SheetItem> _selectedSheets;
        private List<SheetItem> _activeTargetSheets;
        private string _detectedCommonPrefix = string.Empty;

        /// <summary>
        /// Sự kiện kích hoạt khi người dùng bấm Áp dụng thay đổi số hiệu ở chế độ Modeless
        /// </summary>
        public event Action<Dictionary<SheetItem, string>> ChangesApplied;

        // Bộ nhớ lưu các số hiệu đã sửa thủ công hoặc do kéo chuột Excel AutoFill
        private Dictionary<SheetItem, string> _manualOverrides = new Dictionary<SheetItem, string>();

        // Biến trạng thái kéo chuột Excel AutoFill Drag Handle
        private bool _isDraggingFillHandle = false;
        private int _dragStartRow = -1;
        private int _dragCurrentRow = -1;
        private Rectangle _currentFillHandleRect = Rectangle.Empty;
        private System.Windows.Forms.ToolTip _dragToolTip = new System.Windows.Forms.ToolTip();

        // UI Controls
        private System.Windows.Forms.Panel pnlHeader;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblSubtitle;

        private System.Windows.Forms.Panel pnlTopContainer;
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
        private System.Windows.Forms.Panel pnlPreviewToolbar;
        private System.Windows.Forms.Button btnQuickAutoFill;
        private System.Windows.Forms.Button btnResetPrefix;
        private System.Windows.Forms.Label lblExcelHint;

        private DataGridView dgvPreview;
        private DataGridViewTextBoxColumn colPvStt;
        private DataGridViewTextBoxColumn colPvName;
        private DataGridViewTextBoxColumn colPvOld;
        private DataGridViewTextBoxColumn colPvNew;
        private DataGridViewTextBoxColumn colPvStatus;
        private System.Windows.Forms.Label lblPreviewStats;

        private ContextMenuStrip ctxGridMenu;

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
            // Sử dụng ClientSize cố định vùng nội dung để các nút dưới đáy KHÔNG BAO GIỜ bị che khuất
            this.ClientSize = new Size(980, 720);
            this.MinimumSize = new Size(920, 640);
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
            lblTitle.Font = new Font("Segoe UI", 11.5f, FontStyle.Bold);
            lblTitle.Location = new Point(16, 12);
            lblTitle.AutoSize = true;

            lblSubtitle = new System.Windows.Forms.Label();
            lblSubtitle.Text = "Tự động nhận diện tiền tố chung, tìm & thay thế và hỗ trợ KÉO CHUỘT TĂNG SỐ ĐUÔI GIỐNG EXCEL";
            lblSubtitle.ForeColor = Color.FromArgb(189, 195, 199);
            lblSubtitle.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblSubtitle.Location = new Point(16, 38);
            lblSubtitle.AutoSize = true;

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);

            // Container phía trên
            pnlTopContainer = new System.Windows.Forms.Panel();
            pnlTopContainer.Dock = DockStyle.Top;
            pnlTopContainer.Height = 145;
            pnlTopContainer.BackColor = Color.FromArgb(245, 247, 250);

            // 2. Mode Selector Group
            grpMode = new System.Windows.Forms.GroupBox();
            grpMode.Text = "Chọn Chế Độ Thay Đổi";
            grpMode.Location = new Point(14, 6);
            grpMode.Size = new Size(430, 52);
            grpMode.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            grpMode.ForeColor = Color.FromArgb(24, 43, 73);

            rdoModeReplace = new System.Windows.Forms.RadioButton();
            rdoModeReplace.Text = "1. Thay Thế Tiền Tố";
            rdoModeReplace.Location = new Point(10, 19);
            rdoModeReplace.Size = new Size(145, 22);
            rdoModeReplace.Checked = true;
            rdoModeReplace.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            rdoModeReplace.CheckedChanged += Mode_CheckedChanged;

            rdoModeAdd = new System.Windows.Forms.RadioButton();
            rdoModeAdd.Text = "2. Thêm Tiền Tố/Hậu Tố";
            rdoModeAdd.Location = new Point(160, 19);
            rdoModeAdd.Size = new Size(150, 22);
            rdoModeAdd.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            rdoModeAdd.CheckedChanged += Mode_CheckedChanged;

            rdoModeNumber = new System.Windows.Forms.RadioButton();
            rdoModeNumber.Text = "3. Đánh Số TĐ";
            rdoModeNumber.Location = new Point(315, 19);
            rdoModeNumber.Size = new Size(110, 22);
            rdoModeNumber.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            rdoModeNumber.CheckedChanged += Mode_CheckedChanged;

            grpMode.Controls.Add(rdoModeReplace);
            grpMode.Controls.Add(rdoModeAdd);
            grpMode.Controls.Add(rdoModeNumber);

            // Scope Selector Group
            grpScope = new System.Windows.Forms.GroupBox();
            grpScope.Text = "Phạm Vi Áp Dụng";
            grpScope.Location = new Point(452, 6);
            grpScope.Size = new Size(514, 52);
            grpScope.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            grpScope.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            grpScope.ForeColor = Color.FromArgb(24, 43, 73);

            rdoScopeMatched = new System.Windows.Forms.RadioButton();
            rdoScopeMatched.Text = "⭐ Cùng tiền tố (0 bản vẽ)";
            rdoScopeMatched.Location = new Point(10, 19);
            rdoScopeMatched.Size = new Size(230, 22);
            rdoScopeMatched.Checked = true;
            rdoScopeMatched.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            rdoScopeMatched.ForeColor = Color.FromArgb(24, 106, 59); // Green
            rdoScopeMatched.CheckedChanged += Scope_CheckedChanged;

            rdoScopeSelected = new System.Windows.Forms.RadioButton();
            rdoScopeSelected.Text = string.Format("Đang chọn ({0})", _selectedSheets.Count);
            rdoScopeSelected.Location = new Point(245, 19);
            rdoScopeSelected.Size = new Size(140, 22);
            rdoScopeSelected.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            rdoScopeSelected.CheckedChanged += Scope_CheckedChanged;

            rdoScopeAll = new System.Windows.Forms.RadioButton();
            rdoScopeAll.Text = string.Format("Tất cả ({0})", _allSheets.Count);
            rdoScopeAll.Location = new Point(390, 19);
            rdoScopeAll.Size = new Size(110, 22);
            rdoScopeAll.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            rdoScopeAll.CheckedChanged += Scope_CheckedChanged;

            grpScope.Controls.Add(rdoScopeMatched);
            grpScope.Controls.Add(rdoScopeSelected);
            grpScope.Controls.Add(rdoScopeAll);

            // Mode 1: Replace inputs
            pnlModeReplace = new System.Windows.Forms.Panel();
            pnlModeReplace.Location = new Point(14, 64);
            pnlModeReplace.Size = new Size(952, 74);
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
            txtFind.TextChanged += (s, e) => { _manualOverrides.Clear(); UpdateLivePreview(); };
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
            txtReplace.Size = new Size(330, 23);
            txtReplace.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtReplace.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            txtReplace.ForeColor = Color.FromArgb(24, 106, 59);
            txtReplace.TextChanged += (s, e) => { _manualOverrides.Clear(); UpdateLivePreview(); };
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
            chkOnlyPrefix.CheckedChanged += (s, e) => { _manualOverrides.Clear(); UpdateLivePreview(); };

            chkMatchCase = new System.Windows.Forms.CheckBox();
            chkMatchCase.Text = "Phân biệt chữ hoa / thường";
            chkMatchCase.Location = new Point(580, 42);
            chkMatchCase.Size = new Size(220, 22);
            chkMatchCase.Checked = false;
            chkMatchCase.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            chkMatchCase.CheckedChanged += (s, e) => { _manualOverrides.Clear(); UpdateLivePreview(); };

            pnlModeReplace.Controls.Add(lblFind);
            pnlModeReplace.Controls.Add(txtFind);
            pnlModeReplace.Controls.Add(lblReplace);
            pnlModeReplace.Controls.Add(txtReplace);
            pnlModeReplace.Controls.Add(chkOnlyPrefix);
            pnlModeReplace.Controls.Add(chkMatchCase);

            // Mode 2: Add inputs
            pnlModeAdd = new System.Windows.Forms.Panel();
            pnlModeAdd.Location = new Point(14, 64);
            pnlModeAdd.Size = new Size(952, 74);
            pnlModeAdd.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            pnlModeAdd.BackColor = Color.FromArgb(238, 242, 246);
            pnlModeAdd.BorderStyle = BorderStyle.FixedSingle;
            pnlModeAdd.Visible = false;

            lblAddPrefix = new System.Windows.Forms.Label();
            lblAddPrefix.Text = "Thêm Tiền tố vào trước (Prefix):";
            lblAddPrefix.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            lblAddPrefix.ForeColor = Color.FromArgb(41, 128, 185);
            lblAddPrefix.Location = new Point(12, 16);
            lblAddPrefix.AutoSize = true;

            txtAddPrefix = new System.Windows.Forms.TextBox();
            txtAddPrefix.Location = new Point(205, 13);
            txtAddPrefix.Size = new Size(210, 23);
            txtAddPrefix.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            txtAddPrefix.TextChanged += (s, e) => { _manualOverrides.Clear(); UpdateLivePreview(); };

            lblAddSuffix = new System.Windows.Forms.Label();
            lblAddSuffix.Text = "Thêm Hậu tố vào sau (Suffix):";
            lblAddSuffix.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            lblAddSuffix.ForeColor = Color.FromArgb(142, 68, 173);
            lblAddSuffix.Location = new Point(430, 16);
            lblAddSuffix.AutoSize = true;

            txtAddSuffix = new System.Windows.Forms.TextBox();
            txtAddSuffix.Location = new Point(600, 13);
            txtAddSuffix.Size = new Size(210, 23);
            txtAddSuffix.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            txtAddSuffix.TextChanged += (s, e) => { _manualOverrides.Clear(); UpdateLivePreview(); };

            System.Windows.Forms.Label lblAddHint = new System.Windows.Forms.Label();
            lblAddHint.Text = "💡 Ví dụ: Thêm tiền tố \"PCCC-\" hoặc thêm hậu tố \"-REV1\" vào tất cả các số hiệu đang chọn.";
            lblAddHint.Location = new Point(12, 46);
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
            pnlModeNumber.Location = new Point(14, 64);
            pnlModeNumber.Size = new Size(952, 74);
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
            txtNumPrefix.TextChanged += (s, e) => { _manualOverrides.Clear(); UpdateLivePreview(); };

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
            numStart.ValueChanged += (s, e) => { _manualOverrides.Clear(); UpdateLivePreview(); };

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
            cmbNumFormat.SelectedIndexChanged += (s, e) => { _manualOverrides.Clear(); UpdateLivePreview(); };

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
            numStep.ValueChanged += (s, e) => { _manualOverrides.Clear(); UpdateLivePreview(); };

            lblNumSuffix = new System.Windows.Forms.Label();
            lblNumSuffix.Text = "Hậu tố:";
            lblNumSuffix.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            lblNumSuffix.Location = new Point(705, 15);
            lblNumSuffix.AutoSize = true;

            txtNumSuffix = new System.Windows.Forms.TextBox();
            txtNumSuffix.Location = new Point(755, 12);
            txtNumSuffix.Size = new Size(140, 23);
            txtNumSuffix.TextChanged += (s, e) => { _manualOverrides.Clear(); UpdateLivePreview(); };

            System.Windows.Forms.Label lblNumHint = new System.Windows.Forms.Label();
            lblNumHint.Text = "💡 Ví dụ: Tiền tố \"HDM/CB/B1/T1.\" ➔ Đánh số tự động: HDM/CB/B1/T1.01, HDM/CB/B1/T1.02...";
            lblNumHint.Location = new Point(10, 46);
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

            pnlTopContainer.Controls.Add(pnlModeReplace);
            pnlTopContainer.Controls.Add(pnlModeAdd);
            pnlTopContainer.Controls.Add(pnlModeNumber);
            pnlTopContainer.Controls.Add(grpScope);
            pnlTopContainer.Controls.Add(grpMode);

            // 3. Live Preview GroupBox & DataGridView (Dock = Fill để co dãn chuẩn mực 100%)
            grpPreview = new System.Windows.Forms.GroupBox();
            grpPreview.Text = "Xem Trước Thay Đổi Thời Gian Thực (Live Preview)";
            grpPreview.Dock = DockStyle.Fill;
            grpPreview.Padding = new Padding(12, 8, 12, 8);
            grpPreview.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            grpPreview.ForeColor = Color.FromArgb(24, 43, 73);

            // Thanh Toolbar thao tác Excel AutoFill
            pnlPreviewToolbar = new System.Windows.Forms.Panel();
            pnlPreviewToolbar.Dock = DockStyle.Top;
            pnlPreviewToolbar.Height = 32;
            pnlPreviewToolbar.BackColor = Color.FromArgb(240, 244, 248);
            pnlPreviewToolbar.Padding = new Padding(6, 3, 6, 3);

            lblExcelHint = new System.Windows.Forms.Label();
            lblExcelHint.Text = "✨ Kéo ô vuông góc dưới-phải (Fill Handle) hoặc bấm nút để số đuôi tự động tăng dần:";
            lblExcelHint.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
            lblExcelHint.ForeColor = Color.FromArgb(16, 124, 65); // Excel Green
            lblExcelHint.Location = new Point(6, 7);
            lblExcelHint.AutoSize = true;

            btnQuickAutoFill = new System.Windows.Forms.Button();
            btnQuickAutoFill.Text = "⚡ Kéo Tăng Đuôi Theo Ô Đang Chọn";
            btnQuickAutoFill.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            btnQuickAutoFill.Size = new Size(220, 26);
            btnQuickAutoFill.Location = new Point(510, 3);
            btnQuickAutoFill.BackColor = Color.FromArgb(16, 124, 65);
            btnQuickAutoFill.ForeColor = Color.White;
            btnQuickAutoFill.FlatStyle = FlatStyle.Flat;
            btnQuickAutoFill.FlatAppearance.BorderSize = 0;
            btnQuickAutoFill.Cursor = Cursors.Hand;
            btnQuickAutoFill.Click += (s, e) => { AutoFillFromCurrentSelection(); };

            btnResetPrefix = new System.Windows.Forms.Button();
            btnResetPrefix.Text = "↺ Đặt Lại Theo Tiền Tố";
            btnResetPrefix.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            btnResetPrefix.Size = new Size(150, 26);
            btnResetPrefix.Location = new Point(738, 3);
            btnResetPrefix.BackColor = Color.FromArgb(220, 226, 235);
            btnResetPrefix.ForeColor = Color.FromArgb(44, 62, 80);
            btnResetPrefix.FlatStyle = FlatStyle.Flat;
            btnResetPrefix.FlatAppearance.BorderSize = 0;
            btnResetPrefix.Cursor = Cursors.Hand;
            btnResetPrefix.Click += (s, e) => { _manualOverrides.Clear(); UpdateLivePreview(); };

            pnlPreviewToolbar.Controls.Add(lblExcelHint);
            pnlPreviewToolbar.Controls.Add(btnQuickAutoFill);
            pnlPreviewToolbar.Controls.Add(btnResetPrefix);

            dgvPreview = new DataGridView();
            dgvPreview.Dock = DockStyle.Fill;
            dgvPreview.BackgroundColor = Color.White;
            dgvPreview.BorderStyle = BorderStyle.FixedSingle;
            dgvPreview.RowHeadersVisible = false;
            dgvPreview.AllowUserToAddRows = false;
            dgvPreview.AllowUserToDeleteRows = false;
            dgvPreview.AllowUserToResizeRows = false;
            dgvPreview.ReadOnly = false; // Bật cho phép sửa cột New
            dgvPreview.SelectionMode = DataGridViewSelectionMode.CellSelect;
            dgvPreview.MultiSelect = true;
            dgvPreview.RowTemplate.Height = 26;
            dgvPreview.ColumnHeadersHeight = 28;
            dgvPreview.EnableHeadersVisualStyles = false;
            dgvPreview.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(235, 240, 245);
            dgvPreview.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(44, 62, 80);
            dgvPreview.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);

            // Bật DoubleBuffered chống giật lag khi kéo chuột
            try
            {
                typeof(DataGridView).InvokeMember("DoubleBuffered",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.SetProperty,
                    null, dgvPreview, new object[] { true });
            }
            catch { }

            colPvStt = new DataGridViewTextBoxColumn();
            colPvStt.HeaderText = "STT";
            colPvStt.Width = 45;
            colPvStt.ReadOnly = true;
            colPvStt.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvPreview.Columns.Add(colPvStt);

            colPvName = new DataGridViewTextBoxColumn();
            colPvName.HeaderText = "Tên Bản Vẽ (Sheet Name)";
            colPvName.Width = 270;
            colPvName.ReadOnly = true;
            colPvName.DefaultCellStyle.ForeColor = Color.FromArgb(80, 80, 80);
            dgvPreview.Columns.Add(colPvName);

            colPvOld = new DataGridViewTextBoxColumn();
            colPvOld.HeaderText = "Số Hiệu Hiện Tại (Cũ)";
            colPvOld.Width = 210;
            colPvOld.ReadOnly = true;
            colPvOld.DefaultCellStyle.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
            colPvOld.DefaultCellStyle.ForeColor = Color.FromArgb(100, 100, 100);
            dgvPreview.Columns.Add(colPvOld);

            // CỘT SỐ HIỆU MỚI: Cho phép sửa trực tiếp & Kéo chuột tăng đuôi kiểu Excel
            colPvNew = new DataGridViewTextBoxColumn();
            colPvNew.HeaderText = "Số Hiệu Mới (Xem Trước / Sửa Trực Tiếp / Kéo Chuột Excel ➔)";
            colPvNew.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colPvNew.ReadOnly = false;
            colPvNew.DefaultCellStyle.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            colPvNew.DefaultCellStyle.ForeColor = Color.FromArgb(16, 124, 65);
            dgvPreview.Columns.Add(colPvNew);

            colPvStatus = new DataGridViewTextBoxColumn();
            colPvStatus.HeaderText = "Trạng Thái";
            colPvStatus.Width = 125;
            colPvStatus.ReadOnly = true;
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

            // Gắn các sự kiện Excel Drag Handle & Inline Edit
            dgvPreview.CellPainting += DgvPreview_CellPainting;
            dgvPreview.Paint += DgvPreview_Paint;
            dgvPreview.MouseDown += DgvPreview_MouseDown;
            dgvPreview.MouseMove += DgvPreview_MouseMove;
            dgvPreview.MouseUp += DgvPreview_MouseUp;
            dgvPreview.CellDoubleClick += DgvPreview_CellDoubleClick;
            dgvPreview.CellEndEdit += DgvPreview_CellEndEdit;
            dgvPreview.KeyDown += DgvPreview_KeyDown;

            // Context Menu
            ctxGridMenu = new ContextMenuStrip();
            ToolStripMenuItem mnuAutoFill = new ToolStripMenuItem("⚡ Tự Động Kéo Tăng Dần Số Đuôi (AutoFill)", null, (s, e) => { AutoFillFromCurrentSelection(); });
            ToolStripMenuItem mnuFillDown = new ToolStripMenuItem("📋 Sao Chép Ô Đầu Xuống Dưới (Fill Down)", null, (s, e) => { FillDownFromCurrentSelection(); });
            ToolStripMenuItem mnuReset = new ToolStripMenuItem("↺ Đặt Lại Theo Tiền Tố Ban Đầu", null, (s, e) => { _manualOverrides.Clear(); UpdateLivePreview(); });
            ctxGridMenu.Items.Add(mnuAutoFill);
            ctxGridMenu.Items.Add(mnuFillDown);
            ctxGridMenu.Items.Add(new ToolStripSeparator());
            ctxGridMenu.Items.Add(mnuReset);
            dgvPreview.ContextMenuStrip = ctxGridMenu;

            grpPreview.Controls.Add(dgvPreview);
            grpPreview.Controls.Add(pnlPreviewToolbar);
            grpPreview.Controls.Add(lblPreviewStats);

            // 4. Bottom Action Panel (Dock = Bottom, hiển thị đầy đủ 100% không che khuất)
            pnlBottom = new System.Windows.Forms.Panel();
            pnlBottom.Dock = DockStyle.Bottom;
            pnlBottom.Height = 56;
            pnlBottom.BackColor = Color.FromArgb(240, 243, 246);

            btnApply = new System.Windows.Forms.Button();
            btnApply.Text = "✔  ÁP DỤNG THAY ĐỔI";
            btnApply.Font = new Font("Segoe UI", 10.0f, FontStyle.Bold);
            btnApply.BackColor = Color.FromArgb(39, 174, 96); // Green
            btnApply.ForeColor = Color.White;
            btnApply.FlatStyle = FlatStyle.Flat;
            btnApply.FlatAppearance.BorderSize = 0;
            btnApply.Cursor = Cursors.Hand;
            btnApply.Size = new Size(320, 42);
            btnApply.Click += BtnApply_Click;

            btnCancel = new System.Windows.Forms.Button();
            btnCancel.Text = "❌  HỦY BỎ";
            btnCancel.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            btnCancel.BackColor = Color.FromArgb(149, 165, 166);
            btnCancel.ForeColor = Color.White;
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Cursor = Cursors.Hand;
            btnCancel.Size = new Size(115, 42);
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
            this.CancelButton = btnCancel;

            pnlBottom.Resize += (s, e) =>
            {
                btnApply.Left = Math.Max(10, (pnlBottom.Width - btnApply.Width - btnCancel.Width - 15) / 2);
                btnApply.Top = 7;
                btnCancel.Left = btnApply.Right + 15;
                btnCancel.Top = 7;
            };

            pnlBottom.Controls.Add(btnApply);
            pnlBottom.Controls.Add(btnCancel);

            // Gắn Controls vào Form theo thứ tự Dock chuẩn
            this.Controls.Add(grpPreview);
            this.Controls.Add(pnlTopContainer);
            this.Controls.Add(pnlHeader);
            this.Controls.Add(pnlBottom);

            this.ResumeLayout(false);
        }

        #region ====== EXCEL AUTOFILL DRAG HANDLE ENGINE ======

        private void DgvPreview_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            // Vẽ viền highlight ô đang chọn trong cột New
            if (e.RowIndex >= 0 && e.ColumnIndex == colPvNew.Index)
            {
                if (dgvPreview.Rows[e.RowIndex].Cells[e.ColumnIndex].Selected)
                {
                    e.Paint(e.ClipBounds, DataGridViewPaintParts.All);
                    using (Pen p = new Pen(Color.FromArgb(16, 124, 65), 2.0f))
                    {
                        Rectangle r = e.CellBounds;
                        e.Graphics.DrawRectangle(p, r.Left + 1, r.Top + 1, r.Width - 2, r.Height - 2);
                    }
                    e.Handled = true;
                }
            }
        }

        private void DgvPreview_Paint(object sender, PaintEventArgs e)
        {
            // Vẽ nút vuông Fill Handle nhỏ tại góc dưới phải của ô/vùng chọn cột colPvNew
            _currentFillHandleRect = Rectangle.Empty;

            if (dgvPreview.SelectedCells.Count == 0) return;

            int maxRow = -1;
            foreach (DataGridViewCell cell in dgvPreview.SelectedCells)
            {
                if (cell.ColumnIndex == colPvNew.Index && cell.RowIndex > maxRow)
                {
                    maxRow = cell.RowIndex;
                }
            }

            if (maxRow >= 0 && maxRow < dgvPreview.Rows.Count)
            {
                Rectangle cellRect = dgvPreview.GetCellDisplayRectangle(colPvNew.Index, maxRow, true);
                if (cellRect.Width > 0 && cellRect.Height > 0)
                {
                    int hSize = 6;
                    Rectangle handleRect = new Rectangle(cellRect.Right - hSize - 1, cellRect.Bottom - hSize - 1, hSize, hSize);
                    _currentFillHandleRect = handleRect;

                    using (SolidBrush b = new SolidBrush(Color.FromArgb(16, 124, 65)))
                    {
                        e.Graphics.FillRectangle(b, handleRect);
                    }
                    using (Pen p = new Pen(Color.White, 1.0f))
                    {
                        e.Graphics.DrawRectangle(p, handleRect);
                    }
                }
            }

            // Vẽ khung nét đứt khi đang kéo chuột AutoFill
            if (_isDraggingFillHandle && _dragStartRow >= 0 && _dragCurrentRow >= 0)
            {
                int rMin = Math.Min(_dragStartRow, _dragCurrentRow);
                int rMax = Math.Max(_dragStartRow, _dragCurrentRow);

                Rectangle topRect = dgvPreview.GetCellDisplayRectangle(colPvNew.Index, rMin, true);
                Rectangle btmRect = dgvPreview.GetCellDisplayRectangle(colPvNew.Index, rMax, true);

                if (topRect.Width > 0 && btmRect.Width > 0)
                {
                    int topY = topRect.Top;
                    int btmY = btmRect.Bottom;
                    Rectangle dragBox = new Rectangle(topRect.Left, topY, topRect.Width, btmY - topY);

                    using (SolidBrush shade = new SolidBrush(Color.FromArgb(35, 16, 124, 65)))
                    {
                        e.Graphics.FillRectangle(shade, dragBox);
                    }
                    using (Pen dashPen = new Pen(Color.FromArgb(16, 124, 65), 1.8f))
                    {
                        dashPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                        e.Graphics.DrawRectangle(dashPen, dragBox.Left + 1, dragBox.Top + 1, dragBox.Width - 3, dragBox.Height - 3);
                    }
                }
            }
        }

        private void DgvPreview_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && !_currentFillHandleRect.IsEmpty)
            {
                Rectangle hitArea = new Rectangle(_currentFillHandleRect.X - 3, _currentFillHandleRect.Y - 3, _currentFillHandleRect.Width + 6, _currentFillHandleRect.Height + 6);
                if (hitArea.Contains(e.Location))
                {
                    _isDraggingFillHandle = true;
                    dgvPreview.Capture = true;

                    // Xác định hàng xuất phát
                    _dragStartRow = -1;
                    foreach (DataGridViewCell cell in dgvPreview.SelectedCells)
                    {
                        if (cell.ColumnIndex == colPvNew.Index)
                        {
                            if (_dragStartRow == -1 || cell.RowIndex > _dragStartRow)
                            {
                                _dragStartRow = cell.RowIndex;
                            }
                        }
                    }
                    if (_dragStartRow == -1 && dgvPreview.CurrentCell != null)
                    {
                        _dragStartRow = dgvPreview.CurrentCell.RowIndex;
                    }
                    _dragCurrentRow = _dragStartRow;
                }
            }
        }

        private void DgvPreview_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDraggingFillHandle)
            {
                if (!_currentFillHandleRect.IsEmpty)
                {
                    Rectangle hitArea = new Rectangle(_currentFillHandleRect.X - 3, _currentFillHandleRect.Y - 3, _currentFillHandleRect.Width + 6, _currentFillHandleRect.Height + 6);
                    if (hitArea.Contains(e.Location))
                    {
                        dgvPreview.Cursor = Cursors.Cross;
                        return;
                    }
                }
                dgvPreview.Cursor = Cursors.Default;
            }
            else
            {
                dgvPreview.Cursor = Cursors.Cross;
                DataGridView.HitTestInfo hit = dgvPreview.HitTest(e.X, e.Y);
                if (hit.RowIndex >= 0 && hit.RowIndex != _dragCurrentRow && hit.RowIndex < dgvPreview.Rows.Count)
                {
                    _dragCurrentRow = hit.RowIndex;
                    string previewVal = CalculateAutoFillValue(_dragStartRow, _dragCurrentRow);
                    _dragToolTip.Show(string.Format("→ {0}", previewVal), dgvPreview, e.X + 18, e.Y + 12);
                    dgvPreview.Invalidate();
                }
            }
        }

        private void DgvPreview_MouseUp(object sender, MouseEventArgs e)
        {
            if (_isDraggingFillHandle)
            {
                _isDraggingFillHandle = false;
                dgvPreview.Capture = false;
                _dragToolTip.Hide(dgvPreview);
                dgvPreview.Cursor = Cursors.Default;

                if (_dragStartRow >= 0 && _dragCurrentRow >= 0 && _dragStartRow != _dragCurrentRow)
                {
                    ApplyAutoFillRange(_dragStartRow, _dragCurrentRow);
                }

                dgvPreview.Invalidate();
            }
        }

        private void DgvPreview_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            // Nhấp đúp vào Fill Handle -> Tự động điền tăng dần xuống hết bảng!
            if (e.RowIndex >= 0 && e.ColumnIndex == colPvNew.Index)
            {
                Point mousePos = dgvPreview.PointToClient(Control.MousePosition);
                if (!_currentFillHandleRect.IsEmpty)
                {
                    Rectangle hitArea = new Rectangle(_currentFillHandleRect.X - 4, _currentFillHandleRect.Y - 4, _currentFillHandleRect.Width + 8, _currentFillHandleRect.Height + 8);
                    if (hitArea.Contains(mousePos))
                    {
                        AutoFillFromRowDown(e.RowIndex);
                    }
                }
            }
        }

        private void DgvPreview_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == colPvNew.Index)
            {
                DataGridViewRow row = dgvPreview.Rows[e.RowIndex];
                string enteredVal = row.Cells[colPvNew.Index].Value as string ?? string.Empty;

                if (row.Tag is KeyValuePair<SheetItem, string>)
                {
                    SheetItem item = ((KeyValuePair<SheetItem, string>)row.Tag).Key;
                    _manualOverrides[item] = enteredVal;
                    row.Tag = new KeyValuePair<SheetItem, string>(item, enteredVal);
                }

                UpdateDuplicateCheckAndStats();
            }
        }

        private void DgvPreview_KeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+D: Fill Down
            if (e.Control && e.KeyCode == Keys.D)
            {
                FillDownFromCurrentSelection();
                e.Handled = true;
            }
        }

        private string CalculateAutoFillValue(int startRow, int targetRow)
        {
            if (startRow < 0 || startRow >= dgvPreview.Rows.Count) return string.Empty;
            string baseVal = dgvPreview.Rows[startRow].Cells[colPvNew.Index].Value as string ?? string.Empty;
            int offset = targetRow - startRow;
            return IncrementLastNumber(baseVal, offset);
        }

        private void ApplyAutoFillRange(int startRow, int targetRow)
        {
            if (startRow < 0 || startRow >= dgvPreview.Rows.Count) return;
            if (targetRow < 0 || targetRow >= dgvPreview.Rows.Count) return;

            string baseVal = dgvPreview.Rows[startRow].Cells[colPvNew.Index].Value as string ?? string.Empty;

            if (targetRow > startRow)
            {
                for (int r = startRow + 1; r <= targetRow; r++)
                {
                    int offset = r - startRow;
                    string newVal = IncrementLastNumber(baseVal, offset);
                    SetRowNewNumber(r, newVal);
                }
            }
            else if (targetRow < startRow)
            {
                for (int r = startRow - 1; r >= targetRow; r--)
                {
                    int offset = r - startRow;
                    string newVal = IncrementLastNumber(baseVal, offset);
                    SetRowNewNumber(r, newVal);
                }
            }

            UpdateDuplicateCheckAndStats();
        }

        private void AutoFillFromRowDown(int startRow)
        {
            if (startRow < 0 || startRow >= dgvPreview.Rows.Count) return;
            string baseVal = dgvPreview.Rows[startRow].Cells[colPvNew.Index].Value as string ?? string.Empty;

            for (int r = startRow + 1; r < dgvPreview.Rows.Count; r++)
            {
                int offset = r - startRow;
                string newVal = IncrementLastNumber(baseVal, offset);
                SetRowNewNumber(r, newVal);
            }

            UpdateDuplicateCheckAndStats();
            dgvPreview.Invalidate();
        }

        private void AutoFillFromCurrentSelection()
        {
            if (dgvPreview.CurrentCell == null) return;
            int startRow = dgvPreview.CurrentCell.RowIndex;
            AutoFillFromRowDown(startRow);
        }

        private void FillDownFromCurrentSelection()
        {
            if (dgvPreview.SelectedCells.Count <= 1) return;

            int minRow = int.MaxValue;
            int maxRow = int.MinValue;
            foreach (DataGridViewCell cell in dgvPreview.SelectedCells)
            {
                if (cell.RowIndex < minRow) minRow = cell.RowIndex;
                if (cell.RowIndex > maxRow) maxRow = cell.RowIndex;
            }

            if (minRow >= 0 && maxRow > minRow)
            {
                string copyVal = dgvPreview.Rows[minRow].Cells[colPvNew.Index].Value as string ?? string.Empty;
                for (int r = minRow + 1; r <= maxRow; r++)
                {
                    SetRowNewNumber(r, copyVal);
                }
                UpdateDuplicateCheckAndStats();
                dgvPreview.Invalidate();
            }
        }

        private void SetRowNewNumber(int rowIdx, string newVal)
        {
            if (rowIdx < 0 || rowIdx >= dgvPreview.Rows.Count) return;
            DataGridViewRow row = dgvPreview.Rows[rowIdx];
            row.Cells[colPvNew.Index].Value = newVal;

            if (row.Tag is KeyValuePair<SheetItem, string>)
            {
                SheetItem item = ((KeyValuePair<SheetItem, string>)row.Tag).Key;
                _manualOverrides[item] = newVal;
                row.Tag = new KeyValuePair<SheetItem, string>(item, newVal);
            }
        }

        /// <summary>
        /// Thuật toán tìm cụm số cuối cùng trong chuỗi và tăng/giảm theo offset, bảo toàn tiền tố và độ dài đệm 0
        /// </summary>
        public static string IncrementLastNumber(string baseText, int offset)
        {
            if (string.IsNullOrEmpty(baseText)) return baseText;

            // Tìm cụm số cuối cùng trong chuỗi: (\d+)(?!.*\d)
            Match m = Regex.Match(baseText, @"(\d+)(?!.*\d)");
            if (!m.Success)
            {
                return string.Format("{0}{1:D2}", baseText, Math.Max(1, 1 + offset));
            }

            string numStr = m.Value;
            int baseNum;
            if (!int.TryParse(numStr, out baseNum))
            {
                return baseText;
            }

            int targetNum = Math.Max(0, baseNum + offset);
            int padLength = numStr.Length; // Giữ nguyên số chữ số 0 (ví dụ 01 -> pad 2, 005 -> pad 3)

            string newNumStr = targetNum.ToString("D" + padLength);
            return baseText.Substring(0, m.Index) + newNumStr + baseText.Substring(m.Index + m.Length);
        }

        #endregion

        #region ====== LOGIC TÍNH TOÁN & CẬP NHẬT LIVE PREVIEW ======

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

            _manualOverrides.Clear();
            UpdateLivePreview();
        }

        private void Scope_CheckedChanged(object sender, EventArgs e)
        {
            _manualOverrides.Clear();
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
            else
            {
                pattern = _detectedCommonPrefix ?? string.Empty;
            }

            if (string.IsNullOrEmpty(pattern))
            {
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

            int stt = 1;
            int autoNum = (int)numStart.Value;
            int step = (int)numStep.Value;

            foreach (SheetItem item in _activeTargetSheets)
            {
                string oldNum = item.SheetNumber ?? string.Empty;
                string newNum = string.Empty;

                // Nếu có sửa đổi thủ công hoặc do kéo chuột Excel AutoFill
                if (_manualOverrides.ContainsKey(item))
                {
                    newNum = _manualOverrides[item];
                }
                else
                {
                    newNum = ComputeNewNumber(oldNum, autoNum);
                }
                autoNum += step;

                int rowIdx = dgvPreview.Rows.Add(
                    stt++,
                    item.SheetName,
                    oldNum,
                    newNum,
                    "Tính toán..."
                );
                DataGridViewRow row = dgvPreview.Rows[rowIdx];
                row.Tag = new KeyValuePair<SheetItem, string>(item, newNum);
            }

            UpdateDuplicateCheckAndStats();
        }

        private void UpdateDuplicateCheckAndStats()
        {
            if (dgvPreview == null) return;

            int changedCount = 0;
            int duplicateCount = 0;

            HashSet<string> generatedNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> existingExternalNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (SheetItem s in _allSheets)
            {
                if (!_activeTargetSheets.Contains(s) && !string.IsNullOrEmpty(s.SheetNumber))
                {
                    existingExternalNumbers.Add(s.SheetNumber);
                }
            }

            foreach (DataGridViewRow row in dgvPreview.Rows)
            {
                string oldNum = row.Cells[colPvOld.Index].Value as string ?? string.Empty;
                string newNum = row.Cells[colPvNew.Index].Value as string ?? string.Empty;

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

                row.Cells[colPvStatus.Index].Value = statusText;
                row.Cells[colPvStatus.Index].Style.ForeColor = statusColor;
                row.Cells[colPvStatus.Index].Style.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);

                if (isChanged)
                {
                    row.Cells[colPvNew.Index].Style.BackColor = Color.FromArgb(234, 250, 234);
                    row.Cells[colPvNew.Index].Style.ForeColor = Color.FromArgb(24, 106, 59);
                }
                else
                {
                    row.Cells[colPvNew.Index].Style.BackColor = Color.White;
                    row.Cells[colPvNew.Index].Style.ForeColor = Color.FromArgb(16, 124, 65);
                }
            }

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

            if (ChangesApplied != null)
            {
                ChangesApplied(new Dictionary<SheetItem, string>(ModifiedNumbers));
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        #endregion
    }
}
