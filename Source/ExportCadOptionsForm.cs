using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Color = System.Drawing.Color;
using Font = System.Drawing.Font;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;
using Size = System.Drawing.Size;
using View = Autodesk.Revit.DB.View;

namespace HDTool
{
    /// <summary>
    /// Giao diện cấu hình và xuất bản vẽ sang AutoCAD DWG kèm bảng quy đổi Layer MEP
    /// Kế thừa HDToolBaseForm: Modeless 100%, phím ESC thoát lệnh ngay lập tức,
    /// hỗ trợ xuất trực tiếp từ: (1) View đang vẽ, (2) Danh sách Sheet, (3) Danh sách View dự án.
    /// </summary>
    public class ExportCadOptionsForm : HDToolBaseForm
    {
        private UIDocument _uidoc;
        private Document _doc;
        private CadExportTargetItem _activeViewTarget;
        private List<CadExportTargetItem> _sheetTargets;
        private List<CadExportTargetItem> _viewTargets;
        private List<CadLayerMappingItem> _allMappings;
        private MepCategoryGroup _activeFilter;
        private CadExportMepSettings _currentSettings;
        private bool _isLoading = false;

        // Header controls
        private System.Windows.Forms.Panel pnlHeader;
        private Label lblHeaderTitle;
        private Label lblHeaderSubtitle;
        private Label lblHeaderBadge;

        // Upper Section: Export Scope (Active View / Sheets / Views) & DWG Settings
        private System.Windows.Forms.Panel pnlTopContainer;
        private GroupBox grpScope;
        private System.Windows.Forms.Panel pnlScopeRadios;
        private RadioButton radScopeActiveView;
        private RadioButton radScopeSheets;
        private RadioButton radScopeViews;

        // Panels for Scope
        private System.Windows.Forms.Panel pnlActiveViewCard;
        private Label lblActiveViewBadge;
        private Label lblActiveViewName;
        private Label lblActiveViewDesc;

        private System.Windows.Forms.Panel pnlSheetsContainer;
        private CheckedListBox clbSheets;
        private System.Windows.Forms.Panel pnlSheetActions;
        private System.Windows.Forms.Button btnSelectAllSheets;
        private System.Windows.Forms.Button btnDeselectAllSheets;
        private Label lblSheetCount;

        private System.Windows.Forms.Panel pnlViewsContainer;
        private CheckedListBox clbViews;
        private System.Windows.Forms.Panel pnlViewActions;
        private System.Windows.Forms.Button btnSelectAllViews;
        private System.Windows.Forms.Button btnDeselectAllViews;
        private Label lblViewCount;

        // Output Config
        private GroupBox grpExportConfig;
        private Label lblFolder;
        private System.Windows.Forms.TextBox txtOutputFolder;
        private System.Windows.Forms.Button btnBrowseFolder;
        private Label lblCadVersion;
        private System.Windows.Forms.ComboBox cboCadVersion;
        private Label lblNamingRule;
        private System.Windows.Forms.ComboBox cboNamingRule;
        private CheckBox chkMergedViews;
        private CheckBox chkApplyMepLayers;
        private CheckBox chkKeepTitleBlock;
        private CheckBox chkUnicodeFont;

        // Middle Section: Layer Mapping DataGrid
        private GroupBox grpLayerMapping;
        private System.Windows.Forms.Panel pnlMatrixToolbar;
        private Label lblFilterCategory;
        private System.Windows.Forms.Button btnFilterAll;
        private System.Windows.Forms.Button btnFilterDuct;
        private System.Windows.Forms.Button btnFilterPipe;
        private System.Windows.Forms.Button btnFilterEquip;
        private Label lblPreset;
        private System.Windows.Forms.ComboBox cboPresets;
        private System.Windows.Forms.Button btnLoadProjectSys;
        private System.Windows.Forms.Button btnSaveDefault;
        private System.Windows.Forms.Button btnLoadDefault;
        private System.Windows.Forms.Button btnSaveJson;
        private System.Windows.Forms.Button btnLoadJson;
        private System.Windows.Forms.Button btnAddRow;
        private System.Windows.Forms.Button btnDeleteRow;
        private DataGridView dgvMappings;
        private System.Windows.Forms.Panel pnlMatrixFooter;
        private Label lblMatrixFooterNote;

        // Bottom Section: Actions & Modeless Status
        private System.Windows.Forms.Panel pnlBottom;
        private Label lblStatus;
        private Label lblModelessNotice;
        private System.Windows.Forms.Button btnExport;
        private System.Windows.Forms.Button btnClose;

        public ExportCadOptionsForm(UIDocument uidoc)
            : base()
        {
            _uidoc = uidoc;
            _doc = (uidoc != null) ? uidoc.Document : null;
            InitializeData();
            InitializeComponent();
            RefreshDataFromDocument();
            LoadInitialPresets();
            SwitchScopeMode(CadExportScope.ActiveView);
        }

        public ExportCadOptionsForm(Document doc)
            : base()
        {
            _uidoc = null;
            _doc = doc;
            InitializeData();
            InitializeComponent();
            RefreshDataFromDocument();
            LoadInitialPresets();
            SwitchScopeMode(CadExportScope.ActiveView);
        }

        private void LoadInitialPresets()
        {
            ScanSystemsFromCurrentSelection();
        }

        public void UpdateContext(UIDocument uidoc)
        {
            if (uidoc == null) return;
            _uidoc = uidoc;
            _doc = uidoc.Document;
            RefreshDataFromDocument();
            UpdateScopeStatus();
        }

        private void InitializeData()
        {
            _sheetTargets = new List<CadExportTargetItem>();
            _viewTargets = new List<CadExportTargetItem>();
            _allMappings = new List<CadLayerMappingItem>();
            _activeFilter = MepCategoryGroup.All;
            _currentSettings = new CadExportMepSettings();
            _currentSettings.ExportScope = CadExportScope.ActiveView;

            string defaultFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "HDTOOL_MEP_CAD_EXPORTS"
            );
            _currentSettings.TargetFolder = defaultFolder;
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Form Settings
            this.Text = "HDTOOL - XUẤT BẢN VẼ SANG AUTOCAD & QUY ĐỔI LAYER HỆ THỐNG MEP";
            this.ClientSize = new Size(1280, 820);
            this.MinimumSize = new Size(1180, 720);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(24, 29, 38);
            this.ForeColor = Color.FromArgb(241, 245, 249);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.ShowIcon = false;

            // ==================== 1. MASTER TOP CONTAINER ====================
            // Gom chung Header + 2 GroupBox vào 1 Panel mẹ duy nhất để tuyệt đối không bị đè lên bảng
            System.Windows.Forms.Panel pnlMasterTop = new System.Windows.Forms.Panel();
            pnlMasterTop.Dock = DockStyle.Top;
            pnlMasterTop.Height = 265;
            pnlMasterTop.BackColor = Color.FromArgb(24, 29, 38);

            // 1.1 HEADER BANNER TRÊN CÙNG
            pnlHeader = new System.Windows.Forms.Panel();
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = 48;
            pnlHeader.BackColor = Color.FromArgb(16, 22, 32);
            pnlHeader.Paint += PnlHeader_Paint;

            lblHeaderTitle = new Label();
            lblHeaderTitle.Text = "XUẤT BẢN VẼ SANG AUTOCAD & QUY ĐỔI LAYER HỆ THỐNG MEP";
            lblHeaderTitle.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            lblHeaderTitle.ForeColor = Color.FromArgb(248, 250, 252);
            lblHeaderTitle.Location = new Point(14, 8);
            lblHeaderTitle.AutoSize = true;

            lblHeaderBadge = new Label();
            lblHeaderBadge.Text = "TCVN / ISO";
            lblHeaderBadge.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            lblHeaderBadge.ForeColor = Color.FromArgb(110, 231, 183);
            lblHeaderBadge.BackColor = Color.FromArgb(6, 78, 59);
            lblHeaderBadge.TextAlign = ContentAlignment.MiddleCenter;
            lblHeaderBadge.Size = new Size(76, 18);
            lblHeaderBadge.Location = new Point(530, 9);

