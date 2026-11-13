# TRẠNG THÁI LÀM VIỆC GẦN NHẤT (SESSION STATE)

- **Thời gian cập nhật**: 2026-11-13 17:08
- **Phiên bản hiện tại**: **Version 97** (`HDTOOL_Setup_Version_97.exe`)
- **Bộ cài lưu trữ**: `HDTOOL_Setup_Version_96.exe` và `HDTOOL_Setup_Version_97.exe` (0 file trên Desktop)

## 1. Công việc vừa hoàn thành
- **Tái tạo 100% toàn bộ Icon trên thanh Ribbon Revit theo đúng chuẩn SVG của Preview Studio Web**:
  1. **Đồng bộ hóa giao diện đồ họa cao cấp**:
     - `CreateCropAlignCombinedIcon`: Tái tạo chuẩn SVG `CropAlignCombined` với khung viền crop nền tối nét đứt, trục lưới xanh dương #0284c7 có 2 bong bóng tròn số 1 và 2 viền xanh, đường cao độ đỏ #ef4444 nét đứt kèm mũi tên đỏ và mũi tên cam ở đáy.
     - `CreateMepQuantityIcon`: Tái tạo chuẩn SVG `MepQuantity` (Thống Kê Ống Nước) với ống ngang, ống đứng rẽ nhánh, viền bích xanh sky #38bdf8, giọt nước xanh cyan #06b6d4 và huy hiệu bảng khối lượng nhỏ màu vàng cam #f59e0b có 2 vạch kẻ trắng.
     - `CreateFitScaleIcon`: Tái tạo chuẩn SVG `FitScale` (Khớp Tỷ Lệ Bảng Khối Lượng) với khung tối, thanh tiêu đề xanh #0284c7, lưới bảng tính #1e293b và mũi tên co giãn hai bên #38bdf8.
     - `CreateDuctQuantityIcon`: Tái tạo chuẩn SVG `DuctQuantity` (Khối Lượng Ống Gió) với khối hộp 3D Isometric tôn mạ kẽm (3 mặt chiếu #94a3b8, #cbd5e1, #64748b) và huy hiệu tròn cam #f59e0b viền trắng có chữ **m²** sắc nét.
     - `CreateCadToDuctIcon`: Tái tạo chuẩn SVG `CadToDuct` (Dựng Ống Gió Từ CAD Link) với thân ống CAD #38bdf8 có chữ **CAD** đậm, đường tim đứt trắng, cút cam rẽ nhánh #f59e0b, ống rẽ xanh lá #10b981 và dấu nhân X xanh sky.
     - `CreateSheetListIcon`: Tái tạo chuẩn SVG `SheetList` (Xuất Danh Mục Bản Vẽ) với tờ tài liệu gập mép trắng, các dòng kẻ xanh/xám và huy hiệu tròn Excel xanh lá #16a34a viền trắng có chữ **X** đậm.
     - `CreateUpdateSheetListIcon`: Tái tạo chuẩn SVG `UpdateSheetList` (Cập Nhật Danh Mục) với tờ tài liệu gập mép và cung mũi tên xoay tròn xanh #0284c7.
     - `CreateExportCadIcon`: Tái tạo chuẩn SVG `ExportCad` (Xuất CAD Quy Đổi Layer) với màn hình CAD nền đen xanh #0f172a, tiêu đề chữ **CAD / DWG** xanh sky #38bdf8, 3 thanh layer màu (Cyan, Hồng, Xanh lá) và nút tam giác cam Play/Export.
     - `CreateUpdateToolIcon`: Tái tạo chuẩn SVG `UpdateTool` (Cập Nhật Tool) với vòng tròn nét đứt ngoài #38bdf8, cung mũi tên xoay tròn xanh #0284c7 và chấm tâm cam #f59e0b.
     - `CreateHDToolMenuIcon` / `CreateHDToolIcon`: Tái tạo chuẩn SVG `HDToolMenu` với huy hiệu tròn nền đen xanh viền #0284c7, chữ **HD** xanh sky và chữ **TOOL** vàng cam rực rỡ.
- **Loại bỏ hoàn toàn tính năng `CHECK VAR PLUMBING` theo lệnh Sếp Hồ**:
  - Xóa nút `cmdHDToolPickTableHighlight` ("CHECK VAR\nPLUMBING") khỏi Ribbon `Source/App.cs`. Panel `Khối Lượng MEP` hiện tại gồm 4 nút chuẩn: `Thống Kê Ống Nước`, `Khớp Tỷ Lệ Bảng Khối Lượng`, `Khối Lượng Ống Gió`, `Dựng Ống Gió Từ CAD`.
  - Xóa nút `cmdHDToolPickTableHighlight` khỏi cấu hình `DEFAULT_LAYOUT` trong `PreviewStudio/HDTool_PreviewStudio.html`.
  - Loại bỏ hoàn toàn các nhắc đến tính năng đã bỏ trong `SKILL.md` và `.agents/skills/hdtool/SKILL.md`.
- **Biên dịch & Đóng gói**:
  - Biên dịch thành công 0 lỗi -> **HDTOOL Version 97** (`HDTOOL_Setup_Version_97.exe`).
  - Tự động nạp Hot Reload sang `%APPDATA%\Autodesk\Revit\Addins\2024\HDTOOL`.
  - Chỉ lưu 2 bản gần nhất (Version 96 và Version 97), 0 file trên Desktop.

## 2. File liên quan
- `Source/ResourceHelper.cs`
- `Source/App.cs`
- `PreviewStudio/HDTool_PreviewStudio.html`
- `Build.ps1`, `VERSION.txt`
- `SKILL.md`, `.agents/skills/hdtool/SKILL.md`
