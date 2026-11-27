using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Autodesk.Revit.UI;

namespace HDTool
{
    /// <summary>
    /// Trình xử lý ExternalEvent dùng chung cho tất cả các Tool Modeless của HDTOOL
    /// Cho phép chạy mọi tác vụ Revit API (Transaction, Pick, Selection, Zoom...)
    /// hoàn toàn mượt mà trong khi người dùng vẫn thao tác chuột, pan/zoom bản vẽ 60 FPS
    /// </summary>
    public class HDToolExternalEventHandler : IExternalEventHandler
    {
        private readonly Queue<Action<UIApplication>> _actionQueue = new Queue<Action<UIApplication>>();
        private readonly object _lock = new object();

        public void Run(Action<UIApplication> action, ExternalEvent exEvent)
        {
            if (action == null || exEvent == null) return;
            lock (_lock)
            {
                _actionQueue.Enqueue(action);
            }
            exEvent.Raise();
        }

        public void Execute(UIApplication app)
        {
            List<Action<UIApplication>> actionsToRun = new List<Action<UIApplication>>();
            lock (_lock)
            {
                while (_actionQueue.Count > 0)
                {
                    actionsToRun.Add(_actionQueue.Dequeue());
                }
            }

            foreach (var act in actionsToRun)
            {
                try
                {
                    act(app);
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("HDTOOL", "Lỗi tác vụ: " + ex.Message);
                }
            }
        }

        public string GetName()
        {
            return "HDToolExternalEventHandler";
        }
    }

    /// <summary>
    /// Trình quản lý Modeless Host toàn cục của HDTOOL:
    /// Đảm bảo tất cả các cửa sổ công cụ khi mở lên đều cho phép người dùng
    /// tự do tương tác, pan/zoom, chọn đối tượng trên bản vẽ Revit mà không bị khóa.
    /// Mọi lệnh can thiệp dữ liệu Revit API (Transaction, Pick) đều được nạp vào ExternalEvent an toàn.
    /// </summary>
    public static class HDToolModelessHost
    {
        private static HDToolExternalEventHandler _sharedHandler = null;
        private static ExternalEvent _sharedExternalEvent = null;

        public static void EnsureInitialized()
        {
            if (_sharedHandler == null)
            {
                _sharedHandler = new HDToolExternalEventHandler();
            }
            if (_sharedExternalEvent == null)
            {
                try
                {
                    _sharedExternalEvent = ExternalEvent.Create(_sharedHandler);
                }
                catch
                {
                    // Đã khởi tạo hoặc đang trong luồng không có API context
                }
            }
        }

        public static HDToolExternalEventHandler Handler
        {
            get
            {
                EnsureInitialized();
                return _sharedHandler;
            }
        }

        public static ExternalEvent ExEvent
        {
            get
            {
                EnsureInitialized();
                return _sharedExternalEvent;
            }
        }

        public static void Run(Action<UIApplication> action)
        {
            if (action == null) return;
            EnsureInitialized();
            if (_sharedHandler != null && _sharedExternalEvent != null)
            {
                _sharedHandler.Run(action, _sharedExternalEvent);
            }
            else
            {
                // KHÔNG gọi trực tiếp Revit API ngoài API context (Revit sẽ từ chối Transaction/Pick)
                ReportNoApiContext();
            }
        }

