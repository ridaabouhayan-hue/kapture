using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;

namespace Kapture
{
    // ------------------------------------------------------------------ Instellingen (Stitch-scherm 6)
    // Een gewoon venster, zelf getekend: bovenaan de zoekbalk, links het menu, rechts kaarten met de instellingen.
    // Elke wijziging wordt meteen bewaard en toegepast.
    class SettingsForm : Form
    {
        // ---- de inhoud als gegevens: secties met kaarten met regels
        public enum RowKind { Toggle, Segments, Stepper, Buttons, Info }

        public class Btn { public string Label, Icon; public Action Click; }

        public class Row
        {
            public string Id, Title, Keywords = "";
            public Func<string> Sub;                 // tweede regel (mag veranderen)
            public Func<bool> Warn;                  // tweede regel in rood (probleem)
            public bool MonoSub;
            public RowKind Kind;
            public string Keycap;                    // sneltoets als toetsje voor de schakelaar
            public Func<bool> Get; public Action<bool> Set;
            public string[] Segments; public Func<int> GetIndex; public Action<int> SetIndex;
            public Func<int> GetNumber; public Action<int> SetNumber; public int Min, Max; public string Unit = "";
            public List<Btn> Buttons = new List<Btn>();
            public Func<bool> Enabled;
        }

        public class Card { public string Title, Desc, Icon; public List<Row> Rows = new List<Row>(); }

        public class Section { public string Name, Icon, Desc; public List<Card> Cards = new List<Card>(); }

        // ---- maten bij 100%
        const float TopH = 76, SearchW = 576, SearchH = 42, NavW = 232, NavItemH = 36, NavPad = 12;
        const float ContentPadX = 32, ContentPadTop = 28, CardPad = 20, CardGap = 16, SectionGap = 40, RowPadY = 14, ControlGap = 20;

        readonly Settings cfg;
        readonly App app;
        public readonly List<Section> Sections = new List<Section>();
        readonly TextBox search = new TextBox();
        float s = 1;
        Theme t = Theme.Light;
        int navIndex;
        float scrollY, contentH;
        readonly List<float> sectionY = new List<float>();
        int pendingNav = -1;               // na het leegmaken van het zoekvak: naar deze sectie scrollen zodra de opmaak klopt
        string inboxProblem;               // uitkomst van de laatste controle van de inbox (niet bij elke keer tekenen controleren)
        bool inboxChecked;

        // klikvlakken, opnieuw berekend bij elke keer tekenen
        class Zone { public RectangleF R; public string Id; public Action Click; }
        readonly List<Zone> zones = new List<Zone>();
        string hot, pressed;
        bool draggingThumb; float dragFrom, dragScroll;
        RectangleF thumbR, trackR;

        public SettingsForm(Settings settings, App owner)
        {
            cfg = settings; app = owner;
            Text = "Kapture · Instellingen";
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;
            ShowInTaskbar = true;
            KeyPreview = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            try { using (Bitmap b = Logo.Bitmap(32)) Icon = Icon.FromHandle(b.GetHicon()); } catch { }
            search.BorderStyle = BorderStyle.None;
            search.TextChanged += delegate { scrollY = 0; Invalidate(); };
            search.KeyDown += delegate(object o, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) { search.Text = ""; e.Handled = true; e.SuppressKeyPress = true; } };
            Controls.Add(search);
            Build();
            ApplyScale(W.ScaleAt(Cursor.Position.X, Cursor.Position.Y));
        }

        int P(float v) { return (int)Math.Round(v * s); }

        void ApplyScale(float scale)
        {
            s = scale;
            MinimumSize = new Size(P(820), P(560));
            if (!IsHandleCreated) ClientSize = new Size(P(1040), P(720));
            search.Font = Fonts.Ui(14 * s, 0);
            ApplyTheme();
            LayoutSearch();
            Invalidate();
        }

        void ApplyTheme()
        {
            t = Theme.Current;
            BackColor = t.Surface;
            search.BackColor = t.Surface;
            search.ForeColor = t.OnSurface;
            if (IsHandleCreated) { try { int v = t.Dark ? 1 : 0; W.DwmSetWindowAttribute(Handle, 20, ref v, 4); } catch { } }
        }

        public void ShowAndFocus()
        {
            CheckInbox();
            RefreshValues();
            if (!Visible) Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            W.SetForegroundWindow(Handle);
            Activate();
        }

