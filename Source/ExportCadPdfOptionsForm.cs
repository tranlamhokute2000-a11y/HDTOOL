using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Color = System.Drawing.Color;
using Font = System.Drawing.Font;
using Point = System.Drawing.Point;

namespace HDTool
{
    public enum ExportTargetFormat
    {
        Pdf,
        Cad
    }

    public class ExportCadPdfOptionsForm : System.Windows.Forms.Form
    {
        private Document _doc;
        private List<SheetItem> _sheets;
        private ExportTargetFormat _initialFormat;
        private string _groupTitle;

        // UI Controls
        private System.Windows.Forms.Panel pnlHeader;
        private Label lblTitle;
        private Label lblSubtitle;
        private PictureBox picLogo;

        private GroupBox grpList;
        private ListBox lstSheets;
        private Label lblListInfo;

        private GroupBox grpFormat;
        private RadioButton radPdf;
        private RadioButton radCad;
        private CheckBox chkPdfCombine;
        private Label lblFileName;
        private TextBox txtPdfFileName;
        private CheckBox chkCadCombineOne;
        private Label lblCadFileName;
        private TextBox txtCadFileName;
        private CheckBox chkCadMerge;

        private GroupBox grpFolder;
        private TextBox txtFolder;
        private Button btnBrowseFolder;
        private CheckBox chkOpenFolderAfter;

        private Button btnStartExport;
        private Button btnCancel;

        public ExportCadPdfOptionsForm(Document doc, List<SheetItem> sheets, ExportTargetFormat initialFormat, string groupTitle)
        {
            _doc = doc;
            _sheets = sheets ?? new List<SheetItem>();
            _initialFormat = initialFormat;
            _groupTitle = groupTitle ?? string.Empty;

            InitializeComponent();
            PopulateSheetList();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.Text = "HDTOOL - Xuất Hồ Sơ Bản Vẽ (PDF & CAD)";
            this.Size = new Size(650, 725);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.TopMost = true;
            this.BackColor = Color.FromArgb(245, 247, 250);
            this.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

            // 1. Header
            pnlHeader = new System.Windows.Forms.Panel();
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = 75;
            pnlHeader.BackColor = Color.FromArgb(24, 43, 73);

            lblTitle = new Label();
            lblTitle.Text = "HDTOOL - XUẤT HỒ SƠ BẢN VẼ (PDF & CAD)";
            lblTitle.ForeColor = Color.White;
            lblTitle.Font = new Font("Segoe UI", 12.0f, FontStyle.Bold);
            lblTitle.Location = new Point(16, 14);
            lblTitle.AutoSize = true;

            lblSubtitle = new Label();
            lblSubtitle.Text = "Xuất các bản vẽ theo đúng thứ tự tùy biến bạn đã chọn";
            lblSubtitle.ForeColor = Color.FromArgb(189, 195, 199);
            lblSubtitle.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblSubtitle.Location = new Point(16, 42);
            lblSubtitle.AutoSize = true;

            picLogo = new PictureBox();
            picLogo.Size = new Size(130, 42);
            picLogo.Location = new Point(490, 16);
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

            // 2. Sheet List Group
            grpList = new GroupBox();
            grpList.Text = string.Format("Danh Sách Bản Vẽ Sắp Xuất ({0} Bản Vẽ)", _sheets.Count);
            grpList.Location = new Point(16, 85);
            grpList.Size = new Size(602, 180);
            grpList.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            grpList.ForeColor = Color.FromArgb(44, 62, 80);

            lblListInfo = new Label();
            lblListInfo.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
            lblListInfo.ForeColor = Color.FromArgb(41, 128, 185);
            lblListInfo.Location = new Point(12, 22);
            lblListInfo.Size = new Size(578, 20);
            lblListInfo.Text = "• Thứ tự xuất bảo toàn chính xác theo thứ tự bạn vừa sắp xếp:";

            lstSheets = new ListBox();
            lstSheets.Location = new Point(12, 45);
            lstSheets.Size = new Size(578, 120);
            lstSheets.Font = new Font("Consolas", 9.0f, FontStyle.Regular);
            lstSheets.BackColor = Color.White;
            lstSheets.ForeColor = Color.FromArgb(44, 62, 80);

            grpList.Controls.Add(lblListInfo);
            grpList.Controls.Add(lstSheets);

            // 3. Format Options Group
            grpFormat = new GroupBox();
            grpFormat.Text = "Định Dạng & Tùy Chọn Xuất";
            grpFormat.Location = new Point(16, 275);
            grpFormat.Size = new Size(602, 225);
            grpFormat.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            grpFormat.ForeColor = Color.FromArgb(44, 62, 80);

            radPdf = new RadioButton();
            radPdf.Text = "📄  Xuất File PDF (.pdf)";
            radPdf.Location = new Point(18, 22);
            radPdf.Size = new Size(200, 24);
            radPdf.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            radPdf.ForeColor = Color.FromArgb(192, 57, 43); // Red
            radPdf.Checked = (_initialFormat == ExportTargetFormat.Pdf);
            radPdf.CheckedChanged += RadFormat_CheckedChanged;

            chkPdfCombine = new CheckBox();
            chkPdfCombine.Text = "Gom tất cả bản vẽ vào 1 file PDF duy nhất (Combined PDF)";
            chkPdfCombine.Checked = true;
            chkPdfCombine.Location = new Point(38, 48);
            chkPdfCombine.Size = new Size(450, 22);
            chkPdfCombine.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            chkPdfCombine.CheckedChanged += (s, e) => { txtPdfFileName.Enabled = chkPdfCombine.Checked; };

            lblFileName = new Label();
            lblFileName.Text = "Tên file PDF gộp:";
            lblFileName.Location = new Point(38, 73);
            lblFileName.Size = new Size(110, 22);
            lblFileName.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);

