using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Color = System.Drawing.Color;
using Control = System.Windows.Forms.Control;
using Font = System.Drawing.Font;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;
using Form = System.Windows.Forms.Form;

namespace HDTool
{
    /// <summary>1 ống / phụ kiện trong khung xem trước.</summary>
    public class DuctPreviewItem
    {
        public CadDuctSegment Segment;
        public bool IsDuct;
        public int Number;          // ống thẳng: trùng số dòng trong bảng ở cửa sổ chính
        public bool Unlinked;       // không nối với đoạn nào -> cần kiểm tra
        internal PointF[] Outline;  // đáy khối, đơn vị feet
        internal float Height;      // feet

        public string Title
        {
            get
            {
                if (IsDuct) return "Ống #" + Number;
                string kind;
                switch (Segment.ShapeKind)
                {
                    case CadShapeKind.Transition: kind = "Côn thu"; break;
                    case CadShapeKind.TakeoffTap: kind = "Chân rẽ"; break;
                    case CadShapeKind.Elbow: kind = "Cút"; break;
                    case CadShapeKind.TeeBranch: kind = "Chạc 3"; break;
                    default: kind = "Phụ kiện"; break;
                }
                return kind + " P" + Number;
            }
        }

        public double LengthMm
        {
            get
            {
                if (Segment.StartPoint == null || Segment.EndPoint == null) return 0;
                return Segment.StartPoint.DistanceTo(Segment.EndPoint) * 304.8;
            }
        }
    }

    /// <summary>
    /// Khung 3D tự vẽ các ống gió nhận diện được (chiếu song song, vẽ xa trước gần sau), nét CAD nằm lót bên dưới.
    /// Chuột trái xoay, lăn phóng to, chuột phải / giữa dời, bấm để chọn, nhấp đúp vừa khung.
    /// </summary>
    public class CadDuctPreviewCanvas : Control
    {
        private const float DefaultYaw = -0.6f;
        private const float DefaultPitch = 0.85f;
        private const float TopPitch = (float)(Math.PI / 2);
        private const double MmToFeet = 1.0 / 304.8;
        public const double DefaultHeightMm = 300;
        private static readonly Color SceneColor = Color.FromArgb(15, 20, 36);

        private class Face
        {
            public DuctPreviewItem Item;
            public PointF[] Points;
            public float Depth;
            public Color Color;
        }

        private List<DuctPreviewItem> _items = new List<DuctPreviewItem>();
        private float[] _cad;              // x0,y0,x1,y1... feet, đã trừ gốc
        private double _ox, _oy;
        private readonly List<Face> _faces = new List<Face>();
        private float _yaw = DefaultYaw, _pitch = DefaultPitch;
        private float _scale = 1f, _panX, _panY, _cx, _cy;
        private bool _needFit = true;
        private Point _last;
        private int _moved;
        private MouseButtons _drag = MouseButtons.None;

        private DuctPreviewItem _selected;
        private bool _showCad = true;
        private bool _onlyProblems;

        public event EventHandler SelectionPicked;

        public CadDuctPreviewCanvas()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
                | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = SceneColor;
        }

        public DuctPreviewItem Selected
        {
            get { return _selected; }
            set { _selected = value; Invalidate(); }
        }

        public bool ShowCad
        {
            get { return _showCad; }
            set { _showCad = value; Invalidate(); }
        }

        public bool OnlyProblems
        {
            get { return _onlyProblems; }
            set { _onlyProblems = value; Invalidate(); }
        }

        public void SetData(List<DuctPreviewItem> items, float[] cadSegmentsFeet)
        {
            _items = items ?? new List<DuctPreviewItem>();
            _selected = null;
            _ox = 0; _oy = 0;
            int n = 0;
            foreach (DuctPreviewItem it in _items)
            {
                XYZ c = it.Segment.CenterPoint ?? it.Segment.StartPoint;
                if (c == null) continue;
                _ox += c.X; _oy += c.Y; n++;
            }
            if (n > 0) { _ox /= n; _oy /= n; }

            _cad = null;
            if (cadSegmentsFeet != null)
            {
                _cad = new float[cadSegmentsFeet.Length];
                for (int i = 0; i + 1 < cadSegmentsFeet.Length; i += 2)
                {
                    _cad[i] = (float)(cadSegmentsFeet[i] - _ox);
                    _cad[i + 1] = (float)(cadSegmentsFeet[i + 1] - _oy);
                }
            }
            RebuildGeometry();
            ResetView(false);
        }

