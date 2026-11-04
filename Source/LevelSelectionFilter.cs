using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace HDTool
{
    /// <summary>
    /// Bộ lọc chọn đối tượng: Cho phép chọn Level (trong Section/Elevation) hoặc Viewport (trên Sheet)
    /// </summary>
    public class LevelSelectionFilter : ISelectionFilter
    {
        private readonly bool _isSheet;

        public LevelSelectionFilter(bool isSheet = false)
        {
            _isSheet = isSheet;
        }

        public bool AllowElement(Element elem)
        {
            if (elem == null) return false;

            if (_isSheet)
            {
                // Trên Sheet, người dùng chọn các Viewport (H12, H14...)
                return elem is Viewport;
            }

            // Trong View thường, người dùng chọn các Level
            return elem is Level;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}
