using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Color = System.Drawing.Color;
using Font = System.Drawing.Font;
using Point = System.Drawing.Point;

namespace HDTool
{
    public class ExportSheetListForm : HDToolBaseForm
    {
        private Document _doc;
        private List<SheetItem> _allSheets;
        private List<SheetItem> _filteredSheets;
        private bool _isUpdatingCheck = false;
        private int _lastCheckedRowIndex = -1;
        private bool _lastTargetCheckState = true;
        private string _lastCopiedGroupTitle = string.Empty;
        private bool _isRebuildingTree = false;

        // Context Menu & Shortcuts
        private ContextMenuStrip cmsGrid;
        private ToolStripMenuItem miCheckSelected;
        private ToolStripMenuItem miUncheckSelected;
        private ToolStripMenuItem miInvertSelected;
        private ToolStripMenuItem miCopyGroup;
        private ToolStripMenuItem miPasteGroup;
        private ToolStripMenuItem miFillDown;
        private ToolStripMenuItem miFillEmpty;
        private ToolStripMenuItem miQuickAssignSubmenu;
        private ToolStripMenuItem miMoveUp;
        private ToolStripMenuItem miMoveDown;
        private ToolStripMenuItem miSetGroup;
        private ToolStripMenuItem miBatchEditNumber;

        // UI Controls
        private System.Windows.Forms.Panel pnlHeader;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblSubtitle;
        private PictureBox picLogo;

        private System.Windows.Forms.GroupBox grpFilter;
        private System.Windows.Forms.Label lblSearch;
        private System.Windows.Forms.TextBox txtSearch;
        private System.Windows.Forms.Button btnMoveUp;
        private System.Windows.Forms.Button btnMoveDown;
        private System.Windows.Forms.Button btnSelectAll;
        private System.Windows.Forms.Button btnDeselectAll;
        private System.Windows.Forms.Button btnInvert;
        private System.Windows.Forms.Button btnGroupFilter;
        private System.Windows.Forms.Button btnBatchEditNumber;

        // TreeView & SplitContainer Controls
        private SplitContainer splitMain;
        private System.Windows.Forms.Panel pnlTreeContainer;
        private System.Windows.Forms.Panel pnlTreeHeader;
        private System.Windows.Forms.Label lblTreeTitle;
        private System.Windows.Forms.Button btnNewGroup;
        private System.Windows.Forms.Button btnAutoGroup;
        private System.Windows.Forms.TreeView tvGroups;
        private ImageList imgListTree;
        private ContextMenuStrip cmsTree;
        private System.Windows.Forms.Panel pnlFolderBreadcrumb;
        private System.Windows.Forms.Label lblCurrentFolder;
        private System.Windows.Forms.Button btnViewAll;
        private System.Windows.Forms.Button btnGroupSelected;
        private System.Windows.Forms.Button btnPasteGroup;
        private System.Windows.Forms.Button btnFillUnassigned;

        private string _selectedTreeGroup = null;
        private TreeNode _rootNode;
        private bool _isUpdatingTreeCheck = false;
        private Point _dragStartPoint = Point.Empty;

        private DataGridView dgvSheets;
        private DataGridViewCheckBoxColumn colCheck;
        private DataGridViewTextBoxColumn colStt;
        private DataGridViewTextBoxColumn colNumber;
        private DataGridViewTextBoxColumn colName;
        private DataGridViewTextBoxColumn colGroup;
        private DataGridViewTextBoxColumn colRev;
        private DataGridViewTextBoxColumn colDate;
        private DataGridViewTextBoxColumn colDrawn;

        private System.Windows.Forms.Panel pnlBottom;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.CheckBox chkOpenAfter;
        private System.Windows.Forms.Button btnSaveToRevit;
        private System.Windows.Forms.Button btnExport;
        private System.Windows.Forms.Button btnExportPdf;
        private System.Windows.Forms.Button btnExportCad;
        private System.Windows.Forms.Button btnExportCombinedCad;
        private System.Windows.Forms.Button btnClipboard;
        private System.Windows.Forms.Button btnUpdateExcel;
        private System.Windows.Forms.Button btnUpdateClipboard;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.ToolTip toolTip;

        public ExportSheetListForm(Document doc)
        {
            _doc = doc;
            _allSheets = ExportSheetListService.GetSheetsFromDocument(doc);
            _filteredSheets = new List<SheetItem>(_allSheets);

            try
            {
                Application.ThreadException -= Form_ThreadException;
                Application.ThreadException += Form_ThreadException;
            }
            catch { }

            InitializeComponent();
            RebuildTreeNodes();
            RefreshGridData();
            UpdateSaveButtonState();
        }

        private void Form_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            Debug.WriteLine("Suppressed Form_ThreadException: " + e.Exception.Message);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            try
            {
                Application.ThreadException -= Form_ThreadException;
            }
            catch { }
            base.OnFormClosed(e);
        }

