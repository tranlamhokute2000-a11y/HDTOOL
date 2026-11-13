using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace HDTool
{
    /// <summary>
    /// Hộp thoại chọn màu chuẩn AutoCAD ACI (AutoCAD Color Index - Select Color Dialog)
    /// Tái hiện 100% giao diện và trải nghiệm chọn màu của AutoCAD:
    /// - Ma trận đầy đủ 256 màu chuẩn ACI (10-249 dải Hue/Lightness, 1-9 Standard Colors, 250-255 Grayscale)
    /// - Nút ByLayer (256) và ByBlock (0)
    /// - Hiển thị Index color và RGB chính xác
    /// - Ô nhập số màu Color: cho phép gõ trực tiếp
    /// - Kế thừa HDToolBaseForm: ấn ESC là đóng/thoát lệnh ngay lập tức!
    /// </summary>
    public class CadColorPickerDialog : HDToolBaseForm
    {
        private int _selectedColorIndex = 7;
        private int _hoveredColorIndex = -1;

        // Controls
        private Label lblAciTitle;
        private Panel pnlPalette;
        private Label lblIndexColor;
        private Label lblRgb;
        private Panel pnlPreview;
        private Label lblColorPrompt;
        private TextBox txtColorInput;
        private Button btnByLayer;
        private Button btnByBlock;
        private Button btnOk;
        private Button btnCancel;
        private Button btnHelp;

        // Ma trận vị trí các ô màu
        private struct ColorBox
        {
            public Rectangle Rect;
            public int ColorIndex;
            public Color Rgb;
        }

        private List<ColorBox> _colorBoxes = new List<ColorBox>();

        public int SelectedColorIndex
        {
            get { return _selectedColorIndex; }
            set
            {
                _selectedColorIndex = (value >= 0 && value <= 256) ? value : 7;
                UpdateUiFromSelection();
            }
        }

        public Color SelectedRgbColor
        {
            get { return GetAciRgb(_selectedColorIndex); }
        }

        public CadColorPickerDialog() : this(7)
        {
        }

        public CadColorPickerDialog(int initialAci)
        {
            this._selectedColorIndex = (initialAci >= 0 && initialAci <= 256) ? initialAci : 7;

            InitializeComponent();
            BuildPaletteMap();
            UpdateUiFromSelection();
        }

        private void InitializeComponent()
        {
            this.Text = "Select Color";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ClientSize = new Size(470, 480);
            this.BackColor = Color.FromArgb(240, 240, 240);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            // Tab Header (Giả lập thanh Tab AutoCAD: Index Color, True Color, Color Books)
            Panel pnlTabContainer = new Panel();
            pnlTabContainer.Location = new Point(14, 10);
            pnlTabContainer.Size = new Size(442, 28);
            pnlTabContainer.Paint += (s, e) =>
            {
                // Tab 1: Index Color (Active)
                Rectangle r1 = new Rectangle(0, 0, 95, 27);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(240, 240, 240)))
                    e.Graphics.FillRectangle(b, r1);
                using (Pen p = new Pen(Color.FromArgb(170, 170, 170)))
                {
                    e.Graphics.DrawLine(p, r1.Left, r1.Top, r1.Right, r1.Top);
                    e.Graphics.DrawLine(p, r1.Left, r1.Top, r1.Left, r1.Bottom);
                    e.Graphics.DrawLine(p, r1.Right, r1.Top, r1.Right, r1.Bottom);
                    // Đường kẻ ngang dưới các tab khác
                    e.Graphics.DrawLine(p, r1.Right, r1.Bottom, pnlTabContainer.Width, r1.Bottom);
                }
                TextRenderer.DrawText(e.Graphics, "Index Color", new Font("Segoe UI", 9F, FontStyle.Bold), r1, Color.FromArgb(30, 41, 59), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                // Tab 2 & 3: True Color & Color Books (Mờ)
                Rectangle r2 = new Rectangle(100, 3, 85, 24);
                TextRenderer.DrawText(e.Graphics, "True Color", new Font("Segoe UI", 8.5F, FontStyle.Regular), r2, Color.FromArgb(120, 120, 120), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                Rectangle r3 = new Rectangle(190, 3, 90, 24);
                TextRenderer.DrawText(e.Graphics, "Color Books", new Font("Segoe UI", 8.5F, FontStyle.Regular), r3, Color.FromArgb(120, 120, 120), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            this.Controls.Add(pnlTabContainer);

            // Label Title
            lblAciTitle = new Label();
            lblAciTitle.Text = "AutoCAD Color Index (ACI):";
            lblAciTitle.Location = new Point(16, 44);
            lblAciTitle.AutoSize = true;
            lblAciTitle.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            lblAciTitle.ForeColor = Color.Black;
            this.Controls.Add(lblAciTitle);

            // Panel Palette vẽ ma trận màu ACI
            pnlPalette = new Panel();
            pnlPalette.Location = new Point(14, 68);
            pnlPalette.Size = new Size(442, 235);
            pnlPalette.BackColor = Color.FromArgb(240, 240, 240);
            pnlPalette.Paint += PnlPalette_Paint;
            pnlPalette.MouseMove += PnlPalette_MouseMove;
            pnlPalette.MouseLeave += (s, e) => { _hoveredColorIndex = -1; pnlPalette.Invalidate(); };
            pnlPalette.MouseClick += PnlPalette_MouseClick;
            this.Controls.Add(pnlPalette);

            // Info Labels
            lblIndexColor = new Label();
            lblIndexColor.Location = new Point(16, 310);
            lblIndexColor.Size = new Size(200, 20);
            lblIndexColor.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            lblIndexColor.ForeColor = Color.FromArgb(15, 23, 42);
            lblIndexColor.Text = "Index color: 7";
            this.Controls.Add(lblIndexColor);

            lblRgb = new Label();
            lblRgb.Location = new Point(220, 310);
            lblRgb.Size = new Size(236, 20);
            lblRgb.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            lblRgb.ForeColor = Color.FromArgb(71, 85, 105);
            lblRgb.Text = "Red, Green, Blue: 255,255,255";
            lblRgb.TextAlign = ContentAlignment.TopRight;
            this.Controls.Add(lblRgb);

            // Nút ByLayer và ByBlock
            btnByLayer = new Button();
            btnByLayer.Text = "ByLayer";
            btnByLayer.Location = new Point(285, 336);
            btnByLayer.Size = new Size(80, 26);
            btnByLayer.UseVisualStyleBackColor = true;
            btnByLayer.Click += (s, e) => { SelectedColorIndex = 256; };
            this.Controls.Add(btnByLayer);

            btnByBlock = new Button();
            btnByBlock.Text = "ByBlock";
            btnByBlock.Location = new Point(372, 336);
            btnByBlock.Size = new Size(84, 26);
            btnByBlock.UseVisualStyleBackColor = true;
            btnByBlock.Click += (s, e) => { SelectedColorIndex = 0; };
            this.Controls.Add(btnByBlock);

            // Color Swatch Preview
            pnlPreview = new Panel();
            pnlPreview.Location = new Point(372, 372);
            pnlPreview.Size = new Size(84, 46);
            pnlPreview.Paint += PnlPreview_Paint;
            this.Controls.Add(pnlPreview);

            // Ô nhập Color:
            lblColorPrompt = new Label();
            lblColorPrompt.Text = "Color:";
            lblColorPrompt.Location = new Point(16, 378);
            lblColorPrompt.AutoSize = true;
            lblColorPrompt.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.Controls.Add(lblColorPrompt);

            txtColorInput = new TextBox();
            txtColorInput.Location = new Point(18, 398);
            txtColorInput.Size = new Size(130, 23);
            txtColorInput.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            txtColorInput.TextChanged += TxtColorInput_TextChanged;
            this.Controls.Add(txtColorInput);

            // Buttons: OK, Cancel, Help
            btnOk = new Button();
            btnOk.Text = "OK";
            btnOk.Location = new Point(170, 436);
            btnOk.Size = new Size(88, 30);
            btnOk.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnOk.UseVisualStyleBackColor = true;
            btnOk.Click += (s, e) =>
            {
                this.DialogResult = DialogResult.OK;
                this.Close();
            };
            this.Controls.Add(btnOk);

            btnCancel = new Button();
            btnCancel.Text = "Cancel";
            btnCancel.Location = new Point(266, 436);
            btnCancel.Size = new Size(88, 30);
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += (s, e) =>
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };
            this.Controls.Add(btnCancel);

            btnHelp = new Button();
            btnHelp.Text = "Help";
            btnHelp.Location = new Point(362, 436);
            btnHelp.Size = new Size(94, 30);
            btnHelp.UseVisualStyleBackColor = true;
            btnHelp.Click += (s, e) =>
            {
                MessageBox.Show(
                    "BẢNG MÀU AUTOCAD COLOR INDEX (ACI 1-255)\n\n" +
                    "- Click chuột trực tiếp vào bất kỳ ô màu nào trên ma trận để chọn.\n" +
                    "- Hoặc gõ số màu ACI từ 1 đến 255 vào ô 'Color:'.\n" +
                    "- Phím tắt: Ấn 'ESC' để hủy và thoát ngay lập tức.",
                    "AutoCAD Color Index Help",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            };
            this.Controls.Add(btnHelp);

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }

        private void BuildPaletteMap()
        {
            _colorBoxes.Clear();

            int startX = 2;
            int startY = 2;
            int boxW = 17;
            int boxH = 11;
            int gapX = 1;
            int gapY = 1;

            // 1. Khối trên (Top block): 24 cột x 5 hàng (các màu giảm độ sáng: base + 8, 6, 4, 2, 0)
            int[] topOffsets = new int[] { 8, 6, 4, 2, 0 };
            for (int r = 0; r < 5; r++)
            {
                int offset = topOffsets[r];
                for (int c = 0; c < 24; c++)
                {
                    int baseVal = 10 + c * 10;
                    int colorIdx = baseVal + offset;
                    Rectangle rect = new Rectangle(startX + c * (boxW + gapX), startY + r * (boxH + gapY), boxW, boxH);
                    _colorBoxes.Add(new ColorBox
                    {
                        Rect = rect,
                        ColorIndex = colorIdx,
                        Rgb = GetAciRgb(colorIdx)
                    });
                }
            }

            // Khoảng cách giữa 2 khối
            int midY = startY + 5 * (boxH + gapY) + 4;

            // 2. Khối dưới (Bottom block): 24 cột x 5 hàng (các màu pha trắng: base + 1, 3, 5, 7, 9)
            int[] btmOffsets = new int[] { 1, 3, 5, 7, 9 };
            for (int r = 0; r < 5; r++)
            {
                int offset = btmOffsets[r];
                for (int c = 0; c < 24; c++)
                {
                    int baseVal = 10 + c * 10;
                    int colorIdx = baseVal + offset;
                    Rectangle rect = new Rectangle(startX + c * (boxW + gapX), midY + r * (boxH + gapY), boxW, boxH);
                    _colorBoxes.Add(new ColorBox
                    {
                        Rect = rect,
                        ColorIndex = colorIdx,
                        Rgb = GetAciRgb(colorIdx)
                    });
                }
            }

            // 3. Khối màu cơ bản & Xám (Standard & Grayscale Colors) bên dưới
            int stdY = midY + 5 * (boxH + gapY) + 8;
            int stdBoxW = 20;
            int stdBoxH = 14;

            // Hàng màu tiêu chuẩn: 1 (Red), 2 (Yellow), 3 (Green), 4 (Cyan), 5 (Blue), 6 (Magenta), 7 (White), 8 (Dark Gray), 9 (Light Gray)
            int[] stdColors = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            for (int i = 0; i < stdColors.Length; i++)
            {
                int cIdx = stdColors[i];
                Rectangle rect = new Rectangle(startX + i * (stdBoxW + 3), stdY, stdBoxW, stdBoxH);
                _colorBoxes.Add(new ColorBox
                {
                    Rect = rect,
                    ColorIndex = cIdx,
                    Rgb = GetAciRgb(cIdx)
                });
            }

            // Hàng cấp độ xám: 250..255
            int grayY = stdY + stdBoxH + 4;
            int[] grayColors = new int[] { 250, 251, 252, 253, 254, 255 };
            int grayBoxW = 16;
            for (int i = 0; i < grayColors.Length; i++)
            {
                int cIdx = grayColors[i];
                Rectangle rect = new Rectangle(startX + i * (grayBoxW + 2), grayY, grayBoxW, stdBoxH);
                _colorBoxes.Add(new ColorBox
                {
                    Rect = rect,
                    ColorIndex = cIdx,
                    Rgb = GetAciRgb(cIdx)
                });
            }
        }

        private void PnlPalette_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.None;

            for (int i = 0; i < _colorBoxes.Count; i++)
            {
                ColorBox box = _colorBoxes[i];
                using (SolidBrush b = new SolidBrush(box.Rgb))
                {
                    g.FillRectangle(b, box.Rect);
                }

                // Vẽ viền mờ cho từng ô
                using (Pen p = new Pen(Color.FromArgb(120, 120, 120)))
                {
                    g.DrawRectangle(p, box.Rect);
                }

                // Nếu là ô đang được rê chuột tới -> vẽ viền vàng hoặc trắng
                if (box.ColorIndex == _hoveredColorIndex && box.ColorIndex != _selectedColorIndex)
                {
                    using (Pen p = new Pen(Color.White, 1.5f))
                    {
                        Rectangle r = box.Rect;
                        g.DrawRectangle(p, r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2);
                    }
                }

                // Nếu là ô đang được CHỌN -> vẽ ô vuông viền trắng dày nổi bật đúng chuẩn AutoCAD!
                if (box.ColorIndex == _selectedColorIndex)
                {
                    Rectangle selRect = box.Rect;
                    selRect.Inflate(2, 2);
                    using (Pen pOuter = new Pen(Color.Black, 2f))
                    {
                        g.DrawRectangle(pOuter, selRect);
                    }
                    selRect.Inflate(-1, -1);
                    using (Pen pInner = new Pen(Color.White, 2f))
                    {
                        g.DrawRectangle(pInner, selRect);
                    }
                }
            }
        }

        private void PnlPalette_MouseMove(object sender, MouseEventArgs e)
        {
            int foundIdx = -1;
            for (int i = 0; i < _colorBoxes.Count; i++)
            {
                if (_colorBoxes[i].Rect.Contains(e.Location))
                {
                    foundIdx = _colorBoxes[i].ColorIndex;
                    break;
                }
            }

            if (foundIdx != _hoveredColorIndex)
            {
                _hoveredColorIndex = foundIdx;
                pnlPalette.Invalidate();
            }
        }

        private void PnlPalette_MouseClick(object sender, MouseEventArgs e)
        {
            for (int i = 0; i < _colorBoxes.Count; i++)
            {
                if (_colorBoxes[i].Rect.Contains(e.Location))
                {
                    SelectedColorIndex = _colorBoxes[i].ColorIndex;
                    break;
                }
            }
        }

        private void PnlPreview_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Rectangle rect = pnlPreview.ClientRectangle;

            // Vẽ viền xám bên ngoài
            using (Pen p = new Pen(Color.FromArgb(160, 160, 160)))
            {
                g.DrawRectangle(p, 0, 0, rect.Width - 1, rect.Height - 1);
            }

            // Vẽ swatch màu đang chọn
            Rectangle swatch = new Rectangle(2, 2, rect.Width - 4, rect.Height - 4);
            using (SolidBrush b = new SolidBrush(SelectedRgbColor))
            {
                g.FillRectangle(b, swatch);
            }
        }

        private void TxtColorInput_TextChanged(object sender, EventArgs e)
        {
            string val = txtColorInput.Text.Trim();
            if (string.IsNullOrEmpty(val)) return;

            int parsed;
            if (int.TryParse(val, out parsed))
            {
                if (parsed >= 0 && parsed <= 256)
                {
                    _selectedColorIndex = parsed;
                    UpdateLabelsAndPreviewOnly();
                    pnlPalette.Invalidate();
                }
            }
            else
            {
                // Thử nhận diện tên màu
                int namedIdx = ParseNamedColor(val);
                if (namedIdx >= 0)
                {
                    _selectedColorIndex = namedIdx;
                    UpdateLabelsAndPreviewOnly();
                    pnlPalette.Invalidate();
                }
            }
        }

        private void UpdateUiFromSelection()
        {
            UpdateLabelsAndPreviewOnly();

            // Cập nhật text input mà không kích hoạt đệ quy
            txtColorInput.TextChanged -= TxtColorInput_TextChanged;
            txtColorInput.Text = GetAciDisplayName(_selectedColorIndex);
            txtColorInput.TextChanged += TxtColorInput_TextChanged;

            if (pnlPalette != null) pnlPalette.Invalidate();
            if (pnlPreview != null) pnlPreview.Invalidate();
        }

        private void UpdateLabelsAndPreviewOnly()
        {
            if (lblIndexColor != null)
            {
                lblIndexColor.Text = string.Format("Index color: {0}", GetAciDisplayName(_selectedColorIndex));
            }
            if (lblRgb != null)
            {
                Color c = SelectedRgbColor;
                lblRgb.Text = string.Format("Red, Green, Blue: {0},{1},{2}", c.R, c.G, c.B);
            }
            if (pnlPreview != null)
            {
                pnlPreview.Invalidate();
            }
        }

        private string GetAciDisplayName(int aci)
        {
            switch (aci)
            {
                case 0: return "ByBlock";
                case 1: return "1 (red)";
                case 2: return "2 (yellow)";
                case 3: return "3 (green)";
                case 4: return "4 (cyan)";
                case 5: return "5 (blue)";
                case 6: return "6 (magenta)";
                case 7: return "7 (white)";
                case 256: return "ByLayer";
                default: return aci.ToString(CultureInfo.InvariantCulture);
            }
        }

        private int ParseNamedColor(string name)
        {
            if (string.IsNullOrEmpty(name)) return -1;
            string n = name.Trim().ToLowerInvariant();
            if (n == "red") return 1;
            if (n == "yellow") return 2;
            if (n == "green") return 3;
            if (n == "cyan") return 4;
            if (n == "blue") return 5;
            if (n == "magenta") return 6;
            if (n == "white") return 7;
            if (n == "bylayer") return 256;
            if (n == "byblock") return 0;
            return -1;
        }

        // ==================== BẢNG MÀU ACI CHUẨN AUTOCAD ====================
        private static readonly int[][] PureHues = new int[][]
        {
            new int[] { 255, 0, 0 },     // 0: Red (10)
            new int[] { 255, 63, 0 },    // 1 (20)
            new int[] { 255, 127, 0 },   // 2: Orange (30)
            new int[] { 255, 191, 0 },   // 3 (40)
            new int[] { 255, 255, 0 },   // 4: Yellow (50)
            new int[] { 191, 255, 0 },   // 5 (60)
            new int[] { 127, 255, 0 },   // 6 (70)
            new int[] { 63, 255, 0 },    // 7 (80)
            new int[] { 0, 255, 0 },     // 8: Green (90)
            new int[] { 0, 255, 63 },    // 9 (100)
            new int[] { 0, 255, 127 },   // 10 (110)
            new int[] { 0, 255, 191 },   // 11 (120)
            new int[] { 0, 255, 255 },   // 12: Cyan (130)
            new int[] { 0, 191, 255 },   // 13 (140)
            new int[] { 0, 127, 255 },   // 14 (150)
            new int[] { 0, 63, 255 },    // 15 (160)
            new int[] { 0, 0, 255 },     // 16: Blue (170)
            new int[] { 63, 0, 255 },    // 17 (180)
            new int[] { 127, 0, 255 },   // 18 (190)
            new int[] { 191, 0, 255 },   // 19 (200)
            new int[] { 255, 0, 255 },   // 20: Magenta (210)
            new int[] { 255, 0, 191 },   // 21 (220)
            new int[] { 255, 0, 127 },   // 22 (230)
            new int[] { 255, 0, 63 }     // 23 (240)
        };

        public static Color GetAciRgb(int aci)
        {
            if (aci <= 0) return Color.FromArgb(255, 255, 255); // ByBlock: mặc định trắng
            if (aci == 256) return Color.FromArgb(255, 255, 255); // ByLayer: mặc định trắng

            switch (aci)
            {
                case 1: return Color.FromArgb(255, 0, 0);       // Red
                case 2: return Color.FromArgb(255, 255, 0);     // Yellow
                case 3: return Color.FromArgb(0, 255, 0);       // Green
                case 4: return Color.FromArgb(0, 255, 255);     // Cyan
                case 5: return Color.FromArgb(0, 0, 255);       // Blue
                case 6: return Color.FromArgb(255, 0, 255);     // Magenta
                case 7: return Color.FromArgb(255, 255, 255);   // White
                case 8: return Color.FromArgb(128, 128, 128);   // Dark Gray
                case 9: return Color.FromArgb(192, 192, 192);   // Light Gray
                case 250: return Color.FromArgb(51, 51, 51);
                case 251: return Color.FromArgb(91, 91, 91);
                case 252: return Color.FromArgb(132, 132, 132);
                case 253: return Color.FromArgb(173, 173, 173);
                case 254: return Color.FromArgb(214, 214, 214);
                case 255: return Color.FromArgb(255, 255, 255);
            }

            if (aci >= 10 && aci <= 249)
            {
                int colIdx = (aci - 10) / 10;
                if (colIdx < 0) colIdx = 0;
                if (colIdx > 23) colIdx = 23;

                int[] pure = PureHues[colIdx];
                int offset = aci % 10;

                // Các màu khối trên (giảm độ sáng / dark shades)
                switch (offset)
                {
                    case 0: return Color.FromArgb(pure[0], pure[1], pure[2]);
                    case 2: return Color.FromArgb((int)(pure[0] * 0.80), (int)(pure[1] * 0.80), (int)(pure[2] * 0.80));
                    case 4: return Color.FromArgb((int)(pure[0] * 0.50), (int)(pure[1] * 0.50), (int)(pure[2] * 0.50));
                    case 6: return Color.FromArgb((int)(pure[0] * 0.35), (int)(pure[1] * 0.35), (int)(pure[2] * 0.35));
                    case 8: return Color.FromArgb((int)(pure[0] * 0.25), (int)(pure[1] * 0.25), (int)(pure[2] * 0.25));

                    // Các màu khối dưới (pha trắng / pastel shades)
                    case 1: return Color.FromArgb((int)(pure[0] * 0.80 + 255 * 0.20), (int)(pure[1] * 0.80 + 255 * 0.20), (int)(pure[2] * 0.80 + 255 * 0.20));
                    case 3: return Color.FromArgb((int)(pure[0] * 0.65 + 255 * 0.35), (int)(pure[1] * 0.65 + 255 * 0.35), (int)(pure[2] * 0.65 + 255 * 0.35));
                    case 5: return Color.FromArgb((int)(pure[0] * 0.50 + 255 * 0.50), (int)(pure[1] * 0.50 + 255 * 0.50), (int)(pure[2] * 0.50 + 255 * 0.50));
                    case 7: return Color.FromArgb((int)(pure[0] * 0.35 + 255 * 0.65), (int)(pure[1] * 0.35 + 255 * 0.65), (int)(pure[2] * 0.35 + 255 * 0.65));
                    case 9: return Color.FromArgb((int)(pure[0] * 0.20 + 255 * 0.80), (int)(pure[1] * 0.20 + 255 * 0.80), (int)(pure[2] * 0.20 + 255 * 0.80));
                }
            }

            return Color.White;
        }
    }
}
