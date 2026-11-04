# HƯỚNG DẪN SỬ DỤNG BỘ ADD-IN HDTOOL (REVIT)

---

## 1. GIỚI THIỆU TỔNG QUAN

**HDTOOL** là bộ công cụ chuyên nghiệp toàn diện dành cho Autodesk Revit, giúp kỹ sư và kiến trúc sư:
1. **Căn chỉnh Lưới trục (Grids)** trên Mặt bằng & Trang in (Sheet) theo khung **Crop View** theo 4 hướng **Top, Bottom, Left, Right (mm)** cân đối chuẩn xác.
2. **Căn chỉnh Cao độ (Level)** trên Mặt cắt, Mặt đứng & Trang in (Sheet) theo khung **Crop View** theo 2 đầu **Left, Right (mm)**.
3. **Xuất Danh Mục Bản Vẽ (Sheet List)**: Tìm kiếm, lọc và tích chọn các Sheet trong dự án để xuất bảng danh mục ra Excel (.xls), CSV UTF-8 hoặc sao chép nhanh vào Clipboard.
4. **Cập Nhật Danh Mục Từ Excel (Hai Chiều)**: Đặt lại tên bản vẽ trong Excel hoặc copy bảng Excel ➔ Bấm cập nhật là tên các Sheet trong Revit tự động đổi theo!
5. **Chủ động lựa chọn khi cần**: Người dùng hoàn toàn tự do lựa chọn công cụ muốn dùng trực tiếp từ Ribbon hoặc Menu trung tâm mà không bị ép buộc tự động mở ngoài ý muốn!

---

### PHẦN I: CĂN CHỈNH LƯỚI TRỤC (GRIDS) TRÊN MẶT BẰNG, MẶT ĐỨNG, MẶT CẮT & SHEET

Khi trình bày bản vẽ trên Mặt bằng (Floor Plan), Mặt đứng (Elevation), Mặt cắt (Section) hoặc Sheet chứa các Viewport này, khung viền màu trắng là **Crop View**. Các đường lưới trục ngang (như **Y2, Y3, Y4**) và lưới trục dọc (như **X8**) được căn đều, thò ra ngoài khung Crop một khoảng cách tiêu chuẩn:

```
                            [X8] (Top Offset)
                             |
                   +---------+---------+ (Khung Crop View Trên)
                   |                   |
   (Left Offset)   |                   |   (Right Offset)
   (Y4) ===========+===================+=========== (Y4)
   (Y3) ===========+===================+=========== (Y3)
   (Y2) ===========+===================+=========== (Y2)
                   |                   |
                   +---------+---------+ (Khung Crop View Dưới)
                             |
                            [X8] (Bottom Offset)
```

- **Giữ nguyên bảng điều khiển để tinh chỉnh liên tục (Live Tuning)**: Khi bấm nút *Tự động căn chỉnh* hoặc *Pick chọn*, tool lập tức cập nhật vị trí các lưới trục Grids trực tiếp trên màn hình Revit mà **vẫn giữ nguyên bảng điều khiển mở**, đồng thời **hoàn toàn không hiện bảng pop-up thông báo phiền phức**. Người dùng có thể quan sát bản vẽ và thoải mái thay đổi thông số (ví dụ từ 1500 sang 1200, 1000...) rồi bấm căn chỉnh tiếp cho đến khi hoàn toàn ưng ý, khi xong chỉ cần bấm nút *Đóng* hoặc phím `ESC` để tắt bảng.
- **Phía Trên (Top)**: Khoảng cách lưới trục dọc nhô lên trên mép Crop (mặc định: `1500 mm`).
- **Phía Dưới (Bottom)**: Khoảng cách lưới trục dọc nhô xuống dưới mép Crop (mặc định: `1500 mm`).
- **Bên Trái (Left)**: Khoảng cách lưới trục ngang nhô sang trái mép Crop (mặc định: `1500 mm`).
- **Bên Phải (Right)**: Khoảng cách lưới trục ngang nhô sang phải mép Crop (mặc định: `1500 mm`).
- **Khóa 4 hướng bằng nhau**: Chỉ cần nhập 1 ô, cả 4 hướng tự động bằng nhau.
- **Tùy chọn bật/tắt Symbol (Bong bóng trục) độc lập ở cả 4 hướng**: Top, Bottom, Left, Right.

---

### PHẦN II: CĂN CHỈNH CAO ĐỘ (LEVEL) TRÊN MẶT CẮT / MẶT ĐỨNG / SHEET

Khi trình bày mặt cắt hoặc trang in (Sheet) chứa nhiều khung nhìn (như **H12, H14**):

```
       [Đầu Trái] <---- Left Offset (mm) ----> [Khung Crop View] <---- Right Offset (mm) ----> [Đầu Phải + Bubble]
           |                                         |                                            |
   ========+=========================================+============================================( Tầng 17 - 52000 )
   ========+=========================================+============================================( Tầng 16 - 48850 )
```

- Hỗ trợ căn chỉnh **Đồng loạt tất cả các View trên cùng màn hình làm việc hoặc Sheet** chỉ với 1 cú click!

---

### PHẦN III: XUẤT DANH MỤC BẢN VẼ (SHEET LIST EXPORT)

