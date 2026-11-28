# TRẠNG THÁI LÀM VIỆC GẦN NHẤT (SESSION STATE)

- **Thời gian cập nhật**: 2026-11-28
- **Phiên bản hiện tại**: **Version 125** (`HDTOOL_Setup_Version_125.exe`)
- **Bộ cài lưu trữ**: `HDTOOL_Setup_Version_124.exe` và `HDTOOL_Setup_Version_125.exe` (0 file trên Desktop)
- **Git**: Version 112 đã commit (`f3390e3`), Version 113–114 (Mac Sáng + Menu cột trái) đã commit (`97ccf1a`). Version 115–125 + sửa quy tắc 16h50 đã commit ngày 2026-11-28.

## 0. Vừa sửa — Mất đoạn SAD:400x250 trong Revit (Version 125)
- Nguyên nhân gốc (từ `CadDuct_scan.log` 13:32): Revit 2025.5 gộp đoạn 400x250 + cổ loe sát đầu thành 1 PolyLine 9 điểm đi qua 2 đỉnh 2 lần -> không phân loại được. DWG là 2 hình rời nên trước đó không tái hiện được.
- Sửa: `SplitPinchedPolyline` + `AddPolylinePart`; cổ loe chọn ống chính lệch vách ít nhất (trước lấy ống xét sau cùng -> khúc 200mm bị gắn chân rẽ, không thành côn thu).
- Kiểm tra: nạp đúng nét từ nhật ký quét, không sửa ra đúng 17 ống như Revit; có sửa ra 18 ống, 400x250 dài 7359 + côn 400x250->400x150 + 2 chân rẽ ở hộp VCD/FD. Tầng 1: kết quả không đổi (34 ống). Build 125 0 lỗi.
- ĐÃ THỬ trong Revit 2025.5 (28/11 14:33, tes.dwg): `CadDuct_build.log` 18 ống, chưa dựng được 0; journal: 18 ống, 1 côn thu, 0 cút, 14 chân rẽ (14 transaction Tap thành công, kể cả chân rẽ rộng bằng ống chính). Chưa xem bằng mắt hướng vát chân rẽ trên mô hình.
- Còn lại: đoạn dài 1427mm tại y=7536 (cạnh ghi chú TEAD:200x150) bị loại vì block tiêu âm 820mm đè giữa đoạn (quy tắc loại thiết bị, chưa đổi, cần Sếp quyết).

## 0x. Mọi tay nhánh dựng bằng chân rẽ (Version 123)
- Sếp: "các vị trí tay nhánh quy về vẽ bằng chân rẽ, không cần chạc 3". Thêm `NormalizeBranchesToTaps` / `ConnectSplitBranchesToTrunk`.
- Ngoài Revit: tes.dwg hộp VCD/FD -> ống đứng 400x250 là chính, 2 chân rẽ (400x250 và 200x150); nhánh cuối tuyến cút -> chân rẽ; tầng 1 đổi 1 cút -> chân rẽ, còn lại giữ nguyên. Build 123 0 lỗi.
- CHƯA thử trong Revit. Rủi ro: chân rẽ rộng 400 / cao 250 trên ống chính 400x250 (nhánh bằng ống chính) Revit có thể từ chối -> xem `%LOCALAPPDATA%\HDTOOL\Logs\CadDuct_build.log`.

## 0y. 2 đoạn ống khác cỡ không dựng được (Version 122)
- Sếp gửi ảnh `SAD:400x150` | `SAD:400x250`. Bản vẽ thử: `C:\Users\Nguye\Downloads\tes.dwg` (xref HVAC 7~16F).
- Tìm được ngoài Revit: khúc chuyển 200mm bị coi là ống 400x150; ống 400x250 bị kéo dài 18mm đè lên khúc đó để đón chân rẽ. CHƯA tái hiện được chính xác vì sao Revit không có đoạn 400x250 (không chạy được Revit) -> đã thêm nhật ký dựng.
- Sửa: `ConvertInlineReducers`; không kéo đầu ống đã nối côn; mỗi côn/cút 1 Transaction; ghi `CadDuct_build.log`; siết khớp ghi chú / lan truyền 25mm (3 ống nhánh 150 bị gán 200x150 đã đúng); chặn vùng loại trừ > 8000mm (HVAC tầng 1 từng bị lọc sạch 42 -> 0, nay 34 ống); sửa đơn vị `keeper.Length` (feet -> mm).
- Kiểm tra: KiemTra ĐẠT, build 122 0 lỗi; chạy ngoài Revit 2 bản vẽ (tes: 18 ống + 1 côn đúng chỗ). CHƯA thử trong Revit.
- Còn lại: chạc 3 ngả ở hộp VCD/FD (400x250 -> rẽ xuống 400x250 + đi tiếp 200x150) chưa nối ống 200x150; tuyến TEAD phía dưới chưa nhận đoạn 200x150; tầng 1 còn các khối W1200-3040 xa tuyến.

## 0z. Chuột kiểu Revit trong 2 khung 3D (Version 121)
- `CadDuctPreviewCanvas`, `FamilyMeshViewer`: giữ chuột giữa kéo view (con trỏ 4 mũi tên ở khung ống gió), Shift + giữa xoay; sửa dòng hướng dẫn. Giả lập chuột trên cửa sổ ống gió ngoài Revit: đúng. CHƯA thử trong Revit.

