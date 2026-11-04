using System;
using Autodesk.Revit.DB;

namespace HDTool
{
    /// <summary>
    /// Đối tượng lưu trữ thông tin của một Sheet (Bản vẽ) phục vụ hiển thị và xuất danh mục
    /// </summary>
    public class SheetItem
    {
        public bool IsSelected { get; set; }
        public ElementId SheetId { get; set; }
        public string SheetNumber { get; set; }
        public string OriginalSheetNumber { get; set; }
        public string SheetName { get; set; }
        public string OriginalSheetName { get; set; }
        public string GroupTitle { get; set; }
        public string Discipline
        {
            get { return GroupTitle; }
            set { GroupTitle = value; }
        }
        public string Revision { get; set; }
        public string IssueDate { get; set; }
        public string DrawnBy { get; set; }
        public string ApprovedBy { get; set; }
        public string Scale { get; set; }
        public bool IsPlaceholder { get; set; }

        public bool IsNameModified
        {
            get
            {
                return !string.Equals(SheetName ?? string.Empty, OriginalSheetName ?? string.Empty, StringComparison.Ordinal);
            }
        }

        public bool IsNumberModified
        {
            get
            {
                return !string.Equals(SheetNumber ?? string.Empty, OriginalSheetNumber ?? string.Empty, StringComparison.Ordinal);
            }
        }

        public bool IsModified
        {
            get
            {
                return IsNameModified || IsNumberModified;
            }
        }

        public SheetItem()
        {
            IsSelected = true;
            SheetNumber = string.Empty;
            SheetName = string.Empty;
            OriginalSheetName = string.Empty;
            Revision = string.Empty;
            IssueDate = string.Empty;
            DrawnBy = string.Empty;
            ApprovedBy = string.Empty;
            Scale = string.Empty;
            IsPlaceholder = false;
        }

        public override string ToString()
        {
            return string.Format("{0} - {1}", SheetNumber, SheetName);
        }
    }
}
