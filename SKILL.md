---
name: hdtool
description: >-
  HDTOOL: Bộ công cụ chuyên nghiệp Autodesk Revit căn chỉnh khoảng cách Level (Cao độ) và Grids (Lưới trục) so với khung Crop View theo đơn vị mm, hỗ trợ Mặt bằng, Mặt cắt, Mặt đứng và Trang in (Sheet) với giao diện người dùng chủ động lựa chọn công cụ.
---

# Skill: HDTOOL - Căn Chỉnh Level & Lưới Trục Grids Theo Crop View (Autodesk Revit)

## 1. Giới thiệu Tổng quan
Skill này định nghĩa cấu trúc, thuật toán hình học vector, mã nguồn C# Revit API và quy trình tự động hóa triển khai cho Add-in **HDTOOL** trong Autodesk Revit.

### Chức năng chính:
1. **Căn chỉnh Lưới trục (Grids) theo Khung Crop View trên Mặt bằng, Mặt đứng, Mặt cắt & Sheet**:
   - Tự động co/dãn hai đầu lưới trục Grids (phương ngang Y2, Y3, Y4... và phương dọc X8...) cân đối theo khung **Crop View** theo đơn vị **milimet (mm)**.
   - **Hỗ trợ toàn diện Mặt đứng (Elevation) & Mặt cắt (Section)**: Nhận diện lưới trục xuất hiện trên Mặt đứng và căn chỉnh chuẩn xác theo khung Crop View. Khi mở trên Sheet, công cụ tự động quét và căn chỉnh đồng loạt cả các Viewport Mặt bằng và Mặt đứng có Grids chỉ với 1 cú click!
   - **Giữ nguyên bảng điều khiển để tinh chỉnh liên tục (Live Tuning)**: Khi bấm căn chỉnh, lưới trục trên màn hình lập tức thay đổi trong khi bảng điều khiển vẫn giữ nguyên mở, không có pop-up kết quả phiền phức. Người dùng có thể tiếp tục nhập lại thông số nếu chưa ưng ý cho đến khi bấm nút Đóng hoặc phím ESC.
   - **Tự động đưa các Grid ngoài Crop View vào căn cùng**: Quét toàn bộ Document để tìm các Grid có đường tim đi qua Crop View (dù đầu mút đang nằm lệch xa ra ngoài như ở trích xuất căn hộ). Tự động kéo hai đầu mút về đúng biên Crop View.
   - Bảng thông số 4 hướng: **Phía Trên (Top), Phía Dưới (Bottom), Bên Trái (Left), Bên Phải (Right)**.
   - **Bật / Tắt Ký Hiệu Đầu Trục (Symbols / Bubbles 4 Hướng)**: Tùy chỉnh bật/tắt độc lập bong bóng tròn chứa tên trục ở 4 hướng: Phía Trên (Top), Phía Dưới (Bottom), Bên Trái (Left), Bên Phải (Right).
   - Hỗ trợ căn chỉnh 1-click toàn bộ lưới trục hoặc quét chọn trực quan trên màn hình.
2. **Căn chỉnh Cao độ (Level) theo Khung Crop View trên Mặt cắt, Mặt đứng & Sheet**:
   - Tự động điều chỉnh khoảng cách hai đầu mút Level thò ra ngoài khung Crop View theo mm.
   - **Bật / Tắt Ký Hiệu Đầu Tầng (Symbols / Bubbles Độc Lập)**: Cho phép chủ động bật hoặc tắt bong bóng cao độ độc lập ở Bên Trái (Left) và Bên Phải (Right) (mặc định Bên Phải Bật, Bên Trái Tắt theo chuẩn hồ sơ kỹ thuật).
   - Hỗ trợ căn chỉnh đồng loạt tất cả các View / Viewports trên Sheet (như H12, H14...). Giữ nguyên bảng để tinh chỉnh liên tục.
