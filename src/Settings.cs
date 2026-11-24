using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace Kapture
{
    // ------------------------------------------------------------------ Instellingen (%APPDATA%\Kapture\instellingen.txt, sleutel=waarde)
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

        // Opslaan en mappen
        public string Inbox = "";              // leeg = de standaardmap (InboxFolder.Default)
        public bool MonthFolders = false;      // elke maand een eigen map: Screenshots\2026-11

        // Notities
        public bool NoteStrip = true;          // notitiestrook onder de afbeelding op het klembord
        public bool NoteTextFile = true;       // lange notitie of meerdere regels ook als .txt ernaast

        public bool SnipHintShown = false;     // melding over de Windows-instelling voor Print Screen al gezien

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
                        case "sneltoetsenPauze": s.HotkeysPaused = v == "1"; break;
                        case "inbox": s.Inbox = v; break;
                        case "mapPerMaand": s.MonthFolders = v == "1"; break;
                        case "notitieStrook": s.NoteStrip = v != "0"; break;
                        case "notitieTekstbestand": s.NoteTextFile = v != "0"; break;
                        case "knipprogrammaMelding": s.SnipHintShown = v == "1"; break;
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
                sb.AppendLine("sneltoetsenPauze=" + B(HotkeysPaused));
                sb.AppendLine("inbox=" + (Inbox ?? "").Replace("\r", "").Replace("\n", ""));
                sb.AppendLine("mapPerMaand=" + B(MonthFolders));
                sb.AppendLine("notitieStrook=" + B(NoteStrip));
                sb.AppendLine("notitieTekstbestand=" + B(NoteTextFile));
                sb.AppendLine("knipprogrammaMelding=" + B(SnipHintShown));
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, sb.ToString(), Encoding.UTF8);
                if (File.Exists(path)) File.Replace(tmp, path, null); else File.Move(tmp, path);
            }
            catch (Exception ex) { Log.Error("Instellingen bewaren mislukt", ex); }
        }
    }

    // ------------------------------------------------------------------ Starten met Windows (dezelfde plek als de installer gebruikt)
    static class Autostart
    {
        const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run", Name = "Kapture";

        public static bool IsOn
        {
            get
            {
                try { using (RegistryKey k = Registry.CurrentUser.OpenSubKey(Key)) return k != null && k.GetValue(Name) != null; }
                catch { return false; }
            }
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
