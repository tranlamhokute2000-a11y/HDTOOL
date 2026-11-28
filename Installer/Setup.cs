using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace AddinInstaller
{
    public class SetupForm : Form
    {
        private Panel pnlHeader;
        private PictureBox picLogo;
        private Label lblHeaderTitle;
        private Label lblHeaderSubtitle;
        private Panel pnlBorderLine;

        private GroupBox grpVersions;
        private CheckedListBox lstVersions;
        private Label lblVersionNote;

        private Button btnInstall;
        private Button btnUninstall;
        private Button btnOpenFolder;
        private Button btnExit;
        private Label lblStatus;
        private Label lblFooter;

        public SetupForm()
        {
            InitializeComponent();
            DetectRevitVersions();
        }

        private static string _appVersion;
        public static string AppVersion
        {
            get
            {
                if (_appVersion == null)
                {
                    try
                    {
                        Assembly asm = Assembly.GetExecutingAssembly();
                        using (Stream stream = asm.GetManifestResourceStream("version.txt"))
                        {
                            if (stream != null)
                            {
                                using (StreamReader reader = new StreamReader(stream))
                                {
                                    string val = reader.ReadToEnd().Trim();
                                    if (!string.IsNullOrEmpty(val))
                                    {
                                        _appVersion = "Version " + val;
                                        return _appVersion;
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                    _appVersion = "Version 1";
                }
                return _appVersion;
            }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.Text = string.Format("HadoMEE - Bộ Cài Đặt Add-in HDTOOL Cho Autodesk Revit ({0})", AppVersion);
            this.Size = new Size(570, 530);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(245, 247, 250);
            this.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

            // Set Window Icon from executable or disk
            try
            {
                this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                try
                {
                    string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                    if (File.Exists(iconPath))
                    {
                        this.Icon = new Icon(iconPath);
                    }
                }
                catch { }
            }

            // 1. Header Panel
            pnlHeader = new Panel();
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = 100;
            pnlHeader.BackColor = Color.White;

            picLogo = new PictureBox();
            picLogo.Size = new Size(260, 76);
            picLogo.Location = new Point(16, 12);
            picLogo.SizeMode = PictureBoxSizeMode.Zoom;
            picLogo.BackColor = Color.Transparent;

            // Load logo from embedded resource or disk
            Image logoImg = LoadEmbeddedImage("hadomee_logo.png");
            if (logoImg != null)
            {
                picLogo.Image = logoImg;
            }

            lblHeaderTitle = new Label();
            lblHeaderTitle.Text = string.Format("HDTOOL - BỘ CÔNG CỤ REVIT ({0})", AppVersion.ToUpper());
            lblHeaderTitle.ForeColor = Color.FromArgb(24, 43, 73);
            lblHeaderTitle.Font = new Font("Segoe UI", 12.0f, FontStyle.Bold);
            lblHeaderTitle.Location = new Point(288, 20);
            lblHeaderTitle.AutoSize = true;

            lblHeaderSubtitle = new Label();
            lblHeaderSubtitle.Text = "Căn chỉnh Level, Grids Crop View & Xuất Danh Mục Bản Vẽ\nBản cài đặt 1-File độc lập tiện lợi - HadoMEE Team";
            lblHeaderSubtitle.ForeColor = Color.FromArgb(100, 110, 120);
            lblHeaderSubtitle.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblHeaderSubtitle.Location = new Point(288, 50);
            lblHeaderSubtitle.AutoSize = true;

            pnlBorderLine = new Panel();
            pnlBorderLine.Dock = DockStyle.Bottom;
            pnlBorderLine.Height = 3;
            pnlBorderLine.BackColor = Color.FromArgb(192, 36, 40); // HadoMEE Red Accent

            pnlHeader.Controls.Add(picLogo);
            pnlHeader.Controls.Add(lblHeaderTitle);
            pnlHeader.Controls.Add(lblHeaderSubtitle);
            pnlHeader.Controls.Add(pnlBorderLine);

            // 2. Revit Versions Group
            grpVersions = new GroupBox();
            grpVersions.Text = "Chọn Các Phiên Bản Revit Muốn Cài Đặt";
            grpVersions.Location = new Point(18, 115);
            grpVersions.Size = new Size(518, 195);
            grpVersions.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            grpVersions.ForeColor = Color.FromArgb(44, 62, 80);

            lstVersions = new CheckedListBox();
            lstVersions.Location = new Point(15, 25);
            lstVersions.Size = new Size(488, 120);
            lstVersions.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            lstVersions.CheckOnClick = true;

            lblVersionNote = new Label();
            lblVersionNote.Text = "* Các phiên bản Revit tìm thấy trên máy sẽ được tự động tích chọn sẵn.";
            lblVersionNote.Font = new Font("Segoe UI", 8.0f, FontStyle.Italic);
            lblVersionNote.ForeColor = Color.Gray;
            lblVersionNote.Location = new Point(15, 155);
            lblVersionNote.Size = new Size(488, 20);

            grpVersions.Controls.Add(lstVersions);
            grpVersions.Controls.Add(lblVersionNote);

            // 3. Status label
            lblStatus = new Label();
            lblStatus.Location = new Point(18, 308);
            lblStatus.Size = new Size(518, 22);
            lblStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
            lblStatus.ForeColor = Color.FromArgb(39, 174, 96);
            lblStatus.Text = "✅ Đã nén sẵn dữ liệu cài đặt - Sẵn sàng thực hiện.";

            // 4. Action Buttons
            btnInstall = new Button();
            btnInstall.Text = "🚀  CÀI ĐẶT ADD-IN HDTOOL";
            btnInstall.Location = new Point(18, 338);
            btnInstall.Size = new Size(250, 48);
            btnInstall.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            btnInstall.BackColor = Color.FromArgb(192, 36, 40); // HadoMEE Red
            btnInstall.ForeColor = Color.White;
            btnInstall.FlatStyle = FlatStyle.Flat;
            btnInstall.FlatAppearance.BorderSize = 0;
            btnInstall.Cursor = Cursors.Hand;
            btnInstall.Click += BtnInstall_Click;

            btnUninstall = new Button();
            btnUninstall.Text = "🗑️  GỠ CÀI ĐẶT";
            btnUninstall.Location = new Point(286, 338);
            btnUninstall.Size = new Size(250, 48);
            btnUninstall.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            btnUninstall.BackColor = Color.FromArgb(30, 34, 99); // HadoMEE Navy Blue
            btnUninstall.ForeColor = Color.White;
            btnUninstall.FlatStyle = FlatStyle.Flat;
            btnUninstall.FlatAppearance.BorderSize = 0;
            btnUninstall.Cursor = Cursors.Hand;
            btnUninstall.Click += BtnUninstall_Click;

            btnOpenFolder = new Button();
            btnOpenFolder.Text = "📂  Mở Thư Mục Addins";
            btnOpenFolder.Location = new Point(18, 396);
            btnOpenFolder.Size = new Size(250, 34);
            btnOpenFolder.BackColor = Color.FromArgb(236, 240, 241);
            btnOpenFolder.FlatStyle = FlatStyle.Flat;
            btnOpenFolder.FlatAppearance.BorderColor = Color.FromArgb(189, 195, 199);
            btnOpenFolder.Click += BtnOpenFolder_Click;

            btnExit = new Button();
            btnExit.Text = "Đóng";
            btnExit.Location = new Point(286, 396);
            btnExit.Size = new Size(250, 34);
            btnExit.BackColor = Color.FromArgb(236, 240, 241);
            btnExit.FlatStyle = FlatStyle.Flat;
            btnExit.FlatAppearance.BorderColor = Color.FromArgb(189, 195, 199);
            btnExit.Click += (s, e) => this.Close();
            this.CancelButton = btnExit;

            // 5. Footer Label
            lblFooter = new Label();
            lblFooter.Text = string.Format("Bản quyền HadoMEE • Gói cài đặt 1-File độc lập ({0})", AppVersion);
            lblFooter.Font = new Font("Segoe UI", 8.0f, FontStyle.Italic);
            lblFooter.ForeColor = Color.Gray;
            lblFooter.TextAlign = ContentAlignment.MiddleCenter;
            lblFooter.Location = new Point(18, 442);
            lblFooter.Size = new Size(518, 22);

            this.Controls.Add(pnlHeader);
            this.Controls.Add(grpVersions);
            this.Controls.Add(lblStatus);
            this.Controls.Add(btnInstall);
            this.Controls.Add(btnUninstall);
            this.Controls.Add(btnOpenFolder);
            this.Controls.Add(btnExit);
            this.Controls.Add(lblFooter);

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

        private Image LoadEmbeddedImage(string resourceName)
        {
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream stream = asm.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        return Image.FromStream(stream);
                    }
                }

                // Fallback to local files
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] candidates = new string[] {
                    Path.Combine(baseDir, resourceName),
                    Path.Combine(baseDir, "Resources", resourceName),
                    Path.Combine(baseDir, "Installer", resourceName),
                    Path.Combine(baseDir, "..", "Resources", resourceName)
                };

                foreach (string c in candidates)
                {
                    if (File.Exists(c))
                    {
                        return Image.FromFile(c);
                    }
                }
            }
            catch { }
            return null;
        }

        private byte[] GetResourceBytes(string resourceName)
        {
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream stream = asm.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        byte[] buffer = new byte[stream.Length];
                        stream.Read(buffer, 0, buffer.Length);
                        return buffer;
                    }
                }
            }
            catch { }
            return null;
        }

        private string FindFileOnDisk(string fileName)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates = new string[] {
                Path.Combine(baseDir, fileName),
                Path.Combine(baseDir, "Build", fileName),
                Path.Combine(baseDir, "..", "Build", fileName),
                Path.Combine(baseDir, "..", fileName)
            };

            foreach (string c in candidates)
            {
                if (File.Exists(c)) return Path.GetFullPath(c);
            }
            return null;
        }

        private void DetectRevitVersions()
        {
            lstVersions.Items.Clear();

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string revitAddinsBase = Path.Combine(appData, "Autodesk", "Revit", "Addins");

            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string autodeskDir = Path.Combine(programFiles, "Autodesk");

            string[] knownYears = new string[] { "2027", "2026", "2025", "2024", "2023", "2022", "2021", "2020" };
            List<string> detectedYears = new List<string>();

            foreach (string year in knownYears)
            {
                bool found = false;

                // Kiểm tra thư mục cài đặt Revit
                string revitAppDir = Path.Combine(autodeskDir, "Revit " + year);
                if (Directory.Exists(revitAppDir)) found = true;

                // Kiểm tra thư mục Addins
                string addinDir = Path.Combine(revitAddinsBase, year);
                if (Directory.Exists(addinDir)) found = true;

                if (found)
                {
                    detectedYears.Add(year);
                    int idx = lstVersions.Items.Add(string.Format("Autodesk Revit {0}", year));
                    lstVersions.SetItemChecked(idx, true);
                }
            }

            if (detectedYears.Count == 0)
            {
                int idx = lstVersions.Items.Add("Autodesk Revit 2024 (Mặc định)");
                lstVersions.SetItemChecked(idx, true);
                lblStatus.Text = "Phát hiện cấu hình mặc định Revit 2024.";
            }
            else
            {
                lblStatus.Text = string.Format("✅ Tìm thấy {0} phiên bản Revit ({1}) sẵn sàng cài đặt.",
                    detectedYears.Count, string.Join(", ", detectedYears.ToArray()));
            }
        }

        private void BtnInstall_Click(object sender, EventArgs e)
        {
            // 1. Lấy dữ liệu DLL từ tài nguyên nhúng (Embedded) hoặc ổ đĩa
            byte[] dllBytes = GetResourceBytes("HDTOOL.dll");
            if (dllBytes == null)
            {
                string localDll = FindFileOnDisk("HDTOOL.dll") ?? FindFileOnDisk("LevelCropOffset.dll");
                if (localDll != null && File.Exists(localDll))
                {
                    dllBytes = File.ReadAllBytes(localDll);
                }
            }

            if (dllBytes == null || dllBytes.Length == 0)
            {
                MessageBox.Show(
                    "Không tìm thấy dữ liệu HDTOOL.dll!\nVui lòng đảm bảo file cài đặt không bị lỗi.",
                    "Lỗi Dữ Liệu",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            // 2. Lấy dữ liệu file .addin từ tài nguyên nhúng hoặc ổ đĩa
            byte[] addinBytes = GetResourceBytes("HDTOOL.addin");
            string addinContent = null;
            if (addinBytes != null && addinBytes.Length > 0)
            {
                addinContent = System.Text.Encoding.UTF8.GetString(addinBytes);
            }
            else
            {
                string localAddin = FindFileOnDisk("HDTOOL.addin");
                if (localAddin != null && File.Exists(localAddin))
                {
                    addinContent = File.ReadAllText(localAddin, System.Text.Encoding.UTF8);
                }
                else
                {
                    // Tự sinh nội dung manifest chuẩn
                    addinContent = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                        "<RevitAddIns>\r\n" +
                        "  <AddIn Type=\"Application\">\r\n" +
                        "    <Name>HDTOOL</Name>\r\n" +
                        "    <Assembly>HDTOOL\\HDTOOL.dll</Assembly>\r\n" +
                        "    <FullClassName>HDTool.App</FullClassName>\r\n" +
                        "    <ClientId>B7C59E12-4C10-4A82-96DE-57C894EF31A0</ClientId>\r\n" +
                        "    <VendorId>HDTOOL</VendorId>\r\n" +
                        "    <VendorDescription>HadoMEE BIM Development</VendorDescription>\r\n" +
                        "  </AddIn>\r\n" +
                        "  <AddIn Type=\"Command\">\r\n" +
                        "    <Name>HDTOOL - Căn Chỉnh Grids</Name>\r\n" +
                        "    <Assembly>HDTOOL\\HDTOOL.dll</Assembly>\r\n" +
                        "    <FullClassName>HDTool.GridCropCommand</FullClassName>\r\n" +
                        "    <ClientId>E1F30C4D-2374-4C75-A8F8-690DF27E1C38</ClientId>\r\n" +
                        "    <VendorId>HDTOOL</VendorId>\r\n" +
                        "    <VendorDescription>HadoMEE BIM Development</VendorDescription>\r\n" +
                        "  </AddIn>\r\n" +
                        "  <AddIn Type=\"Command\">\r\n" +
                        "    <Name>HDTOOL - Căn Chỉnh Level</Name>\r\n" +
                        "    <Assembly>HDTOOL\\HDTOOL.dll</Assembly>\r\n" +
                        "    <FullClassName>HDTool.LevelCropCommand</FullClassName>\r\n" +
                        "    <ClientId>7E659A1D-318D-4F2A-85F0-5C2BA267DF6C</ClientId>\r\n" +
                        "    <VendorId>HDTOOL</VendorId>\r\n" +
                        "    <VendorDescription>HadoMEE BIM Development</VendorDescription>\r\n" +
                        "  </AddIn>\r\n" +
                        "  <AddIn Type=\"Command\">\r\n" +
                        "    <Name>HDTOOL - Menu Tùy Chọn</Name>\r\n" +
                        "    <Assembly>HDTOOL\\HDTOOL.dll</Assembly>\r\n" +
                        "    <FullClassName>HDTool.HDToolMainCommand</FullClassName>\r\n" +
                        "    <ClientId>A42BF97D-0C7A-4598-BE95-A7A890CF6C5D</ClientId>\r\n" +
                        "    <VendorId>HDTOOL</VendorId>\r\n" +
                        "    <VendorDescription>HadoMEE BIM Development</VendorDescription>\r\n" +
                        "  </AddIn>\r\n" +
                        "  <AddIn Type=\"Command\">\r\n" +
                        "    <Name>HDTOOL - Tự Động Căn</Name>\r\n" +
                        "    <Assembly>HDTOOL\\HDTOOL.dll</Assembly>\r\n" +
                        "    <FullClassName>HDTool.SmartCropCommand</FullClassName>\r\n" +
                        "    <ClientId>2D7C8A9B-4321-4E7B-9A23-C5E6D7E8F9A0</ClientId>\r\n" +
                        "    <VendorId>HDTOOL</VendorId>\r\n" +
                        "    <VendorDescription>HadoMEE BIM Development</VendorDescription>\r\n" +
                        "  </AddIn>\r\n" +
                        "  <AddIn Type=\"Command\">\r\n" +
                        "    <Name>HDTOOL - Xuất Danh Mục Bản Vẽ</Name>\r\n" +
                        "    <Assembly>HDTOOL\\HDTOOL.dll</Assembly>\r\n" +
                        "    <FullClassName>HDTool.ExportSheetListCommand</FullClassName>\r\n" +
                        "    <ClientId>9F3A1C2E-5B4D-4A78-8E6C-3D2E1F0A9B8C</ClientId>\r\n" +
                        "    <VendorId>HDTOOL</VendorId>\r\n" +
                        "    <VendorDescription>HadoMEE BIM Development</VendorDescription>\r\n" +
                        "  </AddIn>\r\n" +
                        "  <AddIn Type=\"Command\">\r\n" +
                        "    <Name>HDTOOL - Cập Nhật Danh Mục Bản Vẽ</Name>\r\n" +
                        "    <Assembly>HDTOOL\\HDTOOL.dll</Assembly>\r\n" +
                        "    <FullClassName>HDTool.UpdateSheetListCommand</FullClassName>\r\n" +
                        "    <ClientId>A3C8B7D1-84E2-4F15-9A6D-0C7E5F2B1A9D</ClientId>\r\n" +
                        "    <VendorId>HDTOOL</VendorId>\r\n" +
                        "    <VendorDescription>HadoMEE BIM Development</VendorDescription>\r\n" +
                        "  </AddIn>\r\n" +
                        "</RevitAddIns>";
                }
            }

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string baseAddinsDir = Path.Combine(appData, "Autodesk", "Revit", "Addins");

            List<string> installedYears = new List<string>();

            for (int i = 0; i < lstVersions.Items.Count; i++)
            {
                if (lstVersions.GetItemChecked(i))
                {
                    string itemText = lstVersions.Items[i].ToString();
                    string year = itemText.Split(' ')[2]; // "Autodesk Revit 2024..."

                    string targetYearDir = Path.Combine(baseAddinsDir, year);
                    if (!Directory.Exists(targetYearDir))
                    {
                        Directory.CreateDirectory(targetYearDir);
                    }

                    string targetPluginDir = Path.Combine(targetYearDir, "HDTOOL");
                    if (!Directory.Exists(targetPluginDir))
                    {
                        Directory.CreateDirectory(targetPluginDir);
                    }

                    string targetDll = Path.Combine(targetPluginDir, "HDTOOL.dll");

                    // Ghi đè file DLL (có xử lý nếu file đang bị Revit khóa)
                    try
                    {
                        File.WriteAllBytes(targetDll, dllBytes);
                    }
                    catch (IOException)
                    {
                        try
                        {
                            string backupOld = Path.Combine(targetPluginDir, "HDTOOL_old_" + Guid.NewGuid().ToString().Substring(0, 8) + ".dll");
                            File.Move(targetDll, backupOld);
                            File.WriteAllBytes(targetDll, dllBytes);
                        }
                        catch { }
                    }

                    // Ghi file HDTOOL.addin
                    string targetAddin = Path.Combine(targetYearDir, "HDTOOL.addin");
                    File.WriteAllText(targetAddin, addinContent, System.Text.Encoding.UTF8);

                    // Xóa file cũ LevelCropOffset nếu có để không bị xung đột
                    try
                    {
                        string oldAddin = Path.Combine(targetYearDir, "LevelCropOffset.addin");
                        if (File.Exists(oldAddin)) File.Delete(oldAddin);
                        string oldPluginDir = Path.Combine(targetYearDir, "LevelCropOffset");
                        if (Directory.Exists(oldPluginDir)) Directory.Delete(oldPluginDir, true);
                    }
                    catch { }

                    installedYears.Add(year);
                }
            }

            if (installedYears.Count > 0)
            {
                lblStatus.Text = "✅ Đã cài đặt HDTOOL thành công cho: Revit " + string.Join(", ", installedYears.ToArray());
                lblStatus.ForeColor = Color.FromArgb(39, 174, 96);

                MessageBox.Show(
                    "CÀI ĐẶT THÀNH CÔNG!\n\n" +
                    "Add-in HDTOOL (HadoMEE) đã được cài đặt cho: Revit " + string.Join(", ", installedYears.ToArray()) + ".\n\n" +
                    "Khi mở Autodesk Revit, bạn sẽ thấy Ribbon Tab 'HDTOOL' với các công cụ:\n" +
                    "  • Căn Chỉnh Grids (Mặt bằng & Sheet)\n" +
                    "  • Căn Chỉnh Level (Mặt cắt, Mặt đứng & Sheet)\n" +
                    "  • Xuất Danh Mục Bản Vẽ (Sheet List sang Excel / CSV / Clipboard)\n" +
                    "  • Cập Nhật Danh Mục (Đổi tên trong Excel ➔ Đồng bộ vào Revit)\n" +
                    "  • Menu HDTOOL (Tự do lựa chọn công cụ)\n" +
                    "  • Tự Động Căn (Smart Align)\n\n" +
                    "Chúc bạn làm việc hiệu quả cùng HadoMEE!",
                    "HadoMEE - Cài Đặt Thành Công",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Vui lòng tích chọn ít nhất 1 phiên bản Revit để cài đặt.", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BtnUninstall_Click(object sender, EventArgs e)
        {
            DialogResult dr = MessageBox.Show(
                "Bạn có chắc chắn muốn gỡ cài đặt Add-in HDTOOL khỏi các phiên bản Revit đã chọn?",
                "Xác Nhận Gỡ Bỏ",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (dr != DialogResult.Yes) return;

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string baseAddinsDir = Path.Combine(appData, "Autodesk", "Revit", "Addins");

            List<string> uninstalledYears = new List<string>();

            for (int i = 0; i < lstVersions.Items.Count; i++)
            {
                if (lstVersions.GetItemChecked(i))
                {
                    string itemText = lstVersions.Items[i].ToString();
                    string year = itemText.Split(' ')[2];

                    string targetYearDir = Path.Combine(baseAddinsDir, year);
                    string targetPluginDir = Path.Combine(targetYearDir, "HDTOOL");
                    string targetAddin = Path.Combine(targetYearDir, "HDTOOL.addin");

                    try
                    {
                        if (File.Exists(targetAddin)) File.Delete(targetAddin);
                        if (Directory.Exists(targetPluginDir)) Directory.Delete(targetPluginDir, true);

                        string oldAddin = Path.Combine(targetYearDir, "LevelCropOffset.addin");
                        if (File.Exists(oldAddin)) File.Delete(oldAddin);
                        string oldPluginDir = Path.Combine(targetYearDir, "LevelCropOffset");
                        if (Directory.Exists(oldPluginDir)) Directory.Delete(oldPluginDir, true);

                        uninstalledYears.Add(year);
                    }
                    catch { }
                }
            }

            if (uninstalledYears.Count > 0)
            {
                lblStatus.Text = "Đã gỡ bỏ HDTOOL khỏi Revit " + string.Join(", ", uninstalledYears.ToArray());
                lblStatus.ForeColor = Color.FromArgb(231, 76, 60);

                MessageBox.Show(
                    "Đã gỡ bỏ Add-in HDTOOL thành công khỏi: Revit " + string.Join(", ", uninstalledYears.ToArray()),
                    "Gỡ Cài Đặt Hoàn Tất",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private void BtnOpenFolder_Click(object sender, EventArgs e)
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string baseAddinsDir = Path.Combine(appData, "Autodesk", "Revit", "Addins");
            if (!Directory.Exists(baseAddinsDir))
            {
                Directory.CreateDirectory(baseAddinsDir);
            }
            Process.Start("explorer.exe", baseAddinsDir);
        }

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm());
        }
    }
}
