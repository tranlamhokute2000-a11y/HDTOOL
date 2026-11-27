# TRẠNG THÁI LÀM VIỆC GẦN NHẤT (SESSION STATE)

- **Thời gian cập nhật**: 2026-11-27
- **Phiên bản hiện tại**: **Version 114** (`HDTOOL_Setup_Version_114.exe`)
- **Bộ cài lưu trữ**: `HDTOOL_Setup_Version_113.exe` và `HDTOOL_Setup_Version_114.exe` (0 file trên Desktop)
- **Git**: Version 112 đã commit (`f3390e3`, chưa push). Version 113 (Mac Sáng) và 114 (Menu cột trái) CHƯA commit.

## 0. Vừa xong — Menu HDTOOL cột trái kiểu Mac (Version 114)
- `HDToolMainForm` dựng lại: cột trái (ô tìm + `HDToolSidebar`: Tất cả / nhóm kèm số lượng / Gần đây) + khung phải (tiêu đề nhóm, dòng View đang mở, danh sách hàng trong khung trắng bo góc). Cửa sổ 860x640, đổi kích thước được.
- Thêm trong `HDToolControls.cs`: `HDToolSidebar` + `HDToolSidebarEventArgs`, `HDToolToolCard.RowStyle/FirstRow`, `HDToolCardGrid.ListStyle`.
- Kiểm tra: KiemTra ĐẠT, build Version 114 0 lỗi; chụp bản dựng lại bằng control thật khớp bản xem trước (Menu thật cần RevitAPIUI nên không mở được ngoài Revit). CHƯA thử trong Revit.

## 0a0. Giao diện MAC SÁNG cho toàn bộ HDTOOL (Version 113)
- Sếp chọn "Mac Sáng" từ `PreviewStudio/HDTool_GiaoDienMac.html`.
- `HDToolTheme`: thêm `HDToolThemeMode.Mac` (mặc định), token Mac qua `Pm(...)`, `IsMac`.
- `HDToolBaseForm.ApplyMacChrome()`: cửa sổ không viền + thanh tiêu đề tự vẽ 3 nút tròn, kéo/phóng to/đổi kích thước, bo góc Win11.
- `.agents/tools/KiemTra.ps1`: bỏ báo nhầm "Form không kế thừa HDToolBaseForm" ở chính lớp HDToolBaseForm.
- Kiểm tra: build 0 lỗi; chụp PrintWindow (Ống Gió, Menu, hộp thoại nhỏ) đúng. CHƯA thử trong Revit thật (kéo cửa sổ, đổi kích thước, các form tự vẽ màu tối cứng như Tổng Hợp Family).


## 0a. Tổng Hợp Family đã load + xem trước 3D xoay được (Version 112)
- Sếp duyệt bản xem trước `PreviewStudio/HDTool_FamilyPalette_v2.html`.
- Bảng: mỗi dòng = 1 Family; cột `Số Type`; bỏ hẳn đếm số lượng trên bản vẽ (cột Đã Vẽ, ô lọc, vòng đếm FamilyInstance). Chọn Type ở `cboTypes` khung phải.
- `Source/FamilyMeshViewer.cs` (mới): khung 3D tự vẽ, xoay/phóng/dời bằng chuột; lưới lấy bằng `FamilyPaletteService.GetSymbolMesh` trong ExternalEvent; Family không có hình khối -> ảnh `GetPreviewImage`.
- Kiểm tra: biên dịch 0 lỗi; dựng thử khối hộp ngoài Revit đúng hướng Z lên. CHƯA thử trong Revit thật (hình khối Family thật, tốc độ với Family nặng).


## 0b. Đợt 2: cửa sổ Khối Lượng Ống Gió (Version 111)
- `Source/DuctQuantityView.cs` (mới): toàn bộ giao diện v2, không phụ thuộc Revit; `DuctQuantityForm.InitializeComponent` viết lại chỉ để gắn sự kiện vào control của View
  (logic quét / lọc / làm sáng / đặt Sheet giữ nguyên; 3 RadioButton phạm vi giữ ẩn, đồng bộ với nút phân đoạn qua `SyncScopeView`).
- Số liệu 4 ô cập nhật trong `FilterAndDisplayItems` (`_view.SetTotals`), dòng thông tin `UpdateInfoBar()`.
- Control mới trong `HDToolControls.cs`: `HDToolStepBar`, `HDToolSegmented`, `HDToolChipToggle`, `HDToolSwitch`, `HDToolKpiCard`, `HDToolCardPanel`, `HDToolEmptyState`; icon nhỏ trong `HDToolIcons`.
- `HDToolTheme`: `SafeSkin` (lỗi 1 control không dừng cả Form, ghi `SkinErrors`); ComboBox nền tối dùng `FlatStyle.Standard` + `DarkMode_CFD`.
- Kiểm tra: build 0 lỗi 0 cảnh báo; chụp `PrintWindow` cửa sổ Ống Gió (có dữ liệu mẫu + chưa có dữ liệu) dựng bằng đúng `DuctQuantityView`.
  Chưa thử trong Revit thật (quét vùng, chuyển phạm vi, đặt Sheet).

