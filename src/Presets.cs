using System;
using System.IO;

namespace Kapture
{
    // ------------------------------------------------------------------ Wat er na een screenshot gebeurt (BOUWPLAN, Presets)
    // Een preset is een lijst stappen (gegevens), geen code: waar hij heen gaat, en welke stappen aan staan.
    // De stappen kun je per preset aan- of uitzetten in Instellingen, Presets (zoals de "na het vastleggen"-taken van ShareX).
    // Kiezen doe je met de knop in de werkbalk (klik wisselt) of met 1 tot en met 5 in de overlay. Kapture onthoudt de keuze.
    static class Presets
    {
        public const int NowToClaude = 0;   // opslaan en kopiëren (met notitiestrook als er een notitie is)
        public const int SaveForClaude = 1; // alleen opslaan; het klembord blijft zoals het was
        public const int Forward = 2;       // opslaan en meteen de editor; nooit de notitie
        public const int ChooseLater = 3;   // "Kies zelf": niets vanzelf; in de melding kies je Bewaren of Kopiëren
        public const int SaveIn = 4;        // "Opslaan in...": je kiest de map (recente mappen of een andere)

        public static readonly string[] Names = { "Nu naar Claude", "Bewaren voor Claude", "Doorsturen", "Kies zelf", "Opslaan in..." };
        public static readonly string[] Chips = { "Naar Claude", "Bewaren", "Doorsturen", "Kies zelf", "Opslaan in" };
        public static readonly string[] Icons = { "sparkles", "bookmark", "send", "list-checks", "folder-open" };
        public static readonly string[] About =
        {
            "Opslaan in je inbox en op het klembord, klaar om te plakken.",
            "Alleen opslaan in je inbox; wat er op je klembord stond blijft staan.",
            "Opslaan en meteen de editor open, om pijlen te zetten of te vervagen. Zonder notitie.",
            "Eerst niets: in de melding kies je Bewaren of Kopiëren. Bewaar je niet, dan ruimt Kapture hem later op.",
            "Na het vastleggen kies je de map: een van de laatste mappen of een andere.",
        };

        // ---- de stappen (bits), per preset aan of uit
        public const int StepCopyImage = 1;   // afbeelding op het klembord
        public const int StepCopyFile = 2;    // het bestand op het klembord (plakken in Verkenner, mail of WhatsApp als bijlage)
        public const int StepEditor = 4;      // meteen de editor openen
        public const int StepToast = 8;       // de melding rechtsonder
        public const int StepNote = 16;       // notitie mag (notitieveld, Shift+PrtSc, Ctrl+PrtSc)

        public static readonly int[] Steps = { StepCopyImage, StepCopyFile, StepEditor, StepToast, StepNote };
        public static readonly string[] StepNames = { "Afbeelding kopiëren", "Bestand kopiëren", "Editor openen", "Melding tonen", "Notitie" };
        public static readonly string[] StepAbout =
        {
            "De afbeelding op het klembord, om te plakken in Claude, Word of de browser.",
            "Het bestand zelf op het klembord, om te plakken als bijlage of in een map.",
            "Meteen de editor open na het opslaan.",
            "De melding rechtsonder met de miniatuur, het notitieveld en de knoppen.",
            "Een notitie typen bij de screenshot (notitieveld, Shift+PrtSc, Ctrl+PrtSc).",
        };

        // Standaard per preset
        public static readonly int[] Defaults =
        {
            StepCopyImage | StepToast | StepNote,   // Nu naar Claude
            StepToast | StepNote,                   // Bewaren voor Claude
            StepEditor,                             // Doorsturen: nooit een notitie
            StepToast | StepNote,                   // Kies zelf: de melding is hier de keuze
            StepToast | StepNote,                   // Opslaan in...
        };

        // Stappen die bij een preset vast staan (niet te wijzigen in Instellingen)
        public static bool Locked(int preset, int step)
        {
            if (preset == Forward && step == StepNote) return true;         // afspraak: Doorsturen nooit met notitie
            if (preset == ChooseLater && step == StepToast) return true;    // zonder melding kun je niets kiezen
            if (preset == ChooseLater && (step == StepCopyImage || step == StepCopyFile)) return true;   // kopiëren kies je in de melding
            return false;
        }

        public static int Clamp(int i) { return i < 0 || i >= Names.Length ? NowToClaude : i; }

        // De stappen van een preset zoals ze nu ingesteld zijn (met de vaste stappen altijd zoals bedoeld)
        public static int StepsOf(Settings c, int preset)
        {
            preset = Clamp(preset);
            int v = c != null && c.PresetSteps != null && preset < c.PresetSteps.Length && c.PresetSteps[preset] >= 0 ? c.PresetSteps[preset] : Defaults[preset];
            foreach (int st in Steps)
                if (Locked(preset, st)) v = (v & ~st) | (Defaults[preset] & st);
            return v;
        }

        public static bool Has(Settings c, int preset, int step) { return (StepsOf(c, preset) & step) != 0; }

        public static int IndexOf(string name)
        {
            for (int i = 0; i < Names.Length; i++) if (Names[i] == name) return i;
            return NowToClaude;
        }

        // ---- "Kies zelf": tijdelijk bewaren tot je kiest
        public static string TempFolder
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kapture", "tijdelijk"); }
        }

        public static bool IsTemp(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            try { return Path.GetFullPath(path).StartsWith(Path.GetFullPath(TempFolder) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }

        // Tijdelijke screenshots die ouder zijn dan een dag weggooien (bij het opstarten)
        public static void CleanTemp()
        {
            try
            {
                if (!Directory.Exists(TempFolder)) return;
                foreach (string f in Directory.GetFiles(TempFolder))
                    try { if (DateTime.Now - File.GetLastWriteTime(f) > TimeSpan.FromDays(1)) File.Delete(f); } catch { }
            }
            catch (Exception ex) { Log.Error("Tijdelijke screenshots opruimen mislukt", ex); }
        }
    }
}
