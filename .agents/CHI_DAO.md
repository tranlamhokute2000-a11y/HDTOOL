# CHỈ ĐẠO KỸ THUẬT CHO ANTIGRAVITY (do Claude soạn — BẮT BUỘC TUÂN THỦ)

> **Vai trò**
> - **Sếp Hồ**: người quyết định cuối cùng (duyệt giao diện, duyệt gộp vào `main`, duyệt push GitHub).
> - **Claude**: kiểm soát kỹ thuật — giao nhiệm vụ, đặt tiêu chí, kiểm tra kết quả, chỉ ra lỗi bắt buộc sửa. Chỉ Claude được sửa file này.
> - **Antigravity**: thực thi — làm đúng nhiệm vụ đang giao, tự kiểm tra, báo cáo, sửa theo kết luận kiểm tra.
>
> Nếu chỉ đạo trong file này mâu thuẫn với yêu cầu trực tiếp của Sếp Hồ → **làm theo Sếp Hồ** và ghi rõ vào báo cáo.

---

## 1. QUY TRÌNH MỖI NHIỆM VỤ (Antigravity làm đúng thứ tự)

0. **Đọc `.agents/LENH_SEP.md`**: thực hiện các lệnh `MỚI` gửi `ANTIGRAVITY` hoặc `CẢ HAI`, xong đổi thành `ĐÃ NHẬN (Antigravity)`.
1. **Đọc file này** và lấy nhiệm vụ có trạng thái **`ĐANG GIAO`** ở mục 4. Mỗi lần chỉ làm **1 nhiệm vụ**. Không tự làm nhiệm vụ `CHỜ` hay việc ngoài phạm vi.
2. **Tạo nhánh riêng** từ `main`: `git checkout -b anti/<mã-nv>` (ví dụ `anti/nv-01`). **Không commit thẳng `main`, không merge, không push.**
3. **Làm việc** theo đúng "Tiêu chí đạt" của nhiệm vụ và các quy tắc ở mục 2.
   - Thay đổi giao diện mới (chưa có trong `PreviewStudio/HDTool_ThemeStudio.html`) → phải cập nhật Preview Studio và chờ Sếp Hồ duyệt trước khi sửa C#.
4. **Tự kiểm tra** bằng: `powershell -ExecutionPolicy Bypass -File .\.agents\tools\KiemTra.ps1` → phải ra **`KET QUA: DAT`**.
   Sau đó chạy `Build.ps1` (0 lỗi), cập nhật `SKILL.md` + `.agents/skills/hdtool/SKILL.md` (2 bản giống nhau) + `.agents/SESSION_STATE.md`.
5. **Commit trên nhánh** với thông điệp `<loại>: <mô tả> - Version N` (loại: `feat` / `fix` / `refactor` / `docs`).
6. **Ghi báo cáo** vào `.agents/BAO_CAO.md` theo mẫu có sẵn, trạng thái `CHỜ KIỂM TRA`.
7. **DỪNG LẠI** và nói với Sếp Hồ: *"Xong NV-xx, nhờ Sếp bảo Claude kiểm tra."* Không làm tiếp nhiệm vụ khác.
8. Khi Claude ghi kết quả ở mục 5:
   - **`CẦN SỬA`** → sửa đúng các mục được liệt kê trên cùng nhánh, chạy lại bước 4–7.
   - **`ĐẠT`** → chờ Sếp Hồ quyết định gộp. Nhiệm vụ tiếp theo sẽ được Claude chuyển sang `ĐANG GIAO`.

---

## 2. QUY TẮC KỸ THUẬT BẮT BUỘC (vi phạm = CẦN SỬA)

