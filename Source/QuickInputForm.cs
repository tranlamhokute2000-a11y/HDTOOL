using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;

namespace HDTool
{
    public class QuickInputForm : System.Windows.Forms.Form
    {
        private System.Windows.Forms.Label lblHeader;
        private System.Windows.Forms.Label lblLevelCount;

        private System.Windows.Forms.Label lblLeft;
        private System.Windows.Forms.NumericUpDown numLeft;
        private System.Windows.Forms.Label lblUnitLeft;

        private System.Windows.Forms.Label lblRight;
        private System.Windows.Forms.NumericUpDown numRight;
        private System.Windows.Forms.Label lblUnitRight;

        private System.Windows.Forms.CheckBox chkEqual;
        private System.Windows.Forms.CheckBox chk2D;
        private System.Windows.Forms.CheckBox chkSymbolLeft;
        private System.Windows.Forms.CheckBox chkSymbolRight;

        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;

        public double OffsetLeft { get; private set; }
        public double OffsetRight { get; private set; }
        public bool Make2D { get; private set; }
        public bool ShowSymbolLeft { get; private set; }
        public bool ShowSymbolRight { get; private set; }

        public QuickInputForm(int count, string names, bool isMultiView = false)
        {
            InitializeComponent(count, names, isMultiView);
        }

        private void InitializeComponent(int count, string names, bool isMultiView)
        {
            this.Text = isMultiView ? "Căn Chỉnh Đồng Loạt Đa View (mm)" : "Nhập Khoảng Cách Cho Level";
            this.Size = new System.Drawing.Size(430, 350);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.TopMost = true;
            this.BackColor = System.Drawing.Color.FromArgb(245, 247, 250);
            this.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Regular);

            // Header banner
            System.Windows.Forms.Panel pnlBanner = new System.Windows.Forms.Panel();
            pnlBanner.Dock = DockStyle.Top;
            pnlBanner.Height = 55;
            pnlBanner.BackColor = System.Drawing.Color.FromArgb(24, 43, 73);

            lblHeader = new System.Windows.Forms.Label();
            lblHeader.Text = isMultiView ? "CĂN CHỈNH ĐỒNG LOẠT ĐA VIEW (H12, H14...)" : "NHẬP KHOẢNG CÁCH LEVEL SO VỚI CROP";
            lblHeader.ForeColor = System.Drawing.Color.White;
            lblHeader.Font = new System.Drawing.Font("Segoe UI", 9.5f, FontStyle.Bold);
            lblHeader.Location = new System.Drawing.Point(12, 16);
            lblHeader.AutoSize = true;
            pnlBanner.Controls.Add(lblHeader);

            // Thông tin Level / View đã chọn
            lblLevelCount = new System.Windows.Forms.Label();
            lblLevelCount.Text = isMultiView 
                ? string.Format("Đang áp dụng cho {0} View: {1}", count, names)
                : string.Format("Đã chọn {0} Level: {1}", count, names);
            lblLevelCount.ForeColor = System.Drawing.Color.FromArgb(41, 128, 185);
            lblLevelCount.Font = new System.Drawing.Font("Segoe UI", 8.5f, FontStyle.Bold);
            lblLevelCount.Location = new System.Drawing.Point(15, 65);
            lblLevelCount.Size = new System.Drawing.Size(385, 20);

            // Left
            lblLeft = new System.Windows.Forms.Label();
            lblLeft.Text = "Khoảng cách bên Trái (Left):";
            lblLeft.Location = new System.Drawing.Point(15, 95);
            lblLeft.Size = new System.Drawing.Size(170, 23);

            numLeft = new System.Windows.Forms.NumericUpDown();
            numLeft.Location = new System.Drawing.Point(190, 93);
            numLeft.Size = new System.Drawing.Size(95, 23);
            numLeft.Minimum = -50000;
            numLeft.Maximum = 100000;
            numLeft.Value = 500;
            numLeft.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Bold);
            numLeft.ValueChanged += (s, e) => {
                if (chkEqual.Checked) numRight.Value = numLeft.Value;
            };

            lblUnitLeft = new System.Windows.Forms.Label();
            lblUnitLeft.Text = "mm";
            lblUnitLeft.Location = new System.Drawing.Point(290, 95);
            lblUnitLeft.Size = new System.Drawing.Size(35, 23);

