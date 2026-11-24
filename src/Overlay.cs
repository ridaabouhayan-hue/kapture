using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Kapture
{
    // ------------------------------------------------------------------ De werkbalk bovenaan (Stitch-scherm 1)
    class Toolbar
    {
        public enum Kind { Mode, Chip, Icon, Separator }
        public class Item
        {
            public string Key, Icon, Label; public Kind Kind; public bool Active, Enabled = true;
            public Rectangle Rect;
        }

        public readonly List<Item> Items = new List<Item>();
        public Rectangle Rect;
        float s;

        public Toolbar()
        {
            Items.Add(new Item { Key = "kader", Icon = "square-dashed", Label = "Kader", Kind = Kind.Mode, Active = true });
            Items.Add(new Item { Key = "vrij", Icon = "lasso", Label = "Vrij", Kind = Kind.Mode, Enabled = false });
            Items.Add(new Item { Key = "venster", Icon = "app-window", Label = "Venster", Kind = Kind.Mode, Enabled = false });
            Items.Add(new Item { Key = "scherm", Icon = "monitor", Label = "Hele scherm", Kind = Kind.Mode });
            Items.Add(new Item { Key = "", Kind = Kind.Separator });
            Items.Add(new Item { Key = "preset", Icon = "sparkles", Label = "Naar Claude", Kind = Kind.Chip, Enabled = false });
            Items.Add(new Item { Key = "sluiten", Icon = "x", Label = "Sluiten", Kind = Kind.Icon });
        }

        int P(float v) { return (int)Math.Round(v * s); }

        // Plaatst de balk bovenaan in het midden van 'monitor' (in de coordinaten van de overlay)
        public void Layout(Rectangle monitor, float scale, Graphics measure)
        {
            s = scale;
            Font f = Fonts.Ui(12 * s, 0), fb = Fonts.Ui(12 * s, 1);
            int pad = P(8), gap = P(6), h = P(36);
            int x = pad;
            foreach (Item it in Items)
            {
                int w;
                if (it.Kind == Kind.Separator) w = P(1) + 2 * P(4);
                else if (it.Kind == Kind.Icon) w = h;
                else
                {
                    Size ts = TextRenderer.MeasureText(measure, it.Label, it.Active ? fb : f, Size.Empty, TextFormatFlags.NoPadding);
                    w = P(14) + P(16) + P(8) + ts.Width + P(14);
                }
                it.Rect = new Rectangle(x, pad, w, h);
                x += w + gap;
            }
            x = x - gap + pad;
            Rect = new Rectangle(monitor.X + (monitor.Width - x) / 2, monitor.Y + P(24), x, h + 2 * pad);
            foreach (Item it in Items) it.Rect.Offset(Rect.X, Rect.Y);
        }

        public Item HitTest(Point p)
        {
            if (!Rect.Contains(p)) return null;
            foreach (Item it in Items) if (it.Kind != Kind.Separator && it.Rect.Contains(p)) return it;
            return null;
        }

        public Rectangle Bounds { get { Rectangle r = Rect; r.Inflate(P(16), P(16)); r.Height += P(8); return r; } }

        public void Draw(Graphics g, Theme t, Item hover)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            // zachte schaduw onder de balk
            for (int i = 3; i >= 1; i--)
            {
                Rectangle sh = Rect; sh.Inflate(P(2 * i), P(2 * i)); sh.Offset(0, P(4 + 2 * i));
                using (GraphicsPath gp = Svg.RoundRect(sh, P(12 + 2 * i)))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(t.Dark ? 34 : 18, 0, 0, 0))) g.FillPath(b, gp);
            }
            using (GraphicsPath gp = Svg.RoundRect(new RectangleF(Rect.X + 0.5f, Rect.Y + 0.5f, Rect.Width - 1, Rect.Height - 1), P(12)))
            using (SolidBrush b = new SolidBrush(t.Surface))
            using (Pen p = new Pen(t.OutlineVariant, Math.Max(1, P(1))))
            { g.FillPath(b, gp); g.DrawPath(p, gp); }

            Font f = Fonts.Ui(12 * s, 0), fb = Fonts.Ui(12 * s, 1);
            foreach (Item it in Items)
            {
                Rectangle r = it.Rect;
                if (it.Kind == Kind.Separator)
                {
                    using (SolidBrush b = new SolidBrush(t.OutlineVariant))
                        g.FillRectangle(b, r.X + P(4), r.Y + (r.Height - P(20)) / 2, Math.Max(1, P(1)), P(20));
                    continue;
                }
                Color bg = Color.Empty, fg = t.Variant;
                if (it.Active) { bg = t.Primary; fg = t.OnPrimary; }
                else if (it.Kind == Kind.Chip) { bg = t.PrimaryContainer; fg = t.OnPrimaryContainer; }
                else if (it == hover && it.Enabled) { bg = t.Dark ? t.Container : t.Container; fg = t.OnSurface; }
                if (!it.Enabled && it.Kind != Kind.Chip) fg = Color.FromArgb(110, t.Variant);
                if (bg != Color.Empty)
                    using (GraphicsPath gp = Svg.RoundRect(r, P(8)))
                    using (SolidBrush b = new SolidBrush(bg)) g.FillPath(b, gp);
                if (it.Kind == Kind.Icon)
                {
                    Icons.Draw(g, it.Icon, r, 16 * s, fg, 2);
                    continue;
                }
                int ix = r.X + P(14);
                float isz = it.Kind == Kind.Chip ? 14 * s : 16 * s;
                Icons.Draw(g, it.Icon, new RectangleF(ix, r.Y, isz, r.Height), isz, fg, 2);
                Rectangle tr = new Rectangle(ix + P(16) + P(8), r.Y, r.Right - (ix + P(24)), r.Height);
                TextRenderer.DrawText(g, it.Label, it.Active ? fb : f, tr, fg, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            }
        }
    }

    // ------------------------------------------------------------------ Het stilstaande, donkere beeld met het kader (Stitch-schermen 1 en 2)
    class Overlay : Form
    {
        public const int DimFactor = 141;   // gelijk aan het donkere venster: 45% zwart

        Dib bright, dimmed, work;
        Theme theme = Theme.Light;
        Rectangle sel = Rectangle.Empty, monitor;
        Point start;
        bool dragging, active, wasActivated;
        float s = 1;
        readonly Toolbar bar = new Toolbar();
        Toolbar.Item hover;

        public event Action<Rectangle> Selected;   // in de coordinaten van het beeld
        public event Action Cancelled;

        protected override CreateParams CreateParams
        {
            get { CreateParams cp = base.CreateParams; cp.ExStyle |= W.WS_EX_TOOLWINDOW | W.WS_EX_TOPMOST; return cp; }
        }

        public Overlay()
        {
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None; KeyPreview = true; Text = "Kapture";
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.Opaque, true);
            DoubleBuffered = false;
            Cursor = Cursors.Cross;
            IntPtr h = Handle;
        }

        public float TestScale;   // alleen voor de zelftest

        public bool IsActive { get { return active; } }
        public Rectangle Selection { get { return sel; } }

        int P(float v) { return (int)Math.Round(v * s); }

        // Voorbereiden: donker beeld maken en de werkbalk plaatsen. 'cursor' en 'origin' in schermcoordinaten.
        public bool Prepare(Dib capture, Rectangle origin, Theme t, Point cursor, Rectangle monitorScreen)
        {
            bright = capture; theme = t;
            dimmed = Dib.Create(capture.Width, capture.Height);
            work = Dib.Create(capture.Width, capture.Height);
            if (dimmed == null || work == null) { Release(); return false; }
            Dib.Darken(bright, dimmed, DimFactor);
            s = TestScale > 0 ? TestScale : W.ScaleAt(cursor.X, cursor.Y);
            monitor = monitorScreen; monitor.Offset(-origin.X, -origin.Y);
            using (Graphics g = Graphics.FromHdc(work.Dc)) bar.Layout(monitor, s, g);
            sel = Rectangle.Empty; dragging = false; hover = null; wasActivated = false;
            Bounds = origin;
            return true;
        }

        // Tonen en het toetsenbord pakken (de sneltoets geeft Kapture daar het recht toe)
        public void ShowNow()
        {
            active = true;
            Rectangle b = Bounds;
            W.SetWindowPos(Handle, W.HWND_TOPMOST, b.X, b.Y, b.Width, b.Height, W.SWP_SHOWWINDOW);
            Refresh();
            W.SetForegroundWindow(Handle);
            Activate();
            Focus();
        }

        public void End()
        {
            active = false; dragging = false;
            W.ShowWindow(Handle, W.SW_HIDE);
            Release();
        }

        void Release()
        {
            if (dimmed != null) { dimmed.Dispose(); dimmed = null; }
            if (work != null) { work.Dispose(); work = null; }
            bright = null;   // is van de aanroeper
        }

        // ---- tekenen: altijd eerst in 'work', dan in een keer naar het scherm (geen geflikker)
        public void Compose(Rectangle clip)
        {
            if (work == null) return;
            clip.Intersect(new Rectangle(0, 0, work.Width, work.Height));
            if (clip.Width <= 0 || clip.Height <= 0) return;
            W.BitBlt(work.Dc, clip.X, clip.Y, clip.Width, clip.Height, dimmed.Dc, clip.X, clip.Y, W.SRCCOPY);
            Rectangle lit = Rectangle.Intersect(sel, clip);
            if (lit.Width > 0 && lit.Height > 0)
                W.BitBlt(work.Dc, lit.X, lit.Y, lit.Width, lit.Height, bright.Dc, lit.X, lit.Y, W.SRCCOPY);
            using (Graphics g = Graphics.FromHdc(work.Dc))
            {
                g.SetClip(clip);
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                if (sel.Width > 0 && sel.Height > 0) DrawFrame(g);
                else DrawHint(g);
                if (clip.IntersectsWith(bar.Bounds)) bar.Draw(g, theme, hover);
            }
        }

        public Dib WorkBuffer { get { return work; } }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (work == null) return;
            Rectangle clip = e.ClipRectangle;
            Compose(clip);
            IntPtr hdc = e.Graphics.GetHdc();
            try { W.BitBlt(hdc, clip.X, clip.Y, clip.Width, clip.Height, work.Dc, clip.X, clip.Y, W.SRCCOPY); }
            finally { e.Graphics.ReleaseHdc(hdc); }
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        // Gestippelde rand (streepjes, zoals het Knipprogramma), net buiten het gekozen stuk, met acht handvatten
        void DrawFrame(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.None;
            int lw = Math.Max(1, P(1.5f));
            Rectangle r = sel; r.Inflate(lw, lw);
            using (Pen white = new Pen(Color.White, lw))
            using (Pen dash = new Pen(theme.Primary, lw))
            {
                white.Alignment = PenAlignment.Inset; dash.Alignment = PenAlignment.Inset;
                dash.DashPattern = new float[] { 4, 3 };
                g.DrawRectangle(white, r.X, r.Y, r.Width - 1, r.Height - 1);
                g.DrawRectangle(dash, r.X, r.Y, r.Width - 1, r.Height - 1);
            }
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (sel.Width >= P(28) && sel.Height >= P(28))
            {
                float d = 10 * s, bw = Math.Max(1.5f, 2 * s);
                float[] xs = { sel.X, sel.X + sel.Width / 2f, sel.Right }, ys = { sel.Y, sel.Y + sel.Height / 2f, sel.Bottom };
                using (SolidBrush fill = new SolidBrush(Color.White))
                using (Pen p = new Pen(theme.Primary, bw))
                    for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++)
                        {
                            if (i == 1 && j == 1) continue;
                            RectangleF h = new RectangleF(xs[i] - d / 2, ys[j] - d / 2, d, d);
                            g.FillEllipse(fill, h); g.DrawEllipse(p, h);
                        }
            }
            DrawLabel(g, sel.Width + " x " + sel.Height, LabelRect(sel));
        }

        Rectangle LabelRect(Rectangle r)
        {
            Font f = Fonts.Mono(11 * s);
            Size ts = TextRenderer.MeasureText(r.Width + " x " + r.Height, f, Size.Empty, TextFormatFlags.NoPadding);
            int w = ts.Width + P(20), h = P(24);
            int y = r.Y - h - P(8);
            if (y < monitor.Y + P(4)) y = r.Y + P(8);
            int x = r.X;
            if (y >= r.Y) x += P(8);
            return new Rectangle(x, y, w, h);
        }

        void DrawLabel(Graphics g, string text, Rectangle lr)
        {
            using (GraphicsPath gp = Svg.RoundRect(lr, P(6)))
            using (SolidBrush b = new SolidBrush(theme.Inverse)) g.FillPath(b, gp);
            TextRenderer.DrawText(g, text, Fonts.Mono(11 * s), lr, theme.OnInverse, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        }

        Rectangle HintRect()
        {
            int w = P(300), h = P(32);
            return new Rectangle(monitor.X + (monitor.Width - w) / 2, monitor.Bottom - h - P(32), w, h);
        }

        void DrawHint(Graphics g)
        {
            Rectangle r = HintRect();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath gp = Svg.RoundRect(r, P(16)))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(220, theme.Inverse))) g.FillPath(b, gp);
            TextRenderer.DrawText(g, "Sleep een kader  ·  Esc annuleert", Fonts.Ui(12 * s, 0), r, theme.OnInverse,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        }

        Rectangle FrameBounds(Rectangle r)
        {
            if (r.Width <= 0 || r.Height <= 0) return HintRect();
            Rectangle b = r; b.Inflate(P(10), P(10));
            return Rectangle.Union(b, Inflate(LabelRect(r), P(2)));
        }

        static Rectangle Inflate(Rectangle r, int n) { r.Inflate(n, n); return r; }

        void SetSelection(Rectangle next)
        {
            Rectangle old = sel;
            sel = next;
            Rectangle dirty = Rectangle.Union(FrameBounds(old), FrameBounds(next));
            Invalidate(dirty);
            Update();
        }

        public static Rectangle Norm(Point a, Point b)
        {
            int x = Math.Min(a.X, b.X), y = Math.Min(a.Y, b.Y);
            return new Rectangle(x, y, Math.Abs(a.X - b.X) + 1, Math.Abs(a.Y - b.Y) + 1);
        }

        // ---- muis en toetsen
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (!active) return;
            if (e.Button == MouseButtons.Right) { if (dragging) { dragging = false; SetSelection(Rectangle.Empty); } else Cancel(); return; }
            if (e.Button != MouseButtons.Left) return;
            Toolbar.Item it = bar.HitTest(e.Location);
            if (it != null) { OnToolbarClick(it); return; }
            if (bar.Rect.Contains(e.Location)) return;
            dragging = true; start = e.Location;
            Capture = true;
            SetSelection(Rectangle.Empty);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!active) return;
            if (dragging)
            {
                Point p = new Point(Math.Max(0, Math.Min(ClientSize.Width - 1, e.X)), Math.Max(0, Math.Min(ClientSize.Height - 1, e.Y)));
                Rectangle next = Norm(start, p);
                if (next != sel) SetSelection(next);
                return;
            }
            Toolbar.Item h = bar.HitTest(e.Location);
            if (h != null && !h.Enabled) h = null;
            if (h != hover) { hover = h; Invalidate(bar.Bounds); }
            Cursor want = bar.Rect.Contains(e.Location) ? Cursors.Default : Cursors.Cross;
            if (Cursor != want) Cursor = want;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (!active || !dragging || e.Button != MouseButtons.Left) return;
            dragging = false; Capture = false;
            if (sel.Width >= 4 && sel.Height >= 4) Finish(sel);
            else SetSelection(Rectangle.Empty);
        }

        void OnToolbarClick(Toolbar.Item it)
        {
            if (!it.Enabled) return;
            if (it.Key == "sluiten") Cancel();
            else if (it.Key == "scherm") Finish(monitor);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (active && keyData == Keys.Escape) { Cancel(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnActivated(EventArgs e) { base.OnActivated(e); if (active) wasActivated = true; }

        // Klikt iemand weg (Alt+Tab, een ander venster), dan stopt Kapture: nooit een donker scherm dat blijft hangen
        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            if (active && wasActivated && !dragging) Cancel();
        }

        void Finish(Rectangle r)
        {
            if (!active) return;
            active = false;
            Action<Rectangle> h = Selected;
            if (h != null) h(r);
        }

        public void Cancel()
        {
            if (!active) return;
            active = false;
            Action h = Cancelled;
            if (h != null) h();
        }

        // Voor de zelftest: een kader zetten zonder muis
        public void SetSelectionForTest(Rectangle r) { sel = r; }
    }
}
