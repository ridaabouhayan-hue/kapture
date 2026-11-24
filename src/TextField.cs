using System;
using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Kapture
{
    // ------------------------------------------------------------------ Een zelf getekend tekstveld (voor de melding en de notitiebalk)
    // Zelf getekende vensters (UpdateLayeredWindow) kunnen geen gewoon tekstvak tonen. Dit houdt de tekst en de
    // cursor bij en tekent ze; het venster stuurt de toetsen door.
    class TextField
    {
        public const int MaxLength = 500;
        public string Text = "";
        public int Caret;
        public bool AllSelected;          // na Ctrl+A: de volgende toets vervangt alles
        public bool AllowNewLine = true;  // Shift+Enter = nieuwe regel
        DateTime blinkFrom = DateTime.Now;

        public event Action Changed;

        public void Set(string text)
        {
            Text = text ?? "";
            if (Text.Length > MaxLength) Text = Text.Substring(0, MaxLength);
            Caret = Text.Length; AllSelected = false; Touch();
        }

        void Touch() { blinkFrom = DateTime.Now; }

        void Fire() { Touch(); Action h = Changed; if (h != null) h(); }

        // De cursor knippert: aan in de eerste helft van elke 1,06 s, net als Windows
        public bool CaretVisible { get { return ((DateTime.Now - blinkFrom).TotalMilliseconds % 1060) < 530; } }

        void Insert(string s)
        {
            if (AllSelected) { Text = ""; Caret = 0; AllSelected = false; }
            s = s.Replace("\r\n", "\n").Replace("\r", "\n");
            if (!AllowNewLine) s = s.Replace("\n", " ");
            int room = MaxLength - Text.Length;
            if (room <= 0) return;
            if (s.Length > room) s = s.Substring(0, room);
            Text = Text.Insert(Caret, s); Caret += s.Length;
            Fire();
        }

        // Een getypt teken (WM_CHAR). Geeft true als het verwerkt is.
        public bool OnChar(char c)
        {
            if (c < ' ' || c == (char)127) return false;
            Insert(c.ToString());
            return true;
        }

        // Toetsen als Backspace, pijltjes en plakken. Enter en Esc handelt het venster zelf af.
        public bool OnKey(Keys key)
        {
            Keys code = key & Keys.KeyCode;
            bool ctrl = (key & Keys.Control) != 0;
            switch (code)
            {
                case Keys.Back:
                    if (AllSelected) { Text = ""; Caret = 0; AllSelected = false; Fire(); return true; }
                    if (Caret == 0) return true;
                    {
                        int from = ctrl ? WordStart(Caret) : Caret - 1;
                        Text = Text.Remove(from, Caret - from); Caret = from; Fire();
                    }
                    return true;
                case Keys.Delete:
                    if (AllSelected) { Text = ""; Caret = 0; AllSelected = false; Fire(); return true; }
                    if (Caret < Text.Length) { Text = Text.Remove(Caret, 1); Fire(); }
                    return true;
                case Keys.Left: AllSelected = false; Caret = ctrl ? WordStart(Caret) : Math.Max(0, Caret - 1); Touch(); return true;
                case Keys.Right: AllSelected = false; Caret = ctrl ? WordEnd(Caret) : Math.Min(Text.Length, Caret + 1); Touch(); return true;
                case Keys.Home: AllSelected = false; Caret = 0; Touch(); return true;
                case Keys.End: AllSelected = false; Caret = Text.Length; Touch(); return true;
                case Keys.A: if (ctrl) { AllSelected = Text.Length > 0; Caret = Text.Length; Touch(); return true; } return false;
                case Keys.V:
                    if (ctrl)
                    {
                        try { if (Clipboard.ContainsText()) Insert(Clipboard.GetText()); } catch (Exception ex) { Log.Error("Plakken mislukt", ex); }
                        return true;
                    }
                    return false;
            }
            return false;
        }

        public void NewLine() { if (AllowNewLine) Insert("\n"); }

        int WordStart(int i)
        {
            while (i > 0 && char.IsWhiteSpace(Text[i - 1])) i--;
            while (i > 0 && !char.IsWhiteSpace(Text[i - 1])) i--;
            return i;
        }

        int WordEnd(int i)
        {
            while (i < Text.Length && char.IsWhiteSpace(Text[i])) i++;
            while (i < Text.Length && !char.IsWhiteSpace(Text[i])) i++;
            return i;
        }

        // ---- tekenen: tekst (afbreken op woorden), geselecteerd = blauwe achtergrond, cursor als dun streepje
        static StringFormat Fmt()
        {
            StringFormat f = new StringFormat(StringFormat.GenericTypographic);
            f.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
            return f;
        }

        public static float LineHeight(Font f) { using (Bitmap b = new Bitmap(1, 1)) using (Graphics g = Graphics.FromImage(b)) return f.GetHeight(g); }

        // Hoeveel regels heeft de tekst bij deze breedte (voor de hoogte van het veld)
        public int Lines(Font f, float width)
        {
            if (Text.Length == 0) return 1;
            using (Bitmap b = new Bitmap(1, 1)) using (Graphics g = Graphics.FromImage(b))
            {
                SizeF sz = g.MeasureString(Text + (Text.EndsWith("\n") ? " " : ""), f, (int)Math.Max(1, width), Fmt());
                return Math.Max(1, (int)Math.Round(sz.Height / f.GetHeight(g)));
            }
        }

        public void Draw(Graphics g, RectangleF r, Font f, Color fg, Color placeholderColor, string placeholder, bool focused, Color caretColor, Color selection)
        {
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            StringFormat fmt = Fmt();
            if (Text.Length == 0)
            {
                if (!string.IsNullOrEmpty(placeholder))
                {
                    StringFormat pf = new StringFormat(fmt); pf.FormatFlags |= StringFormatFlags.NoWrap; pf.Trimming = StringTrimming.EllipsisCharacter;
                    using (SolidBrush b = new SolidBrush(placeholderColor)) g.DrawString(placeholder, f, b, r, pf);
                }
                if (focused && CaretVisible) DrawCaret(g, r.X, r.Y, f, caretColor);
                return;
            }
            if (focused && AllSelected)
            {
                SizeF all = g.MeasureString(Text, f, (int)r.Width, fmt);
                using (SolidBrush sb = new SolidBrush(selection)) g.FillRectangle(sb, r.X - 1, r.Y, Math.Min(r.Width, all.Width) + 2, all.Height);
            }
            using (SolidBrush b = new SolidBrush(fg)) g.DrawString(Text, f, b, r, fmt);
            if (focused && CaretVisible)
            {
                PointF p = CaretPoint(g, r, f, fmt);
                DrawCaret(g, p.X, p.Y, f, caretColor);
            }
        }

        // Waar de cursor staat: meet de tekst tot de cursor met dezelfde afbreking als bij het tekenen
        PointF CaretPoint(Graphics g, RectangleF r, Font f, StringFormat fmt)
        {
            if (Caret == 0) return new PointF(r.X, r.Y);
            CharacterRange[] cr = { new CharacterRange(Caret - 1, 1) };
            StringFormat mf = new StringFormat(fmt);
            mf.SetMeasurableCharacterRanges(cr);
            Region[] reg = g.MeasureCharacterRanges(Text, f, r, mf);
            RectangleF ch = reg.Length > 0 ? reg[0].GetBounds(g) : RectangleF.Empty;
            foreach (Region x in reg) x.Dispose();
            if (Text[Caret - 1] == '\n') return new PointF(r.X, ch.Y + f.GetHeight(g));
            if (ch.Width <= 0 && ch.Height <= 0) return new PointF(r.X, r.Y);
            return new PointF(ch.Right, ch.Y);
        }

        static void DrawCaret(Graphics g, float x, float y, Font f, Color c)
        {
            float h = f.GetHeight(g);
            using (Pen p = new Pen(c, Math.Max(1f, f.Size / 12f))) g.DrawLine(p, (float)Math.Round(x) + 0.5f, y + 1, (float)Math.Round(x) + 0.5f, y + h - 1);
        }
    }
}