        /// <summary>Dựng lại khối (sau khi người dùng sửa W / H trong bảng), giữ nguyên góc nhìn.</summary>
        public void RebuildGeometry()
        {
            foreach (DuctPreviewItem it in _items)
            {
                CadDuctSegment s = it.Segment;
                it.Height = (float)((s.HeightMm > 0 ? s.HeightMm : DefaultHeightMm) * MmToFeet);
                it.Outline = null;
                List<XYZ> pts = null;
                // Ống thẳng vẽ theo Start -> End x W: đúng cái sẽ được dựng (kể cả đoạn đã gộp / đã sửa W)
                if (it.IsDuct && s.StartPoint != null && s.EndPoint != null && s.StartPoint.DistanceTo(s.EndPoint) > 1e-6)
                {
                    XYZ d = s.EndPoint - s.StartPoint;
                    double len = Math.Sqrt(d.X * d.X + d.Y * d.Y);
                    if (len > 1e-6)
                    {
                        XYZ side = new XYZ(-d.Y / len, d.X / len, 0) * (s.WidthMm * 0.5 * MmToFeet);
                        pts = new List<XYZ> { s.StartPoint + side, s.EndPoint + side, s.EndPoint - side, s.StartPoint - side };
                    }
                }
                if (pts == null && s.PolygonVertices != null && s.PolygonVertices.Count >= 3) pts = s.PolygonVertices;
                if (pts == null) continue;

                PointF[] o = new PointF[pts.Count];
                for (int i = 0; i < pts.Count; i++) o[i] = new PointF((float)(pts[i].X - _ox), (float)(pts[i].Y - _oy));
                it.Outline = o;
            }
            Invalidate();
        }

        public void ResetView(bool topDown)
        {
            _yaw = topDown ? 0f : DefaultYaw;
            _pitch = topDown ? TopPitch : DefaultPitch;
            Fit();
        }

        /// <summary>Đoạn được chọn nằm ngoài khung nhìn thì dời khung để thấy.</summary>
        public void Reveal(DuctPreviewItem item)
        {
            if (item == null || item.Outline == null || _needFit) return;
            float x = 0, y = 0, depth;
            foreach (PointF p in item.Outline) { x += p.X; y += p.Y; }
            PointF c = Project(x / item.Outline.Length, y / item.Outline.Length, 0, out depth);
            if (c.X > 30 && c.Y > 40 && c.X < ClientSize.Width - 30 && c.Y < ClientSize.Height - 40) return;
            _panX += ClientSize.Width / 2f - c.X;
            _panY += ClientSize.Height / 2f - c.Y;
            Invalidate();
        }

        public void Fit()
        {
            _needFit = true;
            Invalidate();
        }

        private PointF Project(float x, float y, float z, out float depth)
        {
            float X = (float)(x * Math.Cos(_yaw) - y * Math.Sin(_yaw));
            float Y = (float)(x * Math.Sin(_yaw) + y * Math.Cos(_yaw));
            depth = (float)(Y * Math.Cos(_pitch) - z * Math.Sin(_pitch));
            float up = (float)(Y * Math.Sin(_pitch) + z * Math.Cos(_pitch));
            return new PointF(ClientSize.Width / 2f + _panX + (X - _cx) * _scale, ClientSize.Height / 2f + _panY - (up - _cy) * _scale);
        }

        // Canh khung theo tuyến đã nối (đoạn lạc nằm xa không làm tuyến chính bé tí)
        private void ComputeFit()
        {
            bool hasLinked = false;
            foreach (DuctPreviewItem it in _items) if (!it.Unlinked && it.Outline != null) { hasLinked = true; break; }

            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (DuctPreviewItem it in _items)
            {
                if (it.Outline == null || (hasLinked && it.Unlinked)) continue;
                foreach (PointF p in it.Outline)
                {
                    float X = (float)(p.X * Math.Cos(_yaw) - p.Y * Math.Sin(_yaw));
                    float Y = (float)((p.X * Math.Sin(_yaw) + p.Y * Math.Cos(_yaw)) * Math.Sin(_pitch));
                    minX = Math.Min(minX, X); maxX = Math.Max(maxX, X);
                    minY = Math.Min(minY, Y); maxY = Math.Max(maxY, Y);
                }
            }
            _panX = 0; _panY = 0;
            if (minX > maxX) { _cx = 0; _cy = 0; _scale = 1; return; }
            _cx = (minX + maxX) / 2f;
            _cy = (minY + maxY) / 2f;
            float sx = (ClientSize.Width * 0.9f) / Math.Max(1f, maxX - minX);
            float sy = (ClientSize.Height * 0.82f) / Math.Max(1f, maxY - minY);
            _scale = Math.Max(0.01f, Math.Min(sx, sy));
        }

