using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Kapture
{
    // ------------------------------------------------------------------ Wat er in een melding staat
    class ToastContent
    {
        public Bitmap Thumb;            // miniatuur; null = icoon
        public string Icon = "check";   // als er geen miniatuur is
        public bool Ok = true;          // vinkje in een blauw rondje; false = waarschuwing
        public string Title = "", Sub = "";
        public string ActionLabel, ActionIcon; public Action OnAction;
        public Action OnThumb;
        public int Seconds = 6;
        public bool Stay;               // blijft staan tot je hem wegklikt (voor een probleem)
    }

    // ------------------------------------------------------------------ De melding rechtsonder (Stitch-scherm 3)
    // Pakt het toetsenbord niet af en blijft staan zolang de muis erboven hangt.
    class Toast : Form
    {
        ToastContent c;
        Theme t = Theme.Light;
        float s = 1;
        int margin;                     // ruimte voor de schaduw
        int bodyH;                      // hoogte van miniatuur plus tekst
        Rectangle card, thumbR, closeR, actionR, dismissR;
        string hot;                     // knop onder de muis
        readonly Timer tick = new Timer();
        DateTime shownAt; double elapsedMs, totalMs;
        bool hovering, closing;
        byte alpha;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= W.WS_EX_LAYERED | W.WS_EX_TOOLWINDOW | W.WS_EX_TOPMOST | W.WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        public Toast()
        {
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            tick.Interval = 33;
            tick.Tick += delegate { OnTick(); };
        }

        int P(float v) { return (int)Math.Round(v * s); }

        public void ShowToast(ToastContent content, Theme theme)
        {
            if (c != null && c.Thumb != null && c.Thumb != content.Thumb) c.Thumb.Dispose();
            c = content; t = theme;
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            s = W.ScaleAt(wa.Right - 10, wa.Bottom - 10);
            Arrange(wa);
            totalMs = Math.Max(2, c.Seconds) * 1000.0; elapsedMs = 0; shownAt = DateTime.Now;
            hovering = false; closing = false; hot = null;
            alpha = 0;
            Render();
            W.ShowWindow(Handle, W.SW_SHOWNOACTIVATE);
            W.SetWindowPos(Handle, W.HWND_TOPMOST, 0, 0, 0, 0, W.SWP_NOMOVE | W.SWP_NOSIZE | W.SWP_NOACTIVATE);
            tick.Start();
        }

        void Arrange(Rectangle wa)
        {
            margin = P(18);
            int w = P(388), pad = P(14);
            // de tweede regel mag over meerdere regels lopen: de melding wordt dan hoger
            int textW = w - pad - P(80) - P(12) - P(32) - pad;
            int subH;
            using (Bitmap mb = new Bitmap(1, 1)) using (Graphics mg = Graphics.FromImage(mb))
                subH = (int)Math.Ceiling(mg.MeasureString(c.Sub ?? "", Fonts.Ui(12 * s, 0), textW, Wrap()).Height);
            bodyH = Math.Max(P(48), P(2) + P(21) + subH + P(2));
            int h = pad + bodyH + P(12) + P(28) + pad + (c.Stay ? 0 : P(4));
            card = new Rectangle(margin, margin, w, h);
            Size = new Size(w + 2 * margin, h + 2 * margin);
            Location = new Point(wa.Right - Width - P(20) + margin, wa.Bottom - Height - P(16) + margin);
            thumbR = new Rectangle(card.X + pad, card.Y + pad, P(80), P(48));
            closeR = new Rectangle(card.Right - pad - P(32) + P(6), card.Y + pad - P(6), P(32), P(32));
            int ay = card.Y + pad + bodyH + P(12);
            actionR = Rectangle.Empty;
            if (c.ActionLabel != null)
            {
                SizeF ts = Measure(c.ActionLabel);
                actionR = new Rectangle(card.X + pad, ay, P(10) + (c.ActionIcon != null ? P(14) + P(6) : 0) + (int)ts.Width + P(10), P(28));
            }
            SizeF ds = Measure("Sluiten");
            dismissR = new Rectangle(card.Right - pad - ((int)ds.Width + P(16)), ay, (int)ds.Width + P(16), P(28));
        }

        SizeF Measure(string text)
        {
            using (Bitmap b = new Bitmap(1, 1)) using (Graphics g = Graphics.FromImage(b))
                return g.MeasureString(text, Fonts.Ui(12 * s, 0), PointF.Empty, StringFormat.GenericTypographic);
        }

        // Tekent de hele melding in een plaatje met doorzichtigheid en zet dat op het scherm
        public Bitmap RenderBitmap()
        {
            Bitmap b = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                // schaduw
                for (int i = 6; i >= 1; i--)
                {
                    Rectangle sh = card; sh.Inflate(P(i * 2), P(i * 2)); sh.Offset(0, P(6));
                    using (GraphicsPath gp = Svg.RoundRect(sh, P(12 + i * 2)))
                    using (SolidBrush br = new SolidBrush(Color.FromArgb(t.Dark ? 14 : 4, 0, 0, 0))) g.FillPath(br, gp);
                }
                using (GraphicsPath gp = Svg.RoundRect(new RectangleF(card.X + 0.5f, card.Y + 0.5f, card.Width - 1, card.Height - 1), P(12)))
                using (SolidBrush br = new SolidBrush(t.Surface))
                using (Pen pen = new Pen(t.OutlineVariant, Math.Max(1, P(1))))
                {
                    g.FillPath(br, gp);
                    // voortgangslijn onderaan, binnen de ronde hoeken
                    GraphicsState st = g.Save();
                    g.SetClip(gp);
                    Rectangle track = new Rectangle(card.X, card.Bottom - P(4), card.Width, P(4));
                    if (!c.Stay)
                    {
                        using (SolidBrush tb = new SolidBrush(t.ContainerHigh)) g.FillRectangle(tb, track);
                        float left = (float)Math.Max(0, 1 - elapsedMs / totalMs);
                        using (SolidBrush pb = new SolidBrush(t.Primary)) g.FillRectangle(pb, track.X, track.Y, track.Width * left, track.Height);
                    }
                    g.Restore(st);
                    g.DrawPath(pen, gp);
                }

                // miniatuur of icoon
                using (GraphicsPath tp = Svg.RoundRect(thumbR, P(6)))
                {
                    if (c.Thumb != null)
                    {
                        GraphicsState st = g.Save();
                        g.SetClip(tp);
                        using (SolidBrush cb = new SolidBrush(t.Container)) g.FillRectangle(cb, thumbR);
                        float k = Math.Max((float)thumbR.Width / c.Thumb.Width, (float)thumbR.Height / c.Thumb.Height);
                        float w = c.Thumb.Width * k, h = c.Thumb.Height * k;
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.DrawImage(c.Thumb, thumbR.X + (thumbR.Width - w) / 2, thumbR.Y + (thumbR.Height - h) / 2, w, h);
                        g.Restore(st);
                        using (Pen p = new Pen(t.OutlineVariant, Math.Max(1, P(1)))) g.DrawPath(p, tp);
                        if (hot == "thumb") using (Pen p = new Pen(t.Primary, Math.Max(1, P(2)))) g.DrawPath(p, tp);
                    }
                    else
                    {
                        using (SolidBrush cb = new SolidBrush(c.Ok ? t.PrimaryContainer : t.Container)) g.FillPath(cb, tp);
                        Icons.Draw(g, c.Icon, thumbR, 22 * s, c.Ok ? t.Primary : t.Error, 2);
                    }
                }

                // titel met vinkje, en de tweede regel
                int tx = thumbR.Right + P(12);
                int textRight = closeR.X - P(4);
                float ty = card.Y + P(14) + P(2);
                if (c.Thumb != null && c.Ok)
                {
                    RectangleF dot = new RectangleF(tx, ty + P(1), P(16), P(16));
                    using (SolidBrush db = new SolidBrush(t.PrimaryContainer)) g.FillEllipse(db, dot);
                    Icons.Draw(g, "check", dot, 12 * s, t.Primary, 2.5f);
                    tx += P(16) + P(6);
                }
                using (SolidBrush fg = new SolidBrush(t.OnSurface))
                    g.DrawString(c.Title, Fonts.Ui(13 * s, 1), fg, new RectangleF(tx, ty, textRight - tx, P(20)), NoWrap());
                using (SolidBrush fg2 = new SolidBrush(t.Variant))
                    g.DrawString(c.Sub, Fonts.Ui(12 * s, 0), fg2, new RectangleF(thumbR.Right + P(12), ty + P(21), textRight - thumbR.Right - P(12), bodyH), Wrap());

                // sluitknop
                if (hot == "close") using (GraphicsPath hp = Svg.RoundRect(closeR, P(8))) using (SolidBrush hb = new SolidBrush(t.Container)) g.FillPath(hb, hp);
                Icons.Draw(g, "x", closeR, 16 * s, t.Variant, 2);

                // knoppen
                if (c.ActionLabel != null) DrawButton(g, actionR, c.ActionIcon, c.ActionLabel, hot == "action", true);
                DrawButton(g, dismissR, null, "Sluiten", hot == "dismiss", false);
            }
            return b;
        }

        void DrawButton(Graphics g, Rectangle r, string icon, string label, bool hover, bool primary)
        {
            Color bg = primary ? (hover ? t.ContainerHigh : t.Container) : (hover ? t.Container : Color.Empty);
            if (bg != Color.Empty) using (GraphicsPath gp = Svg.RoundRect(r, P(6))) using (SolidBrush b = new SolidBrush(bg)) g.FillPath(b, gp);
            int x = r.X + P(10);
            if (icon != null) { Icons.Draw(g, icon, new RectangleF(x, r.Y, P(14), r.Height), 14 * s, t.Primary, 2); x += P(14) + P(6); }
            using (SolidBrush fg = new SolidBrush(primary ? t.OnSurface : t.Variant))
            {
                StringFormat sf = NoWrap(); sf.LineAlignment = StringAlignment.Center;
                if (icon == null && !primary) { sf.Alignment = StringAlignment.Center; g.DrawString(label, Fonts.Ui(12 * s, 0), fg, r, sf); }
                else g.DrawString(label, Fonts.Ui(12 * s, 0), fg, new RectangleF(x, r.Y, r.Right - x, r.Height), sf);
            }
        }

        static StringFormat NoWrap() { StringFormat f = new StringFormat(StringFormat.GenericTypographic); f.FormatFlags |= StringFormatFlags.NoWrap; f.Trimming = StringTrimming.EllipsisCharacter; return f; }
        static StringFormat Wrap() { StringFormat f = new StringFormat(StringFormat.GenericTypographic); f.Trimming = StringTrimming.EllipsisWord; return f; }

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
                W.BLENDFUNCTION bf = new W.BLENDFUNCTION { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = alpha, AlphaFormat = 1 };
                W.UpdateLayeredWindow(Handle, scr, ref pt, ref sz, mem, ref src, 0, ref bf, W.ULW_ALPHA);
                W.SelectObject(mem, old); W.DeleteObject(hb); W.DeleteDC(mem); W.ReleaseDC(IntPtr.Zero, scr);
            }
        }

        void OnTick()
        {
            DateTime now = DateTime.Now;
            double dt = (now - shownAt).TotalMilliseconds; shownAt = now;
            if (closing)
            {
                int a = alpha - (int)(255 * dt / 150);
                if (a <= 0)
                {
                    alpha = 0; tick.Stop(); W.ShowWindow(Handle, W.SW_HIDE);
                    if (c != null && c.Thumb != null) { c.Thumb.Dispose(); c.Thumb = null; }
                    return;
                }
                alpha = (byte)a; Render(); return;
            }
            if (alpha < 255) alpha = (byte)Math.Min(255, alpha + (int)(255 * dt / 200));
            if (!hovering && !c.Stay) elapsedMs += dt;
            if (!c.Stay && elapsedMs >= totalMs) { closing = true; return; }
            Render();
        }

        public void Dismiss() { if (Visible && !closing) { closing = true; shownAt = DateTime.Now; tick.Start(); } }

        string HitTest(Point p)
        {
            if (closeR.Contains(p)) return "close";
            if (c.ActionLabel != null && actionR.Contains(p)) return "action";
            if (dismissR.Contains(p)) return "dismiss";
            if (c.Thumb != null && c.OnThumb != null && thumbR.Contains(p)) return "thumb";
            return null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool inside = card.Contains(e.Location);
            if (inside != hovering) hovering = inside;
            string h = inside ? HitTest(e.Location) : null;
            Cursor = h != null ? Cursors.Hand : Cursors.Default;
            if (h != hot) { hot = h; Render(); }
        }

        protected override void OnMouseLeave(EventArgs e) { hovering = false; if (hot != null) { hot = null; Render(); } }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || c == null) return;
            string h = HitTest(e.Location);
            try
            {
                if (h == "close" || h == "dismiss") Dismiss();
                else if (h == "action" && c.OnAction != null) { c.OnAction(); Dismiss(); }
                else if (h == "thumb" && c.OnThumb != null) c.OnThumb();
            }
            catch (Exception ex) { Log.Error("Knop in de melding mislukt", ex); }
        }

        // Voor de zelftest: zonder scherm de melding als plaatje maken
        public Bitmap RenderForTest(ToastContent content, Theme theme, float scale)
        {
            c = content; t = theme; s = scale; alpha = 255; totalMs = 6000; elapsedMs = 2000;
            Arrange(new Rectangle(0, 0, 2000, 1200));
            return RenderBitmap();
        }
    }
}