3. **Quản Lý, Gom Nhóm & Xuất Hồ Sơ Bản Vẽ (Sheet List Manager, Grouping & Exporter)**:
   - **✍ Sửa Trực Tiếp Cả Số Hiệu & Tên Bản Vẽ (Inline Edit)**: Người dùng có thể click trực tiếp hoặc bấm `F2` vào ô `Số Hiệu (Sheet Number)` và `Tên Bản Vẽ (Sheet Name)` để gõ lại nội dung như trong Excel. Các ô được sửa đổi tự động đổi màu nền xanh bạc hà nổi bật.
   - **Lưu 1-Click Vào Revit (Chống Xung Đột Trùng Số Hiệu)**: Nút màu đỏ nổi bật `✔ LƯU {N} BẢN VẼ VÀO REVIT` cho phép áp dụng tức thì cả Số Hiệu và Tên vừa sửa vào các ViewSheet trong Document với thuật toán đổi tên 2 giai đoạn (tránh lỗi xung đột trùng số hiệu của Revit).
   - **📁 Cây Thư Mục & Gom Nhóm Bản Vẽ (Folder Tree & Sheet Grouping)**: Tích hợp Cây Thư Mục (`TreeView`) trực quan bên trái, hiển thị thư mục gốc `📁 TẤT CẢ BẢN VẼ` và các thư mục nhánh theo nhóm do người dùng phân loại. Cho phép lọc nhanh bản vẽ theo thư mục, tích chọn hàng loạt theo nhánh cây, kéo thả (`Drag & Drop`) bản vẽ vào thư mục, đổi tên, giải tán nhóm hoặc bấm `⚡ Tự Động` để nhận diện phân nhóm theo bộ môn. Cột `Thư Mục / Nhóm ✎` hiển thị trực tiếp trên lưới hỗ trợ AutoComplete gợi ý nhóm khi gõ, phím tắt sao chép & dán (`Ctrl+C` / `Ctrl+V`), điền dải (`Ctrl+D` Fill Down), và nút `[⚡ Điền Ô Trống]` tự động gán tên nhóm cho tất cả các bản vẽ chưa phân nhóm mà không cần gõ lại.
   - **✏ Sửa & Thay Thế Số Hiệu Hàng Loạt Với Tiền Tố Giống Nhau (Batch Edit Prefix)**: Nút `[✏ ĐỔI SỐ HIỆU...]` hỗ trợ tự động nhận diện chuỗi tiền tố chung dài nhất (Longest Common Prefix), tìm kiếm & thay thế tiền tố, thêm tiền tố/hậu tố hoặc đánh số thứ tự liên tiếp (`01, 02...`), tích hợp bảng Live Preview xem trước thời gian thực và kiểm tra chống trùng lặp số hiệu an toàn trước khi lưu vào Revit.
   - **▲/▼ Sắp Xếp Thứ Tự Tự Do (Arbitrary Ordering)**: Nút `▲ Lên` và `▼ Xuống` cho phép người dùng di chuyển đổi chỗ thứ tự bản vẽ tùy biến theo ý muốn mà hoàn toàn không bị ép buộc theo thứ tự chữ cái ABC.
   - **📄 Xuất File PDF Native Revit 2024 (.pdf)**: Hỗ trợ gom tất cả bản vẽ vào 1 file PDF duy nhất (Combined PDF) hoặc xuất từng file PDF riêng lẻ theo đúng thứ tự tùy biến đã sắp xếp.
   - **📦 Xuất Gộp Cùng 1 File CAD (.dwg) Duy Nhất (Có Đầy Đủ Khung Tên)**: Nút `📦 GỘP 1 FILE CAD` trên giao diện cho phép gộp tất cả các bản vẽ vào CÙNG 1 FILE CAD DUY NHẤT. Giữ nguyên 100% Khung tên (Title Block) và Viewport trên từng Layout Tab riêng biệt theo chuẩn in ấn, đồng thời dàn trang toàn bộ bản vẽ thẳng hàng trong không gian Model (Model Space) 5 bản vẽ mỗi hàng bằng AutoCAD Core Engine (`accoreconsole.exe`) chạy ngầm.
   - **📐 Xuất File AutoCAD (.dwg)**: Xuất đồng loạt sang file DWG, hỗ trợ tự động gộp liên kết Xref (Merged Views).
   - **⚡ Xuất Excel (.xls, .csv) & Clipboard**: Xuất bảng danh mục có sẵn cột Nhóm / Tiêu Đề chuẩn đẹp.
   - **Tích chọn linh hoạt & Chọn dải bằng Shift (Shift-Select & Spacebar Toggle)**: Hỗ trợ tích chọn hoặc bỏ chọn từng ô riêng lẻ. Giữ phím `Shift` + click chuột để chọn hoặc bỏ chọn liên tiếp cả một dải nhiều bản vẽ theo mong muốn. Hỗ trợ phím Cách (`Space`) và Menu chuột phải để bật/tắt trạng thái chọn cho tất cả các dòng bôi đen.
