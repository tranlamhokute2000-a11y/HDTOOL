# TRẠNG THÁI LÀM VIỆC GẦN NHẤT (SESSION STATE)

- **Thời gian cập nhật**: 2026-11-14 14:02
- **Phiên bản hiện tại**: **Version 107** (`HDTOOL_Setup_Version_107.exe`)
- **Bộ cài lưu trữ**: `HDTOOL_Setup_Version_106.exe` và `HDTOOL_Setup_Version_107.exe` (0 file trên Desktop)

## 1. Công việc vừa hoàn thành
- **Loại bỏ hoàn toàn chức năng "Khớp Tỷ Lệ Bảng Khối Lượng" theo lệnh Sếp Hồ**:
  1. **Xóa khỏi mã nguồn C# & Ribbon Revit**:
     - Xóa nút `cmdHDToolFitTableScale` ("Khớp Tỷ Lệ\nBảng Khối Lượng") khỏi Panel `Khối Lượng MEP` trong `Source/App.cs`.
     - Xóa đăng ký Command số 14 trong `HDTOOL.addin`.
     - Xóa file thực thi `Source/FitTableScaleCommand.cs`.
     - Panel `Khối Lượng MEP` hiện tại gồm 3 nút chuẩn: `Thống Kê Ống Nước`, `Khối Lượng Ống Gió`, `Dựng Ống Gió Từ CAD`.
  2. **Xóa khỏi Preview Studio & Cấu hình Ribbon Layout**:
     - Xóa nút `cmdHDToolFitTableScale` khỏi `hdtool_ribbon_layout.json`.
     - Xóa nút và form mockup `cmdHDToolFitTableScale` khỏi `PreviewStudio/HDTool_PreviewStudio.html`.
  3. **Xóa triệt để khỏi Kỹ năng (Skill)**:
     - Xóa hoàn toàn khỏi `SKILL.md` và `.agents/skills/hdtool/SKILL.md` theo đúng quy tắc chỉ đạo: Những gì đã bỏ khỏi tool thì trong Skill cũng bỏ đi ngay lập tức, không cần ghi nhớ.
  4. **Biên dịch & Đóng gói**:
     - Chạy `Build.ps1` -> Tự động tăng lên **Version 107** (`HDTOOL_Setup_Version_107.exe`).
     - Tự động đồng bộ Hot Reload sang Revit 2024, 2025, 2027 sẵn sàng 100%.
     - Giữ đúng 2 bản gần nhất (Version 106 và Version 107), 0 file trên Desktop.

## 2. File liên quan
- `Source/App.cs`
- `HDTOOL.addin`
- `hdtool_ribbon_layout.json`
- `PreviewStudio/HDTool_PreviewStudio.html`
- `SKILL.md`, `.agents/skills/hdtool/SKILL.md`
- `Build.ps1`, `VERSION.txt`