        protected override void WndProc(ref Message m)
        {
            try
            {
                base.WndProc(ref m);
            }
            catch (InvalidOperationException ex)
            {
                Debug.WriteLine("Suppressed WndProc InvalidOperationException: " + ex.Message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Suppressed WndProc Exception: " + ex.Message);
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if ((keyData & Keys.KeyCode) == Keys.Escape)
            {
                if (dgvSheets != null && dgvSheets.IsCurrentCellInEditMode)
                {
                    try { dgvSheets.CancelEdit(); } catch { }
                }

                // Nhấn phím ESC là thoát/đóng cửa sổ làm việc ngay lập tức theo lệnh Sếp Hồ
                this.Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.Text = "HDTOOL - Quản Lý & Chỉnh Sửa Số Hiệu, Tên Bản Vẽ / Xuất Bản Vẽ";
            this.Size = new Size(1250, 680);
            this.MinimumSize = new Size(1150, 560);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.TopMost = false;
            this.BackColor = Color.FromArgb(245, 247, 250);
            this.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

            toolTip = new System.Windows.Forms.ToolTip();

            // 1. Header Panel
            pnlHeader = new System.Windows.Forms.Panel();
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = 75;
            pnlHeader.BackColor = Color.FromArgb(24, 43, 73); // Navy HadoMEE

            lblTitle = new System.Windows.Forms.Label();
            lblTitle.Text = "HDTOOL - QUẢN LÝ & XUẤT HỒ SƠ BẢN VẼ (SHEET LIST)";
            lblTitle.ForeColor = Color.White;
            lblTitle.Font = new Font("Segoe UI", 11.5f, FontStyle.Bold);
            lblTitle.Location = new Point(16, 14);
            lblTitle.AutoSize = true;

            lblSubtitle = new System.Windows.Forms.Label();
            lblSubtitle.Text = "Nhấp chuột trực tiếp để sửa Số Hiệu, Tên Bản Vẽ ➔ Sắp Xếp Tự Do (▲/▼) ➔ Xuất PDF, CAD hoặc Lưu vào Revit!";
            lblSubtitle.ForeColor = Color.FromArgb(189, 195, 199);
            lblSubtitle.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblSubtitle.Location = new Point(16, 42);
            lblSubtitle.AutoSize = true;

            picLogo = new PictureBox();
            picLogo.Size = new Size(130, 45);
            picLogo.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            picLogo.Location = new Point(1020, 15);
            picLogo.SizeMode = PictureBoxSizeMode.Zoom;
            picLogo.BackColor = Color.Transparent;
            try
            {
                using (Stream stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("hadomee_logo.png"))
                {
                    if (stream != null) picLogo.Image = Image.FromStream(stream);
                }
            }
            catch { }

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);
            pnlHeader.Controls.Add(picLogo);

            // 2. Filter & Selection Group
            grpFilter = new System.Windows.Forms.GroupBox();
            grpFilter.Text = "Bộ Lọc & Sắp Xếp Bản Vẽ";
            grpFilter.Dock = DockStyle.Top;
            grpFilter.Height = 82;
            grpFilter.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            grpFilter.ForeColor = Color.FromArgb(44, 62, 80);
            grpFilter.Padding = new Padding(10, 5, 10, 5);

            lblSearch = new System.Windows.Forms.Label();
            lblSearch.Text = "Tìm kiếm:";
            lblSearch.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblSearch.Location = new Point(16, 26);
            lblSearch.AutoSize = true;

            txtSearch = new System.Windows.Forms.TextBox();
            txtSearch.Location = new Point(80, 23);
            txtSearch.Size = new Size(240, 23);
            txtSearch.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            txtSearch.TextChanged += TxtSearch_TextChanged;
            toolTip.SetToolTip(txtSearch, "Nhập số hiệu hoặc tên bản vẽ để tìm kiếm");

            btnMoveUp = new System.Windows.Forms.Button();
            btnMoveUp.Text = "▲ Lên";
            btnMoveUp.Location = new Point(340, 21);
            btnMoveUp.Size = new Size(70, 27);
            btnMoveUp.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            btnMoveUp.BackColor = Color.FromArgb(236, 240, 241);
            btnMoveUp.ForeColor = Color.FromArgb(41, 128, 185);
            btnMoveUp.FlatStyle = FlatStyle.Flat;
            btnMoveUp.Cursor = Cursors.Hand;
            btnMoveUp.Click += BtnMoveUp_Click;
            toolTip.SetToolTip(btnMoveUp, "Di chuyển bản vẽ đã chọn lên trên");

            btnMoveDown = new System.Windows.Forms.Button();
            btnMoveDown.Text = "▼ Xuống";
            btnMoveDown.Location = new Point(418, 21);
            btnMoveDown.Size = new Size(78, 27);
            btnMoveDown.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            btnMoveDown.BackColor = Color.FromArgb(236, 240, 241);
            btnMoveDown.ForeColor = Color.FromArgb(41, 128, 185);
            btnMoveDown.FlatStyle = FlatStyle.Flat;
            btnMoveDown.Cursor = Cursors.Hand;
            btnMoveDown.Click += BtnMoveDown_Click;
            toolTip.SetToolTip(btnMoveDown, "Di chuyển bản vẽ đã chọn xuống dưới");

            btnSelectAll = new System.Windows.Forms.Button();
            btnSelectAll.Text = "✓ Tất Cả";
            btnSelectAll.Location = new Point(516, 21);
            btnSelectAll.Size = new Size(75, 27);
            btnSelectAll.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            btnSelectAll.Click += (s, e) => SetSelectionState(true);

            btnDeselectAll = new System.Windows.Forms.Button();
            btnDeselectAll.Text = "✗ Bỏ Chọn";
            btnDeselectAll.Location = new Point(598, 21);
            btnDeselectAll.Size = new Size(80, 27);
            btnDeselectAll.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            btnDeselectAll.Click += (s, e) => SetSelectionState(false);

            btnInvert = new System.Windows.Forms.Button();
            btnInvert.Text = "🔄 Đảo";
            btnInvert.Location = new Point(628, 20);
            btnInvert.Size = new Size(62, 27);
            btnInvert.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            btnInvert.Click += (s, e) => InvertSelection();

            btnGroupFilter = new System.Windows.Forms.Button();
            btnGroupFilter.Text = "📁 Gom Nhóm...";
            btnGroupFilter.Location = new Point(696, 20);
            btnGroupFilter.Size = new Size(105, 27);
            btnGroupFilter.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            btnGroupFilter.BackColor = Color.FromArgb(236, 240, 241);
            btnGroupFilter.ForeColor = Color.FromArgb(41, 128, 185);
            btnGroupFilter.FlatStyle = FlatStyle.Flat;
            btnGroupFilter.Cursor = Cursors.Hand;
            btnGroupFilter.Click += (s, e) => OpenGroupTitleDialog();
            toolTip.SetToolTip(btnGroupFilter, "Gom các bản vẽ đã chọn vào một thư mục nhóm riêng");

            btnBatchEditNumber = new System.Windows.Forms.Button();
            btnBatchEditNumber.Text = "✏  ĐỔI SỐ HIỆU...";
            btnBatchEditNumber.Location = new Point(808, 20);
            btnBatchEditNumber.Size = new Size(160, 27);
            btnBatchEditNumber.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            btnBatchEditNumber.BackColor = Color.FromArgb(142, 68, 173); // Purple Amethyst
            btnBatchEditNumber.ForeColor = Color.White;
            btnBatchEditNumber.FlatStyle = FlatStyle.Flat;
            btnBatchEditNumber.FlatAppearance.BorderSize = 0;
            btnBatchEditNumber.Cursor = Cursors.Hand;
            btnBatchEditNumber.Click += (s, e) => OpenBatchEditSheetNumberDialog();
            toolTip.SetToolTip(btnBatchEditNumber, "Sửa hoặc thay thế số hiệu bản vẽ hàng loạt theo tiền tố giống nhau (Tìm & Thay thế, Thêm tiền tố/hậu tố, Đánh số thứ tự)");

            grpFilter.Controls.Add(lblSearch);
            grpFilter.Controls.Add(txtSearch);
            grpFilter.Controls.Add(btnMoveUp);
            grpFilter.Controls.Add(btnMoveDown);
            grpFilter.Controls.Add(btnSelectAll);
            grpFilter.Controls.Add(btnDeselectAll);
            grpFilter.Controls.Add(btnInvert);
            grpFilter.Controls.Add(btnGroupFilter);
            grpFilter.Controls.Add(btnBatchEditNumber);

            System.Windows.Forms.Label lblHint = new System.Windows.Forms.Label();
            lblHint.Text = "💡 Giữ Shift chọn dải  |  Phím Cách (Space) bật/tắt chọn  |  Ctrl+C / Ctrl+V / Ctrl+D sao chép & điền nhóm  |  Kéo thả bản vẽ vào Cây Thư Mục";
            lblHint.Location = new Point(16, 52);
            lblHint.AutoSize = true;
            lblHint.ForeColor = Color.FromArgb(70, 95, 120);
            lblHint.Font = new Font("Segoe UI", 8.25f, FontStyle.Italic);
            grpFilter.Controls.Add(lblHint);

            // 3. SplitContainer & TreeView Setup
            splitMain = new SplitContainer();
            splitMain.Dock = DockStyle.Fill;
            splitMain.Orientation = Orientation.Vertical;
            splitMain.SplitterDistance = 290;
            splitMain.SplitterWidth = 5;
            splitMain.BackColor = Color.FromArgb(215, 220, 228);

            // Left: TreeView Container
            pnlTreeContainer = new System.Windows.Forms.Panel();
            pnlTreeContainer.Dock = DockStyle.Fill;
            pnlTreeContainer.BackColor = Color.White;

            pnlTreeHeader = new System.Windows.Forms.Panel();
            pnlTreeHeader.Dock = DockStyle.Top;
            pnlTreeHeader.Height = 36;
            pnlTreeHeader.BackColor = Color.FromArgb(235, 240, 246);

            lblTreeTitle = new System.Windows.Forms.Label();
            lblTreeTitle.Text = "📁 CÂY THƯ MỤC HỒ SƠ";
            lblTreeTitle.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            lblTreeTitle.ForeColor = Color.FromArgb(24, 43, 73);
            lblTreeTitle.Location = new Point(8, 9);
            lblTreeTitle.AutoSize = true;

            btnNewGroup = new System.Windows.Forms.Button();
            btnNewGroup.Text = "➕ Nhóm Mới";
            btnNewGroup.Font = new Font("Segoe UI", 8.0f, FontStyle.Bold);
            btnNewGroup.BackColor = Color.FromArgb(39, 174, 96);
            btnNewGroup.ForeColor = Color.White;
            btnNewGroup.FlatStyle = FlatStyle.Flat;
            btnNewGroup.FlatAppearance.BorderSize = 0;
            btnNewGroup.Cursor = Cursors.Hand;
            btnNewGroup.Location = new Point(148, 5);
            btnNewGroup.Size = new Size(82, 26);
            btnNewGroup.Click += (s, e) => OpenGroupTitleDialog();
            toolTip.SetToolTip(btnNewGroup, "Tạo thư mục nhóm mới và gán các bản vẽ đang chọn vào nhóm");

            btnAutoGroup = new System.Windows.Forms.Button();
            btnAutoGroup.Text = "⚡ Tự Động";
            btnAutoGroup.Font = new Font("Segoe UI", 8.0f, FontStyle.Bold);
            btnAutoGroup.BackColor = Color.FromArgb(41, 128, 185);
            btnAutoGroup.ForeColor = Color.White;
            btnAutoGroup.FlatStyle = FlatStyle.Flat;
            btnAutoGroup.FlatAppearance.BorderSize = 0;
            btnAutoGroup.Cursor = Cursors.Hand;
            btnAutoGroup.Location = new Point(234, 5);
            btnAutoGroup.Size = new Size(68, 26);
            btnAutoGroup.Click += (s, e) => AutoDetectDisciplines();
            toolTip.SetToolTip(btnAutoGroup, "Tự động phân nhóm bản vẽ theo bộ môn (Kiến trúc, Điện, Nước, Điều hòa, PCCC...)");

            pnlTreeHeader.Controls.Add(lblTreeTitle);
            pnlTreeHeader.Controls.Add(btnNewGroup);
            pnlTreeHeader.Controls.Add(btnAutoGroup);

            tvGroups = new System.Windows.Forms.TreeView();
            tvGroups.Dock = DockStyle.Fill;
            tvGroups.BorderStyle = BorderStyle.None;
            tvGroups.CheckBoxes = true;
            tvGroups.ShowLines = true;
            tvGroups.ShowPlusMinus = true;
            tvGroups.ShowRootLines = true;
            tvGroups.FullRowSelect = false;
            tvGroups.HideSelection = false;
            imgListTree = CreateTreeImageList();
            tvGroups.ImageList = imgListTree;
            tvGroups.AllowDrop = true;

            tvGroups.AfterSelect += TvGroups_AfterSelect;
            tvGroups.AfterCheck += TvGroups_AfterCheck;
            tvGroups.ItemDrag += TvGroups_ItemDrag;
            tvGroups.DragOver += TvGroups_DragOver;
            tvGroups.DragDrop += TvGroups_DragDrop;

            // Setup Context Menu for TreeView
            cmsTree = new ContextMenuStrip();
            cmsTree.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

            ToolStripMenuItem miTreeGroup = new ToolStripMenuItem("📁 Gom các bản vẽ đang chọn vào nhóm...");
            miTreeGroup.Click += (s, e) => OpenGroupTitleDialog();

            ToolStripMenuItem miTreeAuto = new ToolStripMenuItem("⚡ Tự động phân nhóm theo bộ môn (Kiến trúc, Điện, Nước, MEP...)");
            miTreeAuto.Click += (s, e) => AutoDetectDisciplines();

            ToolStripMenuItem miTreeRename = new ToolStripMenuItem("✏ Đổi tên thư mục nhóm này...");
            miTreeRename.Click += (s, e) => RenameSelectedGroup();

            ToolStripMenuItem miTreeCheck = new ToolStripMenuItem("✔ Tích chọn toàn bộ bản vẽ trong nhóm này");
            miTreeCheck.Click += (s, e) => CheckSelectedGroup(true);

            ToolStripMenuItem miTreeUncheck = new ToolStripMenuItem("✖ Bỏ chọn toàn bộ bản vẽ trong nhóm này");
            miTreeUncheck.Click += (s, e) => CheckSelectedGroup(false);

            ToolStripMenuItem miTreeDelete = new ToolStripMenuItem("🗑 Giải tán nhóm này (Chuyển về Chưa phân nhóm)");
            miTreeDelete.Click += (s, e) => DeleteSelectedGroup();

            cmsTree.Items.Add(miTreeGroup);
            cmsTree.Items.Add(miTreeAuto);
            cmsTree.Items.Add(new ToolStripSeparator());
            cmsTree.Items.Add(miTreeRename);
            cmsTree.Items.Add(miTreeCheck);
            cmsTree.Items.Add(miTreeUncheck);
            cmsTree.Items.Add(miTreeDelete);
            tvGroups.ContextMenuStrip = cmsTree;

            pnlTreeContainer.Controls.Add(tvGroups);
            pnlTreeContainer.Controls.Add(pnlTreeHeader);

            // Right: Folder Breadcrumb + Grid
            pnlFolderBreadcrumb = new System.Windows.Forms.Panel();
            pnlFolderBreadcrumb.Dock = DockStyle.Top;
            pnlFolderBreadcrumb.Height = 34;
            pnlFolderBreadcrumb.BackColor = Color.FromArgb(244, 247, 250);

            lblCurrentFolder = new System.Windows.Forms.Label();
            lblCurrentFolder.Text = "📁 Đang hiển thị: Tất Cả Bản Vẽ";
            lblCurrentFolder.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            lblCurrentFolder.ForeColor = Color.FromArgb(24, 43, 73);
            lblCurrentFolder.Location = new Point(10, 8);
            lblCurrentFolder.AutoSize = true;

            btnViewAll = new System.Windows.Forms.Button();
            btnViewAll.Text = "🔄 Xem Tất Cả";
            btnViewAll.Font = new Font("Segoe UI", 8.25f, FontStyle.Regular);
            btnViewAll.Location = new Point(235, 4);
            btnViewAll.Size = new Size(95, 26);
            btnViewAll.Click += (s, e) =>
            {
                _selectedTreeGroup = null;
                if (_rootNode != null) tvGroups.SelectedNode = _rootNode;
                lblCurrentFolder.Text = string.Format("📁 Đang hiển thị: Tất Cả Bản Vẽ ({0} bản vẽ)", _allSheets.Count);
                ApplyFilter();
            };

            btnGroupSelected = new System.Windows.Forms.Button();
            btnGroupSelected.Text = "📁 Gom Vào Nhóm...";
            btnGroupSelected.Font = new Font("Segoe UI", 8.25f, FontStyle.Bold);
            btnGroupSelected.BackColor = Color.FromArgb(41, 128, 185);
            btnGroupSelected.ForeColor = Color.White;
            btnGroupSelected.FlatStyle = FlatStyle.Flat;
            btnGroupSelected.FlatAppearance.BorderSize = 0;
            btnGroupSelected.Cursor = Cursors.Hand;
            btnGroupSelected.Location = new Point(335, 4);
            btnGroupSelected.Size = new Size(135, 26);
            btnGroupSelected.Click += (s, e) => OpenGroupTitleDialog();
            toolTip.SetToolTip(btnGroupSelected, "Gom các bản vẽ đang chọn vào một nhóm thư mục mới hoặc nhóm có sẵn");

            btnPasteGroup = new System.Windows.Forms.Button();
            btnPasteGroup.Text = "📋 Dán Nhóm (Ctrl+V)";
            btnPasteGroup.Font = new Font("Segoe UI", 8.25f, FontStyle.Bold);
            btnPasteGroup.BackColor = Color.FromArgb(39, 174, 96);
            btnPasteGroup.ForeColor = Color.White;
            btnPasteGroup.FlatStyle = FlatStyle.Flat;
            btnPasteGroup.FlatAppearance.BorderSize = 0;
            btnPasteGroup.Cursor = Cursors.Hand;
            btnPasteGroup.Location = new Point(475, 4);
            btnPasteGroup.Size = new Size(140, 26);
            btnPasteGroup.Click += (s, e) => PasteToSelectedRows();
            toolTip.SetToolTip(btnPasteGroup, "Dán tên nhóm đang sao chép vào tất cả các bản vẽ đang bôi đen (Phím tắt: Ctrl + V)");

            btnFillUnassigned = new System.Windows.Forms.Button();
            btnFillUnassigned.Text = "⚡ Điền Ô Trống";
            btnFillUnassigned.Font = new Font("Segoe UI", 8.25f, FontStyle.Bold);
            btnFillUnassigned.BackColor = Color.FromArgb(230, 126, 34);
            btnFillUnassigned.ForeColor = Color.White;
            btnFillUnassigned.FlatStyle = FlatStyle.Flat;
            btnFillUnassigned.FlatAppearance.BorderSize = 0;
            btnFillUnassigned.Cursor = Cursors.Hand;
            btnFillUnassigned.Location = new Point(620, 4);
            btnFillUnassigned.Size = new Size(120, 26);
            btnFillUnassigned.Click += (s, e) => FillEmptyGroupCells();
            toolTip.SetToolTip(btnFillUnassigned, "Tự động điền tên nhóm này vào tất cả các ô bản vẽ chưa phân nhóm mà không cần gõ lại");

            pnlFolderBreadcrumb.Controls.Add(lblCurrentFolder);
            pnlFolderBreadcrumb.Controls.Add(btnViewAll);
            pnlFolderBreadcrumb.Controls.Add(btnGroupSelected);
            pnlFolderBreadcrumb.Controls.Add(btnPasteGroup);
            pnlFolderBreadcrumb.Controls.Add(btnFillUnassigned);

            // 4. DataGridView Setup
            dgvSheets = new DataGridView();
            dgvSheets.Dock = DockStyle.Fill;
            dgvSheets.BackgroundColor = Color.White;
            dgvSheets.BorderStyle = BorderStyle.None;
            dgvSheets.RowHeadersVisible = false;
            dgvSheets.AllowUserToAddRows = false;
            dgvSheets.AllowUserToDeleteRows = false;
            dgvSheets.AllowUserToResizeRows = false;
            dgvSheets.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvSheets.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
            dgvSheets.MultiSelect = true;
            dgvSheets.RowTemplate.Height = 28;
            dgvSheets.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 252);
            dgvSheets.DefaultCellStyle.SelectionBackColor = Color.FromArgb(214, 234, 248);
            dgvSheets.DefaultCellStyle.SelectionForeColor = Color.Black;
            dgvSheets.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(235, 240, 245);
            dgvSheets.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(44, 62, 80);
            dgvSheets.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            dgvSheets.ColumnHeadersHeight = 32;
            dgvSheets.EnableHeadersVisualStyles = false;

            try
            {
                typeof(DataGridView).GetProperty("DoubleBuffered",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(dgvSheets, true, null);
            }
            catch { }

            // Cột 1: Checkbox
            colCheck = new DataGridViewCheckBoxColumn();
            colCheck.Name = "colCheck";
            colCheck.HeaderText = "Chọn";
            colCheck.Width = 45;
            colCheck.Frozen = true;
            dgvSheets.Columns.Add(colCheck);

            // Cột 2: STT (theo thứ tự tùy biến)
            colStt = new DataGridViewTextBoxColumn();
            colStt.Name = "colStt";
            colStt.HeaderText = "STT";
            colStt.Width = 45;
            colStt.ReadOnly = true;
            colStt.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvSheets.Columns.Add(colStt);

            // Cột 3: Số Hiệu Bản Vẽ (CHO PHÉP SỬA TRỰC TIẾP)
            colNumber = new DataGridViewTextBoxColumn();
            colNumber.Name = "colNumber";
            colNumber.HeaderText = "Số Hiệu (Sheet Number) ✎ [Sửa]";
            colNumber.Width = 150;
            colNumber.ReadOnly = false;
            colNumber.DefaultCellStyle.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            colNumber.DefaultCellStyle.ForeColor = Color.FromArgb(41, 128, 185);
            colNumber.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dgvSheets.Columns.Add(colNumber);

            // Cột 4: Tên Bản Vẽ (CHO PHÉP SỬA TRỰC TIẾP)
            colName = new DataGridViewTextBoxColumn();
            colName.Name = "colName";
            colName.HeaderText = "Tên Bản Vẽ (Sheet Name) ✎ [Sửa]";
            colName.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colName.ReadOnly = false;
            colName.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dgvSheets.Columns.Add(colName);

            // Cột 5: Thư Mục / Nhóm Bản Vẽ (CHO PHÉP SỬA TRỰC TIẾP HOẶC CHỌN TỪ CÂY)
            colGroup = new DataGridViewTextBoxColumn();
            colGroup.Name = "colGroup";
            colGroup.HeaderText = "Thư Mục / Nhóm ✎";
            colGroup.Width = 145;
            colGroup.ReadOnly = false;
            colGroup.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dgvSheets.Columns.Add(colGroup);

            // Cột 6: Phiên Bản
            colRev = new DataGridViewTextBoxColumn();
            colRev.Name = "colRev";
            colRev.HeaderText = "Phiên Bản ✎";
            colRev.Width = 80;
            colRev.ReadOnly = false;
            colRev.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvSheets.Columns.Add(colRev);

            // Cột 7: Ngày Phát Hành
            colDate = new DataGridViewTextBoxColumn();
            colDate.Name = "colDate";
            colDate.HeaderText = "Ngày Phát Hành ✎";
            colDate.Width = 110;
            colDate.ReadOnly = false;
            colDate.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvSheets.Columns.Add(colDate);

            // Cột 8: Người Vẽ
            colDrawn = new DataGridViewTextBoxColumn();
            colDrawn.Name = "colDrawn";
            colDrawn.HeaderText = "Người Vẽ ✎";
            colDrawn.Width = 90;
            colDrawn.ReadOnly = false;
            colDrawn.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvSheets.Columns.Add(colDrawn);

            dgvSheets.MouseDown += DgvSheets_MouseDown;
            dgvSheets.MouseMove += DgvSheets_MouseMove;

            dgvSheets.CurrentCellDirtyStateChanged += DgvSheets_CurrentCellDirtyStateChanged;
            dgvSheets.CellValueChanged += DgvSheets_CellValueChanged;
            dgvSheets.CellClick += DgvSheets_CellClick;
            dgvSheets.CellMouseDown += DgvSheets_CellMouseDown;
            dgvSheets.KeyDown += DgvSheets_KeyDown;
            dgvSheets.ColumnHeaderMouseClick += DgvSheets_ColumnHeaderMouseClick;
            dgvSheets.CellEndEdit += DgvSheets_CellEndEdit;
            dgvSheets.DataError += DgvSheets_DataError;
            dgvSheets.EditingControlShowing += DgvSheets_EditingControlShowing;

            // Setup Context Menu for dgvSheets
            cmsGrid = new ContextMenuStrip();
            cmsGrid.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

            miCheckSelected = new ToolStripMenuItem("✔ Tích chọn các dòng đang bôi đen (Space)");
            miCheckSelected.Click += (s, e) => SetSelectedRowsCheckState(true);

            miUncheckSelected = new ToolStripMenuItem("✖ Bỏ chọn các dòng đang bôi đen");
            miUncheckSelected.Click += (s, e) => SetSelectedRowsCheckState(false);

            miInvertSelected = new ToolStripMenuItem("◫ Đảo chọn các dòng đang bôi đen");
            miInvertSelected.Click += (s, e) => InvertSelectedRowsCheckState();

            miCopyGroup = new ToolStripMenuItem("📋 Sao chép tên nhóm (Ctrl+C)");
            miCopyGroup.Click += (s, e) => CopyCurrentSelection();

            miPasteGroup = new ToolStripMenuItem("📥 Dán tên nhóm vào các dòng đang chọn (Ctrl+V)");
            miPasteGroup.Click += (s, e) => PasteToSelectedRows();

            miFillDown = new ToolStripMenuItem("⚡ Điền tên nhóm xuống các dòng dưới (Ctrl+D)");
            miFillDown.Click += (s, e) => FillDownGroupTitle();

            miFillEmpty = new ToolStripMenuItem("⚡ Điền tên nhóm này vào tất cả ô chưa có nhóm");
            miFillEmpty.Click += (s, e) => FillEmptyGroupCells();

            miQuickAssignSubmenu = new ToolStripMenuItem("📁 Gán nhanh tên nhóm");

            miMoveUp = new ToolStripMenuItem("▲ Di chuyển lên (Alt + Lên)");
            miMoveUp.Click += BtnMoveUp_Click;

            miMoveDown = new ToolStripMenuItem("▼ Di chuyển xuống (Alt + Xuống)");
            miMoveDown.Click += BtnMoveDown_Click;

            miSetGroup = new ToolStripMenuItem("✏ Gom nhóm / Đặt tiêu đề nhóm...");
            miSetGroup.Click += (s, e) => OpenGroupTitleDialog();

            miBatchEditNumber = new ToolStripMenuItem("✏ Sửa / Thay thế số hiệu hàng loạt (Tiền tố)...");
            miBatchEditNumber.Click += (s, e) => OpenBatchEditSheetNumberDialog();

            cmsGrid.Items.Add(miCheckSelected);
            cmsGrid.Items.Add(miUncheckSelected);
            cmsGrid.Items.Add(miInvertSelected);
            cmsGrid.Items.Add(new ToolStripSeparator());
            cmsGrid.Items.Add(miCopyGroup);
            cmsGrid.Items.Add(miPasteGroup);
            cmsGrid.Items.Add(miFillDown);
            cmsGrid.Items.Add(miFillEmpty);
            cmsGrid.Items.Add(miQuickAssignSubmenu);
            cmsGrid.Items.Add(new ToolStripSeparator());
            cmsGrid.Items.Add(miMoveUp);
            cmsGrid.Items.Add(miMoveDown);
            cmsGrid.Items.Add(new ToolStripSeparator());
            cmsGrid.Items.Add(miSetGroup);
            cmsGrid.Items.Add(miBatchEditNumber);

            cmsGrid.Opening += CmsGrid_Opening;
            dgvSheets.ContextMenuStrip = cmsGrid;

            // 4. Bottom Panel
            pnlBottom = new System.Windows.Forms.Panel();
            pnlBottom.Dock = DockStyle.Bottom;
            pnlBottom.Height = 90;
            pnlBottom.BackColor = Color.FromArgb(240, 243, 246);

            lblStatus = new System.Windows.Forms.Label();
            lblStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            lblStatus.ForeColor = Color.FromArgb(41, 128, 185);
            lblStatus.Location = new Point(16, 8);
            lblStatus.Size = new Size(680, 20);

            chkOpenAfter = new System.Windows.Forms.CheckBox();
            chkOpenAfter.Text = "Tự động mở file/thư mục sau khi xuất";
            chkOpenAfter.Checked = true;
            chkOpenAfter.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            chkOpenAfter.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            chkOpenAfter.Location = new Point(950, 8);
            chkOpenAfter.Size = new Size(270, 20);

            // Nút Lưu vào Revit
            btnSaveToRevit = new System.Windows.Forms.Button();
            btnSaveToRevit.Text = "✔  LƯU VÀO REVIT";
            btnSaveToRevit.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnSaveToRevit.BackColor = Color.FromArgb(160, 170, 180);
            btnSaveToRevit.ForeColor = Color.White;
            btnSaveToRevit.FlatStyle = FlatStyle.Flat;
            btnSaveToRevit.FlatAppearance.BorderSize = 0;
            btnSaveToRevit.Location = new Point(14, 32);
            btnSaveToRevit.Size = new Size(160, 44);
            btnSaveToRevit.Enabled = false;
            btnSaveToRevit.Cursor = Cursors.Hand;
            btnSaveToRevit.Click += BtnSaveToRevit_Click;
            toolTip.SetToolTip(btnSaveToRevit, "Lưu ngay tất cả các bản vẽ bạn đã sửa Số Hiệu hoặc Tên trực tiếp vào Revit");

            // Nút Xuất Excel
            btnExport = new System.Windows.Forms.Button();
            btnExport.Text = "⚡  XUẤT EXCEL";
            btnExport.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnExport.BackColor = Color.FromArgb(39, 174, 96);
            btnExport.ForeColor = Color.White;
            btnExport.FlatStyle = FlatStyle.Flat;
            btnExport.FlatAppearance.BorderSize = 0;
            btnExport.Location = new Point(179, 32);
            btnExport.Size = new Size(115, 44);
            btnExport.Cursor = Cursors.Hand;
            btnExport.Click += BtnExport_Click;
            toolTip.SetToolTip(btnExport, "Xuất danh mục các bản vẽ đang được tích chọn ra file Excel XML hoặc CSV đẹp mắt");

            // Nút Xuất PDF
            btnExportPdf = new System.Windows.Forms.Button();
            btnExportPdf.Text = "📄  XUẤT RA PDF";
            btnExportPdf.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnExportPdf.BackColor = Color.FromArgb(192, 57, 43); // Red Accent
            btnExportPdf.ForeColor = Color.White;
            btnExportPdf.FlatStyle = FlatStyle.Flat;
            btnExportPdf.FlatAppearance.BorderSize = 0;
            btnExportPdf.Location = new Point(299, 32);
            btnExportPdf.Size = new Size(120, 44);
            btnExportPdf.Cursor = Cursors.Hand;
            btnExportPdf.Click += BtnExportPdf_Click;
            toolTip.SetToolTip(btnExportPdf, "Xuất các bản vẽ đã chọn ra file PDF gộp hoặc PDF từng trang theo đúng thứ tự tùy biến");

            // Nút Xuất CAD (Tùy chọn)
            btnExportCad = new System.Windows.Forms.Button();
            btnExportCad.Text = "📐  XUẤT CAD";
            btnExportCad.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnExportCad.BackColor = Color.FromArgb(24, 43, 73); // Navy Accent
            btnExportCad.ForeColor = Color.White;
            btnExportCad.FlatStyle = FlatStyle.Flat;
            btnExportCad.FlatAppearance.BorderSize = 0;
            btnExportCad.Location = new Point(424, 32);
            btnExportCad.Size = new Size(125, 44);
            btnExportCad.Cursor = Cursors.Hand;
            btnExportCad.Click += BtnExportCad_Click;
            toolTip.SetToolTip(btnExportCad, "Mở bảng tùy chọn nâng cao để xuất các bản vẽ đã chọn sang file bản vẽ AutoCAD (.dwg)");

            // Nút Xuất Gộp 1 File CAD
            btnExportCombinedCad = new System.Windows.Forms.Button();
            btnExportCombinedCad.Text = "📦  GỘP 1 FILE CAD";
            btnExportCombinedCad.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnExportCombinedCad.BackColor = Color.FromArgb(31, 58, 96); // Deep Indigo/Navy
            btnExportCombinedCad.ForeColor = Color.White;
            btnExportCombinedCad.FlatStyle = FlatStyle.Flat;
            btnExportCombinedCad.FlatAppearance.BorderSize = 0;
            btnExportCombinedCad.Location = new Point(554, 32);
            btnExportCombinedCad.Size = new Size(150, 44);
            btnExportCombinedCad.Cursor = Cursors.Hand;
            btnExportCombinedCad.Click += BtnExportCombinedCad_Click;
            toolTip.SetToolTip(btnExportCombinedCad, "Xuất tất cả các bản vẽ được chọn ra CÙNG 1 FILE CAD (.dwg) DUY NHẤT có đầy đủ Khung Tên (Title Block) như trong Revit");

            // Nút Sao Chép
            btnClipboard = new System.Windows.Forms.Button();
            btnClipboard.Text = "SAO CHÉP";
            btnClipboard.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            btnClipboard.BackColor = Color.FromArgb(41, 128, 185);
            btnClipboard.ForeColor = Color.White;
            btnClipboard.FlatStyle = FlatStyle.Flat;
            btnClipboard.FlatAppearance.BorderSize = 0;
            btnClipboard.Location = new Point(709, 32);
            btnClipboard.Size = new Size(85, 44);
            btnClipboard.Cursor = Cursors.Hand;
            btnClipboard.Click += BtnClipboard_Click;
            toolTip.SetToolTip(btnClipboard, "Sao chép dữ liệu để bạn dán (Ctrl+V) trực tiếp vào Excel, Word hoặc Schedule");

            // Nút Nhập từ Excel
            btnUpdateExcel = new System.Windows.Forms.Button();
            btnUpdateExcel.Text = "NHẬP EXCEL";
            btnUpdateExcel.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            btnUpdateExcel.BackColor = Color.FromArgb(211, 84, 0); // Orange
            btnUpdateExcel.ForeColor = Color.White;
            btnUpdateExcel.FlatStyle = FlatStyle.Flat;
            btnUpdateExcel.FlatAppearance.BorderSize = 0;
            btnUpdateExcel.Location = new Point(799, 32);
            btnUpdateExcel.Size = new Size(105, 44);
            btnUpdateExcel.Cursor = Cursors.Hand;
            btnUpdateExcel.Click += BtnUpdateExcel_Click;
            toolTip.SetToolTip(btnUpdateExcel, "Đọc file Excel/CSV đã sửa tên và cập nhật trực tiếp vào bản vẽ trong Revit");

            // Nút Dán Clipboard
            btnUpdateClipboard = new System.Windows.Forms.Button();
            btnUpdateClipboard.Text = "DÁN CLIPBOARD";
            btnUpdateClipboard.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            btnUpdateClipboard.BackColor = Color.FromArgb(22, 160, 133); // Teal
            btnUpdateClipboard.ForeColor = Color.White;
            btnUpdateClipboard.FlatStyle = FlatStyle.Flat;
            btnUpdateClipboard.FlatAppearance.BorderSize = 0;
            btnUpdateClipboard.Location = new Point(909, 32);
            btnUpdateClipboard.Size = new Size(120, 44);
            btnUpdateClipboard.Cursor = Cursors.Hand;
            btnUpdateClipboard.Click += BtnUpdateClipboard_Click;
            toolTip.SetToolTip(btnUpdateClipboard, "Cập nhật tên bản vẽ từ dữ liệu bảng bạn vừa Copy trong Excel (Ctrl+C)");

            // Nút Đóng
            btnClose = new System.Windows.Forms.Button();
            btnClose.Text = "Đóng";
            btnClose.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
            btnClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClose.Location = new Point(1135, 32);
            btnClose.Size = new Size(85, 44);
            btnClose.Cursor = Cursors.Hand;
            btnClose.Click += (s, e) => this.Close();
            this.CancelButton = btnClose;

            pnlBottom.Controls.Add(lblStatus);
            pnlBottom.Controls.Add(chkOpenAfter);
            pnlBottom.Controls.Add(btnSaveToRevit);
            pnlBottom.Controls.Add(btnExport);
            pnlBottom.Controls.Add(btnExportPdf);
            pnlBottom.Controls.Add(btnExportCad);
            pnlBottom.Controls.Add(btnExportCombinedCad);
            pnlBottom.Controls.Add(btnClipboard);
            pnlBottom.Controls.Add(btnUpdateExcel);
            pnlBottom.Controls.Add(btnUpdateClipboard);
            pnlBottom.Controls.Add(btnClose);

            // Gắn vào SplitContainer và Form theo thứ tự Dock
            splitMain.Panel1.Controls.Add(pnlTreeContainer);
            splitMain.Panel2.Controls.Add(dgvSheets);
            splitMain.Panel2.Controls.Add(pnlFolderBreadcrumb);

            this.Controls.Add(splitMain);
            this.Controls.Add(grpFilter);
            this.Controls.Add(pnlHeader);
            this.Controls.Add(pnlBottom);

            this.ResumeLayout(false);
        }

        private void ApplyFilter()
        {
            string keyword = txtSearch.Text.Trim().ToLower();

            _filteredSheets = _allSheets.Where(delegate(SheetItem s)
            {
                // Lọc theo thư mục nhóm trên Cây Thư Mục nếu có
                if (!string.IsNullOrEmpty(_selectedTreeGroup))
                {
                    if (_selectedTreeGroup == "[Chưa phân nhóm]")
                    {
                        if (!string.IsNullOrEmpty(s.GroupTitle)) return false;
                    }
                    else
                    {
                        if (!string.Equals(s.GroupTitle, _selectedTreeGroup, StringComparison.OrdinalIgnoreCase)) return false;
                    }
                }

                // Lọc theo từ khóa tìm kiếm
                if (!string.IsNullOrEmpty(keyword))
                {
                    bool matchNum = (s.SheetNumber != null && s.SheetNumber.ToLower().Contains(keyword));
                    bool matchName = (s.SheetName != null && s.SheetName.ToLower().Contains(keyword));
                    bool matchGroup = (s.GroupTitle != null && s.GroupTitle.ToLower().Contains(keyword));
                    return matchNum || matchName || matchGroup;
                }

                return true;
            }).ToList();

            RefreshGridData();
        }

        private void RefreshGridData()
        {
            _isUpdatingCheck = true;
            _lastCheckedRowIndex = -1;
            int scrollIdx = -1;
            try
            {
                if (dgvSheets.Rows.Count > 0)
                {
                    scrollIdx = dgvSheets.FirstDisplayedScrollingRowIndex;
                }

                dgvSheets.Rows.Clear();

                int stt = 1;
                foreach (SheetItem item in _filteredSheets)
                {
                    int rowIndex = dgvSheets.Rows.Add(
                        item.IsSelected,
                        stt++,
                        item.SheetNumber,
                        item.SheetName,
                        string.IsNullOrEmpty(item.GroupTitle) ? "[Chưa phân nhóm]" : item.GroupTitle,
                        item.Revision,
                        item.IssueDate,
                        item.DrawnBy
                    );
                    DataGridViewRow row = dgvSheets.Rows[rowIndex];
                    row.Tag = item;

                    if (string.IsNullOrEmpty(item.GroupTitle))
                    {
                        row.Cells[colGroup.Index].Style.ForeColor = Color.FromArgb(160, 160, 160);
                        row.Cells[colGroup.Index].Style.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
                    }
                    else
                    {
                        row.Cells[colGroup.Index].Style.ForeColor = Color.FromArgb(39, 174, 96);
                        row.Cells[colGroup.Index].Style.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                    }

                    // Nếu số hiệu bị sửa đổi:
                    if (item.IsNumberModified)
                    {
                        row.Cells[colNumber.Index].Style.BackColor = Color.FromArgb(234, 250, 234);
                        row.Cells[colNumber.Index].Style.ForeColor = Color.FromArgb(24, 106, 59);
                        row.Cells[colNumber.Index].Style.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                    }

                    // Nếu tên bị sửa đổi:
                    if (item.IsNameModified)
                    {
                        row.Cells[colName.Index].Style.BackColor = Color.FromArgb(234, 250, 234);
                        row.Cells[colName.Index].Style.ForeColor = Color.FromArgb(24, 106, 59);
                        row.Cells[colName.Index].Style.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                    }
                }

                if (scrollIdx >= 0 && scrollIdx < dgvSheets.Rows.Count)
                {
                    dgvSheets.FirstDisplayedScrollingRowIndex = scrollIdx;
                }
            }
            finally
            {
                _isUpdatingCheck = false;
            }

            UpdateStatusCount();
        }

        private void DgvSheets_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (dgvSheets.IsCurrentCellDirty && dgvSheets.CurrentCellAddress.X == colCheck.Index)
            {
                if ((System.Windows.Forms.Control.ModifierKeys & Keys.Shift) == Keys.Shift)
                {
                    dgvSheets.CancelEdit();
                    return;
                }
                dgvSheets.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        private void DgvSheets_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (_isUpdatingCheck) return;
            if (e.RowIndex >= 0 && e.ColumnIndex == colCheck.Index)
            {
                DataGridViewRow row = dgvSheets.Rows[e.RowIndex];
                bool isChecked = Convert.ToBoolean(row.Cells[colCheck.Index].Value);

                SheetItem singleItem = row.Tag as SheetItem;
                if (singleItem != null)
                {
                    singleItem.IsSelected = isChecked;
                    UpdateStatusCount();
                }

                // Ghi nhận mốc dòng vừa click để hỗ trợ giữ Shift chọn dải
                _lastCheckedRowIndex = e.RowIndex;
                _lastTargetCheckState = isChecked;
            }
        }

        private void DgvSheets_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0) return;

            // Xử lý chuột phải: giữ hoặc chọn dòng để mở Context Menu
            if (e.Button == MouseButtons.Right)
            {
                if (!dgvSheets.Rows[e.RowIndex].Selected)
                {
                    dgvSheets.ClearSelection();
                    dgvSheets.Rows[e.RowIndex].Selected = true;
                    if (e.ColumnIndex >= 0)
                    {
                        try
                        {
                            dgvSheets.CurrentCell = dgvSheets.Rows[e.RowIndex].Cells[e.ColumnIndex];
                        }
                        catch { }
                    }
                }
                return;
            }

            // Xử lý chuột trái
            if (e.Button == MouseButtons.Left)
            {
                // Nếu người dùng click vào cột Checkbox hoặc cột STT
                if (e.ColumnIndex == colCheck.Index || e.ColumnIndex == colStt.Index)
                {
                    if ((System.Windows.Forms.Control.ModifierKeys & Keys.Shift) == Keys.Shift)
                    {
                        // Giữ phím Shift để chọn hoặc bỏ chọn dải nhiều dòng liên tiếp
                        HandleShiftClickCheck(e.RowIndex);
                    }
                    else if (e.ColumnIndex == colStt.Index)
                    {
                        // Nếu bấm vào STT không giữ Shift, lưu làm mốc Anchor
                        _lastCheckedRowIndex = e.RowIndex;
                        _lastTargetCheckState = Convert.ToBoolean(dgvSheets.Rows[e.RowIndex].Cells[colCheck.Index].Value);
                    }
                }
            }
        }