## 0a004. Cửa sổ xem trước 3D cho CAD -> Ống gió (Version 120)
- Sếp chốt bản mẫu. File mới `Source/CadDuctPreviewCanvas.cs`, `Source/CadDuctPreviewForm.cs`; sửa `CadToDuctForm` (tự mở sau quét, nút Xem 3D, bỏ đoạn trước khi dựng), `CadDuctModels.IsExcluded`, `CadToDuctService.LastCadLinework`.
- Kiểm tra: KiemTra ĐẠT, build 120 0 lỗi; mở cửa sổ thật ngoài Revit với dữ liệu HVAC tầng 1 (3 ảnh đúng, cả cỡ 900x600). CHƯA thử trong Revit: tự mở sau quét, ESC chỉ đóng cửa sổ 3D, dựng khi có đoạn bị bỏ, DPI 125/150%.

## 0a003. Sửa nhận diện ống gió vẽ lung tung (Version 119)
- Bản vẽ thử: `M:\2026. Dự án Xuân La\ADD_REVIT\HVAC TẦNG 1.dwg`. Nguyên nhân: đọc mọi layer -> hơn 1.100 đối tượng, chỉ khoảng 40 trên layer ống (còn lại kiến trúc, nội thất, bãi xe, điện).
- Sửa: tự chọn layer ống gió + bỏ ký hiệu miệng gió. Chạy thử ngoài Revit (mô phỏng hình học Revit bằng dữ liệu accoreconsole, CHƯA gồm khớp Text WxH): 43 ống thẳng + 24 phụ kiện, tuyến chính khớp; còn 14 chỗ chưa nối (có vài khối lớn W1200–3040 nằm xa tuyến, trên layer ống).
- CHƯA thử trong Revit.

## 0a002. Tăng tốc Đổi Category (Version 118)
- Sếp báo chuyển đổi hơi nặng. Bỏ phần thừa của HDTOOL sau khi đổi: quét lại dự án + dựng lại hình 3D (có thể phải đặt tạm + Regenerate). Giờ chỉ sửa đúng dòng.
- Thanh trạng thái hiện thời gian: tổng / mở ngầm / Revit nạp vào dự án. CHỜ Sếp thử trong Revit và gửi số đo. CHƯA đo thật.

## 0a001. Đổi Category trực tiếp trong Tổng Hợp Family (Version 117)
- Sếp chốt bản xem trước `PreviewStudio/HDTool_FamilyDoiCategory.html`.
- `Source/FamilyCategoryService.cs` (mới) + khung "Đổi Category" trong `FamilyPaletteForm` (khung 3D còn 200px, bảng thông số 112px). Sửa kèm: thanh cuộn ngang thừa ở cột phải; Làm Mới khi đang lọc không còn làm mất Family khỏi danh sách.
- Kiểm tra: KiemTra ĐẠT, build 117 0 lỗi, chụp form ngoài Revit đúng. CHƯA thử đổi Category trong Revit thật (đối tượng đã vẽ, Workshared, cặp Category bị từ chối).

## 0a000. Mọi Family đã load đều xem 3D xoay được (Version 116)
- Lỗi: Type chưa kích hoạt / chưa đặt -> `symbol.get_Geometry` rỗng -> chỉ hiện ảnh tĩnh ("Family không có hình khối 3D").
- `FamilyPaletteService.GetSymbolGeometry`: thêm giao dịch tạm (Activate / đặt thử theo `FamilyPlacementType`, tường tạm cho Family gắn tường/mặt) rồi RollBack; gom thêm đường nét (Curve/PolyLine).
- `FamilyMeshViewer.SetGeometry(tri, lines)`: vẽ đường nét; Family 2D (Tag) -> ảnh xem trước xoay như tấm phẳng.
- Kiểm tra: KiemTra ĐẠT; chụp viewer ngoài Revit (ảnh 2D + khung nét) đúng. CHƯA thử trong Revit (đặc biệt: giao dịch tạm trên dự án Workshared, Family gắn trần/sàn, Tag).

## 0a00. Tổng Hợp Family theo giao diện Mac Sáng (Version 115)
- Sếp chê form Tổng Hợp Family xấu, chữ không nổi, ô tìm đen không rõ công dụng.
- `FamilyPaletteForm`: bỏ toàn bộ màu tối cứng -> token `HDToolTheme`; ô tìm thay bằng `HDToolSearchBox` (nhãn "Lọc Family:", gợi ý, Ctrl+F); ComboBox nền sáng; chữ bảng đậm/rõ (Text/Text2), dòng 36px; nút "Đặt Vào Bản Vẽ" tự vẽ bo góc đều mọi dòng; tiêu đề khung phải bỏ dấu `&` bị nuốt; thêm phím `F5` làm mới (trước chỉ ghi nhãn).
- Kiểm tra: KiemTra ĐẠT; chụp form thật ngoài Revit bằng thư viện giả `RevitAPIUI` (scratchpad `stubui`) — đúng. CHƯA thử trong Revit.

## 0z. Menu HDTOOL cột trái kiểu Mac (Version 114)
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
