using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Color = System.Drawing.Color;
using Font = System.Drawing.Font;
using Point = System.Drawing.Point;
using ComboBox = System.Windows.Forms.ComboBox;
using TextBox = System.Windows.Forms.TextBox;

namespace HDTool
{
    /// <summary>
    /// Event handler phục vụ các thao tác tương tác Revit an toàn từ cửa sổ Modeless
    /// </summary>
    public class MepInspectorExternalEventHandler : IExternalEventHandler
    {
        public Action<UIApplication> ActionToRun;

        public void Execute(UIApplication app)
        {
            if (ActionToRun != null)
            {
                try
                {
                    ActionToRun(app);
                }
                catch (Exception ex)
                {
                    try { TaskDialog.Show("HDTOOL MEP", "Lỗi thao tác Revit: " + ex.Message); } catch { }
                }
            }
        }

        public string GetName()
        {
            return "MepInspectorExternalEventHandler";
        }
    }

    /// <summary>
    /// Form giao diện WinForms hiện đại: Thống kê khối lượng phụ kiện & ống nước,
    /// tự động gộp 3 parameters phân mảnh, soi sáng đối tượng tương tác 2 chiều và đưa bảng vào Sheet
    /// </summary>
    /// <summary>
    /// Chế độ lọc bóc tách khối lượng MEP theo lệnh Sếp Hồ:
    /// Bóc ống nước riêng, bóc phụ kiện riêng hoặc thống kê cả hai
    /// </summary>
    public enum MepQuantityFilterMode
    {
        All = 0,
        PipesOnly = 1,
        FittingsOnly = 2
    }

    public class MepQuantityInspectorForm : System.Windows.Forms.Form
    {
        private UIDocument _uidoc;
        private Document _doc;
        private Autodesk.Revit.DB.View _activeView;
        private ExternalEvent _extEvent;
        private MepInspectorExternalEventHandler _handler;

        // Dữ liệu nội bộ
        private List<MepFittingItem> _allItems = new List<MepFittingItem>();
        private List<MepFittingItem> _filteredItems = new List<MepFittingItem>();
        private HashSet<ElementId> _lastSelectedElementIds = new HashSet<ElementId>();

        // Controls Header
        private System.Windows.Forms.Panel pnlHeader;
        private Label lblTitle;
        private Label lblSubtitle;
        private PictureBox picLogo;

        // Filter Controls
        private GroupBox grpFilter;
        private RadioButton radCurrentView;
        private RadioButton radSelection;
        private RadioButton radAllProject;
        private Button btnPickLocalArea;
        private Button btnPickAppendArea;
        private List<ElementId> _customSelectionIds = new List<ElementId>();

        // Bộ chọn Chế Độ Bóc Khối Lượng theo lệnh Sếp Hồ
        private MepQuantityFilterMode _filterMode = MepQuantityFilterMode.All;
        private System.Windows.Forms.Panel pnlModeSelector;
        private Label lblModeTitle;
        private RadioButton radModePipes;
        private RadioButton radModeFittings;
        private RadioButton radModeAll;
        private CheckBox chkIncludeFittings;
        private CheckBox chkIncludePipes;
        private bool _discretePipes = true; // Chế độ tách rời rạc theo lệnh Sếp Hồ
        private System.Windows.Forms.Panel pnlPipeMode;
        private Label lblPipeModeTitle;
        private RadioButton radPipeDiscrete;
        private RadioButton radPipeSum;
        private Label lblFilterArea;
        private ComboBox cboArea;
        private Label lblFilterLevel;
        private ComboBox cboLevel;
        private Label lblFilterSystem;
        private ComboBox cboSystem;
        private TextBox txtSearch;
        private Button btnRefresh;

        // Cây Thư Mục Khối Lượng Bên Trái
        private System.Windows.Forms.Panel pnlTreeLeft;
        private Label lblTreeTitle;
        private TreeView tvQuantityTree;
        private Button btnTreeCreateSchedule;
        private string _treeSelectedLevel = null;
        private string _treeSelectedArea = null;
        private string _treeSelectedSystem = null;

        // Data Table
        private DataGridView dgvItems;
        private Label lblTableSummary;

        // Inspection / Highlight Toolbar
        private System.Windows.Forms.Panel pnlHighlightBar;
        private Label lblHighlightStatus;
        private CheckBox chkAutoHighlight;
        private Button btnHighlightAllPicked;
        private Button btnClearHighlight;
        private Button btnZoom;
        private Button btnIsolate;
        private Button btnResetIsolate;
        private Button btnToggleGridCols;
        private Button btnAddProjectParamCol;
        private Button btnPickHighlightOnSheet;
        private List<string> _customParamColumns = new List<string>();

        // Bottom Action Bar
        private System.Windows.Forms.Panel pnlBottom;
        private Button btnQuickCreateSchedule;
        private Button btnPlaceOnSheet;
        private Button btnSyncParams;
        private Button btnExportExcel;
        private Button btnCopyClipboard;
        private Button btnClose;

        public MepQuantityFilterMode FilterMode
        {
            get { return _filterMode; }
        }

        public MepQuantityInspectorForm(UIDocument uidoc, ExternalEvent extEvent, MepInspectorExternalEventHandler handler)
            : this(uidoc, extEvent, handler, MepQuantityFilterMode.All)
        {
        }

        public MepQuantityInspectorForm(UIDocument uidoc, ExternalEvent extEvent, MepInspectorExternalEventHandler handler, MepQuantityFilterMode initialMode)
        {
            _uidoc = uidoc;
            _doc = uidoc != null ? uidoc.Document : null;
            _activeView = uidoc != null ? uidoc.ActiveView : null;
            _extEvent = extEvent;
            _handler = handler;

            // Sáº¾P Há»’: Tá»± Ä‘á»™ng táº£i láº¡i toÃ n bá»™ tÃ¹y chá»n ngÆ°á»i dÃ¹ng Ä‘Ã£ lÆ°u á»Ÿ láº§n lÃ m viá»‡c trÆ°á»›c
            var settings = MepUserSettings.Instance;
            if (initialMode == MepQuantityFilterMode.All && settings.FilterMode != 0)
            {
                initialMode = (MepQuantityFilterMode)settings.FilterMode;
            }
            _filterMode = initialMode;
            _discretePipes = settings.DiscretePipes;

            if (settings.CustomParamColumns != null && settings.CustomParamColumns.Count > 0)
            {
                _customParamColumns = new List<string>(settings.CustomParamColumns);
            }

            CheckInitialSelection(initialMode);

            InitializeComponent();
            ApplyInitialMode(initialMode);

            RestoreUserSettingsToUi(settings);

            LoadDataAndRefreshUi();
        }

        private void RestoreUserSettingsToUi(MepUserSettings settings)
        {
            if (settings == null) return;

            if (radPipeDiscrete != null && radPipeSum != null)
            {
                if (settings.DiscretePipes)
                {
                    radPipeDiscrete.Checked = true;
                    radPipeSum.Checked = false;
                }
                else
                {
                    radPipeDiscrete.Checked = false;
                    radPipeSum.Checked = true;
                }
            }

            if (chkAutoHighlight != null)
            {
                chkAutoHighlight.Checked = settings.AutoHighlight;
            }

            if (_customSelectionIds == null || _customSelectionIds.Count == 0)
            {
                if (settings.ScopeOption == 0 && radCurrentView != null) radCurrentView.Checked = true;
                else if (settings.ScopeOption == 2 && radAllProject != null) radAllProject.Checked = true;
            }

            RestoreColumnSettings();
        }

        public void SaveColumnSettings()
        {
            try
            {
                if (dgvItems == null || dgvItems.Columns.Count == 0) return;
                var settings = MepUserSettings.Instance;
                if (settings.Columns == null) settings.Columns = new Dictionary<string, MepColumnConfig>(StringComparer.OrdinalIgnoreCase);

                foreach (DataGridViewColumn col in dgvItems.Columns)
                {
                    if (col == null || string.IsNullOrEmpty(col.HeaderText)) continue;
                    string header = col.HeaderText.Trim();
                    settings.Columns[header] = new MepColumnConfig(col.DisplayIndex, col.Width, col.Visible);
                }
                settings.CustomParamColumns = new List<string>(_customParamColumns);
                settings.Save();
            }
            catch { }
        }

        public void RestoreColumnSettings()
        {
            try
            {
                if (dgvItems == null || dgvItems.Columns.Count == 0) return;
                var settings = MepUserSettings.Instance;
                if (settings.Columns == null || settings.Columns.Count == 0) return;

                foreach (DataGridViewColumn col in dgvItems.Columns)
                {
                    if (col == null || string.IsNullOrEmpty(col.HeaderText)) continue;
                    string header = col.HeaderText.Trim();
                    MepColumnConfig cfg;
                    if (settings.Columns.TryGetValue(header, out cfg) && cfg != null)
                    {
                        if (cfg.Width > 20 && cfg.Width < 1000)
                        {
                            col.Width = cfg.Width;
                        }
                        col.Visible = cfg.Visible;
                    }
                }

                var orderedConfigs = new List<KeyValuePair<DataGridViewColumn, int>>();
                foreach (DataGridViewColumn col in dgvItems.Columns)
                {
                    if (col == null || string.IsNullOrEmpty(col.HeaderText)) continue;
                    string header = col.HeaderText.Trim();
                    MepColumnConfig cfg;
                    if (settings.Columns.TryGetValue(header, out cfg) && cfg != null && cfg.DisplayIndex >= 0)
                    {
                        orderedConfigs.Add(new KeyValuePair<DataGridViewColumn, int>(col, cfg.DisplayIndex));
                    }
                }

                orderedConfigs.Sort((a, b) => a.Value.CompareTo(b.Value));
                int curDisplayIdx = 0;
                foreach (var pair in orderedConfigs)
                {
                    if (curDisplayIdx < dgvItems.Columns.Count)
                    {
                        try { pair.Key.DisplayIndex = curDisplayIdx++; } catch { }
                    }
                }
            }
            catch { }
        }

        public void ApplyInitialMode(MepQuantityFilterMode mode)
        {
            _filterMode = mode;

            if (mode == MepQuantityFilterMode.PipesOnly)
            {
                if (radModePipes != null) radModePipes.Checked = true;
                if (chkIncludePipes != null) chkIncludePipes.Checked = true;
                if (chkIncludeFittings != null) chkIncludeFittings.Checked = false;
                this.Text = "HDTOOL - BÓC KHỐI LƯỢNG ĐƯỜNG ỐNG NƯỚC (CHUYÊN SÂU)";
                if (lblTitle != null) lblTitle.Text = "BÓC KHỐI LƯỢNG ĐƯỜNG ỐNG NƯỚC (PIPES)";
                if (lblSubtitle != null) lblSubtitle.Text = "Chuyên bóc mét dài ống nước, phân loại hệ thống, size đường kính và cao độ tầng";
                if (pnlPipeMode != null) pnlPipeMode.Visible = true;
            }
            else if (mode == MepQuantityFilterMode.FittingsOnly)
            {
                if (radModeFittings != null) radModeFittings.Checked = true;
                if (chkIncludePipes != null) chkIncludePipes.Checked = false;
                if (chkIncludeFittings != null) chkIncludeFittings.Checked = true;
                this.Text = "HDTOOL - BÓC KHỐI LƯỢNG PHỤ KIỆN ỐNG NƯỚC (CHUYÊN SÂU)";
                if (lblTitle != null) lblTitle.Text = "BÓC KHỐI LƯỢNG PHỤ KIỆN ỐNG NƯỚC (FITTINGS)";
                if (lblSubtitle != null) lblSubtitle.Text = "Chuyên bóc co, cút, tê, côn, van, phụ kiện: đếm số lượng chiếc, quy cách và xuất bảng";
                if (pnlPipeMode != null) pnlPipeMode.Visible = false;
            }
            else
            {
                if (radModeAll != null) radModeAll.Checked = true;
                if (chkIncludePipes != null) chkIncludePipes.Checked = true;
                if (chkIncludeFittings != null) chkIncludeFittings.Checked = true;
                this.Text = "HDTOOL - THỐNG KÊ KHỐI LƯỢNG ỐNG NƯỚC & PHỤ KIỆN";
                if (lblTitle != null) lblTitle.Text = "THỐNG KÊ KHỐI LƯỢNG MEP - ỐNG NƯỚC & PHỤ KIỆN";
                if (lblSubtitle != null) lblSubtitle.Text = "Gộp parameter thông minh, bóc tách chính xác và soi sáng tương tác 2 chiều trên mô hình Revit";
                if (pnlPipeMode != null) pnlPipeMode.Visible = true;
            }
        }

        public void SetFilterMode(MepQuantityFilterMode mode)
        {
            ApplyInitialMode(mode);
            LoadDataAndRefreshUi();
            try
            {
                MepUserSettings.Instance.FilterMode = (int)mode;
                MepUserSettings.Instance.Save();
            }
            catch { }
        }

