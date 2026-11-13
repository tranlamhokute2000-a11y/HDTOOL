# TRẠNG THÁI LÀM VIỆC GẦN NHẤT (SESSION STATE)

- **Thời gian cập nhật**: 2026-11-13 16:50
- **Phiên bản hiện tại**: **Version 95** (`HDTOOL_Setup_Version_95.exe`)
- **Bộ cài lưu trữ**: `HDTOOL_Setup_Version_94.exe` và `HDTOOL_Setup_Version_95.exe` (0 file trên Desktop)

## 1. Công việc vừa hoàn thành
- **Khắc phục triệt để lỗi "Tên Layer và Màu sắc CAD không thay đổi theo thiết lập công cụ, vẫn giữ tên mặc định template Revit"**:
  1. **Nguyên nhân gốc rễ**:
     - Trong file DWG do Revit xuất ra: Tên layer ống nước và phụ kiện mang tiền tố dự án/template mặc định (như `DSC_P-PIPE-____-OTLN P_VENT PIPE _TH_-1`, `DSC_P-PIPE-____-CNTR-1`, `DSC_P-PIPE-____-OTLN-1` hoặc `P-PIPE-MED_`). Ký tự dấu ngoặc đơn `(` và `)` trong tên hệ thống `P_VENT PIPE (TH)` bị Revit thay thế thành dấu gạch dưới `_TH_`.
     - Script AutoLISP trước đó tìm kiếm layer bằng `(wcmatch layUpper "*P_VENT PIPE (TH)*")`, do đó bị trượt (không khớp ký tự dấu ngoặc), dẫn đến bỏ qua hoàn toàn không di chuyển đối tượng nào sang layer đích.
     - Đồng thời, các nét tim ống (`CNTR`) và viền ống (`OTLN`) không mang tên hệ thống nên bị bỏ sót; và nếu file DWG đang được mở trong AutoCAD bởi người dùng thì thao tác ghi đè file bị từ chối do file lock.
  2. **Giải pháp khắc phục toàn diện (Dual-Action Strategy)**:
     - **Tầng 1 (Revit API - `ApplyLayerMappingToCategories`)**:
       * Duyệt qua toàn bộ `ExportLayerKey` trong `ExportLayerTable`: Mọi category/subcategory có chứa từ khóa `"Pipe"` (hoặc `"Duct"`) đều được định hướng sẵn sang `TargetCadLayerName` và mã màu ACI do người dùng chọn ngay từ khi xuất.
     - **Tầng 2 (AutoCAD Core Engine hậu kỳ - AutoLISP thông minh)**:
       * **Xử lý chuyên sâu cho View 1 hệ thống (Single System per Group)**: Khi trong View chỉ có 1 hệ thống Pipes (như `P_VENT PIPE (TH)`), AutoLISP tự động chuyển toàn bộ 100% đối tượng trên mọi layer chứa `*PIPE*` (tim ống, viền ống, phụ kiện, v.v.) sang layer đích `TargetCadLayerName` (ví dụ `P-PIPE-THOATNUOC`).
       * **Trích xuất System Tokens linh hoạt**: Chuẩn hóa ký tự đặc biệt, chuyển dấu ngoặc `()` thành `_` và tách từ khóa (`VENT`, `TH`, `PIPE`...), đảm bảo so khớp chuẩn xác 100% bất kể cách đặt tên của Revit template.
       * **Chuyển màu ByLayer**: Đặt màu tất cả đối tượng về ByLayer (`256`), xóa bỏ TrueColor override, và đặt màu của Layer đích thành đúng mã màu ACI (Index 1-255).
       * **Cơ chế chống khóa file (File Lock Resilience)**: Thử ghi đè an toàn với retry; nếu file đang mở trong AutoCAD thì tự động xuất sang file dự phòng `{Tên_Bản_Vẽ}_REMAP.dwg` và thông báo rõ ràng cho Sếp Hồ.
  3. **Biên dịch & Đóng gói**:
     - Biên dịch thành công 0 lỗi -> **HDTOOL Version 95** (`HDTOOL_Setup_Version_95.exe`).
     - Tự động đồng bộ Hot Reload sang `%APPDATA%\Autodesk\Revit\Addins\2024\HDTOOL`.
     - Giữ nguyên tắc chỉ lưu 2 version gần nhất (Version 94 và Version 95), 0 file trên Desktop.

## 2. File liên quan
- `Source/CadExportMepService.cs`
- `Source/ExportCadOptionsForm.cs`
- `Build.ps1`, `VERSION.txt`
- `SKILL.md`, `.agents/skills/hdtool/SKILL.md`
