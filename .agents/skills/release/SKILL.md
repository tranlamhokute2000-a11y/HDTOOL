---
name: release
description: Quy trình kiểm tra an toàn, tối ưu mã nguồn, đóng gói bộ cài đặt chính thức và cập nhật tài liệu (/release).
---

# Workflow: Đóng Gói Phát Hành Phiên Bản Mới (/release)

Khi Sếp Hồ gõ `/release <tool> <phiên bản>`:

1. **Rà soát chất lượng**: `revit-reviewer` và `revit-security` rà soát toàn diện mã nguồn và độ an toàn dữ liệu.
2. **Cập nhật tài liệu**: `revit-docs-writer` hoàn thiện `HUONG_DAN_SU_DUNG.md` và ghi nhận changelog chi tiết.
3. **Đóng gói bộ cài đặt**: `revit-packaging` chạy `Build.ps1`, tạo file `HDTOOL_Setup_Version_{N}.exe` và zip trọn gói.
4. **Dọn dẹp lưu trữ**: Tự động dọn dẹp version cũ, chỉ giữ lại đúng 2 bản gần nhất, đảm bảo 0 file trên Desktop.
5. **Báo cáo Sếp Hồ**: Phát giọng nói thông báo qua loa và gửi link bộ cài hoàn chỉnh cho Sếp.

