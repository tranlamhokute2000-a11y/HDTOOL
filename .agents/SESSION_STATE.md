# TRẠNG THÁI LÀM VIỆC GẦN NHẤT (SESSION STATE)

- **Thời gian cập nhật**: 2026-11-21 15:30
- **Phiên bản hiện tại**: **Version 108** (`HDTOOL_Setup_Version_108.exe`)
- **Bộ cài lưu trữ**: `HDTOOL_Setup_Version_107.exe` và `HDTOOL_Setup_Version_108.exe` (0 file trên Desktop)

## 1. Công việc vừa hoàn thành — Sửa 4 nhóm lỗi ưu tiên cao (sau rà soát toàn dự án)
1. **Tương thích Revit 2026+**: Thay toàn bộ 12 chỗ `ElementId.IntegerValue` (đã bị Autodesk loại bỏ từ API 2026) bằng `ElementId.Value` (`long`, ép `(int)` cho Category Id). Biên dịch còn 0 cảnh báo.
2. **Lưu Số hiệu/Tên Sheet an toàn** (`ExportSheetListForm.SaveModifiedSheetsToRevit`):
   - Luôn ghi vào đúng dự án gốc của Form (`HDToolModelessHost.ResolveOwnerDocument(_doc)`), không dùng `ActiveUIDocument` (Form Modeless có thể đang mở khi chuyển dự án).
   - Có lỗi ở bất kỳ bản vẽ nào -> `RollBack` toàn bộ (không còn Sheet kẹt số tạm `HD_TMP_...`); trạng thái bảng chỉ cập nhật sau khi `Commit` thành công.
   - Áp dụng cùng nguyên tắc dự án gốc cho `UpdateSheetListPreviewForm` và `MepQuantityInspectorForm` (cập nhật/đồng bộ Parameter).
3. **Loại bỏ emoji** (lỗi ô vuông WinForms/TextNote) ở 17 file: emoji bị xóa, `✅`->`✔`, `❌`->`✖`. Dòng `TỔNG CỘNG` trong bảng khối lượng ống gió trên bản vẽ không còn ký tự lỗi.
4. **Chặn gọi Revit API ngoài API context**:
   - `HDToolModelessHost.Run` không còn gọi `action(null)`; báo `ReportNoApiContext()`.
   - `App.OnStartup` khởi tạo sẵn ExternalEvent dùng chung (`HDToolModelessHost.EnsureInitialized()`).
   - Nút Thống Kê Ống Nước trong Menu HDTOOL: `ExternalEvent.Create` chuyển vào bên trong ExternalEvent dùng chung (trước đây gọi từ sự kiện Click -> lỗi).
5. **Build**: `Build.ps1` -> Version 108, 0 lỗi, deploy Revit 2024/2025/2027. Revit 2024 đang mở lúc build -> cần mở lại Revit hoặc bấm "Cập Nhật Tool".

## 2. Việc còn tồn (từ báo cáo rà soát, chưa làm)
- Mức trung bình: gộp 3 kiểu ExternalEvent handler về `HDToolModelessHost`; bỏ đường dẫn cứng `c:\Users\Nguye\...` trong `App.cs`/`ToolHotReloadManager`; `OnViewActivated` tự mở Transaction; 362 `catch {}` rỗng; đóng Form khi `DocumentClosing`; dọn `HDTOOL_old_*.dll`; xóa code chết CHECK VAR PLUMBING.
- Các nhánh `else` gọi API trực tiếp trong Form (khi ExternalEvent null) nay không còn đường chạy tới, nhưng chưa xóa.
- DLL vẫn là .NET Framework 4.8 cho Revit 2024; Revit 2025+ (.NET 8) chưa có bản build riêng.
- Giao diện: `AutoScaleMode` theo DPI, lớp `HDToolTheme` dùng chung, header thống nhất, icon thay emoji (phải qua Preview Studio trước).
- Tài liệu: `HUONG_DAN_SU_DUNG.md` (còn ghi Version 84/85), `README.md` (Version 37), gộp `GEMINI.md`/`AGENTS.md`.

## 3. File liên quan
- `Source/HDToolExternalEvent.cs`, `Source/App.cs`, `Source/HDToolMainForm.cs`
- `Source/ExportSheetListForm.cs`, `Source/UpdateSheetListPreviewForm.cs`, `Source/MepQuantityInspectorForm.cs`
- `Source/AutoCropService.cs`, `Source/CadExportMepService.cs`, `Source/DuctQuantityCommand.cs`, `Source/DuctQuantityService.cs`, `Source/FamilyPaletteService.cs`
- 17 file Form/Service có chuỗi giao diện chứa emoji
- `SKILL.md`, `.agents/skills/hdtool/SKILL.md`, `VERSION.txt`
