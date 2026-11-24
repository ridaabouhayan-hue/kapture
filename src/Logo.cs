using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace Kapture
{
    // ------------------------------------------------------------------ Logo: Kader-K van Stitch (cobalt 5a.1)
    // 48 px en groter: witte tegel, kobalt kader met vier handvatten en de K.
    // 32 px en kleiner: alleen de K, wit op een kobalt tegel (kader en handvatten zijn dan niet meer te zien).
    // Dit bestand mag alleen Svg.cs gebruiken: tools/MaakIcoon.cs bouwt er het .ico mee voordat Kapture zelf gebouwd wordt.
    static class Logo
    {
        public static readonly Color Cobalt = Color.FromArgb(0x25, 0x63, 0xEB);
        public static readonly Color Ink = Color.FromArgb(0x15, 0x17, 0x1C);
        const string KTop = "M21 21H26.5V31.2C29.6 29.6 33 27.6 35.8 25.2C38 23.3 39.8 21.6 42.2 21C41.8 25.6 39.6 29.6 36 32.4C33 34.7 29.5 35.9 26.5 38.8V44H21Z";
        const string KLeg = "M30.6 35.6C33.4 33.9 36.6 34.6 38.6 37.4C40 39.4 41 42.2 43.4 44H37.6C35.9 42.8 35 40.6 33.7 38.8C32.8 37.5 31.8 36.4 30.6 35.6Z";

        static GraphicsPath Round(float x, float y, float w, float h, float r)
        {
            return Svg.RoundRect(new RectangleF(x, y, w, h), r);
        }

        // Tekent het logo in een vak van 'size' pixels (raster van 64)
        public static void Draw(Graphics g, float x, float y, float size)
        {
            GraphicsState st = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TranslateTransform(x, y);
            g.ScaleTransform(size / 64f, size / 64f);
            using (GraphicsPath k1 = Svg.Path(KTop))
            using (GraphicsPath k2 = Svg.Path(KLeg))
            {
                if (size >= 48)
                {
                    // lijnen dikker naarmate het logo kleiner wordt, anders verdwijnen ze
                    float line = size >= 200 ? 1.2f : size >= 100 ? 1.4f : size >= 60 ? 2f : 2.4f;
                    using (GraphicsPath tile = Round(4, 4, 56, 56, 15))
                    using (SolidBrush white = new SolidBrush(Color.White))
                    using (Pen ink = new Pen(Ink, line))
                    using (Pen blue = new Pen(Cobalt, line))
                    using (SolidBrush cob = new SolidBrush(Cobalt))
                    {
                        g.FillPath(white, tile);
                        g.DrawPath(ink, tile);
                        g.DrawRectangle(blue, 13, 13, 38, 38);
                        float hs = size >= 60 ? 4 : 5, ho = hs / 2;
                        float[] cs = { 13, 51 };
                        foreach (float hx in cs) foreach (float hy in cs)
                            {
                                if (size >= 60) { g.FillRectangle(white, hx - ho, hy - ho, hs, hs); g.DrawRectangle(blue, hx - ho, hy - ho, hs, hs); }
                                else g.FillRectangle(cob, hx - ho, hy - ho, hs, hs);
                            }
                        g.FillPath(cob, k1); g.FillPath(cob, k2);
                    }
                }
                else
                {
                    using (GraphicsPath tile = Round(4, 4, 56, 56, 15))
                    using (SolidBrush cob = new SolidBrush(Cobalt))
                    using (SolidBrush white = new SolidBrush(Color.White))
                    {
                        g.FillPath(cob, tile);
                        g.TranslateTransform(-6.2f, -5.6f);
                        g.ScaleTransform(1.2f, 1.2f);
                        g.FillPath(white, k1); g.FillPath(white, k2);
                    }
                }
            }
            g.Restore(st);
        }

        public static Bitmap Bitmap(int size)
        {
            Bitmap b = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(b)) { g.Clear(Color.Transparent); Draw(g, 0, 0, size); }
            return b;
        }

        public static readonly int[] IcoSizes = { 16, 20, 24, 32, 40, 48, 64, 96, 128, 256 };

        // Een .ico met PNG-plaatjes in alle maten
        public static void WriteIco(string path)
        {
            List<byte[]> pngs = new List<byte[]>();
            foreach (int s in IcoSizes)
                using (Bitmap b = Bitmap(s))
                using (MemoryStream ms = new MemoryStream()) { b.Save(ms, ImageFormat.Png); pngs.Add(ms.ToArray()); }
            using (FileStream fs = File.Create(path))
            using (BinaryWriter w = new BinaryWriter(fs))
            {
                w.Write((short)0); w.Write((short)1); w.Write((short)IcoSizes.Length);
                int offset = 6 + 16 * IcoSizes.Length;
                for (int i = 0; i < IcoSizes.Length; i++)
                {
                    int s = IcoSizes[i];
                    w.Write((byte)(s >= 256 ? 0 : s)); w.Write((byte)(s >= 256 ? 0 : s));
                    w.Write((byte)0); w.Write((byte)0); w.Write((short)1); w.Write((short)32);
                    w.Write(pngs[i].Length); w.Write(offset);
                    offset += pngs[i].Length;
                }
                foreach (byte[] p in pngs) w.Write(p);
            }
        }
    }
}
