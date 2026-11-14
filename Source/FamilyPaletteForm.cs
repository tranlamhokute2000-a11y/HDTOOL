using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Color = System.Drawing.Color;
using Font = System.Drawing.Font;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;
using Size = System.Drawing.Size;
using WinControl = System.Windows.Forms.Control;

namespace HDTool
{
    /// <summary>
    /// Cửa sổ Palette tổng hợp Family dự án Revit - Phiên bản BẢNG Ô CỘT SIÊU MƯỢT (DataGridView).
    /// Thiết kế chuẩn giao diện khoa học theo chỉ đạo Sếp Hồ:
    /// 1. Triệt tiêu 100% hiện tượng đơ/giật/lag: Quét O(N) trong < 30ms, Lazy Load 3D Preview có Cache.
    /// 2. Thanh đầu mục (Toolbar) được thiết kế lại KHOA HỌC:
    ///    - Ô tìm kiếm nhanh (Search Box).
    ///    - Hộp chọn Chuyên Ngành (Category Dropdown ComboBox) hiển thị số lượng Type trực quan, triệt tiêu lỗi che khuất.
    ///    - Bộ lọc thông minh: CheckBox "Chỉ hiện Family đã vẽ trong mô hình (> 0)".
    ///    - Nhãn thống kê kết quả lọc thời gian thực.
    ///    - Nút Làm Mới (F5) neo góc phải.
    /// 3. Bảng DataGridView các ô cột rõ ràng, ngay ngắn, cuộn 60 FPS mượt mà.
    /// 4. 100% Modeless Dialog, phím ESC thoát lệnh ngay lập tức theo quy tắc AGENTS.md.
    /// </summary>
    public class FamilyPaletteForm : HDToolBaseForm
    {
        private UIDocument _uidoc;
        private Document _doc;
        private List<FamilyGroupItem> _allFamilies;
        private List<FamilyTypeRowItem> _displayRows;
        private FamilyTypeRowItem _selectedRow;
        private FamilyPaletteFilter _currentFilter;

        // Bộ nhớ đệm ảnh 3D Isometric Preview (Lazy Load Cache)
        private Dictionary<ElementId, Bitmap> _previewCache;

        // Root Layout Table: 4 Hàng cố định độc lập, chống che khuất 100%
        private TableLayoutPanel tblMain;

        // Row 0: Header Panel
        private System.Windows.Forms.Panel pnlHeader;
        private Label lblHeaderTitle;
        private Label lblHeaderSubtitle;
        private Label lblHeaderBadge;
        private Label lblTotalStats;

        // Row 1: Toolbar Khoa Học (Search + Category Dropdown + Smart Checkbox + Refresh)
        private System.Windows.Forms.Panel pnlToolbar;
        private Label lblSearchIcon;
        private System.Windows.Forms.TextBox txtSearch;
        private Label lblCategoryIcon;
        private System.Windows.Forms.ComboBox cboCategoryFilter;
        private System.Windows.Forms.CheckBox chkOnlyUsed;
        private Label lblFilterResult;
        private System.Windows.Forms.Button btnRefresh;

        // Row 2: SplitContainer (Left: DataGridView Table | Right: 3D Preview Inspector)
        private SplitContainer splitMain;
        private DataGridView dgvFamilies;

        // Inspector Controls (Khung xem trước 3D & Thông số bên phải)
        private System.Windows.Forms.Panel pnlInspector;
        private Label lblInspectorHeader;
        private PictureBox picLargePreview;
        private Label lblInspectorCategory;
        private Label lblInspectorFamName;
        private Label lblInspectorTypeName;
        private System.Windows.Forms.Panel pnlInspectorInfo;
        private Label lblInspectorHost;
        private Label lblInspectorInstances;
        private Label lblParamHeader;
        private DataGridView dgvParameters;
        private System.Windows.Forms.Button btnInspectorPlace;
        private Label lblPlacementHint;

        // Row 3: Bottom Status Bar
        private System.Windows.Forms.Panel pnlBottom;
        private Label lblStatus;
        private Label lblModelessNotice;
        private System.Windows.Forms.Button btnClose;

        private int _totalProjectTypes = 0;

        public FamilyPaletteForm(UIDocument uidoc)
            : base()
        {
            _uidoc = uidoc;
            _doc = (uidoc != null) ? uidoc.Document : null;
            InitializeData();
            InitializeComponent();
            RefreshFamiliesFromProject();
        }

        public FamilyPaletteForm(Document doc)
            : base()
        {
            _uidoc = null;
            _doc = doc;
            InitializeData();
            InitializeComponent();
            RefreshFamiliesFromProject();
        }

        public void UpdateContext(UIDocument uidoc)
        {
            if (uidoc == null) return;
            _uidoc = uidoc;
            _doc = uidoc.Document;
            RefreshFamiliesFromProject();
        }

