using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Color = System.Drawing.Color;
using Font = System.Drawing.Font;
using Point = System.Drawing.Point;
using ComboBox = System.Windows.Forms.ComboBox;
using TextBox = System.Windows.Forms.TextBox;
using CheckBox = System.Windows.Forms.CheckBox;
using RadioButton = System.Windows.Forms.RadioButton;
using GroupBox = System.Windows.Forms.GroupBox;
using Button = System.Windows.Forms.Button;

namespace HDTool
{
    public enum DuctPickMode
    {
        None,
        NewArea,
        AppendArea
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class DuctQuantityCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (ToolHotReloadManager.HasActiveHotReloadAssembly())
            {
                return ToolHotReloadManager.ExecuteDynamicCommand(
                    this.GetType().FullName,
                    commandData,
                    ref message,
                    elements
                );
            }
            return ExecuteDirect(commandData, ref message, elements);
        }

        public Result ExecuteDirect(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiapp = commandData.Application;
                UIDocument uidoc = uiapp.ActiveUIDocument;
                if (uidoc == null)
                {
                    TaskDialog.Show("HDTOOL", "Vui lòng mở một dự án Revit trước khi chạy công cụ này.");
                    return Result.Cancelled;
                }

                Document doc = uidoc.Document;

                List<ElementId> currentCustomSelection = new List<ElementId>();
                bool forceSelectionScope = false;

                // Nhan dien selection co san truoc khi mo tool
                try
                {
                    ICollection<ElementId> initSel = uidoc.Selection.GetElementIds();
                    if (initSel != null && initSel.Count > 0)
                    {
                        foreach (ElementId id in initSel)
                        {
                            Element el = doc.GetElement(id);
                            if (el != null && el.Category != null)
                            {
                                BuiltInCategory bic = (BuiltInCategory)el.Category.Id.IntegerValue;
                                if (bic == BuiltInCategory.OST_DuctCurves ||
                                    bic == BuiltInCategory.OST_DuctFitting ||
                                    bic == BuiltInCategory.OST_DuctAccessory ||
                                    bic == BuiltInCategory.OST_DuctTerminal ||
                                    bic == BuiltInCategory.OST_FlexDuctCurves)
                                {
                                    currentCustomSelection.Add(id);
                                }
                            }
                        }
                        if (currentCustomSelection.Count > 0)
                        {
                            forceSelectionScope = true;
                        }
                    }
                }
                catch { }

                // Vong lap Modal an toan tuyet doi: Giai phong hoan toan Revit UI giup quet chuot 60 FPS khong bao gio do lag
                while (true)
                {
                    using (DuctQuantityForm form = new DuctQuantityForm(doc, uidoc, currentCustomSelection, forceSelectionScope))
                    {
                        DialogResult res = form.ShowDialog();

                        if (res == DialogResult.Retry)
                        {
                            DuctPickMode mode = form.RequestedPickMode;
                            List<ElementId> pickedIds = PerformRevitDuctPick(uidoc, doc, mode);

                            if (pickedIds != null && pickedIds.Count > 0)
                            {
                                if (mode == DuctPickMode.AppendArea)
                                {
                                    foreach (ElementId pid in pickedIds)
                                    {
                                        if (!currentCustomSelection.Contains(pid)) currentCustomSelection.Add(pid);
                                    }
                                }
                                else
                                {
                                    currentCustomSelection = pickedIds.Distinct().ToList();
                                }
                                forceSelectionScope = true;
                            }

                            // Mo lai form voi vung vua quet
                            continue;
                        }

                        // Nguoi dung dong form binh thuong
                        break;
                    }
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("HDTOOL - Lỗi Khối Lượng Ống Gió", ex.Message);
                return Result.Failed;
            }
        }

        private static List<ElementId> PerformRevitDuctPick(UIDocument uidoc, Document doc, DuctPickMode mode)
        {
            List<ElementId> result = new List<ElementId>();
            if (uidoc == null || doc == null) return result;

            Autodesk.Revit.DB.View activeV = uidoc.ActiveView;

            try
            {
                if (activeV is ViewSheet)
                {
                    // 1. TREN SHEET: Click chon Viewport hoac click truc tiep ong gio
                    Reference pickedRef = uidoc.Selection.PickObject(
                        ObjectType.Element,
                        "HDTOOL: Click chuột chọn một Viewport trên Sheet (hoặc ống gió) để bóc khối lượng..."
                    );
                    if (pickedRef != null && pickedRef.ElementId != ElementId.InvalidElementId)
                    {
                        Element el = doc.GetElement(pickedRef.ElementId);
                        if (el is Viewport)
                        {
                            Viewport vp = el as Viewport;
                            Autodesk.Revit.DB.View targetV = doc.GetElement(vp.ViewId) as Autodesk.Revit.DB.View;
                            if (targetV != null)
                            {
                                result = DuctQuantityForm.CollectElementIdsFromTargetView(doc, targetV);
                            }
                        }
                        else if (el != null && el.Category != null)
                        {
                            result.Add(el.Id);
                        }
                    }
                }
                else if (activeV is View3D)
                {
                    // 2. TRONG VIEW 3D: Dùng PickObjects với bộ lọc chính xác, click hoặc quét chọn mượt mà
                    IList<Reference> pickedRefs = uidoc.Selection.PickObjects(
                        ObjectType.Element,
                        new DuctSelectionFilter(doc),
                        "HDTOOL (View 3D): Click hoặc quét chọn các ống gió/phụ kiện (Bấm Finish trên thanh Options Bar khi xong):"
                    );
                    if (pickedRefs != null && pickedRefs.Count > 0)
                    {
                        foreach (Reference r in pickedRefs)
                        {
                            if (r != null && r.ElementId != ElementId.InvalidElementId)
                            {
                                result.Add(r.ElementId);
                            }
                        }
                    }
                }
                else
                {
                    // 3. TRONG VIEW 2D (MẶT BẰNG, MẶT CẮT): Kéo quét hình chữ nhật siêu tốc
                    try
                    {
                        IList<Element> picked = uidoc.Selection.PickElementsByRectangle(
                            new DuctSelectionFilter(doc),
                            "HDTOOL: Kéo chuột quét một vùng hình chữ nhật để bóc khối lượng ống gió:"
                        );
                        if (picked != null && picked.Count > 0)
                        {
                            foreach (Element el in picked)
                            {
                                if (el != null) result.Add(el.Id);
                            }
                        }
                    }
                    catch (Autodesk.Revit.Exceptions.InvalidOperationException)
                    {
                        IList<Reference> fallbackRefs = uidoc.Selection.PickObjects(
                            ObjectType.Element,
                            new DuctSelectionFilter(doc),
                            "HDTOOL: Quét chọn hoặc click chọn các ống gió/phụ kiện cần bóc (Bấm Finish khi xong):"
                        );
                        if (fallbackRefs != null)
                        {
                            foreach (Reference r in fallbackRefs)
                            {
                                if (r != null && r.ElementId != ElementId.InvalidElementId) result.Add(r.ElementId);
                            }
                        }
                    }
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // Nguoi dung bam ESC hoac Cancel -> khong lam gi, mo lai form
            }
            catch (Exception ex)
            {
                TaskDialog.Show("HDTOOL", "Lỗi quét vùng: " + ex.Message);
            }

            return result;
        }
    }

    /// <summary>
    /// Form Thống Kê Khối Lượng Ống Gió HVAC (Tôn m² & Phụ Kiện Chuẩn TCVN 5687:2010 / SMACNA)
    /// ĐÃ TỐI ƯU HÓA SIÊU TỐC: Không lag đơ máy, không khóa UI, Highlight tức thì 60 FPS
    /// </summary>
    public class DuctQuantityForm : System.Windows.Forms.Form
    {
        private Document _doc;
        private UIDocument _uidoc;
        private Autodesk.Revit.DB.View _activeView;

        // Dữ liệu nội bộ
        private List<DuctQuantityItem> _allItems = new List<DuctQuantityItem>();
        private List<ElementId> _customSelectionIds = new List<ElementId>();
        private List<string> _customParamColumns = new List<string>();
        private HashSet<ElementId> _lastHighlightedIds = new HashSet<ElementId>();

        // CỜ KHÓA CHỐNG LẶP SỰ KIỆN & CHỐNG TREO MÁY
        private bool _isUpdatingGrid = false;
        private bool _isUpdatingScope = false;

        // Controls
        private System.Windows.Forms.Panel pnlHeader;
        private Label lblTitle;
        private Label lblSubtitle;
        private PictureBox picLogo;

        private GroupBox grpFilter;
        private RadioButton radCurrentView;
        private RadioButton radSelection;
        private RadioButton radAllProject;
        private Button btnPickArea;
        private Button btnPickAppendArea;

        private CheckBox chkDuct;
        private CheckBox chkFitting;
        private CheckBox chkDamper;
        private CheckBox chkDiffuser;
        private CheckBox chkFlex;

        private ComboBox cboLevel;
        private ComboBox cboSystem;
        private TextBox txtSearch;

        private System.Windows.Forms.Panel pnlActionToolbar;
        private CheckBox chkAutoHighlight;
        private Button btnHighlightAll;
        private Button btnClearHighlight;
        private Button btnZoom;
        private Button btnAddParamCol;
        private Button btnRemoveParamCol;

        private DataGridView dgvItems;
        private Label lblSummary;

