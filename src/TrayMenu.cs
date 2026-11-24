using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Kapture
{
    // ------------------------------------------------------------------ Het tray-menu, helemaal zelf getekend
    // Zelfde aanpak als de melding: een plaatje met doorzichtigheid (UpdateLayeredWindow), zodat de maten
    // op elk scherm en bij elke schaal precies kloppen. Sluit bij een klik ergens anders of met Esc.
    class TrayMenu : Form
    {
        public class Entry
        {
            public string Text, Icon, Hint, Chip;
            public bool Check, Sep, Header;
            public Action Click;
        }

        // Maten in pixels bij 100%. Alles op een raster van 2: overal evenveel ruimte rondom.
        public const float ShadowM = 16;      // ruimte voor de schaduw
        public const float Pad = 6;          // binnenrand van het menu tot het vlakje onder de muis
        public const float RowH = 36;        // hoogte van een regel
        public const float Inset = 12;       // van de rand van het vlakje tot icoon, en van sneltoets tot de rand
        public const float IconS = 16;       // icoon
        public const float Gap = 12;         // tussen icoon en tekst
        public const float HintGap = 32;     // minimaal tussen tekst en sneltoets
        public const float SepH = 9;         // hoogte van een scheidingslijn
        public const float HeaderH = 40;     // kop met logo, "Kapture" en de actieve preset
        public const float MinW = 220;       // minimale breedte van het menu
        public const float Radius = 8;       // hoeken van het menu
        public const float FontPx = 12;

        readonly List<Entry> entries = new List<Entry>();
        readonly List<RectangleF> rows = new List<RectangleF>();
        Theme t = Theme.Light;
        float s = 1;
        RectangleF card;
        int hot = -1;
        bool showing;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= W.WS_EX_LAYERED | W.WS_EX_TOOLWINDOW | W.WS_EX_TOPMOST;
                return cp;
            }
        }

        public TrayMenu()
        {
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None; KeyPreview = true;
            Text = "Kapture menu";
        }

        public void Clear() { entries.Clear(); }
        public void Add(string text, string icon, Action click) { Entry e = new Entry(); e.Text = text; e.Icon = icon; e.Click = click; entries.Add(e); }
        public void Add(string text, string icon, string hint, Action click) { Add(text, icon, click); entries[entries.Count - 1].Hint = hint; }
        public void AddCheck(string text, string icon, bool on, Action click) { Add(text, icon, click); entries[entries.Count - 1].Check = on; }
        public void AddSeparator() { Entry e = new Entry(); e.Sep = true; entries.Add(e); }

        // Een nieuwe versie staat klaar: bovenaan, onder de kop, een regel om bij te werken
        public void InsertUpdate(string text, Action click)
        {
            int at = entries.Count > 0 && entries[0].Header ? 2 : 0;
            Entry e = new Entry(); e.Text = text; e.Icon = "download"; e.Click = click;
            Entry sep = new Entry(); sep.Sep = true;
            entries.Insert(Math.Min(at, entries.Count), sep);
            entries.Insert(Math.Min(at, entries.Count), e);
        }
        // Kop bovenaan (Stitch-scherm 7): logo, naam en rechts de actieve preset; daaronder een lijn
        public void AddHeader(string text, string chip) { Entry e = new Entry(); e.Header = true; e.Text = text; e.Chip = chip; entries.Add(e); AddSeparator(); }

        float P(float v) { return (float)Math.Round(v * s); }

        // Maten berekenen en het venster zo groot maken als het menu plus schaduw
        void Arrange()
        {
            float textW = 0, hintW = 0;
            using (Bitmap mb = new Bitmap(1, 1)) using (Graphics mg = Graphics.FromImage(mb))
            {
                mg.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;   // dichte achtergrond: scherpe ClearType-tekst, zoals NexusPen
                foreach (Entry e in entries)
                {
                    if (e.Sep) continue;
                    if (e.Header)
                    {
                        float hw = mg.MeasureString(e.Text, Fonts.Ui(13 * s, 1), PointF.Empty, StringFormat.GenericTypographic).Width;
                        if (e.Chip != null) hw += P(HintGap) + mg.MeasureString(e.Chip, Fonts.Ui(11 * s, 1), PointF.Empty, StringFormat.GenericTypographic).Width + P(16);
                        textW = Math.Max(textW, hw);
                        continue;
                    }
                    textW = Math.Max(textW, mg.MeasureString(e.Text, Fonts.Ui(FontPx * s, 0), PointF.Empty, StringFormat.GenericTypographic).Width);
                    if (e.Hint != null) hintW = Math.Max(hintW, mg.MeasureString(e.Hint, Fonts.Mono(11 * s), PointF.Empty, StringFormat.GenericTypographic).Width + P(14));
                }
            }
            float m = P(ShadowM), pad = P(Pad);
            float inner = P(Inset) + P(IconS) + P(Gap) + (float)Math.Ceiling(textW) + (hintW > 0 ? P(HintGap) + (float)Math.Ceiling(hintW) : 0) + P(Inset);
            float w = Math.Max(P(MinW), pad + inner + pad);
            rows.Clear();
            float y = pad;
            foreach (Entry e in entries)
            {
                float h = e.Sep ? P(SepH) : (e.Header ? P(HeaderH) : P(RowH));
                rows.Add(new RectangleF(m + pad, m + y, w - 2 * pad, h));
                y += h;
            }
            float hgt = y + pad;
            card = new RectangleF(m, m, w, hgt);
            Size = new Size((int)Math.Ceiling(w + 2 * m), (int)Math.Ceiling(hgt + 2 * m));
        }

        public Bitmap RenderBitmap()
        {
            Bitmap b = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;   // dichte achtergrond: scherpe ClearType-tekst, zoals NexusPen

                // schaduw
                for (int i = 6; i >= 1; i--)
                {
                    RectangleF sh = card; sh.Inflate(P(i * 1.5f), P(i * 1.5f)); sh.Offset(0, P(4));
                    using (GraphicsPath gp = Svg.RoundRect(sh, P(Radius + i * 1.5f)))
                    using (SolidBrush br = new SolidBrush(Color.FromArgb(t.Dark ? 16 : 5, 0, 0, 0))) g.FillPath(br, gp);
                }
                // vlak met rand
                using (GraphicsPath gp = Svg.RoundRect(new RectangleF(card.X + 0.5f, card.Y + 0.5f, card.Width - 1, card.Height - 1), P(Radius)))
                using (SolidBrush br = new SolidBrush(t.Surface))
                using (Pen pen = new Pen(t.OutlineVariant, Math.Max(1, P(1))))
                {
                    g.FillPath(br, gp);
                    g.DrawPath(pen, gp);
                }

                Font f = Fonts.Ui(FontPx * s, 0);
                StringFormat sf = new StringFormat(StringFormat.GenericTypographic);
                sf.FormatFlags |= StringFormatFlags.NoWrap; sf.LineAlignment = StringAlignment.Center;
                for (int i = 0; i < entries.Count; i++)
                {
                    Entry e = entries[i];
                    RectangleF r = rows[i];
                    if (e.Sep)
                    {
                        // scheidingslijn over de hele breedte binnen het menu, even ver van links als van rechts
                        float ly = (float)Math.Floor(r.Y + r.Height / 2) + 0.5f;
                        using (Pen p = new Pen(t.OutlineVariant, Math.Max(1, P(1)))) g.DrawLine(p, card.X + P(Pad), ly, card.Right - P(Pad), ly);
                        continue;
                    }
                    if (e.Header) { DrawHeader(g, e, r); continue; }
                    bool off = e.Click == null;
                    bool h = i == hot && !off;
                    if (h)
                        using (GraphicsPath hp = Svg.RoundRect(r, P(6)))
                        using (SolidBrush hb = new SolidBrush(t.PrimaryContainer)) g.FillPath(hb, hp);
                    // icoon (op hele pixels), bij een aangevinkte regel een blauw vinkje
                    RectangleF ir = new RectangleF(r.X + P(Inset), (float)Math.Round(r.Y + (r.Height - P(IconS)) / 2), P(IconS), P(IconS));
                    Color iconC = off ? Color.FromArgb(110, t.Variant) : (h ? t.OnPrimaryContainer : t.Variant);
                    if (e.Check) Icons.Draw(g, "check", ir, IconS * s, h ? t.OnPrimaryContainer : t.Primary, 2.5f);
                    else if (e.Icon != null) Icons.Draw(g, e.Icon, ir, IconS * s, iconC, 2);
                    float tx = ir.Right + P(Gap);
                    using (SolidBrush fg = new SolidBrush(off ? Color.FromArgb(120, t.OnSurface) : (h ? t.OnPrimaryContainer : t.OnSurface)))
                        g.DrawString(e.Text, f, fg, new RectangleF(tx, r.Y, r.Right - tx - P(Inset), r.Height), sf);
                    if (e.Hint != null)
                    {
                        // sneltoets als een toetsje, rechts uitgelijnd
                        Font kf = Fonts.Mono(11 * s);
                        float kw = g.MeasureString(e.Hint, kf, PointF.Empty, StringFormat.GenericTypographic).Width + P(14);
                        RectangleF key = new RectangleF(r.Right - P(Inset) - kw, (float)Math.Round(r.Y + (r.Height - P(22)) / 2), kw, P(22));
                        using (GraphicsPath kp = Svg.RoundRect(key, P(5)))
                        using (SolidBrush kb = new SolidBrush(t.Surface))
                        using (Pen kpn = new Pen(t.OutlineVariant, Math.Max(1, P(1)))) { g.FillPath(kb, kp); g.DrawPath(kpn, kp); }
                        StringFormat kc = new StringFormat(sf); kc.Alignment = StringAlignment.Center;
                        using (SolidBrush fg = new SolidBrush(off ? Color.FromArgb(120, t.Variant) : t.Variant)) g.DrawString(e.Hint, kf, fg, key, kc);
                    }
                }
            }
            return b;
        }

        void DrawHeader(Graphics g, Entry e, RectangleF r)
        {
            RectangleF logo = new RectangleF(r.X + P(Inset) - P(2), (float)Math.Round(r.Y + (r.Height - P(20)) / 2), P(20), P(20));
            using (Bitmap lb = Logo.Bitmap((int)logo.Width))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(lb, logo);
            }
            StringFormat sf = new StringFormat(StringFormat.GenericTypographic);
            sf.FormatFlags |= StringFormatFlags.NoWrap; sf.LineAlignment = StringAlignment.Center;
            float tx = r.X + P(Inset) + P(IconS) + P(Gap);
            using (SolidBrush fg = new SolidBrush(t.OnSurface)) g.DrawString(e.Text, Fonts.Ui(13 * s, 1), fg, new RectangleF(tx, r.Y, r.Width, r.Height), sf);
            if (e.Chip != null)
            {
                Font cf = Fonts.Ui(11 * s, 1);
                float cw = g.MeasureString(e.Chip, cf, PointF.Empty, StringFormat.GenericTypographic).Width + P(16);
                RectangleF chip = new RectangleF(r.Right - P(Inset) + P(4) - cw, (float)Math.Round(r.Y + (r.Height - P(22)) / 2), cw, P(22));
                using (GraphicsPath cp = Svg.RoundRect(chip, P(11))) using (SolidBrush cb = new SolidBrush(t.PrimaryContainer)) g.FillPath(cb, cp);
                StringFormat cc = new StringFormat(sf); cc.Alignment = StringAlignment.Center;
                using (SolidBrush fg = new SolidBrush(t.OnPrimaryContainer)) g.DrawString(e.Chip, cf, fg, chip, cc);
            }
        }

        void Render()
        {
            using (Bitmap b = RenderBitmap())
            {
                IntPtr scr = W.GetDC(IntPtr.Zero);
                IntPtr mem = W.CreateCompatibleDC(scr);
                IntPtr hb = b.GetHbitmap(Color.FromArgb(0));
                IntPtr old = W.SelectObject(mem, hb);
                W.POINT pt = new W.POINT(Left, Top), src = new W.POINT(0, 0);
                W.SIZE sz = new W.SIZE(Width, Height);
                W.BLENDFUNCTION bf = new W.BLENDFUNCTION { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = 1 };
                W.UpdateLayeredWindow(Handle, scr, ref pt, ref sz, mem, ref src, 0, ref bf, W.ULW_ALPHA);
                W.SelectObject(mem, old); W.DeleteObject(hb); W.DeleteDC(mem); W.ReleaseDC(IntPtr.Zero, scr);
            }
        }

        // Toont het menu bij de muis (bij de klok): onderkant op de muis, binnen het scherm
        public void ShowAt(Point cursor)
        {
            t = Theme.Current;
            s = W.ScaleAt(cursor.X, cursor.Y);
            hot = -1;
            Arrange();
            Rectangle wa = Screen.FromPoint(cursor).WorkingArea;
            int m = (int)P(ShadowM);
            int x = cursor.X - m, y = cursor.Y - Height + m;
            if (x + Width - m > wa.Right) x = cursor.X - Width + m;
            if (x + m < wa.Left) x = wa.Left - m;
            if (y + m < wa.Top) y = cursor.Y - m;
            if (y + Height - m > wa.Bottom) y = wa.Bottom - Height + m;
            Location = new Point(x, y);
            showing = true;
            Render();
            Show();
            W.SetForegroundWindow(Handle);   // nodig bij een tray-menu, anders sluit hij niet bij een klik ernaast
            Activate();
        }

        public void HideMenu()
        {
            if (!showing) return;
            showing = false;
            Hide();
        }

        protected override void OnDeactivate(EventArgs e) { base.OnDeactivate(e); HideMenu(); }

        int HitTest(Point p)
        {
            for (int i = 0; i < rows.Count; i++) if (!entries[i].Sep && !entries[i].Header && rows[i].Contains(p)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = HitTest(e.Location);
            if (h != hot) { hot = h; Render(); }
        }

        protected override void OnMouseLeave(EventArgs e) { if (hot != -1) { hot = -1; Render(); } }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (!card.Contains(e.Location)) { HideMenu(); return; }
            int h = HitTest(e.Location);
            if (h >= 0) Choose(h);
        }

        void Choose(int i)
        {
            Action a = entries[i].Click;
            if (a == null) return;   // grijze regel: niets doen, menu blijft open
            HideMenu();
            // na het verbergen, zodat een screenshot het menu niet meer ziet
            BeginInvoke((MethodInvoker)delegate
            {
                try { a(); } catch (Exception ex) { Log.Error("Menukeuze mislukt", ex); }
            });
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys key)
        {
            if (key == Keys.Escape) { HideMenu(); return true; }
            if (key == Keys.Down || key == Keys.Up)
            {
                int n = entries.Count, i = hot >= 0 ? hot : (key == Keys.Down ? -1 : n);
                for (int k = 0; k < n; k++)
                {
                    i = key == Keys.Down ? (i + 1) % n : (i - 1 + n) % n;
                    if (!entries[i].Sep && !entries[i].Header && entries[i].Click != null) break;
                }
                hot = i; Render(); return true;
            }
            if ((key == Keys.Enter || key == Keys.Space) && hot >= 0) { Choose(hot); return true; }
            return base.ProcessCmdKey(ref msg, key);
        }

        // Voor de zelftest: zonder scherm het menu als plaatje maken, met een regel als gekozen
        public Bitmap RenderForTest(Theme theme, float scale, int hotRow)
        {
            t = theme; s = scale; hot = hotRow;
            Arrange();
            return RenderBitmap();
        }

        // Voor de tests: de vakjes van de regels, om de marges na te meten
        public RectangleF CardForTest { get { return card; } }
        public List<RectangleF> RowsForTest { get { return rows; } }
    }
}
