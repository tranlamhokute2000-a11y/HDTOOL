using Autodesk.Revit.UI;
using TextBox = System.Windows.Forms.TextBox;
using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Color = System.Drawing.Color;
using Font = System.Drawing.Font;
using Point = System.Drawing.Point;

namespace HDTool
{
    public class UpdateToolForm : HDToolBaseForm
    {
        private System.Windows.Forms.Panel pnlHeader;
        private Label lblTitle;
        private Label lblSubtitle;
        private PictureBox picLogo;

        private GroupBox grpCurrent;
        private Label lblCurrentStatus;

        private GroupBox grpTarget;
        private TextBox txtFilePath;
        private Button btnBrowse;
        private Label lblTargetInfo;

        private Button btnApply;
        private Button btnClose;

                private string _selectedFilePath = string.Empty;
        private UIApplication _uiapp = null;

        public UpdateToolForm(UIApplication uiapp = null)
        {
            _uiapp = uiapp;
            InitializeComponent();
            DetectInitialFile();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.Text = "HDTOOL - Cập Nhật Tính Năng Mới Vào Revit (Hot Reload)";
            this.Size = new Size(580, 480);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.TopMost = true;
            this.BackColor = Color.FromArgb(245, 247, 250);
            this.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

            // 1. Header Panel
            pnlHeader = new System.Windows.Forms.Panel();
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = 75;
            pnlHeader.BackColor = Color.FromArgb(24, 43, 73); // Navy HadoMEE

            lblTitle = new Label();
            lblTitle.Text = "HDTOOL - CẬP NHẬT TÍNH NĂNG MỚI";
            lblTitle.ForeColor = Color.White;
            lblTitle.Font = new Font("Segoe UI", 12.0f, FontStyle.Bold);
            lblTitle.Location = new Point(16, 14);
            lblTitle.AutoSize = true;

            lblSubtitle = new Label();
            lblSubtitle.Text = "Nạp trực tiếp DLL mới vào Revit đang mở - Không cần tắt bản vẽ hay khởi động lại";
            lblSubtitle.ForeColor = Color.FromArgb(189, 195, 199);
            lblSubtitle.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblSubtitle.Location = new Point(16, 42);
            lblSubtitle.AutoSize = true;

            picLogo = new PictureBox();
            picLogo.Size = new Size(130, 42);
            picLogo.Location = new Point(420, 16);
            picLogo.SizeMode = PictureBoxSizeMode.Zoom;
            picLogo.BackColor = Color.White;
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("hadomee_logo.png"))
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

            // 2. GroupBox Hiện Tại
            grpCurrent = new GroupBox();
            grpCurrent.Text = "Trạng Thái Phiên Bản Đang Hoạt Động";
            grpCurrent.Location = new Point(16, 85);
            grpCurrent.Size = new Size(532, 85);
            grpCurrent.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            grpCurrent.ForeColor = Color.FromArgb(44, 62, 80);

            lblCurrentStatus = new Label();
            lblCurrentStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblCurrentStatus.ForeColor = Color.FromArgb(52, 73, 94);
            lblCurrentStatus.Location = new Point(14, 22);
            lblCurrentStatus.Size = new Size(505, 55);

            UpdateCurrentStatusLabel();
            grpCurrent.Controls.Add(lblCurrentStatus);

            // 3. GroupBox Chọn File Mới
            grpTarget = new GroupBox();
            grpTarget.Text = "Chỉ Định File HDTOOL.dll Cập Nhật Mới";
            grpTarget.Location = new Point(16, 180);
            grpTarget.Size = new Size(532, 160);
            grpTarget.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            grpTarget.ForeColor = Color.FromArgb(44, 62, 80);

            Label lblPathDesc = new Label();
            lblPathDesc.Text = "Đường dẫn file DLL mới (Tự động tìm kiếm hoặc nhấn Duyệt File):";
            lblPathDesc.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblPathDesc.ForeColor = Color.FromArgb(127, 140, 141);
            lblPathDesc.Location = new Point(14, 24);
            lblPathDesc.AutoSize = true;

            txtFilePath = new TextBox();
            txtFilePath.Location = new Point(14, 48);
            txtFilePath.Size = new Size(385, 23);
            txtFilePath.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
            txtFilePath.TextChanged += TxtFilePath_TextChanged;