        private System.Windows.Forms.Panel pnlBottom;
        private Button btnPlaceSheet;
        private Button btnExportExcel;
        private Button btnCopyClipboard;
        private Button btnClose;

        public DuctPickMode RequestedPickMode { get; private set; }

        public DuctQuantityForm(Document doc, UIDocument uidoc, List<ElementId> preSelectedIds = null, bool forceSelectionScope = false)
        {
            _doc = doc;
            _uidoc = uidoc;
            _activeView = uidoc != null ? uidoc.ActiveView : null;
            this.RequestedPickMode = DuctPickMode.None;

            if (preSelectedIds != null && preSelectedIds.Count > 0)
            {
                _customSelectionIds = new List<ElementId>(preSelectedIds);
            }
            else
            {
                CheckInitialSelection();
            }

            // Nạp cấu hình các cột Parameter dự án đã lưu từ trước
            try
            {
                var settings = MepUserSettings.Instance;
                if (settings.DuctCustomParamColumns != null && settings.DuctCustomParamColumns.Count > 0)
                {
                    _customParamColumns = new List<string>(settings.DuctCustomParamColumns);
                }
                else
                {
                    _customParamColumns.Add("Loại Bảo Ôn");
                    _customParamColumns.Add("Comments");
                }
            }
            catch { }

            InitializeComponent();

            if (forceSelectionScope && _customSelectionIds.Count > 0)
            {
                _isUpdatingScope = true;
                radSelection.Text = string.Format("Vùng Quét Cục Bộ ({0} đ.tượng)", _customSelectionIds.Count);
                radSelection.Checked = true;
                radCurrentView.Checked = false;
                _isUpdatingScope = false;
            }

            LoadDataAndRefreshUi();
        }

        private void CheckInitialSelection()
        {
            if (_uidoc == null || _doc == null) return;
            try
            {
                ICollection<ElementId> selIds = _uidoc.Selection.GetElementIds();
                if (selIds != null && selIds.Count > 0)
                {
                    List<ElementId> validIds = new List<ElementId>();
                    foreach (ElementId id in selIds)
                    {
                        Element el = _doc.GetElement(id);
                        if (el == null || el.Category == null) continue;

                        BuiltInCategory bic = (BuiltInCategory)el.Category.Id.IntegerValue;
                        if (bic == BuiltInCategory.OST_DuctCurves ||
                            bic == BuiltInCategory.OST_DuctFitting ||
                            bic == BuiltInCategory.OST_DuctAccessory ||
                            bic == BuiltInCategory.OST_DuctTerminal ||
                            bic == BuiltInCategory.OST_FlexDuctCurves)
                        {
                            validIds.Add(id);
                        }
                    }
                    if (validIds.Count > 0)
                    {
                        _customSelectionIds = validIds.Distinct().ToList();
                    }
                }
            }
            catch { }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.Text = "HDTOOL - THỐNG KÊ KHỐI LƯỢNG ỐNG GIÓ & PHỤ KIỆN HVAC (TCVN 5687:2010 / SMACNA)";
            this.Size = new Size(1300, 800);
            this.MinimumSize = new Size(1160, 680);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(248, 250, 252);
            this.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
            this.KeyPreview = true;
            this.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) this.Close(); };

            ToolTip toolTip = new ToolTip();
            toolTip.AutoPopDelay = 5000;
            toolTip.InitialDelay = 500;
            toolTip.ReshowDelay = 200;
            toolTip.ShowAlways = true;

            // ================= 1. HEADER BANNER HADOMEE =================
            pnlHeader = new System.Windows.Forms.Panel();
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = 72;
            pnlHeader.BackColor = Color.FromArgb(24, 43, 73);

            lblTitle = new Label();
            lblTitle.Text = "HDTOOL — THỐNG KÊ KHỐI LƯỢNG ỐNG GIÓ & PHỤ KIỆN HVAC (M² TÔN & PHỤ KIỆN)";
            lblTitle.ForeColor = Color.White;
            lblTitle.Font = new Font("Segoe UI", 11.5f, FontStyle.Bold);
            lblTitle.Location = new Point(16, 12);
            lblTitle.AutoSize = true;

            lblSubtitle = new Label();
            lblSubtitle.Text = "Tự động tính diện tích tôn m² theo TCVN 5687:2010 / SMACNA  |  Phân loại cút, côn, van VCD, miệng gió  |  Thêm cột Parameter dự án";
            lblSubtitle.ForeColor = Color.FromArgb(203, 213, 225);
            lblSubtitle.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblSubtitle.Location = new Point(16, 42);
            lblSubtitle.AutoSize = true;

            picLogo = new PictureBox();
            picLogo.Size = new Size(130, 42);
            picLogo.Location = new Point(this.ClientSize.Width - 145, 15);
            picLogo.Anchor = AnchorStyles.Top | AnchorStyles.Right;
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

            // ================= 2. FILTER PANEL =================
            grpFilter = new GroupBox();
            grpFilter.Text = "Bộ Lọc & Phạm Vi Bóc Khối Lượng Ống Gió";
            grpFilter.Location = new Point(12, 78);
            grpFilter.Size = new Size(this.ClientSize.Width - 24, 115);
            grpFilter.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            grpFilter.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            grpFilter.ForeColor = Color.FromArgb(30, 41, 59);

            // Cụm 1: Phạm vi
            string viewName = _activeView != null ? _activeView.Name : "View Hiện Tại";
            radCurrentView = new RadioButton();
            radCurrentView.Text = string.Format("Khung Nhìn Hiện Hành ({0})", TruncateString(viewName, 18));
            radCurrentView.Location = new Point(12, 22);
            radCurrentView.Size = new Size(245, 22);
            radCurrentView.Checked = (_customSelectionIds.Count == 0);
            radCurrentView.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            radCurrentView.Cursor = Cursors.Hand;
            radCurrentView.CheckedChanged += (s, e) => {
                if (radCurrentView.Checked && !_isUpdatingScope) LoadDataAndRefreshUi();
            };

            radSelection = new RadioButton();
            radSelection.Text = _customSelectionIds.Count > 0
                ? string.Format("Vùng Quét Cục Bộ ({0} đ.tượng)", _customSelectionIds.Count)
                : "Vùng Quét Cục Bộ (Chưa quét)";
            radSelection.Location = new Point(12, 48);
            radSelection.Size = new Size(245, 22);
            radSelection.Checked = (_customSelectionIds.Count > 0);
            radSelection.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            radSelection.ForeColor = Color.FromArgb(217, 119, 6);
            radSelection.Cursor = Cursors.Hand;
            radSelection.CheckedChanged += (s, e) => {
                if (radSelection.Checked && !_isUpdatingScope) LoadDataAndRefreshUi();
            };

            radAllProject = new RadioButton();
            radAllProject.Text = "Toàn Bộ Dự Án (All Document)";
            radAllProject.Location = new Point(12, 74);
            radAllProject.Size = new Size(245, 22);
            radAllProject.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            radAllProject.Cursor = Cursors.Hand;
            radAllProject.CheckedChanged += (s, e) => {
                if (radAllProject.Checked && !_isUpdatingScope) LoadDataAndRefreshUi();
            };

            // Cụm 2: Nút quét chuột siêu mượt
            btnPickArea = new Button();
            btnPickArea.Text = "⬛ QUÉT VÙNG MỚI\r\n(Kéo quét hoặc Click View)";
            btnPickArea.Location = new Point(265, 20);
            btnPickArea.Size = new Size(160, 42);
            btnPickArea.BackColor = Color.FromArgb(217, 83, 79);
            btnPickArea.ForeColor = Color.White;
            btnPickArea.FlatStyle = FlatStyle.Flat;
            btnPickArea.FlatAppearance.BorderSize = 0;
            btnPickArea.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            btnPickArea.Cursor = Cursors.Hand;
            btnPickArea.Click += BtnPickArea_Click;
            toolTip.SetToolTip(btnPickArea, "Trên Sheet: Click chọn trực tiếp vào khung nhìn Viewport để lấy toàn bộ ống gió của View!\nTrong View mô hình: Kéo quét một khung chữ nhật cực nhanh không lag.");

            btnPickAppendArea = new Button();
            btnPickAppendArea.Text = "➕ Bóc Thêm (Cộng Dồn)";
            btnPickAppendArea.Location = new Point(265, 66);
            btnPickAppendArea.Size = new Size(160, 34);
            btnPickAppendArea.BackColor = Color.FromArgb(13, 148, 136);
            btnPickAppendArea.ForeColor = Color.White;
            btnPickAppendArea.FlatStyle = FlatStyle.Flat;
            btnPickAppendArea.FlatAppearance.BorderSize = 0;
            btnPickAppendArea.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            btnPickAppendArea.Cursor = Cursors.Hand;
            btnPickAppendArea.Click += BtnPickAppendArea_Click;
            toolTip.SetToolTip(btnPickAppendArea, "Quét thêm vùng mới hoặc click thêm Viewport để cộng dồn vào bảng");

            // Cụm 3: Hạng mục HVAC Checkbox
            System.Windows.Forms.Panel pnlCategories = new System.Windows.Forms.Panel();
            pnlCategories.Location = new Point(435, 18);
            pnlCategories.Size = new Size(280, 84);
            pnlCategories.BackColor = Color.FromArgb(241, 245, 249);
            pnlCategories.BorderStyle = BorderStyle.FixedSingle;

