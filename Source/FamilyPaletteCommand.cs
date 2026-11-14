using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace HDTool
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class FamilyPaletteCommand : IExternalCommand
    {
        private static FamilyPaletteForm _instance = null;

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
                    TaskDialog.Show("HDTOOL", "Vui lòng mở một dự án Revit trước khi chạy công cụ Palette Family.");
                    return Result.Cancelled;
                }

                Document doc = uidoc.Document;
                if (doc == null)
                {
                    TaskDialog.Show("HDTOOL", "Không có tài liệu Revit nào đang hoạt động.");
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

                // Mở Modeless Form 100% (Kỹ sư thoải mái pan, zoom, vẽ và đặt Family trên Revit)
                _instance = new FamilyPaletteForm(uidoc);
                _instance.FormClosed += delegate { _instance = null; };
                HDToolModelessHost.ShowModeless(_instance, uiapp.MainWindowHandle);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("HDTOOL - Lỗi Palette Family 3D", ex.Message + "\n\n" + ex.StackTrace);
                return Result.Failed;
            }
        }
    }
}
