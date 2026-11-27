using System;
using System.IO;
using System.Text;

namespace Kapture
{
    // ------------------------------------------------------------------ Foutlog
    // Nooit in het log: de inhoud van een screenshot, een notitie, een bestandsnaam of een venstertitel.
    // Daarom schrijven we bij een fout alleen het soort fout en waar in de code, niet de foutmelding zelf
    // (die bevat vaak een pad met de bestandsnaam).
    static class Log
    {
        static readonly object gate = new object();
        public static string Dir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Kapsure"); } }
        public static string FilePath { get { return Path.Combine(Dir, "foutlog.txt"); } }

        public static void Info(string msg) { Write("INFO ", msg); }

        // Spoor van een screenshot (welke sneltoets binnenkwam, waarom het donkere scherm dichtging), om storingen te vinden.
        // Alleen toetsnamen en programmanamen; hooguit 200 regels per keer dat Kapsure draait.
        static int traces;
        public static void Trace(string msg) { if (System.Threading.Interlocked.Increment(ref traces) <= 200) Write("SPOOR ", msg); }

        public static void Error(string msg, Exception ex)
        {
            string text = msg;
            if (ex != null) text += Environment.NewLine + Describe(ex);
            Write("FOUT ", text);
            if (Program.SelfTest) { try { Console.WriteLine("FOUT " + text); } catch { } }
        }

        // Soort fout, foutcode en de plek in de code; zonder de tekst van de fout
        public static string Describe(Exception ex)
        {
            StringBuilder sb = new StringBuilder();
            Exception e = ex;
            int depth = 0;
            while (e != null && depth < 4)
            {
                sb.Append(e.GetType().FullName).Append(" (0x").Append(e.HResult.ToString("X8")).Append(")").AppendLine();
                if (e.StackTrace != null) sb.AppendLine(e.StackTrace);
                e = e.InnerException; depth++;
            }
            return sb.ToString().TrimEnd();
        }

        static void Write(string level, string msg)
        {
            try
            {
                lock (gate)
                {
                    Directory.CreateDirectory(Dir);
                    FileInfo fi = new FileInfo(FilePath);
                    if (fi.Exists && fi.Length > 1024 * 1024) File.Delete(FilePath);
                    File.AppendAllText(FilePath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + level + msg + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch { }
        }
    }
}