4. **Thống Kê Khối Lượng Ống Nước & Phụ Kiện (MEP Quantity Takeoff & Visual Inspector)**:
   - **🎯 Bóc Khối Lượng Cục Bộ Tại Vị Trí Bất Kỳ (Quét Vùng Trên Bản Vẽ ➔ Hiện Ngay Cả Tên Ống & Phụ Kiện)**: Nút `[ 🎯 QUÉT VÙNG BÓC CỤC BỘ ]` cho phép người dùng kéo chuột quét 1 khung chữ nhật trên mặt bằng/3D, bảng khối lượng lập tức hiện đầy đủ và tính đúng số lượng tên ống, phụ kiện & van trong khu vực đó, phân loại màu sắc rõ ràng (Đường Ống màu xanh kèm mét dài, Phụ Kiện màu tím kèm số cái) giúp kỹ sư có cái nhìn tổng thể 100% trước khi đưa vào sheet. Tự động bật cả Phụ Kiện và Đường Ống mặc định để không sót đối tượng.
   - **📁 Cây Thư Mục Khối Lượng Bên Trái (Hierarchical Quantity Tree)**: Tích hợp bảng Cây Thư Mục (`TreeView`) trực quan bên trái form: Phân cấp tự động `📁 Tất cả ➔ 🏢 Tầng ➔ 📍 Khu vực / Căn hộ ➔ 💧 Hệ thống ➔ 🧩 Phụ kiện & Ống`. Click chọn nhánh nào, bảng DataGridView bên phải tự động lọc theo nhánh đó. Tự động sinh tên Schedule thông minh `<KLPK {Hệ Thống} {Khu Vực} {Tầng}>` (ví dụ: `<KLPK THOÁT NƯỚC MƯA TM-DL8 T5>`).
   - **💧 Gộp 3 Parameters Tên Phụ Kiện Phân Mảnh Thành 1 Cột Duy Nhất**: Tự động nhận diện và quét qua tất cả các parameter tên phụ kiện (kể cả khác chữ hoa/thường hay khác GUID trong file family), lấy giá trị không rỗng đầu tiên và gộp vào 1 cột duy nhất `Tên Phụ Kiện`. Ba chạc, Cút, Côn thu, Thông tắc... gom dòng theo đúng quy cách Size, tính tổng số lượng (`Count`) và chiều dài (`Length`).
   - **Bảo Vệ Dữ Liệu Gốc 100%**: Giữ nguyên toàn bộ 3 parameters ban đầu trong Family, hoàn toàn không làm hỏng hay thay đổi dữ liệu dự án.
   - **💡 Soi Khối Lượng Tương Tác 2 Chiều (Bi-directional Highlight / Visual Inspection)**: Quét chuột hoặc click chọn dòng trong bảng khối lượng, các đối tượng tương ứng trên mô hình Revit (Mặt bằng Tầng/View 3D) LẬP TỨC SÁNG XANH (Revit Selection Highlight). Hỗ trợ nút `[ 🔍 Zoom Lại Gần ]`, `[ 👁 Cách Ly View (Isolate) ]` và `[ 🔄 Bỏ Cách Ly ]` giúp kỹ sư nhận biết tức thì đã bóc đúng hay chưa.
   - **📦 Kéo Thả Bảng Khối Lượng Vào Sheet / Đặt Trực Tiếp (Drag & Drop & Pick Point)**:
     - Nút 1-click `[ 📦 TẠO ĐỂ KÉO THẢ VÀO SHEET ]`: Tạo ngay bảng Schedule (`<KL PHU KIEN ONG - HDTOOL>` hoặc `<KL PHU KIEN ONG - BOC CUC BO>`) vào Project Browser của Revit. Kỹ sư mở bất kỳ Sheet nào và nắm chuột kéo thả (Drag & Drop) trực tiếp vào vị trí mong muốn trên bản vẽ.
     - Hỗ trợ chọn điểm đặt trực tiếp bằng click chuột trên Sheet (`Interactive Pick Point`) hoặc đặt tự động vào Sheet danh sách.
     - Với chế độ Bóc Cục Bộ: Schedule tự động tích hợp bộ lọc Revit API (`HD_Phạm Vi Bóc = "Cục Bộ"` ẩn cột), đảm bảo khi kéo vào Sheet chỉ hiển thị đúng các đối tượng bóc cục bộ và Grand Total khớp 100%!
   - **⚡ Đồng Bộ Parameter Dự Án**: Nút `[ ⚡ ĐỒNG BỘ GỘP PARAMETER ]` tự động gán giá trị gộp vào tham số `HD_Tên Phụ Kiện` cho toàn bộ phụ kiện trong công trình.
   - **✏️ Sửa Trực Tiếp Parameter & Thêm Cột Dự Án Tùy Ý (MỚI)**:
     - Cho phép click/gõ sửa trực tiếp từng ô trên 2 cột `Location by Area` và `Location by Level` (ví dụ: gõ "MƯA", "GOM T3", "Tầng 13"... như Excel). Tự động cập nhật tức thì xuống các đối tượng Revit.
     - Nút `[ ➕ Thêm Cột Parameter ]`: Quét toàn bộ Shared / Project Parameter của các ống và phụ kiện trong dự án, cho phép chọn hoặc nhập tên bất kỳ để chèn thêm cột mới vào bảng, tự nạp dữ liệu và cho phép chỉnh sửa từng ô.
     - Chuột phải gán hàng loạt: Bôi đen nhiều dòng -> Chuột phải -> `✏️ Gán giá trị Parameter cho {N} dòng đã chọn...` gán đồng loạt cho nhiều cấu kiện.
     - Nút `[ ⚡ ĐỒNG BỘ PARAMETER ]`: Ghi đồng bộ tất cả giá trị bảng vào mô hình Revit trong 1 giao dịch.
   - **📏 Tách Rời Rạc Đoạn Ống Theo Chiều Dài (Không SUM) & Chuyển Đổi Linh Hoạt (MỚI THEO LỆNH SẾP HỒ)**:
     - Bóc được bao nhiêu đoạn có chiều dài khác nhau thì tách ra từng dòng riêng biệt, hiển thị đúng chiều dài thực tế từng đoạn, không bị SUM gộp mét dài.
     - Bộ chuyển đổi 2 chế độ `🔘 Tách Rời Rạc` và `⚪ Gom Mét Dài (SUM)` ngay trên Form cho phép chuyển đổi tức thì 1-click.
   - **Xuất Excel / CSV & Sao Chép**: Xuất ra file CSV UTF-8 hoặc sao chép nhanh vào Clipboard (`Ctrl + V`) để dán sang Excel làm bảng dự toán.