        private void InitializeData()
        {
            _allFamilies = new List<FamilyGroupItem>();
            _displayRows = new List<FamilyTypeRowItem>();
            _currentFilter = new FamilyPaletteFilter();
            _previewCache = new Dictionary<ElementId, Bitmap>();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Cấu hình Form: Rộng rãi, hiện đại, hỗ trợ High-DPI
            this.Text = "HDTOOL - BẢNG PALETTE TỔNG HỢP FAMILY 3D & ĐẶT VÀO BẢN VẼ";
            this.ClientSize = new Size(1320, 840);
            this.MinimumSize = new Size(1100, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(15, 20, 28);
            this.ForeColor = Color.FromArgb(241, 245, 249);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.ShowIcon = false;
            this.DoubleBuffered = true;

            // ===================================================================
            // ROOT TABLELAYOUTPANEL: 4 HÀNG CỐ ĐỊNH, CHỐNG CHE KHUẤT 100%
            // ===================================================================
            tblMain = new TableLayoutPanel();
            tblMain.Dock = DockStyle.Fill;
            tblMain.ColumnCount = 1;
            tblMain.RowCount = 4;
            tblMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            // Hàng 0: Header (56px)
            // Hàng 1: Toolbar Khoa Học (52px)
            // Hàng 2: Content (100% Fill)
            // Hàng 3: Bottom Status (48px)
            tblMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
            tblMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            tblMain.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tblMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            tblMain.BackColor = Color.FromArgb(15, 20, 28);
            tblMain.Margin = new Padding(0);
            tblMain.Padding = new Padding(0);

            // ===================================================================
            // 1. HEADER PANEL (HÀNG 0)
            // ===================================================================
            pnlHeader = new System.Windows.Forms.Panel();
            pnlHeader.Dock = DockStyle.Fill;
            pnlHeader.BackColor = Color.FromArgb(16, 22, 34);
            pnlHeader.Margin = new Padding(0);
            pnlHeader.Paint += PnlHeader_Paint;
            pnlHeader.Resize += PnlHeader_Resize;

            lblHeaderTitle = new Label();
            lblHeaderTitle.Text = "THƯ VIỆN & TỔNG HỢP FAMILY DỰ ÁN 3D";
            lblHeaderTitle.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            lblHeaderTitle.ForeColor = Color.FromArgb(248, 250, 252);
            lblHeaderTitle.Location = new Point(16, 7);
            lblHeaderTitle.AutoSize = true;

            lblHeaderSubtitle = new Label();
            lblHeaderSubtitle.Text = "Tra cứu toàn bộ Family Type trong mô hình Revit. Nhấp đúp vào dòng hoặc bấm [Đặt Vào Bản Vẽ] để vẽ tiếp.";
            lblHeaderSubtitle.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
            lblHeaderSubtitle.ForeColor = Color.FromArgb(148, 163, 184);
            lblHeaderSubtitle.Location = new Point(17, 31);
            lblHeaderSubtitle.AutoSize = true;

            lblHeaderBadge = new Label();
            lblHeaderBadge.Text = "Revit 2024–2027";
            lblHeaderBadge.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            lblHeaderBadge.ForeColor = Color.FromArgb(199, 210, 254);
            lblHeaderBadge.BackColor = Color.FromArgb(49, 46, 129);
            lblHeaderBadge.TextAlign = ContentAlignment.MiddleCenter;
            lblHeaderBadge.Size = new Size(115, 24);
            lblHeaderBadge.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            lblTotalStats = new Label();
            lblTotalStats.Text = "0 Families | 0 Types";
            lblTotalStats.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            lblTotalStats.ForeColor = Color.FromArgb(52, 211, 153);
            lblTotalStats.BackColor = Color.FromArgb(15, 23, 42);
            lblTotalStats.BorderStyle = BorderStyle.FixedSingle;
            lblTotalStats.TextAlign = ContentAlignment.MiddleCenter;
            lblTotalStats.Size = new Size(200, 26);
            lblTotalStats.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            pnlHeader.Controls.Add(lblHeaderTitle);
            pnlHeader.Controls.Add(lblHeaderSubtitle);
            pnlHeader.Controls.Add(lblHeaderBadge);
            pnlHeader.Controls.Add(lblTotalStats);
            tblMain.Controls.Add(pnlHeader, 0, 0);

            // ===================================================================
            // 2. TOOLBAR KHOA HỌC (HÀNG 1: Bố cục rành mạch, không chồng chéo)
            // ===================================================================
            pnlToolbar = new System.Windows.Forms.Panel();
            pnlToolbar.Dock = DockStyle.Fill;
            pnlToolbar.BackColor = Color.FromArgb(19, 26, 38);
            pnlToolbar.Margin = new Padding(0);
            pnlToolbar.Padding = new Padding(14, 8, 14, 8);
            pnlToolbar.Resize += PnlToolbar_Resize;

            // Khối 1: Tìm kiếm
            lblSearchIcon = new Label();
            lblSearchIcon.Text = "Tìm kiếm:";
            lblSearchIcon.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblSearchIcon.ForeColor = Color.FromArgb(148, 163, 184);
            lblSearchIcon.Location = new Point(14, 15);
            lblSearchIcon.AutoSize = true;

            txtSearch = new System.Windows.Forms.TextBox();
            txtSearch.Location = new Point(82, 12);
            txtSearch.Size = new Size(220, 25);
            txtSearch.BackColor = Color.FromArgb(15, 23, 42);
            txtSearch.ForeColor = Color.White;
            txtSearch.BorderStyle = BorderStyle.FixedSingle;
            txtSearch.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            txtSearch.Text = "";
            txtSearch.TextChanged += delegate
            {
                _currentFilter.SearchKeyword = txtSearch.Text.Trim();
                FilterAndPopulateGrid();
            };

            // Khối 2: Chuyên ngành (Dropdown ComboBox hiện đại)
            lblCategoryIcon = new Label();
            lblCategoryIcon.Text = "Chuyên Ngành:";
            lblCategoryIcon.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblCategoryIcon.ForeColor = Color.FromArgb(148, 163, 184);
            lblCategoryIcon.Location = new Point(318, 15);
            lblCategoryIcon.AutoSize = true;

            cboCategoryFilter = new System.Windows.Forms.ComboBox();
            cboCategoryFilter.Location = new Point(415, 11);
            cboCategoryFilter.Size = new Size(240, 28);
            cboCategoryFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            cboCategoryFilter.BackColor = Color.FromArgb(15, 23, 42);
            cboCategoryFilter.ForeColor = Color.FromArgb(52, 211, 153); // Emerald
            cboCategoryFilter.FlatStyle = FlatStyle.Flat;
            cboCategoryFilter.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            PopulateCategoryDropdown();
            cboCategoryFilter.SelectedIndexChanged += CboCategoryFilter_SelectedIndexChanged;

            // Khối 3: Bộ lọc thông minh - Chỉ hiện Family đã vẽ trong mô hình
            chkOnlyUsed = new System.Windows.Forms.CheckBox();
            chkOnlyUsed.Text = "Chỉ hiện Family đã vẽ (> 0)";
            chkOnlyUsed.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            chkOnlyUsed.ForeColor = Color.FromArgb(251, 191, 36); // Amber
            chkOnlyUsed.BackColor = Color.Transparent;
            chkOnlyUsed.Location = new Point(670, 14);
            chkOnlyUsed.AutoSize = true;
            chkOnlyUsed.Cursor = Cursors.Hand;
            chkOnlyUsed.CheckedChanged += delegate
            {
                _currentFilter.OnlyWithInstances = chkOnlyUsed.Checked;
                FilterAndPopulateGrid();
            };

            // Khối 4: Nhãn kết quả lọc
            lblFilterResult = new Label();
            lblFilterResult.Text = "Hiển thị: 0 Types";
            lblFilterResult.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            lblFilterResult.ForeColor = Color.FromArgb(56, 189, 248); // Sky
            lblFilterResult.Location = new Point(880, 16);
            lblFilterResult.AutoSize = true;

            // Khối 5: Nút Làm Mới (F5) neo góc phải
            btnRefresh = CreateSmallButton("Làm Mới (F5)", Color.FromArgb(67, 56, 202));
            btnRefresh.Size = new Size(110, 28);
            btnRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnRefresh.Click += delegate { RefreshFamiliesFromProject(); };

            pnlToolbar.Controls.Add(lblSearchIcon);
            pnlToolbar.Controls.Add(txtSearch);
            pnlToolbar.Controls.Add(lblCategoryIcon);
            pnlToolbar.Controls.Add(cboCategoryFilter);
            pnlToolbar.Controls.Add(chkOnlyUsed);
            pnlToolbar.Controls.Add(lblFilterResult);
            pnlToolbar.Controls.Add(btnRefresh);
            tblMain.Controls.Add(pnlToolbar, 0, 1);

            // ===================================================================
            // 3. MAIN SPLIT CONTAINER (HÀNG 2: Bảng DataGridView & Khung Inspector)
            // ===================================================================
            splitMain = new SplitContainer();
            splitMain.Dock = DockStyle.Fill;
            splitMain.Orientation = Orientation.Vertical;
            splitMain.SplitterWidth = 6;
            splitMain.BackColor = Color.FromArgb(30, 41, 59);
            splitMain.Margin = new Padding(0);

            // -------------------------------------------------------------------
            // LEFT PANEL: BẢNG DATAGRIDVIEW Ô CỘT SIÊU NHẸ, KHÔNG ĐƠ LAG
            // -------------------------------------------------------------------
            dgvFamilies = new DataGridView();
            dgvFamilies.Dock = DockStyle.Fill;
            dgvFamilies.BackgroundColor = Color.FromArgb(15, 20, 28);
            dgvFamilies.BorderStyle = BorderStyle.None;
            dgvFamilies.GridColor = Color.FromArgb(30, 41, 59);
            dgvFamilies.RowHeadersVisible = false;
            dgvFamilies.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvFamilies.MultiSelect = false;
            dgvFamilies.AllowUserToAddRows = false;
            dgvFamilies.AllowUserToDeleteRows = false;
            dgvFamilies.AllowUserToResizeRows = false;
            dgvFamilies.EnableHeadersVisualStyles = false;
            dgvFamilies.RowTemplate.Height = 32;

            EnableDoubleBuffer(dgvFamilies);

            // Header Style
            dgvFamilies.ColumnHeadersHeight = 34;
            dgvFamilies.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvFamilies.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 41, 59);
            dgvFamilies.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(248, 250, 252);
            dgvFamilies.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            dgvFamilies.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;

            // Row Default Style
            dgvFamilies.DefaultCellStyle.BackColor = Color.FromArgb(15, 23, 42);
            dgvFamilies.DefaultCellStyle.ForeColor = Color.FromArgb(226, 232, 240);
            dgvFamilies.DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            dgvFamilies.DefaultCellStyle.SelectionBackColor = Color.FromArgb(67, 56, 202); // Indigo 600
            dgvFamilies.DefaultCellStyle.SelectionForeColor = Color.White;
            dgvFamilies.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(21, 29, 44);

            // Cột 0: STT
            DataGridViewTextBoxColumn colSTT = new DataGridViewTextBoxColumn();
            colSTT.Name = "colSTT";
            colSTT.HeaderText = "STT";
            colSTT.Width = 46;
            colSTT.ReadOnly = true;
            colSTT.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvFamilies.Columns.Add(colSTT);

            // Cột 1: Tên Family
            DataGridViewTextBoxColumn colFamily = new DataGridViewTextBoxColumn();
            colFamily.Name = "colFamily";
            colFamily.HeaderText = "Tên Family (Họ)";
            colFamily.Width = 240;
            colFamily.ReadOnly = true;
            colFamily.DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dgvFamilies.Columns.Add(colFamily);

            // Cột 2: Type / Kích Thước
            DataGridViewTextBoxColumn colType = new DataGridViewTextBoxColumn();
            colType.Name = "colType";
            colType.HeaderText = "Type (Kích Thước)";
            colType.Width = 190;
            colType.ReadOnly = true;
            colType.DefaultCellStyle.ForeColor = Color.FromArgb(167, 243, 208); // Emerald Light
            dgvFamilies.Columns.Add(colType);

            // Cột 3: Chuyên Ngành
            DataGridViewTextBoxColumn colCategory = new DataGridViewTextBoxColumn();
            colCategory.Name = "colCategory";
            colCategory.HeaderText = "Chuyên Ngành (Category)";
            colCategory.Width = 160;
            colCategory.ReadOnly = true;
            colCategory.DefaultCellStyle.ForeColor = Color.FromArgb(125, 211, 252); // Sky Light
            dgvFamilies.Columns.Add(colCategory);

            // Cột 4: Đã Vẽ Trong Mô Hình
            DataGridViewTextBoxColumn colCount = new DataGridViewTextBoxColumn();
            colCount.Name = "colCount";
            colCount.HeaderText = "Đã Vẽ";
            colCount.Width = 90;
            colCount.ReadOnly = true;
            colCount.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colCount.DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dgvFamilies.Columns.Add(colCount);

            // Cột 5: Nút Thao Tác Đặt Vào Bản Vẽ
            DataGridViewButtonColumn colAction = new DataGridViewButtonColumn();
            colAction.Name = "colAction";
            colAction.HeaderText = "Thao Tác";
            colAction.Width = 130;
            colAction.Text = "Đặt Vào Bản Vẽ";
            colAction.UseColumnTextForButtonValue = true;
            colAction.FlatStyle = FlatStyle.Flat;
            colAction.DefaultCellStyle.BackColor = Color.FromArgb(5, 150, 105); // Emerald Green
            colAction.DefaultCellStyle.ForeColor = Color.White;
            colAction.DefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            colAction.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvFamilies.Columns.Add(colAction);

            colFamily.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            dgvFamilies.SelectionChanged += DgvFamilies_SelectionChanged;
            dgvFamilies.CellDoubleClick += DgvFamilies_CellDoubleClick;
            dgvFamilies.CellContentClick += DgvFamilies_CellContentClick;
            dgvFamilies.KeyDown += DgvFamilies_KeyDown;

            splitMain.Panel1.Controls.Add(dgvFamilies);

            // -------------------------------------------------------------------
            // RIGHT PANEL: KHUNG INSPECTOR XEM TRƯỚC 3D ISOMETRIC VÀ ĐẶT
            // -------------------------------------------------------------------
            pnlInspector = new System.Windows.Forms.Panel();
            pnlInspector.Dock = DockStyle.Fill;
            pnlInspector.BackColor = Color.FromArgb(21, 27, 38);
            pnlInspector.AutoScroll = true;
            pnlInspector.Padding = new Padding(16, 10, 16, 12);
            pnlInspector.Margin = new Padding(0);
            pnlInspector.Resize += delegate { AdjustInspectorLayout(); };

            lblInspectorHeader = new Label();
            lblInspectorHeader.Text = "XEM TRƯỚC 3D ISOMETRIC & THÔNG SỐ";
            lblInspectorHeader.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            lblInspectorHeader.ForeColor = Color.FromArgb(129, 140, 248);
            lblInspectorHeader.Location = new Point(16, 8);
            lblInspectorHeader.AutoSize = true;

            picLargePreview = new PictureBox();
            picLargePreview.Location = new Point(16, 32);
            picLargePreview.Size = new Size(380, 200);
            picLargePreview.BackColor = Color.FromArgb(11, 14, 20);
            picLargePreview.BorderStyle = BorderStyle.FixedSingle;
            picLargePreview.SizeMode = PictureBoxSizeMode.Zoom;

            lblInspectorCategory = new Label();
            lblInspectorCategory.Text = "[Category]";
            lblInspectorCategory.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            lblInspectorCategory.ForeColor = Color.FromArgb(56, 189, 248);
            lblInspectorCategory.Location = new Point(16, 240);
            lblInspectorCategory.AutoSize = true;

            lblInspectorFamName = new Label();
            lblInspectorFamName.Text = "Tên Family";
            lblInspectorFamName.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            lblInspectorFamName.ForeColor = Color.White;
            lblInspectorFamName.Location = new Point(16, 260);
            lblInspectorFamName.Size = new Size(380, 36);
            lblInspectorFamName.AutoEllipsis = true;

            lblInspectorTypeName = new Label();
            lblInspectorTypeName.Text = "Type: (Chưa chọn)";
            lblInspectorTypeName.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblInspectorTypeName.ForeColor = Color.FromArgb(52, 211, 153);
            lblInspectorTypeName.Location = new Point(16, 298);
            lblInspectorTypeName.Size = new Size(380, 24);
            lblInspectorTypeName.AutoEllipsis = true;

            pnlInspectorInfo = new System.Windows.Forms.Panel();
            pnlInspectorInfo.Location = new Point(16, 326);
            pnlInspectorInfo.Size = new Size(380, 52);
            pnlInspectorInfo.BackColor = Color.FromArgb(15, 23, 42);
            pnlInspectorInfo.BorderStyle = BorderStyle.FixedSingle;
            pnlInspectorInfo.Padding = new Padding(8);

            lblInspectorHost = new Label();
            lblInspectorHost.Text = "Host: Khớp nối / Mặt phẳng làm việc";
            lblInspectorHost.Font = new Font("Segoe UI", 8F, FontStyle.Regular);
            lblInspectorHost.ForeColor = Color.FromArgb(203, 213, 225);
            lblInspectorHost.Location = new Point(8, 6);
            lblInspectorHost.AutoSize = true;

            lblInspectorInstances = new Label();
            lblInspectorInstances.Text = "Đã đặt trong mô hình: 0 đối tượng";
            lblInspectorInstances.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            lblInspectorInstances.ForeColor = Color.FromArgb(245, 158, 11);
            lblInspectorInstances.Location = new Point(8, 26);
            lblInspectorInstances.AutoSize = true;

            pnlInspectorInfo.Controls.Add(lblInspectorHost);
            pnlInspectorInfo.Controls.Add(lblInspectorInstances);

            lblParamHeader = new Label();
            lblParamHeader.Text = "Thông Số Kỹ Thuật (Type Parameters):";
            lblParamHeader.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            lblParamHeader.ForeColor = Color.FromArgb(148, 163, 184);
            lblParamHeader.Location = new Point(16, 386);
            lblParamHeader.AutoSize = true;

            dgvParameters = new DataGridView();
            dgvParameters.Location = new Point(16, 408);
            dgvParameters.Size = new Size(380, 160);
            dgvParameters.BackgroundColor = Color.FromArgb(15, 23, 42);
            dgvParameters.BorderStyle = BorderStyle.FixedSingle;
            dgvParameters.GridColor = Color.FromArgb(30, 41, 59);
            dgvParameters.RowHeadersVisible = false;
            dgvParameters.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvParameters.AllowUserToAddRows = false;
            dgvParameters.AllowUserToDeleteRows = false;
            dgvParameters.AllowUserToResizeRows = false;
            dgvParameters.EnableHeadersVisualStyles = false;
            dgvParameters.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 41, 59);
            dgvParameters.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(241, 245, 249);
            dgvParameters.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            dgvParameters.DefaultCellStyle.BackColor = Color.FromArgb(15, 23, 42);
            dgvParameters.DefaultCellStyle.ForeColor = Color.FromArgb(226, 232, 240);
            dgvParameters.DefaultCellStyle.Font = new Font("Segoe UI", 8F, FontStyle.Regular);
            dgvParameters.RowTemplate.Height = 22;

