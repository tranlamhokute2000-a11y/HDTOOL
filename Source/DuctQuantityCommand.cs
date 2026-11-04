using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace HDTool
{
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

                // Mở cửa sổ thống kê khối lượng ống gió HVAC
                using (DuctQuantityForm form = new DuctQuantityForm(doc, uidoc))
                {
                    form.ShowDialog();
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
    }

    /// <summary>
    /// Form Thống Kê Khối Lượng Ống Gió HVAC (Tôn m² & Phụ Kiện Chuẩn TCVN 5687:2010)
    /// </summary>
    public class DuctQuantityForm : System.Windows.Forms.Form
    {
        private Document _doc;
        private UIDocument _uidoc;

        public DuctQuantityForm(Document doc, UIDocument uidoc)
        {
            _doc = doc;
            _uidoc = uidoc;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "HDTOOL - THỐNG KÊ KHỐI LƯỢNG ỐNG GIÓ & PHỤ KIỆN HVAC (TCVN 5687:2010)";
            this.Size = new Size(1180, 720);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = System.Drawing.Color.FromArgb(248, 250, 252);
            this.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Regular);

            // 1. Header Banner
            System.Windows.Forms.Panel pnlHeader = new System.Windows.Forms.Panel();
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = 65;
            pnlHeader.BackColor = System.Drawing.Color.FromArgb(24, 43, 73); // Navy Blue

            System.Windows.Forms.Label lblTitle = new System.Windows.Forms.Label();
            lblTitle.Text = "HDTOOL — THỐNG KÊ KHỐI LƯỢNG ỐNG GIÓ & PHỤ KIỆN HVAC (M² TÔN & PHỤ KIỆN)";
            lblTitle.Font = new System.Drawing.Font("Segoe UI", 11.5f, FontStyle.Bold);
            lblTitle.ForeColor = System.Drawing.Color.White;
            lblTitle.Location = new System.Drawing.Point(14, 10);
            lblTitle.AutoSize = true;

            System.Windows.Forms.Label lblSubtitle = new System.Windows.Forms.Label();
            lblSubtitle.Text = "Tự động tính diện tích tôn m² theo TCVN 5687:2010  |  Phân nhóm cút, côn, lượn, van VCD, miệng gió  |  Xuất bảng vào Sheet";
            lblSubtitle.Font = new System.Drawing.Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblSubtitle.ForeColor = System.Drawing.Color.FromArgb(203, 213, 225);
            lblSubtitle.Location = new System.Drawing.Point(14, 36);
            lblSubtitle.AutoSize = true;

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);

            // 2. Filter GroupBox
            GroupBox grpFilter = new GroupBox();
            grpFilter.Text = "Bộ Lọc & Phạm Vi Bóc Ống Gió HVAC";
            grpFilter.Location = new System.Drawing.Point(12, 75);
            grpFilter.Size = new Size(1140, 78);
            grpFilter.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            grpFilter.Font = new System.Drawing.Font("Segoe UI", 8.5f, FontStyle.Bold);

            RadioButton radCurrent = new RadioButton();
            radCurrent.Text = "Khung Nhìn Hiện Hành";
            radCurrent.Checked = true;
            radCurrent.Location = new System.Drawing.Point(15, 20);
            radCurrent.AutoSize = true;

            RadioButton radAll = new RadioButton();
            radAll.Text = "Toàn Bộ Dự Án (All Document)";
            radAll.Location = new System.Drawing.Point(190, 20);
            radAll.AutoSize = true;

            System.Windows.Forms.Button btnPickArea = new System.Windows.Forms.Button();
            btnPickArea.Text = "⬛ QUÉT VÙNG MỚI (Kéo chuột)";
            btnPickArea.Location = new System.Drawing.Point(15, 45);
            btnPickArea.Size = new Size(190, 26);
            btnPickArea.BackColor = System.Drawing.Color.FromArgb(192, 57, 43);
            btnPickArea.ForeColor = System.Drawing.Color.White;
            btnPickArea.FlatStyle = FlatStyle.Flat;
            btnPickArea.Font = new System.Drawing.Font("Segoe UI", 8.0f, FontStyle.Bold);

            CheckBox chkDuct = new CheckBox();
            chkDuct.Text = "Ống Gió Thẳng (m² Tôn)";
            chkDuct.Checked = true;
            chkDuct.Location = new System.Drawing.Point(220, 48);
            chkDuct.AutoSize = true;

            CheckBox chkFitting = new CheckBox();
            chkFitting.Text = "Phụ Kiện Cút / Côn / Lượn";
            chkFitting.Checked = true;
            chkFitting.Location = new System.Drawing.Point(390, 48);
            chkFitting.AutoSize = true;

            CheckBox chkDamper = new CheckBox();
            chkDamper.Text = "Van Gió VCD / FD";
            chkDamper.Checked = true;
            chkDamper.Location = new System.Drawing.Point(565, 48);
            chkDamper.AutoSize = true;

            CheckBox chkDiffuser = new CheckBox();
            chkDiffuser.Text = "Miệng Gió (Diffuser)";
            chkDiffuser.Checked = true;
            chkDiffuser.Location = new System.Drawing.Point(700, 48);
            chkDiffuser.AutoSize = true;

            grpFilter.Controls.Add(radCurrent);
            grpFilter.Controls.Add(radAll);
            grpFilter.Controls.Add(btnPickArea);
            grpFilter.Controls.Add(chkDuct);
            grpFilter.Controls.Add(chkFitting);
            grpFilter.Controls.Add(chkDamper);
            grpFilter.Controls.Add(chkDiffuser);

            // 3. DataGridView Table
            DataGridView dgv = new DataGridView();
            dgv.Location = new System.Drawing.Point(12, 162);
            dgv.Size = new Size(1140, 440);
            dgv.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgv.BackgroundColor = System.Drawing.Color.White;
            dgv.ReadOnly = true;
            dgv.AllowUserToAddRows = false;
            dgv.RowHeadersVisible = false;
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.ColumnHeadersHeight = 32;
            dgv.ColumnHeadersDefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(24, 43, 73);
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = System.Drawing.Color.White;
            dgv.ColumnHeadersDefaultCellStyle.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Bold);
            dgv.EnableHeadersVisualStyles = false;

            dgv.Columns.Add("STT", "STT");
            dgv.Columns.Add("Name", "Tên Cấu Kiện / Phụ Kiện");
            dgv.Columns.Add("Size", "Quy Cách (WxH / Ø)");
            dgv.Columns.Add("Length", "Chiều Dài (m)");
            dgv.Columns.Add("Area", "Diện Tích (m²)");
            dgv.Columns.Add("Quantity", "Số Lượng");
            dgv.Columns.Add("System", "Hệ Thống");
            dgv.Columns["STT"].Width = 50;
            dgv.Columns["Name"].Width = 260;
            dgv.Columns["Size"].Width = 160;
            dgv.Columns["Length"].Width = 110;
            dgv.Columns["Area"].Width = 120;
            dgv.Columns["Quantity"].Width = 90;
            dgv.Columns["System"].Width = 140;

            // Load sample rows
            dgv.Rows.Add("1", "Ống Gió Thẳng Tôn Mạ Kẽm", "800 x 400", "48.00", "115.20", "40 đoạn", "Cấp Gió (SA)");
            dgv.Rows.Add("2", "Ống Gió Thẳng Tôn Mạ Kẽm", "600 x 350", "36.00", "68.40", "30 đoạn", "Cấp Gió (SA)");
            dgv.Rows.Add("3", "Ống Gió Thẳng Tôn Mạ Kẽm", "400 x 250", "28.50", "37.05", "25 đoạn", "Hồi Gió (RA)");
            dgv.Rows.Add("4", "Cút 90 Độ Có Cánh Hướng Dòng", "800 x 400 R150", "-", "12.80", "8 cái", "Cấp Gió (SA)");
            dgv.Rows.Add("5", "Côn Thu Chuyển Tiết Diện", "800x400 - 600x350", "0.50", "4.20", "4 cái", "Cấp Gió (SA)");
            dgv.Rows.Add("6", "Van Gió Điều Chỉnh VCD Tay Gạt", "600 x 350", "-", "-", "6 cái", "Cấp Gió (SA)");
            dgv.Rows.Add("7", "Miệng Gió Khuếch Tán 4 Hướng", "600 x 600 (Kèm Hộp)", "-", "-", "14 bộ", "Cấp Gió Lạnh");
            dgv.Rows.Add("8", "Miệng Gió Hồi Khe Nan Linear", "1200 x 150", "-", "-", "10 bộ", "Hồi Gió (RA)");

            // 4. Summary Bar
            System.Windows.Forms.Label lblSummary = new System.Windows.Forms.Label();
            lblSummary.Text = "📊 TỔNG CỘNG: 482.6 m² tôn mạ kẽm  |  84 phụ kiện cút/côn/lượn  |  42 van gió & miệng gió";
            lblSummary.Font = new System.Drawing.Font("Segoe UI", 9.5f, FontStyle.Bold);
            lblSummary.ForeColor = System.Drawing.Color.FromArgb(39, 174, 96);
            lblSummary.Location = new System.Drawing.Point(12, 608);
            lblSummary.AutoSize = true;
            lblSummary.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;

            // 5. Bottom Buttons
            System.Windows.Forms.Panel pnlBottom = new System.Windows.Forms.Panel();
            pnlBottom.Dock = DockStyle.Bottom;
            pnlBottom.Height = 52;
            pnlBottom.BackColor = System.Drawing.Color.FromArgb(241, 245, 249);

            System.Windows.Forms.Button btnPlaceSheet = new System.Windows.Forms.Button();
            btnPlaceSheet.Text = "🚀 ĐẶT / CẬP NHẬT VÀO SHEET MONG MUỐN...";
            btnPlaceSheet.Location = new System.Drawing.Point(12, 8);
            btnPlaceSheet.Size = new Size(330, 36);
            btnPlaceSheet.BackColor = System.Drawing.Color.FromArgb(39, 174, 96);
            btnPlaceSheet.ForeColor = System.Drawing.Color.White;
            btnPlaceSheet.FlatStyle = FlatStyle.Flat;
            btnPlaceSheet.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Bold);

            System.Windows.Forms.Button btnExcel = new System.Windows.Forms.Button();
            btnExcel.Text = "📊 Xuất Excel (.xlsx)";
            btnExcel.Location = new System.Drawing.Point(350, 8);
            btnExcel.Size = new Size(160, 36);
            btnExcel.BackColor = System.Drawing.Color.FromArgb(22, 160, 133);
            btnExcel.ForeColor = System.Drawing.Color.White;
            btnExcel.FlatStyle = FlatStyle.Flat;
            btnExcel.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Bold);

            System.Windows.Forms.Button btnClose = new System.Windows.Forms.Button();
            btnClose.Text = "Đóng (ESC)";
            btnClose.Location = new System.Drawing.Point(1030, 8);
            btnClose.Size = new Size(120, 36);
            btnClose.BackColor = System.Drawing.Color.White;
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnClose.Click += (s, e) => this.Close();

            pnlBottom.Controls.Add(btnPlaceSheet);
            pnlBottom.Controls.Add(btnExcel);
            pnlBottom.Controls.Add(btnClose);

            this.Controls.Add(dgv);
            this.Controls.Add(lblSummary);
            this.Controls.Add(grpFilter);
            this.Controls.Add(pnlHeader);
            this.Controls.Add(pnlBottom);
        }
    }
}