            btnBrowse = new Button();
            btnBrowse.Text = "📁  Duyệt File...";
            btnBrowse.Location = new Point(405, 46);
            btnBrowse.Size = new Size(115, 27);
            btnBrowse.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            btnBrowse.BackColor = Color.FromArgb(236, 240, 241);
            btnBrowse.ForeColor = Color.FromArgb(44, 62, 80);
            btnBrowse.FlatStyle = FlatStyle.Flat;
            btnBrowse.Cursor = Cursors.Hand;
            btnBrowse.Click += BtnBrowse_Click;

            lblTargetInfo = new Label();
            lblTargetInfo.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblTargetInfo.ForeColor = Color.FromArgb(39, 174, 96);
            lblTargetInfo.Location = new Point(14, 82);
            lblTargetInfo.Size = new Size(505, 68);

            grpTarget.Controls.Add(lblPathDesc);
            grpTarget.Controls.Add(txtFilePath);
            grpTarget.Controls.Add(btnBrowse);
            grpTarget.Controls.Add(lblTargetInfo);

            // 4. Action Buttons
            btnApply = new Button();
            btnApply.Text = "🚀   NẠP & CẬP NHẬT NGAY VÀO REVIT";
            btnApply.Location = new Point(16, 355);
            btnApply.Size = new Size(390, 48);
            btnApply.Font = new Font("Segoe UI", 10.0f, FontStyle.Bold);
            btnApply.BackColor = Color.FromArgb(39, 174, 96); // Green Accent
            btnApply.ForeColor = Color.White;
            btnApply.FlatStyle = FlatStyle.Flat;
            btnApply.FlatAppearance.BorderSize = 0;
            btnApply.Cursor = Cursors.Hand;
            btnApply.Click += BtnApply_Click;

            btnClose = new Button();
            btnClose.Text = "Đóng";
            btnClose.Location = new Point(415, 355);
            btnClose.Size = new Size(133, 48);
            btnClose.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            btnClose.BackColor = Color.FromArgb(236, 240, 241);
            btnClose.ForeColor = Color.FromArgb(44, 62, 80);
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.Cursor = Cursors.Hand;
            btnClose.Click += (s, e) => this.Close();

            this.Controls.Add(pnlHeader);
            this.Controls.Add(grpCurrent);
            this.Controls.Add(grpTarget);
            this.Controls.Add(btnApply);
            this.Controls.Add(btnClose);

            this.AcceptButton = btnApply;
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

        private void UpdateCurrentStatusLabel()
        {
            if (ToolHotReloadManager.ActiveAssembly != null)
            {
                lblCurrentStatus.Text = string.Format(
                    "• Trạng thái: Đang chạy phiên bản CẬP NHẬT TRỰC TIẾP (Hot Reload)\n" +
                    "• Nguồn file: {0}\n" +
                    "• Thời gian nạp: {1:dd/MM/yyyy HH:mm:ss}",
                    ToolHotReloadManager.ActivePath ?? "Bộ nhớ RAM",
                    ToolHotReloadManager.ActiveTime ?? DateTime.Now
                );
            }
            else
            {
                string loc = Assembly.GetExecutingAssembly().Location;
                DateTime dt = File.Exists(loc) ? File.GetLastWriteTime(loc) : DateTime.Now;
                lblCurrentStatus.Text = string.Format(
                    "• Trạng thái: Đang chạy phiên bản nạp khi khởi động Revit\n" +
                    "• Vị trí gốc: {0}\n" +
                    "• Ngày biên dịch: {1:dd/MM/yyyy HH:mm:ss}",
                    Path.GetFileName(loc),
                    dt
                );
            }
        }

        private void DetectInitialFile()
        {
            FileInfo newest = ToolHotReloadManager.FindLatestBuildDll();
            if (newest != null)
            {
                txtFilePath.Text = newest.FullName;
            }
            else
            {
                txtFilePath.Text = @"c:\Users\Nguye\Desktop\ADDIN REVIT\Build\HDTOOL.dll";
            }
        }

        private void TxtFilePath_TextChanged(object sender, EventArgs e)
        {
            _selectedFilePath = txtFilePath.Text.Trim();
            RefreshTargetFileInfo();
        }

