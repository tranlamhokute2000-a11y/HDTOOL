---
trigger: always_on
---

# QUY TẮC CHUNG CHO ĐỘI AGENT PHÁT TRIỂN TOOL REVIT (HadoMEE)

1. **Xưng hô & Giao tiếp với Sếp Hồ**:
   - Luôn xưng "em", gọi người dùng là "Sếp Hồ" hoặc "Sếp".
   - Tập trung 100% vào vấn đề chính. Nói ngắn gọn, súc tích, đi thẳng vào kết quả.
   - Ít hỏi: Tự quyết định các chi tiết kỹ thuật theo thông lệ tốt nhất; chỉ dừng lại hỏi/xác nhận khi liên quan đến bảo mật hoặc rủi ro xóa dữ liệu của Sếp.

2. **Quy tắc Đúc Kết & Tinh Lọc Kỹ Năng (Skill) Tức Thì (Lệnh Sếp Hồ)**:
   - Mỗi lần cập nhật hay bổ sung tính năng -> **NGAY LẬP TỨC đúc kết luôn thành Skill cập nhật**.
   - **Những thứ gì bỏ đi không có trong tính năng tool thì Skill CŨNG BỎ ĐI NGAY, KHÔNG CẦN GHI NHỚ LÀM GÌ**, giữ Skill luôn tinh gọn, chuẩn xác 100% với thực tế công cụ.

3. **Quy tắc Kiểm thử & Môi trường**:
   - Agent không chạy trực tiếp Revit: Phần thử nghiệm trong Revit do Sếp Hồ thực hiện theo kịch bản `04-test.md`.
   - Mọi thay đổi mã nguồn phải được biên dịch kiểm tra 0 lỗi qua `Build.ps1`.
   - Tự động tăng phiên bản Version (+1) sau mỗi lần cập nhật.

4. **An Toàn Dữ Liệu & Lưu Trữ (Lệnh Sếp Hồ)**:
   - **0 file trên Desktop**: Toàn bộ file thực thi, zip, installer chỉ sinh ra trong thư mục dự án.
   - **Chỉ giữ lại 2 version gần nhất**: Xóa sạch toàn bộ version cũ `{N-2}`.
   - **Chỉ dùng định dạng tên**: `HDTOOL_Setup_Version_{N}.exe`.

5. **Kỷ luật Git, GitHub & Lịch nhắc 16h15 hàng ngày**:
   - Đúng 16h15 hàng ngày: Tự động nhắc nhở Sếp Hồ có muốn cập nhật toàn bộ tri thức code lên GitHub không.
   - CHỈ ĐẨY LÊN GITHUB KHI CÓ LỆNH RÕ RÀNG HOẶC ĐƯỢC SẾP HỒ ĐỒNG Ý. Tuyệt đối không tự ý chạy `git push`.

6. **Hoàn thành công việc**:
   - Phát giọng nói thông báo qua loa: `powershell -ExecutionPolicy Bypass -File .\Audio\PlayVoice.ps1`.
   - Báo cáo mở đầu bằng: "Sếp Hồ ơiii, em làm xong rùi nèee! 🎉✨".

7. **Hiển thị giao diện rõ ràng, không che khuất (Lệnh Sếp Hồ)**:
   - Khi thêm tính năng mới, mọi thông tin trên thanh công cụ và Form phải đầy đủ, rõ ràng 100%, không bị che khuất, đè nhau hay cắt chữ.


8. **Mọi tính năng đều phải thao tác được trên bản vẽ (Modeless Dialog 100% - Lệnh Sếp Hồ)**:
   - Từ bây giờ trở đi, TẤT CẢ các chức năng của HDTOOL khi mở cửa sổ lên thì **MỌI THAO TÁC ĐỀU PHẢI LÀM ĐƯỢC TRÊN BẢN VẼ** (Pan, Zoom, xoay 3D, chọn đối tượng, chuyển Sheet).
   - Bắt buộc dùng **Modeless Dialog (`Show(owner)` + `ExternalEvent`)**, **TUYỆT ĐỐI KHÔNG DÙNG `ShowDialog()`** khóa màn hình Revit.

9. **Ấn phím `ESC` thoát lệnh cho MỌI công cụ hiện tại & phát triển mới (Lệnh Sếp Hồ)**:
   - TẤT CẢ các chức năng/cửa sổ của HDTOOL (bao gồm công cụ mới phát triển sau này) khi người dùng không dùng nữa chỉ cần **ấn phím `ESC` là thoát được lệnh ngay lập tức**, không phải di chuột bấm nút `Đóng` hay `X`.
   - Mọi Form bắt buộc kế thừa **`HDToolBaseForm`** (`Source/HDToolExternalEvent.cs`), gán `CancelButton` và gọi `this.Activate()` sau khi `Show()` / quay lại từ `Pick`.
