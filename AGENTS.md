# QUY TẮC HOẠT ĐỘNG VÀ DUY TRÌ TRẠNG THÁI LÀM VIỆC (AGENTS.MD)

Tài liệu này là quy tắc bắt buộc Antigravity phải tuân thủ trong suốt quá trình hỗ trợ người dùng tại dự án **HDTOOL (ADDIN REVIT)**.

---

## 0. QUY TẮC LÀM VIỆC & PHONG CÁCH GIAO TIẾP VỚI SẾP HỒ (BẮT BUỘC)

- **Tập trung vào vấn đề chính**: Luôn nắm bắt và giải quyết trực tiếp 100% cốt lõi vấn đề Sếp giao.
- **Nói ngắn gọn, đúng trọng tâm**: Trả lời súc tích, đi thẳng vào kết quả và thông tin cần cung cấp, không giải thích dài dòng.
- **Làm việc đúng yêu cầu, không lan man**: Thực thi chính xác theo chỉ thị, không lan man sang các nội dung không liên quan.

---

## 1. DUY TRÌ TRẠNG THÁI LÀM VIỆC LIÊN TỤC (SESSION CONTINUITY)

- **Ghi nhớ trạng thái làm việc**:
  - Antigravity PHẢI luôn duy trì và cập nhật file trạng thái làm việc gần nhất tại:
    `.agents/SESSION_STATE.md`
  - Bất cứ khi nào hoàn thành hoặc đang thực hiện dở một tính năng, sửa lỗi, biên dịch hay cấu hình, Antigravity PHẢI tự động cập nhật nội dung vào `.agents/SESSION_STATE.md`.

- **Tự động khôi phục bối cảnh khi tiếp tục công việc**:
  - Khi người dùng yêu cầu tiếp tục (ví dụ: *"Tiếp tục công việc vừa rồi"*, *"Làm tiếp"*, *"Tiếp tục"*...), ngay cả khi người dùng chưa thoát hoặc đã đóng mở lại Antigravity:
    1. Đọc ngay `.agents/SESSION_STATE.md` để nắm bắt công việc gần nhất, danh sách file liên quan và trạng thái hiện tại.
    2. Kiểm tra nhanh các file gần nhất và lịch sử transcript nếu cần để tiếp nối chính xác 100%.
    3. Tuyệt đối không yêu cầu người dùng phải nhắc lại việc mình đang làm.

---

## 2. QUY TRÌNH BIÊN DỊCH, TĂNG VERSION VÀ TRIỂN KHAI

- **Tự động tăng phiên bản Version (+1) sau mỗi lần sửa đổi, bổ sung**:
  - Sau mọi thay đổi trong mã nguồn `Source/*.cs` hoặc cập nhật giao diện Ribbon, luôn thực thi lệnh biên dịch tự động:
    `powershell -ExecutionPolicy Bypass -File .\Build.ps1`
  - Phiên bản Version sẽ **TỰ ĐỘNG TĂNG DẦN** (+1): Version 5 -> Version 6 -> Version 7...

- **QUY TẮC LƯU TRỮ VÀ VỊ TRÍ XUẤT FILE (BẮT BUỘC THEO LỆNH SẾP HỒ)**:
  1. **KHÔNG BAO GIỜ sinh ra tệp trên màn hình Desktop**:
     - Toàn bộ các file thực thi `.exe`, file nén `.zip`, file cài đặt... **CHỈ ĐƯỢC PHÉP SINH RA TRONG THƯ MỤC DỰ ÁN ĐANG LÀM VIỆC (`c:\Users\Nguye\Desktop\ADDIN REVIT`)**.
     - Tuyệt đối không sinh bất kỳ tệp nào ra màn hình Desktop. Giữ cho Desktop của Sếp Hồ luôn sạch sẽ 100%.
  2. **CHỈ GIỮ LẠI ĐÚNG 2 VERSION GẦN NHẤT, CÒN LẠI XÓA HẾT**:
     - Sau mỗi lần sửa đổi hoặc build tăng Version mới, hệ thống tự động quét và chỉ lưu lại đúng 2 version số lớn nhất gần nhất (`Version {N}` và `Version {N-1}`).
     - Toàn bộ các file của các version cũ hơn (`v1`, `v2`, `v3`... đến `{N-2}`) phải được **TỰ ĐỘNG XÓA BỎ HOÀN TOÀN** khỏi thư mục dự án và thư mục `Installer`.
     - **Chỉ dùng một định dạng tên duy nhất**: `HDTOOL_Setup_Version_{N}.exe`. Xóa bỏ hoàn toàn các file viết tắt `_v{N}` và các file trùng lặp để thư mục luôn ngăn nắp chuẩn chỉ 100%.
  3. **Đảm bảo biên dịch thành công 0 lỗi**:
     - `HDTOOL.dll` và `Setup.exe` biên dịch thành công 0 lỗi.
     - Tự động đồng bộ sang thư mục `%APPDATA%\Autodesk\Revit\Addins\2024\HDTOOL` phục vụ tính năng Hot Reload trực tiếp trong Revit.

