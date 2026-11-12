using System;
using System.IO;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace HDTool
{
    /// <summary>
    /// Quản lý Hot Reload & Cập nhật Add-in trực tiếp khi Revit đang mở.
    /// Cho phép nạp DLL mới vào bộ nhớ (in-memory) mà không bị Windows khóa file,
    /// đồng thời điều hướng (dispatch) các lệnh Revit sang phiên bản mới nhất.
    /// </summary>
    public static class ToolHotReloadManager
    {
        private const string APPDOMAIN_KEY_ASSEMBLY = "HDTOOL_ACTIVE_ASSEMBLY";
        private const string APPDOMAIN_KEY_PATH = "HDTOOL_ACTIVE_PATH";
        private const string APPDOMAIN_KEY_TIME = "HDTOOL_ACTIVE_TIME";

        private static bool _resolveAttached = false;

        public static Assembly ActiveAssembly
        {
            get
            {
                return AppDomain.CurrentDomain.GetData(APPDOMAIN_KEY_ASSEMBLY) as Assembly;
            }
            private set
            {
                AppDomain.CurrentDomain.SetData(APPDOMAIN_KEY_ASSEMBLY, value);
            }
        }

        public static string ActivePath
        {
            get
            {
                object obj = AppDomain.CurrentDomain.GetData(APPDOMAIN_KEY_PATH);
                return obj != null ? obj.ToString() : null;
            }
            private set
            {
                AppDomain.CurrentDomain.SetData(APPDOMAIN_KEY_PATH, value);
            }
        }

        public static DateTime? ActiveTime
        {
            get
            {
                object obj = AppDomain.CurrentDomain.GetData(APPDOMAIN_KEY_TIME);
                if (obj is DateTime)
                {
                    return (DateTime)obj;
                }
                return null;
            }
            private set
            {
                AppDomain.CurrentDomain.SetData(APPDOMAIN_KEY_TIME, value);
            }
        }

        /// <summary>
        /// Đảm bảo CLR tự động nhận diện các kiểu dữ liệu của HDTOOL từ assembly mới nạp
        /// </summary>
        public static void EnsureAssemblyResolve()
        {
            if (!_resolveAttached)
            {
                AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
                _resolveAttached = true;
            }
        }

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            if (string.IsNullOrEmpty(args.Name)) return null;

            Assembly active = ActiveAssembly;
            if (active != null && args.Name.StartsWith("HDTOOL", StringComparison.OrdinalIgnoreCase))
            {
                return active;
            }

            // Đảm bảo tương thích chéo 100% giữa các phiên bản Revit (Revit 2024, 2025, 2026, 2027...)
            if (args.Name.StartsWith("RevitAPI", StringComparison.OrdinalIgnoreCase) ||
                args.Name.StartsWith("RevitAPIUI", StringComparison.OrdinalIgnoreCase))
            {
                string simpleName = args.Name.Split(',')[0].Trim();
                foreach (Assembly loadedAsm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (loadedAsm.GetName().Name.Equals(simpleName, StringComparison.OrdinalIgnoreCase))
                    {
                        return loadedAsm;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Nạp file DLL mới vào Revit từ mảng byte (không khóa file trên ổ cứng)
        /// </summary>
        public static bool ReloadAssembly(string dllPath, out string statusMessage)
        {
            statusMessage = string.Empty;

            if (string.IsNullOrEmpty(dllPath) || !File.Exists(dllPath))
            {
                statusMessage = "Đường dẫn file không hợp lệ hoặc không tồn tại:\n" + dllPath;
                return false;
            }

            try
            {
                // 1. Đọc toàn bộ nội dung file nhị phân vào RAM để không khóa file
                byte[] dllBytes = File.ReadAllBytes(dllPath);

                byte[] pdbBytes = null;
                string pdbPath = Path.ChangeExtension(dllPath, ".pdb");
                if (File.Exists(pdbPath))
                {
                    try
                    {
                        pdbBytes = File.ReadAllBytes(pdbPath);
                    }
                    catch
                    {
                        // Bỏ qua PDB nếu đang bị khóa
                    }
                }

                // 2. Nạp Assembly vào AppDomain
                Assembly newAssembly;
                if (pdbBytes != null && pdbBytes.Length > 0)
                {
                    newAssembly = Assembly.Load(dllBytes, pdbBytes);
                }
                else
                {
                    newAssembly = Assembly.Load(dllBytes);
                }

                // 3. Kiểm tra tính hợp lệ của Assembly (phải chứa các lệnh HDTOOL)
                Type gridType = newAssembly.GetType("HDTool.GridCropCommand");
                if (gridType == null)
                {
                    bool hasHdTool = false;
                    foreach (Type t in newAssembly.GetExportedTypes())
                    {
                        if (t.Namespace != null && t.Namespace.StartsWith("HDTool", StringComparison.OrdinalIgnoreCase))
                        {
                            hasHdTool = true;
                            break;
                        }
                    }

                    if (!hasHdTool)
                    {
                        statusMessage = "File DLL đã chọn không phải là bộ công cụ HDTOOL hợp lệ!";
                        return false;
                    }
                }

                // 4. Lưu trạng thái vào AppDomain
                ActiveAssembly = newAssembly;
                ActivePath = dllPath;
                ActiveTime = DateTime.Now;

                // 5. Đăng ký AssemblyResolve
                EnsureAssemblyResolve();

                // 6. Đồng bộ sang thư mục Addins của Revit để lần sau khởi động vẫn dùng bản mới
                SyncToRevitAddinsFolder(dllPath);

                statusMessage = string.Format(
                    "Đã nạp thành công phiên bản mới ({0:N0} bytes) vào Revit!",
                    dllBytes.Length
                );
                return true;
            }
            catch (Exception ex)
            {
                statusMessage = "Lỗi khi nạp file DLL vào Revit:\n" + ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Sao chép file DLL mới sang các thư mục Addin của Revit (2025, 2024...) để lưu trữ và hot reload
        /// </summary>
        public static void SyncToRevitAddinsFolder(string sourcePath)
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string[] targetYears = new string[] { "2025", "2024", "2026", "2027", "2023", "2022" };

                foreach (string yr in targetYears)
                {
                    try
                    {
                        string yearDir = Path.Combine(appData, @"Autodesk\Revit\Addins", yr);
                        if (Directory.Exists(yearDir))
                        {
                            string targetDir = Path.Combine(yearDir, "HDTOOL");
                            if (!Directory.Exists(targetDir))
                            {
                                Directory.CreateDirectory(targetDir);
                            }

                            string targetDll = Path.Combine(targetDir, "HDTOOL.dll");

                            try
                            {
                                File.Copy(sourcePath, targetDll, true);
                            }
                            catch
                            {
                                try
                                {
                                    string backupDll = Path.Combine(targetDir, "HDTOOL_old_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".dll");
                                    File.Move(targetDll, backupDll);
                                    File.Copy(sourcePath, targetDll, true);
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        /// <summary>
        /// Tìm file HDTOOL.dll mới nhất trên máy tính (Build folder, Desktop, Addins 2025/2024)
        /// </summary>
        public static FileInfo FindLatestBuildDll()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string[] searchPaths = new string[]
            {
                @"c:\Users\Nguye\Desktop\ADDIN REVIT\Build\HDTOOL.dll",
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HDTOOL.dll"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "HDTOOL.dll"),
                Path.Combine(appData, @"Autodesk\Revit\Addins\2025\HDTOOL\HDTOOL.dll"),
                Path.Combine(appData, @"Autodesk\Revit\Addins\2024\HDTOOL\HDTOOL.dll")
            };

            FileInfo newest = null;
            foreach (string p in searchPaths)
            {
                try
                {
                    if (File.Exists(p))
                    {
                        FileInfo fi = new FileInfo(p);
                        if (newest == null || fi.LastWriteTimeUtc > newest.LastWriteTimeUtc)
                        {
                            newest = fi;
                        }
                    }
                }
                catch { }
            }

            return newest;
        }

        /// <summary>
        /// Kiểm tra xem có phiên bản Hot Reload nào đang hoạt động khác với assembly hiện tại hay không
        /// </summary>
        public static bool HasActiveHotReloadAssembly()
        {
            try
            {
                FileInfo newest = FindLatestBuildDll();
                if (newest != null && newest.Exists)
                {
                    DateTime? activeDt = ActiveTime;
                    if (ActiveAssembly == null || !activeDt.HasValue || newest.LastWriteTime > activeDt.Value.AddSeconds(2))
                    {
                        string msg;
                        ReloadAssembly(newest.FullName, out msg);
                    }
                }
            }
            catch { }

            Assembly active = ActiveAssembly;
            return active != null && active != Assembly.GetExecutingAssembly();
        }

        /// <summary>
        /// Điều hướng thực thi lệnh sang Assembly mới nhất nếu đã được hot reload
        /// </summary>
        public static Result ExecuteDynamicCommand(
            string commandTypeName,
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            Assembly active = ActiveAssembly;
            if (active == null || active == Assembly.GetExecutingAssembly())
            {
                return Result.Failed;
            }

            try
            {
                Type targetType = active.GetType(commandTypeName);
                if (targetType != null)
                {
                    object instance = Activator.CreateInstance(targetType);
                    MethodInfo mi = targetType.GetMethod("ExecuteDirect") ?? targetType.GetMethod("Execute");
                    if (mi != null)
                    {
                        object[] args = new object[] { commandData, message, elements };
                        object res = mi.Invoke(instance, args);
                        if (args[1] is string)
                        {
                            message = (string)args[1];
                        }
                        return (Result)res;
                    }
                }
            }
            catch (TargetInvocationException tie)
            {
                Exception inner = tie.InnerException != null ? tie.InnerException : tie;
                message = inner.Message;
                TaskDialog.Show("HDTOOL - Lỗi Thực Thi Lệnh Đã Cập Nhật", inner.Message + "\n" + inner.StackTrace);
                return Result.Failed;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("HDTOOL - Lỗi Điều Hướng Lệnh", ex.Message);
                return Result.Failed;
            }

            return Result.Failed;
        }
    }
}
