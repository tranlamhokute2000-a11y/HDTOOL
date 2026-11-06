---
trigger: always_on
description: "Quy tắc ghi sự kiện vào work/<tool-slug>/events.jsonl để ứng dụng Văn phòng 3D hiển thị đúng ai đang làm gì (áp dụng cho mọi vai trò revit-*)."
---

# Quy tắc ghi sự kiện cho Văn phòng 3D (áp dụng cho mọi vai trò `revit-*`)

Mục đích: để ứng dụng `office-3d/` hiển thị đúng ai đang làm gì. Ghi sự kiện chỉ là việc phụ, không được làm chậm hay làm hỏng công việc chính.

## Cách ghi
- Mỗi sự kiện là MỘT dòng JSON hợp lệ, **ghi nối thêm (append)** vào `work/<tool-slug>/events.jsonl` (UTF-8). Không ghi đè, không sửa dòng cũ, không xuống dòng giữa JSON.
- `ts` lấy giờ hiện tại, định dạng ISO 8601 có múi giờ (ví dụ `2026-11-05T09:30:12+07:00`).
- PowerShell: `Add-Content -Path "work/<slug>/events.jsonl" -Encoding utf8 -Value '<một dòng JSON>'`
- Bash: `echo '<một dòng JSON>' >> work/<slug>/events.jsonl`
- Hoặc dùng công cụ ghi file của bạn ở chế độ nối thêm.

## Trường chung
`ts`, `tool` (tool-slug), `agent` (mã vai trò), `event`, `detail` (tiếng Việt, ≤ 80 ký tự).
Mã agent: `orchestrator, requirements, architect, researcher, api-developer, ui, reviewer, tester, debugger, security, packaging, docs-writer`.
Mã giai đoạn: `spec, design, code, review, test, security, package, docs`.

## Khi nào ghi gì
| Sự kiện | Khi nào | Trường thêm |
|---|---|---|
| `tool_start` | Orchestrator nhận yêu cầu mới | — |
| `start` | Một vai trò bắt đầu việc của mình | — |
| `progress` | Giữa chừng việc dài (> 20 giây), tối đa 3 lần mỗi việc | `pct` (0–100) |
| `handoff` | Giao kết quả cho vai trò khác, hoặc hỏi / trả lời Researcher | `to`, `file` (tên file bàn giao), `kind`: `work` / `query` / `reply` |
| `gate` | Orchestrator kiểm cổng giai đoạn | `stage`, `result`: `pass` / `fail` |
| `issue` | Reviewer / Security / Tester phát hiện vấn đề | `severity`: `block` / `warn` / `hint` |
| `loop_back` | Trả việc về vòng sửa | `from`, `to`, `round` |
| `build` | Sau mỗi lần build | `result`: `ok` / `fail`, `errors` |
| `test` | Sau khi chạy test | `passed`, `failed` |
| `ask_user` | Cần chủ dự án xác nhận (theo mục 1 của rules) | — |
| `user_reply` | Chủ dự án đã trả lời | — |
| `error` | Việc không thể tiếp tục | — |
| `done` | Vai trò xong phần việc | `file` (nếu có) |
| `tool_done` | Orchestrator kết thúc cả tool | — |
| `note` | Lời nhắn ngắn đáng chú ý | — |

Ví dụ:
`{"ts":"2026-11-05T09:30:12+07:00","tool":"danh-so-phong","agent":"requirements","event":"start","detail":"Đang viết 01-spec.md"}`
`{"ts":"2026-11-05T09:31:40+07:00","tool":"danh-so-phong","agent":"requirements","event":"handoff","to":"architect","file":"01-spec.md","kind":"work","detail":"Giao đặc tả cho Kiến trúc"}`

## Giới hạn
- Không quá 1 dòng mỗi 3 giây, trừ `handoff`, `gate`, `issue`, `ask_user`.
- `detail` KHÔNG chứa khóa API, mật khẩu, tên khách hàng / dự án nhạy cảm, đường dẫn tuyệt đối trên máy cá nhân hay nội dung model.
- Quên ghi sự kiện thì cứ làm tiếp công việc chính; ứng dụng sẽ tự suy luận từ file trong `work/`. Ghi sự kiện cho kịp thời chỉ để cảnh sinh động chính xác hơn.

