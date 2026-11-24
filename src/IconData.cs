// Lucide-icoontjes (ISC-licentie, zie assets/LICENSE-Lucide.txt), paden uit lucide-static.
// p = pad, c = cirkel (cx cy r), r = rechthoek (x y b h hoek), l = lijn (x1 y1 x2 y2).
using System.Collections.Generic;

namespace Kapture
{
    static class IconData
    {
        public static readonly Dictionary<string, string[]> All = new Dictionary<string, string[]>
        {
            { "square-dashed", new string[] { "p|M5 3a2 2 0 0 0-2 2", "p|M19 3a2 2 0 0 1 2 2", "p|M21 19a2 2 0 0 1-2 2", "p|M5 21a2 2 0 0 1-2-2", "p|M9 3h1", "p|M9 21h1", "p|M14 3h1", "p|M14 21h1", "p|M3 9v1", "p|M21 9v1", "p|M3 14v1", "p|M21 14v1" } },
            { "lasso", new string[] { "p|M7 22a5 5 0 0 1-2-4", "p|M7 16.93c.96.43 1.96.74 2.99.91", "p|M3.34 14A6.8 6.8 0 0 1 2 10c0-4.42 4.48-8 10-8s10 3.58 10 8a7.19 7.19 0 0 1-.33 2", "p|M5 18a2 2 0 1 0 0-4 2 2 0 0 0 0 4z", "p|M14.33 22h-.09a.35.35 0 0 1-.24-.32v-10a.34.34 0 0 1 .33-.34c.08 0 .15.03.21.08l7.34 6a.33.33 0 0 1-.21.59h-4.49l-2.57 3.85a.35.35 0 0 1-.28.14z" } },
            { "app-window", new string[] { "r|2 4 20 16 2", "p|M10 4v4", "p|M2 8h20", "p|M6 4v4" } },
            { "monitor", new string[] { "r|2 3 20 14 2", "l|8 21 16 21", "l|12 17 12 21" } },
            { "sparkles", new string[] { "p|M9.937 15.5A2 2 0 0 0 8.5 14.063l-6.135-1.582a.5.5 0 0 1 0-.962L8.5 9.936A2 2 0 0 0 9.937 8.5l1.582-6.135a.5.5 0 0 1 .963 0L14.063 8.5A2 2 0 0 0 15.5 9.937l6.135 1.581a.5.5 0 0 1 0 .964L15.5 14.063a2 2 0 0 0-1.437 1.437l-1.582 6.135a.5.5 0 0 1-.963 0z", "p|M20 3v4", "p|M22 5h-4", "p|M4 17v2", "p|M5 18H3" } },
            { "chevron-down", new string[] { "p|m6 9 6 6 6-6" } },
            { "x", new string[] { "p|M18 6 6 18", "p|m6 6 12 12" } },
            { "check", new string[] { "p|M20 6 9 17l-5-5" } },
            { "folder-open", new string[] { "p|m6 14 1.5-2.9A2 2 0 0 1 9.24 10H20a2 2 0 0 1 1.94 2.5l-1.54 6a2 2 0 0 1-1.95 1.5H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h3.9a2 2 0 0 1 1.69.9l.81 1.2a2 2 0 0 0 1.67.9H18a2 2 0 0 1 2 2v2" } },
            { "camera", new string[] { "p|M14.5 4h-5L7 7H4a2 2 0 0 0-2 2v9a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2V9a2 2 0 0 0-2-2h-3l-2.5-3z", "c|12 13 3" } },
            { "pause", new string[] { "r|14 4 4 16 1", "r|6 4 4 16 1" } },
            { "power", new string[] { "p|M12 2v10", "p|M18.4 6.6a9 9 0 1 1-12.77.04" } },
            { "alert", new string[] { "c|12 12 10", "l|12 8 12 12", "l|12 16 12.01 16" } },
            { "settings-2", new string[] { "p|M20 7h-9", "p|M14 17H5", "c|17 17 3", "c|7 7 3" } }
        };
    }
}