---

## 3. QUY TẮC ĐÚC KẾT & TINH LỌC KỸ NĂNG (SKILL) TỨC THÌ (BẮT BUỘC THEO LỆNH SẾP HỒ)

- **Đúc kết thành Skill tức thì sau mỗi lần cập nhật / bổ sung tính năng**:
  - Mỗi lần cập nhật, cải tiến hay bổ sung bất kỳ tính năng nào cho công cụ, **NGAY LẬP TỨC đúc kết toàn bộ logic, công thức, quy trình thành kỹ năng (Skill)** tại:
    * `.agents/skills/hdtool/SKILL.md`
    * `SKILL.md` (thư mục gốc dự án)
- **LOẠI BỎ TRIỆT ĐỂ NHỮNG THỨ ĐÃ BỎ / KHÔNG CÒN TRONG TOOL**:
  - Những thứ gì bỏ đi, không còn tồn tại trong tính năng của tool thì trong Skill **CŨNG BỎ ĐI NGAY LẬP TỨC, TUYỆT ĐỐI KHÔNG CẦN GHI NHỚ LÀM GÌ**.
  - Giữ cho Skill luôn tinh gọn, sắc bén, phản ánh chính xác 100% hiện trạng thực tế công cụ đang hoạt động.
- **Đồng bộ song song**:
  - `HUONG_DAN_SU_DUNG.md`
  - `.agents/SESSION_STATE.md`

---

## 4. QUY TRÌNH PREVIEW STUDIO TRỰC QUAN TRƯỚC KHI LÀM GIAO DIỆN ADDIN (BẮT BUỘC THEO LỆNH SẾP HỒ)

- **Mỗi khi có yêu cầu sửa đổi, bổ sung công cụ hoặc làm giao diện cho HDTOOL**:
  1. Antigravity **BẮT BUỘC tạo hoặc cập nhật cửa sổ PREVIEW STUDIO trực quan** trước khi đụng vào code C# UI của Add-in.
  2. Preview Studio (`HDTool_PreviewStudio.html` / `OpenPreviewStudio.bat`) phải mô phỏng 100% giao diện thực tế thanh Ribbon của Autodesk Revit 2024 (Tab HDTOOL, các Panel, nút bấm lớn/nhỏ, SplitButton, Icon chuẩn, Tooltip...).
  3. Cung cấp khả năng tương tác toàn diện cho Sếp Hồ:
     - Kéo thả sắp xếp thứ tự các nút công cụ trong cùng Panel hoặc di chuyển qua lại giữa các Panel.
     - Chia nhóm Panel (Căn Chỉnh Đối Tượng, Khối Lượng MEP, Quản Lý Bản Vẽ & Sheet, Hệ Thống...).
     - Chuyển đổi giữa Nút Lớn (32x32), Nút Nhỏ (16x16) hoặc SplitButton xổ xuống.
     - Sửa tên nút (Label), icon, tooltip, thêm nút mới và xem trước popup Form của từng công cụ khi click vào.
  4. Sếp Hồ kiểm tra trực quan, điều chỉnh sắp xếp đến khi ưng ý và bấm **"CHỐT & XUẤT CẤU HÌNH"**.
  5. Chỉ sau khi Sếp Hồ chốt layout, Antigravity mới áp dụng cấu hình này vào `Source/App.cs`, tạo code giao diện Add-in và tiến hành biên dịch cập nhật!

---

## 5. QUY TẮC ĐẨY DỮ LIỆU LÊN GITHUB & LỊCH NHẮC NHỞ HÀNG NGÀY (BẮT BUỘC THEO LỆNH SẾP HỒ)

- **LỊCH NHẮC NHỞ ĐỊNH KỲ 16H15 HÀNG NGÀY**:
  - Đúng **16h15 hàng ngày**, Antigravity tự động chủ động hỏi/nhắc nhở Sếp Hồ: *"Sếp Hồ ơi, đã 16h15 rồi, Sếp có muốn em cập nhật (push) toàn bộ code, tài liệu và tri thức mới nhất hôm nay lên GitHub không ạ?"*
  - Nếu Sếp đồng ý -> Antigravity tiến hành kiểm tra git status, tạo commit chuẩn chỉ và push lên GitHub an toàn 100%.

