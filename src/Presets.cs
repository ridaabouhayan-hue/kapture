namespace Kapture
{
    // ------------------------------------------------------------------ Wat er na een screenshot gebeurt (BOUWPLAN, Presets)
    // Kies je met de knop in de werkbalk (klik wisselt) of met 1, 2, 3 in de overlay. Kapture onthoudt de laatste keuze.
    static class Presets
    {
        public const int NowToClaude = 0;   // opslaan en kopiëren (met notitiestrook als er een notitie is)
        public const int SaveForClaude = 1; // alleen opslaan; het klembord blijft zoals het was
        public const int Forward = 2;       // opslaan en meteen de editor; nooit de notitie

        public static readonly string[] Names = { "Nu naar Claude", "Bewaren voor Claude", "Doorsturen" };
        public static readonly string[] Chips = { "Naar Claude", "Bewaren", "Doorsturen" };
        public static readonly string[] Icons = { "sparkles", "bookmark", "send" };

        public static int Clamp(int i) { return i < 0 || i >= Names.Length ? NowToClaude : i; }
    }
}
