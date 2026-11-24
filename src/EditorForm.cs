using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;

namespace Kapture
{
    // ------------------------------------------------------------------ De editor (Stitch-scherm 5)
    // Midden de screenshot met bovenaan een zwevende werkbalk, rechts Eigenschappen en de notitie,
    // onderaan Kopiëren, Opslaan en Klaar. Zelf getekend; alleen het notitievak is een echt tekstvak.
    class EditorForm : Form
    {
        // Wat de editor van Kapture nodig heeft (zo blijft hij los te testen)
        public class Host
        {
            public Func<Bitmap, bool> SaveImage;          // de bewerkte afbeelding over het bestand heen zetten
            public Func<Bitmap, string, bool, bool> Copy; // naar het klembord, met de notitie; derde: is dit precies het bestand (opgeslagen)
            public Action<string> NoteChanged;            // de notitie is anders: bestandsnaam bijwerken
        }

        // ---- maten bij 100%
        const float PanelW = 284, FootH = 68, BarH = 52, BarBtn = 38, BarTop = 20, Pad = 24;

        readonly EditDoc doc;
        readonly Host host;
        readonly TextBox noteBox = new TextBox();
        string noteAtOpen;
        float s = 1;
        Theme t = Theme.Light;

        // gereedschap en eigenschappen
        EditTool tool = EditTool.Arrow;
        static readonly Color[] Swatches = {
            Color.FromArgb(0x25, 0x63, 0xEB), Color.FromArgb(0xDC, 0x26, 0x26), Color.FromArgb(0xEA, 0x58, 0x0C),
            Color.FromArgb(0xFA, 0xCC, 0x15), Color.FromArgb(0x19, 0x1B, 0x23), Color.White };
        Color color = Swatches[0];
        int lineWidth = 4, opacity = 100, textSize = 28;

        // bezig met tekenen of slepen
        Mark drawing;                 // nieuwe markering die nu getekend wordt
        Mark selected; Mark selectedBefore; PointF dragFrom; bool moving;
        Rectangle dragRect; bool draggingRect; PointF rectStart;
        Mark typing; Mark typingBefore; bool typingNew;
        readonly Timer caretTimer = new Timer { Interval = 530 };
        bool caretOn = true;

        // beeld
        RectangleF imgRect;           // waar de (uitgesneden) afbeelding staat in het venster
        Bitmap view; Size viewSize; Rectangle viewCrop; int viewVersion = -1, docVersion;

        class Zone { public RectangleF R; public string Id; public Action Click; }
        readonly List<Zone> zones = new List<Zone>();
        string hot, pressed;
        string dragSlider;            // "dikte", "dekking" of "tekst" tijdens het slepen van een schuifje
        Mark sliderBefore;            // hoe het gekozen ding was voor het slepen (één stap ongedaan maken)

        public EditorForm(Bitmap image, string file, string note, Host h)
        {
            doc = new EditDoc(image);
            host = h ?? new Host();
            noteAtOpen = note ?? "";
            doc.Changed += delegate { docVersion++; Invalidate(); };
            Text = "Kapture Editor";
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;
            KeyPreview = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            if (AppIcon.Get() != null) Icon = AppIcon.Get();
            noteBox.Multiline = true; noteBox.BorderStyle = BorderStyle.None; noteBox.ScrollBars = ScrollBars.None;
            noteBox.Text = noteAtOpen;
            noteBox.TextChanged += delegate { Invalidate(); };
            Controls.Add(noteBox);
            caretTimer.Tick += delegate { caretOn = !caretOn; if (typing != null) Invalidate(); };
            ApplyScale(W.ScaleAt(Cursor.Position.X, Cursor.Position.Y));
            UpdateTitle();
        }

        public EditDoc Doc { get { return doc; } }
        public string Note { get { return noteBox.Text.Trim(); } }

        int P(float v) { return (int)Math.Round(v * s); }

        void ApplyScale(float scale)
        {
            s = scale;
            MinimumSize = new Size(P(900), P(600));
            if (!IsHandleCreated)
            {
                Rectangle wa = Screen.FromPoint(Cursor.Position).WorkingArea;
                ClientSize = new Size(Math.Min(wa.Width - P(40), P(1280)), Math.Min(wa.Height - P(40), P(820)));
            }
            noteBox.Font = Fonts.Ui(13 * s, 0);
            ApplyTheme();
            LayoutNote();
            Invalidate();
        }

