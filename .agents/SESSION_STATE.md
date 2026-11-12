# TRẠNG THÁI LÀM VIỆC HIỆN TẠI (SESSION STATE)

- **Ngày cập nhật gần nhất**: 12/11/2026
- **Phiên bản hiện tại**: **Version 83** (`HDTOOL_Setup_Version_83.exe`, giữ đúng 2 bản `Version 82` và `Version 83`, Desktop = 0 file)
- **Tình trạng Build**: Đã nâng cấp CAD-to-Duct BIM ở **Version 83**: chân rẽ `TakeoffTap` tự động khớp đúng chiều vát 45° bản vẽ CAD (`TapBevelDirection` + `EnsureTakeoffTapBevelOrientation`), gộp tuyến thẳng qua Quần ống gió/chạc Y (`MergeCollinearTapHosts`) + cô lập `txOneTap` giảm thiểu lỗi Revit, và lọc bỏ mũi tên chỉ hướng gió rỗng không kèm text (`IsAirflowArrowPolygon` / `TryMatchArrowHeadPair`) để giữ nguyên 100% ống thật; biên dịch `Build.ps1` đạt 0 lỗi, vượt qua `TestEscExit.exe` (20 Form), `VerifyV79.exe`, `VerifyV81.exe` (21/21 PASS) và triển khai sang Revit 2024 / 2025 / 2027.

---

## TÍNH NĂNG / CẬP NHẬT VỪA HOÀN THÀNH

1. **Chống bật bảng lỗi Revit 3D khi dựng hàng loạt ống & Tap (Version 80)**:
   - `Source/CadToDuctService.cs` (`BuildDuctsInRevit`, `SilentFittingFailuresPreprocessor`, `TryEnsureTapHostCoversBranch`):
     - Tách dựng hình thành 3 pha trong `TransactionGroup`: (1) Dựng toàn bộ ống thẳng $\to$ (2) Nối đầu-đối-đầu (`Transition`/`CollinearDirect`/`Elbow` có kiểm tra hướng connector `BasisZ` & khoảng cách $\le 8\text{ mm}$) $\to$ (3) Gắn `TakeoffTap` sau cùng khi đầu ống chính đã cố định.
     - Kiểm tra hình học nhánh `TryEnsureTapHostCoversBranch` (tránh đặt Tap lơ lửng ngoài đầu ống) và gắn `SilentFittingFailuresPreprocessor` tự động xử lý/ngắt lỗi mạng lưới âm thầm.

2. **Nhận diện `QLEADER` / `MULTILEADER` & Phân rã chạc chữ Y -> Chân rẽ `TakeoffTap` (Version 79)**:
   - `Source/CadDuctModels.cs` & `Source/CadDwgExtractor.cs` (cache `_v6`): Trích thêm `LEADER` (`QLEADER`) + `MULTILEADER` kèm bounding box `TEXT`/`MTEXT` (`hd-text-bbox-wcs`), gán `LeaderArrowPoint` mà không hút nhầm leader của `ATT:`/`Grille`/Van.
   - `Source/CadToDuctService.cs`:
     - `OrientShortCollarRectanglesByTopology`: tự chỉnh trục & đúng `WidthMm` cho các đoạn ống cổ ngắn (`Length < Width`, VD `647x2200`, `911x2000`) theo mặt tiếp xúc fitting/ống và mũi tên Leader.
     - `RunLeaderArrowMatchTier` (Pass 1-L1/L2): ưu tiên khớp `QLEADER`/`MULTILEADER` (kể cả `OAD 2200x550: EI60`) thẳng vào ống tại đầu mũi tên trước Pass 1A.
     - `BuildGeometricTopology` + `ExtendMainDuctsForTaps`: loại bỏ `TeeBranch` (Y gió), kéo dài ống chính qua vùng phụ kiện Y (`<= 7500mm`) kết hợp Côn đồng tâm + Chân rẽ `TakeoffTap`.
   - Kiểm thử thực tế trên `THÔNG GIÓ 1.dwg` (`accoreconsole.exe`) & `TestEscExit.exe` (20/20 Form kế thừa `HDToolBaseForm`) đều pass 100%.

2. **Tách biệt tuyến giao chéo (v77) & Chuẩn phím `ESC` / `HDToolBaseForm` (v76)**:
   - Tách riêng 2 tuyến ống gió đi ngang qua nhau (`ColorRgb`, `ExtractHvacSystemCode`, `XCROSS`, Overpass/Underpass, chống bắt chéo Text `OAD`/`EAD`).
   - Toàn bộ công cụ dùng `HDToolModelessHost` (Modeless 100%) + kế thừa `HDToolBaseForm` (`ESC` thoát lệnh tức thì).

---

## DANH SÁCH FILE QUAN TRỌNG ĐANG DUY TRÌ

- `Build.ps1`, `HDTOOL.addin`, `hdtool_ribbon_layout.json`, `Source/App.cs`, `Source/HDToolExternalEvent.cs`, `Source/ToolHotReloadManager.cs`
- `Source/CadDuctModels.cs`, `Source/CadDwgExtractor.cs`, `Source/CadToDuctService.cs`, `Source/CadToDuctForm.cs`, `Source/CadToDuctCommand.cs`
- `SKILL.md`, `.agents/skills/hdtool/SKILL.md`, `C:\Users\Nguye\.gemini\config\skills\hdtool\SKILL.md`, `HUONG_DAN_SU_DUNG.md`, `PreviewStudio/HDTool_PreviewStudio.html`
