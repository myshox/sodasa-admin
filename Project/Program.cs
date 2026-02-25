using System.Windows.Forms;
using OfficeOpenXml;

namespace SQ_Email_Tools
{
    internal static class Program
    {
        [System.STAThread]
        private static void Main()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