        /// <summary>
        /// Thông báo khi không thể đưa tác vụ vào ExternalEvent (không được gọi Revit API trực tiếp từ Form Modeless).
        /// </summary>
        public static void ReportNoApiContext()
        {
            MessageBox.Show(
                "Không thể thực hiện thao tác lên mô hình Revit lúc này (ExternalEvent chưa sẵn sàng).\n\nVui lòng đóng cửa sổ công cụ và mở lại từ thanh Ribbon HDTOOL.",
                "HDTOOL - Thông Báo",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        /// <summary>
        /// Trả về đúng Document gốc mà cửa sổ công cụ đang làm việc.
        /// Form Modeless có thể vẫn mở khi người dùng chuyển sang dự án khác: KHÔNG được ghi vào ActiveUIDocument
        /// (sẽ sửa nhầm dự án). Trả về null nếu dự án gốc đã bị đóng.
        /// </summary>
        public static Autodesk.Revit.DB.Document ResolveOwnerDocument(Autodesk.Revit.DB.Document ownerDoc)
        {
            try
            {
                if (ownerDoc != null && ownerDoc.IsValidObject) return ownerDoc;
            }
            catch { }
            return null;
        }

        public static void EnableEscToClose(Form form, Button cancelButton = null)
        {
            if (form == null || form.IsDisposed) return;

            form.KeyPreview = true;
            if (cancelButton != null && form.CancelButton == null)
            {
                form.CancelButton = cancelButton;
            }

            if (form is HDToolBaseForm) return;
            if (object.Equals(form.Tag, "HDTOOL_ESC_BOUND")) return;

            if (form.Tag == null)
            {
                form.Tag = "HDTOOL_ESC_BOUND";
            }

            form.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    if (form.Modal)
                    {
                        form.DialogResult = DialogResult.Cancel;
                    }
                    form.Close();
                }
            };
        }

        public static void ShowModeless(Form form, IntPtr revitMainHwnd)
        {
            if (form == null || form.IsDisposed) return;

            EnableEscToClose(form);

            if (form.WindowState == FormWindowState.Minimized)
            {
                form.WindowState = FormWindowState.Normal;
            }

            if (!form.Visible)
            {
                if (revitMainHwnd != IntPtr.Zero)
                {
                    form.Show(new RevitWindowWrapper(revitMainHwnd));
                }
                else
                {
                    form.Show();
                }
            }

            form.BringToFront();
            form.Activate();
        }
    }

    /// <summary>
    /// Bộ lọc thông điệp Windows toàn cục: Bắt phím ESC ở cấp Message Loop trước khi bất kỳ Control nào
    /// (TextBox, DataGridView, ComboBox...) kịp nuốt phím, đảm bảo 100% ấn ESC là thoát lệnh ngay lập tức.
    /// </summary>
    public class HDToolEscapeMessageFilter : IMessageFilter
    {
        private static HDToolEscapeMessageFilter _instance;
        private static bool _registered = false;
        private static readonly object _filterLock = new object();

        public static void EnsureRegistered()
        {
            if (!_registered)
            {
                lock (_filterLock)
                {
                    if (!_registered)
                    {
                        try
                        {
                            if (_instance == null) _instance = new HDToolEscapeMessageFilter();
                            Application.AddMessageFilter(_instance);
                            _registered = true;
                        }
                        catch { }
                    }
                }
            }
        }

