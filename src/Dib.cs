using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Kapture
{
    // ------------------------------------------------------------------ Een stuk geheugen voor pixels waar Windows direct in kan tekenen
    // Een 4K-scherm kost 32 MB. Daarom alleen maken als het nodig is en daarna meteen weggooien (zie docs/SPIKE.md).
    sealed class Dib : IDisposable
    {
        public IntPtr Dc, Bits;
        IntPtr bmp, old;
        public readonly int Width, Height;

        Dib(int w, int h) { Width = w; Height = h; }

        // Geeft null als Windows de buffer niet kan maken (te weinig geheugen)
        public static Dib Create(int w, int h)
        {
            if (w <= 0 || h <= 0) return null;
            Dib d = new Dib(w, h);
            W.BITMAPINFO bi = new W.BITMAPINFO();
            bi.h.biSize = Marshal.SizeOf(typeof(W.BITMAPINFOHEADER)); bi.h.biWidth = w; bi.h.biHeight = -h; bi.h.biPlanes = 1; bi.h.biBitCount = 32;
            IntPtr scr = W.GetDC(IntPtr.Zero);
            d.Dc = W.CreateCompatibleDC(scr);
            d.bmp = W.CreateDIBSection(scr, ref bi, 0, out d.Bits, IntPtr.Zero, 0);
            W.ReleaseDC(IntPtr.Zero, scr);
            if (d.bmp == IntPtr.Zero || d.Bits == IntPtr.Zero) { W.DeleteDC(d.Dc); return null; }
            d.old = W.SelectObject(d.Dc, d.bmp);
            return d;
        }

        public int Stride { get { return Width * 4; } }

        // Een Bitmap die dezelfde pixels laat zien (geen kopie: alleen gebruiken zolang deze Dib bestaat)
        public Bitmap View() { W.GdiFlush(); return new Bitmap(Width, Height, Stride, PixelFormat.Format32bppRgb, Bits); }

        // Een losse kopie van een stuk, als gewone afbeelding zonder doorzichtigheid
        public Bitmap Crop(Rectangle r)
        {
            r.Intersect(new Rectangle(0, 0, Width, Height));
            if (r.Width <= 0 || r.Height <= 0) return null;
            using (Bitmap v = View()) return v.Clone(r, PixelFormat.Format24bppRgb);
        }

        // Alle pixels donkerder maken (factor 0..256), voor het donkere beeld achter het kader
        public static unsafe void Darken(Dib src, Dib dst, int factor)
        {
            W.GdiFlush();
            int n = src.Width * src.Height;
            uint* s = (uint*)src.Bits.ToPointer();
            uint* d = (uint*)dst.Bits.ToPointer();
            for (int i = 0; i < n; i++)
            {
                uint p = s[i];
                uint rb = ((p & 0xFF00FFu) * (uint)factor >> 8) & 0xFF00FFu;
                uint g = ((p & 0x00FF00u) * (uint)factor >> 8) & 0x00FF00u;
                d[i] = 0xFF000000u | rb | g;
            }
        }

        public void Dispose()
        {
            if (Dc == IntPtr.Zero) return;
            W.SelectObject(Dc, old); W.DeleteObject(bmp); W.DeleteDC(Dc);
            Dc = IntPtr.Zero; Bits = IntPtr.Zero;
        }
    }
}
