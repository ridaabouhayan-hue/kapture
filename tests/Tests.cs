using System;
using System.Drawing;
using System.IO;

namespace Kapture
{
    // Tests zonder scherm: bestandsnamen, instellingen, kader, donker maken, logo.
    static class Tests
    {
        static int fails, total;

        static void Is(bool ok, string what)
        {
            total++;
            if (!ok) { fails++; Console.WriteLine("FOUT  " + what); }
        }

        static void Eq(string got, string want, string what) { Is(got == want, what + ": kreeg \"" + got + "\", wilde \"" + want + "\""); }

        static int Main()
        {
            DateTime t = new DateTime(2026, 11, 24, 0, 12, 0);

            // bestandsnamen
            Eq(Naming.Base(t, "Chrome", ""), "2026-11-24 0012 Chrome", "naam zonder notitie");
            Eq(Naming.Base(t, "Chrome", "Opslaan-knop werkt niet"), "2026-11-24 0012 Chrome - Opslaan-knop werkt niet", "naam met notitie");
            Eq(Naming.Base(t, "", ""), "2026-11-24 0012", "naam zonder programma");
            Eq(Naming.Clean("a/b\\c:d*e?f\"g<h>i|j"), "abcdefghij", "verboden tekens weg");
            Eq(Naming.Clean("  twee\r\nregels   en\tspaties  "), "twee regels en spaties", "regeleinden en spaties");
            Eq(Naming.Clean("eindigt op punt..."), "eindigt op punt", "geen punt aan het eind");
            Eq(Naming.Clean(null), "", "leeg blijft leeg");
            string lang = Naming.Base(t, "Chrome", new string('x', 300));
            Is(lang.Length <= Naming.MaxLength, "lange naam wordt ingekort (" + lang.Length + ")");
            Eq(Naming.AppName("chrome"), "Chrome", "programmanaam Chrome");
            Eq(Naming.AppName("WINWORD"), "Word", "programmanaam Word");
            Eq(Naming.AppName("notepad"), "Notepad", "onbekend programma met hoofdletter");
            Eq(Naming.AppName(""), "", "geen programma");

            // uniek
            string dir = Path.Combine(Path.GetTempPath(), "kapture-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                File.WriteAllText(Path.Combine(dir, "a.png"), "");
                Eq(Path.GetFileName(Naming.Unique(dir, "a", ".png")), "a (2).png", "dubbele naam krijgt (2)");
                File.WriteAllText(Path.Combine(dir, "b.png.tmp"), "");
                Eq(Path.GetFileName(Naming.Unique(dir, "b", ".png")), "b (2).png", "half geschreven bestand telt mee");

                // instellingen heen en terug
                string sp = Path.Combine(dir, "instellingen.txt");
                Settings s = new Settings { Inbox = @"D:\Map met spaties", ToastSeconds = 9, HotkeysPaused = true, SnipHintShown = true };
                s.Save(sp);
                Settings r = Settings.Load(sp);
                Is(r.Inbox == s.Inbox && r.ToastSeconds == 9 && r.HotkeysPaused && r.SnipHintShown, "instellingen heen en terug");
                s.ToastSeconds = 4; s.Save(sp);
                Is(Settings.Load(sp).ToastSeconds == 4, "instellingen overschrijven");
                File.WriteAllText(sp, "meldingSeconden=999\nonzin\n=leeg");
                Is(Settings.Load(sp).ToastSeconds == 60, "te grote waarde wordt begrensd");
                Is(Settings.Load(Path.Combine(dir, "bestaatniet.txt")).ToastSeconds == 6, "geen bestand = standaard");

                // nieuwe instellingen heen en terug
                Settings n = new Settings { ThemeMode = 2, HotkeyNote = false, HotkeyLastNote = false, MonthFolders = true, NoteStrip = false, NoteTextFile = false };
                n.Save(sp);
                Settings nb = Settings.Load(sp);
                Is(nb.ThemeMode == 2 && !nb.HotkeyNote && !nb.HotkeyLastNote && nb.HotkeyCapture && nb.MonthFolders && !nb.NoteStrip && !nb.NoteTextFile, "thema, sneltoetsen, mappen en notities heen en terug");
                File.WriteAllText(sp, "thema=9");
                Is(Settings.Load(sp).ThemeMode == 2, "thema wordt begrensd");
                Settings def = new Settings();
                Is(def.HotkeyCapture && def.HotkeyNote && def.HotkeyLastNote && def.NoteStrip && def.NoteTextFile && !def.MonthFolders, "standaard: alles aan, geen maandmappen");

                // mappen
                Settings m = new Settings { Inbox = dir };
                Eq(InboxFolder.ForShot(m, t), dir, "zonder maandmappen: de inbox zelf");
                m.MonthFolders = true;
                Eq(InboxFolder.ForShot(m, t), Path.Combine(dir, "2026-11"), "met maandmappen: jjjj-mm");
                Is(InboxFolder.Check(dir) == null, "bestaande map is bruikbaar");
                Is(InboxFolder.Check("relatief\\pad") != null, "een half pad wordt geweigerd");
                Is(InboxFolder.Check("") != null, "geen map wordt geweigerd");
            }
            finally { try { Directory.Delete(dir, true); } catch { } }

            // werkkopie voor Instellingen: pas na Opslaan echt
            Settings live = new Settings();
            Settings copy = live.Clone();
            Is(!copy.DiffersFrom(live), "kopie is gelijk");
            copy.MonthFolders = true; copy.UpdateCheck = false;
            Is(copy.DiffersFrom(live) && !live.MonthFolders, "wijzigen in de kopie raakt de echte instellingen niet");
            live.CopyFrom(copy);
            Is(live.MonthFolders && !live.UpdateCheck && !copy.DiffersFrom(live), "opslaan neemt de kopie over");

            // eigen sneltoets
            int cas = (int)(System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Alt | System.Windows.Forms.Keys.S);
            Eq(App.KeyName(cas), "Ctrl+Alt+S", "naam van een eigen sneltoets");
            Is(App.UsableKey(cas) && App.UsableKey((int)System.Windows.Forms.Keys.F9), "Ctrl+Alt+S en F9 mogen");
            Is(!App.UsableKey((int)System.Windows.Forms.Keys.S) && !App.UsableKey((int)(System.Windows.Forms.Keys.Shift | System.Windows.Forms.Keys.S)), "gewone letter (ook met Shift) mag niet");
            Is(App.ModsOf(cas) == (W.MOD_NOREPEAT | W.MOD_CONTROL | W.MOD_ALT), "Ctrl en Alt gaan mee naar Windows");
            Settings ck = new Settings { CustomKey = cas };
            Is(ck.Clone().CustomKey == cas && new Settings().DiffersFrom(ck), "eigen sneltoets in de werkkopie");

            // bijwerken: versies vergelijken en het antwoord van GitHub lezen
            Is(Updates.IsNewer("0.2.1", "0.2.0") && Updates.IsNewer("v1.0", "0.9.9") && !Updates.IsNewer("0.2.0", "0.2.0") && !Updates.IsNewer("0.1.9", "0.2.0"), "nieuwere versie herkennen");
            string sha = new string('a', 64);
            string json = "{\"tag_name\":\"v0.2.1\",\"html_url\":\"https://github.com/ridaabouhayan-hue/kapture/releases/tag/v0.2.1\",\"assets\":[{\"name\":\"Kapture-Setup.exe\",\"digest\":\"sha256:" + sha + "\",\"browser_download_url\":\"https://github.com/ridaabouhayan-hue/kapture/releases/download/v0.2.1/Kapture-Setup.exe\"},{\"name\":\"Kapture.exe\"}]}";
            Updates.Info ui = Updates.Parse(json);
            Is(ui != null && ui.Version == "0.2.1" && ui.Sha256 == sha && ui.SetupUrl.EndsWith("/Kapture-Setup.exe"), "antwoord van GitHub lezen");
            Is(Updates.Parse(json.Replace("ridaabouhayan-hue/kapture/releases/download", "iemand-anders/kapture/releases/download")).SetupUrl == null, "installer van een ander adres wordt niet gebruikt");
            Is(Updates.Parse("onzin") == null, "raar antwoord: geen versie");
            string pretty = "{\n  \"tag_name\": \"v0.2.1\",\n  \"html_url\": \"https://github.com/ridaabouhayan-hue/kapture/releases/tag/v0.2.1\",\n  \"assets\": [\n    {\n      \"name\": \"Kapture-Setup.exe\",\n      \"digest\": \"sha256:" + sha + "\",\n      \"browser_download_url\": \"https://github.com/ridaabouhayan-hue/kapture/releases/download/v0.2.1/Kapture-Setup.exe\"\n    },\n    {\n      \"name\": \"Kapture.exe\"\n    }\n  ]\n}";
            Updates.Info up = Updates.Parse(pretty);
            Is(up != null && up.Sha256 == sha && up.SetupUrl != null, "antwoord van GitHub met spaties en regels lezen");

            // notities: wanneer een tekstbestand, en is de naam al goed
            Is(!Notes.NeedsTextFile(t, "Chrome", "Opslaan-knop werkt niet"), "korte notitie: geen tekstbestand");
            Is(Notes.NeedsTextFile(t, "Chrome", "regel een\nregel twee"), "notitie met regels: tekstbestand");
            Is(Notes.NeedsTextFile(t, "Chrome", new string('x', 200)), "te lange notitie: tekstbestand");
            Is(!Notes.NeedsTextFile(t, "Chrome", ""), "geen notitie: geen tekstbestand");
            Is(Notes.HasName(@"C:\a\2026-11-24 0012 Chrome.png", "2026-11-24 0012 Chrome"), "naam klopt al");
            Is(Notes.HasName(@"C:\a\2026-11-24 0012 Chrome (3).png", "2026-11-24 0012 Chrome"), "naam met (3) klopt ook");
            Is(!Notes.HasName(@"C:\a\2026-11-24 0012 Chrome - x.png", "2026-11-24 0012 Chrome"), "andere notitie = andere naam");
            Is(!Notes.HasName(@"C:\a\2026-11-24 0012 Chrome (x).png", "2026-11-24 0012 Chrome"), "(x) is geen nummer");

            // tekstveld
            TextField tf = new TextField();
            tf.OnChar('a'); tf.OnChar('b'); tf.OnKey(System.Windows.Forms.Keys.Left); tf.OnChar('X');
            Eq(tf.Text, "aXb", "typen met de cursor in het midden");
            tf.OnKey(System.Windows.Forms.Keys.Back);
            Eq(tf.Text, "ab", "backspace");
            tf.OnKey(System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.A); tf.OnChar('z');
            Eq(tf.Text, "z", "alles selecteren en overtypen");
            tf.Set(new string('q', 600));
            Is(tf.Text.Length == TextField.MaxLength, "notitie wordt begrensd op " + TextField.MaxLength + " tekens");

            // kader van twee punten, in elke richting
            Is(Overlay.Norm(new Point(10, 10), new Point(20, 30)) == new Rectangle(10, 10, 11, 21), "kader van linksboven naar rechtsonder");
            Is(Overlay.Norm(new Point(20, 30), new Point(10, 10)) == new Rectangle(10, 10, 11, 21), "kader van rechtsonder naar linksboven");

            // donker maken
            using (Dib a = Dib.Create(2, 1))
            using (Dib b = Dib.Create(2, 1))
            {
                System.Runtime.InteropServices.Marshal.WriteInt32(a.Bits, 0, unchecked((int)0x00FFFFFF));
                System.Runtime.InteropServices.Marshal.WriteInt32(a.Bits, 4, unchecked((int)0x00804020));
                Dib.Darken(a, b, 128);
                uint p0 = (uint)System.Runtime.InteropServices.Marshal.ReadInt32(b.Bits, 0), p1 = (uint)System.Runtime.InteropServices.Marshal.ReadInt32(b.Bits, 4);
                Is((p0 & 0xFFFFFF) == 0x7F7F7F, "wit wordt half zo licht (" + p0.ToString("X8") + ")");
                Is((p1 & 0xFFFFFF) == 0x402010, "kleur wordt half zo licht (" + p1.ToString("X8") + ")");
            }

            // logo en icoontjes zijn te lezen
            using (Bitmap l = Logo.Bitmap(32)) Is(l.GetPixel(16, 16).A > 0, "logo 32 px heeft inhoud");
            using (Bitmap l = Logo.Bitmap(256)) Is(l.GetPixel(128, 128).A > 0, "logo 256 px heeft inhoud");
            foreach (string name in IconData.All.Keys)
                using (Bitmap ib = new Bitmap(24, 24))
                using (Graphics g = Graphics.FromImage(ib))
                {
                    Icons.Draw(g, name, new RectangleF(0, 0, 24, 24), 24, Color.Black, 2);
                    bool any = false;
                    for (int x = 0; x < 24 && !any; x++) for (int y = 0; y < 24 && !any; y++) if (ib.GetPixel(x, y).A > 0) any = true;
                    Is(any, "icoon " + name + " tekent iets");
                }

            // Phosphor-icoontjes: elke naam is te lezen en heeft een vorm
            foreach (string name in PhosphorData.All.Keys)
                using (Bitmap ib = new Bitmap(32, 32))
                using (Graphics g = Graphics.FromImage(ib))
                {
                    Icons.Draw(g, name, new RectangleF(0, 0, 32, 32), 24, Color.Black, 2);
                    int n = 0;
                    for (int x = 0; x < 32; x++) for (int y = 0; y < 32; y++) if (ib.GetPixel(x, y).A > 128) n++;
                    Is(n > 8, "phosphor " + name + " tekent een vorm (" + n + ")");
                }
            foreach (string name in IconData.All.Keys) Is(PhosphorData.All.ContainsKey(name), "phosphor heeft " + name);

            // inboxmap
            Is(!string.IsNullOrEmpty(InboxFolder.Default()), "er is altijd een inboxmap");

            Console.WriteLine(fails == 0 ? "Alle " + total + " tests geslaagd" : fails + " van " + total + " tests mislukt");
            return fails == 0 ? 0 : 1;
        }
    }
}
