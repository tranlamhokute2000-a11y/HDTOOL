using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Color = System.Drawing.Color;
using Point = System.Drawing.Point;

namespace HDTool
{
    public class HDToolMainForm : HDToolBaseForm
    {
        private UIDocument _uidoc;
        private Document _doc;
        private Autodesk.Revit.DB.View _activeView;

        // UI Controls (giao diện kỹ thuật - HDToolTheme)
        private HDToolHeader hdrMain;
        private System.Windows.Forms.Panel pnlContent;
        private System.Windows.Forms.Panel pnlStatus;
        private HDToolStatusText lblStatus;
        private Button btnClose;
        private readonly List<System.Windows.Forms.Control> _flow = new List<System.Windows.Forms.Control>();

        public HDToolMainForm(UIDocument uidoc)
        {
            _uidoc = uidoc;
            _doc = uidoc != null ? uidoc.Document : null;
            _activeView = uidoc != null ? uidoc.ActiveView : null;

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.Text = "HDTOOL — Trung Tâm Điều Khiển";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.TopMost = true;
            this.BackColor = HDToolTheme.Bg;
            this.Font = HDToolTheme.Body;
            this.ClientSize = new Size(560, 760);

            // TẦNG 1: Header 58px
            hdrMain = new HDToolHeader();
            hdrMain.Code = "HDT-00";
            hdrMain.Title = "TRUNG TÂM ĐIỀU KHIỂN";
            hdrMain.Subtitle = "Chọn công cụ cần dùng · mọi cửa sổ đều Modeless";

            // TẦNG 4: Status 52px
            pnlStatus = new System.Windows.Forms.Panel();
            pnlStatus.Dock = DockStyle.Bottom;
            pnlStatus.Height = HDToolTheme.StatusHeight;
            pnlStatus.BackColor = HDToolTheme.Surface2;
            pnlStatus.Paint += (s, e) =>
            {
                using (Pen p = new Pen(HDToolTheme.Line)) e.Graphics.DrawLine(p, 0, 0, pnlStatus.Width, 0);
            };

            btnClose = new Button();
            btnClose.Text = "Đóng (ESC)";
            btnClose.AutoSize = true;
            btnClose.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            btnClose.Padding = new Padding(12, 2, 12, 2);
            btnClose.MinimumSize = new Size(0, 30);
            btnClose.Font = HDToolTheme.Body;
            btnClose.Click += (s, e) => this.Close();
            HDToolTheme.StyleButton(btnClose, HDToolButtonKind.Secondary);
            this.CancelButton = btnClose;

            lblStatus = new HDToolStatusText();
            lblStatus.Text = "SẴN SÀNG · 13 công cụ · HDTOOL";
            pnlStatus.Controls.Add(lblStatus);
            pnlStatus.Controls.Add(btnClose);
            pnlStatus.Resize += (s, e) => LayoutStatus();

            // TẦNG 3: Content (cuộn được, không bao giờ che khuất)
            pnlContent = new System.Windows.Forms.Panel();
            pnlContent.Dock = DockStyle.Fill;
            pnlContent.AutoScroll = true;
            pnlContent.BackColor = HDToolTheme.Bg;
            pnlContent.Padding = new Padding(16, 14, 16, 16);

            // Khung thông tin View đang mở
            HDToolTickPanel pnlInfo = new HDToolTickPanel();
            pnlInfo.Height = 74;
            AddInfoLine(pnlInfo, 0, "VIEW", _activeView != null ? _activeView.Name : "—");
            AddInfoLine(pnlInfo, 1, "LOẠI", _activeView != null ? _activeView.ViewType.ToString() : "—");
            AddInfoLine(pnlInfo, 2, "TỶ LỆ", (_activeView != null && _activeView.Scale > 0) ? ("1 : " + _activeView.Scale) : "—");
            AddFlow(pnlInfo, 16);

            AddSection("01", "Căn chỉnh đối tượng");
            AddGroup(new HDToolToolRow[] {
                MakeRow("grid", "HDT-01A", "Căn Chỉnh Grids", "Căn đầu trục theo khoảng cách mm tới mép Crop View / Sheet", BtnGrids_Click),
                MakeRow("level", "HDT-01B", "Căn Chỉnh Level", "Căn 2 đầu cao độ trên Mặt cắt / Mặt đứng / Sheet", BtnLevel_Click),
                MakeRow("align", "HDT-01C", "Tự Nhận Theo View Đang Mở", "Plan / Sheet → Grids · Section / Elevation → Level", BtnSmart_Click),
                MakeRow("crop", "HDT-02", "Tự Động Crop View", "Crop ôm khít đối tượng · Plan / Section / 3D / Sheet", BtnAutoCrop_Click)
            });

            AddSection("02", "Khối lượng MEP");
            AddGroup(new HDToolToolRow[] {
                MakeRow("pipe", "HDT-03", "Thống Kê Ống Nước", "Ống & phụ kiện · soi sáng mô hình · đưa vào Sheet", BtnMepQuantity_Click),
                MakeRow("duct", "HDT-04", "Khối Lượng Ống Gió", "m² tôn theo TCVN 5687:2010 / SMACNA", BtnDuctQuantity_Click),
                MakeRow("cad2duct", "HDT-05", "Dựng Ống Gió Từ CAD", "Nhận diện tuyến, size WxH · dựng mô hình 3D", BtnCadToDuct_Click)
            });

            AddSection("03", "Bản vẽ & Sheet");
            AddGroup(new HDToolToolRow[] {
                MakeRow("sheetlist", "HDT-06", "Xuất Danh Mục Bản Vẽ", "Excel / CSV / Clipboard · sửa số hiệu & tên trực tiếp", BtnSheetList_Click),
                MakeRow("sheetsync", "HDT-07", "Cập Nhật Danh Mục", "Đối chiếu file Excel → cập nhật Sheet trong Revit", BtnUpdateSheetList_Click),
                MakeRow("layers", "HDT-08", "Xuất CAD Quy Đổi Layer", "DWG · Layer & màu ACI theo hệ thống MEP", BtnExportCad_Click)
            });

            AddSection("04", "Thư viện & hệ thống");
            AddGroup(new HDToolToolRow[] {
                MakeRow("family", "HDT-09", "Tổng Hợp Family 3D", "Xem trước 3D · tìm nhanh · đặt vào bản vẽ", BtnFamilyPalette_Click),
                MakeRow("update", "HDT-10", "Cập Nhật Tool", "Nạp phiên bản mới (Hot Reload) không cần tắt Revit", BtnReloadTool_Click)
            });

            pnlContent.Resize += (s, e) => LayoutFlow();

            this.Controls.Add(pnlContent);
            this.Controls.Add(pnlStatus);
            this.Controls.Add(hdrMain);

            this.ResumeLayout(false);
            this.Shown += (s, e) => { LayoutFlow(); LayoutStatus(); };
        }

