using System;
using System.Collections.Generic;
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
}
