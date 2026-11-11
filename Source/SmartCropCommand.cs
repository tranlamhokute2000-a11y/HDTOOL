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
        private static GridCropForm _gridInstance = null;
        private static LevelCropForm _levelInstance = null;

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

                HDToolModelessHost.EnsureInitialized();

                // 1. Đang ở Mặt bằng HOẶC Trang in (Sheet) -> Tự động mở công cụ Căn Grids Modeless
                if (GridCropService.IsPlanView(view) || view is ViewSheet)
                {
                    if (_gridInstance != null && !_gridInstance.IsDisposed)
                    {
                        _gridInstance.UpdateContext(uidoc);
                        HDToolModelessHost.ShowModeless(_gridInstance, uiapp.MainWindowHandle);
                    }
                    else
                    {
                        _gridInstance = new GridCropForm(uidoc, HDToolModelessHost.ExEvent, HDToolModelessHost.Handler);
                        _gridInstance.FormClosed += (s, e) => { _gridInstance = null; };
                        HDToolModelessHost.ShowModeless(_gridInstance, uiapp.MainWindowHandle);
                    }
                    return Result.Succeeded;
                }

                // 2. Đang ở Mặt cắt / Mặt đứng -> Tự động mở công cụ Căn Level Modeless
                string errorMsg;
                if (LevelCropService.IsValidContext(view, out errorMsg))
                {
                    if (_levelInstance != null && !_levelInstance.IsDisposed)
                    {
                        _levelInstance.UpdateContext(uidoc, new List<Level>());
                        HDToolModelessHost.ShowModeless(_levelInstance, uiapp.MainWindowHandle);
                    }
                    else
                    {
                        _levelInstance = new LevelCropForm(uidoc, new List<Level>());
                        _levelInstance.FormClosed += (s, e) => { _levelInstance = null; };
                        HDToolModelessHost.ShowModeless(_levelInstance, uiapp.MainWindowHandle);
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