Khi chuẩn bị phát hành hồ sơ, nghiệm thu hoặc bàn giao bản vẽ:
- **Cây Thư Mục Hồ Sơ Bản Vẽ & Gom Nhóm Linh Hoạt (Folder Tree & Sheet Grouping)**:
  - **Cây thư mục trực quan bên trái**: Hiển thị cấu trúc cây gồm thư mục gốc `📁 TẤT CẢ BẢN VẼ` và các thư mục nhóm do người dùng phân loại (ví dụ: `📁 BẢN VẼ COMBINE TẦNG 1`, `📁 BẢN VẼ ĐIỀU HÒA`, `📁 PCCC`...).
  - **Click chọn thư mục trên cây**: Bảng danh sách bên phải lập tức lọc và chỉ hiển thị đúng các bản vẽ nằm trong thư mục đó. Bấm `[🔄 Xem Tất Cả]` để hiển thị lại toàn bộ.
  - **Tích chọn nhanh theo thư mục**: Bật/tắt checkbox của một thư mục trên cây sẽ tự động chọn hoặc bỏ chọn toàn bộ bản vẽ trong nhóm đó ngay lập tức.
  - **Gom bản vẽ vào cây thư mục**:
    - Bôi đen các bản vẽ trong bảng $\rightarrow$ Bấm nút `📁 Gom Nhóm...` (hoặc nút `➕ Nhóm Mới` trên đầu cây thư mục) $\rightarrow$ Nhập tên nhóm mới hoặc chọn nhóm có sẵn.
    - Hỗ trợ kéo thả (`Drag & Drop`) trực tiếp các dòng bản vẽ từ bảng thả vào thư mục mong muốn trên cây!
  - **Sửa trực tiếp cột Thư Mục / Nhóm & AutoComplete gợi ý**: Người dùng có thể click đúp hoặc gõ trực tiếp tên nhóm ngay trên cột `Thư Mục / Nhóm ✎`. Hệ thống tự động gợi ý danh sách nhóm có sẵn (AutoComplete) khi gõ, không cần phải nhập lại từ đầu.
  - **Sao chép & Dán tên nhóm siêu tốc (Ctrl + C & Ctrl + V)**:
    - Bấm `Ctrl + C` trên ô có tên nhóm để sao chép.
    - Bôi đen chọn một hoặc nhiều dòng bản vẽ khác rồi bấm `Ctrl + V` (hoặc bấm nút `[📋 Dán Nhóm]`): Tên nhóm lập tức được dán đồng loạt vào tất cả các dòng đang chọn!
    - Chuẩn hóa thông minh: Dù copy cả dòng dữ liệu hay văn bản từ bên ngoài, HDTOOL tự lọc lấy đúng tên nhóm sạch sẽ, không bị dính chữ hay lỗi dữ liệu.
  - **Điền tên nhóm xuống các dòng dưới (Fill Down - Ctrl + D như Excel)**: Bôi đen dải dòng từ trên xuống dưới và bấm `Ctrl + D`, tên nhóm của dòng đầu tiên sẽ tự động điền xuống tất cả các dòng còn lại trong dải chọn!
  - **⚡ Điền tên nhóm vào tất cả ô chưa có nhóm (Điền ô trống)**: Bấm nút `[⚡ Điền Ô Trống]` hoặc nhấp chuột phải chọn *Điền tên nhóm này vào tất cả ô chưa có nhóm*: Tự động gán tên nhóm cho mọi bản vẽ còn trống trong bảng chỉ với 1 cú click!
  - **Chuột phải ➔ Gán nhanh tên nhóm**: Nhấp chuột phải vào bất kỳ bản vẽ nào, menu con `📁 Gán nhanh tên nhóm` sẽ liệt kê toàn bộ các nhóm đã có trong dự án để bạn chọn gán ngay lập tức.
  - **⚡ Tự Động Phân Nhóm Theo Bộ Môn**: Nút `⚡ Tự Động` giúp nhận diện nhanh tên và số hiệu để tự động gom bản vẽ vào các cây thư mục: *Kiến trúc, Kết cấu, Điều hòa, Điện, Cấp thoát nước, PCCC...* chỉ với 1 cú click!
- **✏ Sửa & Thay Thế Số Hiệu Hàng Loạt Với Tiền Tố Giống Nhau (Batch Edit Prefix)**:
  - **Nút `[✏ ĐỔI SỐ HIỆU...]` màu tím nổi bật**: Nằm ngay trên thanh công cụ lọc bản vẽ hoặc chuột phải chọn *Sửa / Thay thế số hiệu hàng loạt*.
  - **⭐ Tự động quét TẤT CẢ bản vẽ có cùng tiền tố trong dự án (Mặc định)**:
    + Khi bạn click vào bất kỳ bản vẽ nào (ví dụ `HB/CD1/B1/CH1-01`), hệ thống tự động bóc tách tiền tố chuẩn (`HB/CD1/B1/CH1-`) và quét toàn bộ dự án Revit để tìm **TẤT CẢ các bản vẽ có cùng tiền tố này** (ví dụ 14 bản vẽ).
    + Phạm vi mặc định được chọn là **`⭐ Cùng tiền tố ({N} bản vẽ)`**, toàn bộ các bản vẽ liên quan đều được nạp sẵn vào bảng xem trước để bạn sửa đồng loạt mà không bị sót bất kỳ bản vẽ nào!
    + Điền sẵn tiền tố vào ô *Thay thế bằng* để bạn chỉ việc sửa ký tự cần đổi (ví dụ đổi `CD1` thành `CD2` hoặc `CH1` thành `CH2`).
  - **3 chế độ chỉnh sửa mạnh mẽ**:
    1. *Thay thế Tiền Tố / Chuỗi*: Tìm tiền tố cũ $\rightarrow$ Nhập tiền tố mới $\rightarrow$ Tự động đổi hàng loạt số hiệu trên toàn bộ bản vẽ cùng tiền tố. Có tùy chọn chỉ đổi ở đầu chuỗi (tiền tố chuẩn).
    2. *Thêm Tiền tố / Hậu tố*: Thêm tiền tố vào trước (ví dụ `PCCC-`) hoặc hậu tố vào sau (ví dụ `-REV1`).
    3. *Đánh số thứ tự tự động theo tiền tố*: Nhập tiền tố cố định (ví dụ `HDM/CB/B1/T1.`) $\rightarrow$ Bắt đầu từ số $\rightarrow$ Định dạng `01, 02...` hoặc `001, 002...` $\rightarrow$ Tự động đánh số liên tiếp.
  - **Bảng xem trước trực quan thời gian thực (Live Preview)**:
    - Hiển thị so sánh: `[Số Hiệu Cũ] ➔ [Số Hiệu Mới]` với màu xanh lá nổi bật theo từng phím gõ.
    - Cảnh báo trùng lặp (Duplicate Warning): Tự động phát hiện nếu có số hiệu trùng lặp trong dự án để bảo vệ an toàn Revit.
  - **Áp dụng tức thì & Lưu vào Revit**:
    - Sau khi bấm *Áp dụng*, toàn bộ các bản vẽ vừa sửa sẽ được cập nhật, đánh dấu chọn và tô màu xanh lá nổi bật trên bảng chính.
    - Add-in tự động đặt lại bộ lọc để hiển thị đầy đủ mọi bản vẽ vừa sửa kể cả khi trước đó bạn đang đứng ở thư mục con.
    - Kích hoạt nút đỏ `✔ LƯU {N} BẢN VẼ VÀO REVIT` ở góc dưới để người dùng áp dụng vĩnh viễn vào dự án Revit chỉ với 1 click!
