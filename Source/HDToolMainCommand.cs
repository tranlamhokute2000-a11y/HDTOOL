using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace HDTool
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class HDToolMainCommand : IExternalCommand
    {
        private static HDToolMainForm _instance = null;

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
                    TaskDialog.Show("HDTOOL", "Vui lòng mở một dự án Revit.");
                    return Result.Cancelled;
                }

                // Tự động bơm nút Tự Động Crop View lên thanh Ribbon nếu Revit đang mở
                App.EnsureAutoCropButtonDynamic(uiapp);
                HDToolModelessHost.EnsureInitialized();

                if (_instance != null && !_instance.IsDisposed)
                {
                    HDToolModelessHost.ShowModeless(_instance, uiapp.MainWindowHandle);
                    return Result.Succeeded;
                }

                _instance = new HDToolMainForm(uidoc);
                _instance.FormClosed += (s, e) => { _instance = null; };
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
