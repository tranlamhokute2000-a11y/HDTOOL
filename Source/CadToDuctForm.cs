using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Color = System.Drawing.Color;
using Font = System.Drawing.Font;
using Point = System.Drawing.Point;

namespace HDTool
{
    /// <summary>
    /// Giao diện Modeless Dialog 60 FPS: Dựng lại mô hình hệ thống ống gió 3D từ hình vẽ CAD Link
    /// Nhận diện hình học (Rectangle, Trapezoid Côn thu, Tap Shoe, Elbow), đọc Text kích thước và lan truyền BFS
    /// </summary>
    public class CadToDuctForm : System.Windows.Forms.Form
    {
        private UIDocument _uidoc;
        private Document _doc;
        private Autodesk.Revit.DB.View _activeView;

        // Header
        private System.Windows.Forms.Panel pnlHeader;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblSubtitle;
        private System.Windows.Forms.PictureBox picLogo;

        // Group 1: File CAD Link
        private System.Windows.Forms.GroupBox grpCad;
        private System.Windows.Forms.Label lblCadLink;
        private System.Windows.Forms.ComboBox cmbCadLinks;
        private System.Windows.Forms.Button btnPickCadLink;
        private System.Windows.Forms.Label lblDuctLayer;
        private System.Windows.Forms.ComboBox cmbDuctLayer;

        // Group 2: Các nút nhận diện hình học CAD
        private System.Windows.Forms.GroupBox grpActions;
        private System.Windows.Forms.Button btnPickChain;
        private System.Windows.Forms.Button btnPickRegion;
        private System.Windows.Forms.Button btnScanAll;

        // Group 3: Xem trước dữ liệu nhận diện
        private System.Windows.Forms.GroupBox grpPreview;
        private System.Windows.Forms.DataGridView dgvPreview;
        private System.Windows.Forms.Label lblPreviewSummary;

        // Group 4: Thiết lập Revit MEP
        private System.Windows.Forms.GroupBox grpRevit;
        private System.Windows.Forms.Label lblSystemType;
        private System.Windows.Forms.ComboBox cmbSystemType;
        private System.Windows.Forms.Label lblDuctType;
        private System.Windows.Forms.ComboBox cmbDuctType;
        private System.Windows.Forms.Label lblLevel;
        private System.Windows.Forms.ComboBox cmbLevel;
        private System.Windows.Forms.Label lblElev;
        private System.Windows.Forms.NumericUpDown numElev;
        private System.Windows.Forms.Label lblUnitElev;
        private System.Windows.Forms.Label lblDefaultH;
        private System.Windows.Forms.NumericUpDown numDefaultH;
        private System.Windows.Forms.Label lblUnitH;
        private System.Windows.Forms.CheckBox chkConcentric;
        private System.Windows.Forms.CheckBox chkTakeoffTap;

        // Bottom Actions
        private System.Windows.Forms.Button btnBuild;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.ToolTip toolTip;

        // Data Caches
        private List<ImportInstance> _cadLinks;
        private List<MEPSystemType> _systemTypes;
        private List<DuctType> _ductTypes;
        private List<Level> _levels;

        // Phân tích gần nhất
        private List<CadDuctSegment> _currentStraightDucts = new List<CadDuctSegment>();
        private List<CadDuctSegment> _currentFittings = new List<CadDuctSegment>();
        private List<CadConnectionEdge> _currentEdges = new List<CadConnectionEdge>();
        private List<CadDamperItem> _currentDampers = new List<CadDamperItem>();

        public CadToDuctForm(UIDocument uidoc)
        {
            _uidoc = uidoc;
            _doc = uidoc.Document;
            _activeView = uidoc.ActiveView;

            InitializeComponent();
            LoadRevitData();
        }

        public void UpdateContext(UIDocument uidoc)
        {
            if (uidoc == null) return;
            _uidoc = uidoc;
            _doc = uidoc.Document;
            _activeView = uidoc.ActiveView;
            LoadRevitData();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.Text = "HDTOOL - Dựng Mô Hình Hệ Thống Ống Gió 3D Từ Bản Vẽ CAD Link";
            this.ClientSize = new Size(780, 720);
            this.MinimumSize = new Size(780, 720);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(248, 250, 252);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);

            toolTip = new System.Windows.Forms.ToolTip();

            // 1. HEADER
            pnlHeader = new System.Windows.Forms.Panel();
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = 65;
            pnlHeader.BackColor = Color.FromArgb(15, 23, 42);

            lblTitle = new System.Windows.Forms.Label();
            lblTitle.Text = "★ DỰNG HỆ THỐNG ỐNG GIÓ 3D TỰ ĐỘNG TỪ HÌNH VẼ CAD LINK ★";
            lblTitle.Font = new Font("Segoe UI", 11.5F, FontStyle.Bold);
            lblTitle.ForeColor = Color.FromArgb(254, 240, 138);
            lblTitle.Location = new Point(14, 9);
            lblTitle.AutoSize = true;