- **⚡ Phím tắt Thoát Nhanh (`ESC`)**: Khi cửa sổ làm việc hiện lên (Menu HDTOOL, Bảng Quản lý Bản vẽ, Hộp thoại Đổi số hiệu, Căn Grids/Level...), nếu bạn không muốn làm việc hoặc muốn đóng cửa sổ, chỉ cần nhấn phím **`ESC`** trên bàn phím là cửa sổ sẽ **tự động đóng ngay lập tức**, không cần phải dùng chuột bấm vào dấu `X` ở góc trên!
- **Tích chọn linh hoạt & Chọn dải bằng Shift**:
  - Tích chọn hoặc bỏ chọn từng bản vẽ riêng lẻ dễ dàng.
  - **Giữ phím `Shift` + Click chuột vào ô chọn**: Tự động chọn (hoặc bỏ chọn) đồng loạt cả một dải nhiều bản vẽ liên tiếp theo ý muốn.
  - **Phím Cách (`Space`)**: Bật/tắt tích chọn nhanh cho tất cả các dòng đang bôi đen.
  - **Chuột phải (Context Menu)**: Hỗ trợ tích chọn, bỏ chọn hoặc đảo chọn nhanh các dòng đang bôi đen.
  - **Click tiêu đề cột "Chọn"**: Bật/tắt chọn toàn bộ bản vẽ chỉ với 1 click.
- **Tìm kiếm & Lọc nhanh**: Tìm theo tên, số hiệu hoặc lọc theo bộ môn (Kiến trúc, Kết cấu, MEP...).
- **Chọn nhanh**: Hỗ trợ các nút **Chọn Tất Cả**, **Bỏ Chọn**, **Đảo Lựa Chọn**.
- **Sắp xếp tự nhiên (Natural Sort)**: Số hiệu bản vẽ được sắp thứ tự chuẩn xác (`A-01, A-02, ..., A-10`).
- **Xuất đa định dạng**:
  - `⚡ XUẤT RA FILE EXCEL (.XLS / .CSV)`: Lưu file Excel (.xls) định dạng bảng đẹp mắt, tự mở ngay sau khi lưu.
  - `📋 SAO CHÉP VÀO CLIPBOARD`: Sao chép bảng dữ liệu dạng Tab, dán trực tiếp (`Ctrl + V`) vào Excel, Word hoặc bảng Schedule trong Revit.

---

### PHẦN IV: CẬP NHẬT DANH MỤC BẢN VẼ TỪ EXCEL (HAI CHIỀU)

Khi bạn muốn đổi tên hàng loạt bản vẽ trong Revit thông qua Excel:
1. **Xuất file** bằng chức năng *Xuất Danh Mục Bản Vẽ* ra file Excel (.xls / .xlsx / .csv).
2. **Mở file trong Microsoft Excel** và chỉnh sửa cột `Tên Bản Vẽ` (hoặc `Phiên Bản`, `Ngày Phát Hành`, `Người Vẽ`) theo ý bạn rồi bấm Lưu (`Ctrl + S`).
3. **Quay lại Revit**:
   - Bấm nút **`Cập Nhật Danh Mục`** trên thanh Ribbon (hoặc nút **`🔄 CẬP NHẬT TỪ EXCEL`** trong bảng danh mục).
   - Chọn file Excel bạn vừa sửa.
   - Bảng **Xem Trước & Xác Nhận Đối Chiếu** mở ra, tô màu xanh rõ ràng các bản vẽ có thay đổi tên: `[A-101] Tên cũ ➔ Tên mới`.
   - Bấm **`🚀 CẬP NHẬT VÀO REVIT (ÁP DỤNG NGAY)`**: Các Sheet trong Revit tự động đổi tên đồng loạt ngay lập tức!
### PHẦN V: XUẤT GỘP TẤT CẢ BẢN VẼ RA CÙNG 1 FILE CAD DUY NHẤT (CÓ ĐẦY ĐỦ KHUNG TÊN)

Khi người dùng cần nộp hồ sơ hoặc chia sẻ bản vẽ CAD cho đối tác/khách hàng xem toàn bộ các tầng/bản vẽ trong **CÙNG 1 FILE CAD (.dwg) DUY NHẤT**:
1. **Nút `📦 GỘP 1 FILE CAD`**:
   - Nằm ngay trên thanh công cụ đáy của bảng *Quản Lý & Xuất Hồ Sơ Bản Vẽ*.
   - Cho phép xuất toàn bộ bản vẽ trong dự án (hoặc các bản vẽ bạn vừa chọn/sắp xếp).
2. **Khung Tên (Title Block) chuẩn 100% như trong Revit**:
   - Mỗi bản vẽ được xuất thành một **Layout Tab riêng biệt** mang đúng Số Hiệu và Tên bản vẽ (`[A-101] - MAT BANG TANG 1`), chứa đầy đủ Khung tên, logo công ty, bảng thông tin, ghi chú và khung nhìn Viewport chuẩn in ấn.
   - Toàn bộ các bản vẽ đồng thời được chèn vào **Không gian Model (Model Space)** sắp xếp thẳng hàng ngay ngắn (5 bản vẽ mỗi hàng) giúp kỹ sư dễ dàng quan sát, đo đạc hoặc chỉnh sửa tổng thể.
3. **Tự động hóa hoàn toàn với AutoCAD Core Engine (`accoreconsole.exe`)**:
   - HDTOOL tự động quét tìm bộ máy AutoCAD Core Engine trên máy tính (hỗ trợ mọi phiên bản AutoCAD 2028, 2027, 2026, 2025, 2024, 2023, 2022, 2021, 2020...).
   - Thực hiện gộp file ngầm siêu tốc (chỉ mất ~5-8 giây) mà không làm gián đoạn công việc hay chiếm màn hình.
   - Nếu máy tính chưa cài AutoCAD, HDTOOL sẽ xuất các file CAD riêng lẻ an toàn vào thư mục và hướng dẫn người dùng chi tiết.

---

### PHẦN VI: THỐNG KÊ KHỐI LƯỢNG ỐNG NƯỚC & PHỤ KIỆN (GỘP PARAMETERS, BÓC CỤC BỘ & SOI SÁNG ĐỐI TƯỢNG) - MỚI

Khi mô hình MEP có các Family phụ kiện được tạo từ nhiều nguồn hoặc thời điểm khác nhau, mang **3 tham số khác nhau** (như `Tên phụ kiện`, `Tên Phụ Kiện`, `Tên phụ kiện` khác GUID):
- Trong bảng thống kê mặc định của Revit, các tham số này bị **xé nhỏ thành 3 cột riêng biệt (Cột A, Cột B, Cột C)** với vô số ô trống, khiến phụ kiện Ba chạc nằm một cột, Cút/Côn/Thông tắc nằm cột khác, không thể gộp dòng hay tính Grand Total chuẩn.

