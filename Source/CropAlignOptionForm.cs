using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Color = System.Drawing.Color;
using Point = System.Drawing.Point;

namespace HDTool
{
    /// <summary>
    /// Bảng điều khiển tích hợp 2-trong-1: Căn Chỉnh Lưới Trục (Grids) và Cao Độ (Level) trên cùng 1 giao diện Tab
    /// </summary>
    public class CropAlignOptionForm : HDToolBaseForm
    {
        private UIDocument _uidoc;
        private Document _doc;
        private Autodesk.Revit.DB.View _activeView;
        private bool _isSheet;

        // View context data
        private List<Grid> _allGrids;
        private List<Level> _allLevels;
        private List<Autodesk.Revit.DB.View> _sheetPlanViews;
        private List<Autodesk.Revit.DB.View> _sheetSectionViews;

        // Header controls
        private System.Windows.Forms.Panel pnlHeader;
        private Label lblTitle;
        private Label lblSubtitle;
        private PictureBox picLogo;

        // Info box
        private GroupBox grpInfo;
        private Label lblCurrentView;

        // TabControl
        private TabControl tabControl;
        private TabPage tabGrids;
                private TabPage tabLevel;
        private TabPage tabAutoCrop;

        // AutoCrop Controls
        private GroupBox grpCropInfo;
        private Label lblCropViewName;
        private Label lblCropElemCount;
        private GroupBox grpCropPadding;
        private NumericUpDown numCropPadding;
        private TrackBar trackCropPadding;
        private CheckBox chkCropVisible;
        private CheckBox chkCropSectionBox;
        private CheckBox chkCropMepOnly;
        private Button btnCropAuto;
        private Button btnCropPick;

        // --- Grids Controls ---
        private GroupBox grpGridInputs;
        private Label lblGridTop;
        private NumericUpDown numGridTop;
        private Label lblGridBottom;
        private NumericUpDown numGridBottom;
        private Label lblGridLeft;
        private NumericUpDown numGridLeft;
        private Label lblGridRight;
        private NumericUpDown numGridRight;
        private CheckBox chkGridEqual;

        private GroupBox grpGridSymbols;
        private CheckBox chkGridSymTop;
        private CheckBox chkGridSymBottom;
        private CheckBox chkGridSymLeft;
        private CheckBox chkGridSymRight;
        private CheckBox chkGrid2D;

        private Button btnGridAuto;
        private Button btnGridPick;

        // --- Level Controls ---
        private GroupBox grpLevelViews;
        private CheckedListBox lstLevelViews;
        private Label lblLevelViewsSummary;
        private Button btnSelectAllLevelViews;
        private Button btnDeselectAllLevelViews;

        private GroupBox grpLevelInputs;
        private Label lblLevelLeft;
        private NumericUpDown numLevelLeft;
        private Label lblLevelRight;
        private NumericUpDown numLevelRight;
        private CheckBox chkLevelEqual;

        private GroupBox grpLevelSymbols;
        private CheckBox chkLevelSymLeft;
        private CheckBox chkLevelSymRight;
        private CheckBox chkLevel2D;

        private Button btnLevelAuto;
        private Button btnLevelPick;

        // Bottom Controls
        private Label lblStatus;
        private Button btnClose;
        private System.Windows.Forms.ToolTip toolTip;

        public CropAlignOptionForm(UIDocument uidoc)
        {
            _uidoc = uidoc;
            _doc = uidoc.Document;
            _activeView = uidoc.ActiveView;
            _isSheet = (_activeView is ViewSheet);

            LoadContextData();
            InitializeComponent();
        }

        public void UpdateContext(UIDocument uidoc)
        {
            if (uidoc == null) return;
            _uidoc = uidoc;
            _doc = uidoc.Document;
            _activeView = uidoc.ActiveView;
            _isSheet = (_activeView is ViewSheet);
            LoadContextData();
            try
            {
                if (lblCurrentView != null && _activeView != null)
                {
                    lblCurrentView.Text = string.Format("View: {0} ({1})  |  Tỷ lệ: 1:{2}  |  Grids: {3}  |  Levels: {4}",
                        _activeView.Name, _activeView.ViewType.ToString(), _activeView.Scale, _allGrids.Count, _allLevels.Count);
                }
            }
            catch { }
        }

        private void LoadContextData()
        {
            _allGrids = new List<Grid>();
            _allLevels = new List<Level>();
            _sheetPlanViews = new List<Autodesk.Revit.DB.View>();
            _sheetSectionViews = new List<Autodesk.Revit.DB.View>();

            try
            {
                if (_isSheet)
                {
                    _sheetPlanViews = GridCropService.GetApplicablePlanViews(_uidoc);
                    _sheetSectionViews = LevelCropService.GetApplicableViewsInContext(_uidoc);

                    foreach (Autodesk.Revit.DB.View v in _sheetPlanViews)
                    {
                        List<Grid> gList = GridCropService.GetAllGridsInView(_doc, v);
                        foreach (Grid g in gList)
                        {
                            if (!_allGrids.Any(x => x.Id == g.Id)) _allGrids.Add(g);
                        }
                    }

                    foreach (Autodesk.Revit.DB.View v in _sheetSectionViews)
                    {
                        List<Level> lList = LevelCropService.GetAllLevelsInView(_doc, v);
                        foreach (Level l in lList)
                        {
                            if (!_allLevels.Any(x => x.Id == l.Id)) _allLevels.Add(l);
                        }
                    }
                }
                else
                {
                    _allGrids = GridCropService.GetAllGridsInView(_doc, _activeView);
                    _allLevels = LevelCropService.GetAllLevelsInView(_doc, _activeView);
                    if (LevelCropService.IsViewApplicable(_activeView))
                    {
                        _sheetSectionViews.Add(_activeView);
                    }
                }
            }
            catch { }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.Text = "HDTOOL - Căn Chỉnh Bản Vẽ (Grids & Level 2-trong-1)";
            this.ClientSize = new Size(540, 680);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.TopMost = true;
            this.BackColor = Color.FromArgb(245, 247, 250);
            this.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

            toolTip = new System.Windows.Forms.ToolTip();

            // 1. Header
            pnlHeader = new System.Windows.Forms.Panel();
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = 70;
            pnlHeader.BackColor = Color.FromArgb(24, 43, 73);

            lblTitle = new Label();
            lblTitle.Text = "HDTOOL - CĂN CHỈNH BẢN VẼ REVIT";
            lblTitle.ForeColor = Color.White;
            lblTitle.Font = new Font("Segoe UI", 11.5f, FontStyle.Bold);
            lblTitle.Location = new Point(14, 12);
            lblTitle.AutoSize = true;

            lblSubtitle = new Label();
            lblSubtitle.Text = "Bảng điều khiển gộp: Căn chỉnh Lưới trục (Grids) & Cao độ (Level) (mm)";
            lblSubtitle.ForeColor = Color.FromArgb(189, 195, 199);
            lblSubtitle.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblSubtitle.Location = new Point(14, 40);
            lblSubtitle.AutoSize = true;

            picLogo = new PictureBox();
            picLogo.Size = new Size(120, 38);
            picLogo.Location = new Point(405, 16);
            picLogo.SizeMode = PictureBoxSizeMode.Zoom;
            picLogo.BackColor = Color.White;
            try
            {
                using (Stream stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("hadomee_logo.png"))
                {
                    if (stream != null)
                    {
                        picLogo.Image = Image.FromStream(stream);
                    }
                }
            }
            catch { }

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);
            pnlHeader.Controls.Add(picLogo);

