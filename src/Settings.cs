using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace Kapture
{
    // ------------------------------------------------------------------ Instellingen (%APPDATA%\Kapsure\instellingen.txt, sleutel=waarde)
    class Settings
    {
        // Algemeen
        public int ThemeMode = 0;              // 0 = zoals Windows, 1 = licht, 2 = donker
        public int ToastSeconds = 6;

        // Sneltoetsen (elk los aan of uit)
        public bool HotkeyCapture = true;      // PrtSc
        public bool HotkeyNote = true;         // Shift+PrtSc
        public bool HotkeyLastNote = true;     // Ctrl+PrtSc
        public bool HotkeysPaused = false;
        public int Preset = 0;                 // wat er na een screenshot gebeurt (Presets), gekozen in de werkbalk
        public int[] PresetSteps = NewSteps(); // per preset welke stappen aan staan (-1 = standaard), Instellingen, Presets
        public string RecentFolders = "";      // Opslaan in...: de laatste mappen, gescheiden door |
        public int CustomKey = 0;              // eigen sneltoets voor een screenshot (Keys met Ctrl/Alt/Shift), 0 = geen

        // Opslaan en mappen
        public string Inbox = "";              // leeg = de standaardmap (InboxFolder.Default)
        public bool MonthFolders = false;      // elke maand een eigen map: Screenshots\2026-11

        // Notities
        public bool NoteStrip = true;          // notitiestrook onder de afbeelding op het klembord
        public bool NoteTextFile = true;       // lange notitie of meerdere regels ook als .txt ernaast

        // Bewerken
        public bool ThumbOpensEditor = true;   // klik op de miniatuur: de editor van Kapsure (anders het programma van Windows)

        // Over
        public bool UpdateCheck = true;        // bij het starten kijken of er een nieuwe versie op GitHub staat

        public bool SnipHintShown = false;     // melding over de Windows-instelling voor Print Screen al gezien

        // Een werkkopie voor Instellingen: wijzigen zonder dat Kapsure het meteen gebruikt, tot je op Opslaan klikt
        public Settings Clone()
        {
            Settings c = (Settings)MemberwiseClone();
            c.PresetSteps = (int[])PresetSteps.Clone();   // eigen lijst: wijzigen in de werkkopie mag het origineel niet raken
            return c;
        }

        static int[] NewSteps() { int[] a = new int[Presets.Names.Length]; for (int i = 0; i < a.Length; i++) a[i] = -1; return a; }

        static bool SameSteps(int[] a, int[] b)
        {
            for (int i = 0; i < Presets.Names.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        // Neemt over wat je in Instellingen met Opslaan bewaart. Pauze, preset en de Knipprogramma-melding horen daar niet bij:
        // die veranderen meteen (ook via het tray-menu) en mogen niet door een oudere werkkopie teruggezet worden.
        public void CopyFrom(Settings o)
        {
            ThemeMode = o.ThemeMode; ToastSeconds = o.ToastSeconds;
            HotkeyCapture = o.HotkeyCapture; HotkeyNote = o.HotkeyNote; HotkeyLastNote = o.HotkeyLastNote; CustomKey = o.CustomKey;
            Inbox = o.Inbox; MonthFolders = o.MonthFolders; NoteStrip = o.NoteStrip; NoteTextFile = o.NoteTextFile;
            UpdateCheck = o.UpdateCheck; ThumbOpensEditor = o.ThumbOpensEditor;
            PresetSteps = (int[])o.PresetSteps.Clone();
        }

        // Verschilt deze kopie van 'o' in iets wat je in Instellingen kunt wijzigen?
        public bool DiffersFrom(Settings o)
        {
            return ThemeMode != o.ThemeMode || ToastSeconds != o.ToastSeconds || HotkeyCapture != o.HotkeyCapture || HotkeyNote != o.HotkeyNote
                || HotkeyLastNote != o.HotkeyLastNote || CustomKey != o.CustomKey || (Inbox ?? "") != (o.Inbox ?? "")
                || MonthFolders != o.MonthFolders || NoteStrip != o.NoteStrip || NoteTextFile != o.NoteTextFile || UpdateCheck != o.UpdateCheck
                || ThumbOpensEditor != o.ThumbOpensEditor || !SameSteps(PresetSteps, o.PresetSteps);
        }

        public static string FilePath { get { return Path.Combine(Log.Dir, "instellingen.txt"); } }

        public static Settings Load() { return Load(FilePath); }

        public static Settings Load(string path)
        {
            Settings s = new Settings();
            try
            {
                if (!File.Exists(path)) return s;
                foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim(), v = line.Substring(eq + 1).Trim();
                    int n;
                    switch (k)
                    {
                        case "thema": if (int.TryParse(v, out n)) s.ThemeMode = Math.Max(0, Math.Min(2, n)); break;
                        case "meldingSeconden": if (int.TryParse(v, out n)) s.ToastSeconds = Math.Max(2, Math.Min(60, n)); break;
                        case "toetsScreenshot": s.HotkeyCapture = v != "0"; break;
                        case "toetsMetNotitie": s.HotkeyNote = v != "0"; break;
                        case "toetsNotitieLaatste": s.HotkeyLastNote = v != "0"; break;
                        case "preset": if (int.TryParse(v, out n)) s.Preset = Presets.Clamp(n); break;
                        case "presetStappen":
                            {
                                string[] parts = v.Split(',');
                                for (int i = 0; i < parts.Length && i < s.PresetSteps.Length; i++)
                                    if (int.TryParse(parts[i], out n) && n >= -1 && n < 32) s.PresetSteps[i] = n;
                                break;
                            }
                        case "recenteMappen": s.RecentFolders = v; break;
                        case "eigenToets": if (int.TryParse(v, out n) && n >= 0) s.CustomKey = n; break;
                        case "sneltoetsenPauze": s.HotkeysPaused = v == "1"; break;
                        case "inbox": s.Inbox = v; break;
                        case "mapPerMaand": s.MonthFolders = v == "1"; break;
                        case "notitieStrook": s.NoteStrip = v != "0"; break;
                        case "notitieTekstbestand": s.NoteTextFile = v != "0"; break;
                        case "knipprogrammaMelding": s.SnipHintShown = v == "1"; break;
                        case "bijwerkenZoeken": s.UpdateCheck = v != "0"; break;
                        case "miniatuurEditor": s.ThumbOpensEditor = v != "0"; break;
                    }
                }
            }
            catch (Exception ex) { Log.Error("Instellingen lezen mislukt", ex); }
            return s;
        }

        public void Save() { Save(FilePath); }

        static string B(bool b) { return b ? "1" : "0"; }

        // Eerst naar een tijdelijk bestand, dan vervangen: zo is het bestand nooit half geschreven
        public void Save(string path)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("thema=" + ThemeMode);
                sb.AppendLine("meldingSeconden=" + ToastSeconds);
                sb.AppendLine("toetsScreenshot=" + B(HotkeyCapture));
                sb.AppendLine("toetsMetNotitie=" + B(HotkeyNote));
                sb.AppendLine("toetsNotitieLaatste=" + B(HotkeyLastNote));
                sb.AppendLine("preset=" + Preset);
                sb.AppendLine("presetStappen=" + string.Join(",", Array.ConvertAll(PresetSteps, x => x.ToString())));
                sb.AppendLine("recenteMappen=" + (RecentFolders ?? "").Replace("\r", "").Replace("\n", ""));
                sb.AppendLine("eigenToets=" + CustomKey);
                sb.AppendLine("sneltoetsenPauze=" + B(HotkeysPaused));
                sb.AppendLine("inbox=" + (Inbox ?? "").Replace("\r", "").Replace("\n", ""));
                sb.AppendLine("mapPerMaand=" + B(MonthFolders));
                sb.AppendLine("notitieStrook=" + B(NoteStrip));
                sb.AppendLine("notitieTekstbestand=" + B(NoteTextFile));
                sb.AppendLine("knipprogrammaMelding=" + B(SnipHintShown));
                sb.AppendLine("bijwerkenZoeken=" + B(UpdateCheck));
                sb.AppendLine("miniatuurEditor=" + B(ThumbOpensEditor));
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, sb.ToString(), Encoding.UTF8);
                if (File.Exists(path)) File.Replace(tmp, path, null); else File.Move(tmp, path);
            }
            catch (Exception ex) { Log.Error("Instellingen bewaren mislukt", ex); }
        }
    }

    // ------------------------------------------------------------------ Van Kapture naar Kapsure (nieuwe naam, 27-11)
    // Eén keer bij het opstarten: de mappen met instellingen, geschiedenis, foutlog en tijdelijke screenshots krijgen
    // de nieuwe naam, en "Starten met Windows" gaat over naar de nieuwe naam. Screenshots zelf blijven waar ze zijn.
    static class OldName
    {
        public const string Before = "Kapture";

        public static void Move()
        {
            MoveFolder(Environment.SpecialFolder.ApplicationData);
            MoveFolder(Environment.SpecialFolder.LocalApplicationData);
            Autostart.TakeOverOld(Before);
        }

        static void MoveFolder(Environment.SpecialFolder root)
        {
            try
            {
                string baseDir = Environment.GetFolderPath(root);
                string old = Path.Combine(baseDir, Before), nw = Path.Combine(baseDir, "Kapsure");
                if (!Directory.Exists(old)) return;
                if (!Directory.Exists(nw))
                {
                    try { Directory.Move(old, nw); Log.Info("Gegevens van de oude naam overgenomen"); return; }
                    catch (Exception) { }   // iets in de oude map is bezet (virusscanner, OneDrive): dan kopiëren
                    // eerst naar een tijdelijke map en pas aan het eind hernoemen: nooit een halve nieuwe map
                    string tmp = nw + ".tmp";
                    if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
                    CopyTree(old, tmp, false);
                    Directory.Move(tmp, nw);
                    Log.Info("Gegevens van de oude naam gekopieerd");
                    return;
                }
                // allebei aanwezig (bijvoorbeeld na een eerdere mislukte poging): wat in de nieuwe map ontbreekt, aanvullen
                CopyTree(old, nw, true);
            }
            catch (Exception ex) { Log.Error("Gegevens van de oude naam overnemen mislukt", ex); }
        }

        // 'onlyMissing': bestaande bestanden in 'to' nooit overschrijven
        static void CopyTree(string from, string to, bool onlyMissing)
        {
            Directory.CreateDirectory(to);
            foreach (string f in Directory.GetFiles(from))
            {
                string target = Path.Combine(to, Path.GetFileName(f));
                if (onlyMissing && File.Exists(target)) continue;
                try { File.Copy(f, target, false); } catch (IOException) { if (!onlyMissing) throw; }
            }
            foreach (string d in Directory.GetDirectories(from)) CopyTree(d, Path.Combine(to, Path.GetFileName(d)), onlyMissing);
        }
    }

    // ------------------------------------------------------------------ Starten met Windows (dezelfde plek als de installer gebruikt)
    static class Autostart
    {
        const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run", Name = "Kapsure";

        public static bool IsOn
        {
            get
            {
                try { using (RegistryKey k = Registry.CurrentUser.OpenSubKey(Key)) return k != null && k.GetValue(Name) != null; }
                catch { return false; }
            }
        }

        // Stond "Starten met Windows" aan onder de oude naam, dan nu onder de nieuwe (met dit programma), en de oude weg
        public static void TakeOverOld(string oldName)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(Key, true))
                {
                    if (k == null || k.GetValue(oldName) == null) return;
                    if (k.GetValue(Name) == null) k.SetValue(Name, "\"" + System.Windows.Forms.Application.ExecutablePath + "\"");
                    k.DeleteValue(oldName, false);
                }
            }
            catch (Exception ex) { Log.Error("Starten met Windows overnemen mislukt", ex); }
        }

        public static void Set(bool on)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(Key))
                {
                    if (on) k.SetValue(Name, "\"" + System.Windows.Forms.Application.ExecutablePath + "\"");
                    else if (k.GetValue(Name) != null) k.DeleteValue(Name);
                }
            }
            catch (Exception ex) { Log.Error("Starten met Windows aanpassen mislukt", ex); }
        }
    }
}
