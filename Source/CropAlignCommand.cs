using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace HDTool
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CropAlignCommand : IExternalCommand
    {
        private static CropAlignOptionForm _instance = null;

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

                // Tự động bơm nút Tự Động Crop View lên thanh Ribbon nếu chưa có
                App.EnsureAutoCropButtonDynamic(uiapp);

                Autodesk.Revit.DB.View view = uidoc.ActiveView;
                if (view == null) return Result.Cancelled;

                HDToolModelessHost.EnsureInitialized();

                // Kiểm tra form singleton đã mở chưa
                if (_instance != null && !_instance.IsDisposed)
                {
                    _instance.UpdateContext(uidoc);
                    HDToolModelessHost.ShowModeless(_instance, uiapp.MainWindowHandle);
                    return Result.Succeeded;
                }

                // Mở Bảng Tùy Chọn Căn Chỉnh Bản Vẽ dạng Modeless (Không khóa thao tác bản vẽ)
                _instance = new CropAlignOptionForm(uidoc);
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
