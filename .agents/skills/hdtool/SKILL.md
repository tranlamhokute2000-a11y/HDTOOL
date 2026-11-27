---
name: hdtool
description: >-
  HDTOOL (Revit 2024–2027): Auto Crop (Vertex-Exact + Liang-Barsky), căn Grids/Level; CAD-to-Duct BIM (trích DWG accoreconsole, lọc giả 4 tầng, tách tuyến giao chéo, ống chính + Transition + Tap ưu tiên Family sẵn, chuẩn WxH); Khối lượng Ống gió TCVN/SMACNA & Ống nước (Live Tracking); Quản lý Sheet & Xuất CAD Quy Đổi Layer MEP (View đang vẽ / Sheet / View dự án; tự động chuyển đổi đối tượng sang đúng Layer & Màu sắc ACI qua AutoLISP entmake/entmod; tự động lưu và nạp cấu hình mặc định); Tổng Hợp Family Đã Load (mỗi dòng 1 Family, không đếm số lượng, xem trước 3D xoay được, Đặt Vào Bản Vẽ); 100% Modeless, ESC thoát lệnh & Hot Reload.
---

# Skill: HDTOOL — Bộ Công Cụ Autodesk Revit (Version 114)

## 1. Kiến Trúc & Quy Chuẩn Bắt Buộc
- **Build & Đóng gói**: Biên dịch thuần bằng `csc.exe` (C# 5, `.NET Framework 4.8`) qua `Build.ps1` -> `Build/HDTOOL.dll` và bộ cài độc lập `HDTOOL_Setup_Version_{N}.exe` (không dùng MSBuild/`.csproj`).
- **Dynamic Ribbon & Hot Reload**: Tab `HDTOOL` gồm 5 Panel + Menu HDTOOL trung tâm. `ToolHotReloadManager` nạp `HDTOOL.dll` vào bộ nhớ (`byte[]` -> `AppDomain`) không khóa file (`HasActiveHotReloadAssembly`) và tự bơm nút mới (`EnsureAutoCropButtonDynamic`, `EnsureCadToDuctButtonDynamic`, `EnsureExportCadButtonDynamic`, `EnsureFamilyPaletteButtonDynamic`).
- **Khởi động & Nạp Lệnh An Toàn**: File `HDTOOL.addin` đăng ký 1 Application (`HDTool.App`) + 14 Command (gồm `ExportCadCommand` và `FamilyPaletteCommand`).
- **Modeless 100% & Thoát Phím `ESC`**: Mọi công cụ chạy qua `HDToolModelessHost` (cửa sổ công cụ chính tuyệt đối không dùng `ShowDialog()` khóa Revit; chỉ hộp thoại con ngắn như chọn file/thư mục/màu được phép `ShowDialog(this)`) và mọi Form kế thừa `HDToolBaseForm` (`KeyPreview = true`, `ProcessCmdKey(Keys.Escape)`, gọi `Activate()` sau thao tác `Pick`).
- **Revit API chỉ chạy trong API context**: ExternalEvent dùng chung được khởi tạo sẵn trong `App.OnStartup`. Mọi thao tác Transaction/Pick/Selection/`ExternalEvent.Create` từ Form Modeless phải đi qua `HDToolModelessHost.Run(app => ...)`; nếu không có ExternalEvent thì báo `ReportNoApiContext()`, không bao giờ gọi API trực tiếp.
- **Ghi đúng dự án gốc**: Trong lambda ExternalEvent luôn lấy `HDToolModelessHost.ResolveOwnerDocument(_doc)` (dự án Form được mở), KHÔNG dùng `app.ActiveUIDocument.Document` (người dùng có thể đã chuyển sang dự án khác). Trả về `null` nếu dự án gốc đã đóng -> dừng và báo.
- **Transaction nhiều pha (đổi số Sheet qua mã tạm `HD_TMP_`)**: có lỗi ở bất kỳ phần tử nào -> `RollBack` toàn bộ; trạng thái trong bảng chỉ cập nhật sau `Commit` thành công.
- **Tương thích API 2026+**: Dùng `ElementId.Value` (`long`), không dùng `ElementId.IntegerValue` (đã bị loại bỏ); Category Id ép `(int)id.Value` khi cần `BuiltInCategory`.
- **UI & Lưu Trữ**: Dùng `TableLayoutPanel` 4 tầng chống che khuất 100%, thiết lập `SplitterDistance` an toàn trong `OnShown`, tự co giãn `AdjustInspectorLayout` chống tràn nút/cắt chữ, tuyệt đối không dùng emoji gây lỗi ô vuông; lưu trữ chỉ giữ các bản mới nhất trong thư mục dự án, `0` file trên Desktop.
- **Giao diện `HDToolTheme` — phong cách MAC SÁNG (`HDToolThemeMode.Mac`, mặc định từ Version 113; Sếp Hồ chốt từ `PreviewStudio/HDTool_GiaoDienMac.html`)**:
  * Token Mac: `Bg (245,245,247)`, `Surface` trắng, `Surface2 (242,242,245)`, `Header (236,236,238)` (thanh tiêu đề/đầu cửa sổ phẳng), `Line (224,224,229)`, `LineStrong (200,200,206)`, `Text (29,29,31)`, `Text2 (72,72,78)`, `Text3 (134,134,139)`, `Accent (0,122,255)` xanh Apple, chữ trắng trên nút chính, `Ok (40,167,69)`, `Warn (214,122,0)`, `Err (230,50,40)`. Lấy qua `Pm(mac, premium, blueprint, drafting)`; token không có giá trị Mac (VD `SystemColor`) dùng giá trị Drafting. `IsMac`; `IsDark = false`; `IsPremium = true` cho cả Mac (bo góc, chip tròn, bảng kẻ ngang).
  * **Thanh tiêu đề Mac** trong `HDToolBaseForm.ApplyMacChrome()` (gọi ở `OnLoad` trước `ApplyTo`): Form chuyển `FormBorderStyle.None`, thêm thanh tự vẽ cao 38px@96DPI (3 nút tròn đỏ/vàng/xanh — đóng / thu nhỏ / phóng to, hiện ký hiệu khi rê chuột; nút xám khi Form không cho thu nhỏ/phóng to; tên cửa sổ `Text` ở giữa), viền 1px `LineStrong`, bo góc Windows 11 (DWM 33). Nội dung dời xuống: control Dock qua `Padding.Top`, control tọa độ tự do dời `Top` theo Anchor (Top+Bottom thì giảm Height). Kéo thanh để di chuyển, nhấp đúp để phóng to (Form Sizable), kéo mép 2px để đổi kích thước (`WM_NCHITTEST`). Lỗi -> trả lại viền Windows.
  * Còn 3 chế độ dự phòng: `Premium` (Tối Cao Cấp), `Drafting` (Bản Vẽ Sáng), `Blueprint`; đổi bằng `HDToolTheme.Mode`.
  * Token Tối Cao Cấp: `Bg (14,16,22)`, `Surface (22,26,35)`, `Surface2 (27,32,48)`, `SurfaceHover`, `Line (38,44,61)`, `LineStrong`, `Text (236,238,245)`, `Text2`, `Text3`, `Accent (91,140,255)`, `AccentInk (10,15,31)` (chữ tối trên nút chính), `AccentSoft`, `Ok/Warn/Err` + bản `Soft`, dải đầu chuyển màu `HeaderGrad1→2→3` (xanh đêm → tím). `Radius 10 / RadiusSm 7` (Kỹ thuật = 0). Không vẽ lưới blueprint.
  * **Màu nhận diện từng công cụ** `HDToolTheme.ToolColor(iconKey)`: grid #2563EB, level #0EA5E9, crop #7C3AED, align #6366F1, pipe #0891B2, duct #EA580C, cad2duct #DB2777, sheetlist #16A34A, sheetsync #059669, layers #D97706, family #C026D3, update #64748B — dùng chung Ribbon, Menu, chip.
  * `HDToolBaseForm`: `AutoScaleMode.Dpi` (96 gốc) + `PerformAutoScale()` + `HDToolTheme.ApplyTo(this)` trong `OnLoad` -> mọi Form đổi giao diện.
  * `ApplyTo` (Form cũ): chỉ đổi màu đặt tường minh + với nền tối thì đặt luôn nền/chữ mặc định của TextBox, ComboBox, ListBox, TreeView, Label...; nền tối -> `Header` (dải đầu Form vẽ chuyển màu, nhãn con trong suốt); thanh tiêu đề Windows tối + màu thanh tiêu đề (`HDToolNative.UseDarkTitleBar`, DWM 20/35/34), thanh cuộn tối (`SetWindowTheme "DarkMode_Explorer"`), ComboBox `DarkMode_CFD`; TabControl vẽ tab tối (`OwnerDrawFixed`).
  * **Màu đổi lúc chạy**: `ForeColorChanged` (Label/CheckBox/TextBox) tự chuyển sang token; `BackColorChanged` của Button tự phân loại lại kiểu nút; `DataGridView.CellFormatting` chuyển màu ô sáng do code cũ đặt sang màu tối (chỉ khi vẽ, dữ liệu không đổi). Giữ nguyên màu xám đặt tên (`Color.Gray`...) vì code so sánh `ForeColor == Color.Gray` cho chữ gợi ý.
  * `StyleGrid`: dòng 28px, chỉ kẻ ngang (Premium), tiêu đề 32px nền `Surface2`, số / kích thước / mã -> Consolas; cột `System`/"Hệ Thống" vẽ chip đặc màu SA/RA/EA/OA. Không đổi `HeaderText` (MepQuantityInspector dùng làm tên Parameter).
  * Control v2 (`Source/HDToolControls.cs`, tự vẽ, `HDToolPaintedControl`): `HDToolHeader` (nền chuyển màu, ô icon, tiêu đề, mô tả, chip phải), `HDToolSectionLabel` (chấm màu + IN HOA + kẻ), `HDToolButton` (nút bo góc Primary/Secondary/Ghost/Danger, icon tùy chọn), `HDToolToolCard` (thẻ công cụ: ô icon màu, tên, mô tả, phím tắt, rê chuột nổi viền màu công cụ; `RowStyle` = hàng danh sách Mac), `HDToolCardGrid` (lưới N cột tự co giãn; `ListStyle` = khung trắng bo góc 1 cột), `HDToolSidebar` (cột trái Mac: mục icon màu + tên + số lượng, tiêu đề mục IN HOA, mục chọn nền xanh, sự kiện `ItemClick`), `HDToolSearchBox` (TextBox thật + nhãn gợi ý tự vẽ — không dùng EM_SETCUEBANNER), `HDToolInfoBar`, `HDToolPill`, `HDToolRecent` (`%APPDATA%\HDTOOL\recent_tools.txt`). Mọi `TextRenderer.DrawText` phải có `NoPrefix`.
  * Icon `HDToolIcons` (vector GDI+ lưới 32; khóa `align, grid, level, crop, pipe, duct, cad2duct, sheetlist, sheetsync, layers, family, update, menu`); `HDToolIcons.Ribbon(key, size)` nét mực theo `UIThemeManager.CurrentTheme`, điểm nhấn = `ToolColor(key)`.
  * Kiểm tra ngoài Revit: chụp Form bằng `PrintWindow` (không dùng `DrawToBitmap` — vẽ sai thứ tự chồng lớp control con). Lớp nào có trường kiểu Revit (UIDocument...) thì không mở được ngoài Revit -> tách giao diện ra lớp View riêng không phụ thuộc Revit (mẫu: `DuctQuantityView`).
  * Khoác áo từng control qua `SafeSkin` (lỗi 1 control không làm dừng cả Form; lỗi ghi vào `HDToolTheme.SkinErrors`). ComboBox nền tối: `FlatStyle.Standard` + `SetWindowTheme("DarkMode_CFD")` (kiểu Flat để nền trắng).
  * Control v2 thêm: `HDToolStepBar` (thanh bước), `HDToolSegmented` (nút phân đoạn), `HDToolChipToggle` / `HDToolSwitch` (kế thừa CheckBox -> giữ `Checked`/`CheckedChanged`), `HDToolKpiCard` (ô số liệu), `HDToolCardPanel` (thẻ bo góc), `HDToolEmptyState` (màn hình chưa có dữ liệu). Icon nhỏ cho nút: `pick, plus, refresh, light, lightoff, zoom, col, copy, xls, sheet, area, ruler, hash, valve`.

---

## 2. Hệ Thống Chức Năng (5 Panel Thực Tế)

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
   - **Giao diện v2** (`Source/DuctQuantityView.cs`, không phụ thuộc Revit; `DuctQuantityForm.InitializeComponent` chỉ gắn sự kiện): dải đầu màu công cụ · thanh 4 bước (Chọn phạm vi → Quét đối tượng → Kiểm tra bảng → Xuất Excel / Đặt vào Sheet; tự chuyển theo dữ liệu, bấm Xuất/Đặt -> bước 4) · thanh công cụ (phạm vi dạng nút phân đoạn, Quét vùng mới / Bóc thêm / Quét lại, làm sáng / tắt sáng / zoom dạng icon có gợi ý, ô tìm) · chip loại cấu kiện (5 CheckBox cũ) · 4 ô số liệu (m² tôn, mét dài, số đoạn, phụ kiện — cập nhật trong `FilterAndDisplayItems` qua `SetTotals`) · thẻ bảng (lọc Tầng / Hệ thống, Thêm / Xóa cột, cột "Chủng Loại" vẽ nhãn màu theo loại, cột tên tự giãn Fill; chưa có dữ liệu -> màn hình hướng dẫn + nút Quét vùng mới) · thanh hành động (2 công tắc, dòng thông tin số đối tượng / dòng / dòng đang chọn, Copy bảng, Xuất Excel, nút chính "Đặt vào Sheet", Đóng).
   - Phạm vi: 3 `RadioButton` cũ vẫn giữ (ẩn trong `pnlScopeHidden`) để không đổi logic; nút phân đoạn đồng bộ 2 chiều qua `SyncScopeView()` (kèm số đối tượng đã quét).
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
     * Tự động lưu toàn bộ bảng cấu hình Layer vào file JSON `%APPDATA%\Autodesk\Revit\Addins\HDTOOL\mep_cad_layer_mapping.json` sau mỗi lần xuất CAD thành công hoặc khi bấm nút `Lưu Mặc Định`.
     * Tự động nạp lại toàn bộ cấu hình Layer đã lưu mỗi khi mở Form, người dùng không cần thiết lập lại.
     * Bổ sung nút: `Lưu Mặc Định`, `Nạp Đã Lưu`, `Quét Lại Hệ Thống`, `Thêm Dòng`, `Xuất JSON`, `Nhập JSON`, `✖ Xóa Dòng Đã Chọn`.
     * Cột DataGridView hiển thị rõ ràng: `CHUYỂN ĐỔI SANG LAYER CAD (Tên Layer)` và `MÀU SẮC CAD (ACI 1-255)`.
4. **Xuất CAD/PDF Hàng Loạt (`ExportCadPdfOptionsForm` / `SheetExportCadPdfService`)**: Xuất PDF gộp, CAD rời hoặc **gộp nhiều Sheet thành 1 file DWG đa Layout** (`ExportToSingleCombinedCad` qua `accoreconsole.exe`).
5. **Cập Nhật Danh Mục Từ Excel (`UpdateSheetList`)**: Đối chiếu (`MatchAndCompare`) và cập nhật ngược tên/số hiệu Sheet hàng loạt (`ApplyUpdatesToDocument`).

---

### Panel 4 — Thư Viện & Family
1. **Tổng Hợp Family Đã Load (`FamilyPaletteCommand`, `FamilyPaletteForm`, `FamilyPaletteService`, `FamilyPaletteModels`, `FamilyMeshViewer`)**:
   - **Chỉ tổng hợp những gì đã load, KHÔNG đếm số lượng trên bản vẽ** (lệnh Sếp Hồ, Version 112): quét `FamilySymbol` gom theo Family; **mỗi dòng bảng = 1 Family** (load 1 Type thì 1 dòng 1 Type). Đã bỏ cột `Đã Vẽ`, ô "Chỉ hiện Family đã vẽ", dòng "Đã đặt trong mô hình" và vòng đếm `FamilyInstance`.
   - **Toolbar**: ô Tìm (bỏ dấu; khớp tên Family / chuyên ngành / tên Type — khớp Type thì chọn sẵn Type đó), ComboBox chuyên ngành, nhãn `Hiển thị: X / Y Family`, nút `Làm Mới (F5)`.
   - **Bảng**: `STT` | `Tên Family (Họ)` (Fill) | `Số Type` (90px, giữa) | `Chuyên Ngành (Category)` | nút `Đặt Vào Bản Vẽ` (cột 4). Sắp theo nhóm chuyên ngành rồi tên Family.
   - **Khung bên phải**: chip chuyên ngành, tên Family, `cboTypes` chọn Type (đổi Type -> đổi hình 3D + bảng thông số + Type sẽ đặt), "Số Type đã load: N", bảng thông số Type, nút `ĐẶT VÀO BẢN VẼ REVIT`.
   - **Xem trước 3D xoay được (`FamilyMeshViewer`, không phụ thuộc Revit)**: `FamilyPaletteService.GetSymbolMesh(doc, symbolId, 30000)` chạy trong `HDToolModelessHost.Run` (API context) — `symbol.get_Geometry(Options Fine)`, rỗng thì lấy 1 đối tượng đã đặt qua `FamilyInstanceFilter`; `Solid.Faces -> Face.Triangulate()`, `Mesh`, `GeometryInstance.GetInstanceGeometry()` đệ quy; trả `float[]` 9 số/tam giác. Viewer tự canh giữa/vừa khung, tô phẳng 64 mức sáng, vẽ xa trước gần sau (painter). Kéo chuột trái xoay, lăn phóng to, chuột phải/giữa dời, nhấp đúp về góc nhìn ban đầu. Không có hình khối (Tag, chú thích) -> hiện ảnh `GetPreviewImage`. Cache `_meshCache` / `_previewCache`; bỏ kết quả cũ nếu người dùng đã chọn Type khác (`_viewerSymbolId`).
   - **Thao tác vẽ tức thì**: Nhấp đúp dòng, bấm nút `Đặt Vào Bản Vẽ` hoặc Enter -> `uidoc.PromptForFamilyInstancePlacement(symbol)` qua `ExternalEvent` với Type đang chọn ở `cboTypes` (Space xoay, ESC kết thúc).
   - **100% Modeless & Phím ESC**: Vừa mở bảng tra cứu vừa Pan, Zoom, vẽ Revit bình thường; ấn ESC đóng Form ngay lập tức.

---

### Panel 5 — Hệ Thống & Menu
- `HDToolMainForm` (Menu trung tâm kiểu Mac, 860x640, đổi kích thước được; mẫu `PreviewStudio/HDTool_GiaoDienMac.html`): thanh tiêu đề Mac "HDTOOL — Trung Tâm Công Cụ · Version N" (đọc resource `version.txt`) · **cột trái** 220px: ô tìm công cụ (bỏ dấu tiếng Việt khi so khớp; gõ chữ ở đâu cũng nhảy vào ô tìm; Enter mở kết quả đầu; đang tìm thì tự về "Tất cả") + `HDToolSidebar` ("Tất cả", NHÓM CÔNG CỤ kèm số lượng, GẦN ĐÂY 3 công cụ từ `HDToolRecent` — bấm là mở) · **khung phải**: tiêu đề lớn (tên nhóm đang chọn / "Kết quả tìm kiếm"), dòng "Đang mở: View · loại · tỷ lệ", mỗi nhóm = `HDToolSectionLabel` + `HDToolCardGrid.ListStyle` chứa hàng `HDToolToolCard.RowStyle` (ô icon màu đặc, tên đậm, mô tả 1 dòng, phím tắt, dấu ›) · chân trang mẹo + Đóng (ESC). 4 nhóm — Căn chỉnh (Grids, Level, Tự nhận theo View, Crop View), Khối lượng MEP (Ống nước, Ống gió qua `PostCommand` lệnh Ribbon `CustomCtrl_%CustomCtrl_%HDTOOL%Khối Lượng MEP%cmdHDToolDuctQuantity`, CAD→Ống gió), Bản vẽ & Sheet (Danh mục, Cập nhật danh mục, Xuất CAD), Thư viện & hệ thống (Family, Cập nhật tool); phím 1–9 mở 9 thẻ đầu (khi ô tìm trống), Ctrl+F tìm.
- `UpdateToolForm` / `ToolHotReloadManager` (cập nhật DLL nóng trực tiếp trong Revit).
- Ribbon Panel 1: SplitButton "Căn Chỉnh Grids / Level" gồm 3 mục (Bảng tích hợp, Grids, Level) + nút lớn riêng "Tự Động Crop View" (không lặp Crop trong SplitButton).

---

## 3. Bản Đồ Mã Nguồn & Vị Trí File
- Thư mục gốc: `c:\Users\Nguye\Desktop\ADDIN REVIT`
- **File cài đặt chính thức**: `HDTOOL_Setup_Version_111.exe`
- **Bộ cài & Lưu trữ**: Chỉ lưu các bản mới nhất trong thư mục dự án.
- **Core Files Palette Family**: `Source/FamilyPaletteCommand.cs`, `Source/FamilyPaletteForm.cs`, `Source/FamilyPaletteService.cs`, `Source/FamilyPaletteModels.cs`, `Source/FamilyMeshViewer.cs`.
- **Core Files Xuất CAD & Layer**: `Source/ExportCadCommand.cs`, `Source/ExportCadOptionsForm.cs`, `Source/CadExportMepService.cs`, `Source/CadLayerMappingModels.cs`.
