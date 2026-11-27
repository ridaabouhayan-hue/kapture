using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using Microsoft.Win32;

namespace Kapture
{
    // ------------------------------------------------------------------ Kleuren uit het ontwerpsysteem van Stitch (kapture_design_system.md)
    // Het matglas van Stitch wordt een effen vlak in dezelfde kleur (zie BOUWPLAN.md, Ontwerp).
    class Theme
    {
        public bool Dark;
        public Color Surface, SurfaceLow, Container, ContainerHigh, OnSurface, Variant, Outline, OutlineVariant,
            Primary, OnPrimary, PrimaryContainer, OnPrimaryContainer, Inverse, OnInverse, Error;
        // Fill: blauwe vlakken met witte tekst (knoppen, gekozen gereedschap, schakelaars). Ook in donker
        // dezelfde kobalt met wit, want lichtblauw met donkere tekst was slecht te lezen (Rida 24-11).
        // Page: de achtergrond achter de kaarten, duidelijk anders dan de kaarten zelf.
        public Color Fill = H(0x2563EB), OnFill = H(0xFFFFFF), Page;

        static Color H(int rgb) { return Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }

        public static readonly Theme Light = new Theme
        {
            Dark = false,
            Surface = H(0xFFFFFF), SurfaceLow = H(0xF3F3FE), Container = H(0xEDEDF9), ContainerHigh = H(0xE7E7F3),
            OnSurface = H(0x191B23), Variant = H(0x434655), Outline = H(0x737686), OutlineVariant = H(0xC3C6D7),
            Primary = H(0x2563EB), OnPrimary = H(0xFFFFFF), PrimaryContainer = H(0xDBE1FF), OnPrimaryContainer = H(0x00174B),
            Inverse = H(0x2E3039), OnInverse = H(0xF0F0FB), Error = H(0xBA1A1A),
            Page = H(0xECEEF6)
        };

        public static readonly Theme DarkTheme = new Theme
        {
            Dark = true,
            Surface = H(0x1D1F27), SurfaceLow = H(0x191B23), Container = H(0x282A32), ContainerHigh = H(0x32343D),
            OnSurface = H(0xE1E2ED), Variant = H(0xC3C6D7), Outline = H(0x8D90A0), OutlineVariant = H(0x434655),
            Primary = H(0x3B82F6), OnPrimary = H(0x0C0E16), PrimaryContainer = H(0x003EA8), OnPrimaryContainer = H(0xDBE1FF),
            Inverse = H(0x32343D), OnInverse = H(0xE1E2ED), Error = H(0xFFB4AB),
            Page = H(0x121318), Fill = H(0x2F6BF0)
        };

        // Keuze uit Instellingen: 0 = zoals Windows, 1 = licht, 2 = donker
        public static int Mode;

        // Volgt Windows: Instellingen > Persoonlijke instellingen > Kleuren > Kies je app-modus
        public static Theme Current
        {
            get
            {
                if (Program.ForceTheme != null) return Program.ForceTheme;
                if (Mode == 1) return Light;
                if (Mode == 2) return DarkTheme;
                try
                {
                    object v = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1);
                    if (v is int && (int)v == 0) return DarkTheme;
                }
                catch { }
                return Light;
            }
        }

