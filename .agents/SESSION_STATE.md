# TRẠNG THÁI LÀM VIỆC GẦN NHẤT (SESSION STATE)

- **Thời gian cập nhật**: 2026-11-27
- **Phiên bản hiện tại**: **Version 109** (`HDTOOL_Setup_Version_109.exe`)
- **Bộ cài lưu trữ**: `HDTOOL_Setup_Version_108.exe` và `HDTOOL_Setup_Version_109.exe` (0 file trên Desktop)
- **Git**: Version 109 đã commit + push lên `main`.

## 1. Công việc vừa hoàn thành — Giao diện kỹ thuật "Bản Vẽ Sáng" (Version 109)
1. **Preview trước**: `PreviewStudio/HDTool_ThemeStudio.html` (nút "THEME KỸ THUẬT (MỚI)" trong `HDTool_PreviewStudio.html`). Sếp Hồ chốt chế độ **Bản Vẽ Sáng**.
2. **`Source/HDToolTheme.cs` (mới)**: token màu + font (Segoe UI / Consolas), `ApplyTo(Form)` khoác áo tự động mọi control
   (chỉ đổi màu đặt tường minh, giữ bố cục & logic), `StyleButton`, `StyleGrid` (tiêu đề + vạch Accent, dòng 26px, số Consolas,
   chip hệ SA/RA/EA/OA), ToolStrip renderer, lưới blueprint nền. Control tự vẽ: `HDToolHeader`, `HDToolSectionLabel`,
   `HDToolToolRow`, `HDToolTickPanel`, `HDToolStatusText`.
3. **`HDToolBaseForm`**: `AutoScaleMode.Dpi` (96 DPI gốc) + `PerformAutoScale()` + `HDToolTheme.ApplyTo(this)` trong `OnLoad` -> cả 23 Form đổi giao diện.
4. **Menu HDTOOL dựng lại** (`HDToolMainForm`): danh sách công cụ mã `HDT-xx` theo nhóm Panel, khung thông tin View, thanh trạng thái;
   thêm mục Khối Lượng Ống Gió (gọi lệnh Ribbon qua `PostCommand`).
5. **Icon Ribbon mới** `Source/HDToolIcons.cs` (vector, theo DPI, tự đổi màu theo giao diện Sáng/Tối của Revit); `App.cs` dùng `HDToolIcons.Ribbon`.
   Bỏ mục "Tự Động Crop View" trùng trong SplitButton (App.cs + `hdtool_ribbon_layout.json`).
6. **Sửa 41 dòng chữ lỗi mã hóa (mojibake)** — chủ yếu hộp thoại "Đặt Bảng Vào Sheet" (DuctQuantityCommand.cs) và MepQuantityInspectorForm.
7. **Kiểm tra**: build 0 lỗi 0 cảnh báo; chụp thử ngoài Revit 5 Form + Menu dựng bằng control mới + bảng icon (nền sáng & tối).
   Form cần RevitAPIUI (Menu thật, Ống gió, Cập nhật tool...) không chụp được ngoài Revit -> **cần Sếp mở Revit kiểm tra**.

## 1b. Công cụ kiểm tra nhanh
- `.agents/tools/KiemTra.ps1`: biên dịch thử ra TEMP (không tăng Version, không deploy) + quét lỗi cấm (IntegerValue, emoji, mojibake,
  đường dẫn cứng, Form không kế thừa `HDToolBaseForm`...) trên file .cs thay đổi so với `main`; thêm `-All` để quét toàn bộ.
- (2026-11-27) Sếp Hồ đã BỎ quy trình phối hợp 2 AI và Điều Phối Studio — đã xóa CHI_DAO / BAO_CAO / LENH_SEP / studio và quy tắc tương ứng.

## 2. Việc còn tồn
- Chưa thử trong Revit thật: Menu (đặc biệt mục HDT-04 Ống gió qua PostCommand), các Form lớn ở DPI 125/150%.
- Phần mẫu chưa làm: ô chỉ số KPI trên bảng Ống gió, sơ đồ kích thước tương tác ở Căn Chỉnh Grids (cần control `HDToolDimensionPreview`).
- Form Sửa Số Hiệu Hàng Loạt (`BatchEditSheetNumberForm`) bị tràn mép phải — lỗi có từ trước, chưa sửa.
- Từ báo cáo rà soát (mức trung bình): gộp 3 kiểu ExternalEvent handler; bỏ đường dẫn cứng `c:\Users\Nguye\...`; `OnViewActivated` tự mở Transaction;
  362 `catch {}` rỗng; đóng Form khi `DocumentClosing`; dọn `HDTOOL_old_*.dll`; xóa code chết CHECK VAR PLUMBING; bản build .NET 8 cho Revit 2025+.
- Tài liệu: `HUONG_DAN_SU_DUNG.md` (còn ghi Version 84/85), `README.md` (Version 37), gộp `GEMINI.md`/`AGENTS.md`.

## 3. File liên quan
- `Source/HDToolTheme.cs`, `Source/HDToolIcons.cs` (mới), `Source/HDToolExternalEvent.cs`, `Source/HDToolMainForm.cs`, `Source/App.cs`
- `Source/DuctQuantityCommand.cs`, `Source/MepQuantityInspectorForm.cs` (+ các file có chuỗi mojibake)
- `PreviewStudio/HDTool_ThemeStudio.html`, `PreviewStudio/HDTool_PreviewStudio.html`, `hdtool_ribbon_layout.json`
- `SKILL.md`, `.agents/skills/hdtool/SKILL.md`, `VERSION.txt`
