# HƯỚNG DẪN SỬ DỤNG BỘ ADD-IN HDTOOL (REVIT)

---

## 1. GIỚI THIỆU TỔNG QUAN

**HDTOOL (Version 84)** là bộ công cụ chuyên nghiệp toàn diện dành riêng cho Autodesk Revit (hỗ trợ đa phiên bản: Revit 2024, 2025, 2026, 2027...), phục vụ đắc lực cho Kỹ sư MEP và Kiến trúc sư:

> [!NOTE]
> **ĐẶC QUYỀN TRẢI NGHIỆM TỰ DO TRÊN BẢN VẼ & ẤN PHÍM `ESC` THOÁT LỆNH TỨC THÌ (VERSION 84)**:
> Toàn bộ các công cụ của HDTOOL (Dựng Ống Gió Từ Hình Vẽ CAD, Tự Động Crop View, Căn Chỉnh Grids/Level, Thống Kê Ống Nước, Khối Lượng Ống Gió, Xuất CAD Quy Đổi Layer MEP, Xuất/Cập Nhật Danh Mục Bản Vẽ, Menu HDTOOL, Cập Nhật Tool) đều hoạt động ở chế độ **Modeless Dialog 60 FPS** — khi mở bất kỳ cửa sổ nào, kỹ sư **VẪN THAO TÁC HOÀN TOÀN TỰ DO TRÊN BẢN VẼ REVIT** (Pan, Zoom, xoay 3D Isometric, chọn đối tượng, chuyển Tab View/Sheet). Khi không dùng nữa, **chỉ cần ấn phím `ESC` là thoát/đóng ngay công cụ** mà không cần di chuột bấm `Đóng`.

1. **Dựng Mô Hình Hệ Thống Ống Gió 3D Từ HÌNH VẼ CAD Link [CHUẨN KỸ SƯ BIM]**:
   - **Nhận diện mũi tên `QLEADER` / `MULTILEADER` & Đọc đúng `W` trước `H` sau**: Tự động trích xuất cả mũi tên chỉ dẫn (`LEADER` / `MULTILEADER`) gắn với Text `WxH` ở xa ngoài ống (ví dụ `OAD 2200x550: EI60`), tự sửa hướng trục cho đoạn ống cổ ngắn (`Length < Width`, như `647x2200` hay `911x2000`) và ưu tiên gán chuẩn `W` trước `H` sau ngay tại đầu mũi tên chỉ tới.
   - **Tách riêng hoàn toàn các tuyến giao chéo (Crossing Duct Isolation)**: Tự động phân tách các tuyến ống đi ngang qua nhau ở khác cao độ / khác hệ thống / khác màu CAD (ví dụ tuyến dọc `OAD 1700x400` màu Cyan cắt ngang qua tuyến ngang `EAD 1200x300` màu Đỏ), **không gộp chung khi Pick tuyến, không nối nhầm Tap/Cross và không bắt chéo Text WxH**; tự động nối cầu 2 nửa ống đồng trục nếu nét CAD bị cắt ở chỗ đi ngang qua ống khác.
   - **Phân rã Quần ống gió / Chạc Y / Ngã ba thành Ống thẳng + Côn đồng tâm + Chân rẽ đúng chiều CAD & Lọc mũi tên rỗng**: Tại mọi vị trí ngã 3, chạc chia chữ Y / quần ống gió hoặc giật cấp trên CAD (ví dụ `OAD 600x300` + `1000x300` + `OAD 400x250`), Add-in gộp liền mạch ống chủ, nối chuyển size bằng **Côn thu đồng tâm**, đâm nhánh bằng **Chân rẽ `TakeoffTap` tự xoay khớp đúng chiều vát 45° bản vẽ CAD**, ưu tiên sử dụng **Family phụ kiện có sẵn trong Revit**, và bỏ qua hoàn toàn mũi tên chỉ hướng gió không kèm text để bảo toàn 100% ống thật.
   - **Nhận diện đúng 100% Ống Gió & Loại bỏ triệt để đối tượng giả ống**: Tự động nhận diện Hình chữ nhật (Ống gió thẳng), **Cút cong bán kính lớn** (`Arc` đồng tâm & `PolyLine` đến 250 đỉnh), Côn thu đồng tâm, Chân rẽ Tap; đồng thời **tự động loại bỏ không cần chọn Layer**: hộp miệng gió gạch chéo X/V, miệng gió đầu nhánh, khung nét đứt `IAQ`, và thiết bị phòng máy (`ATT`, `MFD`/`OBD`/`NRD`, quạt, Hatch `ANSI37`).
   - **Trích xuất Text `.dwg` chuẩn xác, Đúng thứ tự `W` trước `H` sau & Không mặc định Cao Độ Tim**: Bấm **`🎯 PICK CHỌN HÌNH VẼ TUYẾN ỐNG`** $\to$ Tool đọc Text `WxH` chuẩn **`W` trước – `H` sau** (`EAD 350x300` $\Rightarrow W=350, H=300$), khớp ống **bọc chống cháy EI60**, hiển thị chẩn đoán trong **Live Preview DataGridView**; kỹ sư tự nhập **Cao Độ Tim Hệ Thống (mm)** rồi bấm **`🚀 DỰNG MÔ HÌNH REVIT 3D`**.