            lblSubtitle = new System.Windows.Forms.Label();
            lblSubtitle.Text = "Nhận diện bằng HÌNH HỌC (Ống thẳng, Côn thu đồng tâm, Chân rẽ Tap, Cút) - Modeless 100% không khóa Revit";
            lblSubtitle.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
            lblSubtitle.ForeColor = Color.FromArgb(203, 213, 225);
            lblSubtitle.Location = new Point(15, 36);
            lblSubtitle.AutoSize = true;

            picLogo = new System.Windows.Forms.PictureBox();
            picLogo.Size = new Size(130, 48);
            picLogo.Location = new Point(this.ClientSize.Width - 145, 8);
            picLogo.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            picLogo.SizeMode = PictureBoxSizeMode.Zoom;
            picLogo.BackColor = Color.Transparent;
            TryLoadLogo(picLogo);

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);
            pnlHeader.Controls.Add(picLogo);

            // 2. GROUP CAD LINK
            grpCad = new System.Windows.Forms.GroupBox();
            grpCad.Text = " 1. Chọn Bản Vẽ CAD Link ";
            grpCad.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            grpCad.ForeColor = Color.FromArgb(30, 41, 59);
            grpCad.Location = new Point(12, 75);
            grpCad.Size = new Size(756, 68);
            grpCad.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            lblCadLink = new System.Windows.Forms.Label();
            lblCadLink.Text = "File CAD Link:";
            lblCadLink.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            lblCadLink.Location = new Point(12, 28);
            lblCadLink.AutoSize = true;

            cmbCadLinks = new System.Windows.Forms.ComboBox();
            cmbCadLinks.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbCadLinks.Location = new Point(96, 25);
            cmbCadLinks.Size = new Size(260, 24);
            cmbCadLinks.SelectedIndexChanged += CmbCadLinks_SelectedIndexChanged;

            btnPickCadLink = new System.Windows.Forms.Button();
            btnPickCadLink.Text = "🎯 Pick CAD";
            btnPickCadLink.Location = new Point(362, 23);
            btnPickCadLink.Size = new Size(90, 28);
            btnPickCadLink.BackColor = Color.FromArgb(224, 231, 255);
            btnPickCadLink.ForeColor = Color.FromArgb(30, 58, 138);
            btnPickCadLink.FlatStyle = FlatStyle.Flat;
            btnPickCadLink.Click += BtnPickCadLink_Click;
            toolTip.SetToolTip(btnPickCadLink, "Click vào bản vẽ CAD Link trên màn hình Revit");

            lblDuctLayer = new System.Windows.Forms.Label();
            lblDuctLayer.Text = "Lọc Layer:";
            lblDuctLayer.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            lblDuctLayer.Location = new Point(465, 28);
            lblDuctLayer.AutoSize = true;

            cmbDuctLayer = new System.Windows.Forms.ComboBox();
            cmbDuctLayer.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbDuctLayer.Location = new Point(535, 25);
            cmbDuctLayer.Size = new Size(210, 24);
            cmbDuctLayer.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            grpCad.Controls.Add(lblCadLink);
            grpCad.Controls.Add(cmbCadLinks);
            grpCad.Controls.Add(btnPickCadLink);
            grpCad.Controls.Add(lblDuctLayer);
            grpCad.Controls.Add(cmbDuctLayer);

            // 3. GROUP ACTIONS: NHẬN DIỆN BẰNG HÌNH VẼ
            grpActions = new System.Windows.Forms.GroupBox();
            grpActions.Text = " 2. Nhận Diện Hệ Thống Ống Gió Bằng Hình Vẽ (Geometric Recognition) ";
            grpActions.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            grpActions.ForeColor = Color.FromArgb(30, 41, 59);
            grpActions.Location = new Point(12, 148);
            grpActions.Size = new Size(756, 68);
            grpActions.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            btnPickChain = new System.Windows.Forms.Button();
            btnPickChain.Text = "🎯 PICK CHỌN HÌNH VẼ TUYẾN ỐNG TRÊN CAD LINK";
            btnPickChain.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            btnPickChain.Location = new Point(12, 22);
            btnPickChain.Size = new Size(390, 36);
            btnPickChain.BackColor = Color.FromArgb(16, 185, 129);
            btnPickChain.ForeColor = Color.White;
            btnPickChain.FlatStyle = FlatStyle.Flat;
            btnPickChain.FlatAppearance.BorderSize = 0;
            btnPickChain.Click += BtnPickChain_Click;
            toolTip.SetToolTip(btnPickChain, "Click vào 1 đoạn hình vẽ ống gió (ví dụ 800x350) - Tool tự động nhận diện cả tuyến ống nối tiếp qua côn thu, cút, chân rẽ!");

