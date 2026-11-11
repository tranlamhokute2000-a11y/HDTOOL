using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Color = System.Drawing.Color;
using Point = System.Drawing.Point;

namespace HDTool
{
    public class HDToolMainForm : System.Windows.Forms.Form
    {
        private UIDocument _uidoc;
        private Document _doc;
        private Autodesk.Revit.DB.View _activeView;

        // UI Controls
        private System.Windows.Forms.Panel pnlHeader;
        private Label lblTitle;
        private Label lblSubtitle;

        private GroupBox grpInfo;
        private Label lblCurrentView;

        private Button btnAutoCrop;
        private Button btnCadToDuct;
        private Button btnGrids;
        private Button btnLevel;
        private Button btnMepQuantity;
        private Button btnSheetList;
        private Button btnUpdateSheetList;
        private Button btnReloadTool;
        private Button btnSmart;
        private Button btnClose;

        public HDToolMainForm(UIDocument uidoc)
        {
            _uidoc = uidoc;
            _doc = uidoc.Document;
            _activeView = uidoc.ActiveView;

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.Text = "HDTOOL - Trung Tâm Điều Khiển Bản Vẽ Revit";
            this.Size = new Size(540, 665);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.TopMost = true;
            this.BackColor = Color.FromArgb(245, 247, 250);
            this.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

            // 1. Header
            pnlHeader = new System.Windows.Forms.Panel();
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = 75;
            pnlHeader.BackColor = Color.FromArgb(24, 43, 73);

            lblTitle = new Label();
            lblTitle.Text = "HDTOOL - ĐIỀU KHIỂN & CĂN CHỈNH BẢN VẼ";
            lblTitle.ForeColor = Color.White;
            lblTitle.Font = new Font("Segoe UI", 12.0f, FontStyle.Bold);
            lblTitle.Location = new Point(16, 12);
            lblTitle.AutoSize = true;

            lblSubtitle = new Label();
            lblSubtitle.Text = "Tùy chọn công cụ căn chỉnh khi cần (Chủ động lựa chọn - Không ép buộc tự động)";
            lblSubtitle.ForeColor = Color.FromArgb(189, 195, 199);
            lblSubtitle.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblSubtitle.Location = new Point(16, 42);
            lblSubtitle.AutoSize = true;

            PictureBox picLogo = new PictureBox();
            picLogo.Size = new Size(130, 42);
            picLogo.Location = new Point(385, 16);
            picLogo.SizeMode = PictureBoxSizeMode.Zoom;
            picLogo.BackColor = Color.White;
            try
            {
                using (System.IO.Stream stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("hadomee_logo.png"))
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

            // 2. View Info
            grpInfo = new GroupBox();
            grpInfo.Text = "View / Sheet Đang Mở Hiện Tại";
            grpInfo.Location = new Point(16, 85);
            grpInfo.Size = new Size(492, 58);
            grpInfo.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            grpInfo.ForeColor = Color.FromArgb(44, 62, 80);

            lblCurrentView = new Label();
            lblCurrentView.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblCurrentView.ForeColor = Color.FromArgb(52, 73, 94);
            lblCurrentView.Location = new Point(12, 22);
            lblCurrentView.Size = new Size(465, 28);
            if (_activeView != null)
            {
                lblCurrentView.Text = string.Format("Tên: {0}  |  Loại: {1}  |  Tỷ lệ: 1:{2}",
                    _activeView.Name, _activeView.ViewType.ToString(), _activeView.Scale);
            }
            grpInfo.Controls.Add(lblCurrentView);

            // 3. Button 0: Tự Động Crop View (MỚI THEO LỆNH SẾP HỒ)
            btnAutoCrop = new Button();
            btnAutoCrop.Location = new Point(16, 150);
            btnAutoCrop.Size = new Size(492, 54);
            btnAutoCrop.BackColor = Color.FromArgb(225, 29, 72); // Rose 600
            btnAutoCrop.ForeColor = Color.White;
            btnAutoCrop.FlatStyle = FlatStyle.Flat;
            btnAutoCrop.FlatAppearance.BorderSize = 0;
            btnAutoCrop.Cursor = Cursors.Hand;
            btnAutoCrop.TextAlign = ContentAlignment.MiddleLeft;
            btnAutoCrop.Padding = new Padding(15, 0, 0, 0);
            btnAutoCrop.Text = "🔲  1. TỰ ĐỘNG CROP VIEW (AUTO CROP BOX - MỚI)\r\n" +
                               "      Tự động tính Bounding Box ôm khít toàn bộ đối tượng trong View vừa tạo (SĐKG 3D, Plan)";
            btnAutoCrop.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnAutoCrop.Click += BtnAutoCrop_Click;

            // 4. Button 1: Căn Chỉnh Grids
            btnGrids = new Button();
            btnGrids.Location = new Point(16, 210);
            btnGrids.Size = new Size(492, 54);
            btnGrids.BackColor = Color.FromArgb(39, 174, 96);
            btnGrids.ForeColor = Color.White;
            btnGrids.FlatStyle = FlatStyle.Flat;
            btnGrids.FlatAppearance.BorderSize = 0;
            btnGrids.Cursor = Cursors.Hand;
            btnGrids.TextAlign = ContentAlignment.MiddleLeft;
            btnGrids.Padding = new Padding(15, 0, 0, 0);
            btnGrids.Text = "📐  1. CĂN CHỈNH LƯỚI TRỤC (GRIDS)\r\n" +
                            "      Mặt bằng & Sheet - Nhập thông số 4 hướng Top, Bottom, Left, Right (mm)";
            btnGrids.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnGrids.Click += BtnGrids_Click;

            // 4. Button 2: Căn Chỉnh Level
            btnLevel = new Button();
            btnLevel.Location = new Point(16, 270);
            btnLevel.Size = new Size(492, 54);
            btnLevel.BackColor = Color.FromArgb(41, 128, 185);
            btnLevel.ForeColor = Color.White;
            btnLevel.FlatStyle = FlatStyle.Flat;
            btnLevel.FlatAppearance.BorderSize = 0;
            btnLevel.Cursor = Cursors.Hand;
            btnLevel.TextAlign = ContentAlignment.MiddleLeft;
            btnLevel.Padding = new Padding(15, 0, 0, 0);
            btnLevel.Text = "📏  2. CĂN CHỈNH CAO ĐỘ (LEVEL)\r\n" +
                            "      Mặt cắt, Mặt đứng & Sheet - Nhập khoảng cách 2 đầu thò ra (mm)";
            btnLevel.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnLevel.Click += BtnLevel_Click;

            // 5. Button 3: Thống Kê Khối Lượng Ống Nước & Phụ Kiện (Tính Năng Mới)
            btnMepQuantity = new Button();
            btnMepQuantity.Location = new Point(16, 330);
            btnMepQuantity.Size = new Size(492, 56);
            btnMepQuantity.BackColor = Color.FromArgb(2, 132, 199); // Sky Blue Accent
            btnMepQuantity.ForeColor = Color.White;
            btnMepQuantity.FlatStyle = FlatStyle.Flat;
            btnMepQuantity.FlatAppearance.BorderSize = 0;
            btnMepQuantity.Cursor = Cursors.Hand;
            btnMepQuantity.TextAlign = ContentAlignment.MiddleLeft;
            btnMepQuantity.Padding = new Padding(15, 0, 0, 0);
            btnMepQuantity.Text = "💧  3. THỐNG KÊ KHỐI LƯỢNG ỐNG NƯỚC & PHỤ KIỆN\r\n" +
                                  "      Gộp 3 parameters phân mảnh làm 1 cột, quét chọn sáng đối tượng & đưa vào Sheet";
            btnMepQuantity.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnMepQuantity.Click += BtnMepQuantity_Click;

            // 6. Button 4: Xuất Danh Mục Bản Vẽ
            btnSheetList = new Button();
            btnSheetList.Location = new Point(16, 390);
            btnSheetList.Size = new Size(492, 54);
            btnSheetList.BackColor = Color.FromArgb(22, 160, 133);
            btnSheetList.ForeColor = Color.White;
            btnSheetList.FlatStyle = FlatStyle.Flat;
            btnSheetList.FlatAppearance.BorderSize = 0;
            btnSheetList.Cursor = Cursors.Hand;
            btnSheetList.TextAlign = ContentAlignment.MiddleLeft;
            btnSheetList.Padding = new Padding(15, 0, 0, 0);
            btnSheetList.Text = "📋  4. XUẤT DANH MỤC BẢN VẼ (SHEET LIST)\r\n" +
                                "      Chọn các Sheet và trích xuất danh sách ra file Excel (.xls, .csv) hoặc Clipboard";
            btnSheetList.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnSheetList.Click += BtnSheetList_Click;

            // 7. Button 5: Cập Nhật Danh Mục Từ Excel
            btnUpdateSheetList = new Button();
            btnUpdateSheetList.Location = new Point(16, 450);
            btnUpdateSheetList.Size = new Size(492, 54);
            btnUpdateSheetList.BackColor = Color.FromArgb(211, 84, 0); // Orange Accent
            btnUpdateSheetList.ForeColor = Color.White;
            btnUpdateSheetList.FlatStyle = FlatStyle.Flat;
            btnUpdateSheetList.FlatAppearance.BorderSize = 0;
            btnUpdateSheetList.Cursor = Cursors.Hand;
            btnUpdateSheetList.TextAlign = ContentAlignment.MiddleLeft;
            btnUpdateSheetList.Padding = new Padding(15, 0, 0, 0);
            btnUpdateSheetList.Text = "🔄  5. CẬP NHẬT DANH MỤC TỪ EXCEL (UPDATE SHEET LIST)\r\n" +
                                      "      Đọc file Excel/CSV đã đổi tên và tự động cập nhật bản vẽ trong Revit";
            btnUpdateSheetList.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnUpdateSheetList.Click += BtnUpdateSheetList_Click;

            // 8. Button 6: Cập Nhật Tính Năng Mới (Hot Reload)
            btnReloadTool = new Button();
            btnReloadTool.Location = new Point(16, 510);
            btnReloadTool.Size = new Size(492, 54);
            btnReloadTool.BackColor = Color.FromArgb(100, 116, 139); // Slate Grey
            btnReloadTool.ForeColor = Color.White;
            btnReloadTool.FlatStyle = FlatStyle.Flat;
            btnReloadTool.FlatAppearance.BorderSize = 0;
            btnReloadTool.Cursor = Cursors.Hand;
            btnReloadTool.TextAlign = ContentAlignment.MiddleLeft;
            btnReloadTool.Padding = new Padding(15, 0, 0, 0);
            btnReloadTool.Text = "🚀  6. CẬP NHẬT TÍNH NĂNG MỚI (HOT RELOAD TOOL)\r\n" +
                                "      Nạp trực tiếp DLL mới vào Revit đang mở mà không cần tắt bản vẽ hay khởi động lại";
            btnReloadTool.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnReloadTool.Click += BtnReloadTool_Click;

            // 9. Button 7: Tự Động Theo View
            btnSmart = new Button();
            btnSmart.Location = new Point(16, 570);
            btnSmart.Size = new Size(492, 44);
            btnSmart.BackColor = Color.FromArgb(142, 68, 173); // Purple Accent
            btnSmart.ForeColor = Color.White;
            btnSmart.FlatStyle = FlatStyle.Flat;
            btnSmart.FlatAppearance.BorderSize = 0;
            btnSmart.Cursor = Cursors.Hand;
            btnSmart.TextAlign = ContentAlignment.MiddleLeft;
            btnSmart.Padding = new Padding(15, 0, 0, 0);
            btnSmart.Text = "⚡  7. TỰ ĐỘNG CHỌN THEO VIEW HIỆN TẠI (Tùy chọn nhanh)\r\n" +
                            "      Mặt bằng / Sheet ➔ Mở Grids  |  Mặt cắt / Mặt đứng ➔ Mở Level";
            btnSmart.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            btnSmart.Click += BtnSmart_Click;

            // Button 8: Dựng Ống Gió 3D Từ CAD Link
            btnCadToDuct = new Button();
            btnCadToDuct.Location = new Point(16, 622);
            btnCadToDuct.Size = new Size(492, 52);
            btnCadToDuct.BackColor = Color.FromArgb(12, 74, 110); // Deep Sky Navy
            btnCadToDuct.ForeColor = Color.White;
            btnCadToDuct.FlatStyle = FlatStyle.Flat;
            btnCadToDuct.FlatAppearance.BorderSize = 0;
            btnCadToDuct.Cursor = Cursors.Hand;
            btnCadToDuct.TextAlign = ContentAlignment.MiddleLeft;
            btnCadToDuct.Padding = new Padding(15, 0, 0, 0);
            btnCadToDuct.Text = "💨  8. DỰNG MÔ HÌNH ỐNG GIÓ 3D TỪ BẢN VẼ CAD LINK\r\n" +
                                "      Nhận diện WxH, Côn đồng tâm, Cút, Chân rẽ Tap & Lan truyền BFS";
            btnCadToDuct.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnCadToDuct.Click += BtnCadToDuct_Click;

            // 10. Button Close
            btnClose = new Button();
            btnClose.Text = "Đóng (ESC)";
            btnClose.Location = new Point(16, 682);
            btnClose.Size = new Size(492, 38);
            btnClose.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            btnClose.Click += (s, e) => this.Close();
            this.CancelButton = btnClose;

            this.Controls.Add(pnlHeader);
            this.Controls.Add(grpInfo);
            this.Controls.Add(btnAutoCrop);
            this.Controls.Add(btnGrids);
            this.Controls.Add(btnLevel);
            this.Controls.Add(btnMepQuantity);
            this.Controls.Add(btnSheetList);
            this.Controls.Add(btnUpdateSheetList);
            this.Controls.Add(btnReloadTool);
            this.Controls.Add(btnSmart);
            this.Controls.Add(btnCadToDuct);
            this.Controls.Add(btnClose);

            this.ClientSize = new Size(524, 736);
            this.ResumeLayout(false);
        }

        private void BtnCadToDuct_Click(object sender, EventArgs e)
        {
            try
            {
                HDToolModelessHost.EnsureInitialized();
                CadToDuctForm form = new CadToDuctForm(_uidoc);
                OpenChildModeless(form);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("HDTOOL", "Lỗi mở Dựng Ống Gió Từ CAD: " + ex.Message);
            }
        }

        private void OpenChildModeless(System.Windows.Forms.Form childForm)
        {
            HDToolModelessHost.EnsureInitialized();
            IntPtr mainHwnd = IntPtr.Zero;
            try { mainHwnd = _uidoc.Application.MainWindowHandle; } catch { }
            this.Close();
            HDToolModelessHost.ShowModeless(childForm, mainHwnd);
        }

        private void BtnAutoCrop_Click(object sender, EventArgs e)
        {
            try
            {
                HDToolModelessHost.EnsureInitialized();
                AutoCropForm form = new AutoCropForm(_uidoc, HDToolModelessHost.ExEvent, HDToolModelessHost.Handler);
                OpenChildModeless(form);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("HDTOOL CROP VIEW", "Lỗi mở giao diện Tự Động Crop View: " + ex.Message);
            }
        }

        private void BtnMepQuantity_Click(object sender, EventArgs e)
        {
            try
            {
                MepInspectorExternalEventHandler handler = new MepInspectorExternalEventHandler();
                ExternalEvent extEvent = ExternalEvent.Create(handler);
                MepQuantityInspectorForm form = new MepQuantityInspectorForm(_uidoc, extEvent, handler);
                OpenChildModeless(form);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("HDTOOL MEP", "Lỗi mở giao diện Thống kê khối lượng MEP: " + ex.Message);
            }
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

        private void BtnReloadTool_Click(object sender, EventArgs e)
        {
            try
            {
                UpdateToolForm form = new UpdateToolForm(_uidoc.Application);
                OpenChildModeless(form);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("HDTOOL", "Lỗi mở cập nhật tool: " + ex.Message);
            }
        }

        private void BtnUpdateSheetList_Click(object sender, EventArgs e)
        {
            try
            {
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.Title = "Chọn File Excel hoặc CSV Đã Đổi Tên Bản Vẽ";
                ofd.Filter = "Tất cả file bảng tính (*.xls;*.xlsx;*.csv)|*.xls;*.xlsx;*.csv|" +
                             "Excel 97-2003 / XML (*.xls)|*.xls|" +
                             "Excel Workbook (*.xlsx)|*.xlsx|" +
                             "File CSV chuẩn UTF-8 (*.csv)|*.csv|" +
                             "Tất cả các file (*.*)|*.*";
                ofd.FilterIndex = 1;
                ofd.RestoreDirectory = true;

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    List<List<string>> rawRows = UpdateSheetListService.ReadRowsFromFile(ofd.FileName);
                    if (rawRows != null && rawRows.Count > 0)
                    {
                        List<SheetUpdateItem> items = UpdateSheetListService.MatchAndCompare(_doc, rawRows);
                        if (items.Count > 0)
                        {
                            UpdateSheetListPreviewForm preview = new UpdateSheetListPreviewForm(_doc, items);
                            OpenChildModeless(preview);
                            return;
                        }
                        else
                        {
                            TaskDialog.Show("Thông Báo", "Không tìm thấy số hiệu bản vẽ nào trong file khớp với dự án Revit.");
                        }
                    }
                    else
                    {
                        TaskDialog.Show("Thông Báo", "File đã chọn không có dữ liệu bảng tính hợp lệ.");
                    }
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Lỗi Cập Nhật", "Lỗi: " + ex.Message);
            }
            this.Close();
        }

        private void BtnSheetList_Click(object sender, EventArgs e)
        {
            try
            {
                ExportSheetListForm form = new ExportSheetListForm(_doc);
                OpenChildModeless(form);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("HDTOOL", "Lỗi mở Danh mục bản vẽ: " + ex.Message);
            }
        }

        private void BtnGrids_Click(object sender, EventArgs e)
        {
            try
            {
                HDToolModelessHost.EnsureInitialized();
                GridCropForm form = new GridCropForm(_uidoc, HDToolModelessHost.ExEvent, HDToolModelessHost.Handler);
                OpenChildModeless(form);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("HDTOOL", "Lỗi mở Căn Grids: " + ex.Message);
            }
        }

        private void BtnLevel_Click(object sender, EventArgs e)
        {
            try
            {
                LevelCropForm form = new LevelCropForm(_uidoc, new List<Level>());
                OpenChildModeless(form);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("HDTOOL", "Lỗi mở Căn Level: " + ex.Message);
            }
        }

        private void BtnSmart_Click(object sender, EventArgs e)
        {
            try
            {
                if (GridCropService.IsPlanView(_activeView) || _activeView is ViewSheet)
                {
                    HDToolModelessHost.EnsureInitialized();
                    GridCropForm form = new GridCropForm(_uidoc, HDToolModelessHost.ExEvent, HDToolModelessHost.Handler);
                    OpenChildModeless(form);
                    return;
                }

                string errorMsg;
                if (LevelCropService.IsValidContext(_activeView, out errorMsg))
                {
                    LevelCropForm form = new LevelCropForm(_uidoc, new List<Level>());
                    OpenChildModeless(form);
                    return;
                }

                TaskDialog.Show("HDTOOL", errorMsg);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("HDTOOL", "Lỗi: " + ex.Message);
            }
            this.Close();
        }
    }
}