5. **Cập Nhật Danh Mục Bản Vẽ Từ Excel (Update Sheet List from Excel / CSV / Clipboard)**:
   - **Đồng bộ hai chiều**: Sau khi xuất danh mục ra Excel, người dùng sửa/đặt lại tên bản vẽ (Sheet Name) trong Excel, công cụ sẽ tự động đọc file và cập nhật tên các Sheet trong Revit tương ứng.
   - **Hỗ trợ đa định dạng nguồn**: Đọc file Excel SpreadsheetML (.xls), Excel Workbook OpenXML (.xlsx), CSV chuẩn UTF-8 (.csv), hoặc đọc trực tiếp từ Clipboard khi copy bảng trong Excel (`Ctrl + C`).
   - **Bảng xem trước & xác nhận trực quan (`UpdateSheetListPreviewForm`)**: Hiển thị bảng so sánh đối chiếu: `Số Hiệu`, `Tên Hiện Tại Trong Revit`, `Tên Mới Từ Excel`, `Trạng Thái (Đổi tên / Giữ nguyên / Không tìm thấy)`. Cho phép tích chọn từng bản vẽ trước khi áp dụng.
   - **An toàn Revit Transaction**: Tự động lọc bỏ các ký tự cấm trong tên view/sheet của Revit, cập nhật thông tin chuẩn xác và báo cáo tổng kết chi tiết.