        private void RefreshTargetFileInfo()
        {
            if (string.IsNullOrEmpty(_selectedFilePath) || !File.Exists(_selectedFilePath))
            {
                lblTargetInfo.ForeColor = Color.FromArgb(192, 57, 43); // Red
                lblTargetInfo.Text = "⚠ Không tìm thấy file tại đường dẫn này. Vui lòng bấm 'Duyệt File...' để chọn.";
                btnApply.Enabled = false;
                btnApply.BackColor = Color.FromArgb(189, 195, 199);
                return;
            }

            try
            {
                FileInfo fi = new FileInfo(_selectedFilePath);
                lblTargetInfo.ForeColor = Color.FromArgb(39, 174, 96); // Green
                lblTargetInfo.Text = string.Format(
                    "✔ Đã tìm thấy file sẵn sàng nạp!\n" +
                    "• Kích thước: {0:N0} bytes ({1:N1} KB)\n" +
                    "• Thời gian build: {2:dd/MM/yyyy HH:mm:ss} ({3})\n" +
                    "• Nhấn 'NẠP & CẬP NHẬT NGAY' để áp dụng tức thì vào Revit hiện tại.",
                    fi.Length,
                    fi.Length / 1024.0,
                    fi.LastWriteTime,
                    GetTimeSpanDescription(DateTime.Now - fi.LastWriteTime)
                );
                btnApply.Enabled = true;
                btnApply.BackColor = Color.FromArgb(39, 174, 96);
            }
            catch (Exception ex)
            {
                lblTargetInfo.ForeColor = Color.FromArgb(192, 57, 43);
                lblTargetInfo.Text = "Lỗi đọc thông tin file: " + ex.Message;
                btnApply.Enabled = false;
                btnApply.BackColor = Color.FromArgb(189, 195, 199);
            }
        }

        private string GetTimeSpanDescription(TimeSpan span)
        {
            if (span.TotalSeconds < 60) return "vừa tạo cách đây vài giây";
            if (span.TotalMinutes < 60) return string.Format("cách đây {0} phút", (int)span.TotalMinutes);
            if (span.TotalHours < 24) return string.Format("cách đây {0} giờ", (int)span.TotalHours);
            return string.Format("cách đây {0} ngày", (int)span.TotalDays);
        }

        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "HDTOOL - Chọn File HDTOOL.dll Vừa Cập Nhật Để Nạp Vào Revit";
                ofd.Filter = "HDTOOL Assembly (*.dll)|*.dll|Tất cả các tệp (*.*)|*.*";
                ofd.FilterIndex = 1;
                ofd.RestoreDirectory = true;

                if (!string.IsNullOrEmpty(_selectedFilePath) && File.Exists(_selectedFilePath))
                {
                    ofd.InitialDirectory = Path.GetDirectoryName(_selectedFilePath);
                    ofd.FileName = Path.GetFileName(_selectedFilePath);
                }
                else
                {
                    ofd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                }

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    txtFilePath.Text = ofd.FileName;
                }
            }
        }

        private void BtnApply_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedFilePath) || !File.Exists(_selectedFilePath))
            {
                MessageBox.Show("Vui lòng chọn một file DLL hợp lệ trước khi nạp.",
                    "HDTOOL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string statusMsg;
                        bool success = ToolHotReloadManager.ReloadAssembly(_selectedFilePath, out statusMsg);
            if (success && _uiapp != null)
            {
                App.EnsureAutoCropButtonDynamic(_uiapp);
            }

            if (success)
            {
                FileInfo fi = new FileInfo(_selectedFilePath);
                string msg = string.Format(
                    "CẬP NHẬT TÍNH NĂNG MỚI THÀNH CÔNG!\n\n" +
                    "• File đã nạp: {0}\n" +
                    "• Dung lượng: {1:N0} bytes\n" +
                    "• Thời gian build: {2:dd/MM/yyyy HH:mm:ss}\n\n" +
                    "✔ Phiên bản mới đã được nạp trực tiếp vào phiên Revit hiện tại.\n" +
                    "✔ Các nút bấm trên thanh công cụ HDTOOL (Grids, Level, Xuất/Cập Nhật Danh Mục...) đã được kích hoạt tính năng mới NGAY LẬP TỨC!\n" +
                    "✔ Bạn KHÔNG CẦN tắt Revit hay mở lại file bản vẽ.",
                    Path.GetFileName(_selectedFilePath),
                    fi.Length,
                    fi.LastWriteTime
                );

                MessageBox.Show(msg, "HDTOOL - Cập Nhật Thành Công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                MessageBox.Show("Không thể nạp file cập nhật:\n\n" + statusMsg,
                    "HDTOOL - Lỗi Cập Nhật", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