        /// <summary>
        /// Xử lý giữ phím Shift để tích chọn hoặc bỏ chọn hàng loạt theo mong muốn của người dùng
        /// </summary>
        private void HandleShiftClickCheck(int targetRowIndex)
        {
            if (targetRowIndex < 0 || targetRowIndex >= dgvSheets.Rows.Count) return;

            int fromIndex = _lastCheckedRowIndex;
            if (fromIndex < 0 || fromIndex >= dgvSheets.Rows.Count)
            {
                fromIndex = (dgvSheets.CurrentRow != null && dgvSheets.CurrentRow.Index >= 0)
                    ? dgvSheets.CurrentRow.Index
                    : 0;
            }

            int start = Math.Min(fromIndex, targetRowIndex);
            int end = Math.Max(fromIndex, targetRowIndex);

            bool targetState;
            if (_lastCheckedRowIndex >= 0 && _lastCheckedRowIndex < dgvSheets.Rows.Count)
            {
                targetState = _lastTargetCheckState;

                // Nếu tất cả các dòng trong dải đã có cùng trạng thái targetState rồi, người dùng bấm tiếp để ĐẢO (toggle)
                bool allAlreadySame = true;
                for (int i = start; i <= end; i++)
                {
                    if (Convert.ToBoolean(dgvSheets.Rows[i].Cells[colCheck.Index].Value) != targetState)
                    {
                        allAlreadySame = false;
                        break;
                    }
                }
                if (allAlreadySame)
                {
                    targetState = !targetState;
                    _lastTargetCheckState = targetState;
                }
            }
            else
            {
                // Nếu chưa có mốc trước đó, đảo trạng thái của dòng vừa click
                bool curVal = Convert.ToBoolean(dgvSheets.Rows[targetRowIndex].Cells[colCheck.Index].Value);
                targetState = !curVal;
                _lastTargetCheckState = targetState;
            }

            SetRangeChecked(start, end, targetState);

            // Giữ fromIndex làm mốc Anchor để nếu người dùng tiếp tục bấm Shift-click mở rộng dải
            _lastCheckedRowIndex = fromIndex;

            this.BeginInvoke((MethodInvoker)delegate
            {
                SetRangeChecked(start, end, targetState);
                _lastCheckedRowIndex = fromIndex;
            });
        }