        public bool PreFilterMessage(ref Message m)
        {
            const int WM_KEYDOWN = 0x0100;
            const int VK_ESCAPE = 0x1B;

            if (m.Msg == WM_KEYDOWN && (int)m.WParam == VK_ESCAPE)
            {
                // Kiểm tra Form đang Active
                Form activeForm = Form.ActiveForm;
                if (activeForm is HDToolBaseForm)
                {
                    try
                    {
                        if (activeForm.Modal)
                        {
                            activeForm.DialogResult = DialogResult.Cancel;
                        }
                        activeForm.Close();
                        return true;
                    }
                    catch { }
                }

                // Quét trong Application.OpenForms nếu Form chứa Focus
                FormCollection openForms = Application.OpenForms;
                if (openForms != null)
                {
                    for (int i = openForms.Count - 1; i >= 0; i--)
                    {
                        Form f = openForms[i];
                        if (f is HDToolBaseForm && f.Visible && (f.ContainsFocus || f.Focused))
                        {
                            try
                            {
                                if (f.Modal)
                                {
                                    f.DialogResult = DialogResult.Cancel;
                                }
                                f.Close();
                                return true;
                            }
                            catch { }
                        }
                    }
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Lớp Form chuẩn toàn cục của HDTOOL (Modeless & Modal):
    /// Mọi cửa sổ công cụ hiện tại và phát triển mới sau này BẮT BUỘC kế thừa lớp này
    /// để luôn hỗ trợ ấn phím ESC thoát lệnh ngay lập tức (không cần di chuột bấm Đóng/X).
    /// </summary>
    public class HDToolBaseForm : System.Windows.Forms.Form
    {
        public HDToolBaseForm()
        {
            this.KeyPreview = true;
            // Tự co giãn bố cục theo DPI Windows (125% / 150%...): kích thước control tính theo 96 DPI sẽ được nhân đúng tỷ lệ,
            // khớp với font (pt) vốn đã tự to lên -> không còn chữ bị cắt / đè.
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = AutoScaleMode.Dpi;
            HDToolEscapeMessageFilter.EnsureRegistered();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            try { this.PerformAutoScale(); } catch { }
            // Thanh tiêu đề kiểu Mac (3 nút tròn) - chỉ khi theme Mac Sáng
            if (HDToolTheme.IsMac) ApplyMacChrome();
            // Khoác giao diện (HDToolTheme) cho toàn bộ control của Form
            HDToolTheme.ApplyTo(this);
        }

        // ===================== THANH TIÊU ĐỀ KIỂU MAC (Version 113) =====================
        // Form chuyển sang không viền, nội dung dời xuống đúng chiều cao thanh tiêu đề tự vẽ:
        // control Dock -> qua Padding.Top; control tọa độ tự do -> dời Top theo Anchor. Lỗi -> trả lại viền Windows.
        private int _macTitleH;
        private bool _macSizable;
        private int _macHot = -1; // 0 đóng, 1 thu nhỏ, 2 phóng to
        private const int MacGrip = 6;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public int MacTitleHeight { get { return _macTitleH; } }

        private float MacScale()
        {
            try { using (System.Drawing.Graphics g = CreateGraphics()) return g.DpiX / 96f; } catch { return 1f; }
        }

        private void ApplyMacChrome()
        {
            if (_macTitleH > 0 || FormBorderStyle == FormBorderStyle.None) return;
            System.Drawing.Size client = ClientSize;
            FormBorderStyle oldStyle = FormBorderStyle;
            try
            {
                int th = (int)Math.Round(38 * MacScale());
                _macSizable = oldStyle == FormBorderStyle.Sizable || oldStyle == FormBorderStyle.SizableToolWindow;
                SuspendLayout();
                FormBorderStyle = FormBorderStyle.None;
                ClientSize = new System.Drawing.Size(client.Width, client.Height + th);
                if (MinimumSize.Height > 0) MinimumSize = new System.Drawing.Size(MinimumSize.Width, MinimumSize.Height + th);
                foreach (Control c in Controls)
                {
                    if (c.Dock != DockStyle.None) continue;
                    bool top = (c.Anchor & AnchorStyles.Top) != 0, bottom = (c.Anchor & AnchorStyles.Bottom) != 0;
                    if (top)
                    {
                        c.Top += th;
                        if (bottom) c.Height -= th;
                    }
                    else if (!bottom)
                    {
                        c.Top += th / 2;
                    }
                }
                int g = _macSizable ? 2 : 1;
                Padding = new Padding(Padding.Left + g, Padding.Top + th, Padding.Right + g, Padding.Bottom + g);
                _macTitleH = th;
                SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
                ResumeLayout(true);
                try
                {
                    int round = 2; // DWMWCP_ROUND (Windows 11)
                    DwmSetWindowAttribute(Handle, 33, ref round, 4);
                    System.Drawing.Color bc = HDToolTheme.LineStrong;
                    int border = bc.R | (bc.G << 8) | (bc.B << 16);
                    DwmSetWindowAttribute(Handle, 34, ref border, 4);
                }
                catch { }
                Invalidate();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Mac chrome lỗi, trả lại viền Windows: " + ex.Message);
                _macTitleH = 0;
                try { FormBorderStyle = oldStyle; ClientSize = client; ResumeLayout(true); } catch { }
            }
        }

        private System.Drawing.Rectangle MacLightRect(int i)
        {
            float s = MacScale();
            int d = (int)Math.Round(12 * s), gap = (int)Math.Round(8 * s), x0 = (int)Math.Round(14 * s);
            return new System.Drawing.Rectangle(x0 + i * (d + gap), (_macTitleH - d) / 2, d, d);
        }

        private bool MacLightEnabled(int i)
        {
            return i == 0 || (i == 1 && MinimizeBox) || (i == 2 && MaximizeBox && _macSizable);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_macTitleH <= 0) return;
            System.Drawing.Graphics g = e.Graphics;
            int w = ClientSize.Width;
            using (System.Drawing.SolidBrush b = new System.Drawing.SolidBrush(HDToolTheme.Header))
                g.FillRectangle(b, 0, 0, w, _macTitleH);
            using (System.Drawing.Pen p = new System.Drawing.Pen(HDToolTheme.HeaderLine))
                g.DrawLine(p, 0, _macTitleH - 1, w, _macTitleH - 1);

            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            System.Drawing.Color[] fill = {
                System.Drawing.Color.FromArgb(255, 95, 87), System.Drawing.Color.FromArgb(254, 188, 46), System.Drawing.Color.FromArgb(40, 200, 64) };
            bool hoverAny = _macHot >= 0;
            for (int i = 0; i < 3; i++)
            {
                System.Drawing.Rectangle r = MacLightRect(i);
                System.Drawing.Color c = MacLightEnabled(i) ? fill[i] : System.Drawing.Color.FromArgb(206, 206, 210);
                using (System.Drawing.SolidBrush b = new System.Drawing.SolidBrush(c)) g.FillEllipse(b, r);
                using (System.Drawing.Pen p = new System.Drawing.Pen(System.Drawing.Color.FromArgb(40, 0, 0, 0))) g.DrawEllipse(p, r);
                if (hoverAny && MacLightEnabled(i))
                {
                    // Hiện ký hiệu khi rê chuột vào cụm nút (giống macOS)
                    using (System.Drawing.Pen p = new System.Drawing.Pen(System.Drawing.Color.FromArgb(150, 60, 0, 0), Math.Max(1f, r.Width / 9f)))
                    {
                        float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f, k = r.Width * 0.22f;
                        if (i == 0) { g.DrawLine(p, cx - k, cy - k, cx + k, cy + k); g.DrawLine(p, cx - k, cy + k, cx + k, cy - k); }
                        else if (i == 1) g.DrawLine(p, cx - k * 1.2f, cy, cx + k * 1.2f, cy);
                        else { g.DrawLine(p, cx - k * 1.2f, cy, cx + k * 1.2f, cy); g.DrawLine(p, cx, cy - k * 1.2f, cx, cy + k * 1.2f); }
                    }
                }
            }
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;

            int left = MacLightRect(2).Right + 12;
            System.Drawing.Rectangle tr = new System.Drawing.Rectangle(left, 0, Math.Max(10, w - left * 2), _macTitleH);
            using (System.Drawing.Font f = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold))
            {
                TextRenderer.DrawText(g, Text, f, tr, HDToolTheme.HeaderText,
                    TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            }
            using (System.Drawing.Pen p = new System.Drawing.Pen(HDToolTheme.LineStrong))
                g.DrawRectangle(p, 0, 0, w - 1, ClientSize.Height - 1);
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            if (_macTitleH > 0) Invalidate(new System.Drawing.Rectangle(0, 0, ClientSize.Width, _macTitleH));
        }

        private int MacHitLight(System.Drawing.Point pt)
        {
            for (int i = 0; i < 3; i++)
            {
                System.Drawing.Rectangle r = MacLightRect(i);
                r.Inflate(3, 3);
                if (r.Contains(pt)) return i;
            }
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_macTitleH <= 0) return;
            System.Drawing.Rectangle group = System.Drawing.Rectangle.Union(MacLightRect(0), MacLightRect(2));
            group.Inflate(4, 4);
            int hot = group.Contains(e.Location) ? 1 : -1;
            if (hot != _macHot) { _macHot = hot; Invalidate(new System.Drawing.Rectangle(0, 0, group.Right + 4, _macTitleH)); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_macTitleH > 0 && _macHot >= 0) { _macHot = -1; Invalidate(new System.Drawing.Rectangle(0, 0, ClientSize.Width, _macTitleH)); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_macTitleH <= 0 || e.Button != MouseButtons.Left || e.Y >= _macTitleH) return;
            int light = MacHitLight(e.Location);
            if (light >= 0) return; // xử lý ở OnMouseUp
            if (e.Clicks == 2 && MaximizeBox && _macSizable) { ToggleMacMaximize(); return; }
            ReleaseCapture();
            SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero); // WM_NCLBUTTONDOWN + HTCAPTION: kéo cửa sổ
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_macTitleH <= 0 || e.Button != MouseButtons.Left) return;
            int light = MacHitLight(e.Location);
            if (light < 0 || !MacLightEnabled(light)) return;
            if (light == 0) Close();
            else if (light == 1) WindowState = FormWindowState.Minimized;
            else ToggleMacMaximize();
        }