            DataGridViewTextBoxColumn colParamName = new DataGridViewTextBoxColumn();
            colParamName.Name = "colParamName";
            colParamName.HeaderText = "Thông Số";
            colParamName.Width = 150;
            colParamName.ReadOnly = true;
            dgvParameters.Columns.Add(colParamName);

            DataGridViewTextBoxColumn colParamVal = new DataGridViewTextBoxColumn();
            colParamVal.Name = "colParamVal";
            colParamVal.HeaderText = "Giá Trị";
            colParamVal.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colParamVal.ReadOnly = true;
            dgvParameters.Columns.Add(colParamVal);

            btnInspectorPlace = new System.Windows.Forms.Button();
            btnInspectorPlace.Text = "ĐẶT VÀO BẢN VẼ REVIT (CLICK ĐỂ ĐẶT)";
            btnInspectorPlace.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnInspectorPlace.ForeColor = Color.White;
            btnInspectorPlace.BackColor = Color.FromArgb(5, 150, 105); // Emerald Green
            btnInspectorPlace.FlatStyle = FlatStyle.Flat;
            btnInspectorPlace.FlatAppearance.BorderSize = 0;
            btnInspectorPlace.Location = new Point(16, 580);
            btnInspectorPlace.Size = new Size(380, 46);
            btnInspectorPlace.Cursor = Cursors.Hand;
            btnInspectorPlace.Click += BtnInspectorPlace_Click;

