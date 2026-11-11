using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace HDTool
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CadToDuctCommand : IExternalCommand
    {
        private static CadToDuctForm _instance = null;

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
                    TaskDialog.Show("HDTOOL", "Vui lòng mở một dự án Revit trước khi chạy công cụ.");
                    return Result.Cancelled;
                }

                Document doc = uidoc.Document;
                Autodesk.Revit.DB.View view = uidoc.ActiveView;
                if (view == null)
                {
                    TaskDialog.Show("HDTOOL", "Không có khung nhìn (View) nào đang hoạt động.");
                    return Result.Cancelled;
                }

                HDToolModelessHost.EnsureInitialized();

                // Nếu Form đã mở -> đưa lên trước và cập nhật context
                if (_instance != null && !_instance.IsDisposed)
                {
                    _instance.UpdateContext(uidoc);
                    HDToolModelessHost.ShowModeless(_instance, uiapp.MainWindowHandle);
                    return Result.Succeeded;
                }

                // Mở Modeless Form 60 FPS (Người dùng thoải mái pan, zoom, click chọn trên Revit)
                _instance = new CadToDuctForm(uidoc);
                _instance.FormClosed += (s, e) => { _instance = null; };
                HDToolModelessHost.ShowModeless(_instance, uiapp.MainWindowHandle);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("HDTOOL - Lỗi Dựng Ống Gió Từ CAD", ex.Message + "\n\n" + ex.StackTrace);
                return Result.Failed;
            }
        }
    }
}