**HDTOOL giải quyết triệt để 100% bài toán này**:
1. **Bóc Khối Lượng Cục Bộ Tại Vị Trí Bất Kỳ (Quét Xong Hiện Ngay Toàn Bộ Tên Ống & Phụ Kiện)**:
   - Khi bạn muốn bóc khối lượng cho một **khu vực hoặc vị trí bất kỳ** (ví dụ: bóc riêng 1 căn hộ, 1 nhà vệ sinh, 1 trục ống đứng, hoặc 1 phòng kỹ thuật):
     - Cách 1: Bấm nút rực rỡ **`[ 🎯 QUÉT VÙNG BÓC CỤC BỘ ]`** trên thanh công cụ ➔ Form tự động ẩn đi ➔ Dùng chuột kéo một khung chữ nhật bao quanh khu vực cần bóc trên bản vẽ Revit ➔ Form hiện lại và **toàn bộ TÊN ĐƯỜNG ỐNG, PHỤ KIỆN & VAN lập tức hiện ngay vào bảng với đầy đủ số lượng, mét dài, size, hệ thống...** Người dùng có cái nhìn tổng thể 100% trước khi đưa vào Sheet!
     - Cách 2: Quét chọn các đối tượng trên bản vẽ trước khi bấm mở tool ➔ HDTOOL tự động nhận diện và chuyển sang chế độ **🎯 Vùng Quét Cục Bộ** ngay từ đầu!
   - **Tự động chọn cả Phụ Kiện & Đường Ống theo mặc định**: Đảm bảo toàn bộ hệ thống ống nước trong vùng quét đều được thống kê đầy đủ, không bị bỏ sót.
   - **Cột Phân Loại màu sắc trực quan**: Phân biệt rõ rệt `Đường Ống` (màu xanh dương đậm kèm chiều dài mét), `Phụ Kiện Ống` (màu tím kèm số lượng cái) và `Phụ Kiện Van` (màu cam).
   - Bảng tổng kết và số lượng Grand Total chỉ tính cho các đối tượng trong vùng quét đó, không bị lẫn với các khu vực khác.
2. **Cây Thư Mục Khối Lượng Bên Trái (Hierarchical Quantity Tree & Schedule Generator)**:
   - **Cây thư mục trực quan bên trái form**:
     - Phân cấp cây đa tầng tự động: `📁 TẤT CẢ KHỐI LƯỢNG` ➔ `🏢 Tầng` (T5, T6...) ➔ `📍 Khu vực / Căn hộ` (TM-DL8...) ➔ `💧 Hệ thống` (Thoát Nước Mưa, Cấp Nước...) ➔ `🧩 Chi tiết từng phụ kiện / ống`.
     - Click chọn vào bất kỳ nhánh thư mục nào trên cây, bảng dữ liệu bên phải sẽ lập tức lọc và hiển thị đúng các đối tượng của nhánh đó.
   - **Tự Động Sinh Tên Schedule Thông Minh Chuẩn Hồ Sơ**:
     - Tên bảng Schedule được tự động đặt theo dữ liệu bóc tách: `<KLPK {Hệ Thống} {Khu Vực} {Tầng}>` (ví dụ: `<KLPK THOÁT NƯỚC MƯA TM-DL8 T5>`).
   - **Tạo Schedule Vào Cây Thư Mục Project Browser Bên Trái Của Revit (Để Kéo Thả Vào Sheet)**:
     - Nút **`[ 📦 TẠO ĐỂ KÉO THẢ VÀO SHEET ]`** nằm ngay dưới chân cây thư mục bên trái (và trên thanh công cụ dưới).
     - Bấm 1 click ➔ Bảng Schedule được tạo ngay lập tức vào **cây thư mục Project Browser của Revit (nhánh Schedules/Quantities)**.
     - Người dùng chỉ việc mở bất kỳ Sheet nào trong Revit, nắm chuột giữ bảng Schedule từ cây Project Browser bên trái và **KÉO THẢ (Drag & Drop)** trực tiếp vào Sheet!
     - Bảng Schedule được gắn bộ lọc tự động `HD_Phạm Vi Bóc = "Cục Bộ"` ẩn cột, đảm bảo khi kéo vào Sheet **CHỈ HIỂN THỊ ĐÚNG CÁC PHỤ KIỆN VÀ ĐOẠN ỐNG TRONG VÙNG QUÉT**, số lượng và Grand Total trùng khớp 100%!
3. **Gộp Tự Động Làm 1 Cột Duy Nhất (`Tên Phụ Kiện`)**:
   - Tự động nhận diện và quét qua tất cả các tham số tên phụ kiện của từng đối tượng (kể cả khác chữ hoa/thường hay khác GUID).
   - Lấy giá trị có dữ liệu đầu tiên và gộp toàn bộ vào **1 CỘT DUY NHẤT**: `Tên Phụ Kiện`.
   - Ba chạc, Cút, Côn thu, Thông tắc... đều nằm chung 1 cột thẳng hàng, không còn bất kỳ ô trống nào!
   - **Giữ nguyên 100% các parameters gốc** trong Family (không làm hỏng hay thay đổi dữ liệu ban đầu).
4. **Kiểm Tra Khối Lượng Trực Quan — Quét Bảng Đối Tượng Sáng Lên (Highlight 2 Chiều)**:
   - Khi bạn click vào một dòng hoặc quét chuột (kéo chọn nhiều dòng) trên bảng khối lượng:
     Các đối tượng tương ứng trên mô hình Revit (Mặt bằng Tầng, View 3D...) **LẬP TỨC SÁNG LÊN** (màu xanh Highlight của Revit)!
   - Giúp kỹ sư kiểm tra tức thì: Dòng *Cút uPVC 45 60ø-60ø (10 cái)* đang nằm ở đâu trên bản vẽ, đã bóc đúng căn hộ/vị trí chưa, có bị sót hay đếm nhầm không.
   - Nút **`[ 🔍 Zoom Lại Gần ]`**: Focus camera lại gần các phụ kiện đang sáng.
   - Nút **`[ 👁 Cách Ly View (Isolate) ]`**: Tạm thời ẩn các cấu kiện khác để soi riêng nhóm phụ kiện đang chọn.
   - Nút **`[ 🔄 Bỏ Cách Ly ]`**: Trả lại góc nhìn bình thường của khung nhìn.
