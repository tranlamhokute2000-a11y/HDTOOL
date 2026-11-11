using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Color = System.Drawing.Color;
using Font = System.Drawing.Font;
using Panel = System.Windows.Forms.Panel;
using Button = System.Windows.Forms.Button;
using CheckBox = System.Windows.Forms.CheckBox;

namespace HDTool
{
    /// <summary>
    /// Giao diện Modeless Dialog Tự Động Tạo & Căn Chỉnh Crop View (Auto Crop Box)
    /// Đảm bảo 100% thông tin, nhãn chữ, nút bấm hiển thị đầy đủ, không bao giờ bị che khuất
    /// Tuân thủ nghiêm ngặt Quy Tắc Giao Diện theo lệnh Sếp Hồ (HadoMEE).
    /// </summary>
    public class AutoCropForm : System.Windows.Forms.Form
    {
        private UIDocument _uidoc;
        private Document _doc;
        private ExternalEvent _externalEvent;
        private HDToolExternalEventHandler _eventHandler;

        private Label lblViewName;
        private Label lblElemCount;
        private CheckedListBox lstSheetViewports;
        private Button btnSelectAllVp;
        private Button btnUnselectAllVp;
        private GroupBox grpInfo;

        private NumericUpDown numPadding;
        private TrackBar trackPadding;
        private CheckBox chkInsideCurrentCrop;
        private CheckBox chkIgnoreLinks;
        private CheckBox chkMepOnly;
        private CheckBox chkVisible;
        private CheckBox chkSectionBox;

        private Button btnApply;
        private Button btnPickElements;
        private Button btnRefresh;
        private Button btnClose;
        private Label lblStatus;

        private List<Autodesk.Revit.DB.View> _sheetViews = new List<Autodesk.Revit.DB.View>();
        private ICollection<ElementId> _pickedIds = null;

        public AutoCropForm(UIDocument uidoc, ExternalEvent externalEvent = null, HDToolExternalEventHandler eventHandler = null)
        {
            _uidoc = uidoc;
            _doc = uidoc.Document;
            _externalEvent = externalEvent;
            _eventHandler = eventHandler;

            InitializeComponent();
            RefreshViewInfo();
            this.Activated += (s, e) => { RefreshViewInfo(); };
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.Text = "HDTOOL — TỰ ĐỘNG TẠO & CĂN CHỈNH CROP VIEW (AUTO CROP BOX)";
            // Dùng ClientSize đảm bảo không bị thanh tiêu đề Windows trừ bớt chiều cao
            this.ClientSize = new Size(560, 680);
            this.MinimumSize = new Size(560, 680);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            this.BackColor = Color.FromArgb(15, 23, 42); // Slate 900
            this.ForeColor = Color.FromArgb(248, 250, 252);
            this.KeyPreview = true;
            this.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) this.Close(); };