        // ---------- Dựng bố cục dọc (tự co giãn theo chiều rộng khung chứa) ----------
        private readonly Dictionary<System.Windows.Forms.Control, int> _gapAfter = new Dictionary<System.Windows.Forms.Control, int>();

        private void AddFlow(System.Windows.Forms.Control c, int gapAfter)
        {
            pnlContent.Controls.Add(c);
            _flow.Add(c);
            _gapAfter[c] = gapAfter;
        }

        private void AddSection(string no, string text)
        {
            HDToolSectionLabel sec = new HDToolSectionLabel();
            sec.Number = no;
            sec.Text = text;
            sec.Height = 28;
            AddFlow(sec, 6);
        }

        private void AddGroup(HDToolToolRow[] rows)
        {
            System.Windows.Forms.Panel grp = new System.Windows.Forms.Panel();
            grp.BackColor = HDToolTheme.Surface;
            grp.Padding = new Padding(1);
            grp.Paint += (s, e) =>
            {
                using (Pen p = new Pen(HDToolTheme.Line)) e.Graphics.DrawRectangle(p, 0, 0, grp.Width - 1, grp.Height - 1);
            };
            for (int i = rows.Length - 1; i >= 0; i--)
            {
                rows[i].Dock = DockStyle.Top;
                rows[i].ShowBottomLine = i < rows.Length - 1;
                grp.Controls.Add(rows[i]);
            }
            grp.Height = rows.Length * rows[0].Height + 2;
            AddFlow(grp, 16);
        }

