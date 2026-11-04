using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace HDTool
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class QuickPickCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
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

                // TRƯỜNG HỢP 1: Đang ở trên Sheet (Trang in chứa nhiều Viewport như H12, H14 trong ảnh)
                ViewSheet sheet = view as ViewSheet;
                if (sheet != null)
                {
                    List<Autodesk.Revit.DB.View> placedViews = LevelCropService.GetApplicableViewsInContext(uidoc);
                    if (placedViews.Count == 0)
                    {
                        TaskDialog.Show("Thông Báo", "Không tìm thấy View Mặt cắt hoặc Mặt đứng nào trên Sheet này.");
                        return Result.Cancelled;
                    }

                    string viewNames = string.Join(", ", placedViews.Select(v => v.Name).ToArray());
                    using (QuickInputForm inputForm = new QuickInputForm(placedViews.Count, viewNames, true))
                    {
                        if (inputForm.ShowDialog() == DialogResult.OK)
                        {
                            string summary;
                            LevelCropService.AdjustMultipleViews(
                                doc,
                                placedViews,
                                inputForm.OffsetLeft,
                                inputForm.OffsetRight,
                                inputForm.Make2D,
                                inputForm.ShowSymbolLeft,
                                inputForm.ShowSymbolRight,
                                out summary);

                            TaskDialog.Show("Kết Quả Căn Chỉnh Đa View", summary);
                        }
                    }

                    return Result.Succeeded;
                }

                // TRƯỜNG HỢP 2: Đang ở trong View cụ thể (Section / Elevation)
                List<Level> targetLevels = new List<Level>();

                // Kiểm tra xem người dùng đã chọn trước chưa
                ICollection<ElementId> selIds = uidoc.Selection.GetElementIds();
                if (selIds != null && selIds.Count > 0)
                {
                    foreach (ElementId id in selIds)
                    {
                        Element elem = doc.GetElement(id);
                        Level lvl = elem as Level;
                        if (lvl != null)
                        {
                            targetLevels.Add(lvl);
                        }
                    }
                }

                // Nếu chưa chọn trước, yêu cầu người dùng click/quét chọn trên màn hình
                if (targetLevels.Count == 0)
                {
                    try
                    {
                        IList<Reference> references = uidoc.Selection.PickObjects(
                            ObjectType.Element,
                            new LevelSelectionFilter(false),
                            "Click chọn các Level cần căn chỉnh (nhấn Finish trên thanh Options Bar khi chọn xong)");

                        if (references != null && references.Count > 0)
                        {
                            foreach (Reference r in references)
                            {
                                Element elem = doc.GetElement(r);
                                Level lvl = elem as Level;
                                if (lvl != null)
                                {
                                    targetLevels.Add(lvl);
                                }
                            }
                        }
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        return Result.Cancelled;
                    }
                }

                if (targetLevels.Count == 0)
                {
                    TaskDialog.Show("Thông Báo", "Bạn chưa chọn Level nào.");
                    return Result.Cancelled;
                }

                string levelNames = string.Join(", ", targetLevels.Take(4).Select(l => l.Name).ToArray());
                if (targetLevels.Count > 4)
                {
                    levelNames += string.Format(" và {0} Level khác", targetLevels.Count - 4);
                }

                using (QuickInputForm inputForm = new QuickInputForm(targetLevels.Count, levelNames, false))
                {
                    if (inputForm.ShowDialog() == DialogResult.OK)
                    {
                        string resultMsg;
                        int count = LevelCropService.AdjustLevels(
                            doc,
                            view,
                            targetLevels,
                            inputForm.OffsetLeft,
                            inputForm.OffsetRight,
                            inputForm.Make2D,
                            inputForm.ShowSymbolLeft,
                            inputForm.ShowSymbolRight,
                            out resultMsg);

                        TaskDialog.Show("Kết Quả Căn Chỉnh", resultMsg);
                    }
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