            // Right
            lblRight = new System.Windows.Forms.Label();
            lblRight.Text = "Khoảng cách bên Phải (Right):";
            lblRight.Location = new System.Drawing.Point(15, 130);
            lblRight.Size = new System.Drawing.Size(170, 23);

            numRight = new System.Windows.Forms.NumericUpDown();
            numRight.Location = new System.Drawing.Point(190, 128);
            numRight.Size = new System.Drawing.Size(95, 23);
            numRight.Minimum = -50000;
            numRight.Maximum = 100000;
            numRight.Value = 1500;
            numRight.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Bold);

            lblUnitRight = new System.Windows.Forms.Label();
            lblUnitRight.Text = "mm";
            lblUnitRight.Location = new System.Drawing.Point(290, 130);
            lblUnitRight.Size = new System.Drawing.Size(35, 23);

            // Checkboxes
            chkEqual = new System.Windows.Forms.CheckBox();
            chkEqual.Text = "Khóa 2 bên bằng nhau (Left = Right)";
            chkEqual.Location = new System.Drawing.Point(18, 163);
            chkEqual.Size = new System.Drawing.Size(350, 22);
            chkEqual.CheckedChanged += (s, e) => {
                if (chkEqual.Checked)
                {
                    numRight.Value = numLeft.Value;
                    numRight.Enabled = false;
                }
                else
                {
                    numRight.Enabled = true;
                }
            };

            chk2D = new System.Windows.Forms.CheckBox();
            chk2D.Text = "Chế độ 2D View-Specific (Chỉ ảnh hưởng View này)";
            chk2D.Checked = true;
            chk2D.ForeColor = System.Drawing.Color.FromArgb(39, 174, 96);
            chk2D.Location = new System.Drawing.Point(18, 185);
            chk2D.Size = new System.Drawing.Size(350, 22);

            // Ký hiệu đầu tầng (Symbols / Bubbles)
            chkSymbolLeft = new System.Windows.Forms.CheckBox();
            chkSymbolLeft.Text = "Bật Symbol Trái";
            chkSymbolLeft.Checked = false;
            chkSymbolLeft.Location = new System.Drawing.Point(18, 212);
            chkSymbolLeft.Size = new System.Drawing.Size(150, 22);

            chkSymbolRight = new System.Windows.Forms.CheckBox();
            chkSymbolRight.Text = "Bật Symbol Phải";
            chkSymbolRight.Checked = true;
            chkSymbolRight.Location = new System.Drawing.Point(190, 212);
            chkSymbolRight.Size = new System.Drawing.Size(150, 22);

            // Buttons
            btnOK = new System.Windows.Forms.Button();
            btnOK.Text = "XÁC NHẬN";
            btnOK.BackColor = System.Drawing.Color.FromArgb(39, 174, 96);
            btnOK.ForeColor = System.Drawing.Color.White;
            btnOK.FlatStyle = FlatStyle.Flat;
            btnOK.Font = new System.Drawing.Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnOK.Location = new System.Drawing.Point(180, 255);
            btnOK.Size = new System.Drawing.Size(110, 32);
            btnOK.Click += (s, e) => {
                this.OffsetLeft = (double)numLeft.Value;
                this.OffsetRight = (double)numRight.Value;
                this.Make2D = chk2D.Checked;
                this.ShowSymbolLeft = chkSymbolLeft.Checked;
                this.ShowSymbolRight = chkSymbolRight.Checked;
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            btnCancel = new System.Windows.Forms.Button();
            btnCancel.Text = "HỦY BỎ";
            btnCancel.Location = new System.Drawing.Point(300, 255);
            btnCancel.Size = new System.Drawing.Size(95, 32);
            btnCancel.Click += (s, e) => {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };

            this.Controls.Add(pnlBanner);
            this.Controls.Add(lblLevelCount);
            this.Controls.Add(lblLeft);
            this.Controls.Add(numLeft);
            this.Controls.Add(lblUnitLeft);
            this.Controls.Add(lblRight);
            this.Controls.Add(numRight);
            this.Controls.Add(lblUnitRight);
            this.Controls.Add(chkEqual);
            this.Controls.Add(chk2D);
            this.Controls.Add(chkSymbolLeft);
            this.Controls.Add(chkSymbolRight);
            this.Controls.Add(btnOK);
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;
        }
    }
}
