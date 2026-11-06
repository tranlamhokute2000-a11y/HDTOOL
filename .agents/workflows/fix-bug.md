# Workflow: Sửa Lỗi & Tối Ưu Hóa (/fix-bug)

1. `revit-debugger`: Phân tích nguyên nhân gốc rễ, lập hồ sơ `work/<tool-slug>/bug-<n>.md`.
2. `revit-api-developer`: Áp dụng bản vá tối ưu vào mã nguồn C#.
3. `revit-reviewer` + `revit-tester`: Kiểm tra không gây lỗi hồi quy.
4. `revit-packaging`: Chạy `Build.ps1`, tăng Version, triển khai Hot Reload.
5. `revit-docs-writer`: Phát loa thông báo và báo cáo Sếp Hồ.