| # | Quy tắc | Cách đúng |
|---|---|---|
| 1 | Chỉ C# 5 / .NET Framework 4.8 (csc.exe) | Không `?.`, không `$"..."`, không `nameof`, không thuộc tính `=>` |
| 2 | Không dùng `ElementId.IntegerValue` (bị bỏ từ Revit 2026) | `ElementId.Value` (`long`); Category: `(int)id.Value` |
| 3 | Mọi Form kế thừa `HDToolBaseForm` | Tự có ESC, DPI, giao diện kỹ thuật |
| 4 | Cửa sổ công cụ mở Modeless | `HDToolModelessHost.ShowModeless(form, hwnd)`; `ShowDialog` chỉ cho hộp thoại con ngắn (chọn file/màu) |
| 5 | Mọi thao tác Revit từ Form qua ExternalEvent | `HDToolModelessHost.Run(app => ...)`; không gọi Transaction/Pick trực tiếp trong sự kiện Click; không `ExternalEvent.Create` ngoài API context |
| 6 | Ghi đúng dự án gốc | Trong lambda dùng `HDToolModelessHost.ResolveOwnerDocument(_doc)`, **không** `app.ActiveUIDocument.Document` |
| 7 | Transaction nhiều pha (đổi tên/số tạm) | Có lỗi → `RollBack` toàn bộ; cập nhật trạng thái bảng sau `Commit` |
| 8 | Không emoji, không chuỗi lỗi mã hóa | Chữ tiếng Việt chuẩn hoặc `● ■ ▲ ► ✔ ✖` |
| 9 | Màu & font lấy từ `HDToolTheme` | Không tự đặt màu cầu vồng; nút: `HDToolTheme.StyleButton(btn, HDToolButtonKind.Primary/Secondary/Danger)` |
| 10 | Chữ tự vẽ bằng `TextRenderer.DrawText` | Luôn có `TextFormatFlags.NoPrefix` |
| 11 | Không đổi `HeaderText` cột bảng của MEP | `MepQuantityInspectorForm` dùng `HeaderText` làm tên Parameter |
| 12 | Không đường dẫn cứng tới máy Sếp (`c:\Users\Nguye\...`) | `Assembly.Location`, `%APPDATA%`, đường dẫn tương đối |
| 13 | Bố cục không che khuất, không tràn mép ở DPI 100/125/150% | `TableLayoutPanel`, `Dock`, `Anchor`, `AutoSize`; không tọa độ cứng cho vùng co giãn |
| 14 | Không file nào ra Desktop; chỉ giữ 2 bộ cài gần nhất | Theo `Build.ps1` |
| 15 | Không push GitHub khi Sếp chưa ra lệnh | — |

---

## 3. CLAUDE KIỂM TRA NHỮNG GÌ
- `git diff main...anti/<mã-nv>`: đọc từng thay đổi, tìm lỗi logic Revit API, luồng ExternalEvent, rò rỉ sự kiện, ngoại lệ bị nuốt.
- `KiemTra.ps1` (biên dịch 0 lỗi 0 cảnh báo + quét quy tắc cấm).
- Giao diện: chụp thử Form ngoài Revit (nếu Form không cần RevitAPIUI) hoặc yêu cầu Sếp Hồ chụp màn hình trong Revit.
- Đối chiếu "Tiêu chí đạt" của nhiệm vụ, tài liệu SKILL / SESSION_STATE.

---

## 4. DANH SÁCH NHIỆM VỤ

### NV-01 — Sửa tràn mép phải Form Sửa Số Hiệu Bản Vẽ Hàng Loạt — `ĐANG GIAO`
- **File**: `Source/BatchEditSheetNumberForm.cs`
- **Hiện trạng**: ở kích thước mặc định 996x759, nhóm "Phạm Vi Áp Dụng" và ô "Thay thế bằng (Tiền tố mới)" bị cắt ở mép phải (lỗi có từ trước Version 108).
- **Tiêu chí đạt**:
  1. Mọi GroupBox, Label, TextBox, nút nằm trọn trong Form ở kích thước mặc định và khi kéo Form nhỏ tới `MinimumSize`.
  2. Vùng nhập liệu tự co giãn theo chiều rộng (Dock/Anchor/TableLayoutPanel), không tọa độ X cứng cho phần bên phải.
  3. Không đổi logic thay thế số hiệu; không đổi tên control đang được code khác dùng.
  4. `KiemTra.ps1` ĐẠT; build 0 lỗi.

