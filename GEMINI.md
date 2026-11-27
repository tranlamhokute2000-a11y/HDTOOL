# WORKSPACE RULES & SESSION MEMORY (GEMINI.MD)

Tài liệu này kích hoạt chế độ ghi nhớ trạng thái làm việc liên tục cho Antigravity:

0. **Tập trung, ngắn gọn & đúng trọng tâm theo lệnh Sếp Hồ**:
   - Khi Sếp giao việc: Tập trung 100% vào vấn đề chính.
   - Nói ngắn gọn, súc tích, đi thẳng đúng trọng tâm cần cung cấp.
   - Làm việc đúng yêu cầu, tuyệt đối không lan man.

1. **Luôn nhớ bối cảnh công việc**:
   - Trước khi trả lời yêu cầu tiếp tục công việc, Antigravity đọc ngay `.agents/SESSION_STATE.md`.
   - Nắm rõ công việc đang làm, các file đang mở, tính năng vừa làm và trạng thái biên dịch.

2. **Cập nhật trạng thái sau mỗi bước**:
   - Sau khi hoàn thành hoặc sửa đổi bất kỳ mã nguồn nào, Antigravity tự động cập nhật `.agents/SESSION_STATE.md` để đảm bảo dù người dùng đóng hay mở lại Antigravity, phiên làm việc luôn được tiếp nối chính xác ở trạng thái gần nhất.

3. **Đúc kết & Tinh lọc Kỹ năng (Skill) tức thì (BẮT BUỘC THEO LỆNH SẾP HỒ)**:
   - Mỗi lần cập nhật hay bổ sung tính năng mới -> **NGAY LẬP TỨC đúc kết luôn thành Skill cập nhật** tại `.agents/skills/hdtool/SKILL.md` và `SKILL.md`.
   - **Những thứ gì bỏ đi không còn trong tính năng tool thì Skill CŨNG BỎ ĐI NGAY LẬP TỨC, KHÔNG CẦN GHI NHỚ LÀM GÌ**. Giữ Skill luôn tinh gọn, sắc bén, phản ánh đúng 100% hiện trạng thực tế công cụ.

4. **Quy trình Preview Studio trước khi làm giao diện Add-in**:
   - Mỗi lần sửa đổi, bổ sung cho dự án HDTOOL, bắt buộc tạo/mở Preview Studio với giao diện trực quan cho Sếp Hồ xem trước, kéo thả điều chỉnh sắp xếp công cụ trước khi chốt update Add-in.

5. **Tự động tăng phiên bản Version (+1) & Quy tắc lưu trữ theo lệnh Sếp Hồ**:
   - Mỗi lần hoàn thành sửa đổi, bổ sung và chạy biên dịch `Build.ps1`, hệ thống tự động tăng Version (+1): Version 5 -> Version 6 -> Version 7...
   - **Tuyệt đối KHÔNG sinh tệp ra Desktop**: Mọi file cài đặt `.exe`, `.zip` CHỈ ĐƯỢC PHÉP SINH RA TRONG THƯ MỤC ĐANG LÀM VIỆC (`c:\Users\Nguye\Desktop\ADDIN REVIT`), giữ Desktop của Sếp sạch đẹp 100%.
   - **CHỈ GIỮ LẠI ĐÚNG 2 VERSION GẦN NHẤT**: Hệ thống tự động xóa sạch toàn bộ các file của các version cũ hơn (`v1`, `v2`, `v3`... đến `{N-2}`) để thư mục luôn tinh gọn.
   - **Chỉ giữ định dạng tên duy nhất `HDTOOL_Setup_Version_{N}.exe`**: Xóa sạch toàn bộ các file viết tắt `_v{N}` và các file trùng lặp để thư mục ngăn nắp 100%.

