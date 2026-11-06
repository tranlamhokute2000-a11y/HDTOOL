using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace HDTool
{
    public class GridCropForm : System.Windows.Forms.Form
    {
        private UIDocument _uidoc;
        private Document _doc;
        private Autodesk.Revit.DB.View _activeView;
        private List<Grid> _allGrids;

        // UI Controls
        private System.Windows.Forms.Panel pnlHeader;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblSubtitle;

        private System.Windows.Forms.GroupBox grpInfo;
        private System.Windows.Forms.Label lblViewInfo;

        private System.Windows.Forms.GroupBox grpInputs;

        private System.Windows.Forms.Label lblTop;
        private System.Windows.Forms.NumericUpDown numTop;
        private System.Windows.Forms.Label lblUnitTop;

        private System.Windows.Forms.Label lblBottom;
        private System.Windows.Forms.NumericUpDown numBottom;
        private System.Windows.Forms.Label lblUnitBottom;

        private System.Windows.Forms.Label lblLeft;
        private System.Windows.Forms.NumericUpDown numLeft;
        private System.Windows.Forms.Label lblUnitLeft;

        private System.Windows.Forms.Label lblRight;
        private System.Windows.Forms.NumericUpDown numRight;
        private System.Windows.Forms.Label lblUnitRight;

        private System.Windows.Forms.CheckBox chkEqual;
        private System.Windows.Forms.CheckBox chk2D;
        private System.Windows.Forms.CheckBox chkRecoverHidden;

        private System.Windows.Forms.GroupBox grpSymbols;
        private System.Windows.Forms.CheckBox chkSymbolTop;
        private System.Windows.Forms.CheckBox chkSymbolBottom;
        private System.Windows.Forms.CheckBox chkSymbolLeft;
        private System.Windows.Forms.CheckBox chkSymbolRight;

        private System.Windows.Forms.Button btnAuto;
        private System.Windows.Forms.Button btnPick;
        private System.Windows.Forms.Button btnSwitchLevel;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.ToolTip toolTip;

        private bool _isSheet;
        private List<Autodesk.Revit.DB.View> _targetViews;

        public GridCropForm(UIDocument uidoc)
        {
            _uidoc = uidoc;
            _doc = uidoc.Document;
            _activeView = uidoc.ActiveView;
            _isSheet = (_activeView is ViewSheet);

            if (_isSheet)
            {
                _targetViews = GridCropService.GetApplicablePlanViews(uidoc);
                _allGrids = new List<Grid>();
                foreach (Autodesk.Revit.DB.View v in _targetViews)
                {
                    _allGrids.AddRange(GridCropService.GetAllGridsInView(_doc, v));
                }
            }
            else
            {
                _targetViews = new List<Autodesk.Revit.DB.View>() { _activeView };
                _allGrids = GridCropService.GetAllGridsInView(_doc, _activeView);
            }

            InitializeComponent();
            UpdateViewInfo();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.Text = "HDTOOL - Căn Chỉnh Lưới Trục (Grids) Theo Khung Crop View";
            this.Size = new System.Drawing.Size(530, 668);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.TopMost = true;
            this.BackColor = System.Drawing.Color.FromArgb(245, 247, 250);
            this.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Regular);

            toolTip = new System.Windows.Forms.ToolTip();

            // 1. Header
            pnlHeader = new System.Windows.Forms.Panel();
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = 75;
            pnlHeader.BackColor = System.Drawing.Color.FromArgb(24, 43, 73);

            lblTitle = new System.Windows.Forms.Label();
            lblTitle.Text = "CĂN CHỈNH LƯỚI TRỤC (GRIDS) THEO CROP VIEW";
            lblTitle.ForeColor = System.Drawing.Color.White;
            lblTitle.Font = new System.Drawing.Font("Segoe UI", 11.0f, FontStyle.Bold);
            lblTitle.Location = new System.Drawing.Point(15, 12);
            lblTitle.AutoSize = true;

            lblSubtitle = new System.Windows.Forms.Label();
            lblSubtitle.Text = "Tự động căn đều 4 phía lưới trục so với khung Crop View (mm)";
            lblSubtitle.ForeColor = System.Drawing.Color.FromArgb(189, 195, 199);
            lblSubtitle.Font = new System.Drawing.Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblSubtitle.Location = new System.Drawing.Point(15, 42);
            lblSubtitle.AutoSize = true;

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);

            // 2. View Info
            grpInfo = new System.Windows.Forms.GroupBox();
            grpInfo.Text = "Thông Tin View Hiện Tại";
            grpInfo.Location = new System.Drawing.Point(15, 85);
            grpInfo.Size = new System.Drawing.Size(485, 60);
            grpInfo.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Bold);
            grpInfo.ForeColor = System.Drawing.Color.FromArgb(44, 62, 80);

            lblViewInfo = new System.Windows.Forms.Label();
            lblViewInfo.Font = new System.Drawing.Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblViewInfo.ForeColor = System.Drawing.Color.FromArgb(52, 73, 94);
            lblViewInfo.Location = new System.Drawing.Point(12, 22);
            lblViewInfo.Size = new System.Drawing.Size(460, 30);
            grpInfo.Controls.Add(lblViewInfo);

            // 3. Inputs Group
            grpInputs = new System.Windows.Forms.GroupBox();
            grpInputs.Text = "Thông Số Khoảng Cách 4 Hướng (mm)";
            grpInputs.Location = new System.Drawing.Point(15, 155);
            grpInputs.Size = new System.Drawing.Size(485, 215);
            grpInputs.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Bold);
            grpInputs.ForeColor = System.Drawing.Color.FromArgb(44, 62, 80);

            // Phía Trên (Top)
            lblTop = new System.Windows.Forms.Label();
            lblTop.Text = "Phía Trên (Top):";
            lblTop.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Regular);
            lblTop.ForeColor = System.Drawing.Color.Black;
            lblTop.Location = new System.Drawing.Point(15, 28);
            lblTop.Size = new System.Drawing.Size(180, 22);

            numTop = new System.Windows.Forms.NumericUpDown();
            numTop.Location = new System.Drawing.Point(200, 26);
            numTop.Size = new System.Drawing.Size(95, 23);
            numTop.Minimum = -50000;
            numTop.Maximum = 100000;
            numTop.Value = 1500;
            numTop.Font = new System.Drawing.Font("Segoe UI", 9.5f, FontStyle.Bold);
            numTop.ValueChanged += NumTop_ValueChanged;

            lblUnitTop = new System.Windows.Forms.Label();
            lblUnitTop.Text = "mm  (Lưới trục dọc nhô lên trên Crop)";
            lblUnitTop.Font = new System.Drawing.Font("Segoe UI", 8.0f, FontStyle.Italic);
            lblUnitTop.ForeColor = System.Drawing.Color.Gray;
            lblUnitTop.Location = new System.Drawing.Point(300, 28);
            lblUnitTop.Size = new System.Drawing.Size(180, 22);

            // Phía Dưới (Bottom)
            lblBottom = new System.Windows.Forms.Label();
            lblBottom.Text = "Phía Dưới (Bottom):";
            lblBottom.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Regular);
            lblBottom.ForeColor = System.Drawing.Color.Black;
            lblBottom.Location = new System.Drawing.Point(15, 60);
            lblBottom.Size = new System.Drawing.Size(180, 22);

            numBottom = new System.Windows.Forms.NumericUpDown();
            numBottom.Location = new System.Drawing.Point(200, 58);
            numBottom.Size = new System.Drawing.Size(95, 23);
            numBottom.Minimum = -50000;
            numBottom.Maximum = 100000;
            numBottom.Value = 1500;
            numBottom.Font = new System.Drawing.Font("Segoe UI", 9.5f, FontStyle.Bold);

            lblUnitBottom = new System.Windows.Forms.Label();
            lblUnitBottom.Text = "mm  (Lưới trục dọc nhô xuống dưới Crop)";
            lblUnitBottom.Font = new System.Drawing.Font("Segoe UI", 8.0f, FontStyle.Italic);
            lblUnitBottom.ForeColor = System.Drawing.Color.Gray;
            lblUnitBottom.Location = new System.Drawing.Point(300, 60);
            lblUnitBottom.Size = new System.Drawing.Size(180, 22);

            // Bên Trái (Left)
            lblLeft = new System.Windows.Forms.Label();
            lblLeft.Text = "Bên Trái (Left):";
            lblLeft.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Regular);
            lblLeft.ForeColor = System.Drawing.Color.Black;
            lblLeft.Location = new System.Drawing.Point(15, 92);
            lblLeft.Size = new System.Drawing.Size(180, 22);

            numLeft = new System.Windows.Forms.NumericUpDown();
            numLeft.Location = new System.Drawing.Point(200, 90);
            numLeft.Size = new System.Drawing.Size(95, 23);
            numLeft.Minimum = -50000;
            numLeft.Maximum = 100000;
            numLeft.Value = 1500;
            numLeft.Font = new System.Drawing.Font("Segoe UI", 9.5f, FontStyle.Bold);

            lblUnitLeft = new System.Windows.Forms.Label();
            lblUnitLeft.Text = "mm  (Lưới trục ngang nhô sang trái Crop)";
            lblUnitLeft.Font = new System.Drawing.Font("Segoe UI", 8.0f, FontStyle.Italic);
            lblUnitLeft.ForeColor = System.Drawing.Color.Gray;
            lblUnitLeft.Location = new System.Drawing.Point(300, 92);
            lblUnitLeft.Size = new System.Drawing.Size(180, 22);

            // Bên Phải (Right)
            lblRight = new System.Windows.Forms.Label();
            lblRight.Text = "Bên Phải (Right):";
            lblRight.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Regular);
            lblRight.ForeColor = System.Drawing.Color.Black;
            lblRight.Location = new System.Drawing.Point(15, 124);
            lblRight.Size = new System.Drawing.Size(180, 22);

            numRight = new System.Windows.Forms.NumericUpDown();
            numRight.Location = new System.Drawing.Point(200, 122);
            numRight.Size = new System.Drawing.Size(95, 23);
            numRight.Minimum = -50000;
            numRight.Maximum = 100000;
            numRight.Value = 1500;
            numRight.Font = new System.Drawing.Font("Segoe UI", 9.5f, FontStyle.Bold);

            lblUnitRight = new System.Windows.Forms.Label();
            lblUnitRight.Text = "mm  (Lưới trục ngang nhô sang phải Crop)";
            lblUnitRight.Font = new System.Drawing.Font("Segoe UI", 8.0f, FontStyle.Italic);
            lblUnitRight.ForeColor = System.Drawing.Color.Gray;
            lblUnitRight.Location = new System.Drawing.Point(300, 124);
            lblUnitRight.Size = new System.Drawing.Size(180, 22);

            // Checkboxes
            chkEqual = new System.Windows.Forms.CheckBox();
            chkEqual.Text = "Khóa cả 4 hướng bằng nhau (Top = Bottom = Left = Right)";
            chkEqual.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Regular);
            chkEqual.ForeColor = System.Drawing.Color.FromArgb(41, 128, 185);
            chkEqual.Checked = true;
            chkEqual.Location = new System.Drawing.Point(18, 155);
            chkEqual.Size = new System.Drawing.Size(450, 22);
            chkEqual.CheckedChanged += ChkEqual_CheckedChanged;

            chk2D = new System.Windows.Forms.CheckBox();
            chk2D.Text = "Chế độ 2D View-Specific (Chỉ đổi trong Mặt bằng này - Khuyên dùng)";
            chk2D.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Regular);
            chk2D.ForeColor = System.Drawing.Color.FromArgb(39, 174, 96);
            chk2D.Checked = true;
            chk2D.Location = new System.Drawing.Point(18, 178);
            chk2D.Size = new System.Drawing.Size(450, 22);

            chkRecoverHidden = new System.Windows.Forms.CheckBox();
            chkRecoverHidden.Text = "Tự động Unhide & Kéo các trục bị ẩn / kéo lệch từ View khác về";
            chkRecoverHidden.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Bold);
            chkRecoverHidden.ForeColor = System.Drawing.Color.FromArgb(217, 119, 6);
            chkRecoverHidden.Checked = true;
            chkRecoverHidden.Location = new System.Drawing.Point(18, 202);
            chkRecoverHidden.Size = new System.Drawing.Size(450, 22);
            toolTip.SetToolTip(chkRecoverHidden, "Tự động phát hiện các trục hoành/trục tung bị kéo dạt sang view khác hoặc bị ẩn trong view này để kéo về ngay khớp Crop View");

            grpInputs.Controls.Add(lblTop);
            grpInputs.Controls.Add(numTop);
            grpInputs.Controls.Add(lblUnitTop);
            grpInputs.Controls.Add(lblBottom);
            grpInputs.Controls.Add(numBottom);
            grpInputs.Controls.Add(lblUnitBottom);
            grpInputs.Controls.Add(lblLeft);
            grpInputs.Controls.Add(numLeft);
            grpInputs.Controls.Add(lblUnitLeft);
            grpInputs.Controls.Add(lblRight);
            grpInputs.Controls.Add(numRight);
            grpInputs.Controls.Add(lblUnitRight);
            grpInputs.Controls.Add(chkEqual);
            grpInputs.Controls.Add(chk2D);
            grpInputs.Controls.Add(chkRecoverHidden);

            // 4. Bật/Tắt Ký Hiệu Đầu Trục (Symbols / Bubbles 4 Hướng)
            grpSymbols = new System.Windows.Forms.GroupBox();
            grpSymbols.Text = "Bật / Tắt Ký Hiệu Đầu Trục (Symbols / Bubbles 4 Hướng)";
            grpSymbols.Location = new System.Drawing.Point(15, 396);
            grpSymbols.Size = new System.Drawing.Size(485, 78);
            grpSymbols.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Bold);
            grpSymbols.ForeColor = System.Drawing.Color.FromArgb(44, 62, 80);

            chkSymbolTop = new System.Windows.Forms.CheckBox();
            chkSymbolTop.Text = "Bật Symbol Phía Trên (Top)";
            chkSymbolTop.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Regular);
            chkSymbolTop.Checked = true;
            chkSymbolTop.Location = new System.Drawing.Point(18, 22);
            chkSymbolTop.Size = new System.Drawing.Size(210, 22);

            chkSymbolBottom = new System.Windows.Forms.CheckBox();
            chkSymbolBottom.Text = "Bật Symbol Phía Dưới (Bottom)";
            chkSymbolBottom.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Regular);
            chkSymbolBottom.Checked = true;
            chkSymbolBottom.Location = new System.Drawing.Point(18, 48);
            chkSymbolBottom.Size = new System.Drawing.Size(210, 22);

            chkSymbolLeft = new System.Windows.Forms.CheckBox();
            chkSymbolLeft.Text = "Bật Symbol Bên Trái (Left)";
            chkSymbolLeft.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Regular);
            chkSymbolLeft.Checked = true;
            chkSymbolLeft.Location = new System.Drawing.Point(245, 22);
            chkSymbolLeft.Size = new System.Drawing.Size(210, 22);

            chkSymbolRight = new System.Windows.Forms.CheckBox();
            chkSymbolRight.Text = "Bật Symbol Bên Phải (Right)";
            chkSymbolRight.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Regular);
            chkSymbolRight.Checked = true;
            chkSymbolRight.Location = new System.Drawing.Point(245, 48);
            chkSymbolRight.Size = new System.Drawing.Size(210, 22);

            grpSymbols.Controls.Add(chkSymbolTop);
            grpSymbols.Controls.Add(chkSymbolBottom);
            grpSymbols.Controls.Add(chkSymbolLeft);
            grpSymbols.Controls.Add(chkSymbolRight);

            // 5. Buttons
            btnAuto = new System.Windows.Forms.Button();
            btnAuto.Text = "⚡  TỰ ĐỘNG CĂN CHỈNH TẤT CẢ GRIDS THEO CROP VIEW";
            btnAuto.Font = new System.Drawing.Font("Segoe UI", 10.0f, FontStyle.Bold);
            btnAuto.Location = new System.Drawing.Point(15, 484);
            btnAuto.Size = new System.Drawing.Size(485, 42);
            btnAuto.BackColor = System.Drawing.Color.FromArgb(39, 174, 96);
            btnAuto.ForeColor = System.Drawing.Color.White;
            btnAuto.FlatStyle = FlatStyle.Flat;
            btnAuto.FlatAppearance.BorderSize = 0;
            btnAuto.Click += BtnAuto_Click;
            toolTip.SetToolTip(btnAuto, "Tự động căn chỉnh tất cả các lưới trục Grids cân đối theo khung Crop View và cập nhật ký hiệu 4 hướng");

            btnPick = new System.Windows.Forms.Button();
            btnPick.Text = "🎯  CLICK / QUÉT CHỌN CROP VIEW HOẶC GRIDS (PICK)";
            btnPick.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnPick.Location = new System.Drawing.Point(15, 532);
            btnPick.Size = new System.Drawing.Size(485, 36);
            btnPick.BackColor = System.Drawing.Color.FromArgb(41, 128, 185);
            btnPick.ForeColor = System.Drawing.Color.White;
            btnPick.FlatStyle = FlatStyle.Flat;
            btnPick.FlatAppearance.BorderSize = 0;
            btnPick.Click += BtnPick_Click;
            toolTip.SetToolTip(btnPick, "Bấm để click chọn khung Crop View hoặc chọn cụ thể các Grids trên màn hình");

            btnSwitchLevel = new System.Windows.Forms.Button();
            btnSwitchLevel.Text = "📏  CHUYỂN SANG CĂN LEVEL";
            btnSwitchLevel.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnSwitchLevel.Location = new System.Drawing.Point(15, 574);
            btnSwitchLevel.Size = new System.Drawing.Size(238, 34);
            btnSwitchLevel.BackColor = System.Drawing.Color.FromArgb(41, 128, 185);
            btnSwitchLevel.ForeColor = System.Drawing.Color.White;
            btnSwitchLevel.FlatStyle = FlatStyle.Flat;
            btnSwitchLevel.FlatAppearance.BorderSize = 0;
            btnSwitchLevel.Click += BtnSwitchLevel_Click;
            toolTip.SetToolTip(btnSwitchLevel, "Mở nhanh bảng Căn Chỉnh Cao Độ (Level) trên Mặt cắt / Mặt đứng / Sheet");

            btnClose = new System.Windows.Forms.Button();
            btnClose.Text = "Đóng (ESC)";
            btnClose.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Regular);
            btnClose.Location = new System.Drawing.Point(262, 574);
            btnClose.Size = new System.Drawing.Size(238, 34);
            btnClose.Click += (s, e) => this.Close();
            this.CancelButton = btnClose;

            this.Controls.Add(pnlHeader);
            this.Controls.Add(grpInfo);
            this.Controls.Add(grpInputs);
            this.Controls.Add(grpSymbols);
            this.Controls.Add(btnAuto);
            this.Controls.Add(btnPick);
            this.Controls.Add(btnSwitchLevel);
            this.Controls.Add(btnClose);

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

        private void UpdateViewInfo()
        {
            if (_activeView != null)
            {
                if (_isSheet)
                {
                    string viewNames = string.Join(", ", _targetViews.Select(v => v.Name).ToArray());
                    lblViewInfo.Text = string.Format("Trang in (Sheet): {0} | Tìm thấy {1} View: {2} | Tổng {3} Grids",
                        _activeView.Name, _targetViews.Count, viewNames, _allGrids.Count);
                    btnAuto.Text = string.Format("⚡  TỰ ĐỘNG CĂN CHỈNH TẤT CẢ GRIDS TRÊN SHEET ({0} VIEW)", _targetViews.Count);
                    btnPick.Text = "🎯  CHỌN VIEWPORT TRÊN SHEET HOẶC GRIDS (PICK)";
                }
                else
                {
                    lblViewInfo.Text = string.Format("View: {0} ({1})  |  Tỷ lệ: 1:{2}\nTìm thấy: {3} Lưới trục (Tự động khôi phục các trục bị ẩn / kéo xa từ View trước)",
                        _activeView.Name,
                        _activeView.ViewType.ToString(),
                        _activeView.Scale,
                        _allGrids.Count);
                }
            }
        }

        private void NumTop_ValueChanged(object sender, EventArgs e)
        {
            if (chkEqual.Checked)
            {
                numBottom.Value = numTop.Value;
                numLeft.Value = numTop.Value;
                numRight.Value = numTop.Value;
            }
        }

        private void ChkEqual_CheckedChanged(object sender, EventArgs e)
        {
            if (chkEqual.Checked)
            {
                numBottom.Value = numTop.Value;
                numLeft.Value = numTop.Value;
                numRight.Value = numTop.Value;

                numBottom.Enabled = false;
                numLeft.Enabled = false;
                numRight.Enabled = false;
            }
            else
            {
                numBottom.Enabled = true;
                numLeft.Enabled = true;
                numRight.Enabled = true;
            }
        }

        private void BtnAuto_Click(object sender, EventArgs e)
        {
            double top = (double)numTop.Value;
            double bottom = (double)numBottom.Value;
            double left = (double)numLeft.Value;
            double right = (double)numRight.Value;
            bool make2D = chk2D.Checked;
            bool symTop = chkSymbolTop.Checked;
            bool symBottom = chkSymbolBottom.Checked;
            bool symLeft = chkSymbolLeft.Checked;
            bool symRight = chkSymbolRight.Checked;

            try
            {
                string msg = string.Empty;
                int count = 0;

                if (_isSheet)
                {
                    count = GridCropService.AdjustMultiplePlanViews(_doc, _targetViews, top, bottom, left, right, make2D,
                        symTop, symBottom, symLeft, symRight, out msg);

                    _allGrids.Clear();
                    foreach (Autodesk.Revit.DB.View v in _targetViews)
                    {
                        _allGrids.AddRange(GridCropService.GetAllGridsInView(_doc, v));
                    }
                }
                else
                {
                    count = GridCropService.AdjustAllGridsInView(_doc, _activeView, top, bottom, left, right, make2D,
                        symTop, symBottom, symLeft, symRight, out msg);

                    _allGrids = GridCropService.GetAllGridsInView(_doc, _activeView);
                }

                UpdateViewInfo();

                if (count > 0)
                {
                    lblViewInfo.Text = string.Format("✓ ĐÃ CĂN CHỈNH THÀNH CÔNG {0} LƯỚI TRỤC THEO CROP VIEW! ({1})\nĐã tự động co đều 4 hướng cách mép Crop {2}mm.", count, DateTime.Now.ToString("HH:mm:ss"), top);
                    lblViewInfo.ForeColor = System.Drawing.Color.FromArgb(39, 174, 96);
                }
                else
                {
                    lblViewInfo.Text = string.Format("⚠ Chưa căn chỉnh được ({0}): {1}", DateTime.Now.ToString("HH:mm:ss"), msg);
                    lblViewInfo.ForeColor = System.Drawing.Color.FromArgb(231, 76, 60);
                }

                // Làm mới khung nhìn Revit tức thì để người dùng thấy trực tiếp thay đổi trên màn hình ngay lập tức (không gây vướng thao tác)
                try
                {
                    _uidoc.RefreshActiveView();
                }
                catch { }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Lỗi", "Đã xảy ra lỗi: " + ex.Message);
            }
            // Giữ nguyên form mở để người dùng tiếp tục xem và tinh chỉnh thông số cho đến khi bấm Đóng
        }

        private void BtnPick_Click(object sender, EventArgs e)
        {
            double top = (double)numTop.Value;
            double bottom = (double)numBottom.Value;
            double left = (double)numLeft.Value;
            double right = (double)numRight.Value;
            bool make2D = chk2D.Checked;
            bool symTop = chkSymbolTop.Checked;
            bool symBottom = chkSymbolBottom.Checked;
            bool symLeft = chkSymbolLeft.Checked;
            bool symRight = chkSymbolRight.Checked;

            this.Hide();

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
                            if (g != null)
                            {
                                pickedGrids.Add(g);
                            }
                            else
                            {
                                pickedCrop = true;
                            }
                        }

                        string msg;
                        if (pickedCrop)
                        {
                            List<Grid> all = GridCropService.GetAllGridsInView(_doc, _activeView);
                            foreach (Grid pg in pickedGrids)
                            {
                                if (!all.Any(x => x.Id == pg.Id))
                                {
                                    all.Add(pg);
                                }
                            }
                            GridCropService.AdjustGrids(_doc, _activeView, all, top, bottom, left, right, make2D,
                                symTop, symBottom, symLeft, symRight, out msg);
                        }
                        else if (pickedGrids.Count > 0)
                        {
                            GridCropService.AdjustGrids(_doc, _activeView, pickedGrids, top, bottom, left, right, make2D,
                                symTop, symBottom, symLeft, symRight, out msg);
                        }
                        else
                        {
                            GridCropService.AdjustAllGridsInView(_doc, _activeView, top, bottom, left, right, make2D,
                                symTop, symBottom, symLeft, symRight, out msg);
                        }
                    }
                }

                try
                {
                    _uidoc.RefreshActiveView();
                }
                catch { }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // Người dùng nhấn Esc hủy chọn
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Lỗi", "Đã xảy ra lỗi: " + ex.Message);
            }
            finally
            {
                this.Show();
            }
        }

        private void BtnSwitchLevel_Click(object sender, EventArgs e)
        {
            this.Hide();
            try
            {
                using (LevelCropForm form = new LevelCropForm(_uidoc, new List<Level>()))
                {
                    form.ShowDialog();
                }
            }
            finally
            {
                this.Close();
            }
        }
    }
}
