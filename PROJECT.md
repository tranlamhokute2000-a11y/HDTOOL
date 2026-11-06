# PROJECT.md — thông tin dự án (điền một lần, mọi agent đều đọc)

- Tên sản phẩm / bộ tool: HDTOOL (Add-in Autodesk Revit HadoMEE)
- Namespace gốc: `HDTool`
- Tên công ty / tác giả: TẬP ĐOÀN HÀ ĐÔ - HadoMEE (Sếp Hồ - tranlamhokute2000-a11y)
- Phiên bản Revit cần hỗ trợ: Autodesk Revit 2024, 2025, 2026, 2027
- Ngôn ngữ giao diện tool: Tiếng Việt (chuẩn kỹ thuật MEP, TCVN 5687:2010, SMACNA)
- Thư mục solution: `c:\Users\Nguye\Desktop\ADDIN REVIT`
- Cách phân phối: Bộ cài đặt độc lập 1-file .exe (`HDTOOL_Setup_Version_{N}.exe`), hỗ trợ Hot Reload trực tiếp vào `%APPDATA%\Autodesk\Revit\Addins\<năm>\HDTOOL`
- Công cụ build: PowerShell `powershell -ExecutionPolicy Bypass -File .\Build.ps1` (csc.exe .NET 4.8 C# 5)
- Ghi chú khác: 0 file trên Desktop, chỉ lưu 2 version gần nhất, không tự ý push GitHub khi chưa có lệnh.

