---
name: hdtool
description: >-
  HDTOOL (Revit 2024–2027): Auto Crop (Vertex-Exact + Liang-Barsky), căn Grids/Level; CAD-to-Duct BIM (trích DWG accoreconsole, lọc giả 4 tầng, tách tuyến giao chéo, ống chính + Transition + Tap ưu tiên Family sẵn, chuẩn WxH); Khối lượng Ống gió TCVN/SMACNA & Ống nước (Live Tracking); Quản lý Sheet & Xuất CAD Quy Đổi Layer MEP (View đang vẽ / Sheet / View dự án; tự động chuyển đổi đối tượng sang đúng Layer & Màu sắc ACI qua AutoLISP entmake/entmod; tự động lưu và nạp cấu hình mặc định); Tổng Hợp Family 3D (Palette Bảng Ô Cột Siêu Mượt, Toolbar Khoa Học Dropdown, Lazy Load 3D Preview, Click/Double-Click Đặt Vào Bản Vẽ); 100% Modeless, ESC thoát lệnh & Hot Reload.
---

# Skill: HDTOOL — Bộ Công Cụ Autodesk Revit (Version 106)

## 1. Kiến Trúc & Quy Chuẩn Bắt Buộc
- **Build & Đóng gói**: Biên dịch thuần bằng `csc.exe` (C# 5, `.NET Framework 4.8`) qua `Build.ps1` -> `Build/HDTOOL.dll` và bộ cài độc lập `HDTOOL_Setup_Version_{N}.exe` (không dùng MSBuild/`.csproj`).
- **Dynamic Ribbon & Hot Reload**: Tab `HDTOOL` gồm 5 Panel + Menu HDTOOL trung tâm. `ToolHotReloadManager` nạp `HDTOOL.dll` vào bộ nhớ (`byte[]` -> `AppDomain`) không khóa file (`HasActiveHotReloadAssembly`) và tự bơm nút mới (`EnsureAutoCropButtonDynamic`, `EnsureCadToDuctButtonDynamic`, `EnsureExportCadButtonDynamic`, `EnsureFamilyPaletteButtonDynamic`).
- **Khởi động & Nạp Lệnh An Toàn**: File `HDTOOL.addin` đăng ký đầy đủ 17 Command (gồm `ExportCadCommand` và `FamilyPaletteCommand`).
- **Modeless 100% & Thoát Phím `ESC`**: Mọi công cụ chạy qua `HDToolModelessHost` (tuyệt đối không dùng `ShowDialog()` khóa Revit) và mọi Form kế thừa `HDToolBaseForm` (`KeyPreview = true`, `ProcessCmdKey(Keys.Escape)`, gọi `Activate()` sau thao tác `Pick`).
- **UI & Lưu Trữ**: Dùng `TableLayoutPanel` 4 tầng chống che khuất 100%, thiết lập `SplitterDistance` an toàn trong `OnShown`, tự co giãn `AdjustInspectorLayout` chống tràn nút/cắt chữ, tuyệt đối không dùng emoji gây lỗi ô vuông (``); lưu trữ chỉ giữ các bản mới nhất trong thư mục dự án, `0` file trên Desktop.

---

## 2. Hệ Thống Chức Năng (4 Panel Thực Tế)

### Panel 1 — Căn Chỉnh Đối Tượng
1. **Auto Crop View (`AutoCrop`)**:
   - Trích xuất hình học đỉnh thực tế + **Liang-Barsky 3D Clipping** (`ClipSegmentLiangBarsky`) cắt theo `SectionBox` đang bật, giữ nguyên Z depth.
   - Cơ chế hiểu ý người dùng (`filterByCurrentCrop`): chỉ giữ đối tượng nằm trong hoặc chạm phạm vi Crop hiện tại, không giãn to ra trục ngoài; mặc định bỏ qua Link Kiến trúc/Kết cấu; hỗ trợ Plan, Section, Elevation, 3D và nhiều Viewport trên `ViewSheet`.
2. **Căn Chỉnh Grids (`GridCrop`), Level (`LevelCrop`), `CropAlign` & `SmartCrop`**:
   - `GridCropService` (4 hướng Top/Bottom/Left/Right trên Plan/Elevation/Section/Sheet) và `LevelCropService` (2 đầu Left/Right trên Section/Elevation/Sheet) căn mép trục/cao độ theo khoảng cách mm từ `CropBox` bằng `DatumExtentType.ViewSpecific` + `SetBubbleVisibility`.

---

### Panel 2 — Khối Lượng MEP & CAD-to-Duct BIM
1. **Dựng Ống Gió 3D Từ Hình Vẽ CAD Link (`CadToDuct`)**:
   - **Trích xuất DWG & QLEADER gốc (`CadDwgExtractor`, cache `_v6`)**: Chạy `accoreconsole.exe` trong thư mục ASCII an toàn; dùng `(textbox)`, `hd-text-bbox-wcs` và `(trans pt ent 0)` lấy đúng tâm & hộp chữ `TEXT`/`MTEXT`/`ATTRIB`/`MULTILEADER`/`DIMENSION`/`INSERT` (kể cả căn lề `72/73` và block lật gương); trích thêm `LEADER` (`QLEADER`) + `MULTILEADER` thành `LDR|arrow|tail|handle` để gán `LeaderArrowPoint` (kèm bộ lọc tránh hút nhầm leader của `ATT:`/`Grille`/Van).
   - **Nhận diện hình học, Quần ống gió & Chân rẽ đúng chiều CAD**: Nhận diện ống thẳng (`OrientShortCollarRectanglesByTopology` cho ống cổ ngắn `Length < Width`), cút cong (`Arc` & `PolyLine` 250 đỉnh), côn thu (`Transition`). Phân rã ngã 3 / chạc chữ Y / **Quần ống gió** (`MergeCollinearTapHosts` gộp ống chủ đồng trục) thành **tuyến ống chính thẳng liên tục + Côn đồng tâm + Chân rẽ `TakeoffTap`**. `ClassifyPolygonShape` trích vector vát 45° (`TapBevelDirection`) của chân rẽ CAD và `EnsureTakeoffTapBevelOrientation` tự lật `flipHand()`/`flipFacing()`/xoay 180° khớp đúng chiều vát thiết kế (hoặc hướng upstream qua `AssignFallbackTapBevelDirections`). `BuildDuctsInRevit` chạy `TransactionGroup` 3 pha (ống thẳng $\to$ `Transition`/`Collinear`/`Elbow` $\to$ mỗi `TakeoffTap` trong `txOneTap` riêng có `TryEnsureTapHostCoversBranch`) kết hợp `SilentFittingFailuresPreprocessor`, giữ nguyên Family sẵn có của `DuctType`.
   - **Lọc đối tượng giả 4 tầng (`FilterFalsePositiveSegments`)**: Loại hộp miệng gió X/V, miệng gió đầu nhánh (ống dừng đúng mép miệng gió), khung `IAQ`, thiết bị phòng máy (`ATT`, `MFD`/`OBD`/`NRD`, quạt, Hatch `ANSI37`) và **mũi tên chỉ hướng gió rỗng không kèm text** (`ArrowSymbol`, `IsAirflowArrowPolygon`, `TryMatchArrowHeadPair`) để không xóa nhầm ống thật chứa mũi tên (như `OAD 400x250`); bảo toàn ống bọc chống cháy `EI60`.
   - **Tách tuyến giao chéo**: Cô lập theo `ColorRgb`, Layer và mã hệ thống (`ExtractHvacSystemCode`); `DoSegmentsCrossThroughEachOtherXY` chặn nối nhầm tại giao lộ `XCROSS`; `DetectOverpassUnderpassCrossings` nối thông 2 nửa ống đồng trục bị ngắt nét bên dưới ống cắt ngang; `IsNoteSystemCompatibleWithDuct` chặn bắt chéo Text `OAD`/`EAD`.
   - **Kích thước (`W` trước `H` sau) & Ưu tiên Leader**: `RunLeaderArrowMatchTier` (Pass 1-L1/L2) ưu tiên khớp mũi tên `QLEADER`/`MULTILEADER` thẳng vào ống trước khi chạy Pass 1A–3B (`EI60` + BFS theo tuyến gần nhất); luôn giữ chuẩn **`W` trước, `H` sau**; ô Cao độ tim & chiều cao `H` khởi tạo trống (tự nhập).
2. **Khối Lượng Ống Gió HVAC (`DuctQuantity`)**:
   - Modeless 60 FPS + Live Tracking (`DocumentChanged`), tính $m^2$ tôn chuẩn **TCVN 5687:2010 / SMACNA**, tách Cút theo góc thực tế & `R150`, Côn thu theo tham số dài `L`, Lượn Z `(L, H)` và phụ kiện/van/miệng gió.
3. **Khối Lượng Ống Nước (`MepQuantity`)**:
   - Quét Pipe/Fitting theo View, Box hoặc Viewport; gộp đa tham số tên phụ kiện (`ExtractFittingName`), chuẩn hóa tên ống (`FormatCleanPipeName`), xuất Excel/`ViewSchedule`/`ViewDrafting`, soi sáng 2 chiều (`MepHighlightManager`).

---

### Panel 3 — Quản Lý Bản Vẽ, Sheet & Xuất CAD Quy Đổi Layer
1. **Xuất Danh Mục Bản Vẽ (`ExportSheetList`)**:
   - Quét Sheet theo cây thư mục (`RootFolder` / `SubFolder1` / `SubFolder2`), gom nhóm theo khung tên TitleBlock, xuất bảng Excel danh mục hoặc nạp vào Drafting View.
2. **Đổi Số Hiệu & Tên Sheet Hàng Loạt (`BatchEditSheetNumber`)**:
   - Đổi số hiệu tiền tố/hậu tố, tìm kiếm thay thế tên bản vẽ tức thì theo Regex.
3. **Xuất Bản Vẽ Sang AutoCAD & Quy Đổi Layer MEP Chuẩn Xác 100% (`ExportCadCommand`, `ExportCadOptionsForm`, `CadExportMepService`, `CadLayerMappingModels`)**:
   - **3 Chế độ nguồn xuất linh hoạt**: `ActiveView` (xuất ngay lập tức View mặt bằng/3D/mặt cắt đang mở vẽ mà không cần tạo Sheet), `Sheets` (xuất danh sách Sheet) và `ProjectViews` (xuất trực tiếp hàng loạt View dự án).
   - **Chuyển đổi đối tượng sang đúng Layer & Màu sắc ACI thực tế**:
     * Cấu hình `DWGExportOptions` + `ExportLayerTable` nạp sẵn Predefined Setup trong Document Revit, gán `ModifierType.Category` + `ModifierType.SystemType` cho mọi Category MEP để tách từng hệ thống sang Layer riêng biệt khi Revit xuất.
     * Xử lý hậu kỳ an toàn qua `accoreconsole.exe`: Copy DWG sang file tạm ASCII trong `%TEMP%` (tránh lỗi đường dẫn tiếng Việt và tránh khóa file), thực thi AutoLISP thuần túy (`entmake` & `entmod`) tạo Layer đích, gán mã màu ACI (1–255), chuyển đổi 100% đối tượng (entities) sang đúng Layer CAD đích và đổi thuộc tính màu về ByLayer (`256`).
   - **Ghi nhớ & Tự động nạp Cài Đặt Layer (Persistence)**:
     * Tự động lưu toàn bộ bảng cấu hình Layer vào file JSON `%APPDATA%\Autodesk\Revit\Addins\HDTOOL\mep_cad_layer_mapping.json` sau mỗi lần xuất CAD thành công hoặc khi bấm nút `💾 Lưu Mặc Định`.
     * Tự động nạp lại toàn bộ cấu hình Layer đã lưu mỗi khi mở Form, người dùng không cần thiết lập lại.
     * Bổ sung nút: `💾 Lưu Mặc Định`, `🔄 Nạp Đã Lưu`, `⚡ Lấy Từ Dự Án`, `➕ Thêm`, `📁 Xuất JSON`, `📂 Nhập JSON`.
     * Cột DataGridView hiển thị rõ ràng: `🔄 CHUYỂN ĐỔI SANG LAYER CAD (Tên Layer)` và `🎨 MÀU SẮC CAD (ACI 1-255)`.
4. **Xuất CAD/PDF Hàng Loạt (`ExportCadPdfOptionsForm` / `SheetExportCadPdfService`)**: Xuất PDF gộp, CAD rời hoặc **gộp nhiều Sheet thành 1 file DWG đa Layout** (`ExportToSingleCombinedCad` qua `accoreconsole.exe`).
5. **Cập Nhật Danh Mục Từ Excel (`UpdateSheetList`)**: Đối chiếu (`MatchAndCompare`) và cập nhật ngược tên/số hiệu Sheet hàng loạt (`ApplyUpdatesToDocument`).

---

### Panel 4 — Thư Viện & Family
1. **Tổng Hợp Family 3D (Palette Bảng Ô Cột Siêu Mượt & Toolbar Khoa Học) (`FamilyPaletteCommand`, `FamilyPaletteForm`, `FamilyPaletteService`, `FamilyPaletteModels`)**:
   - **Triệt tiêu 100% hiện tượng đơ giật lag**: Tối ưu quét metadata O(N) hoàn tất trong < 30ms, không gọi `GetPreviewImage` đồng bộ trong vòng lặp quét ban đầu; mở form lên ngay lập tức.
   - **Thanh Toolbar thiết kế KHOA HỌC, không che khuất**:
     * Khối 1: Ô tìm kiếm nhanh `Tìm kiếm: [TextBox]` lọc tức thì.
     * Khối 2: Hộp chọn chuyên ngành dạng `ComboBox Dropdown` màu xanh ngọc, hiển thị rõ ràng tất cả các chuyên ngành MEP / Kiến trúc, triệt tiêu 100% thanh cuộn ngang màu trắng.
     * Khối 3: Bộ lọc thông minh `☑ Chỉ hiện Family đã vẽ (> 0)` màu vàng cam.
     * Khối 4: Thống kê kết quả `Hiển thị: X / Y Types` màu xanh Sky.
     * Khối 5: Nút `Làm Mới (F5)` neo góc phải.
   - **Giao diện Bảng Lưới Ô Cột (DataGridView) rõ ràng, rành mạch**:
     * Cột `STT` (46px, Center).
     * Cột `Tên Family (Họ)` (Font đậm trắng, Fill).
     * Cột `Type (Kích Thước)` (190px, màu xanh lục sáng).
     * Cột `Chuyên Ngành (Category)` (160px, màu xanh Sky).
     * Cột `Đã Vẽ` (90px, Center, màu cam nổi bật).
     * Cột `Thao Tác` (Nút bấm `Đặt Vào Bản Vẽ` xanh Emerald, 130px).
     * Bật `DoubleBuffered = true` cuộn 60 FPS mượt mà.
   - **On-Demand 3D Preview (Lazy Loading & Cache)**: Khi click chọn dòng trên bảng, khung Inspector bên phải tự động nạp ảnh 3D Isometric to nét của Family Type đó (kèm bộ nhớ đệm Cache `_previewCache`), hiển thị bảng thông số kỹ thuật (Parameters) và nút đặt to rõ.
   - **Thao tác vẽ tức thì**: Nhấp đúp dòng trên bảng hoặc bấm nút `Đặt Vào Bản Vẽ` hoặc bấm Enter -> Kích hoạt lệnh `uidoc.PromptForFamilyInstancePlacement(symbol)` qua `ExternalEvent` (Modeless), rê chuột đặt liên tục vào bản vẽ Revit (Space để xoay góc, ESC để kết thúc).
   - **100% Modeless & Phím ESC**: Vừa mở bảng tra cứu vừa Pan, Zoom, vẽ Revit bình thường; ấn ESC đóng Form ngay lập tức.

---

### Panel 5 — Hệ Thống & Menu
- `HDToolMainForm` (Menu trung tâm điều phối công cụ) và `UpdateToolForm` / `ToolHotReloadManager` (cập nhật DLL nóng trực tiếp trong Revit).

---

## 3. Bản Đồ Mã Nguồn & Vị Trí File
- Thư mục gốc: `c:\Users\Nguye\Desktop\ADDIN REVIT`
- **File cài đặt chính thức**: `HDTOOL_Setup_Version_106.exe`
- **Bộ cài & Lưu trữ**: Chỉ lưu các bản mới nhất trong thư mục dự án.
- **Core Files Palette Family**: `Source/FamilyPaletteCommand.cs`, `Source/FamilyPaletteForm.cs`, `Source/FamilyPaletteService.cs`, `Source/FamilyPaletteModels.cs`.
- **Core Files Xuất CAD & Layer**: `Source/ExportCadCommand.cs`, `Source/ExportCadOptionsForm.cs`, `Source/CadExportMepService.cs`, `Source/CadLayerMappingModels.cs`.
