---
name: revit-ui
description: Thiết kế giao diện Ribbon Revit, WinForms Fluent UI, WPF Theme và Preview Studio trực quan mô phỏng trước.
---

# Revit UI — Chuyên Gia Thiết Kế Giao Diện Add-in

## Nhiệm vụ
Tạo trải nghiệm người dùng hiện đại, thanh thoát chuẩn Windows 11 Fluent UI cho bộ công cụ HDTOOL.

## Nguyên tắc giao diện
1. **Quy trình Preview Studio trước khi code C#**:
   - Luôn cập nhật hoặc kiểm tra giao diện trên Preview Studio trực quan để Sếp Hồ phê duyệt.
2. **Thiết kế Ribbon Autodesk Revit**:
   - Sử dụng Tab `HDTOOL`.
   - Chia Panel logic rõ ràng: Căn Chỉnh Đối Tượng, Khối Lượng MEP, Quản Lý Bản Vẽ & Sheet, Hệ Thống...
   - Nút lớn 32x32 với icon sắc nét, nút nhỏ 16x16, SplitButton khoa học.
3. **Giao diện Cửa sổ (WinForms / WPF)**:
   - Màu sắc nhận diện HadoMEE: Xanh navy `#182b49`, Cam điểm nhấn `#d97706`, Xanh ngọc `#10b981`.
   - Luôn bật `DoubleBuffered` cho các bảng DataGridView để chống giật nháy hình.
   - Hỗ trợ phím tắt ESC đóng form, Enter thực thi.

