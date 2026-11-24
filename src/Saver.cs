using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Kapture
{
    // ------------------------------------------------------------------ De inboxmap
    static class InboxFolder
    {
        // OneDrive\Pictures\Screenshots (of OneDrive\Afbeeldingen\Screenshots); zonder OneDrive de Screenshots-map van Windows
        public static string Default()
        {
            try
            {
                string od = Environment.GetEnvironmentVariable("OneDrive");
                if (!string.IsNullOrEmpty(od) && Directory.Exists(od))
                {
                    foreach (string pics in new string[] { "Pictures", "Afbeeldingen" })
                    {
                        string p = Path.Combine(od, pics);
                        if (Directory.Exists(p)) return Path.Combine(p, "Screenshots");
                    }
                }
                Guid screenshots = new Guid("b7bede81-df94-4682-a7d8-57a52620b86f");
                IntPtr ptr;
                if (W.SHGetKnownFolderPath(ref screenshots, 0, IntPtr.Zero, out ptr) == 0)
                {
                    string s = Marshal.PtrToStringUni(ptr);
                    W.CoTaskMemFree(ptr);
                    if (!string.IsNullOrEmpty(s)) return s;
                }
            }
            catch (Exception ex) { Log.Error("Inboxmap bepalen mislukt", ex); }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots");
        }

        public static string Resolve(Settings s) { return string.IsNullOrEmpty(s.Inbox) ? Default() : s.Inbox; }

        // De map voor een nieuwe screenshot: de inbox, of met "elke maand een eigen map" de submap jjjj-mm
        public static string ForShot(Settings s, DateTime t)
        {
            string root = Resolve(s);
            return s.MonthFolders ? Path.Combine(root, t.ToString("yyyy-MM")) : root;
        }

        // Kan Kapture in deze map schrijven? Maakt de map aan als die er nog niet is. Geeft null of een korte uitleg.
        public static string Check(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return "Er is geen map gekozen.";
            try
            {
                if (!Path.IsPathRooted(folder)) return "Kies een volledige map, bijvoorbeeld op C: of in OneDrive.";
                Directory.CreateDirectory(folder);
                string probe = Path.Combine(folder, ".kapture-proef-" + Guid.NewGuid().ToString("N") + ".tmp");
                File.WriteAllText(probe, "");
                File.Delete(probe);
                return null;
            }
            catch (UnauthorizedAccessException) { return "Kapture mag niet schrijven in deze map."; }
            catch (Exception) { return "Deze map is niet bereikbaar. Staat OneDrive aan, of is de schijf aangesloten?"; }
        }
    }

    // ------------------------------------------------------------------ Bestandsnamen: "2026-11-24 0012 Chrome - Opslaan-knop werkt niet.png"
    static class Naming
    {
        public const int MaxLength = 120;   // zonder ".png"
        static readonly char[] Bad = Path.GetInvalidFileNameChars();

        // Tekens die Windows niet toestaat weg, regeleinden en dubbele spaties worden één spatie
        public static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            StringBuilder sb = new StringBuilder(s.Length);
            bool space = false;
            foreach (char ch in s)
            {
                char c = ch;
                if (char.IsControl(c) || char.IsWhiteSpace(c)) c = ' ';
                else if (Array.IndexOf(Bad, c) >= 0) continue;
                if (c == ' ') { if (space || sb.Length == 0) continue; space = true; }
                else space = false;
                sb.Append(c);
            }
            return sb.ToString().Trim().TrimEnd('.', ' ');
        }

        public static string Base(DateTime t, string app, string note)
        {
            string name = t.ToString("yyyy-MM-dd HHmm");
            string a = Clean(app);
            if (a.Length > 40) a = a.Substring(0, 40).TrimEnd();
            if (a.Length > 0) name += " " + a;
            string n = Clean(note);
            if (n.Length > 0) name += " - " + n;
            if (name.Length > MaxLength) name = name.Substring(0, MaxLength).TrimEnd(' ', '-', '.');
            return name;
        }

        // Bestaat de naam al, dan " (2)", " (3)" erachter
        public static string Unique(string folder, string baseName, string ext)
        {
            string p = Path.Combine(folder, baseName + ext);
            for (int i = 2; File.Exists(p) || File.Exists(p + ".tmp"); i++)
                p = Path.Combine(folder, baseName + " (" + i + ")" + ext);
            return p;
        }

        // Naam van het programma dat vooraan stond, bijvoorbeeld "Chrome" of "WhatsApp". Nooit de venstertitel.
        public static string ForegroundApp()
        {
            try
            {
                IntPtr h = W.GetForegroundWindow();
                if (h == IntPtr.Zero) return "";
                uint pid;
                W.GetWindowThreadProcessId(h, out pid);
                if (pid == 0 || pid == (uint)Process.GetCurrentProcess().Id) return "";
                using (Process p = Process.GetProcessById((int)pid)) return AppName(p.ProcessName);
            }
            catch { return ""; }
        }

        // Programma onder een punt op het scherm (het midden van het kader); werkt ook als je vanuit het tray-menu begon.
        // Loopt de vensters van boven naar onder af en slaat die van Kapture zelf over (de overlay kan nog openstaan).
        public static string AppAt(int x, int y)
        {
            try
            {
                uint me = (uint)Process.GetCurrentProcess().Id;
                IntPtr h = W.GetTopWindow(IntPtr.Zero);
                for (int guard = 0; h != IntPtr.Zero && guard < 2000; guard++, h = W.GetWindow(h, W.GW_HWNDNEXT))
                {
                    if (!W.IsWindowVisible(h)) continue;
                    W.RECT rc;
                    if (!W.GetWindowRect(h, out rc) || x < rc.Left || x >= rc.Right || y < rc.Top || y >= rc.Bottom) continue;
                    int cloaked = 0;
                    try { W.DwmGetWindowAttribute(h, 14, out cloaked, 4); } catch { }
                    if (cloaked != 0) continue;
                    uint pid;
                    W.GetWindowThreadProcessId(h, out pid);
                    if (pid == 0 || pid == me) continue;
                    using (Process p = Process.GetProcessById((int)pid)) return AppName(p.ProcessName);
                }
            }
            catch { }
            return "";
        }

        public static string AppName(string process)
        {
            if (string.IsNullOrEmpty(process)) return "";
            switch (process.ToLowerInvariant())
            {
                case "chrome": return "Chrome";
                case "msedge": return "Edge";
                case "firefox": return "Firefox";
                case "explorer": return "Verkenner";
                case "winword": return "Word";
                case "excel": return "Excel";
                case "powerpnt": return "PowerPoint";
                case "outlook": case "olk": return "Outlook";
                case "whatsapp": case "whatsapp.root": return "WhatsApp";
                case "claude": return "Claude";
                case "code": return "VS Code";
                case "applicationframehost": return "";
            }
            return char.ToUpperInvariant(process[0]) + process.Substring(1);
        }
    }

    // ------------------------------------------------------------------ Opslaan en kopieren
    class SaveResult
    {
        public Shot Shot;            // Shot.Path is null als opslaan mislukte
        public bool Copied;
        public Bitmap Thumb;         // miniatuur voor de melding
        public bool Failed { get { return Shot.Path == null; } }
    }

    static class Saver
    {
        // Opslaan als PNG op de achtergrond, daarna (op de UI-draad) kopieren. 'done' komt altijd op de UI-draad.
        // Een notitie die al bekend is komt meteen in de naam en (als 'strip') als strook op het klembord.
        public static void SaveAndCopy(Bitmap shot, Shot entry, string folder, bool copy, bool strip, bool textFile, System.Windows.Forms.Control ui, Action<SaveResult> done)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                SaveResult r = new SaveResult();
                r.Shot = entry;
                byte[] png = null;
                try
                {
                    using (MemoryStream ms = new MemoryStream()) { shot.Save(ms, ImageFormat.Png); png = ms.ToArray(); }
                    entry.Path = WriteFile(png, folder, Naming.Base(entry.Time, entry.App, entry.Note));
                    if (textFile && Notes.NeedsTextFile(entry.Time, entry.App, entry.Note))
                        Notes.WriteText(Notes.TextPath(entry.Path), entry.Note);
                }
                catch (Exception ex) { Log.Error("Opslaan mislukt", ex); entry.Path = null; }
                try { r.Thumb = Thumbnail(shot, 160, 96); } catch { }
                Action finish = delegate
                {
                    try
                    {
                        if (copy)
                        {
                            if (strip && !string.IsNullOrEmpty(entry.Note))
                                using (Bitmap withNote = Notes.WithStrip(shot, entry.Note)) r.Copied = Copy(withNote, null);
                            else r.Copied = Copy(shot, png);
                            entry.Copied = r.Copied;
                            if (r.Copied) entry.ClipboardSeq = W.GetClipboardSequenceNumber();
                        }
                    }
                    finally { shot.Dispose(); }
                    done(r);
                };
                try { ui.BeginInvoke(finish); } catch (Exception ex) { Log.Error("Terug naar de UI mislukt", ex); shot.Dispose(); }
            });
        }

        // Leest een opgeslagen screenshot terug (zonder het bestand vast te houden)
        public static Bitmap Load(string path)
        {
            byte[] data = File.ReadAllBytes(path);
            using (MemoryStream ms = new MemoryStream(data))
            using (Image img = Image.FromStream(ms))
                return new Bitmap(img);
        }

        // Eerst naar naam.png.tmp in dezelfde map, dan hernoemen: zo ziet OneDrive nooit een half bestand
        static readonly object writeGate = new object();

        public static string WriteFile(byte[] png, string folder, string baseName)
        {
            lock (writeGate) return WriteFileLocked(png, folder, baseName);
        }

        static string WriteFileLocked(byte[] png, string folder, string baseName)
        {
            Directory.CreateDirectory(folder);
            for (int attempt = 0; ; attempt++)
            {
                string final = Naming.Unique(folder, baseName, ".png");
                string tmp = final + ".tmp";
                try
                {
                    using (FileStream fs = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write)) fs.Write(png, 0, png.Length);
                    File.Move(tmp, final);
                    return final;
                }
                catch (IOException)
                {
                    try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                    if (attempt >= 2) throw;
                    Thread.Sleep(200 * (attempt + 1));
                }
            }
        }

        // Op het klembord als gewone afbeelding en als PNG, zodat plakken werkt in Claude, de browser, Word en WhatsApp
        public static bool Copy(Bitmap shot, byte[] png)
        {
            try
            {
                if (png == null)
                    using (MemoryStream ms = new MemoryStream()) { shot.Save(ms, ImageFormat.Png); png = ms.ToArray(); }
                DataObject data = new DataObject();
                data.SetData(DataFormats.Bitmap, true, shot);
                data.SetData("PNG", false, new MemoryStream(png));
                Clipboard.SetDataObject(data, true, 5, 100);
                return true;
            }
            catch (Exception ex) { Log.Error("Kopieren naar het klembord mislukt", ex); return false; }
        }

        public static Bitmap Thumbnail(Image src, int maxW, int maxH)
        {
            float k = Math.Min((float)maxW / src.Width, (float)maxH / src.Height);
            k = Math.Min(1f, k);
            int w = Math.Max(1, (int)(src.Width * k)), h = Math.Max(1, (int)(src.Height * k));
            Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(src, 0, 0, w, h);
            }
            return b;
        }
    }
}
