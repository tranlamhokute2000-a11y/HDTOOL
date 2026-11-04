# HDTOOL - BỘ ADD-IN AUTODESK REVIT CHUYÊN NGHIỆP TOÀN DIỆN

> **Dự án**: HDTOOL (Add-in Revit)  
> **Chủ sở hữu & Chỉ đạo phát triển**: Sếp Hồ (`tranlamhokute2000-a11y`)  
> **Phiên bản hiện tại**: **Version 37**  
> **Tương thích**: Autodesk Revit 2024 (.NET Framework 4.8), Revit 2025 (.NET 8), Revit 2026, Revit 2027  

---

## 🌟 TỔNG QUAN TÍNH NĂNG CHÍNH

### 1. Khối Lượng MEP (Chuyên Sâu Ống Nước & Phụ Kiện)
- **Bóc khối lượng Ống nước (Pipes)**: Đo chiều dài mét dài chi tiết, phân loại hệ thống, đường kính (D34, D60, D110...) và cao độ tầng. Hỗ trợ 2 chế độ:
  - *Tách rời rạc*: Giữ nguyên từng đoạn ống thực tế (Không SUM).
  - *Gom mét dài*: Tự động cộng tổng chiều dài theo quy cách (SUM).
- **Bóc khối lượng Phụ kiện (Fittings)**: Tự động gom 3 parameter phân mảnh thành tên chuẩn (Co, Cút, Tê, Côn thu, Van, Thông tắc...), đếm số lượng chiếc chính xác.
- **Tự động nhận diện bóc từ Sheet hoặc View mô hình đồng nhất 100%**:
  - Khi ở trên Sheet: Click chọn trực tiếp khung nhìn (Viewport) trên Sheet để tự động quét toàn bộ đối tượng trong View đó.
  - Hỗ trợ nút **"Bóc Thêm (Cộng Dồn)"** từ các View khác trên Sheet.
- **CHECK VAR PLUMBING & Soi Sáng Tương Tác 2 Chiều**:
  - Click vào dòng khối lượng trên Form -> Tự động Highlight phát sáng đối tượng tương ứng trên Revit (Cam rực rỡ, LineWeight 5, Zoom cận cảnh).
  - Soi sáng trực tiếp từ Sheet hoặc View mô hình. Tự động dọn dẹp sạch sẽ đồ họa khi bấm ESC / đóng tool.
- **Tạo & Đặt Bảng Khối Lượng Vào Sheet**:
  - Dạng **LEGENDS** (`ViewType.Legend`): Đặt được trên nhiều Sheet cùng lúc, nằm trong nhánh Legends của Project Browser.
  - Khung Viewport ôm sát bảng 100%, tự động gán kiểu No Title, không bị to khổng lồ hay đẩy ra xa.
  - Bảng Excel WYSIWYG giữ nguyên 100% cấu trúc cột hàng.

### 2. Quản Lý & Tự Động Ghi Nhớ Tùy Chọn Người Dùng (Auto-Save Preferences)
- Tự động lưu và khôi phục 100% mọi tác vụ: Chế độ bóc, cách đo đoạn ống, highlight, danh sách cột Parameter tự thêm, thứ tự cột kéo thả (`DisplayIndex`), độ rộng cột (`Width`), ẩn/hiện cột (`Visible`), định dạng bảng và cách đặt vào Sheet.
- Thuần .NET JSON parser độc lập, không phụ thuộc thư viện ngoài, tương thích tuyệt đối cả Revit 2024 (.NET 4.8) và Revit 2025+ (.NET 8).

### 3. Căn Chỉnh Đối Tượng (Grids & Levels)
- **Căn chỉnh Lưới trục (Grids)**: Căn chỉnh 4 hướng (Top, Bottom, Left, Right) so với khung Crop View theo đơn vị mm trên Mặt bằng, Mặt đứng, Mặt cắt và Sheet.
- **Căn chỉnh Cao độ (Levels)**: Căn chỉnh 2 đầu (Left, Right) so với khung Crop View.

### 4. Quản Lý Bản Vẽ & Sheet
- **Xuất danh mục bản vẽ (Sheet List)**: Trích xuất và xuất sang Excel, CSV UTF-8.
- **Cập nhật danh mục 2 chiều**: Sửa số hiệu/tên bản vẽ từ Excel và cập nhật ngược vào Revit tức thì.

### 5. Hệ Thống Đóng Gói & Hot Reload
- **Hot Reload trực tiếp trong Revit**: Nạp DLL mới vào bộ nhớ mà không cần khởi động lại Revit thông qua nút "Cập Nhật Tool".
- **Đóng gói 1-File Standalone**: Tự động tăng phiên bản (+1) sau mỗi lần build (`HDTOOL_Setup_Version_{N}.exe`), chỉ giữ lại 2 version gần nhất, không bao giờ sinh file ra Desktop.
- **Preview Studio**: Mô phỏng 100% Ribbon Revit trực quan (`HDTool_PreviewStudio.html`).

---

## 📂 CẤU TRÚC THƯ MỤC NGUỒN

```
ADDIN REVIT/
├── Source/                         # Toàn bộ mã nguồn C# (.cs)
│   ├── App.cs                      # Khởi tạo Ribbon Autodesk Revit
│   ├── MepQuantityService.cs       # Xử lý logic bóc khối lượng, Legend View & Excel Table
│   ├── MepQuantityInspectorForm.cs # Giao diện chính Bóc Khối Lượng & Check VAR
│   ├── MepUserSettings.cs          # Tự động ghi nhớ cấu hình người dùng (JSON thuần)
│   ├── CheckVarPlumbingForm.cs     # Công cụ Soi Sáng / Check VAR trên Sheet
│   ├── ToolHotReloadManager.cs     # Quản lý Hot Reload trực tiếp trong Revit
│   ├── GridCropService.cs          # Dịch vụ căn chỉnh Lưới trục Grids
│   ├── LevelCropService.cs         # Dịch vụ căn chỉnh Cao độ Levels
│   └── ExportSheetListService.cs   # Quản lý danh mục bản vẽ Sheet
├── Resources/                      # Icon và hình ảnh nhúng
├── Audio/                          # Script và âm thanh thông báo
├── .agents/                        # Trí tuệ quy tắc hoạt động & Session State
├── Build.ps1                       # Script biên dịch tự động tăng Version & Hot Reload
├── VERSION.txt                     # Số phiên bản hiện tại
└── HUONG_DAN_SU_DUNG.md            # Tài liệu hướng dẫn sử dụng chi tiết
```

---

## 🚀 HƯỚNG DẪN BIÊN DỊCH & TRIỂN KHAI

Để biên dịch Add-in và bộ cài đặt tự động:
```powershell
powershell -ExecutionPolicy Bypass -File .\Build.ps1
```
Hệ thống sẽ:
1. Tự động tăng Version (+1): Version 36 -> Version 37...
2. Biên dịch `HDTOOL.dll` và `Setup.exe` 0 lỗi.
3. Tự động đồng bộ sang `%APPDATA%\Autodesk\Revit\Addins\2024`, `2025`, `2027`.
4. Tự động áp dụng Retention Policy: Chỉ giữ 2 version gần nhất trong thư mục dự án, giữ Desktop của Sếp Hồ luôn sạch đẹp 100%!