5. **Cập Nhật Tool Trực Tiếp Khi Đang Mở Revit (In-Memory Hot Reload Engine)**:
   - **Nạp trực tiếp vào RAM (Zero File Lock)**: Đọc file DLL thành mảng byte (`File.ReadAllBytes`) rồi nạp qua `Assembly.Load(byte[])`, hoàn toàn không gây khóa file trên ổ cứng Windows.
   - **Bộ điều hướng động (Dynamic Dispatcher & Reflection)**: Khi các nút trên Ribbon hoặc Menu được bấm, `ToolHotReloadManager` tự động chuyển tiếp lệnh tới `ExecuteDirect` của Assembly mới nhất trong bộ nhớ AppDomain.
   - **Tự động tìm kiếm bản build mới nhất**: Tự động phát hiện file `Build\HDTOOL.dll` vừa biên dịch hoặc cho phép duyệt chọn bất kỳ file DLL nào với giao diện `UpdateToolForm` trực quan.
   - **Đồng bộ nền vĩnh viễn**: Tự động xoay vòng file DLL cũ và sao chép DLL mới vào thư mục `%APPDATA%\Autodesk\Revit\Addins\2024\HDTOOL\HDTOOL.dll` để duy trì bản cập nhật cho các phiên làm việc tiếp theo.
6. **Gom Nút Căn Chỉnh Grids & Level Thành 1 Nút Có Bảng Tùy Chọn**:
   - Gom hai nút Grids và Level riêng lẻ thành **1 nút bấm duy nhất**: `Căn Chỉnh Grids / Level` (`CropAlignCommand`).
   - Mở **Bảng Tùy Chọn (`CropAlignOptionForm`)** hiển thị thông tin view hiện hành, số lượng Grids và Levels, cung cấp 3 lựa chọn trực quan:
     1. 📐 Căn Chỉnh Lưới Trục (Grids)
     2. 📏 Căn Chỉnh Cao Độ (Level)
     3. ⚡ Tự Động Chọn Theo View Hiện Tại (Smart Align)
   - Tích hợp nút chuyển đổi nhanh 1-Click: `[ 📏 Chuyển Sang Căn Level ]` trong `GridCropForm` và `[ 📐 Chuyển Sang Căn Grids ]` trong `LevelCropForm`.
7. **Quyền Chủ Động Lựa Chọn của Người Dùng (Không ép buộc tự động)**:
   - Ribbon Tab **HDTOOL** gồm các nút gọn gàng: **Căn Chỉnh Grids / Level**, **Xuất Danh Mục Bản Vẽ**, **Cập Nhật Danh Mục**, **Cập Nhật Tool (Hot Reload)** và **Menu HDTOOL**.
   - Người dùng thoải mái mở đúng công cụ mình cần vào bất kỳ lúc nào.
8. **An toàn 2D View-Specific**:
   - Áp dụng `DatumExtentType.ViewSpecific`, chỉ thay đổi hiển thị trong View hiện hành, bảo vệ tuyệt đối mô hình 3D và các View khác.

---

## 2. Cấu trúc Thư mục và Triển khai

