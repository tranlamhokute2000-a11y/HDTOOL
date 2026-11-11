using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Color = System.Drawing.Color;
using Font = System.Drawing.Font;
using Point = System.Drawing.Point;

namespace HDTool
{
    public class SetGroupTitleForm : Form
    {
        private Label lblDesc;
        private ComboBox cmbDisciplineName;
        private Button btnOk;
        private Button btnClear;
        private Button btnCancel;

        public string SelectedGroupTitle { get; private set; }
        public event Action<string> GroupTitleConfirmed;

        public SetGroupTitleForm(int selectedCount, IEnumerable<string> existingDisciplines, string currentDiscipline)
        {
            SelectedGroupTitle = string.Empty;
            InitializeComponent(selectedCount, existingDisciplines, currentDiscipline);
        }

        private void InitializeComponent(int selectedCount, IEnumerable<string> existingDisciplines, string currentDiscipline)
        {
            this.SuspendLayout();

            this.Text = "HDTOOL - Gom / Đặt Tên Bộ Môn Cho Bản Vẽ";
            this.Size = new Size(500, 230);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.TopMost = true;
            this.BackColor = Color.FromArgb(245, 247, 250);
            this.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

            lblDesc = new Label();
            lblDesc.Text = string.Format("Nhập tên Bộ Môn cho {0} bản vẽ đã chọn:\n(Bạn hoàn toàn tự do đặt tên theo ý muốn, ví dụ: ĐIỀU HÒA, ĐIỆN, CẤP THOÁT NƯỚC...)", selectedCount);
            lblDesc.Location = new Point(16, 16);
            lblDesc.Size = new Size(450, 42);
            lblDesc.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            lblDesc.ForeColor = Color.FromArgb(24, 43, 73);

            cmbDisciplineName = new ComboBox();
            cmbDisciplineName.Location = new Point(18, 68);
            cmbDisciplineName.Size = new Size(448, 26);
            cmbDisciplineName.Font = new Font("Segoe UI", 10.0f, FontStyle.Regular);
            cmbDisciplineName.DropDownStyle = ComboBoxStyle.DropDown;

            // Nạp các bộ môn đã có trong dự án
            if (existingDisciplines != null)
            {
                foreach (string d in existingDisciplines)
                {
                    if (!string.IsNullOrEmpty(d) && !cmbDisciplineName.Items.Contains(d))
                    {
                        cmbDisciplineName.Items.Add(d);
                    }
                }
            }

            // Gợi ý tham khảo một số bộ môn kỹ thuật phổ biến (không bắt buộc)
            string[] commonSuggestions = new string[]
            {
                "BẢN VẼ ĐIỀU HÒA",
                "BẢN VẼ ĐIỆN",
                "BẢN VẼ CẤP THOÁT NƯỚC",
                "BẢN VẼ PHÒNG CHÁY CHỮA CHÁY",
                "BẢN VẼ KIẾN TRÚC",
                "BẢN VẼ KẾT CẤU"
            };
            foreach (string s in commonSuggestions)
            {
                if (!cmbDisciplineName.Items.Contains(s)) cmbDisciplineName.Items.Add(s);
            }

            if (!string.IsNullOrEmpty(currentDiscipline))
            {
                cmbDisciplineName.Text = currentDiscipline;
            }
            else
            {
                cmbDisciplineName.Text = string.Empty;
            }

            btnOk = new Button();
            btnOk.Text = "✔   ÁP DỤNG BỘ MÔN";
            btnOk.Location = new Point(110, 125);
            btnOk.Size = new Size(175, 40);
            btnOk.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            btnOk.BackColor = Color.FromArgb(39, 174, 96); // Green
            btnOk.ForeColor = Color.White;
            btnOk.FlatStyle = FlatStyle.Flat;
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Cursor = Cursors.Hand;
            btnOk.Click += BtnOk_Click;

            btnClear = new Button();
            btnClear.Text = "Bỏ Bộ Môn";
            btnClear.Location = new Point(292, 125);
            btnClear.Size = new Size(95, 40);
            btnClear.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
            btnClear.BackColor = Color.FromArgb(236, 240, 241);
            btnClear.ForeColor = Color.FromArgb(192, 57, 43); // Red text
            btnClear.FlatStyle = FlatStyle.Flat;
            btnClear.Cursor = Cursors.Hand;
            btnClear.Click += (s, e) =>
            {
                SelectedGroupTitle = string.Empty;
                if (GroupTitleConfirmed != null) GroupTitleConfirmed(SelectedGroupTitle);
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            btnCancel = new Button();
            btnCancel.Text = "Hủy";
            btnCancel.Location = new Point(393, 125);
            btnCancel.Size = new Size(75, 40);
            btnCancel.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
            btnCancel.BackColor = Color.FromArgb(236, 240, 241);
            btnCancel.ForeColor = Color.FromArgb(44, 62, 80);
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.Cursor = Cursors.Hand;
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            this.Controls.Add(lblDesc);
            this.Controls.Add(cmbDisciplineName);
            this.Controls.Add(btnOk);
            this.Controls.Add(btnClear);
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;

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

        private void BtnOk_Click(object sender, EventArgs e)
        {
            SelectedGroupTitle = cmbDisciplineName.Text.Trim();
            if (GroupTitleConfirmed != null) GroupTitleConfirmed(SelectedGroupTitle);
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
