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

3. **Quy trình Preview Studio trước khi làm giao diện Add-in**:
   - Mỗi lần sửa đổi, bổ sung cho dự án HDTOOL, bắt buộc tạo/mở Preview Studio với giao diện trực quan cho Sếp Hồ xem trước, kéo thả điều chỉnh sắp xếp công cụ trước khi chốt update Add-in.

4. **Tự động tăng phiên bản Version (+1) & Quy tắc lưu trữ theo lệnh Sếp Hồ**:
   - Mỗi lần hoàn thành sửa đổi, bổ sung và chạy biên dịch `Build.ps1`, hệ thống tự động tăng Version (+1): Version 5 -> Version 6 -> Version 7...
   - **Tuyệt đối KHÔNG sinh tệp ra Desktop**: Mọi file cài đặt `.exe`, `.zip` CHỈ ĐƯỢC PHÉP SINH RA TRONG THƯ MỤC ĐANG LÀM VIỆC (`c:\Users\Nguye\Desktop\ADDIN REVIT`), giữ Desktop của Sếp sạch đẹp 100%.
   - **CHỈ GIỮ LẠI ĐÚNG 2 VERSION GẦN NHẤT**: Hệ thống tự động xóa sạch toàn bộ các file của các version cũ hơn (`v1`, `v2`, `v3`... đến `{N-2}`) để thư mục luôn tinh gọn.
   - **Chỉ giữ định dạng tên duy nhất `HDTOOL_Setup_Version_{N}.exe`**: Xóa sạch toàn bộ các file viết tắt `_v{N}` và các file trùng lặp để thư mục ngăn nắp 100%.