            btnPickRegion = new System.Windows.Forms.Button();
            btnPickRegion.Text = "⬚ Quét Vùng (Box Pick)";
            btnPickRegion.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnPickRegion.Location = new Point(410, 22);
            btnPickRegion.Size = new Size(170, 36);
            btnPickRegion.BackColor = Color.FromArgb(241, 245, 249);
            btnPickRegion.ForeColor = Color.FromArgb(30, 41, 59);
            btnPickRegion.FlatStyle = FlatStyle.Flat;
            btnPickRegion.Click += BtnPickRegion_Click;
            toolTip.SetToolTip(btnPickRegion, "Quét chọn một vùng chữ nhật chứa hệ thống ống gió");

            btnScanAll = new System.Windows.Forms.Button();
            btnScanAll.Text = "🌐 Quét Toàn Bộ CAD";
            btnScanAll.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnScanAll.Location = new Point(588, 22);
            btnScanAll.Size = new Size(157, 36);
            btnScanAll.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnScanAll.BackColor = Color.FromArgb(241, 245, 249);
            btnScanAll.ForeColor = Color.FromArgb(30, 41, 59);
            btnScanAll.FlatStyle = FlatStyle.Flat;
            btnScanAll.Click += BtnScanAll_Click;
            toolTip.SetToolTip(btnScanAll, "Tự động nhận diện tất cả các tuyến ống gió theo hình học trên toàn bản vẽ CAD Link");

            grpActions.Controls.Add(btnPickChain);
            grpActions.Controls.Add(btnPickRegion);
            grpActions.Controls.Add(btnScanAll);

            // 4. GROUP PREVIEW TABLE
            grpPreview = new System.Windows.Forms.GroupBox();
            grpPreview.Text = " 3. Danh Sách Đối Tượng Nhận Diện Thời Gian Thực (Live Preview) ";
            grpPreview.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            grpPreview.ForeColor = Color.FromArgb(30, 41, 59);
            grpPreview.Location = new Point(12, 222);
            grpPreview.Size = new Size(756, 230);
            grpPreview.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            dgvPreview = new System.Windows.Forms.DataGridView();
            dgvPreview.Location = new Point(12, 22);
            dgvPreview.Size = new Size(733, 175);
            dgvPreview.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvPreview.BackgroundColor = Color.White;
            dgvPreview.BorderStyle = BorderStyle.Fixed3D;
            dgvPreview.AllowUserToAddRows = false;
            dgvPreview.AllowUserToDeleteRows = false;
            dgvPreview.ReadOnly = false;
            dgvPreview.RowHeadersVisible = false;
            dgvPreview.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvPreview.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            SetupPreviewGridColumns();

            lblPreviewSummary = new System.Windows.Forms.Label();
            lblPreviewSummary.Text = "Chưa có tuyến ống nào được chọn. Hãy bấm nút [PICK CHỌN HÌNH VẼ TUYẾN ỐNG] ở trên.";
            lblPreviewSummary.Font = new Font("Segoe UI", 8.5F, FontStyle.Italic);
            lblPreviewSummary.ForeColor = Color.FromArgb(71, 85, 105);
            lblPreviewSummary.Location = new Point(12, 204);
            lblPreviewSummary.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            lblPreviewSummary.AutoSize = true;

            grpPreview.Controls.Add(dgvPreview);
            grpPreview.Controls.Add(lblPreviewSummary);

            // 5. GROUP REVIT SETTINGS
            grpRevit = new System.Windows.Forms.GroupBox();
            grpRevit.Text = " 4. Thiết Lập Hệ Thống Ống Gió Revit ";
            grpRevit.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            grpRevit.ForeColor = Color.FromArgb(30, 41, 59);
            grpRevit.Location = new Point(12, 458);
            grpRevit.Size = new Size(756, 175);
            grpRevit.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            lblSystemType = new System.Windows.Forms.Label();
            lblSystemType.Text = "Hệ Thống Gió:";
            lblSystemType.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            lblSystemType.Location = new Point(12, 26);
            lblSystemType.AutoSize = true;

            cmbSystemType = new System.Windows.Forms.ComboBox();
            cmbSystemType.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbSystemType.Location = new Point(108, 23);
            cmbSystemType.Size = new Size(260, 24);

            lblDuctType = new System.Windows.Forms.Label();
            lblDuctType.Text = "Loại Ống Gió:";
            lblDuctType.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            lblDuctType.Location = new Point(390, 26);
            lblDuctType.AutoSize = true;

            cmbDuctType = new System.Windows.Forms.ComboBox();
            cmbDuctType.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbDuctType.Location = new Point(480, 23);
            cmbDuctType.Size = new Size(265, 24);
            cmbDuctType.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            lblLevel = new System.Windows.Forms.Label();
            lblLevel.Text = "Cao Độ Tầng:";
            lblLevel.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            lblLevel.Location = new Point(12, 63);
            lblLevel.AutoSize = true;

