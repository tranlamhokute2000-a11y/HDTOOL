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
                    PushButtonData cmdCropAlignUnified_Data = new PushButtonData("cmdCropAlignUnified", "Căn Chỉnh Grids / Level", assemblyPath, "HDTool.CropAlignCommand");
                    cmdCropAlignUnified_Data.LargeImage = ResourceHelper.CreateCropAlignCombinedIcon(32);
                    cmdCropAlignUnified_Data.Image = ResourceHelper.CreateCropAlignCombinedIcon(16);
                    cmdHDToolCropAlignSplit_Btn.AddPushButton(cmdCropAlignUnified_Data);
                    PushButtonData cmdGridCrop_Item_Data = new PushButtonData("cmdGridCrop_Item", "Căn Chỉnh Grids", assemblyPath, "HDTool.GridCropCommand");
                    cmdGridCrop_Item_Data.LargeImage = ResourceHelper.CreateGridCropIcon(32);
                    cmdGridCrop_Item_Data.Image = ResourceHelper.CreateGridCropIcon(16);
                    cmdHDToolCropAlignSplit_Btn.AddPushButton(cmdGridCrop_Item_Data);
                    PushButtonData cmdLevelCrop_Item_Data = new PushButtonData("cmdLevelCrop_Item", "Căn Chỉnh Level", assemblyPath, "HDTool.LevelCropCommand");
                    cmdLevelCrop_Item_Data.LargeImage = ResourceHelper.CreateLevelCropIcon(32);
                    cmdLevelCrop_Item_Data.Image = ResourceHelper.CreateLevelCropIcon(16);
                    cmdHDToolCropAlignSplit_Btn.AddPushButton(cmdLevelCrop_Item_Data);
                }

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
