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
    public class LevelCropForm : System.Windows.Forms.Form
    {
        private UIDocument _uidoc;
        private Document _doc;
        private Autodesk.Revit.DB.View _activeView;
        private List<Autodesk.Revit.DB.View> _detectedViews;
        private List<Level> _preselectedLevels;
        private bool _isSheet;
        private bool _isPopulating = false;

        // UI Controls
        private System.Windows.Forms.Panel pnlHeader;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblSubtitle;

        private System.Windows.Forms.GroupBox grpViews;
        private System.Windows.Forms.Label lblViewsSummary;
        private System.Windows.Forms.CheckedListBox lstViews;
        private System.Windows.Forms.Button btnSelectAllViews;
        private System.Windows.Forms.Button btnDeselectAllViews;

        private System.Windows.Forms.GroupBox grpInputs;
        private System.Windows.Forms.Label lblLeft;
        private System.Windows.Forms.NumericUpDown numLeft;
        private System.Windows.Forms.Label lblUnitLeft;
        private System.Windows.Forms.Label lblLeftTip;

        private System.Windows.Forms.Label lblRight;
        private System.Windows.Forms.NumericUpDown numRight;
        private System.Windows.Forms.Label lblUnitRight;
        private System.Windows.Forms.Label lblRightTip;

        private System.Windows.Forms.CheckBox chkEqual;
        private System.Windows.Forms.CheckBox chk2D;

        private System.Windows.Forms.GroupBox grpSymbols;
        private System.Windows.Forms.CheckBox chkSymbolLeft;
        private System.Windows.Forms.CheckBox chkSymbolRight;

        private System.Windows.Forms.Button btnApplyAllViews;
        private System.Windows.Forms.Button btnPick;
        private System.Windows.Forms.Button btnSwitchGrid;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.ToolTip toolTip;

        public LevelCropForm(UIDocument uidoc, List<Level> preselectedLevels)
        {
            _uidoc = uidoc;
            _doc = uidoc.Document;
            _activeView = uidoc.ActiveView;
            _isSheet = (_activeView is ViewSheet);
            _preselectedLevels = preselectedLevels ?? new List<Level>();

            // Tự động tìm kiếm tất cả các View trong màn hình hiện tại (Sheet hoặc Tile Windows)
            _detectedViews = LevelCropService.GetApplicableViewsInContext(uidoc);

            InitializeComponent();
            PopulateViewsList();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.Text = "HDTOOL - Căn Chỉnh Level So Với Crop View (Đa View & Sheet)";
            this.Size = new System.Drawing.Size(560, 680);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.TopMost = true;
            this.BackColor = System.Drawing.Color.FromArgb(245, 247, 250);
            this.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Regular);

            toolTip = new System.Windows.Forms.ToolTip();

            // 1. Header Panel
            pnlHeader = new System.Windows.Forms.Panel();
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = 75;
            pnlHeader.BackColor = System.Drawing.Color.FromArgb(24, 43, 73);

            lblTitle = new System.Windows.Forms.Label();
            lblTitle.Text = "HDTOOL - CĂN CHỈNH LEVEL SO VỚI CROP VIEW";
            lblTitle.ForeColor = System.Drawing.Color.White;
            lblTitle.Font = new System.Drawing.Font("Segoe UI", 11.5f, FontStyle.Bold);
            lblTitle.Location = new System.Drawing.Point(15, 12);
            lblTitle.AutoSize = true;

            lblSubtitle = new System.Windows.Forms.Label();
            lblSubtitle.Text = "Hỗ trợ đồng loạt TẤT CẢ các View trên cùng màn hình làm việc / Sheet (mm)";
            lblSubtitle.ForeColor = System.Drawing.Color.FromArgb(189, 195, 199);
            lblSubtitle.Font = new System.Drawing.Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblSubtitle.Location = new System.Drawing.Point(15, 42);
            lblSubtitle.AutoSize = true;

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);

            // 2. Danh sách View được phát hiện trên màn hình / Sheet
            grpViews = new System.Windows.Forms.GroupBox();
            grpViews.Text = "Các View Đang Hiển Thị Trong Màn Hình Làm Việc";
            grpViews.Location = new System.Drawing.Point(15, 85);
            grpViews.Size = new System.Drawing.Size(515, 150);
            grpViews.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Bold);
            grpViews.ForeColor = System.Drawing.Color.FromArgb(44, 62, 80);

            lblViewsSummary = new System.Windows.Forms.Label();
            lblViewsSummary.Font = new System.Drawing.Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblViewsSummary.ForeColor = System.Drawing.Color.FromArgb(41, 128, 185);
            lblViewsSummary.Location = new System.Drawing.Point(12, 20);
            lblViewsSummary.Size = new System.Drawing.Size(490, 20);

            lstViews = new System.Windows.Forms.CheckedListBox();
            lstViews.Location = new System.Drawing.Point(12, 42);
            lstViews.Size = new System.Drawing.Size(490, 75);
            lstViews.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Regular);
            lstViews.CheckOnClick = true;
            lstViews.ItemCheck += (s, e) => {
                if (_isPopulating) return;
                int currentCount = lstViews.CheckedIndices.Count;
                int newCount = currentCount;
                if (e.NewValue == CheckState.Checked && e.CurrentValue != CheckState.Checked)
                {
                    newCount++;
                }
                else if (e.NewValue != CheckState.Checked && e.CurrentValue == CheckState.Checked)
                {
                    newCount = Math.Max(0, newCount - 1);
                }
                UpdateApplyButtonTextWithCount(newCount);
            };

            btnSelectAllViews = new System.Windows.Forms.Button();
            btnSelectAllViews.Text = "Chọn Tất Cả";
            btnSelectAllViews.Location = new System.Drawing.Point(12, 120);
            btnSelectAllViews.Size = new System.Drawing.Size(95, 24);
            btnSelectAllViews.Font = new System.Drawing.Font("Segoe UI", 8.0f, FontStyle.Regular);
            btnSelectAllViews.Click += (s, e) => SetAllViewsCheck(true);

            btnDeselectAllViews = new System.Windows.Forms.Button();
            btnDeselectAllViews.Text = "Bỏ Chọn Hết";
            btnDeselectAllViews.Location = new System.Drawing.Point(115, 120);
            btnDeselectAllViews.Size = new System.Drawing.Size(95, 24);
            btnDeselectAllViews.Font = new System.Drawing.Font("Segoe UI", 8.0f, FontStyle.Regular);
            btnDeselectAllViews.Click += (s, e) => SetAllViewsCheck(false);

            grpViews.Controls.Add(lblViewsSummary);
            grpViews.Controls.Add(lstViews);
            grpViews.Controls.Add(btnSelectAllViews);
            grpViews.Controls.Add(btnDeselectAllViews);

            // 3. Nhập thông số khoảng cách
            grpInputs = new System.Windows.Forms.GroupBox();
            grpInputs.Text = "Thông Số Khoảng Cách Cần Điều Chỉnh (mm)";
            grpInputs.Location = new System.Drawing.Point(15, 245);
            grpInputs.Size = new System.Drawing.Size(515, 185);
            grpInputs.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Bold);
            grpInputs.ForeColor = System.Drawing.Color.FromArgb(44, 62, 80);

            // Bên Trái
            lblLeft = new System.Windows.Forms.Label();
            lblLeft.Text = "Khoảng cách bên Trái (Left):";
            lblLeft.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Regular);
            lblLeft.ForeColor = System.Drawing.Color.Black;
            lblLeft.Location = new System.Drawing.Point(15, 30);
            lblLeft.Size = new System.Drawing.Size(190, 23);

            numLeft = new System.Windows.Forms.NumericUpDown();
            numLeft.Location = new System.Drawing.Point(215, 28);
            numLeft.Size = new System.Drawing.Size(100, 23);
            numLeft.Minimum = -50000;
            numLeft.Maximum = 100000;
            numLeft.Value = 500;
            numLeft.Font = new System.Drawing.Font("Segoe UI", 9.5f, FontStyle.Bold);
            numLeft.ValueChanged += (s, e) => {
                if (chkEqual.Checked) numRight.Value = numLeft.Value;
            };

            lblUnitLeft = new System.Windows.Forms.Label();
            lblUnitLeft.Text = "mm";
            lblUnitLeft.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Regular);
            lblUnitLeft.Location = new System.Drawing.Point(320, 30);
            lblUnitLeft.Size = new System.Drawing.Size(35, 23);

            lblLeftTip = new System.Windows.Forms.Label();
            lblLeftTip.Text = "(Khoảng cách đầu Level thò ra mép trái khung Crop View)";
            lblLeftTip.Font = new System.Drawing.Font("Segoe UI", 8.0f, FontStyle.Italic);
            lblLeftTip.ForeColor = System.Drawing.Color.Gray;
            lblLeftTip.Location = new System.Drawing.Point(15, 53);
            lblLeftTip.Size = new System.Drawing.Size(480, 18);

            // Bên Phải
            lblRight = new System.Windows.Forms.Label();
            lblRight.Text = "Khoảng cách bên Phải (Right):";
            lblRight.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Regular);
            lblRight.ForeColor = System.Drawing.Color.Black;
            lblRight.Location = new System.Drawing.Point(15, 78);
            lblRight.Size = new System.Drawing.Size(190, 23);

            numRight = new System.Windows.Forms.NumericUpDown();
            numRight.Location = new System.Drawing.Point(215, 76);
            numRight.Size = new System.Drawing.Size(100, 23);
            numRight.Minimum = -50000;
            numRight.Maximum = 100000;
            numRight.Value = 1500;
            numRight.Font = new System.Drawing.Font("Segoe UI", 9.5f, FontStyle.Bold);

            lblUnitRight = new System.Windows.Forms.Label();
            lblUnitRight.Text = "mm";
            lblUnitRight.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Regular);
            lblUnitRight.Location = new System.Drawing.Point(320, 78);
            lblUnitRight.Size = new System.Drawing.Size(35, 23);

            lblRightTip = new System.Windows.Forms.Label();
            lblRightTip.Text = "(Khoảng cách đầu Level thò ra mép phải khung Crop, có Bubble tên tầng)";
            lblRightTip.Font = new System.Drawing.Font("Segoe UI", 8.0f, FontStyle.Italic);
            lblRightTip.ForeColor = System.Drawing.Color.Gray;
            lblRightTip.Location = new System.Drawing.Point(15, 101);
            lblRightTip.Size = new System.Drawing.Size(480, 18);

            // Checkbox liên kết & 2D
            chkEqual = new System.Windows.Forms.CheckBox();
            chkEqual.Text = "Khóa 2 bên bằng nhau (Left = Right)";
            chkEqual.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Regular);
            chkEqual.ForeColor = System.Drawing.Color.FromArgb(41, 128, 185);
            chkEqual.Location = new System.Drawing.Point(18, 125);
            chkEqual.Size = new System.Drawing.Size(350, 22);
            chkEqual.CheckedChanged += (s, e) => {
                if (chkEqual.Checked)
                {
                    numRight.Value = numLeft.Value;
                    numRight.Enabled = false;
                }
                else
                {
                    numRight.Enabled = true;
                }
            };

            chk2D = new System.Windows.Forms.CheckBox();
            chk2D.Text = "Chế độ 2D View-Specific (Chỉ đổi trong View này, không đổi 3D - Khuyên dùng)";
            chk2D.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Regular);
            chk2D.ForeColor = System.Drawing.Color.FromArgb(39, 174, 96);
            chk2D.Checked = true;
            chk2D.Location = new System.Drawing.Point(18, 150);
            chk2D.Size = new System.Drawing.Size(480, 22);

            grpInputs.Controls.Add(lblLeft);
            grpInputs.Controls.Add(numLeft);
            grpInputs.Controls.Add(lblUnitLeft);
            grpInputs.Controls.Add(lblLeftTip);
            grpInputs.Controls.Add(lblRight);
            grpInputs.Controls.Add(numRight);
            grpInputs.Controls.Add(lblUnitRight);
            grpInputs.Controls.Add(lblRightTip);
            grpInputs.Controls.Add(chkEqual);
            grpInputs.Controls.Add(chk2D);

            // 4. Bật / Tắt Ký Hiệu Đầu Tầng (Symbols / Bubbles Độc Lập)
            grpSymbols = new System.Windows.Forms.GroupBox();
            grpSymbols.Text = "Bật / Tắt Ký Hiệu Đầu Tầng (Symbols / Bubbles Độc Lập)";
            grpSymbols.Location = new System.Drawing.Point(15, 436);
            grpSymbols.Size = new System.Drawing.Size(515, 56);
            grpSymbols.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Bold);
            grpSymbols.ForeColor = System.Drawing.Color.FromArgb(44, 62, 80);

            chkSymbolLeft = new System.Windows.Forms.CheckBox();
            chkSymbolLeft.Text = "Bật Symbol Bên Trái (Left)";
            chkSymbolLeft.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Regular);
            chkSymbolLeft.Checked = false;
            chkSymbolLeft.Location = new System.Drawing.Point(18, 22);
            chkSymbolLeft.Size = new System.Drawing.Size(220, 22);

            chkSymbolRight = new System.Windows.Forms.CheckBox();
            chkSymbolRight.Text = "Bật Symbol Bên Phải (Right)";
            chkSymbolRight.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Regular);
            chkSymbolRight.Checked = true;
            chkSymbolRight.Location = new System.Drawing.Point(260, 22);
            chkSymbolRight.Size = new System.Drawing.Size(220, 22);

            grpSymbols.Controls.Add(chkSymbolLeft);
            grpSymbols.Controls.Add(chkSymbolRight);

            // 5. Các nút thao tác chính
            btnApplyAllViews = new System.Windows.Forms.Button();
            btnApplyAllViews.Text = "⚡  ĐỒNG LOẠT CĂN CHỈNH TẤT CẢ VIEW ĐÃ CHỌN";
            btnApplyAllViews.Font = new System.Drawing.Font("Segoe UI", 10.0f, FontStyle.Bold);
            btnApplyAllViews.Location = new System.Drawing.Point(15, 502);
            btnApplyAllViews.Size = new System.Drawing.Size(515, 44);
            btnApplyAllViews.BackColor = System.Drawing.Color.FromArgb(39, 174, 96);
            btnApplyAllViews.ForeColor = System.Drawing.Color.White;
            btnApplyAllViews.FlatStyle = FlatStyle.Flat;
            btnApplyAllViews.FlatAppearance.BorderSize = 0;
            btnApplyAllViews.Click += BtnApplyAllViews_Click;
            toolTip.SetToolTip(btnApplyAllViews, "Chỉ cần 1 click: Tất cả các View đang chọn (H12, H14...) sẽ đồng loạt được điều chỉnh và cập nhật Symbol!");

            btnPick = new System.Windows.Forms.Button();
            if (_preselectedLevels.Count > 0 && !_isSheet)
            {
                btnPick.Text = string.Format("⚡  CĂN CHỈNH {0} LEVEL ĐÃ CHỌN TRÊN MÀN HÌNH", _preselectedLevels.Count);
                btnPick.BackColor = System.Drawing.Color.FromArgb(230, 126, 34);
            }
            else
            {
                btnPick.Text = _isSheet ? "🎯  CHỌN VIEWPORT TRÊN SHEET (PICK)" : "🎯  CHỌN LEVEL TRÊN MÀN HÌNH (PICK)";
                btnPick.BackColor = System.Drawing.Color.FromArgb(41, 128, 185);
            }
            btnPick.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnPick.Location = new System.Drawing.Point(15, 552);
            btnPick.Size = new System.Drawing.Size(515, 36);
            btnPick.ForeColor = System.Drawing.Color.White;
            btnPick.FlatStyle = FlatStyle.Flat;
            btnPick.FlatAppearance.BorderSize = 0;
            btnPick.Click += BtnPick_Click;
            toolTip.SetToolTip(btnPick, "Click chọn thủ công các đối tượng trên bản vẽ");

            btnSwitchGrid = new System.Windows.Forms.Button();
            btnSwitchGrid.Text = "📐  CHUYỂN SANG CĂN GRIDS";
            btnSwitchGrid.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnSwitchGrid.Location = new System.Drawing.Point(15, 594);
            btnSwitchGrid.Size = new System.Drawing.Size(252, 34);
            btnSwitchGrid.BackColor = System.Drawing.Color.FromArgb(39, 174, 96);
            btnSwitchGrid.ForeColor = System.Drawing.Color.White;
            btnSwitchGrid.FlatStyle = FlatStyle.Flat;
            btnSwitchGrid.FlatAppearance.BorderSize = 0;
            btnSwitchGrid.Click += BtnSwitchGrid_Click;
            toolTip.SetToolTip(btnSwitchGrid, "Mở nhanh bảng Căn Chỉnh Lưới Trục (Grids) trên Mặt bằng / Mặt đứng / Sheet");

            btnClose = new System.Windows.Forms.Button();
            btnClose.Text = "Đóng (ESC)";
            btnClose.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Regular);
            btnClose.Location = new System.Drawing.Point(278, 594);
            btnClose.Size = new System.Drawing.Size(252, 34);
            btnClose.Click += (s, e) => this.Close();
            this.CancelButton = btnClose;

            this.Controls.Add(pnlHeader);
            this.Controls.Add(grpViews);
            this.Controls.Add(grpInputs);
            this.Controls.Add(grpSymbols);
            this.Controls.Add(btnApplyAllViews);
            this.Controls.Add(btnPick);
            this.Controls.Add(btnSwitchGrid);
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

        private void PopulateViewsList()
        {
            _isPopulating = true;
            try
            {
                lstViews.Items.Clear();

                if (_detectedViews.Count > 0)
                {
                    if (_isSheet)
                    {
                        lblViewsSummary.Text = string.Format("Phát hiện {0} View (H12, H14...) trên Sheet: {1}", _detectedViews.Count, _activeView.Name);
                    }
                    else
                    {
                        lblViewsSummary.Text = string.Format("Phát hiện {0} View đang hiển thị trên màn hình làm việc", _detectedViews.Count);
                    }

                    foreach (Autodesk.Revit.DB.View v in _detectedViews)
                    {
                        List<Level> lvls = LevelCropService.GetAllLevelsInView(_doc, v);
                        string itemText = string.Format("{0} [{1}] - ({2} Level)", v.Name, v.ViewType.ToString(), lvls.Count);
                        lstViews.Items.Add(itemText, true); // Mặc định tích chọn tất cả các view
                    }
                }
                else
                {
                    lblViewsSummary.Text = "Chưa phát hiện View nào. Bạn có thể sử dụng nút Pick để chọn trực tiếp.";
                }
            }
            finally
            {
                _isPopulating = false;
            }

            UpdateApplyButtonText();
        }

        private void SetAllViewsCheck(bool check)
        {
            _isPopulating = true;
            try
            {
                for (int i = 0; i < lstViews.Items.Count; i++)
                {
                    lstViews.SetItemChecked(i, check);
                }
            }
            finally
            {
                _isPopulating = false;
            }
            UpdateApplyButtonText();
        }

        private void UpdateApplyButtonText()
        {
            UpdateApplyButtonTextWithCount(lstViews.CheckedIndices.Count);
        }

        private void UpdateApplyButtonTextWithCount(int checkedCount)
        {
            if (checkedCount > 1)
            {
                btnApplyAllViews.Text = string.Format("⚡  ĐỒNG LOẠT CĂN CHỈNH {0} VIEW ĐANG CHỌN", checkedCount);
                btnApplyAllViews.Enabled = true;
            }
            else if (checkedCount == 1)
            {
                btnApplyAllViews.Text = "⚡  CĂN CHỈNH 1 VIEW ĐÃ CHỌN";
                btnApplyAllViews.Enabled = true;
            }
            else
            {
                btnApplyAllViews.Text = "Vui lòng tích chọn ít nhất 1 View";
                btnApplyAllViews.Enabled = false;
            }
        }

        private List<Autodesk.Revit.DB.View> GetSelectedViewsFromList()
        {
            List<Autodesk.Revit.DB.View> selected = new List<Autodesk.Revit.DB.View>();
            for (int i = 0; i < lstViews.Items.Count; i++)
            {
                if (lstViews.GetItemChecked(i) && i < _detectedViews.Count)
                {
                    selected.Add(_detectedViews[i]);
                }
            }
            return selected;
        }

        private void BtnApplyAllViews_Click(object sender, EventArgs e)
        {
            List<Autodesk.Revit.DB.View> targetViews = GetSelectedViewsFromList();
            if (targetViews.Count == 0)
            {
                TaskDialog.Show("Thông Báo", "Vui lòng tích chọn ít nhất một View trong danh sách.");
                return;
            }

            double offsetLeft = (double)numLeft.Value;
            double offsetRight = (double)numRight.Value;
            bool make2D = chk2D.Checked;
            bool symLeft = chkSymbolLeft.Checked;
            bool symRight = chkSymbolRight.Checked;

            string summaryMsg;
            int totalLevels = LevelCropService.AdjustMultipleViews(_doc, targetViews, offsetLeft, offsetRight, make2D, symLeft, symRight, out summaryMsg);

            try
            {
                _uidoc.RefreshActiveView();
            }
            catch { }
        }

        private void BtnPick_Click(object sender, EventArgs e)
        {
            double offsetLeft = (double)numLeft.Value;
            double offsetRight = (double)numRight.Value;
            bool make2D = chk2D.Checked;
            bool symLeft = chkSymbolLeft.Checked;
            bool symRight = chkSymbolRight.Checked;

            if (_preselectedLevels.Count > 0 && !_isSheet)
            {
                string msg;
                LevelCropService.AdjustLevels(_doc, _activeView, _preselectedLevels, offsetLeft, offsetRight, make2D, symLeft, symRight, out msg);
                try
                {
                    _uidoc.RefreshActiveView();
                }
                catch { }
                return;
            }

            this.Hide();

            try
            {
                if (_isSheet)
                {
                    // Trên Sheet: Người dùng chọn các Viewport (H12, H14...)
                    IList<Reference> references = _uidoc.Selection.PickObjects(
                        ObjectType.Element,
                        new LevelSelectionFilter(true),
                        "Click chọn các Viewport (H12, H14...) trên Sheet (nhấn Finish khi chọn xong)");

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
                            string summary;
                            LevelCropService.AdjustMultipleViews(_doc, pickedViews, offsetLeft, offsetRight, make2D, symLeft, symRight, out summary);
                        }
                    }
                }
                else
                {
                    // Trong View thường: Người dùng chọn các Level
                    IList<Reference> references = _uidoc.Selection.PickObjects(
                        ObjectType.Element,
                        new LevelSelectionFilter(false),
                        "Click chọn các Level cần căn chỉnh (nhấn Finish trên thanh Options Bar khi chọn xong)");

                    if (references != null && references.Count > 0)
                    {
                        List<Level> pickedLevels = new List<Level>();
                        foreach (Reference r in references)
                        {
                            Element elem = _doc.GetElement(r);
                            Level lvl = elem as Level;
                            if (lvl != null)
                            {
                                pickedLevels.Add(lvl);
                            }
                        }

                        string msg;
                        LevelCropService.AdjustLevels(_doc, _activeView, pickedLevels, offsetLeft, offsetRight, make2D, symLeft, symRight, out msg);
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

        private void BtnSwitchGrid_Click(object sender, EventArgs e)
        {
            this.Hide();
            try
            {
                using (GridCropForm form = new GridCropForm(_uidoc))
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
