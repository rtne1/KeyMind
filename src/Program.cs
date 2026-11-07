using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace KeyMind
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool created;
            using (var single = new Mutex(true, "Local\\KeyMindDesktopApp_4D6383F4", out created))
            {
                if (!created)
                {
                    MessageBox.Show("KeyMind يعمل بالفعل. ابحث عن أيقونته بجانب الساعة.", "KeyMind", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e) { LogError(e.Exception); };
                AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
                {
                    if (e.ExceptionObject is Exception) LogError((Exception)e.ExceptionObject);
                };
                try
                {
                    Application.Run(new MainForm());
                }
                catch (Exception e)
                {
                    LogError(e);
                    MessageBox.Show("تعذر تشغيل KeyMind. راجع ملف error.log داخل AppData.", "KeyMind", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        public static void LogError(Exception error)
        {
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KeyMind");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "error.log"), DateTime.UtcNow.ToString("u") + "\r\n" + error + "\r\n\r\n");
            }
            catch { }
        }
    }
}