            txtPdfFileName = new TextBox();
            txtPdfFileName.Location = new Point(155, 70);
            txtPdfFileName.Size = new Size(330, 23);
            txtPdfFileName.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
            string defaultName = !string.IsNullOrEmpty(_groupTitle) ? _groupTitle : "HoSoBanVe_" + DateTime.Now.ToString("yyyyMMdd");
            txtPdfFileName.Text = defaultName;

            radCad = new RadioButton();
            radCad.Text = "📐  Xuất File AutoCAD (.dwg)";
            radCad.Location = new Point(18, 102);
            radCad.Size = new Size(250, 24);
            radCad.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            radCad.ForeColor = Color.FromArgb(24, 43, 73); // Navy
            radCad.Checked = (_initialFormat == ExportTargetFormat.Cad);
            radCad.CheckedChanged += RadFormat_CheckedChanged;

            chkCadCombineOne = new CheckBox();
            chkCadCombineOne.Text = "Gộp tất cả bản vẽ vào CÙNG 1 FILE CAD DUY NHẤT (Có đầy đủ khung tên)";
            chkCadCombineOne.Checked = true;
            chkCadCombineOne.Location = new Point(38, 128);
            chkCadCombineOne.Size = new Size(520, 22);
            chkCadCombineOne.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            chkCadCombineOne.ForeColor = Color.FromArgb(31, 58, 96);
            chkCadCombineOne.CheckedChanged += (s, e) => { txtCadFileName.Enabled = chkCadCombineOne.Checked; };

            lblCadFileName = new Label();
            lblCadFileName.Text = "Tên file CAD gộp:";
            lblCadFileName.Location = new Point(38, 153);
            lblCadFileName.Size = new Size(110, 22);
            lblCadFileName.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);

            txtCadFileName = new TextBox();
            txtCadFileName.Location = new Point(155, 150);
            txtCadFileName.Size = new Size(330, 23);
            txtCadFileName.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
            txtCadFileName.Text = defaultName;