        void ApplyTheme()
        {
            t = Theme.Current;
            BackColor = t.SurfaceLow;
            noteBox.BackColor = t.Surface;
            noteBox.ForeColor = t.OnSurface;
            if (IsHandleCreated) { try { int v = t.Dark ? 1 : 0; W.DwmSetWindowAttribute(Handle, 20, ref v, 4); } catch { } }
        }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); ApplyTheme(); }

        void UpdateTitle() { Text = "Kapture Editor  /  " + doc.Crop.Width + " x " + doc.Crop.Height + (doc.Dirty ? "  (niet bewaard)" : ""); }

        // ---- indeling
        RectangleF Panel { get { return new RectangleF(ClientSize.Width - P(PanelW), 0, P(PanelW), ClientSize.Height - P(FootH)); } }
        RectangleF Canvas { get { return new RectangleF(0, 0, ClientSize.Width - P(PanelW), ClientSize.Height - P(FootH)); } }
        RectangleF NoteRect { get { RectangleF p = Panel; return new RectangleF(p.X + P(Pad), p.Bottom - P(Pad) - P(96), p.Width - 2 * P(Pad), P(96)); } }

        void LayoutNote()
        {
            RectangleF r = NoteRect;
            noteBox.Bounds = new Rectangle((int)r.X + P(12), (int)r.Y + P(10), (int)r.Width - P(24), (int)r.Height - P(20));
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); LayoutNote(); Invalidate(); }

        // De afbeelding zo groot mogelijk in het canvas, onder de werkbalk, nooit groter dan 100%
        RectangleF FitImage(RectangleF canvas)
        {
            Rectangle c = doc.Crop;
            float top = canvas.Y + P(BarTop) + P(BarH) + P(20);
            RectangleF room = RectangleF.FromLTRB(canvas.X + P(32), top, canvas.Right - P(32), canvas.Bottom - P(24));
            float k = Math.Min(1f, Math.Min(room.Width / c.Width, room.Height / c.Height));   // nooit groter dan echt
            float w = c.Width * k, h = c.Height * k;
            return new RectangleF((float)Math.Round(room.X + (room.Width - w) / 2), (float)Math.Round(room.Y + (room.Height - h) / 2), (float)Math.Round(w), (float)Math.Round(h));
        }

        float K { get { return imgRect.Width / Math.Max(1, doc.Crop.Width); } }

        PointF ToImage(Point p) { return new PointF(doc.Crop.X + (p.X - imgRect.X) / K, doc.Crop.Y + (p.Y - imgRect.Y) / K); }

        // Het geschaalde beeld bewaren: alleen opnieuw maken als de afbeelding, de uitsnede of de grootte verandert
        void EnsureView()
        {
            Size want = new Size(Math.Max(1, (int)imgRect.Width), Math.Max(1, (int)imgRect.Height));
            if (view != null && viewSize == want && viewCrop == doc.Crop && viewVersion == docVersion) return;
            if (view != null) view.Dispose();
            view = new Bitmap(want.Width, want.Height, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(view))
            {
                g.InterpolationMode = want.Width < doc.Crop.Width ? InterpolationMode.HighQualityBicubic : InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(doc.Image, new Rectangle(0, 0, want.Width, want.Height), doc.Crop, GraphicsUnit.Pixel);
            }
            viewSize = want; viewCrop = doc.Crop; viewVersion = docVersion;
        }

        // ---- tekenen
        protected override void OnPaint(PaintEventArgs e) { Draw(e.Graphics, ClientSize); }

        public void Draw(Graphics g, Size size)
        {
            zones.Clear();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            RectangleF canvas = new RectangleF(0, 0, size.Width - P(PanelW), size.Height - P(FootH));
            using (SolidBrush b = new SolidBrush(t.SurfaceLow)) g.FillRectangle(b, 0, 0, size.Width, size.Height);

            // de afbeelding met schaduw en rand
            imgRect = FitImage(canvas);
            for (int i = 4; i >= 1; i--)
            {
                RectangleF sh = imgRect; sh.Inflate(P(i * 2), P(i * 2)); sh.Offset(0, P(4));
                using (GraphicsPath gp = Svg.RoundRect(sh, P(4 + i * 2))) using (SolidBrush b = new SolidBrush(Color.FromArgb(t.Dark ? 22 : 8, 0, 0, 0))) g.FillPath(b, gp);
            }
            EnsureView();
            g.DrawImageUnscaled(view, (int)imgRect.X, (int)imgRect.Y);
            using (Pen p = new Pen(t.OutlineVariant)) g.DrawRectangle(p, imgRect.X - 0.5f, imgRect.Y - 0.5f, imgRect.Width, imgRect.Height);

            // de markeringen, in de maat van het scherm
            GraphicsState st = g.Save();
            g.SetClip(imgRect);
            g.TranslateTransform(imgRect.X, imgRect.Y);
            g.ScaleTransform(K, K);
            g.TranslateTransform(-doc.Crop.X, -doc.Crop.Y);
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            foreach (Mark m in doc.Marks) if (m != typing) MarkRender.Draw(g, m);
            if (drawing != null) MarkRender.Draw(g, drawing);
            if (typing != null) DrawTyping(g);
            if (selected != null && doc.Marks.Contains(selected))
            {
                RectangleF b = MarkRender.Bounds(selected); b.Inflate(4 / K, 4 / K);
                using (Pen p = new Pen(t.Primary, 1.5f / K)) { p.DashPattern = new float[] { 4, 3 }; g.DrawRectangle(p, b.X, b.Y, b.Width, b.Height); }
            }
            g.Restore(st);

            // kader tijdens vervagen of uitsnijden
            if (draggingRect && dragRect.Width > 0 && dragRect.Height > 0)
            {
                RectangleF vr = ToView(dragRect);
                if (tool == EditTool.Crop)
                {
                    using (Region outside = new Region(imgRect)) { outside.Exclude(vr); using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 0, 0, 0))) g.FillRegion(b, outside); }
                }
                else using (SolidBrush b = new SolidBrush(Color.FromArgb(40, t.Primary))) g.FillRectangle(b, vr);
                using (Pen p = new Pen(t.Primary, Math.Max(1.5f, 1.5f * s))) { p.DashPattern = new float[] { 4, 3 }; g.DrawRectangle(p, vr.X, vr.Y, vr.Width, vr.Height); }
            }

            DrawToolbar(g, canvas);
            DrawPanel(g, new RectangleF(size.Width - P(PanelW), 0, P(PanelW), size.Height - P(FootH)));
            DrawFooter(g, new RectangleF(0, size.Height - P(FootH), size.Width, P(FootH)));
        }

        RectangleF ToView(Rectangle r)
        {
            return new RectangleF(imgRect.X + (r.X - doc.Crop.X) * K, imgRect.Y + (r.Y - doc.Crop.Y) * K, r.Width * K, r.Height * K);
        }

        // De tekst die je typt, met een cursor aan het eind
        void DrawTyping(Graphics g)
        {
            Mark m = typing;
            if (m.Text.Length > 0) MarkRender.Draw(g, m);
            if (!caretOn) return;
            string[] lines = m.Text.Split('\n');
            using (Font f = new Font(MarkRender.TextFont(m.FontSize).FontFamily, m.FontSize, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                StringFormat sf = MarkRender.TextFormat;
                float lh = f.FontFamily.GetLineSpacing(FontStyle.Bold) * m.FontSize / f.FontFamily.GetEmHeight(FontStyle.Bold);
                float w = g.MeasureString(lines[lines.Length - 1], f, PointF.Empty, sf).Width;
                float x = m.A.X + w + 2, y = m.A.Y + lh * (lines.Length - 1);
                using (Pen p = new Pen(t.Primary, Math.Max(2f, 2f / K))) g.DrawLine(p, x, y + 2, x, y + lh - 2);
            }
        }

        // ---- werkbalk (zwevend boven het canvas)
        static readonly EditTool[] Tools = { EditTool.Select, EditTool.Pen, EditTool.Marker, EditTool.Arrow, EditTool.Rect, EditTool.Text, EditTool.Number, EditTool.Blur, EditTool.Crop, EditTool.Eraser };
        static readonly string[] ToolIcons = { "mouse-pointer-2", "pencil", "highlighter", "arrow-up-right", "square", "type", "hash", "grid-3x3", "crop", "eraser" };
        public static readonly string[] ToolNames = { "Selecteren", "Pen", "Markeerstift", "Pijl", "Rechthoek", "Tekst", "Nummer", "Vervagen", "Uitsnijden", "Gum" };
        static readonly string[] ToolKeys = { "V", "P", "M", "A", "R", "T", "N", "B", "C", "E" };

        public static string NameOf(EditTool tl) { return ToolNames[Array.IndexOf(Tools, tl)]; }

        void DrawToolbar(Graphics g, RectangleF canvas)
        {
            float gap = P(4), btn = P(BarBtn);
            float w = P(8) + Tools.Length * (btn + gap) + P(9) + P(56) + P(9) + 2 * (btn + gap) + P(4);
            RectangleF bar = new RectangleF((float)Math.Round(canvas.X + (canvas.Width - w) / 2), canvas.Y + P(BarTop), (float)Math.Round(w), P(BarH));
            for (int i = 3; i >= 1; i--)
            {
                RectangleF sh = bar; sh.Inflate(P(i * 2), P(i * 2)); sh.Offset(0, P(4));
                using (GraphicsPath gp = Svg.RoundRect(sh, P(12 + i * 2))) using (SolidBrush b = new SolidBrush(Color.FromArgb(t.Dark ? 30 : 12, 0, 0, 0))) g.FillPath(b, gp);
            }
            using (GraphicsPath gp = Svg.RoundRect(new RectangleF(bar.X + 0.5f, bar.Y + 0.5f, bar.Width - 1, bar.Height - 1), P(12)))
            using (SolidBrush b = new SolidBrush(t.Surface))
            using (Pen p = new Pen(t.OutlineVariant)) { g.FillPath(b, gp); g.DrawPath(p, gp); }
            float x = bar.X + P(8), y = bar.Y + (bar.Height - btn) / 2;
            for (int i = 0; i < Tools.Length; i++)
            {
                RectangleF r = new RectangleF(x, y, btn, btn);
                EditTool tl = Tools[i];
                bool on = tl == tool;
                string id = "tool" + i;
                if (on || hot == id)
                    using (GraphicsPath gp = Svg.RoundRect(r, P(8))) using (SolidBrush b = new SolidBrush(on ? t.Primary : t.Container)) g.FillPath(b, gp);
                Icons.Draw(g, ToolIcons[i], r, 18 * s, on ? t.OnPrimary : t.Variant, 2);
                zones.Add(new Zone { R = r, Id = id, Click = delegate { SetTool(tl); } });
                x += btn + gap;
            }
            Sep(g, ref x, bar);
            // kleur: rondje met pijltje (opent de kleurkiezer)
            RectangleF cr = new RectangleF(x, y, P(56), btn);
            if (hot == "kleurkiezer") using (GraphicsPath gp = Svg.RoundRect(cr, P(8))) using (SolidBrush b = new SolidBrush(t.Container)) g.FillPath(b, gp);
            RectangleF dot = new RectangleF(cr.X + P(10), cr.Y + (cr.Height - P(18)) / 2, P(18), P(18));
            using (SolidBrush b = new SolidBrush(color)) g.FillEllipse(b, dot);
            using (Pen p = new Pen(t.OutlineVariant)) g.DrawEllipse(p, dot);
            Icons.Draw(g, "chevron-down", new RectangleF(cr.X + P(32), cr.Y, P(14), cr.Height), 14 * s, t.Variant, 2);
            zones.Add(new Zone { R = cr, Id = "kleurkiezer", Click = PickColor });
            x += P(56) + gap;
            Sep(g, ref x, bar);
            ToolbarIcon(g, "undo-2", "ongedaan", new RectangleF(x, y, btn, btn), doc.CanUndo, delegate { CommitTyping(); doc.Undo(); selected = null; UpdateTitle(); });
            x += btn + gap;
            ToolbarIcon(g, "redo-2", "opnieuw", new RectangleF(x, y, btn, btn), doc.CanRedo, delegate { doc.Redo(); selected = null; UpdateTitle(); });
        }

        void Sep(Graphics g, ref float x, RectangleF bar)
        {
            using (SolidBrush b = new SolidBrush(t.OutlineVariant)) g.FillRectangle(b, x + P(4), bar.Y + (bar.Height - P(24)) / 2, Math.Max(1, P(1)), P(24));
            x += P(9);
        }

        void ToolbarIcon(Graphics g, string icon, string id, RectangleF r, bool enabled, Action click)
        {
            if (enabled && hot == id) using (GraphicsPath gp = Svg.RoundRect(r, P(8))) using (SolidBrush b = new SolidBrush(t.Container)) g.FillPath(b, gp);
            Icons.Draw(g, icon, r, 18 * s, enabled ? t.Variant : Color.FromArgb(90, t.Variant), 2);
            if (enabled) zones.Add(new Zone { R = r, Id = id, Click = click });
        }

        // ---- Eigenschappen rechts
        static void Txt(Graphics g, string text, Font f, RectangleF r, Color c, TextFormatFlags extra)
        {
            Rectangle ri = Rectangle.FromLTRB((int)Math.Round(r.Left), (int)Math.Round(r.Top), (int)Math.Round(r.Right), (int)Math.Round(r.Bottom));
            TextRenderer.DrawText(g, text, f, ri, c, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | extra);
        }

        const TextFormatFlags Mid = TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;

        bool UsesWidth { get { EditTool x = CurrentKind; return x == EditTool.Pen || x == EditTool.Marker || x == EditTool.Arrow || x == EditTool.Rect || x == EditTool.Number; } }
        bool UsesColor { get { EditTool x = CurrentKind; return x != EditTool.Blur && x != EditTool.Crop && x != EditTool.Eraser; } }
        // met Selecteren gelden de eigenschappen voor wat je gekozen hebt
        EditTool CurrentKind { get { return tool == EditTool.Select && selected != null ? selected.Tool : tool; } }

        void DrawPanel(Graphics g, RectangleF p)
        {
            using (SolidBrush b = new SolidBrush(t.Surface)) g.FillRectangle(b, p);
            using (Pen pen = new Pen(t.OutlineVariant)) g.DrawLine(pen, p.X + 0.5f, p.Y, p.X + 0.5f, p.Bottom);
            float x = p.X + P(Pad), w = p.Width - 2 * P(Pad), y = p.Y + P(Pad);
            Font h = Fonts.Ui(14 * s, 1), lf = Fonts.Ui(12 * s, 1);
            Txt(g, "Eigenschappen", h, new RectangleF(x, y, w, P(24)), t.OnSurface, Mid);
            string chip = NameOf(CurrentKind);
            float cw = TextRenderer.MeasureText(chip, Fonts.Ui(11 * s, 1), Size.Empty, TextFormatFlags.NoPadding).Width + P(16);
            RectangleF chipR = new RectangleF(x + w - cw, y + P(2), cw, P(20));
            using (GraphicsPath gp = Svg.RoundRect(chipR, P(6))) using (SolidBrush b = new SolidBrush(t.PrimaryContainer)) g.FillPath(b, gp);
            Txt(g, chip, Fonts.Ui(11 * s, 1), chipR, t.OnPrimaryContainer, Mid | TextFormatFlags.HorizontalCenter);
            y += P(44);

            if (UsesColor)
            {
                Txt(g, "Kleur", lf, new RectangleF(x, y, w, P(18)), t.Variant, Mid);
                y += P(26);
                float d = P(24), gap = P(8), sx = x;
                for (int i = 0; i < Swatches.Length; i++)
                {
                    RectangleF r = new RectangleF(sx, y, d, d);
                    Color c = Swatches[i];
                    bool on = c.ToArgb() == color.ToArgb();
                    if (on) using (Pen ring = new Pen(t.Primary, Math.Max(2f, 2 * s))) g.DrawEllipse(ring, RectangleF.Inflate(r, P(3), P(3)));
                    using (SolidBrush b = new SolidBrush(c)) g.FillEllipse(b, r);
                    using (Pen ol = new Pen(t.OutlineVariant)) g.DrawEllipse(ol, r);
                    Color cc = c;
                    zones.Add(new Zone { R = RectangleF.Inflate(r, P(3), P(3)), Id = "kleur" + i, Click = delegate { SetColor(cc); } });
                    sx += d + gap;
                }
                RectangleF plus = new RectangleF(sx, y, d, d);
                using (Pen ol = new Pen(hot == "kleur+" ? t.Primary : t.Outline, Math.Max(1.5f, 1.5f * s))) { ol.DashPattern = new float[] { 2, 2 }; g.DrawEllipse(ol, plus); }
                Icons.Draw(g, "plus", plus, 14 * s, t.Variant, 2);
                zones.Add(new Zone { R = plus, Id = "kleur+", Click = PickColor });
                y += d + P(28);
            }
            if (UsesWidth)
            {
                y = Slider(g, "Lijndikte", lineWidth + " px", "dikte", (lineWidth - 1) / 19f, x, y, w);
                y = Slider(g, "Dekking", opacity + "%", "dekking", (opacity - 10) / 90f, x, y, w);
            }
            if (CurrentKind == EditTool.Text)
                y = Slider(g, "Tekstgrootte", textSize + " px", "tekst", (textSize - 12) / 84f, x, y, w);
            string help = HelpFor(CurrentKind);
            if (help != null)
                Txt(g, help, Fonts.Ui(12 * s, 0), new RectangleF(x, y, w, P(120)), t.Variant, TextFormatFlags.WordBreak | TextFormatFlags.Top);

            // notitie onderaan
            RectangleF nr = NoteRect;
            using (Pen pen = new Pen(t.OutlineVariant)) g.DrawLine(pen, x, nr.Y - P(48), x + w, nr.Y - P(48));
            Icons.Draw(g, "sticky-note", new RectangleF(x, nr.Y - P(34), P(16), P(20)), 16 * s, t.Primary, 2);
            Txt(g, "Notitie bij screenshot", lf, new RectangleF(x + P(24), nr.Y - P(34), w - P(24), P(20)), t.OnSurface, Mid);
            using (GraphicsPath gp = Svg.RoundRect(new RectangleF(nr.X + 0.5f, nr.Y + 0.5f, nr.Width - 1, nr.Height - 1), P(10)))
            using (SolidBrush b = new SolidBrush(t.Surface))
            using (Pen pen = new Pen(noteBox.Focused ? t.Primary : t.OutlineVariant, noteBox.Focused ? Math.Max(1.5f, 2 * s) : 1)) { g.FillPath(b, gp); g.DrawPath(pen, gp); }
        }

        static string HelpFor(EditTool tl)
        {
            switch (tl)
            {
                case EditTool.Select: return "Klik op iets om het te kiezen. Sleep om het te verplaatsen. Delete haalt het weg.";
                case EditTool.Text: return "Klik waar de tekst moet komen en typ. Enter is klaar, Shift+Enter een nieuwe regel.";
                case EditTool.Number: return "Klik voor 1, 2, 3 enzovoort.";
                case EditTool.Blur: return "Sleep een kader over wat niemand mag zien. De pixels worden echt vervangen: in het bestand is het niet meer terug te halen.";
                case EditTool.Crop: return "Sleep een kader: alleen dat stuk blijft over. Ctrl+Z zet het terug.";
                case EditTool.Eraser: return "Klik of sleep over een markering om hem weg te halen.";
            }
            return null;
        }

        float Slider(Graphics g, string label, string value, string id, float frac, float x, float y, float w)
        {
            Font lf = Fonts.Ui(12 * s, 1);
            Txt(g, label, lf, new RectangleF(x, y, w, P(18)), t.Variant, Mid);
            Txt(g, value, Fonts.Mono(12 * s), new RectangleF(x, y, w, P(18)), t.OnSurface, Mid | TextFormatFlags.Right);
            float ty = y + P(30);
            RectangleF track = new RectangleF(x, ty - P(2), w, P(4));
            using (GraphicsPath gp = Svg.RoundRect(track, P(2))) using (SolidBrush b = new SolidBrush(t.ContainerHigh)) g.FillPath(b, gp);
            frac = Math.Max(0, Math.Min(1, frac));
            RectangleF fill = new RectangleF(x, track.Y, w * frac, track.Height);
            if (fill.Width > 0) using (GraphicsPath gp = Svg.RoundRect(fill, P(2))) using (SolidBrush b = new SolidBrush(t.Primary)) g.FillPath(b, gp);
            RectangleF knob = new RectangleF(x + w * frac - P(8), ty - P(8), P(16), P(16));
            using (SolidBrush b = new SolidBrush(t.Surface)) g.FillEllipse(b, knob);
            using (Pen p = new Pen(t.Primary, Math.Max(2f, 2 * s))) g.DrawEllipse(p, knob);
            zones.Add(new Zone { R = new RectangleF(x - P(8), ty - P(12), w + P(16), P(24)), Id = "slider:" + id });
            sliderTracks[id] = new RectangleF(x, ty, w, 0);
            return y + P(56);
        }

        readonly Dictionary<string, RectangleF> sliderTracks = new Dictionary<string, RectangleF>();

        void SetSlider(string id, float x)
        {
            RectangleF tr;
            if (!sliderTracks.TryGetValue(id, out tr)) return;
            float f = Math.Max(0, Math.Min(1, (x - tr.X) / Math.Max(1, tr.Width)));
            if (id == "dikte") lineWidth = 1 + (int)Math.Round(f * 19);
            else if (id == "dekking") opacity = 10 + (int)Math.Round(f * 90 / 5) * 5;
            else if (id == "tekst") textSize = 12 + (int)Math.Round(f * 84);
            // tijdens het slepen meteen laten zien; de stap voor ongedaan maken komt bij het loslaten
            if (typing != null) typing.FontSize = textSize;
            else if (selected != null && doc.Marks.Contains(selected))
            {
                selected.Opacity = opacity;
                if (selected.Tool == EditTool.Text) selected.FontSize = textSize; else selected.Width = lineWidth;
            }
            Invalidate();
        }

        // ---- onderbalk: uitleg links, Kopiëren, Opslaan en Klaar rechts
        void DrawFooter(Graphics g, RectangleF f)
        {
            using (SolidBrush b = new SolidBrush(t.Surface)) g.FillRectangle(b, f);
            using (Pen p = new Pen(t.OutlineVariant)) g.DrawLine(p, f.X, f.Y + 0.5f, f.Right, f.Y + 0.5f);
            float bh = P(40), by = f.Y + (f.Height - bh) / 2, x = f.Right - P(Pad);
            x = FooterButton(g, "Klaar", "check", "klaar", true, x, by, bh, Finish);
            x = FooterButton(g, "Opslaan", "save", "opslaan", false, x, by, bh, delegate { SaveNow(); });
            x = FooterButton(g, "Kopiëren", "copy", "kopieren", false, x, by, bh, CopyNow);
            string status = message ?? (doc.Dirty ? "Nog niet opgeslagen. Klaar bewaart de screenshot en zet hem op het klembord." : "Klaar bewaart de screenshot en zet hem op het klembord.");
            Txt(g, status, Fonts.Ui(13 * s, message != null ? 1 : 0), new RectangleF(f.X + P(Pad), f.Y, x - P(16) - f.X - P(Pad), f.Height), message != null ? t.Primary : t.Variant, Mid);
        }

        string message;
        readonly Timer messageTimer = new Timer { Interval = 3000 };

        void Say(string text)
        {
            message = text;
            messageTimer.Stop();
            messageTimer.Tick -= ClearMessage; messageTimer.Tick += ClearMessage;
            messageTimer.Start();
            Invalidate();
        }

        void ClearMessage(object o, EventArgs e) { messageTimer.Stop(); message = null; Invalidate(); }

        float FooterButton(Graphics g, string label, string icon, string id, bool primary, float right, float y, float h, Action click)
        {
            Font bf = Fonts.Ui(13 * s, 1);
            float w = TextRenderer.MeasureText(label, bf, Size.Empty, TextFormatFlags.NoPadding).Width + P(16) + P(24) + P(18);
            RectangleF r = new RectangleF(right - w, y, w, h);
            Color bg = primary ? (hot == id ? Theme.Mix(t.Primary, t.OnSurface, 0.15f) : t.Primary) : (hot == id ? t.Container : t.Surface);
            using (GraphicsPath gp = Svg.RoundRect(new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1), P(8)))
            using (SolidBrush b = new SolidBrush(bg))
            {
                g.FillPath(b, gp);
                if (!primary) using (Pen p = new Pen(t.OutlineVariant)) g.DrawPath(p, gp);
            }
            Color fc = primary ? t.OnPrimary : t.OnSurface;
            Icons.Draw(g, icon, new RectangleF(r.X + P(16), r.Y, P(16), r.Height), 16 * s, fc, 2);
            Txt(g, label, bf, new RectangleF(r.X + P(40), r.Y, r.Width - P(48), r.Height), fc, Mid);
            zones.Add(new Zone { R = r, Id = id, Click = click });
            return r.X - P(10);
        }

        // ---- gereedschap en eigenschappen
        void SetTool(EditTool tl)
        {
            CommitTyping();
            tool = tl;
            if (tl != EditTool.Select) selected = null;
            Cursor = tl == EditTool.Select ? Cursors.Default : tl == EditTool.Text ? Cursors.IBeam : Cursors.Cross;
            Invalidate();
        }

        void SetColor(Color c)
        {
            color = c;
            ApplyToSelected();
            if (typing != null) typing.Color = c;
            Invalidate();
        }

        void PickColor()
        {
            using (ColorDialog d = new ColorDialog())
            {
                d.Color = color; d.FullOpen = true; d.AnyColor = true;
                if (d.ShowDialog(this) == DialogResult.OK) SetColor(Color.FromArgb(255, d.Color));
            }
        }

        // Met Selecteren: een andere kleur geldt voor wat je gekozen hebt (en is ongedaan te maken)
        void ApplyToSelected()
        {
            if (selected == null || !doc.Marks.Contains(selected)) return;
            Mark before = selected.Clone();
            selected.Color = color;
            if (before.Color != selected.Color) { doc.Changed1(selected, before); UpdateTitle(); }
        }

        Mark NewMark(EditTool tl, PointF at)
        {
            Mark m = new Mark { Tool = tl, Color = color, Width = lineWidth, Opacity = opacity, FontSize = textSize };
            m.Pts.Add(at);
            return m;
        }

        // ---- muis
        string HitZone(Point p)
        {
            for (int i = zones.Count - 1; i >= 0; i--) if (zones[i].R.Contains(p)) return zones[i].Id;
            return null;
        }

        Mark MarkAt(PointF ip)
        {
            float r = 6 / K;
            for (int i = doc.Marks.Count - 1; i >= 0; i--) if (MarkRender.Hit(doc.Marks[i], ip, r)) return doc.Marks[i];
            return null;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            pressed = HitZone(e.Location);
            if (pressed != null && pressed.StartsWith("slider:"))
            {
                dragSlider = pressed.Substring(7); Capture = true;
                sliderBefore = selected != null && doc.Marks.Contains(selected) ? selected.Clone() : null;
                SetSlider(dragSlider, e.X); return;
            }
            if (pressed != null) return;
            if (!imgRect.Contains(e.Location)) { CommitTyping(); return; }
            ActiveControl = null;   // anders gaat het toetsenbord terug naar het notitievak (Delete, Ctrl+Z, letters)
            Focus();
            PointF ip = ToImage(e.Location);
            switch (tool)
            {
                case EditTool.Pen: case EditTool.Marker: case EditTool.Arrow: case EditTool.Rect:
                    CommitTyping();
                    drawing = NewMark(tool, ip);
                    if (tool == EditTool.Arrow || tool == EditTool.Rect) drawing.Pts.Add(ip);
                    Capture = true;
                    break;
                case EditTool.Number:
                    CommitTyping();
                    { Mark m = NewMark(EditTool.Number, ip); m.Number = doc.NextNumber(); doc.Add(m); UpdateTitle(); }
                    break;
                case EditTool.Text:
                    {
                        Mark hit = MarkAt(ip);
                        CommitTyping();
                        if (hit != null && hit.Tool == EditTool.Text) StartTyping(hit, false);
                        else StartTyping(NewMark(EditTool.Text, ip), true);
                        break;
                    }
                case EditTool.Blur: case EditTool.Crop:
                    CommitTyping();
                    draggingRect = true; rectStart = ip; dragRect = Rectangle.Empty; Capture = true;
                    break;
                case EditTool.Eraser:
                    CommitTyping();
                    EraseAt(ip);
                    Capture = true;
                    break;
                case EditTool.Select:
                    CommitTyping();
                    selected = MarkAt(ip);
                    if (selected != null)
                    {
                        selectedBefore = selected.Clone(); dragFrom = ip; moving = true; Capture = true;
                        color = selected.Color; opacity = selected.Opacity;
                        if (selected.Tool == EditTool.Text) textSize = (int)selected.FontSize; else lineWidth = (int)Math.Max(1, Math.Min(20, selected.Width));
                    }
                    break;
            }
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragSlider != null) { SetSlider(dragSlider, e.X); return; }
            PointF ip = ToImage(e.Location);
            if (drawing != null)
            {
                if (drawing.Tool == EditTool.Arrow || drawing.Tool == EditTool.Rect) drawing.Pts[1] = ip;
                else
                {
                    PointF last = drawing.B;
                    float dx = (ip.X - last.X) * K, dy = (ip.Y - last.Y) * K;
                    if (dx * dx + dy * dy >= 2.5f) drawing.Pts.Add(ip);   // niet elk pixeltje: rustigere lijn
                }
                Invalidate();
                return;
            }
            if (draggingRect)
            {
                RectangleF r = MarkRender.Norm(rectStart, ip);
                dragRect = Rectangle.Round(r);
                dragRect.Intersect(doc.Crop);
                Invalidate();
                return;
            }
            if (moving && selected != null)
            {
                MarkRender.Move(selected, ip.X - dragFrom.X, ip.Y - dragFrom.Y);
                dragFrom = ip;
                Invalidate();
                return;
            }
            if (tool == EditTool.Eraser && e.Button == MouseButtons.Left) { EraseAt(ip); return; }
            string h = HitZone(e.Location);
            if (h != hot) { hot = h; Invalidate(); }
            Cursor want = h != null ? Cursors.Hand : !imgRect.Contains(e.Location) ? Cursors.Default
                : tool == EditTool.Select ? (MarkAt(ip) != null ? Cursors.SizeAll : Cursors.Default) : tool == EditTool.Text ? Cursors.IBeam : Cursors.Cross;
            if (Cursor != want) Cursor = want;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            Capture = false;
            if (dragSlider != null)
            {
                dragSlider = null;
                if (sliderBefore != null && selected != null && (sliderBefore.Width != selected.Width || sliderBefore.Opacity != selected.Opacity || sliderBefore.FontSize != selected.FontSize))
                { doc.Changed1(selected, sliderBefore); UpdateTitle(); }
                sliderBefore = null;
                return;
            }
            if (drawing != null)
            {
                Mark m = drawing; drawing = null;
                bool tiny = (m.Tool == EditTool.Arrow || m.Tool == EditTool.Rect) && Math.Abs(m.A.X - m.B.X) * K < 4 && Math.Abs(m.A.Y - m.B.Y) * K < 4;
                if (!tiny) { doc.Add(m); UpdateTitle(); }
                Invalidate();
                return;
            }
            if (draggingRect)
            {
                draggingRect = false;
                if (dragRect.Width >= 4 && dragRect.Height >= 4)
                {
                    if (tool == EditTool.Blur) doc.Blur(dragRect);
                    else { doc.SetCrop(dragRect); SetTool(EditTool.Select); }
                    UpdateTitle();
                }
                dragRect = Rectangle.Empty;
                Invalidate();
                return;
            }
            if (moving)
            {
                moving = false;
                if (selected != null && selectedBefore != null && selected.A != selectedBefore.A) { doc.Changed1(selected, selectedBefore); UpdateTitle(); }
                return;
            }
            string h = HitZone(e.Location);
            if (h != null && h == pressed)
                foreach (Zone z in zones.ToArray()) if (z.Id == h && z.Click != null) { z.Click(); break; }
            Invalidate();
        }

        void EraseAt(PointF ip)
        {
            Mark m = MarkAt(ip);
            if (m == null) return;
            if (m == selected) selected = null;
            doc.Remove(m);
            UpdateTitle();
        }

        // ---- tekst typen op de afbeelding
        void StartTyping(Mark m, bool isNew)
        {
            typing = m; typingNew = isNew;
            typingBefore = isNew ? null : m.Clone();
            if (!isNew) { color = m.Color; textSize = (int)m.FontSize; }
            caretOn = true; caretTimer.Start();
            ActiveControl = null; Focus();
            Invalidate();
        }

        void CommitTyping()
        {
            if (typing == null) return;
            Mark m = typing; typing = null; caretTimer.Stop();
            m.Text = m.Text.TrimEnd();
            if (typingNew) { if (m.Text.Length > 0) doc.Add(m); }
            else if (m.Text.Length == 0)
            {
                if (typingBefore != null) { m.Text = typingBefore.Text; m.Color = typingBefore.Color; m.FontSize = typingBefore.FontSize; }
                doc.Remove(m);
            }
            else if (typingBefore != null && (typingBefore.Text != m.Text || typingBefore.Color != m.Color || typingBefore.FontSize != m.FontSize)) doc.Changed1(m, typingBefore);
            typingBefore = null;
            UpdateTitle();
            Invalidate();
        }

        void CancelTyping()
        {
            if (typing == null) return;
            if (!typingNew && typingBefore != null) { typing.Text = typingBefore.Text; typing.Color = typingBefore.Color; typing.FontSize = typingBefore.FontSize; }
            typing = null; typingBefore = null; caretTimer.Stop();
            Invalidate();
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            if (typing == null || noteBox.Focused) return;
            if (e.KeyChar >= ' ' && typing.Text.Length < 500) { typing.Text += e.KeyChar; caretOn = true; Invalidate(); e.Handled = true; }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys key)
        {
            if (typing != null && !noteBox.Focused)
            {
                if (key == Keys.Escape) { CancelTyping(); return true; }
                if (key == Keys.Enter) { CommitTyping(); return true; }
                if (key == (Keys.Shift | Keys.Enter)) { typing.Text += "\n"; Invalidate(); return true; }
                if (key == Keys.Back) { if (typing.Text.Length > 0) typing.Text = typing.Text.Substring(0, typing.Text.Length - 1); caretOn = true; Invalidate(); return true; }
                if (key == (Keys.Control | Keys.V)) { try { if (Clipboard.ContainsText()) typing.Text += Clipboard.GetText().Replace("\r\n", "\n"); } catch { } Invalidate(); return true; }
                return base.ProcessCmdKey(ref msg, key);
            }
            if (key == (Keys.Control | Keys.S)) { SaveNow(); return true; }
            if (noteBox.Focused) return base.ProcessCmdKey(ref msg, key);
            if (key == (Keys.Control | Keys.Z)) { doc.Undo(); selected = null; UpdateTitle(); return true; }
            if (key == (Keys.Control | Keys.Y) || key == (Keys.Control | Keys.Shift | Keys.Z)) { doc.Redo(); selected = null; UpdateTitle(); return true; }
            if (key == (Keys.Control | Keys.C)) { CopyNow(); return true; }
            if (key == Keys.Delete && selected != null) { Mark m = selected; selected = null; doc.Remove(m); UpdateTitle(); return true; }
            if (key == Keys.Escape) { if (selected != null) { selected = null; Invalidate(); } else Close(); return true; }
            for (int i = 0; i < ToolKeys.Length; i++)
                if (key == (Keys)Enum.Parse(typeof(Keys), ToolKeys[i])) { SetTool(Tools[i]); return true; }
            return base.ProcessCmdKey(ref msg, key);
        }

        // ---- opslaan, kopiëren, klaar
        public bool SaveNow()
        {
            CommitTyping();
            bool ok = true;
            if (doc.Dirty)
            {
                using (Bitmap b = doc.Export())
                    ok = host.SaveImage != null && host.SaveImage(b);
                if (ok) { doc.Dirty = false; Say("Opgeslagen"); }
                else Say("Opslaan lukte niet. Is de map bereikbaar?");
            }
            ApplyNote();
            UpdateTitle();
            return ok;
        }

        void ApplyNote()
        {
            string n = Note;
            if (n != noteAtOpen.Trim())
            {
                noteAtOpen = n;
                if (host.NoteChanged != null) host.NoteChanged(n);
            }
        }

        void CopyNow()
        {
            CommitTyping();
            bool ok;
            using (Bitmap b = doc.Export()) ok = host.Copy != null && host.Copy(b, Note, !doc.Dirty);
            Say(ok ? "Gekopieerd naar het klembord" : "Kopiëren lukte niet");
        }

        void Finish()
        {
            if (!SaveNow()) return;
            CopyNow();
            Close();
        }

        bool closedForQuit;

        // Kapture stopt (Afsluiten of bijwerken): vragen of bewaren. Geeft false bij Annuleren.
        public bool PrepareQuit()
        {
            CommitTyping();
            if (doc.Dirty)
            {
                if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
                Activate();
                DialogResult a = MessageBox.Show(this, "Kapture stopt. Wil je de bewerkte screenshot eerst opslaan?", "Kapture", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (a == DialogResult.Cancel) return false;
                if (a == DialogResult.Yes && !SaveNow()) return false;
            }
            ApplyNote();
            closedForQuit = true;
            return true;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            CommitTyping();
            if (closedForQuit) { base.OnFormClosing(e); return; }
            // Windows sluit af: niet vragen (dat kan niet), wel bewaren
            if (e.CloseReason != CloseReason.UserClosing && doc.Dirty) SaveNow();
            if (e.CloseReason == CloseReason.UserClosing && doc.Dirty)
            {
                DialogResult a = MessageBox.Show(this, "Je hebt de screenshot bewerkt. Wil je de wijzigingen opslaan?", "Kapture", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (a == DialogResult.Cancel) { e.Cancel = true; return; }
                if (a == DialogResult.Yes && !SaveNow()) { e.Cancel = true; return; }
            }
            if (!e.Cancel) ApplyNote();
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                caretTimer.Dispose(); messageTimer.Dispose();
                if (view != null) { view.Dispose(); view = null; }
                doc.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnShown(EventArgs e) { base.OnShown(e); ActiveControl = null; Focus(); }

        // ---- voor de zelftest: het venster als plaatje
        public Bitmap RenderForTest(Theme theme, float scale, EditTool tl, Size size)
        {
            Program.ForceTheme = theme;
            try
            {
                t = theme; s = scale; tool = tl;
                ClientSize = size;
                noteBox.Text = noteAtOpen;
                Bitmap b = new Bitmap(size.Width, size.Height);
                using (Graphics g = Graphics.FromImage(b))
                {
                    Draw(g, size);
                    // het notitievak zelf tekenen (het echte tekstvak staat niet op een plaatje)
                    RectangleF nr = NoteRect;
                    Txt(g, noteAtOpen, Fonts.Ui(13 * s, 0), new RectangleF(nr.X + P(12), nr.Y + P(10), nr.Width - P(24), nr.Height - P(20)), t.OnSurface, TextFormatFlags.WordBreak | TextFormatFlags.Top);
                }
                return b;
            }
            finally { Program.ForceTheme = null; }
        }

        public int ZoneCountForTest { get { return zones.Count; } }
    }
}
