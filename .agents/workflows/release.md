# Workflow: Đóng Gói Phát Hành Phiên Bản Mới (/release)

1. `revit-reviewer` + `revit-security`: Kiểm tra toàn diện an toàn mã nguồn.
2. `revit-docs-writer`: Cập nhật `HUONG_DAN_SU_DUNG.md` và changelog.
3. `revit-packaging`: Chạy `Build.ps1`, tạo bộ cài 1-file `HDTOOL_Setup_Version_{N}.exe`.
4. `revit-packaging`: Xóa bỏ version cũ, chỉ giữ lại đúng 2 bản gần nhất, 0 file trên Desktop.
5. `revit-docs-writer`: Phát âm thanh thông báo và báo cáo Sếp Hồ.