- **CHỈ ĐẨY LÊN GITHUB KHI SẾP HỒ ĐỒNG Ý / YÊU CẦU**:
  - CHỈ ĐƯỢC PHÉP ĐẨY LÊN GITHUB KHI SẾP HỒ CÓ LỆNH RÕ RÀNG (ví dụ: *"đẩy lên github"*, *"push github"*, *"publish"*, hoặc trả lời *"đồng ý"* khi được nhắc vào 16h15).
  - **KHI SẾP CHƯA ĐỒNG Ý THÌ TUYỆT ĐỐI KHÔNG PUSH**: Tuyệt đối không tự tiện chạy `git push`.

---

## 6. QUY TẮC HIỂN THỊ GIAO DIỆN RÕ RÀNG, KHÔNG CHE KHUẤT (BẮT BUỘC THEO LỆNH SẾP HỒ)

- **Mọi thông tin trên thanh công cụ và Form phải đầy đủ, rõ ràng 100%**:
  - Khi thêm hoặc sửa đổi bất kỳ tính năng nào: Toàn bộ văn bản, nhãn chữ, nút bấm, giá trị, thông báo trên thanh Ribbon và hộp thoại Form **PHẢI hiển thị đầy đủ, rõ ràng, không bao giờ bị che khuất, đè lên nhau hay bị cắt xén chữ** ở mọi độ phân giải và tỷ lệ DPI Windows.
  - Bắt buộc dùng `this.ClientSize` thay cho `this.Size` khi định kích thước WinForms; tắt AutoSize = false với TrackBar trước khi đặt chiều cao; tạo khoảng đệm thoáng rộng giữa các control và kiểm tra kỹ trên Preview Studio trước khi chốt.

---

## 7. QUY TẮC MỌI TÍNH NĂNG ĐỀU PHẢI THAO TÁC ĐƯỢC TRÊN BẢN VẼ (MODELESS DIALOG 100% - LỆNH SẾP HỒ)

- **Từ bây giờ trở đi, TẤT CẢ các chức năng / cửa sổ của HDTOOL khi mở lên để sử dụng thì MỌI THAO TÁC ĐỀU LÀM ĐƯỢC TRÊN BẢN VẼ**:
  - Khi người dùng mở bất kỳ cửa sổ nào của HDTOOL (Tự Động Crop View, Căn Chỉnh Grids / Level, Thống Kê Ống Nước, Khối Lượng Ống Gió, Xuất Danh Mục Bản Vẽ, Cập Nhật Danh Mục, Menu HDTOOL, Cập Nhật Tool...): Người dùng **VẪN PHẢI THAO TÁC HOÀN TOÀN TỰ DO TRÊN BẢN VẼ REVIT** (Pan, Zoom, xoay 3D Isometric, click chọn đối tượng, chuyển Tab View / Sheet khác...).
  - **Bắt buộc 100% kiến trúc Modeless Dialog**: Mở cửa sổ bằng `form.Show(new RevitWindowWrapper(revitHwnd))` kết hợp `HDToolModelessHost.Run` / `ExternalEvent`.
  - **TUYỆT ĐỐI CẤM SỬ DỤNG `ShowDialog()`** cho các cửa sổ chức năng của HDTOOL vì `ShowDialog()` sẽ khóa cứng màn hình Revit, không cho kỹ sư thao tác trên bản vẽ.

---

## 8. QUY TẮC ẤN PHÍM `ESC` ĐỂ THOÁT LỆNH NGAY LẬP TỨC CHO MỌI CÔNG CỤ (LỆNH SẾP HỒ)

- **Bắt buộc cho TẤT CẢ công cụ hiện tại VÀ mọi công cụ phát triển mới sau này**:
  - Khi người dùng không dùng nữa chỉ cần **ấn phím `ESC` là thoát/đóng ngay cửa sổ công cụ** mà không cần di chuột bấm nút `Đóng` hay `X`.
  - Mọi `Form` trong HDTOOL (kể cả Form chính lẫn các hộp thoại con) **BẮT BUỘC kế thừa `HDToolBaseForm`** (trong `Source/HDToolExternalEvent.cs`) — lớp chuẩn tích hợp `KeyPreview = true` + `ProcessCmdKey(Keys.Escape)` (kèm `CancelButton = btnClose/btnCancel` và `this.Activate()` sau khi quay lại từ thao tác `Pick` trên Revit).
  - Nếu cửa sổ có hành vi phụ khi ấn `ESC` (VD: tắt highlight 3D hoặc hủy chế độ đang gõ ô `DataGridView`), chỉ cần override `OnBeforeEscapeClose()` trong `HDToolBaseForm`.