5. **Đưa Bảng Khối Lượng Vào Sheet / Kéo Thả Trực Tiếp (Drag & Drop)**:
   - **Nút 1-Click: `[ 📦 TẠO ĐỂ KÉO THẢ VÀO SHEET ]`**:
     - Tạo ngay bảng Schedule chuẩn trong Project Browser (`<KLPK THOÁT NƯỚC MƯA TM-DL8 T5>`).
     - Người dùng chỉ việc mở bất kỳ Sheet nào và **nắm chuột kéo thả (Drag & Drop)** bảng Schedule từ cây Project Browser vào bản vẽ một cách tự do, nhanh chóng và trực quan nhất!
   - **Nút Nâng Cao: `[ 📄 ĐẶT VÀO SHEET / KÉO THẢ... ]`**:
     - Cung cấp 3 chế độ lựa chọn tùy thích:
       1. `📦 Tạo trong Project Browser để nắm chuột Kéo Thả tự do vào Sheet`.
       2. `🎯 Click chuột lên Sheet đang mở để chọn điểm đặt chính xác (Interactive Pick Point)`.
       3. `📄 Đặt tự động vào Sheet được chọn từ danh sách`.
   - **Đặc biệt với Bóc Cục Bộ**:
     - Schedule được tự động gán bộ lọc chuẩn Revit API (`HD_Phạm Vi Bóc = "Cục Bộ"` ẩn cột).
     - Do đó khi kéo thả vào Sheet, bảng Schedule của Revit **CHỈ HIỂN THỊ ĐÚNG CÁC ĐỐI TƯỢNG TRONG VÙNG BÓC CỤC BỘ**, tổng số lượng Grand Total trùng khớp 100% với form HDTOOL!
6. **Nút `[ ⚡ ĐỒNG BỘ GỘP PARAMETER ]`**:
   - Đồng bộ giá trị tên gộp vào tham số `HD_Tên Phụ Kiện` trong Revit (hỗ trợ đồng bộ riêng cho vùng bóc cục bộ hoặc toàn bộ dự án).
   - Tự động tạo và gán giá trị tên gộp vào tham số dự án `HD_Tên Phụ Kiện` cho toàn bộ phụ kiện trong công trình để bất kỳ Schedule nào của Revit cũng có thể dùng được.
7. **Xuất Excel / CSV & Sao Chép**:
   - Hỗ trợ xuất ra file CSV UTF-8 hoặc sao chép nhanh vào Clipboard (`Ctrl + V`) để dán sang Excel làm báo cáo dự toán.

---

## 2. CÁCH CÀI ĐẶT ADD-IN HDTOOL (GÓI CÀI ĐẶT 1-FILE THEO VERSION)

Bộ cài đặt HDTOOL được đóng gói thành **1 file `.exe` duy nhất**, độc lập hoàn toàn, không cần giải nén:
- **Tên file cài đặt**: **`HDTOOL_Setup_Version_6.exe`** (chuẩn 1 file duy nhất theo số Version).
- **Vị trí lưu trữ**: Tất cả file cài đặt và đóng gói **chỉ sinh ra bên trong thư mục dự án (`c:\Users\Nguye\Desktop\ADDIN REVIT`)**, tuyệt đối không sinh file ra Desktop làm rối màn hình máy tính của bạn.
- **Cơ chế lưu trữ 2 Version gần nhất**: Hệ thống tự động lưu giữ **đúng 2 version mới nhất** (hiện tại là `Version 5` và `Version 6`), toàn bộ các version cũ hơn (`v1`, `v2`, `v3`...) sẽ tự động được xóa sạch để giữ thư mục luôn tinh gọn.

### Cách dùng gửi cho Bạn bè / Đồng nghiệp:
1. Bạn chỉ cần gửi duy nhất 1 file **`HDTOOL_Setup_Version_6.exe`** (nằm ngay trong thư mục dự án `ADDIN REVIT`) qua Zalo, Drive, USB hoặc Email.
2. Người nhận nhấp đúp chuột vào file **`HDTOOL_Setup_Version_6.exe`**.
3. Cửa sổ cài đặt mang thương hiệu **HadoMEE (Version 6)** mở ra, tự động phát hiện các phiên bản Revit có trên máy (Revit 2026, 2025, 2024, 2023, 2022, 2021, 2020...).
4. Bấm nút màu đỏ: **`🚀 CÀI ĐẶT ADD-IN HDTOOL`**.
5. Màn hình báo "Cài đặt thành công" là xong ngay trong 1 giây! Không cần giải nén, không cần thư mục đính kèm!

> **Gỡ cài đặt**: Mở lại file cài đặt bấm nút **`🗑️ GỠ CÀI ĐẶT`** là gỡ sạch sẽ.
> **Tăng phiên bản khi cập nhật**: Chạy file **`TANG_VERSION.bat`** để tự động nâng phiên bản lên Version tiếp theo (Version 5, 6...).

---

## 3. HƯỚNG DẪN THAO TÁC TRONG REVIT

Sau khi cài đặt xong, khởi động Autodesk Revit:
Trên thanh menu Ribbon của Revit, bạn sẽ thấy Tab mới: **`HDTOOL`** > Nhóm **`Căn Chỉnh Bản Vẽ`**:

### NÚT 1: CĂN CHỈNH GRIDS / LEVEL (`Căn Chỉnh Grids / Level`) - [GOM CHUNG 1 NÚT]
- **Mục đích**: Tối ưu không gian thanh Ribbon, gom hai chức năng căn chỉnh Lưới trục (Grids) và Cao độ (Level) vào **1 nút bấm duy nhất** với **Bảng Tùy Chọn trung tâm**:
  1. `📐 1. CĂN CHỈNH LƯỚI TRỤC (GRIDS)`: Mở giao diện căn Grids theo 4 hướng Top, Bottom, Left, Right (mm) trên Mặt bằng, Mặt đứng, Mặt cắt & Trang in (Sheet).
  2. `📏 2. CĂN CHỈNH CAO ĐỘ (LEVEL)`: Mở giao diện căn Level nhô 2 đầu Left, Right (mm) trên Mặt cắt, Mặt đứng & Sheet.
  3. `⚡ 3. TỰ ĐỘNG CHỌN THEO VIEW HIỆN TẠI (SMART)`: Tự nhận diện view đang mở:
     - Nếu đang ở Mặt bằng hoặc Sheet ➔ Mở Căn Grids.
     - Nếu đang ở Mặt cắt hoặc Mặt đứng ➔ Mở Căn Level.
- **Nút chuyển đổi nhanh 1-Click (Quick Switch) giữa 2 bảng**:
  - Trong bảng Grids có thêm nút: **`[ 📏 CHUYỂN SANG CĂN LEVEL ]`**
  - Trong bảng Level có thêm nút: **`[ 📐 CHUYỂN SANG CĂN GRIDS ]`**
  - Bạn có thể chuyển đổi qua lại giữa 2 công cụ bất kỳ lúc nào mà không cần quay lại thanh công cụ Ribbon!
- **Cơ chế Tinh Chỉnh Trực Tiếp (Live Tuning)**:
  - Bảng điều khiển luôn giữ nguyên mở sau mỗi lần bấm căn chỉnh, Revit lập tức refresh làm mới hiển thị trực tiếp.
  - Bạn thoải mái đổi số mm cho đến khi thực sự ưng ý, sau đó chỉ cần bấm nút `Đóng` hoặc phím **`ESC`** để tắt.
  - Hoàn toàn chạy êm (Silent Execution), không hiện bất kỳ hộp thoại pop-up thông báo gây phiền.