            cmbLevel = new System.Windows.Forms.ComboBox();
            cmbLevel.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbLevel.Location = new Point(108, 60);
            cmbLevel.Size = new Size(260, 24);

            lblElev = new System.Windows.Forms.Label();
            lblElev.Text = "Cao Độ Tim:";
            lblElev.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            lblElev.Location = new Point(390, 63);
            lblElev.AutoSize = true;

            numElev = new System.Windows.Forms.NumericUpDown();
            numElev.Minimum = -20000;
            numElev.Maximum = 100000;
            numElev.Value = 3200;
            numElev.Increment = 50;
            numElev.Location = new Point(480, 60);
            numElev.Size = new Size(110, 24);

            lblUnitElev = new System.Windows.Forms.Label();
            lblUnitElev.Text = "mm (so với tầng)";
            lblUnitElev.Font = new Font("Segoe UI", 8.5F, FontStyle.Italic);
            lblUnitElev.ForeColor = Color.Gray;
            lblUnitElev.Location = new Point(595, 63);
            lblUnitElev.AutoSize = true;

            lblDefaultH = new System.Windows.Forms.Label();
            lblDefaultH.Text = "Chiều Cao H Mặc Định:";
            lblDefaultH.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            lblDefaultH.Location = new Point(12, 100);
            lblDefaultH.AutoSize = true;

            numDefaultH = new System.Windows.Forms.NumericUpDown();
            numDefaultH.Minimum = 100;
            numDefaultH.Maximum = 3000;
            numDefaultH.Value = 300;
            numDefaultH.Increment = 50;
            numDefaultH.Location = new Point(160, 97);
            numDefaultH.Size = new Size(100, 24);

            lblUnitH = new System.Windows.Forms.Label();
            lblUnitH.Text = "mm (áp dụng khi đoạn ống thiếu ghi chú kích thước)";
            lblUnitH.Font = new Font("Segoe UI", 8.5F, FontStyle.Italic);
            lblUnitH.ForeColor = Color.Gray;
            lblUnitH.Location = new Point(265, 100);
            lblUnitH.AutoSize = true;

            chkConcentric = new System.Windows.Forms.CheckBox();
            chkConcentric.Text = "✔ Quy tắc 1: Côn thu tạo thành Côn Đồng Tâm (Concentric Transition) theo cao độ tim";
            chkConcentric.Checked = true;
            chkConcentric.Font = new Font("Segoe UI", 8.5F);
            chkConcentric.ForeColor = Color.FromArgb(15, 23, 42);
            chkConcentric.Location = new Point(15, 130);
            chkConcentric.AutoSize = true;

            chkTakeoffTap = new System.Windows.Forms.CheckBox();
            chkTakeoffTap.Text = "✔ Quy tắc 2: Chân rẽ dùng dạng TAP đâm vào sườn ống chính";
            chkTakeoffTap.Checked = true;
            chkTakeoffTap.Font = new Font("Segoe UI", 8.5F);
            chkTakeoffTap.ForeColor = Color.FromArgb(15, 23, 42);
            chkTakeoffTap.Location = new Point(15, 150);
            chkTakeoffTap.AutoSize = true;

            grpRevit.Controls.Add(lblSystemType);
            grpRevit.Controls.Add(cmbSystemType);
            grpRevit.Controls.Add(lblDuctType);
            grpRevit.Controls.Add(cmbDuctType);
            grpRevit.Controls.Add(lblLevel);
            grpRevit.Controls.Add(cmbLevel);
            grpRevit.Controls.Add(lblElev);
            grpRevit.Controls.Add(numElev);
            grpRevit.Controls.Add(lblUnitElev);
            grpRevit.Controls.Add(lblDefaultH);
            grpRevit.Controls.Add(numDefaultH);
            grpRevit.Controls.Add(lblUnitH);
            grpRevit.Controls.Add(chkConcentric);
            grpRevit.Controls.Add(chkTakeoffTap);

            // 6. BOTTOM BAR: BUILD & STATUS
            lblStatus = new System.Windows.Forms.Label();
            lblStatus.Text = "Sẵn sàng. Hãy chọn CAD Link và bấm [PICK CHỌN HÌNH VẼ TUYẾN ỐNG].";
            lblStatus.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            lblStatus.ForeColor = Color.FromArgb(71, 85, 105);
            lblStatus.Location = new Point(12, 642);
            lblStatus.Size = new Size(460, 34);
            lblStatus.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            btnBuild = new System.Windows.Forms.Button();
            btnBuild.Text = "🚀 DỰNG MÔ HÌNH REVIT 3D";
            btnBuild.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnBuild.Location = new Point(480, 640);
            btnBuild.Size = new Size(200, 38);
            btnBuild.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnBuild.BackColor = Color.FromArgb(37, 99, 235);
            btnBuild.ForeColor = Color.White;
            btnBuild.FlatStyle = FlatStyle.Flat;
            btnBuild.FlatAppearance.BorderSize = 0;
            btnBuild.Click += BtnBuild_Click;

