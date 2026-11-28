using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace HDTool
{
    /// <summary>
    /// Khung xem trước 3D xoay được (không phụ thuộc Revit API).
    /// Nhận lưới tam giác (9 số / tam giác, đơn vị bất kỳ, trục Z hướng lên), tự canh giữa và vừa khung.
    /// Giữ chuột giữa (hoặc phải): kéo view như Revit | Chuột trái hoặc Shift + giữa: xoay | Lăn chuột: phóng to/thu nhỏ | Nhấp đúp: về góc nhìn ban đầu.
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
        private float[] _seg;
        private int _segCount;
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

        public bool HasMesh { get { return _triCount > 0 || _segCount > 0; } }

        /// <summary>Có gì để xoay: lưới, đường nét, hoặc ảnh (xoay như tấm phẳng).</summary>
        private bool CanRotate { get { return _triCount > 0 || _segCount > 0 || _image != null; } }

        /// <summary>Gán lưới tam giác mới (null = xóa) và về góc nhìn ban đầu.</summary>
        public void SetMesh(float[] triangles)
        {
            SetGeometry(triangles, null);
        }

        /// <summary>Gán tam giác (9 số/tam giác) + đoạn thẳng (6 số/đoạn); null = không có. Về góc nhìn ban đầu.</summary>
        public void SetGeometry(float[] triangles, float[] segments)
        {
            _tri = triangles;
            _triCount = triangles != null ? triangles.Length / 9 : 0;
            _seg = segments;
            _segCount = segments != null ? segments.Length / 6 : 0;
            _pts = new PointF[_triCount][];
            for (int i = 0; i < _triCount; i++) _pts[i] = new PointF[3];
            _depth = new float[_triCount];
            _order = new int[_triCount];
            _shadeIdx = new int[_triCount];

            if (_triCount > 0 || _segCount > 0)
            {
                float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
                float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
                float[][] sets = { _tri, _seg };
                foreach (float[] a in sets)
                {
                    if (a == null) continue;
                    for (int i = 0; i + 2 < a.Length; i += 3)
                    {
                        float x = a[i], y = a[i + 1], z = a[i + 2];
                        if (x < minX) minX = x; if (x > maxX) maxX = x;
                        if (y < minY) minY = y; if (y > maxY) maxY = y;
                        if (z < minZ) minZ = z; if (z > maxZ) maxZ = z;
                    }
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
            if (_dragButton == MouseButtons.None || !CanRotate) return;
            int dx = e.X - _lastMouse.X, dy = e.Y - _lastMouse.Y;
            _lastMouse = e.Location;
            // Giống Revit: giữ chuột giữa kéo view, Shift + chuột giữa xoay
            if (_dragButton == MouseButtons.Left || (_dragButton == MouseButtons.Middle && (ModifierKeys & Keys.Shift) != 0))
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
            if (!CanRotate) return;
            _zoom *= e.Delta > 0 ? 1.15f : 1f / 1.15f;
            if (_zoom < 0.2f) _zoom = 0.2f;
            if (_zoom > 20f) _zoom = 20f;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            if (CanRotate) Focus(); // nhận lăn chuột ngay khi rê vào
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

            if (_triCount > 0 || _segCount > 0) DrawMesh(g, rc);
            else if (_image != null) DrawImage(g, rc);
            else DrawCenterText(g, rc, _message);

            using (Pen border = new Pen(Color.FromArgb(52, 66, 100)))
            {
                g.DrawRectangle(border, 0, 0, rc.Width - 1, rc.Height - 1);
            }

            if (CanRotate)
            {
                string tip = (_triCount > 0 || _segCount > 0)
                    ? "Chuột giữa: kéo view | Trái: xoay | Lăn: phóng to"
                    : "Family 2D | Chuột giữa: kéo view | Trái: xoay | Lăn: phóng to";
                Rectangle hint = new Rectangle(8, rc.Height - 22, rc.Width - 16, 18);
                TextRenderer.DrawText(g, tip, HintFont, hint, Color.FromArgb(138, 151, 178),
                    TextFormatFlags.NoPrefix | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
            }
        }

        /// <summary>Chiếu 1 điểm (đã trừ tâm) theo góc xoay hiện tại ra tọa độ màn hình.</summary>
        private PointF Project(float px, float py, float pz, float scale, float ox, float oy)
        {
            float cyw = (float)Math.Cos(_yaw), syw = (float)Math.Sin(_yaw);
            float cp = (float)Math.Cos(_pitch), sp = (float)Math.Sin(_pitch);
            float x1 = px * cyw - py * syw;
            float y1 = px * syw + py * cyw;
            float up = pz * cp + y1 * sp;
            return new PointF(ox + x1 * scale, oy - up * scale);
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

            // Đường nét (đường mô hình / ký hiệu / Family chỉ có nét): vẽ đè lên
            if (_segCount > 0)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen pen = new Pen(_triCount > 0 ? Color.FromArgb(150, 186, 230, 255) : Color.FromArgb(150, 214, 255), 1.3f))
                {
                    for (int i = 0; i < _segCount; i++)
                    {
                        int b = i * 6;
                        PointF a = Project(_seg[b] - _cx, _seg[b + 1] - _cy, _seg[b + 2] - _cz, scale, ox, oy);
                        PointF c = Project(_seg[b + 3] - _cx, _seg[b + 4] - _cy, _seg[b + 5] - _cz, scale, ox, oy);
                        g.DrawLine(pen, a, c);
                    }
                }
            }
        }

        /// <summary>Family 2D: ảnh xem trước dựng thành tấm phẳng đứng, xoay/phóng/dời như hình 3D (chiếu song song = hình bình hành).</summary>
        private void DrawImage(Graphics g, Rectangle rc)
        {
            float iw = _image.Width, ih = _image.Height;
            float half = (float)Math.Sqrt(iw * iw + ih * ih) / 2f;
            if (half < 1f) half = 1f;
            float scale = Math.Min(rc.Width, rc.Height - 20) * 0.5f / half * _zoom;
            float ox = rc.Width / 2f + _panX, oy = (rc.Height - 16) / 2f + _panY;
            // Tấm nằm trong mặt X-Z, nhìn chính diện khi yaw = 0, pitch = 0
            float yaw0 = _yaw, pitch0 = _pitch;
            _yaw = yaw0 - DefaultYaw; _pitch = pitch0 - DefaultPitch;
            PointF tl = Project(-iw / 2f, 0, ih / 2f, scale, ox, oy);
            PointF tr = Project(iw / 2f, 0, ih / 2f, scale, ox, oy);
            PointF bl = Project(-iw / 2f, 0, -ih / 2f, scale, ox, oy);
            _yaw = yaw0; _pitch = pitch0;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            try { g.DrawImage(_image, new PointF[] { tl, tr, bl }); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Vẽ ảnh xoay lỗi: " + ex.Message); }
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
