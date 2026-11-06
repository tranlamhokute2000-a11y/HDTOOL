using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace HDTool
{
    /// <summary>
    /// Đối tượng lưu trữ một dòng thống kê khối lượng ống gió hoặc phụ kiện HVAC
    /// Đầy đủ: Số Lượng, Diện Tích m² tôn (TCVN 5687:2010), Hệ Thống và Custom Parameters dự án
    /// </summary>
    public class DuctQuantityItem
    {
        public int STT { get; set; }

        /// <summary>
        /// Tên cấu kiện / phụ kiện / van / miệng gió đã chuẩn hóa
        /// </summary>
        public string MergedName { get; set; }

        /// <summary>
        /// Kích thước / Tiết diện (Size: ví dụ 800x400, Ø300, 800x400 - 600x350...)
        /// </summary>
        public string Size { get; set; }

        /// <summary>
        /// Hình dạng tiết diện: Chữ Nhật, Tròn, Oval
        /// </summary>
        public string Shape { get; set; }

        /// <summary>
        /// Chiều dài (m) của 1 đoạn ống gió hoặc phụ kiện
        /// </summary>
        public double LengthM { get; set; }

        /// <summary>
        /// Tổng chiều dài (m) của cả nhóm
        /// </summary>
        public double TotalLengthM { get; set; }

        /// <summary>
        /// Diện tích tôn m² của 1 cấu kiện (tính chuẩn theo TCVN 5687:2010 / SMACNA)
        /// </summary>
        public double AreaM2 { get; set; }

        /// <summary>
        /// Tổng diện tích tôn m² của cả nhóm
        /// </summary>
        public double TotalAreaM2 { get; set; }

        /// <summary>
        /// Số lượng: đoạn đối với ống gió; cái / chiếc / bộ đối với phụ kiện, van, miệng gió
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Đơn vị tính: đoạn, cái, bộ, chiếc...
        /// </summary>
        public string Unit { get; set; }

        /// <summary>
        /// Phân loại hệ thống (System Type / Classification: ví dụ Cấp Gió SA, Hồi Gió RA, Hút Khí Thải EA, Gió Tươi OA, Hút Khói...)
        /// </summary>
        public string SystemType { get; set; }

        /// <summary>
        /// Độ dày tôn mạ kẽm (mm) tính theo TCVN 5687:2010 hoặc trích xuất từ Revit parameter
        /// </summary>
        public string Thickness { get; set; }

        /// <summary>
        /// Tên tầng / Cao độ (Level)
        /// </summary>
        public string LocationByLevel { get; set; }

        /// <summary>
        /// Phân loại: Ống Gió Thẳng, Phụ Kiện Ống Gió, Van Gió, Miệng Gió, Ống Gió Mềm
        /// </summary>
        public string CategoryKind { get; set; }

        /// <summary>
        /// Danh sách ElementId thực tế trong Revit của nhóm này (phục vụ soi sáng mô hình 2 chiều)
        /// </summary>
        public List<ElementId> ElementIds { get; set; }

        /// <summary>
        /// Lưu trữ các tham số dự án tùy chỉnh do người dùng thêm vào bảng (Project / Shared / Family Parameters)
        /// </summary>
        public Dictionary<string, string> CustomParameters { get; set; }

        public DuctQuantityItem()
        {
            MergedName = string.Empty;
            Size = string.Empty;
            Shape = "Chữ Nhật";
            LengthM = 0.0;
            TotalLengthM = 0.0;
            AreaM2 = 0.0;
            TotalAreaM2 = 0.0;
            Count = 0;
            Unit = "đoạn";
            SystemType = string.Empty;
            Thickness = "0.75 mm";
            LocationByLevel = string.Empty;
            CategoryKind = "Ống Gió Thẳng";
            ElementIds = new List<ElementId>();
            CustomParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Khóa gom nhóm để tổng hợp số lượng (Count), chiều dài (LengthM) và diện tích tôn (AreaM2)
        /// </summary>
        public string GetGroupingKey()
        {
            return string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}",
                (MergedName ?? "").Trim().ToLowerInvariant(),
                (Size ?? "").Trim().ToLowerInvariant(),
                (Shape ?? "").Trim().ToLowerInvariant(),
                (SystemType ?? "").Trim().ToLowerInvariant(),
                (Thickness ?? "").Trim().ToLowerInvariant(),
                (LocationByLevel ?? "").Trim().ToLowerInvariant(),
                (CategoryKind ?? "").Trim().ToLowerInvariant()
            );
        }
    }
}