        private Color ColorOf(DuctPreviewItem it)
        {
            if (it == _selected) return Color.FromArgb(255, 214, 10);
            if (it.Segment.IsExcluded) return Color.FromArgb(55, 150, 150, 160);
            if (it.Unlinked) return Color.FromArgb(255, 69, 58);
            return it.IsDuct ? Color.FromArgb(76, 141, 255) : Color.FromArgb(52, 199, 89);
        }

        private static Color Shade(Color c, float k)
        {
            return Color.FromArgb(c.A, (int)(c.R * k), (int)(c.G * k), (int)(c.B * k));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(SceneColor); // khung 3D luôn nền tối, không theo giao diện sáng của cửa sổ
            if (ClientSize.Width < 10 || ClientSize.Height < 10) return;
            if (_needFit) { ComputeFit(); _needFit = false; }
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float depth;

            if (_showCad && _cad != null)
            {
                using (Pen pen = new Pen(Color.FromArgb(110, 190, 200, 225), 1f))
                {
                    float z = (float)(-DefaultHeightMm * 0.5 * MmToFeet);
                    for (int i = 0; i + 3 < _cad.Length; i += 4)
                    {
                        g.DrawLine(pen, Project(_cad[i], _cad[i + 1], z, out depth), Project(_cad[i + 2], _cad[i + 3], z, out depth));
                    }
                }
            }

            _faces.Clear();
            foreach (DuctPreviewItem it in _items)
            {
                if (it.Outline == null) continue;
                if (_onlyProblems && !it.Unlinked && !it.Segment.IsExcluded && it != _selected) continue;
                Color color = ColorOf(it);
                int n = it.Outline.Length;
                PointF[] top = new PointF[n], bottom = new PointF[n];
                float[] dTop = new float[n], dBottom = new float[n];
                float topDepth = 0;
                for (int i = 0; i < n; i++)
                {
                    top[i] = Project(it.Outline[i].X, it.Outline[i].Y, it.Height / 2, out dTop[i]);
                    bottom[i] = Project(it.Outline[i].X, it.Outline[i].Y, -it.Height / 2, out dBottom[i]);
                    topDepth += dTop[i];
                }
                _faces.Add(new Face { Item = it, Points = top, Depth = topDepth / n, Color = color });
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    double edge = Math.Atan2(it.Outline[j].Y - it.Outline[i].Y, it.Outline[j].X - it.Outline[i].X) + _yaw;
                    _faces.Add(new Face
                    {
                        Item = it,
                        Points = new PointF[] { top[i], top[j], bottom[j], bottom[i] },
                        Depth = (dTop[i] + dTop[j] + dBottom[j] + dBottom[i]) / 4,
                        Color = Shade(color, (float)(0.5 + 0.28 * Math.Abs(Math.Cos(edge))))
                    });
                }
            }
            _faces.Sort(delegate(Face a, Face b) { return b.Depth.CompareTo(a.Depth); });

            using (Pen edgePen = new Pen(Color.FromArgb(70, 0, 0, 0), 1f))
            using (Pen selPen = new Pen(Color.White, 1.4f))
            using (SolidBrush brush = new SolidBrush(Color.White))
            {
                foreach (Face f in _faces)
                {
                    brush.Color = f.Color;
                    g.FillPolygon(brush, f.Points);
                    g.DrawPolygon(f.Item == _selected ? selPen : edgePen, f.Points);
                }
            }

            // Ống đang chọn / chỗ cần kiểm tra quá nhỏ trên màn hình -> khoanh tròn để thấy được
            if (_selected != null && _selected.Outline != null)
            {
                float cx = 0, cy = 0;
                foreach (PointF p in _selected.Outline) { cx += p.X; cy += p.Y; }
                PointF c = Project(cx / _selected.Outline.Length, cy / _selected.Outline.Length, 0, out depth);
                using (Pen ring = new Pen(Color.FromArgb(255, 214, 10), 2f)) g.DrawEllipse(ring, c.X - 18, c.Y - 18, 36, 36);
            }