2. **Xuất Bản Vẽ Sang AutoCAD & Quy Đổi Layer Hệ Thống MEP [MỚI VERSION 84 - THEO LỆNH SẾP HỒ]**:
   - **3 Chế độ nguồn xuất linh hoạt**: Xuất trực tiếp **`View Đang Vẽ (Active View)`** (mặt bằng, 3D, mặt cắt đang mở) mà không cần tạo Sheet; xuất danh sách **`Sheet Bản Vẽ`**; hoặc xuất hàng loạt **`View Dự Án`**.
   - **Ma trận quy đổi Layer MEP (Ducts, Pipes, Equipment)**: Tự động quét toàn bộ `MechanicalSystemType` & `PipingSystemType`, cho phép gán Target CAD Layer, mã màu AutoCAD ACI (1–255), Linetype, Lineweight.
   - **3 Bộ Preset chuẩn**: TCVN MEP (Việt Nam), AIA / NCS (Quốc tế), Preset Tùy Chỉnh (Custom) và Lưu/Nạp cấu hình JSON.
   - **Tự động hóa xuất & Hậu kỳ**: Kết hợp `DWGExportOptions` + `ExportLayerTable` (`ModifierType.SystemType`) và kịch bản `accoreconsole.exe` headless chuẩn hóa Layer và màu sắc.
