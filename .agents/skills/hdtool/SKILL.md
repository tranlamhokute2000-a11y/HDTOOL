---
name: hdtool
description: >-
  HDTOOL: Bộ công cụ chuyên nghiệp Autodesk Revit: Dựng Lại Mô Hình Hệ Thống Ống Gió 3D Tự Động Từ Hình Vẽ Bản Vẽ CAD Link (Nhận diện hình học Rectangle ống thẳng, Trapezoid Côn thu đồng tâm, Chân rẽ Tap đâm sườn ống, Cút góc, Chạc chia Tee, trích xuất Text WxH từ DWG gốc, lan truyền kích thước BFS, Live Preview Modeless); Tự Động Tạo & Căn Chỉnh Khung Crop View (Auto Crop Box) bao quanh toàn bộ đối tượng mô hình trong View vừa tạo (SĐKG 3D, Mặt bằng, Mặt cắt, Sheet Viewport) với cơ chế Tự Động Hiểu Ý Người Dùng (không dãn to ra các trục khác); Căn chỉnh khoảng cách Level (Cao độ) và Grids (Lưới trục) theo Crop View (mm); Quản lý & Xuất Danh mục Bản vẽ Sheet (PDF, DWG, Excel); Sửa số hiệu hàng loạt có tính năng Kéo chuột tự động tăng đuôi giống Excel (AutoFill Drag Handle); Thống kê Khối lượng Ống Nước & Phụ kiện MEP; Bóc tách Khối lượng Ống Gió & Phụ kiện HVAC (TCVN 5687:2010 / SMACNA - m² Tôn & Phụ kiện) với giao diện Modeless Dialog 60 FPS 100% không khóa Revit, Live Tracking, Tự Động Cập Nhật thời gian thực, đo Côn thu L, Cút uốn R150, Lượn Z và sắp xếp chuẩn kỹ sư MEP HadoMEE.
---

# Skill: HDTOOL - Bộ Công Cụ Chuyên Nghiệp Autodesk Revit (Version 71)

## 1. Giới thiệu Tổng quan
Skill này định nghĩa cấu trúc, thuật toán hình học vector, mô hình bóc tách khối lượng HVAC & MEP, tính năng Dựng Mô Hình 3D Ống Gió Từ HÌNH VẼ CAD Link Vào Revit (Geometric Shape Recognition), tính năng Quản lý Danh mục Sheet & Đổi Số Hiệu Kéo Tự Động Tăng Đuôi giống Excel, kiến trúc lập trình C# Revit API không khóa luồng (**Modeless Dialog 60 FPS 100% cho TẤT CẢ các chức năng**), giải pháp Dynamic Ribbon Injection không cần tắt Revit, tích hợp project `.csproj` chuẩn .NET Framework 4.8 và quy trình tự động hóa triển khai cho Add-in **HDTOOL** trong Autodesk Revit (tương thích đa phiên bản: Revit 2024, 2025, 2026, 2027...).

> [!IMPORTANT]
> **Quy tắc Đúc Kết & Tinh Lọc Kỹ Năng (Lệnh Sếp Hồ)**:
> - Mỗi lần cập nhật hay bổ sung tính năng -> **NGAY LẬP TỨC đúc kết luôn thành Skill cập nhật**.
> - **Những thứ gì bỏ đi, không còn trong tính năng tool thì Skill CŨNG BỎ ĐI NGAY, KHÔNG CẦN GHI NHỚ LÀM GÌ**, đảm bảo Skill luôn tinh gọn, sắc bén và phản ánh đúng 100% hiện trạng công cụ đang hoạt động thực tế.
> - **Quy tắc hiển thị giao diện rõ ràng (Lệnh Sếp Hồ)**: Mọi thông tin, nhãn chữ, nút bấm, thông số trên thanh công cụ Ribbon và giao diện Form **PHẢI hiển thị đầy đủ, rõ ràng 100%, tuyệt đối không được che khuất, đè nhau hay cắt xén chữ** (sử dụng ClientSize, tắt AutoSize ở TrackBar).
> - **Quy tắc Modeless Dialog 100% (Lệnh Sếp Hồ)**: Từ bây giờ trở đi, TẤT CẢ các chức năng / cửa sổ của HDTOOL khi mở lên để sử dụng thì **MỌI THAO TÁC ĐỀU PHẢI LÀM ĐƯỢC TRÊN BẢN VẼ REVIT** (Pan, Zoom, xoay 3D, chọn đối tượng, chuyển Sheet/View). TUYỆT ĐỐI KHÔNG dùng `ShowDialog()` khóa giao diện Revit.

---

## 2. Hệ Thống Chức Năng Chính (Cập Nhật Chuẩn Version 71)

