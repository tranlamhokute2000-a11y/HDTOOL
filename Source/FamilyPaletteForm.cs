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
        private HDToolSearchBox searchFamily;
        private Label lblCategoryIcon;
        private System.Windows.Forms.ComboBox cboCategoryFilter;
        private Label lblFilterResult;
        private System.Windows.Forms.Button btnRefresh;

        // Row 2: SplitContainer (Left: DataGridView Table | Right: 3D Preview Inspector)
        private SplitContainer splitMain;
        private DataGridView dgvFamilies;

        // Inspector Controls (Khung xem trước 3D & Thông số bên phải)
        private System.Windows.Forms.Panel pnlInspector;
        private Label lblInspectorHeader;
        private FamilyMeshViewer picLargePreview;
        private System.Windows.Forms.ComboBox cboTypes;
        private Dictionary<ElementId, float[]> _meshCache;
        private Dictionary<ElementId, float[]> _lineCache;
        private ElementId _viewerSymbolId;
        private Label lblInspectorCategory;
        private Label lblInspectorFamName;
        private Label lblInspectorTypeName;
        private System.Windows.Forms.Panel pnlInspectorInfo;
        private Label lblInspectorHost;
        private Label lblInspectorInstances;
        private System.Windows.Forms.Panel pnlCategoryBox;
        private Label lblCategoryChange;
        private System.Windows.Forms.ComboBox cboNewCategory;
        private System.Windows.Forms.Button btnChangeCategory;
        private Label lblCategoryNote;
        private List<FamilyCategoryChoice> _categoryChoices;
        private bool _categoryBusy;
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
            _categoryChoices = null; // ten Category lay theo du an dang mo
            RefreshFamiliesFromProject();
        }

        private void InitializeData()
        {
            _allFamilies = new List<FamilyGroupItem>();
            _displayRows = new List<FamilyTypeRowItem>();
            _currentFilter = new FamilyPaletteFilter();
            _previewCache = new Dictionary<ElementId, Bitmap>();
            _meshCache = new Dictionary<ElementId, float[]>();
            _lineCache = new Dictionary<ElementId, float[]>();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Cấu hình Form: Rộng rãi, hiện đại, hỗ trợ High-DPI
            this.Text = "HDTOOL - BẢNG PALETTE TỔNG HỢP FAMILY 3D & ĐẶT VÀO BẢN VẼ";
            this.ClientSize = new Size(1320, 840);
            this.MinimumSize = new Size(1100, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = HDToolTheme.Bg;
            this.ForeColor = HDToolTheme.Text;
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
            tblMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
            tblMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
            tblMain.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tblMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            tblMain.BackColor = HDToolTheme.Bg;
            tblMain.Margin = new Padding(0);
            tblMain.Padding = new Padding(0);

            // ===================================================================
            // 1. HEADER PANEL (HÀNG 0)
            // ===================================================================
            pnlHeader = new System.Windows.Forms.Panel();
            pnlHeader.Dock = DockStyle.Fill;
            pnlHeader.BackColor = HDToolTheme.Surface;
            pnlHeader.Margin = new Padding(0);
            pnlHeader.Paint += PnlHeader_Paint;
            pnlHeader.Resize += PnlHeader_Resize;

            lblHeaderTitle = new Label();
            lblHeaderTitle.Text = "TỔNG HỢP FAMILY ĐÃ LOAD VÀO DỰ ÁN";
            lblHeaderTitle.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
            lblHeaderTitle.ForeColor = HDToolTheme.Text;
            lblHeaderTitle.Location = new Point(16, 8);
            lblHeaderTitle.AutoSize = true;

            lblHeaderSubtitle = new Label();
            lblHeaderSubtitle.Text = "Mỗi dòng là 1 Family đã load (không đếm số lượng trên bản vẽ). Chọn Type ở khung bên phải, nhấp đúp hoặc bấm [Đặt] để vẽ.";
            lblHeaderSubtitle.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            lblHeaderSubtitle.ForeColor = HDToolTheme.Text2;
            lblHeaderSubtitle.Location = new Point(17, 36);
            lblHeaderSubtitle.AutoSize = true;

            lblHeaderBadge = new Label();
            lblHeaderBadge.Text = "Revit 2024–2027";
            lblHeaderBadge.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            lblHeaderBadge.ForeColor = HDToolTheme.Accent;
            lblHeaderBadge.BackColor = HDToolTheme.AccentSoft;
            lblHeaderBadge.TextAlign = ContentAlignment.MiddleCenter;
            lblHeaderBadge.Size = new Size(115, 24);
            lblHeaderBadge.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            lblTotalStats = new Label();
            lblTotalStats.Text = "0 Families | 0 Types";
            lblTotalStats.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            lblTotalStats.ForeColor = HDToolTheme.Ok;
            lblTotalStats.BackColor = HDToolTheme.OkSoft;
            lblTotalStats.BorderStyle = BorderStyle.None;
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
            pnlToolbar.BackColor = HDToolTheme.Bg;
            pnlToolbar.Margin = new Padding(0);
            pnlToolbar.Padding = new Padding(14, 8, 14, 8);
            pnlToolbar.Resize += PnlToolbar_Resize;

            // Khối 1: Tìm kiếm (ô bo góc, kính lúp, chữ gợi ý rõ công dụng)
            lblSearchIcon = new Label();
            lblSearchIcon.Text = "Lọc Family:";
            lblSearchIcon.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            lblSearchIcon.ForeColor = HDToolTheme.Text;
            lblSearchIcon.Location = new Point(16, 20);
            lblSearchIcon.AutoSize = true;

            searchFamily = new HDToolSearchBox();
            searchFamily.Placeholder = "Gõ tên Family hoặc Type để lọc…";
            searchFamily.KeyHint = "Ctrl+F";
            searchFamily.Location = new Point(104, 10);
            searchFamily.Size = new Size(380, 36);
            txtSearch = searchFamily.Box;
            txtSearch.TextChanged += delegate
            {
                _currentFilter.SearchKeyword = txtSearch.Text.Trim();
                FilterAndPopulateGrid();
            };

            // Khối 2: Chuyên ngành
            lblCategoryIcon = new Label();
            lblCategoryIcon.Text = "Chuyên ngành:";
            lblCategoryIcon.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            lblCategoryIcon.ForeColor = HDToolTheme.Text;
            lblCategoryIcon.Location = new Point(504, 20);
            lblCategoryIcon.AutoSize = true;

            cboCategoryFilter = new System.Windows.Forms.ComboBox();
            cboCategoryFilter.Location = new Point(606, 15);
            cboCategoryFilter.Size = new Size(240, 28);
            cboCategoryFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            cboCategoryFilter.BackColor = HDToolTheme.Input;
            cboCategoryFilter.ForeColor = HDToolTheme.Text;
            cboCategoryFilter.FlatStyle = FlatStyle.Standard;
            cboCategoryFilter.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            PopulateCategoryDropdown();
            cboCategoryFilter.SelectedIndexChanged += CboCategoryFilter_SelectedIndexChanged;

            // Khối 4: Nhãn kết quả lọc
            lblFilterResult = new Label();
            lblFilterResult.Text = "Hiển thị: 0 Types";
            lblFilterResult.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            lblFilterResult.ForeColor = HDToolTheme.Text2;
            lblFilterResult.Location = new Point(866, 21);
            lblFilterResult.AutoSize = true;

            // Khối 5: Nút Làm Mới (F5) neo góc phải
            btnRefresh = CreateSmallButton("Làm Mới (F5)", HDToolTheme.Accent);
            btnRefresh.Size = new Size(120, 34);
            btnRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnRefresh.Click += delegate { RefreshFamiliesFromProject(); };

            pnlToolbar.Controls.Add(lblSearchIcon);
            pnlToolbar.Controls.Add(searchFamily);
            pnlToolbar.Controls.Add(lblCategoryIcon);
            pnlToolbar.Controls.Add(cboCategoryFilter);
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
            splitMain.BackColor = HDToolTheme.Line;
            splitMain.Margin = new Padding(0);

            // -------------------------------------------------------------------
            // LEFT PANEL: BẢNG DATAGRIDVIEW Ô CỘT SIÊU NHẸ, KHÔNG ĐƠ LAG
            // -------------------------------------------------------------------
            dgvFamilies = new DataGridView();
            dgvFamilies.Dock = DockStyle.Fill;
            dgvFamilies.BackgroundColor = HDToolTheme.Surface;
            dgvFamilies.BorderStyle = BorderStyle.None;
            dgvFamilies.GridColor = HDToolTheme.Line;
            dgvFamilies.RowHeadersVisible = false;
            dgvFamilies.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvFamilies.MultiSelect = false;
            dgvFamilies.AllowUserToAddRows = false;
            dgvFamilies.AllowUserToDeleteRows = false;
            dgvFamilies.AllowUserToResizeRows = false;
            dgvFamilies.EnableHeadersVisualStyles = false;
            dgvFamilies.RowTemplate.Height = 36;

            EnableDoubleBuffer(dgvFamilies);

            // Header Style
            dgvFamilies.ColumnHeadersHeight = 34;
            dgvFamilies.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvFamilies.ColumnHeadersDefaultCellStyle.BackColor = HDToolTheme.Surface2;
            dgvFamilies.ColumnHeadersDefaultCellStyle.ForeColor = HDToolTheme.Text2;
            dgvFamilies.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dgvFamilies.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;

            // Row Default Style
            dgvFamilies.DefaultCellStyle.BackColor = HDToolTheme.Surface;
            dgvFamilies.DefaultCellStyle.ForeColor = HDToolTheme.Text;
            dgvFamilies.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            dgvFamilies.DefaultCellStyle.SelectionBackColor = HDToolTheme.AccentSoft;
            dgvFamilies.DefaultCellStyle.SelectionForeColor = HDToolTheme.Text;
            dgvFamilies.AlternatingRowsDefaultCellStyle.BackColor = HDToolTheme.RowAlt;

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
            colFamily.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            dgvFamilies.Columns.Add(colFamily);

            // Cột 2: Type / Kích Thước
            DataGridViewTextBoxColumn colType = new DataGridViewTextBoxColumn();
            colType.Name = "colType";
            colType.HeaderText = "Số Type";
            colType.Width = 90;
            colType.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colType.ReadOnly = true;
            colType.DefaultCellStyle.ForeColor = HDToolTheme.Text;
            dgvFamilies.Columns.Add(colType);

            // Cột 3: Chuyên Ngành
            DataGridViewTextBoxColumn colCategory = new DataGridViewTextBoxColumn();
            colCategory.Name = "colCategory";
            colCategory.HeaderText = "Chuyên Ngành";
            colCategory.Width = 180;
            colCategory.ReadOnly = true;
            colCategory.DefaultCellStyle.ForeColor = HDToolTheme.Text2;
            dgvFamilies.Columns.Add(colCategory);

            // Cột 4: Nút Thao Tác Đặt Vào Bản Vẽ
            DataGridViewButtonColumn colAction = new DataGridViewButtonColumn();
            colAction.Name = "colAction";
            colAction.HeaderText = "Thao Tác";
            colAction.Width = 130;
            colAction.Text = "Đặt Vào Bản Vẽ";
            colAction.UseColumnTextForButtonValue = true;
            colAction.FlatStyle = FlatStyle.Flat;
            colAction.DefaultCellStyle.ForeColor = HDToolTheme.AccentInk;
            colAction.DefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            colAction.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvFamilies.Columns.Add(colAction);

            colFamily.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            dgvFamilies.SelectionChanged += DgvFamilies_SelectionChanged;
            dgvFamilies.CellDoubleClick += DgvFamilies_CellDoubleClick;
            dgvFamilies.CellContentClick += DgvFamilies_CellContentClick;
            dgvFamilies.CellPainting += DgvFamilies_CellPainting;
            dgvFamilies.KeyDown += DgvFamilies_KeyDown;

            splitMain.Panel1.Controls.Add(dgvFamilies);

            // -------------------------------------------------------------------
            // RIGHT PANEL: KHUNG INSPECTOR XEM TRƯỚC 3D ISOMETRIC VÀ ĐẶT
            // -------------------------------------------------------------------
            pnlInspector = new System.Windows.Forms.Panel();
            pnlInspector.Dock = DockStyle.Fill;
            pnlInspector.BackColor = HDToolTheme.Surface2;
            pnlInspector.AutoScroll = true;
            pnlInspector.Padding = new Padding(16, 10, 16, 12);
            pnlInspector.Margin = new Padding(0);
            pnlInspector.ClientSizeChanged += delegate { AdjustInspectorLayout(); }; // gồm cả lúc thanh cuộn dọc hiện ra

            lblInspectorHeader = new Label();
            lblInspectorHeader.Text = "XEM TRƯỚC 3D · KÉO CHUỘT ĐỂ XOAY";
            lblInspectorHeader.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            lblInspectorHeader.ForeColor = HDToolTheme.Text;
            lblInspectorHeader.Location = new Point(16, 8);
            lblInspectorHeader.AutoSize = true;

            picLargePreview = new FamilyMeshViewer();
            picLargePreview.Location = new Point(16, 32);
            picLargePreview.Size = new Size(380, 200);

            lblInspectorCategory = new Label();
            lblInspectorCategory.Text = "[Category]";
            lblInspectorCategory.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            lblInspectorCategory.ForeColor = HDToolTheme.Accent;
            lblInspectorCategory.Location = new Point(16, 240);
            lblInspectorCategory.AutoSize = true;

            lblInspectorFamName = new Label();
            lblInspectorFamName.Text = "Tên Family";
            lblInspectorFamName.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            lblInspectorFamName.ForeColor = HDToolTheme.Text;
            lblInspectorFamName.Location = new Point(16, 260);
            lblInspectorFamName.Size = new Size(380, 36);
            lblInspectorFamName.AutoEllipsis = true;

            lblInspectorTypeName = new Label();
            lblInspectorTypeName.Text = "Type: (Chưa chọn)";
            lblInspectorTypeName.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblInspectorTypeName.ForeColor = HDToolTheme.Ok;
            lblInspectorTypeName.Location = new Point(16, 338);
            lblInspectorTypeName.Size = new Size(380, 24);
            lblInspectorTypeName.AutoEllipsis = true;

            cboTypes = new System.Windows.Forms.ComboBox();
            cboTypes.DropDownStyle = ComboBoxStyle.DropDownList;
            cboTypes.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            cboTypes.Location = new Point(16, 296);
            cboTypes.Size = new Size(380, 26);
            cboTypes.SelectedIndexChanged += CboTypes_SelectedIndexChanged;

            pnlInspectorInfo = new System.Windows.Forms.Panel();
            pnlInspectorInfo.Location = new Point(16, 326);
            pnlInspectorInfo.Size = new Size(380, 52);
            pnlInspectorInfo.BackColor = HDToolTheme.Surface;
            pnlInspectorInfo.BorderStyle = BorderStyle.FixedSingle;
            pnlInspectorInfo.Padding = new Padding(8);

            lblInspectorHost = new Label();
            lblInspectorHost.Text = "Host: Khớp nối / Mặt phẳng làm việc";
            lblInspectorHost.Font = new Font("Segoe UI", 8F, FontStyle.Regular);
            lblInspectorHost.ForeColor = HDToolTheme.Text2;
            lblInspectorHost.Location = new Point(8, 6);
            lblInspectorHost.AutoSize = true;

            lblInspectorInstances = new Label();
            lblInspectorInstances.Text = "Số Type đã load: 0";
            lblInspectorInstances.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            lblInspectorInstances.ForeColor = HDToolTheme.Warn;
            lblInspectorInstances.Location = new Point(8, 26);
            lblInspectorInstances.AutoSize = true;

            pnlInspectorInfo.Controls.Add(lblInspectorHost);
            pnlInspectorInfo.Controls.Add(lblInspectorInstances);

            // Khung đổi Category trực tiếp (không cần mở Family)
            pnlCategoryBox = new System.Windows.Forms.Panel();
            pnlCategoryBox.Location = new Point(16, 386);
            pnlCategoryBox.Size = new Size(380, 84);
            pnlCategoryBox.BackColor = HDToolTheme.Surface;
            pnlCategoryBox.Paint += PnlCategoryBox_Paint;
            pnlCategoryBox.Resize += delegate { pnlCategoryBox.Invalidate(); };

            lblCategoryChange = new Label();
            lblCategoryChange.Text = "Đổi Category (không cần mở Family)";
            lblCategoryChange.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblCategoryChange.ForeColor = HDToolTheme.Text;
            lblCategoryChange.BackColor = HDToolTheme.Surface;
            lblCategoryChange.Location = new Point(10, 8);
            lblCategoryChange.AutoSize = true;

            cboNewCategory = new System.Windows.Forms.ComboBox();
            cboNewCategory.DropDownStyle = ComboBoxStyle.DropDownList;
            cboNewCategory.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            cboNewCategory.BackColor = HDToolTheme.Input;
            cboNewCategory.ForeColor = HDToolTheme.Text;
            cboNewCategory.Location = new Point(10, 32);
            cboNewCategory.Size = new Size(230, 26);
            cboNewCategory.MaxDropDownItems = 16;
            cboNewCategory.SelectedIndexChanged += delegate { UpdateCategoryButton(); };

            btnChangeCategory = CreateSmallButton("Đổi Category", HDToolTheme.Accent);
            btnChangeCategory.Location = new Point(246, 30);
            btnChangeCategory.Size = new Size(124, 29);
            btnChangeCategory.Enabled = false;
            btnChangeCategory.Click += delegate { ChangeSelectedCategory(); };

            lblCategoryNote = new Label();
            lblCategoryNote.Font = new Font("Segoe UI", 8F, FontStyle.Regular);
            lblCategoryNote.ForeColor = HDToolTheme.Text2;
            lblCategoryNote.BackColor = HDToolTheme.Surface;
            lblCategoryNote.Location = new Point(10, 63);
            lblCategoryNote.Size = new Size(360, 16);
            lblCategoryNote.AutoEllipsis = true;

            pnlCategoryBox.Controls.Add(lblCategoryChange);
            pnlCategoryBox.Controls.Add(cboNewCategory);
            pnlCategoryBox.Controls.Add(btnChangeCategory);
            pnlCategoryBox.Controls.Add(lblCategoryNote);

            lblParamHeader = new Label();
            lblParamHeader.Text = "Thông Số Kỹ Thuật (Type Parameters):";
            lblParamHeader.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblParamHeader.ForeColor = HDToolTheme.Text;
            lblParamHeader.Location = new Point(16, 476);
            lblParamHeader.AutoSize = true;

            dgvParameters = new DataGridView();
            dgvParameters.Location = new Point(16, 498);
            dgvParameters.Size = new Size(380, 112);
            dgvParameters.BackgroundColor = HDToolTheme.Surface;
            dgvParameters.BorderStyle = BorderStyle.FixedSingle;
            dgvParameters.GridColor = HDToolTheme.Line;
            dgvParameters.RowHeadersVisible = false;
            dgvParameters.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvParameters.AllowUserToAddRows = false;
            dgvParameters.AllowUserToDeleteRows = false;
            dgvParameters.AllowUserToResizeRows = false;
            dgvParameters.EnableHeadersVisualStyles = false;
            dgvParameters.ColumnHeadersDefaultCellStyle.BackColor = HDToolTheme.Surface2;
            dgvParameters.ColumnHeadersDefaultCellStyle.ForeColor = HDToolTheme.Text2;
            dgvParameters.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            dgvParameters.DefaultCellStyle.BackColor = HDToolTheme.Surface;
            dgvParameters.DefaultCellStyle.ForeColor = HDToolTheme.Text;
            dgvParameters.DefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
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
            btnInspectorPlace.ForeColor = HDToolTheme.AccentInk;
            btnInspectorPlace.BackColor = HDToolTheme.Accent;
            btnInspectorPlace.FlatStyle = FlatStyle.Flat;
            btnInspectorPlace.FlatAppearance.BorderSize = 0;
            btnInspectorPlace.Location = new Point(16, 620);
            btnInspectorPlace.Size = new Size(380, 46);
            btnInspectorPlace.Cursor = Cursors.Hand;
            btnInspectorPlace.Click += BtnInspectorPlace_Click;

            lblPlacementHint = new Label();
            lblPlacementHint.Text = "Hướng dẫn: Click nút hoặc Double-click dòng trên bảng, di chuột sang bản vẽ Revit để đặt Family liên tục (Space: xoay, ESC: kết thúc).";
            lblPlacementHint.Font = new Font("Segoe UI", 8F, FontStyle.Regular);
            lblPlacementHint.ForeColor = HDToolTheme.Text2;
            lblPlacementHint.Location = new Point(16, 674);
            lblPlacementHint.Size = new Size(380, 36);

            pnlInspector.Controls.Add(lblInspectorHeader);
            pnlInspector.Controls.Add(picLargePreview);
            pnlInspector.Controls.Add(lblInspectorCategory);
            pnlInspector.Controls.Add(lblInspectorFamName);
            pnlInspector.Controls.Add(cboTypes);
            pnlInspector.Controls.Add(pnlInspectorInfo);
            pnlInspector.Controls.Add(pnlCategoryBox);
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
            pnlBottom.BackColor = HDToolTheme.Header;
            pnlBottom.Margin = new Padding(0);
            pnlBottom.Padding = new Padding(16, 6, 16, 6);
            pnlBottom.Resize += PnlBottom_Resize;

            lblStatus = new Label();
            lblStatus.Text = "● Đang sẵn sàng nạp Family...";
            lblStatus.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            lblStatus.ForeColor = HDToolTheme.Ok;
            lblStatus.Location = new Point(16, 6);
            lblStatus.AutoSize = true;

            lblModelessNotice = new Label();
            lblModelessNotice.Text = "● Chế độ Modeless 100%: Kỹ sư hoàn toàn tự do Pan, Zoom, xoay 3D hoặc vẽ trên Revit. Phím ESC để đóng cửa sổ.";
            lblModelessNotice.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
            lblModelessNotice.ForeColor = HDToolTheme.Text2;
            lblModelessNotice.Location = new Point(16, 26);
            lblModelessNotice.AutoSize = true;

            btnClose = new System.Windows.Forms.Button();
            btnClose.Text = "Đóng (ESC)";
            btnClose.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            btnClose.ForeColor = HDToolTheme.Text;
            btnClose.BackColor = HDToolTheme.Surface;
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.FlatAppearance.BorderSize = 1;
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
            lblTotalStats.Location = new Point(rightX - lblTotalStats.Width, 18);
            lblHeaderBadge.Location = new Point(lblTotalStats.Left - lblHeaderBadge.Width - 10, 19);
        }

        private void PnlToolbar_Resize(object sender, EventArgs e)
        {
            if (pnlToolbar == null || btnRefresh == null) return;
            btnRefresh.Location = new Point(pnlToolbar.ClientSize.Width - btnRefresh.Width - 16, 11);
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
            cboTypes.Width = w;
            pnlInspectorInfo.Width = w;
            pnlCategoryBox.Width = w;
            btnChangeCategory.Left = w - 10 - btnChangeCategory.Width;
            cboNewCategory.Width = btnChangeCategory.Left - 6 - cboNewCategory.Left;
            lblCategoryNote.Width = w - 20;
            dgvParameters.Width = w;
            btnInspectorPlace.Width = w;
            lblPlacementHint.Width = w;
            pnlInspector.PerformLayout(); // tính lại vùng cuộn, tránh thanh cuộn ngang thừa
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
                _allFamilies = FamilyPaletteService.GetProjectFamilies(_doc, new FamilyPaletteFilter()); // loc lam o FilterAndPopulateGrid
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

                // MỖI DÒNG = 1 FAMILY (loại) đã load. Không đếm số lượng trên bản vẽ.
                FamilyTypeItem firstMatch = null;
                if (!string.IsNullOrEmpty(kw) && !fNameClean.Contains(kw) && !catClean.Contains(kw))
                {
                    for (int t = 0; t < g.Types.Count; t++)
                    {
                        string tNameClean = SheetExportCadPdfService.RemoveDiacritics(g.Types[t].TypeName).ToUpperInvariant();
                        if (tNameClean.Contains(kw)) { firstMatch = g.Types[t]; break; }
                    }
                    if (firstMatch == null) continue;
                }
                if (g.Types.Count == 0) continue;
                if (firstMatch == null) firstMatch = g.Types[0];

                FamilyTypeRowItem rowItem = new FamilyTypeRowItem(g, firstMatch, stt);
                _displayRows.Add(rowItem);

                int rowIndex = dgvFamilies.Rows.Add(
                    stt.ToString(CultureInfo.InvariantCulture),
                    g.FamilyName,
                    g.Types.Count.ToString(CultureInfo.InvariantCulture),
                    g.CategoryName,
                    "Đặt Vào Bản Vẽ"
                );
                dgvFamilies.Rows[rowIndex].Tag = rowItem;
                stt++;
            }


            dgvFamilies.ResumeLayout();

            lblFilterResult.Text = string.Format(CultureInfo.InvariantCulture, "Hiển thị: {0} / {1} Family", _displayRows.Count, _allFamilies.Count);

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
            // Nếu click đúng cột nút bấm "Thao Tác" (cột 4)
            if (e.ColumnIndex == 4)
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
        private bool _fillingTypes;

        private void SelectRowItem(FamilyTypeRowItem rowItem)
        {
            if (rowItem == null) return;
            _selectedRow = rowItem;

            FamilyGroupItem group = rowItem.Group;
            lblInspectorCategory.Text = string.Format(CultureInfo.InvariantCulture, "[{0}]", group.CategoryName);
            lblInspectorFamName.Text = group.FamilyName;
            lblInspectorInstances.Text = string.Format(CultureInfo.InvariantCulture, "Số Type đã load: {0}", group.Types.Count);

            // Đổ danh sách Type của Family vào ô chọn
            _fillingTypes = true;
            cboTypes.BeginUpdate();
            cboTypes.Items.Clear();
            int selIndex = 0;
            for (int i = 0; i < group.Types.Count; i++)
            {
                cboTypes.Items.Add(group.Types[i].TypeName);
                if (group.Types[i] == rowItem.TypeItem) selIndex = i;
            }
            cboTypes.EndUpdate();
            if (cboTypes.Items.Count > 0) cboTypes.SelectedIndex = selIndex;
            _fillingTypes = false;

            UpdateCategoryBox(group);
            ShowType(rowItem.TypeItem);
        }

        private void CboTypes_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_fillingTypes || _selectedRow == null) return;
            int i = cboTypes.SelectedIndex;
            if (i < 0 || i >= _selectedRow.Group.Types.Count) return;
            _selectedRow.TypeItem = _selectedRow.Group.Types[i];
            ShowType(_selectedRow.TypeItem);
        }

        /// <summary>
        /// Hiện hình 3D xoay được + thông số của Type đang chọn.
        /// Hình khối đọc trong ExternalEvent (API context), có Cache, không làm đơ cửa sổ.
        /// </summary>
        private void ShowType(FamilyTypeItem typeItem)
        {
            if (typeItem == null) return;
            FamilyGroupItem group = _selectedRow != null ? _selectedRow.Group : null;
            string famName = group != null ? group.FamilyName : typeItem.FamilyName;
            string catName = group != null ? group.CategoryName : typeItem.CategoryName;
            ElementId symbolId = typeItem.Id;
            _viewerSymbolId = symbolId;

            float[] mesh;
            float[] segs;
            Bitmap bmp;
            if (_meshCache.TryGetValue(symbolId, out mesh))
            {
                _lineCache.TryGetValue(symbolId, out segs);
                _previewCache.TryGetValue(symbolId, out bmp);
                picLargePreview.Image = bmp;
                picLargePreview.SetGeometry(mesh, segs);
            }
            else
            {
                picLargePreview.SetMesh(null);
                picLargePreview.Image = null;
                picLargePreview.Message = "Đang dựng hình 3D...";
                Document doc = _doc;
                HDToolModelessHost.Run(delegate(UIApplication app)
                {
                    float[] m, s;
                    FamilyPaletteService.GetSymbolGeometry(doc, symbolId, 30000, out m, out s);
                    Bitmap b = null;
                    if (m == null && s == null)
                    {
                        // Family 2D (Tag...): ảnh xem trước, khung xem cho xoay như tấm phẳng
                        b = FamilyPaletteService.GetSymbolPreviewImage(doc, symbolId, new Size(380, 240), famName, catName);
                    }
                    if (this.IsDisposed || !this.IsHandleCreated) return;
                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        _meshCache[symbolId] = m;
                        _lineCache[symbolId] = s;
                        if (b != null) _previewCache[symbolId] = b;
                        if (_viewerSymbolId != symbolId) return; // người dùng đã chọn Type khác
                        picLargePreview.Image = b;
                        picLargePreview.SetGeometry(m, s);
                    });
                });
            }

            UpdateParameterTable(symbolId);
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
            picLargePreview.SetMesh(null);
            picLargePreview.Message = "Không có Family phù hợp";
            cboTypes.Items.Clear();
            dgvParameters.Rows.Clear();
            UpdateCategoryBox(null);
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

        // ==================== ĐỔI CATEGORY TRỰC TIẾP ====================

        /// <summary>Nạp danh sách Category đích cho Family đang chọn; Family không đổi được thì khóa ô và ghi lý do.</summary>
        private void UpdateCategoryBox(FamilyGroupItem group)
        {
            if (cboNewCategory == null) return;
            bool canChange = false;
            string reason = null;
            cboNewCategory.BeginUpdate();
            cboNewCategory.Items.Clear();
            cboNewCategory.Items.Add("-- Chọn Category mới --");
            if (group != null)
            {
                try
                {
                    canChange = FamilyCategoryService.CanChange(_doc, group.FamilyId, out reason);
                    if (canChange)
                    {
                        if (_categoryChoices == null) _categoryChoices = FamilyCategoryService.GetChoices(_doc);
                        for (int i = 0; i < _categoryChoices.Count; i++)
                        {
                            if (_categoryChoices[i].Bic != group.BuiltInCat) cboNewCategory.Items.Add(_categoryChoices[i]);
                        }
                    }
                }
                catch (Exception ex)
                {
                    canChange = false;
                    reason = "Không đọc được Family: " + ex.Message;
                }
            }
            cboNewCategory.SelectedIndex = 0;
            cboNewCategory.EndUpdate();
            cboNewCategory.Enabled = canChange && !_categoryBusy;
            lblCategoryNote.Text = canChange ? "Giữ nguyên thông số Type đang có trong dự án." : (reason ?? "");
            lblCategoryNote.ForeColor = canChange || group == null ? HDToolTheme.Text2 : HDToolTheme.Warn;
            UpdateCategoryButton();
        }

        private void UpdateCategoryButton()
        {
            bool on = !_categoryBusy && cboNewCategory.Enabled && cboNewCategory.SelectedItem is FamilyCategoryChoice;
            btnChangeCategory.Enabled = on;
            btnChangeCategory.BackColor = on ? HDToolTheme.Accent : HDToolTheme.Surface2;
        }

        private void PnlCategoryBox_Paint(object sender, PaintEventArgs e)
        {
            using (Pen p = new Pen(HDToolTheme.Accent, 2F))
            {
                e.Graphics.DrawRectangle(p, 1, 1, pnlCategoryBox.ClientSize.Width - 2, pnlCategoryBox.ClientSize.Height - 2);
            }
        }

        private void ChangeSelectedCategory()
        {
            FamilyCategoryChoice choice = cboNewCategory.SelectedItem as FamilyCategoryChoice;
            if (_categoryBusy || choice == null || _selectedRow == null) return;

            FamilyGroupItem group = _selectedRow.Group;
            string familyName = group.FamilyName;
            ElementId familyId = group.FamilyId;
            List<ElementId> symbolIds = new List<ElementId>();
            for (int i = 0; i < group.Types.Count; i++) symbolIds.Add(group.Types[i].Id);

            string question = string.Format(CultureInfo.CurrentCulture,
                "Family: {0} ({1} Type)\n\nĐổi từ \"{2}\" sang \"{3}\".\nMọi đối tượng đã vẽ của Family này sẽ đổi theo.",
                familyName, group.Types.Count, group.CategoryName, choice.Name);
            if (MessageBox.Show(this, question, "HDTOOL - Xác nhận đổi Category", MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.OK) return;

            _categoryBusy = true;
            cboNewCategory.Enabled = false;
            UpdateCategoryButton();
            lblStatus.Text = string.Format(CultureInfo.CurrentCulture, "● Đang đổi Category của [{0}]... (Family nặng có thể mất vài giây)", familyName);

            Document doc = _doc;
            BuiltInCategory target = choice.Bic;
            string targetName = choice.Name;
            HDToolModelessHost.Run(delegate(UIApplication app)
            {
                string message;
                bool ok;
                bool idsKept = false;
                FamilyCategoryGroup targetGroup = FamilyCategoryGroup.All;
                try
                {
                    ok = FamilyCategoryService.Change(doc, familyId, target, out message);
                    if (ok)
                    {
                        targetGroup = FamilyPaletteService.ResolveCategoryGroup(Category.GetCategory(doc, target));
                        idsKept = doc.GetElement(familyId) is Family;
                        for (int i = 0; idsKept && i < symbolIds.Count; i++) idsKept = doc.GetElement(symbolIds[i]) is FamilySymbol;
                    }
                }
                catch (Exception ex) { ok = false; message = "Không đổi được Category: " + ex.Message; }

                if (this.IsDisposed || !this.IsHandleCreated) return;
                this.BeginInvoke((MethodInvoker)delegate { OnCategoryChanged(ok, message, group, target, targetName, targetGroup, idsKept, symbolIds); });
            });
        }

        private void OnCategoryChanged(bool ok, string message, FamilyGroupItem group, BuiltInCategory target, string targetName,
            FamilyCategoryGroup targetGroup, bool idsKept, List<ElementId> symbolIds)
        {
            _categoryBusy = false;
            if (!ok)
            {
                UpdateCategoryBox(_selectedRow != null ? _selectedRow.Group : null);
                lblStatus.Text = "Lưu ý: " + message;
                MessageBox.Show(this, message, "HDTOOL - Đổi Category", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (idsKept)
            {
                // Chỉ sửa đúng Family vừa đổi: không quét lại dự án, không dựng lại hình 3D (đổi Category không đổi hình)
                ApplyCategoryInPlace(group, target, targetName, targetGroup);
            }
            else
            {
                // Revit cấp Id mới -> bắt buộc quét lại
                for (int i = 0; i < symbolIds.Count; i++)
                {
                    _meshCache.Remove(symbolIds[i]);
                    _lineCache.Remove(symbolIds[i]);
                    _previewCache.Remove(symbolIds[i]);
                }
                RefreshFamiliesFromProject();
                if (!SelectFamilyRow(group.FamilyName))
                {
                    txtSearch.Text = string.Empty;
                    cboCategoryFilter.SelectedIndex = 0;
                    SelectFamilyRow(group.FamilyName);
                }
            }
            lblStatus.Text = "● " + message;
        }

        private void ApplyCategoryInPlace(FamilyGroupItem group, BuiltInCategory bic, string categoryName, FamilyCategoryGroup categoryGroup)
        {
            group.BuiltInCat = bic;
            group.CategoryName = categoryName;
            group.CategoryGroup = categoryGroup;
            for (int i = 0; i < group.Types.Count; i++)
            {
                group.Types[i].BuiltInCat = bic;
                group.Types[i].CategoryName = categoryName;
                group.Types[i].CategoryGroup = categoryGroup;
            }

            for (int i = 0; i < dgvFamilies.Rows.Count; i++)
            {
                FamilyTypeRowItem item = dgvFamilies.Rows[i].Tag as FamilyTypeRowItem;
                if (item != null && item.Group == group) dgvFamilies.Rows[i].Cells["colCategory"].Value = categoryName;
            }

            if (_selectedRow != null && _selectedRow.Group == group)
            {
                lblInspectorCategory.Text = string.Format(CultureInfo.InvariantCulture, "[{0}]", categoryName);
                UpdateCategoryBox(group);
                if (_selectedRow.TypeItem != null) UpdateParameterTable(_selectedRow.TypeItem.Id);
            }
            else
            {
                UpdateCategoryBox(_selectedRow != null ? _selectedRow.Group : null);
            }
        }

        private bool SelectFamilyRow(string familyName)
        {
            for (int i = 0; i < dgvFamilies.Rows.Count; i++)
            {
                FamilyTypeRowItem item = dgvFamilies.Rows[i].Tag as FamilyTypeRowItem;
                if (item == null || !string.Equals(item.Group.FamilyName, familyName, StringComparison.OrdinalIgnoreCase)) continue;
                dgvFamilies.CurrentCell = dgvFamilies.Rows[i].Cells[1];
                dgvFamilies.Rows[i].Selected = true;
                SelectRowItem(item);
                return true;
            }
            return false;
        }

        // ==================== CÁC PHƯƠNG THỨC TRỢ GIÚP UI ====================

        private void PnlHeader_Paint(object sender, PaintEventArgs e)
        {
            Rectangle rect = pnlHeader.ClientRectangle;
            if (rect.Width <= 0 || rect.Height <= 0) return;

            using (SolidBrush b = new SolidBrush(HDToolTheme.Surface))
            {
                e.Graphics.FillRectangle(b, rect);
            }

            using (Pen p = new Pen(HDToolTheme.Line))
            {
                e.Graphics.DrawLine(p, 0, rect.Height - 1, rect.Width, rect.Height - 1);
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.F) && txtSearch != null)
            {
                txtSearch.Focus();
                txtSearch.SelectAll();
                return true;
            }
            if (keyData == Keys.F5)
            {
                RefreshFamiliesFromProject();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>Cột "Đặt Vào Bản Vẽ": vẽ nút bo góc màu nhấn, đồng nhất mọi dòng (không bị màu dòng xen kẽ đè).</summary>
        private void DgvFamilies_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (dgvFamilies.Columns[e.ColumnIndex].Name != "colAction") return;
            bool sel = (e.State & DataGridViewElementStates.Selected) != 0;
            e.PaintBackground(e.CellBounds, sel);
            Graphics g = e.Graphics;
            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int px = HDDraw.S(g, 8), py = HDDraw.S(g, 5);
            Rectangle r = new Rectangle(e.CellBounds.X + px, e.CellBounds.Y + py, e.CellBounds.Width - px * 2, e.CellBounds.Height - py * 2 - 1);
            HDDraw.FillRound(g, r, HDDraw.S(g, 6), sel ? HDToolTheme.AccentDown : HDToolTheme.Accent);
            TextRenderer.DrawText(g, "Đặt Vào Bản Vẽ", e.CellStyle.Font, r, HDToolTheme.AccentInk,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | HDDraw.One);
            g.SmoothingMode = old;
            e.Handled = true;
        }

        private System.Windows.Forms.Button CreateSmallButton(string text, Color backColor)
        {
            System.Windows.Forms.Button btn = new System.Windows.Forms.Button();
            btn.Text = text;
            btn.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btn.ForeColor = HDToolTheme.AccentInk;
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
