---
name: new-tool
description: Quy trình phát triển một công cụ Revit mới trọn gói từ đặc tả, UI, code API, review, đóng gói đến bộ cài (/new-tool).
---

# Workflow: Làm Tool Revit Mới Trọn Quy Trình (/new-tool)

Khi Sếp Hồ gõ `/new-tool <mô tả>` hoặc yêu cầu phát triển công cụ mới, thực thi tuần tự các bước:

1. **Nhận lệnh & Điều phối**: `revit-orchestrator` tiếp nhận yêu cầu và tạo thư mục làm việc `work/<tool-slug>/`.
2. **Đặc tả kỹ thuật**: `revit-requirements` lập hồ sơ `work/<tool-slug>/01-spec.md` (mục tiêu, Categories, Parameters, Input/Output).
3. **Thiết kế kiến trúc**: `revit-architect` thiết kế cấu trúc lớp tại `work/<tool-slug>/02-design.md` (Command, Service, Model, tương thích Revit 2024-2027).
4. **Giao diện trực quan**: `revit-ui` tạo bố cục Preview Studio (`HDTool_PreviewStudio.html`) để Sếp Hồ duyệt trước.
5. **Lập trình Revit API**: `revit-api-developer` viết mã nguồn C# chuẩn chỉ trong `Source/` (**mọi Form mới BẮT BUỘC kế thừa `HDToolBaseForm` để chạy Modeless + ấn `ESC` thoát lệnh ngay lập tức**, gán `CancelButton`, và gọi `this.Activate()` sau các thao tác `Pick`).
6. **Thẩm định & Kiểm thử**: 
   - `revit-reviewer` soi xét code (`work/<tool-slug>/03-review.md`).
   - `revit-tester` lập kịch bản test (`work/<tool-slug>/04-test.md`).
   - `revit-security` rà soát an toàn dữ liệu (`work/<tool-slug>/05-security.md`).
7. **Đóng gói & Triển khai**: `revit-packaging` chạy `Build.ps1`, tăng Version (+1), Hot Reload vào Revit.
8. **Bàn giao**: `revit-docs-writer` cập nhật tài liệu, phát âm thanh thông báo và báo cáo Sếp Hồ.