            chkDuct = new CheckBox { Text = "Ống Gió Thẳng (m² Tôn)", Checked = true, Location = new Point(8, 6), AutoSize = true, Font = new Font("Segoe UI", 8.0f, FontStyle.Bold), ForeColor = Color.FromArgb(2, 132, 199) };
            chkFitting = new CheckBox { Text = "Phụ Kiện Cút/Côn/Tê/Lượn", Checked = true, Location = new Point(8, 28), AutoSize = true, Font = new Font("Segoe UI", 8.0f, FontStyle.Bold), ForeColor = Color.FromArgb(16, 185, 129) };
            chkDamper = new CheckBox { Text = "Van Gió VCD / FD / NRD", Checked = true, Location = new Point(8, 50), AutoSize = true, Font = new Font("Segoe UI", 8.0f, FontStyle.Bold), ForeColor = Color.FromArgb(217, 119, 6) };
            chkDiffuser = new CheckBox { Text = "Miệng Gió (Diffuser, Slot)", Checked = true, Location = new Point(155, 6), AutoSize = true, Font = new Font("Segoe UI", 8.0f, FontStyle.Bold), ForeColor = Color.FromArgb(147, 51, 234) };
            chkFlex = new CheckBox { Text = "Ống Gió Mềm", Checked = true, Location = new Point(155, 28), AutoSize = true, Font = new Font("Segoe UI", 8.0f, FontStyle.Regular), ForeColor = Color.FromArgb(71, 85, 105) };

            chkDuct.CheckedChanged += (s, e) => LoadDataAndRefreshUi();
            chkFitting.CheckedChanged += (s, e) => LoadDataAndRefreshUi();
            chkDamper.CheckedChanged += (s, e) => LoadDataAndRefreshUi();
            chkDiffuser.CheckedChanged += (s, e) => LoadDataAndRefreshUi();
            chkFlex.CheckedChanged += (s, e) => LoadDataAndRefreshUi();

            pnlCategories.Controls.Add(chkDuct);
            pnlCategories.Controls.Add(chkFitting);
            pnlCategories.Controls.Add(chkDamper);
            pnlCategories.Controls.Add(chkDiffuser);
            pnlCategories.Controls.Add(chkFlex);

            // Cụm 4: Bộ lọc Tầng, Hệ Thống, Tìm kiếm
            Label lblLvl = new Label { Text = "Tầng:", Location = new Point(725, 24), Size = new Size(42, 20), Font = new Font("Segoe UI", 8.5f, FontStyle.Regular) };
            cboLevel = new ComboBox { Location = new Point(770, 21), Size = new Size(110, 24), DropDownStyle = ComboBoxStyle.DropDownList };
            cboLevel.SelectedIndexChanged += (s, e) => FilterAndDisplayItems();

            Label lblSys = new Label { Text = "Hệ Thống:", Location = new Point(890, 24), Size = new Size(62, 20), Font = new Font("Segoe UI", 8.5f, FontStyle.Regular) };
            cboSystem = new ComboBox { Location = new Point(955, 21), Size = new Size(125, 24), DropDownStyle = ComboBoxStyle.DropDownList };
            cboSystem.SelectedIndexChanged += (s, e) => FilterAndDisplayItems();

            Label lblSrch = new Label { Text = "Tìm:", Location = new Point(725, 60), Size = new Size(42, 20), Font = new Font("Segoe UI", 8.5f, FontStyle.Regular) };
            txtSearch = new TextBox { Location = new Point(770, 57), Size = new Size(180, 24), Text = "Tìm tên, size, hệ thống..." };
            txtSearch.ForeColor = Color.Gray;
            txtSearch.Enter += (s, e) => { if (txtSearch.ForeColor == Color.Gray) { txtSearch.Text = ""; txtSearch.ForeColor = Color.Black; } };
            txtSearch.Leave += (s, e) => { if (string.IsNullOrWhiteSpace(txtSearch.Text)) { txtSearch.Text = "Tìm tên, size, hệ thống..."; txtSearch.ForeColor = Color.Gray; } };
            txtSearch.TextChanged += (s, e) => { if (txtSearch.ForeColor != Color.Gray) FilterAndDisplayItems(); };

            // Cụm 5: Nút Quét Lại Dữ Liệu
            Button btnRefresh = new Button();
            btnRefresh.Text = "Quét Lại\r\nDữ Liệu";
            btnRefresh.Location = new Point(grpFilter.Width - 135, 20);
            btnRefresh.Size = new Size(120, 80);
            btnRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnRefresh.BackColor = Color.FromArgb(52, 73, 94);
            btnRefresh.ForeColor = Color.White;
            btnRefresh.FlatStyle = FlatStyle.Flat;
            btnRefresh.FlatAppearance.BorderSize = 0;
            btnRefresh.Cursor = Cursors.Hand;
            btnRefresh.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnRefresh.Click += (s, e) => LoadDataAndRefreshUi();

            grpFilter.Controls.Add(radCurrentView);
            grpFilter.Controls.Add(radSelection);
            grpFilter.Controls.Add(radAllProject);
            grpFilter.Controls.Add(btnPickArea);
            grpFilter.Controls.Add(btnPickAppendArea);
            grpFilter.Controls.Add(pnlCategories);
            grpFilter.Controls.Add(lblLvl);
            grpFilter.Controls.Add(cboLevel);
            grpFilter.Controls.Add(lblSys);
            grpFilter.Controls.Add(cboSystem);
            grpFilter.Controls.Add(lblSrch);
            grpFilter.Controls.Add(txtSearch);
            grpFilter.Controls.Add(btnRefresh);

            // ================= 3. ACTION TOOLBAR & QUẢN LÝ CỘT PARAMETER =================
            pnlActionToolbar = new System.Windows.Forms.Panel();
            pnlActionToolbar.Location = new Point(12, 200);
            pnlActionToolbar.Size = new Size(this.ClientSize.Width - 24, 40);
            pnlActionToolbar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            pnlActionToolbar.BackColor = Color.FromArgb(241, 245, 249);
            pnlActionToolbar.BorderStyle = BorderStyle.FixedSingle;

            chkAutoHighlight = new CheckBox();
            chkAutoHighlight.Text = "Tự động Highlight khi click dòng";
            chkAutoHighlight.Checked = true;
            chkAutoHighlight.Location = new Point(10, 9);
            chkAutoHighlight.AutoSize = true;
            chkAutoHighlight.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            chkAutoHighlight.ForeColor = Color.FromArgb(3, 105, 161);

            btnHighlightAll = new Button();
            btnHighlightAll.Text = "💡 Sáng Đã Bóc";
            btnHighlightAll.Location = new Point(230, 6);
            btnHighlightAll.Size = new Size(110, 26);
            btnHighlightAll.BackColor = Color.FromArgb(217, 119, 6);
            btnHighlightAll.ForeColor = Color.White;
            btnHighlightAll.FlatStyle = FlatStyle.Flat;
            btnHighlightAll.FlatAppearance.BorderSize = 0;
            btnHighlightAll.Font = new Font("Segoe UI", 8.0f, FontStyle.Bold);
            btnHighlightAll.Cursor = Cursors.Hand;
            btnHighlightAll.Click += BtnHighlightAll_Click;

            btnClearHighlight = new Button();
            btnClearHighlight.Text = "Tắt Sáng";
            btnClearHighlight.Location = new Point(345, 6);
            btnClearHighlight.Size = new Size(80, 26);
            btnClearHighlight.BackColor = Color.FromArgb(100, 116, 139);
            btnClearHighlight.ForeColor = Color.White;
            btnClearHighlight.FlatStyle = FlatStyle.Flat;
            btnClearHighlight.FlatAppearance.BorderSize = 0;
            btnClearHighlight.Font = new Font("Segoe UI", 8.0f, FontStyle.Regular);
            btnClearHighlight.Cursor = Cursors.Hand;
            btnClearHighlight.Click += BtnClearHighlight_Click;

            btnZoom = new Button();
            btnZoom.Text = "🔍 Zoom Gần";
            btnZoom.Location = new Point(430, 6);
            btnZoom.Size = new Size(95, 26);
            btnZoom.BackColor = Color.FromArgb(2, 132, 199);
            btnZoom.ForeColor = Color.White;
            btnZoom.FlatStyle = FlatStyle.Flat;
            btnZoom.FlatAppearance.BorderSize = 0;
            btnZoom.Font = new Font("Segoe UI", 8.0f, FontStyle.Bold);
            btnZoom.Cursor = Cursors.Hand;
            btnZoom.Click += BtnZoom_Click;

            // NÚT THÊM CỘT PARAMETER DỰ ÁN & HỆ ỐNG GIÓ
            btnAddParamCol = new Button();
            btnAddParamCol.Text = "➕ Thêm Cột Parameter";
            btnAddParamCol.Location = new Point(pnlActionToolbar.Width - 280, 6);
            btnAddParamCol.Size = new Size(165, 26);
            btnAddParamCol.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnAddParamCol.BackColor = Color.FromArgb(14, 116, 144);
            btnAddParamCol.ForeColor = Color.White;
            btnAddParamCol.FlatStyle = FlatStyle.Flat;
            btnAddParamCol.FlatAppearance.BorderSize = 0;
            btnAddParamCol.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            btnAddParamCol.Cursor = Cursors.Hand;
            btnAddParamCol.Click += BtnAddParamCol_Click;
            toolTip.SetToolTip(btnAddParamCol, "Bổ sung bất kỳ tham số nào của Dự án hoặc Hệ ống gió vào bảng (Comments, Mark, Loại Bảo Ôn...)");