            lblHeaderSubtitle = new Label();
            lblHeaderSubtitle.Text = "Hỗ trợ xuất trực tiếp View đang vẽ, danh sách Sheet hoặc View dự án; tự động quy đổi Layer/Màu/Nét MEP.";
            lblHeaderSubtitle.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
            lblHeaderSubtitle.ForeColor = Color.FromArgb(148, 163, 184);
            lblHeaderSubtitle.Location = new Point(15, 28);
            lblHeaderSubtitle.AutoSize = true;

            pnlHeader.Controls.Add(lblHeaderTitle);
            pnlHeader.Controls.Add(lblHeaderBadge);
            pnlHeader.Controls.Add(lblHeaderSubtitle);

            // 1.2 CONTAINER CHỨA 2 GROUPBOX CẤU HÌNH
            pnlTopContainer = new System.Windows.Forms.Panel();
            pnlTopContainer.Dock = DockStyle.Fill;
            pnlTopContainer.Padding = new Padding(12, 6, 12, 4);

            // Left: GroupBox Scope (View đang vẽ / Sheet / View dự án)
            grpScope = new GroupBox();
            grpScope.Text = " 📋 1. Nguồn Đối Tượng Cần Xuất ";
            grpScope.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            grpScope.ForeColor = Color.FromArgb(56, 189, 248);
            grpScope.Dock = DockStyle.Left;
            grpScope.Width = 470;
            grpScope.Padding = new Padding(8, 6, 8, 6);

            // Radio Buttons for Scope
            pnlScopeRadios = new System.Windows.Forms.Panel();
            pnlScopeRadios.Dock = DockStyle.Top;
            pnlScopeRadios.Height = 32;
            pnlScopeRadios.BackColor = Color.FromArgb(15, 23, 42);

            radScopeActiveView = new RadioButton();
            radScopeActiveView.Text = "👉 View Đang Vẽ";
            radScopeActiveView.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            radScopeActiveView.ForeColor = Color.FromArgb(52, 211, 153);
            radScopeActiveView.Location = new Point(8, 4);
            radScopeActiveView.AutoSize = true;
            radScopeActiveView.Checked = true;
            radScopeActiveView.CheckedChanged += delegate { if (radScopeActiveView.Checked) SwitchScopeMode(CadExportScope.ActiveView); };

            radScopeSheets = new RadioButton();
            radScopeSheets.Text = "📑 Sheet Bản Vẽ";
            radScopeSheets.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
            radScopeSheets.ForeColor = Color.FromArgb(203, 213, 225);
            radScopeSheets.Location = new Point(160, 4);
            radScopeSheets.AutoSize = true;
            radScopeSheets.CheckedChanged += delegate { if (radScopeSheets.Checked) SwitchScopeMode(CadExportScope.Sheets); };

            radScopeViews = new RadioButton();
            radScopeViews.Text = "📂 View Dự Án";
            radScopeViews.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
            radScopeViews.ForeColor = Color.FromArgb(203, 213, 225);
            radScopeViews.Location = new Point(310, 4);
            radScopeViews.AutoSize = true;
            radScopeViews.CheckedChanged += delegate { if (radScopeViews.Checked) SwitchScopeMode(CadExportScope.ProjectViews); };

            pnlScopeRadios.Controls.Add(radScopeActiveView);
            pnlScopeRadios.Controls.Add(radScopeSheets);
            pnlScopeRadios.Controls.Add(radScopeViews);
            grpScope.Controls.Add(pnlScopeRadios);

            // Container 1: Active View Card
            pnlActiveViewCard = new System.Windows.Forms.Panel();
            pnlActiveViewCard.Dock = DockStyle.Fill;
            pnlActiveViewCard.BackColor = Color.FromArgb(16, 36, 32);
            pnlActiveViewCard.Padding = new Padding(12, 10, 12, 8);

            lblActiveViewBadge = new Label();
            lblActiveViewBadge.Text = "✔ VIEW REVIT ĐANG MỞ VẼ HIỆN HÀNH (ACTIVE VIEW):";
            lblActiveViewBadge.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            lblActiveViewBadge.ForeColor = Color.FromArgb(110, 231, 183);
            lblActiveViewBadge.Location = new Point(12, 12);
            lblActiveViewBadge.AutoSize = true;

            lblActiveViewName = new Label();
            lblActiveViewName.Text = "[FloorPlan] Mặt Bằng Cấp Gió Lầu 1";
            lblActiveViewName.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblActiveViewName.ForeColor = Color.White;
            lblActiveViewName.BackColor = Color.FromArgb(15, 23, 42);
            lblActiveViewName.BorderStyle = BorderStyle.FixedSingle;
            lblActiveViewName.Location = new Point(12, 38);
            lblActiveViewName.Size = new Size(430, 32);
            lblActiveViewName.TextAlign = ContentAlignment.MiddleLeft;
            lblActiveViewName.Padding = new Padding(8, 0, 8, 0);

            lblActiveViewDesc = new Label();
            lblActiveViewDesc.Text = "⚡ Xuất trực tiếp toàn bộ mô hình MEP từ View đang vẽ, không cần tạo Sheet.";
            lblActiveViewDesc.Font = new Font("Segoe UI", 8F, FontStyle.Italic);
            lblActiveViewDesc.ForeColor = Color.FromArgb(148, 163, 184);
            lblActiveViewDesc.Location = new Point(12, 80);
            lblActiveViewDesc.AutoSize = true;

            pnlActiveViewCard.Controls.Add(lblActiveViewBadge);
            pnlActiveViewCard.Controls.Add(lblActiveViewName);
            pnlActiveViewCard.Controls.Add(lblActiveViewDesc);
            grpScope.Controls.Add(pnlActiveViewCard);

            // Container 2: Sheets List
            pnlSheetsContainer = new System.Windows.Forms.Panel();
            pnlSheetsContainer.Dock = DockStyle.Fill;
            pnlSheetsContainer.Visible = false;

            clbSheets = new CheckedListBox();
            clbSheets.Dock = DockStyle.Fill;
            clbSheets.BackColor = Color.FromArgb(15, 23, 42);
            clbSheets.ForeColor = Color.FromArgb(226, 232, 240);
            clbSheets.BorderStyle = BorderStyle.None;
            clbSheets.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
            clbSheets.CheckOnClick = true;
            clbSheets.ItemCheck += delegate
            {
                if (_isLoading) return;
                SafeBeginInvoke(delegate
                {
                    UpdateScopeStatus();
                    ScanSystemsFromCurrentSelection();
                });
            };

            pnlSheetActions = new System.Windows.Forms.Panel();
            pnlSheetActions.Dock = DockStyle.Bottom;
            pnlSheetActions.Height = 26;
            pnlSheetActions.Padding = new Padding(2, 2, 2, 0);

            btnSelectAllSheets = CreateSmallButton("Chọn Tất Cả", Color.FromArgb(15, 118, 110));
            btnSelectAllSheets.Location = new Point(2, 2);
            btnSelectAllSheets.Size = new Size(85, 22);
            btnSelectAllSheets.Click += delegate { SetAllListChecked(clbSheets, true); };

            btnDeselectAllSheets = CreateSmallButton("Bỏ Chọn", Color.FromArgb(71, 85, 105));
            btnDeselectAllSheets.Location = new Point(90, 2);
            btnDeselectAllSheets.Size = new Size(75, 22);
            btnDeselectAllSheets.Click += delegate { SetAllListChecked(clbSheets, false); };

            lblSheetCount = new Label();
            lblSheetCount.Text = "Đã chọn: 0 bản vẽ";
            lblSheetCount.ForeColor = Color.FromArgb(148, 163, 184);
            lblSheetCount.Font = new Font("Segoe UI", 8F, FontStyle.Regular);
            lblSheetCount.AutoSize = true;
            lblSheetCount.Location = new Point(175, 6);

            pnlSheetActions.Controls.Add(btnSelectAllSheets);
            pnlSheetActions.Controls.Add(btnDeselectAllSheets);
            pnlSheetActions.Controls.Add(lblSheetCount);

            pnlSheetsContainer.Controls.Add(clbSheets);
            pnlSheetsContainer.Controls.Add(pnlSheetActions);
            grpScope.Controls.Add(pnlSheetsContainer);

