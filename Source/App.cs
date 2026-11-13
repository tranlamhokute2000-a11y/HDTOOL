using System;
using System.Collections.Generic;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace HDTool
{
    public class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                string tabName = "HDTOOL";

                // 1. Tao Ribbon Tab HDTOOL (neu chua co)
                try
                {
                    application.CreateRibbonTab(tabName);
                }
                catch
                {
                    // Tab co the da ton tai tu truoc
                }

                string assemblyPath = Assembly.GetExecutingAssembly().Location;

                // ==================================================================
                // PANEL 1 : CĂN CHỈNH ĐỐI TƯỢNG
                // ==================================================================
                RibbonPanel panel_1 = GetOrCreateRibbonPanel(application, tabName, "Căn Chỉnh Đối Tượng");

                // SplitButton: Căn Chỉnh Grids / Level
                SplitButtonData cmdHDToolCropAlignSplit_Data = new SplitButtonData("cmdHDToolCropAlignSplit", "Căn Chỉnh\nGrids / Level");
                SplitButton cmdHDToolCropAlignSplit_Btn = panel_1.AddItem(cmdHDToolCropAlignSplit_Data) as SplitButton;
                if (cmdHDToolCropAlignSplit_Btn != null)
                {
                    cmdHDToolCropAlignSplit_Btn.IsSynchronizedWithCurrentItem = true;

                    // Mục 1: Bảng điều khiển tích hợp Grids & Level
                    PushButtonData cmdCropAlignUnified_Data = new PushButtonData("cmdCropAlignUnified", "Căn Chỉnh Grids / Level", assemblyPath, "HDTool.CropAlignCommand");
                    cmdCropAlignUnified_Data.LargeImage = ResourceHelper.CreateCropAlignCombinedIcon(32);
                    cmdCropAlignUnified_Data.Image = ResourceHelper.CreateCropAlignCombinedIcon(16);
                    cmdHDToolCropAlignSplit_Btn.AddPushButton(cmdCropAlignUnified_Data);

                    // Mục 2: Căn Grids
                    PushButtonData cmdGridCrop_Item_Data = new PushButtonData("cmdGridCrop_Item", "Căn Chỉnh Grids", assemblyPath, "HDTool.GridCropCommand");
                    cmdGridCrop_Item_Data.LargeImage = ResourceHelper.CreateGridCropIcon(32);
                    cmdGridCrop_Item_Data.Image = ResourceHelper.CreateGridCropIcon(16);
                    cmdHDToolCropAlignSplit_Btn.AddPushButton(cmdGridCrop_Item_Data);

                    // Mục 3: Căn Level
                    PushButtonData cmdLevelCrop_Item_Data = new PushButtonData("cmdLevelCrop_Item", "Căn Chỉnh Level", assemblyPath, "HDTool.LevelCropCommand");
                    cmdLevelCrop_Item_Data.LargeImage = ResourceHelper.CreateLevelCropIcon(32);
                    cmdLevelCrop_Item_Data.Image = ResourceHelper.CreateLevelCropIcon(16);
                    cmdHDToolCropAlignSplit_Btn.AddPushButton(cmdLevelCrop_Item_Data);

                    // Mục 4: Tự Động Crop View (Menu con)
                    PushButtonData cmdAutoCrop_SubItem_Data = new PushButtonData("cmdAutoCrop_SubItem", "Tự Động Crop View", assemblyPath, "HDTool.AutoCropCommand");
                    cmdAutoCrop_SubItem_Data.LargeImage = ResourceHelper.CreateAutoCropIcon(32);
                    cmdAutoCrop_SubItem_Data.Image = ResourceHelper.CreateAutoCropIcon(16);
                    cmdHDToolCropAlignSplit_Btn.AddPushButton(cmdAutoCrop_SubItem_Data);
                }

                // PushButton Riêng Biệt (Lớn 32x32): TỰ ĐỘNG CROP VIEW (THEO LỆNH SẾP HỒ)
                PushButtonData cmdHDToolAutoCrop_Data = new PushButtonData(
                    "cmdHDToolAutoCrop", 
                    "Tự Động\nCrop View", 
                    assemblyPath, 
                    "HDTool.AutoCropCommand"
                );
                cmdHDToolAutoCrop_Data.ToolTip = "[HDTOOL] Tự động tạo và căn chỉnh Crop View bao quanh toàn bộ các đối tượng có trong View vừa tạo (Sơ đồ không gian 3D, Mặt bằng, Mặt cắt, Viewport trên Sheet).";
                cmdHDToolAutoCrop_Data.LargeImage = ResourceHelper.CreateAutoCropIcon(32);
                cmdHDToolAutoCrop_Data.Image = ResourceHelper.CreateAutoCropIcon(16);
                panel_1.AddItem(cmdHDToolAutoCrop_Data);

                // ==================================================================
                // PANEL 2 : KHỐI LƯỢNG MEP
                // ==================================================================
                RibbonPanel panel_2 = GetOrCreateRibbonPanel(application, tabName, "Khối Lượng MEP");

                // PushButton: Thống Kê Ống Nước
                PushButtonData cmdHDToolMepQuantity_Data = new PushButtonData("cmdHDToolMepQuantity", "Thống Kê\nỐng Nước", assemblyPath, "HDTool.MepQuantityCommand");
                cmdHDToolMepQuantity_Data.ToolTip = "[HDTOOL] Thống kê khối lượng ống nước & phụ kiện: Gộp parameter, soi sáng mô hình và đưa vào Sheet.";
                cmdHDToolMepQuantity_Data.LargeImage = ResourceHelper.CreateMepQuantityIcon(32);
                cmdHDToolMepQuantity_Data.Image = ResourceHelper.CreateMepQuantityIcon(16);
                panel_2.AddItem(cmdHDToolMepQuantity_Data);

                // PushButton: Soi Sáng Bảng Sheet
                PushButtonData cmdHDToolPickTableHighlight_Data = new PushButtonData("cmdHDToolPickTableHighlight", "CHECK VAR\nPLUMBING", assemblyPath, "HDTool.PickTableHighlightCommand");
                cmdHDToolPickTableHighlight_Data.ToolTip = "[HDTOOL] CHECK VAR PLUMBING: Đối soát, soi sáng khối lượng ống nước & phụ kiện trực tiếp với mô hình 3D trên Sheet.";
                cmdHDToolPickTableHighlight_Data.LargeImage = ResourceHelper.CreatePickTableHighlightIcon(32);
                cmdHDToolPickTableHighlight_Data.Image = ResourceHelper.CreatePickTableHighlightIcon(16);
                panel_2.AddItem(cmdHDToolPickTableHighlight_Data);

                // PushButton: Khớp Tỷ Lệ Bảng Khối Lượng
                PushButtonData cmdHDToolFitTableScale_Data = new PushButtonData("cmdHDToolFitTableScale", "Khớp Tỷ Lệ\nBảng Khối Lượng", assemblyPath, "HDTool.FitTableScaleCommand");
                cmdHDToolFitTableScale_Data.ToolTip = "[HDTOOL] Tự động đồng bộ và căn khớp kích thước ô kẻ với cỡ chữ theo mọi tỷ lệ (1:1, 1:2, 1:5, 1:10...).";
                cmdHDToolFitTableScale_Data.LargeImage = ResourceHelper.CreateFitScaleIcon(32);
                cmdHDToolFitTableScale_Data.Image = ResourceHelper.CreateFitScaleIcon(16);
                panel_2.AddItem(cmdHDToolFitTableScale_Data);

                // PushButton: Khối Lượng Ống Gió
                PushButtonData cmdHDToolDuctQuantity_Data = new PushButtonData("cmdHDToolDuctQuantity", "Khối Lượng\nỐng Gió", assemblyPath, "HDTool.DuctQuantityCommand");
                cmdHDToolDuctQuantity_Data.ToolTip = "[HDTOOL] Thống kê khối lượng ống gió HVAC: Tính diện tích tôn m², phân loại cút, côn, lượn, van gió VCD và miệng gió.";
                cmdHDToolDuctQuantity_Data.LargeImage = ResourceHelper.CreateDuctQuantityIcon(32);
                cmdHDToolDuctQuantity_Data.Image = ResourceHelper.CreateDuctQuantityIcon(16);
                panel_2.AddItem(cmdHDToolDuctQuantity_Data);

                // PushButton: Dựng Ống Gió Từ CAD Link (MỚI VERSION 70 - THEO LỆNH SẾP HỒ)
                PushButtonData cmdHDToolCadToDuct_Data = new PushButtonData("cmdHDToolCadToDuct", "Dựng Ống Gió\nTừ CAD", assemblyPath, "HDTool.CadToDuctCommand");
                cmdHDToolCadToDuct_Data.ToolTip = "[HDTOOL] Tự động nhận diện tuyến ống, size WxH, cút, côn đồng tâm, chân rẽ Tap và dựng lại mô hình 3D ống gió hoàn chỉnh từ bản vẽ CAD Link.";
                cmdHDToolCadToDuct_Data.LargeImage = ResourceHelper.CreateCadToDuctIcon(32);
                cmdHDToolCadToDuct_Data.Image = ResourceHelper.CreateCadToDuctIcon(16);
                panel_2.AddItem(cmdHDToolCadToDuct_Data);

                // ==================================================================
                // PANEL 3 : QUẢN LÝ BẢN VẼ & SHEET
                // ==================================================================
                RibbonPanel panel_3 = GetOrCreateRibbonPanel(application, tabName, "Quản Lý Bản Vẽ & Sheet");

                // PushButton: Xuất Danh Mục Bản Vẽ
                PushButtonData cmdHDToolSheetList_Data = new PushButtonData("cmdHDToolSheetList", "Xuất Danh Mục\nBản Vẽ", assemblyPath, "HDTool.ExportSheetListCommand");
                cmdHDToolSheetList_Data.ToolTip = "[HDTOOL] Xuất danh mục bản vẽ (Sheet List) ra Excel (.xls, .csv) hoặc Clipboard.";
                cmdHDToolSheetList_Data.LargeImage = ResourceHelper.CreateSheetListIcon(32);
                cmdHDToolSheetList_Data.Image = ResourceHelper.CreateSheetListIcon(16);
                panel_3.AddItem(cmdHDToolSheetList_Data);

                // PushButton: Cập Nhật Danh Mục
                PushButtonData cmdHDToolUpdateSheetList_Data = new PushButtonData("cmdHDToolUpdateSheetList", "Cập Nhật\nDanh Mục", assemblyPath, "HDTool.UpdateSheetListCommand");
                cmdHDToolUpdateSheetList_Data.ToolTip = "[HDTOOL] Cập nhật lại tên và thông tin Sheet trong Revit từ file Excel.";
                cmdHDToolUpdateSheetList_Data.LargeImage = ResourceHelper.CreateUpdateSheetListIcon(32);
                cmdHDToolUpdateSheetList_Data.Image = ResourceHelper.CreateUpdateSheetListIcon(16);
                panel_3.AddItem(cmdHDToolUpdateSheetList_Data);

                // PushButton: Xuất CAD Quy Đổi Layer (MỚI THEO LỆNH SẾP HỒ)
                PushButtonData cmdHDToolExportCad_Data = new PushButtonData("cmdHDToolExportCad", "Xuất CAD\nQuy Đổi Layer", assemblyPath, "HDTool.ExportCadCommand");
                cmdHDToolExportCad_Data.ToolTip = "[HDTOOL] Xuất bản vẽ hoặc View đang vẽ sang AutoCAD (.dwg) & tự động quy đổi Layer/Màu/Nét chuẩn hệ thống MEP (Ducts, Pipes, Equipment).";
                cmdHDToolExportCad_Data.LargeImage = ResourceHelper.CreateExportCadIcon(32);
                cmdHDToolExportCad_Data.Image = ResourceHelper.CreateExportCadIcon(16);
                panel_3.AddItem(cmdHDToolExportCad_Data);

                // ==================================================================
                // PANEL 4 : HỆ THỐNG & MENU
                // ==================================================================
                RibbonPanel panel_4 = GetOrCreateRibbonPanel(application, tabName, "Hệ Thống & Menu");

                // PushButton: Cập Nhật Tool
                PushButtonData cmdHDToolUpdateTool_Data = new PushButtonData("cmdHDToolUpdateTool", "Cập Nhật\nTool", assemblyPath, "HDTool.UpdateToolCommand");
                cmdHDToolUpdateTool_Data.ToolTip = "[HDTOOL] Nạp lại tính năng mới / Hot Reload trực tiếp không cần tắt Revit.";
                cmdHDToolUpdateTool_Data.LargeImage = ResourceHelper.CreateUpdateToolIcon(32);
                cmdHDToolUpdateTool_Data.Image = ResourceHelper.CreateUpdateToolIcon(16);
                panel_4.AddItem(cmdHDToolUpdateTool_Data);

                // PushButton: Menu HDTOOL
                PushButtonData cmdHDToolMenu_Data = new PushButtonData("cmdHDToolMenu", "Menu\nHDTOOL", assemblyPath, "HDTool.HDToolMainCommand");
                cmdHDToolMenu_Data.ToolTip = "[HDTOOL] Bảng điều khiển trung tâm: Tự do lựa chọn công cụ mong muốn.";
                cmdHDToolMenu_Data.LargeImage = ResourceHelper.CreateHDToolMenuIcon(32);
                cmdHDToolMenu_Data.Image = ResourceHelper.CreateHDToolMenuIcon(16);
                panel_4.AddItem(cmdHDToolMenu_Data);

                // Dam bao CLR nhan dien Assembly dong cho Hot Reload
                ToolHotReloadManager.EnsureAssemblyResolve();

                // Dang ky tu dong kiem tra va can khop ty le Bang Khoi Luong khi kich hoat View/Sheet
                try
                {
                    application.ViewActivated += OnViewActivated;
                }
                catch { }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Loi Khoi Dong HDTOOL", ex.Message);
                return Result.Failed;
            }
        }

        /// <summary>
        /// Bơm nút Tự Động Crop View vào Ribbon Panel động khi Revit đang mở (Hot Reload Ribbon Injection)
        /// </summary>
        public static void EnsureAutoCropButtonDynamic(UIApplication uiapp)
        {
            try
            {
                if (uiapp == null) return;
                string tabName = "HDTOOL";
                List<RibbonPanel> panels = uiapp.GetRibbonPanels(tabName);
                if (panels == null) return;

                foreach (RibbonPanel p in panels)
                {
                    if (p.Name.Equals("Căn Chỉnh Đối Tượng", StringComparison.OrdinalIgnoreCase))
                    {
                        bool hasAutoCrop = false;
                        IList<RibbonItem> items = p.GetItems();
                        if (items != null)
                        {
                            foreach (RibbonItem it in items)
                            {
                                if (it != null && (it.Name.Equals("cmdHDToolAutoCrop", StringComparison.OrdinalIgnoreCase) ||
                                    it.ItemText.Contains("Crop View")))
                                {
                                    hasAutoCrop = true;
                                    break;
                                }
                            }
                        }

                        if (!hasAutoCrop)
                        {
                            string assemblyPath = string.Empty;
                            // 1. Lấy chính xác đường dẫn Assembly gốc từ các nút có sẵn trên Panel để tương thích 100% với LoadContext của Revit
                            foreach (RibbonPanel pnlCheck in panels)
                            {
                                foreach (RibbonItem ri in pnlCheck.GetItems())
                                {
                                    PushButton pb = ri as PushButton;
                                    if (pb != null && !string.IsNullOrEmpty(pb.AssemblyName) && System.IO.File.Exists(pb.AssemblyName))
                                    {
                                        assemblyPath = pb.AssemblyName;
                                        break;
                                    }
                                    SplitButton sb = ri as SplitButton;
                                    if (sb != null)
                                    {
                                        foreach (PushButton subPb in sb.GetItems())
                                        {
                                            if (subPb != null && !string.IsNullOrEmpty(subPb.AssemblyName) && System.IO.File.Exists(subPb.AssemblyName))
                                            {
                                                assemblyPath = subPb.AssemblyName;
                                                break;
                                            }
                                        }
                                    }
                                    if (!string.IsNullOrEmpty(assemblyPath)) break;
                                }
                                if (!string.IsNullOrEmpty(assemblyPath)) break;
                            }

                            if (string.IsNullOrEmpty(assemblyPath) || !System.IO.File.Exists(assemblyPath))
                            {
                                assemblyPath = Assembly.GetExecutingAssembly().Location;
                            }
                            if (string.IsNullOrEmpty(assemblyPath) || !System.IO.File.Exists(assemblyPath))
                            {
                                assemblyPath = @"c:\Users\Nguye\Desktop\ADDIN REVIT\Build\HDTOOL.dll";
                            }

                            PushButtonData btnData = new PushButtonData(
                                "cmdHDToolAutoCrop",
                                "Tự Động\nCrop View",
                                assemblyPath,
                                "HDTool.AutoCropCommand"
                            );
                            btnData.ToolTip = "[HDTOOL] Tự động tạo và căn chỉnh Crop View bao quanh toàn bộ các đối tượng có trong View vừa tạo (Sơ đồ không gian 3D, Mặt bằng, Mặt cắt, Viewport trên Sheet).";
                            btnData.LargeImage = ResourceHelper.CreateAutoCropIcon(32);
                            btnData.Image = ResourceHelper.CreateAutoCropIcon(16);
                            p.AddItem(btnData);
                        }
                        break;
                    }
                }
                EnsureCadToDuctButtonDynamic(uiapp, panels);
                EnsureExportCadButtonDynamic(uiapp, panels);
            }
            catch { }
        }

        /// <summary>
        /// Bơm nút Dựng Ống Gió Từ CAD Link vào Panel Khối Lượng MEP khi Hot Reload
        /// </summary>
        public static void EnsureCadToDuctButtonDynamic(UIApplication uiapp, List<RibbonPanel> panels = null)
        {
            try
            {
                if (uiapp == null) return;
                if (panels == null) panels = uiapp.GetRibbonPanels("HDTOOL");
                if (panels == null) return;

                foreach (RibbonPanel p in panels)
                {
                    if (p.Name.Equals("Khối Lượng MEP", StringComparison.OrdinalIgnoreCase))
                    {
                        bool hasCadToDuct = false;
                        foreach (RibbonItem it in p.GetItems())
                        {
                            if (it != null && (it.Name.Equals("cmdHDToolCadToDuct", StringComparison.OrdinalIgnoreCase) ||
                                it.ItemText.Contains("Từ CAD")))
                            {
                                hasCadToDuct = true;
                                break;
                            }
                        }

                        if (!hasCadToDuct)
                        {
                            string assemblyPath = Assembly.GetExecutingAssembly().Location;
                            foreach (RibbonItem ri in p.GetItems())
                            {
                                PushButton pb = ri as PushButton;
                                if (pb != null && !string.IsNullOrEmpty(pb.AssemblyName) && System.IO.File.Exists(pb.AssemblyName))
                                {
                                    assemblyPath = pb.AssemblyName;
                                    break;
                                }
                            }
                            if (string.IsNullOrEmpty(assemblyPath) || !System.IO.File.Exists(assemblyPath))
                            {
                                assemblyPath = @"c:\Users\Nguye\Desktop\ADDIN REVIT\Build\HDTOOL.dll";
                            }

                            PushButtonData btnData = new PushButtonData("cmdHDToolCadToDuct", "Dựng Ống Gió\nTừ CAD", assemblyPath, "HDTool.CadToDuctCommand");
                            btnData.ToolTip = "[HDTOOL] Tự động nhận diện tuyến ống, size WxH, cút, côn đồng tâm, chân rẽ Tap và dựng lại mô hình 3D ống gió hoàn chỉnh từ bản vẽ CAD Link.";
                            btnData.LargeImage = ResourceHelper.CreateCadToDuctIcon(32);
                            btnData.Image = ResourceHelper.CreateCadToDuctIcon(16);
                            p.AddItem(btnData);
                        }
                        break;
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Bơm nút Xuất CAD Quy Đổi Layer vào Panel Quản Lý Bản Vẽ & Sheet khi Hot Reload
        /// </summary>
        public static void EnsureExportCadButtonDynamic(UIApplication uiapp, List<RibbonPanel> panels = null)
        {
            try
            {
                if (uiapp == null) return;
                if (panels == null) panels = uiapp.GetRibbonPanels("HDTOOL");
                if (panels == null) return;

                foreach (RibbonPanel p in panels)
                {
                    if (p.Name.Equals("Quản Lý Bản Vẽ & Sheet", StringComparison.OrdinalIgnoreCase))
                    {
                        bool hasExportCad = false;
                        foreach (RibbonItem it in p.GetItems())
                        {
                            if (it != null && (it.Name.Equals("cmdHDToolExportCad", StringComparison.OrdinalIgnoreCase) ||
                                it.ItemText.Contains("Xuất CAD")))
                            {
                                hasExportCad = true;
                                break;
                            }
                        }

                        if (!hasExportCad)
                        {
                            string assemblyPath = Assembly.GetExecutingAssembly().Location;
                            foreach (RibbonItem ri in p.GetItems())
                            {
                                PushButton pb = ri as PushButton;
                                if (pb != null && !string.IsNullOrEmpty(pb.AssemblyName) && System.IO.File.Exists(pb.AssemblyName))
                                {
                                    assemblyPath = pb.AssemblyName;
                                    break;
                                }
                            }
                            if (string.IsNullOrEmpty(assemblyPath) || !System.IO.File.Exists(assemblyPath))
                            {
                                assemblyPath = @"c:\Users\Nguye\Desktop\ADDIN REVIT\Build\HDTOOL.dll";
                            }

                            PushButtonData btnData = new PushButtonData("cmdHDToolExportCad", "Xuất CAD\nQuy Đổi Layer", assemblyPath, "HDTool.ExportCadCommand");
                            btnData.ToolTip = "[HDTOOL] Xuất bản vẽ hoặc View đang vẽ sang AutoCAD (.dwg) & tự động quy đổi Layer/Màu/Nét chuẩn hệ thống MEP (Ducts, Pipes, Equipment).";
                            btnData.LargeImage = ResourceHelper.CreateExportCadIcon(32);
                            btnData.Image = ResourceHelper.CreateExportCadIcon(16);
                            p.AddItem(btnData);
                        }
                        break;
                    }
                }
            }
            catch { }
        }

        private void OnViewActivated(object sender, Autodesk.Revit.UI.Events.ViewActivatedEventArgs e)
        {
            try
            {
                if (e == null || e.Document == null) return;
                Autodesk.Revit.DB.View curView = e.CurrentActiveView;
                if (curView != null && (curView is ViewSheet || curView is ViewDrafting))
                {
                    MepQuantityService.CheckAndRescaleAllTables(e.Document, curView);
                }
            }
            catch { }
        }

        private RibbonPanel GetOrCreateRibbonPanel(UIControlledApplication application, string tabName, string panelName)
        {
            List<RibbonPanel> panels = application.GetRibbonPanels(tabName);
            foreach (RibbonPanel p in panels)
            {
                if (p.Name.Equals(panelName, StringComparison.OrdinalIgnoreCase))
                {
                    return p;
                }
            }
            return application.CreateRibbonPanel(tabName, panelName);
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
