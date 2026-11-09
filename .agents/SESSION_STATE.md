# SESSION STATE - HDTOOL (ADDIN REVIT)

- **Thời gian cập nhật**: 2026-11-09 16:38
- **Người dùng**: Sếp Hồ (`tranlamhokute2000-a11y`)
- **Doanh nghiệp**: **TẬP ĐOÀN HÀ ĐÔ - HadoMEE**
- **Tiêu ngữ chính thức**: **★ TẤT CẢ VÌ HÀ ĐÔ THÂN YÊU ★**
- **Hệ điều hành**: **Windows 11**
- **Phiên bản hiện tại**: **Version 59** (`HDTOOL_Setup_Version_59.exe`)

---

## 1. Công việc vừa hoàn thành theo lệnh Sếp Hồ:
**NÂNG CẤP THUẬT TOÁN BÓC KHỐI LƯỢNG ỐNG GIÓ & PHỤ KIỆN HVAC (TCVN 5687:2010 / SMACNA)**:

1. **Phân biệt Cút ống gió (Elbow) theo Góc độ uốn thực tế và Bán kính cong R**:
   - Tự động trích xuất chính xác góc độ uốn uốn thực tế ($90^\circ, 45^\circ, 60^\circ, 30^\circ...$) từ parameter và vector hướng Connector (không còn bị gán cứng tất cả vào 90° như trước).
   - Tự động trích xuất bán kính cong trong $R$ (chuẩn $R150$ theo bản vẽ Sếp gửi).
   - Tên cấu kiện: `Cút 90° Tôn Mạ Kẽm (R150)`, `Cút 45° Tôn Mạ Kẽm (R150)`.
   - Quy cách: `800x250 (90°, R150)`, `800x250 (45°, R150)`.
   - Diện tích m² tôn được tính chuẩn theo hình học cung cong uốn: cút $45^\circ$ tính đúng theo một nửa góc uốn của cút $90^\circ$, chuẩn xác 100%.

2. **Côn thu ống gió (Transition) trích xuất Chiều dài $L$ (Khoảng cách giữa 2 đầu kích thước)**:
   - Trích xuất tự động khoảng cách nối giữa 2 Connector hoặc parameter `Length` (ví dụ $L = 200\text{ mm} = 0.20\text{ m}$ theo hình Sếp vẽ).
   - Cột `Chiều Dài (m)` hiển thị rõ ràng chiều dài $L$ ($0.20\text{ m}$ thay vì để trống `-`).
   - Quy cách: `800x250 - 500x250 (L=200)`.
   - Diện tích m² tôn tính theo hình chóp cụt / hình thang chuẩn kỹ thuật.

3. **Lượn Z ống gió (Offset) đầy đủ Chiều dài $L$ và Độ chênh cao độ $\Delta H$**:
   - Trích xuất tự động Chiều dài dọc trục $L$ và Độ lệch cao độ $\Delta H$ (chênh lệch trục $Z$ giữa 2 đầu hoặc parameter Offset).
   - Quy cách: `250x800 (L=600, Chênh H=200)`.
   - Cột `Chiều Dài (m)` hiển thị rõ $0.60\text{ m}$.
   - Diện tích tôn tính theo cung lượn chữ Z chuẩn xác.

4. **Sắp xếp bảng khối lượng chuẩn chỉ theo lệnh Sếp Hồ (HẾT ỐNG GIÓ THẲNG TỪ TO ĐẾN NHỎ RỒI MỚI ĐẾN PHỤ KIỆN)**:
   - Toàn bộ **Ống Gió Thẳng** xếp lên hàng đầu, sắp xếp theo thứ tự kích thước tiết diện từ **LỚN NHẤT ĐẾN NHỎ NHẤT** (ví dụ: $1000\times400 \rightarrow 800\times250 \rightarrow 500\times250$).
   - Sau đó đến **Ống Gió Mềm**.
   - Sau đó đến **Phụ Kiện** (Cút từ to đến nhỏ, Côn từ to đến nhỏ, Tê, Lượn Z).
   - Sau đó đến **Van Gió** (VCD, FD, NRD).
   - Cuối cùng là **Miệng Gió**.
   - STT tự động đánh tuần tự $1, 2, 3...$ chuẩn mực cho cả DataGridView, file Excel CSV và Bảng Legend đặt lên Sheet.

5. **Biên dịch & Đóng gói**:
   - Biên dịch thành công 0 lỗi: `HDTOOL.dll` (Version 59) và [`HDTOOL_Setup_Version_59.exe`](file:///c:/Users/Nguye/Desktop/ADDIN%20REVIT/HDTOOL_Setup_Version_59.exe).
   - Tự động đồng bộ Hot Reload sang Revit 2024, Revit 2025, Revit 2027.
   - Tuân thủ nghiêm ngặt quy tắc Sếp Hồ: 0 file trên Desktop, chỉ giữ lại đúng 2 Version gần nhất (Version 58 và Version 59).