        // Waarden opnieuw lezen (bijvoorbeeld na Sneltoetsen pauzeren uit het tray-menu)
        public void RefreshValues() { ApplyTheme(); Invalidate(); }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); ApplyTheme(); }

        // De inbox controleren (map aanmaken, proefbestand schrijven): alleen bij openen en na een wijziging
        void CheckInbox() { inboxProblem = InboxFolder.Check(InboxFolder.Resolve(cfg)); inboxChecked = true; }

        string InboxProblem { get { if (!inboxChecked) CheckInbox(); return inboxProblem; } }

        void Changed()
        {
            cfg.Save();
            CheckInbox();
            if (app != null) app.SettingsChanged();
            ApplyTheme();
            Invalidate();
        }

        // ---- de inhoud
        void Build()
        {
            Section algemeen = new Section { Name = "Algemeen", Icon = "sliders-horizontal", Desc = "Hoe Kapture eruitziet en opstart." };
            Card thema = new Card { Title = "Thema", Icon = "palette", Desc = "Licht of donker, of hetzelfde als Windows. De kleur is kobalt." };
            thema.Rows.Add(new Row
            {
                Id = "thema", Title = "Thema", Keywords = "licht donker kleur systeem dark mode uiterlijk",
                Sub = delegate { return "Geldt voor de werkbalk, de melding, het menu en dit venster."; },
                Kind = RowKind.Segments, Segments = new string[] { "Systeem", "Licht", "Donker" },
                GetIndex = delegate { return cfg.ThemeMode; }, SetIndex = delegate(int i) { cfg.ThemeMode = i; Changed(); }
            });
            algemeen.Cards.Add(thema);
            Card start = new Card { Title = "Opstarten en meldingen", Icon = "settings", Desc = "Wanneer Kapture start en hoe lang de melding blijft staan." };
            start.Rows.Add(new Row
            {
                Id = "autostart", Title = "Starten met Windows", Keywords = "opstarten automatisch aanmelden",
                Sub = delegate { return "Kapture staat dan meteen bij de klok."; },
                Kind = RowKind.Toggle, Get = delegate { return Autostart.IsOn; }, Set = delegate(bool v) { Autostart.Set(v); Invalidate(); }
            });
            start.Rows.Add(new Row
            {
                Id = "melding", Title = "Melding zichtbaar", Keywords = "seconden tijd toast rechtsonder",
                Sub = delegate { return "Hoe lang de melding rechtsonder blijft staan. Zolang je muis erboven hangt, blijft hij staan."; },
                Kind = RowKind.Stepper, GetNumber = delegate { return cfg.ToastSeconds; }, SetNumber = delegate(int n) { cfg.ToastSeconds = n; Changed(); },
                Min = 2, Max = 60, Unit = " s"
            });
            algemeen.Cards.Add(start);
            Sections.Add(algemeen);

            Section toetsen = new Section { Name = "Sneltoetsen", Icon = "keyboard", Desc = "Elke sneltoets kun je los aan of uit zetten." };
            Card keys = new Card { Title = "Sneltoetsen", Icon = "keyboard", Desc = "Werkt overal, ook als Kapture op de achtergrond staat." };
            keys.Rows.Add(HotkeyRow("toets-screenshot", "Screenshot", "PrtSc", "print screen prtsc", App.HotkeyCapture, delegate { return cfg.HotkeyCapture; }, delegate(bool v) { cfg.HotkeyCapture = v; }));
            keys.Rows.Add(HotkeyRow("toets-notitie", "Screenshot met notitie", "Shift+PrtSc", "notitie shift", App.HotkeyNote, delegate { return cfg.HotkeyNote; }, delegate(bool v) { cfg.HotkeyNote = v; }));
            keys.Rows.Add(HotkeyRow("toets-laatste", "Notitie bij de laatste screenshot", "Ctrl+PrtSc", "notitie ctrl laatste achteraf", App.HotkeyLastNote, delegate { return cfg.HotkeyLastNote; }, delegate(bool v) { cfg.HotkeyLastNote = v; }));
            keys.Rows.Add(new Row
            {
                Id = "pauze", Title = "Alle sneltoetsen pauzeren", Keywords = "pauze uit tijdelijk",
                Sub = delegate { return "Handig als een ander programma Print Screen even nodig heeft. Ook in het menu bij de klok."; },
                Kind = RowKind.Toggle, Get = delegate { return cfg.HotkeysPaused; }, Set = delegate(bool v) { cfg.HotkeysPaused = v; Changed(); }
            });
            toetsen.Cards.Add(keys);
            Card knip = new Card { Title = "Print Screen en het Knipprogramma", Icon = "info", Desc = "Windows 11 opent standaard het Knipprogramma met Print Screen." };
            Row knipRow = new Row
            {
                Id = "knipprogramma", Title = "Print Screen voor Kapture", Keywords = "knipprogramma snipping windows instelling",
                Sub = delegate { return App.SnippingUsesPrintScreen() ? "Windows gebruikt Print Screen nog voor het Knipprogramma. Zet in de instelling \"Print Screen gebruiken om het Knipprogramma te openen\" uit." : "In orde: Print Screen is vrij voor Kapture."; },
                Warn = delegate { return App.SnippingUsesPrintScreen(); },
                Kind = RowKind.Buttons
            };
            knipRow.Buttons.Add(new Btn { Label = "Windows-instelling openen", Icon = "external-link", Click = delegate { App.OpenSnippingSetting(); } });
            knip.Rows.Add(knipRow);
            toetsen.Cards.Add(knip);
            Sections.Add(toetsen);

            Section opslaan = new Section { Name = "Opslaan en mappen", Icon = "folder", Desc = "Waar je screenshots komen en hoe ze heten." };
            Card inbox = new Card { Title = "Inbox-map", Icon = "folder-open", Desc = "Hier zet Kapture elke screenshot neer. In OneDrive ziet Claude ze ook." };
            Row inboxRow = new Row
            {
                Id = "inbox", Title = "Map voor je screenshots", Keywords = "inbox map onedrive pictures screenshots opslaan pad locatie",
                Sub = delegate
                {
                    string f = InboxFolder.Resolve(cfg);
                    string problem = InboxProblem;
                    return f + (problem != null ? "\n" + problem : (string.IsNullOrEmpty(cfg.Inbox) ? "\nStandaardmap" : ""));
                },
                Warn = delegate { return InboxProblem != null; },
                MonoSub = true, Kind = RowKind.Buttons
            };
            inboxRow.Buttons.Add(new Btn { Label = "Wijzig...", Icon = "folder", Click = ChooseInbox });
            inboxRow.Buttons.Add(new Btn { Label = "Openen", Icon = "external-link", Click = delegate { if (app != null) app.OpenInbox(); } });
            inbox.Rows.Add(inboxRow);
            Row standaard = new Row
            {
                Id = "inbox-standaard", Title = "Terug naar de standaardmap", Keywords = "standaard onedrive herstellen",
                Sub = delegate { return InboxFolder.Default(); }, MonoSub = true, Kind = RowKind.Buttons,
                Enabled = delegate { return !string.IsNullOrEmpty(cfg.Inbox); }
            };
            standaard.Buttons.Add(new Btn { Label = "Standaard", Icon = "rotate-ccw", Click = delegate { cfg.Inbox = ""; Changed(); } });
            inbox.Rows.Add(standaard);
            inbox.Rows.Add(new Row
            {
                Id = "maand", Title = "Elke maand een eigen map", Keywords = "maand submap ordenen structuur mappen",
                Sub = delegate { return "Bijvoorbeeld " + Path.Combine(Path.GetFileName(InboxFolder.Resolve(cfg).TrimEnd('\\')), DateTime.Now.ToString("yyyy-MM")) + ". Handig als de inbox vol raakt."; },
                Kind = RowKind.Toggle, Get = delegate { return cfg.MonthFolders; }, Set = delegate(bool v) { cfg.MonthFolders = v; Changed(); }
            });
            inbox.Rows.Add(new Row
            {
                Id = "naam", Title = "Bestandsnaam", Keywords = "naam bestand datum programma patroon",
                Sub = delegate { return Naming.Base(DateTime.Now, "Chrome", "Opslaan-knop werkt niet") + ".png"; },
                MonoSub = true, Kind = RowKind.Info
            });
            opslaan.Cards.Add(inbox);
            Card data = new Card { Title = "Gegevens van Kapture", Icon = "file-text", Desc = "Instellingen, de laatste 10 screenshots en het foutlog. Alleen op deze pc." };
            Row dataRow = new Row
            {
                Id = "gegevens", Title = "Map met gegevens", Keywords = "appdata foutlog log geschiedenis instellingen",
                Sub = delegate { return Log.Dir; }, MonoSub = true, Kind = RowKind.Buttons
            };
            dataRow.Buttons.Add(new Btn { Label = "Openen", Icon = "external-link", Click = delegate { Directory.CreateDirectory(Log.Dir); App.Open(Log.Dir); } });
            data.Rows.Add(dataRow);
            opslaan.Cards.Add(data);
            Sections.Add(opslaan);

            Section notities = new Section { Name = "Notities", Icon = "sticky-note", Desc = "Waarom je een screenshot maakte, zonder op de foto te schrijven." };
            Card notes = new Card { Title = "Notities", Icon = "sticky-note", Desc = "De notitie komt in de bestandsnaam. Het bestand zelf blijft schoon." };
            notes.Rows.Add(new Row
            {
                Id = "strook", Title = "Notitiestrook op het klembord", Keywords = "klembord plakken claude strook",
                Sub = delegate { return "Plak je de screenshot in Claude, dan staat je notitie eronder. Het bestand blijft schoon."; },
                Kind = RowKind.Toggle, Get = delegate { return cfg.NoteStrip; }, Set = delegate(bool v) { cfg.NoteStrip = v; Changed(); }
            });
            notes.Rows.Add(new Row
            {
                Id = "tekstbestand", Title = "Lange notitie ook als tekstbestand", Keywords = "txt tekst lang regels",
                Sub = delegate { return "Past de notitie niet in de naam, of heeft hij meerdere regels, dan staat de hele tekst in een .txt met dezelfde naam."; },
                Kind = RowKind.Toggle, Get = delegate { return cfg.NoteTextFile; }, Set = delegate(bool v) { cfg.NoteTextFile = v; Changed(); }
            });
            notes.Rows.Add(new Row
            {
                Id = "notitie-hoe", Title = "Zo typ je een notitie", Keywords = "hoe sneltoets melding shift ctrl",
                Sub = delegate { return "Shift+PrtSc: het kader blijft staan met een notitiebalk eronder.\nCtrl+PrtSc of een klik in de melding: notitie bij de laatste screenshot.\nEnter bewaart, Shift+Enter is een nieuwe regel, Esc sluit."; },
                Kind = RowKind.Info
            });
            notities.Cards.Add(notes);
            Sections.Add(notities);

            Section over = new Section { Name = "Over", Icon = "info", Desc = "Versie en privacy." };
            Card about = new Card { Title = "Kapture " + Program.Version, Icon = "camera", Desc = "Je eigen screenshot-tool voor Windows." };
            Row versie = new Row
            {
                Id = "versie", Title = "Nieuwe versie", Keywords = "update versie bijwerken github release",
                Sub = delegate { return "Nieuwe versies staan op GitHub. Installeer de nieuwe Kapture-Setup over de oude heen; je instellingen blijven staan."; },
                Kind = RowKind.Buttons
            };
            versie.Buttons.Add(new Btn { Label = "Versies bekijken", Icon = "external-link", Click = delegate { App.Open("https://github.com/ridaabouhayan-hue/kapture/releases"); } });
            about.Rows.Add(versie);
            about.Rows.Add(new Row
            {
                Id = "privacy", Title = "Privacy", Keywords = "privacy internet gegevens",
                Sub = delegate { return "Kapture stuurt nooit vanzelf iets naar internet. Het foutlog bevat nooit bestandsnamen, notities of wat er op een screenshot staat."; },
                Kind = RowKind.Info
            });
            over.Cards.Add(about);
            Sections.Add(over);
        }

        Row HotkeyRow(string id, string title, string keycap, string keywords, int hotkeyId, Func<bool> get, Action<bool> set)
        {
            return new Row
            {
                Id = id, Title = title, Keycap = keycap, Keywords = keywords + " sneltoets toets",
                Sub = delegate
                {
                    if (cfg.HotkeysPaused) return "Op pauze";
                    if (!get()) return "Uit";
                    if (app != null && app.HotkeyTaken.ContainsKey(hotkeyId)) return "Bezet: een ander programma gebruikt deze toets, bijvoorbeeld ShareX.";
                    return "Aan";
                },
                Warn = delegate { return app != null && get() && !cfg.HotkeysPaused && app.HotkeyTaken.ContainsKey(hotkeyId); },
                Kind = RowKind.Toggle, Get = get, Set = delegate(bool v) { set(v); Changed(); }
            };
        }

        void ChooseInbox()
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.Description = "Kies de map voor je screenshots";
                d.SelectedPath = InboxFolder.Resolve(cfg);
                d.ShowNewFolderButton = true;
                if (d.ShowDialog(this) != DialogResult.OK) return;
                string problem = InboxFolder.Check(d.SelectedPath);
                if (problem != null) { MessageBox.Show(this, problem, "Kapture", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                cfg.Inbox = string.Equals(d.SelectedPath.TrimEnd('\\'), InboxFolder.Default().TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) ? "" : d.SelectedPath;
                Changed();
            }
        }

        // ---- zoeken: een regel past als alle woorden in de titel, de uitleg of de trefwoorden staan
        public static bool Matches(Row r, string query)
        {
            if (string.IsNullOrEmpty(query) || query.Trim().Length == 0) return true;
            string hay = (r.Title + " " + (r.Sub != null ? r.Sub() : "") + " " + r.Keywords + " " + (r.Keycap ?? "")).ToLowerInvariant();
            foreach (string w in query.ToLowerInvariant().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                if (hay.IndexOf(w, StringComparison.Ordinal) < 0) return false;
            return true;
        }

        // Een regel met Enabled = false staat er (nog) niet, bijvoorbeeld "Terug naar de standaardmap" als je die al gebruikt
        static bool ShowsRow(Row r) { return r.Enabled == null || r.Enabled(); }

        public string Query { get { return search.Text; } set { search.Text = value; } }

        // ---- indeling
        RectangleF SearchBox { get { float w = Math.Min(P(SearchW), ClientSize.Width - P(NavW) - P(48)); w = Math.Max(w, P(240)); return new RectangleF((ClientSize.Width - w) / 2f, (P(TopH) - P(SearchH)) / 2f, w, P(SearchH)); } }
        RectangleF Content { get { return new RectangleF(P(NavW), P(TopH), ClientSize.Width - P(NavW), ClientSize.Height - P(TopH)); } }

        void LayoutSearch()
        {
            RectangleF b = SearchBox;
            int h = search.PreferredHeight;
            search.Bounds = new Rectangle((int)(b.X + P(44)), (int)(b.Y + (b.Height - h) / 2), (int)(b.Width - P(44) - P(14)), h);
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); LayoutSearch(); ClampScroll(); Invalidate(); }

        protected override void WndProc(ref Message m)
        {
            const int WM_DPICHANGED = 0x02E0;
            if (m.Msg == WM_DPICHANGED)
            {
                int dpi = (int)((long)m.WParam & 0xFFFF);
                W.RECT rc = (W.RECT)System.Runtime.InteropServices.Marshal.PtrToStructure(m.LParam, typeof(W.RECT));
                ApplyScale(dpi / 96f);
                Bounds = new Rectangle(rc.Left, rc.Top, rc.Right - rc.Left, rc.Bottom - rc.Top);
                return;
            }
            base.WndProc(ref m);
        }

        // ---- tekenen
        protected override void OnPaint(PaintEventArgs e) { Draw(e.Graphics, ClientSize, true); }

        // Tekent het hele venster. live = met het echte zoekvak (anders tekenen we de tekst zelf, voor de zelftest)
        public void Draw(Graphics g, Size size, bool live)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            zones.Clear();
            using (SolidBrush b = new SolidBrush(t.Surface)) g.FillRectangle(b, 0, 0, size.Width, size.Height);

            // inhoud (eerst, zodat de bovenbalk en het menu eroverheen liggen)
            RectangleF content = new RectangleF(P(NavW), P(TopH), size.Width - P(NavW), size.Height - P(TopH));
            GraphicsState st = g.Save();
            g.SetClip(content);
            DrawContent(g, content);
            if (pendingNav >= 0 && pendingNav < sectionY.Count)
            {
                // nu kloppen de posities: naar de gekozen sectie en nog een keer tekenen
                scrollY = sectionY[pendingNav]; pendingNav = -1;
                float max = Math.Max(0, contentH - content.Height);
                scrollY = Math.Max(0, Math.Min(max, scrollY));
                zones.Clear();
                using (SolidBrush b = new SolidBrush(t.Surface)) g.FillRectangle(b, content);
                DrawContent(g, content);
            }
            g.Restore(st);

            // bovenbalk met zoekvak
            using (SolidBrush b = new SolidBrush(t.SurfaceLow)) g.FillRectangle(b, 0, 0, size.Width, P(TopH));
            using (Pen p = new Pen(t.OutlineVariant)) g.DrawLine(p, 0, P(TopH) - 0.5f, size.Width, P(TopH) - 0.5f);
            RectangleF sb = SearchBox;
            using (GraphicsPath gp = Svg.RoundRect(new RectangleF(sb.X + 0.5f, sb.Y + 0.5f, sb.Width - 1, sb.Height - 1), P(8)))
            using (SolidBrush b = new SolidBrush(t.Surface))
            using (Pen p = new Pen(search.Focused ? t.Primary : t.OutlineVariant, search.Focused ? Math.Max(1.5f, 2 * s) : 1))
            { g.FillPath(b, gp); g.DrawPath(p, gp); }
            Icons.Draw(g, "search", new RectangleF(sb.X + P(16), sb.Y, P(18), sb.Height), 18 * s, t.Variant, 2);
            if (!live || search.Text.Length == 0)
            {
                StringFormat sf = Line();
                string shown = !live && search.Text.Length > 0 ? search.Text : "Zoek een instelling...";
                using (SolidBrush b = new SolidBrush(!live && search.Text.Length > 0 ? t.OnSurface : t.Outline))
                    g.DrawString(shown, Fonts.Ui(14 * s, 0), b, new RectangleF(sb.X + P(46), sb.Y, sb.Width - P(60), sb.Height), sf);
            }

            // menu links
            using (SolidBrush b = new SolidBrush(t.SurfaceLow)) g.FillRectangle(b, 0, P(TopH), P(NavW), size.Height - P(TopH));
            using (Pen p = new Pen(t.OutlineVariant)) g.DrawLine(p, P(NavW) - 0.5f, P(TopH), P(NavW) - 0.5f, size.Height);
            bool searching = search.Text.Trim().Length > 0;
            float y = P(TopH) + P(NavPad) + P(8);
            for (int i = 0; i < Sections.Count; i++)
            {
                RectangleF r = new RectangleF(P(NavPad), y, P(NavW) - 2 * P(NavPad), P(NavItemH));
                bool sel = !searching && i == navIndex;
                string id = "nav" + i;
                if (sel || hot == id)
                    using (GraphicsPath gp = Svg.RoundRect(r, P(8)))
                    using (SolidBrush b = new SolidBrush(sel ? t.PrimaryContainer : t.Container)) g.FillPath(b, gp);
                Color fg = sel ? t.OnPrimaryContainer : t.OnSurface;
                Icons.Draw(g, Sections[i].Icon, new RectangleF(r.X + P(12), r.Y, P(16), r.Height), 16 * s, sel ? t.Primary : t.Variant, 2);
                using (SolidBrush b = new SolidBrush(fg)) g.DrawString(Sections[i].Name, Fonts.Ui(13 * s, sel ? 1 : 0), b, new RectangleF(r.X + P(40), r.Y, r.Width - P(44), r.Height), Line());
                int idx = i;
                zones.Add(new Zone { R = r, Id = id, Click = delegate { GoTo(idx); } });
                y += P(NavItemH) + P(4);
            }
        }

        static StringFormat Line()
        {
            StringFormat f = new StringFormat(StringFormat.GenericTypographic);
            f.LineAlignment = StringAlignment.Center; f.FormatFlags |= StringFormatFlags.NoWrap; f.Trimming = StringTrimming.EllipsisCharacter;
            return f;
        }

        static StringFormat Wrap() { return new StringFormat(StringFormat.GenericTypographic); }

        float TextH(Graphics g, string text, Font f, float width)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return (float)Math.Ceiling(g.MeasureString(text, f, (int)Math.Max(1, width), Wrap()).Height);
        }

        // Maakt de inhoud op (en tekent hem als 'draw'), met de scroll erbij. Geeft de totale hoogte.
        void DrawContent(Graphics g, RectangleF area)
        {
            string q = search.Text.Trim();
            float x = area.X + P(ContentPadX), w = Math.Min(area.Width - 2 * P(ContentPadX) - P(8), P(820));
            float y = area.Y + P(ContentPadTop) - scrollY;
            float top = y;
            sectionY.Clear();
            bool any = false;
            for (int si = 0; si < Sections.Count; si++)
            {
                Section sec = Sections[si];
                List<Card> cards = new List<Card>();
                foreach (Card c in sec.Cards) foreach (Row r in c.Rows) if (ShowsRow(r) && Matches(r, q)) { cards.Add(c); break; }
                sectionY.Add(y - top);
                if (cards.Count == 0) continue;
                any = true;
                using (SolidBrush b = new SolidBrush(t.OnSurface)) g.DrawString(sec.Name, Fonts.Ui(22 * s, 1), b, x, y, Wrap());
                y += P(32);
                using (SolidBrush b = new SolidBrush(t.Variant)) g.DrawString(sec.Desc, Fonts.Ui(13 * s, 0), b, new RectangleF(x, y, w, P(40)), Wrap());
                y += TextH(g, sec.Desc, Fonts.Ui(13 * s, 0), w) + P(16);
                foreach (Card c in cards) { y += DrawCard(g, c, x, y, w, q); y += P(CardGap); }
                y += P(SectionGap) - P(CardGap);
            }
            if (!any)
                using (SolidBrush b = new SolidBrush(t.Variant))
                    g.DrawString("Niets gevonden voor \"" + q + "\". Probeer een ander woord, bijvoorbeeld map, notitie of sneltoets.", Fonts.Ui(14 * s, 0), b, new RectangleF(x, y, w, P(60)), Wrap());
            contentH = (y + scrollY) - (area.Y + P(ContentPadTop)) + P(ContentPadTop) + P(40);
            DrawScrollbar(g, area);
        }

        float DrawCard(Graphics g, Card c, float x, float y, float w, string q)
        {
            Font titleF = Fonts.Ui(15 * s, 1), descF = Fonts.Ui(12 * s, 0);
            float pad = P(CardPad), inner = w - 2 * pad;
            // hoogte eerst uitrekenen
            float headH = Math.Max(P(40), P(22) + TextH(g, c.Desc, descF, inner - P(56)));
            List<Row> rows = new List<Row>();
            foreach (Row r in c.Rows) if (ShowsRow(r) && Matches(r, q)) rows.Add(r);
            List<float> rowH = new List<float>();
            float total = pad + headH + P(16);
            foreach (Row r in rows) { float h = RowHeight(g, r, inner); rowH.Add(h); total += h; }
            total += P(4) + pad - P(RowPadY);

            RectangleF cr = new RectangleF(x, y, w, total);
            using (GraphicsPath gp = Svg.RoundRect(new RectangleF(cr.X + 0.5f, cr.Y + 0.5f, cr.Width - 1, cr.Height - 1), P(12)))
            using (SolidBrush b = new SolidBrush(t.Surface))
            using (Pen p = new Pen(t.OutlineVariant)) { g.FillPath(b, gp); g.DrawPath(p, gp); }
            // kop: blauw tegeltje met icoon, titel en uitleg
            RectangleF tile = new RectangleF(x + pad, y + pad, P(40), P(40));
            using (GraphicsPath tp = Svg.RoundRect(tile, P(10))) using (SolidBrush b = new SolidBrush(t.Primary)) g.FillPath(b, tp);
            Icons.Draw(g, c.Icon, tile, 20 * s, Color.White, 2);
            float tx = tile.Right + P(16);
            using (SolidBrush b = new SolidBrush(t.OnSurface)) g.DrawString(c.Title, titleF, b, tx, y + pad, Wrap());
            using (SolidBrush b = new SolidBrush(t.Variant)) g.DrawString(c.Desc, descF, b, new RectangleF(tx, y + pad + P(22), inner - P(56), P(60)), Wrap());
            float ry = y + pad + headH + P(16);
            for (int i = 0; i < rows.Count; i++)
            {
                using (Pen p = new Pen(t.OutlineVariant)) g.DrawLine(p, x + pad, ry - 0.5f, x + w - pad, ry - 0.5f);
                DrawRow(g, rows[i], new RectangleF(x + pad, ry, inner, rowH[i]));
                ry += rowH[i];
            }
            return total;
        }

        float ControlWidth(Graphics g, Row r)
        {
            switch (r.Kind)
            {
                case RowKind.Toggle:
                    return P(40) + (r.Keycap != null ? KeycapW(g, r.Keycap) + P(12) : 0);
                case RowKind.Segments:
                    { float w = 0; foreach (string seg in r.Segments) w += SegW(g, seg); return w + P(8); }
                case RowKind.Stepper: return P(120);
                case RowKind.Buttons:
                    { float w = 0; foreach (Btn b in r.Buttons) w += BtnW(g, b) + P(8); return Math.Max(0, w - P(8)); }
            }
            return 0;
        }

        float KeycapW(Graphics g, string k) { return g.MeasureString(k, Fonts.Mono(12 * s), PointF.Empty, StringFormat.GenericTypographic).Width + P(16); }
        float SegW(Graphics g, string seg) { return g.MeasureString(seg, Fonts.Ui(13 * s, 1), PointF.Empty, StringFormat.GenericTypographic).Width + P(28); }
        float BtnW(Graphics g, Btn b) { return g.MeasureString(b.Label, Fonts.Ui(13 * s, 1), PointF.Empty, StringFormat.GenericTypographic).Width + P(24) + (b.Icon != null ? P(22) : 0); }

        Font SubFont(Row r) { return r.MonoSub ? Fonts.Mono(12 * s) : Fonts.Ui(12 * s, 0); }

        float RowHeight(Graphics g, Row r, float inner)
        {
            float cw = ControlWidth(g, r);
            float tw = inner - (cw > 0 ? cw + P(ControlGap) : 0);
            float h = P(RowPadY) + P(20) + P(4) + TextH(g, r.Sub != null ? r.Sub() : "", SubFont(r), tw) + P(RowPadY);
            return Math.Max(P(60), h);
        }

        void DrawRow(Graphics g, Row r, RectangleF rr)
        {
            bool enabled = r.Enabled == null || r.Enabled();
            float cw = ControlWidth(g, r);
            float tw = rr.Width - (cw > 0 ? cw + P(ControlGap) : 0);
            Color titleC = enabled ? t.OnSurface : Color.FromArgb(120, t.OnSurface);
            using (SolidBrush b = new SolidBrush(titleC)) g.DrawString(r.Title, Fonts.Ui(14 * s, 0), b, new RectangleF(rr.X, rr.Y + P(RowPadY), tw, P(20)), Line());
            string sub = r.Sub != null ? r.Sub() : "";
            bool warn = r.Warn != null && r.Warn();
            using (SolidBrush b = new SolidBrush(warn ? t.Error : t.Variant))
                g.DrawString(sub, SubFont(r), b, new RectangleF(rr.X, rr.Y + P(RowPadY) + P(24), tw, rr.Height), Wrap());

            float cx = rr.Right - cw, cy = rr.Y + P(RowPadY) - P(2);
            switch (r.Kind)
            {
                case RowKind.Toggle:
                    {
                        if (r.Keycap != null)
                        {
                            float kw = KeycapW(g, r.Keycap);
                            RectangleF key = new RectangleF(cx, cy, kw, P(26));
                            using (GraphicsPath kp = Svg.RoundRect(key, P(6)))
                            using (SolidBrush kb = new SolidBrush(t.SurfaceLow))
                            using (Pen kpn = new Pen(t.OutlineVariant)) { g.FillPath(kb, kp); g.DrawPath(kpn, kp); }
                            StringFormat c = Line(); c.Alignment = StringAlignment.Center;
                            using (SolidBrush fb = new SolidBrush(t.OnSurface)) g.DrawString(r.Keycap, Fonts.Mono(12 * s), fb, key, c);
                            cx += kw + P(12);
                        }
                        RectangleF tr = new RectangleF(cx, cy + P(2), P(40), P(22));
                        DrawToggle(g, tr, r.Get(), hot == r.Id);
                        RectangleF hit = tr; hit.Inflate(P(6), P(6));
                        Row row = r;
                        zones.Add(new Zone { R = hit, Id = r.Id, Click = delegate { row.Set(!row.Get()); Invalidate(); } });
                        break;
                    }
                case RowKind.Segments:
                    {
                        float total = ControlWidth(g, r);
                        RectangleF box = new RectangleF(cx, cy, total, P(32));
                        using (GraphicsPath bp = Svg.RoundRect(box, P(8))) using (SolidBrush bb = new SolidBrush(t.Container)) g.FillPath(bb, bp);
                        float sx = box.X + P(4);
                        int cur = r.GetIndex();
                        for (int i = 0; i < r.Segments.Length; i++)
                        {
                            float sw = SegW(g, r.Segments[i]);
                            RectangleF seg = new RectangleF(sx, box.Y + P(3), sw, box.Height - P(6));
                            string id = r.Id + ":" + i;
                            if (i == cur)
                                using (GraphicsPath sp = Svg.RoundRect(seg, P(6)))
                                using (SolidBrush sb = new SolidBrush(t.Surface))
                                using (Pen spn = new Pen(t.OutlineVariant)) { g.FillPath(sb, sp); g.DrawPath(spn, sp); }
                            else if (hot == id)
                                using (GraphicsPath sp = Svg.RoundRect(seg, P(6))) using (SolidBrush sb = new SolidBrush(t.ContainerHigh)) g.FillPath(sb, sp);
                            StringFormat c = Line(); c.Alignment = StringAlignment.Center;
                            using (SolidBrush fb = new SolidBrush(i == cur ? t.OnSurface : t.Variant)) g.DrawString(r.Segments[i], Fonts.Ui(13 * s, 1), fb, seg, c);
                            int idx = i; Row row = r;
                            zones.Add(new Zone { R = seg, Id = id, Click = delegate { row.SetIndex(idx); } });
                            sx += sw;
                        }
                        break;
                    }
                case RowKind.Stepper:
                    {
                        RectangleF box = new RectangleF(cx, cy, P(120), P(32));
                        using (GraphicsPath bp = Svg.RoundRect(new RectangleF(box.X + 0.5f, box.Y + 0.5f, box.Width - 1, box.Height - 1), P(8)))
                        using (Pen pn = new Pen(t.OutlineVariant)) g.DrawPath(pn, bp);
                        RectangleF minus = new RectangleF(box.X, box.Y, P(36), box.Height), plus = new RectangleF(box.Right - P(36), box.Y, P(36), box.Height);
                        Row row = r;
                        foreach (RectangleF b in new RectangleF[] { minus, plus })
                        {
                            bool isPlus = b == plus;
                            string id = r.Id + (isPlus ? ":plus" : ":min");
                            if (hot == id) using (GraphicsPath hp = Svg.RoundRect(b, P(8))) using (SolidBrush hb = new SolidBrush(t.Container)) g.FillPath(hb, hp);
                            using (Pen ip = new Pen(t.OnSurface, Math.Max(1.5f, 1.6f * s)))
                            {
                                float mx = b.X + b.Width / 2, my = b.Y + b.Height / 2, k = P(5);
                                g.DrawLine(ip, mx - k, my, mx + k, my);
                                if (isPlus) g.DrawLine(ip, mx, my - k, mx, my + k);
                            }
                            zones.Add(new Zone { R = b, Id = id, Click = delegate { int n = row.GetNumber() + (isPlus ? 1 : -1); row.SetNumber(Math.Max(row.Min, Math.Min(row.Max, n))); } });
                        }
                        StringFormat c = Line(); c.Alignment = StringAlignment.Center;
                        using (SolidBrush fb = new SolidBrush(t.OnSurface)) g.DrawString(r.GetNumber() + r.Unit, Fonts.Ui(14 * s, 1), fb, new RectangleF(minus.Right, box.Y, plus.X - minus.Right, box.Height), c);
                        break;
                    }
                case RowKind.Buttons:
                    {
                        float bx = cx;
                        for (int i = 0; i < r.Buttons.Count; i++)
                        {
                            Btn b = r.Buttons[i];
                            float bw = BtnW(g, b);
                            RectangleF br = new RectangleF(bx, cy, bw, P(32));
                            string id = r.Id + ":b" + i;
                            Color bg = !enabled ? Color.Empty : (hot == id ? t.Container : t.Surface);
                            using (GraphicsPath bp = Svg.RoundRect(new RectangleF(br.X + 0.5f, br.Y + 0.5f, br.Width - 1, br.Height - 1), P(8)))
                            using (Pen pn = new Pen(t.OutlineVariant))
                            {
                                if (bg != Color.Empty) using (SolidBrush bb = new SolidBrush(bg)) g.FillPath(bb, bp);
                                g.DrawPath(pn, bp);
                            }
                            Color fc = enabled ? t.OnSurface : Color.FromArgb(110, t.OnSurface);
                            float lx = br.X + P(12);
                            if (b.Icon != null) { Icons.Draw(g, b.Icon, new RectangleF(lx, br.Y, P(16), br.Height), 16 * s, enabled ? t.Primary : fc, 2); lx += P(22); }
                            using (SolidBrush fb = new SolidBrush(fc)) g.DrawString(b.Label, Fonts.Ui(13 * s, 1), fb, new RectangleF(lx, br.Y, br.Right - lx, br.Height), Line());
                            if (enabled)
                            {
                                Action click = b.Click;
                                zones.Add(new Zone { R = br, Id = id, Click = delegate { try { click(); } catch (Exception ex) { Log.Error("Knop in Instellingen mislukt", ex); } } });
                            }
                            bx += bw + P(8);
                        }
                        break;
                    }
            }
        }

        void DrawToggle(Graphics g, RectangleF r, bool on, bool hover)
        {
            using (GraphicsPath gp = Svg.RoundRect(r, r.Height / 2))
            {
                if (on) using (SolidBrush b = new SolidBrush(hover ? Theme.Mix(t.Primary, t.OnSurface, 0.12f) : t.Primary)) g.FillPath(b, gp);
                else
                {
                    using (SolidBrush b = new SolidBrush(hover ? t.Container : t.Surface)) g.FillPath(b, gp);
                    using (Pen p = new Pen(t.Outline, Math.Max(1.5f, 2 * s))) g.DrawPath(p, gp);
                }
            }
            float d = on ? r.Height - P(6) : r.Height - P(10);
            float kx = on ? r.Right - P(3) - d : r.X + P(5);
            using (SolidBrush k = new SolidBrush(on ? t.OnPrimary : t.Outline)) g.FillEllipse(k, kx, r.Y + (r.Height - d) / 2, d, d);
        }

        void DrawScrollbar(Graphics g, RectangleF area)
        {
            thumbR = trackR = RectangleF.Empty;
            if (contentH <= area.Height) return;
            trackR = new RectangleF(area.Right - P(12), area.Y + P(4), P(8), area.Height - P(8));
            float th = Math.Max(P(40), trackR.Height * area.Height / contentH);
            float ty = trackR.Y + (trackR.Height - th) * (scrollY / Math.Max(1, contentH - area.Height));
            thumbR = new RectangleF(trackR.X + P(1), ty, P(6), th);
            using (GraphicsPath gp = Svg.RoundRect(thumbR, P(3)))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(hot == "thumb" || draggingThumb ? 170 : 110, t.Outline))) g.FillPath(b, gp);
        }

        // ---- scrollen en klikken
        void ClampScroll()
        {
            float max = Math.Max(0, contentH - Content.Height);
            scrollY = Math.Max(0, Math.Min(max, scrollY));
        }

        void GoTo(int i)
        {
            navIndex = i;
            if (search.Text.Length > 0)
            {
                // de posities horen nog bij de gefilterde lijst: na het tekenen van de volledige lijst scrollen
                pendingNav = i;
                search.Text = "";
                Invalidate();
                return;
            }
            if (i < sectionY.Count) scrollY = sectionY[i];
            ClampScroll();
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            scrollY -= e.Delta * P(60) / 120f;
            ClampScroll();
            UpdateNavFromScroll();
            Invalidate();
        }

        void UpdateNavFromScroll()
        {
            if (search.Text.Trim().Length > 0) return;
            for (int i = sectionY.Count - 1; i >= 0; i--) if (scrollY + P(40) >= sectionY[i]) { navIndex = i; return; }
            navIndex = 0;
        }

        string HitTest(Point p)
        {
            if (!thumbR.IsEmpty && thumbR.Contains(p)) return "thumb";
            bool inContent = Content.Contains(p);
            for (int i = zones.Count - 1; i >= 0; i--)
            {
                if (!zones[i].R.Contains(p)) continue;
                // knoppen van de inhoud die onder de bovenbalk of het menu weggescrold zijn, tellen niet
                if (!zones[i].Id.StartsWith("nav", StringComparison.Ordinal) && !inContent) continue;
                return zones[i].Id;
            }
            return null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (draggingThumb)
            {
                float range = trackR.Height - thumbR.Height;
                if (range > 0) scrollY = dragScroll + (e.Y - dragFrom) * (contentH - Content.Height) / range;
                ClampScroll(); UpdateNavFromScroll(); Invalidate();
                return;
            }
            string h = HitTest(e.Location);
            Cursor want = h != null && h != "thumb" ? Cursors.Hand : Cursors.Default;
            if (Cursor != want) Cursor = want;
            if (h != hot) { hot = h; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); if (hot != null) { hot = null; Invalidate(); } }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (!thumbR.IsEmpty && thumbR.Contains(e.Location)) { draggingThumb = true; dragFrom = e.Y; dragScroll = scrollY; Capture = true; return; }
            pressed = HitTest(e.Location);
            if (SearchBox.Contains(e.Location)) search.Focus();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (draggingThumb) { draggingThumb = false; Capture = false; Invalidate(); return; }
            if (e.Button != MouseButtons.Left) return;
            string h = HitTest(e.Location);
            if (h == null || h != pressed) return;
            foreach (Zone z in zones.ToArray()) if (z.Id == h && z.Click != null) { z.Click(); break; }
            Invalidate();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys key)
        {
            if (key == (Keys.Control | Keys.F)) { search.Focus(); search.SelectAll(); return true; }
            if (key == Keys.Escape && !search.Focused) { Close(); return true; }
            if (!search.Focused)
            {
                float page = Content.Height - P(60);
                if (key == Keys.PageDown || key == Keys.Down || key == Keys.PageUp || key == Keys.Up || key == Keys.Home || key == Keys.End)
                {
                    if (key == Keys.PageDown) scrollY += page; else if (key == Keys.PageUp) scrollY -= page;
                    else if (key == Keys.Down) scrollY += P(40); else if (key == Keys.Up) scrollY -= P(40);
                    else if (key == Keys.Home) scrollY = 0; else scrollY = contentH;
                    ClampScroll(); UpdateNavFromScroll(); Invalidate();
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, key);
        }

        // Sluiten verbergt alleen; zo blijft de plek en de zoekterm bewaard tot Kapture stopt
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); return; }
            base.OnFormClosing(e);
        }

        // ---- voor de zelftest: het venster als plaatje, zonder het op het scherm te zetten
        public Bitmap RenderForTest(Theme theme, float scale, int section, string query)
        {
            Program.ForceTheme = theme;
            try
            {
                s = scale; t = theme;
                search.Font = Fonts.Ui(14 * s, 0);
                Size size = new Size(P(1040), P(720));
                search.Text = query ?? "";
                navIndex = section;
                Bitmap b = new Bitmap(size.Width, size.Height);
                // twee keer: de eerste keer leert de posities van de secties
                using (Graphics g = Graphics.FromImage(b)) Draw(g, size, false);
                scrollY = section < sectionY.Count && string.IsNullOrEmpty(query) ? sectionY[section] : 0;
                float max = Math.Max(0, contentH - (size.Height - P(TopH)));
                scrollY = Math.Max(0, Math.Min(max, scrollY));
                using (Graphics g = Graphics.FromImage(b)) Draw(g, size, false);
                return b;
            }
            finally { Program.ForceTheme = null; }
        }

        public int ZoneCountForTest { get { return zones.Count; } }
    }
}