6. **Quy tắc Git, GitHub & Lịch nhắc 16h15 hàng ngày theo lệnh Sếp Hồ**:
   - **Lịch nhắc nhở 16h15 hàng ngày**: Cứ đúng 16h15 mỗi ngày, Antigravity chủ động nhắc Sếp Hồ: *"Sếp Hồ ơi, đã 16h15 rồi, Sếp có muốn em cập nhật (push) toàn bộ code, tài liệu và tri thức mới nhất hôm nay lên GitHub không ạ?"*
   - CHỈ ĐẨY LÊN GITHUB KHI CÓ LỆNH RÕ RÀNG HOẶC ĐƯỢC SẾP ĐỒNG Ý. Tuyệt đối không tự tiện chạy `git push`.

7. **Quy tắc thiết kế giao diện UX/UI rõ ràng, rành mạch, không che khuất, không nửa vời (Lệnh Sếp Hồ)**:
   - Khi thêm hoặc sửa đổi tính năng, toàn bộ nhãn chữ, nút bấm, thông số trên Ribbon và Form **PHẢI hiển thị đầy đủ, rõ ràng 100%, tuyệt đối không bị che khuất, đè nhau, tràn mép hay bị cắt xén chữ** ở mọi độ phân giải và tỷ lệ DPI Windows.
   - **5 nguyên tắc vàng bắt buộc**:
     1. Tuyệt đối KHÔNG dùng emoji đa byte trên WinForms (tránh lỗi ô vuông font chữ `[]`). Dùng tiếng Việt chuẩn mực hoặc bullet `●`.
     2. Bố cục phân tầng độc lập (`TableLayoutPanel` cố định hàng cho Header/Toolbar/Bottom, Content Fill 100% -> triệt tiêu lỗi che khuất hàng 1).
     3. Tự động co giãn theo chiều rộng (`AdjustLayout` / `w = ClientSize.Width - 32` / `Anchor = Left | Right`) chống tràn mép hoặc cắt cụt nút bấm lớn.
     4. Nút lọc danh mục dùng `AutoSize = true` + `Padding(12, 2, 12, 2)`; cột DataGridView giá trị đặt `AutoSizeMode = Fill`.
     5. Thông điệp và hướng dẫn sử dụng phải trực quan, rành mạch, ghi rõ Modeless 100% và phím ESC để người dùng nhìn vào là hiểu ngay cách dùng.


8. **Quy tắc mọi tính năng đều phải thao tác được trên bản vẽ (Modeless Dialog 100% - Lệnh Sếp Hồ)**:
   - Từ bây giờ trở đi, TẤT CẢ các chức năng / cửa sổ của HDTOOL khi mở lên để sử dụng thì **MỌI THAO TÁC ĐỀU PHẢI LÀM ĐƯỢC TRÊN BẢN VẼ** (Pan, Zoom, xoay 3D, click chọn ống, chuyển Tab View/Sheet).
   - Bắt buộc 100% dùng kiến trúc **Modeless Dialog (`Show(new RevitWindowWrapper(hwnd))` + `HDToolModelessHost.Run` / `ExternalEvent`)**.
   - **TUYỆT ĐỐI KHÔNG DÙNG `ShowDialog()`** làm khóa cứng giao diện bản vẽ Revit của Sếp Hồ.

9. **Quy tắc ấn phím `ESC` thoát lệnh cho mọi công cụ hiện tại & tương lai (Lệnh Sếp Hồ)**:
   - TẤT CẢ các chức năng/cửa sổ của HDTOOL (hiện có và phát triển thêm sau này) đều phải hỗ trợ **ấn phím `ESC` là thoát ngay cửa sổ công cụ lập tức**, không nhất thiết phải di chuột bấm `Đóng` / `X`.
   - Mọi Form bắt buộc kế thừa **`HDToolBaseForm`** (`Source/HDToolExternalEvent.cs` — tích hợp `HDToolEscapeMessageFilter` đón đầu phím ESC toàn cục, `KeyPreview = true` + `ProcessCmdKey` và `OnKeyDown` bắt `Keys.Escape`, tự động `Activate()` khi hiển thị).
   - Tuyệt đối không viết logic chặn phím ESC (kể cả ô DataGridView đang gõ chữ: ấn ESC là đóng Form ngay lập tức).