            btnRemoveParamCol = new Button();
            btnRemoveParamCol.Text = "🗑️ Xóa Cột";
            btnRemoveParamCol.Location = new Point(pnlActionToolbar.Width - 105, 6);
            btnRemoveParamCol.Size = new Size(95, 26);
            btnRemoveParamCol.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnRemoveParamCol.BackColor = Color.FromArgb(185, 28, 28);
            btnRemoveParamCol.ForeColor = Color.White;
            btnRemoveParamCol.FlatStyle = FlatStyle.Flat;
            btnRemoveParamCol.FlatAppearance.BorderSize = 0;
            btnRemoveParamCol.Font = new Font("Segoe UI", 8.0f, FontStyle.Bold);
            btnRemoveParamCol.Cursor = Cursors.Hand;
            btnRemoveParamCol.Click += BtnRemoveParamCol_Click;
            toolTip.SetToolTip(btnRemoveParamCol, "Xóa một cột tham số tùy chọn đã thêm khỏi bảng");

            pnlActionToolbar.Controls.Add(chkAutoHighlight);
            pnlActionToolbar.Controls.Add(btnHighlightAll);
            pnlActionToolbar.Controls.Add(btnClearHighlight);
            pnlActionToolbar.Controls.Add(btnZoom);
            pnlActionToolbar.Controls.Add(btnAddParamCol);
            pnlActionToolbar.Controls.Add(btnRemoveParamCol);

            // ================= 4. DATA GRID VIEW TABLE (DOUBLE BUFFERED) =================
            dgvItems = new DataGridView();
            dgvItems.Location = new Point(12, 246);
            dgvItems.Size = new Size(this.ClientSize.Width - 24, this.ClientSize.Height - 365);
            dgvItems.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvItems.BackgroundColor = Color.White;
            dgvItems.AllowUserToAddRows = false;
            dgvItems.RowHeadersVisible = false;
            dgvItems.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvItems.ColumnHeadersHeight = 34;
            dgvItems.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(24, 43, 73);
            dgvItems.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvItems.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            dgvItems.EnableHeadersVisualStyles = false;
            dgvItems.CellEndEdit += DgvItems_CellEndEdit;
            dgvItems.SelectionChanged += DgvItems_SelectionChanged;

            EnableDoubleBuffered(dgvItems);
            BuildGridColumns();

            // ================= 5. SUMMARY BAR =================
            lblSummary = new Label();
            lblSummary.Text = "📊 Đang tính toán dữ liệu khối lượng ống gió...";
            lblSummary.Location = new Point(14, this.ClientSize.Height - 110);
            lblSummary.Size = new Size(this.ClientSize.Width - 28, 24);
            lblSummary.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            lblSummary.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            lblSummary.ForeColor = Color.FromArgb(22, 101, 52);

            // ================= 6. BOTTOM ACTION BAR =================
            pnlBottom = new System.Windows.Forms.Panel();
            pnlBottom.Dock = DockStyle.Bottom;
            pnlBottom.Height = 54;
            pnlBottom.BackColor = Color.FromArgb(241, 245, 249);
            pnlBottom.BorderStyle = BorderStyle.FixedSingle;

            btnPlaceSheet = new Button();
            btnPlaceSheet.Text = "🚀 ĐẶT / CẬP NHẬT VÀO SHEET MONG MUỐN...";
            btnPlaceSheet.Location = new Point(12, 9);
            btnPlaceSheet.Size = new Size(340, 36);
            btnPlaceSheet.BackColor = Color.FromArgb(16, 185, 129);
            btnPlaceSheet.ForeColor = Color.White;
            btnPlaceSheet.FlatStyle = FlatStyle.Flat;
            btnPlaceSheet.FlatAppearance.BorderSize = 0;
            btnPlaceSheet.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnPlaceSheet.Cursor = Cursors.Hand;
            btnPlaceSheet.Click += BtnPlaceSheet_Click;

            btnExportExcel = new Button();
            btnExportExcel.Text = "📊 Xuất Excel (.csv / .xlsx)";
            btnExportExcel.Location = new Point(360, 9);
            btnExportExcel.Size = new Size(180, 36);
            btnExportExcel.BackColor = Color.FromArgb(13, 148, 136);
            btnExportExcel.ForeColor = Color.White;
            btnExportExcel.FlatStyle = FlatStyle.Flat;
            btnExportExcel.FlatAppearance.BorderSize = 0;
            btnExportExcel.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnExportExcel.Cursor = Cursors.Hand;
            btnExportExcel.Click += BtnExportExcel_Click;

            btnCopyClipboard = new Button();
            btnCopyClipboard.Text = "📋 Copy Bảng";
            btnCopyClipboard.Location = new Point(548, 9);
            btnCopyClipboard.Size = new Size(110, 36);
            btnCopyClipboard.BackColor = Color.FromArgb(71, 85, 105);
            btnCopyClipboard.ForeColor = Color.White;
            btnCopyClipboard.FlatStyle = FlatStyle.Flat;
            btnCopyClipboard.FlatAppearance.BorderSize = 0;
            btnCopyClipboard.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnCopyClipboard.Cursor = Cursors.Hand;
            btnCopyClipboard.Click += BtnCopyClipboard_Click;

            btnClose = new Button();
            btnClose.Text = "Đóng (ESC)";
            btnClose.Location = new Point(pnlBottom.Width - 130, 9);
            btnClose.Size = new Size(115, 36);
            btnClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClose.BackColor = Color.White;
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnClose.Cursor = Cursors.Hand;
            btnClose.Click += (s, e) => this.Close();

            pnlBottom.Controls.Add(btnPlaceSheet);
            pnlBottom.Controls.Add(btnExportExcel);
            pnlBottom.Controls.Add(btnCopyClipboard);
            pnlBottom.Controls.Add(btnClose);

            this.Controls.Add(dgvItems);
            this.Controls.Add(lblSummary);
            this.Controls.Add(pnlActionToolbar);
            this.Controls.Add(grpFilter);
            this.Controls.Add(pnlHeader);
            this.Controls.Add(pnlBottom);