            DrawLegend(g);
            TextRenderer.DrawText(g, "Giữ chuột giữa: kéo view | Shift + chuột giữa: xoay | Lăn: phóng to | Bấm trái: chọn ống | Nhấp đúp: vừa khung",
                Font, new Rectangle(8, ClientSize.Height - Font.Height - 8, ClientSize.Width - 16, Font.Height + 4),
                Color.FromArgb(154, 164, 189), TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        private void DrawLegend(Graphics g)
        {
            string[] names = { "Ống thẳng", "Phụ kiện", "Chưa nối - cần kiểm tra", "Đang chọn" };
            Color[] colors = { Color.FromArgb(76, 141, 255), Color.FromArgb(52, 199, 89), Color.FromArgb(255, 69, 58), Color.FromArgb(255, 214, 10) };
            int x = 10, y = 8, h = Font.Height;
            for (int i = 0; i < names.Length; i++)
            {
                int w = TextRenderer.MeasureText(g, names[i], Font, Size.Empty, TextFormatFlags.NoPrefix).Width;
                if (x + w + h + 14 > ClientSize.Width && x > 10) { x = 10; y += h + 4; }
                using (SolidBrush b = new SolidBrush(colors[i])) g.FillRectangle(b, x, y + 2, h - 4, h - 4);
                TextRenderer.DrawText(g, names[i], Font, new Point(x + h, y), Color.FromArgb(220, 226, 240), TextFormatFlags.NoPrefix);
                x += w + h + 14;
            }
        }

        private DuctPreviewItem Pick(Point at)
        {
            for (int k = _faces.Count - 1; k >= 0; k--)
            {
                PointF[] p = _faces[k].Points;
                bool inside = false;
                for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
                {
                    if ((p[i].Y > at.Y) != (p[j].Y > at.Y)
                        && at.X < (p[j].X - p[i].X) * (at.Y - p[i].Y) / (p[j].Y - p[i].Y) + p[i].X) inside = !inside;
                }
                if (inside) return _faces[k].Item;
            }
            // Ống mảnh khó bấm trúng: lấy mặt có đỉnh gần con trỏ trong 6 px
            DuctPreviewItem best = null;
            float bestD = 36f;
            foreach (Face f in _faces)
            {
                foreach (PointF p in f.Points)
                {
                    float d = (p.X - at.X) * (p.X - at.X) + (p.Y - at.Y) * (p.Y - at.Y);
                    if (d < bestD) { bestD = d; best = f.Item; }
                }
            }
            return best;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            Form f = FindForm();
            if (f != null && Form.ActiveForm == f) Focus();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            _drag = e.Button;
            _last = e.Location;
            _moved = 0;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_drag == MouseButtons.None) return;
            int dx = e.X - _last.X, dy = e.Y - _last.Y;
            _last = e.Location;
            _moved += Math.Abs(dx) + Math.Abs(dy);
            if (_moved < 4) return;
            // Giống Revit: giữ chuột giữa kéo view, Shift + chuột giữa xoay (chuột trái vẫn xoay, chuột phải vẫn dời)
            bool rotate = _drag == MouseButtons.Left || (_drag == MouseButtons.Middle && (ModifierKeys & Keys.Shift) != 0);
            Cursor = rotate ? Cursors.Default : Cursors.SizeAll;
            if (rotate)
            {
                _yaw += dx * 0.008f;
                _pitch = Math.Max(0.05f, Math.Min(TopPitch, _pitch + dy * 0.008f));
            }
            else
            {
                _panX += dx;
                _panY += dy;
            }
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_drag == MouseButtons.Left && _moved < 4)
            {
                _selected = Pick(e.Location);
                Invalidate();
                if (SelectionPicked != null) SelectionPicked(this, EventArgs.Empty);
            }
            _drag = MouseButtons.None;
            Cursor = Cursors.Default;
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            Fit();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            float k = e.Delta > 0 ? 1.18f : 1f / 1.18f;
            float next = Math.Max(0.01f, Math.Min(20000f, _scale * k));
            k = next / _scale;
            float mx = e.X - ClientSize.Width / 2f, my = e.Y - ClientSize.Height / 2f;
            _panX = mx - (mx - _panX) * k;
            _panY = my - (my - _panY) * k;
            _scale = next;
            Invalidate();
        }
    }
}