        private void ToggleMacMaximize()
        {
            if (WindowState == FormWindowState.Maximized) { WindowState = FormWindowState.Normal; return; }
            MaximizedBounds = Screen.FromHandle(Handle).WorkingArea;
            WindowState = FormWindowState.Maximized;
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            // Kéo mép để đổi kích thước (cửa sổ không viền)
            if (m.Msg == 0x84 && _macTitleH > 0 && _macSizable && WindowState == FormWindowState.Normal)
            {
                System.Drawing.Point p = PointToClient(new System.Drawing.Point((short)((long)m.LParam & 0xFFFF), (short)(((long)m.LParam >> 16) & 0xFFFF)));
                int w = ClientSize.Width, h = ClientSize.Height;
                bool l = p.X < MacGrip, r = p.X >= w - MacGrip, t = p.Y < 4, b = p.Y >= h - MacGrip;
                if (t && l) m.Result = (IntPtr)13; else if (t && r) m.Result = (IntPtr)14;
                else if (b && l) m.Result = (IntPtr)16; else if (b && r) m.Result = (IntPtr)17;
                else if (l) m.Result = (IntPtr)10; else if (r) m.Result = (IntPtr)11;
                else if (t) m.Result = (IntPtr)12; else if (b) m.Result = (IntPtr)15;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            try
            {
                this.Activate();
                this.Focus();
            }
            catch { }
        }

        /// <summary>
        /// Cho phép Form con thực hiện dọn dẹp trước khi đóng (VD: tắt highlight 3D, hủy ô đang edit).
        /// Dù trả về gì thì sau khi dọn dẹp xong Form VẪN LUÔN LUÔN ĐƯỢC ĐÓNG ngay lập tức khi ấn ESC.
        /// </summary>
        protected virtual bool OnBeforeEscapeClose()
        {
            return false;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if ((keyData & Keys.KeyCode) == Keys.Escape)
            {
                try { OnBeforeEscapeClose(); } catch { }
                if (this.Modal)
                {
                    this.DialogResult = DialogResult.Cancel;
                }
                this.Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                try { OnBeforeEscapeClose(); } catch { }
                if (this.Modal)
                {
                    this.DialogResult = DialogResult.Cancel;
                }
                this.Close();
                e.Handled = true;
                return;
            }
            base.OnKeyDown(e);
        }
    }
}