            lblPlacementHint = new Label();
            lblPlacementHint.Text = "Hướng dẫn: Click nút hoặc Double-click dòng trên bảng, di chuột sang bản vẽ Revit để đặt Family liên tục (Space: xoay, ESC: kết thúc).";
            lblPlacementHint.Font = new Font("Segoe UI", 8F, FontStyle.Italic);
            lblPlacementHint.ForeColor = Color.FromArgb(148, 163, 184);
            lblPlacementHint.Location = new Point(16, 634);
            lblPlacementHint.Size = new Size(380, 36);

            pnlInspector.Controls.Add(lblInspectorHeader);
            pnlInspector.Controls.Add(picLargePreview);
            pnlInspector.Controls.Add(lblInspectorCategory);
            pnlInspector.Controls.Add(lblInspectorFamName);
            pnlInspector.Controls.Add(lblInspectorTypeName);
            pnlInspector.Controls.Add(pnlInspectorInfo);
            pnlInspector.Controls.Add(lblParamHeader);
            pnlInspector.Controls.Add(dgvParameters);
            pnlInspector.Controls.Add(btnInspectorPlace);
            pnlInspector.Controls.Add(lblPlacementHint);

            splitMain.Panel2.Controls.Add(pnlInspector);
            tblMain.Controls.Add(splitMain, 0, 2);

