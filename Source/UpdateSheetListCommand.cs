using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace HDTool
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class UpdateSheetListCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (ToolHotReloadManager.HasActiveHotReloadAssembly())
            {
                return ToolHotReloadManager.ExecuteDynamicCommand(
                    this.GetType().FullName,
                    commandData,
                    ref message,
                    elements
                );
            }
            return ExecuteDirect(commandData, ref message, elements);
        }

        public Result ExecuteDirect(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null)
            {
                MessageBox.Show("Vui lòng mở một dự án Revit trước khi thực hiện cập nhật danh mục bản vẽ.",
                    "HDTOOL Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return Result.Cancelled;
            }

            Document doc = uidoc.Document;

            try
            {
                // Mở hộp thoại chọn file Excel / CSV
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.Title = "HDTOOL - Chọn File Excel hoặc CSV Chứa Danh Mục Bản Vẽ Đã Đổi Tên";
                ofd.Filter = "Tất cả file bảng tính (*.xls;*.xlsx;*.csv)|*.xls;*.xlsx;*.csv|" +
                             "Excel 97-2003 / XML (*.xls)|*.xls|" +
                             "Excel Workbook (*.xlsx)|*.xlsx|" +
                             "File CSV chuẩn UTF-8 (*.csv)|*.csv|" +
                             "Tất cả các file (*.*)|*.*";
                ofd.FilterIndex = 1;
                ofd.RestoreDirectory = true;

                if (ofd.ShowDialog() != DialogResult.OK)
                {
                    return Result.Cancelled;
                }

                string filePath = ofd.FileName;
                List<List<string>> rawRows = UpdateSheetListService.ReadRowsFromFile(filePath);

                if (rawRows == null || rawRows.Count == 0)
                {
                    MessageBox.Show("File đã chọn không có dữ liệu hoặc không đọc được nội dung bảng tính.",
                        "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return Result.Cancelled;
                }

                List<SheetUpdateItem> items = UpdateSheetListService.MatchAndCompare(doc, rawRows);

                if (items.Count == 0)
                {
                    MessageBox.Show("Không tìm thấy số hiệu bản vẽ nào trong file khớp với dự án Revit hiện tại.",
                        "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return Result.Cancelled;
                }

                HDToolModelessHost.EnsureInitialized();

                // Hiển thị giao diện xem trước & xác nhận cập nhật dạng Modeless
                UpdateSheetListPreviewForm previewForm = new UpdateSheetListPreviewForm(doc, items);
                HDToolModelessHost.ShowModeless(previewForm, commandData.Application.MainWindowHandle);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Đã xảy ra lỗi khi đọc file và cập nhật danh mục bản vẽ:\n\n" + ex.Message,
                    "HDTOOL Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return Result.Failed;
            }
        }
    }
}
