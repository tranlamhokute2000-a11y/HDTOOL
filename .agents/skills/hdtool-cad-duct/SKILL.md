---
name: hdtool-cad-duct
description: Quy trình chẩn đoán, sửa lỗi và kiểm tra hồi quy công cụ Dựng Ống Gió Từ CAD của HDTOOL (CadToDuct) — đọc nhật ký quét / dựng, chạy lại đúng dữ liệu Revit ngoài Revit, bộ bản vẽ mẫu và các bẫy đã gặp.
---

# Skill: Dựng Ống Gió Từ CAD (CadToDuct) — Chẩn Đoán & Kiểm Tra Hồi Quy

Quy tắc nhận diện / dựng (hiện trạng) nằm trong `SKILL.md` gốc, mục Panel 2 -> `CadToDuct`. Skill này là **cách làm việc** khi Sếp báo "tool vẽ sai / thiếu / thừa ống".

## 1. Ý muốn của Sếp (đã chốt, không hỏi lại)
- Mọi tay nhánh dựng bằng **chân rẽ (TakeoffTap)**, kể cả chỗ chia chữ Y: ống lớn đi thẳng, nhánh gắn vào sườn. Không chạc 3, không chạc Y.
- Ghi chú kích thước (`OAD 500x350: EI60`, `SAD:400x250`...) là ký hiệu của ống gần nó; đoạn không có ghi chú thì **lan truyền** từ tuyến nối với nó.
- Khung có gạch chéo = **ống trục đứng**, cao bằng **1 tầng** (tầng đang chọn -> tầng kế trên).
- Đường dẫn ghi chú, ký hiệu van, miệng gió, khung chú thích **không phải ống**.

## 2. Môi trường thử thật
- Sếp thử trên **Revit 2025.5** (`%APPDATA%\Autodesk\Revit\Addins\2025\HDTOOL\HDTOOL.dll`), dù mã build theo API 2024. Máy thường mở cả Revit 2024 lẫn 2025: xem tiến trình `Revit.exe` trước khi kết luận.
- Journal: `%LOCALAPPDATA%\Autodesk\Revit\Autodesk Revit 2025\Journals\journal.NNNN.txt`.
  - `Execute external command:...HDTool.<Lệnh>` = giờ chạy lệnh (UpdateToolCommand, CadToDuctCommand...).
  - `"Transaction Successful" , "HDTOOL - Tạo Ống Gió Thẳng"` = **mỗi dòng là 1 lần bấm dựng**; đếm để phát hiện dựng chồng.
  - `TaskDialog "..."` = nội dung báo cáo sau dựng. `ID_BUTTON_DELETE` = Sếp xoá.
  - Journal ghi trễ: vừa thao tác xong có thể chưa thấy, đọc lại sau vài giây.
- **Sau "Cập nhật Tool", cửa sổ công cụ mở từ trước vẫn chạy mã cũ.** Phải đóng (ESC) và mở lại từ Ribbon; không chắc thì đóng hẳn Revit. Dấu hiệu chạy bản mới: thời gian `CadDuct_scan.log` mới hơn giờ build, hoặc có dòng mới của nhật ký (`TRUC-DUNG`, `CAT-TAI-TRUC`...).

## 3. Chẩn đoán — luôn theo thứ tự này
1. Đọc `%LOCALAPPDATA%\HDTOOL\Logs\CadDuct_scan.log` (dòng đầu = giờ quét + tên DWG trong `DIAG`). So `STAGE|truoc-loc` -> `sau-loc` -> `cuoi` để biết **bước nào** làm mất / sinh ra đoạn đó.
2. Lấy nét quanh chỗ lỗi bằng toạ độ trong ảnh Sếp gửi: lọc `POLY|`, `LINE|`, `NOTE|`, `ZONE|` theo khung x/y (mm). Nhìn nét thật trước khi đoán (ví dụ: 2 đường dẫn song song cách 975mm = "ống 975").
3. Đọc `CadDuct_build.log`: "Chưa dựng được" + lý do từng chỗ Revit từ chối.
4. `TaoDuLieu.ps1 -KemNhatKyQuet` cho bản vẽ đó, tái hiện bằng `ChayLai.ps1` (mục 4), sửa, rồi chạy hồi quy cả bộ (mục 5). Chưa tái hiện được thì thêm nhật ký và nhờ Sếp quét lại, không đoán.