        /// <summary>
        /// Thiết lập trạng thái tích chọn cho dải dòng từ startRow đến endRow
        /// </summary>
        private void SetRangeChecked(int startRow, int endRow, bool checkState)
        {
            if (startRow < 0) startRow = 0;
            if (endRow >= dgvSheets.Rows.Count) endRow = dgvSheets.Rows.Count - 1;
            if (startRow > endRow)
            {
                int temp = startRow;
                startRow = endRow;
                endRow = temp;
            }

            _isUpdatingCheck = true;
            try
            {
                for (int i = startRow; i <= endRow; i++)
                {
                    DataGridViewRow row = dgvSheets.Rows[i];
                    row.Cells[colCheck.Index].Value = checkState;
                    SheetItem item = row.Tag as SheetItem;
                    if (item != null)
                    {
                        item.IsSelected = checkState;
                    }
                }
            }
            finally
            {
                _isUpdatingCheck = false;
            }

            UpdateStatusCount();
            dgvSheets.InvalidateColumn(colCheck.Index);
        }

        /// <summary>
        /// Bấm phím Cách (Spacebar) để bật/tắt tích chọn các dòng đang bôi đen;
        /// Ctrl+C, Ctrl+V, Ctrl+D để sao chép, dán, điền nhóm tiện lợi
        /// </summary>
        private void DgvSheets_KeyDown(object sender, KeyEventArgs e)
        {
            if (dgvSheets.IsCurrentCellInEditMode) return;

            // Space: Toggle Checkbox
            if (e.KeyCode == Keys.Space)
            {
                if (dgvSheets.SelectedRows.Count > 0)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;

                    bool anyUnchecked = false;
                    foreach (DataGridViewRow row in dgvSheets.SelectedRows)
                    {
                        if (!Convert.ToBoolean(row.Cells[colCheck.Index].Value))
                        {
                            anyUnchecked = true;
                            break;
                        }
                    }

                    bool newState = anyUnchecked;
                    SetSelectedRowsCheckState(newState);
                }
                return;
            }

