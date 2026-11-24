using System;
using System.Threading;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Kapture")]
[assembly: System.Reflection.AssemblyProduct("Kapture")]
[assembly: System.Reflection.AssemblyVersion("0.3.1.0")]

namespace Kapture
{
    static class Program
    {
        public const string Version = "0.3.1";
        public static bool SelfTest;
        public static Theme ForceTheme = null;

        [STAThread]
        static int Main(string[] args)
        {
            try { W.SetProcessDpiAwarenessContext(new IntPtr(-4)); }
            catch (EntryPointNotFoundException) { try { W.SetProcessDpiAwareness(2); } catch { } }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args.Length > 0 && args[0] == "--zelftest")
            {
                SelfTest = true;
                return Kapture.SelfTest.Run(args.Length > 1 ? args[1] : "out\\schermen");
            }

            if (args.Length > 0 && args[0] == "--proef-echt") { SelfTest = true; return Kapture.SelfTest.Live(); }

            bool created;
            using (Mutex one = new Mutex(true, "Local\\Kapture.EenKeer", out created))
            {
                if (!created)
                {
                    // Kapture draait al: dat exemplaar laat een melding zien
                    W.PostMessage(new IntPtr(0xFFFF), HotkeyWindow.ShowMessage, IntPtr.Zero, IntPtr.Zero);
                    return 0;
                }
                Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Log.Error("Onverwachte fout", e.Exception); };
                AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { Log.Error("Onverwachte fout (afgesloten)", e.ExceptionObject as Exception); };
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.Run(new App());
            }
            return 0;
        }
    }
}