            // 2. Info Box
            grpInfo = new GroupBox();
            grpInfo.Text = "Khung Nhìn / Trang In (Sheet) Đang Mở";
            grpInfo.Location = new Point(12, 76);
            grpInfo.Size = new Size(516, 54);
            grpInfo.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            grpInfo.ForeColor = Color.FromArgb(44, 62, 80);

            lblCurrentView = new Label();
            lblCurrentView.Location = new Point(10, 20);
            lblCurrentView.Size = new Size(496, 26);
            lblCurrentView.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblCurrentView.ForeColor = Color.FromArgb(52, 73, 94);

            if (_activeView != null)
            {
                lblCurrentView.Text = string.Format("View: {0} ({1})  |  Tỷ lệ: 1:{2}  |  Grids: {3}  |  Levels: {4}",
                    _activeView.Name, _activeView.ViewType.ToString(), _activeView.Scale, _allGrids.Count, _allLevels.Count);
            }
            grpInfo.Controls.Add(lblCurrentView);

            // 3. TabControl
            tabControl = new TabControl();
            tabControl.Location = new Point(12, 136);
            tabControl.Size = new Size(516, 455);
            tabControl.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);

            // Build Tab 1: Grids
            BuildTabGrids();

            // Build Tab 2: Level
            BuildTabLevel();

            BuildTabAutoCrop();

            tabControl.TabPages.Add(tabGrids);
            tabControl.TabPages.Add(tabLevel);
            tabControl.TabPages.Add(tabAutoCrop);

            // Auto-select tab based on view
            if (_activeView is View3D)
            {
                tabControl.SelectedTab = tabAutoCrop;
            }
            else if (LevelCropService.IsViewApplicable(_activeView))
            {
                tabControl.SelectedTab = tabLevel;
            }
            else if (_isSheet && _allGrids.Count == 0 && _allLevels.Count > 0)
            {
                tabControl.SelectedTab = tabLevel;
            }
            else
            {
                tabControl.SelectedTab = tabGrids;
            }

            // 4. Status Bar & Close Button
            lblStatus = new Label();
            lblStatus.Location = new Point(14, 598);
            lblStatus.Size = new Size(512, 24);
            lblStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            lblStatus.ForeColor = Color.FromArgb(39, 174, 96);
            lblStatus.Text = "Sẵn sàng căn chỉnh. Bạn có thể thay đổi số mm và bấm áp dụng liên tục.";

            btnClose = new Button();
            btnClose.Text = "Đóng Bảng (ESC)";
            btnClose.Location = new Point(12, 628);
            btnClose.Size = new Size(516, 36);
            btnClose.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
            btnClose.BackColor = Color.FromArgb(236, 240, 241);
            btnClose.Click += (s, e) => this.Close();
            this.CancelButton = btnClose;

            this.Controls.Add(pnlHeader);
            this.Controls.Add(grpInfo);
            this.Controls.Add(tabControl);
            this.Controls.Add(lblStatus);
            this.Controls.Add(btnClose);

