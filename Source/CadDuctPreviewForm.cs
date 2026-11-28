using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Color = System.Drawing.Color;
using Control = System.Windows.Forms.Control;
using Font = System.Drawing.Font;
using Point = System.Drawing.Point;

namespace HDTool
{
    /// <summary>
    /// Cửa sổ xem trước 3D ống gió nhận diện từ CAD: tự mở sau khi quét để kỹ sư soát lỗi,
    /// bỏ đoạn sai rồi mới dựng hàng loạt. Không gọi Revit API (chỉ đọc dữ liệu đã nhận diện).
    /// </summary>
    public class CadDuctPreviewForm : HDToolBaseForm
    {
        private CadDuctPreviewCanvas _canvas;
        private Label _lblCount;
        private Label _lblProblems;
        private Label _lblDetail;
        private Button _btnToggle;
        private ListBox _list;
        private Label _lblStatus;
        private Button _btnBuild;
        private readonly List<DuctPreviewItem> _items = new List<DuctPreviewItem>();
        private bool _syncingList;

        /// <summary>Người dùng bỏ / đưa lại 1 đoạn.</summary>
        public event EventHandler ExclusionChanged;
        public event EventHandler BuildRequested;
        public event EventHandler RescanRequested;
        /// <summary>Người dùng chọn 1 ống thẳng (tham số: số dòng trong bảng, tính từ 1).</summary>
        public event Action<int> DuctPicked;

        public CadDuctPreviewForm()
            : base()
        {
            BuildLayout();
        }

        private static Button MakeButton(string text, int width, bool primary)
        {
            Button b = new Button();
            b.Text = text;
            b.Size = new Size(width, 32);
            b.Margin = new Padding(0, 0, 8, 0);
            b.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = primary ? 0 : 1;
            b.FlatAppearance.BorderColor = HDToolTheme.LineStrong;
            b.BackColor = primary ? HDToolTheme.Accent : HDToolTheme.Surface;
            b.ForeColor = primary ? HDToolTheme.AccentInk : HDToolTheme.Text;
            b.Cursor = Cursors.Hand;
            b.UseVisualStyleBackColor = false;
            return b;
        }

        private static CheckBox MakeCheck(string text, bool on)
        {
            CheckBox c = new CheckBox();
            c.Text = text;
            c.Checked = on;
            c.AutoSize = true;
            c.Margin = new Padding(8, 6, 8, 0);
            c.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            c.ForeColor = HDToolTheme.Text;
            return c;
        }

        private static Label MakeLabel(float size, FontStyle style, Color color)
        {
            Label l = new Label();
            l.Dock = DockStyle.Fill;
            l.Font = new Font("Segoe UI", size, style);
            l.ForeColor = color;
            l.TextAlign = ContentAlignment.MiddleLeft;
            l.AutoEllipsis = true;
            l.UseMnemonic = false;
            l.Margin = new Padding(0);
            return l;
        }

        private static TableLayoutPanel MakeTable(int columns, int rows)
        {
            TableLayoutPanel t = new TableLayoutPanel();
            t.Dock = DockStyle.Fill;
            t.ColumnCount = columns;
            t.RowCount = rows;
            t.Margin = new Padding(0);
            return t;
        }

        private void BuildLayout()
        {
            Text = "HDTOOL - Xem Trước 3D Ống Gió";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1160, 720);
            MinimumSize = new Size(900, 600);
            BackColor = HDToolTheme.Bg;
            ShowInTaskbar = false;

