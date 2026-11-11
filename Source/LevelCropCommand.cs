using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace HDTool
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class LevelCropCommand : IExternalCommand
    {
        private static LevelCropForm _instance = null;

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

                Document doc = uidoc.Document;
                Autodesk.Revit.DB.View view = uidoc.ActiveView;

                // Kiểm tra loại View / Sheet
                string errorMsg;
                if (!LevelCropService.IsValidContext(view, out errorMsg))
                {
                    TaskDialog.Show("Cảnh Báo", errorMsg);
                    return Result.Cancelled;
                }

                HDToolModelessHost.EnsureInitialized();

                // Kiểm tra các Level đã được người dùng chọn từ trước (nếu có)
                List<Level> preselectedLevels = new List<Level>();
                ICollection<ElementId> selIds = uidoc.Selection.GetElementIds();
                if (selIds != null && selIds.Count > 0)
                {
                    foreach (ElementId id in selIds)
                    {
                        Element elem = doc.GetElement(id);
                        Level lvl = elem as Level;
                        if (lvl != null)
                        {
                            preselectedLevels.Add(lvl);
                        }
                    }
                }

                // Nếu Form đã mở -> Cập nhật context và kích hoạt
                if (_instance != null && !_instance.IsDisposed)
                {
                    _instance.UpdateContext(uidoc, preselectedLevels);
                    HDToolModelessHost.ShowModeless(_instance, uiapp.MainWindowHandle);
                    return Result.Succeeded;
                }

                // Mở cửa sổ điều khiển chính dạng Modeless Dialog
                _instance = new LevelCropForm(uidoc, preselectedLevels);
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
