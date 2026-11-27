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
            Items.Add(new Item { Key = "vrij", Icon = "lasso", Label = "Vrij", Kind = Kind.Mode });
            Items.Add(new Item { Key = "venster", Icon = "app-window", Label = "Venster", Kind = Kind.Mode });
            Items.Add(new Item { Key = "scherm", Icon = "monitor", Label = "Hele scherm", Kind = Kind.Mode });
            Items.Add(new Item { Key = "", Kind = Kind.Separator });
            Items.Add(new Item { Key = "preset", Icon = "sparkles", Label = "Naar Claude", Kind = Kind.Chip });
            Items.Add(new Item { Key = "notitie", Icon = "sticky-note", Label = "Met notitie", Kind = Kind.Icon });
            Items.Add(new Item { Key = "sluiten", Icon = "x", Label = "Sluiten", Kind = Kind.Icon });
        }

        int P(float v) { return (int)Math.Round(v * s); }

        // Plaatst de balk bovenaan in het midden van 'monitor' (in de coordinaten van de overlay)
        public void Layout(Rectangle monitor, float scale, Graphics measure)
        {
            s = scale;
            Font f = Fonts.Ui(12 * s, 2), fb = Fonts.Ui(12 * s, 2);   // Stitch: label-md, 12 px medium
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

            Font f = Fonts.Ui(12 * s, 2), fb = Fonts.Ui(12 * s, 2);   // Stitch: label-md, 12 px medium
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

        // De vorm: kader slepen, vrij tekenen of een venster aanklikken. Blijft bewaard tot de volgende screenshot.
        public enum Shape { Kader, Vrij, Venster }
        Shape shape = Shape.Kader;
        readonly List<Point> lasso = new List<Point>();   // Vrij: de getekende lijn (coordinaten van het beeld)
        Point[] lassoDone;                                // Vrij: de gesloten vorm na loslaten, voor App
        readonly List<Rectangle> windows = new List<Rectangle>();   // Venster: de zichtbare vensters, bovenste eerst
        Rectangle hoverWin = Rectangle.Empty;

        public Shape CurrentShape { get { return shape; } }

        // De preset (Presets): App zet hem voor het tonen; een klik op de knop of 1, 2, 3 wisselt hem
        int preset;
        public event Action<int> PresetChanged;
        public int Preset { get { return preset; } set { preset = Presets.Clamp(value); } }

        // De vrije vorm van de laatste keuze (of null bij een kader of venster); daarna is hij weg
        public Point[] TakeLasso() { Point[] p = lassoDone; lassoDone = null; return p; }

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
            SyncShapeButtons();
            using (Graphics g = Graphics.FromHdc(work.Dc)) bar.Layout(monitor, s, g);
            sel = Rectangle.Empty; dragging = false; hover = null; wasActivated = false;
            lasso.Clear(); lassoDone = null; hoverWin = Rectangle.Empty;
            FindWindows(origin);
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
            shownAt = DateTime.UtcNow; regains = 0;
        }

        // Bij het loslaten van Print Screen haalt Windows (of een ander programma, zoals het Knipprogramma) soms een
        // eigen venster naar voren. In de eerste ogenblikken na het tonen sluiten we dan niet, maar pakken we de focus terug.
        public const int GraceMs = 700;
        DateTime shownAt;
        int regains;
        public static string LastThief = "";   // voor de zelftest en het foutlog: welk programma nam de focus

        bool InGrace { get { return (DateTime.UtcNow - shownAt).TotalMilliseconds < GraceMs && regains < 3; } }

        void Regain()
        {
            if (!(active || noteMode) || !W.IsWindowVisible(Handle)) return;   // al dicht: niets terugpakken
            regains++;
            IntPtr fg = W.GetForegroundWindow();
            uint pid = 0, fgThread = fg != IntPtr.Zero ? W.GetWindowThreadProcessId(fg, out pid) : 0;
            string who = ProcessName(pid);
            LastThief = who;
            uint me = W.GetCurrentThreadId();
            bool attached = fgThread != 0 && fgThread != me && W.AttachThreadInput(me, fgThread, true);
            try { W.SetForegroundWindow(Handle); W.BringWindowToTop(Handle); }
            finally { if (attached) W.AttachThreadInput(me, fgThread, false); }
            Activate();
            Focus();
            bool ok = W.GetForegroundWindow() == Handle;
            Log.Info("Overlay: focus kwijt aan " + (who.Length > 0 ? who : "onbekend") + " vlak na het openen; " + (ok ? "teruggepakt" : "terugpakken lukte niet"));
            if (ok) return;
            // nog een keer proberen; lukt het dan nog niet, dan sluiten: het donkere scherm mag nooit blijven staan
            Timer again = new Timer { Interval = 250 };
            again.Tick += delegate
            {
                again.Stop(); again.Dispose();
                if (!(active || noteMode) || W.GetForegroundWindow() == Handle) return;
                W.SetForegroundWindow(Handle); Activate();
                if (W.GetForegroundWindow() == Handle) return;
                Log.Info("Overlay: focus bleef weg, overlay gesloten");
                if (noteMode) FinishNote(true); else Cancel("focus bleef weg");
            };
            again.Start();
        }

        // Alleen de programmanaam (bijvoorbeeld SnippingTool), nooit een venstertitel. Leeg als het Kapture zelf is
        // of als het programma al weg is.
        public static string ProcessName(uint pid)
        {
            try
            {
                if (pid != 0 && pid != (uint)System.Diagnostics.Process.GetCurrentProcess().Id)
                    using (System.Diagnostics.Process p = System.Diagnostics.Process.GetProcessById((int)pid)) return p.ProcessName;
            }
            catch { }
            return "";
        }

        // Het programma dat nu vooraan staat, voor het spoor in het foutlog
        public static string ForegroundName()
        {
            IntPtr fg = W.GetForegroundWindow();
            if (fg == IntPtr.Zero) return "niets";
            uint pid; W.GetWindowThreadProcessId(fg, out pid);
            string n = ProcessName(pid);
            return n.Length > 0 ? n : "Kapture of onbekend";
        }

        void SyncShapeButtons()
        {
            Toolbar.Item chip = bar.Find("preset");
            chip.Label = Presets.Chips[preset]; chip.Icon = Presets.Icons[preset];
            bar.Find("kader").Active = shape == Shape.Kader;
            bar.Find("vrij").Active = shape == Shape.Vrij;
            bar.Find("venster").Active = shape == Shape.Venster;
        }

        void SetPreset(int next)
        {
            next = Presets.Clamp(next);
            if (next == preset) return;
            preset = next;
            Rectangle old = bar.Bounds;
            SyncShapeButtons();
            using (Graphics g = Graphics.FromHdc(work.Dc)) bar.Layout(monitor, s, g);   // ander woord: andere breedte
            Invalidate(Rectangle.Union(old, bar.Bounds));
            Update();
            Action<int> h = PresetChanged;
            if (h != null) h(preset);
        }

        void SetShape(Shape next)
        {
            if (next == shape) return;
            shape = next;
            Rectangle old = bar.Bounds;
            SyncShapeButtons();
            using (Graphics g = Graphics.FromHdc(work.Dc)) bar.Layout(monitor, s, g);   // het gekozen woord is vet: andere breedte
            lasso.Clear(); hoverWin = Rectangle.Empty;
            Rectangle dirty = Rectangle.Union(Rectangle.Union(old, bar.Bounds), FrameBounds(sel));
            sel = Rectangle.Empty;
            Invalidate(Rectangle.Union(dirty, HintRect()));
            Update();
        }

        // Alle zichtbare vensters van andere programma's, van boven naar onder, in de coordinaten van het beeld.
        // Nu al, voordat de overlay zelf vooraan staat. Geen titels, alleen de plek.
        void FindWindows(Rectangle origin)
        {
            windows.Clear();
            try
            {
                uint me = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
                IntPtr h = W.GetTopWindow(IntPtr.Zero);
                for (int guard = 0; h != IntPtr.Zero && guard < 2000; guard++, h = W.GetWindow(h, W.GW_HWNDNEXT))
                {
                    if (!W.IsWindowVisible(h) || W.IsIconic(h)) continue;
                    int cloaked = 0;
                    try { W.DwmGetWindowAttribute(h, 14, out cloaked, 4); } catch { }
                    if (cloaked != 0) continue;
                    uint pid; W.GetWindowThreadProcessId(h, out pid);
                    if (pid == 0 || pid == me) continue;
                    // doorklikbare, onzichtbare en hulpvensters overslaan (bijvoorbeeld de laag van schermdelen of een spelbalk)
                    int ex = W.GetWindowLong(h, W.GWL_EXSTYLE);
                    if ((ex & 0x20) != 0 || (ex & 0x80) != 0) continue;   // WS_EX_TRANSPARENT, WS_EX_TOOLWINDOW
                    if ((ex & 0x80000) != 0)                               // WS_EX_LAYERED: helemaal doorzichtig?
                    {
                        uint key; byte alpha; uint flags;
                        if (W.GetLayeredWindowAttributes(h, out key, out alpha, out flags) && (flags & 2) != 0 && alpha == 0) continue;
                    }
                    // het bureaublad loopt over alle schermen: niet als venster
                    System.Text.StringBuilder cls = new System.Text.StringBuilder(32);
                    W.GetClassName(h, cls, cls.Capacity);
                    string cn = cls.ToString();
                    if (cn == "Progman" || cn == "WorkerW") continue;
                    W.RECT rc;
                    // de zichtbare rand (zonder de onzichtbare sleeprand van Windows 11)
                    if (W.DwmGetWindowAttribute(h, 9, out rc, 16) != 0 && !W.GetWindowRect(h, out rc)) continue;
                    Rectangle r = Rectangle.FromLTRB(rc.Left, rc.Top, rc.Right, rc.Bottom);
                    r.Offset(-origin.X, -origin.Y);
                    r.Intersect(new Rectangle(0, 0, origin.Width, origin.Height));
                    if (r.Width < 8 || r.Height < 8) continue;
                    windows.Add(r);
                }
            }
            catch { }
        }

        Rectangle WindowAt(Point p) { foreach (Rectangle r in windows) if (r.Contains(p)) return r; return Rectangle.Empty; }

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
                if (shape == Shape.Vrij && dragging) DrawLasso(g);
                else if (sel.Width > 0 && sel.Height > 0) DrawFrame(g);
                else DrawHint(g);
                if (!dragging && !noteMode && clip.IntersectsWith(bar.Bounds)) bar.Draw(g, theme, hover);   // tijdens het slepen en bij de notitie weg, zoals in Windows
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

        // Vrij: de lijn die je tekent, wit met een kobalt streep erover (zoals de rand van het kader)
        void DrawLasso(Graphics g)
        {
            if (lasso.Count < 2) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float lw = Math.Max(1.5f, 2 * s);
            using (Pen white = new Pen(Color.White, lw + 2 * s))
            using (Pen line = new Pen(theme.Primary, lw))
            {
                white.LineJoin = line.LineJoin = LineJoin.Round;
                Point[] pts = lasso.ToArray();
                g.DrawLines(white, pts);
                g.DrawLines(line, pts);
            }
        }

        Rectangle LassoBounds()
        {
            if (lasso.Count == 0) return Rectangle.Empty;
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            foreach (Point p in lasso) { x0 = Math.Min(x0, p.X); y0 = Math.Min(y0, p.Y); x1 = Math.Max(x1, p.X); y1 = Math.Max(y1, p.Y); }
            return Rectangle.FromLTRB(x0, y0, x1 + 1, y1 + 1);
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
            string what = shape == Shape.Vrij ? "Teken een vorm" : shape == Shape.Venster ? "Klik op een venster" : "Sleep een kader";
            TextRenderer.DrawText(g, what + "  ·  Esc of rechtsklik annuleert", Fonts.Ui(12 * s, 0), r, theme.OnInverse,
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
            if (e.Button == MouseButtons.Right) { if (dragging) { dragging = false; Capture = false; Invalidate(bar.Bounds); Invalidate(Inflate(LassoBounds(), P(6))); lasso.Clear(); SetSelection(Rectangle.Empty); } else Cancel("rechtermuisknop"); return; }
            if (e.Button != MouseButtons.Left) return;
            Toolbar.Item it = bar.HitTest(e.Location);
            if (it != null) { OnToolbarClick(it); return; }
            if (bar.Rect.Contains(e.Location)) return;
            if (shape == Shape.Venster)
            {
                Rectangle w = WindowAt(e.Location);
                if (w.Width > 0) Finish(w);
                return;
            }
            dragging = true; start = e.Location; hover = null;
            Capture = true;
            Invalidate(bar.Bounds);   // de werkbalk gaat weg zolang je sleept
            if (shape == Shape.Vrij) { lasso.Clear(); lasso.Add(e.Location); Invalidate(Inflate(FrameBounds(sel), P(4))); sel = Rectangle.Empty; Invalidate(HintRect()); Update(); return; }
            SetSelection(Rectangle.Empty);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!active) return;
            if (dragging)
            {
                Point p = new Point(Math.Max(0, Math.Min(ClientSize.Width - 1, e.X)), Math.Max(0, Math.Min(ClientSize.Height - 1, e.Y)));
                if (shape == Shape.Vrij)
                {
                    Point last = lasso[lasso.Count - 1];
                    if (Math.Abs(p.X - last.X) + Math.Abs(p.Y - last.Y) < 2) return;
                    lasso.Add(p);
                    Rectangle seg = Rectangle.FromLTRB(Math.Min(p.X, last.X), Math.Min(p.Y, last.Y), Math.Max(p.X, last.X) + 1, Math.Max(p.Y, last.Y) + 1);
                    Invalidate(Inflate(seg, P(6)));
                    Update();
                    return;
                }
                Rectangle next = Norm(start, p);
                if (next != sel) SetSelection(next);
                return;
            }
            if (shape == Shape.Venster && !bar.Rect.Contains(e.Location))
            {
                Rectangle w = WindowAt(e.Location);
                if (w != hoverWin) { hoverWin = w; SetSelection(w); }
            }
            else if (shape == Shape.Venster && hoverWin.Width > 0) { hoverWin = Rectangle.Empty; SetSelection(Rectangle.Empty); }
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
            if (shape == Shape.Vrij)
            {
                Rectangle lb = LassoBounds();
                if (lasso.Count >= 3 && lb.Width >= 4 && lb.Height >= 4)
                {
                    lassoDone = lasso.ToArray();
                    lasso.Clear();
                    sel = lb;
                    Invalidate(Inflate(lb, P(6)));
                    Finish(lb);
                    return;
                }
                Invalidate(Inflate(lb, P(6))); lasso.Clear();
                Invalidate(bar.Bounds); Invalidate(HintRect());   // te klein: werkbalk terug
                return;
            }
            if (sel.Width >= 4 && sel.Height >= 4) Finish(sel);
            else { Invalidate(bar.Bounds); SetSelection(Rectangle.Empty); }   // te klein: werkbalk terug
        }

        void OnToolbarClick(Toolbar.Item it)
        {
            if (!it.Enabled) return;
            if (it.Key == "notitie") { withNote = !withNote; it.Active = withNote; Invalidate(bar.Bounds); return; }
            if (it.Key == "preset") { SetPreset((preset + 1) % Presets.Names.Length); return; }
            if (it.Key == "kader") { SetShape(Shape.Kader); return; }
            if (it.Key == "vrij") { SetShape(Shape.Vrij); return; }
            if (it.Key == "venster") { SetShape(Shape.Venster); return; }
            if (it.Key == "sluiten") Cancel("knop Sluiten");
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
            if (active && keyData == Keys.Escape) { Cancel("Esc"); return true; }
            if (active && !dragging && keyData >= Keys.D1 && keyData < Keys.D1 + Presets.Names.Length) { SetPreset(keyData - Keys.D1); return true; }
            if (active && !dragging && keyData >= Keys.NumPad1 && keyData < Keys.NumPad1 + Presets.Names.Length) { SetPreset(keyData - Keys.NumPad1); return true; }
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
            if ((active || noteMode) && InGrace)
            {
                // net geopend: niet sluiten, de focus terugpakken (na dit bericht, anders werkt het niet)
                BeginInvoke((MethodInvoker)Regain);
                return;
            }
            if (noteMode) { FinishNote(true); return; }   // weggeklikt: de notitie tot nu toe bewaren
            if (active && wasActivated && !dragging) Cancel("weggeklikt naar " + ForegroundName());
        }

        void Finish(Rectangle r)
        {
            if (!active) return;
            active = false;
            Action<Rectangle> h = Selected;
            if (h != null) h(r);
        }

        public void Cancel() { Cancel(null); }

        public void Cancel(string why)
        {
            if (!active) return;
            if (why != null) Log.Trace("Donker scherm dicht: " + why);
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
            Invalidate(bar.Bounds);   // de werkbalk hoort niet bij de notitie
            RelayoutNote();
            blink.Start();
            W.SetForegroundWindow(Handle); Activate(); Focus();
            shownAt = DateTime.UtcNow; regains = 0;
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

        // Wat er al gebeurd is, rechts in de notitiebalk (hangt af van de preset)
        string SavedText
        {
            get
            {
                int st = StepsFor != null ? StepsFor(preset) : Presets.Defaults[preset];
                if (preset == Presets.ChooseLater) return "Nog niet bewaard";
                return (st & (Presets.StepCopyImage | Presets.StepCopyFile)) != 0 ? "Al opgeslagen en gekopieerd" : "Al opgeslagen";
            }
        }

        public Func<int, int> StepsFor;   // App: de ingestelde stappen per preset

        int ChipsWidth()
        {
            using (Bitmap b = new Bitmap(1, 1)) using (Graphics g = Graphics.FromImage(b))
            {
                float a = g.MeasureString(SavedText, Fonts.Ui(12 * s, 2), PointF.Empty, StringFormat.GenericTypographic).Width;
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

            // rechts: wat er al gebeurd is (SavedText) en het toetsje Enter
            Font chipF = Fonts.Ui(12 * s, 2), keyF = Fonts.Mono(11 * s);
            float aw = g.MeasureString(SavedText, chipF, PointF.Empty, StringFormat.GenericTypographic).Width;
            float kw = g.MeasureString("Enter", keyF, PointF.Empty, StringFormat.GenericTypographic).Width;
            Rectangle key = new Rectangle(noteBar.Right - P(14) - (int)Math.Ceiling(kw + P(16)), top + (lineH - P(24)) / 2, (int)Math.Ceiling(kw + P(16)), P(24));
            Rectangle chip = new Rectangle(key.X - P(8) - (int)Math.Ceiling(P(10) + P(14) + P(6) + aw + P(10)), key.Y, (int)Math.Ceiling(P(10) + P(14) + P(6) + aw + P(10)), P(24));
            using (GraphicsPath cp = Svg.RoundRect(chip, P(12))) using (SolidBrush cb = new SolidBrush(t.PrimaryContainer)) g.FillPath(cb, cp);
            Icons.Draw(g, "check", new RectangleF(chip.X + P(10), chip.Y, P(14), chip.Height), 14 * s, t.Primary, 2.5f);
            StringFormat mid = new StringFormat(StringFormat.GenericTypographic); mid.LineAlignment = StringAlignment.Center; mid.FormatFlags |= StringFormatFlags.NoWrap;
            using (SolidBrush cf = new SolidBrush(t.OnPrimaryContainer)) g.DrawString(SavedText, chipF, cf, new RectangleF(chip.X + P(30), chip.Y, aw + P(4), chip.Height), mid);
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
        public void SetDraggingForTest(bool on) { dragging = on; }
        public void SetLassoForTest(Point[] pts) { shape = Shape.Vrij; SyncShapeButtons(); lasso.Clear(); lasso.AddRange(pts); dragging = true; }
        public void SetShapeForTest(Shape sh) { shape = sh; SyncShapeButtons(); }
    }
}
