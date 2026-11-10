using System;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DriftClock
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            ScreensaverArgs a = ScreensaverArgs.Parse(args);
            switch (a.Mode)
            {
                case SaverMode.Fullscreen: RunSaver(); break;
                case SaverMode.Preview: RunPreview(a.Handle); break;
                default: RunSettings(a.Handle); break;
            }
        }

        private static void RunSaver()
        {
            // A screensaver must never show error dialogs: on any failure just leave.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => { LogError(e.Exception); Environment.Exit(0); };
            AppDomain.CurrentDomain.UnhandledException += (s, e) => { LogError(e.ExceptionObject as Exception); Environment.Exit(0); };

            Settings settings = Settings.Load(Settings.DefaultPath);
            using (var data = new DataService(settings))
            {
                data.Start();
                Application.AddMessageFilter(new SaverInput());

                SaverForm primary = null;
                foreach (Screen screen in Screen.AllScreens.OrderBy(sc => sc.Primary))
                {
                    var form = new SaverForm(screen, screen.Primary, data, settings);
                    if (screen.Primary) primary = form;
                    form.Show();
                }
                if (primary == null) return;

                // Monitors added/removed/resized while running (dock, hot-plug, cable drop): the forms would sit on
                // stale bounds and a monitor could show the static desktop, so just close. Windows restarts the saver
                // on the next idle period.
                var initial = Screen.AllScreens.Select(sc => sc.Bounds).ToList();
                EventHandler onDisplayChange = (s, e) =>
                {
                    if (ScreenLayout.Differs(initial, Screen.AllScreens.Select(sc => sc.Bounds)))
                    {
                        try { primary.BeginInvoke(new Action(Application.Exit)); }
                        catch { Environment.Exit(0); }
                    }
                };
                SystemEvents.DisplaySettingsChanged += onDisplayChange;
                try { Application.Run(primary); }
                finally { SystemEvents.DisplaySettingsChanged -= onDisplayChange; }
            }
        }

        /// <summary>Best-effort error log so a silent saver can still be diagnosed.</summary>
        private static void LogError(Exception ex)
        {
            try
            {
                string dir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DriftClock");
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "error.log"),
                    DateTime.Now.ToString("s") + " " + ex + Environment.NewLine);
            }
            catch { }
        }

        private static void RunPreview(IntPtr parent)
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => { LogError(e.Exception); Environment.Exit(0); };
            AppDomain.CurrentDomain.UnhandledException += (s, e) => { LogError(e.ExceptionObject as Exception); Environment.Exit(0); };

            Settings settings = Settings.Load(Settings.DefaultPath);
            using (var data = new DataService(settings)) // cache only: Start() is not called, so no network
            using (var preview = new PreviewWindow(parent, data, settings))
            {
                preview.Attach();
                Application.Run(); // ends via Application.Exit() when the parent window goes away
            }
        }

        private static void RunSettings(IntPtr owner)
        {
            using (var form = new SettingsForm(Settings.Load(Settings.DefaultPath)))
            {
                if (owner != IntPtr.Zero) form.ShowDialog(new Win32Owner(owner));
                else form.ShowDialog();
            }
        }
    }
}