            btnClose = new System.Windows.Forms.Button();
            btnClose.Text = "Đóng";
            btnClose.Location = new Point(688, 640);
            btnClose.Size = new Size(80, 38);
            btnClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnClose.BackColor = Color.FromArgb(241, 245, 249);
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.Click += (s, e) => this.Close();

            this.Controls.Add(pnlHeader);
            this.Controls.Add(grpCad);
            this.Controls.Add(grpActions);
            this.Controls.Add(grpPreview);
            this.Controls.Add(grpRevit);
            this.Controls.Add(lblStatus);
            this.Controls.Add(btnBuild);
            this.Controls.Add(btnClose);

            this.ResumeLayout(false);
        }

        private void SetupPreviewGridColumns()
        {
            dgvPreview.Columns.Clear();

            var colStt = new DataGridViewTextBoxColumn();
            colStt.HeaderText = "STT";
            colStt.FillWeight = 30;
            colStt.ReadOnly = true;
            dgvPreview.Columns.Add(colStt);

            var colKind = new DataGridViewTextBoxColumn();
            colKind.HeaderText = "Phân Loại Đối Tượng";
            colKind.FillWeight = 85;
            colKind.ReadOnly = true;
            dgvPreview.Columns.Add(colKind);

            var colSize = new DataGridViewTextBoxColumn();
            colSize.HeaderText = "Kích Thước WxH (mm)";
            colSize.FillWeight = 80;
            colSize.ReadOnly = false; // Kỹ sư có thể gõ sửa trực tiếp nếu muốn!
            dgvPreview.Columns.Add(colSize);

            var colLen = new DataGridViewTextBoxColumn();
            colLen.HeaderText = "Chiều Dài";
            colLen.FillWeight = 60;
            colLen.ReadOnly = true;
            dgvPreview.Columns.Add(colLen);

            var colNext = new DataGridViewTextBoxColumn();
            colNext.HeaderText = "Kết Nối Nối Tiếp";
            colNext.FillWeight = 85;
            colNext.ReadOnly = true;
            dgvPreview.Columns.Add(colNext);

            var colSource = new DataGridViewTextBoxColumn();
            colSource.HeaderText = "Nguồn Kích Thước";
            colSource.FillWeight = 110;
            colSource.ReadOnly = true;
            dgvPreview.Columns.Add(colSource);
        }

        private void LoadRevitData()
        {
            try
            {
                // 1. CAD Links
                _cadLinks = CadToDuctService.GetCadLinksInView(_doc, _activeView);
                cmbCadLinks.Items.Clear();
                foreach (ImportInstance imp in _cadLinks)
                {
                    cmbCadLinks.Items.Add(CadToDuctService.GetCadLinkDisplayName(_doc, imp));
                }
                if (cmbCadLinks.Items.Count > 0) cmbCadLinks.SelectedIndex = 0;

                // 2. MEPSystemType
                FilteredElementCollector sysCol = new FilteredElementCollector(_doc).OfClass(typeof(MEPSystemType));
                _systemTypes = new List<MEPSystemType>();
                cmbSystemType.Items.Clear();
                foreach (Element e in sysCol)
                {
                    MEPSystemType st = e as MEPSystemType;
                    if (st != null && (st.SystemClassification == MEPSystemClassification.SupplyAir ||
                                      st.SystemClassification == MEPSystemClassification.ReturnAir ||
                                      st.SystemClassification == MEPSystemClassification.ExhaustAir ||
                                      st.SystemClassification == MEPSystemClassification.OtherAir))
                    {
                        _systemTypes.Add(st);
                        cmbSystemType.Items.Add(st.Name);
                    }
                }
                if (cmbSystemType.Items.Count > 0)
                {
                    int defaultIdx = 0;
                    for (int i = 0; i < cmbSystemType.Items.Count; i++)
                    {
                        string n = cmbSystemType.Items[i].ToString().ToLowerInvariant();
                        if (n.Contains("hút khói") || n.Contains("exhaust") || n.Contains("thải")) { defaultIdx = i; break; }
                    }
                    cmbSystemType.SelectedIndex = defaultIdx;
                }

                // 3. DuctType
                FilteredElementCollector dtCol = new FilteredElementCollector(_doc).OfClass(typeof(DuctType));
                _ductTypes = new List<DuctType>();
                cmbDuctType.Items.Clear();
                foreach (Element e in dtCol)
                {
                    DuctType dt = e as DuctType;
                    if (dt != null && dt.Shape == ConnectorProfileType.Rectangular)
                    {
                        _ductTypes.Add(dt);
                        cmbDuctType.Items.Add(dt.Name);
                    }
                }
                if (cmbDuctType.Items.Count > 0) cmbDuctType.SelectedIndex = 0;

                // 4. Levels
                FilteredElementCollector lvlCol = new FilteredElementCollector(_doc).OfClass(typeof(Level)).WhereElementIsNotElementType();
                _levels = new List<Level>();
                cmbLevel.Items.Clear();
                foreach (Element e in lvlCol)
                {
                    Level l = e as Level;
                    if (l != null)
                    {
                        _levels.Add(l);
                        cmbLevel.Items.Add(l.Name);
                    }
                }
                if (cmbLevel.Items.Count > 0)
                {
                    int curLvlIdx = 0;
                    if (_activeView != null && _activeView.GenLevel != null)
                    {
                        for (int i = 0; i < _levels.Count; i++)
                        {
                            if (_levels[i].Id == _activeView.GenLevel.Id) { curLvlIdx = i; break; }
                        }
                    }
                    cmbLevel.SelectedIndex = curLvlIdx;
                }
            }
            catch (Exception ex)
            {
                SetStatus(Color.Red, "Lỗi nạp dữ liệu Revit: " + ex.Message);
            }
        }

        private void CmbCadLinks_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbCadLinks.SelectedIndex < 0 || cmbCadLinks.SelectedIndex >= _cadLinks.Count) return;
            ImportInstance cad = _cadLinks[cmbCadLinks.SelectedIndex];

            cmbDuctLayer.Items.Clear();
            cmbDuctLayer.Items.Add("(Tất cả Layer - Nhận diện theo hình vẽ)");

            List<string> layers = CadToDuctService.GetLayersFromCadLink(cad, _doc);
            foreach (string l in layers) cmbDuctLayer.Items.Add(l);

            cmbDuctLayer.SelectedIndex = 0; // Mặc định tự động nhận diện theo hình vẽ
        }

        private void BtnPickCadLink_Click(object sender, EventArgs e)
        {
            HDToolModelessHost.Run(uiapp =>
            {
                try
                {
                    this.BeginInvoke((MethodInvoker)(() => this.Hide()));
                    Reference r = _uidoc.Selection.PickObject(ObjectType.Element, "Click chọn file CAD Link trên màn hình Revit:");
                    if (r != null)
                    {
                        Element elem = _doc.GetElement(r);
                        ImportInstance imp = elem as ImportInstance;
                        if (imp != null)
                        {
                            this.BeginInvoke((MethodInvoker)delegate
                            {
                                if (!_cadLinks.Any(x => x.Id == imp.Id)) _cadLinks.Add(imp);
                                LoadRevitData();
                                for (int i = 0; i < _cadLinks.Count; i++)
                                {
                                    if (_cadLinks[i].Id == imp.Id) { cmbCadLinks.SelectedIndex = i; break; }
                                }
                            });
                        }
                    }
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
                catch (Exception ex)
                {
                    SetStatus(Color.Red, "Lỗi pick CAD: " + ex.Message);
                }
                finally
                {
                    this.BeginInvoke((MethodInvoker)delegate { this.Show(); this.BringToFront(); });
                }
            });
        }

        // =========================================================================
        // ACTIONS: NHẬN DIỆN BẰNG HÌNH VẼ
        // =========================================================================

        private void BtnPickChain_Click(object sender, EventArgs e)
        {
            if (cmbCadLinks.SelectedIndex < 0 || cmbCadLinks.SelectedIndex >= _cadLinks.Count)
            {
                SetStatus(Color.Red, "Vui lòng chọn 1 CAD Link trước.");
                return;
            }

            ImportInstance cad = _cadLinks[cmbCadLinks.SelectedIndex];

            HDToolModelessHost.Run(uiapp =>
            {
                try
                {
                    this.BeginInvoke((MethodInvoker)(() => this.Hide()));
                    Reference r = _uidoc.Selection.PickObject(
                        ObjectType.PointOnElement,
                        "Click trực tiếp vào 1 hình vẽ ống gió trên CAD Link (Tool tự động nhận diện cả tuyến):");

                    if (r != null)
                    {
                        XYZ pickPt = r.GlobalPoint;
                        ExecuteAnalyze(cad, null, pickPt);
                    }
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
                catch (Exception ex)
                {
                    SetStatus(Color.Red, "Lỗi pick tuyến ống: " + ex.Message);
                }
                finally
                {
                    this.BeginInvoke((MethodInvoker)delegate { this.Show(); this.BringToFront(); });
                }
            });
        }

        private void BtnPickRegion_Click(object sender, EventArgs e)
        {
            if (cmbCadLinks.SelectedIndex < 0 || cmbCadLinks.SelectedIndex >= _cadLinks.Count)
            {
                SetStatus(Color.Red, "Vui lòng chọn 1 CAD Link trước.");
                return;
            }

            ImportInstance cad = _cadLinks[cmbCadLinks.SelectedIndex];

            HDToolModelessHost.Run(uiapp =>
            {
                try
                {
                    this.BeginInvoke((MethodInvoker)(() => this.Hide()));
                    PickedBox box = _uidoc.Selection.PickBox(
                        PickBoxStyle.Crossing,
                        "Kéo quét chọn vùng chữ nhật chứa hệ thống ống gió cần dựng:");

                    if (box != null)
                    {
                        Outline outline = new Outline(box.Min, box.Max);
                        ExecuteAnalyze(cad, outline, null);
                    }
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
                catch (Exception ex)
                {
                    SetStatus(Color.Red, "Lỗi quét vùng: " + ex.Message);
                }
                finally
                {
                    this.BeginInvoke((MethodInvoker)delegate { this.Show(); this.BringToFront(); });
                }
            });
        }

        private void BtnScanAll_Click(object sender, EventArgs e)
        {
            if (cmbCadLinks.SelectedIndex < 0 || cmbCadLinks.SelectedIndex >= _cadLinks.Count)
            {
                SetStatus(Color.Red, "Vui lòng chọn 1 CAD Link trước.");
                return;
            }
            ImportInstance cad = _cadLinks[cmbCadLinks.SelectedIndex];
            ExecuteAnalyze(cad, null, null);
        }

        private void ExecuteAnalyze(ImportInstance cad, Outline pickBox, XYZ pickSeed)
        {
            double centerElevMm = (double)numElev.Value;
            double defaultHMm = (double)numDefaultH.Value;
            string selectedLayer = cmbDuctLayer.SelectedItem != null ? cmbDuctLayer.SelectedItem.ToString() : null;

            SetStatus(Color.FromArgb(2, 132, 199), "⏳ Đang nhận diện hình học (Ống, Côn, Cút, Chân rẽ) và trích xuất Text kích thước...");

            HDToolModelessHost.Run(uiapp =>
            {
                try
                {
                    List<CadDuctSegment> ducts;
                    List<CadDuctSegment> fittings;
                    List<CadConnectionEdge> edges;
                    List<CadDamperItem> dampers;

                    CadToDuctService.AnalyzeCadDuctNetwork(
                        _doc,
                        cad,
                        centerElevMm,
                        selectedLayer,
                        pickBox,
                        pickSeed,
                        defaultHMm,
                        out ducts,
                        out fittings,
                        out edges,
                        out dampers);

                    _currentStraightDucts = ducts;
                    _currentFittings = fittings;
                    _currentEdges = edges;
                    _currentDampers = dampers;

                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        PopulatePreviewGrid();
                    });
                }
                catch (Exception ex)
                {
                    SetStatus(Color.Red, "Lỗi phân tích: " + ex.Message);
                }
            });
        }

        private void PopulatePreviewGrid()
        {
            dgvPreview.Rows.Clear();

            int transitionCount = 0;
            int elbowCount = 0;
            int takeoffCount = 0;
            int teeCount = 0;

            if (_currentEdges != null)
            {
                foreach (CadConnectionEdge e in _currentEdges)
                {
                    if (e.Kind == CadConnectionKind.Transition) transitionCount++;
                    else if (e.Kind == CadConnectionKind.Elbow) elbowCount++;
                    else if (e.Kind == CadConnectionKind.TakeoffTap) takeoffCount++;
                    else if (e.Kind == CadConnectionKind.TeeBranch) teeCount++;
                }
            }

            for (int i = 0; i < _currentStraightDucts.Count; i++)
            {
                CadDuctSegment d = _currentStraightDucts[i];
                string nextDesc = "Đoạn cuối / Tự do";

                var conns = d.Connections;
                if (conns != null && conns.Count > 0)
                {
                    List<string> parts = new List<string>();
                    foreach (var c in conns)
                    {
                        CadDuctSegment other = (c.SegmentA == d) ? c.SegmentB : c.SegmentA;
                        int otherIdx = _currentStraightDucts.IndexOf(other) + 1;
                        string otherName = otherIdx > 0 ? ("#" + otherIdx) : "";

                        if (c.Kind == CadConnectionKind.Transition)
                            parts.Add(string.Format("Côn thu -> {0} ({1}x{2})", otherName, (int)other.WidthMm, (int)other.HeightMm));
                        else if (c.Kind == CadConnectionKind.TakeoffTap)
                            parts.Add(string.Format("Chân rẽ Tap {0} {1}", (c.SegmentA == d ? "đón nhánh" : "vào sườn"), otherName));
                        else if (c.Kind == CadConnectionKind.Elbow)
                            parts.Add(string.Format("Cút góc -> {0}", otherName));
                        else if (c.Kind == CadConnectionKind.TeeBranch)
                            parts.Add(string.Format("Chạc chia Tee -> {0}", otherName));
                        else
                            parts.Add("Nối tiếp " + otherName);
                    }
                    nextDesc = string.Join("; ", parts.ToArray());
                }

                dgvPreview.Rows.Add(
                    (i + 1).ToString(),
                    "Ống gió thẳng",
                    string.Format("{0} x {1}", (int)d.WidthMm, (int)d.HeightMm),
                    string.Format("{0:N0} mm", d.Length),
                    nextDesc,
                    d.SizeSource);
            }

            lblPreviewSummary.Text = string.Format(
                "✔ Nhận diện: {0} đoạn ống gió thẳng | {1} côn thu đồng tâm | {2} cút chuyển hướng | {3} chân rẽ Tap{4} | {5} van gió",
                _currentStraightDucts.Count,
                transitionCount,
                elbowCount,
                takeoffCount,
                teeCount > 0 ? string.Format(" | {0} chạc chia", teeCount) : "",
                _currentDampers.Count);

            if (_currentStraightDucts.Count > 0)
            {
                SetStatus(Color.FromArgb(16, 185, 129), "✔ Nhận diện hình học hoàn tất! Kiểm tra danh sách ở trên và bấm [DỰNG MÔ HÌNH REVIT 3D].");
            }
            else
            {
                SetStatus(Color.FromArgb(234, 88, 12), "Không tìm thấy hình vẽ tuyến ống phù hợp. Hãy thử click vào đúng nét biên ống.");
            }
        }

        // =========================================================================
        // DỰNG HỆ THỐNG ỐNG GIÓ TRONG REVIT
        // =========================================================================

        private void BtnBuild_Click(object sender, EventArgs e)
        {
            if (_currentStraightDucts == null || _currentStraightDucts.Count == 0)
            {
                SetStatus(Color.Red, "Chưa có đoạn ống nào được nhận diện. Hãy bấm [PICK CHỌN HÌNH VẼ TUYẾN ỐNG] trước.");
                return;
            }

            if (cmbSystemType.SelectedIndex < 0 || cmbDuctType.SelectedIndex < 0 || cmbLevel.SelectedIndex < 0)
            {
                SetStatus(Color.Red, "Vui lòng chọn Hệ thống gió, Loại ống và Cao độ tầng.");
                return;
            }

            ElementId sysTypeId = _systemTypes[cmbSystemType.SelectedIndex].Id;
            ElementId ductTypeId = _ductTypes[cmbDuctType.SelectedIndex].Id;
            ElementId levelId = _levels[cmbLevel.SelectedIndex].Id;

            bool useConcentric = chkConcentric.Checked;
            bool useTap = chkTakeoffTap.Checked;

            // Đồng bộ lại kích thước nếu kỹ sư có sửa trong bảng DataGridView
            for (int r = 0; r < dgvPreview.Rows.Count && r < _currentStraightDucts.Count; r++)
            {
                object val = dgvPreview.Rows[r].Cells[2].Value;
                if (val != null)
                {
                    string str = val.ToString();
                    var match = System.Text.RegularExpressions.Regex.Match(str, @"(\d+)\s*[xX\*]\s*(\d+)");
                    if (match.Success)
                    {
                        double w = double.Parse(match.Groups[1].Value);
                        double h = double.Parse(match.Groups[2].Value);
                        _currentStraightDucts[r].WidthMm = w;
                        _currentStraightDucts[r].HeightMm = h;
                    }
                }
            }

            SetStatus(Color.FromArgb(2, 132, 199), "⏳ Đang dựng ống gió và tự động lắp côn thu, cút, chân rẽ trong Revit...");

            HDToolModelessHost.Run(uiapp =>
            {
                try
                {
                    string report;
                    int count = CadToDuctService.BuildDuctsInRevit(
                        _doc,
                        _currentStraightDucts,
                        _currentEdges,
                        sysTypeId,
                        ductTypeId,
                        levelId,
                        useConcentric,
                        useTap,
                        out report);

                    SetStatus(Color.FromArgb(16, 185, 129), report);

                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        TaskDialog.Show("HDTOOL - DỰNG ỐNG GIÓ 3D THÀNH CÔNG", report);
                    });
                }
                catch (Exception ex)
                {
                    SetStatus(Color.Red, "Lỗi dựng ống gió: " + ex.Message);
                }
            });
        }

        private void SetStatus(Color color, string text)
        {
            if (this.IsDisposed) return;
            Action act = delegate
            {
                if (this.IsDisposed) return;
                lblStatus.ForeColor = color;
                lblStatus.Text = text;
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

        private void TryLoadLogo(System.Windows.Forms.PictureBox pb)
        {
            try
            {
                System.Reflection.Assembly asm = System.Reflection.Assembly.GetExecutingAssembly();
                using (Stream s = asm.GetManifestResourceStream("hadomee_logo.png"))
                {
                    if (s != null) pb.Image = Image.FromStream(s);
                }
            }
            catch { }
        }
    }
}