            this.ResumeLayout(false);
        }

        private void BuildTabGrids()
        {
            tabGrids = new TabPage();
            tabGrids.Text = " 📐  CĂN CHỈNH LƯỚI TRỤC (GRIDS) ";
            tabGrids.BackColor = Color.FromArgb(250, 252, 254);
            tabGrids.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

            // 1. Group Inputs
            grpGridInputs = new GroupBox();
            grpGridInputs.Text = "Khoảng Cách 4 Hướng Nhô Ra So Với Khung Crop (mm)";
            grpGridInputs.Location = new Point(12, 12);
            grpGridInputs.Size = new Size(484, 155);
            grpGridInputs.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            grpGridInputs.ForeColor = Color.FromArgb(44, 62, 80);

            // Top
            lblGridTop = new Label();
            lblGridTop.Text = "Phía Trên (Top):";
            lblGridTop.Location = new Point(15, 25);
            lblGridTop.Size = new Size(130, 22);
            lblGridTop.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

            numGridTop = new NumericUpDown();
            numGridTop.Location = new Point(150, 23);
            numGridTop.Size = new Size(90, 23);
            numGridTop.Minimum = -50000;
            numGridTop.Maximum = 100000;
            numGridTop.Value = 1500;
            numGridTop.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            numGridTop.ValueChanged += (s, e) => {
                if (chkGridEqual.Checked)
                {
                    numGridBottom.Value = numGridTop.Value;
                    numGridLeft.Value = numGridTop.Value;
                    numGridRight.Value = numGridTop.Value;
                }
            };

            // Bottom
            lblGridBottom = new Label();
            lblGridBottom.Text = "Phía Dưới (Bottom):";
            lblGridBottom.Location = new Point(15, 54);
            lblGridBottom.Size = new Size(130, 22);
            lblGridBottom.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

            numGridBottom = new NumericUpDown();
            numGridBottom.Location = new Point(150, 52);
            numGridBottom.Size = new Size(90, 23);
            numGridBottom.Minimum = -50000;
            numGridBottom.Maximum = 100000;
            numGridBottom.Value = 1500;
            numGridBottom.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);

            // Left
            lblGridLeft = new Label();
            lblGridLeft.Text = "Bên Trái (Left):";
            lblGridLeft.Location = new Point(255, 25);
            lblGridLeft.Size = new Size(110, 22);
            lblGridLeft.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

            numGridLeft = new NumericUpDown();
            numGridLeft.Location = new Point(370, 23);
            numGridLeft.Size = new Size(90, 23);
            numGridLeft.Minimum = -50000;
            numGridLeft.Maximum = 100000;
            numGridLeft.Value = 1500;
            numGridLeft.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);

            // Right
            lblGridRight = new Label();
            lblGridRight.Text = "Bên Phải (Right):";
            lblGridRight.Location = new Point(255, 54);
            lblGridRight.Size = new Size(110, 22);
            lblGridRight.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

            numGridRight = new NumericUpDown();
            numGridRight.Location = new Point(370, 52);
            numGridRight.Size = new Size(90, 23);
            numGridRight.Minimum = -50000;
            numGridRight.Maximum = 100000;
            numGridRight.Value = 1500;
            numGridRight.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);

            // Checkbox Equal
            chkGridEqual = new CheckBox();
            chkGridEqual.Text = "Khóa cả 4 hướng bằng nhau (Top = Bottom = Left = Right)";
            chkGridEqual.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            chkGridEqual.ForeColor = Color.FromArgb(39, 174, 96);
            chkGridEqual.Location = new Point(15, 85);
            chkGridEqual.Size = new Size(450, 22);
            chkGridEqual.Checked = true;
            chkGridEqual.CheckedChanged += (s, e) => {
                bool eq = chkGridEqual.Checked;
                numGridBottom.Enabled = !eq;
                numGridLeft.Enabled = !eq;
                numGridRight.Enabled = !eq;
                if (eq)
                {
                    numGridBottom.Value = numGridTop.Value;
                    numGridLeft.Value = numGridTop.Value;
                    numGridRight.Value = numGridTop.Value;
                }
            };
            numGridBottom.Enabled = false;
            numGridLeft.Enabled = false;
            numGridRight.Enabled = false;

            // 2D checkbox
            chkGrid2D = new CheckBox();
            chkGrid2D.Text = "Chỉ thay đổi trong khung nhìn này (2D View-Specific - Khuyên dùng)";
            chkGrid2D.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            chkGrid2D.ForeColor = Color.FromArgb(52, 73, 94);
            chkGrid2D.Location = new Point(15, 115);
            chkGrid2D.Size = new Size(450, 22);
            chkGrid2D.Checked = true;

            grpGridInputs.Controls.Add(lblGridTop);
            grpGridInputs.Controls.Add(numGridTop);
            grpGridInputs.Controls.Add(lblGridBottom);
            grpGridInputs.Controls.Add(numGridBottom);
            grpGridInputs.Controls.Add(lblGridLeft);
            grpGridInputs.Controls.Add(numGridLeft);
            grpGridInputs.Controls.Add(lblGridRight);
            grpGridInputs.Controls.Add(numGridRight);
            grpGridInputs.Controls.Add(chkGridEqual);
            grpGridInputs.Controls.Add(chkGrid2D);

            // 2. Group Symbols
            grpGridSymbols = new GroupBox();
            grpGridSymbols.Text = "Tùy Chọn Bật / Tắt Bong Bóng Tên Trục (Symbols 4 Hướng)";
            grpGridSymbols.Location = new Point(12, 175);
            grpGridSymbols.Size = new Size(484, 80);
            grpGridSymbols.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            grpGridSymbols.ForeColor = Color.FromArgb(44, 62, 80);

            chkGridSymTop = new CheckBox();
            chkGridSymTop.Text = "Trên (Top)";
            chkGridSymTop.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
            chkGridSymTop.Location = new Point(15, 25);
            chkGridSymTop.Size = new Size(100, 22);
            chkGridSymTop.Checked = true;

            chkGridSymBottom = new CheckBox();
            chkGridSymBottom.Text = "Dưới (Bottom)";
            chkGridSymBottom.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
            chkGridSymBottom.Location = new Point(125, 25);
            chkGridSymBottom.Size = new Size(110, 22);
            chkGridSymBottom.Checked = true;

            chkGridSymLeft = new CheckBox();
            chkGridSymLeft.Text = "Trái (Left)";
            chkGridSymLeft.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
            chkGridSymLeft.Location = new Point(245, 25);
            chkGridSymLeft.Size = new Size(100, 22);
            chkGridSymLeft.Checked = true;

            chkGridSymRight = new CheckBox();
            chkGridSymRight.Text = "Phải (Right)";
            chkGridSymRight.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
            chkGridSymRight.Location = new Point(355, 25);
            chkGridSymRight.Size = new Size(100, 22);
            chkGridSymRight.Checked = true;

            Label lblSymTip = new Label();
            lblSymTip.Text = "(Tích để hiện bong bóng tên trục; bỏ tích để chỉ vẽ đường tim trục)";
            lblSymTip.Font = new Font("Segoe UI", 8.0f, FontStyle.Italic);
            lblSymTip.ForeColor = Color.Gray;
            lblSymTip.Location = new Point(15, 52);
            lblSymTip.Size = new Size(450, 18);

            grpGridSymbols.Controls.Add(chkGridSymTop);
            grpGridSymbols.Controls.Add(chkGridSymBottom);
            grpGridSymbols.Controls.Add(chkGridSymLeft);
            grpGridSymbols.Controls.Add(chkGridSymRight);
            grpGridSymbols.Controls.Add(lblSymTip);

            // 3. Action Buttons
            btnGridAuto = new Button();
            btnGridAuto.Location = new Point(12, 268);
            btnGridAuto.Size = new Size(484, 60);
            btnGridAuto.BackColor = Color.FromArgb(39, 174, 96);
            btnGridAuto.ForeColor = Color.White;
            btnGridAuto.FlatStyle = FlatStyle.Flat;
            btnGridAuto.FlatAppearance.BorderSize = 0;
            btnGridAuto.Cursor = Cursors.Hand;
            btnGridAuto.Font = new Font("Segoe UI", 10.0f, FontStyle.Bold);
            if (_isSheet)
            {
                btnGridAuto.Text = string.Format("⚡  TỰ ĐỘNG CĂN CHỈNH TẤT CẢ GRIDS TRÊN SHEET ({0} VIEW)", _sheetPlanViews.Count);
            }
            else
            {
                btnGridAuto.Text = "⚡  TỰ ĐỘNG CĂN CHỈNH TẤT CẢ GRIDS THEO CROP VIEW";
            }
            btnGridAuto.Click += BtnGridAuto_Click;

            btnGridPick = new Button();
            btnGridPick.Location = new Point(12, 336);
            btnGridPick.Size = new Size(484, 45);
            btnGridPick.BackColor = Color.FromArgb(52, 73, 94);
            btnGridPick.ForeColor = Color.White;
            btnGridPick.FlatStyle = FlatStyle.Flat;
            btnGridPick.FlatAppearance.BorderSize = 0;
            btnGridPick.Cursor = Cursors.Hand;
            btnGridPick.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnGridPick.Text = "🎯  CLICK / QUÉT CHỌN GRIDS HOẶC CROP TRÊN MÀN HÌNH (PICK)";
            btnGridPick.Click += BtnGridPick_Click;

            tabGrids.Controls.Add(grpGridInputs);
            tabGrids.Controls.Add(grpGridSymbols);
            tabGrids.Controls.Add(btnGridAuto);
            tabGrids.Controls.Add(btnGridPick);
        }

        private void BuildTabLevel()
        {
            tabLevel = new TabPage();
            tabLevel.Text = " 📏  CĂN CHỈNH CAO ĐỘ (LEVEL) ";
            tabLevel.BackColor = Color.FromArgb(250, 252, 254);
            tabLevel.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

            // 1. View Selection (If on Sheet)
            grpLevelViews = new GroupBox();
            grpLevelViews.Location = new Point(12, 12);
            grpLevelViews.Size = new Size(484, 115);
            grpLevelViews.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            grpLevelViews.ForeColor = Color.FromArgb(44, 62, 80);

            if (_isSheet && _sheetSectionViews.Count > 0)
            {
                grpLevelViews.Text = string.Format("Phát Hiện {0} Khung Nhìn Mặt Cắt / Đứng Trên Sheet", _sheetSectionViews.Count);
                lstLevelViews = new CheckedListBox();
                lstLevelViews.Location = new Point(15, 22);
                lstLevelViews.Size = new Size(454, 55);
                lstLevelViews.CheckOnClick = true;
                lstLevelViews.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);

                foreach (Autodesk.Revit.DB.View v in _sheetSectionViews)
                {
                    List<Level> lvls = LevelCropService.GetAllLevelsInView(_doc, v);
                    lstLevelViews.Items.Add(string.Format("{0} [{1}] - ({2} Level)", v.Name, v.ViewType.ToString(), lvls.Count), true);
                }

                btnSelectAllLevelViews = new Button();
                btnSelectAllLevelViews.Text = "Chọn Hết";
                btnSelectAllLevelViews.Location = new Point(15, 82);
                btnSelectAllLevelViews.Size = new Size(80, 24);
                btnSelectAllLevelViews.Font = new Font("Segoe UI", 8.0f, FontStyle.Regular);
                btnSelectAllLevelViews.Click += (s, e) => {
                    for (int i = 0; i < lstLevelViews.Items.Count; i++) lstLevelViews.SetItemChecked(i, true);
                };

                btnDeselectAllLevelViews = new Button();
                btnDeselectAllLevelViews.Text = "Bỏ Chọn";
                btnDeselectAllLevelViews.Location = new Point(105, 82);
                btnDeselectAllLevelViews.Size = new Size(80, 24);
                btnDeselectAllLevelViews.Font = new Font("Segoe UI", 8.0f, FontStyle.Regular);
                btnDeselectAllLevelViews.Click += (s, e) => {
                    for (int i = 0; i < lstLevelViews.Items.Count; i++) lstLevelViews.SetItemChecked(i, false);
                };

                grpLevelViews.Controls.Add(lstLevelViews);
                grpLevelViews.Controls.Add(btnSelectAllLevelViews);
                grpLevelViews.Controls.Add(btnDeselectAllLevelViews);
            }
            else
            {
                grpLevelViews.Text = "Khung Nhìn Mặt Cắt / Mặt Đứng Hiện Tại";
                lblLevelViewsSummary = new Label();
                lblLevelViewsSummary.Location = new Point(15, 30);
                lblLevelViewsSummary.Size = new Size(454, 60);
                lblLevelViewsSummary.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
                lblLevelViewsSummary.ForeColor = Color.FromArgb(52, 73, 94);
                lblLevelViewsSummary.Text = string.Format("Đang áp dụng cho View: {0} ({1})\r\n" +
                    "Số lượng Cao độ (Level) tìm thấy trong View: {2} Level.\r\n" +
                    "Đầu trái thò ra không Bubble, đầu phải thò ra kèm Bubble tên & cao độ tầng.",
                    _activeView.Name, _activeView.ViewType.ToString(), _allLevels.Count);
                grpLevelViews.Controls.Add(lblLevelViewsSummary);
            }

            // 2. Group Inputs
            grpLevelInputs = new GroupBox();
            grpLevelInputs.Text = "Khoảng Cách Hai Đầu Level Nhô Ra So Với Khung Crop (mm)";
            grpLevelInputs.Location = new Point(12, 135);
            grpLevelInputs.Size = new Size(484, 115);
            grpLevelInputs.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            grpLevelInputs.ForeColor = Color.FromArgb(44, 62, 80);

            lblLevelLeft = new Label();
            lblLevelLeft.Text = "Bên Trái (Left - Đầu mút):";
            lblLevelLeft.Location = new Point(15, 25);
            lblLevelLeft.Size = new Size(160, 22);
            lblLevelLeft.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

            numLevelLeft = new NumericUpDown();
            numLevelLeft.Location = new Point(180, 23);
            numLevelLeft.Size = new Size(90, 23);
            numLevelLeft.Minimum = -50000;
            numLevelLeft.Maximum = 100000;
            numLevelLeft.Value = 500;
            numLevelLeft.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            numLevelLeft.ValueChanged += (s, e) => {
                if (chkLevelEqual.Checked) numLevelRight.Value = numLevelLeft.Value;
            };

            lblLevelRight = new Label();
            lblLevelRight.Text = "Bên Phải (Right - Bubble):";
            lblLevelRight.Location = new Point(15, 54);
            lblLevelRight.Size = new Size(160, 22);
            lblLevelRight.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

            numLevelRight = new NumericUpDown();
            numLevelRight.Location = new Point(180, 52);
            numLevelRight.Size = new Size(90, 23);
            numLevelRight.Minimum = -50000;
            numLevelRight.Maximum = 100000;
            numLevelRight.Value = 1500;
            numLevelRight.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);

            chkLevelEqual = new CheckBox();
            chkLevelEqual.Text = "Khóa 2 bên bằng nhau (Left = Right)";
            chkLevelEqual.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            chkLevelEqual.ForeColor = Color.FromArgb(41, 128, 185);
            chkLevelEqual.Location = new Point(15, 83);
            chkLevelEqual.Size = new Size(260, 22);
            chkLevelEqual.CheckedChanged += (s, e) => {
                numLevelRight.Enabled = !chkLevelEqual.Checked;
                if (chkLevelEqual.Checked) numLevelRight.Value = numLevelLeft.Value;
            };

            chkLevel2D = new CheckBox();
            chkLevel2D.Text = "Chỉ thay đổi trong View này (2D)";
            chkLevel2D.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            chkLevel2D.ForeColor = Color.FromArgb(52, 73, 94);
            chkLevel2D.Location = new Point(285, 83);
            chkLevel2D.Size = new Size(190, 22);
            chkLevel2D.Checked = true;

            grpLevelInputs.Controls.Add(lblLevelLeft);
            grpLevelInputs.Controls.Add(numLevelLeft);
            grpLevelInputs.Controls.Add(lblLevelRight);
            grpLevelInputs.Controls.Add(numLevelRight);
            grpLevelInputs.Controls.Add(chkLevelEqual);
            grpLevelInputs.Controls.Add(chkLevel2D);

            // 3. Group Symbols
            grpLevelSymbols = new GroupBox();
            grpLevelSymbols.Text = "Bật / Tắt Ký Hiệu Đầu Tầng (Level Symbols)";
            grpLevelSymbols.Location = new Point(12, 258);
            grpLevelSymbols.Size = new Size(484, 55);
            grpLevelSymbols.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            grpLevelSymbols.ForeColor = Color.FromArgb(44, 62, 80);

            chkLevelSymLeft = new CheckBox();
            chkLevelSymLeft.Text = "Bong bóng Bên Trái (Left)";
            chkLevelSymLeft.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
            chkLevelSymLeft.Location = new Point(15, 22);
            chkLevelSymLeft.Size = new Size(180, 22);
            chkLevelSymLeft.Checked = false; // Chuẩn kỹ thuật: Tắt bên trái

            chkLevelSymRight = new CheckBox();
            chkLevelSymRight.Text = "Bong bóng Bên Phải (Right - Chuẩn)";
            chkLevelSymRight.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
            chkLevelSymRight.Location = new Point(220, 22);
            chkLevelSymRight.Size = new Size(240, 22);
            chkLevelSymRight.Checked = true; // Chuẩn kỹ thuật: Bật bên phải

            grpLevelSymbols.Controls.Add(chkLevelSymLeft);
            grpLevelSymbols.Controls.Add(chkLevelSymRight);

            // 4. Action Buttons
            btnLevelAuto = new Button();
            btnLevelAuto.Location = new Point(12, 321);
            btnLevelAuto.Size = new Size(484, 52);
            btnLevelAuto.BackColor = Color.FromArgb(41, 128, 185);
            btnLevelAuto.ForeColor = Color.White;
            btnLevelAuto.FlatStyle = FlatStyle.Flat;
            btnLevelAuto.FlatAppearance.BorderSize = 0;
            btnLevelAuto.Cursor = Cursors.Hand;
            btnLevelAuto.Font = new Font("Segoe UI", 10.0f, FontStyle.Bold);
            btnLevelAuto.Text = "⚡  TỰ ĐỘNG CĂN CHỈNH LEVEL (TẤT CẢ VIEW ĐÃ CHỌN)";
            btnLevelAuto.Click += BtnLevelAuto_Click;

            btnLevelPick = new Button();
            btnLevelPick.Location = new Point(12, 381);
            btnLevelPick.Size = new Size(484, 40);
            btnLevelPick.BackColor = Color.FromArgb(52, 73, 94);
            btnLevelPick.ForeColor = Color.White;
            btnLevelPick.FlatStyle = FlatStyle.Flat;
            btnLevelPick.FlatAppearance.BorderSize = 0;
            btnLevelPick.Cursor = Cursors.Hand;
            btnLevelPick.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnLevelPick.Text = "🎯  CHỌN LEVEL / VIEWPORT TRÊN MÀN HÌNH (PICK)";
            btnLevelPick.Click += BtnLevelPick_Click;

            tabLevel.Controls.Add(grpLevelViews);
            tabLevel.Controls.Add(grpLevelInputs);
            tabLevel.Controls.Add(grpLevelSymbols);
            tabLevel.Controls.Add(btnLevelAuto);
            tabLevel.Controls.Add(btnLevelPick);
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

        // ======================= ACTION HANDLERS (MODELESS EXTERNALEVENT) =======================

        private void SyncContextFromApp(UIApplication uiapp)
        {
            if (uiapp != null && uiapp.ActiveUIDocument != null)
            {
                _uidoc = uiapp.ActiveUIDocument;
                _doc = _uidoc.Document;
                _activeView = _uidoc.ActiveView;
                _isSheet = (_activeView is ViewSheet);
                LoadContextData();
            }
        }

        private void SetStatusSafe(Color color, string text)
        {
            if (this.IsDisposed) return;
            Action act = () =>
            {
                if (this.IsDisposed) return;
                lblStatus.ForeColor = color;
                lblStatus.Text = text;
                if (lblCurrentView != null && _activeView != null)
                {
                    lblCurrentView.Text = string.Format("View: {0} ({1})  |  Tỷ lệ: 1:{2}  |  Grids: {3}  |  Levels: {4}",
                        _activeView.Name, _activeView.ViewType.ToString(), _activeView.Scale, _allGrids.Count, _allLevels.Count);
                }
            };
            if (this.InvokeRequired)
            {
                try { this.BeginInvoke((MethodInvoker)(() => act())); } catch { }
            }
            else
            {
                act();
            }
        }

        private void RunPickSafely(Action<UIApplication> pickAction)
        {
            HDToolModelessHost.Run(uiapp =>
            {
                SyncContextFromApp(uiapp);
                try { if (!this.IsDisposed) this.BeginInvoke((MethodInvoker)(() => this.Hide())); } catch { }
                try
                {
                    pickAction(uiapp);
                }
                finally
                {
                    try
                    {
                        if (!this.IsDisposed)
                        {
                            this.BeginInvoke((MethodInvoker)(() =>
                            {
                                this.Show();
                                this.BringToFront();
                                this.Activate();
                            }));
                        }
                    }
                    catch { }
                }
            });
        }

        private void BtnGridAuto_Click(object sender, EventArgs e)
        {
            double top = (double)numGridTop.Value;
            double bottom = (double)numGridBottom.Value;
            double left = (double)numGridLeft.Value;
            double right = (double)numGridRight.Value;
            bool make2D = chkGrid2D.Checked;
            bool symTop = chkGridSymTop.Checked;
            bool symBottom = chkGridSymBottom.Checked;
            bool symLeft = chkGridSymLeft.Checked;
            bool symRight = chkGridSymRight.Checked;

            HDToolModelessHost.Run(uiapp =>
            {
                try
                {
                    SyncContextFromApp(uiapp);
                    string msg;
                    if (_isSheet)
                    {
                        GridCropService.AdjustMultiplePlanViews(_doc, _sheetPlanViews, top, bottom, left, right, make2D,
                            symTop, symBottom, symLeft, symRight, out msg);
                    }
                    else
                    {
                        GridCropService.AdjustAllGridsInView(_doc, _activeView, top, bottom, left, right, make2D,
                            symTop, symBottom, symLeft, symRight, out msg);
                    }

                    try { _uidoc.RefreshActiveView(); } catch { }
                    SetStatusSafe(Color.FromArgb(39, 174, 96),
                        string.Format("✔ Đã căn chỉnh Grids thành công lúc {0}!", DateTime.Now.ToString("HH:mm:ss")));
                }
                catch (Exception ex)
                {
                    SetStatusSafe(Color.Red, "Lỗi căn Grids: " + ex.Message);
                }
            });
        }

        private void BtnGridPick_Click(object sender, EventArgs e)
        {
            double top = (double)numGridTop.Value;
            double bottom = (double)numGridBottom.Value;
            double left = (double)numGridLeft.Value;
            double right = (double)numGridRight.Value;
            bool make2D = chkGrid2D.Checked;
            bool symTop = chkGridSymTop.Checked;
            bool symBottom = chkGridSymBottom.Checked;
            bool symLeft = chkGridSymLeft.Checked;
            bool symRight = chkGridSymRight.Checked;

            RunPickSafely(uiapp =>
            {
                try
                {
                    if (_isSheet)
                    {
                        IList<Reference> references = _uidoc.Selection.PickObjects(
                            ObjectType.Element,
                            new GridSelectionFilter(true),
                            "Click chọn các Viewport trên Sheet cần căn chỉnh Grids (nhấn Finish khi xong)");

                        if (references != null && references.Count > 0)
                        {
                            List<Autodesk.Revit.DB.View> pickedPlanViews = new List<Autodesk.Revit.DB.View>();
                            foreach (Reference r in references)
                            {
                                Element elem = _doc.GetElement(r);
                                Viewport vp = elem as Viewport;
                                if (vp != null)
                                {
                                    Autodesk.Revit.DB.View v = _doc.GetElement(vp.ViewId) as Autodesk.Revit.DB.View;
                                    if (v != null && GridCropService.IsViewApplicableForGrids(v))
                                    {
                                        pickedPlanViews.Add(v);
                                    }
                                }
                            }

                            if (pickedPlanViews.Count > 0)
                            {
                                string msg;
                                GridCropService.AdjustMultiplePlanViews(_doc, pickedPlanViews, top, bottom, left, right, make2D,
                                    symTop, symBottom, symLeft, symRight, out msg);
                            }
                        }
                    }
                    else
                    {
                        IList<Reference> references = _uidoc.Selection.PickObjects(
                            ObjectType.Element,
                            new GridSelectionFilter(false),
                            "Click chọn Crop View hoặc các Grids cần căn chỉnh (nhấn Finish khi xong)");

                        if (references != null && references.Count > 0)
                        {
                            List<Grid> pickedGrids = new List<Grid>();
                            bool pickedCrop = false;

                            foreach (Reference r in references)
                            {
                                Element elem = _doc.GetElement(r);
                                Grid g = elem as Grid;
                                if (g != null) pickedGrids.Add(g);
                                else pickedCrop = true;
                            }

                            string msg;
                            if (pickedCrop)
                            {
                                List<Grid> all = GridCropService.GetAllGridsInView(_doc, _activeView);
                                foreach (Grid pg in pickedGrids)
                                {
                                    if (!all.Any(x => x.Id == pg.Id)) all.Add(pg);
                                }
                                GridCropService.AdjustGrids(_doc, _activeView, all, top, bottom, left, right, make2D,
                                    symTop, symBottom, symLeft, symRight, out msg);
                            }
                            else if (pickedGrids.Count > 0)
                            {
                                GridCropService.AdjustGrids(_doc, _activeView, pickedGrids, top, bottom, left, right, make2D,
                                    symTop, symBottom, symLeft, symRight, out msg);
                            }
                        }
                    }

                    try { _uidoc.RefreshActiveView(); } catch { }
                    SetStatusSafe(Color.FromArgb(39, 174, 96),
                        string.Format("✔ Đã áp dụng các đối tượng Grids vừa chọn lúc {0}!", DateTime.Now.ToString("HH:mm:ss")));
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    SetStatusSafe(Color.Red, "Lỗi pick Grids: " + ex.Message);
                }
            });
        }

        private void BtnLevelAuto_Click(object sender, EventArgs e)
        {
            double offsetLeft = (double)numLevelLeft.Value;
            double offsetRight = (double)numLevelRight.Value;
            bool make2D = chkLevel2D.Checked;
            bool symLeft = chkLevelSymLeft.Checked;
            bool symRight = chkLevelSymRight.Checked;

            List<int> checkedIndices = new List<int>();
            if (_isSheet && lstLevelViews != null)
            {
                for (int i = 0; i < lstLevelViews.Items.Count; i++)
                {
                    if (lstLevelViews.GetItemChecked(i)) checkedIndices.Add(i);
                }
            }

            HDToolModelessHost.Run(uiapp =>
            {
                try
                {
                    SyncContextFromApp(uiapp);
                    List<Autodesk.Revit.DB.View> targetViews = new List<Autodesk.Revit.DB.View>();

                    if (_isSheet && _sheetSectionViews != null)
                    {
                        foreach (int idx in checkedIndices)
                        {
                            if (idx >= 0 && idx < _sheetSectionViews.Count)
                                targetViews.Add(_sheetSectionViews[idx]);
                        }
                    }
                    else
                    {
                        targetViews.Add(_activeView);
                    }

                    if (targetViews.Count == 0)
                    {
                        SetStatusSafe(Color.OrangeRed, "Vui lòng tích chọn ít nhất 1 View trong danh sách!");
                        return;
                    }

                    string summaryMsg;
                    int count = LevelCropService.AdjustMultipleViews(_doc, targetViews, offsetLeft, offsetRight, make2D, symLeft, symRight, out summaryMsg);

                    try { _uidoc.RefreshActiveView(); } catch { }
                    SetStatusSafe(Color.FromArgb(41, 128, 185),
                        string.Format("✔ Đã căn chỉnh thành công {0} Level ({1} View) lúc {2}!", count, targetViews.Count, DateTime.Now.ToString("HH:mm:ss")));
                }
                catch (Exception ex)
                {
                    SetStatusSafe(Color.Red, "Lỗi căn Level: " + ex.Message);
                }
            });
        }

        private void BtnLevelPick_Click(object sender, EventArgs e)
        {
            double offsetLeft = (double)numLevelLeft.Value;
            double offsetRight = (double)numLevelRight.Value;
            bool make2D = chkLevel2D.Checked;
            bool symLeft = chkLevelSymLeft.Checked;
            bool symRight = chkLevelSymRight.Checked;

            RunPickSafely(uiapp =>
            {
                try
                {
                    if (_isSheet)
                    {
                        IList<Reference> references = _uidoc.Selection.PickObjects(
                            ObjectType.Element,
                            new LevelSelectionFilter(true),
                            "Click chọn các Viewport trên Sheet cần căn chỉnh Level (nhấn Finish khi xong)");

                        if (references != null && references.Count > 0)
                        {
                            List<Autodesk.Revit.DB.View> pickedViews = new List<Autodesk.Revit.DB.View>();
                            foreach (Reference r in references)
                            {
                                Element elem = _doc.GetElement(r);
                                Viewport vp = elem as Viewport;
                                if (vp != null)
                                {
                                    Autodesk.Revit.DB.View v = _doc.GetElement(vp.ViewId) as Autodesk.Revit.DB.View;
                                    if (v != null && LevelCropService.IsViewApplicable(v))
                                    {
                                        pickedViews.Add(v);
                                    }
                                }
                            }

                            if (pickedViews.Count > 0)
                            {
                                string summaryMsg;
                                LevelCropService.AdjustMultipleViews(_doc, pickedViews, offsetLeft, offsetRight, make2D, symLeft, symRight, out summaryMsg);
                            }
                        }
                    }
                    else
                    {
                        IList<Reference> references = _uidoc.Selection.PickObjects(
                            ObjectType.Element,
                            new LevelSelectionFilter(false),
                            "Click chọn Crop View hoặc các Level cần căn chỉnh (nhấn Finish khi xong)");

                        if (references != null && references.Count > 0)
                        {
                            List<Level> pickedLevels = new List<Level>();
                            foreach (Reference r in references)
                            {
                                Element elem = _doc.GetElement(r);
                                Level l = elem as Level;
                                if (l != null) pickedLevels.Add(l);
                            }

                            string msg;
                            if (pickedLevels.Count > 0)
                            {
                                LevelCropService.AdjustLevels(_doc, _activeView, pickedLevels, offsetLeft, offsetRight, make2D, symLeft, symRight, out msg);
                            }
                            else
                            {
                                List<Level> all = LevelCropService.GetAllLevelsInView(_doc, _activeView);
                                LevelCropService.AdjustLevels(_doc, _activeView, all, offsetLeft, offsetRight, make2D, symLeft, symRight, out msg);
                            }
                        }
                    }

                    try { _uidoc.RefreshActiveView(); } catch { }
                    SetStatusSafe(Color.FromArgb(41, 128, 185),
                        string.Format("✔ Đã áp dụng các đối tượng Level vừa chọn lúc {0}!", DateTime.Now.ToString("HH:mm:ss")));
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    SetStatusSafe(Color.Red, "Lỗi pick Level: " + ex.Message);
                }
            });
        }

        private void BuildTabAutoCrop()
        {
            tabAutoCrop = new TabPage();
            tabAutoCrop.Text = " 🔲  TỰ ĐỘNG CROP VIEW ";
            tabAutoCrop.BackColor = Color.FromArgb(250, 252, 254);
            tabAutoCrop.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

            // 1. Group Info
            grpCropInfo = new GroupBox();
            grpCropInfo.Text = "Khung Nhìn Mục Tiêu (Target View)";
            grpCropInfo.Location = new Point(12, 12);
            grpCropInfo.Size = new Size(484, 85);
            grpCropInfo.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            grpCropInfo.ForeColor = Color.FromArgb(44, 62, 80);

            lblCropViewName = new Label();
            lblCropViewName.Location = new Point(15, 24);
            lblCropViewName.Size = new Size(454, 22);
            lblCropViewName.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            lblCropViewName.ForeColor = Color.FromArgb(2, 132, 199);
            if (_activeView != null)
            {
                lblCropViewName.Text = string.Format("View: {0} ({1}) - Tỷ lệ 1:{2}", _activeView.Name, _activeView.ViewType.ToString(), _activeView.Scale);
            }

            lblCropElemCount = new Label();
            lblCropElemCount.Location = new Point(15, 50);
            lblCropElemCount.Size = new Size(454, 22);
            lblCropElemCount.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblCropElemCount.ForeColor = Color.FromArgb(16, 185, 129);
            try
            {
                List<Element> list = AutoCropService.GetModelElementsInView(_doc, _activeView);
                lblCropElemCount.Text = string.Format("Phát hiện {0} đối tượng mô hình trong View (Pipes, Ducts, Fittings...)", list != null ? list.Count : 0);
            }
            catch
            {
                lblCropElemCount.Text = "Sẵn sàng tính toán Bounding Box ôm khít toàn bộ đối tượng.";
            }

            grpCropInfo.Controls.Add(lblCropViewName);
            grpCropInfo.Controls.Add(lblCropElemCount);

            // 2. Group Padding
            grpCropPadding = new GroupBox();
            grpCropPadding.Text = "Khoảng Đệm Viền Ngoài Mô Hình (Padding / Margin mm)";
            grpCropPadding.Location = new Point(12, 105);
            grpCropPadding.Size = new Size(484, 125);
            grpCropPadding.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            grpCropPadding.ForeColor = Color.FromArgb(44, 62, 80);

            Label lblPad = new Label();
            lblPad.Text = "Khoảng đệm lề (mm):";
            lblPad.Location = new Point(15, 25);
            lblPad.Size = new Size(140, 22);
            lblPad.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

            numCropPadding = new NumericUpDown();
            numCropPadding.Location = new Point(160, 23);
            numCropPadding.Size = new Size(85, 23);
            numCropPadding.Minimum = 10;
            numCropPadding.Maximum = 5000;
            numCropPadding.Value = 150;
            numCropPadding.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);

            trackCropPadding = new TrackBar();
            trackCropPadding.AutoSize = false;
            trackCropPadding.Height = 26;
            trackCropPadding.Location = new Point(15, 50);
            trackCropPadding.Size = new Size(454, 26);
            trackCropPadding.Minimum = 20;
            trackCropPadding.Maximum = 600;
            trackCropPadding.Value = 150;
            trackCropPadding.TickFrequency = 50;

            numCropPadding.ValueChanged += (s, e) => {
                int val = (int)numCropPadding.Value;
                if (val >= trackCropPadding.Minimum && val <= trackCropPadding.Maximum) trackCropPadding.Value = val;
            };
            trackCropPadding.Scroll += (s, e) => {
                numCropPadding.Value = trackCropPadding.Value;
            };

            // Nút bấm nhanh mm
            System.Windows.Forms.Panel pnlQ = new System.Windows.Forms.Panel();
            pnlQ.Location = new Point(15, 84);
            pnlQ.Size = new Size(454, 30);

            int[] quicks = new int[] { 50, 100, 150, 200, 300 };
            int qPos = 0;
            foreach (int q in quicks)
            {
                Button bq = new Button();
                bq.Text = string.Format("{0}mm", q);
                bq.Size = new Size(68, 26);
                bq.Location = new Point(qPos, 0);
                bq.Font = new Font("Segoe UI", 8.0f, FontStyle.Bold);
                bq.BackColor = (q == 150) ? Color.FromArgb(16, 185, 129) : Color.FromArgb(236, 240, 241);
                bq.ForeColor = (q == 150) ? Color.White : Color.Black;
                bq.FlatStyle = FlatStyle.Flat;
                int currentQ = q;
                bq.Click += (s, e) => { numCropPadding.Value = currentQ; };
                pnlQ.Controls.Add(bq);
                qPos += 68;
            }

            grpCropPadding.Controls.Add(lblPad);
            grpCropPadding.Controls.Add(numCropPadding);
            grpCropPadding.Controls.Add(trackCropPadding);
            grpCropPadding.Controls.Add(pnlQ);

            // 3. Checkboxes
            chkCropMepOnly = new CheckBox();
            chkCropMepOnly.Text = "Ưu tiên ôm khít hệ thống MEP (Ống nước, ống gió, thiết bị - Cắt gọn sàn/vách)";
            chkCropMepOnly.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            chkCropMepOnly.ForeColor = Color.FromArgb(217, 119, 6); // Amber
            chkCropMepOnly.Location = new Point(15, 235);
            chkCropMepOnly.Size = new Size(454, 22);
            chkCropMepOnly.Checked = true;

            chkCropVisible = new CheckBox();
            chkCropVisible.Text = "Hiển thị đường viền Crop View (Crop Region Visible)";
            chkCropVisible.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            chkCropVisible.ForeColor = Color.FromArgb(52, 73, 94);
            chkCropVisible.Location = new Point(15, 258);
            chkCropVisible.Size = new Size(454, 22);
            chkCropVisible.Checked = true;

            chkCropSectionBox = new CheckBox();
            chkCropSectionBox.Text = "Đồng bộ khớp cả Section Box 3D (nếu đang bật trong View 3D)";
            chkCropSectionBox.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            chkCropSectionBox.ForeColor = Color.FromArgb(100, 116, 139);
            chkCropSectionBox.Location = new Point(15, 281);
            chkCropSectionBox.Size = new Size(454, 20);
            chkCropSectionBox.Checked = false;

            // 4. Action Buttons
            btnCropAuto = new Button();
            btnCropAuto.Location = new Point(12, 308);
            btnCropAuto.Size = new Size(484, 48);
            btnCropAuto.BackColor = Color.FromArgb(225, 29, 72); // Rose Red
            btnCropAuto.ForeColor = Color.White;
            btnCropAuto.FlatStyle = FlatStyle.Flat;
            btnCropAuto.FlatAppearance.BorderSize = 0;
            btnCropAuto.Cursor = Cursors.Hand;
            btnCropAuto.Font = new Font("Segoe UI", 10.0f, FontStyle.Bold);
            btnCropAuto.Text = "⚡  TỰ ĐỘNG FIT CROP VIEW (ÔM KHÍT TOÀN BỘ ĐỐI TƯỢNG)";
            btnCropAuto.Click += BtnCropAuto_Click;

            btnCropPick = new Button();
            btnCropPick.Location = new Point(12, 362);
            btnCropPick.Size = new Size(484, 42);
            btnCropPick.BackColor = Color.FromArgb(14, 116, 144); // Cyan 700
            btnCropPick.ForeColor = Color.White;
            btnCropPick.FlatStyle = FlatStyle.Flat;
            btnCropPick.FlatAppearance.BorderSize = 0;
            btnCropPick.Cursor = Cursors.Hand;
            btnCropPick.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnCropPick.Text = "🎯  CLICK / QUÉT CHỌN CỤM ĐỐI TƯỢNG CẦN CROP (PICK)";
            btnCropPick.Click += BtnCropPick_Click;

            tabAutoCrop.Controls.Add(grpCropInfo);
            tabAutoCrop.Controls.Add(grpCropPadding);
            tabAutoCrop.Controls.Add(chkCropMepOnly);
            tabAutoCrop.Controls.Add(chkCropVisible);
            tabAutoCrop.Controls.Add(chkCropSectionBox);
            tabAutoCrop.Controls.Add(btnCropAuto);
            tabAutoCrop.Controls.Add(btnCropPick);
        }

        private void BtnCropAuto_Click(object sender, EventArgs e)
        {
            double pad = (double)numCropPadding.Value;
            bool sec = chkCropSectionBox.Checked;
            bool vis = chkCropVisible.Checked;
            bool mepOnly = chkCropMepOnly.Checked;

            HDToolModelessHost.Run(uiapp =>
            {
                try
                {
                    SyncContextFromApp(uiapp);
                    string msg;
                    bool ok = AutoCropService.ApplyAutoCrop(_uidoc, out msg, pad, sec, vis, null, true, mepOnly, true);
                    if (ok)
                    {
                        SetStatusSafe(Color.FromArgb(39, 174, 96),
                            string.Format("✔ Đã Fit Crop View thành công lúc {0}!", DateTime.Now.ToString("HH:mm:ss")));
                    }
                    else
                    {
                        SetStatusSafe(Color.Red, msg);
                    }
                }
                catch (Exception ex)
                {
                    SetStatusSafe(Color.Red, "Lỗi Crop View: " + ex.Message);
                }
            });
        }

        private void BtnCropPick_Click(object sender, EventArgs e)
        {
            double pad = (double)numCropPadding.Value;
            bool sec = chkCropSectionBox.Checked;
            bool vis = chkCropVisible.Checked;
            bool mepOnly = chkCropMepOnly.Checked;

            RunPickSafely(uiapp =>
            {
                try
                {
                    IList<Reference> refs = _uidoc.Selection.PickObjects(
                        ObjectType.Element,
                        "HDTOOL: Quét chọn các đối tượng cần tạo Crop View bao quanh:"
                    );

                    if (refs != null && refs.Count > 0)
                    {
                        string msg = string.Empty;
                        bool ok = false;

                        if (_isSheet)
                        {
                            List<Autodesk.Revit.DB.View> vpViews = new List<Autodesk.Revit.DB.View>();
                            foreach (Reference r in refs)
                            {
                                Viewport vp = _doc.GetElement(r.ElementId) as Viewport;
                                if (vp != null)
                                {
                                    Autodesk.Revit.DB.View tv = _doc.GetElement(vp.ViewId) as Autodesk.Revit.DB.View;
                                    if (tv != null && !vpViews.Any(x => x.Id == tv.Id)) vpViews.Add(tv);
                                }
                            }
                            if (vpViews.Count > 0)
                            {
                                int cnt = AutoCropService.ApplyAutoCropToViews(_uidoc, vpViews, out msg, pad, true, mepOnly, true, sec, vis, null);
                                ok = cnt > 0;
                            }
                            else
                            {
                                msg = "Vui lòng chọn ít nhất 1 Viewport trên Sheet.";
                            }
                        }
                        else
                        {
                            List<ElementId> ids = new List<ElementId>();
                            foreach (Reference r in refs) ids.Add(r.ElementId);
                            ok = AutoCropService.ApplyAutoCrop(_uidoc, out msg, pad, sec, vis, ids, true, mepOnly, true);
                        }

                        if (ok)
                        {
                            SetStatusSafe(Color.FromArgb(39, 174, 96),
                                string.Format("✔ Đã Fit Crop View thành công lúc {0}!", DateTime.Now.ToString("HH:mm:ss")));
                        }
                        else
                        {
                            SetStatusSafe(Color.Red, msg);
                        }
                    }
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
                catch (Exception ex)
                {
                    SetStatusSafe(Color.Red, "Lỗi pick đối tượng: " + ex.Message);
                }
            });
        }
    }
}
