# SESSION STATE - HDTOOL (ADDIN REVIT)

- **Thời gian cập nhật**: 2026-11-09 17:06
- **Người dùng**: Sếp Hồ (`tranlamhokute2000-a11y`)
- **Doanh nghiệp**: **TẬP ĐOÀN HÀ ĐÔ - HadoMEE**
- **Tiêu ngữ chính thức**: **★ TẤT CẢ VÌ HÀ ĐÔ THÂN YÊU ★**
- **Hệ điều hành**: **Windows 11**
- **Phiên bản hiện tại**: **Version 60** (`HDTOOL_Setup_Version_60.exe`)

---

## 1. Công việc vừa hoàn thành theo lệnh Sếp Hồ:
1. **Tự động cập nhật thông số ống gió tức thì khi người dùng thay đổi trên Revit**:
   - Đăng ký sự kiện native Revit API: `Application.DocumentChanged`.
   - Khi người dùng ở trên màn hình Revit chỉnh sửa bất kỳ thông số nào của ống gió (thay đổi kích thước, chiều dài, cao độ, hệ thống, đổi góc cút, dịch chuyển...):
     * Hệ thống tự động bắt trọn sự kiện thay đổi.
     * Áp dụng cơ chế **Debounce 300ms** an toàn chống giật lag.
     * **Bảng khối lượng tự động cập nhật số liệu mới ngay lập tức**, người dùng không cần phải bấm nút "Quét Lại Dữ Liệu"!
2. **Quy cách Lượn Z (Offset) bỏ chữ "Chênh", chỉ để `H=`**:
   - Quy cách chuyển từ: `250x800 (L=555, Chênh H=400)` $\rightarrow$ `250x800 (L=555, H=400)`.
   - Ngắn gọn, súc tích và đúng chuẩn kỹ thuật theo lệnh Sếp.
3. **Biên dịch & Triển khai**:
   - Biên dịch thành công 0 lỗi: `HDTOOL.dll` (Version 60) và [`HDTOOL_Setup_Version_60.exe`](file:///c:/Users/Nguye/Desktop/ADDIN%20REVIT/HDTOOL_Setup_Version_60.exe).
   - Tự động đồng bộ Hot Reload sang Revit 2024.
   - Giữ đúng 2 version gần nhất (Version 59 và Version 60), 0 file trên Desktop.