        private void CheckInitialSelection(MepQuantityFilterMode mode)
        {
            if (_uidoc == null || _doc == null) return;
            try
            {
                ICollection<ElementId> selIds = _uidoc.Selection.GetElementIds();
                if (selIds != null && selIds.Count > 0)
                {
                    List<ElementId> validMepIds = new List<ElementId>();
                    string viewNameDetected = null;

                    foreach (ElementId id in selIds)
                    {
                        Element el = _doc.GetElement(id);
                        if (el == null) continue;

                        // NẾU NGƯỜI DÙNG TICK CHỌN VIEWPORT TRÊN SHEET: Tự động quét toàn bộ đối tượng trong View đó!
                        if (el is Viewport)
                        {
                            Viewport vp = el as Viewport;
                            Autodesk.Revit.DB.View targetV = _doc.GetElement(vp.ViewId) as Autodesk.Revit.DB.View;
                            if (targetV != null && !(targetV is ViewDrafting) && targetV.ViewType != ViewType.Legend)
                            {
                                viewNameDetected = targetV.Name;
                                bool incP = (mode != MepQuantityFilterMode.FittingsOnly);
                                bool incF = (mode != MepQuantityFilterMode.PipesOnly);
                                var vIds = MepQuantityService.GetModelElementIdsFromView(_doc, targetV, incP, incF);
                                validMepIds.AddRange(vIds);
                            }
                            continue;
                        }

                        if (el.Category != null)
                        {
                            ElementId catId = el.Category.Id;
                            bool isPipe = catId == new ElementId(BuiltInCategory.OST_PipeCurves);
                            bool isFitting = catId == new ElementId(BuiltInCategory.OST_PipeFitting) ||
                                            catId == new ElementId(BuiltInCategory.OST_PipeAccessory);

                            if (mode == MepQuantityFilterMode.PipesOnly && isPipe)
                            {
                                validMepIds.Add(id);
                            }
                            else if (mode == MepQuantityFilterMode.FittingsOnly && isFitting)
                            {
                                validMepIds.Add(id);
                            }
                            else if (mode == MepQuantityFilterMode.All && (isPipe || isFitting))
                            {
                                validMepIds.Add(id);
                            }
                        }
                    }

                    if (validMepIds.Count > 0)
                    {
                        _customSelectionIds = validMepIds.Distinct().ToList();
                        if (radSelection != null)
                        {
                            radSelection.Checked = true;
                            if (!string.IsNullOrEmpty(viewNameDetected))
                            {
                                radSelection.Text = string.Format("View: {0} ({1} đối tượng)", TruncateString(viewNameDetected, 18), _customSelectionIds.Count);
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.Text = "HDTOOL - THỐNG KÊ KHỐI LƯỢNG ỐNG NƯỚC & PHỤ KIỆN (SOI KIỂM TRA ĐỐI TƯỢNG)";
            this.Size = new Size(1320, 800);
            this.MinimumSize = new Size(1180, 680);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(248, 250, 252);
            this.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

            // ================= 1. HEADER =================
            pnlHeader = new System.Windows.Forms.Panel();
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = 72;
            pnlHeader.BackColor = Color.FromArgb(24, 43, 73);

            lblTitle = new Label();
            lblTitle.Text = "HDTOOL — THỐNG KÊ KHỐI LƯỢNG ỐNG NƯỚC & PHỤ KIỆN (GỘP CỘT TỰ ĐỘNG)";
            lblTitle.ForeColor = Color.White;
            lblTitle.Font = new Font("Segoe UI", 12.0f, FontStyle.Bold);
            lblTitle.Location = new Point(16, 12);
            lblTitle.AutoSize = true;

            lblSubtitle = new Label();
            lblSubtitle.Text = "Gộp các cột 'Tên phụ kiện' phân mảnh làm 1 bảng hoàn chỉnh  |  Quét vào bảng để đối tượng sáng lên kiểm tra  |  Đưa thẳng vào Sheet";
            lblSubtitle.ForeColor = Color.FromArgb(189, 195, 199);
            lblSubtitle.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblSubtitle.Location = new Point(16, 42);
            lblSubtitle.AutoSize = true;

            picLogo = new PictureBox();
            picLogo.Size = new Size(130, 42);
            picLogo.Location = new Point(this.ClientSize.Width - 145, 15);
            picLogo.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            picLogo.SizeMode = PictureBoxSizeMode.Zoom;
            picLogo.BackColor = Color.White;
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("hadomee_logo.png"))
                {
                    if (stream != null) picLogo.Image = Image.FromStream(stream);
                }
            }
            catch { }

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);
            pnlHeader.Controls.Add(picLogo);

            // ================= 2. FILTER PANEL =================
            grpFilter = new GroupBox();
            grpFilter.Text = "Bộ Lọc & Phạm Vi Bóc Khối Lượng";
            grpFilter.Location = new Point(12, 78);
            grpFilter.Size = new Size(this.ClientSize.Width - 24, 110);
            grpFilter.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            grpFilter.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            grpFilter.ForeColor = Color.FromArgb(44, 62, 80);

            ToolTip toolTip = new ToolTip();
            toolTip.AutoPopDelay = 5000;
            toolTip.InitialDelay = 500;
            toolTip.ReshowDelay = 200;
            toolTip.ShowAlways = true;

            // ================= CỤM 1: PHẠM VI BÓC KHỐI LƯỢNG (Rộng 270px, thẳng thớm không bị che) =================
            string viewName = _activeView != null ? _activeView.Name : "View Hiện Tại";
            radCurrentView = new RadioButton();
            radCurrentView.Text = string.Format("Khung Nhìn Hiện Hành ({0})", TruncateString(viewName, 18));
            radCurrentView.Location = new Point(14, 22);
            radCurrentView.Size = new Size(265, 24);
            radCurrentView.Checked = (_customSelectionIds.Count == 0);
            radCurrentView.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            radCurrentView.ForeColor = Color.FromArgb(15, 23, 42);
            radCurrentView.Cursor = Cursors.Hand;
            radCurrentView.CheckedChanged += (s, e) => {
                if (radCurrentView.Checked) {
                    LoadDataAndRefreshUi();
                    try { MepUserSettings.Instance.ScopeOption = 0; MepUserSettings.Instance.Save(); } catch { }
                }
            };
            toolTip.SetToolTip(radCurrentView, "Khung nhìn hiện hành đang mở: " + viewName);

            radSelection = new RadioButton();
            radSelection.Text = _customSelectionIds.Count > 0
                ? string.Format("Vùng Quét Cục Bộ ({0} đ.tượng)", _customSelectionIds.Count)
                : "Vùng Quét Cục Bộ (Chưa quét)";
            radSelection.Location = new Point(14, 49);
            radSelection.Size = new Size(265, 24);
            radSelection.Checked = (_customSelectionIds.Count > 0);
            radSelection.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            radSelection.ForeColor = Color.FromArgb(217, 119, 6); // Amber
            radSelection.Cursor = Cursors.Hand;
            radSelection.CheckedChanged += (s, e) => {
                if (radSelection.Checked) {
                    LoadDataAndRefreshUi();
                    try { MepUserSettings.Instance.ScopeOption = 1; MepUserSettings.Instance.Save(); } catch { }
                }
            };
            toolTip.SetToolTip(radSelection, "Bóc theo các đối tượng đã quét chọn trực tiếp trên bản vẽ Revit");

            radAllProject = new RadioButton();
            radAllProject.Text = "Toàn Bộ Dự Án (All Document)";
            radAllProject.Location = new Point(14, 76);
            radAllProject.Size = new Size(265, 24);
            radAllProject.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            radAllProject.ForeColor = Color.FromArgb(15, 23, 42);
            radAllProject.Cursor = Cursors.Hand;
            radAllProject.CheckedChanged += (s, e) => {
                if (radAllProject.Checked) {
                    LoadDataAndRefreshUi();
                    try { MepUserSettings.Instance.ScopeOption = 2; MepUserSettings.Instance.Save(); } catch { }
                }
            };
            toolTip.SetToolTip(radAllProject, "Quét tất cả đối tượng trong toàn bộ mô hình dự án Revit");

            // ================= CỤM 2: THAO TÁC QUÉT BẢN VẼ (Rộng 175px) =================
            btnPickLocalArea = new Button();
            btnPickLocalArea.Text = "Quét / Chọn View\r\n(Kéo quét hoặc Click View)";
            btnPickLocalArea.Location = new Point(285, 20);
            btnPickLocalArea.Size = new Size(170, 36);
            btnPickLocalArea.BackColor = Color.FromArgb(217, 83, 79); // Coral Red Accent
            btnPickLocalArea.ForeColor = Color.White;
            btnPickLocalArea.FlatStyle = FlatStyle.Flat;
            btnPickLocalArea.FlatAppearance.BorderSize = 0;
            btnPickLocalArea.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            btnPickLocalArea.Cursor = Cursors.Hand;
            btnPickLocalArea.Click += BtnPickLocalArea_Click;
            toolTip.SetToolTip(btnPickLocalArea, "Trên Sheet: Click chọn trực tiếp vào khung nhìn Viewport để tự động quét toàn bộ đối tượng trong View!\nTrong View mô hình: Kéo chuột quét một khung chữ nhật.");

            btnPickAppendArea = new Button();
            btnPickAppendArea.Text = "Bóc Thêm (Cộng Dồn)\r\n(Thêm View / Vùng khác)";
            btnPickAppendArea.Location = new Point(285, 60);
            btnPickAppendArea.Size = new Size(170, 36);
            btnPickAppendArea.BackColor = Color.FromArgb(13, 148, 136); // Teal Accent
            btnPickAppendArea.ForeColor = Color.White;
            btnPickAppendArea.FlatStyle = FlatStyle.Flat;
            btnPickAppendArea.FlatAppearance.BorderSize = 0;
            btnPickAppendArea.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            btnPickAppendArea.Cursor = Cursors.Hand;
            btnPickAppendArea.Click += BtnPickAppendArea_Click;
            toolTip.SetToolTip(btnPickAppendArea, "Chọn thêm Viewport trên Sheet hoặc quét thêm vùng mới để cộng dồn vào bảng khối lượng.");

            // Checkbox ngầm điều khiển logic quét dữ liệu
            chkIncludeFittings = new CheckBox();
            chkIncludeFittings.Visible = false;
            chkIncludeFittings.Checked = true;
            chkIncludeFittings.CheckedChanged += (s, e) => LoadDataAndRefreshUi();

            chkIncludePipes = new CheckBox();
            chkIncludePipes.Visible = false;
            chkIncludePipes.Checked = true;
            chkIncludePipes.CheckedChanged += (s, e) => LoadDataAndRefreshUi();

            // ================= CỤM 3: CHẾ ĐỘ BÓC KHỐI LƯỢNG (Rộng 215px, chữ rõ ràng) =================
            pnlModeSelector = new System.Windows.Forms.Panel();
            pnlModeSelector.Location = new Point(465, 16);
            pnlModeSelector.Size = new Size(215, 82);
            pnlModeSelector.BackColor = Color.FromArgb(241, 245, 249);
            pnlModeSelector.BorderStyle = BorderStyle.FixedSingle;

            lblModeTitle = new Label();
            lblModeTitle.Text = "Chế Độ Bóc Khối Lượng:";
            lblModeTitle.Location = new Point(6, 4);
            lblModeTitle.AutoSize = true;
            lblModeTitle.Font = new Font("Segoe UI", 8.0f, FontStyle.Bold);
            lblModeTitle.ForeColor = Color.FromArgb(30, 41, 59);

            radModePipes = new RadioButton();
            radModePipes.Text = "Ống Nước (Pipes - Mét Dài)";
            radModePipes.Location = new Point(8, 22);
            radModePipes.Size = new Size(200, 18);
            radModePipes.Font = new Font("Segoe UI", 8.0f, FontStyle.Bold);
            radModePipes.ForeColor = Color.FromArgb(2, 132, 199);
            radModePipes.Cursor = Cursors.Hand;
            radModePipes.CheckedChanged += (s, e) => {
                if (radModePipes.Checked && _filterMode != MepQuantityFilterMode.PipesOnly)
                {
                    SetFilterMode(MepQuantityFilterMode.PipesOnly);
                }
            };
            toolTip.SetToolTip(radModePipes, "Chỉ bóc đường ống nước: tính mét dài chi tiết, đường kính và hệ thống");

            radModeFittings = new RadioButton();
            radModeFittings.Text = "Phụ Kiện (Fittings - Số Lượng)";
            radModeFittings.Location = new Point(8, 41);
            radModeFittings.Size = new Size(200, 18);
            radModeFittings.Font = new Font("Segoe UI", 8.0f, FontStyle.Bold);
            radModeFittings.ForeColor = Color.FromArgb(16, 185, 129);
            radModeFittings.Cursor = Cursors.Hand;
            radModeFittings.CheckedChanged += (s, e) => {
                if (radModeFittings.Checked && _filterMode != MepQuantityFilterMode.FittingsOnly)
                {
                    SetFilterMode(MepQuantityFilterMode.FittingsOnly);
                }
            };
            toolTip.SetToolTip(radModeFittings, "Chỉ bóc co, cút, tê, côn, van, phụ kiện: đếm số lượng chiếc và gộp parameter");

            radModeAll = new RadioButton();
            radModeAll.Text = "Tất Cả (Ống & Phụ Kiện)";
            radModeAll.Location = new Point(8, 60);
            radModeAll.Size = new Size(200, 18);
            radModeAll.Font = new Font("Segoe UI", 8.0f, FontStyle.Regular);
            radModeAll.ForeColor = Color.FromArgb(71, 85, 105);
            radModeAll.Cursor = Cursors.Hand;
            radModeAll.CheckedChanged += (s, e) => {
                if (radModeAll.Checked && _filterMode != MepQuantityFilterMode.All)
                {
                    SetFilterMode(MepQuantityFilterMode.All);
                }
            };
            toolTip.SetToolTip(radModeAll, "Thống kê tổng hợp cả đường ống nước và toàn bộ phụ kiện");

            pnlModeSelector.Controls.Add(lblModeTitle);
            pnlModeSelector.Controls.Add(radModePipes);
            pnlModeSelector.Controls.Add(radModeFittings);
            pnlModeSelector.Controls.Add(radModeAll);

            // ================= CỤM 4: BỘ LỌC THUỘC TÍNH & TÌM KIẾM (Rộng rãi, thoáng đãng) =================
            lblFilterArea = new Label();
            lblFilterArea.Text = "Khu Vực:";
            lblFilterArea.Location = new Point(695, 25);
            lblFilterArea.Size = new Size(60, 20);
            lblFilterArea.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblFilterArea.ForeColor = Color.FromArgb(30, 41, 59);

            cboArea = new ComboBox();
            cboArea.Location = new Point(760, 22);
            cboArea.Size = new Size(110, 24);
            cboArea.DropDownStyle = ComboBoxStyle.DropDownList;
            cboArea.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            cboArea.SelectedIndexChanged += (s, e) => FilterAndDisplayItems();

            lblFilterLevel = new Label();
            lblFilterLevel.Text = "Tầng:";
            lblFilterLevel.Location = new Point(880, 25);
            lblFilterLevel.Size = new Size(42, 20);
            lblFilterLevel.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblFilterLevel.ForeColor = Color.FromArgb(30, 41, 59);

            cboLevel = new ComboBox();
            cboLevel.Location = new Point(926, 22);
            cboLevel.Size = new Size(100, 24);
            cboLevel.DropDownStyle = ComboBoxStyle.DropDownList;
            cboLevel.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            cboLevel.SelectedIndexChanged += (s, e) => FilterAndDisplayItems();

            lblFilterSystem = new Label();
            lblFilterSystem.Text = "Hệ Thống:";
            lblFilterSystem.Location = new Point(695, 63);
            lblFilterSystem.Size = new Size(60, 20);
            lblFilterSystem.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblFilterSystem.ForeColor = Color.FromArgb(30, 41, 59);

            cboSystem = new ComboBox();
            cboSystem.Location = new Point(760, 60);
            cboSystem.Size = new Size(110, 24);
            cboSystem.DropDownStyle = ComboBoxStyle.DropDownList;
            cboSystem.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            cboSystem.SelectedIndexChanged += (s, e) => FilterAndDisplayItems();

            Label lblSearchPrompt = new Label();
            lblSearchPrompt.Text = "Tìm:";
            lblSearchPrompt.Location = new Point(880, 63);
            lblSearchPrompt.Size = new Size(42, 20);
            lblSearchPrompt.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblSearchPrompt.ForeColor = Color.FromArgb(30, 41, 59);

            txtSearch = new TextBox();
            txtSearch.Location = new Point(926, 60);
            txtSearch.Size = new Size(140, 24);
            txtSearch.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            txtSearch.Text = "Tìm tên, size...";
            txtSearch.ForeColor = Color.Gray;
            txtSearch.Enter += (s, e) => {
                if (txtSearch.ForeColor == Color.Gray || txtSearch.Text.Contains("Tìm"))
                {
                    txtSearch.Text = "";
                    txtSearch.ForeColor = Color.Black;
                }
            };
            txtSearch.Leave += (s, e) => {
                if (string.IsNullOrWhiteSpace(txtSearch.Text))
                {
                    txtSearch.Text = "Tìm tên, size...";
                    txtSearch.ForeColor = Color.Gray;
                }
            };
            txtSearch.TextChanged += (s, e) => {
                if (txtSearch.ForeColor != Color.Gray)
                {
                    FilterAndDisplayItems();
                }
            };

            // ================= CỤM 5: NÚT QUÉT LẠI DỮ LIỆU (Neo sát góc phải) =================
            btnRefresh = new Button();
            btnRefresh.Text = "Quét Lại\r\nDữ Liệu";
            btnRefresh.Location = new Point(grpFilter.Width - 145, 20);
            btnRefresh.Size = new Size(130, 76);
            btnRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnRefresh.BackColor = Color.FromArgb(52, 73, 94);
            btnRefresh.ForeColor = Color.White;
            btnRefresh.FlatStyle = FlatStyle.Flat;
            btnRefresh.FlatAppearance.BorderSize = 0;
            btnRefresh.Cursor = Cursors.Hand;
            btnRefresh.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnRefresh.Click += (s, e) => LoadDataAndRefreshUi();
            toolTip.SetToolTip(btnRefresh, "Làm mới và quét lại toàn bộ dữ liệu mới nhất từ mô hình Revit");

            grpFilter.Controls.Add(radCurrentView);
            grpFilter.Controls.Add(radSelection);
            grpFilter.Controls.Add(radAllProject);
            grpFilter.Controls.Add(btnPickLocalArea);
            grpFilter.Controls.Add(btnPickAppendArea);
            grpFilter.Controls.Add(chkIncludeFittings);
            grpFilter.Controls.Add(chkIncludePipes);
            grpFilter.Controls.Add(pnlModeSelector);
            grpFilter.Controls.Add(lblFilterArea);
            grpFilter.Controls.Add(cboArea);
            grpFilter.Controls.Add(lblFilterLevel);
            grpFilter.Controls.Add(cboLevel);
            grpFilter.Controls.Add(lblFilterSystem);
            grpFilter.Controls.Add(cboSystem);
            grpFilter.Controls.Add(lblSearchPrompt);
            grpFilter.Controls.Add(txtSearch);
            grpFilter.Controls.Add(btnRefresh);

            // ================= 3. HIGHLIGHT & INSPECTION TOOLBAR =================
            pnlHighlightBar = new System.Windows.Forms.Panel();
            pnlHighlightBar.Location = new Point(12, 196);
            pnlHighlightBar.Size = new Size(this.ClientSize.Width - 24, 70);
            pnlHighlightBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            pnlHighlightBar.BackColor = Color.FromArgb(241, 245, 249);
            pnlHighlightBar.BorderStyle = BorderStyle.FixedSingle;

            lblHighlightStatus = new Label();
            lblHighlightStatus.Text = "Hãy click chọn hoặc quét chuột qua các dòng bảng để đối tượng sáng lên kiểm tra...";
            lblHighlightStatus.Location = new Point(12, 8);
            lblHighlightStatus.Size = new Size(530, 20);
            lblHighlightStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            lblHighlightStatus.ForeColor = Color.FromArgb(3, 105, 161);

            // Bộ chọn Chế Độ Bóc Đoạn Ống theo yêu cầu Sếp Hồ
            pnlPipeMode = new System.Windows.Forms.Panel();
            pnlPipeMode.Location = new Point(548, 5);
            pnlPipeMode.Size = new Size(375, 25);
            pnlPipeMode.BackColor = Color.FromArgb(254, 243, 199); // Amber-100 tint
            pnlPipeMode.BorderStyle = BorderStyle.FixedSingle;

            lblPipeModeTitle = new Label();
            lblPipeModeTitle.Text = "Đoạn Ống:";
            lblPipeModeTitle.Location = new Point(4, 3);
            lblPipeModeTitle.AutoSize = true;
            lblPipeModeTitle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            lblPipeModeTitle.ForeColor = Color.FromArgb(120, 53, 15); // Amber-900

            radPipeDiscrete = new RadioButton();
            radPipeDiscrete.Text = "Tách Rời Rạc (Không SUM)";
            radPipeDiscrete.Location = new Point(88, 2);
            radPipeDiscrete.Size = new Size(165, 20);
            radPipeDiscrete.Checked = true;
            radPipeDiscrete.Font = new Font("Segoe UI", 8.0f, FontStyle.Bold);
            radPipeDiscrete.ForeColor = Color.FromArgb(3, 105, 161);
            radPipeDiscrete.Cursor = Cursors.Hand;
            radPipeDiscrete.CheckedChanged += (s, e) =>
            {
                if (radPipeDiscrete.Checked)
                {
                    _discretePipes = true;
                    LoadDataAndRefreshUi();
                    try { MepUserSettings.Instance.DiscretePipes = true; MepUserSettings.Instance.Save(); } catch { }
                }
            };

            radPipeSum = new RadioButton();
            radPipeSum.Text = "Gom MÃ©t DÃ i (SUM)";
            radPipeSum.Location = new Point(255, 2);
            radPipeSum.Size = new Size(115, 20);
            radPipeSum.Checked = false;
            radPipeSum.Font = new Font("Segoe UI", 8.0f, FontStyle.Regular);
            radPipeSum.ForeColor = Color.FromArgb(71, 85, 105);
            radPipeSum.Cursor = Cursors.Hand;
            radPipeSum.CheckedChanged += (s, e) =>
            {
                if (radPipeSum.Checked)
                {
                    _discretePipes = false;
                    LoadDataAndRefreshUi();
                    try { MepUserSettings.Instance.DiscretePipes = false; MepUserSettings.Instance.Save(); } catch { }
                }
            };

            pnlPipeMode.Controls.Add(lblPipeModeTitle);
            pnlPipeMode.Controls.Add(radPipeDiscrete);
            pnlPipeMode.Controls.Add(radPipeSum);

            chkAutoHighlight = new CheckBox();
            chkAutoHighlight.CheckedChanged += (s, e) =>
            {
                try { MepUserSettings.Instance.AutoHighlight = chkAutoHighlight.Checked; MepUserSettings.Instance.Save(); } catch { }
            };
            chkAutoHighlight.Text = "Tự động Highlight";
            chkAutoHighlight.Location = new Point(935, 7);
            chkAutoHighlight.Size = new Size(150, 22);
            chkAutoHighlight.Checked = true;
            chkAutoHighlight.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            chkAutoHighlight.ForeColor = Color.FromArgb(30, 41, 59);

            btnHighlightAllPicked = new Button();
            btnHighlightAllPicked.Text = "Sáng Đã Bóc";
            btnHighlightAllPicked.Location = new Point(12, 32);
            btnHighlightAllPicked.Size = new Size(115, 28);
            btnHighlightAllPicked.BackColor = Color.FromArgb(230, 126, 34); // #e67e22
            btnHighlightAllPicked.ForeColor = Color.White;
            btnHighlightAllPicked.FlatStyle = FlatStyle.Flat;
            btnHighlightAllPicked.FlatAppearance.BorderSize = 0;
            btnHighlightAllPicked.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            btnHighlightAllPicked.Cursor = Cursors.Hand;
            btnHighlightAllPicked.Click += BtnHighlightAllPicked_Click;

            btnClearHighlight = new Button();
            btnClearHighlight.Text = "Tắt Sáng";
            btnClearHighlight.Location = new Point(133, 32);
            btnClearHighlight.Size = new Size(95, 28);
            btnClearHighlight.BackColor = Color.FromArgb(127, 140, 141); // #7f8c8d
            btnClearHighlight.ForeColor = Color.White;
            btnClearHighlight.FlatStyle = FlatStyle.Flat;
            btnClearHighlight.FlatAppearance.BorderSize = 0;
            btnClearHighlight.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            btnClearHighlight.Cursor = Cursors.Hand;
            btnClearHighlight.Click += BtnClearHighlight_Click;

            btnZoom = new Button();
            btnZoom.Text = "Zoom Gần";
            btnZoom.Location = new Point(234, 32);
            btnZoom.Size = new Size(100, 28);
            btnZoom.BackColor = Color.FromArgb(41, 128, 185); // #2980b9
            btnZoom.ForeColor = Color.White;
            btnZoom.FlatStyle = FlatStyle.Flat;
            btnZoom.FlatAppearance.BorderSize = 0;
            btnZoom.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            btnZoom.Cursor = Cursors.Hand;
            btnZoom.Click += BtnZoom_Click;

            btnIsolate = new Button();
            btnIsolate.Text = "Cách Ly View";
            btnIsolate.Location = new Point(340, 32);
            btnIsolate.Size = new Size(115, 28);
            btnIsolate.BackColor = Color.FromArgb(211, 84, 0); // #d35400
            btnIsolate.ForeColor = Color.White;
            btnIsolate.FlatStyle = FlatStyle.Flat;
            btnIsolate.FlatAppearance.BorderSize = 0;
            btnIsolate.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            btnIsolate.Cursor = Cursors.Hand;
            btnIsolate.Click += BtnIsolate_Click;

            btnResetIsolate = new Button();
            btnResetIsolate.Text = "Bỏ Cách Ly";
            btnResetIsolate.Location = new Point(461, 32);
            btnResetIsolate.Size = new Size(105, 28);
            btnResetIsolate.BackColor = Color.FromArgb(142, 68, 173); // #8e44ad
            btnResetIsolate.ForeColor = Color.White;
            btnResetIsolate.FlatStyle = FlatStyle.Flat;
            btnResetIsolate.FlatAppearance.BorderSize = 0;
            btnResetIsolate.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            btnResetIsolate.Cursor = Cursors.Hand;
            btnResetIsolate.Click += BtnResetIsolate_Click;

            btnToggleGridCols = new Button();
            btnToggleGridCols.Text = "Ẩn / Hiện Cột";
            btnToggleGridCols.Location = new Point(572, 32);
            btnToggleGridCols.Size = new Size(115, 28);
            btnToggleGridCols.BackColor = Color.FromArgb(71, 85, 105); // #475569
            btnToggleGridCols.ForeColor = Color.White;
            btnToggleGridCols.FlatStyle = FlatStyle.Flat;
            btnToggleGridCols.FlatAppearance.BorderSize = 0;
            btnToggleGridCols.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            btnToggleGridCols.Cursor = Cursors.Hand;
            btnToggleGridCols.Click += BtnToggleGridCols_Click;

            btnAddProjectParamCol = new Button();
            btnAddProjectParamCol.Text = "Thêm Cột Parameter";
            btnAddProjectParamCol.Location = new Point(693, 32);
            btnAddProjectParamCol.Size = new Size(160, 28);
            btnAddProjectParamCol.BackColor = Color.FromArgb(14, 116, 144); // #0e7490 Cyan/Teal
            btnAddProjectParamCol.ForeColor = Color.White;
            btnAddProjectParamCol.FlatStyle = FlatStyle.Flat;
            btnAddProjectParamCol.FlatAppearance.BorderSize = 0;
            btnAddProjectParamCol.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            btnAddProjectParamCol.Cursor = Cursors.Hand;
            btnAddProjectParamCol.Click += BtnAddProjectParamCol_Click;

            btnPickHighlightOnSheet = new Button();
            btnPickHighlightOnSheet.Text = "CHECK VAR PLUMBING";
            btnPickHighlightOnSheet.Location = new Point(859, 32);
            btnPickHighlightOnSheet.Size = new Size(185, 28);
            btnPickHighlightOnSheet.BackColor = Color.FromArgb(220, 38, 38); // #dc2626
            btnPickHighlightOnSheet.ForeColor = Color.White;
            btnPickHighlightOnSheet.FlatStyle = FlatStyle.Flat;
            btnPickHighlightOnSheet.FlatAppearance.BorderSize = 0;
            btnPickHighlightOnSheet.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            btnPickHighlightOnSheet.Cursor = Cursors.Hand;
            btnPickHighlightOnSheet.Click += BtnPickHighlightOnSheet_Click;

            pnlHighlightBar.Controls.Add(lblHighlightStatus);
            pnlHighlightBar.Controls.Add(pnlPipeMode);
            pnlHighlightBar.Controls.Add(chkAutoHighlight);
            pnlHighlightBar.Controls.Add(btnHighlightAllPicked);
            pnlHighlightBar.Controls.Add(btnClearHighlight);
            pnlHighlightBar.Controls.Add(btnZoom);
            pnlHighlightBar.Controls.Add(btnIsolate);
            pnlHighlightBar.Controls.Add(btnResetIsolate);
            pnlHighlightBar.Controls.Add(btnToggleGridCols);
            pnlHighlightBar.Controls.Add(btnAddProjectParamCol);
            pnlHighlightBar.Controls.Add(btnPickHighlightOnSheet);

            // Tự động dọn dẹp Graphic Overrides sạch sẽ khi đóng Form
            // Bắt phím ESC để đóng tool ngay lập tức từ bất kỳ control nào
            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    this.Close();
                }
            };

            // Tự động BỎ HIGHLIGHT và khôi phục giao diện Revit sạch sẽ khi đóng Form (ESC hoặc nút Đóng)
            this.FormClosing += (s, e) =>
            {
                SaveColumnSettings();
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

            // ================= 3.5. CÂY THƯ MỤC KHỐI LƯỢNG BÊN TRÁI =================
            pnlTreeLeft = new System.Windows.Forms.Panel();
            pnlTreeLeft.Location = new Point(12, 274);
            pnlTreeLeft.Size = new Size(260, this.ClientSize.Height - 345);
            pnlTreeLeft.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            pnlTreeLeft.BorderStyle = BorderStyle.FixedSingle;
            pnlTreeLeft.BackColor = Color.White;

            lblTreeTitle = new Label();
            lblTreeTitle.Text = "CÂY THƯ MỤC KHỐI LƯỢNG";
            lblTreeTitle.Dock = DockStyle.Top;
            lblTreeTitle.Height = 32;
            lblTreeTitle.BackColor = Color.FromArgb(24, 43, 73);
            lblTreeTitle.ForeColor = Color.White;
            lblTreeTitle.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            lblTreeTitle.TextAlign = ContentAlignment.MiddleCenter;

            btnTreeCreateSchedule = new Button();
            btnTreeCreateSchedule.Text = "Đặt / Cập Nhật Vào Sheet";
            btnTreeCreateSchedule.Dock = DockStyle.Bottom;
            btnTreeCreateSchedule.Height = 38;
            btnTreeCreateSchedule.BackColor = Color.FromArgb(39, 174, 96); // Emerald Green
            btnTreeCreateSchedule.ForeColor = Color.White;
            btnTreeCreateSchedule.FlatStyle = FlatStyle.Flat;
            btnTreeCreateSchedule.FlatAppearance.BorderSize = 0;
            btnTreeCreateSchedule.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            btnTreeCreateSchedule.Cursor = Cursors.Hand;
            btnTreeCreateSchedule.Click += BtnPlaceOnSheet_Click;

            tvQuantityTree = new TreeView();
            tvQuantityTree.Dock = DockStyle.Fill;
            tvQuantityTree.BorderStyle = BorderStyle.None;
            tvQuantityTree.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            tvQuantityTree.ShowLines = true;
            tvQuantityTree.ShowPlusMinus = true;
            tvQuantityTree.ShowRootLines = true;
            tvQuantityTree.HideSelection = false;
            tvQuantityTree.AfterSelect += TvQuantityTree_AfterSelect;

            pnlTreeLeft.Controls.Add(tvQuantityTree);
            pnlTreeLeft.Controls.Add(btnTreeCreateSchedule);
            pnlTreeLeft.Controls.Add(lblTreeTitle);

            // ================= 4. DATA TABLE =================
            dgvItems = new DataGridView();
            dgvItems.Location = new Point(280, 274);
            dgvItems.Size = new Size(this.ClientSize.Width - 292, this.ClientSize.Height - 345);
            dgvItems.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvItems.BackgroundColor = Color.White;
            dgvItems.BorderStyle = BorderStyle.Fixed3D;
            dgvItems.ReadOnly = false; // Cho phép người dùng chỉnh sửa trực tiếp ô Parameter!
            dgvItems.EditMode = DataGridViewEditMode.EditOnEnter; // Click vào ô là chỉnh sửa được ngay như Excel!
            dgvItems.AllowUserToAddRows = false;
            dgvItems.AllowUserToDeleteRows = false;
            dgvItems.AllowUserToResizeRows = false;
            dgvItems.AllowUserToOrderColumns = true; // SẾP HỒ: Cho phép tùy ý kéo cột sang trái sang phải sắp xếp theo ý muốn!
            dgvItems.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvItems.MultiSelect = true;
            dgvItems.RowHeadersVisible = false;
            dgvItems.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvItems.ColumnHeadersHeight = 32;
            dgvItems.RowTemplate.Height = 26;
            dgvItems.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
            dgvItems.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(24, 43, 73);
            dgvItems.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvItems.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            dgvItems.EnableHeadersVisualStyles = false;

            // Bật DoubleBuffered chống giật nháy
            typeof(DataGridView).InvokeMember("DoubleBuffered",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetProperty,
                null, dgvItems, new object[] { true });

            BuildColumns();

                        dgvItems.ColumnWidthChanged += (s, e) => SaveColumnSettings();
            dgvItems.ColumnDisplayIndexChanged += (s, e) => SaveColumnSettings();
            dgvItems.SelectionChanged += DgvItems_SelectionChanged;
            dgvItems.CellEndEdit += DgvItems_CellEndEdit;
            dgvItems.MouseClick += DgvItems_MouseClick;
            dgvItems.ColumnHeaderMouseClick += DgvItems_ColumnHeaderMouseClick;

            // ================= 5. SUMMARY LABEL =================
            lblTableSummary = new Label();
            lblTableSummary.Location = new Point(280, this.ClientSize.Height - 88);
            lblTableSummary.Size = new Size(this.ClientSize.Width - 292, 24);
            lblTableSummary.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            lblTableSummary.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            lblTableSummary.ForeColor = Color.FromArgb(39, 174, 96);
            lblTableSummary.Text = "Tổng cộng: 0 phụ kiện | 0 dòng";

            // ================= 6. BOTTOM ACTION BAR =================
            pnlBottom = new System.Windows.Forms.Panel();
            pnlBottom.Dock = DockStyle.Bottom;
            pnlBottom.Height = 56;
            pnlBottom.BackColor = Color.FromArgb(241, 245, 249);
            pnlBottom.BorderStyle = BorderStyle.FixedSingle;

            btnPlaceOnSheet = new Button();
            btnPlaceOnSheet.Text = "Đặt / Cập Nhật Vào Sheet Mong Muốn...";
            btnPlaceOnSheet.Location = new Point(14, 10);
            btnPlaceOnSheet.Size = new Size(330, 38);
            btnPlaceOnSheet.BackColor = Color.FromArgb(39, 174, 96);
            btnPlaceOnSheet.ForeColor = Color.White;
            btnPlaceOnSheet.FlatStyle = FlatStyle.Flat;
            btnPlaceOnSheet.FlatAppearance.BorderSize = 0;
            btnPlaceOnSheet.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnPlaceOnSheet.Cursor = Cursors.Hand;
            btnPlaceOnSheet.Click += BtnPlaceOnSheet_Click;

            btnQuickCreateSchedule = new Button();
            btnQuickCreateSchedule.Text = "Tạo Vào Project Browser (Kéo Thả)";
            btnQuickCreateSchedule.Location = new Point(350, 10);
            btnQuickCreateSchedule.Size = new Size(270, 38);
            btnQuickCreateSchedule.BackColor = Color.FromArgb(13, 148, 136); // Teal Accent
            btnQuickCreateSchedule.ForeColor = Color.White;
            btnQuickCreateSchedule.FlatStyle = FlatStyle.Flat;
            btnQuickCreateSchedule.FlatAppearance.BorderSize = 0;
            btnQuickCreateSchedule.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnQuickCreateSchedule.Cursor = Cursors.Hand;
            btnQuickCreateSchedule.Click += BtnQuickCreateSchedule_Click;

            btnSyncParams = new Button();
            btnSyncParams.Text = "Đồng Bộ Parameter";
            btnSyncParams.Location = new Point(626, 10);
            btnSyncParams.Size = new Size(195, 38);
            btnSyncParams.BackColor = Color.FromArgb(41, 128, 185);
            btnSyncParams.ForeColor = Color.White;
            btnSyncParams.FlatStyle = FlatStyle.Flat;
            btnSyncParams.FlatAppearance.BorderSize = 0;
            btnSyncParams.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnSyncParams.Cursor = Cursors.Hand;
            btnSyncParams.Click += BtnSyncParams_Click;

            btnExportExcel = new Button();
            btnExportExcel.Text = "Xuất Excel";
            btnExportExcel.Location = new Point(827, 10);
            btnExportExcel.Size = new Size(115, 38);
            btnExportExcel.BackColor = Color.FromArgb(22, 160, 133);
            btnExportExcel.ForeColor = Color.White;
            btnExportExcel.FlatStyle = FlatStyle.Flat;
            btnExportExcel.FlatAppearance.BorderSize = 0;
            btnExportExcel.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnExportExcel.Cursor = Cursors.Hand;
            btnExportExcel.Click += BtnExportExcel_Click;

            btnCopyClipboard = new Button();
            btnCopyClipboard.Text = "Sao Chép";
            btnCopyClipboard.Location = new Point(948, 10);
            btnCopyClipboard.Size = new Size(110, 38);
            btnCopyClipboard.BackColor = Color.FromArgb(100, 116, 139);
            btnCopyClipboard.ForeColor = Color.White;
            btnCopyClipboard.FlatStyle = FlatStyle.Flat;
            btnCopyClipboard.FlatAppearance.BorderSize = 0;
            btnCopyClipboard.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnCopyClipboard.Cursor = Cursors.Hand;
            btnCopyClipboard.Click += BtnCopyClipboard_Click;

            btnClose = new Button();
            btnClose.Text = "Đóng (ESC)";
            btnClose.Location = new Point(1064, 10);
            btnClose.Size = new Size(115, 38);
            btnClose.BackColor = Color.FromArgb(226, 232, 240);
            btnClose.ForeColor = Color.FromArgb(51, 65, 85);
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.FlatAppearance.BorderSize = 1;
            btnClose.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            btnClose.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
            btnClose.Click += (s, e) => this.Close();
            this.CancelButton = btnClose;

            pnlBottom.Controls.Add(btnQuickCreateSchedule);
            pnlBottom.Controls.Add(btnPlaceOnSheet);
            pnlBottom.Controls.Add(btnSyncParams);
            pnlBottom.Controls.Add(btnExportExcel);
            pnlBottom.Controls.Add(btnCopyClipboard);
            pnlBottom.Controls.Add(btnClose);

            this.Controls.Add(pnlHeader);
            this.Controls.Add(grpFilter);
            this.Controls.Add(pnlHighlightBar);
            this.Controls.Add(pnlTreeLeft);
            this.Controls.Add(dgvItems);
            this.Controls.Add(lblTableSummary);
            this.Controls.Add(pnlBottom);

            this.ResumeLayout(false);
        }

        private void BuildColumns()
        {
            dgvItems.Columns.Clear();

            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "STT", Width = 42, ReadOnly = true });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Tên Phụ Kiện / Đường Ống (Đã Gộp)", Width = 230, ReadOnly = true });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Phân Loại", Width = 95, ReadOnly = true });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Size", Width = 110, ReadOnly = true });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Số Lượng", Width = 70, ReadOnly = true });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Chiều Dài (m)", Width = 95, ReadOnly = true });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Type", Width = 95, ReadOnly = true });
            
            // Cột 7: Location by Area - NGƯỜI DÙNG SỬA TRỰC TIẾP ĐƯỢC!
            var colArea = new DataGridViewTextBoxColumn { HeaderText = "Location by Area", Width = 115, ReadOnly = false };
            colArea.DefaultCellStyle.BackColor = Color.FromArgb(240, 249, 255);
            colArea.DefaultCellStyle.ForeColor = Color.FromArgb(15, 23, 42);
            colArea.DefaultCellStyle.SelectionBackColor = Color.FromArgb(2, 132, 199);
            dgvItems.Columns.Add(colArea);

            var colPn = new DataGridViewTextBoxColumn { HeaderText = "PN", Width = 55, ReadOnly = false };
            colPn.DefaultCellStyle.BackColor = Color.FromArgb(240, 249, 255);
            colPn.DefaultCellStyle.ForeColor = Color.FromArgb(15, 23, 42);
            colPn.DefaultCellStyle.SelectionBackColor = Color.FromArgb(2, 132, 199);
            dgvItems.Columns.Add(colPn);

            // Cột 9: Location by Level - HIỂN THỊ VÀ NGƯỜI DÙNG SỬA TRỰC TIẾP ĐƯỢC THEO LỆNH SẾP HỒ!
            var colLevel = new DataGridViewTextBoxColumn { HeaderText = "Location by Level", Width = 115, ReadOnly = false, Visible = true };
            colLevel.DefaultCellStyle.BackColor = Color.FromArgb(240, 249, 255);
            colLevel.DefaultCellStyle.ForeColor = Color.FromArgb(15, 23, 42);
            colLevel.DefaultCellStyle.SelectionBackColor = Color.FromArgb(2, 132, 199);
            dgvItems.Columns.Add(colLevel);

            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Hệ Thống", Width = 120, ReadOnly = true, Visible = false });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Param Nguồn", Width = 105, ReadOnly = true, Visible = false });

            // CÁC CỘT PARAMETER DỰ ÁN TỰ THÊM TÙY Ý
            if (_customParamColumns != null && _customParamColumns.Count > 0)
            {
                foreach (string cp in _customParamColumns)
                {
                    var colCust = new DataGridViewTextBoxColumn { HeaderText = cp, Width = 115, ReadOnly = false, Visible = true };
                    colCust.DefaultCellStyle.BackColor = Color.FromArgb(240, 249, 255);
                    colCust.DefaultCellStyle.ForeColor = Color.FromArgb(15, 23, 42);
                    colCust.DefaultCellStyle.SelectionBackColor = Color.FromArgb(2, 132, 199);
                    dgvItems.Columns.Add(colCust);
                }
            }

            dgvItems.Columns[0].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvItems.Columns[1].DefaultCellStyle.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            dgvItems.Columns[1].DefaultCellStyle.ForeColor = Color.FromArgb(15, 23, 42);
            dgvItems.Columns[2].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvItems.Columns[3].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvItems.Columns[3].DefaultCellStyle.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            dgvItems.Columns[4].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvItems.Columns[4].DefaultCellStyle.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            dgvItems.Columns[5].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvItems.Columns[5].DefaultCellStyle.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            dgvItems.Columns[6].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dgvItems.Columns[8].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvItems.Columns[9].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            // Sáº¾P Há»’: Tá»± Ä‘á»™ng khÃ´i phá»¥c Ä‘á»™ rá»™ng, thá»© tá»± kÃ©o tháº£ vÃ  áº©n/hiá»‡n cá»™t Ä‘Ã£ lÆ°u
            RestoreColumnSettings();
        }

        private void LoadDataAndRefreshUi()
        {
            if (_doc == null) return;

            bool incFittings = chkIncludeFittings.Checked;
            bool incPipes = chkIncludePipes.Checked;

            ICollection<ElementId> customIds = null;
            bool currentViewOnly = false;

            if (radSelection != null && radSelection.Checked)
            {
                customIds = _customSelectionIds;
            }
            else if (radCurrentView != null && radCurrentView.Checked)
            {
                currentViewOnly = true;
            }
            else
            {
                currentViewOnly = false;
            }

            _allItems = MepQuantityService.ScanAndGroupElements(
                _doc,
                _activeView,
                currentViewOnly,
                incFittings,
                incPipes,
                null, null, null, null,
                customIds,
                _discretePipes
            );

            // Populate Dropdown Comboboxes
            PopulateComboboxes();

            // Populate Cây Thư Mục Khối Lượng Bên Trái
            PopulateQuantityTree();

            FilterAndDisplayItems();
        }

        /// <summary>
        /// Thực thi an toàn trên UI thread mà không bao giờ gặp lỗi 'Invoke or BeginInvoke cannot be called until window handle has been created'
        /// </summary>
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
            catch
            {
                try { action(); } catch { }
            }
        }

        private void BtnPickLocalArea_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;

            if (_handler != null && _extEvent != null)
            {
                _handler.ActionToRun = (app) =>
                {
                    try
                    {
                        UIDocument uidoc = app.ActiveUIDocument;
                        if (uidoc == null) return;
                        Document doc = uidoc.Document;
                        Autodesk.Revit.DB.View curView = uidoc.ActiveView;
                        bool isSheet = curView is ViewSheet;

                        List<ElementId> ids = new List<ElementId>();
                        string detectedViewName = null;

                        if (isSheet)
                        {
                            // TRÊN SHEET: Cho phép Sếp click chọn trực tiếp 1 Viewport trên Sheet
                            try
                            {
                                Reference pickedRef = uidoc.Selection.PickObject(
                                    global::Autodesk.Revit.UI.Selection.ObjectType.Element,
                                    new SheetOrMepSelectionFilter(chkIncludePipes.Checked, chkIncludeFittings.Checked, true),
                                    "HDTOOL: Click chọn vào khung nhìn (View) trên Sheet để tự động quét toàn bộ đối tượng..."
                                );

                                if (pickedRef != null)
                                {
                                    Element el = doc.GetElement(pickedRef.ElementId);
                                    if (el is Viewport)
                                    {
                                        Viewport vp = el as Viewport;
                                        Autodesk.Revit.DB.View targetV = doc.GetElement(vp.ViewId) as Autodesk.Revit.DB.View;
                                        if (targetV != null)
                                        {
                                            detectedViewName = targetV.Name;
                                            ids = MepQuantityService.GetModelElementIdsFromView(doc, targetV, chkIncludePipes.Checked, chkIncludeFittings.Checked);
                                        }
                                    }
                                    else if (el != null && el.Category != null)
                                    {
                                        ids.Add(el.Id);
                                    }
                                }
                            }
                            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                            {
                                throw;
                            }
                        }
                        else
                        {
                            // TRONG VIEW MÔ HÌNH: Kéo chuột quét khung chữ nhật
                            IList<Element> picked = uidoc.Selection.PickElementsByRectangle(
                                new SheetOrMepSelectionFilter(chkIncludePipes.Checked, chkIncludeFittings.Checked, false),
                                "HDTOOL: Kéo chuột quét một khung chữ nhật tại vị trí bất kỳ để bóc khối lượng..."
                            );

                            if (picked != null)
                            {
                                foreach (Element el in picked)
                                {
                                    if (el != null && el.Category != null)
                                    {
                                        ElementId catId = el.Category.Id;
                                        if (catId == new ElementId(BuiltInCategory.OST_PipeFitting) ||
                                            catId == new ElementId(BuiltInCategory.OST_PipeCurves) ||
                                            catId == new ElementId(BuiltInCategory.OST_PipeAccessory))
                                        {
                                            ids.Add(el.Id);
                                        }
                                    }
                                }
                            }
                        }

                        SafeInvoke(() =>
                        {
                            this.Show();
                            this.WindowState = FormWindowState.Normal;
                            this.BringToFront();
                            this.Activate();

                            if (ids.Count > 0)
                            {
                                _customSelectionIds = ids.Distinct().ToList();
                                if (!string.IsNullOrEmpty(detectedViewName))
                                {
                                    radSelection.Text = string.Format("View: {0} ({1} đối tượng)", TruncateString(detectedViewName, 18), _customSelectionIds.Count);
                                }
                                else
                                {
                                    radSelection.Text = string.Format("Vùng Quét Cục Bộ ({0} đ.tượng)", _customSelectionIds.Count);
                                }

                                chkIncludeFittings.Checked = true;
                                chkIncludePipes.Checked = true;

                                txtSearch.Text = "Tìm tên, size...";
                                txtSearch.ForeColor = Color.Gray;

                                _treeSelectedLevel = null;
                                _treeSelectedArea = null;
                                _treeSelectedSystem = null;

                                radSelection.Checked = true;
                                LoadDataAndRefreshUi();

                                MepHighlightManager.HighlightElements(_uidoc, _customSelectionIds, new Autodesk.Revit.DB.Color(255, 69, 0), 5, false);
                                lblHighlightStatus.ForeColor = Color.FromArgb(234, 88, 12);
                                lblHighlightStatus.Text = string.Format("ĐÃ TỰ ĐỘNG HIGHLIGHT TOÀN BỘ {0} ĐỐI TƯỢNG ĐÃ BÓC TRÊN MÀN HÌNH REVIT!", _customSelectionIds.Count);
                            }
                            else
                            {
                                MessageBox.Show("Không tìm thấy phụ kiện hoặc đường ống nào trong View hoặc vùng vừa chọn.\nVui lòng chọn lại khung nhìn có chứa ống nước.", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        });
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        SafeInvoke(() =>
                        {
                            this.Show();
                            this.WindowState = FormWindowState.Normal;
                            this.BringToFront();
                            this.Activate();
                        });
                    }
                    catch (Exception ex)
                    {
                        SafeInvoke(() =>
                        {
                            this.Show();
                            this.WindowState = FormWindowState.Normal;
                            this.BringToFront();
                            this.Activate();
                            MessageBox.Show("Lỗi quét đối tượng: " + ex.Message, "HDTOOL", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        });
                    }
                };
                _extEvent.Raise();
            }
        }

        private void BtnPickAppendArea_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;

            if (_handler != null && _extEvent != null)
            {
                _handler.ActionToRun = (app) =>
                {
                    try
                    {
                        UIDocument uidoc = app.ActiveUIDocument;
                        if (uidoc == null) return;
                        Document doc = uidoc.Document;
                        if (doc == null) return;

                        List<ElementId> newIds = new List<ElementId>();
                        string detectedAppendName = string.Empty;

                        if (_activeView is ViewSheet)
                        {
                            // ÄANG á»ž TRÃŠN SHEET: Cho phÃ©p click chá»n thÃªm má»™t Viewport trÃªn Sheet
                            try
                            {
                                Reference pickedRef = uidoc.Selection.PickObject(
                                    global::Autodesk.Revit.UI.Selection.ObjectType.Element,
                                    new SheetOrMepSelectionFilter(chkIncludePipes.Checked, chkIncludeFittings.Checked, true),
                                    "HDTOOL: Click chuá»™t chá»n má»™t Viewport trÃªn Sheet Ä‘á»ƒ bÃ³c cá»™ng dá»“n toÃ n bá»™ Ä‘á»‘i tÆ°á»£ng trong View Ä‘Ã³..."
                                );

                                if (pickedRef != null && pickedRef.ElementId != ElementId.InvalidElementId)
                                {
                                    Element el = doc.GetElement(pickedRef.ElementId);
                                    Viewport vp = el as Viewport;
                                    if (vp != null)
                                    {
                                        Autodesk.Revit.DB.View targetV = doc.GetElement(vp.ViewId) as Autodesk.Revit.DB.View;
                                        if (targetV != null)
                                        {
                                            detectedAppendName = targetV.Name;
                                            newIds = MepQuantityService.GetModelElementIdsFromView(doc, targetV, chkIncludePipes.Checked, chkIncludeFittings.Checked);
                                        }
                                    }
                                    else if (el != null && el.Category != null)
                                    {
                                        newIds.Add(el.Id);
                                    }
                                }
                            }
                            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                            {
                                throw;
                            }
                        }
                        else
                        {
                            // TRONG VIEW MÃ” HÃŒNH: KÃ©o chuá»™t quÃ©t thÃªm má»™t khung chá»¯ nháº­t
                            IList<Element> picked = uidoc.Selection.PickElementsByRectangle(
                                new SheetOrMepSelectionFilter(chkIncludePipes.Checked, chkIncludeFittings.Checked, false),
                                "HDTOOL: KÃ©o chuá»™t quÃ©t thÃªm vÃ¹ng bá»• sung Ä‘á»ƒ bÃ³c cá»™ng dá»“n khá»‘i lÆ°á»£ng..."
                            );

                            if (picked != null)
                            {
                                foreach (Element el in picked)
                                {
                                    if (el != null && el.Category != null)
                                    {
                                        ElementId catId = el.Category.Id;
                                        if (catId == new ElementId(BuiltInCategory.OST_PipeFitting) ||
                                            catId == new ElementId(BuiltInCategory.OST_PipeCurves) ||
                                            catId == new ElementId(BuiltInCategory.OST_PipeAccessory))
                                        {
                                            newIds.Add(el.Id);
                                        }
                                    }
                                }
                            }
                        }

                        SafeInvoke(() =>
                        {
                            this.Show();
                            this.WindowState = FormWindowState.Normal;
                            this.BringToFront();
                            this.Activate();

                            if (newIds.Count > 0)
                            {
                                HashSet<ElementId> idSet = new HashSet<ElementId>(_customSelectionIds);
                                int addedCount = 0;
                                foreach (ElementId id in newIds)
                                {
                                    if (idSet.Add(id)) addedCount++;
                                }

                                _customSelectionIds = idSet.ToList();
                                radSelection.Text = string.Format("VÃ¹ng QuÃ©t Cá»¥c Bá»™ ({0} Ä‘.tÆ°á»£ng)", _customSelectionIds.Count);

                                chkIncludeFittings.Checked = true;
                                chkIncludePipes.Checked = true;

                                _treeSelectedLevel = null;
                                _treeSelectedArea = null;
                                _treeSelectedSystem = null;

                                radSelection.Checked = true;
                                LoadDataAndRefreshUi();

                                // Tá»± Ä‘á»™ng highlight toÃ n bá»™ Ä‘á»‘i tÆ°á»£ng Ä‘Ã£ bÃ³c trÃªn mÃ n hÃ¬nh Revit
                                MepHighlightManager.HighlightElements(_uidoc, _customSelectionIds, new Autodesk.Revit.DB.Color(255, 69, 0), 5, false);
                                lblHighlightStatus.ForeColor = Color.FromArgb(234, 88, 12);
                                lblHighlightStatus.Text = string.Format("ÄÃƒ Tá»° Äá»˜NG HIGHLIGHT TOÃ€N Bá»˜ {0} Äá»I TÆ¯á»¢NG ÄÃƒ BÃ“C TRÃŠN MÃ€N HÃŒNH REVIT!", _customSelectionIds.Count);

                                string infoMsg = !string.IsNullOrEmpty(detectedAppendName)
                                    ? string.Format("ÄÃ£ bÃ³c thÃªm thÃ nh cÃ´ng tá»« View '{0}': {1} Ä‘á»‘i tÆ°á»£ng má»›i!\n\nTá»•ng cá»™ng hiá»‡n táº¡i: {2} Ä‘á»‘i tÆ°á»£ng.", detectedAppendName, addedCount, _customSelectionIds.Count)
                                    : string.Format("ÄÃ£ bÃ³c thÃªm thÃ nh cÃ´ng {0} Ä‘á»‘i tÆ°á»£ng má»›i!\n\nTá»•ng cá»™ng hiá»‡n táº¡i: {1} Ä‘á»‘i tÆ°á»£ng.", addedCount, _customSelectionIds.Count);

                                MessageBox.Show(infoMsg, "BÃ³c ThÃªm ThÃ nh CÃ´ng", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            else
                            {
                                MessageBox.Show("KhÃ´ng tÃ¬m tháº¥y phá»¥ kiá»‡n hoáº·c Ä‘Æ°á»ng á»‘ng má»›i nÃ o trong View hoáº·c vÃ¹ng vá»«a chá»n thÃªm.", "ThÃ´ng BÃ¡o", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        });
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        SafeInvoke(() =>
                        {
                            this.Show();
                            this.WindowState = FormWindowState.Normal;
                            this.BringToFront();
                            this.Activate();
                        });
                    }
                    catch (Exception ex)
                    {
                        SafeInvoke(() =>
                        {
                            this.Show();
                            this.WindowState = FormWindowState.Normal;
                            this.BringToFront();
                            this.Activate();
                            MessageBox.Show("Lá»—i bÃ³c thÃªm Ä‘á»‘i tÆ°á»£ng: " + ex.Message, "HDTOOL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        });
                    }
                };

                _extEvent.Raise();
            }
        }
        private void PopulateComboboxes()
        {
            string prevArea = cboArea.SelectedItem as string;
            string prevLvl = cboLevel.SelectedItem as string;
            string prevSys = cboSystem.SelectedItem as string;

            cboArea.Items.Clear();
            cboArea.Items.Add("Tất cả");
            HashSet<string> areas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in _allItems)
            {
                if (!string.IsNullOrEmpty(item.LocationByArea) && item.LocationByArea != "-")
                    areas.Add(item.LocationByArea);
            }
            foreach (var a in areas.OrderBy(x => x)) cboArea.Items.Add(a);
            cboArea.SelectedItem = (prevArea != null && cboArea.Items.Contains(prevArea)) ? prevArea : "Tất cả";

            cboLevel.Items.Clear();
            cboLevel.Items.Add("Tất cả");
            HashSet<string> levels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in _allItems)
            {
                if (!string.IsNullOrEmpty(item.LocationByLevel) && item.LocationByLevel != "-")
                    levels.Add(item.LocationByLevel);
            }
            foreach (var l in levels.OrderBy(x => x)) cboLevel.Items.Add(l);
            cboLevel.SelectedItem = (prevLvl != null && cboLevel.Items.Contains(prevLvl)) ? prevLvl : "Tất cả";

            cboSystem.Items.Clear();
            cboSystem.Items.Add("Tất cả");
            HashSet<string> systems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in _allItems)
            {
                if (!string.IsNullOrEmpty(item.SystemType) && item.SystemType != "-")
                    systems.Add(item.SystemType);
            }
            foreach (var s in systems.OrderBy(x => x)) cboSystem.Items.Add(s);
            cboSystem.SelectedItem = (prevSys != null && cboSystem.Items.Contains(prevSys)) ? prevSys : "Tất cả";
        }

        private void FilterAndDisplayItems()
        {
            string selArea = !string.IsNullOrEmpty(_treeSelectedArea) ? _treeSelectedArea : cboArea.SelectedItem as string;
            string selLevel = !string.IsNullOrEmpty(_treeSelectedLevel) ? _treeSelectedLevel : cboLevel.SelectedItem as string;
            string selSystem = !string.IsNullOrEmpty(_treeSelectedSystem) ? _treeSelectedSystem : cboSystem.SelectedItem as string;
            string rawSearch = null;
            if (txtSearch != null && txtSearch.ForeColor != Color.Gray && !string.IsNullOrWhiteSpace(txtSearch.Text))
            {
                string t = txtSearch.Text.Trim();
                if (!t.Contains("Tìm tên") && !t.Contains("Tìm nhanh"))
                {
                    rawSearch = t;
                }
            }
            string searchClean = MepQuantityService.RemoveDiacritics(rawSearch ?? "");

            _filteredItems = new List<MepFittingItem>();
            foreach (var item in _allItems)
            {
                if (!string.IsNullOrEmpty(selArea) && selArea != "Tất cả" &&
                    !item.LocationByArea.Equals(selArea, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.IsNullOrEmpty(selLevel) && selLevel != "Tất cả" &&
                    !item.LocationByLevel.Equals(selLevel, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.IsNullOrEmpty(selSystem) && selSystem != "Tất cả" &&
                    !item.SystemType.Equals(selSystem, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.IsNullOrEmpty(searchClean))
                {
                    string target = MepQuantityService.RemoveDiacritics(string.Format("{0} {1} {2} {3} {4} {5}",
                        item.MergedName, item.Size, item.TypeName, item.LocationByArea, item.LocationByLevel, item.SystemType));
                    if (!target.Contains(searchClean)) continue;
                }

                _filteredItems.Add(item);
            }

            // Đổ vào DataGridView
            int expectedColCount = 12 + (_customParamColumns != null ? _customParamColumns.Count : 0);
            if (dgvItems.Columns.Count != expectedColCount)
            {
                BuildColumns();
            }

            dgvItems.Rows.Clear();
            int fittingCount = 0;
            int pipeCount = 0;
            double pipeLengthM = 0;

            for (int i = 0; i < _filteredItems.Count; i++)
            {
                var it = _filteredItems[i];
                it.STT = i + 1;

                if (it.CategoryKind == "Đường Ống")
                {
                    pipeCount += it.Count;
                    pipeLengthM += (it.TotalLengthM > 0 ? it.TotalLengthM : (it.LengthM * (it.Count > 0 ? it.Count : 1)));
                }
                else
                {
                    fittingCount += it.Count;
                }

                int rowIdx = dgvItems.Rows.Add(
                    it.STT,
                    it.MergedName,
                    it.CategoryKind,
                    it.Size,
                    it.Count,
                    it.LengthM > 0 ? it.LengthM.ToString("0.00") : "-",
                    it.TypeName,
                    it.LocationByArea,
                    it.PN,
                    it.LocationByLevel,
                    it.SystemType,
                    it.SourceParamName
                );

                if (it.CategoryKind == "Đường Ống")
                {
                    dgvItems.Rows[rowIdx].Cells[2].Style.ForeColor = Color.FromArgb(2, 132, 199); // Blue
                    dgvItems.Rows[rowIdx].Cells[2].Style.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                    dgvItems.Rows[rowIdx].Cells[5].Style.ForeColor = Color.FromArgb(2, 132, 199);
                }
                else if (it.CategoryKind == "Phụ Kiện Van")
                {
                    dgvItems.Rows[rowIdx].Cells[2].Style.ForeColor = Color.FromArgb(217, 119, 6); // Amber
                    dgvItems.Rows[rowIdx].Cells[2].Style.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                }
                else
                {
                    dgvItems.Rows[rowIdx].Cells[2].Style.ForeColor = Color.FromArgb(124, 58, 237); // Purple
                }

                dgvItems.Rows[rowIdx].Tag = it;

                // Điền giá trị các cột Parameter dự án tự thêm
                if (_customParamColumns != null && _customParamColumns.Count > 0)
                {
                    for (int cpIdx = 0; cpIdx < _customParamColumns.Count; cpIdx++)
                    {
                        string pName = _customParamColumns[cpIdx];
                        string val = "";
                        if (it.CustomParameters != null && it.CustomParameters.ContainsKey(pName))
                        {
                            val = it.CustomParameters[pName];
                        }
                        else
                        {
                            val = GetElementParamValue(_doc, it.ElementIds, pName);
                            if (it.CustomParameters == null) it.CustomParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            it.CustomParameters[pName] = val;
                        }

                        int colIndex = 12 + cpIdx;
                        if (colIndex < dgvItems.Columns.Count)
                        {
                            dgvItems.Rows[rowIdx].Cells[colIndex].Value = val;
                        }
                    }
                }
            }

            if (radSelection != null && radSelection.Checked)
            {
                lblTableSummary.ForeColor = Color.FromArgb(217, 119, 6); // Amber
                lblTableSummary.Text = string.Format("BÓC CỤC BỘ: {0} cái phụ kiện  |  {1} đoạn ống ({2:0.00} m)  |  {3} {4} ({5} đối tượng quét)",
                    fittingCount, pipeCount, pipeLengthM, _filteredItems.Count,
                    _discretePipes ? "nhóm/đoạn rời rạc" : "nhóm quy cách",
                    _customSelectionIds.Count);
            }
            else
            {
                lblTableSummary.ForeColor = Color.FromArgb(39, 174, 96);
                lblTableSummary.Text = string.Format("TỔNG CỘNG: {0} cái phụ kiện  |  {1} đoạn ống ({2:0.00} m)  |  {3} {4}",
                    fittingCount, pipeCount, pipeLengthM, _filteredItems.Count,
                    _discretePipes ? "nhóm/đoạn rời rạc" : "nhóm quy cách");
            }
        }

        // ======================= SELECTION & HIGHLIGHT =======================

        private void DgvItems_SelectionChanged(object sender, EventArgs e)
        {
            if (!chkAutoHighlight.Checked) return;

            HashSet<ElementId> targetIds = new HashSet<ElementId>();
            int rowCount = dgvItems.SelectedRows.Count;
            string firstItemName = "";

            foreach (DataGridViewRow row in dgvItems.SelectedRows)
            {
                MepFittingItem it = row.Tag as MepFittingItem;
                if (it != null && it.ElementIds != null)
                {
                    if (string.IsNullOrEmpty(firstItemName)) firstItemName = it.MergedName + " (" + it.Size + ")";
                    foreach (ElementId id in it.ElementIds)
                    {
                        targetIds.Add(id);
                    }
                }
            }

            _lastSelectedElementIds = targetIds;

            if (targetIds.Count > 0)
            {
                lblHighlightStatus.ForeColor = Color.FromArgb(16, 185, 129); // Emerald Green
                lblHighlightStatus.Text = string.Format("Đang quét chọn {0} dòng ({1} đối tượng: {2}) -> ĐANG SÁNG ĐỎ TRÊN REVIT!",
                    rowCount, targetIds.Count, TruncateString(firstItemName, 35));

                TriggerRevitSelection(targetIds.ToList(), false);
            }
            else
            {
                if (_customSelectionIds != null && _customSelectionIds.Count > 0 && radSelection.Checked)
                {
                    MepHighlightManager.HighlightElements(_uidoc, _customSelectionIds, new Autodesk.Revit.DB.Color(255, 69, 0), 5, false);
                    lblHighlightStatus.ForeColor = Color.FromArgb(234, 88, 12);
                    lblHighlightStatus.Text = string.Format("ĐANG GIỮ SÁNG TOÀN BỘ {0} ĐỐI TƯỢNG ĐÃ BÓC TRÊN MÀN HÌNH REVIT!", _customSelectionIds.Count);
                }
                else
                {
                    lblHighlightStatus.ForeColor = Color.FromArgb(3, 105, 161);
                    lblHighlightStatus.Text = "Hãy click chọn hoặc quét chuột qua các dòng bảng để đối tượng sáng lên kiểm tra...";
                }
            }
        }

        private void TriggerRevitSelection(List<ElementId> ids, bool zoom)
        {
            if (_handler == null || _extEvent == null)
            {
                try
                {
                    if (_uidoc != null)
                    {
                        MepHighlightManager.HighlightElements(_uidoc, ids, new Autodesk.Revit.DB.Color(255, 0, 0), 7, zoom);
                    }
                }
                catch { }
                return;
            }

            _handler.ActionToRun = (app) =>
            {
                UIDocument uidoc = app.ActiveUIDocument;
                if (uidoc != null)
                {
                    MepHighlightManager.HighlightElements(uidoc, ids, new Autodesk.Revit.DB.Color(255, 0, 0), 7, zoom);
                }
            };
            _extEvent.Raise();
        }

        private void BtnHighlightAllPicked_Click(object sender, EventArgs e)
        {
            List<ElementId> idsToLight = (_customSelectionIds != null && _customSelectionIds.Count > 0)
                ? _customSelectionIds
                : GetAllItemIds();

            if (idsToLight.Count == 0)
            {
                MessageBox.Show("Chưa có đối tượng nào trong bảng khối lượng để làm sáng.", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            TriggerRevitSelection(idsToLight, false);
            lblHighlightStatus.ForeColor = Color.FromArgb(234, 88, 12);
            lblHighlightStatus.Text = string.Format("ĐÃ LÀM SÁNG TOÀN BỘ {0} ĐỐI TƯỢNG TRONG BẢNG TRÊN MÀN HÌNH REVIT!", idsToLight.Count);
        }

        private void BtnClearHighlight_Click(object sender, EventArgs e)
        {
            if (_handler != null && _extEvent != null)
            {
                _handler.ActionToRun = (app) =>
                {
                    if (app.ActiveUIDocument != null)
                    {
                        MepHighlightManager.ClearHighlights(app.ActiveUIDocument.Document);
                        try { app.ActiveUIDocument.Selection.SetElementIds(new List<ElementId>()); } catch { }
                        app.ActiveUIDocument.RefreshActiveView();
                    }
                };
                _extEvent.Raise();
            }
            else
            {
                try { if (_doc != null) MepHighlightManager.ClearHighlights(_doc); } catch { }
            }

            lblHighlightStatus.ForeColor = Color.FromArgb(3, 105, 161);
            lblHighlightStatus.Text = "Đã tắt chế độ Highlight trên bản vẽ Revit.";
        }

        private List<ElementId> GetAllItemIds()
        {
            HashSet<ElementId> set = new HashSet<ElementId>();
            foreach (var it in _allItems)
            {
                if (it.ElementIds != null)
                {
                    foreach (var id in it.ElementIds) set.Add(id);
                }
            }
            return set.ToList();
        }

        private void BtnZoom_Click(object sender, EventArgs e)
        {
            if (_lastSelectedElementIds.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất 1 dòng trong bảng khối lượng để Zoom tới.", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            TriggerRevitSelection(_lastSelectedElementIds.ToList(), true);
        }

        private void BtnIsolate_Click(object sender, EventArgs e)
        {
            if (_lastSelectedElementIds.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn các dòng phụ kiện cần cách ly trong bảng.", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<ElementId> ids = _lastSelectedElementIds.ToList();

            if (_handler != null && _extEvent != null)
            {
                _handler.ActionToRun = (app) =>
                {
                    UIDocument uidoc = app.ActiveUIDocument;
                    if (uidoc != null && uidoc.ActiveView != null)
                    {
                        using (Transaction tx = new Transaction(uidoc.Document, "HDTOOL - Cách Ly Đối Tượng Soi Khối Lượng"))
                        {
                            tx.Start();
                            uidoc.ActiveView.IsolateElementsTemporary(ids);
                            tx.Commit();
                        }
                        uidoc.RefreshActiveView();
                    }
                };
                _extEvent.Raise();
            }
            else
            {
                try
                {
                    using (Transaction tx = new Transaction(_doc, "HDTOOL - Cách Ly Đối Tượng Soi Khối Lượng"))
                    {
                        tx.Start();
                        _activeView.IsolateElementsTemporary(ids);
                        tx.Commit();
                    }
                    _uidoc.RefreshActiveView();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Không thể cách ly View: " + ex.Message);
                }
            }
        }

        private void BtnResetIsolate_Click(object sender, EventArgs e)
        {
            if (_handler != null && _extEvent != null)
            {
                _handler.ActionToRun = (app) =>
                {
                    UIDocument uidoc = app.ActiveUIDocument;
                    if (uidoc != null && uidoc.ActiveView != null)
                    {
                        using (Transaction tx = new Transaction(uidoc.Document, "HDTOOL - Bỏ Cách Ly View"))
                        {
                            tx.Start();
                            uidoc.ActiveView.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
                            tx.Commit();
                        }
                        uidoc.RefreshActiveView();
                    }
                };
                _extEvent.Raise();
            }
            else
            {
                try
                {
                    using (Transaction tx = new Transaction(_doc, "HDTOOL - Bỏ Cách Ly View"))
                    {
                        tx.Start();
                        _activeView.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
                        tx.Commit();
                    }
                    _uidoc.RefreshActiveView();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi bỏ cách ly: " + ex.Message);
                }
            }
        }

        private void BtnToggleGridCols_Click(object sender, EventArgs e)
        {
            if (dgvItems == null || dgvItems.Columns.Count == 0) return;

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

            foreach (DataGridViewColumn col in dgvItems.Columns)
            {
                DataGridViewColumn targetCol = col;
                ToolStripMenuItem item = new ToolStripMenuItem(targetCol.HeaderText);
                item.Checked = targetCol.Visible;
                item.CheckOnClick = true;
                item.Click += (s, ev) =>
                {
                    targetCol.Visible = item.Checked;
                };
                menu.Items.Add(item);
            }

            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem itemAddCustom = new ToolStripMenuItem("Thêm Cột Parameter Dự Án...");
            itemAddCustom.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            itemAddCustom.Click += (s, ev) => BtnAddProjectParamCol_Click(s, ev);
            menu.Items.Add(itemAddCustom);

            Button btn = sender as Button;
            if (btn != null)
            {
                menu.Show(btn, new Point(0, btn.Height));
            }
        }

        // ======================= CHỈNH SỬA & ĐỒNG BỘ PARAMETER =======================

        private void DgvItems_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            DataGridViewRow row = dgvItems.Rows[e.RowIndex];
            MepFittingItem it = row.Tag as MepFittingItem;
            if (it == null) return;

            string colHeader = dgvItems.Columns[e.ColumnIndex].HeaderText;
            string newVal = row.Cells[e.ColumnIndex].Value != null ? row.Cells[e.ColumnIndex].Value.ToString().Trim() : string.Empty;

            string targetParam = null;
            if (colHeader.Equals("Location by Area", StringComparison.OrdinalIgnoreCase))
            {
                it.LocationByArea = newVal;
                targetParam = "Location by Area";
            }
            else if (colHeader.Equals("PN", StringComparison.OrdinalIgnoreCase))
            {
                it.PN = newVal;
                targetParam = "PN";
            }
            else if (colHeader.Equals("Location by Level", StringComparison.OrdinalIgnoreCase))
            {
                it.LocationByLevel = newVal;
                targetParam = "Location by Level";
            }
            else if (_customParamColumns != null && _customParamColumns.Contains(colHeader))
            {
                if (it.CustomParameters == null) it.CustomParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                it.CustomParameters[colHeader] = newVal;
                targetParam = colHeader;
            }

            if (!string.IsNullOrEmpty(targetParam) && it.ElementIds != null && it.ElementIds.Count > 0)
            {
                WriteParamValueToElements(it.ElementIds, targetParam, newVal);
            }
        }

        private void DgvItems_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                DataGridView.HitTestInfo hit = dgvItems.HitTest(e.X, e.Y);
                if (hit.RowIndex >= 0 && !dgvItems.Rows[hit.RowIndex].Selected)
                {
                    dgvItems.ClearSelection();
                    dgvItems.Rows[hit.RowIndex].Selected = true;
                }

                ContextMenuStrip menu = new ContextMenuStrip();
                menu.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

                int selCount = dgvItems.SelectedRows.Count;
                ToolStripMenuItem itemBatch = new ToolStripMenuItem(string.Format("Gán giá trị Parameter cho {0} dòng đã chọn...", selCount));
                itemBatch.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                itemBatch.Click += (s, ev) => BatchAssignParameterValue();
                menu.Items.Add(itemBatch);

                menu.Items.Add(new ToolStripSeparator());

                ToolStripMenuItem itemAddParam = new ToolStripMenuItem("Thêm Cột Parameter Dự Án Vào Bảng...");
                itemAddParam.Click += (s, ev) => BtnAddProjectParamCol_Click(s, ev);
                menu.Items.Add(itemAddParam);

                ToolStripMenuItem itemCols = new ToolStripMenuItem("Ẩn / Hiện Cột Bảng...");
                itemCols.Click += (s, ev) => BtnToggleGridCols_Click(btnToggleGridCols, EventArgs.Empty);
                menu.Items.Add(itemCols);

                menu.Items.Add(new ToolStripSeparator());

                ToolStripMenuItem itemCopy = new ToolStripMenuItem("Sao Chép Các Dòng Đã Chọn");
                itemCopy.Click += (s, ev) => BtnCopyClipboard_Click(s, ev);
                menu.Items.Add(itemCopy);

                menu.Show(dgvItems, e.Location);
            }
        }

        private void BatchAssignParameterValue()
        {
            var selectedRows = dgvItems.SelectedRows;
            if (selectedRows.Count == 0)
            {
                MessageBox.Show("Vui lòng quét chọn các dòng trong bảng trước khi gán giá trị parameter!", "HDTOOL", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<string> availableParams = new List<string>();
            availableParams.Add("Location by Area");
            availableParams.Add("PN");
            availableParams.Add("Location by Level");
            if (_customParamColumns != null)
            {
                foreach (string cp in _customParamColumns)
                {
                    if (!availableParams.Contains(cp)) availableParams.Add(cp);
                }
            }

            using (BatchAssignParamDialog dlg = new BatchAssignParamDialog(availableParams, selectedRows.Count))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    string targetParam = dlg.SelectedParam;
                    string targetValue = dlg.NewValue ?? string.Empty;

                    int colIdx = -1;
                    foreach (DataGridViewColumn col in dgvItems.Columns)
                    {
                        if (col.HeaderText.Equals(targetParam, StringComparison.OrdinalIgnoreCase))
                        {
                            colIdx = col.Index;
                            break;
                        }
                    }

                    List<ElementId> allTargetIds = new List<ElementId>();
                    foreach (DataGridViewRow row in selectedRows)
                    {
                        if (colIdx >= 0) row.Cells[colIdx].Value = targetValue;
                        MepFittingItem it = row.Tag as MepFittingItem;
                        if (it != null)
                        {
                            if (targetParam.Equals("Location by Area", StringComparison.OrdinalIgnoreCase)) it.LocationByArea = targetValue;
                            else if (targetParam.Equals("PN", StringComparison.OrdinalIgnoreCase)) it.PN = targetValue;
                            else if (targetParam.Equals("Location by Level", StringComparison.OrdinalIgnoreCase)) it.LocationByLevel = targetValue;
                            else
                            {
                                if (it.CustomParameters == null) it.CustomParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                                it.CustomParameters[targetParam] = targetValue;
                            }

                            if (it.ElementIds != null) allTargetIds.AddRange(it.ElementIds);
                        }
                    }

                    if (allTargetIds.Count > 0)
                    {
                        WriteParamValueToElements(allTargetIds, targetParam, targetValue);
                    }
                }
            }
        }


        // ======================= QUẢN LÝ & LOẠI BỎ CỘT PARAMETER THEO LỆNH SẾP HỒ =======================

        private void DgvItems_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && e.ColumnIndex >= 0)
            {
                DataGridViewColumn col = dgvItems.Columns[e.ColumnIndex];
                string colName = col.HeaderText;

                ContextMenuStrip menu = new ContextMenuStrip();
                menu.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

                // 1. Loại bỏ cột này khỏi bảng
                ToolStripMenuItem itemRemove = new ToolStripMenuItem(string.Format("Loại Bỏ Cột '{0}' Khỏi Bảng", colName));
                itemRemove.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                itemRemove.ForeColor = Color.FromArgb(220, 38, 38); // Đỏ nổi bật
                itemRemove.Click += (s, ev) => RemoveParameterColumn(colName);
                menu.Items.Add(itemRemove);

                // 2. Ẩn cột này
                ToolStripMenuItem itemHide = new ToolStripMenuItem(string.Format("Ẩn Cột '{0}'", colName));
                itemHide.Click += (s, ev) => { col.Visible = false; };
                menu.Items.Add(itemHide);

                menu.Items.Add(new ToolStripSeparator());

                // 3. Thêm cột Parameter mới
                ToolStripMenuItem itemAdd = new ToolStripMenuItem("Thêm Cột Parameter Dự Án...");
                itemAdd.Click += (s, ev) => BtnAddProjectParamCol_Click(null, null);
                menu.Items.Add(itemAdd);

                // 4. Đồng bộ theo Schedule Revit
                ToolStripMenuItem itemSync = new ToolStripMenuItem("Đồng Bộ Cột Theo Bảng Schedule Trong Revit");
                itemSync.Click += (s, ev) => SyncColumnsWithRevitSchedule();
                menu.Items.Add(itemSync);

                // 5. Quản lý ẩn hiện cột
                ToolStripMenuItem itemManage = new ToolStripMenuItem("Quản Lý Ẩn / Hiện Cột...");
                itemManage.Click += (s, ev) => BtnToggleGridCols_Click(null, null);
                menu.Items.Add(itemManage);

                menu.Show(Cursor.Position);
            }
        }

        public void RemoveParameterColumn(string colName)
        {
            if (string.IsNullOrWhiteSpace(colName)) return;

            if (colName.Equals("STT", StringComparison.OrdinalIgnoreCase) ||
                colName.StartsWith("Tên Phụ Kiện", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Cột '" + colName + "' là cột định danh cơ bản của bảng, bạn có thể kéo sang vị trí khác chứ không nên xóa bỏ.", "HDTOOL", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 1. Nếu là cột tùy chỉnh
            if (_customParamColumns != null && _customParamColumns.Contains(colName))
            {
                _customParamColumns.Remove(colName); SaveColumnSettings();
                foreach (var it in _allItems)
                {
                    if (it.CustomParameters != null && it.CustomParameters.ContainsKey(colName))
                    {
                        it.CustomParameters.Remove(colName);
                    }
                }
            }

            // 2. Tìm và loại bỏ hoặc ẩn khỏi DataGridView
            DataGridViewColumn targetCol = null;
            foreach (DataGridViewColumn col in dgvItems.Columns)
            {
                if (col.HeaderText.Equals(colName, StringComparison.OrdinalIgnoreCase))
                {
                    targetCol = col;
                    break;
                }
            }

            if (targetCol != null)
            {
                if (targetCol.Name.StartsWith("col_cust_"))
                {
                    dgvItems.Columns.Remove(targetCol);
                }
                else
                {
                    targetCol.Visible = false;
                }
            }

            lblHighlightStatus.ForeColor = Color.FromArgb(16, 185, 129);
            lblHighlightStatus.Text = string.Format("Đã loại bỏ cột '{0}' khỏi bảng khối lượng.", colName);
        }

        public void AddCustomProjectParameterColumn(string paramName)
        {
            if (string.IsNullOrWhiteSpace(paramName)) return;

            foreach (DataGridViewColumn col in dgvItems.Columns)
            {
                if (col.HeaderText.Equals(paramName, StringComparison.OrdinalIgnoreCase))
                {
                    col.Visible = true;
                    return;
                }
            }

            if (!_customParamColumns.Contains(paramName))
            {
                _customParamColumns.Add(paramName);
            }

            DataGridViewTextBoxColumn newCol = new DataGridViewTextBoxColumn();
            newCol.Name = "col_cust_" + paramName;
            newCol.HeaderText = paramName;
            newCol.Width = 115;
            newCol.ReadOnly = false;
            newCol.DefaultCellStyle.BackColor = Color.FromArgb(240, 249, 255);
            newCol.DefaultCellStyle.ForeColor = Color.FromArgb(15, 23, 42);
            newCol.DefaultCellStyle.SelectionBackColor = Color.FromArgb(2, 132, 199);
            newCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;

            dgvItems.Columns.Add(newCol);

            foreach (DataGridViewRow row in dgvItems.Rows)
            {
                MepFittingItem it = row.Tag as MepFittingItem;
                if (it != null)
                {
                    string val = GetElementParamValue(_doc, it.ElementIds, paramName);
                    if (it.CustomParameters == null) it.CustomParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    it.CustomParameters[paramName] = val;
                    row.Cells[newCol.Index].Value = val;
                }
            }
        }

        public void SyncColumnsWithRevitSchedule()
        {
            if (_doc == null) return;

            List<string> allAvail;
            string schedName;
            List<string> schedFields = MepQuantityService.GetRevitScheduleFields(_doc, out allAvail, out schedName);

            if (schedFields == null || schedFields.Count == 0)
            {
                MessageBox.Show("Không tìm thấy bảng Schedule khối lượng ống nào trong dự án Revit để đồng bộ.", "HDTOOL", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 1. Tham số nào KHÔNG CÒN nằm trong bảng Schedule Revit -> TỰ ĐỘNG MẤT ĐI (loại bỏ)!
            List<string> toRemove = new List<string>();
            foreach (string cp in _customParamColumns)
            {
                if (!schedFields.Contains(cp))
                {
                    toRemove.Add(cp);
                }
            }
            foreach (string rem in toRemove)
            {
                RemoveParameterColumn(rem);
            }

            // Cập nhật ẩn/hiện các cột chuẩn theo Schedule
            bool hasArea = schedFields.Any(x => x.IndexOf("Area", StringComparison.OrdinalIgnoreCase) >= 0 || x.IndexOf("Khu", StringComparison.OrdinalIgnoreCase) >= 0);
            bool hasLvl = schedFields.Any(x => x.IndexOf("Level", StringComparison.OrdinalIgnoreCase) >= 0 || x.IndexOf("Tầng", StringComparison.OrdinalIgnoreCase) >= 0);
            bool hasPn = schedFields.Any(x => x.Equals("PN", StringComparison.OrdinalIgnoreCase) || x.IndexOf("Áp lực", StringComparison.OrdinalIgnoreCase) >= 0);

            foreach (DataGridViewColumn col in dgvItems.Columns)
            {
                if (col.HeaderText.Equals("Location by Area", StringComparison.OrdinalIgnoreCase))
                {
                    col.Visible = hasArea;
                }
                else if (col.HeaderText.Equals("Location by Level", StringComparison.OrdinalIgnoreCase))
                {
                    col.Visible = hasLvl;
                }
                else if (col.HeaderText.Equals("PN", StringComparison.OrdinalIgnoreCase))
                {
                    col.Visible = hasPn;
                }
            }

            // 2. Tham số nào CÓ trong Schedule Revit -> TỰ ĐỘNG THÊM VÀO!
            int addedCount = 0;
            string[] standardNames = new string[] { "STT", "Tên Phụ Kiện", "Phân Loại", "Size", "Diameter", "Kích Thước", "Số Lượng", "Count", "Chiều Dài", "Length", "Type", "Location by Area", "PN", "Location by Level", "System Type", "Hệ Thống" };

            foreach (string sf in schedFields)
            {
                if (standardNames.Any(x => sf.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0)) continue;

                if (!_customParamColumns.Contains(sf))
                {
                    AddCustomProjectParameterColumn(sf);
                    addedCount++;
                }
            }

            lblHighlightStatus.ForeColor = Color.FromArgb(16, 185, 129);
            lblHighlightStatus.Text = string.Format("Đã đồng bộ 100% cột theo bảng Schedule '{0}' (Thêm {1} cột, loại bỏ {2} cột).",
                schedName, addedCount, toRemove.Count);

            MessageBox.Show(string.Format("Đã đồng bộ 100% cột theo Bảng Khối Lượng Schedule '{0}' trong Revit!\n\n• Thêm mới các tham số đang dùng: {1} cột\n• Tự động loại bỏ các tham số không dùng: {2} cột",
                schedName, addedCount, toRemove.Count), "HDTOOL - Đồng Bộ Schedule", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnAddProjectParamCol_Click(object sender, EventArgs e)
        {
            List<string> allAvail;
            string schedName;
            List<string> schedParams = MepQuantityService.GetRevitScheduleFields(_doc, out allAvail, out schedName);
            if (allAvail == null || allAvail.Count == 0) allAvail = GetAvailableProjectParameters();

            List<string> currentCols = new List<string>();
            foreach (DataGridViewColumn col in dgvItems.Columns)
            {
                if (col.Visible && !col.HeaderText.Equals("STT", StringComparison.OrdinalIgnoreCase) && !col.HeaderText.StartsWith("Tên Phụ Kiện", StringComparison.OrdinalIgnoreCase))
                {
                    currentCols.Add(col.HeaderText);
                }
            }

            using (AddProjectParameterDialog dlg = new AddProjectParameterDialog(schedParams, allAvail, currentCols, schedName))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    if (dlg.Action == AddProjectParamAction.SyncSchedule)
                    {
                        SyncColumnsWithRevitSchedule();
                    }
                    else if (dlg.Action == AddProjectParamAction.RemoveColumn)
                    {
                        RemoveParameterColumn(dlg.SelectedRemoveColumn);
                    }
                    else if (dlg.Action == AddProjectParamAction.AddColumn)
                    {
                        string paramName = dlg.SelectedParamName;
                        if (string.IsNullOrWhiteSpace(paramName)) return;

                        AddCustomProjectParameterColumn(paramName);
                        lblHighlightStatus.ForeColor = Color.FromArgb(16, 185, 129);
                        lblHighlightStatus.Text = string.Format("Đã thêm cột Parameter '{0}' vào bảng! Bạn có thể click trực tiếp vào từng ô để chỉnh sửa giá trị.", paramName);
                    }
                }
            }
        }

        private List<string> GetAvailableProjectParameters()
        {
            SortedSet<string> paramNames = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            // Các tham số phổ biến trong dự án MEP
            paramNames.Add("Comments");
            paramNames.Add("Mark");
            paramNames.Add("Location by Area");
            paramNames.Add("Location by Level");
            paramNames.Add("MN_FOLDER");
            paramNames.Add("MN_SUB-FOLDER 1");
            paramNames.Add("MN_SUB-FOLDER 2");
            paramNames.Add("Workset");
            paramNames.Add("Phase Created");
            paramNames.Add("Fabrication Service");
            paramNames.Add("System Classification");

            if (_doc != null && _allItems != null)
            {
                int sampleCount = 0;
                foreach (var it in _allItems)
                {
                    if (it.ElementIds == null) continue;
                    foreach (ElementId id in it.ElementIds)
                    {
                        Element el = _doc.GetElement(id);
                        if (el == null) continue;

                        foreach (Parameter p in el.Parameters)
                        {
                            if (p.IsReadOnly && p.StorageType != StorageType.String) continue;
                            string pName = p.Definition != null ? p.Definition.Name : string.Empty;
                            if (!string.IsNullOrEmpty(pName) && !pName.StartsWith("ELEM_") && !pName.StartsWith("SYMBOL_"))
                            {
                                paramNames.Add(pName);
                            }
                        }

                        sampleCount++;
                        if (sampleCount >= 25) break;
                    }
                    if (sampleCount >= 25) break;
                }
            }

            return new List<string>(paramNames);
        }

        private string GetElementParamValue(Document doc, List<ElementId> ids, string paramName)
        {
            if (doc == null || ids == null || ids.Count == 0 || string.IsNullOrWhiteSpace(paramName))
                return string.Empty;

            foreach (ElementId id in ids)
            {
                Element el = doc.GetElement(id);
                if (el == null) continue;
                Parameter p = el.LookupParameter(paramName);
                if (p != null && p.HasValue)
                {
                    switch (p.StorageType)
                    {
                        case StorageType.String:
                            string s = p.AsString();
                            if (!string.IsNullOrEmpty(s)) return s;
                            break;
                        case StorageType.Integer:
                            return p.AsInteger().ToString();
                        case StorageType.Double:
                            return p.AsValueString() ?? p.AsDouble().ToString("0.##");
                        case StorageType.ElementId:
                            ElementId eid = p.AsElementId();
                            if (eid != ElementId.InvalidElementId)
                            {
                                Element refEl = doc.GetElement(eid);
                                if (refEl != null) return refEl.Name;
                            }
                            break;
                    }
                }
            }
            return string.Empty;
        }

        private void WriteParamValueToElements(List<ElementId> ids, string paramName, string newVal)
        {
            if (ids == null || ids.Count == 0 || string.IsNullOrEmpty(paramName)) return;

            if (_handler != null && _extEvent != null)
            {
                _handler.ActionToRun = (app) =>
                {
                    try
                    {
                        Document doc = app.ActiveUIDocument != null ? app.ActiveUIDocument.Document : _doc;
                        if (doc == null) return;

                        int updatedCount = 0;
                        using (Transaction t = new Transaction(doc, "HDTOOL - Cập Nhật Parameter " + paramName))
                        {
                            t.Start();
                            foreach (ElementId id in ids)
                            {
                                Element el = doc.GetElement(id);
                                if (el == null) continue;

                                Parameter p = el.LookupParameter(paramName);
                                if (p != null && !p.IsReadOnly)
                                {
                                    switch (p.StorageType)
                                    {
                                        case StorageType.String:
                                            p.Set(newVal ?? string.Empty);
                                            updatedCount++;
                                            break;
                                        case StorageType.Integer:
                                            int iVal;
                                            if (int.TryParse(newVal, out iVal)) { p.Set(iVal); updatedCount++; }
                                            break;
                                        case StorageType.Double:
                                            double dVal;
                                            if (double.TryParse(newVal, out dVal)) { p.Set(dVal); updatedCount++; }
                                            break;
                                    }
                                }
                            }
                            t.Commit();
                        }

                        SafeInvoke(() =>
                        {
                            lblHighlightStatus.ForeColor = Color.FromArgb(16, 185, 129); // Emerald Green
                            lblHighlightStatus.Text = string.Format("Đã lưu '{0}' vào tham số '{1}' cho {2} đối tượng Revit!",
                                newVal, paramName, updatedCount);
                        });
                    }
                    catch (Exception ex)
                    {
                        SafeInvoke(() =>
                        {
                            lblHighlightStatus.ForeColor = Color.FromArgb(220, 38, 38);
                            lblHighlightStatus.Text = "Lỗi khi cập nhật tham số: " + ex.Message;
                        });
                    }
                };
                _extEvent.Raise();
            }
        }

        private void SyncAllTableParametersToRevit()
        {
            if (_doc == null || _allItems == null || _allItems.Count == 0) return;

            if (_handler != null && _extEvent != null)
            {
                _handler.ActionToRun = (app) =>
                {
                    try
                    {
                        Document doc = app.ActiveUIDocument != null ? app.ActiveUIDocument.Document : _doc;
                        if (doc == null) return;

                        int totalUpdated = 0;
                        using (Transaction t = new Transaction(doc, "HDTOOL - Đồng Bộ Toàn Bộ Parameter Bảng Khối Lượng"))
                        {
                            t.Start();

                            foreach (var it in _allItems)
                            {
                                if (it.ElementIds == null || it.ElementIds.Count == 0) continue;

                                foreach (ElementId id in it.ElementIds)
                                {
                                    Element el = doc.GetElement(id);
                                    if (el == null) continue;

                                    if (!string.IsNullOrEmpty(it.LocationByArea))
                                    {
                                        Parameter pArea = el.LookupParameter("Location by Area");
                                        if (pArea != null && !pArea.IsReadOnly && pArea.StorageType == StorageType.String)
                                        {
                                            pArea.Set(it.LocationByArea);
                                            totalUpdated++;
                                        }
                                    }

                                    if (!string.IsNullOrEmpty(it.PN))
                                    {
                                        Parameter pPN = el.LookupParameter("PN");
                                        if (pPN != null && !pPN.IsReadOnly && pPN.StorageType == StorageType.String)
                                        {
                                            pPN.Set(it.PN);
                                            totalUpdated++;
                                        }
                                    }

                                    if (!string.IsNullOrEmpty(it.LocationByLevel))
                                    {
                                        Parameter pLvl = el.LookupParameter("Location by Level");
                                        if (pLvl != null && !pLvl.IsReadOnly && pLvl.StorageType == StorageType.String)
                                        {
                                            pLvl.Set(it.LocationByLevel);
                                            totalUpdated++;
                                        }
                                    }

                                    if (it.CustomParameters != null && it.CustomParameters.Count > 0)
                                    {
                                        foreach (var kvp in it.CustomParameters)
                                        {
                                            Parameter pCust = el.LookupParameter(kvp.Key);
                                            if (pCust != null && !pCust.IsReadOnly)
                                            {
                                                switch (pCust.StorageType)
                                                {
                                                    case StorageType.String:
                                                        pCust.Set(kvp.Value ?? "");
                                                        totalUpdated++;
                                                        break;
                                                    case StorageType.Integer:
                                                        int iV;
                                                        if (int.TryParse(kvp.Value, out iV)) { pCust.Set(iV); totalUpdated++; }
                                                        break;
                                                    case StorageType.Double:
                                                        double dV;
                                                        if (double.TryParse(kvp.Value, out dV)) { pCust.Set(dV); totalUpdated++; }
                                                        break;
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            t.Commit();
                        }

                        int unifiedCount = 0;
                        MepQuantityService.SyncUnifiedParameterValues(
                            app.Application,
                            doc,
                            false,
                            null,
                            out unifiedCount,
                            MepQuantityService.DefaultUnifiedParamName,
                            (radSelection != null && radSelection.Checked) ? _customSelectionIds : null
                        );

                        SafeInvoke(() =>
                        {
                            TaskDialog.Show("HDTOOL - Đồng Bộ Parameter",
                                string.Format("ĐỒNG BỘ THÀNH CÔNG 100% VÀO MÔ HÌNH REVIT!\n\n" +
                                              "• Đã cập nhật {0} giá trị tham số (Location by Area, Location by Level, Custom Params) vào các đối tượng mô hình.\n" +
                                              "• Đã đồng bộ {1} đối tượng vào tham số tên gộp '{2}'.\n\n" +
                                              "Các đối tượng trên bản vẽ và bảng Schedule trong Revit đã được cập nhật đồng bộ hoàn toàn!",
                                              totalUpdated, unifiedCount, MepQuantityService.DefaultUnifiedParamName));
                            lblHighlightStatus.ForeColor = Color.FromArgb(16, 185, 129);
                            lblHighlightStatus.Text = string.Format("Đã đồng bộ toàn bộ giá trị bảng vào {0} đối tượng trên mô hình Revit!", totalUpdated);
                        });
                    }
                    catch (Exception ex)
                    {
                        SafeInvoke(() =>
                        {
                            TaskDialog.Show("HDTOOL - Lỗi Đồng Bộ", "Lỗi: " + ex.Message);
                        });
                    }
                };
                _extEvent.Raise();
            }
        }

        private void BtnPickHighlightOnSheet_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;

            if (_handler != null && _extEvent != null)
            {
                _handler.ActionToRun = (app) =>
                {
                    try
                    {
                        UIDocument uidoc = app.ActiveUIDocument;
                        if (uidoc == null) return;

                        string msg;
                        bool ok = MepQuantityService.PickAndHighlightFromSheetTable(uidoc, out msg);
                        if (!ok && !string.IsNullOrEmpty(msg))
                        {
                            TaskDialog.Show("HDTOOL - Soi Sáng Bảng Sheet", msg);
                        }
                    }
                    catch (Exception ex)
                    {
                        TaskDialog.Show("HDTOOL - Lỗi", "Lỗi soi sáng từ bảng Sheet: " + ex.Message);
                    }
                    finally
                    {
                        SafeInvoke(() =>
                        {
                            this.WindowState = FormWindowState.Normal;
                            this.BringToFront();
                            this.Activate();
                        });
                    }
                };

                _extEvent.Raise();
            }
        }

        // ======================= ĐẶT VÀO SHEET & ĐỒNG BỘ =======================

        private void BtnQuickCreateSchedule_Click(object sender, EventArgs e)
        {
            // Mở ngay hộp thoại để Sếp chọn Sheet mong muốn và tùy ý ẩn/hiện cột
            BtnPlaceOnSheet_Click(sender, e);
        }

        private void BtnPlaceOnSheet_Click(object sender, EventArgs e)
        {
            if (_doc == null) return;

            // 1. Lấy danh sách cột đang hiển thị theo đúng thứ tự DisplayIndex người dùng sắp xếp
            var visibleCols = dgvItems.Columns.Cast<DataGridViewColumn>()
                .Where(c => c.Visible)
                .OrderBy(c => c.DisplayIndex)
                .ToList();

            if (visibleCols.Count == 0)
            {
                MessageBox.Show(this, "Không có cột nào đang hiển thị trên bảng khối lượng.", "HDTOOL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 2. Lấy danh sách hàng đang hiển thị theo đúng bộ lọc trên bảng
            var visibleRows = dgvItems.Rows.Cast<DataGridViewRow>()
                .Where(r => !r.IsNewRow && r.Visible)
                .ToList();

            if (visibleRows.Count == 0)
            {
                MessageBox.Show(this, "Không có dữ liệu khối lượng nào đang hiển thị trên bảng để đưa vào Sheet.", "HDTOOL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 3. Trích xuất ma trận giá trị chuỗi từng ô và danh sách Item tương ứng
            List<string[]> rowValues = new List<string[]>();
            List<MepFittingItem> exportItems = new List<MepFittingItem>();

            int autoStt = 1;
            foreach (DataGridViewRow row in visibleRows)
            {
                string[] vals = new string[visibleCols.Count];
                for (int c = 0; c < visibleCols.Count; c++)
                {
                    var gridCol = visibleCols[c];
                    object cellObj = row.Cells[gridCol.Index].Value;
                    string textVal = cellObj != null ? cellObj.ToString() : "";

                    if (gridCol.HeaderText.Trim().Equals("STT", StringComparison.OrdinalIgnoreCase))
                    {
                        textVal = autoStt.ToString();
                    }
                    vals[c] = textVal;
                }
                rowValues.Add(vals);
                autoStt++;

                var it = row.Tag as MepFittingItem;
                if (it == null) it = new MepFittingItem();
                exportItems.Add(it);
            }

            // 4. Xây dựng danh sách dynamicColumns chuẩn 100% WYSIWYG
            List<ExcelTableColumn> dynamicColumns = new List<ExcelTableColumn>();
            for (int c = 0; c < visibleCols.Count; c++)
            {
                var gridCol = visibleCols[c];
                int colIdxInMatrix = c;
                string header = gridCol.HeaderText;

                // Xác định căn lề chuẩn kỹ thuật
                HorizontalTextAlignment align = HorizontalTextAlignment.Left;
                string hLower = header.ToLowerInvariant();
                if (hLower == "stt" || hLower.Contains("size") || hLower == "pn" || hLower.Contains("phân loại") || hLower.Contains("tầng") || hLower.Contains("khu vực") || hLower.Contains("level") || hLower.Contains("area"))
                {
                    align = HorizontalTextAlignment.Center;
                }
                else if (hLower.Contains("số lượng") || hLower.Contains("chiều dài") || hLower.Contains("count") || hLower.Contains("length") || hLower.Contains("(m)"))
                {
                    align = HorizontalTextAlignment.Right;
                }

                // Chiều rộng khởi điểm (mm) từ Width của gridCol
                double initialWidthMm = Math.Max(gridCol.Width * 0.28, 14.0);

                var excelCol = new ExcelTableColumn(
                    header,
                    initialWidthMm,
                    align,
                    (item, rowNum1Based) =>
                    {
                        int r = rowNum1Based - 1;
                        if (r >= 0 && r < rowValues.Count && colIdxInMatrix < rowValues[r].Length)
                        {
                            return rowValues[r][colIdxInMatrix];
                        }
                        return "";
                    }
                );

                dynamicColumns.Add(excelCol);
            }

            // 5. Lấy danh sách Sheet trong dự án
            List<ViewSheet> sheets = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(s => !s.IsTemplate)
                .OrderBy(s => s.SheetNumber)
                .ToList();

            string defaultSchedName = GenerateSmartScheduleName();

            // Mở hộp thoại chọn định dạng bảng và cách đưa vào Sheet
            using (SelectSheetDialog dlg = new SelectSheetDialog(sheets, _activeView as ViewSheet, defaultSchedName))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    string schedName = dlg.ScheduleName;
                    SheetPlacementMode mode = dlg.PlacementMode;
                    ViewSheet targetSheet = dlg.SelectedSheet;
                    ScheduleColumnVisibilityOptions colOptions = dlg.ColumnOptions;
                    TableExportFormat tableFormat = dlg.TableFormat;

                    if (tableFormat == TableExportFormat.ExcelTableOnSheet)
                    {
                        // ================= 1. BẢNG KẺ KHUNG EXCEL CHUẨN 100% NHƯ ADDIN =================
                        if (mode == SheetPlacementMode.CreateInProjectBrowserOnly)
                        {
                            if (_handler != null && _extEvent != null)
                            {
                                _handler.ActionToRun = (app) =>
                                {
                                    Document doc = app.ActiveUIDocument.Document;
                                    Autodesk.Revit.DB.View createdDrafting;
                                    Viewport createdVp;
                                    string msg;
                                    bool ok = MepQuantityService.CreateOrUpdateExcelTableOnSheet(
                                        app.Application,
                                        doc,
                                        schedName,
                                        null,
                                        null,
                                        exportItems,
                                        colOptions,
                                        out createdDrafting,
                                        out createdVp,
                                        out msg,
                                        dynamicColumns,
                                        true
                                    );

                                    if (ok)
                                    {
                                        TaskDialog.Show("HDTOOL - Bảng Khối Lượng Legends", string.Format("Đã tạo bảng khối lượng Excel '{0}' trong mục LEGENDS (Chú giải)!\n\nBạn hãy mở Sheet mong muốn và KÉO THẢ bảng này từ nhánh Legends trong Project Browser vào bản vẽ (có thể đặt lên nhiều Sheet cùng lúc).",
                                            createdDrafting != null ? createdDrafting.Name : schedName));
                                    }
                                    else
                                    {
                                        TaskDialog.Show("HDTOOL - Lỗi", msg);
                                    }
                                };
                                _extEvent.Raise();
                            }
                        }
                        else if (mode == SheetPlacementMode.PickPointOnActiveSheet)
                        {
                            this.WindowState = FormWindowState.Minimized;

                            if (_handler != null && _extEvent != null)
                            {
                                _handler.ActionToRun = (app) =>
                                {
                                    try
                                    {
                                        UIDocument uidoc = app.ActiveUIDocument;
                                        Document doc = uidoc.Document;
                                        Autodesk.Revit.DB.View curView = uidoc.ActiveView;
                                        ViewSheet sheet = curView as ViewSheet;

                                        if (sheet == null)
                                        {
                                            TaskDialog.Show("HDTOOL", "Khung nhìn hiện tại (" + curView.Name + ") không phải là Sheet.\nVui lòng mở một bản vẽ Sheet trên Revit trước khi chọn điểm đặt!");
                                            return;
                                        }

                                        XYZ pickedPoint = uidoc.Selection.PickPoint(
                                            Autodesk.Revit.UI.Selection.ObjectSnapTypes.None,
                                            "Click chọn vị trí bất kỳ trên bản vẽ Sheet để đặt bảng khối lượng Excel..."
                                        );

                                        Autodesk.Revit.DB.View createdDrafting;
                                        Viewport createdVp;
                                        string msg;
                                        bool ok = MepQuantityService.CreateOrUpdateExcelTableOnSheet(
                                            app.Application,
                                            doc,
                                            schedName,
                                            sheet,
                                            pickedPoint,
                                            exportItems,
                                            colOptions,
                                            out createdDrafting,
                                            out createdVp,
                                            out msg,
                                            dynamicColumns,
                                            true
                                        );

                                        if (ok)
                                        {
                                            TaskDialog.Show("HDTOOL - Thành Công", msg);
                                        }
                                        else
                                        {
                                            TaskDialog.Show("HDTOOL - Lỗi", msg);
                                        }
                                    }
                                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                                    {
                                        // Người dùng bấm ESC
                                    }
                                    catch (Exception ex)
                                    {
                                        TaskDialog.Show("HDTOOL - Lỗi", "Lỗi đặt bảng lên Sheet: " + ex.Message);
                                    }
                                    finally
                                    {
                                        SafeInvoke(() =>
                                        {
                                            this.WindowState = FormWindowState.Normal;
                                            this.BringToFront();
                                            this.Activate();
                                        });
                                    }
                                };
                                _extEvent.Raise();
                            }
                        }
                        else if (mode == SheetPlacementMode.AutoPlaceOnSelectedSheet && targetSheet != null)
                        {
                            if (_handler != null && _extEvent != null)
                            {
                                _handler.ActionToRun = (app) =>
                                {
                                    Document doc = app.ActiveUIDocument.Document;
                                    Autodesk.Revit.DB.View createdDrafting;
                                    Viewport createdVp;
                                    string msg;
                                    bool ok = MepQuantityService.CreateOrUpdateExcelTableOnSheet(
                                        app.Application,
                                        doc,
                                        schedName,
                                        targetSheet,
                                        null,
                                        exportItems,
                                        colOptions,
                                        out createdDrafting,
                                        out createdVp,
                                        out msg,
                                        dynamicColumns,
                                        true
                                    );

                                    if (ok)
                                    {
                                        TaskDialog.Show("HDTOOL - Thành Công", msg);
                                    }
                                    else
                                    {
                                        TaskDialog.Show("HDTOOL - Lỗi", msg);
                                    }
                                };
                                _extEvent.Raise();
                            }
                        }
                    }
                    else
                    {
                        // ================= 2. BẢNG SCHEDULE REVIT (GỐC) =================
                        ICollection<ElementId> customIds = (radSelection != null && radSelection.Checked) ? _customSelectionIds : null;
                        if (_handler != null && _extEvent != null)
                        {
                            _handler.ActionToRun = (app) =>
                            {
                                Document doc = app.ActiveUIDocument.Document;
                                ViewSchedule createdSched;
                                                                string msg;
                                bool ok = MepQuantityService.CreateScheduleAndPlaceOnSheet(
                                    app.Application,
                                    doc,
                                    schedName,
                                    mode == SheetPlacementMode.AutoPlaceOnSelectedSheet ? targetSheet : null,
                                    mode == SheetPlacementMode.AutoPlaceOnSelectedSheet ? new XYZ(1.2, 1.2, 0) : null,
                                    out createdSched,
                                    out msg,
                                    MepQuantityService.DefaultUnifiedParamName,
                                    customIds,
                                    colOptions
                                );

                                if (ok)
                                {
                                    TaskDialog.Show("HDTOOL - Tạo Schedule", msg);
                                }
                                else
                                {
                                    TaskDialog.Show("HDTOOL - Lỗi", msg);
                                }
                            };
                            _extEvent.Raise();
                        }
                    }
                }
            }
        }

        private void BtnSyncParams_Click(object sender, EventArgs e)
        {
            SyncAllTableParametersToRevit();
        }

        private void BtnExportExcel_Click(object sender, EventArgs e)
        {
            if (dgvItems.Rows.Count == 0)
            {
                MessageBox.Show("Không có dữ liệu để xuất.", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Title = "Lưu File Khối Lượng Ống Nước & Phụ Kiện";
            sfd.Filter = "File CSV UTF-8 (*.csv)|*.csv|Tất Cả Các File (*.*)|*.*";
            string fileKind = _filterMode == MepQuantityFilterMode.PipesOnly ? "OngNuoc" : (_filterMode == MepQuantityFilterMode.FittingsOnly ? "PhuKien" : "OngNuoc_Va_PhuKien");
            sfd.FileName = string.Format("HDTOOL_KhoiLuong_{0}_{1}.csv", fileKind, DateTime.Now.ToString("yyyyMMdd_HHmm"));

            if (sfd.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    var orderedCols = dgvItems.Columns.Cast<DataGridViewColumn>()
                        .Where(c => c.Visible)
                        .OrderBy(c => c.DisplayIndex)
                        .ToList();

                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine(string.Join(",", orderedCols.Select(c => "\"" + c.HeaderText.Replace("\"", "\"\"") + "\"")));

                    foreach (DataGridViewRow row in dgvItems.Rows)
                    {
                        if (row.IsNewRow) continue;
                        List<string> rowVals = new List<string>();
                        foreach (var col in orderedCols)
                        {
                            object val = row.Cells[col.Index].Value;
                            string sVal = val != null ? val.ToString().Replace("\"", "\"\"") : "";
                            rowVals.Add("\"" + sVal + "\"");
                        }
                        sb.AppendLine(string.Join(",", rowVals));
                    }

                    File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show("Xuất file thành công (theo đúng thứ tự cột đã sắp xếp) tại:\n" + sfd.FileName, "Thành Công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi xuất file: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnCopyClipboard_Click(object sender, EventArgs e)
        {
            if (dgvItems.Rows.Count == 0)
            {
                MessageBox.Show("Không có dữ liệu để sao chép.", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                // Lấy các cột hiển thị theo thứ tự DisplayIndex mà người dùng đã kéo thả sắp xếp!
                var orderedCols = dgvItems.Columns.Cast<DataGridViewColumn>()
                    .Where(c => c.Visible)
                    .OrderBy(c => c.DisplayIndex)
                    .ToList();

                StringBuilder sb = new StringBuilder();
                sb.AppendLine(string.Join("\t", orderedCols.Select(c => c.HeaderText)));

                foreach (DataGridViewRow row in dgvItems.Rows)
                {
                    if (row.IsNewRow) continue;
                    List<string> rowVals = new List<string>();
                    foreach (var col in orderedCols)
                    {
                        object val = row.Cells[col.Index].Value;
                        rowVals.Add(val != null ? val.ToString().Replace("\t", " ").Replace("\r", "").Replace("\n", " ") : "");
                    }
                    sb.AppendLine(string.Join("\t", rowVals));
                }

                Clipboard.SetText(sb.ToString());
                MessageBox.Show("Đã sao chép toàn bộ bảng vào Clipboard (theo đúng thứ tự cột bạn đã sắp xếp)!\nBạn có thể mở Excel và nhấn Ctrl+V để dán trực tiếp.", "Thành Công", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi sao chép: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                // TỰ ĐỘNG TẮT HẾT HIGHLIGHT, TRẢ VỀ MÀU GỐC NGUYÊN BẢN TRÊN REVIT KHI BẤM ESC!
                try
                {
                    if (_doc != null)
                    {
                        MepHighlightManager.ClearHighlights(_doc);
                        if (_uidoc != null)
                        {
                            _uidoc.Selection.SetElementIds(new List<ElementId>());
                            _uidoc.RefreshActiveView();
                        }
                    }
                }
                catch { }

                this.Close();
                return true;
            }
            if (keyData == Keys.F5)
            {
                LoadDataAndRefreshUi();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private static string TruncateString(string s, int maxLen)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            if (s.Length <= maxLen) return s;
            return s.Substring(0, maxLen - 3) + "...";
        }

        // ======================= CÂY THƯ MỤC KHỐI LƯỢNG BÊN TRÁI =======================

        public class TreeFilterTag
        {
            public string Level { get; set; }
            public string Area { get; set; }
            public string System { get; set; }
        }

        private string GenerateSmartScheduleName()
        {
            if (_allItems == null || _allItems.Count == 0)
            {
                return "<KL PHU KIEN ONG - HDTOOL>";
            }

            string primarySys = _allItems
                .Where(x => !string.IsNullOrEmpty(x.SystemType) && x.SystemType != "-")
                .GroupBy(x => x.SystemType)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault();

            string primaryArea = _allItems
                .Where(x => !string.IsNullOrEmpty(x.LocationByArea) && x.LocationByArea != "-")
                .GroupBy(x => x.LocationByArea)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault();

            string primaryLvl = _allItems
                .Where(x => !string.IsNullOrEmpty(x.LocationByLevel) && x.LocationByLevel != "-")
                .GroupBy(x => x.LocationByLevel)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault();

            string prefix = _filterMode == MepQuantityFilterMode.PipesOnly ? "<KL ONG" : (_filterMode == MepQuantityFilterMode.FittingsOnly ? "<KLPK" : "<KL ONG & PK");
            StringBuilder sb = new StringBuilder(prefix);
            if (!string.IsNullOrEmpty(primarySys))
            {
                sb.Append(" ");
                sb.Append(primarySys.ToUpper());
            }
            if (!string.IsNullOrEmpty(primaryArea))
            {
                sb.Append(" ");
                sb.Append(primaryArea.ToUpper());
            }
            if (!string.IsNullOrEmpty(primaryLvl))
            {
                sb.Append(" ");
                sb.Append(primaryLvl.ToUpper());
            }
            sb.Append(">");

            return sb.ToString();
        }

        private void PopulateQuantityTree()
        {
            if (tvQuantityTree == null) return;
            tvQuantityTree.BeginUpdate();
            tvQuantityTree.Nodes.Clear();

            // 1. Root node
            int totalCount = _allItems.Sum(x => x.Count);
            double totalLen = Math.Round(_allItems.Where(x => x.CategoryKind == "Đường Ống").Sum(x => x.TotalLengthM > 0 ? x.TotalLengthM : (x.LengthM * (x.Count > 0 ? x.Count : 1))), 2);
            string rootText = totalLen > 0
                ? string.Format("TẤT CẢ KHỐI LƯỢNG ({0} đ.tượng | {1:0.0}m)", totalCount, totalLen)
                : string.Format("TẤT CẢ KHỐI LƯỢNG ({0} đối tượng)", totalCount);

            TreeNode rootNode = new TreeNode(rootText);
            rootNode.Tag = null;

            var levelGroups = _allItems
                .GroupBy(x => string.IsNullOrEmpty(x.LocationByLevel) || x.LocationByLevel == "-" ? "Tầng Chung" : x.LocationByLevel)
                .OrderBy(g => g.Key);

            foreach (var lvlGrp in levelGroups)
            {
                int lvlCount = lvlGrp.Sum(x => x.Count);
                double lvlLen = Math.Round(lvlGrp.Where(x => x.CategoryKind == "Đường Ống").Sum(x => x.TotalLengthM > 0 ? x.TotalLengthM : (x.LengthM * (x.Count > 0 ? x.Count : 1))), 2);
                string lvlText = lvlLen > 0
                    ? string.Format("Tầng: {0} ({1} cái | {2:0.0}m)", lvlGrp.Key, lvlCount, lvlLen)
                    : string.Format("Tầng: {0} ({1} cái)", lvlGrp.Key, lvlCount);

                TreeNode lvlNode = new TreeNode(lvlText);
                lvlNode.Tag = new TreeFilterTag { Level = lvlGrp.Key == "Tầng Chung" ? null : lvlGrp.Key };

                var areaGroups = lvlGrp
                    .GroupBy(x => string.IsNullOrEmpty(x.LocationByArea) || x.LocationByArea == "-" ? "Toàn Khu Vực" : x.LocationByArea)
                    .OrderBy(g => g.Key);

                foreach (var areaGrp in areaGroups)
                {
                    int areaCount = areaGrp.Sum(x => x.Count);
                    double areaLen = Math.Round(areaGrp.Where(x => x.CategoryKind == "Đường Ống").Sum(x => x.TotalLengthM > 0 ? x.TotalLengthM : (x.LengthM * (x.Count > 0 ? x.Count : 1))), 2);
                    string areaText = areaLen > 0
                        ? string.Format("Khu vực: {0} ({1} cái | {2:0.0}m)", areaGrp.Key, areaCount, areaLen)
                        : string.Format("Khu vực: {0} ({1} cái)", areaGrp.Key, areaCount);

                    TreeNode areaNode = new TreeNode(areaText);
                    areaNode.Tag = new TreeFilterTag
                    {
                        Level = lvlGrp.Key == "Tầng Chung" ? null : lvlGrp.Key,
                        Area = areaGrp.Key == "Toàn Khu Vực" ? null : areaGrp.Key
                    };

                    var sysGroups = areaGrp
                        .GroupBy(x => string.IsNullOrEmpty(x.SystemType) || x.SystemType == "-" ? "Hệ Thống Chung" : x.SystemType)
                        .OrderBy(g => g.Key);

                    foreach (var sysGrp in sysGroups)
                    {
                        int sysCount = sysGrp.Sum(x => x.Count);
                        double sysLen = Math.Round(sysGrp.Where(x => x.CategoryKind == "Đường Ống").Sum(x => x.TotalLengthM > 0 ? x.TotalLengthM : (x.LengthM * (x.Count > 0 ? x.Count : 1))), 2);
                        string sysText = sysLen > 0
                            ? string.Format("Hệ thống: {0} ({1} cái | {2:0.0}m)", sysGrp.Key, sysCount, sysLen)
                            : string.Format("Hệ thống: {0} ({1} cái)", sysGrp.Key, sysCount);

                        TreeNode sysNode = new TreeNode(sysText);
                        sysNode.Tag = new TreeFilterTag
                        {
                            Level = lvlGrp.Key == "Tầng Chung" ? null : lvlGrp.Key,
                            Area = areaGrp.Key == "Toàn Khu Vực" ? null : areaGrp.Key,
                            System = sysGrp.Key == "Hệ Thống Chung" ? null : sysGrp.Key
                        };

                        foreach (var item in sysGrp)
                        {
                            string itText;
                            if (item.CategoryKind == "Đường Ống")
                            {
                                itText = item.Count > 1
                                    ? string.Format("• {0} [{1}] ({2} đoạn, dài {3:0.00}m)", item.MergedName, item.Size, item.Count, item.LengthM)
                                    : string.Format("• {0} [{1}] (Đoạn {2:0.00}m)", item.MergedName, item.Size, item.LengthM);
                            }
                            else
                            {
                                itText = string.Format("• {0} [{1}] ({2} cái)", item.MergedName, item.Size, item.Count);
                            }

                            TreeNode itNode = new TreeNode(itText);
                            itNode.Tag = item;
                            sysNode.Nodes.Add(itNode);
                        }

                        areaNode.Nodes.Add(sysNode);
                    }

                    lvlNode.Nodes.Add(areaNode);
                }

                rootNode.Nodes.Add(lvlNode);
            }

            tvQuantityTree.Nodes.Add(rootNode);

            // 2. Node Bảng Schedule để kéo thả vào Sheet
            string schedName = GenerateSmartScheduleName();
            TreeNode schedBranch = new TreeNode("BẢNG SCHEDULE REVIT (KÉO THẢ)");
            schedBranch.ForeColor = Color.FromArgb(13, 148, 136); // Teal
            TreeNode schedNode = new TreeNode("" + schedName);
            schedNode.ForeColor = Color.FromArgb(185, 28, 28); // Red
            schedNode.NodeFont = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            schedNode.Tag = "SCHEDULE:" + schedName;
            schedBranch.Nodes.Add(schedNode);
            tvQuantityTree.Nodes.Add(schedBranch);

            rootNode.Expand();
            if (rootNode.Nodes.Count > 0)
            {
                rootNode.Nodes[0].Expand();
                if (rootNode.Nodes[0].Nodes.Count > 0)
                {
                    rootNode.Nodes[0].Nodes[0].Expand();
                }
            }
            schedBranch.Expand();

            tvQuantityTree.EndUpdate();
        }

        private void TvQuantityTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node == null || e.Node.Tag == null)
            {
                _treeSelectedLevel = null;
                _treeSelectedArea = null;
                _treeSelectedSystem = null;
                FilterAndDisplayItems();
                return;
            }

            if (e.Node.Tag is TreeFilterTag)
            {
                TreeFilterTag tag = (TreeFilterTag)e.Node.Tag;
                _treeSelectedLevel = tag.Level;
                _treeSelectedArea = tag.Area;
                _treeSelectedSystem = tag.System;
                FilterAndDisplayItems();
            }
            else if (e.Node.Tag is MepFittingItem)
            {
                MepFittingItem item = (MepFittingItem)e.Node.Tag;
                SelectAndFocusGridRow(item);
            }
            else if (e.Node.Tag is string && ((string)e.Node.Tag).StartsWith("SCHEDULE:"))
            {
                BtnPlaceOnSheet_Click(this, EventArgs.Empty);
            }
        }

        private void SelectAndFocusGridRow(MepFittingItem targetItem)
        {
            if (targetItem == null || dgvItems.Rows.Count == 0) return;

            foreach (DataGridViewRow row in dgvItems.Rows)
            {
                MepFittingItem it = row.Tag as MepFittingItem;
                if (it != null && it.GetGroupingKey() == targetItem.GetGroupingKey())
                {
                    row.Selected = true;
                    dgvItems.FirstDisplayedScrollingRowIndex = row.Index;
                    break;
                }
            }
        }
    }

    public enum TableExportFormat
    {
        ExcelTableOnSheet, // Bảng kẻ khung Excel chuẩn 100% như Add-in (gồm cả ống + phụ kiện, STT, Dài m, Tự cập nhật)
        RevitViewSchedule  // Bảng ViewSchedule Revit truyền thống (chỉ 1 Category)
    }

    public enum SheetPlacementMode
    {
        CreateInProjectBrowserOnly,
        PickPointOnActiveSheet,
        AutoPlaceOnSelectedSheet
    }

    /// <summary>
    /// Hộp thoại trực quan cho phép người dùng chọn Sheet mong muốn, chọn định dạng bảng (Excel hoặc Schedule) và tùy ý ẩn / hiện các cột
    /// </summary>
    public class SelectSheetDialog : System.Windows.Forms.Form
    {
        private List<ViewSheet> _sheets;

        // Group 1: Định dạng bảng
        private GroupBox grpFormat;
        private RadioButton radFormatExcel;
        private RadioButton radFormatSchedule;

        // Group 2: Cách đặt vào Sheet
        private RadioButton radModeAutoSheet;
        private RadioButton radModePickPoint;
        private RadioButton radModeDragDrop;
        private ComboBox cboSheets;
        private TextBox txtScheduleName;

        private Button btnOk;
        private Button btnCancel;

        public TableExportFormat TableFormat { get; private set; }
        public SheetPlacementMode PlacementMode { get; private set; }
        public ViewSheet SelectedSheet { get; private set; }
        public string ScheduleName { get; private set; }
        public ScheduleColumnVisibilityOptions ColumnOptions { get; private set; }

        public SelectSheetDialog(List<ViewSheet> sheets, ViewSheet defaultSheet, string defaultScheduleName = null)
        {
            _sheets = sheets ?? new List<ViewSheet>();

            var userSettings = MepUserSettings.Instance;
            if (defaultSheet == null && !string.IsNullOrEmpty(userSettings.LastSelectedSheetNumber))
            {
                foreach (ViewSheet s in _sheets)
                {
                    if (s.SheetNumber.Equals(userSettings.LastSelectedSheetNumber, StringComparison.OrdinalIgnoreCase))
                    {
                        defaultSheet = s;
                        break;
                    }
                }
            }
            if (string.IsNullOrEmpty(defaultScheduleName) && !string.IsNullOrEmpty(userSettings.LastScheduleName))
            {
                defaultScheduleName = userSettings.LastScheduleName;
            }

            InitializeComponent(defaultSheet, defaultScheduleName);
        }

        private void InitializeComponent(ViewSheet defaultSheet, string defaultScheduleName)
        {
            this.SuspendLayout();

            this.Text = "HDTOOL - ĐẶT BẢNG KHỐI LƯỢNG VÀO SHEET MONG MUỐN";
            this.Size = new Size(580, 420);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(248, 250, 252);
            this.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

            // ================= 1. ĐỊNH DẠNG BẢNG =================
            grpFormat = new GroupBox();
            grpFormat.Text = " 1. ĐỊNH DẠNG BẢNG KHỐI LƯỢNG KHI ĐẶT VÀO SHEET: ";
            grpFormat.Location = new Point(20, 10);
            grpFormat.Size = new Size(520, 80);
            grpFormat.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            grpFormat.ForeColor = Color.FromArgb(24, 43, 73);

            radFormatExcel = new RadioButton();
            radFormatExcel.Text = "1. BẢNG KẺ KHUNG EXCEL CHUẨN 100% (Gồm Cả Ống & Phụ Kiện, STT, Dài m, Tự Cập Nhật)";
            radFormatExcel.Location = new Point(15, 22);
            radFormatExcel.Size = new Size(495, 24);
            radFormatExcel.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            radFormatExcel.ForeColor = Color.FromArgb(16, 124, 65); // Emerald Green
            var userSettings = MepUserSettings.Instance;
            radFormatExcel.Checked = (userSettings.SheetTableFormat == 0);

            radFormatSchedule = new RadioButton();
            radFormatSchedule.Checked = (userSettings.SheetTableFormat == 1);
            radFormatExcel.CheckedChanged += (s, e) => {
                if (radFormatExcel.Checked) { MepUserSettings.Instance.SheetTableFormat = 0; MepUserSettings.Instance.Save(); }
            };
            radFormatSchedule.CheckedChanged += (s, e) => {
                if (radFormatSchedule.Checked) { MepUserSettings.Instance.SheetTableFormat = 1; MepUserSettings.Instance.Save(); }
            };
            radFormatSchedule.Text = "2. Bảng ViewSchedule Revit (Giới hạn của Autodesk chỉ chứa 1 Category)";
            radFormatSchedule.Location = new Point(15, 48);
            radFormatSchedule.Size = new Size(495, 24);
            radFormatSchedule.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            radFormatSchedule.ForeColor = Color.FromArgb(100, 116, 139);

            grpFormat.Controls.Add(radFormatExcel);
            grpFormat.Controls.Add(radFormatSchedule);

            // ================= 2. CHỌN CÁCH ĐẶT BẢNG KHỐI LƯỢNG =================
            Label lblMode = new Label();
            lblMode.Text = "2. CHỌN SHEET HOẶC CÁCH ĐẶT BẢNG KHỐI LƯỢNG:";
            lblMode.Location = new Point(20, 98);
            lblMode.Size = new Size(520, 20);
            lblMode.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            lblMode.ForeColor = Color.FromArgb(24, 43, 73);

            radModeAutoSheet = new RadioButton();
            radModeAutoSheet.Text = "1. Đặt trực tiếp vào Sheet mong muốn (Chọn từ danh sách bên dưới):";
            radModeAutoSheet.Location = new Point(25, 122);
            radModeAutoSheet.Size = new Size(520, 24);
            radModeAutoSheet.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            radModeAutoSheet.ForeColor = Color.FromArgb(16, 124, 65); // Emerald Green
            radModeAutoSheet.Checked = true;

            cboSheets = new ComboBox();
            cboSheets.Location = new Point(48, 150);
            cboSheets.Size = new Size(490, 26);
            cboSheets.DropDownStyle = ComboBoxStyle.DropDownList;
            cboSheets.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
            cboSheets.Enabled = true;

            int selIdx = 0;
            for (int i = 0; i < _sheets.Count; i++)
            {
                ViewSheet s = _sheets[i];
                cboSheets.Items.Add(string.Format("{0} - {1}", s.SheetNumber, s.Name));
                if (defaultSheet != null && s.Id == defaultSheet.Id)
                {
                    selIdx = i;
                }
            }
            if (cboSheets.Items.Count > 0) cboSheets.SelectedIndex = selIdx;

            radModePickPoint = new RadioButton();
            radModePickPoint.Text = "2. Click chuột lên bản vẽ Sheet đang mở để chọn điểm đặt chính xác";
            radModePickPoint.Location = new Point(25, 182);
            radModePickPoint.Size = new Size(520, 24);
            radModePickPoint.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            radModePickPoint.ForeColor = Color.FromArgb(217, 83, 79); // Coral Red

            radModeDragDrop = new RadioButton();
            radModeDragDrop.Text = "3. Tạo vào Project Browser (Để nắm chuột Kéo Thả tự do vào Sheet)";
            radModeDragDrop.Location = new Point(25, 208);
            radModeDragDrop.Size = new Size(520, 24);
            radModeDragDrop.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
            radModeDragDrop.ForeColor = Color.FromArgb(13, 148, 136); // Teal

                        radModeAutoSheet.CheckedChanged += (s, e) => {
                cboSheets.Enabled = radModeAutoSheet.Checked;
                if (radModeAutoSheet.Checked) { MepUserSettings.Instance.SheetPlacementMode = 0; MepUserSettings.Instance.Save(); }
            };
            radModePickPoint.CheckedChanged += (s, e) => {
                cboSheets.Enabled = radModeAutoSheet.Checked;
                if (radModePickPoint.Checked) { MepUserSettings.Instance.SheetPlacementMode = 1; MepUserSettings.Instance.Save(); }
            };
            radModeDragDrop.CheckedChanged += (s, e) => {
                cboSheets.Enabled = radModeAutoSheet.Checked;
                if (radModeDragDrop.Checked) { MepUserSettings.Instance.SheetPlacementMode = 2; MepUserSettings.Instance.Save(); }
            };
            if (userSettings.SheetPlacementMode == 1) radModePickPoint.Checked = true;
            else if (userSettings.SheetPlacementMode == 2) radModeDragDrop.Checked = true;
            cboSheets.Enabled = radModeAutoSheet.Checked;

            cboSheets.SelectedIndexChanged += (s, e) => {
                if (cboSheets.SelectedIndex >= 0 && cboSheets.SelectedIndex < _sheets.Count) {
                    MepUserSettings.Instance.LastSelectedSheetNumber = _sheets[cboSheets.SelectedIndex].SheetNumber;
                    MepUserSettings.Instance.Save();
                }
            };

            // ================= 3. TÊN BẢNG SCHEDULE / VIEW =================
            Label lblName = new Label();
            lblName.Text = "3. TÊN BẢNG KHỐI LƯỢNG TẠO TRONG REVIT:";
            lblName.Location = new Point(20, 238);
            lblName.Size = new Size(520, 20);
            lblName.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            lblName.ForeColor = Color.FromArgb(24, 43, 73);

            txtScheduleName = new TextBox();
            txtScheduleName.Location = new Point(20, 262);
            txtScheduleName.Size = new Size(520, 24);
            txtScheduleName.Text = !string.IsNullOrEmpty(defaultScheduleName) ? defaultScheduleName : "<KHOI LUONG MEP - HDTOOL>";
            txtScheduleName.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            // ================= GỢI Ý & NÚT THỰC HIỆN =================
            Label lblNote = new Label();
            lblNote.Text = "💡 Bảng khối lượng đưa vào Sheet giữ NGUYÊN 100% các cột, hàng và thứ tự đúng như bạn đang nhìn thấy trên bảng (WYSIWYG)!";
            lblNote.Location = new Point(20, 296);
            lblNote.Size = new Size(520, 32);
            lblNote.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
            lblNote.ForeColor = Color.FromArgb(15, 118, 110);

            btnOk = new Button();
            btnOk.Text = "Thực Hiện & Đặt Vào Sheet";
            btnOk.Location = new Point(230, 335);
            btnOk.Size = new Size(195, 38);
            btnOk.BackColor = Color.FromArgb(16, 124, 65); // Green Accent
            btnOk.ForeColor = Color.White;
            btnOk.FlatStyle = FlatStyle.Flat;
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            btnOk.Cursor = Cursors.Hand;
            btnOk.Click += (s, e) => {
                ScheduleName = !string.IsNullOrWhiteSpace(txtScheduleName.Text) ? txtScheduleName.Text.Trim() : "<KHOI LUONG MEP>";
                TableFormat = radFormatExcel.Checked ? TableExportFormat.ExcelTableOnSheet : TableExportFormat.RevitViewSchedule;
                ColumnOptions = new ScheduleColumnVisibilityOptions();

                var dlgSettings = MepUserSettings.Instance;
                dlgSettings.SheetTableFormat = radFormatExcel.Checked ? 0 : 1;
                dlgSettings.LastScheduleName = ScheduleName;

                if (radModeDragDrop.Checked)
                {
                    PlacementMode = SheetPlacementMode.CreateInProjectBrowserOnly;
                    dlgSettings.SheetPlacementMode = 2;
                    dlgSettings.Save();
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else if (radModePickPoint.Checked)
                {
                    PlacementMode = SheetPlacementMode.PickPointOnActiveSheet;
                    dlgSettings.SheetPlacementMode = 1;
                    dlgSettings.Save();
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    PlacementMode = SheetPlacementMode.AutoPlaceOnSelectedSheet;
                    dlgSettings.SheetPlacementMode = 0;
                    if (cboSheets.SelectedIndex >= 0)
                    {
                        SelectedSheet = _sheets[cboSheets.SelectedIndex];
                        dlgSettings.LastSelectedSheetNumber = SelectedSheet.SheetNumber;
                        dlgSettings.Save();
                        this.DialogResult = DialogResult.OK;
                        this.Close();
                    }
                    else
                    {
                        MessageBox.Show("Vui lÃ²ng chá»n má»™t Sheet trong danh sÃ¡ch.", "ThÃ´ng BÃ¡o", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            };

            btnCancel = new Button();
            btnCancel.Text = "Hủy";
            btnCancel.Location = new Point(435, 335);
            btnCancel.Size = new Size(105, 38);
            btnCancel.BackColor = Color.FromArgb(226, 232, 240);
            btnCancel.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
            btnCancel.Click += (s, e) => {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };

            this.Controls.Add(grpFormat);
            this.Controls.Add(lblMode);
            this.Controls.Add(radModeAutoSheet);
            this.Controls.Add(cboSheets);
            this.Controls.Add(radModePickPoint);
            this.Controls.Add(radModeDragDrop);
            this.Controls.Add(lblName);
            this.Controls.Add(txtScheduleName);
            this.Controls.Add(lblNote);
            this.Controls.Add(btnOk);
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;

            this.ResumeLayout(false);
        }
    }

    /// <summary>
    /// Hộp thoại thêm cột Parameter dự án tùy ý vào bảng khối lượng
    /// </summary>
    public enum AddProjectParamAction
    {
        AddColumn,
        RemoveColumn,
        SyncSchedule,
        Cancel
    }

    /// <summary>
    /// Hộp thoại Quản Lý, Thêm & Loại Bỏ Parameter Dự Án theo lệnh Sếp Hồ
    /// Hỗ trợ:
    /// 1. Chỉ hiện tham số trong Bảng Khối Lượng Schedule Revit (Scheduled Fields)
    /// 2. Toàn bộ tham số khả dụng trong dự án Revit (79+ Available Fields)
    /// 3. Loại bỏ các parameter khi không dùng đến
    /// 4. Đồng bộ tự động 1:1 theo Schedule Revit
    /// </summary>
    public class AddProjectParameterDialog : System.Windows.Forms.Form
    {
        public AddProjectParamAction Action { get; private set; }
        public string SelectedParamName { get; private set; }
        public string SelectedRemoveColumn { get; private set; }

        private RadioButton radSchedOnly;
        private RadioButton radAllProject;
        private ComboBox cboParam;
        private ComboBox cboRemovable;
        private Button btnAddCol;
        private Button btnRemoveCol;
        private Button btnSyncSched;
        private Button btnCancel;

        private List<string> _schedParams;
        private List<string> _allParams;

        public AddProjectParameterDialog(List<string> schedParams, List<string> allParams, List<string> currentColumns, string scheduleName)
        {
            _schedParams = schedParams ?? new List<string>();
            _allParams = allParams ?? new List<string>();
            Action = AddProjectParamAction.Cancel;

            InitializeUi(currentColumns, scheduleName);
        }

        private void InitializeUi(List<string> currentColumns, string scheduleName)
        {
            this.SuspendLayout();

            this.Text = "HDTOOL - QUẢN LÝ & THÊM CỘT PARAMETER DỰ ÁN";
            this.Size = new Size(580, 420);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(248, 250, 252);
            this.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

            // Header Banner
            System.Windows.Forms.Panel pnlHead = new System.Windows.Forms.Panel();
            pnlHead.Dock = DockStyle.Top;
            pnlHead.Height = 58;
            pnlHead.BackColor = Color.FromArgb(24, 43, 73);

            Label lblHead = new Label();
            lblHead.Text = "QUẢN LÝ VÀ THÊM CỘT PARAMETER BẢNG KHỐI LƯỢNG";
            lblHead.Font = new Font("Segoe UI", 10.0f, FontStyle.Bold);
            lblHead.ForeColor = Color.White;
            lblHead.Location = new Point(16, 10);
            lblHead.AutoSize = true;

            Label lblSub = new Label();
            lblSub.Text = !string.IsNullOrEmpty(scheduleName)
                ? string.Format("Đang liên kết Bảng Schedule Revit: '{0}' ({1} tham số đang dùng)", scheduleName, _schedParams.Count)
                : "Thêm tham số từ Schedule Revit, toàn bộ dự án hoặc loại bỏ cột không dùng đến";
            lblSub.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblSub.ForeColor = Color.FromArgb(203, 213, 225);
            lblSub.Location = new Point(16, 32);
            lblSub.AutoSize = true;

            pnlHead.Controls.Add(lblHead);
            pnlHead.Controls.Add(lblSub);

            // 1. GROUPBOX THÊM PARAMETER
            GroupBox grpAdd = new GroupBox();
            grpAdd.Text = "1. Thêm Cột Parameter Vào Bảng";
            grpAdd.Location = new Point(16, 68);
            grpAdd.Size = new Size(532, 160);
            grpAdd.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            grpAdd.ForeColor = Color.FromArgb(30, 41, 59);

            radSchedOnly = new RadioButton();
            radSchedOnly.Text = string.Format("Chỉ hiện tham số trong Bảng Schedule Revit ({0} tham số)", _schedParams.Count);
            radSchedOnly.Location = new Point(16, 24);
            radSchedOnly.Size = new Size(490, 22);
            radSchedOnly.Checked = true;
            radSchedOnly.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            radSchedOnly.ForeColor = Color.FromArgb(2, 132, 199);
            radSchedOnly.CheckedChanged += (s, e) => PopulateCombo();

            radAllProject = new RadioButton();
            radAllProject.Text = string.Format("Toàn bộ tham số khả dụng trong dự án Revit ({0} tham số Available Fields)", _allParams.Count);
            radAllProject.Location = new Point(16, 48);
            radAllProject.Size = new Size(490, 22);
            radAllProject.Checked = false;
            radAllProject.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            radAllProject.ForeColor = Color.FromArgb(71, 85, 105);
            radAllProject.CheckedChanged += (s, e) => PopulateCombo();

            cboParam = new ComboBox();
            cboParam.Location = new Point(16, 76);
            cboParam.Size = new Size(495, 26);
            cboParam.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            cboParam.DropDownStyle = ComboBoxStyle.DropDown;
            cboParam.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            cboParam.AutoCompleteSource = AutoCompleteSource.ListItems;

            btnAddCol = new Button();
            btnAddCol.Text = "Thêm Cột Này Vào Bảng";
            btnAddCol.Location = new Point(16, 112);
            btnAddCol.Size = new Size(220, 34);
            btnAddCol.BackColor = Color.FromArgb(39, 174, 96); // Emerald Green
            btnAddCol.ForeColor = Color.White;
            btnAddCol.FlatStyle = FlatStyle.Flat;
            btnAddCol.FlatAppearance.BorderSize = 0;
            btnAddCol.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnAddCol.Cursor = Cursors.Hand;
            btnAddCol.Click += (s, e) => {
                string name = cboParam.Text != null ? cboParam.Text.Trim() : string.Empty;
                if (string.IsNullOrEmpty(name))
                {
                    MessageBox.Show("Vui lòng chọn hoặc nhập tên Parameter cần thêm!", "HDTOOL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                SelectedParamName = name;
                Action = AddProjectParamAction.AddColumn;
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            btnSyncSched = new Button();
            btnSyncSched.Text = "Đồng Bộ Khớp Schedule Revit";
            btnSyncSched.Location = new Point(246, 112);
            btnSyncSched.Size = new Size(265, 34);
            btnSyncSched.BackColor = Color.FromArgb(2, 132, 199); // Blue
            btnSyncSched.ForeColor = Color.White;
            btnSyncSched.FlatStyle = FlatStyle.Flat;
            btnSyncSched.FlatAppearance.BorderSize = 0;
            btnSyncSched.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnSyncSched.Cursor = Cursors.Hand;
            btnSyncSched.Click += (s, e) => {
                Action = AddProjectParamAction.SyncSchedule;
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            grpAdd.Controls.Add(radSchedOnly);
            grpAdd.Controls.Add(radAllProject);
            grpAdd.Controls.Add(cboParam);
            grpAdd.Controls.Add(btnAddCol);
            grpAdd.Controls.Add(btnSyncSched);

            // 2. GROUPBOX LOẠI BỎ PARAMETER KHI KHÔNG DÙNG ĐẾN
            GroupBox grpRemove = new GroupBox();
            grpRemove.Text = "2. Loại Bỏ Cột Parameter Khi Không Dùng Đến (Theo Lệnh Sếp Hồ)";
            grpRemove.Location = new Point(16, 238);
            grpRemove.Size = new Size(532, 95);
            grpRemove.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            grpRemove.ForeColor = Color.FromArgb(30, 41, 59);

            cboRemovable = new ComboBox();
            cboRemovable.Location = new Point(16, 32);
            cboRemovable.Size = new Size(280, 26);
            cboRemovable.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
            cboRemovable.DropDownStyle = ComboBoxStyle.DropDownList;

            if (currentColumns != null && currentColumns.Count > 0)
            {
                foreach (string col in currentColumns) cboRemovable.Items.Add(col);
                if (cboRemovable.Items.Count > 0) cboRemovable.SelectedIndex = 0;
            }

            btnRemoveCol = new Button();
            btnRemoveCol.Text = "Loại Bỏ Cột Này Khỏi Bảng";
            btnRemoveCol.Location = new Point(306, 28);
            btnRemoveCol.Size = new Size(205, 34);
            btnRemoveCol.BackColor = Color.FromArgb(220, 38, 38); // Red
            btnRemoveCol.ForeColor = Color.White;
            btnRemoveCol.FlatStyle = FlatStyle.Flat;
            btnRemoveCol.FlatAppearance.BorderSize = 0;
            btnRemoveCol.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnRemoveCol.Cursor = Cursors.Hand;
            btnRemoveCol.Click += (s, e) => {
                if (cboRemovable.SelectedItem == null)
                {
                    MessageBox.Show("Vui lòng chọn cột cần loại bỏ!", "HDTOOL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                SelectedRemoveColumn = cboRemovable.SelectedItem.ToString();
                Action = AddProjectParamAction.RemoveColumn;
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            grpRemove.Controls.Add(cboRemovable);
            grpRemove.Controls.Add(btnRemoveCol);

            // Bottom Close Button
            btnCancel = new Button();
            btnCancel.Text = "Đóng (ESC)";
            btnCancel.Location = new Point(428, 342);
            btnCancel.Size = new Size(120, 32);
            btnCancel.BackColor = Color.FromArgb(226, 232, 240);
            btnCancel.ForeColor = Color.FromArgb(51, 65, 85);
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.FlatAppearance.BorderSize = 1;
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            btnCancel.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnCancel.DialogResult = DialogResult.Cancel;
            btnCancel.Click += (s, e) => { this.Close(); };

            this.Controls.Add(btnCancel);
            this.Controls.Add(grpRemove);
            this.Controls.Add(grpAdd);
            this.Controls.Add(pnlHead);

            PopulateCombo();

            this.CancelButton = btnCancel;
            this.ResumeLayout(false);
        }

        private void PopulateCombo()
        {
            if (cboParam == null) return;
            cboParam.Items.Clear();

            List<string> listToUse = (radSchedOnly != null && radSchedOnly.Checked && _schedParams.Count > 0)
                ? _schedParams
                : _allParams;

            foreach (string p in listToUse)
            {
                cboParam.Items.Add(p);
            }

            if (cboParam.Items.Count > 0)
            {
                cboParam.SelectedIndex = 0;
            }
        }
    }

    public class BatchAssignParamDialog : System.Windows.Forms.Form
    {
        public string SelectedParam { get; private set; }
        public string NewValue { get; private set; }

        private ComboBox cboParam;
        private TextBox txtValue;
        private Button btnOk;
        private Button btnCancel;

        public BatchAssignParamDialog(List<string> availableParams, int rowCount)
        {
            InitializeComponent(availableParams, rowCount);
        }

        private void InitializeComponent(List<string> availableParams, int rowCount)
        {
            this.SuspendLayout();

            this.Text = "HDTOOL - GÁN GIÁ TRỊ PARAMETER HÀNG LOẠT";
            this.Size = new Size(500, 290);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(248, 250, 252);
            this.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

            // Header Banner
            System.Windows.Forms.Panel pnlHead = new System.Windows.Forms.Panel();
            pnlHead.Dock = DockStyle.Top;
            pnlHead.Height = 55;
            pnlHead.BackColor = Color.FromArgb(24, 43, 73);

            Label lblHead = new Label();
            lblHead.Text = "GÁN GIÁ TRỊ PARAMETER CHO CÁC DÒNG ĐÃ CHỌN";
            lblHead.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            lblHead.ForeColor = Color.White;
            lblHead.Location = new Point(16, 10);
            lblHead.AutoSize = true;

            Label lblSub = new Label();
            lblSub.Text = string.Format("Đang áp dụng đồng thời cho {0} dòng được quét chọn trong bảng:", rowCount);
            lblSub.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblSub.ForeColor = Color.FromArgb(125, 211, 252); // Sky-300
            lblSub.Location = new Point(16, 30);
            lblSub.AutoSize = true;

            pnlHead.Controls.Add(lblHead);
            pnlHead.Controls.Add(lblSub);

            // Chọn Parameter
            Label lblParam = new Label();
            lblParam.Text = "1. Chọn Parameter cần gán giá trị:";
            lblParam.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            lblParam.ForeColor = Color.FromArgb(30, 41, 59);
            lblParam.Location = new Point(20, 70);
            lblParam.Size = new Size(440, 20);

            cboParam = new ComboBox();
            cboParam.Location = new Point(20, 92);
            cboParam.Size = new Size(440, 25);
            cboParam.DropDownStyle = ComboBoxStyle.DropDownList;
            cboParam.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

            if (availableParams != null)
            {
                foreach (string p in availableParams) cboParam.Items.Add(p);
                if (cboParam.Items.Count > 0) cboParam.SelectedIndex = 0;
            }

            // Nhập Giá trị mới
            Label lblVal = new Label();
            lblVal.Text = "2. Giá trị mới cần gán (Ví dụ: MƯA, GOM T3, Tầng 13...):";
            lblVal.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            lblVal.ForeColor = Color.FromArgb(30, 41, 59);
            lblVal.Location = new Point(20, 128);
            lblVal.Size = new Size(440, 20);

            txtValue = new TextBox();
            txtValue.Location = new Point(20, 150);
            txtValue.Size = new Size(440, 25);
            txtValue.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            // Buttons
            btnOk = new Button();
            btnOk.Text = "Áp Dụng Ngay";
            btnOk.Location = new Point(180, 195);
            btnOk.Size = new Size(160, 36);
            btnOk.BackColor = Color.FromArgb(39, 174, 96);
            btnOk.ForeColor = Color.White;
            btnOk.FlatStyle = FlatStyle.Flat;
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnOk.Cursor = Cursors.Hand;
            btnOk.Click += (s, e) => {
                SelectedParam = cboParam.Text != null ? cboParam.Text.Trim() : string.Empty;
                NewValue = txtValue.Text != null ? txtValue.Text.Trim() : string.Empty;
                if (string.IsNullOrEmpty(SelectedParam))
                {
                    MessageBox.Show("Vui lòng chọn Parameter cần gán!", "HDTOOL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            btnCancel = new Button();
            btnCancel.Text = "Hủy (ESC)";
            btnCancel.Location = new Point(350, 195);
            btnCancel.Size = new Size(110, 36);
            btnCancel.BackColor = Color.FromArgb(226, 232, 240);
            btnCancel.ForeColor = Color.FromArgb(51, 65, 85);
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.FlatAppearance.BorderSize = 1;
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            btnCancel.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
            btnCancel.Click += (s, e) => {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };

            this.Controls.Add(pnlHead);
            this.Controls.Add(lblParam);
            this.Controls.Add(cboParam);
            this.Controls.Add(lblVal);
            this.Controls.Add(txtValue);
            this.Controls.Add(btnOk);
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;

            this.ResumeLayout(false);
        }
    }
}
