using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace HDTool
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class GridCropCommand : IExternalCommand
    {
        private static GridCropForm _instance = null;
        private static HDToolExternalEventHandler _eventHandler = null;
        private static ExternalEvent _externalEvent = null;

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
                    TaskDialog.Show("Thông Báo", "Vui lòng mở một dự án Revit.");
                    return Result.Cancelled;
                }

                // Tự động kiểm tra và thêm nút Tự Động Crop View vào Ribbon nếu chưa có
                App.EnsureAutoCropButtonDynamic(uiapp);

                Document doc = uidoc.Document;
                Autodesk.Revit.DB.View view = uidoc.ActiveView;

                // Kiểm tra loại View
                string errorMsg;
                if (!GridCropService.IsValidContextForGrids(view, out errorMsg))
                {
                    TaskDialog.Show("Cảnh Báo", errorMsg);
                    return Result.Cancelled;
                }

                if (_eventHandler == null) _eventHandler = new HDToolExternalEventHandler();
                if (_externalEvent == null) _externalEvent = ExternalEvent.Create(_eventHandler);

                if (_instance != null && !_instance.IsDisposed)
                {
                    _instance.UpdateContext(uidoc);
                    _instance.WindowState = System.Windows.Forms.FormWindowState.Normal;
                    _instance.BringToFront();
                    _instance.Activate();
                    return Result.Succeeded;
                }

                _instance = new GridCropForm(uidoc, _externalEvent, _eventHandler);
                _instance.FormClosed += (s, e) => { _instance = null; };
                _instance.Show(new RevitWindowWrapper(uiapp.MainWindowHandle));

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