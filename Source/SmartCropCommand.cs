using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace HDTool
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class SmartCropCommand : IExternalCommand
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
            try
            {
                UIApplication uiapp = commandData.Application;
                UIDocument uidoc = uiapp.ActiveUIDocument;
                if (uidoc == null)
                {
                    TaskDialog.Show("Thông Báo", "Vui lòng mở một dự án Revit.");
                    return Result.Cancelled;
                }

                Autodesk.Revit.DB.View view = uidoc.ActiveView;
                if (view == null) return Result.Cancelled;

                // 1. Đang ở Mặt bằng HOẶC Trang in (Sheet) -> Tự động mở công cụ Căn Grids
                if (GridCropService.IsPlanView(view) || view is ViewSheet)
                {
                    using (GridCropForm form = new GridCropForm(uidoc))
                    {
                        form.ShowDialog();
                    }
                    return Result.Succeeded;
                }

                // 2. Đang ở Mặt cắt / Mặt đứng -> Tự động mở công cụ Căn Level
                string errorMsg;
                if (LevelCropService.IsValidContext(view, out errorMsg))
                {
                    using (LevelCropForm form = new LevelCropForm(uidoc, new List<Level>()))
                    {
                        form.ShowDialog();
                    }
                    return Result.Succeeded;
                }

                TaskDialog.Show("Cảnh Báo", errorMsg);
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
