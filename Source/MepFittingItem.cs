using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace HDTool
{
    /// <summary>
    /// Đối tượng lưu trữ một dòng thống kê khối lượng phụ kiện hoặc ống nước đã được gộp tên chuẩn hóa
    /// </summary>
    public class MepFittingItem
    {
        public int STT { get; set; }

        /// <summary>
        /// Tên phụ kiện đã gộp làm 1 (lấy từ tham số có giá trị đầu tiên trong 3 parameters hoặc fallback)
        /// </summary>
        public string MergedName { get; set; }

        /// <summary>
        /// Tên parameter gốc mà đối tượng đã lấy giá trị (để người dùng dễ kiểm chứng)
        /// </summary>
        public string SourceParamName { get; set; }

        /// <summary>
        /// Kích thước / Tiết diện (Size: ví dụ 60ø-60ø, 110ø-110ø-60ø...)
        /// </summary>
        public string Size { get; set; }

        /// <summary>
        /// Số lượng (Count: ví dụ 10, 2, 1, 6...)
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Chiều dài theo mét (dành cho đường ống Pipes)
        /// </summary>
        public double LengthM { get; set; }

        /// <summary>
        /// Tổng chiều dài theo mét của toàn bộ các đoạn trong nhóm/dòng này
        /// </summary>
        public double TotalLengthM { get; set; }

        /// <summary>
        /// Loại (Type: ví dụ Standard...)
        /// </summary>
        public string TypeName { get; set; }

        /// <summary>
        /// Khu vực (Location by Area: ví dụ TM-DL8...)
        /// </summary>
        public string LocationByArea { get; set; }

        /// <summary>
        /// Cấp áp lực (PN)
        /// </summary>
        public string PN { get; set; }

        /// <summary>
        /// Cao độ / Tầng (Location by Level: ví dụ T5...)
        /// </summary>
        public string LocationByLevel { get; set; }

        /// <summary>
        /// Phân loại hệ thống (System Type / Classification: ví dụ Thoát Nước Mưa, Cấp Nước...)
        /// </summary>
        public string SystemType { get; set; }

        /// <summary>
        /// Chủng loại ("Phụ Kiện Ống" hoặc "Đường Ống")
        /// </summary>
        public string CategoryKind { get; set; }

        /// <summary>
        /// Danh sách ElementId thực tế trong Revit của nhóm này (phục vụ quét sáng đối tượng)
        /// </summary>
        public List<ElementId> ElementIds { get; set; }

        /// <summary>
        /// Lưu trữ các tham số dự án tùy chỉnh do người dùng thêm vào bảng
        /// </summary>
        public Dictionary<string, string> CustomParameters { get; set; }

        public MepFittingItem()
        {
            MergedName = string.Empty;
            SourceParamName = string.Empty;
            Size = string.Empty;
            Count = 0;
            LengthM = 0.0;
            TotalLengthM = 0.0;
            TypeName = string.Empty;
            LocationByArea = string.Empty;
            PN = string.Empty;
            LocationByLevel = string.Empty;
            SystemType = string.Empty;
            CategoryKind = "Phụ Kiện Ống";
            ElementIds = new List<ElementId>();
            CustomParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Khóa gom nhóm để tổng hợp số lượng (Count) hoặc chiều dài
        /// </summary>
        public string GetGroupingKey()
        {
            return string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}",
                (MergedName ?? "").Trim().ToLowerInvariant(),
                (Size ?? "").Trim().ToLowerInvariant(),
                (TypeName ?? "").Trim().ToLowerInvariant(),
                (LocationByArea ?? "").Trim().ToLowerInvariant(),
                (PN ?? "").Trim().ToLowerInvariant(),
                (LocationByLevel ?? "").Trim().ToLowerInvariant(),
                (SystemType ?? "").Trim().ToLowerInvariant(),
                (CategoryKind ?? "").Trim().ToLowerInvariant()
            );
        }
    }
}