3. **Tự Động Tạo & Căn Chỉnh Crop View (Auto Crop Box) [CƠ CHẾ HIỂU Ý NGƯỜI DÙNG]**: Tự động tính toán Bounding Box 3D bao quanh toàn bộ các đối tượng mô hình có trong View vừa tạo (Sơ đồ không gian 3D, Mặt bằng, Mặt cắt, Viewport trên Sheet), chỉ giữ đối tượng trong phạm vi Crop hiện tại, không dãn to ra trục khác.
4. **Căn chỉnh Lưới trục (Grids)** trên Mặt bằng, Mặt đứng, Mặt cắt & Trang in (Sheet) theo khung **Crop View** theo 4 hướng **Top, Bottom, Left, Right (mm)** cân đối chuẩn xác. Tự động kiểm tra Unhide và kéo các trục bị ẩn / kéo lệch về sát mép bản vẽ.
5. **Căn chỉnh Cao độ (Level)** trên Mặt cắt, Mặt đứng & Trang in (Sheet) theo khung **Crop View** theo 2 đầu **Left, Right (mm)**.
6. **Thống Kê Khối Lượng Ống Gió & Phụ Kiện HVAC (TCVN 5687:2010 / SMACNA)**: Tự động tính diện tích tôn m², nhận diện Cút góc/R, Côn thu đọc chuẩn Parameter Chiều dài $L$ ($200\text{ mm}$), Lượn Z có độ chênh cao $H$, sắp xếp hết toàn bộ ống thẳng từ to đến nhỏ rồi mới đến phụ kiện, giao diện **Modeless Dialog 60 FPS**, **Live Tracking** và **Tự Động Cập Nhật thời gian thực** khi sửa thông số trên Revit.
7. **Thống Kê Khối Lượng Ống Nước & Phụ Kiện MEP**: Bóc cục bộ theo vùng quét, gộp 3 tham số tên phụ kiện phân mảnh làm 1 cột duy nhất, soi sáng 2 chiều (Highlight), kéo thả Schedule vào Sheet.
8. **Quản Lý & Xuất Danh Mục Bản Vẽ (Sheet List)**: Sửa trực tiếp Số Hiệu & Tên bản vẽ (Inline Edit), đổi số hiệu hàng loạt có kéo chuột tăng đuôi giống Excel, xuất PDF gộp, xuất CAD DWG gộp có đầy đủ Khung tên (TitleBlock), xuất Excel và cập nhật 2 chiều từ Excel.
9. **Cập Nhật Tool Nóng (In-Memory Hot Reload)**: Cập nhật DLL và tính năng mới trực tiếp ngay trong khi đang mở Revit mà không cần tắt phần mềm.

---

## PHẦN I: TỰ ĐỘNG TẠO & CĂN CHỈNH CROP VIEW (AUTO CROP BOX) — [MỚI VERSION 62]

Khi người dùng vừa tạo một khung nhìn mới trong Revit (đặc biệt là **View 3D Isometric / Sơ đồ không gian SĐKG** như *SĐKG TRỤC THOÁT BẾP CĂN HỘ PH-5*, Mặt bằng, Mặt cắt... hoặc kéo View vào Sheet tạo Viewport):
Khung viền Crop View mặc định của Revit hoặc Section Box thường rất rộng, chứa khoảng trắng thừa thãi mênh mông, làm bản vẽ bị loãng và tốn diện tích Sheet.

Tính năng **Tự Động Crop View** giải quyết triệt để vấn đề này chỉ với 1 cú click!

```
      +-----------------------------------------------------------+
      |  (Khoảng trắng thừa thãi mênh mông của Crop View cũ)      |
      |                                                           |
      |          + - - - - - - - - - - - - - - - - - - +          |
      |          | [Khung Crop Box Mới Sau Khi Fit]    |          |
      |          |                                     |          |
      |          |       ┌──┐ (Cụm Ống Thoát Nước/     |          |
      |          |       │  └───┐ Thiết Bị Vừa Tạo)    |          |
      |          |       │  ┌───┘                      |          |
      |          |       └──┘                          |          |
      |          |                                     |          |
      |          + - - - - - - - - - - - - - - - - - - +          |
      |                                                           |
      +-----------------------------------------------------------+
```

### Các bước sử dụng cực kỳ đơn giản:
1. **Vị trí nút bấm trên Ribbon**: Tab `HDTOOL` ➔ Panel `Căn Chỉnh Đối Tượng` ➔ Nút **`Tự Động Crop View`**.
2. **Khởi chạy công cụ**:
   - Nếu đang mở một View 3D, Mặt bằng hoặc Mặt cắt: Bấm nút `Tự Động Crop View`.
   - Nếu đang ở trên Sheet: Click chuột chọn một Viewport trên Sheet rồi bấm nút `Tự Động Crop View`. Tool sẽ tự động Crop View con của Viewport đó ngay trên Sheet!
