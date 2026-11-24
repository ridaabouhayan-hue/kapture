using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;

namespace Kapture
{
    // ------------------------------------------------------------------ Eén screenshot in de geschiedenis
    [DataContract]
    class Shot
    {
        [DataMember(Name = "pad")] public string Path;
        [DataMember(Name = "tijd")] public DateTime Time;
        [DataMember(Name = "programma")] public string App = "";
        [DataMember(Name = "preset")] public string Preset = "Nu naar Claude";
        [DataMember(Name = "notitie")] public string Note = "";

        // Alleen in het geheugen: wat er op het klembord gezet is, om te weten of we het nog mogen vervangen
        public uint ClipboardSeq;
        public bool Copied;
        public string PendingNote;   // notitie die al getypt is terwijl het bestand nog wordt opgeslagen
    }

    // ------------------------------------------------------------------ Geschiedenis: %APPDATA%\Kapture\geschiedenis.json (laatste 10, alleen op deze pc)
    class History
    {
        public const int Max = 10;
        readonly List<Shot> items = new List<Shot>();
        readonly string path;

        public History(string path) { this.path = path; }

        public static string DefaultPath { get { return System.IO.Path.Combine(Log.Dir, "geschiedenis.json"); } }

        public IList<Shot> Items { get { return items.AsReadOnly(); } }

        public Shot Last
        {
            get
            {
                for (int i = items.Count - 1; i >= 0; i--) if (items[i].Path != null && File.Exists(items[i].Path)) return items[i];
                return null;
            }
        }

        public void Add(Shot s)
        {
            items.Add(s);
            while (items.Count > Max) items.RemoveAt(0);
            Save();
        }

        public static History Load(string path)
        {
            History h = new History(path);
            try
            {
                if (!File.Exists(path)) return h;
                using (FileStream fs = File.OpenRead(path))
                {
                    Shot[] arr = (Shot[])new DataContractJsonSerializer(typeof(Shot[])).ReadObject(fs);
                    if (arr != null) foreach (Shot s in arr) if (s != null && !string.IsNullOrEmpty(s.Path)) { if (s.Note == null) s.Note = ""; if (s.App == null) s.App = ""; h.items.Add(s); }
                }
                while (h.items.Count > Max) h.items.RemoveAt(0);
            }
            catch (Exception ex) { Log.Error("Geschiedenis lezen mislukt", ex); h.items.Clear(); }
            return h;
        }

