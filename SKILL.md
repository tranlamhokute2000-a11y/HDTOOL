---
name: hdtool
description: >-
  HDTOOL (Revit 2024–2027): Auto Crop (Vertex-Exact + Liang-Barsky), căn Grids/Level; CAD-to-Duct BIM (trích DWG accoreconsole, lọc giả 4 tầng, tách tuyến giao chéo, ống chính + Transition + Tap ưu tiên Family sẵn, chuẩn WxH); Khối lượng Ống gió TCVN/SMACNA & Ống nước (Live Tracking, CHECK VAR PLUMBING, FitTableScale); Quản lý Sheet (Excel AutoFill, gộp DWG/PDF, cập nhật ngược); 100% Modeless, ESC thoát lệnh & Hot Reload.
---

# Skill: HDTOOL — Bộ Công Cụ Autodesk Revit (Version 83)

## 1. Kiến Trúc & Quy Chuẩn Bắt Buộc
- **Build & Đóng gói**: Biên dịch thuần bằng `csc.exe` (C# 5, `.NET Framework 4.8`) qua `Build.ps1` -> `Build/HDTOOL.dll` và bộ cài độc lập `HDTOOL_Setup_Version_{N}.exe` (không dùng MSBuild/`.csproj`).
- **Dynamic Ribbon & Hot Reload**: Tab `HDTOOL` gồm 4 Panel (10 item Ribbon). `ToolHotReloadManager` nạp `HDTOOL.dll` vào bộ nhớ (`byte[]` -> `AppDomain`) không khóa file (`HasActiveHotReloadAssembly`) và tự bơm nút mới (`EnsureAutoCropButtonDynamic`, `EnsureCadToDuctButtonDynamic`).
- **Modeless 100% & Thoát Phím `ESC`**: Mọi công cụ chạy qua `HDToolModelessHost` (tuyệt đối không dùng `ShowDialog()` khóa Revit) và mọi Form kế thừa `HDToolBaseForm` (`KeyPreview = true`, `ProcessCmdKey(Keys.Escape)`, gọi `Activate()` sau thao tác `Pick`).
- **UI & Lưu Trữ**: Dùng `ClientSize`, tắt `AutoSize` ở `TrackBar`, không cắt chữ/che khuất; lưu trữ chỉ giữ 2 bản mới nhất (`HDTOOL_Setup_Version_{N-1}.exe`, `HDTOOL_Setup_Version_{N}.exe`) trong thư mục dự án, `0` file trên Desktop.

---

## 2. Hệ Thống Chức Năng (4 Panel Thực Tế)

### Panel 1 — Căn Chỉnh Đối Tượng
1. **Auto Crop View (`AutoCrop`)**:
   - Trích xuất hình học đỉnh thực tế + **Liang-Barsky 3D Clipping** (`ClipSegmentLiangBarsky`) cắt theo `SectionBox` đang bật, giữ nguyên Z depth.
   - Cơ chế hiểu ý người dùng (`filterByCurrentCrop`): chỉ giữ đối tượng nằm trong hoặc chạm phạm vi Crop hiện tại, không dãn to ra trục ngoài; mặc định bỏ qua Link Kiến trúc/Kết cấu; hỗ trợ Plan, Section, Elevation, 3D và nhiều Viewport trên `ViewSheet`.
2. **Căn Chỉnh Grids (`GridCrop`), Level (`LevelCrop`), `CropAlign` & `SmartCrop`**:
   - `GridCropService` (4 hướng Top/Bottom/Left/Right trên Plan/Elevation/Section/Sheet) và `LevelCropService` (2 đầu Left/Right trên Section/Elevation/Sheet) căn mép trục/cao độ theo khoảng cách mm từ `CropBox` bằng `DatumExtentType.ViewSpecific` + `SetBubbleVisibility`.
   - `CropAlign` mở menu gộp căn chỉnh Grids/Level; `SmartCrop` tự nhận diện loại View để mở `GridCropForm` hoặc `LevelCropForm`.

### Panel 2 — Khối Lượng MEP & CAD-to-Duct BIM
1. **Dựng Ống Gió 3D Từ Hình Vẽ CAD Link (`CadToDuct`)**:
   - **Trích xuất DWG & QLEADER gốc (`CadDwgExtractor`, cache `_v6`)**: Chạy `accoreconsole.exe` trong thư mục ASCII an toàn; dùng `(textbox)`, `hd-text-bbox-wcs` và `(trans pt ent 0)` lấy đúng tâm & hộp chữ `TEXT`/`MTEXT`/`ATTRIB`/`MULTILEADER`/`DIMENSION`/`INSERT` (kể cả căn lề `72/73` và block lật gương); trích thêm `LEADER` (`QLEADER`) + `MULTILEADER` thành `LDR|arrow|tail|handle` để gán `LeaderArrowPoint` (kèm bộ lọc tránh hút nhầm leader của `ATT:`/`Grille`/Van).
   - **Nhận diện hình học, Quần ống gió & Chân rẽ đúng chiều CAD**: Nhận diện ống thẳng (`OrientShortCollarRectanglesByTopology` cho ống cổ ngắn `Length < Width`), cút cong (`Arc` & `PolyLine` 250 đỉnh), côn thu (`Transition`). Phân rã ngã 3 / chạc chữ Y / **Quần ống gió** (`MergeCollinearTapHosts` gộp ống chủ đồng trục) thành **tuyến ống chính thẳng liên tục + Côn đồng tâm + Chân rẽ `TakeoffTap`**. `ClassifyPolygonShape` trích vector vát 45° (`TapBevelDirection`) của chân rẽ CAD và `EnsureTakeoffTapBevelOrientation` tự lật `flipHand()`/`flipFacing()`/xoay 180° khớp đúng chiều vát thiết kế (hoặc hướng upstream qua `AssignFallbackTapBevelDirections`). `BuildDuctsInRevit` chạy `TransactionGroup` 3 pha (Ống thẳng $\to$ `Transition`/`Collinear`/`Elbow` $\to$ mỗi `TakeoffTap` trong `txOneTap` riêng có `TryEnsureTapHostCoversBranch`) kết hợp `SilentFittingFailuresPreprocessor`, giữ nguyên Family sẵn có của `DuctType`.
   - **Lọc đối tượng giả 4 tầng (`FilterFalsePositiveSegments`)**: Loại hộp miệng gió X/V, miệng gió đầu nhánh (ống dừng đúng mép miệng gió), khung `IAQ`, thiết bị phòng máy (`ATT`, `MFD`/`OBD`/`NRD`, quạt, Hatch `ANSI37`) và **mũi tên chỉ hướng gió rỗng không kèm text** (`ArrowSymbol`, `IsAirflowArrowPolygon`, `TryMatchArrowHeadPair`) để không xóa nhầm ống thật chứa mũi tên (như `OAD 400x250`); bảo toàn ống bọc chống cháy `EI60`.
   - **Tách tuyến giao chéo**: Cô lập theo `ColorRgb`, Layer và mã hệ thống (`ExtractHvacSystemCode`); `DoSegmentsCrossThroughEachOtherXY` chặn nối nhầm tại giao lộ `XCROSS`; `DetectOverpassUnderpassCrossings` nối thông 2 nửa ống đồng trục bị ngắt nét bên dưới ống cắt ngang; `IsNoteSystemCompatibleWithDuct` chặn bắt chéo Text `OAD`/`EAD`.
   - **Kích thước (`W` trước `H` sau) & Ưu tiên Leader**: `RunLeaderArrowMatchTier` (Pass 1-L1/L2) ưu tiên khớp mũi tên `QLEADER`/`MULTILEADER` thẳng vào ống trước khi chạy Pass 1A–3B (`EI60` + BFS theo tuyến gần nhất); luôn giữ chuẩn **`W` trước, `H` sau**; ô Cao độ tim & chiều cao `H` khởi tạo trống (tự nhập).
2. **Khối Lượng Ống Gió HVAC (`DuctQuantity`)**:
   - Modeless 60 FPS + Live Tracking (`DocumentChanged`), tính $m^2$ tôn chuẩn **TCVN 5687:2010 / SMACNA**, tách Cút theo góc thực tế & `R150`, Côn thu theo tham số dài `L`, Lượn Z `(L, H)` và phụ kiện/van/miệng gió.
3. **Khối Lượng Ống Nước (`MepQuantity`), `CHECK VAR PLUMBING` & `FitTableScale`**:
   - Quét Pipe/Fitting theo View, Box hoặc Viewport; gộp đa tham số tên phụ kiện (`ExtractFittingName`), chuẩn hóa tên ống (`FormatCleanPipeName`), xuất Excel/`ViewSchedule`/`ViewDrafting`, soi sáng 2 chiều (`MepHighlightManager`).
   - `CHECK VAR PLUMBING` (`PickTableHighlightCommand`/`CheckVarPlumbingForm`) đọc `PlacedTableMetadata` trên Sheet để đối soát & highlight 2 chiều trực tiếp.
   - `FitTableScaleCommand` (+ tự động theo sự kiện `ViewActivated` trong `App.cs`) đồng bộ tỷ lệ bảng `ViewDrafting` (`RescaleDraftingViewTable`).

### Panel 3 — Quản Lý Bản Vẽ & Sheet
1. **Xuất & Quản Lý Danh Mục Sheet (`ExportSheetList`)**: Sắp xếp tự nhiên (`StrCmpLogicalW`), phân nhóm bộ môn (`SetGroupTitleForm`), xuất CSV/Excel/Clipboard.
2. **Đổi Số Hiệu Hàng Loạt (`BatchEditSheetNumberForm`)**: Kéo chuột **Excel AutoFill Drag Handle** tăng số đuôi tự động, Double-Click Fill Handle và `Ctrl+D`.
3. **Xuất CAD/PDF Hàng Loạt (`ExportCadPdfOptionsForm` / `SheetExportCadPdfService`)**: Xuất PDF gộp, CAD rời hoặc **gộp nhiều Sheet thành 1 file DWG đa Layout** (`ExportToSingleCombinedCad` qua `accoreconsole.exe`).
4. **Cập Nhật Danh Mục Từ Excel (`UpdateSheetList`)**: Đối chiếu (`MatchAndCompare`) và cập nhật ngược tên/số hiệu Sheet hàng loạt (`ApplyUpdatesToDocument`).

### Panel 4 — Hệ Thống & Menu
- `HDToolMainForm` (Menu trung tâm điều phối công cụ) và `UpdateToolForm` / `ToolHotReloadManager` (cập nhật DLL nóng trực tiếp trong Revit).

---

## 3. Bản Đồ Mã Nguồn (`Source/`)
- **Hạ tầng, Ribbon & Hot Reload**: `App.cs`, `HDToolExternalEvent.cs`, `ToolHotReloadManager.cs`, `ResourceHelper.cs`, `HDToolMainCommand.cs`, `HDToolMainForm.cs`, `UpdateToolCommand.cs`, `UpdateToolForm.cs`.
- **Crop View, Grids & Level**: `AutoCropCommand.cs`, `AutoCropForm.cs`, `AutoCropService.cs`, `GridCropCommand.cs`, `GridCropForm.cs`, `GridCropService.cs`, `GridSelectionFilter.cs`, `LevelCropCommand.cs`, `LevelCropForm.cs`, `LevelCropService.cs`, `LevelSelectionFilter.cs`, `CropAlignCommand.cs`, `CropAlignOptionForm.cs`, `SmartCropCommand.cs`.
- **CAD-to-Duct BIM**: `CadDuctModels.cs`, `CadDwgExtractor.cs`, `CadToDuctCommand.cs`, `CadToDuctForm.cs`, `CadToDuctService.cs`.
- **Khối lượng Ống Gió (HVAC)**: `DuctQuantityCommand.cs`, `DuctQuantityItem.cs`, `DuctQuantityService.cs`.
- **Khối lượng Ống Nước, Check Var & Fit Scale**: `MepQuantityCommand.cs`, `MepQuantityInspectorForm.cs`, `MepQuantityService.cs`, `MepFittingItem.cs`, `MepUserSettings.cs`, `CheckVarPlumbingForm.cs`, `FitTableScaleCommand.cs`.
- **Quản lý Sheet, Đổi Số Hiệu & Xuất CAD/PDF**: `ExportSheetListCommand.cs`, `ExportSheetListForm.cs`, `ExportSheetListService.cs`, `SheetItem.cs`, `SetGroupTitleForm.cs`, `BatchEditSheetNumberForm.cs`, `ExportCadPdfOptionsForm.cs`, `SheetExportCadPdfService.cs`, `UpdateSheetListCommand.cs`, `UpdateSheetListPreviewForm.cs`, `UpdateSheetListService.cs`, `SheetUpdateItem.cs`.