3. **Cửa sổ Modeless Dialog mở ra**:
   - Hiển thị tên View, tỷ lệ View, và số lượng đối tượng mô hình nhận diện được trong View.
   - **Khoảng Đệm Viền Crop (Padding / Margin)**: Mặc định là `150 mm`. Bạn có thể bấm các nút chọn nhanh `50mm`, `100mm`, `150mm`, `200mm`, `300mm` hoặc kéo thanh trượt.
   - **Khớp Section Box 3D**: Nếu View 3D đang bật Section Box, tool tự động co cả Section Box 3D ôm khít mô hình.
   - Bấm nút đỏ to: **`⚡ TỰ ĐỘNG FIT CROP VIEW (ÔM KHÍT ĐỐI TƯỢNG)`**.
   - Khung Crop Box lập tức co lại ôm sát khít 100% đối tượng mô hình trong tích tắc!
4. **Tính năng Quét Chọn Cụm Đối Tượng Cụ Thể (🎯)**:
   - Nếu trong View có nhiều căn hộ hoặc nhiều trục, nhưng bạn chỉ muốn Crop riêng một trục: Bấm nút `🎯 Quét Chọn Cụm Đối Tượng Cần Crop` ➔ Quét chọn cụm ống đó trên Revit ➔ Tool lập tức Crop riêng cho cụm đó!
5. Bấm `Đóng` hoặc nhấn `ESC` để tắt bảng.

---

## PHẦN II: CĂN CHỈNH LƯỚI TRỤC (GRIDS) THEO KHUNG CROP VIEW (MM)

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
- **Tự động Unhide & Khôi phục các trục bị ẩn / kéo lệch từ View trước**:
  - Khi một View bị thiếu trục hoành hoặc trục tung do ở View khác người vẽ đã kéo đầu mút Grid ra ngoài phạm vi hoặc bị ẩn trong View:
  - HDTOOL tự động quét tìm đường tim Grid của công trình đi qua phạm vi Crop View hiện tại.
  - Tự động bật Unhide và kéo 2 đầu mút ôm sát 4 cạnh Crop View của bản vẽ hiện tại kèm đầy đủ ký hiệu bong bóng!

---

## PHẦN III: CĂN CHỈNH CAO ĐỘ (LEVEL) TRÊN MẶT CẮT / MẶT ĐỨNG / SHEET

Khi trình bày mặt cắt hoặc trang in (Sheet) chứa nhiều khung nhìn:

```
       [Đầu Trái] <---- Left Offset (mm) ----> [Khung Crop View] <---- Right Offset (mm) ----> [Đầu Phải + Bubble]
           |                                         |                                            |
   ========+=========================================+============================================( Tầng 17 - 52000 )
   ========+=========================================+============================================( Tầng 16 - 48850 )
```

- Hỗ trợ căn chỉnh **Đồng loạt tất cả các View trên cùng màn hình làm việc hoặc Sheet** chỉ với 1 cú click!
- Bật/tắt ký hiệu đầu tầng (Symbols / Bubbles) độc lập Bên Trái và Bên Phải.
- Giữ nguyên bảng để tinh chỉnh khoảng cách liên tục, nhấn `ESC` để thoát nhanh.

---

## PHẦN IV: THỐNG KÊ KHỐI LƯỢNG ỐNG GIÓ & PHỤ KIỆN HVAC (TCVN 5687:2010 / SMACNA)

Công cụ bóc tách khối lượng chuyên sâu dành riêng cho hệ thống Thông gió & Điều hòa không khí (HVAC):
- **Vị trí nút bấm trên Ribbon Revit**: Tab `HDTOOL` ➔ Panel `Khối Lượng MEP` ➔ Nút **`Khối Lượng Ống Gió`**.
- **Các tính năng đột phá hàng đầu**:

### 1. Kiến Trúc Modeless Dialog 60 FPS (Không Cần Tắt Tool Vẫn Thao Tác Bản Vẽ)
- Bảng công cụ được mở dưới dạng cửa sổ nổi độc lập (**Modeless Dialog**).
- Kỹ sư có thể đặt bảng công cụ ở một nửa màn hình (hoặc màn hình phụ), trong khi trên màn hình Revit **vẫn dùng chuột Pan, Zoom, click chọn đối tượng, chuyển tab mặt bằng 100% tự do mượt mà** mà không bị khóa luồng!