        // Veilig bewaren: eerst .tmp, dan vervangen
        public void Save()
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                string tmp = path + ".tmp";
                using (FileStream fs = new FileStream(tmp, FileMode.Create, FileAccess.Write))
                    new DataContractJsonSerializer(typeof(Shot[])).WriteObject(fs, items.ToArray());
                if (File.Exists(path)) File.Replace(tmp, path, null); else File.Move(tmp, path);
            }
            catch (Exception ex) { Log.Error("Geschiedenis bewaren mislukt", ex); }
        }
    }

    // ------------------------------------------------------------------ Notities: in de bestandsnaam, lang ook in een .txt ernaast
    static class Notes
    {
        // Moet de volledige notitie in een tekstbestand? Als hij niet helemaal in de naam past, of meerdere regels heeft.
        public static bool NeedsTextFile(DateTime t, string app, string note)
        {
            if (string.IsNullOrEmpty(note) || note.Trim().Length == 0) return false;
            if (note.IndexOf('\n') >= 0 || note.IndexOf('\r') >= 0) return true;
            string clean = Naming.Clean(note);
            string name = Naming.Base(t, app, note);
            return !name.EndsWith(" - " + clean, StringComparison.Ordinal);
        }

        // Heeft dit bestand al de naam bij deze notitie? Ook "naam (2)" telt mee: dan niets hernoemen.
        public static bool HasName(string path, string baseName)
        {
            string n = System.IO.Path.GetFileNameWithoutExtension(path);
            if (string.Equals(n, baseName, StringComparison.Ordinal)) return true;
            if (!n.StartsWith(baseName + " (", StringComparison.Ordinal) || !n.EndsWith(")", StringComparison.Ordinal)) return false;
            int k;
            return int.TryParse(n.Substring(baseName.Length + 2, n.Length - baseName.Length - 3), out k) && k >= 2;
        }

        public static string TextPath(string png) { return System.IO.Path.ChangeExtension(png, ".txt"); }

        // Geeft het bestand de naam bij de notitie (en zet of verwijdert het tekstbestand).
        // Probeert het 3 keer (OneDrive kan een bestand even vasthouden); daarna een uitzondering.
        public static string Apply(Shot s, string note, bool textFile)
        {
            if (s == null || s.Path == null) throw new ArgumentException("geen bestand");
            note = (note ?? "").Trim();
            string folder = System.IO.Path.GetDirectoryName(s.Path);
            string baseName = Naming.Base(s.Time, s.App, note);
            string current = s.Path;
            int[] waits = { 0, 400, 1000, 2000 };
            string origTxt = TextPath(current);       // het tekstbestand hoort bij de naam van voor het hernoemen
            string caseTmp = current + ".hernoem";
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    if (waits[attempt] > 0) Thread.Sleep(waits[attempt]);
                    // een vorige poging bleef halverwege de hoofdletter-route steken: eerst terugzetten
                    if (!File.Exists(current) && File.Exists(caseTmp)) { File.Move(caseTmp, current); }
                    string wanted = System.IO.Path.Combine(folder, baseName + ".png");
                    if (!HasName(current, baseName))
                    {
                        string target = string.Equals(wanted, current, StringComparison.OrdinalIgnoreCase) ? wanted : Naming.Unique(folder, baseName, ".png");
                        if (string.Equals(target, current, StringComparison.OrdinalIgnoreCase))
                        {
                            // alleen hoofdletters anders: via een tijdelijke naam
                            File.Move(current, caseTmp);
                            File.Move(caseTmp, target);
                        }
                        else File.Move(current, target);
                        // meteen bijwerken: mislukt hierna het tekstbestand, dan weet de volgende poging waar het bestand staat
                        current = target; s.Path = target;
                    }
                    // tekstbestand: het oude weg (als het een andere naam had), het nieuwe schrijven of weghalen
                    string newTxt = TextPath(current);
                    bool needs = textFile && NeedsTextFile(s.Time, s.App, note);
                    if (needs) WriteText(newTxt, note);
                    else if (File.Exists(newTxt)) File.Delete(newTxt);
                    if (!string.Equals(origTxt, newTxt, StringComparison.OrdinalIgnoreCase) && File.Exists(origTxt)) File.Delete(origTxt);
                    s.Path = current; s.Note = note;
                    return current;
                }
                catch (IOException)
                {
                    if (attempt >= waits.Length - 1) throw;
                }
                catch (UnauthorizedAccessException)
                {
                    if (attempt >= waits.Length - 1) throw;
                }
            }
        }

        public static void WriteText(string path, string note)
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, note.Replace("\r\n", "\n").Replace("\n", "\r\n") + "\r\n", new UTF8Encoding(true));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        // ---- de notitiestrook op het klembord: de afbeelding met eronder de notitie. Het bestand blijft schoon.
        public static Bitmap WithStrip(Image img, string note)
        {
            note = (note ?? "").Trim();
            int w = Math.Max(img.Width, 360);
            float fontPx = Math.Max(14f, Math.Min(22f, w / 60f));
            int pad = (int)Math.Round(fontPx * 0.9f);
            Font f = Fonts.Ui(fontPx, 0), fb = Fonts.Ui(fontPx, 1);
            string label = "Notitie:";
            int textH;
            using (Bitmap mb = new Bitmap(1, 1)) using (Graphics mg = Graphics.FromImage(mb))
            {
                float lw = mg.MeasureString(label, fb, PointF.Empty, StringFormat.GenericTypographic).Width + fontPx * 0.4f;
                SizeF sz = mg.MeasureString(note, f, (int)(w - 2 * pad - lw), StringFormat.GenericTypographic);
                textH = (int)Math.Ceiling(Math.Max(sz.Height, f.GetHeight(mg)));
            }
            int stripH = textH + 2 * pad;
            Bitmap b = new Bitmap(w, img.Height + stripH, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.Clear(Color.White);
                g.DrawImage(img, 0, 0, img.Width, img.Height);
                Rectangle strip = new Rectangle(0, img.Height, w, stripH);
                using (SolidBrush bg = new SolidBrush(Color.FromArgb(0xFF, 0xF3, 0xF3, 0xFE))) g.FillRectangle(bg, strip);
                using (Pen line = new Pen(Color.FromArgb(0xFF, 0xC3, 0xC6, 0xD7))) g.DrawLine(line, 0, img.Height, w, img.Height);
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;   // geen ClearType: de afbeelding wordt elders geschaald, dan geven kleurranden vlekken
                using (SolidBrush ink = new SolidBrush(Color.FromArgb(0xFF, 0x19, 0x1B, 0x23)))
                using (SolidBrush accent = new SolidBrush(Color.FromArgb(0xFF, 0x25, 0x63, 0xEB)))
                {
                    float lw = g.MeasureString(label, fb, PointF.Empty, StringFormat.GenericTypographic).Width + fontPx * 0.4f;
                    g.DrawString(label, fb, accent, pad, img.Height + pad, StringFormat.GenericTypographic);
                    g.DrawString(note, f, ink, new RectangleF(pad + lw, img.Height + pad, w - 2 * pad - lw, textH + 2), StringFormat.GenericTypographic);
                }
            }
            return b;
        }
    }
}