        private HDToolToolRow MakeRow(string icon, string code, string title, string desc, EventHandler onClick)
        {
            HDToolToolRow row = new HDToolToolRow();
            row.IconKey = icon;
            row.Code = code;
            row.Title = title;
            row.Description = desc;
            row.Height = 46;
            row.Click += onClick;
            return row;
        }

        private void AddInfoLine(System.Windows.Forms.Panel host, int index, string key, string value)
        {
            Label k = new Label();
            k.Text = key;
            k.Font = HDToolTheme.LabelFont;
            k.ForeColor = HDToolTheme.Text3;
            k.BackColor = Color.Transparent;
            k.AutoSize = false;
            k.Location = new Point(14, 9 + index * 20);
            k.Size = new Size(64, 18);
            k.TextAlign = ContentAlignment.MiddleLeft;

            Label v = new Label();
            v.Text = value;
            v.Font = HDToolTheme.Mono;
            v.ForeColor = HDToolTheme.Text;
            v.BackColor = Color.Transparent;
            v.AutoSize = false;
            v.AutoEllipsis = true;
            v.Location = new Point(84, 9 + index * 20);
            v.Size = new Size(400, 18);
            v.TextAlign = ContentAlignment.MiddleLeft;
            v.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            host.Controls.Add(k);
            host.Controls.Add(v);
            host.Resize += (s, e) => { v.Width = Math.Max(40, host.ClientSize.Width - v.Left - 12); };
        }

        private void LayoutFlow()
        {
            if (pnlContent == null) return;
            float sc = this.AutoScaleDimensions.Width > 0 ? this.CurrentAutoScaleDimensions.Width / this.AutoScaleDimensions.Width : 1f;
            Point scroll = pnlContent.AutoScrollPosition;
            int x = pnlContent.Padding.Left;
            int w = Math.Max(100, pnlContent.ClientSize.Width - pnlContent.Padding.Horizontal);
            int y = pnlContent.Padding.Top + scroll.Y;
            foreach (System.Windows.Forms.Control c in _flow)
            {
                c.SetBounds(x, y, w, c.Height);
                y += c.Height + (int)Math.Round(_gapAfter[c] * Math.Max(1f, sc));
            }
        }

        private void LayoutStatus()
        {
            if (pnlStatus == null) return;
            int pad = (int)Math.Round(12 * (this.AutoScaleDimensions.Width > 0 ? this.CurrentAutoScaleDimensions.Width / this.AutoScaleDimensions.Width : 1f));
            btnClose.Location = new Point(pnlStatus.ClientSize.Width - btnClose.Width - pad, (pnlStatus.ClientSize.Height - btnClose.Height) / 2);
            lblStatus.SetBounds(pad + 2, 0, Math.Max(40, btnClose.Left - pad * 2), pnlStatus.ClientSize.Height);
        }

        /// <summary>Mở Khối Lượng Ống Gió bằng đúng lệnh trên Ribbon (PostCommand) để dùng chung ExternalEvent của lệnh gốc.</summary>
        private void BtnDuctQuantity_Click(object sender, EventArgs e)
        {
            PostRibbonCommand("Khối Lượng MEP", "cmdHDToolDuctQuantity", "Khối Lượng Ống Gió");
        }

