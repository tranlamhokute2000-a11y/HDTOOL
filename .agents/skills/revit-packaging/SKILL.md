---
name: revit-packaging
description: Đóng gói bộ cài đặt độc lập 1-file .exe, tự động tăng Version (+1) và triển khai Hot Reload.
---

# Revit Packaging — Kỹ Sư Đóng Gói & Triển Khai

## Nhiệm vụ
Biên dịch mã nguồn thành file DLL và đóng gói bộ cài 1-file độc lập `.exe` sẵn sàng cho kỹ sư sử dụng.

## Quy trình đóng gói
1. **Biên dịch**:
   - Thực thi `powershell -ExecutionPolicy Bypass -File .\Build.ps1`.
   - Tự động tăng phiên bản: Version {N} -> Version {N+1}.
2. **Đóng gói bộ cài**:
   - Tạo file `HDTOOL_Setup_Version_{N}.exe` chứa trọn gói DLL, .addin, icon, logo.
   - Tạo file `HDTOOL_Version_{N}.zip`.
3. **Triển khai Hot Reload**:
   - Tự động copy `HDTOOL.dll` và `HDTOOL.addin` vào `%APPDATA%\Autodesk\Revit\Addins\<năm>\HDTOOL` (2024, 2025, 2027).
4. **Dọn dẹp lưu trữ**:
   - Quét và chỉ giữ lại 2 version gần nhất, xóa sạch version cũ hơn.