---

### NÚT 2: THỐNG KÊ KHỐI LƯỢNG ỐNG NƯỚC & PHỤ KIỆN (`Thống Kê Ống Nước`) - [MỚI]
- **Mục đích**: Thống kê khối lượng phụ kiện & ống nước MEP, gộp 3 tham số tên phụ kiện phân mảnh làm 1 bảng duy nhất, quét sáng đối tượng kiểm tra trực quan và đưa bảng vào Sheet:
  1. `💧 Bảng Khối Lượng Gộp 1 Cột Tên Phụ Kiện`:
     - Tự động nhận diện phụ kiện dù mang tham số nào trong 3 parameters (`Tên phụ kiện`, `Tên Phụ Kiện`...) và đưa chung vào 1 cột duy nhất `Tên Phụ Kiện`.
     - Phụ kiện Ba chạc, Cút, Côn thu, Thông tắc đều gom dòng theo đúng quy cách Size, tính tổng số lượng (`Count`) và chiều dài mét (`Length`).
  2. `💡 Quét Chọn Dòng ➔ Đối Tượng Sáng Lên Trong Revit (Live Inspection)`:
     - Khi bôi đen hoặc click chọn các dòng trên bảng, toàn bộ các đối tượng phụ kiện/ống tương ứng trong mô hình Revit (Mặt bằng/3D) **LẬP TỨC SÁNG XANH**.
     - Giúp kỹ sư nhận biết ngay lập tức mình đã bóc đúng hay chưa, có phụ kiện nào bị sót hay đếm nhầm không.
     - Nút `[ 🔍 Zoom Lại Gần ]`: Focus camera đến các đối tượng đang sáng.
     - Nút `[ 👁 Cách Ly View (Isolate) ]`: Tạm thời ẩn các cấu kiện khác để soi nhóm đối tượng đang chọn.
     - Nút `[ 🔄 Bỏ Cách Ly ]`: Trả lại trạng thái bình thường của khung nhìn.
  3. `📦 Kéo Thả Bảng Khối Lượng Vào Sheet / Đặt Trực Tiếp`:
     - Nút `[ 📦 TẠO ĐỂ KÉO THẢ VÀO SHEET ]`: 1 click tạo ngay Schedule trong Project Browser để kỹ sư nắm kéo thả trực tiếp vào bất kỳ Sheet nào.
     - Nút `[ 📄 ĐẶT VÀO SHEET / KÉO THẢ... ]`: Chọn đặt bằng cách kéo thả, click chuột chọn điểm đặt hoặc chọn Sheet từ danh sách.
     - Schedule bóc cục bộ tự động lọc chỉ hiển thị các đối tượng trong vùng quét, Grand Total trùng khớp 100%!
  4. `⚡ Đồng Bộ Gộp Parameter Vào Revit`:
     - Bấm **`[ ⚡ ĐỒNG BỘ GỘP PARAMETER ]`** để gán giá trị gộp vào tham số `HD_Tên Phụ Kiện` cho toàn bộ phụ kiện trong dự án.
  5. `📊 Xuất CSV / Excel & Sao Chép`:
     - Bấm **`[ 📊 Xuất CSV / Excel ]`** hoặc **`[ 📋 Sao Chép ]`** để dán sang Excel làm bảng dự toán.
  6. `📐 Khớp Tỷ Lệ Bảng Khối Lượng Tự Động (Auto-Fit Scale)`:
     - **Vấn đề triệt tiêu**: Khi thay đổi tỷ lệ Viewport trên Sheet (ví dụ từ 1:1 sang 1:2, 1:5, 1:10...), chữ và ô kẻ trước đây bị lệch nhau khiến chữ tràn ra ngoài đường kẻ.
     - **Giải pháp HDTOOL**:
       - Toàn bộ đường kẻ ô và vị trí chữ được tính toán nhân động theo hệ số tỷ lệ `ScaleFactor`.
       - Tự động nhận diện khi kích hoạt Sheet hoặc View để căn khớp lại tức thì.
       - Nút **`Khớp Tỷ Lệ Bảng Khối Lượng`** trên Ribbon cho phép 1-click đồng bộ lại toàn bộ bảng trong dự án chuẩn 100%!
  7. `✏️ Sửa Trực Tiếp Parameter & Thêm Cột Dự Án Vào Bảng Khối Lượng (MỚI)`:
     - **Sửa trực tiếp từng ô (Direct Cell Editing)**: Click hoặc gõ sửa trực tiếp từng ô trên 2 cột `Location by Area` và `Location by Level` (ví dụ nhập "MƯA", "GOM T3", "Tầng 13", "T1"... như trong Excel). Giá trị sau khi gõ tự động ghi thẳng vào các đối tượng Revit tương ứng.
     - **Thêm cột Parameter dự án (`[➕ Thêm Cột Parameter]`)**: Nhấp nút này để thêm bất kỳ Project / Shared Parameter nào vào bảng. Hệ thống tự động quét danh sách tham số hiện có từ các ống & phụ kiện trong mô hình để chọn nhanh, hoặc cho phép gõ tên tham số tùy ý. Cột mới thêm có màu xanh nhạt nhận diện, hiển thị dữ liệu thực tế và cho phép chỉnh sửa từng ô.
     - **Gán Parameter hàng loạt (Batch Assign)**: Quét chọn nhiều dòng -> Chuột phải -> Chọn **`✏️ Gán giá trị Parameter cho {N} dòng đã chọn...`** để đổi giá trị cùng lúc cho hàng loạt phụ kiện/đoạn ống.
     - **Nút `[⚡ ĐỒNG BỘ PARAMETER]`**: Ghi nhận toàn bộ thay đổi tham số từ bảng vào mô hình Revit trong 1 lượt giao dịch an toàn và tối ưu.
  8. `📏 Tách Rời Rạc Đoạn Ống Theo Chiều Dài (Không SUM) & Chuyển Đổi Linh Hoạt (MỚI THEO LỆNH SẾP HỒ)`:
     - **Tách rời rạc theo chiều dài**: Bóc được bao nhiêu đoạn có chiều dài khác nhau thì tách ra từng dòng riêng biệt. Cột `Chiều Dài (m)` hiển thị chính xác chiều dài thực tế của từng đoạn ống đơn lẻ, hoàn toàn không bị SUM cộng dồn mét dài vào 1 dòng.
     - **Bộ chuyển đổi linh hoạt trên thanh công cụ**:
       - `🔘 Tách Rời Rạc (Không SUM)`: Bật mặc định, bóc tách chi tiết từng đoạn ống, click vào dòng nào thì đúng đoạn ống đó sáng xanh rực rỡ trên bản vẽ Revit.
       - `⚪ Gom Mét Dài (SUM)`: Hỗ trợ gom tổng mét dài khi cần lập dự toán vật tư mua sắm toàn dự án.
       - Cho phép chuyển đổi tức thì giữa 2 chế độ chỉ bằng 1 click chuột mà không cần quét lại!