            this.ResumeLayout(false);
        }

        private static void EnableDoubleBuffered(System.Windows.Forms.Control control)
        {
            try
            {
                PropertyInfo prop = typeof(System.Windows.Forms.Control).GetProperty("DoubleBuffered", BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null) prop.SetValue(control, true, null);
            }
            catch { }
        }

        private void BuildGridColumns()
        {
            _isUpdatingGrid = true;
            dgvItems.SuspendLayout();
            try
            {
                dgvItems.Columns.Clear();

                // 1. STT
                DataGridViewTextBoxColumn colStt = new DataGridViewTextBoxColumn();
                colStt.Name = "STT";
                colStt.HeaderText = "STT";
                colStt.Width = 50;
                colStt.ReadOnly = true;
                colStt.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                dgvItems.Columns.Add(colStt);

                // 2. Tên cấu kiện
                DataGridViewTextBoxColumn colName = new DataGridViewTextBoxColumn();
                colName.Name = "Name";
                colName.HeaderText = "Tên Cấu Kiện / Phụ Kiện";
                colName.Width = 240;
                colName.ReadOnly = true;
                dgvItems.Columns.Add(colName);

                // 3. Quy cách Size
                DataGridViewTextBoxColumn colSize = new DataGridViewTextBoxColumn();
                colSize.Name = "Size";
                colSize.HeaderText = "Quy Cách (WxH / Ø)";
                colSize.Width = 140;
                colSize.ReadOnly = true;
                colSize.DefaultCellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                dgvItems.Columns.Add(colSize);

                // 4. Chiều dài (m)
                DataGridViewTextBoxColumn colLen = new DataGridViewTextBoxColumn();
                colLen.Name = "Length";
                colLen.HeaderText = "Chiều Dài (m)";
                colLen.Width = 100;
                colLen.ReadOnly = true;
                colLen.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                dgvItems.Columns.Add(colLen);

                // 5. Số Lượng
                DataGridViewTextBoxColumn colQty = new DataGridViewTextBoxColumn();
                colQty.Name = "Quantity";
                colQty.HeaderText = "Số Lượng";
                colQty.Width = 95;
                colQty.ReadOnly = true;
                colQty.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                colQty.DefaultCellStyle.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                colQty.DefaultCellStyle.ForeColor = Color.FromArgb(180, 83, 9);
                dgvItems.Columns.Add(colQty);

                // 6. Diện Tích Tôn m² (TCVN 5687:2010)
                DataGridViewTextBoxColumn colArea = new DataGridViewTextBoxColumn();
                colArea.Name = "Area";
                colArea.HeaderText = "Diện Tích (m²)";
                colArea.Width = 120;
                colArea.ReadOnly = true;
                colArea.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                colArea.DefaultCellStyle.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                colArea.DefaultCellStyle.ForeColor = Color.FromArgb(16, 185, 129);
                dgvItems.Columns.Add(colArea);

                // 7. Hệ Thống
                DataGridViewTextBoxColumn colSys = new DataGridViewTextBoxColumn();
                colSys.Name = "System";
                colSys.HeaderText = "Hệ Thống";
                colSys.Width = 130;
                colSys.ReadOnly = true;
                dgvItems.Columns.Add(colSys);

                // 8. Độ Dày Tôn
                DataGridViewTextBoxColumn colThick = new DataGridViewTextBoxColumn();
                colThick.Name = "Thickness";
                colThick.HeaderText = "Độ Dày Tôn";
                colThick.Width = 95;
                colThick.ReadOnly = true;
                colThick.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                dgvItems.Columns.Add(colThick);

                // 9. Tầng
                DataGridViewTextBoxColumn colLvl = new DataGridViewTextBoxColumn();
                colLvl.Name = "Level";
                colLvl.HeaderText = "Tầng";
                colLvl.Width = 90;
                colLvl.ReadOnly = true;
                dgvItems.Columns.Add(colLvl);

                // 10. Chủng Loại
                DataGridViewTextBoxColumn colCat = new DataGridViewTextBoxColumn();
                colCat.Name = "Category";
                colCat.HeaderText = "Chủng Loại";
                colCat.Width = 115;
                colCat.ReadOnly = true;
                dgvItems.Columns.Add(colCat);

                // 11. CÁC CỘT PARAMETER DỰ ÁN TÙY CHỌN
                if (_customParamColumns != null)
                {
                    foreach (string pName in _customParamColumns)
                    {
                        DataGridViewTextBoxColumn colCustom = new DataGridViewTextBoxColumn();
                        colCustom.Name = "CUST_" + pName;
                        colCustom.HeaderText = pName + " (Custom)";
                        colCustom.Width = 135;
                        colCustom.ReadOnly = false;
                        colCustom.HeaderCell.Style.BackColor = Color.FromArgb(14, 116, 144);
                        colCustom.HeaderCell.Style.ForeColor = Color.White;
                        dgvItems.Columns.Add(colCustom);
                    }
                }
            }
            finally
            {
                dgvItems.ResumeLayout();
                _isUpdatingGrid = false;
            }
        }

        private void LoadDataAndRefreshUi()
        {
            try
            {
                int scope = 0;
                if (radSelection.Checked) scope = 1;
                else if (radAllProject.Checked) scope = 2;

                _allItems = DuctQuantityService.CollectDuctItems(
                    _doc,
                    _activeView,
                    scope,
                    chkDuct.Checked,
                    chkFitting.Checked,
                    chkDamper.Checked,
                    chkDiffuser.Checked,
                    chkFlex.Checked,
                    _customSelectionIds,
                    null, null, null,
                    _customParamColumns,
                    false
                );

                // NẾU Ở KHUNG NHÌN HIỆN HÀNH MÀ BỊ RỖNG 0 ĐỐI TƯỢNG (VD VIEW 3D BỊ ẨN DO BỘ LỌC HOẶC KHÔNG CÓ ỐNG GIÓ):
                // Tự động kiểm tra và chuyển sang nạp "Toàn Bộ Dự Án" để tránh tình trạng Sếp mở lên thấy bảng trắng trơn!
                if (scope == 0 && (_allItems == null || _allItems.Count == 0))
                {
                    var allDocItems = DuctQuantityService.CollectDuctItems(
                        _doc,
                        _activeView,
                        2, // Toàn bộ dự án
                        chkDuct.Checked,
                        chkFitting.Checked,
                        chkDamper.Checked,
                        chkDiffuser.Checked,
                        chkFlex.Checked,
                        null, null, null, null,
                        _customParamColumns,
                        false
                    );

                    if (allDocItems != null && allDocItems.Count > 0)
                    {
                        _isUpdatingScope = true;
                        radAllProject.Checked = true;
                        radCurrentView.Checked = false;
                        _isUpdatingScope = false;
                        _allItems = allDocItems;
                    }
                }

                PopulateFilterDropdowns();
                FilterAndDisplayItems();
            }
            catch (Exception ex)
            {
                TaskDialog.Show("HDTOOL", "Lỗi thu thập dữ liệu ống gió: " + ex.Message);
            }
        }

        private void PopulateFilterDropdowns()
        {
            string oldLvl = cboLevel.SelectedItem as string;
            string oldSys = cboSystem.SelectedItem as string;

            cboLevel.Items.Clear();
            cboLevel.Items.Add("Tất Cả");
            var levels = _allItems.Select(x => x.LocationByLevel).Where(x => !string.IsNullOrEmpty(x)).Distinct().OrderBy(x => x);
            foreach (var lvl in levels) cboLevel.Items.Add(lvl);

            cboSystem.Items.Clear();
            cboSystem.Items.Add("Tất Cả");
            var systems = _allItems.Select(x => x.SystemType).Where(x => !string.IsNullOrEmpty(x)).Distinct().OrderBy(x => x);
            foreach (var sys in systems) cboSystem.Items.Add(sys);

            if (!string.IsNullOrEmpty(oldLvl) && cboLevel.Items.Contains(oldLvl)) cboLevel.SelectedItem = oldLvl;
            else cboLevel.SelectedIndex = 0;

            if (!string.IsNullOrEmpty(oldSys) && cboSystem.Items.Contains(oldSys)) cboSystem.SelectedItem = oldSys;
            else cboSystem.SelectedIndex = 0;
        }

        private void FilterAndDisplayItems()
        {
            string selectedLevel = cboLevel.SelectedItem as string;
            string selectedSystem = cboSystem.SelectedItem as string;
            string kw = (txtSearch.ForeColor != Color.Gray) ? txtSearch.Text.Trim().ToLowerInvariant() : "";

            IEnumerable<DuctQuantityItem> filtered = _allItems;

            if (!string.IsNullOrEmpty(selectedLevel) && selectedLevel != "Tất Cả")
            {
                filtered = filtered.Where(x => string.Equals(x.LocationByLevel, selectedLevel, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(selectedSystem) && selectedSystem != "Tất Cả")
            {
                filtered = filtered.Where(x => string.Equals(x.SystemType, selectedSystem, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(kw))
            {
                filtered = filtered.Where(x =>
                    (x.MergedName != null && x.MergedName.ToLowerInvariant().Contains(kw)) ||
                    (x.Size != null && x.Size.ToLowerInvariant().Contains(kw)) ||
                    (x.SystemType != null && x.SystemType.ToLowerInvariant().Contains(kw)) ||
                    (x.LocationByLevel != null && x.LocationByLevel.ToLowerInvariant().Contains(kw))
                );
            }

            List<DuctQuantityItem> displayList = filtered.ToList();

            _isUpdatingGrid = true;
            dgvItems.SuspendLayout();
            try
            {
                dgvItems.Rows.Clear();
                double totalAreaM2 = 0;
                double totalLengthM = 0;
                int totalCount = 0;
                int ductSections = 0;
                int fittingsCount = 0;

                for (int i = 0; i < displayList.Count; i++)
                {
                    DuctQuantityItem it = displayList[i];
                    it.STT = i + 1;

                    totalAreaM2 += it.TotalAreaM2;
                    totalLengthM += it.TotalLengthM;
                    totalCount += it.Count;

                    if (it.CategoryKind == "Ống Gió Thẳng") ductSections += it.Count;
                    else fittingsCount += it.Count;

                    List<object> rowValues = new List<object>
                    {
                        it.STT,
                        it.MergedName,
                        it.Size,
                        it.TotalLengthM > 0 ? it.TotalLengthM.ToString("0.00") : "-",
                        string.Format("{0} {1}", it.Count, it.Unit),
                        it.TotalAreaM2 > 0 ? it.TotalAreaM2.ToString("0.00") : "-",
                        it.SystemType,
                        it.Thickness,
                        it.LocationByLevel,
                        it.CategoryKind
                    };

                    if (_customParamColumns != null)
                    {
                        foreach (string cp in _customParamColumns)
                        {
                            string val = "";
                            if (it.CustomParameters != null && it.CustomParameters.ContainsKey(cp))
                            {
                                val = it.CustomParameters[cp];
                            }
                            rowValues.Add(val);
                        }
                    }

                    int rowIdx = dgvItems.Rows.Add(rowValues.ToArray());
                    dgvItems.Rows[rowIdx].Tag = it;
                }

                lblSummary.Text = string.Format(
                    "📊 TỔNG CỘNG DIỆN TÍCH TÔN: {0:0.00} m² tôn mạ kẽm  |  {1:0.00} mét dài ống ({2} đoạn)  |  {3} phụ kiện & van/miệng gió  |  {4} dòng thống kê",
                    totalAreaM2, totalLengthM, ductSections, fittingsCount, displayList.Count
                );
            }
            finally
            {
                dgvItems.ResumeLayout();
                _isUpdatingGrid = false;
            }
        }

        // ================= VÁ LỖI QUÉT VÙNG ỐNG GIÓ CỰC BỘ SIÊU MƯỢT KHÔNG LAG =================
        private void BtnPickArea_Click(object sender, EventArgs e)
        {
            this.RequestedPickMode = DuctPickMode.NewArea;
            this.DialogResult = DialogResult.Retry;
            this.Close();
        }

        private void BtnPickAppendArea_Click(object sender, EventArgs e)
        {
            this.RequestedPickMode = DuctPickMode.AppendArea;
            this.DialogResult = DialogResult.Retry;
            this.Close();
        }

        public static List<ElementId> CollectElementIdsFromTargetView(Document doc, Autodesk.Revit.DB.View targetV)
        {
            List<ElementId> ids = new List<ElementId>();
            if (doc == null || targetV == null) return ids;

            BuiltInCategory[] cats = new BuiltInCategory[]
            {
                BuiltInCategory.OST_DuctCurves,
                BuiltInCategory.OST_DuctFitting,
                BuiltInCategory.OST_DuctAccessory,
                BuiltInCategory.OST_DuctTerminal,
                BuiltInCategory.OST_FlexDuctCurves
            };

            foreach (BuiltInCategory bic in cats)
            {
                try
                {
                    var col = new FilteredElementCollector(doc, targetV.Id)
                        .OfCategory(bic)
                        .WhereElementIsNotElementType()
                        .ToElementIds();
                    ids.AddRange(col);
                }
                catch { }
            }
            return ids;
        }

        // ================= VÁ LỖI HIGHLIGHT: KHÔNG MỞ TRANSACTION LẶP GÂY ĐƠ MÁY =================
        private void DgvItems_SelectionChanged(object sender, EventArgs e)
        {
            if (_isUpdatingGrid || !chkAutoHighlight.Checked || dgvItems.SelectedRows.Count == 0) return;

            List<ElementId> targetIds = new List<ElementId>();
            foreach (DataGridViewRow row in dgvItems.SelectedRows)
            {
                DuctQuantityItem it = row.Tag as DuctQuantityItem;
                if (it != null && it.ElementIds != null) targetIds.AddRange(it.ElementIds);
            }

            if (targetIds.Count > 0)
            {
                // HIGHLIGHT BẰNG SELECTION THUẦN REVIT: Tức thì (0.001s), mượt mà 60 FPS, KHÔNG ĐƠ MÁY!
                try
                {
                    _uidoc.Selection.SetElementIds(targetIds);
                }
                catch { }
            }
        }

        private void BtnHighlightAll_Click(object sender, EventArgs e)
        {
            List<ElementId> allIds = _allItems.SelectMany(x => x.ElementIds).Distinct().ToList();
            if (allIds.Count > 0)
            {
                // Chọn toàn bộ đối tượng trên Revit
                _uidoc.Selection.SetElementIds(allIds);

                // Đồng thời tô màu cam nổi bật một lần duy nhất
                DuctQuantityService.HighlightElements(_uidoc, allIds, new Autodesk.Revit.DB.Color(245, 158, 11), 6, true);
                _lastHighlightedIds = new HashSet<ElementId>(allIds);
            }
        }

        private void BtnClearHighlight_Click(object sender, EventArgs e)
        {
            try
            {
                _uidoc.Selection.SetElementIds(new List<ElementId>());
                if (_lastHighlightedIds.Count > 0)
                {
                    DuctQuantityService.ClearHighlight(_uidoc, _lastHighlightedIds);
                    _lastHighlightedIds.Clear();
                }
            }
            catch { }
        }

        private void BtnZoom_Click(object sender, EventArgs e)
        {
            if (dgvItems.SelectedRows.Count == 0) return;
            List<ElementId> targetIds = new List<ElementId>();
            foreach (DataGridViewRow row in dgvItems.SelectedRows)
            {
                DuctQuantityItem it = row.Tag as DuctQuantityItem;
                if (it != null && it.ElementIds != null) targetIds.AddRange(it.ElementIds);
            }
            if (targetIds.Count > 0)
            {
                _uidoc.Selection.SetElementIds(targetIds);
                _uidoc.ShowElements(targetIds);
            }
        }

        private void DgvItems_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            DataGridViewColumn col = dgvItems.Columns[e.ColumnIndex];
            if (!col.Name.StartsWith("CUST_")) return;

            string paramName = col.Name.Substring(5);
            DataGridViewRow row = dgvItems.Rows[e.RowIndex];
            DuctQuantityItem it = row.Tag as DuctQuantityItem;
            if (it == null) return;

            string newVal = row.Cells[e.ColumnIndex].Value as string ?? "";
            it.CustomParameters[paramName] = newVal;

            try
            {
                using (Transaction t = new Transaction(_doc, "HDTOOL - Cập nhật tham số " + paramName))
                {
                    t.Start();
                    foreach (ElementId id in it.ElementIds)
                    {
                        Element el = _doc.GetElement(id);
                        if (el == null) continue;
                        Parameter p = el.LookupParameter(paramName);
                        if (p != null && !p.IsReadOnly)
                        {
                            if (p.StorageType == StorageType.String)
                            {
                                p.Set(newVal);
                            }
                            else if (p.StorageType == StorageType.Double)
                            {
                                double dVal;
                                if (double.TryParse(newVal, out dVal)) p.Set(dVal);
                            }
                            else if (p.StorageType == StorageType.Integer)
                            {
                                int iVal;
                                if (int.TryParse(newVal, out iVal)) p.Set(iVal);
                            }
                        }
                    }
                    t.Commit();
                }
            }
            catch { }
        }

        private void BtnAddParamCol_Click(object sender, EventArgs e)
        {
            List<ElementId> sampleIds = _allItems.SelectMany(x => x.ElementIds).Take(30).ToList();
            List<string> availableParams = DuctQuantityService.GetAvailableDuctProjectParameters(_doc, sampleIds);

            using (AddDuctProjectParameterDialog dlg = new AddDuctProjectParameterDialog(availableParams, _customParamColumns))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrEmpty(dlg.SelectedParamName))
                {
                    string pName = dlg.SelectedParamName.Trim();
                    if (!_customParamColumns.Contains(pName))
                    {
                        _customParamColumns.Add(pName);

                        try
                        {
                            MepUserSettings.Instance.DuctCustomParamColumns = new List<string>(_customParamColumns);
                            MepUserSettings.Instance.Save();
                        }
                        catch { }

                        BuildGridColumns();
                        LoadDataAndRefreshUi();

                        TaskDialog.Show("HDTOOL", string.Format("Đã bổ sung thành công cột Parameter [{0}] vào bảng thống kê khối lượng ống gió!", pName));
                    }
                }
            }
        }

        private void BtnRemoveParamCol_Click(object sender, EventArgs e)
        {
            if (_customParamColumns == null || _customParamColumns.Count == 0)
            {
                TaskDialog.Show("HDTOOL", "Hiện không có cột Parameter tùy chọn nào để xóa.");
                return;
            }

            string lastCol = _customParamColumns.Last();
            _customParamColumns.RemoveAt(_customParamColumns.Count - 1);

            try
            {
                MepUserSettings.Instance.DuctCustomParamColumns = new List<string>(_customParamColumns);
                MepUserSettings.Instance.Save();
            }
            catch { }

            BuildGridColumns();
            LoadDataAndRefreshUi();

            TaskDialog.Show("HDTOOL", string.Format("Đã xóa cột Parameter tùy chọn [{0}] khỏi bảng.", lastCol));
        }

        private void BtnPlaceSheet_Click(object sender, EventArgs e)
        {
            if (_allItems == null || _allItems.Count == 0)
            {
                TaskDialog.Show("HDTOOL", "Chua co du lieu khoi luong ong gio de dat vao Sheet.\nVui long quet du lieu truoc.");
                return;
            }

            using (DuctPlaceSheetForm form = new DuctPlaceSheetForm(_doc, _uidoc, _allItems, _customParamColumns, _activeView))
            {
                form.ShowDialog(this);
            }
        }

        private void BtnExportExcel_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "File Excel CSV (*.csv)|*.csv|All Files (*.*)|*.*";
            sfd.FileName = string.Format("HDTOOL_Bang_Khoi_Luong_Ong_Gio_HVAC_{0}.csv", DateTime.Now.ToString("yyyyMMdd_HHmmss"));

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    string outPath = DuctQuantityService.ExportToCsv(_allItems, _customParamColumns, sfd.FileName);
                    if (File.Exists(outPath))
                    {
                        TaskDialog.Show("HDTOOL - Xuất Excel Thành Công", "Đã xuất bảng khối lượng ống gió thành công ra file:\n" + outPath);
                        System.Diagnostics.Process.Start(outPath);
                    }
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("HDTOOL", "Lỗi xuất file: " + ex.Message);
                }
            }
        }

        private void BtnCopyClipboard_Click(object sender, EventArgs e)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("STT\tTên Cấu Kiện\tQuy Cách\tChiều Dài (m)\tSố Lượng\tDiện Tích (m²)\tHệ Thống\tĐộ Dày\tTầng\tChủng Loại");
                foreach (DataGridViewRow row in dgvItems.Rows)
                {
                    DuctQuantityItem it = row.Tag as DuctQuantityItem;
                    if (it != null)
                    {
                        sb.AppendLine(string.Format("{0}\t{1}\t{2}\t{3:0.00}\t{4} {5}\t{6:0.00}\t{7}\t{8}\t{9}\t{10}",
                            it.STT, it.MergedName, it.Size, it.TotalLengthM, it.Count, it.Unit, it.TotalAreaM2, it.SystemType, it.Thickness, it.LocationByLevel, it.CategoryKind));
                    }
                }
                Clipboard.SetText(sb.ToString());
                TaskDialog.Show("HDTOOL", "Đã sao chép toàn bộ bảng dữ liệu vào bộ nhớ tạm (Clipboard)!");
            }
            catch { }
        }

        private static string TruncateString(string s, int maxLen)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= maxLen) return s;
            return s.Substring(0, maxLen - 2) + "..";
        }
    }

    /// <summary>
    /// Dialog cho phép người dùng lựa chọn bất kỳ Parameter nào của Dự Án hoặc Hệ Thống Ống Gió để thêm vào bảng
    /// </summary>
    public class AddDuctProjectParameterDialog : System.Windows.Forms.Form
    {
        public string SelectedParamName { get; private set; }

        private List<string> _allParams;
        private List<string> _existingCols;
        private ListBox lbParams;
        private TextBox txtSearch;

        public AddDuctProjectParameterDialog(List<string> allAvailableParams, List<string> existingCols)
        {
            _allParams = allAvailableParams ?? new List<string>();
            _existingCols = existingCols ?? new List<string>();
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "HDTOOL - CHỌN THAM SỐ DỰ ÁN & HỆ ỐNG GIÓ ĐỂ THÊM VÀO BẢNG";
            this.Size = new Size(520, 520);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(248, 250, 252);
            this.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

            Label lblPrompt = new Label();
            lblPrompt.Text = "Chọn một tham số từ Dự Án, Shared Parameters hoặc Family Parameters để thêm cột vào bảng:";
            lblPrompt.Location = new Point(14, 12);
            lblPrompt.Size = new Size(480, 32);
            lblPrompt.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            lblPrompt.ForeColor = Color.FromArgb(15, 23, 42);

            Label lblSearch = new Label();
            lblSearch.Text = "🔍 Tìm kiếm nhanh parameter:";
            lblSearch.Location = new Point(14, 50);
            lblSearch.Size = new Size(200, 18);
            lblSearch.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);

            txtSearch = new TextBox();
            txtSearch.Location = new Point(14, 72);
            txtSearch.Size = new Size(475, 24);
            txtSearch.TextChanged += (s, e) => FilterList();

            Label lblList = new Label();
            lblList.Text = "Danh sách tham số khả dụng:";
            lblList.Location = new Point(14, 105);
            lblList.Size = new Size(200, 18);

            lbParams = new ListBox();
            lbParams.Location = new Point(14, 128);
            lbParams.Size = new Size(475, 280);
            lbParams.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
            lbParams.DoubleClick += (s, e) => ConfirmSelection();

            FilterList();

            Button btnOk = new Button();
            btnOk.Text = "➕ Thêm Cột Này";
            btnOk.Location = new Point(230, 425);
            btnOk.Size = new Size(140, 34);
            btnOk.BackColor = Color.FromArgb(14, 116, 144);
            btnOk.ForeColor = Color.White;
            btnOk.FlatStyle = FlatStyle.Flat;
            btnOk.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnOk.Cursor = Cursors.Hand;
            btnOk.Click += (s, e) => ConfirmSelection();

            Button btnCancel = new Button();
            btnCancel.Text = "Hủy Bỏ";
            btnCancel.Location = new Point(380, 425);
            btnCancel.Size = new Size(110, 34);
            btnCancel.BackColor = Color.White;
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
            btnCancel.Cursor = Cursors.Hand;
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            this.Controls.Add(lblPrompt);
            this.Controls.Add(lblSearch);
            this.Controls.Add(txtSearch);
            this.Controls.Add(lblList);
            this.Controls.Add(lbParams);
            this.Controls.Add(btnOk);
            this.Controls.Add(btnCancel);
        }

        private void FilterList()
        {
            lbParams.Items.Clear();
            string kw = txtSearch != null ? txtSearch.Text.Trim().ToLowerInvariant() : "";

            foreach (string p in _allParams)
            {
                if (_existingCols.Contains(p)) continue;
                if (string.IsNullOrEmpty(kw) || p.ToLowerInvariant().Contains(kw))
                {
                    lbParams.Items.Add(p);
                }
            }

            if (lbParams.Items.Count > 0) lbParams.SelectedIndex = 0;
        }

        private void ConfirmSelection()
        {
            if (lbParams.SelectedItem != null)
            {
                SelectedParamName = lbParams.SelectedItem.ToString();
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }
    }

    /// <summary>
    /// Bộ lọc quét chuột siêu tốc O(1) chỉ nhận phần tử HVAC
    /// An toàn 100% trong cả View 2D lẫn View 3D, khắc phục triệt để lỗi đơ máy chuột
    /// </summary>
    public class DuctSelectionFilter : ISelectionFilter
    {
        private readonly Document _filterDoc;
        private static readonly HashSet<int> _allowedCatIds = new HashSet<int>
        {
            (int)BuiltInCategory.OST_DuctCurves,
            (int)BuiltInCategory.OST_DuctFitting,
            (int)BuiltInCategory.OST_DuctAccessory,
            (int)BuiltInCategory.OST_DuctTerminal,
            (int)BuiltInCategory.OST_FlexDuctCurves
        };

        public DuctSelectionFilter(Document doc = null)
        {
            _filterDoc = doc;
        }

        public bool AllowElement(Element elem)
        {
            if (elem == null) return false;
            try
            {
                Category cat = elem.Category;
                if (cat == null) return false;
                return _allowedCatIds.Contains(cat.Id.IntegerValue);
            }
            catch
            {
                return false;
            }
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            if (reference == null) return false;
            if (_filterDoc != null && reference.ElementId != ElementId.InvalidElementId)
            {
                Element el = _filterDoc.GetElement(reference.ElementId);
                return AllowElement(el);
            }
            return true;
        }
    }

    /// <summary>
    /// Há»™p thoáº¡i chá»n Trang in (Sheet) vÃ  tá»± Ä‘á»™ng Ä‘áº·t Báº£ng Thá»‘ng KÃª Khá»‘i LÆ°á»£ng á»ng GiÃ³ Chuáº©n HadoMEE
    /// </summary>
    public class DuctPlaceSheetForm : System.Windows.Forms.Form
    {
        private Document _doc;
        private UIDocument _uidoc;
        private List<DuctQuantityItem> _items;
        private List<string> _customParamCols;
        private Autodesk.Revit.DB.View _activeView;

        private System.Windows.Forms.Panel pnlHeader;
        private Label lblHeaderTitle;
        private Label lblHeaderDesc;
        private TextBox txtTableName;
        private TextBox txtSearchSheet;
        private ListBox lbSheets;
        private RadioButton radAutoCorner;
        private RadioButton radPickPoint;
        private CheckBox chkActivateSheet;
        private Button btnPlaceNow;
        private Button btnCancel;

        private List<ViewSheet> _allSheets = new List<ViewSheet>();

        public DuctPlaceSheetForm(Document doc, UIDocument uidoc, List<DuctQuantityItem> items, List<string> customParamCols, Autodesk.Revit.DB.View activeView)
        {
            _doc = doc;
            _uidoc = uidoc;
            _items = items;
            _customParamCols = customParamCols;
            _activeView = activeView;

            LoadAllSheets();
            InitializeComponent();
        }

        private void LoadAllSheets()
        {
            try
            {
                var col = new FilteredElementCollector(_doc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .Where(s => !s.IsTemplate)
                    .OrderBy(s => s.SheetNumber)
                    .ToList();
                _allSheets = col;
            }
            catch { }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.Text = "HDTOOL â€” Äáº¶T Báº¢NG KHá»I LÆ¯á»¢NG á»NG GIÃ“ VÃ€O SHEET Báº¢N Váº¼";
            this.Size = new Size(580, 560);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(248, 250, 252);
            this.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

            // Header Banner
            pnlHeader = new System.Windows.Forms.Panel();
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = 65;
            pnlHeader.BackColor = Color.FromArgb(24, 43, 73);

            lblHeaderTitle = new Label();
            lblHeaderTitle.Text = "ðŸš€ Äáº¶T Báº¢NG KHá»I LÆ¯á»¢NG á»NG GIÃ“ LÃŠN SHEET";
            lblHeaderTitle.ForeColor = Color.White;
            lblHeaderTitle.Font = new Font("Segoe UI", 11.0f, FontStyle.Bold);
            lblHeaderTitle.Location = new Point(16, 12);
            lblHeaderTitle.AutoSize = true;

            lblHeaderDesc = new Label();
            lblHeaderDesc.Text = "Tá»± Ä‘á»™ng táº¡o Legend View vÃ  Äáº·t Viewport báº£ng khá»‘i lÆ°á»£ng chuáº©n TCVN 5687:2010";
            lblHeaderDesc.ForeColor = Color.FromArgb(203, 213, 225);
            lblHeaderDesc.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblHeaderDesc.Location = new Point(16, 36);
            lblHeaderDesc.AutoSize = true;

            pnlHeader.Controls.Add(lblHeaderTitle);
            pnlHeader.Controls.Add(lblHeaderDesc);

            // TÃªn Báº£ng
            Label lblName = new Label();
            lblName.Text = "TÃªn Báº£ng Khá»‘i LÆ°á»£ng (Hiá»ƒn thá»‹ tiÃªu Ä‘á» vÃ  tÃªn Legend View):";
            lblName.Location = new Point(16, 75);
            lblName.Size = new Size(400, 18);
            lblName.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            lblName.ForeColor = Color.FromArgb(30, 41, 59);

            txtTableName = new TextBox();
            txtTableName.Location = new Point(16, 96);
            txtTableName.Size = new Size(530, 24);
            txtTableName.Text = "Báº¢NG THá»NG KÃŠ KHá»I LÆ¯á»¢NG á»NG GIÃ“ & PHá»¤ KIá»†N HVAC";
            txtTableName.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);

            // TÃ¬m kiáº¿m Sheet
            Label lblSearch = new Label();
            lblSearch.Text = "ðŸ” Chá»n Trang In (Sheet) Cáº§n Äáº·t Báº£ng:";
            lblSearch.Location = new Point(16, 130);
            lblSearch.Size = new Size(300, 18);
            lblSearch.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);

            txtSearchSheet = new TextBox();
            txtSearchSheet.Location = new Point(16, 150);
            txtSearchSheet.Size = new Size(530, 24);
            txtSearchSheet.Text = "";
            txtSearchSheet.TextChanged += (s, e) => FilterSheetsList();

            lbSheets = new ListBox();
            lbSheets.Location = new Point(16, 180);
            lbSheets.Size = new Size(530, 175);
            lbSheets.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
            lbSheets.DoubleClick += (s, e) => ExecutePlaceTable();

            FilterSheetsList();

            // NhÃ³m TÃ¹y Chá»n Äáº·t Vá»‹ TrÃ­
            GroupBox grpOption = new GroupBox();
            grpOption.Text = "Vá»‹ TrÃ­ Äáº·t Báº£ng TrÃªn Báº£n Váº½ (Sheet)";
            grpOption.Location = new Point(16, 365);
            grpOption.Size = new Size(530, 78);
            grpOption.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);

            radAutoCorner = new RadioButton();
            radAutoCorner.Text = "Tá»± Ä‘á»™ng cÄƒn gÃ³c trÃªn bÃªn pháº£i khung tÃªn báº£n váº½ (TitleBlock) â€” Äáº¹p & Chuáº©n";
            radAutoCorner.Checked = true;
            radAutoCorner.Location = new Point(15, 22);
            radAutoCorner.Size = new Size(500, 22);
            radAutoCorner.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            radAutoCorner.Cursor = Cursors.Hand;

            radPickPoint = new RadioButton();
            radPickPoint.Text = "Táº¡m áº©n cá»­a sá»• Ä‘á»ƒ Click chuá»™t chá»n Ä‘iá»ƒm Ä‘áº·t trÃªn Sheet";
            radPickPoint.Location = new Point(15, 48);
            radPickPoint.Size = new Size(500, 22);
            radPickPoint.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            radPickPoint.Cursor = Cursors.Hand;

            grpOption.Controls.Add(radAutoCorner);
            grpOption.Controls.Add(radPickPoint);

            chkActivateSheet = new CheckBox();
            chkActivateSheet.Text = "Tá»± Ä‘á»™ng chuyá»ƒn sang xem Sheet ngay sau khi Ä‘áº·t thÃ nh cÃ´ng";
            chkActivateSheet.Checked = true;
            chkActivateSheet.Location = new Point(16, 452);
            chkActivateSheet.Size = new Size(450, 22);
            chkActivateSheet.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);

            // NÃºt báº¥m
            btnPlaceNow = new Button();
            btnPlaceNow.Text = "ðŸš€ Äáº¶T VÃ€O SHEET NGAY";
            btnPlaceNow.Location = new Point(230, 480);
            btnPlaceNow.Size = new Size(200, 36);
            btnPlaceNow.BackColor = Color.FromArgb(16, 185, 129);
            btnPlaceNow.ForeColor = Color.White;
            btnPlaceNow.FlatStyle = FlatStyle.Flat;
            btnPlaceNow.FlatAppearance.BorderSize = 0;
            btnPlaceNow.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnPlaceNow.Cursor = Cursors.Hand;
            btnPlaceNow.Click += (s, e) => ExecutePlaceTable();

            btnCancel = new Button();
            btnCancel.Text = "Há»§y Bá»";
            btnCancel.Location = new Point(440, 480);
            btnCancel.Size = new Size(106, 36);
            btnCancel.BackColor = Color.White;
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
            btnCancel.Cursor = Cursors.Hand;
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            this.Controls.Add(pnlHeader);
            this.Controls.Add(lblName);
            this.Controls.Add(txtTableName);
            this.Controls.Add(lblSearch);
            this.Controls.Add(txtSearchSheet);
            this.Controls.Add(lbSheets);
            this.Controls.Add(grpOption);
            this.Controls.Add(chkActivateSheet);
            this.Controls.Add(btnPlaceNow);
            this.Controls.Add(btnCancel);

            this.ResumeLayout(false);
        }

        private void FilterSheetsList()
        {
            lbSheets.Items.Clear();
            string kw = txtSearchSheet != null ? txtSearchSheet.Text.Trim().ToLowerInvariant() : "";

            int selectedIdx = -1;
            int counter = 0;

            foreach (ViewSheet sheet in _allSheets)
            {
                string num = sheet.SheetNumber ?? "";
                string name = sheet.Name ?? "";
                string fullDisplay = string.Format("{0} - {1}", num, name);

                if (string.IsNullOrEmpty(kw) || fullDisplay.ToLowerInvariant().Contains(kw))
                {
                    SheetListItem item = new SheetListItem { Sheet = sheet, DisplayText = fullDisplay };
                    lbSheets.Items.Add(item);

                    if (_activeView is ViewSheet && _activeView.Id == sheet.Id)
                    {
                        selectedIdx = counter;
                    }
                    counter++;
                }
            }

            if (selectedIdx >= 0) lbSheets.SelectedIndex = selectedIdx;
            else if (lbSheets.Items.Count > 0) lbSheets.SelectedIndex = 0;
        }

        private void ExecutePlaceTable()
        {
            if (lbSheets.SelectedItem == null)
            {
                TaskDialog.Show("HDTOOL", "Vui lÃ²ng chá»n má»™t Sheet trong danh sÃ¡ch Ä‘á»ƒ Ä‘áº·t báº£ng khá»‘i lÆ°á»£ng.");
                return;
            }

            SheetListItem selItem = lbSheets.SelectedItem as SheetListItem;
            if (selItem == null || selItem.Sheet == null) return;

            ViewSheet targetSheet = selItem.Sheet;
            string tTitle = !string.IsNullOrWhiteSpace(txtTableName.Text)
                ? txtTableName.Text.Trim()
                : "Báº¢NG THá»NG KÃŠ KHá»I LÆ¯á»¢NG á»NG GIÃ“ & PHá»¤ KIá»†N HVAC";

            XYZ pickPoint = null;

            if (radPickPoint.Checked)
            {
                this.Hide();
                if (this.Owner != null) this.Owner.Hide();

                try
                {
                    _uidoc.ActiveView = targetSheet;
                    pickPoint = _uidoc.Selection.PickPoint("HDTOOL: Click chá»n Ä‘iá»ƒm trÃªn Sheet Ä‘á»ƒ Ä‘áº·t gÃ³c trÃªn-trÃ¡i Báº£ng Khá»‘i LÆ°á»£ng á»ng GiÃ³:");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    if (this.Owner != null) { this.Owner.Show(); this.Owner.BringToFront(); }
                    this.Show();
                    this.BringToFront();
                    return;
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("HDTOOL", "Lá»—i chá»n vá»‹ trÃ­: " + ex.Message);
                    if (this.Owner != null) { this.Owner.Show(); this.Owner.BringToFront(); }
                    this.Show();
                    this.BringToFront();
                    return;
                }
                finally
                {
                    if (this.Owner != null) { this.Owner.Show(); this.Owner.BringToFront(); }
                    this.Show();
                    this.BringToFront();
                }
            }

            Autodesk.Revit.DB.View createdView;
            Viewport createdViewport;
            string msg;

            bool success = DuctQuantityService.CreateOrUpdateDuctTableOnSheet(
                _doc,
                tTitle,
                targetSheet,
                pickPoint,
                _items,
                _customParamCols,
                out createdView,
                out createdViewport,
                out msg
            );

            if (success)
            {
                if (chkActivateSheet.Checked)
                {
                    try { _uidoc.ActiveView = targetSheet; } catch { }
                }

                TaskDialog.Show("HDTOOL â€” ThÃ nh CÃ´ng",
                    string.Format("ðŸŽ‰ ÄÃƒ Äáº¶T THÃ€NH CÃ”NG Báº¢NG KHá»I LÆ¯á»¢NG á»NG GIÃ“ VÃ€O SHEET!\n\n" +
                                  "â€¢ Sheet: {0} - {1}\n" +
                                  "â€¢ TÃªn Báº£ng: {2}\n" +
                                  "â€¢ Tá»•ng dÃ²ng: {3} dÃ²ng\n" +
                                  "â€¢ Loáº¡i báº£ng: Legend View (CÃ³ thá»ƒ dÃ¹ng láº¡i trÃªn nhiá»u Sheet tÃ¹y thÃ­ch!)\n\n" +
                                  "{4}",
                                  targetSheet.SheetNumber, targetSheet.Name, tTitle, _items.Count, msg));

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                TaskDialog.Show("HDTOOL â€” Tháº¥t Báº¡i", "KhÃ´ng thá»ƒ táº¡o báº£ng trÃªn Sheet:\n" + msg);
            }
        }

        private class SheetListItem
        {
            public ViewSheet Sheet { get; set; }
            public string DisplayText { get; set; }
            public override string ToString()
            {
                return DisplayText;
            }
        }
    }
}
