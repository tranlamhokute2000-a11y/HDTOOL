using System;
using System.Collections.Generic;
using System.Drawing;
using Autodesk.Revit.DB;

namespace HDTool
{
    /// <summary>
    /// Nhóm danh mục Family để lọc nhanh trên Palette
    /// </summary>
    public enum FamilyCategoryGroup
    {
        All = 0,                // Tất Cả
        Ducts = 1,              // Ống Gió & Miệng Gió
        DuctFittings = 2,        // Phụ Kiện Ống Gió
        Pipes = 3,              // Ống Nước & Van
        PipeFittings = 4,       // Phụ Kiện Ống Nước
        MechanicalEquipment = 5,// Thiết Bị Cơ Điện (AHU, FCU, Bơm, Chiller...)
        Electrical = 6,         // Thiết Bị Điện & Chiếu Sáng
        Plumbing = 7,           // Thiết Bị Vệ Sinh & PCCC (Sprinkler...)
        Architecture = 8,       // Kiến Trúc (Cửa, Nội thất...)
        Structure = 9,          // Kết Cấu (Cột, Dầm, Móng...)
        Other = 10              // Danh Mục Khác
    }

    /// <summary>
    /// Bản ghi đại diện cho một Kiểu Family (FamilySymbol / Type) trong dự án Revit
    /// </summary>
    public class FamilyTypeItem
    {
        public ElementId Id { get; set; }
        public string TypeName { get; set; }
        public string FamilyName { get; set; }
        public string CategoryName { get; set; }
        public BuiltInCategory BuiltInCat { get; set; }
        public FamilyCategoryGroup CategoryGroup { get; set; }
        public bool IsActive { get; set; }
        public int InstanceCount { get; set; }
        public Bitmap PreviewImage { get; set; }

        public FamilyTypeItem()
        {
            this.Id = ElementId.InvalidElementId;
            this.TypeName = string.Empty;
            this.FamilyName = string.Empty;
            this.CategoryName = string.Empty;
            this.BuiltInCat = BuiltInCategory.INVALID;
            this.CategoryGroup = FamilyCategoryGroup.All;
            this.IsActive = true;
            this.InstanceCount = 0;
            this.PreviewImage = null;
        }

        public FamilyTypeItem(
            ElementId id,
            string typeName,
            string familyName,
            string categoryName,
            BuiltInCategory builtInCat,
            FamilyCategoryGroup categoryGroup,
            bool isActive,
            int instanceCount,
            Bitmap previewImage)
        {
            this.Id = id;
            this.TypeName = typeName != null ? typeName : string.Empty;
            this.FamilyName = familyName != null ? familyName : string.Empty;
            this.CategoryName = categoryName != null ? categoryName : string.Empty;
            this.BuiltInCat = builtInCat;
            this.CategoryGroup = categoryGroup;
            this.IsActive = isActive;
            this.InstanceCount = instanceCount;
            this.PreviewImage = previewImage;
        }

        public string GetFullDisplayName()
        {
            if (string.IsNullOrEmpty(this.TypeName) || this.TypeName.Equals(this.FamilyName, StringComparison.OrdinalIgnoreCase))
            {
                return this.FamilyName;
            }
            return string.Format("{0} : {1}", this.FamilyName, this.TypeName);
        }
    }

    /// <summary>
    /// Nhóm Family chứa danh sách các Type tương ứng để hiển thị dạng Card trên Palette
    /// </summary>
    public class FamilyGroupItem
    {
        public string FamilyName { get; set; }
        public string CategoryName { get; set; }
        public BuiltInCategory BuiltInCat { get; set; }
        public FamilyCategoryGroup CategoryGroup { get; set; }
        public ElementId FamilyId { get; set; }
        public List<FamilyTypeItem> Types { get; set; }
        public Bitmap RepresentativePreview { get; set; }
        public int TotalInstances { get; set; }

        public FamilyGroupItem()
        {
            this.FamilyName = string.Empty;
            this.CategoryName = string.Empty;
            this.BuiltInCat = BuiltInCategory.INVALID;
            this.CategoryGroup = FamilyCategoryGroup.All;
            this.FamilyId = ElementId.InvalidElementId;
            this.Types = new List<FamilyTypeItem>();
            this.RepresentativePreview = null;
            this.TotalInstances = 0;
        }

        public FamilyGroupItem(
            string familyName,
            string categoryName,
            BuiltInCategory builtInCat,
            FamilyCategoryGroup categoryGroup,
            ElementId familyId)
        {
            this.FamilyName = familyName != null ? familyName : string.Empty;
            this.CategoryName = categoryName != null ? categoryName : string.Empty;
            this.BuiltInCat = builtInCat;
            this.CategoryGroup = categoryGroup;
            this.FamilyId = familyId;
            this.Types = new List<FamilyTypeItem>();
            this.RepresentativePreview = null;
            this.TotalInstances = 0;
        }

        public FamilyTypeItem GetDefaultType()
        {
            if (this.Types != null && this.Types.Count > 0)
            {
                return this.Types[0];
            }
            return null;
        }
    }

    /// <summary>
    /// Bộ lọc tìm kiếm và phân loại Family trên Palette
    /// </summary>
    public class FamilyPaletteFilter
    {
        public string SearchKeyword { get; set; }
        public FamilyCategoryGroup SelectedGroup { get; set; }
        public bool OnlyWithInstances { get; set; }

        public FamilyPaletteFilter()
        {
            this.SearchKeyword = string.Empty;
            this.SelectedGroup = FamilyCategoryGroup.All;
            this.OnlyWithInstances = false;
        }
    }
}
