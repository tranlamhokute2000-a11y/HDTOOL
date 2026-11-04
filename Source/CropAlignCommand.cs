using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace HDTool
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CropAlignCommand : IExternalCommand
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

                // Mở Bảng Tùy Chọn Căn Chỉnh Bản Vẽ (Grids & Level)
                using (CropAlignOptionForm form = new CropAlignOptionForm(uidoc))
                {
                    form.ShowDialog();
                }

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