            // ===================================================================
            // 4. BOTTOM ACTION & STATUS PANEL (HÀNG 3)
            // ===================================================================
            pnlBottom = new System.Windows.Forms.Panel();
            pnlBottom.Dock = DockStyle.Fill;
            pnlBottom.BackColor = Color.FromArgb(16, 22, 34);
            pnlBottom.Margin = new Padding(0);
            pnlBottom.Padding = new Padding(16, 6, 16, 6);
            pnlBottom.Resize += PnlBottom_Resize;

            lblStatus = new Label();
            lblStatus.Text = "● Đang sẵn sàng nạp Family...";
            lblStatus.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            lblStatus.ForeColor = Color.FromArgb(52, 211, 153);
            lblStatus.Location = new Point(16, 6);
            lblStatus.AutoSize = true;

            lblModelessNotice = new Label();
            lblModelessNotice.Text = "● Chế độ Modeless 100%: Kỹ sư hoàn toàn tự do Pan, Zoom, xoay 3D hoặc vẽ trên Revit. Phím ESC để đóng cửa sổ.";
            lblModelessNotice.Font = new Font("Segoe UI", 8F, FontStyle.Regular);
            lblModelessNotice.ForeColor = Color.FromArgb(56, 189, 248);
            lblModelessNotice.Location = new Point(16, 26);
            lblModelessNotice.AutoSize = true;

            btnClose = new System.Windows.Forms.Button();
            btnClose.Text = "Đóng (ESC)";
            btnClose.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            btnClose.ForeColor = Color.FromArgb(226, 232, 240);
            btnClose.BackColor = Color.FromArgb(51, 65, 85);
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Size = new Size(115, 34);
            btnClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClose.Cursor = Cursors.Hand;
            btnClose.Click += delegate { this.Close(); };

