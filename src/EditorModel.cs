using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace Kapture
{
    // ------------------------------------------------------------------ De editor: wat er op een screenshot getekend is
    // Opzet en tekenstijl naar de tekenmotor van NexusPen (zelfde maker): pen met een vloeiende lijn, markeerstift,
    // pijl met een dichte punt, tekst met een rand, genummerde rondjes. Alles in pixels van de afbeelding.
    enum EditTool { Select, Pen, Marker, Arrow, Rect, Text, Number, Blur, Crop, Eraser }

    class Mark
    {
        public EditTool Tool;
        public Color Color = Color.FromArgb(0x25, 0x63, 0xEB);
        public float Width = 4;            // lijndikte in pixels van de afbeelding
        public int Opacity = 100;          // dekking in procent
        public List<PointF> Pts = new List<PointF>();
        public string Text = "";
        public float FontSize = 28;
        public int Number;

        public Mark Clone()
        {
            Mark m = (Mark)MemberwiseClone();
            m.Pts = new List<PointF>(Pts);
            return m;
        }

        public PointF A { get { return Pts[0]; } }
        public PointF B { get { return Pts[Pts.Count - 1]; } }
    }

    static class MarkRender
    {
        public static float MarkerWidth(float w) { return w * 3 + 8; }
        public static float NumberSize(float w) { return 24 + w * 3; }

        public static RectangleF Norm(PointF a, PointF b)
        {
            return RectangleF.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
        }

        public static Pen MakePen(Color c, float w)
        {
            Pen p = new Pen(c, w);
            p.StartCap = p.EndCap = LineCap.Round;
            p.LineJoin = LineJoin.Round;
            return p;
        }

        static Color WithOpacity(Color c, int pct) { return Color.FromArgb(Math.Max(0, Math.Min(255, c.A * pct / 100)), c); }

        public static double Luma(Color c) { return (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0; }

        public static Font TextFont(float px) { return Fonts.Ui(px, 1); }

        // Een vloeiende lijn door de punten (de pen en de markeerstift)
        static void Free(Graphics g, List<PointF> pts, Color c, float w)
        {
            if (pts.Count == 0) return;
            if (pts.Count == 1)
            {
                using (SolidBrush b = new SolidBrush(c)) g.FillEllipse(b, pts[0].X - w / 2, pts[0].Y - w / 2, w, w);
                return;
            }
            using (Pen p = MakePen(c, w))
            {
                if (pts.Count == 2) g.DrawLine(p, pts[0], pts[1]);
                else g.DrawCurve(p, pts.ToArray(), 0.5f);
            }
        }

        // Pijl zoals in NexusPen: een lijn met een dichte, puntige kop
        static void Arrow(Graphics g, PointF a, PointF b, Color c, float w)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 2) return;
            float ux = dx / len, uy = dy / len;
            float head = Math.Min(len, Math.Max(14f, w * 4f)), hw = head * 0.6f;
            PointF bp = new PointF(b.X - ux * head, b.Y - uy * head);
            using (Pen p = MakePen(c, w)) g.DrawLine(p, a, new PointF(b.X - ux * head * 0.6f, b.Y - uy * head * 0.6f));
            PointF[] tri = { b, new PointF(bp.X - uy * hw, bp.Y + ux * hw), new PointF(bp.X + uy * hw, bp.Y - ux * hw) };
            using (SolidBrush br = new SolidBrush(c)) g.FillPolygon(br, tri);
        }

        // Tekst met een rand in de tegenkleur: leesbaar op elke achtergrond
        public static GraphicsPath TextPath(Mark m)
        {
            GraphicsPath gp = new GraphicsPath();
            if (string.IsNullOrEmpty(m.Text)) return gp;
            using (Font f = new Font(TextFont(m.FontSize).FontFamily, m.FontSize, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                StringFormat sf = new StringFormat(StringFormat.GenericTypographic);
                sf.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
                gp.AddString(m.Text, f.FontFamily, (int)FontStyle.Bold, m.FontSize, m.A, sf);
            }
            return gp;
        }

        public static Color EdgeOf(Color c) { return Luma(c) < 0.5 ? Color.White : Color.FromArgb(0x19, 0x1B, 0x23); }

        public static void Draw(Graphics g, Mark m)
        {
            if (m.Pts.Count == 0) return;
            Color c = WithOpacity(m.Color, m.Opacity);
            switch (m.Tool)
            {
                case EditTool.Pen: Free(g, m.Pts, c, m.Width); break;
                case EditTool.Marker: Free(g, m.Pts, Color.FromArgb(Math.Max(0, Math.Min(255, 110 * m.Opacity / 100)), m.Color), MarkerWidth(m.Width)); break;
                case EditTool.Arrow: Arrow(g, m.A, m.B, c, m.Width); break;
                case EditTool.Rect:
                    using (Pen p = MakePen(c, m.Width)) { RectangleF r = Norm(m.A, m.B); g.DrawRectangle(p, r.X, r.Y, r.Width, r.Height); }
                    break;
                case EditTool.Text:
                    using (GraphicsPath gp = TextPath(m))
                    {
                        if (gp.PointCount == 0) break;
                        using (Pen p = new Pen(WithOpacity(EdgeOf(m.Color), m.Opacity), Math.Max(2f, m.FontSize / 8f))) { p.LineJoin = LineJoin.Round; g.DrawPath(p, gp); }
                        using (SolidBrush br = new SolidBrush(c)) g.FillPath(br, gp);
                    }
                    break;
                case EditTool.Number:
                    {
                        float size = NumberSize(m.Width);
                        RectangleF r = new RectangleF(m.A.X - size / 2, m.A.Y - size / 2, size, size);
                        using (SolidBrush b = new SolidBrush(c)) g.FillEllipse(b, r);
                        using (Pen p = new Pen(Color.FromArgb(220, 255, 255, 255), Math.Max(1.5f, size / 14f))) g.DrawEllipse(p, r);
                        using (Font f = new Font(Fonts.Ui(10, 1).FontFamily, size * 0.5f, FontStyle.Bold, GraphicsUnit.Pixel))
                        using (SolidBrush b = new SolidBrush(Luma(m.Color) > 0.6 ? Color.Black : Color.White))
                        using (StringFormat sf = new StringFormat())
                        {
                            sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center;
                            g.DrawString(m.Number.ToString(), f, b, new RectangleF(r.X, r.Y + size * 0.03f, r.Width, r.Height), sf);
                        }
                        break;
                    }
            }
        }

        public static RectangleF Bounds(Mark m)
        {
            if (m.Pts.Count == 0) return RectangleF.Empty;
            switch (m.Tool)
            {
                case EditTool.Text:
                    using (GraphicsPath gp = TextPath(m))
                    {
                        RectangleF r = gp.PointCount > 0 ? gp.GetBounds() : new RectangleF(m.A.X, m.A.Y, m.FontSize, m.FontSize);
                        float e = Math.Max(2f, m.FontSize / 8f);
                        r.Inflate(e, e);
                        return r;
                    }
                case EditTool.Number:
                    { float s = NumberSize(m.Width); return new RectangleF(m.A.X - s / 2 - 2, m.A.Y - s / 2 - 2, s + 4, s + 4); }
            }
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (PointF p in m.Pts) { minX = Math.Min(minX, p.X); minY = Math.Min(minY, p.Y); maxX = Math.Max(maxX, p.X); maxY = Math.Max(maxY, p.Y); }
            float w = (m.Tool == EditTool.Marker ? MarkerWidth(m.Width) : m.Width) / 2 + (m.Tool == EditTool.Arrow ? Math.Max(14f, m.Width * 4f) : 0) + 2;
            return RectangleF.FromLTRB(minX - w, minY - w, maxX + w, maxY + w);
        }

        // Ligt punt p op (of vlak bij) deze markering? r = hoe ruim, in pixels van de afbeelding
        public static bool Hit(Mark m, PointF p, float r)
        {
            if (m.Pts.Count == 0) return false;
            if (m.Tool == EditTool.Text || m.Tool == EditTool.Number) { RectangleF b = Bounds(m); b.Inflate(r, r); return b.Contains(p); }
            float half = (m.Tool == EditTool.Marker ? MarkerWidth(m.Width) : m.Width) / 2 + r;
            if (m.Tool == EditTool.Rect)
            {
                RectangleF rr = Norm(m.A, m.B);
                RectangleF outer = rr, inner = rr;
                outer.Inflate(half, half); inner.Inflate(-half, -half);
                return outer.Contains(p) && !(inner.Width > 0 && inner.Height > 0 && inner.Contains(p));
            }
            int last = m.Tool == EditTool.Arrow ? 1 : m.Pts.Count - 1;
            if (m.Pts.Count == 1 || last == 0) { float dx = p.X - m.A.X, dy = p.Y - m.A.Y; return dx * dx + dy * dy <= half * half; }
            for (int i = 0; i < last; i++)
            {
                PointF a = m.Tool == EditTool.Arrow ? m.A : m.Pts[i], b = m.Tool == EditTool.Arrow ? m.B : m.Pts[i + 1];
                float vx = b.X - a.X, vy = b.Y - a.Y, len = vx * vx + vy * vy;
                float t = len > 0 ? Math.Max(0, Math.Min(1, ((p.X - a.X) * vx + (p.Y - a.Y) * vy) / len)) : 0;
                float ex = a.X + t * vx - p.X, ey = a.Y + t * vy - p.Y;
                if (ex * ex + ey * ey <= half * half) return true;
            }
            return false;
        }

        public static void Move(Mark m, float dx, float dy)
        {
            for (int i = 0; i < m.Pts.Count; i++) m.Pts[i] = new PointF(m.Pts[i].X + dx, m.Pts[i].Y + dy);
        }
    }

    // ------------------------------------------------------------------ Het document: de screenshot, de markeringen, uitsnede en ongedaan maken
    // Ongedaan maken bewaart stappen, geen hele afbeeldingen (een 4K-screenshot is 32 MB). Alleen bij vervagen
    // bewaren we het kleine stukje pixels dat veranderde.
    class EditDoc : IDisposable
    {
        public Bitmap Image;                         // de screenshot zelf (vervagen verandert echte pixels)
        public readonly List<Mark> Marks = new List<Mark>();
        public Rectangle Crop;                       // wat er overblijft; standaard de hele afbeelding
        public event Action Changed;

        abstract class Step
        {
            public abstract void Undo(EditDoc d);
            public abstract void Redo(EditDoc d);
            public virtual void Dispose() { }
        }

        class AddStep : Step
        {
            public Mark M; public int At;
            public override void Undo(EditDoc d) { d.Marks.Remove(M); }
            public override void Redo(EditDoc d) { d.Marks.Insert(Math.Min(At, d.Marks.Count), M); }
        }

        class RemoveStep : Step
        {
            public Mark M; public int At;
            public override void Undo(EditDoc d) { d.Marks.Insert(Math.Min(At, d.Marks.Count), M); }
            public override void Redo(EditDoc d) { d.Marks.Remove(M); }
        }

        class ChangeStep : Step
        {
            public Mark M; public Mark Before, After;
            void Put(Mark src) { M.Pts = new List<PointF>(src.Pts); M.Color = src.Color; M.Width = src.Width; M.Opacity = src.Opacity; M.Text = src.Text; M.FontSize = src.FontSize; M.Number = src.Number; }
            public override void Undo(EditDoc d) { Put(Before); }
            public override void Redo(EditDoc d) { Put(After); }
        }

        class CropStep : Step
        {
            public Rectangle Before, After;
            public override void Undo(EditDoc d) { d.Crop = Before; }
            public override void Redo(EditDoc d) { d.Crop = After; }
        }

        // Vervagen: het oude en het nieuwe stukje pixels (alleen dat stukje)
        class BlurStep : Step
        {
            public Rectangle R; public Bitmap Before, After;
            public override void Undo(EditDoc d) { d.Paste(Before, R); }
            public override void Redo(EditDoc d) { d.Paste(After, R); }
            public override void Dispose() { if (Before != null) Before.Dispose(); if (After != null) After.Dispose(); }
        }

        readonly List<Step> undo = new List<Step>(), redo = new List<Step>();
        public const int MaxUndo = 200;

        public EditDoc(Bitmap image)
        {
            Image = image;
            Crop = new Rectangle(0, 0, image.Width, image.Height);
        }

        public bool CanUndo { get { return undo.Count > 0; } }
        public bool CanRedo { get { return redo.Count > 0; } }
        public bool Dirty;                           // is er iets veranderd sinds openen of opslaan

        void Push(Step s)
        {
            undo.Add(s);
            foreach (Step r in redo) r.Dispose();
            redo.Clear();
            while (undo.Count > MaxUndo) { undo[0].Dispose(); undo.RemoveAt(0); }
            Dirty = true;
            Fire();
        }

        void Fire() { Action h = Changed; if (h != null) h(); }

        public void Undo()
        {
            if (undo.Count == 0) return;
            Step s = undo[undo.Count - 1]; undo.RemoveAt(undo.Count - 1);
            s.Undo(this); redo.Add(s); Dirty = true; Fire();
        }

        public void Redo()
        {
            if (redo.Count == 0) return;
            Step s = redo[redo.Count - 1]; redo.RemoveAt(redo.Count - 1);
            s.Redo(this); undo.Add(s); Dirty = true; Fire();
        }

        public void Add(Mark m) { Marks.Add(m); Push(new AddStep { M = m, At = Marks.Count - 1 }); }

        public void Remove(Mark m)
        {
            int at = Marks.IndexOf(m);
            if (at < 0) return;
            Marks.RemoveAt(at);
            Push(new RemoveStep { M = m, At = at });
        }

        // Na verplaatsen of een andere kleur: 'before' is een kopie van hoe het was
        public void Changed1(Mark m, Mark before) { Push(new ChangeStep { M = m, Before = before, After = m.Clone() }); }

        public void SetCrop(Rectangle r)
        {
            r.Intersect(new Rectangle(0, 0, Image.Width, Image.Height));
            if (r.Width < 4 || r.Height < 4 || r == Crop) return;
            Rectangle before = Crop;
            Crop = r;
            Push(new CropStep { Before = before, After = r });
        }

        // Volgend nummer voor de nummerstempel: een hoger dan het hoogste dat er staat
        public int NextNumber()
        {
            int n = 0;
            foreach (Mark m in Marks) if (m.Tool == EditTool.Number) n = Math.Max(n, m.Number);
            return n + 1;
        }

        // ---- vervagen: de pixels worden echt vervangen door blokjes (pixeleren). Niet terug te halen uit het bestand.
        public static int BlockSize(Rectangle r) { return Math.Max(8, Math.Min(r.Width, r.Height) / 8); }

        public void Blur(Rectangle r)
        {
            r.Intersect(new Rectangle(0, 0, Image.Width, Image.Height));
            if (r.Width < 2 || r.Height < 2) return;
            Bitmap before = Image.Clone(r, PixelFormat.Format32bppArgb);
            Pixelate(Image, r, BlockSize(r));
            Bitmap after = Image.Clone(r, PixelFormat.Format32bppArgb);
            Push(new BlurStep { R = r, Before = before, After = after });
        }

        void Paste(Bitmap piece, Rectangle r)
        {
            using (Graphics g = Graphics.FromImage(Image))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.DrawImageUnscaled(piece, r.X, r.Y);
            }
        }

        // Elk blokje krijgt de gemiddelde kleur van zijn pixels
        public static void Pixelate(Bitmap img, Rectangle r, int block)
        {
            BitmapData d = img.LockBits(r, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                int stride = d.Stride;
                byte[] buf = new byte[stride * r.Height];
                Marshal.Copy(d.Scan0, buf, 0, buf.Length);
                for (int by = 0; by < r.Height; by += block)
                    for (int bx = 0; bx < r.Width; bx += block)
                    {
                        int w = Math.Min(block, r.Width - bx), h = Math.Min(block, r.Height - by);
                        long sb = 0, sg = 0, sr = 0, n = (long)w * h;
                        for (int y = by; y < by + h; y++)
                        {
                            int row = y * stride;
                            for (int x = bx; x < bx + w; x++) { int i = row + x * 4; sb += buf[i]; sg += buf[i + 1]; sr += buf[i + 2]; }
                        }
                        byte b = (byte)(sb / n), g = (byte)(sg / n), rr = (byte)(sr / n);
                        for (int y = by; y < by + h; y++)
                        {
                            int row = y * stride;
                            for (int x = bx; x < bx + w; x++) { int i = row + x * 4; buf[i] = b; buf[i + 1] = g; buf[i + 2] = rr; buf[i + 3] = 255; }
                        }
                    }
                Marshal.Copy(buf, 0, d.Scan0, buf.Length);
            }
            finally { img.UnlockBits(d); }
        }

        // ---- de uitkomst: afbeelding met markeringen, uitgesneden
        public Bitmap Export()
        {
            Bitmap b = new Bitmap(Crop.Width, Crop.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.DrawImage(Image, new Rectangle(0, 0, Crop.Width, Crop.Height), Crop, GraphicsUnit.Pixel);
                g.CompositingMode = CompositingMode.SourceOver;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.TranslateTransform(-Crop.X, -Crop.Y);
                foreach (Mark m in Marks) MarkRender.Draw(g, m);
            }
            return b;
        }

        public void Dispose()
        {
            foreach (Step s in undo) s.Dispose();
            foreach (Step s in redo) s.Dispose();
            undo.Clear(); redo.Clear();
            if (Image != null) { Image.Dispose(); Image = null; }
        }
    }
}
