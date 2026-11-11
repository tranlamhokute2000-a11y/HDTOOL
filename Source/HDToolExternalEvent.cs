using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Autodesk.Revit.UI;

namespace HDTool
{
    /// <summary>
    /// Trình xử lý ExternalEvent dùng chung cho tất cả các Tool Modeless của HDTOOL
    /// Cho phép chạy mọi tác vụ Revit API (Transaction, Pick, Selection, Zoom...)
    /// hoàn toàn mượt mà trong khi người dùng vẫn thao tác chuột, pan/zoom bản vẽ 60 FPS
    /// </summary>
    public class HDToolExternalEventHandler : IExternalEventHandler
    {
        private readonly Queue<Action<UIApplication>> _actionQueue = new Queue<Action<UIApplication>>();
        private readonly object _lock = new object();

        public void Run(Action<UIApplication> action, ExternalEvent exEvent)
        {
            if (action == null || exEvent == null) return;
            lock (_lock)
            {
                _actionQueue.Enqueue(action);
            }
            exEvent.Raise();
        }

        public void Execute(UIApplication app)
        {
            List<Action<UIApplication>> actionsToRun = new List<Action<UIApplication>>();
            lock (_lock)
            {
                while (_actionQueue.Count > 0)
                {
                    actionsToRun.Add(_actionQueue.Dequeue());
                }
            }

            foreach (var act in actionsToRun)
            {
                try
                {
                    act(app);
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("HDTOOL", "Lỗi tác vụ: " + ex.Message);
                }
            }
        }

        public string GetName()
        {
            return "HDToolExternalEventHandler";
        }
    }

    /// <summary>
    /// Trình quản lý Modeless Host toàn cục của HDTOOL:
    /// Đảm bảo tất cả các cửa sổ công cụ khi mở lên đều cho phép người dùng
    /// tự do tương tác, pan/zoom, chọn đối tượng trên bản vẽ Revit mà không bị khóa.
    /// Mọi lệnh can thiệp dữ liệu Revit API (Transaction, Pick) đều được nạp vào ExternalEvent an toàn.
    /// </summary>
    public static class HDToolModelessHost
    {
        private static HDToolExternalEventHandler _sharedHandler = null;
        private static ExternalEvent _sharedExternalEvent = null;

        public static void EnsureInitialized()
        {
            if (_sharedHandler == null)
            {
                _sharedHandler = new HDToolExternalEventHandler();
            }
            if (_sharedExternalEvent == null)
            {
                try
                {
                    _sharedExternalEvent = ExternalEvent.Create(_sharedHandler);
                }
                catch
                {
                    // Đã khởi tạo hoặc đang trong luồng không có API context
                }
            }
        }

        public static HDToolExternalEventHandler Handler
        {
            get
            {
                EnsureInitialized();
                return _sharedHandler;
            }
        }

        public static ExternalEvent ExEvent
        {
            get
            {
                EnsureInitialized();
                return _sharedExternalEvent;
            }
        }

        public static void Run(Action<UIApplication> action)
        {
            if (action == null) return;
            EnsureInitialized();
            if (_sharedHandler != null && _sharedExternalEvent != null)
            {
                _sharedHandler.Run(action, _sharedExternalEvent);
            }
            else
            {
                // Fallback nếu không có ExternalEvent (gọi trực tiếp)
                action(null);
            }
        }

        public static void ShowModeless(Form form, IntPtr revitMainHwnd)
        {
            if (form == null || form.IsDisposed) return;

            if (form.WindowState == FormWindowState.Minimized)
            {
                form.WindowState = FormWindowState.Normal;
            }

            if (!form.Visible)
            {
                if (revitMainHwnd != IntPtr.Zero)
                {
                    form.Show(new RevitWindowWrapper(revitMainHwnd));
                }
                else
                {
                    form.Show();
                }
            }

            form.BringToFront();
            form.Activate();
        }
    }
}