---

### NÚT 3: SỬA TRỰC TIẾP & XUẤT DANH MỤC BẢN VẼ (`Xuất Danh Mục Bản Vẽ`)
- Dành cho: Quản lý danh mục bản vẽ gọn gàng chuẩn 7 cột, sắp xếp thứ tự tự do (▲/▼), sửa trực tiếp Số Hiệu & Tên bản vẽ ngay trên bảng, xuất hồ sơ ra file PDF, CAD (.dwg) hoặc Excel/CSV.
- Bảng điều khiển mở ra:
  - **📊 BẢNG DANH MỤC GỌN GÀNG CHUẨN 7 CỘT KỸ THUẬT**:
    - Bảng gồm 7 cột chuẩn: `STT | SỐ HIỆU BẢN VẼ | TÊN BẢN VẼ | PHIÊN BẢN | NGÀY PHÁT HÀNH | NGƯỜI VẼ | TỶ LỆ`.
    - Đã bỏ hoàn toàn cột Bộ Môn và chức năng gán đầu mục theo yêu cầu, giao diện thanh thoát và trực quan tối đa.
  - **▲/▼ SẮP XẾP THỨ TỰ TỰ DO CHO BẢN VẼ**:
    - Chọn bất kỳ bản vẽ nào rồi bấm **`▲ Lên`** hoặc **`▼ Xuống`** để sắp xếp thứ tự các bản vẽ theo ý muốn mà không bị ép buộc theo thứ tự chữ cái ABC.
    - Cột **`STT`** (1, 2, 3...) phản ánh chính xác thứ tự thực tế khi bạn xuất hồ sơ.
  - **✍ SỬA TRỰC TIẾP SỐ HIỆU & TÊN BẢN VẼ (INLINE EDIT)**:
    - Nhấp chuột trực tiếp (hoặc bấm `F2`) vào ô **`Số Hiệu (Sheet Number)`** hoặc **`Tên Bản Vẽ (Sheet Name)`** để gõ lại nội dung mới ngay lập tức (giống hệt làm việc trong Excel).
    - Các ô được sửa đổi sẽ đổi nền xanh bạc hà dịu mát với chữ in đậm nổi bật.
    - Nút màu đỏ **`✔ LƯU {N} BẢN VẼ VÀO REVIT`** sẽ sáng lên. Bấm nút này để cập nhật đồng loạt cả Số Hiệu và Tên vào cây Project Browser của Revit (có cơ chế chống xung đột trùng số hiệu tự động).
  - **📄 XUẤT RA FILE PDF (.PDF)**:
    - Bấm nút **`📄 XUẤT RA PDF`** để xuất các bản vẽ đã chọn.
    - Lựa chọn: **Gom tất cả vào 1 file PDF duy nhất (Combined PDF)** hoặc **Xuất từng file PDF riêng lẻ** cho từng bản vẽ theo đúng thứ tự bạn đã xếp!
  - **📐 XUẤT RA FILE AUTOCAD (.DWG)**:
    - Bấm nút **`📐 XUẤT CAD (.DWG)`** để xuất đồng loạt các bản vẽ ra định dạng AutoCAD (.dwg) vào thư mục chỉ định, hỗ trợ tự động gộp Xref (Merged Views).
  - **⚡ XUẤT EXCEL, CSV & ĐỒNG BỘ DỮ LIỆU**:
    - Bấm **`⚡ XUẤT EXCEL`** để xuất file Excel XML Spreadsheet hoặc CSV chuẩn 7 cột.
    - Nút **`SAO CHÉP`**, **`NHẬP TỪ EXCEL`** và **`DÁN CLIPBOARD`** hỗ trợ đồng bộ dữ liệu tức thì.

---

---

### NÚT 4: CẬP NHẬT DANH MỤC (`Cập Nhật Danh Mục`)
- Dành cho: Cập nhật lại tên bản vẽ (Sheet Name) và thông tin bản vẽ trong Revit sau khi bạn đã sửa trong Excel.
- Thao tác:
  1. Bấm nút **`Cập Nhật Danh Mục`** trên Ribbon (hoặc bấm nút **`🔄 CẬP NHẬT TỪ EXCEL`** trong bảng danh mục bản vẽ).
  2. Chọn file Excel (.xls, .xlsx, .csv) đã sửa.
  3. Bảng **Xem Trước & Xác Nhận Đối Chiếu** hiển thị:
     - So sánh `Tên Hiện Tại (Trong Revit)` với `Tên Mới (Từ Excel)`.
     - Đánh dấu màu xanh lá các bản vẽ có thay đổi tên.
     - Tích chọn các bản vẽ bạn muốn cập nhật.
  4. Bấm **`🚀 CẬP NHẬT VÀO REVIT (ÁP DỤNG NGAY)`** -> Tên các Sheet trong Revit lập tức được đổi mới!
  - **Cập nhật nhanh từ Clipboard**: Quét chọn các dòng trong Excel, nhấn `Ctrl + C`, rồi trong bảng HDTOOL bấm nút **`📋 TỪ CLIPBOARD`** để cập nhật ngay lập tức!

---

### NÚT 5: CẬP NHẬT TOOL / HOT RELOAD (`Cập Nhật Tool`) - MỚI
- **Mục đích**: Cập nhật tính năng mới hoặc nạp lại bản DLL vừa biên dịch/cập nhật trực tiếp khi **ĐANG MỞ DỰ ÁN REVIT** mà **KHÔNG CẦN TẮT REVIT HAY MỞ LẠI FILE**.
- **Cách sử dụng**:
  1. Khi có tính năng mới hoặc vừa biên dịch xong file `HDTOOL.dll`, bạn bấm nút **`Cập Nhật Tool`** trên thanh công cụ HDTOOL (hoặc trong `Menu HDTOOL`).
  2. Bảng điều khiển mở ra:
     - **Tự động nhận diện bản build mới nhất**: Phần mềm tự động quét và hiển thị file DLL mới nhất vừa được biên dịch (ngày giờ, dung lượng).
     - **Hoặc chọn file tùy ý**: Bấm nút **`📁 Duyệt File...`** để chọn file `HDTOOL.dll` bất kỳ trên máy tính nếu bạn lưu ở nơi khác.
  3. Bấm **`🚀 NẠP & CẬP NHẬT NGAY VÀO REVIT`** (hoặc nhấn phím `Enter`):
     - File DLL mới lập tức được nạp trực tiếp vào phiên làm việc Revit hiện tại.
     - Toàn bộ các nút bấm trên thanh công cụ (Căn Grids, Căn Level, Quản lý Sheet...) sẽ nhận ngay các tính năng và logic mới mà không cần khởi động lại Revit!

