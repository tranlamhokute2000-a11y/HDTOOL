---
name: revit-requirements
description: Phân tích và chuyển đổi mô tả của Sếp Hồ thành đặc tả kỹ thuật chi tiết cho Add-in Revit (01-spec.md).
---

# Revit Requirements — Chuyên Viên Phân Tích Yêu Cầu

## Nhiệm vụ
Lắng nghe yêu cầu của Sếp Hồ, trích xuất mục tiêu kỹ thuật và lập tài liệu đặc tả `work/<tool-slug>/01-spec.md`.

## Nội dung hồ sơ 01-spec.md
1. **Mục tiêu công cụ**: Giải quyết bài toán gì cho kỹ sư MEP / Revit (ví dụ: bóc khối lượng, căn chỉnh dim, xuất sheet...).
2. **Đối tượng Revit tác động**:
   - Categories liên quan (`OST_DuctCurves`, `OST_PipeCurves`, `OST_DuctFitting`, `OST_Levels`, `OST_Grids`...).
   - Loại View hỗ trợ: Mặt bằng (FloorPlan), Mặt đứng (Elevation), Mặt cắt (Section), View 3D, Sheet.
3. **Đầu vào & Đầu ra**:
   - Đầu vào: Lựa chọn chuột (Pick), quét vùng (Rectangle), toàn dự án hoặc active view.
   - Đầu ra: Bảng WinForms/WPF, bảng Schedule Revit, xuất Excel (.csv), highlight đối tượng.
4. **Tham số & Tiêu chuẩn**:
   - Các Parameter dự án cần đọc / ghi.
   - Tiêu chuẩn kỹ thuật áp dụng (TCVN 5687:2010, SMACNA, v.v.).