            chkCadMerge = new CheckBox();
            chkCadMerge.Text = "Gộp các liên kết ngoài Xref trực tiếp vào bản vẽ CAD (Merged Views)";
            chkCadMerge.Checked = true;
            chkCadMerge.Location = new Point(38, 182);
            chkCadMerge.Size = new Size(520, 22);
            chkCadMerge.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);

            grpFormat.Controls.Add(radPdf);
            grpFormat.Controls.Add(chkPdfCombine);
            grpFormat.Controls.Add(lblFileName);
            grpFormat.Controls.Add(txtPdfFileName);
            grpFormat.Controls.Add(radCad);
            grpFormat.Controls.Add(chkCadCombineOne);
            grpFormat.Controls.Add(lblCadFileName);
            grpFormat.Controls.Add(txtCadFileName);
            grpFormat.Controls.Add(chkCadMerge);

            // 4. Target Folder Group
            grpFolder = new GroupBox();
            grpFolder.Text = "Thư Mục Lưu Trữ File Xuất";
            grpFolder.Location = new Point(16, 510);
            grpFolder.Size = new Size(602, 95);
            grpFolder.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            grpFolder.ForeColor = Color.FromArgb(44, 62, 80);

            txtFolder = new TextBox();
            txtFolder.Location = new Point(18, 26);
            txtFolder.Size = new Size(460, 23);
            txtFolder.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string defaultExportDir = Path.Combine(desktop, "HDTOOL_Exports");
            txtFolder.Text = defaultExportDir;

            btnBrowseFolder = new Button();
            btnBrowseFolder.Text = "📁  Duyệt...";
            btnBrowseFolder.Location = new Point(488, 24);
            btnBrowseFolder.Size = new Size(100, 27);
            btnBrowseFolder.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            btnBrowseFolder.BackColor = Color.FromArgb(236, 240, 241);
            btnBrowseFolder.ForeColor = Color.FromArgb(44, 62, 80);
            btnBrowseFolder.FlatStyle = FlatStyle.Flat;
            btnBrowseFolder.Cursor = Cursors.Hand;
            btnBrowseFolder.Click += BtnBrowseFolder_Click;

            chkOpenFolderAfter = new CheckBox();
            chkOpenFolderAfter.Text = "Tự động mở thư mục / file sau khi xuất xong";
            chkOpenFolderAfter.Checked = true;
            chkOpenFolderAfter.Location = new Point(18, 58);
            chkOpenFolderAfter.Size = new Size(350, 22);
            chkOpenFolderAfter.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);

            grpFolder.Controls.Add(txtFolder);
            grpFolder.Controls.Add(btnBrowseFolder);
            grpFolder.Controls.Add(chkOpenFolderAfter);

            // 5. Action Buttons
            btnStartExport = new Button();
            btnStartExport.Text = string.Format("🚀   BẮT ĐẦU XUẤT ({0} BẢN VẼ)", _sheets.Count);
            btnStartExport.Location = new Point(16, 618);
            btnStartExport.Size = new Size(450, 48);
            btnStartExport.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            btnStartExport.BackColor = Color.FromArgb(39, 174, 96);
            btnStartExport.ForeColor = Color.White;
            btnStartExport.FlatStyle = FlatStyle.Flat;
            btnStartExport.FlatAppearance.BorderSize = 0;
            btnStartExport.Cursor = Cursors.Hand;
            btnStartExport.Click += BtnStartExport_Click;

            btnCancel = new Button();
            btnCancel.Text = "Đóng";
            btnCancel.Location = new Point(478, 618);
            btnCancel.Size = new Size(140, 48);
            btnCancel.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            btnCancel.BackColor = Color.FromArgb(236, 240, 241);
            btnCancel.ForeColor = Color.FromArgb(44, 62, 80);
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.Cursor = Cursors.Hand;
            btnCancel.Click += (s, e) => this.Close();

            this.Controls.Add(pnlHeader);
            this.Controls.Add(grpList);
            this.Controls.Add(grpFormat);
            this.Controls.Add(grpFolder);
            this.Controls.Add(btnStartExport);
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnStartExport;
            this.CancelButton = btnCancel;

            RadFormat_CheckedChanged(null, null);

            this.ResumeLayout(false);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void PopulateSheetList()
        {
            lstSheets.Items.Clear();
            var groups = _sheets.GroupBy(delegate(SheetItem s) { return s.GroupTitle ?? string.Empty; }).ToList();
            int stt = 1;
            foreach (var g in groups)
            {
                if (!string.IsNullOrEmpty(g.Key))
                {
                    lstSheets.Items.Add(string.Format("📁  [{0}] ({1} bản vẽ)", g.Key.ToUpper(), g.Count()));
                }
                foreach (SheetItem s in g)
                {
                    string indent = !string.IsNullOrEmpty(g.Key) ? "     " : " ";
                    lstSheets.Items.Add(string.Format("{0}{1,2}. [{2}] - {3}", indent, stt++, s.SheetNumber, s.SheetName));
                }
            }
        }

        private void RadFormat_CheckedChanged(object sender, EventArgs e)
        {
            bool isPdf = radPdf.Checked;
            chkPdfCombine.Enabled = isPdf;
            lblFileName.Enabled = isPdf;
            txtPdfFileName.Enabled = isPdf && chkPdfCombine.Checked;

            chkCadCombineOne.Enabled = !isPdf;
            lblCadFileName.Enabled = !isPdf;
            txtCadFileName.Enabled = !isPdf && chkCadCombineOne.Checked;
            chkCadMerge.Enabled = !isPdf;

            if (isPdf)
            {
                btnStartExport.BackColor = Color.FromArgb(192, 57, 43); // Red HadoMEE
                btnStartExport.Text = string.Format("📄   XUẤT {0} BẢN VẼ SANG PDF", _sheets.Count);
            }
            else
            {
                btnStartExport.BackColor = Color.FromArgb(24, 43, 73); // Navy
                btnStartExport.Text = string.Format("📐   XUẤT {0} BẢN VẼ SANG CAD (.DWG)", _sheets.Count);
            }
        }

        private void BtnBrowseFolder_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Chọn thư mục lưu các file bản vẽ xuất từ HDTOOL:";
                if (Directory.Exists(txtFolder.Text))
                {
                    fbd.SelectedPath = txtFolder.Text;
                }
                else
                {
                    fbd.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                }

                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    txtFolder.Text = fbd.SelectedPath;
                }
            }
        }

        private void BtnStartExport_Click(object sender, EventArgs e)
        {
            string folder = txtFolder.Text.Trim();
            if (string.IsNullOrEmpty(folder))
            {
                MessageBox.Show("Vui lòng chọn thư mục lưu trữ trước khi xuất.", "HDTOOL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!Directory.Exists(folder))
            {
                try
                {
                    Directory.CreateDirectory(folder);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Không thể tạo thư mục lưu trữ: " + ex.Message, "HDTOOL", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            this.Cursor = Cursors.WaitCursor;
            btnStartExport.Enabled = false;

            bool isPdf = radPdf.Checked;
            string fileName = txtPdfFileName.Text.Trim();
            bool combine = chkPdfCombine.Checked;
            bool cadCombineOne = chkCadCombineOne.Checked;
            string cadFileName = txtCadFileName.Text.Trim();
            bool openFolderAfter = chkOpenFolderAfter.Checked;
            bool mergeViews = chkCadMerge.Checked;

            HDToolModelessHost.Run(uiapp =>
            {
                try
                {
                    string resultMsg;
                    bool success = false;

                    if (isPdf)
                    {
                        success = SheetExportCadPdfService.ExportToPdf(_doc, _sheets, folder, fileName, combine, out resultMsg);
                    }
                    else
                    {
                        if (cadCombineOne)
                        {
                            string safeName = cadFileName;
                            if (string.IsNullOrEmpty(safeName)) safeName = "HoSoBanVe_TongHop";
                            if (!safeName.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase)) safeName += ".dwg";
                            string targetDwg = Path.Combine(folder, safeName);
                            success = SheetExportCadPdfService.ExportToSingleCombinedCad(_doc, _sheets, targetDwg, openFolderAfter, out resultMsg);
                        }
                        else
                        {
                            success = SheetExportCadPdfService.ExportToCad(_doc, _sheets, folder, mergeViews, out resultMsg);
                        }
                    }

                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        this.Cursor = Cursors.Default;
                        btnStartExport.Enabled = true;

                        if (success)
                        {
                            MessageBox.Show(resultMsg, "HDTOOL - Xuất Bản Vẽ Thành Công", MessageBoxButtons.OK, MessageBoxIcon.Information);

                            if (openFolderAfter && Directory.Exists(folder))
                            {
                                try
                                {
                                    Process.Start(folder);
                                }
                                catch { }
                            }

                            this.DialogResult = DialogResult.OK;
                            this.Close();
                        }
                        else
                        {
                            MessageBox.Show(resultMsg, "HDTOOL - Lỗi Xuất Bản Vẽ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    });
                }
                catch (Exception ex)
                {
                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        this.Cursor = Cursors.Default;
                        btnStartExport.Enabled = true;
                        MessageBox.Show("Lỗi trong quá trình xuất: " + ex.Message, "HDTOOL Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    });
                }
            });
        }
    }
}
