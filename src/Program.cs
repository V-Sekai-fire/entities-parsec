using System;
using System.Threading;
using System.Windows.Forms;

namespace WallpaperParsec
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            string attachHint = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--attach" && i + 1 < args.Length)
                    attachHint = args[i + 1];
            }

            // Two instances would fight over the same window and each would hold a stale
            // copy of its original styles, so the second one to start loses.
            bool isFirst;
            using (Mutex mutex = new Mutex(true, "Local\\WallpaperParsec.SingleInstance", out isFirst))
            {
                if (!isFirst)
                {
                    MessageBox.Show("Wallpaper Parsec is already running — look for it in the tray.",
                        "Wallpaper Parsec", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                using (TrayApp app = new TrayApp(attachHint))
                {
                    Application.Run(app);
                }

                GC.KeepAlive(mutex);
            }
        }
    }
}
