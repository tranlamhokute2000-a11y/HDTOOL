# Văn phòng Agent Revit 3D — HadoMEE
Ứng dụng mô phỏng đội ngũ 12 kỹ thuật viên BIM Revit bằng Three.js theo thời gian thực.

## Cách chạy
1. `cd office-3d`
2. `npm install` (nếu chưa cài)
3. `npm run dev`
4. Mở trình duyệt tại `http://localhost:5173`
- Server cầu nối SSE chạy tại cổng 5174, đọc dữ liệu từ `work/<tool-slug>/events.jsonl`.
- Nhấn phím 1-5 để chuyển góc camera, bấm vào nhân vật để xem chi tiết.