## 1. Công việc vừa hoàn thành — Giao diện TỐI CAO CẤP, đợt 1 (Version 110)
- Bản xem trước đã duyệt: `PreviewStudio/HDTool_GiaoDienMoi.html` (3 phong cách; Sếp Hồ chốt **Tối Cao Cấp**).
1. **`Source/HDToolTheme.cs` viết lại**: chế độ `Premium` (Tối Cao Cấp) mặc định + giữ `Drafting` / `Blueprint`; màu nhận diện từng công cụ `ToolColor`;
   chuyển nền tối tự động cho mọi Form cũ (ô nhập, danh sách, bảng, tab, menu chuột phải, dải đầu chuyển màu); xử lý màu đổi lúc chạy
   (`ForeColorChanged`, `BackColorChanged` của nút, `CellFormatting` của bảng); thanh tiêu đề & thanh cuộn tối (`HDToolNative`).
2. **`Source/HDToolControls.cs` (mới)**: `HDToolHeader` v2, `HDToolSectionLabel` v2, `HDToolButton` (bo góc), `HDToolToolCard`, `HDToolCardGrid`,
   `HDToolSearchBox`, `HDToolInfoBar`, `HDToolPill`, `HDToolRecent`. Đã bỏ `HDToolToolRow`, `HDToolTickPanel`, `HDToolStatusText` (v109).
3. **Menu HDTOOL v2** (`HDToolMainForm`): thẻ 2 cột theo nhóm, ô tìm (bỏ dấu), dùng gần đây, phím 1–9, Ctrl+F, Enter mở kết quả đầu.
4. **Icon Ribbon**: điểm nhấn mang màu riêng của từng công cụ (`HDToolIcons.Ribbon`).
5. **Kiểm tra**: build 0 lỗi 0 cảnh báo; chụp bằng `PrintWindow` ngoài Revit: Menu v2 (dựng lại bằng control thật), 5 Form cũ ở nền tối, bảng icon.
   Thanh tiêu đề tối hoạt động. Form cần RevitAPIUI (Menu thật, Ống gió...) chưa xem được ngoài Revit.

## 1a. ĐỢT TIẾP THEO (chưa làm)
- ~~Đợt 2 — Khối Lượng Ống Gió~~: ĐÃ XONG (Version 111, xem mục 0).
- **Đợt 3 — Căn Chỉnh Grids / Level** (`CropAlignOptionForm` / `GridCropForm`): stepper − / +, mức có sẵn, la bàn hướng, công tắc, sơ đồ kích thước sống.
- Sau đó lần lượt các Form còn lại (Xuất Danh Mục, Xuất CAD, Family Palette, Thống Kê Ống Nước...).

## 1b. Công cụ kiểm tra nhanh
- `.agents/tools/KiemTra.ps1`: biên dịch thử ra TEMP (không tăng Version, không deploy) + quét lỗi cấm (IntegerValue, emoji, mojibake,
  đường dẫn cứng, Form không kế thừa `HDToolBaseForm`...) trên file .cs thay đổi so với `main`; thêm `-All` để quét toàn bộ.

## 2. Việc còn tồn
- Chưa thử trong Revit thật: Menu v2 (đặc biệt mục Ống gió qua PostCommand), các Form lớn ở nền tối và DPI 125/150%;
  Form có vẽ tay màu sáng trong `Paint` riêng (vd. Family Palette, MepQuantityInspector) có thể còn mảng sáng.
- Form Sửa Số Hiệu Hàng Loạt (`BatchEditSheetNumberForm`) bị tràn mép phải — lỗi có từ trước, chưa sửa.
- Từ báo cáo rà soát (mức trung bình): gộp 3 kiểu ExternalEvent handler; bỏ đường dẫn cứng `c:\Users\Nguye\...`; `OnViewActivated` tự mở Transaction;
  362 `catch {}` rỗng; đóng Form khi `DocumentClosing`; dọn `HDTOOL_old_*.dll`; xóa code chết CHECK VAR PLUMBING; bản build .NET 8 cho Revit 2025+.
- Tài liệu: `HUONG_DAN_SU_DUNG.md` (còn ghi Version 84/85), `README.md` (Version 37), gộp `GEMINI.md`/`AGENTS.md`.

## 3. File liên quan
- `Source/HDToolTheme.cs`, `Source/HDToolControls.cs`, `Source/HDToolIcons.cs`, `Source/HDToolExternalEvent.cs`, `Source/HDToolMainForm.cs`, `Source/App.cs`
- `PreviewStudio/HDTool_GiaoDienMoi.html`, `PreviewStudio/HDTool_PreviewStudio.html`
- `SKILL.md`, `.agents/skills/hdtool/SKILL.md`, `VERSION.txt`