## 4. Chạy lại ngoài Revit (không cần mở Revit) — `.agents\tools\cad-duct-replay\`
- **Một lệnh**: `powershell -ExecutionPolicy Bypass -File .\.agents\tools\cad-duct-replay\ChayLai.ps1` (thêm `-Case tes2` để chạy 1 bản vẽ). Tự biên dịch HDTOOL từ `Source` vào `%TEMP%\HDTOOL_Replay` (không tăng Version, không deploy), tự tạo bản trích chữ offline từ `CadDwgExtractor.cs` hiện tại, chạy từng bản vẽ trong `data\`, so `ket-qua\lienket.txt` với `chuan_lienket.txt` -> in `KHOP CHUAN` hoặc từng dòng khác. Có dòng khác thì thoát mã 1.
- Kết quả mỗi bản vẽ: `data\<ten>\ket-qua\run.log` (danh sách ống theo bước `truoc loc` / `sau loc` / `cuoi`, `DUONG-DAN`, `KHOI-PHUC`, `CAT-TAI-TRUC`, `ONG-DUNG`), `lienket.txt`, `mat-bang.png` (nét CAD + ống + phụ kiện + ghi chú), `xem-truoc-3d.png` (đúng cửa sổ xem trước của tool).
- **Thay đổi kết quả là cố ý** (đã xem hình, giải thích được từng dòng khác): chạy lại với `-LuuChuan` để lưu làm chuẩn mới.
- **Thêm bản vẽ mới**: `TaoDuLieu.ps1 -Dwg "<đường dẫn.dwg>" -Ten <tên ngắn> [-KemNhatKyQuet]` -> chép DWG ra thư mục tạm, `accoreconsole` + `dump.lsp` -> `lines.txt`, `CadDwgExtractor.RunAcCoreExtraction` -> `cache.txt`; `-KemNhatKyQuet` chép `CadDuct_scan.log` mới nhất (Sếp vừa quét đúng bản vẽ đó). Kiểm tra `mat-bang.png` rồi `ChayLai.ps1 -Case <tên> -LuuChuan`.
- Có `CadDuct_scan.log` trong `data\<ten>` -> chạy bằng **đúng nét + ghi chú + vùng loại trừ Revit đã đọc** (POLY/LINE qua `SplitPinchedPolyline` + `AddPolylinePart`, bỏ các vòng dựng lại từ LINE ở cuối danh sách POLY; NOTE tách W/H bằng regex, `LDR=` -> `LeaderArrowPoint`; ZONE). Không có -> dùng `lines.txt` / `cache.txt` từ DWG.
- File: `MoPhong.cs` (pipeline — **thêm / đổi bước trong `AnalyzeCadDuctNetwork` thì phải sửa theo ở đây**, gọi hàm `private static` qua reflection), `DocNet.cs` (đọc `lines.txt`, đi block lồng nhau, dựng nét như `CollectGeometryRecursive`), `dump.lsp`, `ChupForm.cs` (`%TEMP%\HDTOOL_Replay\ChupForm.exe <ảnh.png>`: dựng `CadToDuctForm` không cần UIDocument bằng `GetUninitializedObject` + constructor `HDToolBaseForm` + `InitializeComponent`, đo bảng và chụp).
- `data\<ten>\` có trên GitHub (Sếp cho phép đưa công khai 2026-11-28): `banve.dwg` (bản gốc), `lines.txt`, `cache.txt`, `CadDuct_scan.log`, `chuan_lienket.txt`; chỉ `ket-qua\` bị bỏ qua. Máy mới: `git pull` là chạy `ChayLai.ps1` được ngay.
- Revit 2025 API cần .NET 10 (`dotnet exec --runtimeconfig`, `net10.0-windows` + `Microsoft.WindowsDesktop.App 10.0.0`); công cụ mặc định dùng API 2024 trên .NET 4.8 là đủ cho nhận diện.

## 5. Bộ bản vẽ hồi quy (`ChayLai.ps1` chạy cả 3 sau mỗi lần sửa)
| Tên trong `data\` | Bản vẽ gốc | Nguồn chạy lại | Chuẩn hiện tại (Version 130) |
|---|---|---|---|
| `tes` | `C:\Users\Nguye\Downloads\tes.dwg` (SAD 400x250/400x150, hộp VCD/FD, TEAD 200x150) | nhật ký quét Revit 14:33 | 23 ống = 20 ngang + 3 trục, 20 liên kết |
| `tes2` | `C:\Users\Nguye\Downloads\tes 2.dwg` (OAD EI60, 2 chỗ chia Y, 16 trục) | nhật ký quét Revit 16:23 | 61 ống = 45 ngang + 16 trục, 58 liên kết |
| `tang1` | `M:\2026. Dự án Xuân La\ADD_REVIT\HVAC TẦNG 1.dwg` | DWG | 40 ống = 36 ngang + 4 trục, 30 liên kết |

- So **liên kết theo toạ độ**, không chỉ đếm số ống. Mọi dòng khác chuẩn phải giải thích được trước khi `-LuuChuan`.
- Bản vẽ trong Revit lệch toạ độ so với DWG (tes 2: dx 45294, dy -2579mm) -> so Revit với kết quả từ DWG phải trừ độ lệch; vì vậy bản vẽ đã có nhật ký quét luôn chạy bằng nhật ký quét.

## 6. Bẫy đã gặp (đừng lặp lại)
- `CadDuctSegment.Length` là **mm**, toạ độ XYZ là **feet**.
- Revit có thể **gộp nhiều hình nối nhau thành 1 PolyLine**; DWG thì vẫn là hình rời -> lỗi chỉ thấy trong Revit (mất SAD 400x250).
- Nhật ký quét **không ghi cung (ARC)**; Revit cũng thường không nhận được phụ kiện chữ Y vẽ cong -> phải xử lý được cả khi không có hình phụ kiện.
- Block xref kiến trúc lớn từng bị coi là vùng loại trừ, xoá sạch ống tầng 1 (giới hạn 8000mm).
- Ống vẽ xuyên vào khung trục đứng: gạch chéo nằm trong đầu ống làm cả đoạn bị lọc là hình giả.
- 2 đường dẫn ghi chú song song bị ghép thành ống (975x350, 2550 thiếu H).
- Nhiều ống cùng đạt dung sai -> phải chọn "khớp nhất", không lấy ống xét sau cùng.
- Sửa quy tắc rộng tay (nới khe, bỏ điều kiện liên kết) phải kèm điều kiện chặn: ví dụ nhánh đã là chân rẽ của ống khác thì không gắn thêm (tầng 1 từng bị gắn trùng).
- Bấm dựng nhiều lần = nhiều lớp ống chồng nhau; tool đã chặn, nhưng sau khi **quét lại** thì không nhận ra ống cũ.
- Đổi `ClientSize` lúc `InitializeComponent` không làm control neo (Anchor) giãn theo; phóng to trong `OnLoad`.
