using System;
using System.IO;
using System.Windows.Forms;
using OfficeOpenXml;

namespace SQ_Email_Tools
{
    internal static class Program
    {
        [System.STAThread]
        private static void Main()
        {
            try
            {
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SQ_Email_Tools_StartupError.txt");
                File.WriteAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\n{ex}\r\n\r\n{ex.StackTrace}");
                MessageBox.Show($"程式啟動失敗，錯誤已寫入：\r\n{logPath}", "啟動錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
