using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
    public class CadToDuctForm : HDToolBaseForm
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
        private System.Windows.Forms.Label lblTextDiag;
        private Font _boldGridFont;
        private string _lastDwgDiagnostic = string.Empty;

        // Group 4: Thiết lập Revit MEP
        private System.Windows.Forms.GroupBox grpRevit;
        private System.Windows.Forms.Label lblSystemType;
        private System.Windows.Forms.ComboBox cmbSystemType;
        private System.Windows.Forms.Label lblDuctType;
        private System.Windows.Forms.ComboBox cmbDuctType;
        private System.Windows.Forms.Label lblLevel;
        private System.Windows.Forms.ComboBox cmbLevel;
        private System.Windows.Forms.Label lblElev;
        private System.Windows.Forms.TextBox txtElev;
        private System.Windows.Forms.Label lblUnitElev;
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
            this.ClientSize = new Size(880, 720);
            this.MinimumSize = new Size(880, 720);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(248, 250, 252);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);

            toolTip = new System.Windows.Forms.ToolTip();

            // 1. HEADER
            pnlHeader = new System.Windows.Forms.Panel();
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = 62;
            pnlHeader.BackColor = Color.FromArgb(15, 23, 42);

            lblTitle = new System.Windows.Forms.Label();
            lblTitle.Text = "★ DỰNG HỆ THỐNG ỐNG GIÓ 3D TỰ ĐỘNG TỪ HÌNH VẼ CAD LINK ★";
            lblTitle.Font = new Font("Segoe UI", 11.5F, FontStyle.Bold);
            lblTitle.ForeColor = Color.FromArgb(254, 240, 138);
            lblTitle.Location = new Point(14, 8);
            lblTitle.AutoSize = true;

            lblSubtitle = new System.Windows.Forms.Label();
            lblSubtitle.Text = "Chuẩn kỹ sư BIM: Ống chính liền mạch + Côn đồng tâm + Chân rẽ Tap (ưu tiên Family Revit có sẵn) - Modeless 100%";
            lblSubtitle.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
            lblSubtitle.ForeColor = Color.FromArgb(203, 213, 225);
            lblSubtitle.Location = new Point(15, 34);
            lblSubtitle.Size = new Size(712, 20);
            lblSubtitle.AutoSize = false;
            lblSubtitle.AutoEllipsis = true;
            lblSubtitle.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            picLogo = new System.Windows.Forms.PictureBox();
            picLogo.Size = new Size(130, 46);
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
            grpCad.Location = new Point(12, 68);
            grpCad.Size = new Size(856, 66);
            grpCad.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            lblCadLink = new System.Windows.Forms.Label();
            lblCadLink.Text = "File CAD Link:";
            lblCadLink.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            lblCadLink.Location = new Point(14, 27);
            lblCadLink.AutoSize = true;

            cmbCadLinks = new System.Windows.Forms.ComboBox();
            cmbCadLinks.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbCadLinks.Location = new Point(105, 24);
            cmbCadLinks.Size = new Size(300, 24);
            cmbCadLinks.SelectedIndexChanged += CmbCadLinks_SelectedIndexChanged;

            btnPickCadLink = new System.Windows.Forms.Button();
            btnPickCadLink.Text = "🎯 Pick CAD";
            btnPickCadLink.Location = new Point(413, 22);
            btnPickCadLink.Size = new Size(95, 28);
            btnPickCadLink.BackColor = Color.FromArgb(224, 231, 255);
            btnPickCadLink.ForeColor = Color.FromArgb(30, 58, 138);
            btnPickCadLink.FlatStyle = FlatStyle.Flat;
            btnPickCadLink.Click += BtnPickCadLink_Click;
            toolTip.SetToolTip(btnPickCadLink, "Click vào bản vẽ CAD Link trên màn hình Revit");

            lblDuctLayer = new System.Windows.Forms.Label();
            lblDuctLayer.Text = "Lọc Layer:";
            lblDuctLayer.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            lblDuctLayer.Location = new Point(522, 27);
            lblDuctLayer.AutoSize = true;

            cmbDuctLayer = new System.Windows.Forms.ComboBox();
            cmbDuctLayer.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbDuctLayer.Location = new Point(592, 24);
            cmbDuctLayer.Size = new Size(250, 24);
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
            grpActions.Location = new Point(12, 138);
            grpActions.Size = new Size(856, 66);
            grpActions.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            btnPickChain = new System.Windows.Forms.Button();
            btnPickChain.Text = "🎯 PICK CHỌN HÌNH VẼ TUYẾN ỐNG TRÊN CAD LINK";
            btnPickChain.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            btnPickChain.Location = new Point(12, 21);
            btnPickChain.Size = new Size(430, 36);
            btnPickChain.BackColor = Color.FromArgb(16, 185, 129);
            btnPickChain.ForeColor = Color.White;
            btnPickChain.FlatStyle = FlatStyle.Flat;
            btnPickChain.FlatAppearance.BorderSize = 0;
            btnPickChain.Click += BtnPickChain_Click;
            toolTip.SetToolTip(btnPickChain, "Click vào 1 đoạn hình vẽ ống gió (ví dụ 1900x400) - Tool tự động nhận diện cả tuyến ống nối tiếp qua côn thu, cút cong, chân rẽ và tự tách riêng các tuyến khác hệ/khác màu/đi ngang qua nhau!");

            btnPickRegion = new System.Windows.Forms.Button();
            btnPickRegion.Text = "⬚ Quét Vùng (Box Pick)";
            btnPickRegion.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnPickRegion.Location = new Point(450, 21);
            btnPickRegion.Size = new Size(195, 36);
            btnPickRegion.BackColor = Color.FromArgb(241, 245, 249);
            btnPickRegion.ForeColor = Color.FromArgb(30, 41, 59);
            btnPickRegion.FlatStyle = FlatStyle.Flat;
            btnPickRegion.Click += BtnPickRegion_Click;
            toolTip.SetToolTip(btnPickRegion, "Quét chọn một vùng chữ nhật chứa hệ thống ống gió (tự động tách riêng các tuyến đi ngang qua nhau / khác hệ thống)");

            btnScanAll = new System.Windows.Forms.Button();
            btnScanAll.Text = "🌐 Quét Toàn Bộ CAD";
            btnScanAll.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnScanAll.Location = new Point(653, 21);
            btnScanAll.Size = new Size(191, 36);
            btnScanAll.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnScanAll.BackColor = Color.FromArgb(241, 245, 249);
            btnScanAll.ForeColor = Color.FromArgb(30, 41, 59);
            btnScanAll.FlatStyle = FlatStyle.Flat;
            btnScanAll.Click += BtnScanAll_Click;
            toolTip.SetToolTip(btnScanAll, "Tự động nhận diện toàn bộ mạng ống gió trên CAD Link, tách biệt hoàn toàn các tuyến giao chéo khác hệ/khác màu/khác cao độ");

            grpActions.Controls.Add(btnPickChain);
            grpActions.Controls.Add(btnPickRegion);
            grpActions.Controls.Add(btnScanAll);

            // 4. GROUP PREVIEW TABLE
            grpPreview = new System.Windows.Forms.GroupBox();
            grpPreview.Text = " 3. Danh Sách Đối Tượng Nhận Diện Thời Gian Thực (Live Preview) ";
            grpPreview.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            grpPreview.ForeColor = Color.FromArgb(30, 41, 59);
            grpPreview.Location = new Point(12, 208);
            grpPreview.Size = new Size(856, 250);
            grpPreview.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            dgvPreview = new System.Windows.Forms.DataGridView();
            dgvPreview.Location = new Point(12, 22);
            dgvPreview.Size = new Size(832, 168);
            dgvPreview.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvPreview.BackgroundColor = Color.White;
            dgvPreview.BorderStyle = BorderStyle.Fixed3D;
            dgvPreview.AllowUserToAddRows = false;
            dgvPreview.AllowUserToDeleteRows = false;
            dgvPreview.ReadOnly = false;
            dgvPreview.RowHeadersVisible = false;
            dgvPreview.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvPreview.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvPreview.CellEndEdit += DgvPreview_CellEndEdit;
            _boldGridFont = new Font(dgvPreview.Font, FontStyle.Bold);

            SetupPreviewGridColumns();

            lblPreviewSummary = new System.Windows.Forms.Label();
            lblPreviewSummary.Text = "Chưa có tuyến ống nào được chọn. Hãy bấm nút [PICK CHỌN HÌNH VẼ TUYẾN ỐNG] ở trên.";
            lblPreviewSummary.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            lblPreviewSummary.ForeColor = Color.FromArgb(51, 65, 85);
            lblPreviewSummary.Location = new Point(12, 196);
            lblPreviewSummary.Size = new Size(832, 20);
            lblPreviewSummary.AutoSize = false;
            lblPreviewSummary.AutoEllipsis = true;
            lblPreviewSummary.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            lblTextDiag = new System.Windows.Forms.Label();
            lblTextDiag.Text = "Chẩn đoán Text CAD: Sẵn sàng trích xuất Text WxH từ file DWG gốc (số đầu = Rộng W, số sau = Cao H).";
            lblTextDiag.Font = new Font("Segoe UI", 8.5F, FontStyle.Italic);
            lblTextDiag.ForeColor = Color.FromArgb(71, 85, 105);
            lblTextDiag.Location = new Point(12, 218);
            lblTextDiag.Size = new Size(832, 24);
            lblTextDiag.AutoSize = false;
            lblTextDiag.AutoEllipsis = true;
            lblTextDiag.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            grpPreview.Controls.Add(dgvPreview);
            grpPreview.Controls.Add(lblPreviewSummary);
            grpPreview.Controls.Add(lblTextDiag);

            // 5. GROUP REVIT SETTINGS
            grpRevit = new System.Windows.Forms.GroupBox();
            grpRevit.Text = " 4. Thiết Lập Hệ Thống Ống Gió Revit ";
            grpRevit.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            grpRevit.ForeColor = Color.FromArgb(30, 41, 59);
            grpRevit.Location = new Point(12, 464);
            grpRevit.Size = new Size(856, 184);
            grpRevit.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            lblSystemType = new System.Windows.Forms.Label();
            lblSystemType.Text = "Hệ Thống Gió:";
            lblSystemType.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            lblSystemType.Location = new Point(14, 27);
            lblSystemType.AutoSize = true;

            cmbSystemType = new System.Windows.Forms.ComboBox();
            cmbSystemType.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbSystemType.Location = new Point(118, 24);
            cmbSystemType.Size = new Size(290, 24);

            lblDuctType = new System.Windows.Forms.Label();
            lblDuctType.Text = "Loại Ống Gió:";
            lblDuctType.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            lblDuctType.Location = new Point(430, 27);
            lblDuctType.AutoSize = true;

            cmbDuctType = new System.Windows.Forms.ComboBox();
            cmbDuctType.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbDuctType.Location = new Point(528, 24);
            cmbDuctType.Size = new Size(314, 24);
            cmbDuctType.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            lblLevel = new System.Windows.Forms.Label();
            lblLevel.Text = "Cao Độ Tầng:";
            lblLevel.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            lblLevel.Location = new Point(14, 63);
            lblLevel.AutoSize = true;

            cmbLevel = new System.Windows.Forms.ComboBox();
            cmbLevel.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbLevel.Location = new Point(118, 60);
            cmbLevel.Size = new Size(290, 24);

            lblElev = new System.Windows.Forms.Label();
            lblElev.Text = "Cao Độ Tim Hệ Thống:";
            lblElev.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblElev.ForeColor = Color.FromArgb(15, 23, 42);
            lblElev.Location = new Point(14, 99);
            lblElev.AutoSize = true;

            txtElev = new System.Windows.Forms.TextBox();
            txtElev.Text = string.Empty;
            txtElev.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            txtElev.BackColor = Color.White;
            txtElev.ForeColor = Color.FromArgb(15, 23, 42);
            txtElev.Location = new Point(176, 96);
            txtElev.Size = new Size(135, 24);
            txtElev.TextChanged += TxtElev_TextChanged;
            toolTip.SetToolTip(txtElev, "Nhập cao độ tim ống gió (mm) so với tầng đã chọn (ví dụ: 2850 hoặc -450). Bắt buộc tự nhập trước khi dựng 3D.");

            lblUnitElev = new System.Windows.Forms.Label();
            lblUnitElev.Text = "mm (bắt buộc tự nhập, không mặc định)";
            lblUnitElev.Font = new Font("Segoe UI", 8.5F, FontStyle.Italic);
            lblUnitElev.ForeColor = Color.FromArgb(180, 83, 9);
            lblUnitElev.Location = new Point(320, 99);
            lblUnitElev.AutoSize = true;

            chkConcentric = new System.Windows.Forms.CheckBox();
            chkConcentric.Text = "✔ Quy tắc 1: Côn thu đồng tâm (Concentric Transition) giữ tuyến ống chính liền mạch theo tim";
            chkConcentric.Checked = true;
            chkConcentric.Font = new Font("Segoe UI", 8.5F);
            chkConcentric.ForeColor = Color.FromArgb(15, 23, 42);
            chkConcentric.Location = new Point(16, 130);
            chkConcentric.Size = new Size(824, 22);
            chkConcentric.AutoSize = false;
            chkConcentric.AutoEllipsis = true;

            chkTakeoffTap = new System.Windows.Forms.CheckBox();
            chkTakeoffTap.Text = "✔ Quy tắc 2: Ngã 3/4 tách thành Ống chính + Chân rẽ Tap (thay Y gió, ưu tiên Family Revit có sẵn)";
            chkTakeoffTap.Checked = true;
            chkTakeoffTap.Font = new Font("Segoe UI", 8.5F);
            chkTakeoffTap.ForeColor = Color.FromArgb(15, 23, 42);
            chkTakeoffTap.Location = new Point(16, 154);
            chkTakeoffTap.Size = new Size(824, 22);
            chkTakeoffTap.AutoSize = false;
            chkTakeoffTap.AutoEllipsis = true;
            toolTip.SetToolTip(chkTakeoffTap, "Ngã 3/Ngã 4 được dựng thành tuyến chính liên tục (thêm Côn thu đồng tâm nếu đổi size) + Chân rẽ Tap đâm sườn, ưu tiên giữ nguyên Family phụ kiện có sẵn trong DuctType của Revit.");

            grpRevit.Controls.Add(lblSystemType);
            grpRevit.Controls.Add(cmbSystemType);
            grpRevit.Controls.Add(lblDuctType);
            grpRevit.Controls.Add(cmbDuctType);
            grpRevit.Controls.Add(lblLevel);
            grpRevit.Controls.Add(cmbLevel);
            grpRevit.Controls.Add(lblElev);
            grpRevit.Controls.Add(txtElev);
            grpRevit.Controls.Add(lblUnitElev);
            grpRevit.Controls.Add(chkConcentric);
            grpRevit.Controls.Add(chkTakeoffTap);

            // 6. BOTTOM BAR: BUILD & STATUS
            lblStatus = new System.Windows.Forms.Label();
            lblStatus.Text = "Sẵn sàng. Hãy chọn CAD Link, bấm [PICK CHỌN HÌNH VẼ TUYẾN ỐNG], kiểm tra kích thước và tự nhập Cao độ tim (mm).";
            lblStatus.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            lblStatus.ForeColor = Color.FromArgb(71, 85, 105);
            lblStatus.Location = new Point(12, 654);
            lblStatus.Size = new Size(545, 54);
            lblStatus.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            btnBuild = new System.Windows.Forms.Button();
            btnBuild.Text = "🚀 DỰNG MÔ HÌNH REVIT 3D";
            btnBuild.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnBuild.Location = new Point(568, 658);
            btnBuild.Size = new Size(210, 40);
            btnBuild.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnBuild.BackColor = Color.FromArgb(37, 99, 235);
            btnBuild.ForeColor = Color.White;
            btnBuild.FlatStyle = FlatStyle.Flat;
            btnBuild.FlatAppearance.BorderSize = 0;
            btnBuild.Click += BtnBuild_Click;

            btnClose = new System.Windows.Forms.Button();
            btnClose.Text = "Đóng";
            btnClose.Location = new Point(786, 658);
            btnClose.Size = new Size(82, 40);
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
            this.CancelButton = btnClose;

            this.ResumeLayout(false);
        }

        protected override bool OnBeforeEscapeClose()
        {
            if (dgvPreview != null && dgvPreview.IsCurrentCellInEditMode)
            {
                dgvPreview.CancelEdit();
                dgvPreview.EndEdit();
                return true;
            }
            return false;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_boldGridFont != null)
            {
                _boldGridFont.Dispose();
                _boldGridFont = null;
            }
            base.OnFormClosed(e);
        }

        private void SetupPreviewGridColumns()
        {
            dgvPreview.Columns.Clear();

            var colStt = new DataGridViewTextBoxColumn();
            colStt.HeaderText = "STT";
            colStt.FillWeight = 28;
            colStt.ReadOnly = true;
            dgvPreview.Columns.Add(colStt);

            var colKind = new DataGridViewTextBoxColumn();
            colKind.HeaderText = "Phân Loại Đối Tượng";
            colKind.FillWeight = 72;
            colKind.ReadOnly = true;
            dgvPreview.Columns.Add(colKind);

            var colSize = new DataGridViewTextBoxColumn();
            colSize.HeaderText = "Kích Thước WxH (mm)";
            colSize.FillWeight = 115;
            colSize.ReadOnly = false; // Kỹ sư có thể gõ sửa trực tiếp nếu muốn!
            colSize.ToolTipText = "Cú pháp chuẩn HVAC: W x H (ví dụ: 350 x 300 — số đầu là Rộng W, số sau là Cao H).";
            dgvPreview.Columns.Add(colSize);

            var colLen = new DataGridViewTextBoxColumn();
            colLen.HeaderText = "Chiều Dài";
            colLen.FillWeight = 58;
            colLen.ReadOnly = true;
            dgvPreview.Columns.Add(colLen);

            var colElev = new DataGridViewTextBoxColumn();
            colElev.HeaderText = "Cao Độ Tim (mm)";
            colElev.FillWeight = 70;
            colElev.ReadOnly = true;
            dgvPreview.Columns.Add(colElev);

            var colNext = new DataGridViewTextBoxColumn();
            colNext.HeaderText = "Kết Nối Nối Tiếp";
            colNext.FillWeight = 95;
            colNext.ReadOnly = true;
            dgvPreview.Columns.Add(colNext);

            var colSource = new DataGridViewTextBoxColumn();
            colSource.HeaderText = "Nguồn Kích Thước";
            colSource.FillWeight = 112;
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
                    this.BeginInvoke((MethodInvoker)delegate { this.Show(); this.BringToFront(); this.Activate(); });
                }
            });
        }

        // =========================================================================
        // ACTIONS: NHẬN DIỆN BẰNG HÌNH VẼ
        // =========================================================================

        private void BtnPickChain_Click(object sender, EventArgs e)
        {
            if (_cadLinks == null || cmbCadLinks.SelectedIndex < 0 || cmbCadLinks.SelectedIndex >= _cadLinks.Count)
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
                    this.BeginInvoke((MethodInvoker)delegate { this.Show(); this.BringToFront(); this.Activate(); });
                }
            });
        }

        private void BtnPickRegion_Click(object sender, EventArgs e)
        {
            if (_cadLinks == null || cmbCadLinks.SelectedIndex < 0 || cmbCadLinks.SelectedIndex >= _cadLinks.Count)
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
                    this.BeginInvoke((MethodInvoker)delegate { this.Show(); this.BringToFront(); this.Activate(); });
                }
            });
        }

        private void BtnScanAll_Click(object sender, EventArgs e)
        {
            if (_cadLinks == null || cmbCadLinks.SelectedIndex < 0 || cmbCadLinks.SelectedIndex >= _cadLinks.Count)
            {
                SetStatus(Color.Red, "Vui lòng chọn 1 CAD Link trước.");
                return;
            }
            ImportInstance cad = _cadLinks[cmbCadLinks.SelectedIndex];
            ExecuteAnalyze(cad, null, null);
        }

        private string GetTextControlSafe(System.Windows.Forms.Control ctrl)
        {
            if (ctrl == null || ctrl.IsDisposed) return string.Empty;
            if (this.InvokeRequired)
            {
                string res = string.Empty;
                try
                {
                    this.Invoke((MethodInvoker)delegate { res = ctrl.Text; });
                }
                catch { }
                return res;
            }
            return ctrl.Text;
        }

        private string GetSelectedLayerSafe()
        {
            if (cmbDuctLayer == null || cmbDuctLayer.IsDisposed) return null;
            if (this.InvokeRequired)
            {
                string res = null;
                try
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        res = cmbDuctLayer.SelectedItem != null ? cmbDuctLayer.SelectedItem.ToString() : null;
                    });
                }
                catch { }
                return res;
            }
            return cmbDuctLayer.SelectedItem != null ? cmbDuctLayer.SelectedItem.ToString() : null;
        }

        private bool TryParseElevationMm(out double elevMm)
        {
            elevMm = 0;
            string raw = GetTextControlSafe(txtElev);
            if (string.IsNullOrWhiteSpace(raw)) return false;

            string normalized = raw.Trim().Replace(',', '.');
            double parsed;
            if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                return false;
            if (double.IsNaN(parsed) || double.IsInfinity(parsed))
                return false;

            elevMm = parsed;
            return true;
        }

        private void TxtElev_TextChanged(object sender, EventArgs e)
        {
            txtElev.BackColor = Color.White;
            UpdatePreviewElevationColumn();
        }

        private void UpdatePreviewElevationColumn()
        {
            if (dgvPreview == null || dgvPreview.Columns.Count <= 4) return;

            double elevMm;
            bool hasElev = TryParseElevationMm(out elevMm);
            for (int r = 0; r < dgvPreview.Rows.Count; r++)
            {
                DataGridViewCell cell = dgvPreview.Rows[r].Cells[4];
                if (hasElev)
                {
                    cell.Value = string.Format("{0:N0} mm", elevMm);
                    cell.Style.ForeColor = Color.FromArgb(15, 23, 42);
                    if (_currentStraightDucts != null && r < _currentStraightDucts.Count)
                    {
                        _currentStraightDucts[r].CenterElevationMm = elevMm;
                    }
                }
                else
                {
                    cell.Value = "Chưa nhập";
                    cell.Style.ForeColor = Color.FromArgb(180, 83, 9);
                }
            }
        }

        private void DgvPreview_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex != 2 || e.RowIndex < 0) return;
            if (_currentStraightDucts == null || e.RowIndex >= _currentStraightDucts.Count) return;

            CadDuctSegment seg = _currentStraightDucts[e.RowIndex];
            DataGridViewRow row = dgvPreview.Rows[e.RowIndex];
            if (SyncSizeCellToSegment(e.RowIndex))
            {
                row.Cells[2].Value = string.Format("{0} x {1}", (int)Math.Round(seg.WidthMm), (int)Math.Round(seg.HeightMm));
                row.Cells[2].Style.ForeColor = Color.FromArgb(15, 23, 42);
                row.Cells[2].Style.BackColor = Color.White;
                row.Cells[2].Style.Font = dgvPreview.Font;
                row.Cells[6].Value = seg.SizeSource;
                row.Cells[6].Style.ForeColor = Color.FromArgb(15, 23, 42);
            }
            else if (seg.HeightMm <= 0)
            {
                row.Cells[2].Value = "Chưa có H (nhập tại đây)";
                row.Cells[2].Style.ForeColor = Color.FromArgb(180, 83, 9);
                row.Cells[2].Style.Font = _boldGridFont ?? dgvPreview.Font;
            }
            else
            {
                row.Cells[2].Value = string.Format("{0} x {1}", (int)Math.Round(seg.WidthMm), (int)Math.Round(seg.HeightMm));
            }
        }

        private bool SyncSizeCellToSegment(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= dgvPreview.Rows.Count || _currentStraightDucts == null || rowIndex >= _currentStraightDucts.Count)
                return false;

            object val = dgvPreview.Rows[rowIndex].Cells[2].Value;
            if (val == null) return false;

            string str = val.ToString().Trim();
            if (string.IsNullOrEmpty(str)) return false;

            CadDuctSegment seg = _currentStraightDucts[rowIndex];
            // Chuẩn HVAC: số đầu tiên luôn là Rộng W, số thứ hai luôn là Cao H
            Match matchWxH = Regex.Match(str, @"(\d+(?:[\.,]\d+)?)\s*[xX\*×]\s*(\d+(?:[\.,]\d+)?)");
            if (matchWxH.Success)
            {
                double w;
                double h;
                if (double.TryParse(matchWxH.Groups[1].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out w) &&
                    double.TryParse(matchWxH.Groups[2].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out h) &&
                    w > 0 && h > 0)
                {
                    bool changed = Math.Abs(seg.WidthMm - w) > 0.01 || Math.Abs(seg.HeightMm - h) > 0.01;
                    seg.WidthMm = w;
                    seg.HeightMm = h;
                    if (changed) seg.SizeSource = "Người dùng nhập tay";
                    return true;
                }
            }

            Match matchSingleH = Regex.Match(str, @"^\s*(\d+(?:[\.,]\d+)?)\s*$");
            if (matchSingleH.Success && seg.WidthMm > 0)
            {
                double hOnly;
                if (double.TryParse(matchSingleH.Groups[1].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out hOnly) && hOnly > 0)
                {
                    seg.HeightMm = hOnly;
                    seg.SizeSource = "Người dùng nhập H thủ công";
                    return true;
                }
            }

            return false;
        }

        private void ExecuteAnalyze(ImportInstance cad, Outline pickBox, XYZ pickSeed)
        {
            double parsedElev;
            double centerElevMm = TryParseElevationMm(out parsedElev) ? parsedElev : 0;
            string selectedLayer = GetSelectedLayerSafe();

            SetStatus(Color.FromArgb(2, 132, 199), "⏳ Đang nhận diện hình học (Ống, Côn, Cút cong/góc, Chân rẽ) và trích xuất Text WxH từ file DWG gốc...");

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
                        0,
                        out ducts,
                        out fittings,
                        out edges,
                        out dampers);

                    _currentStraightDucts = ducts;
                    _currentFittings = fittings;
                    _currentEdges = edges;
                    _currentDampers = dampers;
                    _lastDwgDiagnostic = CadDwgExtractor.LastDiagnostic ?? string.Empty;

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
            dgvPreview.SuspendLayout();
            dgvPreview.Rows.Clear();

            double elevMm;
            bool hasElev = TryParseElevationMm(out elevMm);
            int missingHeightCount = 0;
            int resolvedSizeCount = 0;
            int eiAdjustedCount = 0;

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

            Dictionary<string, int> indexById = new Dictionary<string, int>(_currentStraightDucts.Count);
            for (int i = 0; i < _currentStraightDucts.Count; i++)
            {
                CadDuctSegment d = _currentStraightDucts[i];
                if (d != null && !string.IsNullOrEmpty(d.Id) && !indexById.ContainsKey(d.Id))
                {
                    indexById[d.Id] = i + 1;
                }
            }

            List<DataGridViewRow> rowsToAdd = new List<DataGridViewRow>(_currentStraightDucts.Count);

            for (int i = 0; i < _currentStraightDucts.Count; i++)
            {
                CadDuctSegment d = _currentStraightDucts[i];
                if (hasElev)
                {
                    d.CenterElevationMm = elevMm;
                }

                string nextDesc = "Đoạn cuối / Tự do";

                var conns = d.Connections;
                if (conns != null && conns.Count > 0)
                {
                    List<string> parts = new List<string>();
                    foreach (var c in conns)
                    {
                        CadDuctSegment other = (c.SegmentA == d) ? c.SegmentB : c.SegmentA;
                        int otherNum = 0;
                        if (other != null && !string.IsNullOrEmpty(other.Id))
                        {
                            indexById.TryGetValue(other.Id, out otherNum);
                        }
                        string otherName = otherNum > 0 ? ("#" + otherNum) : "";

                        if (c.Kind == CadConnectionKind.Transition && other != null)
                        {
                            if (other.HeightMm > 0)
                                parts.Add(string.Format("Côn thu -> {0} ({1}x{2})", otherName, (int)Math.Round(other.WidthMm), (int)Math.Round(other.HeightMm)));
                            else
                                parts.Add(string.Format("Côn thu -> {0} (W={1}, chưa có H)", otherName, (int)Math.Round(other.WidthMm)));
                        }
                        else if (c.Kind == CadConnectionKind.TakeoffTap)
                            parts.Add(string.Format("Chân rẽ Tap {0} {1}", (c.SegmentA == d ? "đón nhánh" : "vào sườn"), otherName));
                        else if (c.Kind == CadConnectionKind.Elbow)
                            parts.Add(string.Format("Cút chuyển hướng -> {0}", otherName));
                        else if (c.Kind == CadConnectionKind.TeeBranch)
                            parts.Add(string.Format("Chạc chia Tee -> {0}", otherName));
                        else
                            parts.Add("Nối tiếp " + otherName);
                    }
                    nextDesc = string.Join("; ", parts.ToArray());
                }

                bool hasHeight = d.HeightMm > 0;
                if (!hasHeight) missingHeightCount++;
                else resolvedSizeCount++;

                bool isEiAdjusted = !string.IsNullOrEmpty(d.SizeSource) && d.SizeSource.Contains("[Hình học ~");
                if (isEiAdjusted) eiAdjustedCount++;

                int wDisp = (int)Math.Round(d.WidthMm);
                int hDisp = (int)Math.Round(d.HeightMm);

                string sizeText = hasHeight
                    ? string.Format("{0} x {1}", wDisp, hDisp)
                    : "Chưa có H (nhập tại đây)";
                string elevText = hasElev ? string.Format("{0:N0} mm", elevMm) : "Chưa nhập";

                DataGridViewRow row = new DataGridViewRow();
                row.CreateCells(
                    dgvPreview,
                    (i + 1).ToString(),
                    "Ống gió thẳng",
                    sizeText,
                    string.Format("{0:N0} mm", d.Length),
                    elevText,
                    nextDesc,
                    d.SizeSource);

                if (!hasHeight)
                {
                    row.Cells[2].Style.ForeColor = Color.FromArgb(180, 83, 9);
                    row.Cells[2].Style.Font = _boldGridFont ?? dgvPreview.Font;
                    row.Cells[2].ToolTipText = string.Format("Bề rộng đo từ CAD: {0} mm. Nhập WxH (ví dụ: {0}x300 — số đầu W, số sau H) hoặc chỉ nhập H.", wDisp);
                }
                else if (isEiAdjusted)
                {
                    row.Cells[2].Style.ForeColor = Color.FromArgb(2, 132, 199);
                    row.Cells[2].ToolTipText = "Đã ưu tiên chuẩn WxH theo Text CAD (bề rộng nét CAD có lớp bọc/EI60 rộng hơn).";
                    row.Cells[6].Style.ForeColor = Color.FromArgb(2, 132, 199);
                }

                if (!hasElev)
                {
                    row.Cells[4].Style.ForeColor = Color.FromArgb(180, 83, 9);
                }

                rowsToAdd.Add(row);
            }

            if (rowsToAdd.Count > 0)
            {
                dgvPreview.Rows.AddRange(rowsToAdd.ToArray());
            }
            dgvPreview.ResumeLayout();

            lblPreviewSummary.Text = string.Format(
                "✔ Nhận diện: {0} ống thẳng ({1} có size, {2} thiếu H) | {3} côn | {4} cút | {5} Tap{6} | {7} van",
                _currentStraightDucts.Count,
                resolvedSizeCount,
                missingHeightCount,
                transitionCount,
                elbowCount,
                takeoffCount,
                teeCount > 0 ? string.Format(" | {0} Tee", teeCount) : "",
                _currentDampers.Count);
            toolTip.SetToolTip(lblPreviewSummary, lblPreviewSummary.Text);

            bool diagHasError = !string.IsNullOrEmpty(_lastDwgDiagnostic) &&
                (_lastDwgDiagnostic.Contains("Không tìm thấy") ||
                 _lastDwgDiagnostic.Contains("Lỗi") ||
                 _lastDwgDiagnostic.Contains(": 0 text "));

            if (_currentStraightDucts.Count == 0)
            {
                if (!string.IsNullOrEmpty(_lastDwgDiagnostic))
                {
                    lblTextDiag.Text = _lastDwgDiagnostic;
                    lblTextDiag.ForeColor = diagHasError ? Color.FromArgb(185, 28, 28) : Color.FromArgb(100, 116, 139);
                    toolTip.SetToolTip(lblTextDiag, _lastDwgDiagnostic);
                }
                else
                {
                    lblTextDiag.ForeColor = Color.FromArgb(100, 116, 139);
                    lblTextDiag.Text = "Chẩn đoán Text CAD: Chưa nhận diện được đoạn ống gió nào từ CAD Link.";
                    toolTip.SetToolTip(lblTextDiag, lblTextDiag.Text);
                }

                SetStatus(
                    diagHasError ? Color.Red : Color.FromArgb(234, 88, 12),
                    diagHasError
                        ? string.Format("⚠ {0}", _lastDwgDiagnostic)
                        : "Không tìm thấy đoạn ống gió hợp lệ (đã tự loại bỏ miệng gió, khung IAQ, van và thiết bị phòng máy). Hãy click đúng tuyến ống.");
            }
            else
            {
                string diagDisplay = !string.IsNullOrEmpty(_lastDwgDiagnostic)
                    ? _lastDwgDiagnostic
                    : "⚠ Không đọc được thông tin chẩn đoán Text từ file DWG gốc.";
                if (eiAdjustedCount > 0)
                {
                    diagDisplay = diagDisplay + string.Format(" | {0} đoạn giữ chuẩn WxH theo Text (bọc EI)", eiAdjustedCount);
                }

                lblTextDiag.Text = diagDisplay;
                lblTextDiag.ForeColor = diagHasError
                    ? Color.FromArgb(185, 28, 28)
                    : (eiAdjustedCount > 0 ? Color.FromArgb(2, 132, 199) : Color.FromArgb(21, 128, 61));
                toolTip.SetToolTip(lblTextDiag, diagDisplay);

                if (missingHeightCount > 0 && diagHasError)
                {
                    SetStatus(Color.Red, string.Format("⚠ {0} ({1} đoạn thiếu H). Hãy kiểm tra file DWG gốc hoặc nhập WxH trực tiếp vào bảng!", _lastDwgDiagnostic, missingHeightCount));
                }
                else if (missingHeightCount > 0)
                {
                    SetStatus(Color.FromArgb(180, 83, 9), string.Format("⚠ Còn {0} đoạn chưa có chiều cao H. Hãy nhập WxH vào cột [Kích Thước WxH (mm)] và tự nhập Cao độ tim trước khi dựng 3D.", missingHeightCount));
                }
                else if (!hasElev)
                {
                    SetStatus(Color.FromArgb(2, 132, 199), "✔ Nhận diện hoàn tất! Vui lòng tự nhập [Cao Độ Tim Hệ Thống (mm)] ở mục 4 rồi bấm [DỰNG MÔ HÌNH REVIT 3D].");
                }
                else
                {
                    SetStatus(Color.FromArgb(16, 185, 129), "✔ Nhận diện hình học hoàn tất! Kiểm tra danh sách ở trên và bấm [DỰNG MÔ HÌNH REVIT 3D].");
                }
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

            dgvPreview.EndEdit();

            double elevMm;
            if (!TryParseElevationMm(out elevMm))
            {
                txtElev.BackColor = Color.FromArgb(254, 226, 226);
                txtElev.Focus();
                SetStatus(Color.Red, "⚠ Vui lòng nhập Cao độ tim ống gió (mm) trước khi dựng mô hình 3D!");
                return;
            }

            // Đồng bộ lại kích thước nếu kỹ sư có sửa trong bảng DataGridView & áp dụng Cao độ tim cho toàn bộ đoạn ống
            for (int r = 0; r < _currentStraightDucts.Count; r++)
            {
                if (r < dgvPreview.Rows.Count)
                {
                    SyncSizeCellToSegment(r);
                }
                _currentStraightDucts[r].CenterElevationMm = elevMm;
            }
            UpdatePreviewElevationColumn();

            for (int i = 0; i < _currentStraightDucts.Count; i++)
            {
                CadDuctSegment seg = _currentStraightDucts[i];
                if (seg.WidthMm <= 0 || seg.HeightMm <= 0)
                {
                    int wWarn = (int)Math.Round(seg.WidthMm);
                    if (i < dgvPreview.Rows.Count)
                    {
                        dgvPreview.CurrentCell = dgvPreview.Rows[i].Cells[2];
                        dgvPreview.Rows[i].Cells[2].Style.BackColor = Color.FromArgb(254, 226, 226);
                    }
                    dgvPreview.Focus();
                    SetStatus(Color.Red, string.Format("⚠ Đoạn ống #{0} (W={1} mm) chưa có chiều cao H! Vui lòng nhập WxH (ví dụ: {1}x300) vào bảng trước khi dựng 3D.", i + 1, wWarn));
                    return;
                }
            }

            ElementId sysTypeId = _systemTypes[cmbSystemType.SelectedIndex].Id;
            ElementId ductTypeId = _ductTypes[cmbDuctType.SelectedIndex].Id;
            ElementId levelId = _levels[cmbLevel.SelectedIndex].Id;

            bool useConcentric = chkConcentric.Checked;
            bool useTap = chkTakeoffTap.Checked;

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

                    SetStatus(count > 0 ? Color.FromArgb(16, 185, 129) : Color.Red, report);

                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        TaskDialog.Show(count > 0 ? "HDTOOL - DỰNG ỐNG GIÓ 3D THÀNH CÔNG" : "HDTOOL - THÔNG BÁO DỰNG ỐNG GIÓ", report);
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