            TableLayoutPanel root = MakeTable(1, 3);
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));

            // ----- Thanh công cụ -----
            FlowLayoutPanel tools = new FlowLayoutPanel();
            tools.Dock = DockStyle.Fill;
            tools.Margin = new Padding(0);
            tools.Padding = new Padding(12, 8, 12, 0);
            tools.WrapContents = false;
            tools.BackColor = HDToolTheme.Surface;
            Button b3d = MakeButton("Nhìn 3D", 90, false);
            Button bTop = MakeButton("Nhìn từ trên", 112, false);
            Button bFit = MakeButton("Vừa khung", 100, false);
            CheckBox chkCad = MakeCheck("Hiện nét CAD", true);
            CheckBox chkOnly = MakeCheck("Chỉ hiện chỗ cần kiểm tra", false);
            b3d.Click += delegate { _canvas.ResetView(false); };
            bTop.Click += delegate { _canvas.ResetView(true); };
            bFit.Click += delegate { _canvas.Fit(); };
            chkCad.CheckedChanged += delegate { _canvas.ShowCad = chkCad.Checked; };
            chkOnly.CheckedChanged += delegate { _canvas.OnlyProblems = chkOnly.Checked; };
            tools.Controls.AddRange(new Control[] { b3d, bTop, bFit, chkCad, chkOnly });
            root.Controls.Add(tools, 0, 0);

            // ----- Khung 3D + cột phải -----
            TableLayoutPanel body = MakeTable(2, 1);
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 310F));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _canvas = new CadDuctPreviewCanvas();
            _canvas.Dock = DockStyle.Fill;
            _canvas.Margin = new Padding(0);
            _canvas.Font = new Font("Segoe UI", 8.5F);
            _canvas.SelectionPicked += delegate { OnPicked(_canvas.Selected); };
            body.Controls.Add(_canvas, 0, 0);

            TableLayoutPanel side = MakeTable(1, 6);
            side.Padding = new Padding(12, 6, 12, 8);
            side.BackColor = HDToolTheme.Surface2;
            side.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            side.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            side.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            side.RowStyles.Add(new RowStyle(SizeType.Absolute, 112F));
            side.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            side.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            side.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _lblCount = MakeLabel(9.5F, FontStyle.Bold, HDToolTheme.Text);
            _lblProblems = MakeLabel(9.5F, FontStyle.Bold, HDToolTheme.Warn);
            _lblDetail = MakeLabel(9F, FontStyle.Regular, HDToolTheme.Text);
            _lblDetail.BackColor = HDToolTheme.Surface;
            _lblDetail.Padding = new Padding(8, 4, 8, 4);
            _lblDetail.TextAlign = ContentAlignment.TopLeft;
            _lblDetail.Paint += delegate(object s, PaintEventArgs e) { using (Pen p = new Pen(HDToolTheme.Accent, 2F)) e.Graphics.DrawRectangle(p, 1, 1, _lblDetail.Width - 2, _lblDetail.Height - 2); };
            _btnToggle = MakeButton("Bỏ đoạn này, không dựng", 280, false);
            _btnToggle.Dock = DockStyle.Fill;
            _btnToggle.Margin = new Padding(0, 4, 0, 4);
            _btnToggle.Click += delegate { ToggleSelected(); };
            Label lblList = MakeLabel(9F, FontStyle.Bold, HDToolTheme.Text);
            lblList.Text = "Chỗ cần kiểm tra";

            _list = new ListBox();
            _list.Dock = DockStyle.Fill;
            _list.Margin = new Padding(0);
            _list.IntegralHeight = false;
            _list.BorderStyle = BorderStyle.FixedSingle;
            _list.Font = new Font("Segoe UI", 9F);
            _list.BackColor = HDToolTheme.Surface;
            _list.ForeColor = HDToolTheme.Text;
            _list.DrawMode = DrawMode.OwnerDrawFixed;
            _list.ItemHeight = 24;
            _list.DrawItem += List_DrawItem;
            _list.SelectedIndexChanged += delegate
            {
                if (_syncingList) return;
                ListEntry entry = _list.SelectedItem as ListEntry;
                if (entry == null) return;
                _canvas.Selected = entry.Item;
                _canvas.Reveal(entry.Item);
                OnPicked(entry.Item);
            };

            side.Controls.Add(_lblCount, 0, 0);
            side.Controls.Add(_lblProblems, 0, 1);
            side.Controls.Add(_lblDetail, 0, 2);
            side.Controls.Add(_btnToggle, 0, 3);
            side.Controls.Add(lblList, 0, 4);
            side.Controls.Add(_list, 0, 5);
            body.Controls.Add(side, 1, 0);
            root.Controls.Add(body, 0, 1);

            // ----- Chân cửa sổ -----
            TableLayoutPanel foot = MakeTable(4, 1);
            foot.Padding = new Padding(12, 9, 12, 0);
            foot.BackColor = HDToolTheme.Header;
            foot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            foot.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            foot.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            foot.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            foot.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _lblStatus = MakeLabel(8.5F, FontStyle.Bold, HDToolTheme.Ok);
            _lblStatus.Dock = DockStyle.Top;
            _lblStatus.Height = 34;
            Button bScan = MakeButton("Quét lại", 90, false);
            _btnBuild = MakeButton("Dựng hàng loạt", 180, true);
            Button bClose = MakeButton("Đóng (ESC)", 100, false);
            bClose.Margin = new Padding(0);
            bScan.Click += delegate { if (RescanRequested != null) RescanRequested(this, EventArgs.Empty); };
            _btnBuild.Click += delegate { if (BuildRequested != null) BuildRequested(this, EventArgs.Empty); };
            bClose.Click += delegate { Close(); };
            foot.Controls.Add(_lblStatus, 0, 0);
            foot.Controls.Add(bScan, 1, 0);
            foot.Controls.Add(_btnBuild, 2, 0);
            foot.Controls.Add(bClose, 3, 0);
            root.Controls.Add(foot, 0, 2);

            Controls.Add(root);
        }

        private class ListEntry
        {
            public DuctPreviewItem Item;

            public override string ToString()
            {
                string width = Item.Segment.WidthMm > 0 && Item.LengthMm > 0
                    ? string.Format(CultureInfo.CurrentCulture, " · W{0:0} · dài {1:0} mm", Item.Segment.WidthMm, Item.LengthMm)
                    : "";
                return Item.Title + width + (Item.Segment.IsExcluded ? " (đã bỏ)" : "");
            }
        }

        private void List_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _list.Items.Count) return;
            ListEntry entry = (ListEntry)_list.Items[e.Index];
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using (SolidBrush back = new SolidBrush(selected ? HDToolTheme.AccentSoft : HDToolTheme.Surface))
                e.Graphics.FillRectangle(back, e.Bounds);
            Rectangle r = new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, entry.ToString(), _list.Font, r, entry.Item.Segment.IsExcluded ? HDToolTheme.Text3 : HDToolTheme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        /// <summary>Nạp kết quả nhận diện mới.</summary>
        public void SetData(List<CadDuctSegment> ducts, List<CadDuctSegment> fittings, List<CadConnectionEdge> edges, float[] cadLines)
        {
            HashSet<CadDuctSegment> linked = new HashSet<CadDuctSegment>();
            if (edges != null)
            {
                foreach (CadConnectionEdge edge in edges)
                {
                    if (edge.SegmentA != null) linked.Add(edge.SegmentA);
                    if (edge.SegmentB != null) linked.Add(edge.SegmentB);
                    if (edge.BridgePolygon != null) linked.Add(edge.BridgePolygon);
                }
            }

            _items.Clear();
            HashSet<CadDuctSegment> added = new HashSet<CadDuctSegment>();
            if (ducts != null)
            {
                for (int i = 0; i < ducts.Count; i++)
                {
                    if (!added.Add(ducts[i])) continue;
                    _items.Add(new DuctPreviewItem { Segment = ducts[i], IsDuct = true, Number = i + 1, Unlinked = !linked.Contains(ducts[i]) });
                }
            }
            int number = 0;
            if (fittings != null)
            {
                foreach (CadDuctSegment f in fittings)
                {
                    if (!added.Add(f)) continue;
                    _items.Add(new DuctPreviewItem { Segment = f, IsDuct = false, Number = ++number, Unlinked = !linked.Contains(f) });
                }
            }

            _canvas.SetData(_items, cadLines);
            SetStatus(HDToolTheme.Ok, "Kiểm tra hình, bỏ đoạn sai, rồi bấm dựng.");
            RefreshSide();
        }

        /// <summary>Gọi sau khi người dùng sửa W / H ở bảng của cửa sổ chính.</summary>
        public void RefreshGeometry()
        {
            _canvas.RebuildGeometry();
            RefreshSide();
        }

        public void SetStatus(Color color, string text)
        {
            if (IsDisposed) return;
            _lblStatus.ForeColor = color;
            _lblStatus.Text = text;
        }

        private void OnPicked(DuctPreviewItem item)
        {
            RefreshSide();
            if (item != null && item.IsDuct && DuctPicked != null) DuctPicked(item.Number);
        }

        private void ToggleSelected()
        {
            DuctPreviewItem item = _canvas.Selected;
            if (item == null) return;
            item.Segment.IsExcluded = !item.Segment.IsExcluded;
            _canvas.Invalidate();
            RefreshSide();
            if (ExclusionChanged != null) ExclusionChanged(this, EventArgs.Empty);
        }

        private void RefreshSide()
        {
            int ducts = 0, fits = 0, problems = 0;
            foreach (DuctPreviewItem it in _items)
            {
                if (it.Segment.IsExcluded) continue;
                if (it.IsDuct) ducts++; else fits++;
                if (it.Unlinked) problems++;
            }
            _lblCount.Text = string.Format("{0} ống thẳng · {1} phụ kiện", ducts, fits);
            _lblProblems.Text = problems > 0 ? problems + " chỗ chưa nối, cần kiểm tra" : "Không còn chỗ chưa nối";
            _lblProblems.ForeColor = problems > 0 ? HDToolTheme.Warn : HDToolTheme.Ok;
            _btnBuild.Text = "Dựng hàng loạt (" + ducts + " ống)";
            _btnBuild.Enabled = ducts > 0;

            DuctPreviewItem sel = _canvas.Selected;
            _btnToggle.Enabled = sel != null;
            if (sel == null)
            {
                _lblDetail.Text = "Bấm vào một đoạn ống trong hình hoặc trong danh sách để xem chi tiết.";
                _btnToggle.Text = "Bỏ đoạn này, không dựng";
            }
            else
            {
                CadDuctSegment s = sel.Segment;
                string height = s.HeightMm > 0
                    ? string.Format("{0:0} mm", s.HeightMm)
                    : string.Format("chưa có (hình đang vẽ tạm {0:0})", CadDuctPreviewCanvas.DefaultHeightMm);
                _lblDetail.Text = string.Format("{0}\nRộng W: {1:0} mm · Dài: {2:0} mm\nCao H: {3}\nLayer: {4}\n{5}",
                    sel.Title, s.WidthMm, sel.LengthMm, height, s.LayerName,
                    s.IsExcluded ? "ĐÃ BỎ, không dựng" : (sel.Unlinked ? "Chưa nối với đoạn nào" : "Đã nối tuyến"));
                _btnToggle.Text = s.IsExcluded ? "Đưa lại vào danh sách dựng" : "Bỏ đoạn này, không dựng";
            }

            _syncingList = true;
            int topIndex = _list.TopIndex;
            _list.BeginUpdate();
            _list.Items.Clear();
            int selectedIndex = -1;
            foreach (DuctPreviewItem it in _items)
            {
                if (!it.Unlinked && !it.Segment.IsExcluded) continue;
                int index = _list.Items.Add(new ListEntry { Item = it });
                if (it == sel) selectedIndex = index;
            }
            if (_list.Items.Count > 0) _list.TopIndex = Math.Min(Math.Max(0, topIndex), _list.Items.Count - 1);
            _list.SelectedIndex = selectedIndex;
            _list.EndUpdate();
            _syncingList = false;
        }
    }
}
