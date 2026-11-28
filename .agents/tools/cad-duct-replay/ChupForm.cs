using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

// Dựng CadToDuctForm ngoài Revit (bỏ qua constructor cần UIDocument) để chụp bố cục bảng
static class Shot
{
    [STAThread]
    static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object s, ResolveEventArgs e)
        {
            string n = new AssemblyName(e.Name).Name;
            string p = System.IO.Path.Combine(@"C:\Program Files\Autodesk\Revit 2024", n + ".dll");
            return System.IO.File.Exists(p) ? Assembly.LoadFrom(p) : null;
        };
        return Run(args[0]);
    }

    static int Run(string outPath)
    {
        Assembly asm = Assembly.LoadFrom(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HDTOOL_KiemTra.dll"));
        Type t = asm.GetType("HDTool.CadToDuctForm");
        Form form = (Form)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(t);
        t.BaseType.GetConstructor(Type.EmptyTypes).Invoke(form, null);
        BindingFlags f = BindingFlags.NonPublic | BindingFlags.Instance;
        t.GetMethod("InitializeComponent", f).Invoke(form, null);

        DataGridView grid = (DataGridView)t.GetField("dgvPreview", f).GetValue(form);
        string[] kinds = { "Ống gió thẳng", "Ống trục đứng" };
        for (int i = 0; i < 20; i++)
        {
            grid.Rows.Add((i + 1).ToString(), kinds[i % 5 == 4 ? 1 : 0], "500 x 350", "3.112 mm", "1.000 mm",
                "Chân rẽ Tap vào sườn #12; Cút chuyển hướng -> #3", "Text CAD: OAD 500x350: EI60");
        }

        foreach (Control c in form.Controls) Console.WriteLine("TRUOC {0} {1} {2} {3}", c.GetType().Name, c.Bounds, c.Anchor, c.Dock); Console.WriteLine("TRUOC client {0} bang {1}", form.ClientSize, grid.Bounds);
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(0, 0);
        form.Show();
        Application.DoEvents();
        Console.WriteLine("Form {0}x{1}  bang {2}x{3}  so dong thay duoc {4}", form.Width, form.Height, grid.Width, grid.Height, grid.DisplayedRowCount(false));
        foreach (DataGridViewColumn c in grid.Columns) Console.WriteLine("  cot rong {0} (toi thieu {1})", c.Width, c.MinimumWidth);
        foreach (Control c in form.Controls) Console.WriteLine("SAU {0} {1}", c.GetType().Name, c.Bounds); Console.WriteLine("SAU client {0}", form.ClientSize);
        using (Bitmap bmp = new Bitmap(form.Width, form.Height))
        {
            form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
            bmp.Save(outPath, ImageFormat.Png);
        }
        form.Close();
        return 0;
    }
}
