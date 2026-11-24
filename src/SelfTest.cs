using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace Kapture
{
    // ------------------------------------------------------------------ Zelftest: Kapture.exe --zelftest [map]
    // Tekent elk scherm met een NEP-bureaublad (nooit het echte scherm) in licht en donker, op 100% en 150%,
    // en bewaart de plaatjes in de map, zodat Claude ze kan bekijken. Test ook opslaan en uitsnijden.
    static class SelfTest
    {
        static int fails;

        static void Check(bool ok, string what)
        {
            Console.WriteLine((ok ? "goed  " : "FOUT  ") + what);
            if (!ok) fails++;
        }

        public static int Run(string dir)
        {
            Directory.CreateDirectory(dir);
            Console.WriteLine("Kapture " + Program.Version + " zelftest");
            try
            {
                foreach (int size in new int[] { 256, 48, 32, 16 })
                    using (Bitmap b = Logo.Bitmap(size)) b.Save(Path.Combine(dir, "logo-" + size + ".png"), ImageFormat.Png);

                foreach (float scale in new float[] { 1f, 1.5f })
                    foreach (Theme t in new Theme[] { Theme.Light, Theme.DarkTheme })
                    {
                        string tag = (t.Dark ? "donker" : "licht") + "-" + (int)(scale * 100);
                        OverlayShot(dir, t, scale, true, "overlay-kader-" + tag + ".png");
                        if (scale > 1) OverlayShot(dir, t, scale, false, "overlay-leeg-" + tag + ".png");
                        ToastShot(dir, t, scale, "melding-" + tag + ".png");
                    }
                ProblemToastShot(dir, Theme.Light, 1.5f, "melding-probleem-licht-150.png");
                ProblemToastShot(dir, Theme.DarkTheme, 1.5f, "melding-probleem-donker-150.png");

                CropTest();
                SaveTest();
                SpeedTest();
            }
            catch (Exception ex)
            {
                Console.WriteLine("FOUT  zelftest gestopt: " + ex);
                fails++;
            }
            Console.WriteLine(fails == 0 ? "Zelftest geslaagd" : "Zelftest: " + fails + " fout(en)");
            return fails == 0 ? 0 : 1;
        }

        // Kapture.exe --proef-echt: een echte ronde op dit scherm (donker, vastleggen, overlay tonen) en dan annuleren.
        // Bewaart niets en kopieert niets; meet alleen de tijden.
        public static int Live()
        {
            using (DimWindow dim = new DimWindow())
            using (Overlay o = new Overlay())
            {
                GdiCapture cap = new GdiCapture();
                for (int round = 0; round < 3; round++)
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    Rectangle v = VirtualScreen.Bounds;
                    if (dim.Excluded) dim.ShowOver(v);
                    W.DwmFlush();
                    long tDark = sw.ElapsedMilliseconds;
                    Dib shot = cap.Grab(v);
                    if (shot == null) { Console.WriteLine("FOUT  vastleggen mislukt"); return 1; }
                    Point cur = System.Windows.Forms.Cursor.Position;
                    bool ok = o.Prepare(shot, v, Theme.Current, cur, System.Windows.Forms.Screen.FromPoint(cur).Bounds);
                    if (!ok) { Console.WriteLine("FOUT  overlay voorbereiden mislukt"); shot.Dispose(); return 1; }
                    o.ShowNow();
                    dim.HideNow();
                    W.DwmFlush();
                    long tReady = sw.ElapsedMilliseconds;
                    System.Windows.Forms.Application.DoEvents();
                    System.Threading.Thread.Sleep(150);
                    o.Cancel(); o.End(); shot.Dispose();
                    Console.WriteLine("ronde " + (round + 1) + ": donker na " + tDark + " ms, overlay klaar na " + tReady + " ms, virtueel scherm " + v.Width + "x" + v.Height + ", schaal " + W.ScaleAt(cur.X, cur.Y));
                    System.Threading.Thread.Sleep(300);
                }
            }
            return 0;
        }

        // Een nep-bureaublad met een paar vensters, zonder echte gegevens
        static Bitmap FakeDesktop(int w, int h, float s)
        {
            Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppRgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                using (LinearGradientBrush lg = new LinearGradientBrush(new Rectangle(0, 0, w, h), Color.FromArgb(0x5B, 0x6E, 0xA8), Color.FromArgb(0xB9, 0xC3, 0xE6), 30f))
                    g.FillRectangle(lg, 0, 0, w, h);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle win = new Rectangle((int)(120 * s), (int)(140 * s), (int)(900 * s), (int)(560 * s));
                using (SolidBrush white = new SolidBrush(Color.White)) g.FillRectangle(white, win);
                using (SolidBrush bar = new SolidBrush(Color.FromArgb(0xEE, 0xF0, 0xF5))) g.FillRectangle(bar, win.X, win.Y, win.Width, (int)(40 * s));
                using (Font f = new Font("Segoe UI", 20 * s, GraphicsUnit.Pixel))
                using (SolidBrush ink = new SolidBrush(Color.FromArgb(0x19, 0x1B, 0x23)))
                {
                    g.DrawString("Profiel bewerken", f, ink, win.X + 40 * s, win.Y + 70 * s);
                    using (Font f2 = new Font("Segoe UI", 14 * s, GraphicsUnit.Pixel)) g.DrawString("Naam", f2, ink, win.X + 40 * s, win.Y + 130 * s);
                }
                using (Pen p = new Pen(Color.FromArgb(0xC3, 0xC6, 0xD7), s))
                {
                    g.DrawRectangle(p, win.X + 40 * s, win.Y + 156 * s, 380 * s, 36 * s);
                    g.DrawRectangle(p, win.X + 40 * s, win.Y + 236 * s, 380 * s, 36 * s);
                }
                using (SolidBrush btn = new SolidBrush(Color.FromArgb(0x25, 0x63, 0xEB))) g.FillRectangle(btn, win.X + 40 * s, win.Y + 300 * s, 110 * s, 36 * s);
                using (SolidBrush task = new SolidBrush(Color.FromArgb(230, 0xF3, 0xF3, 0xFE))) g.FillRectangle(task, 0, h - 48 * s, w, 48 * s);
            }
            return b;
        }

        static Dib ToDib(Bitmap src)
        {
            Dib d = Dib.Create(src.Width, src.Height);
            using (Graphics g = Graphics.FromHdc(d.Dc)) g.DrawImageUnscaled(src, 0, 0);
            return d;
        }

        static void OverlayShot(string dir, Theme t, float scale, bool withFrame, string name)
        {
            int w = (int)(1440 * scale), h = (int)(900 * scale);
            using (Bitmap desk = FakeDesktop(w, h, scale))
            using (Dib d = ToDib(desk))
            using (Overlay o = new Overlay())
            {
                o.TestScale = scale;
                bool ok = o.Prepare(d, new Rectangle(0, 0, w, h), t, new Point(w / 2, h / 2), new Rectangle(0, 0, w, h));
                Check(ok, "overlay voorbereiden " + name);
                if (!ok) return;
                if (withFrame) o.SetSelectionForTest(new Rectangle((int)(150 * scale), (int)(260 * scale), (int)(560 * scale), (int)(340 * scale)));
                o.Compose(new Rectangle(0, 0, w, h));
                using (Bitmap v = o.WorkBuffer.View()) v.Save(Path.Combine(dir, name), ImageFormat.Png);
                o.End();
            }
        }

        static void ToastShot(string dir, Theme t, float scale, string name)
        {
            using (Bitmap desk = FakeDesktop((int)(640 * scale), (int)(300 * scale), scale))
            using (Toast toast = new Toast())
            {
                ToastContent c = new ToastContent();
                c.Thumb = Saver.Thumbnail(desk, 160, 96);
                c.Title = "Opgeslagen en gekopieerd"; c.Sub = "Preset: Naar Claude";
                c.ActionLabel = "Map openen"; c.ActionIcon = "folder-open";
                using (Bitmap tb = toast.RenderForTest(c, t, scale))
                    Save(dir, name, tb, t);
                c.Thumb.Dispose();
            }
        }

        static void ProblemToastShot(string dir, Theme t, float scale, string name)
        {
            using (Toast toast = new Toast())
            {
                ToastContent c = new ToastContent();
                c.Ok = false; c.Icon = "alert"; c.Stay = true;
                c.Title = "Zet Print Screen vrij voor Kapture";
                c.Sub = "Windows opent nu het Knipprogramma met Print Screen. Zet in de instelling \"Print Screen gebruiken om het Knipprogramma te openen\" uit.";
                c.ActionLabel = "Open instelling"; c.ActionIcon = "settings-2";
                using (Bitmap tb = toast.RenderForTest(c, t, scale)) Save(dir, name, tb, t);
            }
        }

        // De melding op een effen achtergrond van het thema, zodat de schaduw te zien is
        static void Save(string dir, string name, Bitmap tb, Theme t)
        {
            using (Bitmap outB = new Bitmap(tb.Width + 40, tb.Height + 40, PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(outB))
            {
                g.Clear(t.Dark ? Color.FromArgb(0x11, 0x13, 0x1B) : Color.FromArgb(0xF4, 0xF5, 0xFA));
                g.DrawImageUnscaled(tb, 20, 20);
                outB.Save(Path.Combine(dir, name), ImageFormat.Png);
            }
        }

        // Uitsnijden: de pixels in het bestand zijn precies die van het scherm
        static void CropTest()
        {
            using (Bitmap desk = FakeDesktop(800, 600, 1))
            using (Dib d = ToDib(desk))
            using (Bitmap piece = d.Crop(new Rectangle(100, 120, 300, 200)))
            {
                Check(piece.Width == 300 && piece.Height == 200, "uitsnede heeft de goede maat");
                bool same = true;
                int[,] pts = { { 0, 0 }, { 299, 199 }, { 150, 100 }, { 20, 180 } };
                for (int i = 0; i < 4; i++)
                {
                    Color a = piece.GetPixel(pts[i, 0], pts[i, 1]), b = desk.GetPixel(100 + pts[i, 0], 120 + pts[i, 1]);
                    if (a.R != b.R || a.G != b.G || a.B != b.B) same = false;
                }
                Check(same, "uitsnede heeft dezelfde pixels als het scherm");
            }
        }

        // Opslaan in een tijdelijke testmap (nooit in de echte inbox)
        static void SaveTest()
        {
            string folder = Path.Combine(Path.GetTempPath(), "kapture-zelftest-" + Guid.NewGuid().ToString("N"));
            try
            {
                byte[] png;
                using (Bitmap b = FakeDesktop(200, 100, 1))
                using (MemoryStream ms = new MemoryStream()) { b.Save(ms, ImageFormat.Png); png = ms.ToArray(); }
                string base1 = Naming.Base(new DateTime(2026, 11, 24, 0, 12, 0), "Chrome", "");
                string p1 = Saver.WriteFile(png, folder, base1);
                string p2 = Saver.WriteFile(png, folder, base1);
                Check(Path.GetFileName(p1) == "2026-11-24 0012 Chrome.png", "bestandsnaam: " + Path.GetFileName(p1));
                Check(Path.GetFileName(p2) == "2026-11-24 0012 Chrome (2).png", "tweede bestand krijgt (2)");
                Check(Directory.GetFiles(folder, "*.tmp").Length == 0, "geen tijdelijke bestanden over");
                using (Image back = Image.FromFile(p1)) Check(back.Width == 200 && back.Height == 100, "opgeslagen PNG is leesbaar");
            }
            finally { try { Directory.Delete(folder, true); } catch { } }
        }

        static void SpeedTest()
        {
            using (Dib a = Dib.Create(3840, 2160))
            using (Dib b = Dib.Create(3840, 2160))
            {
                Check(a != null && b != null, "4K-buffers maken");
                Stopwatch sw = Stopwatch.StartNew();
                Dib.Darken(a, b, Overlay.DimFactor);
                sw.Stop();
                Console.WriteLine("info  4K donker maken: " + sw.ElapsedMilliseconds + " ms");
                Check(sw.ElapsedMilliseconds < 400, "4K donker maken binnen 400 ms");
            }
        }
    }
}