        private void PostRibbonCommand(string panelName, string buttonName, string friendlyName)
        {
            HDToolModelessHost.EnsureInitialized();
            HDToolModelessHost.Run(app =>
            {
                RevitCommandId id = null;
                try { id = RevitCommandId.LookupCommandId("CustomCtrl_%CustomCtrl_%HDTOOL%" + panelName + "%" + buttonName); }
                catch { }
                if (id != null && app.CanPostCommand(id))
                {
                    app.PostCommand(id);
                }
                else
                {
                    TaskDialog.Show("HDTOOL", "Vui lòng mở \"" + friendlyName + "\" từ thanh Ribbon HDTOOL > " + panelName + ".");
                }
            });
            this.Close();
        }

        private void BtnFamilyPalette_Click(object sender, EventArgs e)
        {
            try
            {
                HDToolModelessHost.EnsureInitialized();
                FamilyPaletteForm form = new FamilyPaletteForm(_uidoc);
                OpenChildModeless(form);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("HDTOOL PALETTE FAMILY", "Lỗi mở Palette Family 3D: " + ex.Message);
            }
        }

        private void BtnExportCad_Click(object sender, EventArgs e)
        {
            try
            {
                HDToolModelessHost.EnsureInitialized();
                ExportCadOptionsForm form = (_uidoc != null) ? new ExportCadOptionsForm(_uidoc) : new ExportCadOptionsForm(_doc);
                OpenChildModeless(form);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("HDTOOL XUẤT CAD", "Lỗi mở giao diện Xuất CAD & Quy Đổi Layer MEP: " + ex.Message);
            }
        }

        private void BtnCadToDuct_Click(object sender, EventArgs e)
        {
            try
            {
                HDToolModelessHost.EnsureInitialized();
                CadToDuctForm form = new CadToDuctForm(_uidoc);
                OpenChildModeless(form);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("HDTOOL", "Lỗi mở Dựng Ống Gió Từ CAD: " + ex.Message);
            }
        }

        private void OpenChildModeless(System.Windows.Forms.Form childForm)
        {
            HDToolModelessHost.EnsureInitialized();
            IntPtr mainHwnd = IntPtr.Zero;
            try { mainHwnd = _uidoc.Application.MainWindowHandle; } catch { }
            this.Close();
            HDToolModelessHost.ShowModeless(childForm, mainHwnd);
        }

        private void BtnAutoCrop_Click(object sender, EventArgs e)
        {
            try
            {
                HDToolModelessHost.EnsureInitialized();
                AutoCropForm form = new AutoCropForm(_uidoc, HDToolModelessHost.ExEvent, HDToolModelessHost.Handler);
                OpenChildModeless(form);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("HDTOOL CROP VIEW", "Lỗi mở giao diện Tự Động Crop View: " + ex.Message);
            }
        }

