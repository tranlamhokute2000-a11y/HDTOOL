using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace HDTool
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class AutoCropCommand : IExternalCommand
    {
        private static AutoCropForm _instance = null;
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

                // Nếu Form đã mở thì kích hoạt đưa lên trên cùng
                if (_instance != null && !_instance.IsDisposed)
                {
                    if (_instance.WindowState == System.Windows.Forms.FormWindowState.Minimized)
                    {
                        _instance.WindowState = System.Windows.Forms.FormWindowState.Normal;
                    }
                    _instance.Show();
                    _instance.BringToFront();
                    _instance.Activate();
                    return Result.Succeeded;
                }

                // Khởi tạo Event Handler và ExternalEvent điều phối luồng Modeless
                if (_eventHandler == null)
                {
                    _eventHandler = new HDToolExternalEventHandler();
                }
                if (_externalEvent == null)
                {
                    _externalEvent = ExternalEvent.Create(_eventHandler);
                }

                // Mở Form Modeless Dialog 60 FPS
                _instance = new AutoCropForm(uidoc, _externalEvent, _eventHandler);
                _instance.FormClosed += (s, e) => { _instance = null; };

                IntPtr hMain = uiapp.MainWindowHandle;
                HDToolModelessHost.ShowModeless(_instance, hMain);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("HDTOOL — Lỗi Mở Công Cụ Tự Động Crop View", ex.Message + "\n\n" + ex.StackTrace);
                return Result.Failed;
            }
        }
    }
}
