# TRẠNG THÁI LÀM VIỆC GẦN NHẤT (SESSION STATE)

- **Thời gian cập nhật**: 2026-11-13 16:18
- **Phiên bản hiện tại**: **Version 93** (`HDTOOL_Setup_Version_93.exe`)
- **Bộ cài lưu trữ**: `HDTOOL_Setup_Version_92.exe` và `HDTOOL_Setup_Version_93.exe` (0 file trên Desktop)

## 1. Công việc vừa hoàn thành
- **Nâng cấp đột phá tính năng Xuất Bản Vẽ Sang AutoCAD & Quy Đổi Layer MEP theo lệnh Sếp Hồ**:
  1. **Tự động nhận diện chính xác các hệ thống MEP đang hiển thị trong View (Loại bỏ 100% hệ thống bị ẩn)**:
     - Hàm `ScanVisibleMepSystemsInViews` quét trực tiếp các đối tượng MEP visible trong View/Sheet được chọn.
     - Tự động loại bỏ triệt để các phần tử bị ẩn qua:
       * `Hide Elements in View` (`elem.IsHidden(view)`)
       * View Filter bị tắt Visibility (`view.GetFilterVisibility(filterId) == false`)
       * Category bị tắt trong View (`view.GetCategoryHidden(catId)`)
     - Bảng quy đổi chỉ hiển thị đúng các hệ thống MEP thực tế đang xuất hiện trên bản vẽ, không còn hiển thị danh sách tĩnh hàng chục dòng thừa thãi.
     - Khi chuyển đổi qua lại giữa *View Đang Vẽ*, *Danh Sách Sheet*, hoặc *Danh Sách View Dự Án*, hệ thống tự động quét lại và cập nhật bảng tương ứng.
  2. **Bảng màu AutoCAD Select Color ACI chuẩn 100% (`CadColorPickerDialog.cs`)**:
     - Tái hiện chính xác 100% hộp thoại `Select Color` của AutoCAD (giao diện, tab Index Color, ma trận màu 24 cột x 10 hàng, hàng màu chuẩn 1-9, hàng sắc xám 250-255, nút ByLayer, ByBlock, khung preview màu, ô nhập Color: 1-255).
     - Khi click đúp hoặc click vào ô cột `Màu Sắc CAD`, cửa sổ chọn màu AutoCAD lập tức xuất hiện cho người dùng chọn trực quan.
     - Tự động tính toán và hiển thị chính xác mã Index Color và giá trị RGB (như Index 134: `0,127,127`, Index 1: `255,0,0`...).
  3. **Cho phép đổi tên Layer CAD tùy ý & Lưu cấu hình mặc định**:
     - Cột *Tên Layer CAD (Xuất Ra)* cho phép người dùng gõ sửa đổi tên theo ý muốn.
     - Sau khi lưu hoặc xuất, Add-in tự động ghi nhớ cấu hình vào file JSON `%APPDATA%\Autodesk\Revit\Addins\HDTOOL\mep_cad_layer_mapping.json`. Lần sau mở bất kỳ dự án nào, khi quét thấy hệ thống đó sẽ tự động áp dụng đúng Tên Layer và Màu Sắc đã lưu.
  4. **Kế thừa Modeless 100% & Phím ESC thoát lệnh ngay lập tức**:
     - Cả cửa sổ chính và hộp thoại chọn màu CAD đều kế thừa `HDToolBaseForm`, ấn phím `ESC` là đóng/thoát lệnh ngay lập tức.

## 2. File liên quan
- `Source/CadColorPickerDialog.cs` (Hộp thoại chọn màu chuẩn AutoCAD ACI)
- `Source/CadExportMepService.cs` (Thêm `ScanVisibleMepSystemsInViews`, xử lý View Filter, Category Hidden)
- `Source/ExportCadOptionsForm.cs` (Tích hợp quét View tự động, gắn sự kiện chọn màu CAD trực quan)
- `Source/CadLayerMappingModels.cs` (Đồng bộ chuẩn màu ACI RGB)
- `SKILL.md`, `.agents/skills/hdtool/SKILL.md`
- `Build.ps1`, `VERSION.txt`