        private void BtnMepQuantity_Click(object sender, EventArgs e)
        {
            try
            {
                // ExternalEvent.Create chỉ hợp lệ trong API context -> tạo bên trong ExternalEvent dùng chung,
                // sau đó quay về luồng UI để mở Form (không gọi Create trực tiếp từ sự kiện Click của Form Modeless)
                HDToolModelessHost.EnsureInitialized();
                HDToolModelessHost.Run(app =>
                {
                    MepInspectorExternalEventHandler handler = new MepInspectorExternalEventHandler();
                    ExternalEvent extEvent = ExternalEvent.Create(handler);
                    if (this.IsDisposed || !this.IsHandleCreated) return;
                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        try
                        {
                            MepQuantityInspectorForm form = new MepQuantityInspectorForm(_uidoc, extEvent, handler);
                            OpenChildModeless(form);
                        }
                        catch (Exception ex2)
                        {
                            TaskDialog.Show("HDTOOL MEP", "Lỗi mở giao diện Thống kê khối lượng MEP: " + ex2.Message);
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                TaskDialog.Show("HDTOOL MEP", "Lỗi mở giao diện Thống kê khối lượng MEP: " + ex.Message);
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                this.Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void BtnReloadTool_Click(object sender, EventArgs e)
        {
            try
            {
                UpdateToolForm form = new UpdateToolForm(_uidoc.Application);
                OpenChildModeless(form);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("HDTOOL", "Lỗi mở cập nhật tool: " + ex.Message);
            }
        }

        private void BtnUpdateSheetList_Click(object sender, EventArgs e)
        {
            try
            {
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.Title = "Chọn File Excel hoặc CSV Đã Đổi Tên Bản Vẽ";
                ofd.Filter = "Tất cả file bảng tính (*.xls;*.xlsx;*.csv)|*.xls;*.xlsx;*.csv|" +
                             "Excel 97-2003 / XML (*.xls)|*.xls|" +
                             "Excel Workbook (*.xlsx)|*.xlsx|" +
                             "File CSV chuẩn UTF-8 (*.csv)|*.csv|" +
                             "Tất cả các file (*.*)|*.*";
                ofd.FilterIndex = 1;
                ofd.RestoreDirectory = true;

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    List<List<string>> rawRows = UpdateSheetListService.ReadRowsFromFile(ofd.FileName);
                    if (rawRows != null && rawRows.Count > 0)
                    {
                        List<SheetUpdateItem> items = UpdateSheetListService.MatchAndCompare(_doc, rawRows);
                        if (items.Count > 0)
                        {
                            UpdateSheetListPreviewForm preview = new UpdateSheetListPreviewForm(_doc, items);
                            OpenChildModeless(preview);
                            return;
                        }
                        else
                        {
                            TaskDialog.Show("Thông Báo", "Không tìm thấy số hiệu bản vẽ nào trong file khớp với dự án Revit.");
                        }
                    }
                    else
                    {
                        TaskDialog.Show("Thông Báo", "File đã chọn không có dữ liệu bảng tính hợp lệ.");
                    }
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Lỗi Cập Nhật", "Lỗi: " + ex.Message);
            }
            this.Close();
        }

        private void BtnSheetList_Click(object sender, EventArgs e)
        {
            try
            {
                ExportSheetListForm form = new ExportSheetListForm(_doc);
                OpenChildModeless(form);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("HDTOOL", "Lỗi mở Danh mục bản vẽ: " + ex.Message);
            }
        }

        private void BtnGrids_Click(object sender, EventArgs e)
        {
            try
            {
                HDToolModelessHost.EnsureInitialized();
                GridCropForm form = new GridCropForm(_uidoc, HDToolModelessHost.ExEvent, HDToolModelessHost.Handler);
                OpenChildModeless(form);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("HDTOOL", "Lỗi mở Căn Grids: " + ex.Message);
            }
        }

        private void BtnLevel_Click(object sender, EventArgs e)
        {
            try
            {
                LevelCropForm form = new LevelCropForm(_uidoc, new List<Level>());
                OpenChildModeless(form);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("HDTOOL", "Lỗi mở Căn Level: " + ex.Message);
            }
        }

        private void BtnSmart_Click(object sender, EventArgs e)
        {
            try
            {
                if (GridCropService.IsPlanView(_activeView) || _activeView is ViewSheet)
                {
                    HDToolModelessHost.EnsureInitialized();
                    GridCropForm form = new GridCropForm(_uidoc, HDToolModelessHost.ExEvent, HDToolModelessHost.Handler);
                    OpenChildModeless(form);
                    return;
                }

                string errorMsg;
                if (LevelCropService.IsValidContext(_activeView, out errorMsg))
                {
                    LevelCropForm form = new LevelCropForm(_uidoc, new List<Level>());
                    OpenChildModeless(form);
                    return;
                }

                TaskDialog.Show("HDTOOL", errorMsg);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("HDTOOL", "Lỗi: " + ex.Message);
            }
            this.Close();
        }
    }
}