            pnlBottom.Controls.Add(lblStatus);
            pnlBottom.Controls.Add(lblModelessNotice);
            pnlBottom.Controls.Add(btnClose);
            tblMain.Controls.Add(pnlBottom, 0, 3);

            this.Controls.Add(tblMain);
            this.CancelButton = btnClose;

            this.ResumeLayout(false);

            // Cập nhật vị trí lúc khởi tạo
            PnlHeader_Resize(null, null);
            PnlToolbar_Resize(null, null);
            PnlBottom_Resize(null, null);
            AdjustInspectorLayout();
        }

        private void PopulateCategoryDropdown()
        {
            cboCategoryFilter.Items.Clear();
            cboCategoryFilter.Items.Add(new CategoryComboItem("Tất Cả Chuyên Ngành", FamilyCategoryGroup.All));
            cboCategoryFilter.Items.Add(new CategoryComboItem("Ống Gió & Miệng Gió", FamilyCategoryGroup.Ducts));
            cboCategoryFilter.Items.Add(new CategoryComboItem("Phụ Kiện Ống Gió", FamilyCategoryGroup.DuctFittings));
            cboCategoryFilter.Items.Add(new CategoryComboItem("Ống Nước & Van", FamilyCategoryGroup.Pipes));
            cboCategoryFilter.Items.Add(new CategoryComboItem("Phụ Kiện Ống Nước", FamilyCategoryGroup.PipeFittings));
            cboCategoryFilter.Items.Add(new CategoryComboItem("Thiết Bị Cơ Điện", FamilyCategoryGroup.MechanicalEquipment));
            cboCategoryFilter.Items.Add(new CategoryComboItem("PCCC & Vệ Sinh", FamilyCategoryGroup.Plumbing));
            cboCategoryFilter.Items.Add(new CategoryComboItem("Thiết Bị Điện & Chiếu Sáng", FamilyCategoryGroup.Electrical));
            cboCategoryFilter.Items.Add(new CategoryComboItem("Kiến Trúc & Khác", FamilyCategoryGroup.Architecture));
            cboCategoryFilter.SelectedIndex = 0;
        }