            // Container 3: Views List
            pnlViewsContainer = new System.Windows.Forms.Panel();
            pnlViewsContainer.Dock = DockStyle.Fill;
            pnlViewsContainer.Visible = false;

            clbViews = new CheckedListBox();
            clbViews.Dock = DockStyle.Fill;
            clbViews.BackColor = Color.FromArgb(15, 23, 42);
            clbViews.ForeColor = Color.FromArgb(226, 232, 240);
            clbViews.BorderStyle = BorderStyle.None;
            clbViews.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
            clbViews.CheckOnClick = true;
            clbViews.ItemCheck += delegate
            {
                if (_isLoading) return;
                SafeBeginInvoke(delegate
                {
                    UpdateScopeStatus();
                    ScanSystemsFromCurrentSelection();
                });
            };

            pnlViewActions = new System.Windows.Forms.Panel();
            pnlViewActions.Dock = DockStyle.Bottom;
            pnlViewActions.Height = 26;
            pnlViewActions.Padding = new Padding(2, 2, 2, 0);

            btnSelectAllViews = CreateSmallButton("Chọn Tất Cả", Color.FromArgb(15, 118, 110));
            btnSelectAllViews.Location = new Point(2, 2);
            btnSelectAllViews.Size = new Size(85, 22);
            btnSelectAllViews.Click += delegate { SetAllListChecked(clbViews, true); };

            btnDeselectAllViews = CreateSmallButton("Bỏ Chọn", Color.FromArgb(71, 85, 105));
            btnDeselectAllViews.Location = new Point(90, 2);
            btnDeselectAllViews.Size = new Size(75, 22);
            btnDeselectAllViews.Click += delegate { SetAllListChecked(clbViews, false); };

            lblViewCount = new Label();
            lblViewCount.Text = "Đã chọn: 0 view";
            lblViewCount.ForeColor = Color.FromArgb(148, 163, 184);
            lblViewCount.Font = new Font("Segoe UI", 8F, FontStyle.Regular);
            lblViewCount.AutoSize = true;
            lblViewCount.Location = new Point(175, 6);

            pnlViewActions.Controls.Add(btnSelectAllViews);
            pnlViewActions.Controls.Add(btnDeselectAllViews);
            pnlViewActions.Controls.Add(lblViewCount);

            pnlViewsContainer.Controls.Add(clbViews);
            pnlViewsContainer.Controls.Add(pnlViewActions);
            grpScope.Controls.Add(pnlViewsContainer);

            // Right: GroupBox Export Config
            grpExportConfig = new GroupBox();
            grpExportConfig.Text = " ⚙ 2. Cấu Hình Đường Dẫn & Định Dạng DWG ";
            grpExportConfig.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            grpExportConfig.ForeColor = Color.FromArgb(52, 211, 153);
            grpExportConfig.Dock = DockStyle.Fill;
            grpExportConfig.Padding = new Padding(12, 8, 12, 6);

            lblFolder = new Label();
            lblFolder.Text = "Thư mục lưu file AutoCAD DWG:";
            lblFolder.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
            lblFolder.ForeColor = Color.FromArgb(203, 213, 225);
            lblFolder.Location = new Point(14, 20);
            lblFolder.AutoSize = true;

            txtOutputFolder = new System.Windows.Forms.TextBox();
            txtOutputFolder.Location = new Point(15, 38);
            txtOutputFolder.Size = new Size(580, 23);
            txtOutputFolder.BackColor = Color.FromArgb(15, 23, 42);
            txtOutputFolder.ForeColor = Color.FromArgb(56, 189, 248);
            txtOutputFolder.BorderStyle = BorderStyle.FixedSingle;
            txtOutputFolder.Text = _currentSettings.TargetFolder;

            btnBrowseFolder = CreateSmallButton("Chọn Thư Mục...", Color.FromArgb(51, 65, 85));
            btnBrowseFolder.Location = new Point(602, 37);
            btnBrowseFolder.Size = new Size(115, 25);
            btnBrowseFolder.Click += BtnBrowseFolder_Click;

            lblCadVersion = new Label();
            lblCadVersion.Text = "Phiên bản AutoCAD:";
            lblCadVersion.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
            lblCadVersion.ForeColor = Color.FromArgb(203, 213, 225);
            lblCadVersion.Location = new Point(14, 68);
            lblCadVersion.AutoSize = true;

            cboCadVersion = new System.Windows.Forms.ComboBox();
            cboCadVersion.DropDownStyle = ComboBoxStyle.DropDownList;
            cboCadVersion.BackColor = Color.FromArgb(15, 23, 42);
            cboCadVersion.ForeColor = Color.FromArgb(248, 250, 252);
            cboCadVersion.FlatStyle = FlatStyle.Flat;
            cboCadVersion.Location = new Point(15, 87);
            cboCadVersion.Size = new Size(220, 23);
            cboCadVersion.Items.AddRange(new object[] {
                "AutoCAD 2018 (.dwg)",
                "AutoCAD 2013 (.dwg)",
                "AutoCAD 2010 (.dwg)"
            });
            cboCadVersion.SelectedIndex = 0;

            lblNamingRule = new Label();
            lblNamingRule.Text = "Quy tắc đặt tên file DWG:";
            lblNamingRule.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
            lblNamingRule.ForeColor = Color.FromArgb(203, 213, 225);
            lblNamingRule.Location = new Point(248, 68);
            lblNamingRule.AutoSize = true;

            cboNamingRule = new System.Windows.Forms.ComboBox();
            cboNamingRule.DropDownStyle = ComboBoxStyle.DropDownList;
            cboNamingRule.BackColor = Color.FromArgb(15, 23, 42);
            cboNamingRule.ForeColor = Color.FromArgb(248, 250, 252);
            cboNamingRule.FlatStyle = FlatStyle.Flat;
            cboNamingRule.Location = new Point(250, 87);
            cboNamingRule.Size = new Size(465, 23);
            cboNamingRule.Items.AddRange(new object[] {
                "[Số Hiệu/Loại View] - [Tên Bản Vẽ].dwg",
                "[Mã Dự Án]_[Số Hiệu].dwg",
                "Chỉ lấy Tên Bản Vẽ / Tên View.dwg"
            });
            cboNamingRule.SelectedIndex = 0;

            chkMergedViews = CreateCheckBox("Gộp liên kết ngoài (Bind Xrefs / Merged Views)", true, 16, 120);
            chkApplyMepLayers = CreateCheckBox("Áp dụng Quy đổi Layer Hệ Thống MEP", true, 340, 120);
            chkApplyMepLayers.ForeColor = Color.FromArgb(52, 211, 153);
            chkApplyMepLayers.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);

            chkKeepTitleBlock = CreateCheckBox("Giữ nguyên khung tên (TitleBlock)", true, 16, 148);
            chkUnicodeFont = CreateCheckBox("Chuẩn hóa Font tiếng Việt Unicode", true, 340, 148);

            grpExportConfig.Controls.Add(lblFolder);
            grpExportConfig.Controls.Add(txtOutputFolder);
            grpExportConfig.Controls.Add(btnBrowseFolder);
            grpExportConfig.Controls.Add(lblCadVersion);
            grpExportConfig.Controls.Add(cboCadVersion);
            grpExportConfig.Controls.Add(lblNamingRule);
            grpExportConfig.Controls.Add(cboNamingRule);
            grpExportConfig.Controls.Add(chkMergedViews);
            grpExportConfig.Controls.Add(chkApplyMepLayers);
            grpExportConfig.Controls.Add(chkKeepTitleBlock);
            grpExportConfig.Controls.Add(chkUnicodeFont);

            pnlTopContainer.Controls.Add(grpExportConfig);
            pnlTopContainer.Controls.Add(grpScope);

            // Add Header và TopContainer vào MasterTop
            pnlMasterTop.Controls.Add(pnlTopContainer);
            pnlMasterTop.Controls.Add(pnlHeader);
            pnlHeader.BringToFront();

            // ==================== 2. MIDDLE CONTAINER: DATA GRID ====================
            grpLayerMapping = new GroupBox();
            grpLayerMapping.Text = " 🎨 3. Bảng Quy Đổi Layer Hệ Thống MEP (DataGrid Trực Quan) ";
            grpLayerMapping.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            grpLayerMapping.ForeColor = Color.FromArgb(245, 158, 11);
            grpLayerMapping.Dock = DockStyle.Fill;
            grpLayerMapping.Padding = new Padding(10, 6, 10, 6);

