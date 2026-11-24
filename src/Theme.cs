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

        static Color H(int rgb) { return Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }

        public static readonly Theme Light = new Theme
        {
            Dark = false,
            Surface = H(0xFFFFFF), SurfaceLow = H(0xF3F3FE), Container = H(0xEDEDF9), ContainerHigh = H(0xE7E7F3),
            OnSurface = H(0x191B23), Variant = H(0x434655), Outline = H(0x737686), OutlineVariant = H(0xC3C6D7),
            Primary = H(0x2563EB), OnPrimary = H(0xFFFFFF), PrimaryContainer = H(0xDBE1FF), OnPrimaryContainer = H(0x00174B),
            Inverse = H(0x2E3039), OnInverse = H(0xF0F0FB), Error = H(0xBA1A1A)
        };

        public static readonly Theme DarkTheme = new Theme
        {
            Dark = true,
            Surface = H(0x1D1F27), SurfaceLow = H(0x191B23), Container = H(0x282A32), ContainerHigh = H(0x32343D),
            OnSurface = H(0xE1E2ED), Variant = H(0xC3C6D7), Outline = H(0x8D90A0), OutlineVariant = H(0x434655),
            Primary = H(0x3B82F6), OnPrimary = H(0x0C0E16), PrimaryContainer = H(0x003EA8), OnPrimaryContainer = H(0xDBE1FF),
            Inverse = H(0x32343D), OnInverse = H(0xE1E2ED), Error = H(0xFFB4AB)
        };

        // Volgt Windows: Instellingen > Persoonlijke instellingen > Kleuren > Kies je app-modus
        public static Theme Current
        {
            get
            {
                if (Program.ForceTheme != null) return Program.ForceTheme;
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
    // Voor nu de lettertypes van Windows 11 (Segoe UI Variable, Cascadia Mono). Inter en JetBrains Mono uit het
    // Stitch-ontwerp komen erbij zodra Rida het downloaden van die bestanden goedkeurt (BOUWPLAN.md, Techniek).
    static class Fonts
    {
        static HashSet<string> installed;
        static readonly Dictionary<string, Font> cache = new Dictionary<string, Font>();

        static bool Has(string name)
        {
            if (installed == null)
            {
                installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try { using (InstalledFontCollection c = new InstalledFontCollection()) foreach (FontFamily f in c.Families) installed.Add(f.Name); }
                catch { }
            }
            return installed.Contains(name);
        }

        static string Pick(params string[] names)
        {
            foreach (string n in names) if (Has(n)) return n;
            return "Segoe UI";
        }

        // gewicht: 0 = gewoon, 1 = halfvet
        public static Font Ui(float px, int weight)
        {
            string fam = weight >= 1 ? Pick("Segoe UI Variable Text Semibold", "Segoe UI Semibold") : Pick("Segoe UI Variable Text", "Segoe UI");
            return Get(fam, px, FontStyle.Regular);
        }

        public static Font Mono(float px) { return Get(Pick("Cascadia Mono", "Consolas"), px, FontStyle.Regular); }

        static Font Get(string fam, float px, FontStyle st)
        {
            string key = fam + "|" + px.ToString("0.00") + "|" + st;
            Font f;
            if (!cache.TryGetValue(key, out f)) { f = new Font(fam, px, st, GraphicsUnit.Pixel); cache[key] = f; }
            return f;
        }
    }
}