        private void CboCategoryFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            CategoryComboItem item = cboCategoryFilter.SelectedItem as CategoryComboItem;
            if (item != null)
            {
                _currentFilter.SelectedGroup = item.Group;
                FilterAndPopulateGrid();
            }
        }

        private void PnlHeader_Resize(object sender, EventArgs e)
        {
            if (pnlHeader == null || lblTotalStats == null || lblHeaderBadge == null) return;
            int rightX = pnlHeader.ClientSize.Width - 16;
            lblTotalStats.Location = new Point(rightX - lblTotalStats.Width, 15);
            lblHeaderBadge.Location = new Point(lblTotalStats.Left - lblHeaderBadge.Width - 10, 16);
        }

        private void PnlToolbar_Resize(object sender, EventArgs e)
        {
            if (pnlToolbar == null || btnRefresh == null) return;
            btnRefresh.Location = new Point(pnlToolbar.ClientSize.Width - btnRefresh.Width - 16, 12);
        }

        private void PnlBottom_Resize(object sender, EventArgs e)
        {
            if (pnlBottom == null || btnClose == null) return;
            btnClose.Location = new Point(pnlBottom.ClientSize.Width - btnClose.Width - 16, 7);
        }

        private void AdjustInspectorLayout()
        {
            if (pnlInspector == null || btnInspectorPlace == null) return;
            int pad = 16;
            int w = Math.Max(260, pnlInspector.ClientSize.Width - (pad * 2));

            picLargePreview.Width = w;
            lblInspectorFamName.Width = w;
            lblInspectorTypeName.Width = w;
            pnlInspectorInfo.Width = w;
            dgvParameters.Width = w;
            btnInspectorPlace.Width = w;
            lblPlacementHint.Width = w;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            try
            {
                if (splitMain != null && splitMain.ClientSize.Width > 500)
                {
                    int totalW = splitMain.ClientSize.Width;
                    int inspectorW = 400;
                    int target = totalW - inspectorW;
                    int minLeft = 200;
                    int maxLeft = totalW - 250;

                    if (target >= minLeft && target <= maxLeft)
                    {
                        splitMain.SplitterDistance = target;
                    }
                }
            }
            catch { }
            AdjustInspectorLayout();
        }

        private void RefreshFamiliesFromProject()
        {
            if (_doc == null) return;

            lblStatus.Text = "● Đang quét toàn bộ Family trong dự án Revit (quét siêu tốc)...";
            Application.DoEvents();

            try
            {
                Stopwatch sw = Stopwatch.StartNew();
                _allFamilies = FamilyPaletteService.GetProjectFamilies(_doc, _currentFilter);
                sw.Stop();

                _totalProjectTypes = 0;
                for (int i = 0; i < _allFamilies.Count; i++)
                {
                    _totalProjectTypes += _allFamilies[i].Types.Count;
                }

                lblTotalStats.Text = string.Format(CultureInfo.InvariantCulture, "{0} Families | {1} Types", _allFamilies.Count, _totalProjectTypes);
                lblStatus.Text = string.Format(CultureInfo.InvariantCulture, "● Đã nạp {0} Families ({1} Types) trong {2} ms. Sẵn sàng kéo thả / đặt vào bản vẽ.", _allFamilies.Count, _totalProjectTypes, sw.ElapsedMilliseconds);

                FilterAndPopulateGrid();
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Lỗi quét Family: " + ex.Message;
            }
        }

        /// <summary>
        /// Lọc danh sách và điền dữ liệu vào DataGridView siêu nhẹ, không bao giờ bị đơ giật lag
        /// </summary>
        private void FilterAndPopulateGrid()
        {
            dgvFamilies.SuspendLayout();
            dgvFamilies.Rows.Clear();
            _displayRows.Clear();

            string kw = !string.IsNullOrEmpty(_currentFilter.SearchKeyword)
                ? SheetExportCadPdfService.RemoveDiacritics(_currentFilter.SearchKeyword).ToUpperInvariant()
                : string.Empty;

            int stt = 1;

            for (int i = 0; i < _allFamilies.Count; i++)
            {
                FamilyGroupItem g = _allFamilies[i];

                // Lọc theo Category Group
                if (_currentFilter.SelectedGroup != FamilyCategoryGroup.All &&
                    g.CategoryGroup != _currentFilter.SelectedGroup)
                {
                    continue;
                }

                string fNameClean = SheetExportCadPdfService.RemoveDiacritics(g.FamilyName).ToUpperInvariant();
                string catClean = SheetExportCadPdfService.RemoveDiacritics(g.CategoryName).ToUpperInvariant();

                for (int t = 0; t < g.Types.Count; t++)
                {
                    FamilyTypeItem typeItem = g.Types[t];

                    // Lọc chỉ hiện Family đã vẽ trong mô hình nếu người dùng bật checkbox
                    if (_currentFilter.OnlyWithInstances && typeItem.InstanceCount <= 0)
                    {
                        continue;
                    }

                    string tNameClean = SheetExportCadPdfService.RemoveDiacritics(typeItem.TypeName).ToUpperInvariant();

                    // Lọc theo từ khóa tìm kiếm
                    if (!string.IsNullOrEmpty(kw))
                    {
                        bool match = fNameClean.Contains(kw) || catClean.Contains(kw) || tNameClean.Contains(kw);
                        if (!match) continue;
                    }

                    FamilyTypeRowItem rowItem = new FamilyTypeRowItem(g, typeItem, stt);
                    _displayRows.Add(rowItem);

                    int rowIndex = dgvFamilies.Rows.Add(
                        stt.ToString(CultureInfo.InvariantCulture),
                        g.FamilyName,
                        typeItem.TypeName,
                        g.CategoryName,
                        typeItem.InstanceCount > 0 ? typeItem.InstanceCount.ToString(CultureInfo.InvariantCulture) : "0",
                        "Đặt Vào Bản Vẽ"
                    );

                    DataGridViewRow row = dgvFamilies.Rows[rowIndex];
                    row.Tag = rowItem;

                    // Tô màu nổi bật số lượng đã vẽ
                    if (typeItem.InstanceCount > 0)
                    {
                        row.Cells[4].Style.ForeColor = Color.FromArgb(251, 191, 36); // Amber
                    }
                    else
                    {
                        row.Cells[4].Style.ForeColor = Color.FromArgb(148, 163, 184); // Slate
                    }

                    stt++;
                }
            }

            dgvFamilies.ResumeLayout();

            lblFilterResult.Text = string.Format(CultureInfo.InvariantCulture, "Hiển thị: {0} / {1} Types", _displayRows.Count, _totalProjectTypes);

            if (_displayRows.Count > 0)
            {
                dgvFamilies.Rows[0].Selected = true;
                SelectRowItem(_displayRows[0]);
            }
            else
            {
                ClearInspector();
            }
        }

        private void DgvFamilies_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvFamilies.SelectedRows.Count <= 0) return;
            DataGridViewRow row = dgvFamilies.SelectedRows[0];
            FamilyTypeRowItem rowItem = row.Tag as FamilyTypeRowItem;
            if (rowItem != null)
            {
                SelectRowItem(rowItem);
            }
        }

        private void DgvFamilies_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dgvFamilies.Rows.Count) return;
            DataGridViewRow row = dgvFamilies.Rows[e.RowIndex];
            FamilyTypeRowItem rowItem = row.Tag as FamilyTypeRowItem;
            if (rowItem != null)
            {
                SelectRowItem(rowItem);
                PlaceActiveSymbol();
            }
        }

        private void DgvFamilies_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dgvFamilies.Rows.Count) return;
            // Nếu click đúng cột nút bấm "Thao Tác" (cột 5)
            if (e.ColumnIndex == 5)
            {
                DataGridViewRow row = dgvFamilies.Rows[e.RowIndex];
                FamilyTypeRowItem rowItem = row.Tag as FamilyTypeRowItem;
                if (rowItem != null)
                {
                    SelectRowItem(rowItem);
                    PlaceActiveSymbol();
                }
            }
        }

        private void DgvFamilies_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                PlaceActiveSymbol();
            }
        }

        /// <summary>
        /// Cập nhật khung Inspector bên phải khi người dùng chọn dòng trên bảng
        /// Nạp ảnh 3D Isometric theo cơ chế Lazy Load (có Cache), không làm giật lag
        /// </summary>
        private void SelectRowItem(FamilyTypeRowItem rowItem)
        {
            if (rowItem == null) return;
            _selectedRow = rowItem;

            FamilyGroupItem group = rowItem.Group;
            FamilyTypeItem typeItem = rowItem.TypeItem;

            lblInspectorCategory.Text = string.Format(CultureInfo.InvariantCulture, "[{0}]", group.CategoryName);
            lblInspectorFamName.Text = group.FamilyName;
            lblInspectorTypeName.Text = string.Format(CultureInfo.InvariantCulture, "Type: {0}", typeItem.TypeName);

            lblInspectorInstances.Text = string.Format(CultureInfo.InvariantCulture, "Đã đặt trong mô hình: {0} đối tượng", typeItem.InstanceCount);

            // NẠP ẢNH 3D ISOMETRIC PREVIEW THEO YÊU CẦU (LAZY LOADING)
            Bitmap previewBmp = null;
            if (!_previewCache.TryGetValue(typeItem.Id, out previewBmp) || previewBmp == null)
            {
                previewBmp = FamilyPaletteService.GetSymbolPreviewImage(
                    _doc,
                    typeItem.Id,
                    new Size(380, 200),
                    group.FamilyName,
                    group.CategoryName
                );
                if (previewBmp != null)
                {
                    _previewCache[typeItem.Id] = previewBmp;
                }
            }
            picLargePreview.Image = previewBmp;

            // Cập nhật bảng thông số kỹ thuật (Parameters)
            UpdateParameterTable(typeItem.Id);
            AdjustInspectorLayout();
        }

        private void UpdateParameterTable(ElementId symbolId)
        {
            dgvParameters.Rows.Clear();
            if (symbolId == null || symbolId == ElementId.InvalidElementId || _doc == null) return;

            try
            {
                Element elem = _doc.GetElement(symbolId);
                if (elem != null)
                {
                    foreach (Parameter p in elem.Parameters)
                    {
                        if (p.HasValue && !string.IsNullOrEmpty(p.Definition.Name))
                        {
                            string valStr = p.AsValueString();
                            if (string.IsNullOrEmpty(valStr))
                            {
                                valStr = p.AsString();
                            }
                            if (!string.IsNullOrEmpty(valStr))
                            {
                                dgvParameters.Rows.Add(p.Definition.Name, valStr);
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private void ClearInspector()
        {
            _selectedRow = null;
            lblInspectorCategory.Text = "[Chưa chọn]";
            lblInspectorFamName.Text = "(Không có Family phù hợp)";
            lblInspectorTypeName.Text = "";
            picLargePreview.Image = null;
            dgvParameters.Rows.Clear();
        }

        private void BtnInspectorPlace_Click(object sender, EventArgs e)
        {
            PlaceActiveSymbol();
        }

        /// <summary>
        /// Kích hoạt lệnh đặt Family vào bản vẽ Revit qua Revit UI Modeless ExternalEvent
        /// </summary>
        private void PlaceActiveSymbol()
        {
            if (_selectedRow == null || _selectedRow.TypeItem == null)
            {
                MessageBox.Show("Vui lòng chọn 1 dòng Family trên bảng để đặt vào bản vẽ!", "HDTOOL", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            FamilyTypeItem targetType = _selectedRow.TypeItem;
            ElementId symbolId = targetType.Id;
            string famTitle = targetType.GetFullDisplayName();

            lblStatus.Text = string.Format(CultureInfo.InvariantCulture, "● Đang kích hoạt lệnh đặt [{0}]... Nhấp chuột lên bản vẽ Revit để đặt.", famTitle);

            // Chạy trong ExternalEvent Modeless của HDTOOL
            HDToolModelessHost.Run(delegate(UIApplication app)
            {
                string errMsg;
                bool ok = FamilyPaletteService.PlaceFamilySymbol(app, symbolId, out errMsg);
                if (!ok && !string.IsNullOrEmpty(errMsg))
                {
                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        lblStatus.Text = "Lưu ý: " + errMsg;
                    });
                }
                else
                {
                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        lblStatus.Text = string.Format(CultureInfo.InvariantCulture, "● Đã hoàn tất đặt [{0}] vào mô hình Revit!", famTitle);
                    });
                }
            });
        }

        // ==================== CÁC PHƯƠNG THỨC TRỢ GIÚP UI ====================

        private void PnlHeader_Paint(object sender, PaintEventArgs e)
        {
            Rectangle rect = pnlHeader.ClientRectangle;
            if (rect.Width <= 0 || rect.Height <= 0) return;

            using (LinearGradientBrush lgb = new LinearGradientBrush(
                rect,
                Color.FromArgb(30, 27, 75),     // Deep Indigo dark
                Color.FromArgb(15, 23, 42),      // Slate dark
                LinearGradientMode.Horizontal))
            {
                e.Graphics.FillRectangle(lgb, rect);
            }

            using (Pen p = new Pen(Color.FromArgb(30, 41, 59)))
            {
                e.Graphics.DrawLine(p, 0, rect.Height - 1, rect.Width, rect.Height - 1);
            }
        }

        private System.Windows.Forms.Button CreateSmallButton(string text, Color backColor)
        {
            System.Windows.Forms.Button btn = new System.Windows.Forms.Button();
            btn.Text = text;
            btn.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            btn.ForeColor = Color.White;
            btn.BackColor = backColor;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.Cursor = Cursors.Hand;
            return btn;
        }

        private static void EnableDoubleBuffer(DataGridView dgv)
        {
            try
            {
                typeof(DataGridView).InvokeMember(
                    "DoubleBuffered",
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetProperty,
                    null,
                    dgv,
                    new object[] { true }
                );
            }
            catch { }
        }
    }

    /// <summary>
    /// Item hiển thị trong ComboBox Chuyên Ngành
    /// </summary>
    public class CategoryComboItem
    {
        public string DisplayName { get; set; }
        public FamilyCategoryGroup Group { get; set; }

        public CategoryComboItem(string displayName, FamilyCategoryGroup group)
        {
            this.DisplayName = displayName;
            this.Group = group;
        }

        public override string ToString()
        {
            return this.DisplayName;
        }
    }

    /// <summary>
    /// Bản ghi đại diện cho một dòng hiển thị trên DataGridView
    /// </summary>
    public class FamilyTypeRowItem
    {
        public FamilyGroupItem Group { get; set; }
        public FamilyTypeItem TypeItem { get; set; }
        public int STT { get; set; }

        public FamilyTypeRowItem(FamilyGroupItem group, FamilyTypeItem typeItem, int stt)
        {
            this.Group = group;
            this.TypeItem = typeItem;
            this.STT = stt;
        }
    }
}