### 1. Dựng Mô Hình 3D Hệ Thống Ống Gió Từ Hình Vẽ CAD Link — [ĐỈNH CAO MỚI VERSION 71]
- **⚡ Đột Phá: Nhận Diện Bằng HÌNH VẼ HÌNH HỌC (Geometric Pattern Recognition) 100% Không Phụ Thuộc Layer**:
  * **Không bắt người dùng phải chọn layer thủ công**: Các bản vẽ CAD thực tế thường vẽ ống chính ở layer này, cút góc và ống nhánh ở layer khác, text ở layer khác. Tool nhận diện trực tiếp bằng hình học thực tế của các đối tượng đồ họa.
  * **Đoạn ống thẳng (Straight Duct)**: Tự động nhận diện các hình chữ nhật khép kín 4 đỉnh (Closed PolyLine / Loop Lines), đo chính xác bề rộng $W = 100 \dots 3500\text{ mm}$ và chiều dài $L$, xác định tim ống nối trung điểm 2 cạnh đáy ngắn.
  * **Côn thu chuyển size (Concentric Transition)**: Tự động nhận diện các hình thang khép kín 4 đỉnh (Trapezoid), có 2 cạnh đáy song song khác kích thước ($W_1 \ne W_2$, ví dụ $800 \to 500$). Tạo liên kết hình học nối mượt mà giữa đoạn ống $W_1$ và $W_2$ bằng fitting `NewTransitionFitting` đồng tâm 100%.
  * **Chân rẽ Tap (Takeoff Shoe Tap 45°)**: Tự động nhận diện các hình thang vát 45° có đáy to áp sát sườn ống chính và đáy nhỏ nối vào nhánh rẽ. Tự động dùng `NewTakeoffFitting` đâm thẳng sườn ống chính.
  * **Cút chuyển hướng (Elbow) & Chạc chia (Tee)**: Tự động nhận diện các đa giác góc quay và chạc chia nhánh (như `VERTS:11`), đồng bộ hóa các đầu mút co-planar để Revit tự động lắp Elbow / Tee / Double Tap.
- **⚡ Trích Xuất Text WxH Từ DWG Gốc & Lan Truyền Kích Thước BFS (Rule 6 Sếp Hồ)**:
  * Tự động trích xuất toàn bộ Text ghi chú ModelSpace từ file `.dwg` gốc qua `accoreconsole.exe` (chỉ chạy 1 lần và cache tức thì vào `%TEMP%\HDTOOL_CadCache`).
  * Khớp chính xác nhãn Text (`OAD 800x350: EI60`, `OAD 500x300: EI60`, `OAD 350x300: EI60`...) với các đoạn hình học tương ứng.
  * Nếu đoạn ống sau chuyển hướng **chưa có Text**: Thuật toán lan truyền BFS tự động kế thừa chiều cao $H$ từ đoạn ống phía trước.
- **⚡ Thao Tác Modeless Chuẩn 1 Click**:
  * **`🎯 PICK CHỌN HÌNH VẼ TUYẾN ỐNG TRÊN CAD LINK`**: Click 1 điểm trên hình vẽ ống gió $\to$ Tool tự động nhận diện cả tuyến ống nối tiếp qua côn, cút, chân rẽ.
  * **`⬚ Quét Vùng (Pick Box)`**: Quét chọn vùng chữ nhật chứa hệ thống ống gió.
  * **`🌐 Quét Toàn Bộ CAD`**: Quét toàn bộ file CAD link.
  * **Bảng Live Preview DataGridView**: Xem trước toàn bộ danh sách ống thẳng, côn thu, cút, kích thước WxH, chiều dài, mối nối tiếp và nguồn kích thước trước khi bấm **`🚀 DỰNG MÔ HÌNH REVIT 3D`**.

---

### 2. Tự Động Tạo & Căn Chỉnh Khung Crop View (Auto Crop Box)
- **⚡ Cơ chế "TỰ ĐỘNG HIỂU Ý NGƯỜI DÙNG" (Không Dãn To Ra Trục Khác)**:
  * Khi người dùng đã co kéo Crop View hướng về một trục hoặc một cụm đối tượng cụ thể: Công cụ tự động nhận diện biên độ Crop hiện tại, **CHỈ giữ lại các đối tượng đang nằm trong (hoặc chạm) phạm vi Crop hiện tại**.
  * **TUYỆT ĐỐI LOẠI BỎ** các trục ống hoặc đối tượng khác nằm ngoài phạm vi Crop hiện tại, không bao giờ dãn to khung hình ra nữa!