            // Ctrl + C: Sao chép tên nhóm
            if (e.Control && e.KeyCode == Keys.C)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                CopyCurrentSelection();
                return;
            }

            // Ctrl + V: Dán tên nhóm vào các dòng đang chọn
            if (e.Control && e.KeyCode == Keys.V)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                PasteToSelectedRows();
                return;
            }

            // Ctrl + D: Điền tên nhóm xuống các dòng dưới (Fill Down)
            if (e.Control && e.KeyCode == Keys.D)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                FillDownGroupTitle();
                return;
            }

            // Delete: Xóa tên nhóm nếu đang ở cột nhóm
            if (e.KeyCode == Keys.Delete)
            {
                if (dgvSheets.CurrentCell != null && dgvSheets.CurrentCell.ColumnIndex == colGroup.Index)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    PasteToSelectedRows(string.Empty);
                    return;
                }
            }
        }

        /// <summary>
        /// Click vào tiêu đề cột "Chọn" để Chọn tất cả hoặc Bỏ chọn tất cả
        /// </summary>
        private void DgvSheets_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.ColumnIndex == colCheck.Index)
            {
                bool allSelected = _filteredSheets.All(delegate(SheetItem x) { return x.IsSelected; });
                SetSelectionState(!allSelected);
            }
        }

        private void CmsGrid_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            int selCount = dgvSheets.SelectedRows.Count;
            if (selCount <= 0 && dgvSheets.CurrentRow != null)
            {
                selCount = 1;
            }

            if (selCount > 1)
            {
                miCheckSelected.Text = string.Format("✔ Tích chọn {0} dòng đang bôi đen (Phím Cách)", selCount);
                miUncheckSelected.Text = string.Format("✖ Bỏ chọn {0} dòng đang bôi đen", selCount);
                miInvertSelected.Text = string.Format("◫ Đảo chọn {0} dòng đang bôi đen", selCount);
            }
            else
            {
                miCheckSelected.Text = "✔ Tích chọn dòng này (Phím Cách)";
                miUncheckSelected.Text = "✖ Bỏ chọn dòng này";
                miInvertSelected.Text = "◫ Đảo chọn dòng này";
            }

            // Cập nhật các menu Sao chép / Dán / Điền nhóm
            SheetItem curItem = (dgvSheets.CurrentRow != null) ? dgvSheets.CurrentRow.Tag as SheetItem : null;
            string curGroup = (curItem != null) ? curItem.GroupTitle : null;

            if (!string.IsNullOrEmpty(curGroup) && curGroup != "[Chưa phân nhóm]")
            {
                miCopyGroup.Text = string.Format("📋 Sao chép tên nhóm \"{0}\" (Ctrl+C)", curGroup);
                miCopyGroup.Enabled = true;
                miFillEmpty.Text = string.Format("⚡ Điền \"{0}\" vào tất cả ô chưa có nhóm", curGroup);
                miFillEmpty.Enabled = true;
            }
            else
            {
                miCopyGroup.Text = "📋 Sao chép tên nhóm (Ctrl+C)";
                miCopyGroup.Enabled = false;
                miFillEmpty.Text = "⚡ Điền tên nhóm này vào tất cả ô chưa có nhóm";
                miFillEmpty.Enabled = false;
            }

            string clip = string.Empty;
            try { if (Clipboard.ContainsText()) clip = ExtractCleanGroupTitle(Clipboard.GetText()); } catch { }
            if (string.IsNullOrEmpty(clip)) clip = _lastCopiedGroupTitle;

            if (!string.IsNullOrEmpty(clip))
            {
                miPasteGroup.Text = string.Format("📥 Dán tên nhóm \"{0}\" vào {1} dòng đang chọn (Ctrl+V)", clip, selCount);
                miPasteGroup.Enabled = true;
            }
            else
            {
                miPasteGroup.Text = "📥 Dán tên nhóm vào các dòng đang chọn (Ctrl+V)";
                miPasteGroup.Enabled = false;
            }

            if (selCount >= 2)
            {
                miFillDown.Text = string.Format("⚡ Điền tên nhóm từ dòng trên xuống {0} dòng dưới (Ctrl+D)", selCount - 1);
                miFillDown.Enabled = true;
            }
            else
            {
                miFillDown.Text = "⚡ Điền tên nhóm xuống các dòng dưới (Ctrl+D)";
                miFillDown.Enabled = false;
            }

            // Menu con: Gán nhanh tên nhóm có sẵn
            miQuickAssignSubmenu.DropDownItems.Clear();
            List<string> existingGroups = GetExistingGroupTitles();
            if (existingGroups.Count > 0)
            {
                foreach (string g in existingGroups)
                {
                    string groupName = g;
                    ToolStripMenuItem item = new ToolStripMenuItem(string.Format("📁 {0}", groupName));
                    item.Click += (s, ev) => PasteToSelectedRows(groupName);
                    miQuickAssignSubmenu.DropDownItems.Add(item);
                }
                miQuickAssignSubmenu.DropDownItems.Add(new ToolStripSeparator());
            }
            ToolStripMenuItem miClearGroup = new ToolStripMenuItem("❌ Xóa nhóm (Chuyển về Chưa phân nhóm)");
            miClearGroup.Click += (s, ev) => PasteToSelectedRows(string.Empty);
            miQuickAssignSubmenu.DropDownItems.Add(miClearGroup);
        }

        private void SetSelectedRowsCheckState(bool checkState)
        {
            if (dgvSheets.SelectedRows.Count == 0) return;

            _isUpdatingCheck = true;
            try
            {
                foreach (DataGridViewRow row in dgvSheets.SelectedRows)
                {
                    row.Cells[colCheck.Index].Value = checkState;
                    SheetItem item = row.Tag as SheetItem;
                    if (item != null)
                    {
                        item.IsSelected = checkState;
                    }
                }
            }
            finally
            {
                _isUpdatingCheck = false;
            }

            UpdateStatusCount();
            dgvSheets.InvalidateColumn(colCheck.Index);
        }

        private void InvertSelectedRowsCheckState()
        {
            if (dgvSheets.SelectedRows.Count == 0) return;

            _isUpdatingCheck = true;
            try
            {
                foreach (DataGridViewRow row in dgvSheets.SelectedRows)
                {
                    bool cur = Convert.ToBoolean(row.Cells[colCheck.Index].Value);
                    bool next = !cur;
                    row.Cells[colCheck.Index].Value = next;
                    SheetItem item = row.Tag as SheetItem;
                    if (item != null)
                    {
                        item.IsSelected = next;
                    }
                }
            }
            finally
            {
                _isUpdatingCheck = false;
            }

            UpdateStatusCount();
            dgvSheets.InvalidateColumn(colCheck.Index);
        }

        private void OpenGroupTitleDialog()
        {
            List<SheetItem> selectedItems = new List<SheetItem>();
            foreach (DataGridViewRow row in dgvSheets.SelectedRows)
            {
                SheetItem item = row.Tag as SheetItem;
                if (item != null) selectedItems.Add(item);
            }

            if (selectedItems.Count == 0)
            {
                selectedItems = _filteredSheets.Where(delegate(SheetItem x) { return x.IsSelected; }).ToList();
            }

            if (selectedItems.Count == 0)
            {
                MessageBox.Show("Vui lòng bôi đen hoặc tích chọn ít nhất 1 bản vẽ để gom nhóm!", "HDTOOL", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<string> existing = _allSheets
                .Select(delegate(SheetItem x) { return x.GroupTitle; })
                .Where(delegate(string x) { return !string.IsNullOrEmpty(x); })
                .Distinct()
                .ToList();

            string cur = selectedItems.Count > 0 ? selectedItems[0].GroupTitle : string.Empty;
            SetGroupTitleForm form = new SetGroupTitleForm(selectedItems.Count, existing, cur);
            form.GroupTitleConfirmed += delegate(string newGroup)
            {
                foreach (SheetItem item in selectedItems)
                {
                    item.GroupTitle = newGroup;
                }
                _selectedTreeGroup = string.IsNullOrEmpty(newGroup) ? null : newGroup;
                RebuildTreeNodes();
                RefreshGridData();
                SelectGroupNodeInTree(newGroup);
            };
            form.Show(this);
        }

        private void OpenBatchEditSheetNumberDialog()
        {
            List<SheetItem> targetSheets = new List<SheetItem>();
            foreach (DataGridViewRow row in dgvSheets.SelectedRows)
            {
                SheetItem item = row.Tag as SheetItem;
                if (item != null && !targetSheets.Contains(item)) targetSheets.Add(item);
            }

            if (targetSheets.Count == 0 && dgvSheets.SelectedCells.Count > 0)
            {
                foreach (DataGridViewCell c in dgvSheets.SelectedCells)
                {
                    SheetItem item = dgvSheets.Rows[c.RowIndex].Tag as SheetItem;
                    if (item != null && !targetSheets.Contains(item)) targetSheets.Add(item);
                }
            }

            if (targetSheets.Count == 0)
            {
                targetSheets = _filteredSheets.Where(delegate(SheetItem x) { return x.IsSelected; }).ToList();
            }

            if (targetSheets.Count == 0)
            {
                targetSheets = new List<SheetItem>(_filteredSheets);
            }

            if (targetSheets.Count == 0)
            {
                MessageBox.Show("Không có bản vẽ nào trong danh sách để chỉnh sửa số hiệu!", "HDTOOL", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            BatchEditSheetNumberForm form = new BatchEditSheetNumberForm(_allSheets, targetSheets);
            form.ChangesApplied += delegate(Dictionary<SheetItem, string> modifiedDict)
            {
                int modifiedCount = 0;
                foreach (KeyValuePair<SheetItem, string> kvp in modifiedDict)
                {
                    SheetItem item = kvp.Key;
                    string newNum = kvp.Value;
                    if (!string.Equals(item.SheetNumber, newNum, StringComparison.Ordinal))
                    {
                        item.SheetNumber = newNum;
                        item.IsSelected = true;
                        modifiedCount++;
                    }
                }

                if (modifiedCount > 0)
                {
                    bool anyOutsideFiltered = false;
                    foreach (KeyValuePair<SheetItem, string> kvp in modifiedDict)
                    {
                        if (!_filteredSheets.Contains(kvp.Key))
                        {
                            anyOutsideFiltered = true;
                            break;
                        }
                    }

                    if (anyOutsideFiltered)
                    {
                        _selectedTreeGroup = null;
                        if (txtSearch != null) txtSearch.Text = string.Empty;
                        ApplyFilter();
                    }

                    RefreshGridData();
                    UpdateSaveButtonState();
                    UpdateStatusCount();
                    try
                    {
                        this.BeginInvoke((MethodInvoker)delegate
                        {
                            try { RebuildTreeNodes(); } catch { }
                        });
                    }
                    catch { }

                    lblStatus.Text = string.Format("✔ Đã sửa số hiệu cho {0} bản vẽ. Vui lòng bấm [LƯU VÀO REVIT] để hoàn tất!", modifiedCount);
                    MessageBox.Show(
                        string.Format(
                            "Đã thay đổi số hiệu thành công cho {0} bản vẽ!\n\n" +
                            "Các ô số hiệu vừa sửa đã được tô màu xanh lá nổi bật trên bảng.\n" +
                            "Vui lòng bấm nút màu đỏ [✔ LƯU {0} BẢN VẼ VÀO REVIT] ở góc dưới để cập nhật chính thức vào Revit.",
                            modifiedCount
                        ),
                        "HDTOOL - Sửa Số Hiệu Hàng Loạt",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                }
            };
            form.Show(this);
        }

        private void DgvSheets_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                if (e.ColumnIndex == colName.Index || e.ColumnIndex == colNumber.Index || e.ColumnIndex == colGroup.Index)
                {
                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        try
                        {
                            if (dgvSheets.IsHandleCreated && !dgvSheets.IsDisposed && dgvSheets.CurrentCell != null)
                            {
                                dgvSheets.BeginEdit(true);
                            }
                        }
                        catch { }
                    });
                }
            }
        }

        private void DgvSheets_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false;
            e.Cancel = false;
        }

        private void DgvSheets_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dgvSheets.Rows.Count) return;
            try
            {
                DataGridViewRow row = dgvSheets.Rows[e.RowIndex];

                SheetItem item = row.Tag as SheetItem;
                if (item == null) return;

                if (e.ColumnIndex == colNumber.Index)
                {
                    string newNumber = (row.Cells[colNumber.Index].Value ?? string.Empty).ToString().Trim();
                    item.SheetNumber = newNumber;

                    if (item.IsNumberModified)
                    {
                        row.Cells[colNumber.Index].Style.BackColor = Color.FromArgb(234, 250, 234);
                        row.Cells[colNumber.Index].Style.ForeColor = Color.FromArgb(24, 106, 59);
                        row.Cells[colNumber.Index].Style.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);

                        // Tự động tích chọn dòng
                        row.Cells[colCheck.Index].Value = true;
                        item.IsSelected = true;
                    }
                    else
                    {
                        row.Cells[colNumber.Index].Style.BackColor = Color.Empty;
                        row.Cells[colNumber.Index].Style.ForeColor = Color.FromArgb(41, 128, 185);
                        row.Cells[colNumber.Index].Style.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                    }
                }
                else if (e.ColumnIndex == colName.Index)
                {
                    string newName = (row.Cells[colName.Index].Value ?? string.Empty).ToString().Trim();
                    item.SheetName = newName;

                    if (item.IsNameModified)
                    {
                        row.Cells[colName.Index].Style.BackColor = Color.FromArgb(234, 250, 234);
                        row.Cells[colName.Index].Style.ForeColor = Color.FromArgb(24, 106, 59);
                        row.Cells[colName.Index].Style.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);

                        // Tự động tích chọn dòng này
                        row.Cells[colCheck.Index].Value = true;
                        item.IsSelected = true;
                    }
                    else
                    {
                        row.Cells[colName.Index].Style.BackColor = Color.Empty;
                        row.Cells[colName.Index].Style.ForeColor = Color.Empty;
                        row.Cells[colName.Index].Style.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
                    }
                }
                else if (e.ColumnIndex == colGroup.Index)
                {
                    string rawVal = (row.Cells[colGroup.Index].Value ?? string.Empty).ToString();
                    string newGroup = ExtractCleanGroupTitle(rawVal);
                    item.GroupTitle = newGroup;
                    UpdateRowGroupCell(row, newGroup);

                    // Rebuild tree asynchronously để tránh reentrancy trong DataGridView!
                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        try
                        {
                            RebuildTreeNodes();
                        }
                        catch { }
                    });
                }
                else if (e.ColumnIndex == colRev.Index)
                {
                    item.Revision = (row.Cells[colRev.Index].Value ?? string.Empty).ToString().Trim();
                }
                else if (e.ColumnIndex == colDate.Index)
                {
                    item.IssueDate = (row.Cells[colDate.Index].Value ?? string.Empty).ToString().Trim();
                }
                else if (e.ColumnIndex == colDrawn.Index)
                {
                    item.DrawnBy = (row.Cells[colDrawn.Index].Value ?? string.Empty).ToString().Trim();
                }

                UpdateStatusCount();
                UpdateSaveButtonState();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("DgvSheets_CellEndEdit error: " + ex.Message);
            }
        }

        #region SAO CHÉP, DÁN VÀ ĐIỀN NHANH TÊN NHÓM (COPY / PASTE / FILL GROUP)

        /// <summary>
        /// Chuẩn hóa chuỗi tên nhóm khi dán từ Clipboard hoặc nhập vào ô
        /// (Tự động bóc tách tên nhóm chuẩn nếu người dùng copy cả dòng DataGridView có chứa tab hoặc boolean)
        /// </summary>
        private string ExtractCleanGroupTitle(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;

            // Nếu người dùng copy cả dòng DataGridView chứa ký tự tab '\t'
            if (raw.Contains("\t"))
            {
                string[] parts = raw.Split(new char[] { '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                // Các cột chuẩn: [Chọn (True/False), STT, Số hiệu, Tên bản vẽ, Thư mục/Nhóm, ...]
                if (parts.Length >= 5)
                {
                    string grpPart = parts[4].Trim();
                    if (grpPart != "[Chưa phân nhóm]" && !string.IsNullOrEmpty(grpPart))
                    {
                        return grpPart;
                    }
                }

                for (int i = 0; i < parts.Length; i++)
                {
                    string p = parts[i].Trim();
                    if (p == "True" || p == "False" || p == "[Chưa phân nhóm]") continue;
                    int dummy;
                    if (int.TryParse(p, out dummy)) continue;
                    if (p.Length > 2) return p;
                }
            }

            string trimmed = raw.Trim(new char[] { '\r', '\n', '\t', ' ' });
            if (trimmed == "[Chưa phân nhóm]") return string.Empty;
            return trimmed;
        }

        /// <summary>
        /// Cập nhật hiển thị và định dạng màu sắc cho ô Thư Mục / Nhóm của dòng
        /// </summary>
        private void UpdateRowGroupCell(DataGridViewRow row, string groupTitle)
        {
            if (row == null || row.Cells.Count <= colGroup.Index) return;

            if (string.IsNullOrEmpty(groupTitle))
            {
                row.Cells[colGroup.Index].Value = "[Chưa phân nhóm]";
                row.Cells[colGroup.Index].Style.ForeColor = Color.FromArgb(160, 160, 160);
                row.Cells[colGroup.Index].Style.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
            }
            else
            {
                row.Cells[colGroup.Index].Value = groupTitle;
                row.Cells[colGroup.Index].Style.ForeColor = Color.FromArgb(39, 174, 96);
                row.Cells[colGroup.Index].Style.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            }
        }

        /// <summary>
        /// Lấy danh sách tất cả các tên nhóm đã có trong dự án
        /// </summary>
        private List<string> GetExistingGroupTitles()
        {
            List<string> list = new List<string>();
            if (_allSheets != null)
            {
                foreach (SheetItem s in _allSheets)
                {
                    string g = (s.GroupTitle ?? string.Empty).Trim();
                    if (!string.IsNullOrEmpty(g) && g != "[Chưa phân nhóm]" && !list.Contains(g))
                    {
                        list.Add(g);
                    }
                }
            }
            ExportSheetListService.NaturalStringComparer comp = new ExportSheetListService.NaturalStringComparer();
            list.Sort(comp);
            return list;
        }

        /// <summary>
        /// Sao chép tên nhóm từ ô hoặc dòng đang chọn vào Clipboard (Ctrl + C)
        /// </summary>
        private void CopyCurrentSelection()
        {
            try
            {
                if (dgvSheets.CurrentCell != null && dgvSheets.CurrentCell.ColumnIndex == colGroup.Index)
                {
                    DataGridViewRow curRow = dgvSheets.CurrentRow;
                    if (curRow != null)
                    {
                        SheetItem item = curRow.Tag as SheetItem;
                        string grp = item != null ? item.GroupTitle : null;
                        if (!string.IsNullOrEmpty(grp) && grp != "[Chưa phân nhóm]")
                        {
                            Clipboard.SetText(grp);
                            _lastCopiedGroupTitle = grp;
                            lblStatus.Text = string.Format("📋 Đã sao chép tên nhóm: \"{0}\" (Bấm Ctrl+V để dán vào các dòng khác)", grp);
                            return;
                        }
                    }
                }

                if (dgvSheets.CurrentCell != null)
                {
                    object val = dgvSheets.CurrentCell.Value;
                    if (val != null)
                    {
                        string sVal = val.ToString();
                        if (sVal == "[Chưa phân nhóm]") sVal = string.Empty;
                        if (!string.IsNullOrEmpty(sVal))
                        {
                            Clipboard.SetText(sVal);
                            _lastCopiedGroupTitle = sVal;
                            lblStatus.Text = string.Format("📋 Đã sao chép: \"{0}\"", sVal);
                            return;
                        }
                    }
                }

                if (dgvSheets.CurrentRow != null)
                {
                    SheetItem item = dgvSheets.CurrentRow.Tag as SheetItem;
                    if (item != null && !string.IsNullOrEmpty(item.GroupTitle))
                    {
                        Clipboard.SetText(item.GroupTitle);
                        _lastCopiedGroupTitle = item.GroupTitle;
                        lblStatus.Text = string.Format("📋 Đã sao chép tên nhóm: \"{0}\"", item.GroupTitle);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("CopyCurrentSelection error: " + ex.Message);
            }
        }

        /// <summary>
        /// Dán tên nhóm vào tất cả các dòng bản vẽ đang chọn (Ctrl + V)
        /// </summary>
        private void PasteToSelectedRows(string customText = null)
        {
            try
            {
                string textToPaste = customText;
                if (textToPaste == null)
                {
                    try
                    {
                        if (Clipboard.ContainsText())
                        {
                            textToPaste = Clipboard.GetText();
                        }
                    }
                    catch { }
                }

                if (string.IsNullOrEmpty(textToPaste))
                {
                    textToPaste = _lastCopiedGroupTitle;
                }

                string cleanGroup = ExtractCleanGroupTitle(textToPaste);

                List<DataGridViewRow> targetRows = new List<DataGridViewRow>();
                foreach (DataGridViewRow r in dgvSheets.SelectedRows)
                {
                    if (!targetRows.Contains(r)) targetRows.Add(r);
                }
                if (targetRows.Count == 0 && dgvSheets.SelectedCells.Count > 0)
                {
                    foreach (DataGridViewCell c in dgvSheets.SelectedCells)
                    {
                        DataGridViewRow r = dgvSheets.Rows[c.RowIndex];
                        if (!targetRows.Contains(r)) targetRows.Add(r);
                    }
                }
                if (targetRows.Count == 0 && dgvSheets.CurrentRow != null)
                {
                    targetRows.Add(dgvSheets.CurrentRow);
                }

                if (targetRows.Count == 0) return;

                foreach (DataGridViewRow r in targetRows)
                {
                    SheetItem item = r.Tag as SheetItem;
                    if (item != null)
                    {
                        item.GroupTitle = cleanGroup;
                    }
                    UpdateRowGroupCell(r, cleanGroup);
                }

                _lastCopiedGroupTitle = cleanGroup;
                if (string.IsNullOrEmpty(cleanGroup))
                {
                    lblStatus.Text = string.Format("❌ Đã xóa nhóm cho {0} bản vẽ đang chọn.", targetRows.Count);
                }
                else
                {
                    lblStatus.Text = string.Format("📥 Đã dán tên nhóm \"{0}\" vào {1} bản vẽ đang chọn.", cleanGroup, targetRows.Count);
                }

                this.BeginInvoke((MethodInvoker)delegate
                {
                    try
                    {
                        RebuildTreeNodes();
                    }
                    catch { }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("PasteToSelectedRows error: " + ex.Message);
            }
        }

        /// <summary>
        /// Điền tên nhóm từ dòng trên xuống các dòng bên dưới trong dải chọn (Ctrl + D)
        /// </summary>
        private void FillDownGroupTitle()
        {
            try
            {
                List<DataGridViewRow> targetRows = new List<DataGridViewRow>();
                foreach (DataGridViewRow r in dgvSheets.SelectedRows)
                {
                    if (!targetRows.Contains(r)) targetRows.Add(r);
                }
                if (targetRows.Count == 0 && dgvSheets.SelectedCells.Count > 0)
                {
                    foreach (DataGridViewCell c in dgvSheets.SelectedCells)
                    {
                        DataGridViewRow r = dgvSheets.Rows[c.RowIndex];
                        if (!targetRows.Contains(r)) targetRows.Add(r);
                    }
                }

                if (targetRows.Count < 2)
                {
                    if (dgvSheets.CurrentRow != null)
                    {
                        int nextIdx = dgvSheets.CurrentRow.Index + 1;
                        if (nextIdx < dgvSheets.Rows.Count)
                        {
                            SheetItem curItem = dgvSheets.CurrentRow.Tag as SheetItem;
                            string grp = curItem != null ? curItem.GroupTitle : null;
                            if (!string.IsNullOrEmpty(grp))
                            {
                                DataGridViewRow nextRow = dgvSheets.Rows[nextIdx];
                                SheetItem nextItem = nextRow.Tag as SheetItem;
                                if (nextItem != null) nextItem.GroupTitle = grp;
                                UpdateRowGroupCell(nextRow, grp);
                                lblStatus.Text = string.Format("⚡ Đã điền tên nhóm \"{0}\" xuống dòng tiếp theo (Ctrl+D).", grp);
                                this.BeginInvoke((MethodInvoker)delegate { try { RebuildTreeNodes(); } catch { } });
                            }
                        }
                    }
                    return;
                }

                targetRows.Sort(delegate(DataGridViewRow a, DataGridViewRow b) { return a.Index.CompareTo(b.Index); });

                SheetItem topItem = targetRows[0].Tag as SheetItem;
                string sourceGroup = (topItem != null ? topItem.GroupTitle : null) ?? string.Empty;
                if (string.IsNullOrEmpty(sourceGroup))
                {
                    MessageBox.Show("Dòng đầu tiên được chọn chưa có tên nhóm để điền xuống!\nVui lòng nhập tên nhóm cho dòng đầu tiên trước.", "HDTOOL", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                for (int i = 1; i < targetRows.Count; i++)
                {
                    DataGridViewRow r = targetRows[i];
                    SheetItem item = r.Tag as SheetItem;
                    if (item != null)
                    {
                        item.GroupTitle = sourceGroup;
                    }
                    UpdateRowGroupCell(r, sourceGroup);
                }

                lblStatus.Text = string.Format("⚡ Đã điền tên nhóm \"{0}\" xuống {1} bản vẽ bên dưới (Ctrl+D).", sourceGroup, targetRows.Count - 1);
                this.BeginInvoke((MethodInvoker)delegate
                {
                    try
                    {
                        RebuildTreeNodes();
                    }
                    catch { }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("FillDownGroupTitle error: " + ex.Message);
            }
        }

        /// <summary>
        /// Điền tên nhóm vào tất cả các ô bản vẽ chưa phân nhóm (ô trống)
        /// </summary>
        private void FillEmptyGroupCells(string specificGroup = null)
        {
            try
            {
                string groupToFill = specificGroup;
                if (string.IsNullOrEmpty(groupToFill))
                {
                    if (dgvSheets.CurrentRow != null)
                    {
                        SheetItem cur = dgvSheets.CurrentRow.Tag as SheetItem;
                        if (cur != null) groupToFill = cur.GroupTitle;
                    }
                }
                if (string.IsNullOrEmpty(groupToFill))
                {
                    groupToFill = _lastCopiedGroupTitle;
                }
                if (string.IsNullOrEmpty(groupToFill))
                {
                    try { if (Clipboard.ContainsText()) groupToFill = ExtractCleanGroupTitle(Clipboard.GetText()); } catch { }
                }

                if (string.IsNullOrEmpty(groupToFill))
                {
                    MessageBox.Show("Vui lòng chọn hoặc nhập một tên nhóm trước khi điền vào các ô trống!", "HDTOOL", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                int filledCount = 0;
                foreach (DataGridViewRow r in dgvSheets.Rows)
                {
                    SheetItem item = r.Tag as SheetItem;
                    if (item != null && string.IsNullOrEmpty(item.GroupTitle))
                    {
                        item.GroupTitle = groupToFill;
                        UpdateRowGroupCell(r, groupToFill);
                        filledCount++;
                    }
                }

                if (filledCount == 0)
                {
                    MessageBox.Show("Không có bản vẽ nào đang để trống nhóm trên bảng!", "HDTOOL", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                lblStatus.Text = string.Format("⚡ Đã điền tên nhóm \"{0}\" vào {1} bản vẽ chưa có nhóm.", groupToFill, filledCount);
                this.BeginInvoke((MethodInvoker)delegate
                {
                    try
                    {
                        RebuildTreeNodes();
                    }
                    catch { }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("FillEmptyGroupCells error: " + ex.Message);
            }
        }

        /// <summary>
        /// Tự động gợi ý danh sách nhóm có sẵn (AutoComplete) khi gõ chữ vào cột Thư Mục / Nhóm
        /// </summary>
        private void DgvSheets_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            System.Windows.Forms.TextBox tb = e.Control as System.Windows.Forms.TextBox;
            if (tb != null)
            {
                tb.KeyDown -= EditingControl_KeyDown;
                tb.KeyDown += EditingControl_KeyDown;

                if (dgvSheets.CurrentCell != null && dgvSheets.CurrentCell.ColumnIndex == colGroup.Index)
                {
                    tb.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                    tb.AutoCompleteSource = AutoCompleteSource.CustomSource;
                    AutoCompleteStringCollection coll = new AutoCompleteStringCollection();
                    List<string> groups = GetExistingGroupTitles();
                    foreach (string g in groups)
                    {
                        coll.Add(g);
                    }
                    tb.AutoCompleteCustomSource = coll;
                }
                else
                {
                    tb.AutoCompleteMode = AutoCompleteMode.None;
                }
            }
        }

        private void EditingControl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.V)
            {
                if (dgvSheets.CurrentCell != null && dgvSheets.CurrentCell.ColumnIndex == colGroup.Index)
                {
                    try
                    {
                        if (Clipboard.ContainsText())
                        {
                            string clip = Clipboard.GetText();
                            string clean = ExtractCleanGroupTitle(clip);
                            if (!string.IsNullOrEmpty(clean))
                            {
                                System.Windows.Forms.TextBox tb = sender as System.Windows.Forms.TextBox;
                                if (tb != null)
                                {
                                    e.Handled = true;
                                    e.SuppressKeyPress = true;
                                    tb.SelectedText = clean;
                                    return;
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
        }

        #endregion

        #region CÂY THƯ MỤC HỒ SƠ & PHÂN NHÓM BẢN VẼ (TREE VIEW)

        private ImageList CreateTreeImageList()
        {
            ImageList imgList = new ImageList();
            imgList.ImageSize = new Size(16, 16);
            imgList.ColorDepth = ColorDepth.Depth32Bit;

            // 0: Root / Project (Navy Blue Folder)
            Bitmap bmpRoot = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(bmpRoot))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (SolidBrush bTab = new SolidBrush(Color.FromArgb(41, 128, 185)))
                {
                    g.FillRectangle(bTab, 1, 2, 6, 3);
                }
                using (SolidBrush bBody = new SolidBrush(Color.FromArgb(24, 43, 73)))
                {
                    g.FillRectangle(bBody, 1, 5, 14, 9);
                }
            }
            imgList.Images.Add("root", bmpRoot);

            // 1: Folder (Amber/Golden Folder)
            Bitmap bmpFolder = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(bmpFolder))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (SolidBrush bTab = new SolidBrush(Color.FromArgb(243, 156, 18)))
                {
                    g.FillRectangle(bTab, 1, 3, 6, 3);
                }
                using (SolidBrush bBody = new SolidBrush(Color.FromArgb(241, 196, 15)))
                {
                    g.FillRectangle(bBody, 1, 5, 14, 9);
                }
                using (Pen p = new Pen(Color.FromArgb(214, 137, 16)))
                {
                    g.DrawRectangle(p, 1, 5, 13, 8);
                }
            }
            imgList.Images.Add("folder", bmpFolder);

            // 2: Folder Open (Orange Open Folder)
            Bitmap bmpFolderOpen = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(bmpFolderOpen))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (SolidBrush bTab = new SolidBrush(Color.FromArgb(230, 126, 34)))
                {
                    g.FillRectangle(bTab, 1, 2, 6, 3);
                }
                using (SolidBrush bBody = new SolidBrush(Color.FromArgb(243, 156, 18)))
                {
                    Point[] poly = new Point[] { new Point(1, 6), new Point(14, 6), new Point(12, 14), new Point(0, 14) };
                    g.FillPolygon(bBody, poly);
                }
            }
            imgList.Images.Add("folder_open", bmpFolderOpen);

            // 3: Sheet / Document (Clean Document icon with blue lines)
            Bitmap bmpSheet = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(bmpSheet))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (SolidBrush bWhite = new SolidBrush(Color.White))
                {
                    g.FillRectangle(bWhite, 2, 1, 11, 14);
                }
                using (Pen p = new Pen(Color.FromArgb(52, 152, 219)))
                {
                    g.DrawRectangle(p, 2, 1, 11, 13);
                    g.DrawLine(p, 5, 5, 10, 5);
                    g.DrawLine(p, 5, 8, 10, 8);
                    g.DrawLine(p, 5, 11, 9, 11);
                }
            }
            imgList.Images.Add("sheet", bmpSheet);

            // 4: Unassigned Folder (Steel Gray)
            Bitmap bmpUnassigned = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(bmpUnassigned))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (SolidBrush bTab = new SolidBrush(Color.FromArgb(149, 165, 166)))
                {
                    g.FillRectangle(bTab, 1, 3, 6, 3);
                }
                using (SolidBrush bBody = new SolidBrush(Color.FromArgb(189, 195, 199)))
                {
                    g.FillRectangle(bBody, 1, 5, 14, 9);
                }
                using (Pen p = new Pen(Color.FromArgb(127, 140, 141)))
                {
                    g.DrawRectangle(p, 1, 5, 13, 8);
                }
            }
            imgList.Images.Add("unassigned", bmpUnassigned);

            return imgList;
        }

        private void RebuildTreeNodes()
        {
            if (tvGroups == null) return;

            _isRebuildingTree = true;
            _isUpdatingTreeCheck = true;
            tvGroups.BeginUpdate();
            try
            {
                string prevSelectedGroup = _selectedTreeGroup;

                tvGroups.Nodes.Clear();

                int totalCount = _allSheets.Count;
                bool allChecked = totalCount > 0 && _allSheets.All(delegate(SheetItem x) { return x.IsSelected; });
                _rootNode = new TreeNode(string.Format("TẤT CẢ BẢN VẼ ({0})", totalCount));
                _rootNode.Tag = null;
                _rootNode.ImageKey = "root";
                _rootNode.SelectedImageKey = "root";
                _rootNode.Checked = allChecked;
                _rootNode.NodeFont = new Font("Segoe UI", 9.0f, FontStyle.Bold);

                Dictionary<string, List<SheetItem>> groupDict = new Dictionary<string, List<SheetItem>>(StringComparer.OrdinalIgnoreCase);
                List<SheetItem> unassignedList = new List<SheetItem>();

                foreach (SheetItem s in _allSheets)
                {
                    string g = (s.GroupTitle ?? string.Empty).Trim();
                    if (string.IsNullOrEmpty(g))
                    {
                        unassignedList.Add(s);
                    }
                    else
                    {
                        if (!groupDict.ContainsKey(g))
                        {
                            groupDict[g] = new List<SheetItem>();
                        }
                        groupDict[g].Add(s);
                    }
                }

                TreeNode nodeToSelect = _rootNode;
                ExportSheetListService.NaturalStringComparer comp = new ExportSheetListService.NaturalStringComparer();

                // Tạo các nhóm có tên
                List<string> sortedGroupKeys = groupDict.Keys.OrderBy(delegate(string x) { return x; }, comp).ToList();
                foreach (string groupName in sortedGroupKeys)
                {
                    List<SheetItem> items = groupDict[groupName];
                    items.Sort(delegate(SheetItem a, SheetItem b)
                    {
                        return comp.Compare(a.SheetNumber ?? string.Empty, b.SheetNumber ?? string.Empty);
                    });

                    bool groupAllChecked = items.Count > 0 && items.All(delegate(SheetItem x) { return x.IsSelected; });

                    TreeNode folderNode = new TreeNode(string.Format("{0} ({1})", groupName, items.Count));
                    folderNode.Tag = groupName;
                    folderNode.ImageKey = "folder";
                    folderNode.SelectedImageKey = "folder_open";
                    folderNode.Checked = groupAllChecked;
                    folderNode.NodeFont = new Font("Segoe UI", 9.0f, FontStyle.Bold);

                    foreach (SheetItem sheet in items)
                    {
                        TreeNode sheetNode = new TreeNode(string.Format("[{0}] {1}", sheet.SheetNumber, sheet.SheetName));
                        sheetNode.Tag = sheet;
                        sheetNode.ImageKey = "sheet";
                        sheetNode.SelectedImageKey = "sheet";
                        sheetNode.Checked = sheet.IsSelected;
                        folderNode.Nodes.Add(sheetNode);
                    }

                    _rootNode.Nodes.Add(folderNode);

                    if (string.Equals(prevSelectedGroup, groupName, StringComparison.OrdinalIgnoreCase))
                    {
                        nodeToSelect = folderNode;
                    }
                }

                // Thư mục Chưa phân nhóm
                if (unassignedList.Count > 0)
                {
                    unassignedList.Sort(delegate(SheetItem a, SheetItem b)
                    {
                        return comp.Compare(a.SheetNumber ?? string.Empty, b.SheetNumber ?? string.Empty);
                    });

                    bool unassignedAllChecked = unassignedList.All(delegate(SheetItem x) { return x.IsSelected; });
                    TreeNode unassignedNode = new TreeNode(string.Format("[Chưa phân nhóm] ({0})", unassignedList.Count));
                    unassignedNode.Tag = "[Chưa phân nhóm]";
                    unassignedNode.ImageKey = "unassigned";
                    unassignedNode.SelectedImageKey = "unassigned";
                    unassignedNode.Checked = unassignedAllChecked;
                    unassignedNode.ForeColor = Color.FromArgb(120, 120, 120);

                    foreach (SheetItem sheet in unassignedList)
                    {
                        TreeNode sheetNode = new TreeNode(string.Format("[{0}] {1}", sheet.SheetNumber, sheet.SheetName));
                        sheetNode.Tag = sheet;
                        sheetNode.ImageKey = "sheet";
                        sheetNode.SelectedImageKey = "sheet";
                        sheetNode.Checked = sheet.IsSelected;
                        unassignedNode.Nodes.Add(sheetNode);
                    }

                    _rootNode.Nodes.Add(unassignedNode);

                    if (prevSelectedGroup == "[Chưa phân nhóm]")
                    {
                        nodeToSelect = unassignedNode;
                    }
                }

                tvGroups.Nodes.Add(_rootNode);

                _rootNode.Expand();
                foreach (TreeNode fn in _rootNode.Nodes)
                {
                    fn.Expand();
                }

                tvGroups.SelectedNode = nodeToSelect;
            }
            finally
            {
                tvGroups.EndUpdate();
                _isUpdatingTreeCheck = false;
                _isRebuildingTree = false;
            }
        }

        private void SelectGroupNodeInTree(string groupName)
        {
            if (tvGroups == null || _rootNode == null) return;

            if (string.IsNullOrEmpty(groupName))
            {
                tvGroups.SelectedNode = _rootNode;
                return;
            }

            foreach (TreeNode node in _rootNode.Nodes)
            {
                string tag = node.Tag as string;
                if (string.Equals(tag, groupName, StringComparison.OrdinalIgnoreCase))
                {
                    tvGroups.SelectedNode = node;
                    node.EnsureVisible();
                    return;
                }
            }
        }

        private void TvGroups_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (_isRebuildingTree || e.Node == null) return;

            if (e.Node == _rootNode)
            {
                _selectedTreeGroup = null;
                lblCurrentFolder.Text = string.Format("📁 Đang hiển thị: Tất Cả Bản Vẽ ({0} bản vẽ)", _allSheets.Count);
                ApplyFilter();
            }
            else if (e.Node.Tag is string)
            {
                string groupName = (string)e.Node.Tag;
                _selectedTreeGroup = groupName;
                int count = _allSheets.Count(delegate(SheetItem s)
                {
                    if (groupName == "[Chưa phân nhóm]") return string.IsNullOrEmpty(s.GroupTitle);
                    return string.Equals(s.GroupTitle, groupName, StringComparison.OrdinalIgnoreCase);
                });
                lblCurrentFolder.Text = string.Format("📁 Đang hiển thị: Thư mục \"{0}\" ({1} bản vẽ)", groupName, count);
                ApplyFilter();
            }
            else if (e.Node.Tag is SheetItem)
            {
                SheetItem target = (SheetItem)e.Node.Tag;
                if (!string.IsNullOrEmpty(_selectedTreeGroup))
                {
                    bool inCurGroup = false;
                    if (_selectedTreeGroup == "[Chưa phân nhóm]") inCurGroup = string.IsNullOrEmpty(target.GroupTitle);
                    else inCurGroup = string.Equals(target.GroupTitle, _selectedTreeGroup, StringComparison.OrdinalIgnoreCase);

                    if (!inCurGroup)
                    {
                        _selectedTreeGroup = null;
                        lblCurrentFolder.Text = string.Format("📁 Đang hiển thị: Tất Cả Bản Vẽ ({0} bản vẽ)", _allSheets.Count);
                        ApplyFilter();
                    }
                }
                HighlightRowBySheet(target);
            }
        }

        private void HighlightRowBySheet(SheetItem target)
        {
            if (target == null) return;
            for (int i = 0; i < dgvSheets.Rows.Count; i++)
            {
                if (dgvSheets.Rows[i].Tag == target)
                {
                    dgvSheets.ClearSelection();
                    dgvSheets.Rows[i].Selected = true;
                    try
                    {
                        if (i >= 0 && i < dgvSheets.Rows.Count)
                        {
                            dgvSheets.FirstDisplayedScrollingRowIndex = i;
                            this.BeginInvoke((MethodInvoker)delegate
                            {
                                try
                                {
                                    if (i >= 0 && i < dgvSheets.Rows.Count)
                                    {
                                        dgvSheets.CurrentCell = dgvSheets.Rows[i].Cells[colNumber.Index];
                                    }
                                }
                                catch { }
                            });
                        }
                    }
                    catch { }
                    break;
                }
            }
        }

        private void TvGroups_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (_isUpdatingTreeCheck || e.Action == TreeViewAction.Unknown) return;

            _isUpdatingTreeCheck = true;
            try
            {
                bool isChecked = e.Node.Checked;

                UpdateChildNodesCheckState(e.Node, isChecked);

                if (e.Node.Tag is SheetItem)
                {
                    SheetItem s = (SheetItem)e.Node.Tag;
                    s.IsSelected = isChecked;
                }
                else if (e.Node.Tag is string)
                {
                    string g = (string)e.Node.Tag;
                    foreach (SheetItem s in _allSheets)
                    {
                        if (g == "[Chưa phân nhóm]")
                        {
                            if (string.IsNullOrEmpty(s.GroupTitle)) s.IsSelected = isChecked;
                        }
                        else
                        {
                            if (string.Equals(s.GroupTitle, g, StringComparison.OrdinalIgnoreCase)) s.IsSelected = isChecked;
                        }
                    }
                }
                else if (e.Node == _rootNode)
                {
                    foreach (SheetItem s in _allSheets)
                    {
                        s.IsSelected = isChecked;
                    }
                }

                if (e.Node.Parent != null)
                {
                    UpdateParentNodeCheckState(e.Node.Parent);
                }

                SyncGridCheckboxesFromData();
                UpdateStatusCount();
            }
            finally
            {
                _isUpdatingTreeCheck = false;
            }
        }

        private void UpdateChildNodesCheckState(TreeNode node, bool isChecked)
        {
            foreach (TreeNode child in node.Nodes)
            {
                child.Checked = isChecked;
                if (child.Tag is SheetItem)
                {
                    ((SheetItem)child.Tag).IsSelected = isChecked;
                }
                else
                {
                    UpdateChildNodesCheckState(child, isChecked);
                }
            }
        }

        private void UpdateParentNodeCheckState(TreeNode parent)
        {
            if (parent == null) return;
            bool allChecked = true;
            foreach (TreeNode c in parent.Nodes)
            {
                if (!c.Checked)
                {
                    allChecked = false;
                    break;
                }
            }
            parent.Checked = (parent.Nodes.Count > 0 && allChecked);
            if (parent.Parent != null)
            {
                UpdateParentNodeCheckState(parent.Parent);
            }
        }

        private void SyncTreeCheckboxesFromData()
        {
            if (tvGroups == null || _rootNode == null || _isUpdatingTreeCheck) return;

            _isUpdatingTreeCheck = true;
            try
            {
                foreach (TreeNode folderNode in _rootNode.Nodes)
                {
                    bool allFolderChecked = true;
                    foreach (TreeNode sheetNode in folderNode.Nodes)
                    {
                        SheetItem s = sheetNode.Tag as SheetItem;
                        if (s != null)
                        {
                            sheetNode.Checked = s.IsSelected;
                            if (!s.IsSelected) allFolderChecked = false;
                        }
                    }
                    folderNode.Checked = (folderNode.Nodes.Count > 0 && allFolderChecked);
                }

                bool allRootChecked = _allSheets.Count > 0 && _allSheets.All(delegate(SheetItem x) { return x.IsSelected; });
                _rootNode.Checked = allRootChecked;
            }
            finally
            {
                _isUpdatingTreeCheck = false;
            }
        }

        private void SyncGridCheckboxesFromData()
        {
            _isUpdatingCheck = true;
            try
            {
                foreach (DataGridViewRow row in dgvSheets.Rows)
                {
                    SheetItem s = row.Tag as SheetItem;
                    if (s != null)
                    {
                        row.Cells[colCheck.Index].Value = s.IsSelected;
                    }
                }
            }
            finally
            {
                _isUpdatingCheck = false;
            }
            dgvSheets.InvalidateColumn(colCheck.Index);
        }

        private void TvGroups_ItemDrag(object sender, ItemDragEventArgs e)
        {
            TreeNode node = e.Item as TreeNode;
            if (node != null && node != _rootNode)
            {
                tvGroups.DoDragDrop(node, DragDropEffects.Move);
            }
        }

        private void TvGroups_DragOver(object sender, DragEventArgs e)
        {
            Point pt = tvGroups.PointToClient(new Point(e.X, e.Y));
            TreeNode target = tvGroups.GetNodeAt(pt);
            if (target != null)
            {
                e.Effect = DragDropEffects.Move;
                tvGroups.SelectedNode = target;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void TvGroups_DragDrop(object sender, DragEventArgs e)
        {
            Point pt = tvGroups.PointToClient(new Point(e.X, e.Y));
            TreeNode targetNode = tvGroups.GetNodeAt(pt);
            if (targetNode == null) return;

            string targetGroupName = string.Empty;
            if (targetNode == _rootNode)
            {
                targetGroupName = string.Empty;
            }
            else if (targetNode.Tag is string)
            {
                targetGroupName = (string)targetNode.Tag;
                if (targetGroupName == "[Chưa phân nhóm]") targetGroupName = string.Empty;
            }
            else if (targetNode.Tag is SheetItem)
            {
                targetGroupName = ((SheetItem)targetNode.Tag).GroupTitle ?? string.Empty;
            }

            // Kéo từ DataGridView sang
            if (e.Data.GetDataPresent(typeof(List<SheetItem>)))
            {
                List<SheetItem> droppedItems = e.Data.GetData(typeof(List<SheetItem>)) as List<SheetItem>;
                if (droppedItems != null && droppedItems.Count > 0)
                {
                    foreach (SheetItem item in droppedItems)
                    {
                        item.GroupTitle = targetGroupName;
                    }
                    _selectedTreeGroup = string.IsNullOrEmpty(targetGroupName) ? null : targetGroupName;
                    RebuildTreeNodes();
                    RefreshGridData();
                    SelectGroupNodeInTree(targetGroupName);
                    return;
                }
            }

            // Kéo từ TreeView sang
            TreeNode draggedNode = e.Data.GetData(typeof(TreeNode)) as TreeNode;
            if (draggedNode != null && draggedNode != targetNode)
            {
                if (draggedNode.Tag is SheetItem)
                {
                    ((SheetItem)draggedNode.Tag).GroupTitle = targetGroupName;
                    _selectedTreeGroup = string.IsNullOrEmpty(targetGroupName) ? null : targetGroupName;
                    RebuildTreeNodes();
                    RefreshGridData();
                    SelectGroupNodeInTree(targetGroupName);
                }
                else if (draggedNode.Tag is string && (string)draggedNode.Tag != "[Chưa phân nhóm]")
                {
                    string srcGroup = (string)draggedNode.Tag;
                    foreach (SheetItem s in _allSheets)
                    {
                        if (string.Equals(s.GroupTitle, srcGroup, StringComparison.OrdinalIgnoreCase))
                        {
                            s.GroupTitle = targetGroupName;
                        }
                    }
                    _selectedTreeGroup = string.IsNullOrEmpty(targetGroupName) ? null : targetGroupName;
                    RebuildTreeNodes();
                    RefreshGridData();
                    SelectGroupNodeInTree(targetGroupName);
                }
            }
        }

        private void DgvSheets_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && dgvSheets.SelectedRows.Count > 0)
            {
                DataGridView.HitTestInfo hit = dgvSheets.HitTest(e.X, e.Y);
                if (hit.RowIndex >= 0 && hit.ColumnIndex != colCheck.Index)
                {
                    _dragStartPoint = e.Location;
                }
                else
                {
                    _dragStartPoint = Point.Empty;
                }
            }
            else
            {
                _dragStartPoint = Point.Empty;
            }
        }

        private void DgvSheets_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _dragStartPoint != Point.Empty)
            {
                if (Math.Abs(e.X - _dragStartPoint.X) > 8 || Math.Abs(e.Y - _dragStartPoint.Y) > 8)
                {
                    List<SheetItem> dragItems = new List<SheetItem>();
                    foreach (DataGridViewRow row in dgvSheets.SelectedRows)
                    {
                        SheetItem item = row.Tag as SheetItem;
                        if (item != null && !dragItems.Contains(item))
                        {
                            dragItems.Add(item);
                        }
                    }
                    if (dragItems.Count > 0)
                    {
                        _dragStartPoint = Point.Empty;
                        dgvSheets.DoDragDrop(dragItems, DragDropEffects.Move);
                    }
                }
            }
        }

        private void RenameSelectedGroup()
        {
            if (tvGroups.SelectedNode == null || tvGroups.SelectedNode == _rootNode)
            {
                MessageBox.Show("Vui lòng chọn một thư mục nhóm trong cây để đổi tên!", "HDTOOL", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string oldName = tvGroups.SelectedNode.Tag as string;
            if (string.IsNullOrEmpty(oldName) || oldName == "[Chưa phân nhóm]")
            {
                MessageBox.Show("Không thể đổi tên thư mục gốc hoặc thư mục chưa phân nhóm!", "HDTOOL", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<SheetItem> inGroup = _allSheets.Where(delegate(SheetItem x) { return string.Equals(x.GroupTitle, oldName, StringComparison.OrdinalIgnoreCase); }).ToList();
            List<string> existing = _allSheets.Select(delegate(SheetItem x) { return x.GroupTitle; }).Where(delegate(string x) { return !string.IsNullOrEmpty(x); }).Distinct().ToList();

            SetGroupTitleForm form = new SetGroupTitleForm(inGroup.Count, existing, oldName);
            form.GroupTitleConfirmed += delegate(string newName)
            {
                if (!string.IsNullOrEmpty(newName) && !string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (SheetItem s in inGroup)
                    {
                        s.GroupTitle = newName;
                    }
                    _selectedTreeGroup = newName;
                    RebuildTreeNodes();
                    RefreshGridData();
                }
            };
            form.Show(this);
        }

        private void DeleteSelectedGroup()
        {
            if (tvGroups.SelectedNode == null || tvGroups.SelectedNode == _rootNode) return;
            string groupName = tvGroups.SelectedNode.Tag as string;
            if (string.IsNullOrEmpty(groupName) || groupName == "[Chưa phân nhóm]") return;

            DialogResult dr = MessageBox.Show(string.Format("Bạn có chắc chắn muốn giải tán thư mục nhóm \"{0}\"?\n(Các bản vẽ sẽ được chuyển về \"Chưa phân nhóm\", không bị xóa)", groupName),
                "Xác Nhận Giải Tán Nhóm", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (dr == DialogResult.Yes)
            {
                foreach (SheetItem s in _allSheets)
                {
                    if (string.Equals(s.GroupTitle, groupName, StringComparison.OrdinalIgnoreCase))
                    {
                        s.GroupTitle = string.Empty;
                    }
                }
                _selectedTreeGroup = null;
                RebuildTreeNodes();
                RefreshGridData();
            }
        }

        private void AutoDetectDisciplines()
        {
            int count = 0;
            foreach (SheetItem s in _allSheets)
            {
                if (string.IsNullOrEmpty(s.GroupTitle))
                {
                    string detected = ExportSheetListService.DetectDisciplineGroupTitle(s);
                    if (!string.IsNullOrEmpty(detected))
                    {
                        s.GroupTitle = detected;
                        count++;
                    }
                }
            }

            RebuildTreeNodes();
            RefreshGridData();

            if (count > 0)
            {
                MessageBox.Show(string.Format("Đã tự động phân nhóm thành công cho {0} bản vẽ theo các bộ môn kỹ thuật!", count),
                    "Tự Động Phân Nhóm", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Tất cả các bản vẽ đã được phân nhóm từ trước hoặc không tìm thấy từ khóa bộ môn phù hợp.",
                    "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void CheckSelectedGroup(bool isChecked)
        {
            if (tvGroups.SelectedNode == null) return;
            TreeNode node = tvGroups.SelectedNode;
            node.Checked = isChecked;

            _isUpdatingTreeCheck = true;
            try
            {
                UpdateChildNodesCheckState(node, isChecked);
                if (node.Tag is string)
                {
                    string g = (string)node.Tag;
                    foreach (SheetItem s in _allSheets)
                    {
                        if (g == "[Chưa phân nhóm]")
                        {
                            if (string.IsNullOrEmpty(s.GroupTitle)) s.IsSelected = isChecked;
                        }
                        else
                        {
                            if (string.Equals(s.GroupTitle, g, StringComparison.OrdinalIgnoreCase)) s.IsSelected = isChecked;
                        }
                    }
                }
                else if (node == _rootNode)
                {
                    foreach (SheetItem s in _allSheets) s.IsSelected = isChecked;
                }

                SyncGridCheckboxesFromData();
                UpdateStatusCount();
            }
            finally
            {
                _isUpdatingTreeCheck = false;
            }
        }

        #endregion

        private void SetSelectionState(bool selected)
        {
            _isUpdatingCheck = true;
            try
            {
                foreach (SheetItem item in _filteredSheets)
                {
                    item.IsSelected = selected;
                }
                RefreshGridData();
            }
            finally
            {
                _isUpdatingCheck = false;
            }
            UpdateStatusCount();
        }

        private void InvertSelection()
        {
            _isUpdatingCheck = true;
            try
            {
                foreach (SheetItem item in _filteredSheets)
                {
                    item.IsSelected = !item.IsSelected;
                }
                RefreshGridData();
            }
            finally
            {
                _isUpdatingCheck = false;
            }
            UpdateStatusCount();
        }

        private void BtnMoveUp_Click(object sender, EventArgs e)
        {
            if (dgvSheets.SelectedRows.Count == 0) return;
            int idx = dgvSheets.SelectedRows[0].Index;
            if (idx <= 0) return;

            DataGridViewRow curRow = dgvSheets.Rows[idx];
            SheetItem item = curRow.Tag as SheetItem;
            if (item == null) return;

            int prevRowIdx = idx - 1;
            SheetItem prevItem = dgvSheets.Rows[prevRowIdx].Tag as SheetItem;
            if (prevItem == null) return;

            // Đổi chỗ trong _filteredSheets
            int f1 = _filteredSheets.IndexOf(item);
            int f2 = _filteredSheets.IndexOf(prevItem);
            if (f1 >= 0 && f2 >= 0)
            {
                _filteredSheets[f1] = prevItem;
                _filteredSheets[f2] = item;
            }

            // Đổi chỗ trong _allSheets
            int a1 = _allSheets.IndexOf(item);
            int a2 = _allSheets.IndexOf(prevItem);
            if (a1 >= 0 && a2 >= 0)
            {
                _allSheets[a1] = prevItem;
                _allSheets[a2] = item;
            }

            RefreshGridData();

            // Khôi phục con trỏ dòng cho bản vẽ vừa di chuyển
            for (int i = 0; i < dgvSheets.Rows.Count; i++)
            {
                if (dgvSheets.Rows[i].Tag == item)
                {
                    dgvSheets.ClearSelection();
                    dgvSheets.Rows[i].Selected = true;
                    try
                    {
                        dgvSheets.CurrentCell = dgvSheets.Rows[i].Cells[colNumber.Index];
                    }
                    catch { }
                    break;
                }
            }
        }

        private void BtnMoveDown_Click(object sender, EventArgs e)
        {
            if (dgvSheets.SelectedRows.Count == 0) return;
            int idx = dgvSheets.SelectedRows[0].Index;
            if (idx >= dgvSheets.Rows.Count - 1) return;

            DataGridViewRow curRow = dgvSheets.Rows[idx];
            SheetItem item = curRow.Tag as SheetItem;
            if (item == null) return;

            int nextRowIdx = idx + 1;
            SheetItem nextItem = dgvSheets.Rows[nextRowIdx].Tag as SheetItem;
            if (nextItem == null) return;

            // Đổi chỗ trong _filteredSheets
            int f1 = _filteredSheets.IndexOf(item);
            int f2 = _filteredSheets.IndexOf(nextItem);
            if (f1 >= 0 && f2 >= 0)
            {
                _filteredSheets[f1] = nextItem;
                _filteredSheets[f2] = item;
            }

            // Đổi chỗ trong _allSheets
            int a1 = _allSheets.IndexOf(item);
            int a2 = _allSheets.IndexOf(nextItem);
            if (a1 >= 0 && a2 >= 0)
            {
                _allSheets[a1] = nextItem;
                _allSheets[a2] = item;
            }

            RefreshGridData();

            // Khôi phục con trỏ dòng cho bản vẽ vừa di chuyển
            for (int i = 0; i < dgvSheets.Rows.Count; i++)
            {
                if (dgvSheets.Rows[i].Tag == item)
                {
                    dgvSheets.ClearSelection();
                    dgvSheets.Rows[i].Selected = true;
                    try
                    {
                        dgvSheets.CurrentCell = dgvSheets.Rows[i].Cells[colNumber.Index];
                    }
                    catch { }
                    break;
                }
            }
        }

        private void UpdateStatusCount()
        {
            int total = _allSheets.Count;
            int selected = _allSheets.Count(delegate(SheetItem s) { return s.IsSelected; });
            int modified = _allSheets.Count(delegate(SheetItem s) { return s.IsModified; });

            if (modified > 0)
            {
                lblStatus.Text = string.Format("Tổng: {0} | Đã chọn: {1} (Hiện: {2}) | ✎ ĐÃ SỬA: {3} bản vẽ (Chưa lưu vào Revit)",
                    total, selected, _filteredSheets.Count, modified);
                lblStatus.ForeColor = Color.FromArgb(192, 57, 43); // Red HadoMEE
            }
            else
            {
                lblStatus.Text = string.Format("Tổng: {0} bản vẽ  |  Đã chọn: {1} bản vẽ  (Hiển thị: {2})",
                    total, selected, _filteredSheets.Count);
                lblStatus.ForeColor = Color.FromArgb(41, 128, 185); // Blue
            }

            btnExport.Enabled = (selected > 0);
            btnExportPdf.Enabled = (selected > 0);
            btnExportCad.Enabled = (selected > 0);
            if (btnExportCombinedCad != null) btnExportCombinedCad.Enabled = (_allSheets.Count > 0);
            btnClipboard.Enabled = (selected > 0);

            UpdateSaveButtonState();
        }

        private void UpdateSaveButtonState()
        {
            if (btnSaveToRevit == null) return;
            int modified = _allSheets.Count(delegate(SheetItem s) { return s.IsModified; });
            if (modified > 0)
            {
                btnSaveToRevit.Text = string.Format("✔  LƯU {0} BẢN VẼ VÀO REVIT", modified);
                btnSaveToRevit.BackColor = Color.FromArgb(192, 57, 43); // HadoMEE Red
                btnSaveToRevit.Enabled = true;
            }
            else
            {
                btnSaveToRevit.Text = "✔  LƯU VÀO REVIT";
                btnSaveToRevit.BackColor = Color.FromArgb(160, 170, 180);
                btnSaveToRevit.Enabled = false;
            }
        }

        private int SaveModifiedSheetsToRevit()
        {
            if (!this.IsDisposed && dgvSheets != null)
            {
                try { dgvSheets.EndEdit(); } catch { }
            }

            List<SheetItem> modifiedItems = _allSheets.Where(delegate(SheetItem s) {
                return s.IsModified;
            }).ToList();

            if (modifiedItems.Count == 0) return 0;

            HDToolModelessHost.Run(uiapp =>
            {
                Document docToUse = (uiapp != null && uiapp.ActiveUIDocument != null) ? uiapp.ActiveUIDocument.Document : _doc;
                int successCount = 0;
                List<string> details = new List<string>();
                List<string> errors = new List<string>();

                using (Transaction trans = new Transaction(docToUse, "HDTOOL - Đổi Tên & Số Hiệu Bản Vẽ Trực Tiếp"))
                {
                    trans.Start();

                    // GIAI ĐOẠN 1: Đổi các SheetNumber bị sửa sang mã tạm ngẫu nhiên để tránh lỗi xung đột "Sheet number already in use" của Revit
                    foreach (SheetItem item in modifiedItems)
                    {
                        if (item.IsNumberModified)
                        {
                            try
                            {
                                ViewSheet vs = docToUse.GetElement(item.SheetId) as ViewSheet;
                                if (vs != null)
                                {
                                    vs.SheetNumber = "HD_TMP_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                                }
                            }
                            catch { }
                        }
                    }

                    // GIAI ĐOẠN 2: Cập nhật Số hiệu và Tên chính thức
                    foreach (SheetItem item in modifiedItems)
                    {
                        try
                        {
                            Element elem = docToUse.GetElement(item.SheetId);
                            ViewSheet vs = elem as ViewSheet;
                            if (vs != null)
                            {
                                string oldNum = item.OriginalSheetNumber;
                                string oldName = item.OriginalSheetName;

                                if (item.IsNumberModified)
                                {
                                    vs.SheetNumber = item.SheetNumber;
                                    item.OriginalSheetNumber = item.SheetNumber;
                                }

                                if (item.IsNameModified)
                                {
                                    string sanitizedName = UpdateSheetListService.SanitizeSheetName(item.SheetName);
                                    vs.Name = sanitizedName;
                                    item.SheetName = sanitizedName;
                                    item.OriginalSheetName = sanitizedName;
                                }

                                if (!string.IsNullOrEmpty(item.Revision))
                                {
                                    Parameter pRev = vs.get_Parameter(BuiltInParameter.SHEET_CURRENT_REVISION);
                                    if (pRev != null && !pRev.IsReadOnly) pRev.Set(item.Revision);
                                }
                                if (!string.IsNullOrEmpty(item.IssueDate))
                                {
                                    Parameter pDate = vs.get_Parameter(BuiltInParameter.SHEET_ISSUE_DATE);
                                    if (pDate != null && !pDate.IsReadOnly) pDate.Set(item.IssueDate);
                                }
                                if (!string.IsNullOrEmpty(item.DrawnBy))
                                {
                                    Parameter pDrawn = vs.get_Parameter(BuiltInParameter.SHEET_DRAWN_BY);
                                    if (pDrawn != null && !pDrawn.IsReadOnly) pDrawn.Set(item.DrawnBy);
                                }

                                details.Add(string.Format("• [{0}]: Số hiệu \"{1}\"➔\"{2}\" | Tên \"{3}\"➔\"{4}\"",
                                    vs.SheetNumber, oldNum, item.SheetNumber, oldName, item.SheetName));
                                successCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Add(string.Format("• [{0}]: Lỗi ({1})", item.SheetNumber, ex.Message));
                        }
                    }

                    trans.Commit();
                }

                StringBuilder sb = new StringBuilder();
                sb.AppendLine(string.Format("Đã cập nhật thành công {0} bản vẽ trong Revit!", successCount));
                if (details.Count > 0)
                {
                    sb.AppendLine("\nChi tiết thay đổi:");
                    foreach (string d in details.Take(25)) sb.AppendLine(d);
                    if (details.Count > 25)
                    {
                        sb.AppendLine(string.Format("... và {0} bản vẽ khác.", details.Count - 25));
                    }
                }
                if (errors.Count > 0)
                {
                    sb.AppendLine("\nBản vẽ gặp lỗi:");
                    foreach (string err in errors) sb.AppendLine(err);
                }

                if (!this.IsDisposed && this.IsHandleCreated)
                {
                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        if (!this.IsDisposed)
                        {
                            RefreshGridData();
                            UpdateSaveButtonState();
                            MessageBox.Show(sb.ToString(), "HDTOOL - Đã Lưu Vào Revit", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    });
                }
                else
                {
                    TaskDialog.Show("HDTOOL - Đã Lưu Vào Revit", sb.ToString());
                }
            });

            return modifiedItems.Count;
        }

        private void BtnSaveToRevit_Click(object sender, EventArgs e)
        {
            dgvSheets.EndEdit();

            int modified = _allSheets.Count(delegate(SheetItem s) { return s.IsModified; });
            if (modified == 0)
            {
                TaskDialog.Show("Thông Báo", "Chưa có bản vẽ nào được sửa Số Hiệu hoặc Tên trong bảng.\n\nBạn hãy nhấp chuột trực tiếp vào cột 'Số Hiệu' hoặc 'Tên Bản Vẽ' để sửa, sau đó bấm nút này để lưu vào Revit!");
                return;
            }

            DialogResult confirm = MessageBox.Show(
                string.Format("Bạn có muốn lưu các thay đổi cho {0} bản vẽ đã sửa vào mô hình Revit không?", modified),
                "Xác Nhận Lưu Vào Revit",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm == DialogResult.Yes)
            {
                SaveModifiedSheetsToRevit();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            dgvSheets.EndEdit();

            int modifiedCount = _allSheets.Count(delegate(SheetItem s) { return s.IsModified; });
            if (modifiedCount > 0)
            {
                DialogResult dr = MessageBox.Show(
                    string.Format("Bạn có {0} bản vẽ đã sửa Số Hiệu/Tên nhưng chưa bấm 'LƯU VÀO REVIT'.\n\nBạn có muốn lưu các thay đổi này vào Revit trước khi đóng không?", modifiedCount),
                    "Xác Nhận Lưu Thay Đổi",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);

                if (dr == DialogResult.Yes)
                {
                    SaveModifiedSheetsToRevit();
                }
                else if (dr == DialogResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
            }

            base.OnFormClosing(e);
        }

        private void TxtSearch_TextChanged(object sender, EventArgs e)
        {
            ApplyFilter();
        }

        private void BtnExport_Click(object sender, EventArgs e)
        {
            List<SheetItem> selectedSheets = _allSheets.Where(delegate(SheetItem s) { return s.IsSelected; }).ToList();
            if (selectedSheets.Count == 0)
            {
                TaskDialog.Show("Thông Báo", "Vui lòng tích chọn ít nhất 1 bản vẽ để xuất danh mục.");
                return;
            }

            string projectName = string.Empty;
            try
            {
                projectName = _doc.Title ?? "RevitProject";
            }
            catch { }

            string defaultName = string.Format("Danh_Muc_Ban_Ve_{0}_{1}",
                projectName.Replace(" ", "_"),
                DateTime.Now.ToString("yyyyMMdd_HHmm"));

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Title = "Lưu Danh Mục Bản Vẽ";
                sfd.FileName = defaultName;
                sfd.Filter = "File Excel XML Spreadsheet (*.xls)|*.xls|File CSV UTF-8 (Tương thích mọi Excel) (*.csv)|*.csv";
                sfd.DefaultExt = "xls";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        string ext = Path.GetExtension(sfd.FileName).ToLower();
                        if (ext == ".csv")
                        {
                            ExportSheetListService.ExportToCsv(sfd.FileName, selectedSheets);
                        }
                        else
                        {
                            ExportSheetListService.ExportToExcelXml(sfd.FileName, selectedSheets, projectName);
                        }

                        TaskDialog.Show("Thành Công",
                            string.Format("Đã xuất danh mục thành công cho {0} bản vẽ!\n\nĐường dẫn file:\n{1}",
                            selectedSheets.Count, sfd.FileName));

                        if (chkOpenAfter.Checked && File.Exists(sfd.FileName))
                        {
                            try
                            {
                                Process.Start(new ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
                            }
                            catch { }
                        }
                    }
                    catch (Exception ex)
                    {
                        TaskDialog.Show("Lỗi Xuất File", "Không thể xuất file: " + ex.Message);
                    }
                }
            }
        }

        private void BtnExportPdf_Click(object sender, EventArgs e)
        {
            List<SheetItem> selected = _allSheets.Where(delegate(SheetItem s) { return s.IsSelected; }).ToList();
            if (selected.Count == 0)
            {
                TaskDialog.Show("Thông Báo", "Vui lòng tích chọn ít nhất một bản vẽ để xuất PDF.");
                return;
            }

            string activeGroup = selected.Count > 0 ? selected[0].GroupTitle : string.Empty;

            ExportCadPdfOptionsForm form = new ExportCadPdfOptionsForm(_doc, selected, ExportTargetFormat.Pdf, activeGroup);
            form.Show(this);
        }

        private void BtnExportCad_Click(object sender, EventArgs e)
        {
            List<SheetItem> selected = _allSheets.Where(delegate(SheetItem s) { return s.IsSelected; }).ToList();
            if (selected.Count == 0)
            {
                TaskDialog.Show("Thông Báo", "Vui lòng tích chọn ít nhất một bản vẽ để xuất CAD (.dwg).");
                return;
            }

            string activeGroup = selected.Count > 0 ? selected[0].GroupTitle : string.Empty;

            ExportCadPdfOptionsForm form = new ExportCadPdfOptionsForm(_doc, selected, ExportTargetFormat.Cad, activeGroup);
            form.Show(this);
        }

        private void BtnExportCombinedCad_Click(object sender, EventArgs e)
        {
            List<SheetItem> selected = _allSheets.Where(delegate(SheetItem s) { return s.IsSelected; }).ToList();
            if (selected.Count == 0)
            {
                DialogResult dr = MessageBox.Show(
                    string.Format("Bạn chưa tích chọn bản vẽ nào.\n\nBạn có muốn xuất TẤT CẢ ({0} bản vẽ) trong dự án ra cùng 1 FILE CAD DUY NHẤT (có đầy đủ Khung Tên) không?", _allSheets.Count),
                    "HDTOOL - Xuất Gộp 1 File CAD",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (dr == DialogResult.Yes)
                {
                    SetSelectionState(true);
                    selected = _allSheets.Where(delegate(SheetItem s) { return s.IsSelected; }).ToList();
                }
                else
                {
                    return;
                }
            }

            if (selected.Count == 0) return;

            string projectName = "RevitProject";
            try
            {
                if (!string.IsNullOrEmpty(_doc.Title)) projectName = _doc.Title;
            }
            catch { }

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                projectName = projectName.Replace(c, '_');
            }

            string defaultName = string.Format("{0}_BanVe_TongHop_{1}",
                projectName.Replace(" ", "_"),
                DateTime.Now.ToString("yyyyMMdd_HHmm"));

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Title = string.Format("Lưu {0} Bản Vẽ Vào CÙNG 1 FILE CAD DUY NHẤT (Có Đầy Đủ Khung Tên)", selected.Count);
                sfd.Filter = "Bản vẽ AutoCAD Drawing (*.dwg)|*.dwg";
                sfd.DefaultExt = "dwg";
                sfd.FileName = defaultName;

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    this.Cursor = Cursors.WaitCursor;
                    lblStatus.Text = string.Format("Đang xuất và gộp {0} bản vẽ sang 1 file CAD...", selected.Count);
                    lblStatus.ForeColor = Color.FromArgb(192, 57, 43);
                    this.Update();

                    string targetFileName = sfd.FileName;
                    bool openAfter = chkOpenAfter.Checked;

                    HDToolModelessHost.Run(uiapp =>
                    {
                        try
                        {
                            string resultMsg;
                            bool success = SheetExportCadPdfService.ExportToSingleCombinedCad(
                                _doc,
                                selected,
                                targetFileName,
                                openAfter,
                                out resultMsg);

                            this.BeginInvoke((MethodInvoker)delegate
                            {
                                this.Cursor = Cursors.Default;
                                UpdateStatusCount();
                                if (success)
                                {
                                    MessageBox.Show(resultMsg, "HDTOOL - Xuất Gộp 1 File CAD Thành Công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                }
                                else
                                {
                                    MessageBox.Show(resultMsg, "HDTOOL - Thông Báo Xuất CAD", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            this.BeginInvoke((MethodInvoker)delegate
                            {
                                this.Cursor = Cursors.Default;
                                UpdateStatusCount();
                                MessageBox.Show("Lỗi xuất CAD: " + ex.Message, "HDTOOL Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            });
                        }
                    });
                }
            }
        }

        private void BtnClipboard_Click(object sender, EventArgs e)
        {
            List<SheetItem> selectedSheets = _allSheets.Where(delegate(SheetItem s) { return s.IsSelected; }).ToList();
            if (selectedSheets.Count == 0)
            {
                TaskDialog.Show("Thông Báo", "Vui lòng tích chọn ít nhất 1 bản vẽ để sao chép.");
                return;
            }

            try
            {
                ExportSheetListService.ExportToClipboard(selectedSheets);
                TaskDialog.Show("Thành Công",
                    string.Format("Đã sao chép danh mục {0} bản vẽ vào bộ nhớ Clipboard!\n\nBạn có thể nhấn Ctrl + V để dán trực tiếp vào Excel, Word hoặc Bảng Schedule trong Revit.",
                    selectedSheets.Count));
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Lỗi", "Không thể sao chép vào Clipboard: " + ex.Message);
            }
        }

        private void BtnUpdateExcel_Click(object sender, EventArgs e)
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

                if (ofd.ShowDialog() != DialogResult.OK) return;

                List<List<string>> rawRows = UpdateSheetListService.ReadRowsFromFile(ofd.FileName);
                if (rawRows == null || rawRows.Count == 0)
                {
                    TaskDialog.Show("Thông Báo", "File đã chọn không có dữ liệu bảng tính hợp lệ.");
                    return;
                }

                List<SheetUpdateItem> items = UpdateSheetListService.MatchAndCompare(_doc, rawRows);
                if (items.Count == 0)
                {
                    TaskDialog.Show("Thông Báo", "Không tìm thấy số hiệu bản vẽ nào trong file khớp với dự án Revit.");
                    return;
                }

                UpdateSheetListPreviewForm preview = new UpdateSheetListPreviewForm(_doc, items);
                preview.UpdatesApplied += delegate(int updatedCount)
                {
                    if (updatedCount > 0 && !this.IsDisposed)
                    {
                        _allSheets = ExportSheetListService.GetSheetsFromDocument(_doc);
                        ApplyFilter();
                    }
                };
                preview.Show(this);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Lỗi Cập Nhật", "Lỗi khi đọc file hoặc cập nhật bản vẽ:\n\n" + ex.Message);
            }
        }

        private void BtnUpdateClipboard_Click(object sender, EventArgs e)
        {
            try
            {
                if (!Clipboard.ContainsText())
                {
                    TaskDialog.Show("Thông Báo", "Bộ nhớ Clipboard đang trống.\n\nHãy mở Excel, quét chọn bảng (chứa cột Sheet Number và Sheet Name) rồi nhấn Ctrl + C trước!");
                    return;
                }

                string clipText = Clipboard.GetText();
                List<List<string>> rawRows = UpdateSheetListService.ReadRowsFromClipboard(clipText);
                if (rawRows == null || rawRows.Count == 0)
                {
                    TaskDialog.Show("Thông Báo", "Dữ liệu trong Clipboard không chứa cấu trúc bảng hợp lệ.");
                    return;
                }

                List<SheetUpdateItem> items = UpdateSheetListService.MatchAndCompare(_doc, rawRows);
                if (items.Count == 0)
                {
                    TaskDialog.Show("Thông Báo", "Không tìm thấy số hiệu bản vẽ nào trong Clipboard khớp với dự án Revit.");
                    return;
                }

                UpdateSheetListPreviewForm preview = new UpdateSheetListPreviewForm(_doc, items);
                preview.UpdatesApplied += delegate(int updatedCount)
                {
                    if (updatedCount > 0 && !this.IsDisposed)
                    {
                        _allSheets = ExportSheetListService.GetSheetsFromDocument(_doc);
                        ApplyFilter();
                    }
                };
                preview.Show(this);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Lỗi Cập Nhật", "Lỗi khi đọc Clipboard hoặc cập nhật bản vẽ:\n\n" + ex.Message);
            }
        }
    }
}
