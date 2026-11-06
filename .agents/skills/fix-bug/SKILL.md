---
name: fix-bug
description: Quy trình truy tìm nguyên nhân gốc rễ, sửa lỗi, chống đơ giật lag và kiểm thử hồi quy cho Revit Add-in (/fix-bug).
---

# Workflow: Sửa Lỗi & Tối Ưu Hóa (/fix-bug)

Khi Sếp Hồ gõ `/fix-bug <mô tả lỗi>` hoặc báo sự cố:

1. **Chẩn đoán gốc rễ**: `revit-debugger` phân tích hiện tượng, xác định 100% nguyên nhân gốc rễ và lập tài liệu `work/<tool-slug>/bug-<n>.md`.
2. **Áp dụng bản vá**: `revit-api-developer` sửa code (tối ưu Transaction, xóa Transaction lặp trong UI, cache HashSet $O(1)$, DoubleBuffered...).
3. **Thẩm định & Kiểm thử**: `revit-reviewer` và `revit-tester` kiểm tra tính toàn vẹn, bảo đảm 0 lỗi biên dịch và không gây lỗi hồi quy.
4. **Đóng gói**: `revit-packaging` chạy `Build.ps1` tăng Version, Hot Reload ngay vào Revit.
5. **Báo cáo**: Phát voice thông báo qua loa và báo cáo giải pháp chi tiết cho Sếp Hồ.

