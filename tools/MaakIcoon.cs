// Maakt out\Kapsure.ico (en logo-PNG's) uit src/Logo.cs, voordat Kapsure zelf gebouwd wordt.
using System;
using System.Drawing.Imaging;
using System.IO;

namespace Kapture
{
    static class MaakIcoon
    {
        static int Main(string[] args)
        {
            string ico = args.Length > 0 ? args[0] : "Kapture.ico";
            Logo.WriteIco(ico);
            if (args.Length > 1)
            {
                Directory.CreateDirectory(args[1]);
                foreach (int s in new int[] { 16, 24, 32, 48, 64, 128, 256, 512, 1024 })
                    using (System.Drawing.Bitmap b = Logo.Bitmap(s)) b.Save(Path.Combine(args[1], "kapture-" + s + ".png"), ImageFormat.Png);
            }
            Console.WriteLine("Icoon gemaakt: " + ico);
            return 0;
        }
    }
}
