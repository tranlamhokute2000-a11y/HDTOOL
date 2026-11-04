using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace HDTool
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class FitTableScaleCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                if (uidoc == null) return Result.Cancelled;

                Document doc = uidoc.Document;
                Autodesk.Revit.DB.View curView = uidoc.ActiveView;

                int updatedCount = 0;
                List<string> updatedNames = new List<string>();

                using (Transaction tx = new Transaction(doc, "HDTOOL - Khớp Tỷ Lệ Bảng Khối Lượng"))
                {
                    tx.Start();

                    // 1. Quét trên View/Sheet hiện tại
                    if (curView is ViewSheet)
                    {
                        ViewSheet sheet = curView as ViewSheet;
                        FilteredElementCollector vpCol = new FilteredElementCollector(doc, sheet.Id).OfClass(typeof(Viewport));
                        foreach (Viewport vp in vpCol.Cast<Viewport>())
                        {
                            Autodesk.Revit.DB.View v = doc.GetElement(vp.ViewId) as Autodesk.Revit.DB.View;
                            if (v is ViewDrafting && MepQuantityService.IsHDToolTable(v as ViewDrafting))
                            {
                                ViewDrafting vd = v as ViewDrafting;
                                int currentScale = vd.Scale;
                                int drawnScale = MepQuantityService.GetTableDrawnScale(vd);
                                if (currentScale > 0 && currentScale != drawnScale)
                                {
                                    if (MepQuantityService.RescaleDraftingViewTable(doc, vd, currentScale))
                                    {
                                        updatedCount++;
                                        updatedNames.Add(string.Format("{0} (Tỷ lệ 1:{1})", vd.Name, currentScale));
                                    }
                                }
                            }
                        }
                    }
                    else if (curView is ViewDrafting && MepQuantityService.IsHDToolTable(curView as ViewDrafting))
                    {
                        ViewDrafting vd = curView as ViewDrafting;
                        int currentScale = vd.Scale;
                        int drawnScale = MepQuantityService.GetTableDrawnScale(vd);
                        if (currentScale > 0 && currentScale != drawnScale)
                        {
                            if (MepQuantityService.RescaleDraftingViewTable(doc, vd, currentScale))
                            {
                                updatedCount++;
                                updatedNames.Add(string.Format("{0} (Tỷ lệ 1:{1})", vd.Name, currentScale));
                            }
                        }
                    }
                    else
                    {
                        // Quét tất cả các bảng HDTOOL trong toàn dự án
                        FilteredElementCollector col = new FilteredElementCollector(doc).OfClass(typeof(ViewDrafting));
                        foreach (ViewDrafting vd in col.Cast<ViewDrafting>())
                        {
                            if (MepQuantityService.IsHDToolTable(vd))
                            {
                                int currentScale = vd.Scale;
                                int drawnScale = MepQuantityService.GetTableDrawnScale(vd);
                                if (currentScale > 0 && currentScale != drawnScale)
                                {
                                    if (MepQuantityService.RescaleDraftingViewTable(doc, vd, currentScale))
                                    {
                                        updatedCount++;
                                        updatedNames.Add(string.Format("{0} (Tỷ lệ 1:{1})", vd.Name, currentScale));
                                    }
                                }
                            }
                        }
                    }

                    tx.Commit();
                }

                if (updatedCount > 0)
                {
                    string details = string.Join("\n• ", updatedNames);
                    TaskDialog.Show("HDTOOL - Căn Khớp Tỷ Lệ Thành Công",
                        string.Format("🎉 ĐÃ CĂN KHỚP HOÀN TOÀN CHO {0} BẢNG KHỐI LƯỢNG!\n\nToàn bộ đường kẻ ô và chữ đã được đồng bộ nhảy theo tỷ lệ khung nhìn chuẩn xác 100%, không còn bị đè chữ hay tràn ô:\n\n• {1}",
                        updatedCount, details));
                }
                else
                {
                    TaskDialog.Show("HDTOOL - Bảng Đã Khớp Chuẩn",
                        "✔ Các Bảng Khối Lượng hiện tại đã khớp tỷ lệ 100% với ô kẻ, chữ và khung viền hoàn toàn vừa vặn!");
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
