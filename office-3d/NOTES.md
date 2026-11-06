# GHI CHÚ KỸ THUẬT & GIẢ ĐỊNH (NOTES.MD)

Ứng dụng **Văn phòng Agent Revit 3D** trong thư mục `office-3d/` được xây dựng theo chuẩn yêu cầu kỹ thuật:

## 1. Công nghệ & Cấu trúc
- **Frontend**: Vite + TypeScript + Three.js (`MeshToonMaterial`, `OrbitControls`, `PCFSoftShadowMap`).
- **Phông chữ**: Be Vietnam Pro (hỗ trợ hiển thị tiếng Việt có dấu 100%).
- **Bridge Server**: Node Express (port 5174) kết nối Vite (port 5173) qua proxy `/api`, cung cấp Server-Sent Events (SSE).
- **Đồ họa Procedural**: 100% nhân vật, bàn ghế, thiết bị, bảng pipeline và mô hình trung tâm dựng bằng hình khối Three.js cơ bản, không dùng model tải ngoài (GLB/FBX).

## 2. Các giả định kỹ thuật
- **Nguồn dữ liệu**: Server đọc file `work/<tool-slug>/events.jsonl`. Nếu không tìm thấy, hệ thống tự động chuyển sang chế độ DEMO 63 sự kiện trong `demo-events.jsonl` (~100s).
- **Âm thanh**: Tạo bằng WebAudio Oscillator (tiếng click bàn phím, ting chuông, thud con dấu), mặc định tắt theo nguyên tắc không làm phiền người dùng.
- **Tối ưu hóa hiệu năng**: Giới hạn pixel ratio 2.0, tạm dừng render khi tab trình duyệt bị ẩn (`document.hidden`), tối ưu polygon cho 60 FPS mượt mà.