---

### NÚT 6: MENU HDTOOL (`Menu HDTOOL`)
- Bảng điều khiển trung tâm cho phép người dùng **tự do lựa chọn**:
  1. `📐 1. CĂN CHỈNH LƯỚI TRỤC (GRIDS)`
  2. `📏 2. CĂN CHỈNH CAO ĐỘ (LEVEL)`
  3. `📋 3. XUẤT DANH MỤC BẢN VẼ (SHEET LIST)`
  4. `🔄 4. CẬP NHẬT DANH MỤC TỪ EXCEL (UPDATE SHEET LIST)`
  5. `🚀 5. CẬP NHẬT TÍNH NĂNG MỚI (HOT RELOAD TOOL)`
  6. `⚡ 6. TỰ ĐỘNG CHỌN THEO VIEW HIỆN TẠI (Tùy chọn nhanh)`
- Bạn thoải mái chọn chức năng mong muốn, hoàn toàn không bị ép tự động mở ngoài ý muốn!

---

### NÚT 7: TỰ ĐỘNG CĂN (`Tự Động Căn (Smart)`)
- Tùy chọn mở nhanh nếu không muốn chọn thủ công:
  - Nếu đang ở **Mặt bằng** hoặc **Trang in (Sheet)** ➔ Mở công cụ **Căn Grids**.
  - Nếu đang ở **Mặt cắt / Mặt đứng** ➔ Mở công cụ **Căn Level**.

---

## 4. CẤU TRÚC THƯ MỤC DỰ ÁN

```
HDTOOL/
├── HDTool_PreviewStudio.html    <-- Trình Preview Studio trực quan xem trước & kéo thả sắp xếp Ribbon
├── OpenPreviewStudio.bat        <-- Phím tắt 1-click mở Preview Studio trên trình duyệt
├── HDTOOL_Setup_Version_6.exe   <-- Gói cài đặt 1-File độc lập Version 6 (Mới nhất)
├── HDTOOL_Setup_Version_5.exe   <-- Gói cài đặt Version 5 (Version liền kề)
├── Setup.exe                    <-- File cài đặt tiêu chuẩn mới nhất
├── VERSION.txt                  <-- File lưu số phiên bản hiện tại (hiện tại là 6)
├── TANG_VERSION.bat             <-- Script 1-click tự động tăng lên Version tiếp theo
├── CAI_DAT_ADDIN.bat            <-- File cài đặt 1-click tự động
├── GO_CAI_DAT.bat               <-- File gỡ cài đặt sạch sẽ
├── Build.bat                    <-- File biên dịch lại mã nguồn C# (Tự động tăng Version)
├── Build.ps1                    <-- Script PowerShell biên dịch & đóng gói Version
├── HUONG_DAN_SU_DUNG.md         <-- Hướng dẫn chi tiết này
├── HDTOOL.addin                 <-- File manifest cấu hình cho Revit
├── HDTOOL_Version_6.zip         <-- File nén đóng gói chia sẻ đồng nghiệp (chỉ lưu trong folder)
├── SKILL.md                     <-- Tài liệu đặc tả kỹ thuật Skill AI
│
├── Build/                       <-- Thư mục chứa file đã biên dịch sẵn
│   ├── HDTOOL.dll               <-- File thư viện Add-in hoàn chỉnh
│   └── HDTOOL.addin
│
├── Source/                      <-- Toàn bộ mã nguồn C# đầy đủ
│   ├── App.cs                   <-- Đăng ký Ribbon Tab HDTOOL, Panel và các Button
│   ├── CropAlignCommand.cs      <-- Lệnh mở Bảng tùy chọn Căn Grids & Level
│   ├── CropAlignOptionForm.cs   <-- Giao diện Bảng tùy chọn căn chỉnh bản vẽ trung tâm
│   ├── HDToolMainCommand.cs     <-- Lệnh mở Menu trung tâm HDTOOL
│   ├── HDToolMainForm.cs        <-- Giao diện Menu trung tâm lựa chọn công cụ
│   ├── ExportSheetListCommand.cs<-- Lệnh Revit mở bảng xuất danh mục bản vẽ
│   ├── ExportSheetListForm.cs   <-- Giao diện WinForms lọc và chọn Sheet để xuất
│   ├── ExportSheetListService.cs<-- Bộ máy xuất Excel (.xls), CSV UTF-8 và Clipboard
│   ├── SheetItem.cs             <-- Mô hình dữ liệu bản vẽ (Sheet Number, Name, Rev...)
│   ├── UpdateSheetListCommand.cs<-- Lệnh Revit mở tính năng cập nhật danh mục từ Excel
│   ├── UpdateSheetListPreviewForm.cs <-- Giao diện xem trước & xác nhận đối chiếu cập nhật
│   ├── UpdateSheetListService.cs<-- Bộ máy đọc Excel/CSV/Clipboard & cập nhật ViewSheet
│   ├── SheetUpdateItem.cs       <-- Mô hình đối tượng so sánh thay đổi bản vẽ
│   ├── GridCropService.cs       <-- Thuật toán tính toán Grid theo khung Crop
│   ├── GridCropForm.cs          <-- Giao diện căn chỉnh Grid 4 hướng & nút chuyển Level
│   ├── GridCropCommand.cs       <-- Lệnh căn Grid mặt bằng & Sheet
│   ├── GridSelectionFilter.cs   <-- Bộ lọc chọn Grid và Crop View
│   ├── LevelCropService.cs      <-- Thuật toán tính toán Level theo Crop
│   ├── LevelCropForm.cs         <-- Giao diện căn chỉnh Level đa View & nút chuyển Grid
│   ├── LevelCropCommand.cs      <-- Lệnh căn Level mặt cắt & Sheet
│   ├── SmartCropCommand.cs      <-- Lệnh thông minh nhận diện nhanh
│   ├── QuickPickCommand.cs      <-- Lệnh chọn nhanh trên màn hình
│   ├── QuickInputForm.cs        <-- Hộp thoại nhập mm nhanh
│   ├── LevelSelectionFilter.cs  <-- Bộ lọc chọn Level
│   └── ResourceHelper.cs        <-- Bộ sinh Icon đồ họa sắc nét cho Ribbon
│
└── Installer/                   <-- Mã nguồn chương trình Setup.exe
    ├── Setup.cs
    ├── Setup.exe
    ├── app.ico
    └── hadomee_logo.png
```