            // Matrix Toolbar
            pnlMatrixToolbar = new System.Windows.Forms.Panel();
            pnlMatrixToolbar.Dock = DockStyle.Top;
            pnlMatrixToolbar.Height = 38;
            pnlMatrixToolbar.BackColor = Color.FromArgb(16, 22, 32);
            pnlMatrixToolbar.Padding = new Padding(4, 4, 4, 4);

            lblFilterCategory = new Label();
            lblFilterCategory.Text = "Lọc:";
            lblFilterCategory.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            lblFilterCategory.ForeColor = Color.FromArgb(148, 163, 184);
            lblFilterCategory.Location = new Point(6, 10);
            lblFilterCategory.AutoSize = true;

            btnFilterAll = CreateFilterButton("Tất Cả", MepCategoryGroup.All, true);
            btnFilterAll.Location = new Point(38, 5);

            btnFilterDuct = CreateFilterButton("Ống Gió", MepCategoryGroup.Ducts, false);
            btnFilterDuct.Location = new Point(104, 5);

            btnFilterPipe = CreateFilterButton("Ống Nước", MepCategoryGroup.Pipes, false);
            btnFilterPipe.Location = new Point(178, 5);

            btnFilterEquip = CreateFilterButton("Thiết Bị", MepCategoryGroup.Equipment, false);
            btnFilterEquip.Location = new Point(258, 5);

            lblPreset = new Label();
            lblPreset.Text = "Chuẩn:";
            lblPreset.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            lblPreset.ForeColor = Color.FromArgb(203, 213, 225);
            lblPreset.Location = new Point(340, 10);
            lblPreset.AutoSize = true;

            cboPresets = new System.Windows.Forms.ComboBox();
            cboPresets.DropDownStyle = ComboBoxStyle.DropDownList;
            cboPresets.BackColor = Color.FromArgb(15, 23, 42);
            cboPresets.ForeColor = Color.FromArgb(52, 211, 153);
            cboPresets.FlatStyle = FlatStyle.Flat;
            cboPresets.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            cboPresets.Location = new Point(392, 7);
            cboPresets.Size = new Size(165, 23);
            cboPresets.Items.AddRange(new object[] {
                "Preset TCVN MEP",
                "Preset AIA / NCS",
                "Preset Tùy Chỉnh (Đã Lưu)"
            });
            cboPresets.SelectedIndex = 0;
            cboPresets.SelectedIndexChanged += CboPresets_SelectedIndexChanged;

            btnSaveDefault = CreateSmallButton("💾 Lưu Mặc Định", Color.FromArgb(79, 70, 229));
            btnSaveDefault.Location = new Point(563, 5);
            btnSaveDefault.Size = new Size(125, 28);
            btnSaveDefault.Click += BtnSaveDefault_Click;

            btnLoadDefault = CreateSmallButton("🔄 Nạp Đã Lưu", Color.FromArgb(13, 148, 136));
            btnLoadDefault.Location = new Point(694, 5);
            btnLoadDefault.Size = new Size(108, 28);
            btnLoadDefault.Click += BtnLoadDefault_Click;

            btnLoadProjectSys = CreateSmallButton("⚡ Quét Lại Hệ Thống", Color.FromArgb(2, 132, 199));
            btnLoadProjectSys.Location = new Point(808, 5);
            btnLoadProjectSys.Size = new Size(130, 28);
            btnLoadProjectSys.Click += BtnLoadProjectSys_Click;

            btnAddRow = CreateSmallButton("➕ Thêm Dòng", Color.FromArgb(5, 150, 105));
            btnAddRow.Location = new Point(932, 5);
            btnAddRow.Size = new Size(95, 28);
            btnAddRow.Click += BtnAddRow_Click;

            btnSaveJson = CreateSmallButton("📁 Xuất JSON", Color.FromArgb(51, 65, 85));
            btnSaveJson.Location = new Point(1034, 5);
            btnSaveJson.Size = new Size(98, 28);
            btnSaveJson.Click += BtnSaveJson_Click;

            btnLoadJson = CreateSmallButton("📂 Nhập JSON", Color.FromArgb(51, 65, 85));
            btnLoadJson.Location = new Point(1138, 5);
            btnLoadJson.Size = new Size(98, 28);
            btnLoadJson.Click += BtnLoadJson_Click;

            pnlMatrixToolbar.Controls.Add(lblFilterCategory);
            pnlMatrixToolbar.Controls.Add(btnFilterAll);
            pnlMatrixToolbar.Controls.Add(btnFilterDuct);
            pnlMatrixToolbar.Controls.Add(btnFilterPipe);
            pnlMatrixToolbar.Controls.Add(btnFilterEquip);
            pnlMatrixToolbar.Controls.Add(lblPreset);
            pnlMatrixToolbar.Controls.Add(cboPresets);
            pnlMatrixToolbar.Controls.Add(btnSaveDefault);
            pnlMatrixToolbar.Controls.Add(btnLoadDefault);
            pnlMatrixToolbar.Controls.Add(btnLoadProjectSys);
            pnlMatrixToolbar.Controls.Add(btnAddRow);
            pnlMatrixToolbar.Controls.Add(btnSaveJson);
            pnlMatrixToolbar.Controls.Add(btnLoadJson);

            // DataGridView Mappings
            dgvMappings = new DataGridView();
            dgvMappings.Dock = DockStyle.Fill;
            dgvMappings.BackgroundColor = Color.FromArgb(15, 23, 42);
            dgvMappings.BorderStyle = BorderStyle.None;
            dgvMappings.GridColor = Color.FromArgb(51, 65, 85);
            dgvMappings.RowHeadersVisible = false;
            dgvMappings.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvMappings.AllowUserToAddRows = false;
            dgvMappings.AllowUserToDeleteRows = false;
            dgvMappings.AllowUserToResizeRows = false;
            dgvMappings.EnableHeadersVisualStyles = false;
            dgvMappings.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            dgvMappings.RowTemplate.Height = 28;
            dgvMappings.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvMappings.ColumnHeadersHeight = 36;

