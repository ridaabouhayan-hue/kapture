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
                        OverlayShot(dir, t, scale, true, "overlay-slepen-" + tag + ".png", true);
                        if (scale > 1) OverlayShot(dir, t, scale, false, "overlay-leeg-" + tag + ".png");
                        ToastShot(dir, t, scale, "melding-" + tag + ".png");
                        NoteToastShot(dir, t, scale, "melding-notitie-" + tag + ".png");
                        NoteBarShot(dir, t, scale, "notitiebalk-" + tag + ".png");
                        EditorShot(dir, t, scale, "editor-" + tag + ".png");
                        SettingsShot(dir, t, scale, 0, null, "instellingen-algemeen-" + tag + ".png");
                        if (scale > 1)
                        {
                            SettingsShot(dir, t, scale, 1, null, "instellingen-sneltoetsen-" + tag + ".png");
                            SettingsShot(dir, t, scale, 2, null, "instellingen-opslaan-" + tag + ".png");
                            SettingsShot(dir, t, scale, 3, null, "instellingen-notities-" + tag + ".png");
                            SettingsShot(dir, t, scale, 4, null, "instellingen-bewerken-" + tag + ".png");
                            SettingsShot(dir, t, scale, 5, null, "instellingen-over-" + tag + ".png");
                            SettingsShot(dir, t, scale, 0, "map", "instellingen-zoeken-" + tag + ".png");
                            SettingsEditedShot(dir, t, scale, tag);
                        }
                    }
                foreach (float scale in new float[] { 1f, 1.5f })
                    foreach (Theme t in new Theme[] { Theme.Light, Theme.DarkTheme })
                        MenuShot(dir, t, scale, "tray-menu-" + (t.Dark ? "donker" : "licht") + "-" + (int)(scale * 100) + ".png");
                ProblemToastShot(dir, Theme.Light, 1.5f, "melding-probleem-licht-150.png");
                ProblemToastShot(dir, Theme.DarkTheme, 1.5f, "melding-probleem-donker-150.png");

                CropTest();
                SaveTest();
                NoteTest();
                EditorTest();
                SearchTest();
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

        static void OverlayShot(string dir, Theme t, float scale, bool withFrame, string name) { OverlayShot(dir, t, scale, withFrame, name, false); }

        // 'dragging': terwijl je het kader sleept (dan hoort de werkbalk weg te zijn)
        static void OverlayShot(string dir, Theme t, float scale, bool withFrame, string name, bool dragging)
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
                o.SetDraggingForTest(dragging);
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

        // De melding met een notitie die je aan het typen bent
        static void NoteToastShot(string dir, Theme t, float scale, string name)
        {
            using (Bitmap desk = FakeDesktop((int)(640 * scale), (int)(300 * scale), scale))
            using (Toast toast = new Toast())
            {
                ToastContent c = new ToastContent();
                c.Thumb = Saver.Thumbnail(desk, 160, 96);
                c.Title = "Opgeslagen en gekopieerd"; c.Sub = "Preset: Nu naar Claude";
                c.ActionLabel = "Map openen"; c.ActionIcon = "folder-open";
                c.NoteField = true;
                using (Bitmap tb = toast.RenderForTest(c, t, scale, false, ""))
                    Save(dir, name.Replace(".png", "-leeg.png"), tb, t);
                using (Bitmap tb = toast.RenderForTest(c, t, scale, true, "Opslaan-knop werkt niet als je op Enter drukt"))
                    Save(dir, name, tb, t);
                c.Thumb.Dispose();
            }
        }

        // Shift+PrtSc: het kader met de notitiebalk eronder
        static void NoteBarShot(string dir, Theme t, float scale, string name)
        {
            int w = (int)(1440 * scale), h = (int)(900 * scale);
            using (Bitmap desk = FakeDesktop(w, h, scale))
            using (Dib d = ToDib(desk))
            using (Overlay o = new Overlay())
            {
                o.TestScale = scale;
                bool ok = o.Prepare(d, new Rectangle(0, 0, w, h), t, new Point(w / 2, h / 2), new Rectangle(0, 0, w, h), true);
                Check(ok, "notitiebalk voorbereiden " + name);
                if (!ok) return;
                Rectangle sel = new Rectangle((int)(150 * scale), (int)(200 * scale), (int)(620 * scale), (int)(340 * scale));
                DateTime when = new DateTime(2026, 11, 24, 0, 12, 0);
                o.SetNoteForTest(sel, "Opslaan-knop werkt niet", delegate(string n) { return Naming.Base(when, "Chrome", n) + ".png"; });
                o.Compose(new Rectangle(0, 0, w, h));
                using (Bitmap v = o.WorkBuffer.View()) v.Save(Path.Combine(dir, name), ImageFormat.Png);
                o.End();
            }
        }

        static void SettingsShot(string dir, Theme t, float scale, int section, string query, string name)
        {
            Settings cfg = new Settings();
            using (SettingsForm f = new SettingsForm(cfg, null))
            using (Bitmap b = f.RenderForTest(t, scale, section, query))
            {
                b.Save(Path.Combine(dir, name), ImageFormat.Png);
                Check(f.ZoneCountForTest > 5, "instellingen getekend " + name + " (" + f.ZoneCountForTest + " knoppen)");
            }
        }

        // De editor met een paar markeringen op een nep-bureaublad (Stitch-scherm 5)
        static void EditorShot(string dir, Theme t, float scale, string name)
        {
            Bitmap desk = FakeDesktop((int)(1280 * scale), (int)(720 * scale), scale);
            using (EditorForm f = new EditorForm(desk, "test.png", "Opslaan-knop werkt niet", null))
            {
                FillDemo(f.Doc, scale);
                using (Bitmap b = f.RenderForTest(t, scale, EditTool.Arrow, new Size((int)(1280 * scale), (int)(800 * scale))))
                    b.Save(Path.Combine(dir, name), ImageFormat.Png);
                Check(f.ZoneCountForTest > 15, "editor getekend " + name + " (" + f.ZoneCountForTest + " knoppen)");
            }
        }

        // Pijl, kader, nummer, tekst, markeerstift en een vervaagd stuk
        public static void FillDemo(EditDoc d, float s)
        {
            Mark a = new Mark { Tool = EditTool.Arrow, Width = 5 * s }; a.Pts.Add(new PointF(700 * s, 520 * s)); a.Pts.Add(new PointF(480 * s, 455 * s)); d.Add(a);
            Mark r = new Mark { Tool = EditTool.Rect, Width = 4 * s }; r.Pts.Add(new PointF(150 * s, 420 * s)); r.Pts.Add(new PointF(470 * s, 490 * s)); d.Add(r);
            Mark n = new Mark { Tool = EditTool.Number, Width = 4 * s, Number = 1 }; n.Pts.Add(new PointF(720 * s, 530 * s)); d.Add(n);
            Mark tx = new Mark { Tool = EditTool.Text, Color = Color.FromArgb(0xDC, 0x26, 0x26), FontSize = 30 * s, Text = "Deze knop doet niets" }; tx.Pts.Add(new PointF(560 * s, 580 * s)); d.Add(tx);
            Mark m = new Mark { Tool = EditTool.Marker, Color = Color.FromArgb(0xFA, 0xCC, 0x15), Width = 6 * s };
            for (int i = 0; i <= 10; i++) m.Pts.Add(new PointF((160 + i * 26) * s, 225 * s));
            d.Add(m);
            d.Blur(new Rectangle((int)(160 * s), (int)(330 * s), (int)(380 * s), (int)(40 * s)));
        }

        // Instellingen met een wijziging (Opslaan wordt blauw), en de bevestiging na Opslaan
        static void SettingsEditedShot(string dir, Theme t, float scale, string tag)
        {
            using (SettingsForm f = new SettingsForm(new Settings(), null))
            {
                using (Bitmap b = f.RenderForTest(t, scale, 2, null, true)) b.Save(Path.Combine(dir, "instellingen-gewijzigd-" + tag + ".png"), ImageFormat.Png);
                Check(f.Dirty, "instellingen: wijziging wordt gezien (" + tag + ")");
            }
            using (SettingsForm f = new SettingsForm(new Settings(), null))
            {
                f.ShowSavedForTest();
                using (Bitmap b = f.RenderForTest(t, scale, 0, null)) b.Save(Path.Combine(dir, "instellingen-opgeslagen-" + tag + ".png"), ImageFormat.Png);
                Check(!f.Dirty, "instellingen: zonder wijziging niets te bewaren (" + tag + ")");
            }
        }

        // Notitie achteraf: hernoemen, tekstbestand, en weer weghalen (in een tijdelijke map)
        static void NoteTest()
        {
            string folder = Path.Combine(Path.GetTempPath(), "kapture-zelftest-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(folder);
                DateTime when = new DateTime(2026, 11, 24, 0, 12, 0);
                string p = Path.Combine(folder, Naming.Base(when, "Chrome", "") + ".png");
                using (Bitmap b = FakeDesktop(200, 100, 1)) b.Save(p, ImageFormat.Png);
                Shot s = new Shot { Path = p, Time = when, App = "Chrome" };
                Notes.Apply(s, "Opslaan-knop werkt niet", true);
                Check(Path.GetFileName(s.Path) == "2026-11-24 0012 Chrome - Opslaan-knop werkt niet.png" && File.Exists(s.Path) && !File.Exists(p), "notitie hernoemt het bestand");
                string lang = "Eerste regel\nTweede regel met " + new string('x', 150);
                Notes.Apply(s, lang, true);
                Check(File.Exists(Notes.TextPath(s.Path)) && File.ReadAllText(Notes.TextPath(s.Path)).Contains("Tweede regel"), "lange notitie komt in een tekstbestand");
                Check(Path.GetFileNameWithoutExtension(s.Path).Length <= Naming.MaxLength, "naam blijft kort genoeg");
                string txt = Notes.TextPath(s.Path);
                Notes.Apply(s, "", true);
                Check(Path.GetFileName(s.Path) == "2026-11-24 0012 Chrome.png" && !File.Exists(txt), "notitie weghalen geeft de oude naam terug");
                Check(Directory.GetFiles(folder).Length == 1, "geen losse bestanden over na hernoemen");
                using (Bitmap img = new Bitmap(400, 200))
                using (Bitmap withNote = Notes.WithStrip(img, "Opslaan-knop werkt niet"))
                    Check(withNote.Height > 200 && withNote.Width == 400, "notitiestrook komt onder de afbeelding");
                History h = new History(Path.Combine(folder, "geschiedenis.json"));
                for (int i = 0; i < 12; i++) h.Add(new Shot { Path = s.Path, Time = when.AddMinutes(i), App = "Chrome", Note = i == 11 ? "laatste \"met\" aanhalingstekens" : "" });
                History back = History.Load(Path.Combine(folder, "geschiedenis.json"));
                Check(back.Items.Count == History.Max && back.Items[back.Items.Count - 1].Note == "laatste \"met\" aanhalingstekens", "geschiedenis bewaart de laatste 10");
            }
            catch (Exception ex) { Check(false, "notitietest: " + ex.GetType().Name); }
            finally { try { Directory.Delete(folder, true); } catch { } }
        }

        // Editor: ongedaan maken, vervagen is echt, uitsnijden, export
        static void EditorTest()
        {
            using (Bitmap img = FakeDesktop(400, 300, 1))
            {
                EditDoc d = new EditDoc(new Bitmap(img));
                Mark a = new Mark { Tool = EditTool.Arrow, Width = 4 }; a.Pts.Add(new PointF(10, 10)); a.Pts.Add(new PointF(100, 100));
                d.Add(a);
                Check(d.Marks.Count == 1 && d.CanUndo, "editor: pijl toegevoegd");
                d.Undo();
                Check(d.Marks.Count == 0 && d.CanRedo, "editor: ongedaan maken");
                d.Redo();
                Check(d.Marks.Count == 1, "editor: opnieuw");
                Color before = d.Image.GetPixel(130, 160);
                d.Blur(new Rectangle(120, 150, 80, 40));
                Color c1 = d.Image.GetPixel(121, 151), c2 = d.Image.GetPixel(122, 152);
                Check(c1 == c2, "editor: vervagen maakt blokjes van gelijke kleur");
                d.Undo();
                Check(d.Image.GetPixel(130, 160) == before, "editor: vervagen ongedaan maken zet de pixels terug");
                d.SetCrop(new Rectangle(50, 40, 200, 100));
                using (Bitmap e = d.Export()) Check(e.Width == 200 && e.Height == 100, "editor: uitsnijden geeft de goede maat");
                d.Undo();
                using (Bitmap e = d.Export()) Check(e.Width == 400 && e.Height == 300, "editor: uitsnijden ongedaan maken");
                Check(MarkRender.Hit(a, new PointF(55, 55), 2) && !MarkRender.Hit(a, new PointF(200, 20), 2), "editor: klikken op een pijl");
                Mark n2 = new Mark { Tool = EditTool.Number, Number = d.NextNumber() };
                Check(n2.Number == 1, "editor: eerste nummer is 1");
                d.Dispose();
            }
        }

        static void SearchTest()
        {
            using (SettingsForm f = new SettingsForm(new Settings(), null))
            {
                int all = 0, map = 0, none = 0;
                foreach (SettingsForm.Section sec in f.Sections)
                    foreach (SettingsForm.Card c in sec.Cards)
                        foreach (SettingsForm.Row r in c.Rows)
                        {
                            all++;
                            if (SettingsForm.Matches(r, "map")) map++;
                            if (SettingsForm.Matches(r, "zzqq")) none++;
                        }
                Check(all > 10 && map >= 2 && map < all && none == 0, "zoeken in instellingen (" + map + " van " + all + " voor \"map\")");
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

        static void MenuShot(string dir, Theme t, float scale, string name)
        {
            using (TrayMenu m = new TrayMenu())
            {
                App.FillMenu(m, new Settings(), true, delegate { }, delegate { }, delegate { }, delegate { }, delegate { }, delegate { }, delegate { });
                using (Bitmap b = m.RenderForTest(t, scale, 2))   // Nieuwe screenshot als gekozen regel, om de kleur te zien
                {
                    Save(dir, name, b, t);
                    Check(b.Width > 100 && b.Height > 100, "tray-menu getekend " + name + " (" + b.Width + "x" + b.Height + ")");
                }
                // marges nameten: elke regel even ver van links als van rechts, scheidingslijnen niet ingesprongen
                RectangleF card = m.CardForTest;
                bool even = true;
                foreach (RectangleF r in m.RowsForTest)
                    if (Math.Abs((r.X - card.X) - (card.Right - r.Right)) > 0.5f) even = false;
                Check(even, "tray-menu: links en rechts evenveel ruimte (" + name + ")");
                float top = m.RowsForTest[0].Y - card.Y, bottom = card.Bottom - m.RowsForTest[m.RowsForTest.Count - 1].Bottom;
                Check(Math.Abs(top - bottom) <= 0.5f, "tray-menu: boven en onder evenveel ruimte (" + name + ")");
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