        public static Color Mix(Color a, Color b, float t)
        {
            return Color.FromArgb(255, (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }
    }

    // ------------------------------------------------------------------ Lettertypes
    // De lettertypes uit het Stitch-ontwerp: Inter voor alle tekst, JetBrains Mono voor sneltoetsen en maten
    // (goedgekeurd door Rida op 27-11; OFL-licentie, in assets/fonts). Lukt laden niet, dan Segoe UI en Consolas.
    static class Fonts
    {
        static readonly Dictionary<string, Font> cache = new Dictionary<string, Font>();
        static readonly object gate = new object();
        // Inter en JetBrains Mono uit Kapture.exe zelf: per bestand een eigen verzameling, zodat elk gewicht zijn eigen
        // bestand houdt (Regular, Medium, SemiBold, Mono), ook als een systeem ze allemaal "Inter" noemt
        static readonly List<PrivateFontCollection> own = new List<PrivateFontCollection>();
        static readonly Dictionary<string, FontFamily> ownFamilies = new Dictionary<string, FontFamily>(StringComparer.OrdinalIgnoreCase);
        static bool loaded;

        // De lettertypes uit het Stitch-ontwerp laden uit de resources van Kapture.exe. Twee keer: voor GDI+ (DrawString)
        // en voor GDI (TextRenderer, scherpe ClearType-tekst). Alleen voor Kapture zelf; er wordt niets geïnstalleerd.
        static void Load()
        {
            if (loaded) return;
            loaded = true;
            try
            {
                System.Reflection.Assembly asm = typeof(Fonts).Assembly;
                foreach (string name in asm.GetManifestResourceNames())
                {
                    if (!name.StartsWith("Kapture.Fonts.", StringComparison.Ordinal) || !name.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)) continue;
                    byte[] data;
                    using (System.IO.Stream st = asm.GetManifestResourceStream(name))
                    using (System.IO.MemoryStream ms = new System.IO.MemoryStream()) { st.CopyTo(ms); data = ms.ToArray(); }
                    // het geheugen moet blijven bestaan zolang Kapture draait: niet vrijgeven
                    IntPtr mem = System.Runtime.InteropServices.Marshal.AllocCoTaskMem(data.Length);
                    System.Runtime.InteropServices.Marshal.Copy(data, 0, mem, data.Length);
                    PrivateFontCollection pfc = new PrivateFontCollection();
                    pfc.AddMemoryFont(mem, data.Length);
                    own.Add(pfc);
                    uint count = 0;
                    try { W.AddFontMemResourceEx(mem, (uint)data.Length, IntPtr.Zero, ref count); } catch { }
                    if (pfc.Families.Length == 0) continue;
                    string key = name.Contains("JetBrainsMono") ? "Mono" : name.Contains("SemiBold") ? "SemiBold" : name.Contains("Medium") ? "Medium" : "Regular";
                    ownFamilies[key] = pfc.Families[0];
                }
            }
            catch (Exception ex) { Log.Error("Lettertypes laden mislukt", ex); }
        }

        // gewicht: 0 = gewoon (400), 1 = halfvet (600, koppen), 2 = medium (500, knoppen en labels)
        public static Font Ui(float px, int weight)
        {
            lock (gate)
            {
                Load();
                string fam = weight == 1 ? "SemiBold" : weight == 2 ? "Medium" : "Regular";
                if (OnMono) return GetInstalled(weight == 1 ? "Inter SemiBold" : weight == 2 ? "Inter Medium" : "Inter", px);   // alleen de tests op Linux
                if (ownFamilies.ContainsKey(fam)) return Get(fam, px);
                // reserve: Segoe UI, net als NexusPen (Segoe UI Variable gaf gekartelde letters)
                return GetInstalled(weight >= 1 ? "Segoe UI Semibold" : "Segoe UI", px);
            }
        }

        // Sneltoetsen, maten en tijden (JetBrains Mono, zoals in Stitch)
        public static Font Mono(float px)
        {
            lock (gate)
            {
                Load();
                if (OnMono) return GetInstalled("JetBrains Mono", px);
                if (ownFamilies.ContainsKey("Mono")) return Get("Mono", px);
                return GetInstalled("Consolas", px);
            }
        }

        // Tekst die in de screenshot zelf komt (editor: tekst en nummers): vet, zoals altijd, met het lettertype van Windows.
        // De eigen lettertypes hebben geen vette stijl, en een opgeslagen screenshot hoort er altijd hetzelfde uit te zien.
        public static Font Markup(float px) { lock (gate) return GetInstalled("Segoe UI Semibold", px); }

        // Voor gewone Windows-tekstvakken (zoekbalk, notitievak)
        public static Font ForControl(float px, int weight)
        {
            return Ui(px, weight);
        }

        // Onder Mono (alleen de tests en schermafdrukken op Linux) tekent libgdiplus geen lettertypes uit het geheugen:
        // daar gebruiken we dezelfde lettertypes als ze in de map met lettertypes staan
        static readonly bool OnMono = Type.GetType("Mono.Runtime") != null;

        public static bool UsesStitchFonts { get { lock (gate) { Load(); return ownFamilies.ContainsKey("Regular"); } } }

        static Font Get(string fam, float px)
        {
            string key = "eigen|" + fam + "|" + px.ToString("0.00");
            Font f;
            if (!cache.TryGetValue(key, out f)) { f = new Font(ownFamilies[fam], px, FontStyle.Regular, GraphicsUnit.Pixel); cache[key] = f; }
            return f;
        }

        static Font GetInstalled(string fam, float px)
        {
            string key = fam + "|" + px.ToString("0.00");
            Font f;
            if (!cache.TryGetValue(key, out f)) { f = new Font(fam, px, FontStyle.Regular, GraphicsUnit.Pixel); cache[key] = f; }
            return f;
        }
    }
}