            dgvMappings.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 41, 59);
            dgvMappings.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(248, 250, 252);
            dgvMappings.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dgvMappings.DefaultCellStyle.BackColor = Color.FromArgb(15, 23, 42);
            dgvMappings.DefaultCellStyle.ForeColor = Color.FromArgb(226, 232, 240);
            dgvMappings.DefaultCellStyle.SelectionBackColor = Color.FromArgb(30, 58, 138);
            dgvMappings.DefaultCellStyle.SelectionForeColor = Color.White;

            SetupGridColumns();
            dgvMappings.CellPainting += DgvMappings_CellPainting;
            dgvMappings.CellClick += DgvMappings_CellClick;
            dgvMappings.CellDoubleClick += DgvMappings_CellClick;

            // Matrix Footer
            pnlMatrixFooter = new System.Windows.Forms.Panel();
            pnlMatrixFooter.Dock = DockStyle.Bottom;
            pnlMatrixFooter.Height = 28;
            pnlMatrixFooter.BackColor = Color.FromArgb(16, 22, 32);
            pnlMatrixFooter.Padding = new Padding(6, 3, 6, 3);

            lblMatrixFooterNote = new Label();
            lblMatrixFooterNote.Text = "💡 Nhấp đúp vào ô để sửa trực tiếp Tên Layer và Màu sắc (ACI 1-255). Bấm '💾 Lưu Mặc Định' để Add-in tự nhớ cho các lần sau.";
            lblMatrixFooterNote.Font = new Font("Segoe UI", 8.5F, FontStyle.Italic);
            lblMatrixFooterNote.ForeColor = Color.FromArgb(148, 163, 184);
            lblMatrixFooterNote.Dock = DockStyle.Left;
            lblMatrixFooterNote.AutoSize = true;

            btnDeleteRow = CreateSmallButton("❌ Xóa Dòng Đã Chọn", Color.FromArgb(127, 29, 29));
            btnDeleteRow.Dock = DockStyle.Right;
            btnDeleteRow.Size = new Size(160, 22);
            btnDeleteRow.Click += BtnDeleteRow_Click;

            pnlMatrixFooter.Controls.Add(lblMatrixFooterNote);
            pnlMatrixFooter.Controls.Add(btnDeleteRow);

            grpLayerMapping.Controls.Add(dgvMappings);
            grpLayerMapping.Controls.Add(pnlMatrixToolbar);
            grpLayerMapping.Controls.Add(pnlMatrixFooter);
            pnlMatrixToolbar.BringToFront();
            pnlMatrixFooter.BringToFront();
            dgvMappings.SendToBack();

            // ==================== 3. BOTTOM ACTION & STATUS PANEL ====================
            pnlBottom = new System.Windows.Forms.Panel();
            pnlBottom.Dock = DockStyle.Bottom;
            pnlBottom.Height = 72;
            pnlBottom.BackColor = Color.FromArgb(15, 20, 28);
            pnlBottom.Padding = new Padding(16, 8, 16, 8);

            lblStatus = new Label();
            lblStatus.Text = "Sẵn sàng xuất sang AutoCAD DWG...";
            lblStatus.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            lblStatus.ForeColor = Color.FromArgb(56, 189, 248);
            lblStatus.Location = new Point(16, 10);
            lblStatus.AutoSize = true;

            lblModelessNotice = new Label();
            lblModelessNotice.Text = "✔ Chế độ Modeless 100%: Kỹ sư hoàn toàn tự do thao tác trên bản vẽ Revit. Nhấn phím ESC để thoát.";
            lblModelessNotice.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
            lblModelessNotice.ForeColor = Color.FromArgb(52, 211, 153);
            lblModelessNotice.Location = new Point(16, 38);
            lblModelessNotice.AutoSize = true;

            btnExport = new System.Windows.Forms.Button();
            btnExport.Text = "🚀 BẮT ĐẦU XUẤT CAD & QUY ĐỔI LAYER MEP";
            btnExport.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnExport.ForeColor = Color.White;
            btnExport.BackColor = Color.FromArgb(5, 150, 105);
            btnExport.FlatStyle = FlatStyle.Flat;
            btnExport.FlatAppearance.BorderSize = 0;
            btnExport.Size = new Size(350, 46);
            btnExport.Location = new Point(780, 12);
            btnExport.Cursor = Cursors.Hand;
            btnExport.Click += BtnExport_Click;

            btnClose = new System.Windows.Forms.Button();
            btnClose.Text = "Đóng (ESC)";
            btnClose.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            btnClose.ForeColor = Color.FromArgb(226, 232, 240);
            btnClose.BackColor = Color.FromArgb(51, 65, 85);
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Size = new Size(110, 46);
            btnClose.Location = new Point(1145, 12);
            btnClose.Cursor = Cursors.Hand;
            btnClose.Click += delegate { this.Close(); };

            pnlBottom.Controls.Add(lblStatus);
            pnlBottom.Controls.Add(lblModelessNotice);
            pnlBottom.Controls.Add(btnExport);
            pnlBottom.Controls.Add(btnClose);

            // ==================== 4. ADD VÀO FORM THEO THỨ TỰ DOCK CHUẨN XÁC 100% ====================
            this.Controls.Add(grpLayerMapping);
            this.Controls.Add(pnlMasterTop);
            this.Controls.Add(pnlBottom);

            pnlMasterTop.SendToBack();
            pnlBottom.SendToBack();
            grpLayerMapping.BringToFront();

            this.CancelButton = btnClose;

            this.ResumeLayout(false);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                this.Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void SafeBeginInvoke(Action action)
        {
            if (action == null || this.IsDisposed) return;
            try
            {
                if (this.IsHandleCreated)
                {
                    this.BeginInvoke((MethodInvoker)delegate { action(); });
                }
                else
                {
                    action();
                }
            }
            catch
            {
                try { action(); } catch { }
            }
        }

        private void SetupGridColumns()
        {
            dgvMappings.Columns.Clear();

            DataGridViewCheckBoxColumn colEnabled = new DataGridViewCheckBoxColumn();
            colEnabled.Name = "colEnabled";
            colEnabled.HeaderText = "Dùng";
            colEnabled.Width = 45;
            dgvMappings.Columns.Add(colEnabled);

            DataGridViewTextBoxColumn colIndex = new DataGridViewTextBoxColumn();
            colIndex.Name = "colIndex";
            colIndex.HeaderText = "STT";
            colIndex.Width = 40;
            colIndex.ReadOnly = true;
            colIndex.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvMappings.Columns.Add(colIndex);

            DataGridViewTextBoxColumn colGroup = new DataGridViewTextBoxColumn();
            colGroup.Name = "colGroup";
            colGroup.HeaderText = "Phân Loại MEP";
            colGroup.Width = 100;
            colGroup.ReadOnly = true;
            dgvMappings.Columns.Add(colGroup);

            DataGridViewTextBoxColumn colSystem = new DataGridViewTextBoxColumn();
            colSystem.Name = "colSystem";
            colSystem.HeaderText = "Hệ Thống Revit (MEP System)";
            colSystem.Width = 230;
            colSystem.ReadOnly = true;
            dgvMappings.Columns.Add(colSystem);

            DataGridViewTextBoxColumn colTargetLayer = new DataGridViewTextBoxColumn();
            colTargetLayer.Name = "colTargetLayer";
            colTargetLayer.HeaderText = "🔄 CHUYỂN ĐỔI SANG LAYER CAD (Tên Layer)";
            colTargetLayer.Width = 260;
            colTargetLayer.DefaultCellStyle.ForeColor = Color.FromArgb(52, 211, 153);
            colTargetLayer.DefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            dgvMappings.Columns.Add(colTargetLayer);

            DataGridViewTextBoxColumn colColor = new DataGridViewTextBoxColumn();
            colColor.Name = "colColor";
            colColor.HeaderText = "🎨 MÀU SẮC CAD (ACI 1-255)";
            colColor.Width = 155;
            dgvMappings.Columns.Add(colColor);

            DataGridViewTextBoxColumn colLinetype = new DataGridViewTextBoxColumn();
            colLinetype.Name = "colLinetype";
            colLinetype.HeaderText = "Kiểu Nét (Linetype)";
            colLinetype.Width = 110;
            dgvMappings.Columns.Add(colLinetype);

            DataGridViewTextBoxColumn colLineweight = new DataGridViewTextBoxColumn();
            colLineweight.Name = "colLineweight";
            colLineweight.HeaderText = "Độ Dày (mm)";
            colLineweight.Width = 100;
            colLineweight.DefaultCellStyle.ForeColor = Color.FromArgb(245, 158, 11);
            dgvMappings.Columns.Add(colLineweight);
        }

        private void RefreshDataFromDocument()
        {
            if (_doc == null) return;

            _isLoading = true;
            try
            {
                // 1. Nạp Active View
                _activeViewTarget = CadExportMepService.GetActiveViewTarget(_doc);
                if (_activeViewTarget != null)
                {
                    lblActiveViewName.Text = _activeViewTarget.GetDisplayName();
                }
                else
                {
                    lblActiveViewName.Text = "(Chưa xác định View đang mở)";
                }

                // 2. Nạp Danh Sách Sheet
                clbSheets.Items.Clear();
                _sheetTargets = CadExportMepService.GetProjectSheetTargets(_doc);

                for (int i = 0; i < _sheetTargets.Count; i++)
                {
                    CadExportTargetItem s = _sheetTargets[i];
                    clbSheets.Items.Add(s.GetDisplayName(), true);
                }

                // 3. Nạp Danh Sách View Dự Án
                clbViews.Items.Clear();
                _viewTargets = CadExportMepService.GetProjectViewTargets(_doc);

                for (int i = 0; i < _viewTargets.Count; i++)
                {
                    CadExportTargetItem v = _viewTargets[i];
                    clbViews.Items.Add(v.GetDisplayName(), true);
                }
            }
            finally
            {
                _isLoading = false;
                UpdateScopeStatus();
            }
        }

        private void SwitchScopeMode(CadExportScope mode)
        {
            _currentSettings.ExportScope = mode;

            pnlActiveViewCard.Visible = (mode == CadExportScope.ActiveView);
            pnlSheetsContainer.Visible = (mode == CadExportScope.Sheets);
            pnlViewsContainer.Visible = (mode == CadExportScope.ProjectViews);

            radScopeActiveView.Font = new Font("Segoe UI", 8.5F, mode == CadExportScope.ActiveView ? FontStyle.Bold : FontStyle.Regular);
            radScopeActiveView.ForeColor = mode == CadExportScope.ActiveView ? Color.FromArgb(52, 211, 153) : Color.FromArgb(203, 213, 225);

            radScopeSheets.Font = new Font("Segoe UI", 8.5F, mode == CadExportScope.Sheets ? FontStyle.Bold : FontStyle.Regular);
            radScopeSheets.ForeColor = mode == CadExportScope.Sheets ? Color.FromArgb(52, 211, 153) : Color.FromArgb(203, 213, 225);

            radScopeViews.Font = new Font("Segoe UI", 8.5F, mode == CadExportScope.ProjectViews ? FontStyle.Bold : FontStyle.Regular);
            radScopeViews.ForeColor = mode == CadExportScope.ProjectViews ? Color.FromArgb(52, 211, 153) : Color.FromArgb(203, 213, 225);

            UpdateScopeStatus();
            ScanSystemsFromCurrentSelection();
        }

        private void SetAllListChecked(CheckedListBox clb, bool check)
        {
            for (int i = 0; i < clb.Items.Count; i++)
            {
                clb.SetItemChecked(i, check);
            }
            UpdateScopeStatus();
            ScanSystemsFromCurrentSelection();
        }

        private void UpdateScopeStatus()
        {
            int layerCount = _allMappings != null ? _allMappings.Count : 0;

            if (_currentSettings.ExportScope == CadExportScope.ActiveView)
            {
                string vName = _activeViewTarget != null ? _activeViewTarget.ItemName : "View hiện hành";
                lblStatus.Text = string.Format(CultureInfo.InvariantCulture, "Sẵn sàng xuất View Đang Vẽ [{0}] sang AutoCAD DWG ({1} Layer MEP)", vName, layerCount);
            }
            else if (_currentSettings.ExportScope == CadExportScope.Sheets)
            {
                int count = clbSheets.CheckedItems.Count;
                lblSheetCount.Text = string.Format(CultureInfo.InvariantCulture, "Đã chọn: {0} / {1} bản vẽ", count, clbSheets.Items.Count);
                lblStatus.Text = string.Format(CultureInfo.InvariantCulture, "Sẵn sàng xuất {0} bản vẽ Sheet sang AutoCAD DWG ({1} Layer MEP)", count, layerCount);
            }
            else
            {
                int count = clbViews.CheckedItems.Count;
                lblViewCount.Text = string.Format(CultureInfo.InvariantCulture, "Đã chọn: {0} / {1} view", count, clbViews.Items.Count);
                lblStatus.Text = string.Format(CultureInfo.InvariantCulture, "Sẵn sàng xuất {0} View dự án sang AutoCAD DWG ({1} Layer MEP)", count, layerCount);
            }
        }

        private void ApplyPreset(string presetCode)
        {
            if (presetCode == "TCVN")
            {
                _allMappings = CadExportMepService.GenerateTcvnMepPreset(_doc);
            }
            else if (presetCode == "AIA")
            {
                _allMappings = CadExportMepService.GenerateAiaNcsPreset(_doc);
            }

            RenderMappingRows();
        }

        private void RenderMappingRows()
        {
            SyncGridDataToMappings();

            dgvMappings.Rows.Clear();
            if (_allMappings == null) return;

            int stt = 1;
            for (int i = 0; i < _allMappings.Count; i++)
            {
                CadLayerMappingItem item = _allMappings[i];
                if (_activeFilter != MepCategoryGroup.All && item.CategoryGroup != _activeFilter)
                {
                    continue;
                }

                string groupStr = "Ống Gió";
                if (item.CategoryGroup == MepCategoryGroup.Pipes) groupStr = "Ống Nước";
                else if (item.CategoryGroup == MepCategoryGroup.Equipment) groupStr = "Thiết Bị";
                else if (item.CategoryGroup == MepCategoryGroup.Electrical) groupStr = "Điện";

                string lwStr = item.Lineweight > 0
                    ? string.Format(CultureInfo.InvariantCulture, "{0:0.00} mm", item.Lineweight / 100.0)
                    : "Mặc định";

                int rowIdx = dgvMappings.Rows.Add(
                    item.IsEnabled,
                    stt++,
                    groupStr,
                    item.RevitSystemName,
                    item.TargetCadLayerName,
                    item.ColorNumber.ToString(),
                    item.Linetype,
                    lwStr
                );

                dgvMappings.Rows[rowIdx].Tag = item;
            }

            UpdateScopeStatus();
        }

        private void SyncGridDataToMappings()
        {
            for (int i = 0; i < dgvMappings.Rows.Count; i++)
            {
                DataGridViewRow r = dgvMappings.Rows[i];
                CadLayerMappingItem item = r.Tag as CadLayerMappingItem;
                if (item != null)
                {
                    if (r.Cells["colEnabled"].Value != null)
                    {
                        item.IsEnabled = Convert.ToBoolean(r.Cells["colEnabled"].Value);
                    }
                    if (r.Cells["colTargetLayer"].Value != null)
                    {
                        item.TargetCadLayerName = r.Cells["colTargetLayer"].Value.ToString().Trim();
                    }
                    if (r.Cells["colColor"].Value != null)
                    {
                        int cNum;
                        if (int.TryParse(r.Cells["colColor"].Value.ToString(), out cNum))
                        {
                            if (cNum > 0 && cNum <= 255) item.ColorNumber = cNum;
                        }
                    }
                    if (r.Cells["colLinetype"].Value != null)
                    {
                        item.Linetype = r.Cells["colLinetype"].Value.ToString().Trim();
                    }
                }
            }
        }

        private void DgvMappings_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == dgvMappings.Columns["colColor"].Index)
            {
                e.Paint(e.CellBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.ContentForeground);

                int aci = 7;
                if (e.Value != null)
                {
                    int.TryParse(e.Value.ToString(), out aci);
                }
                Color swatchColor = CadLayerMappingItem.AciToColor(aci);

                int swatchSize = 14;
                int swatchX = e.CellBounds.X + 8;
                int swatchY = e.CellBounds.Y + (e.CellBounds.Height - swatchSize) / 2;
                Rectangle swatchRect = new Rectangle(swatchX, swatchY, swatchSize, swatchSize);

                using (SolidBrush b = new SolidBrush(swatchColor))
                {
                    e.Graphics.FillRectangle(b, swatchRect);
                }
                using (Pen p = new Pen(Color.FromArgb(180, 255, 255, 255)))
                {
                    e.Graphics.DrawRectangle(p, swatchRect);
                }

                string text = string.Format(CultureInfo.InvariantCulture, "Index {0}", aci);
                Rectangle textRect = new Rectangle(swatchX + swatchSize + 8, e.CellBounds.Y, e.CellBounds.Width - (swatchSize + 16), e.CellBounds.Height);
                TextRenderer.DrawText(
                    e.Graphics,
                    text,
                    e.CellStyle.Font,
                    textRect,
                    Color.FromArgb(56, 189, 248),
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left
                );

                e.Handled = true;
            }
        }

        private void DgvMappings_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            // Bắt sự kiện click vào cột Màu Sắc CAD (colColor)
            if (e.ColumnIndex == dgvMappings.Columns["colColor"].Index)
            {
                DataGridViewRow row = dgvMappings.Rows[e.RowIndex];
                CadLayerMappingItem item = row.Tag as CadLayerMappingItem;
                if (item == null) return;

                int currentAci = item.ColorNumber;
                using (CadColorPickerDialog dlg = new CadColorPickerDialog(currentAci))
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        int newAci = dlg.SelectedColorIndex;
                        item.ColorNumber = newAci;
                        row.Cells["colColor"].Value = newAci.ToString();
                        dgvMappings.InvalidateCell(e.ColumnIndex, e.RowIndex);

                        lblMatrixFooterNote.Text = string.Format(CultureInfo.InvariantCulture,
                            "✔ Đã chọn màu AutoCAD ACI {0} cho hệ thống '{1}'. Bấm '💾 Lưu Mặc Định' để ghi nhớ cho lần sau.",
                            newAci, item.RevitSystemName);
                        lblMatrixFooterNote.ForeColor = Color.FromArgb(52, 211, 153);
                    }
                }
            }
        }

        public List<ElementId> GetCurrentSelectedTargetIds()
        {
            List<ElementId> targetIds = new List<ElementId>();
            if (_currentSettings.ExportScope == CadExportScope.ActiveView)
            {
                if (_activeViewTarget != null && _activeViewTarget.Id != null && _activeViewTarget.Id != ElementId.InvalidElementId)
                {
                    targetIds.Add(_activeViewTarget.Id);
                }
                else if (_doc != null && _doc.ActiveView != null)
                {
                    targetIds.Add(_doc.ActiveView.Id);
                }
            }
            else if (_currentSettings.ExportScope == CadExportScope.Sheets)
            {
                if (clbSheets != null)
                {
                    for (int i = 0; i < clbSheets.Items.Count; i++)
                    {
                        if (clbSheets.GetItemChecked(i) && i < _sheetTargets.Count)
                        {
                            targetIds.Add(_sheetTargets[i].Id);
                        }
                    }
                }
            }
            else if (_currentSettings.ExportScope == CadExportScope.ProjectViews)
            {
                if (clbViews != null)
                {
                    for (int i = 0; i < clbViews.Items.Count; i++)
                    {
                        if (clbViews.GetItemChecked(i) && i < _viewTargets.Count)
                        {
                            targetIds.Add(_viewTargets[i].Id);
                        }
                    }
                }
            }
            return targetIds;
        }

        private void ScanSystemsFromCurrentSelection()
        {
            if (_doc == null || _isLoading) return;

            try
            {
                List<ElementId> targetIds = GetCurrentSelectedTargetIds();
                _allMappings = CadExportMepService.ScanVisibleMepSystemsInViews(_doc, targetIds, _allMappings);
                RenderMappingRows();

                int sysCount = _allMappings != null ? _allMappings.Count : 0;
                lblMatrixFooterNote.Text = string.Format(CultureInfo.InvariantCulture,
                    "✔ Tự động nhận diện {0} hệ thống MEP đang hiển thị (đã loại bỏ 100% hệ thống bị ẩn). Click vào ô màu để chọn màu AutoCAD.", sysCount);
                lblMatrixFooterNote.ForeColor = Color.FromArgb(52, 211, 153);
            }
            catch (Exception ex)
            {
                lblMatrixFooterNote.Text = "Lỗi quét hệ thống: " + ex.Message;
                lblMatrixFooterNote.ForeColor = Color.FromArgb(239, 68, 68);
            }
        }

        // ==================== CÁC SỰ KIỆN NÚT BẤM ====================

        private void BtnSaveDefault_Click(object sender, EventArgs e)
        {
            SyncGridDataToMappings();
            bool ok = CadExportMepService.AutoSaveDefaultConfig(_allMappings);
            if (ok)
            {
                lblMatrixFooterNote.Text = "✔ Đã lưu cài đặt Layer CAD mặc định thành công! Lần sau mở Add-in sẽ tự động áp dụng.";
                lblMatrixFooterNote.ForeColor = Color.FromArgb(52, 211, 153);
                MessageBox.Show(
                    "ĐÃ LƯU CÀI ĐẶT LAYER CAD MẶC ĐỊNH THÀNH CÔNG!\n\n" +
                    "Toàn bộ Tên Layer CAD và Mã Màu Sắc hiện tại đã được lưu vào hệ thống.\n" +
                    "Mỗi lần mở Add-in sau này, các thiết lập này sẽ tự động được nạp lại.",
                    "HDTOOL - Lưu Cài Đặt Mặc Định",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            else
            {
                MessageBox.Show("Không thể lưu cài đặt mặc định.", "HDTOOL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BtnLoadDefault_Click(object sender, EventArgs e)
        {
            string savedPath = CadExportMepService.GetDefaultConfigFilePath();
            if (File.Exists(savedPath))
            {
                _allMappings = CadExportMepService.AutoLoadDefaultConfig(_doc);
                RenderMappingRows();
                cboPresets.SelectedIndex = 2; // Custom
                MessageBox.Show("Đã nạp lại toàn bộ cấu hình Layer CAD đã lưu thành công!", "HDTOOL", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Chưa có cấu hình mặc định nào được lưu trước đó. Hệ thống sẽ áp dụng Preset chuẩn TCVN.", "HDTOOL", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ApplyPreset("TCVN");
            }
        }
        private void CboPresets_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cboPresets.SelectedIndex == 0) ApplyPreset("TCVN");
            else if (cboPresets.SelectedIndex == 1) ApplyPreset("AIA");
        }

        private void BtnLoadProjectSys_Click(object sender, EventArgs e)
        {
            ScanSystemsFromCurrentSelection();
            MessageBox.Show(
                string.Format(CultureInfo.InvariantCulture,
                    "ĐÃ QUÉT XONG HỆ THỐNG MEP TRONG VIEW!\n\n" +
                    "Tìm thấy {0} hệ thống MEP đang hiển thị trên bản vẽ được chọn.\n" +
                    "Toàn bộ các hệ thống đã bị ẩn (View Filter, Category hoặc Hide Elements) đã được loại bỏ 100%.",
                    _allMappings != null ? _allMappings.Count : 0),
                "HDTOOL - Nhận Diện Hệ Thống MEP",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }

        private void BtnSaveJson_Click(object sender, EventArgs e)
        {
            SyncGridDataToMappings();

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Title = "Lưu Cấu Hình Bảng Quy Đổi Layer MEP ra file JSON";
                sfd.Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*";
                sfd.FileName = "HDTOOL_MepLayerMapping_Config.json";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        _currentSettings.LayerMappings = _allMappings;
                        _currentSettings.PresetName = cboPresets.SelectedItem != null ? cboPresets.SelectedItem.ToString() : "Custom";
                        _currentSettings.SaveToFile(sfd.FileName);

                        MessageBox.Show("Đã lưu cấu hình JSON thành công!", "HDTOOL", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Lỗi lưu file JSON: " + ex.Message, "HDTOOL", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void BtnLoadJson_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Nạp Cấu Hình Bảng Quy Đổi Layer MEP từ file JSON";
                ofd.Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        CadExportMepSettings loaded = CadExportMepSettings.LoadFromFile(ofd.FileName);
                        if (loaded != null && loaded.LayerMappings != null && loaded.LayerMappings.Count > 0)
                        {
                            _allMappings = loaded.LayerMappings;
                            RenderMappingRows();
                            cboPresets.SelectedIndex = 2; // Custom
                            MessageBox.Show(
                                string.Format(CultureInfo.InvariantCulture, "Đã nạp thành công {0} quy tắc Layer từ file JSON!", _allMappings.Count),
                                "HDTOOL",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Lỗi đọc file JSON: " + ex.Message, "HDTOOL", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void BtnAddRow_Click(object sender, EventArgs e)
        {
            SyncGridDataToMappings();

            CadLayerMappingItem newItem = new CadLayerMappingItem(
                _activeFilter == MepCategoryGroup.All ? MepCategoryGroup.Ducts : _activeFilter,
                "Tùy chọn (Custom)",
                BuiltInCategory.OST_DuctCurves,
                "New System",
                "M-CUSTOM-LAYER",
                7,
                "Continuous",
                35,
                "Custom"
            );

            _allMappings.Add(newItem);
            RenderMappingRows();
        }

        private void BtnDeleteRow_Click(object sender, EventArgs e)
        {
            if (dgvMappings.SelectedRows.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất 1 dòng trong bảng để xóa.", "HDTOOL", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SyncGridDataToMappings();

            List<CadLayerMappingItem> itemsToRemove = new List<CadLayerMappingItem>();
            foreach (DataGridViewRow r in dgvMappings.SelectedRows)
            {
                CadLayerMappingItem item = r.Tag as CadLayerMappingItem;
                if (item != null) itemsToRemove.Add(item);
            }

            for (int i = 0; i < itemsToRemove.Count; i++)
            {
                _allMappings.Remove(itemsToRemove[i]);
            }

            RenderMappingRows();
        }

        private void BtnBrowseFolder_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Chọn thư mục lưu các bản vẽ AutoCAD DWG sau khi xuất:";
                fbd.SelectedPath = Directory.Exists(txtOutputFolder.Text) ? txtOutputFolder.Text : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    txtOutputFolder.Text = fbd.SelectedPath;
                    _currentSettings.TargetFolder = fbd.SelectedPath;
                }
            }
        }

        private void BtnExport_Click(object sender, EventArgs e)
        {
            // 1. Xác định danh sách ElementId cần xuất dựa trên Scope Mode
            List<ElementId> targetIds = GetCurrentSelectedTargetIds();

            if (targetIds == null || targetIds.Count == 0)
            {
                if (_currentSettings.ExportScope == CadExportScope.ActiveView)
                {
                    MessageBox.Show("Không tìm thấy View đang vẽ hợp lệ để xuất!", "HDTOOL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else if (_currentSettings.ExportScope == CadExportScope.Sheets)
                {
                    MessageBox.Show("Vui lòng chọn ít nhất 1 bản vẽ Sheet cần xuất sang AutoCAD DWG!", "HDTOOL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    MessageBox.Show("Vui lòng chọn ít nhất 1 View dự án cần xuất sang AutoCAD DWG!", "HDTOOL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return;
            }

            // 2. Kiểm tra thư mục đích
            string outDir = txtOutputFolder.Text.Trim();
            if (string.IsNullOrEmpty(outDir))
            {
                MessageBox.Show("Vui lòng chọn thư mục lưu file DWG hợp lệ!", "HDTOOL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtOutputFolder.Focus();
                return;
            }

            try
            {
                if (!Directory.Exists(outDir))
                {
                    Directory.CreateDirectory(outDir);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể tạo thư mục lưu file: " + ex.Message, "HDTOOL - Lỗi Thư Mục", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            SyncGridDataToMappings();
            CadExportMepService.AutoSaveDefaultConfig(_allMappings);

            // 3. Chuẩn bị Settings
            _currentSettings.TargetFolder = outDir;
            _currentSettings.MergedViews = chkMergedViews.Checked;
            if (cboCadVersion.SelectedIndex == 1) _currentSettings.CadVersion = ACADVersion.R2013;
            else if (cboCadVersion.SelectedIndex == 2) _currentSettings.CadVersion = ACADVersion.R2010;
            else _currentSettings.CadVersion = ACADVersion.R2018;

            _currentSettings.LayerMappings = new List<CadLayerMappingItem>(_allMappings);

            btnExport.Enabled = false;
            lblStatus.Text = string.Format(CultureInfo.InvariantCulture, "⏳ Đang tiến hành xuất {0} đối tượng sang AutoCAD DWG...", targetIds.Count);

            // Chạy trong ExternalEvent của HDToolModelessHost
            HDToolModelessHost.Run(delegate(UIApplication app)
            {
                try
                {
                    Document doc = _doc;
                    if (doc == null && app != null && app.ActiveUIDocument != null)
                    {
                        doc = app.ActiveUIDocument.Document;
                    }

                    if (doc == null)
                    {
                        SafeBeginInvoke(delegate
                        {
                            lblStatus.Text = "⚠ Lỗi: Không có Document Revit hợp lệ.";
                            btnExport.Enabled = true;
                        });
                        return;
                    }

                    string exportMsg;
                    bool ok = CadExportMepService.ExportViewsToCad(doc, targetIds, _currentSettings, out exportMsg);

                    SafeBeginInvoke(delegate
                    {
                        btnExport.Enabled = true;
                        if (ok)
                        {
                            lblStatus.Text = string.Format(CultureInfo.InvariantCulture, "✔ Xuất hoàn tất {0} đối tượng sang AutoCAD DWG!", targetIds.Count);

                            DialogResult dr = MessageBox.Show(
                                string.Format(CultureInfo.InvariantCulture,
                                    "ĐÃ XUẤT THÀNH CÔNG {0} ĐỐI TƯỢNG SANG AUTOCAD DWG!\n\n" +
                                    "Định dạng: {1}\n" +
                                    "Thư mục: {2}\n\n" +
                                    "Bạn có muốn mở ngay thư mục chứa file CAD không?",
                                    targetIds.Count,
                                    _currentSettings.CadVersion.ToString(),
                                    _currentSettings.TargetFolder),
                                "HDTOOL - Xuất CAD Thành Công",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Information
                            );

                            if (dr == DialogResult.Yes)
                            {
                                try
                                {
                                    Process.Start("explorer.exe", _currentSettings.TargetFolder);
                                }
                                catch { }
                            }
                        }
                        else
                        {
                            lblStatus.Text = "⚠ Lỗi: " + exportMsg;
                            MessageBox.Show(exportMsg, "HDTOOL - Lỗi Xuất CAD", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    });
                }
                catch (Exception ex)
                {
                    SafeBeginInvoke(delegate
                    {
                        btnExport.Enabled = true;
                        lblStatus.Text = "⚠ Lỗi khi xuất DWG: " + ex.Message;
                        MessageBox.Show("Lỗi trong quá trình xuất DWG: " + ex.Message, "HDTOOL - Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                Color.FromArgb(6, 78, 59),
                Color.FromArgb(15, 23, 42),
                LinearGradientMode.Horizontal))
            {
                e.Graphics.FillRectangle(lgb, rect);
            }

            using (Pen p = new Pen(Color.FromArgb(30, 41, 59)))
            {
                e.Graphics.DrawLine(p, 0, rect.Height - 1, rect.Width, rect.Height - 1);
            }
        }

        private System.Windows.Forms.Button CreateFilterButton(string text, MepCategoryGroup group, bool isActive)
        {
            System.Windows.Forms.Button btn = new System.Windows.Forms.Button();
            btn.Text = text;
            btn.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            btn.Size = new Size(65, 26);
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 1;
            btn.Cursor = Cursors.Hand;
            UpdateFilterButtonStyle(btn, isActive);

            btn.Click += delegate
            {
                _activeFilter = group;
                btnFilterAll.FlatAppearance.BorderColor = Color.FromArgb(51, 65, 85);
                btnFilterDuct.FlatAppearance.BorderColor = Color.FromArgb(51, 65, 85);
                btnFilterPipe.FlatAppearance.BorderColor = Color.FromArgb(51, 65, 85);
                btnFilterEquip.FlatAppearance.BorderColor = Color.FromArgb(51, 65, 85);

                btnFilterAll.BackColor = Color.FromArgb(30, 41, 59);
                btnFilterDuct.BackColor = Color.FromArgb(30, 41, 59);
                btnFilterPipe.BackColor = Color.FromArgb(30, 41, 59);
                btnFilterEquip.BackColor = Color.FromArgb(30, 41, 59);

                UpdateFilterButtonStyle(btn, true);
                RenderMappingRows();
            };

            return btn;
        }

        private void UpdateFilterButtonStyle(System.Windows.Forms.Button btn, bool isActive)
        {
            if (isActive)
            {
                btn.BackColor = Color.FromArgb(5, 150, 105);
                btn.ForeColor = Color.White;
                btn.FlatAppearance.BorderColor = Color.FromArgb(52, 211, 153);
            }
            else
            {
                btn.BackColor = Color.FromArgb(30, 41, 59);
                btn.ForeColor = Color.FromArgb(203, 213, 225);
                btn.FlatAppearance.BorderColor = Color.FromArgb(51, 65, 85);
            }
        }

        private System.Windows.Forms.Button CreateSmallButton(string text, Color backColor)
        {
            System.Windows.Forms.Button btn = new System.Windows.Forms.Button();
            btn.Text = text;
            btn.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            btn.ForeColor = Color.White;
            btn.BackColor = backColor;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.Cursor = Cursors.Hand;
            return btn;
        }

        private CheckBox CreateCheckBox(string text, bool isChecked, int x, int y)
        {
            CheckBox chk = new CheckBox();
            chk.Text = text;
            chk.Checked = isChecked;
            chk.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
            chk.ForeColor = Color.FromArgb(203, 213, 225);
            chk.Location = new Point(x, y);
            chk.AutoSize = true;
            chk.Cursor = Cursors.Hand;
            return chk;
        }
    }
}
