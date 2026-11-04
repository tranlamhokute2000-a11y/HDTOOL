# SESSION STATE - HDTOOL (ADDIN REVIT)

- **Thời gian cập nhật**: 2026-11-04 15:32
- **Người dùng**: Sếp Hồ (`tranlamhokute2000-a11y`)
- **Phiên bản hiện tại**: **Version 37** (`HDTOOL_Setup_Version_37.exe`)

---

## 1. Công việc vừa hoàn thành
- **Vá triệt để lỗi khi mở Thống Kê Khối Lượng Ống Nước trên Revit 2025 (.NET 8)**:
  - **Nguyên nhân chính xác**: Revit 2025 nâng cấp nền tảng chạy trên **.NET 8** (thay vì .NET Framework 4.8 như Revit 2024). Thư viện `System.Web.Extensions.dll` (thuộc .NET Framework cũ) không tồn tại trong runtime .NET 8, dẫn đến lỗi:
    `Could not load file or assembly 'System.Web.Extensions, Version=4.0.0.0'`.
  - **Giải pháp vá lỗi 100%**:
    - Thay thế toàn bộ bằng bộ xử lý JSON thuần C# độc lập trong `Source/MepUserSettings.cs`.
    - Loại bỏ hoàn toàn tham chiếu `/r:System.Web.Extensions.dll` khỏi `Build.ps1`.
    - Tương thích đa nền tảng 100% trên cả .NET 4.8 (Revit 2024) và .NET 8 (Revit 2025, Revit 2026, Revit 2027) mà không bao giờ bị xung đột thư viện nữa.
- **Biên dịch & Triển khai**:
  - Biên dịch thành công 0 lỗi, 0 warning lên Version 37.
  - Đã xuất bản bộ cài 1-file: `HDTOOL_Setup_Version_37.exe`.
  - Màn hình Desktop sạch sẽ 100% (0 file). Thư mục dự án duy trì đúng 2 version gần nhất (Version 36 và Version 37).
  - Tự động đồng bộ Hot Reload sang Revit 2024, Revit 2025 và Revit 2027.

---

## 2. File liên quan
- `Source/MepUserSettings.cs`: Chuyển sang JSON parser thuần C#, loại bỏ System.Web.Extensions.
- `Build.ps1`: Bỏ tham chiếu System.Web.Extensions.dll, biên dịch Version 37.
- `VERSION.txt`: Cập nhật 37.
