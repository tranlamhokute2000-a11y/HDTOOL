using System;
using Autodesk.Revit.DB;

namespace HDTool
{
    public enum SheetUpdateStatus
    {
        Changed,     // Tên hoặc thông tin có thay đổi
        Unchanged,   // Tên và thông tin đã giống nhau trong Revit
        NotFound,    // Số hiệu bản vẽ không tìm thấy trong Revit
        InvalidName  // Tên mới không hợp lệ
    }

    /// <summary>
    /// Đối tượng quản lý dữ liệu so sánh giữa file Excel và bản vẽ trong Revit
    /// </summary>
    public class SheetUpdateItem
    {
        public bool IsSelected { get; set; }
        public ElementId SheetId { get; set; }
        public string SheetNumber { get; set; }
        public string CurrentSheetName { get; set; }
        public string NewSheetName { get; set; }
        public string CurrentRevision { get; set; }
        public string NewRevision { get; set; }
        public string CurrentIssueDate { get; set; }
        public string NewIssueDate { get; set; }
        public string CurrentDrawnBy { get; set; }
        public string NewDrawnBy { get; set; }
        public SheetUpdateStatus Status { get; set; }
        public string Note { get; set; }

        public SheetUpdateItem()
        {
            IsSelected = false;
            SheetNumber = string.Empty;
            CurrentSheetName = string.Empty;
            NewSheetName = string.Empty;
            CurrentRevision = string.Empty;
            NewRevision = string.Empty;
            CurrentIssueDate = string.Empty;
            NewIssueDate = string.Empty;
            CurrentDrawnBy = string.Empty;
            NewDrawnBy = string.Empty;
            Status = SheetUpdateStatus.Unchanged;
            Note = string.Empty;
        }

        public override string ToString()
        {
            return string.Format("{0}: {1} -> {2} ({3})", SheetNumber, CurrentSheetName, NewSheetName, Status);
        }
    }
}
