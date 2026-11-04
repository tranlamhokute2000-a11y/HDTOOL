using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace HDTool
{
    /// <summary>
    /// Bộ lọc chọn đối tượng: Cho phép chọn Grid hoặc Viewport hoặc khung Crop trên mặt bằng
    /// </summary>
    public class GridSelectionFilter : ISelectionFilter
    {
        private readonly bool _isSheet;

        public GridSelectionFilter(bool isSheet = false)
        {
            _isSheet = isSheet;
        }

        public bool AllowElement(Element elem)
        {
            if (elem == null) return false;

            if (_isSheet)
            {
                return elem is Viewport;
            }

            // Cho phép chọn Grid hoặc View (nếu click vào khung Crop)
            if (elem is Grid) return true;
            if (elem.Category != null && elem.Category.Id == new ElementId(BuiltInCategory.OST_CropBoundary)) return true;

            return false;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}
