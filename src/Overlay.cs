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
            Items.Add(new Item { Key = "notitie", Icon = "sticky-note", Label = "Met notitie", Kind = Kind.Icon });
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

        public Item Find(string key) { foreach (Item it in Items) if (it.Key == key) return it; return null; }

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
                if (it.Active && it.Kind == Kind.Icon) { bg = t.PrimaryContainer; fg = t.Primary; }
                else if (it.Active) { bg = t.Fill; fg = t.OnFill; }
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

        // Met notitie (Shift+PrtSc of de knop in de werkbalk): na loslaten blijft het kader staan met de notitiebalk eronder
        bool withNote, noteMode;
        readonly TextField note = new TextField();
        Rectangle noteBar, noteName;
        Func<string, string> preview;
        readonly Timer blink = new Timer { Interval = 265 };

        public event Action<Rectangle> Selected;   // in de coordinaten van het beeld
        public event Action Cancelled;
        public event Action<string> NoteDone;      // de notitie; null = Esc (bewaren zonder notitie)

        public bool WithNote { get { return withNote; } }
        public bool InNoteMode { get { return noteMode; } }

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
            W.NoAnimation(h);
            note.Changed += delegate { if (noteMode) RelayoutNote(); };
            blink.Tick += delegate { if (noteMode) Invalidate(noteBar); };
        }

        public float TestScale;   // alleen voor de zelftest

        public bool IsActive { get { return active; } }
        public Rectangle Selection { get { return sel; } }

        int P(float v) { return (int)Math.Round(v * s); }

        // Voorbereiden: donker beeld maken en de werkbalk plaatsen. 'cursor' en 'origin' in schermcoordinaten.
        public bool Prepare(Dib capture, Rectangle origin, Theme t, Point cursor, Rectangle monitorScreen) { return Prepare(capture, origin, t, cursor, monitorScreen, false); }

        public bool Prepare(Dib capture, Rectangle origin, Theme t, Point cursor, Rectangle monitorScreen, bool note)
        {
            bright = capture; theme = t;
            withNote = note; noteMode = false; noteBar = noteName = Rectangle.Empty;
            bar.Find("notitie").Active = note;
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
            // eerst onzichtbaar tonen en tekenen, dan pas laten zien: zo is er nooit een leeg venster of flits
            W.Cloak(Handle, true);
            W.SetWindowPos(Handle, W.HWND_TOPMOST, b.X, b.Y, b.Width, b.Height, W.SWP_SHOWWINDOW);
            Refresh();
            W.Cloak(Handle, false);
            W.DwmFlush();   // het beeld staat op het scherm voordat het donkere venster weggaat
            W.SetForegroundWindow(Handle);
            Activate();
            Focus();
        }

        public void End()
        {
            active = false; dragging = false; noteMode = false; blink.Stop();
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
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;   // dichte achtergrond: scherpe ClearType-tekst, zoals NexusPen
                if (sel.Width > 0 && sel.Height > 0) DrawFrame(g);
                else DrawHint(g);
                if (clip.IntersectsWith(bar.Bounds)) bar.Draw(g, theme, hover);
                if (noteMode) DrawNoteBar(g);
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
            int w = P(360), h = P(32);
            return new Rectangle(monitor.X + (monitor.Width - w) / 2, monitor.Bottom - h - P(32), w, h);
        }

        void DrawHint(Graphics g)
        {
            Rectangle r = HintRect();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath gp = Svg.RoundRect(r, P(16)))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(220, theme.Inverse))) g.FillPath(b, gp);
            TextRenderer.DrawText(g, "Sleep een kader  ·  Esc of rechtsklik annuleert", Fonts.Ui(12 * s, 0), r, theme.OnInverse,
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
            if (noteMode) { if (e.Button == MouseButtons.Right) FinishNote(false); return; }
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
            if (it.Key == "notitie") { withNote = !withNote; it.Active = withNote; Invalidate(bar.Bounds); return; }
            if (it.Key == "sluiten") Cancel();
            else if (it.Key == "scherm") Finish(monitor);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (noteMode)
            {
                if (keyData == Keys.Escape) { FinishNote(false); return true; }
                if (keyData == Keys.Enter) { FinishNote(true); return true; }
                if (keyData == (Keys.Enter | Keys.Shift)) { note.NewLine(); return true; }
                if (note.OnKey(keyData)) { Invalidate(noteBar); return true; }
                return base.ProcessCmdKey(ref msg, keyData);
            }
            if (active && keyData == Keys.Escape) { Cancel(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            if (noteMode && note.OnChar(e.KeyChar)) { e.Handled = true; Invalidate(noteBar); }
        }

        protected override void OnActivated(EventArgs e) { base.OnActivated(e); if (active) wasActivated = true; }

        // Klikt iemand weg (Alt+Tab, een ander venster), dan stopt Kapture: nooit een donker scherm dat blijft hangen
        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            if (noteMode) { FinishNote(true); return; }   // weggeklikt: de notitie tot nu toe bewaren
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

        // ---- de notitiebalk onder het kader (Stitch-scherm 2)
        // 'namePreview' geeft bij een notitie de bestandsnaam die ontstaat, zodat je ziet wat Claude straks ziet.
        // 'pasteText': wat er voor de screenshot op het klembord stond (daarna staat de screenshot er zelf op)
        public void BeginNote(Rectangle r, Func<string, string> namePreview, string pasteText)
        {
            sel = r; preview = namePreview;
            note.Set(""); note.AllowNewLine = true;
            note.PasteText = delegate { return pasteText; };
            noteMode = true;
            RelayoutNote();
            blink.Start();
            W.SetForegroundWindow(Handle); Activate(); Focus();
        }

        Font NoteFont { get { return Fonts.Ui(14 * s, 0); } }

        void RelayoutNote()
        {
            Rectangle old = Rectangle.Union(noteBar, noteName);
            int lineH = (int)Math.Ceiling(TextField.LineHeight(NoteFont));
            int w = Math.Max(sel.Width, P(560));
            w = Math.Min(w, monitor.Width - P(32));
            int innerW = w - P(16) - P(18) - P(12) - ChipsWidth() - P(16);
            int lines = Math.Min(4, note.Lines(NoteFont, innerW));
            int h = Math.Max(P(48), P(14) * 2 + lines * lineH);
            int x = Math.Max(monitor.X + P(16), Math.Min(sel.X, monitor.Right - P(16) - w));
            int y = sel.Bottom + P(12);
            int nameH = P(26);
            if (y + h + P(8) + nameH > monitor.Bottom - P(8)) y = sel.Y - P(12) - h - P(8) - nameH;   // past niet onder het kader: erboven
            if (y < monitor.Y + P(8)) y = monitor.Bottom - P(16) - h - P(8) - nameH;                  // past ook niet erboven: onderin het scherm
            noteBar = new Rectangle(x, y, w, h);
            noteName = new Rectangle(x, y + h + P(8), w, nameH);
            Rectangle dirty = Rectangle.Union(old.IsEmpty ? noteBar : old, Rectangle.Union(noteBar, noteName));
            dirty.Inflate(P(16), P(16));
            Invalidate(dirty);
        }

        int ChipsWidth()
        {
            using (Bitmap b = new Bitmap(1, 1)) using (Graphics g = Graphics.FromImage(b))
            {
                float a = g.MeasureString("Al opgeslagen en gekopieerd", Fonts.Ui(12 * s, 1), PointF.Empty, StringFormat.GenericTypographic).Width;
                float k = g.MeasureString("Enter", Fonts.Mono(11 * s), PointF.Empty, StringFormat.GenericTypographic).Width;
                return (int)Math.Ceiling(P(10) + P(14) + P(6) + a + P(10) + P(8) + k + P(14));
            }
        }

        void DrawNoteBar(Graphics g)
        {
            Theme t = theme;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            for (int i = 3; i >= 1; i--)
            {
                Rectangle sh = noteBar; sh.Inflate(P(2 * i), P(2 * i)); sh.Offset(0, P(4 + 2 * i));
                using (GraphicsPath gp = Svg.RoundRect(sh, P(12 + 2 * i)))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(t.Dark ? 34 : 18, 0, 0, 0))) g.FillPath(b, gp);
            }
            using (GraphicsPath gp = Svg.RoundRect(new RectangleF(noteBar.X + 0.5f, noteBar.Y + 0.5f, noteBar.Width - 1, noteBar.Height - 1), P(12)))
            using (SolidBrush b = new SolidBrush(t.SurfaceLow))
            using (Pen p = new Pen(t.OutlineVariant, Math.Max(1, P(1))))
            { g.FillPath(b, gp); g.DrawPath(p, gp); }

            int lineH = (int)Math.Ceiling(TextField.LineHeight(NoteFont));
            int top = noteBar.Y + (noteBar.Height > P(48) ? P(14) : (noteBar.Height - lineH) / 2);
            Icons.Draw(g, "file", new RectangleF(noteBar.X + P(16), top + (lineH - P(18)) / 2f, P(18), P(18)), 18 * s, t.Primary, 2);

            // rechts: "Al opgeslagen en gekopieerd" en het toetsje Enter
            Font chipF = Fonts.Ui(12 * s, 1), keyF = Fonts.Mono(11 * s);
            float aw = g.MeasureString("Al opgeslagen en gekopieerd", chipF, PointF.Empty, StringFormat.GenericTypographic).Width;
            float kw = g.MeasureString("Enter", keyF, PointF.Empty, StringFormat.GenericTypographic).Width;
            Rectangle key = new Rectangle(noteBar.Right - P(14) - (int)Math.Ceiling(kw + P(16)), top + (lineH - P(24)) / 2, (int)Math.Ceiling(kw + P(16)), P(24));
            Rectangle chip = new Rectangle(key.X - P(8) - (int)Math.Ceiling(P(10) + P(14) + P(6) + aw + P(10)), key.Y, (int)Math.Ceiling(P(10) + P(14) + P(6) + aw + P(10)), P(24));
            using (GraphicsPath cp = Svg.RoundRect(chip, P(12))) using (SolidBrush cb = new SolidBrush(t.PrimaryContainer)) g.FillPath(cb, cp);
            Icons.Draw(g, "check", new RectangleF(chip.X + P(10), chip.Y, P(14), chip.Height), 14 * s, t.Primary, 2.5f);
            StringFormat mid = new StringFormat(StringFormat.GenericTypographic); mid.LineAlignment = StringAlignment.Center; mid.FormatFlags |= StringFormatFlags.NoWrap;
            using (SolidBrush cf = new SolidBrush(t.OnPrimaryContainer)) g.DrawString("Al opgeslagen en gekopieerd", chipF, cf, new RectangleF(chip.X + P(30), chip.Y, aw + P(4), chip.Height), mid);
            using (GraphicsPath kp = Svg.RoundRect(key, P(6)))
            using (SolidBrush kb = new SolidBrush(t.Surface))
            using (Pen kpn = new Pen(t.OutlineVariant, Math.Max(1, P(1)))) { g.FillPath(kb, kp); g.DrawPath(kpn, kp); }
            StringFormat center = new StringFormat(mid); center.Alignment = StringAlignment.Center;
            using (SolidBrush kf = new SolidBrush(t.Variant)) g.DrawString("Enter", keyF, kf, key, center);

            float tx = noteBar.X + P(16) + P(18) + P(12);
            RectangleF tr = new RectangleF(tx, top, chip.X - P(16) - tx, noteBar.Bottom - top - P(8));
            GraphicsState st = g.Save();
            g.SetClip(new RectangleF(tr.X - 2, noteBar.Y + 2, tr.Width + 4, noteBar.Height - 4));
            note.Draw(g, tr, NoteFont, t.OnSurface, t.Outline, "Waarom maak je deze screenshot?", true, t.Primary, t.PrimaryContainer);
            g.Restore(st);

            // eronder: de bestandsnaam die ontstaat, en "Esc zonder notitie"
            string name = preview != null ? preview(note.Text) : "";
            Font nf = Fonts.Mono(11 * s), uf = Fonts.Ui(12 * s, 0);
            string esc = "Esc  zonder notitie";
            float ew = g.MeasureString(esc, uf, PointF.Empty, StringFormat.GenericTypographic).Width;
            float nw = Math.Min(g.MeasureString(name, nf, PointF.Empty, StringFormat.GenericTypographic).Width, noteName.Width - ew - P(64));
            Rectangle pill = new Rectangle(noteName.X, noteName.Y, (int)Math.Ceiling(P(10) + P(14) + P(6) + nw + P(16) + ew + P(12)), noteName.Height);
            using (GraphicsPath pp = Svg.RoundRect(pill, P(8))) using (SolidBrush pb = new SolidBrush(Color.FromArgb(230, t.Inverse))) g.FillPath(pb, pp);
            Icons.Draw(g, "image", new RectangleF(pill.X + P(10), pill.Y, P(14), pill.Height), 14 * s, t.OnInverse, 2);
            StringFormat trim = new StringFormat(mid); trim.Trimming = StringTrimming.EllipsisCharacter;
            using (SolidBrush nb = new SolidBrush(t.OnInverse))
            {
                g.DrawString(name, nf, nb, new RectangleF(pill.X + P(30), pill.Y, nw + 2, pill.Height), trim);
                using (SolidBrush eb = new SolidBrush(Color.FromArgb(190, t.OnInverse)))
                    g.DrawString(esc, uf, eb, new RectangleF(pill.Right - P(12) - ew, pill.Y, ew + 2, pill.Height), mid);
            }
        }

        // Kapture sluit af terwijl de notitiebalk openstaat: wat er getypt is, bewaren
        public void FinishNoteNow() { FinishNote(true); }

        void FinishNote(bool save)
        {
            if (!noteMode) return;
            noteMode = false; blink.Stop();
            string text = save ? note.Text.Trim() : null;
            Action<string> h = NoteDone;
            if (h != null) h(text);
        }

        // Voor de zelftest: de notitiebalk tonen met een getypte notitie, zonder venster op het scherm
        public void SetNoteForTest(Rectangle r, string typed, Func<string, string> namePreview)
        {
            sel = r; preview = namePreview; noteMode = true;
            note.Set(typed);
            RelayoutNote();
        }

        // Voor de zelftest: een kader zetten zonder muis
        public void SetSelectionForTest(Rectangle r) { sel = r; }
    }
}