            // 1. HEADER BRANDING
            Panel pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 65,
                BackColor = Color.FromArgb(24, 43, 73), // HadoMEE Navy
                Padding = new Padding(16, 10, 16, 10)
            };

            Label lblTitle = new Label
            {
                Text = "★ TỰ ĐỘNG TẠO CROP VIEW ÔM KHÍT ĐỐI TƯỢNG ★",
                Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(245, 158, 11), // Amber 500
                AutoSize = true,
                Location = new System.Drawing.Point(14, 10)
            };

            Label lblSub = new Label
            {
                Text = "Hỗ trợ View 3D SĐKG, Mặt bằng, Mặt cắt & Tự động Crop hàng loạt Viewport trên Sheet",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(203, 213, 225),
                AutoSize = true,
                Location = new System.Drawing.Point(16, 36)
            };

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSub);
            this.Controls.Add(pnlHeader);

            // 2. GROUP THÔNG TIN VIEW / VIEWPORT TRÊN SHEET
            grpInfo = new GroupBox
            {
                Text = " Khung Nhìn / Danh Sách Viewport Đang Mở ",
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                ForeColor = Color.FromArgb(56, 189, 248), // Sky 400
                Location = new System.Drawing.Point(16, 74),
                Size = new Size(528, 165)
            };

            lblViewName = new Label
            {
                Text = "Khung nhìn: Đang phát hiện...",
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new System.Drawing.Point(14, 22),
                Size = new Size(410, 22)
            };

            btnRefresh = new Button
            {
                Text = "↻ Làm Mới",
                Font = new Font("Segoe UI", 8.0f, FontStyle.Bold),
                Size = new Size(84, 25),
                Location = new System.Drawing.Point(430, 19),
                BackColor = Color.FromArgb(30, 41, 59),
                ForeColor = Color.FromArgb(56, 189, 248),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnRefresh.FlatAppearance.BorderSize = 1;
            btnRefresh.FlatAppearance.BorderColor = Color.FromArgb(56, 189, 248);
            btnRefresh.Click += (s, e) => { RefreshViewInfo(); };

            lblElemCount = new Label
            {
                Text = "Đối tượng mô hình: 0",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(52, 211, 153), // Emerald 400
                Location = new System.Drawing.Point(14, 46),
                Size = new Size(495, 20)
            };

            lstSheetViewports = new CheckedListBox
            {
                Location = new System.Drawing.Point(14, 68),
                Size = new Size(500, 64),
                BackColor = Color.FromArgb(30, 41, 59),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                CheckOnClick = true,
                BorderStyle = BorderStyle.FixedSingle
            };

            btnSelectAllVp = new Button
            {
                Text = "✔ Chọn Hết",
                Font = new Font("Segoe UI", 8.0f, FontStyle.Bold),
                Size = new Size(95, 24),
                Location = new System.Drawing.Point(14, 135),
                BackColor = Color.FromArgb(30, 41, 59),
                ForeColor = Color.FromArgb(52, 211, 153),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnSelectAllVp.FlatAppearance.BorderSize = 0;
            btnSelectAllVp.Click += (s, e) =>
            {
                for (int i = 0; i < lstSheetViewports.Items.Count; i++) lstSheetViewports.SetItemChecked(i, true);
            };

            btnUnselectAllVp = new Button
            {
                Text = "✖ Bỏ Chọn",
                Font = new Font("Segoe UI", 8.0f, FontStyle.Bold),
                Size = new Size(95, 24),
                Location = new System.Drawing.Point(115, 135),
                BackColor = Color.FromArgb(30, 41, 59),
                ForeColor = Color.FromArgb(248, 113, 113),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnUnselectAllVp.FlatAppearance.BorderSize = 0;
            btnUnselectAllVp.Click += (s, e) =>
            {
                for (int i = 0; i < lstSheetViewports.Items.Count; i++) lstSheetViewports.SetItemChecked(i, false);
            };

            grpInfo.Controls.Add(lblViewName);
            grpInfo.Controls.Add(btnRefresh);
            grpInfo.Controls.Add(lblElemCount);
            grpInfo.Controls.Add(lstSheetViewports);
            grpInfo.Controls.Add(btnSelectAllVp);
            grpInfo.Controls.Add(btnUnselectAllVp);
            this.Controls.Add(grpInfo);

            // 3. GROUP THIẾT LẬP KHOẢNG ĐỆM (PADDING / MARGIN)
            GroupBox grpPadding = new GroupBox
            {
                Text = " Khoảng Đệm Viền Crop (Padding / Margin) ",
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                ForeColor = Color.FromArgb(251, 191, 36), // Amber 400
                Location = new System.Drawing.Point(16, 248),
                Size = new Size(528, 132)
            };

            Label lblPadLabel = new Label
            {
                Text = "Khoảng cách đệm viền ngoài mô hình (mm):",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(226, 232, 240),
                Location = new System.Drawing.Point(14, 25),
                AutoSize = true
            };

            numPadding = new NumericUpDown
            {
                Minimum = 10,
                Maximum = 5000,
                Value = 150,
                Increment = 10,
                Font = new Font("Segoe UI", 10.0f, FontStyle.Bold),
                BackColor = Color.FromArgb(30, 41, 59),
                ForeColor = Color.FromArgb(52, 211, 153),
                Location = new System.Drawing.Point(375, 21),
                Width = 90,
                TextAlign = HorizontalAlignment.Right
            };

            Label lblUnit = new Label
            {
                Text = "mm",
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                ForeColor = Color.FromArgb(148, 163, 184),
                Location = new System.Drawing.Point(470, 24),
                AutoSize = true
            };

            // Fix lỗi che khuất: tắt AutoSize và khống chế chiều cao TrackBar
            trackPadding = new TrackBar
            {
                AutoSize = false,
                Height = 26,
                Minimum = 20,
                Maximum = 600,
                Value = 150,
                TickFrequency = 50,
                Location = new System.Drawing.Point(14, 52),
                Size = new Size(498, 26)
            };

            numPadding.ValueChanged += (s, e) =>
            {
                int val = (int)numPadding.Value;
                if (val >= trackPadding.Minimum && val <= trackPadding.Maximum) trackPadding.Value = val;
            };
            trackPadding.Scroll += (s, e) => { numPadding.Value = trackPadding.Value; };

            // Panel các nút chọn nhanh mm (cách hẳn TrackBar 10px, hiển thị to rõ 100% không bị che)
            Panel pnlQuick = new Panel
            {
                Location = new System.Drawing.Point(14, 88),
                Size = new Size(498, 32)
            };

            int[] quickVals = new int[] { 50, 100, 150, 200, 300, 500 };
            int qx = 0;
            foreach (int qv in quickVals)
            {
                Button btnQ = new Button
                {
                    Text = string.Format("{0}mm", qv),
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    Size = new Size(74, 28),
                    Location = new System.Drawing.Point(qx, 0),
                    BackColor = qv == 150 ? Color.FromArgb(16, 185, 129) : Color.FromArgb(30, 41, 59),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand
                };
                btnQ.FlatAppearance.BorderSize = 1;
                btnQ.FlatAppearance.BorderColor = (qv == 150) ? Color.FromArgb(52, 211, 153) : Color.FromArgb(71, 85, 105);
                int currentQv = qv;
                btnQ.Click += (s, e) => { numPadding.Value = currentQv; };
                pnlQuick.Controls.Add(btnQ);
                qx += 82;
            }

            grpPadding.Controls.Add(lblPadLabel);
            grpPadding.Controls.Add(numPadding);
            grpPadding.Controls.Add(lblUnit);
            grpPadding.Controls.Add(trackPadding);
            grpPadding.Controls.Add(pnlQuick);
            this.Controls.Add(grpPadding);

            // 4. TÙY CHỌN LỌC ĐỐI TƯỢNG & CROP (BỐ TRÍ CÁCH NHAU RÕ RÀNG 100% THEO RULE SẾP HỒ)
            chkInsideCurrentCrop = new CheckBox
            {
                Text = "Tự động hiểu ý: Chỉ ôm đối tượng trong phạm vi Crop hiện tại (Không dãn ra ngoài)",
                Checked = true,
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                ForeColor = Color.FromArgb(56, 189, 248), // Sky 400
                Location = new System.Drawing.Point(20, 388),
                AutoSize = true
            };

            chkIgnoreLinks = new CheckBox
            {
                Text = "Bỏ qua File Link Kiến Trúc / Kết Cấu (Chỉ ôm đối tượng dự án hiện tại)",
                Checked = true,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 211, 153), // Emerald 400
                Location = new System.Drawing.Point(20, 413),
                AutoSize = true
            };

            chkMepOnly = new CheckBox
            {
                Text = "Ưu tiên ôm khít hệ thống MEP (Ống nước, ống gió, thiết bị - Cắt gọn nền sàn/vách)",
                Checked = true,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(251, 191, 36), // Amber 400
                Location = new System.Drawing.Point(20, 438),
                AutoSize = true
            };

            chkVisible = new CheckBox
            {
                Text = "Hiển thị đường viền Crop View (Crop Region Visible)",
                Checked = true,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(226, 232, 240),
                Location = new System.Drawing.Point(20, 463),
                AutoSize = true
            };

            chkSectionBox = new CheckBox
            {
                Text = "Cắt lại hộp 3D Section Box theo đối tượng (Mặc định giữ nguyên Section Box của Sếp)",
                Checked = false,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(148, 163, 184),
                Location = new System.Drawing.Point(20, 488),
                AutoSize = true
            };

            this.Controls.Add(chkInsideCurrentCrop);
            this.Controls.Add(chkIgnoreLinks);
            this.Controls.Add(chkMepOnly);
            this.Controls.Add(chkVisible);
            this.Controls.Add(chkSectionBox);

            // 5. NÚT APPLY TO MÀU ĐỎ RỰC RỠ
            btnApply = new Button
            {
                Text = "⚡ TỰ ĐỘNG FIT CROP VIEW (ÔM KHÍT ĐỐI TƯỢNG)",
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                Size = new Size(528, 44),
                Location = new System.Drawing.Point(16, 516),
                BackColor = Color.FromArgb(225, 29, 72), // Rose 600
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnApply.FlatAppearance.BorderSize = 0;
            btnApply.Click += (s, e) => { DoExecuteAutoCrop(); };
            this.Controls.Add(btnApply);

            // Nút Pick & Đóng
            btnPickElements = new Button
            {
                Text = "🎯 Pick Chọn Cụm Đối Tượng / Viewport",
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                Size = new Size(260, 34),
                Location = new System.Drawing.Point(16, 566),
                BackColor = Color.FromArgb(14, 116, 144), // Cyan 700
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnPickElements.FlatAppearance.BorderSize = 0;
            btnPickElements.Click += (s, e) => { DoPickElements(); };
            this.Controls.Add(btnPickElements);

            btnClose = new Button
            {
                Text = "Đóng (ESC)",
                Font = new Font("Segoe UI", 9.0f, FontStyle.Regular),
                Size = new Size(260, 34),
                Location = new System.Drawing.Point(284, 566),
                BackColor = Color.FromArgb(51, 65, 85), // Slate 700
                ForeColor = Color.FromArgb(203, 213, 225),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => { this.Close(); };
            this.Controls.Add(btnClose);

            // 6. STATUS BAR THÔNG BÁO RÕ RÀNG KHÔNG BỊ TRÀN VIỀN
            lblStatus = new Label
            {
                Text = "Sẵn sàng. Bấm nút đỏ phía trên để Tự Động Fit Crop View ngay!",
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                ForeColor = Color.FromArgb(56, 189, 248),
                Location = new System.Drawing.Point(16, 608),
                Size = new Size(528, 48)
            };
            this.Controls.Add(lblStatus);

            this.ResumeLayout(false);
        }

        private void SetStatusSafely(string msg, bool ok)
        {
            if (this.IsDisposed) return;
            Action applyUi = () =>
            {
                if (this.IsDisposed) return;
                lblStatus.ForeColor = ok ? Color.FromArgb(52, 211, 153) : Color.FromArgb(248, 113, 113);
                lblStatus.Text = msg;
            };
            if (this.InvokeRequired)
            {
                try { this.Invoke(applyUi); } catch { }
            }
            else
            {
                applyUi();
            }
        }

        private void RefreshViewInfo()
        {
            try
            {
                if (_uidoc == null || _uidoc.ActiveView == null) return;
                Autodesk.Revit.DB.View v = _uidoc.ActiveView;
                _doc = _uidoc.Document;

                lstSheetViewports.Items.Clear();
                _sheetViews.Clear();

                if (v is ViewSheet)
                {
                    ViewSheet vs = v as ViewSheet;
                    lblViewName.Text = string.Format("Trang in (Sheet): {0} - {1}", vs.SheetNumber, vs.Name);

                    List<Autodesk.Revit.DB.View> vpViews = AutoCropService.GetCropableViewsOnSheet(_doc, vs);
                    ICollection<ElementId> selIds = _uidoc.Selection.GetElementIds();
                    List<ElementId> selectedViewIds = new List<ElementId>();

                    if (selIds != null && selIds.Count > 0)
                    {
                        foreach (ElementId id in selIds)
                        {
                            Viewport vp = _doc.GetElement(id) as Viewport;
                            if (vp != null) selectedViewIds.Add(vp.ViewId);
                        }
                    }

                    foreach (Autodesk.Revit.DB.View vpView in vpViews)
                    {
                        _sheetViews.Add(vpView);
                        bool isChecked = (selectedViewIds.Count == 0) || selectedViewIds.Contains(vpView.Id);
                        lstSheetViewports.Items.Add(
                            string.Format("{0}  [{1}] (1:{2})", vpView.Name, vpView.ViewType.ToString(), vpView.Scale),
                            isChecked
                        );
                    }

                    lstSheetViewports.Enabled = true;
                    btnSelectAllVp.Enabled = true;
                    btnUnselectAllVp.Enabled = true;

                    lblElemCount.Text = selectedViewIds.Count > 0
                        ? string.Format("Đang chọn {0} Viewport trên Sheet (hoặc tích chọn thêm ở dưới):", selectedViewIds.Count)
                        : string.Format("Phát hiện {0} Viewport trên Sheet (Đã tích chọn sẵn tất cả để Fit cùng lúc!):", vpViews.Count);
                }
                else
                {
                    lblViewName.Text = string.Format("Khung nhìn: {0} ({1})", v.Name, v.ViewType.ToString());
                    List<Element> list = AutoCropService.GetModelElementsInView(_doc, v, null, chkIgnoreLinks.Checked, chkMepOnly.Checked);
                    lblElemCount.Text = string.Format("Đối tượng mô hình nhận diện: {0} phần tử", list != null ? list.Count : 0);

                    lstSheetViewports.Items.Add(string.Format("✔ Áp dụng cho View hiện hành: {0}", v.Name), true);
                    lstSheetViewports.Enabled = false;
                    btnSelectAllVp.Enabled = false;
                    btnUnselectAllVp.Enabled = false;
                }
            }
            catch { }
        }

        private void DoExecuteAutoCrop()
        {
            double pad = (double)numPadding.Value;
            bool secBox = chkSectionBox.Checked;
            bool vis = chkVisible.Checked;
            bool ignoreLinks = chkIgnoreLinks.Checked;
            bool mepOnly = chkMepOnly.Checked;
            bool onlyInside = chkInsideCurrentCrop.Checked;
            ICollection<ElementId> sel = _pickedIds;

            // Lưu lại danh sách view được check trên Sheet
            List<ElementId> checkedViewIds = new List<ElementId>();
            if (_uidoc != null && _uidoc.ActiveView is ViewSheet)
            {
                for (int i = 0; i < lstSheetViewports.Items.Count; i++)
                {
                    if (lstSheetViewports.GetItemChecked(i) && i < _sheetViews.Count)
                    {
                        checkedViewIds.Add(_sheetViews[i].Id);
                    }
                }
            }

            Action<UIApplication> doAction = (uiapp) =>
            {
                UIDocument activeUidoc = (uiapp != null && uiapp.ActiveUIDocument != null) ? uiapp.ActiveUIDocument : _uidoc;
                _uidoc = activeUidoc;
                _doc = activeUidoc.Document;

                string msg = string.Empty;
                bool ok = false;

                if (activeUidoc.ActiveView is ViewSheet && checkedViewIds.Count > 0)
                {
                    List<Autodesk.Revit.DB.View> targets = new List<Autodesk.Revit.DB.View>();
                    foreach (ElementId vid in checkedViewIds)
                    {
                        Autodesk.Revit.DB.View tv = _doc.GetElement(vid) as Autodesk.Revit.DB.View;
                        if (tv != null) targets.Add(tv);
                    }
                    int count = AutoCropService.ApplyAutoCropToViews(activeUidoc, targets, out msg, pad, ignoreLinks, mepOnly, onlyInside, secBox, vis, null);
                    ok = count > 0;
                }
                else
                {
                    ok = AutoCropService.ApplyAutoCrop(activeUidoc, out msg, pad, secBox, vis, sel, ignoreLinks, mepOnly, onlyInside);
                }

                _pickedIds = null;
                SetStatusSafely(msg, ok);
                if (!ok)
                {
                    TaskDialog.Show("HDTOOL — Tự Động Crop View", msg);
                }
            };

            if (_externalEvent != null && _eventHandler != null)
            {
                SetStatusSafely("⏳ Đang tính toán và cập nhật Crop View trong Revit...", true);
                _eventHandler.Run(doAction, _externalEvent);
            }
            else
            {
                doAction(null);
            }
        }

        private void DoPickElements()
        {
            Action<UIApplication> doPick = (uiapp) =>
            {
                UIDocument activeUidoc = (uiapp != null && uiapp.ActiveUIDocument != null) ? uiapp.ActiveUIDocument : _uidoc;
                _uidoc = activeUidoc;
                _doc = activeUidoc.Document;

                try { if (this.InvokeRequired) this.Invoke(new Action(() => this.Hide())); else this.Hide(); } catch { }

                try
                {
                    if (activeUidoc.ActiveView is ViewSheet)
                    {
                        IList<Reference> vpRefs = activeUidoc.Selection.PickObjects(
                            ObjectType.Element,
                            "HDTOOL: Click chọn 1 hoặc nhiều Viewport trên Sheet rồi nhấn Finish:"
                        );
                        if (vpRefs != null && vpRefs.Count > 0)
                        {
                            List<Autodesk.Revit.DB.View> targets = new List<Autodesk.Revit.DB.View>();
                            foreach (Reference r in vpRefs)
                            {
                                Viewport vp = _doc.GetElement(r.ElementId) as Viewport;
                                if (vp != null)
                                {
                                    Autodesk.Revit.DB.View tv = _doc.GetElement(vp.ViewId) as Autodesk.Revit.DB.View;
                                    if (tv != null && !targets.Any(x => x.Id == tv.Id)) targets.Add(tv);
                                }
                            }
                            if (targets.Count > 0)
                            {
                                string msg;
                                int cnt = AutoCropService.ApplyAutoCropToViews(
                                    activeUidoc, targets, out msg,
                                    (double)numPadding.Value, chkIgnoreLinks.Checked, chkMepOnly.Checked, chkInsideCurrentCrop.Checked, chkSectionBox.Checked, chkVisible.Checked, null
                                );
                                SetStatusSafely(msg, cnt > 0);
                            }
                        }
                    }
                    else
                    {
                        IList<Reference> refs = activeUidoc.Selection.PickObjects(
                            ObjectType.Element,
                            "HDTOOL: Quét chọn cụm đối tượng cần tạo Crop View bao quanh rồi nhấn Finish:"
                        );

                        if (refs != null && refs.Count > 0)
                        {
                            List<ElementId> ids = new List<ElementId>();
                            foreach (Reference r in refs) ids.Add(r.ElementId);
                            _pickedIds = ids;

                            string msg;
                            bool ok = AutoCropService.ApplyAutoCrop(
                                activeUidoc, out msg,
                                (double)numPadding.Value, chkSectionBox.Checked, chkVisible.Checked, _pickedIds, chkIgnoreLinks.Checked, chkMepOnly.Checked, chkInsideCurrentCrop.Checked
                            );
                            _pickedIds = null;
                            SetStatusSafely(msg, ok);
                        }
                    }
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    SetStatusSafely("Đã hủy thao tác chọn đối tượng.", false);
                }
                catch (Exception ex)
                {
                    SetStatusSafely("Lỗi Pick: " + ex.Message, false);
                }
                finally
                {
                    try { if (this.InvokeRequired) this.Invoke(new Action(() => this.Show())); else this.Show(); } catch { }
                }
            };

            if (_externalEvent != null && _eventHandler != null)
            {
                _eventHandler.Run(doPick, _externalEvent);
            }
            else
            {
                doPick(null);
            }
        }
    }
}