### 2. Live Selection Tracking ("⚡ Tự bóc khi chọn trên Revit")
- Khi tích chọn ô `[⚡ Tự bóc khi chọn trên Revit]`: Chỉ cần click chuột hoặc quét chọn bất kỳ ống gió nào trên màn hình Revit, HDTOOL **tự động nhận diện và tính tổng diện tích tôn m² ngay lập tức**.

### 3. Tự Động Cập Nhật Tức Thì Khi Sửa Thông Số Ống Trên Revit (Auto-Sync Realtime)
- Sau khi quét khối lượng xong, nếu kỹ sư click vào ống gió trên Revit và **chỉnh sửa bất kỳ thông số nào** (đổi kích thước $W \times H$, kéo dài ống, đổi cao độ, đổi góc cút, đổi hệ thống...): Bảng khối lượng tự động tính toán và cập nhật lại số liệu mới ngay trong tích tắc (Debounce 300ms)!

### 4. Phân Biệt Cút Ống Gió (Elbow) Theo Góc Uốn Thực Tế & Bán Kính Cong $R$
- Trích xuất góc uốn thực tế ($90^\circ, 45^\circ, 60^\circ, 30^\circ...$) và bán kính cong $R150$. Tính diện tích m² tôn chuẩn xác theo góc uốn.

### 5. Côn Thu Chuyển Tiết Diện (Transition) Trích Xuất Chiều Dài $L$
- Đọc trực tiếp Parameter kích thước `Length` ($200\text{ mm}$), chiếu theo trục dòng chảy (BasisZ). Cột `Chiều Dài (m)`: $0.20\text{ m}$, Quy cách: `800x250 - 500x250 (L=200)`, Diện tích tôn: $0.45\text{ m²}$.

### 6. Lượn Z Ống Gió (Offset) Trích Xuất Chiều Dài $L$ & Độ Chênh Cao $H$
- Quy cách chuẩn gọn: `250x800 (L=555, H=400)` (bỏ triệt để chữ Chênh). Hiển thị chiều dài $L$ trên cột Chiều Dài.

### 7. Bảng Khối Lượng Sắp Xếp Chuẩn: HẾT ỐNG THẲNG TỪ TO ĐẾN NHỎ $\rightarrow$ MỚI ĐẾN PHỤ KIỆN
- Ống Gió Thẳng luôn đứng đầu bảng từ TO ĐẾN NHỎ $\rightarrow$ Phụ Kiện (Cút, Côn, Tê, Lượn Z) $\rightarrow$ Van Gió $\rightarrow$ Miệng Gió. Đánh số STT $1..N$.

### 8. Đặt / Cập Nhật Bảng Khối Lượng Lên Sheet Bản Vẽ Revit
- Bấm nút **`[ 🚀 ĐẶT / CẬP NHẬT VÀO SHEET MONG MUỐN... ]`**: Tự động sinh Legend View và đặt vào Sheet mong muốn.

---

## PHẦN V: THỐNG KÊ KHỐI LƯỢNG ỐNG NƯỚC & PHỤ KIỆN MEP

1. **Bóc Khối Lượng Cục Bộ Tại Vị Trí Bất Kỳ**: Quét chuột chọn vùng trên mặt bằng/3D, bảng hiện ngay lập tức đầy đủ tên ống, phụ kiện, van với số lượng và mét dài.
2. **Gộp 3 Parameters Phân Mảnh Làm 1 Cột Duy Nhất (`Tên Phụ Kiện`)**.
3. **Soi Sáng Highlight 2 Chiều (Bi-directional Highlight)**: Click dòng trong bảng ➔ Đối tượng trên Revit sáng xanh, hỗ trợ nút Zoom gần và Isolate.
4. **Kéo Thả Bảng Khối Lượng Vào Sheet (Drag & Drop)**.
5. **Tách Rời Rạc Đoạn Ống Theo Chiều Dài (Không SUM)**.

