using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace HDTool
{
    /// <summary>
    /// Khung xem trước 3D xoay được (không phụ thuộc Revit API).
    /// Nhận lưới tam giác (9 số / tam giác, đơn vị bất kỳ, trục Z hướng lên), tự canh giữa và vừa khung.
    /// Kéo chuột trái: xoay | Kéo chuột phải/giữa: dời | Lăn chuột: phóng to/thu nhỏ | Nhấp đúp: về góc nhìn ban đầu.
    /// Không có lưới thì hiện ảnh xem trước (Image) hoặc dòng thông báo (Message).
    /// </summary>
    public class FamilyMeshViewer : Control
    {
        private const float DefaultYaw = -0.785f;
        private const float DefaultPitch = 0.55f;
        private static readonly Font HintFont = new Font("Segoe UI", 7.5F);
        private static readonly Font MsgFont = new Font("Segoe UI", 9F);

        private float[] _tri;
        private int _triCount;
        private float _cx, _cy, _cz, _radius = 1f;
        private float _yaw = DefaultYaw, _pitch = DefaultPitch, _zoom = 1f, _panX, _panY;
        private Point _lastMouse;
        private MouseButtons _dragButton = MouseButtons.None;

        private PointF[][] _pts;
        private float[] _depth;
        private int[] _order;
        private int[] _shadeIdx;
        private SolidBrush[] _brushes;

        private Image _image;
        private string _message = "Chọn 1 Family trên bảng để xem trước 3D";

        public FamilyMeshViewer()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = Color.FromArgb(11, 14, 20);
            Cursor = Cursors.SizeAll;
            _brushes = new SolidBrush[64];
            for (int i = 0; i < _brushes.Length; i++)
            {
                float t = 0.22f + 0.78f * i / (_brushes.Length - 1);
                _brushes[i] = new SolidBrush(Color.FromArgb((int)(205 * t), (int)(214 * t), (int)(232 * t)));
            }
        }

        /// <summary>Ảnh dự phòng khi Family không có hình 3D.</summary>
        public Image Image
        {
            get { return _image; }
            set { _image = value; Invalidate(); }
        }

        public string Message
        {
            get { return _message; }
            set { _message = value; Invalidate(); }
        }

        public bool HasMesh { get { return _triCount > 0; } }

        /// <summary>Gán lưới tam giác mới (null = xóa) và về góc nhìn ban đầu.</summary>
        public void SetMesh(float[] triangles)
        {
            _tri = triangles;
            _triCount = triangles != null ? triangles.Length / 9 : 0;
            _pts = new PointF[_triCount][];
            for (int i = 0; i < _triCount; i++) _pts[i] = new PointF[3];
            _depth = new float[_triCount];
            _order = new int[_triCount];
            _shadeIdx = new int[_triCount];

            if (_triCount > 0)
            {
                float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
                float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
                for (int i = 0; i < _tri.Length; i += 3)
                {
                    float x = _tri[i], y = _tri[i + 1], z = _tri[i + 2];
                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                    if (y < minY) minY = y; if (y > maxY) maxY = y;
                    if (z < minZ) minZ = z; if (z > maxZ) maxZ = z;
                }
                _cx = (minX + maxX) / 2f; _cy = (minY + maxY) / 2f; _cz = (minZ + maxZ) / 2f;
                float dx = maxX - minX, dy = maxY - minY, dz = maxZ - minZ;
                _radius = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz) / 2f;
                if (_radius < 1e-6f) _radius = 1f;
            }
            ResetView();
        }

        public void ResetView()
        {
            _yaw = DefaultYaw; _pitch = DefaultPitch; _zoom = 1f; _panX = 0; _panY = 0;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            _dragButton = e.Button;
            _lastMouse = e.Location;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _dragButton = MouseButtons.None;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragButton == MouseButtons.None || _triCount == 0) return;
            int dx = e.X - _lastMouse.X, dy = e.Y - _lastMouse.Y;
            _lastMouse = e.Location;
            if (_dragButton == MouseButtons.Left)
            {
                _yaw += dx * 0.01f;
                _pitch += dy * 0.01f;
                if (_pitch > 1.55f) _pitch = 1.55f;
                if (_pitch < -1.55f) _pitch = -1.55f;
            }
            else
            {
                _panX += dx; _panY += dy;
            }
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_triCount == 0) return;
            _zoom *= e.Delta > 0 ? 1.15f : 1f / 1.15f;
            if (_zoom < 0.2f) _zoom = 0.2f;
            if (_zoom > 20f) _zoom = 20f;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            if (_triCount > 0) Focus(); // nhận lăn chuột ngay khi rê vào
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e);
            ResetView();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Rectangle rc = ClientRectangle;
            using (LinearGradientBrush bg = new LinearGradientBrush(rc, Color.FromArgb(28, 37, 64), Color.FromArgb(10, 14, 26), LinearGradientMode.Vertical))
            {
                g.FillRectangle(bg, rc);
            }

            if (_triCount > 0) DrawMesh(g, rc);
            else if (_image != null) DrawImage(g, rc);
            else DrawCenterText(g, rc, _message);

            using (Pen border = new Pen(Color.FromArgb(52, 66, 100)))
            {
                g.DrawRectangle(border, 0, 0, rc.Width - 1, rc.Height - 1);
            }

            if (_triCount > 0)
            {
                Rectangle hint = new Rectangle(8, rc.Height - 22, rc.Width - 16, 18);
                TextRenderer.DrawText(g, "Kéo chuột trái: xoay | Lăn: phóng to | Chuột phải: dời | Nhấp đúp: về ban đầu",
                    HintFont, hint, Color.FromArgb(138, 151, 178),
                    TextFormatFlags.NoPrefix | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
            }
        }

        private void DrawMesh(Graphics g, Rectangle rc)
        {
            float cyw = (float)Math.Cos(_yaw), syw = (float)Math.Sin(_yaw);
            float cp = (float)Math.Cos(_pitch), sp = (float)Math.Sin(_pitch);
            float scale = Math.Min(rc.Width, rc.Height - 20) * 0.45f / _radius * _zoom;
            float ox = rc.Width / 2f + _panX, oy = (rc.Height - 16) / 2f + _panY;

            // Hướng ánh sáng (hệ nhìn): từ trên-trái, phía người xem
            float lx = -0.35f, ly = 0.6f, lz = -0.72f;
            float[] vx = new float[3], vy = new float[3], vd = new float[3];

            for (int t = 0; t < _triCount; t++)
            {
                int b = t * 9;
                for (int k = 0; k < 3; k++)
                {
                    float px = _tri[b + k * 3] - _cx, py = _tri[b + k * 3 + 1] - _cy, pz = _tri[b + k * 3 + 2] - _cz;
                    float x1 = px * cyw - py * syw;
                    float y1 = px * syw + py * cyw;
                    float up = pz * cp + y1 * sp;
                    float depth = y1 * cp - pz * sp;
                    vx[k] = x1; vy[k] = up; vd[k] = depth;
                    _pts[t][k] = new PointF(ox + x1 * scale, oy - up * scale);
                }
                _depth[t] = -(vd[0] + vd[1] + vd[2]);
                _order[t] = t;

                float ux = vx[1] - vx[0], uy = vy[1] - vy[0], ud = vd[1] - vd[0];
                float wx = vx[2] - vx[0], wy = vy[2] - vy[0], wd = vd[2] - vd[0];
                float nx = uy * wd - ud * wy, ny = ud * wx - ux * wd, nd = ux * wy - uy * wx;
                float len = (float)Math.Sqrt(nx * nx + ny * ny + nd * nd);
                float shade = len > 1e-12f ? Math.Abs((nx * lx + ny * ly + nd * lz) / len) : 0.5f;
                _shadeIdx[t] = (int)(shade * (_brushes.Length - 1));
            }

            // Vẽ xa trước, gần sau (painter)
            float[] keys = (float[])_depth.Clone();
            Array.Sort(keys, _order);

            g.SmoothingMode = SmoothingMode.None;
            for (int i = 0; i < _triCount; i++)
            {
                int t = _order[i];
                g.FillPolygon(_brushes[_shadeIdx[t]], _pts[t]);
            }
        }

        private void DrawImage(Graphics g, Rectangle rc)
        {
            float s = Math.Min((rc.Width - 16f) / _image.Width, (rc.Height - 16f) / _image.Height);
            if (s > 3f) s = 3f;
            int w = (int)(_image.Width * s), h = (int)(_image.Height * s);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(_image, new Rectangle((rc.Width - w) / 2, (rc.Height - h) / 2, w, h));
            Rectangle hint = new Rectangle(8, rc.Height - 22, rc.Width - 16, 18);
            TextRenderer.DrawText(g, "Family không có hình khối 3D - hiển thị ảnh xem trước", HintFont, hint,
                Color.FromArgb(138, 151, 178), TextFormatFlags.NoPrefix | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }

        private void DrawCenterText(Graphics g, Rectangle rc, string text)
        {
            TextRenderer.DrawText(g, text ?? string.Empty, MsgFont, rc, Color.FromArgb(138, 151, 178),
                TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _brushes != null)
            {
                for (int i = 0; i < _brushes.Length; i++) if (_brushes[i] != null) _brushes[i].Dispose();
                _brushes = null;
            }
            base.Dispose(disposing);
        }
    }
}