- **Thuật toán hình học đỉnh thực tế (Vertex-Exact Geometry + Liang-Barsky 3D Clipping)**:
  * Trích xuất trực tiếp tọa độ đỉnh, đường tim tâm ống và bán kính thực tế từ GeometryElement, ôm sát khít từng milimet theo mặt phẳng chiếu của View.
  * Tự động cắt gọt các trục ống đứng xuyên tầng theo biên độ của `Section Box` đang bật, bảo toàn nguyên vẹn Section Box gốc của Sếp.
  * Mặc định kích hoạt: **`Bỏ qua File Link Kiến Trúc/Kết Cấu`** và **`Ưu tiên ôm khít hệ thống MEP`**.

---

### 3. Quản Lý Danh Mục Sheet & Sửa Số Hiệu Hàng Loạt (Sheet List Manager)
- **⚡ Kéo Chuột Tự Động Tăng Số Đuôi Giống Microsoft Excel (Excel AutoFill Drag Handle)**:
  * Tại cột **`Số Hiệu Mới (Xem Trước) ->`**, ô đang chọn hiển thị điểm vuông xanh đặc trưng ở góc dưới-phải (Fill Handle).
  * Rê chuột vào điểm vuông xanh đổi thành con trỏ chữ thập `+`, nhấn giữ và kéo chuột xuống/lên để tăng dần số đuôi (`/C2/00 -> /C2/01 -> /C2/02...` hoặc `05.1 -> 05.2...`).
  * Double-Click Fill Handle tự động điền tăng dần xuống tận cùng danh sách.
  * Hỗ trợ phím tắt `Ctrl+D` (Fill Down), nút bấm tiện ích và Menu chuột phải.

---

### 4. Thống Kê Khối Lượng Ống Gió & Phụ Kiện HVAC (TCVN 5687:2010 / SMACNA)
- Modeless Dialog 60 FPS, Live Selection Tracking ("Tự bóc khi chọn trên Revit"), Tự động cập nhật thời gian thực khi sửa thông số trên Revit (`DocumentChanged`).
- Phân loại Cút (Elbow) theo góc uốn thực tế & bán kính cong $R150$. Côn thu đọc chuẩn Parameter Chiều dài $L$. Lượn Z có độ chênh cao $H$.

---

### 5. Căn Chỉnh Lưới Trục (Grids) & Cao Độ (Level) Theo Khung Crop View (mm)
- Căn chỉnh Lưới trục 4 hướng trên Mặt bằng, Mặt đứng, Mặt cắt & Sheet theo khoảng cách mm. Căn chỉnh Cao độ Level trên Mặt cắt, Mặt đứng. Hoạt động dạng Modeless không khóa bản vẽ.

---

### 6. Thống Kê Khối Lượng Ống Nước & Phụ Kiện MEP
- Bóc cục bộ theo vùng quét, gộp 3 tham số tên phụ kiện phân mảnh làm 1 cột duy nhất, soi sáng 2 chiều (Highlight), kéo thả Schedule vào Sheet.

---

### 7. Đóng Gói Bộ Cài Đặt 1-File Độc Lập & Kỷ Luật Lưu Trữ Sếp Hồ
- **Bộ cài đặt 1-File độc lập**: `HDTOOL_Setup_Version_{N}.exe` (hiện tại là **Version 71**).
- **Quy tắc lưu trữ nghiêm ngặt của Sếp Hồ**:
  * **0 file trên Desktop**: Toàn bộ file cài đặt, zip, build chỉ sinh ra trong thư mục dự án (`c:\Users\Nguye\Desktop\ADDIN REVIT`).
  * **Chỉ giữ lại đúng 2 Version gần nhất**: Tự động xóa sạch version cũ `{N-2}` (hiện giữ `Version 70` và `Version 71`).
  * **Chỉ dùng định dạng tên**: `HDTOOL_Setup_Version_{N}.exe`.
- **In-Memory Hot Reload Engine & Dynamic Ribbon**: Nạp nóng DLL vào AppDomain khi Revit đang mở và bơm nút Ribbon động tức thì.

---

## 3. Cấu Trúc Mã Nguồn C# (`Source/`)
- `CadDuctModels.cs`: Models dữ liệu hình học ống gió, côn thu (`CadShapeKind.Transition`), chân rẽ Tap (`CadShapeKind.TakeoffTap`), cút (`CadShapeKind.Elbow`), chạc 3 (`CadShapeKind.TeeBranch`), `CadConnectionEdge`.
- `CadDwgExtractor.cs`: Bộ trích xuất Text WxH và Block van gió ModelSpace từ file `.dwg` gốc bằng `accoreconsole.exe` có bộ đệm cache tốc độ cao.
- `CadToDuctService.cs`: Engine nhận diện hình học, phân loại đa giác, nối đồ thị topology và dựng 3D trong Revit MEP.
- `CadToDuctForm.cs`: Giao diện Modeless Form với nút Pick tuyến ống 1-click, quét vùng, bảng Live Preview và thiết lập thông số Revit.
- `CadToDuctCommand.cs`: Command khởi chạy và điều phối Form dạng Modeless.