### Thư mục Đóng gói (Chỉ lưu trong thư mục dự án, không sinh ra Desktop):
```
HDTOOL/
├── HDTOOL_Setup_Version_6.exe   <-- Bộ cài đặt 1-File độc lập Version 6 (Mới nhất)
├── HDTOOL_Setup_Version_5.exe   <-- Bộ cài đặt Version 5 (Version liền kề)
├── Setup.exe                    <-- File cài đặt tiêu chuẩn mới nhất
├── VERSION.txt                  <-- File lưu số phiên bản hiện tại (Version 6)
├── TANG_VERSION.bat             <-- Script 1-click tự động nâng lên Version tiếp theo
├── CAI_DAT_ADDIN.bat            <-- Script cài đặt tự động vào %APPDATA%\Autodesk\Revit\Addins
├── GO_CAI_DAT.bat               <-- Script gỡ cài đặt sạch sẽ
├── Build.bat                    <-- Script biên dịch lại mã nguồn C# (Tự động tăng Version)
├── Build.ps1                    <-- Script PowerShell biên dịch & đóng gói Version
├── HUONG_DAN_SU_DUNG.md         <-- Hướng dẫn thao tác chi tiết bằng tiếng Việt
├── HDTOOL.addin                 <-- Manifest đăng ký Add-in với Autodesk Revit
├── HDTOOL_Version_6.zip         <-- File nén trọn gói chia sẻ cho đồng nghiệp (Chỉ lưu trong folder)
├── SKILL.md                     <-- Đặc tả kỹ thuật và thuật toán (File này)
│
├── Build/                       <-- Thư viện nhị phân đã biên dịch sẵn
│   ├── HDTOOL.dll               <-- DLL chứa toàn bộ lệnh và giao diện WinForms
│   └── HDTOOL.addin
│
├── Source/                      <-- Toàn bộ mã nguồn C# sạch sẽ
│   ├── App.cs                   <-- Đăng ký Ribbon Tab HDTOOL, Panel và các Button
│   ├── CropAlignCommand.cs      <-- Lệnh mở Bảng tùy chọn Căn Grids & Level
│   ├── CropAlignOptionForm.cs   <-- Giao diện Bảng tùy chọn căn chỉnh bản vẽ trung tâm
│   ├── HDToolMainCommand.cs     <-- Lệnh mở Menu trung tâm HDTOOL
│   ├── HDToolMainForm.cs        <-- Giao diện Menu trung tâm lựa chọn công cụ
│   ├── DuctQuantityCommand.cs   <-- Lệnh thống kê khối lượng ống gió HVAC TCVN 5687:2010
│   ├── MepQuantityCommand.cs    <-- Lệnh thống kê khối lượng ống nước & phụ kiện gộp
│   ├── MepQuantityInspectorForm.cs <-- Giao diện WinForms bóc tách & soi khối lượng
│   ├── MepQuantityService.cs    <-- Dịch vụ trích xuất & auto-fit tỷ lệ 1:2
│   ├── PickTableHighlightCommand.cs <-- Lệnh soi sáng từ bảng Sheet
│   ├── FitTableScaleCommand.cs  <-- Lệnh khớp tỷ lệ bảng khối lượng
│   ├── GridCropService.cs       <-- Thuật toán hình học căn chỉnh Grids 4 hướng
│   ├── GridCropForm.cs          <-- Giao diện căn chỉnh Grid 4 hướng & nút chuyển Level
│   ├── GridCropCommand.cs       <-- Lệnh Revit căn Grid mặt bằng & Sheet
│   ├── GridSelectionFilter.cs   <-- Bộ lọc chọn Grid, Crop Box hoặc Viewport
│   ├── LevelCropService.cs      <-- Thuật toán hình học căn chỉnh Level đa View/Sheet
│   ├── LevelCropForm.cs         <-- Giao diện căn chỉnh Level đa View
│   ├── LevelCropCommand.cs      <-- Lệnh Revit căn Level mặt cắt & Sheet
│   ├── SmartCropCommand.cs      <-- Lệnh nhận diện nhanh theo loại View
│   ├── ExportSheetListCommand.cs<-- Lệnh Revit mở bảng xuất danh mục bản vẽ
│   ├── ExportSheetListForm.cs   <-- Giao diện WinForms lọc và chọn Sheet để xuất
│   ├── BatchEditSheetNumberForm.cs <-- Hộp thoại sửa & thay thế số hiệu hàng loạt theo tiền tố cho toàn bộ dự án
│   ├── SetGroupTitleForm.cs     <-- Hộp thoại tạo và gán nhanh nhóm bản vẽ (Group/Folder)
│   ├── ExportSheetListService.cs<-- Bộ máy xuất Excel (.xls), CSV UTF-8 và Clipboard
│   ├── SheetItem.cs             <-- Mô hình dữ liệu bản vẽ (Sheet Number, Name, Rev...)
│   ├── UpdateSheetListCommand.cs<-- Lệnh Revit mở tính năng cập nhật danh mục từ Excel
│   ├── UpdateSheetListPreviewForm.cs <-- Giao diện xem trước & xác nhận đối chiếu cập nhật
│   ├── UpdateSheetListService.cs<-- Bộ máy đọc Excel/CSV/Clipboard & cập nhật ViewSheet
│   ├── SheetUpdateItem.cs       <-- Mô hình đối tượng so sánh thay đổi bản vẽ
│   ├── QuickPickCommand.cs      <-- Lệnh chọn nhanh trên màn hình
│   ├── QuickInputForm.cs        <-- Hộp thoại nhập mm nhanh
│   ├── LevelSelectionFilter.cs  <-- Bộ lọc chọn Level
│   └── ResourceHelper.cs        <-- Bộ sinh Icon đồ họa sắc nét cho Ribbon
│
└── Installer/                   <-- Mã nguồn và file thực thi của bộ cài đặt Setup.exe
    ├── Setup.cs
    └── Setup.exe
```

