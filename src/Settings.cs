using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Kapture
{
    // ------------------------------------------------------------------ Instellingen (%APPDATA%\Kapture\instellingen.txt, sleutel=waarde)
    class Settings
    {
        public string Inbox = "";          // leeg = de standaardmap (InboxFolder.Default)
        public int ToastSeconds = 6;
        public bool HotkeysPaused = false;
        public bool SnipHintShown = false;  // melding over de Windows-instelling voor Print Screen al gezien

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
                        case "inbox": s.Inbox = v; break;
                        case "meldingSeconden": if (int.TryParse(v, out n)) s.ToastSeconds = Math.Max(2, Math.Min(60, n)); break;
                        case "sneltoetsenPauze": s.HotkeysPaused = v == "1"; break;
                        case "knipprogrammaMelding": s.SnipHintShown = v == "1"; break;
                    }
                }
            }
            catch (Exception ex) { Log.Error("Instellingen lezen mislukt", ex); }
            return s;
        }

        public void Save() { Save(FilePath); }

        // Eerst naar een tijdelijk bestand, dan vervangen: zo is het bestand nooit half geschreven
        public void Save(string path)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("inbox=" + Inbox);
                sb.AppendLine("meldingSeconden=" + ToastSeconds);
                sb.AppendLine("sneltoetsenPauze=" + (HotkeysPaused ? "1" : "0"));
                sb.AppendLine("knipprogrammaMelding=" + (SnipHintShown ? "1" : "0"));
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, sb.ToString(), Encoding.UTF8);
                if (File.Exists(path)) File.Replace(tmp, path, null); else File.Move(tmp, path);
            }
            catch (Exception ex) { Log.Error("Instellingen bewaren mislukt", ex); }
        }
    }
}