### NV-02 — Ô chỉ số tổng (KPI) trên bảng Khối Lượng Ống Gió — `CHỜ`
- **File**: `Source/DuctQuantityCommand.cs` (`DuctQuantityForm`), có thể thêm control vào `Source/HDToolTheme.cs`.
- **Mẫu đã duyệt**: tab "03 Khối Lượng Ống Gió" trong `PreviewStudio/HDTool_ThemeStudio.html`.
- **Tiêu chí đạt**: dải 4 ô `TỔNG DIỆN TÍCH TÔN (m²)`, `CHIỀU DÀI ỐNG THẲNG (m)`, `SỐ ĐOẠN ỐNG`, `PHỤ KIỆN, VAN, MIỆNG GIÓ` (số Consolas, khung góc ngắm) đặt ngay trên bảng; cập nhật cùng chỗ đang tính `lblSummary`; không che bảng, co giãn theo chiều rộng; số liệu khớp dòng tổng cộng hiện có.

### NV-03 — Sơ đồ kích thước xem trước ở Căn Chỉnh Grids / Level — `CHỜ`
- **File**: control mới `HDToolDimensionPreview` (trong `Source/HDToolTheme.cs` hoặc file riêng), gắn vào `Source/CropAlignOptionForm.cs` tab Grids.
- **Mẫu đã duyệt**: tab "04 Căn Chỉnh Grids / Level" trong Theme Studio.
- **Tiêu chí đạt**: vẽ GDI+ khung Crop nét đứt, trục + bubble, đường kích thước có mũi tên và trị số mm; cập nhật ngay khi đổi giá trị offset / hướng; tự co giãn; không thao tác Revit API trong `OnPaint`.

### NV-04 — Bỏ đường dẫn cứng `c:\Users\Nguye\...` — `CHỜ`
- **File**: `Source/App.cs` (4 chỗ `EnsureXxxButtonDynamic`), `Source/ToolHotReloadManager.cs` (`FindLatestBuildDll`).
- **Tiêu chí đạt**: dùng đường dẫn Assembly / `%APPDATA%\Autodesk\Revit\Addins\<năm>\HDTOOL`; `KiemTra.ps1 -All` không còn lỗi "duong dan cung".

### NV-05 — Đóng Form Modeless khi dự án gốc bị đóng — `CHỜ`
- **Tiêu chí đạt**: Form giữ `_doc` tự đóng (hoặc vô hiệu hóa nút thao tác) khi `DocumentClosing` của đúng tài liệu đó; hủy đăng ký sự kiện khi Form đóng.

### NV-06 — `OnViewActivated` không tự mở Transaction ngầm — `CHỜ`
- **File**: `Source/App.cs`, `Source/MepQuantityService.cs` (`CheckAndRescaleAllTables`).
- **Tiêu chí đạt**: không tạo bước Undo ẩn khi chỉ mở Sheet; chỉ cập nhật tỷ lệ khi người dùng bấm lệnh, hoặc qua ExternalEvent kèm thông báo.

### NV-07 — Dọn code chết & file rác — `CHỜ`
- Xóa `CheckVarPlumbingForm.cs` + `CheckVarPlumbingCommand` (đã bỏ khỏi Ribbon); mockup tương ứng trong Preview Studio.
- `Build.ps1`: tự xóa `HDTOOL_old_*.dll` không bị khóa trong `%APPDATA%\Autodesk\Revit\Addins\<năm>\HDTOOL`.

---

## 5. KẾT QUẢ KIỂM TRA CỦA CLAUDE
_(Claude ghi sau mỗi lần kiểm tra, mục mới nhất ở trên cùng. Antigravity đọc mục này trước khi làm tiếp.)_
_(Định dạng tiêu đề: `### NV-xx — lần N — ĐẠT` hoặc `### NV-xx — lần N — CẦN SỬA`, dòng dưới là gạch đầu dòng từng lỗi / nhận xét. Điều Phối Studio đọc đúng định dạng này.)_

- Chưa có.
