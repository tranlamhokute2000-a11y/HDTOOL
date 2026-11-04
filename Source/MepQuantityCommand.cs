using System;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace HDTool
{
    public class RevitWindowWrapper : IWin32Window
    {
        private IntPtr _hwnd;
        public RevitWindowWrapper(IntPtr handle)
        {
            _hwnd = handle;
        }
        public IntPtr Handle
        {
            get { return _hwnd; }
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class MepQuantityCommand : IExternalCommand
    {
        private static MepQuantityInspectorForm _activeForm = null;
        private static ExternalEvent _extEvent = null;
        private static MepInspectorExternalEventHandler _handler = null;

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
                    TaskDialog.Show("HDTOOL", "Vui lòng mở một dự án Revit có chứa mô hình ống nước hoặc phụ kiện.");
                    return Result.Cancelled;
                }

                if (_activeForm != null && !_activeForm.IsDisposed)
                {
                    _activeForm.WindowState = FormWindowState.Normal;
                    _activeForm.BringToFront();
                    _activeForm.Activate();
                    return Result.Succeeded;
                }

                if (_handler == null)
                {
                    _handler = new MepInspectorExternalEventHandler();
                    _extEvent = ExternalEvent.Create(_handler);
                }

                _activeForm = new MepQuantityInspectorForm(uidoc, _extEvent, _handler);
                _activeForm.FormClosed += (s, e) => { _activeForm = null; };

                // Hiển thị dạng Modeless hoặc gắn vào cửa sổ chính của Revit
                IntPtr mainHwnd = uiapp.MainWindowHandle;
                if (mainHwnd != IntPtr.Zero)
                {
                    _activeForm.Show(new RevitWindowWrapper(mainHwnd));
                }
                else
                {
                    _activeForm.Show();
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

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class PickTableHighlightCommand : IExternalCommand
    {
        private static CheckVarPlumbingForm _activeForm = null;
        private static ExternalEvent _extEvent = null;
        private static CheckVarPlumbingExternalEventHandler _handler = null;

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
                    TaskDialog.Show("HDTOOL", "Vui lòng mở một dự án Revit có chứa mô hình hoặc bản vẽ Sheet.");
                    return Result.Cancelled;
                }

                if (_activeForm != null && !_activeForm.IsDisposed)
                {
                    _activeForm.WindowState = FormWindowState.Normal;
                    _activeForm.BringToFront();
                    _activeForm.Activate();
                    _activeForm.LoadActiveSheetData();
                    return Result.Succeeded;
                }

                if (_handler == null)
                {
                    _handler = new CheckVarPlumbingExternalEventHandler();
                    _extEvent = ExternalEvent.Create(_handler);
                }

                _activeForm = new CheckVarPlumbingForm(uidoc, _extEvent, _handler);
                _activeForm.FormClosed += (s, e) => { _activeForm = null; };

                IntPtr mainHwnd = uiapp.MainWindowHandle;
                if (mainHwnd != IntPtr.Zero)
                {
                    _activeForm.Show(new RevitWindowWrapper(mainHwnd));
                }
                else
                {
                    _activeForm.Show();
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

    /// <summary>
    /// Command chính thức theo lệnh Sếp Hồ: "CHECK VAR PLUMBING"
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CheckVarPlumbingCommand : PickTableHighlightCommand
    {
    }
}
