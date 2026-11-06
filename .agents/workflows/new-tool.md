# Workflow: Làm Tool Revit Mới Trọn Quy Trình (/new-tool)

1. `revit-orchestrator`: Nhận yêu cầu từ Sếp Hồ, tạo `work/<tool-slug>/`.
2. `revit-requirements`: Lập tài liệu đặc tả kỹ thuật `01-spec.md`.
3. `revit-architect`: Thiết kế giải pháp kiến trúc lớp `02-design.md`.
4. `revit-ui`: Tạo Preview Studio trực quan cho Sếp Hồ duyệt trước.
5. `revit-api-developer`: Viết mã nguồn C# Revit API trong `Source/`.
6. `revit-reviewer` + `revit-tester`: Review code `03-review.md` và lập kịch bản test `04-test.md`.
7. `revit-packaging`: Chạy `Build.ps1`, tăng Version, Hot Reload vào Revit.
8. `revit-docs-writer`: Cập nhật tài liệu, phát loa thông báo và báo cáo Sếp Hồ.

