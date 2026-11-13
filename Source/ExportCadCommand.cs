using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace HDTool
{
    /// <summary>
    /// Lệnh Xuất Bản Vẽ Sang AutoCAD & Quy Đổi Layer Hệ Thống MEP (cmdHDToolExportCad)
    /// Hỗ trợ 100% Modeless, thao tác tự do trên bản vẽ và ấn phím ESC thoát lệnh tức thì.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ExportCadCommand : IExternalCommand
    {
        private static ExportCadOptionsForm _instance = null;

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
            try
            {
                UIApplication uiapp = commandData.Application;
                UIDocument uidoc = uiapp.ActiveUIDocument;
                if (uidoc == null)
                {
                    TaskDialog.Show("Thông Báo", "Vui lòng mở một dự án Revit trước khi sử dụng tính năng.");
                    return Result.Cancelled;
                }

                Document doc = uidoc.Document;
                HDToolModelessHost.EnsureInitialized();

                // Nếu Form Xuất CAD & Quy Đổi Layer MEP đã mở -> Kích hoạt đưa lên trước
                if (_instance != null && !_instance.IsDisposed)
                {
                    HDToolModelessHost.ShowModeless(_instance, uiapp.MainWindowHandle);
                    return Result.Succeeded;
                }

                // Mở Modeless Form (100% tự do thao tác trên bản vẽ Revit, ESC thoát lệnh)
                _instance = new ExportCadOptionsForm(doc);
                _instance.FormClosed += delegate(object s, System.Windows.Forms.FormClosedEventArgs e) { _instance = null; };
                HDToolModelessHost.ShowModeless(_instance, uiapp.MainWindowHandle);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
