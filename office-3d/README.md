# Văn phòng Agent 3D — HadoMEE Workspace
Ứng dụng mô phỏng 3D thời gian thực giúp Sếp Hồ theo dõi trực tiếp đội ngũ Worker của Antigravity Swarm / Subagents và quy trình phát triển Add-in Revit trong không gian văn phòng ban ngày trực quan.

## Cách chạy nhanh
- **Cách 1 (1 click)**: Nhấp đúp file [`MoVanPhongAgent3D.bat`](../MoVanPhongAgent3D.bat) tại thư mục gốc dự án — tự động mở `http://localhost:5173` và chạy đồng thời Vite + Bridge Server.
- **Cách 2 (Terminal)**:
  1. `cd office-3d`
  2. `npm run dev`
  3. Mở trình duyệt tại `http://localhost:5173` (Bridge Server SSE chạy ở cổng `5174`).

## Tính năng chính
- **Theo dõi Swarm trực tiếp**: Tự động quét `%USERPROFILE%\.gemini\antigravity\brain` đọc trạng thái từng worker (`WORK`, `WAIT`, `IDLE`, `FAIL`), thao tác đang chạy từ `transcript.jsonl` và bảng tin `board.jsonl` mỗi 500ms qua SSE.
- **7 Khu vực & Di chuyển WASD**: Bấm tab từng khu vực (*Toàn cảnh*, *Điều phối*, *Hình học & Kiến trúc*, *Lập trình & Giao diện*, *Hội đồng Swarm*, *Kiểm thử & Build*, *Hội trường*, *Lễ tân*) hoặc bật **🚶 Đi bộ (WASD)** để dạo quanh văn phòng.
- **Phát lại quy trình Swarm**: Bấm **▶ Xem lại quy trình** để xem hoạt ảnh các nhân vật chạy qua lại giao việc, sửa lỗi và hoàn tất Build.