---

## 3. Thuật toán Hình học Vector Revit API

### A. Căn Chỉnh Lưới Trục Grids trên Mặt Bằng (Plan View)
1. **Hệ tọa độ View Plan**:
   - Chuyển đổi giữa 3D Model Coordinates và 2D View Coordinates thông qua `view.CropBox.Transform.Inverse`.
   - Trục $X$: Chiều ngang màn hình (Right Direction).
   - Trục $Y$: Chiều đứng màn hình (Up Direction).
2. **Xác định phương của Grid**:
   - Vector chỉ phương 3D: $\vec{D} = (P_1 - P_0) / \|P_1 - P_0\|$.
   - Vector trong hệ View: $\vec{D}_v = T_{inv} \cdot \vec{D}$.
   - Nếu $|D_{v.x}| \ge |D_{v.y}|$: **Lưới trục phương Ngang** (như Y2, Y3, Y4):
     - Giao cắt tia $y = y_{grid}$ với biên Crop View: $X_{min}, X_{max}$.
     - Điểm mục tiêu: $X_{target\_left} = X_{min} - \text{offsetLeft}$, $X_{target\_right} = X_{max} + \text{offsetRight}$.
     - Tham số chiếu $s$: $s = (X_{target} - P_{0.x}) / D_{v.x}$.
     - Tọa độ 3D mới: $P = P_0 + s \cdot \vec{D}$.
   - Nếu $|D_{v.y}| > |D_{v.x}|$: **Lưới trục phương Dọc** (như X8):
     - Giao cắt tia $x = x_{grid}$ với biên Crop View: $Y_{min}, Y_{max}$.
     - Điểm mục tiêu: $Y_{target\_bottom} = Y_{min} - \text{offsetBottom}$, $Y_{target\_top} = Y_{max} + \text{offsetTop}$.
     - Tham số chiếu $s$: $s = (Y_{target} - P_{0.y}) / D_{v.y}$.
     - Tọa độ 3D mới: $P = P_0 + s \cdot \vec{D}$.
3. **Cập nhật 2D Extents**:
   ```csharp
   grid.SetDatumExtentType(DatumEnds.End0, view, DatumExtentType.ViewSpecific);
   grid.SetDatumExtentType(DatumEnds.End1, view, DatumExtentType.ViewSpecific);
   Line newCurve = (s_start < s_end) ? Line.CreateBound(ptStart, ptEnd) : Line.CreateBound(ptEnd, ptStart);
   grid.SetCurveInView(DatumExtentType.ViewSpecific, view, newCurve);
   ```

### B. Căn Chỉnh Level trên Mặt Cắt / Mặt Đứng (Section / Elevation)
1. Chiếu điểm đầu/cuối của Level lên đường thẳng nằm ngang của Level:
   - Điểm trái mục tiêu: $P_{left} = C_{crop\_left} - \text{offsetLeftFeet} \cdot \vec{u}_{view\_right}$.
   - Điểm phải mục tiêu: $P_{right} = C_{crop\_right} + \text{offsetRightFeet} \cdot \vec{u}_{view\_right}$.