---

## PHẦN VI: QUẢN LÝ, GOM NHÓM & XUẤT HỒ SƠ BẢN VẼ (SHEET LIST)

1. **Sửa Trực Tiếp Cả Số Hiệu & Tên Bản Vẽ (Inline Edit)**: Nhấn `F2` gõ lại như Excel, đổi tên 2 giai đoạn chống xung đột.
2. **Cây Thư Mục & Gom Nhóm Bản Vẽ (Folder Tree & Sheet Grouping)**: Lọc theo thư mục, kéo thả, điền dải (`Ctrl+D`), sao chép dán nhóm.
3. **Sửa & Thay Thế Số Hiệu Hàng Loạt (Batch Edit Prefix) — [MỚI VERSION 68: KÉO CHUỘT TĂNG ĐUÔI GIỐNG EXCEL]**:
   - **Kéo Chuột Tự Động Tăng Số Đuôi (Excel Fill Handle)**: Trong cột `Số Hiệu Mới`, ô đang chọn có chấm vuông xanh góc dưới-phải. Rê chuột vào chấm xanh, giữ và kéo chuột xuống/lên để tự động tăng dần số đuôi (`/C2/00 -> /C2/01 -> /C2/02...` hoặc `05.1 -> 05.2...`).
   - **Double-Click chấm xanh**: Tự động đánh số tăng dần xuống đến hết danh sách bản vẽ.
   - **Phím tắt `Ctrl+D` / Nút `⚡ Kéo Tăng Đuôi Theo Ô Đang Chọn`**: Quét chọn dải ô và bấm `Ctrl+D` hoặc chuột phải chọn AutoFill để điền tăng dần số đuôi ngay lập tức.
4. **Xuất Gộp Cùng 1 File CAD (.dwg) Duy Nhất Có Đầy Đủ Khung Tên Model / Layout**.
5. **Xuất PDF Native Revit 2024 (.pdf) & Xuất Excel (.xls, .csv)**.
6. **Cập Nhật Danh Mục Từ Excel (Hai Chiều)**.

---

## PHẦN VII: CẬP NHẬT TOOL TRỰC TIẾP / IN-MEMORY HOT RELOAD

- Bấm nút **`Cập Nhật Tool`** trên thanh công cụ HDTOOL: Nạp nóng file DLL mới trực tiếp vào bộ nhớ AppDomain của Revit mà **hoàn toàn không cần tắt Revit hay mở lại dự án**.
- Toàn bộ các nút bấm và tính năng mới lập tức có hiệu lực ngay trong 1 giây!

---

## PHẦN IX: XUẤT BẢN VẼ SANG AUTOCAD & QUY ĐỔI LAYER MEP (MỚI VERSION 85)

### 1. Cách Mở Công Cụ
- **Cách 1 (Dùng Ngay Trong Phiên Revit Hiện Tại)**:
  Bấm nút **`Menu HDTOOL`** trên thanh công cụ Ribbon $\rightarrow$ Bấm nút số **`📁 9. XUẤT CAD & QUY ĐỔI LAYER MEP (MẶT BẰNG / SHEET SANG DWG)`**. Cửa sổ Modeless mở ra ngay lập tức mà không cần tắt hay khởi động lại Revit.
- **Cách 2 (Mở Trực Tiếp Trên Ribbon)**:
  Bấm nút **`Xuất CAD Quy Đổi Layer`** trên Panel 3 ("Quản Lý Bản Vẽ & Sheet"). *(Áp dụng sau khi chạy bộ cài `HDTOOL_Setup_Version_85.exe` và khởi động lại Revit)*.

