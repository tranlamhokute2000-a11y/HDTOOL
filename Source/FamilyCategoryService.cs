using System;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.Revit.DB;

namespace HDTool
{
    /// <summary>1 Category đích có thể chọn khi đổi Category cho Family.</summary>
    public class FamilyCategoryChoice
    {
        public BuiltInCategory Bic { get; private set; }
        public string Name { get; private set; }

        public FamilyCategoryChoice(BuiltInCategory bic, string name)
        {
            this.Bic = bic;
            this.Name = name;
        }

        public override string ToString()
        {
            return this.Name;
        }
    }

    /// <summary>
    /// Đổi Category của Family đã load mà người dùng không phải mở Family:
    /// EditFamily (mở ngầm trong bộ nhớ) -> đổi FamilyCategory -> nạp đè vào dự án -> đóng, không lưu file.
    /// </summary>
    public static class FamilyCategoryService
    {
        // Các Category dạng Model mà Family tải được (loadable) thường chuyển qua lại được
        private static readonly BuiltInCategory[] TargetCategories = new BuiltInCategory[]
        {
            BuiltInCategory.OST_DuctAccessory,
            BuiltInCategory.OST_DuctFitting,
            BuiltInCategory.OST_DuctTerminal,
            BuiltInCategory.OST_MechanicalEquipment,
            BuiltInCategory.OST_PipeAccessory,
            BuiltInCategory.OST_PipeFitting,
            BuiltInCategory.OST_PlumbingFixtures,
            BuiltInCategory.OST_Sprinklers,
            BuiltInCategory.OST_ElectricalEquipment,
            BuiltInCategory.OST_ElectricalFixtures,
            BuiltInCategory.OST_LightingFixtures,
            BuiltInCategory.OST_LightingDevices,
            BuiltInCategory.OST_CableTrayFitting,
            BuiltInCategory.OST_ConduitFitting,
            BuiltInCategory.OST_CommunicationDevices,
            BuiltInCategory.OST_DataDevices,
            BuiltInCategory.OST_FireAlarmDevices,
            BuiltInCategory.OST_NurseCallDevices,
            BuiltInCategory.OST_SecurityDevices,
            BuiltInCategory.OST_TelephoneDevices,
            BuiltInCategory.OST_GenericModel,
            BuiltInCategory.OST_SpecialityEquipment,
            BuiltInCategory.OST_Furniture,
            BuiltInCategory.OST_FurnitureSystems,
            BuiltInCategory.OST_Casework,
            BuiltInCategory.OST_Entourage,
            BuiltInCategory.OST_Planting,
            BuiltInCategory.OST_Parking,
            BuiltInCategory.OST_Site
        };

        /// <summary>Danh sách Category đích, tên lấy đúng theo ngôn ngữ Revit đang dùng, xếp A-Z.</summary>
        public static List<FamilyCategoryChoice> GetChoices(Document doc)
        {
            List<FamilyCategoryChoice> list = new List<FamilyCategoryChoice>();
            if (doc == null) return list;
            foreach (BuiltInCategory bic in TargetCategories)
            {
                try
                {
                    Category c = Category.GetCategory(doc, bic);
                    if (c != null) list.Add(new FamilyCategoryChoice(bic, c.Name));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Bỏ qua Category " + bic + ": " + ex.Message);
                }
            }
            list.Sort(delegate(FamilyCategoryChoice a, FamilyCategoryChoice b) { return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase); });
            return list;
        }

        /// <summary>Family có đổi Category được không; không được thì trả lý do để hiện cho người dùng.</summary>
        public static bool CanChange(Document doc, ElementId familyId, out string reason)
        {
            reason = null;
            Family family = doc != null ? doc.GetElement(familyId) as Family : null;
            if (family == null) reason = "Không tìm thấy Family trong dự án (bấm Làm Mới).";
            else if (doc.IsReadOnly) reason = "Dự án đang ở chế độ chỉ đọc.";
            else if (family.IsInPlace) reason = "Family vẽ tại chỗ (In-Place) không đổi được.";
            else if (!family.IsEditable) reason = "Family này Revit không cho chỉnh sửa.";
            else if (family.FamilyCategory == null || family.FamilyCategory.CategoryType != CategoryType.Model)
                reason = "Tag / ký hiệu chú thích không đổi Category được.";
            return reason == null;
        }

        /// <summary>
        /// Đổi Category. BẮT BUỘC gọi trong API context (ExternalEvent), không có Transaction nào đang mở.
        /// Thông số Type đang có trong dự án được giữ nguyên.
        /// </summary>
        public static bool Change(Document doc, ElementId familyId, BuiltInCategory target, out string message)
        {
            if (!CanChange(doc, familyId, out message)) return false;
            if (doc.IsModifiable)
            {
                message = "Revit đang bận một lệnh khác, hãy kết thúc lệnh đó rồi thử lại.";
                return false;
            }

            Family family = (Family)doc.GetElement(familyId);
            string familyName = family.Name;
            if (family.FamilyCategory.Id.Value == (long)target)
            {
                message = "Family đã thuộc Category này.";
                return false;
            }

            Stopwatch total = Stopwatch.StartNew();
            long openMs = 0, loadMs = 0;
            Document familyDoc = null;
            try
            {
                familyDoc = doc.EditFamily(family);
                openMs = total.ElapsedMilliseconds;
                Category newCategory = Category.GetCategory(familyDoc, target);
                if (newCategory == null)
                {
                    message = "Family này không có Category đích.";
                    return false;
                }

                using (Transaction t = new Transaction(familyDoc, "HDTOOL - Đổi Category"))
                {
                    t.Start();
                    familyDoc.OwnerFamily.FamilyCategory = newCategory;
                    if (t.Commit() != TransactionStatus.Committed)
                    {
                        message = "Revit từ chối đổi Family sang Category này.";
                        return false;
                    }
                }

                long beforeLoad = total.ElapsedMilliseconds;
                Family loaded = familyDoc.LoadFamily(doc, new KeepProjectValuesLoadOptions());
                loadMs = total.ElapsedMilliseconds - beforeLoad;

                // Kiểm lại trong dự án, không tin vào việc lệnh chạy không báo lỗi
                if (loaded == null || loaded.FamilyCategory == null || loaded.FamilyCategory.Id.Value != (long)target)
                {
                    message = "Revit không nạp được Category mới vào dự án.";
                    return false;
                }

                // Ghi rõ thời gian từng phần để biết chậm ở đâu
                message = string.Format("Đã đổi [{0}] sang {1} trong {2:0.0} giây (mở ngầm Family {3:0.0}s, Revit nạp vào dự án {4:0.0}s).",
                    familyName, loaded.FamilyCategory.Name, total.ElapsedMilliseconds / 1000.0, openMs / 1000.0, loadMs / 1000.0);
                return true;
            }
            catch (Exception ex)
            {
                message = "Không đổi được Category: " + ex.Message;
                return false;
            }
            finally
            {
                if (familyDoc != null)
                {
                    try { familyDoc.Close(false); }
                    catch (Exception ex) { Debug.WriteLine("Đóng Family ngầm lỗi: " + ex.Message); }
                }
            }
        }

        private class KeepProjectValuesLoadOptions : IFamilyLoadOptions
        {
            public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
            {
                overwriteParameterValues = false;
                return true;
            }

            public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)
            {
                source = FamilySource.Project;
                overwriteParameterValues = false;
                return true;
            }
        }
    }
}