2. Cập nhật `level.SetDatumExtentType` sang `DatumExtentType.ViewSpecific` và gọi `level.SetCurveInView`.

### C. Xử Lý Trang In (Sheet Multi-View)
- Tìm tất cả `Viewport` đặt trên `ViewSheet`.
- Lấy `View` tương ứng qua `doc.GetElement(viewport.ViewId) as View`.
- Phân loại:
  - Nếu là Mặt bằng (`FloorPlan`, `CeilingPlan`): Căn Grids hàng loạt qua `GridCropService.AdjustMultiplePlanViews`.
  - Nếu là Mặt cắt/đứng (`Section`, `Elevation`): Căn Level hàng loạt qua `LevelCropService.AdjustLevelsMultiView`.

---

## 4. Hướng Dẫn Biên Dịch & Triển Khai Không Cần Visual Studio

Sử dụng trình biên dịch C# có sẵn của Windows:
`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`

```powershell
# 1. Biên dịch HDTOOL.dll
& "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" `
    /target:library `
    /out:"C:\Users\Nguye\Desktop\HDTOOL\Build\HDTOOL.dll" `
    /r:"C:\Program Files\Autodesk\Revit 2024\RevitAPI.dll" `
    /r:"C:\Program Files\Autodesk\Revit 2024\RevitAPIUI.dll" `
    /r:System.Drawing.dll `
    /r:System.Windows.Forms.dll `
    /r:System.Xaml.dll `
    /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationCore.dll" `
    /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll" `
    "C:\Users\Nguye\Desktop\HDTOOL\Source\*.cs"

# 2. Biên dịch Setup.exe
& "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" `
    /target:winexe `
    /out:"C:\Users\Nguye\Desktop\HDTOOL\Setup.exe" `
    /r:System.Drawing.dll `
    /r:System.Windows.Forms.dll `
    "C:\Users\Nguye\Desktop\HDTOOL\Installer\Setup.cs"

# 3. Triển khai vào Revit 2024
Copy-Item "C:\Users\Nguye\Desktop\HDTOOL\Build\HDTOOL.dll" "$env:APPDATA\Autodesk\Revit\Addins\2024\HDTOOL\HDTOOL.dll" -Force
Copy-Item "C:\Users\Nguye\Desktop\HDTOOL\HDTOOL.addin" "$env:APPDATA\Autodesk\Revit\Addins\2024\HDTOOL.addin" -Force
```

---

## 5. Quy Tắc & Lưu Ý Kỹ Thuật (Troubleshooting)
- **Tương thích C# 5**: Không dùng cú pháp C# 6+ như `$""` (dùng `string.Format`), không dùng C# 7 pattern matching `is Type var` (dùng `as Type` và kiểm tra null).
- **Tránh xung đột tên kiểu**: Revit API có `Autodesk.Revit.DB.Panel`, `Color`, `Point`. Trong WinForms, phải dùng alias hoặc chỉ định rõ `System.Windows.Forms.Panel`, `System.Drawing.Color`, `System.Drawing.Point`.
- **Cơ chế khóa file của Revit**: Khi Revit đang mở, DLL nạp vào tiến trình sẽ bị khóa. Để cập nhật không cần tắt Revit: Đổi tên file DLL cũ (ví dụ: `HDTOOL_old.dll`), sau đó copy đè file `HDTOOL.dll` mới.
- **An toàn Window Handle & CheckedListBox**: Tuyệt đối không gọi `BeginInvoke` trong sự kiện `ItemCheck` khi cửa sổ chưa tạo Handle (`IsHandleCreated == false` hoặc đang nạp danh sách `_isPopulating`). Tính toán số lượng check đồng bộ trực tiếp từ `e.NewValue` để cập nhật nút bấm.
- **Bảo đảm kéo dãn / thu ngắn Level**: Bọc `CropBoxActive` trong try-catch phòng View Template khóa. Khi `SetCurveInView` ở chế độ Model gặp lỗi do ràng buộc 3D, tự động kích hoạt fallback sang 2D ViewSpecific (`SetDatumExtentType`) để đảm bảo Level luôn được căn chỉnh chính xác 100%.