### 2. Ba Chế Độ Nguồn Xuất Linh Hoạt
1. **👁 View Đang Vẽ (Active View - Lựa Chọn Mặc Định)**:
   - Xuất trực tiếp mô hình MEP từ View mặt bằng (FloorPlan), sơ đồ 3D, mặt cắt (Section) đang mở mà không cần đưa vào Sheet.
2. **📑 Sheet Bản Vẽ**:
   - Chọn một hoặc nhiều Sheet trong danh sách để xuất sang các file CAD `.dwg` độc lập với tên chuẩn `[Số Hiệu] - [Tên Bản Vẽ].dwg`.
3. **📐 View Dự Án**:
   - Quét chọn hàng loạt View trong toàn bộ cây dự án để xuất sang CAD cùng lúc.

### 3. Ma Trận Quy Đổi Layer Hệ Thống MEP (Ducts, Pipes, Equipment)
- **Tự Động Nạp Hệ Thống Thực Tế**: Tự động đọc danh sách `MechanicalSystemType` và `PipingSystemType` từ mô hình Revit.
- **Tùy Biến Đầy Đủ**: Nhấp đúp vào ô để sửa Tên Layer đích, Mã màu AutoCAD ACI (1–255), Kiểu nét (Linetype) và Độ dày nét (Lineweight mm).
- **Bộ Chuẩn Sẵn Có (Presets)**:
  - **Preset TCVN MEP**: Cấp gió `M-DUCT-CAPGIO` (Cyan 4), Hồi gió `M-DUCT-HOIGIO` (Magenta 6), Thải gió `M-DUCT-THAIGIO` (Red 1), Cấp nước `P-PIPE-CAPNUOC` (Blue 5), Thoát nước `P-PIPE-THOATNUOC` (30), Chữa cháy `FP-PIPE-CHUACHAY` (Red 1)...
  - **Preset AIA / NCS Quốc Tế**: `M-DUCT-SUPP`, `M-DUCT-RETN`, `P-DOMW-CWTR`...
  - **Lưu & Nạp File JSON**: Cho phép lưu cấu hình quy đổi Layer ra file JSON và nạp lại bất cứ lúc nào.
- **Chuẩn Hóa Hậu Kỳ Tự Động**: Tích hợp engine AutoCAD Core Console xử lý trực tiếp trên file DWG, đảm bảo bản vẽ CAD mở lên có đầy đủ Layer, đúng màu sắc chuẩn 100%.

---

## PHẦN X: HƯỚNG DẪN CÀI ĐẶT ADD-IN HDTOOL (BỘ CÀI ĐẶT 1-FILE)

Bộ cài đặt HDTOOL được đóng gói thành **1 file `.exe` duy nhất**, độc lập hoàn toàn:
- **Tên file cài đặt**: **`HDTOOL_Setup_Version_85.exe`** (tự động lưu 2 version gần nhất là Version 84 và Version 85).
- **Vị trí lưu trữ**: Tất cả file cài đặt và đóng gói **chỉ sinh ra bên trong thư mục dự án (`c:\Users\Nguye\Desktop\ADDIN REVIT`)**, tuyệt đối không sinh file ra Desktop.
- **Cơ chế lưu trữ 2 Version gần nhất**: Hệ thống tự động lưu giữ **đúng 2 version mới nhất** (hiện tại là `Version 84` và `Version 85`), các version cũ hơn tự động xóa sạch.

### Cách sử dụng bộ cài đặt:
1. Gửi duy nhất 1 file **`HDTOOL_Setup_Version_85.exe`** qua Zalo, Drive, USB hoặc Email.
2. Nhấp đúp chuột vào file **`HDTOOL_Setup_Version_85.exe`**.
3. Cửa sổ cài đặt HadoMEE mở ra, tự động phát hiện phiên bản Revit trên máy (Revit 2024, 2025, 2026, 2027...).
4. Bấm nút màu đỏ: **`🚀 CÀI ĐẶT ADD-IN HDTOOL`**. Báo "Cài đặt thành công" là xong ngay trong 1 giây!
