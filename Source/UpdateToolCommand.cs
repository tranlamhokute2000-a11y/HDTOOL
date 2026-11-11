using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace HDTool
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class UpdateToolCommand : IExternalCommand
    {
        private static UpdateToolForm _instance = null;

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
                // Tự động kiểm tra và bơm các nút mới lên thanh Ribbon nếu Revit đang chạy từ trước
                App.EnsureAutoCropButtonDynamic(commandData.Application);
                HDToolModelessHost.EnsureInitialized();

                if (_instance != null && !_instance.IsDisposed)
                {
                    HDToolModelessHost.ShowModeless(_instance, commandData.Application.MainWindowHandle);
                    return Result.Succeeded;
                }

                _instance = new UpdateToolForm(commandData.Application);
                _instance.FormClosed += (s, e) => { _instance = null; };
                HDToolModelessHost.ShowModeless(_instance, commandData.Application.MainWindowHandle);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("HDTOOL - Lỗi Mở Cửa Sổ Cập Nhật", ex.Message);
                return Result.Failed;
            }
        }
    }
}
