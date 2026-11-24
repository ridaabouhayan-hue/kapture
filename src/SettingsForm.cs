using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace Kapture
{
    // ------------------------------------------------------------------ Instellingen (Stitch-scherm 6)
    // Een gewoon venster, zelf getekend: bovenaan de zoekbalk, links het menu, rechts één pagina tegelijk.
    // Wijzigingen staan eerst in een werkkopie; pas met Opslaan gebruikt Kapture ze (met een bevestiging).
    // Tekst via Windows zelf (ClearType, zoals NexusPen): scherp en met de normale letterafstand.
    class SettingsForm : Form
    {
        // ---- de inhoud als gegevens: pagina's met kaarten met regels
        public enum RowKind { Toggle, Segments, Stepper, Buttons, Info }

        public class Btn { public string Label, Icon; public Func<string> LabelNow; public Action Click; public Func<bool> Show; public bool Primary; }

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
        }

        public class Card { public string Title, Desc, Icon; public List<Row> Rows = new List<Row>(); }

        public class Section { public string Name, Icon, Desc; public List<Card> Cards = new List<Card>(); }

        // ---- maten bij 100%
        const float TopH = 76, SearchW = 576, SearchH = 42, NavW = 232, NavItemH = 36, NavPad = 12, FootH = 68;
        const float ContentPadX = 32, ContentPadTop = 28, CardPad = 20, CardGap = 16, SectionGap = 40, RowPadY = 14, ControlGap = 20;

        readonly Settings cfg;            // wat Kapture nu gebruikt
        Settings work;                    // wat je hier aan het wijzigen bent
        bool autostartNow, autostartWork;
        readonly App app;
        public readonly List<Section> Sections = new List<Section>();
        readonly TextBox search = new TextBox();
        float s = 1;
        Theme t = Theme.Light;
        int navIndex;
        float scrollY, contentH;
        string saved;                     // bevestiging na Opslaan
        readonly Timer savedTimer = new Timer { Interval = 3500 };
        string updateStatus;              // uitkomst van "Nu zoeken"
        string inboxProblem; bool inboxChecked;

        // klikvlakken, opnieuw berekend bij elke keer tekenen
        class Zone { public RectangleF R; public string Id; public Action Click; public bool Fixed; }
        readonly List<Zone> zones = new List<Zone>();
        string hot, pressed;
        bool draggingThumb; float dragFrom, dragScroll;
        RectangleF thumbR, trackR;

        public SettingsForm(Settings settings, App owner)
        {
            cfg = settings; app = owner;
            Reload();
            Text = "Kapture · Instellingen";
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;
            ShowInTaskbar = true;
            KeyPreview = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            if (AppIcon.Get() != null) Icon = AppIcon.Get();
            search.BorderStyle = BorderStyle.None;
            search.TextChanged += delegate { scrollY = 0; Invalidate(); };
            search.KeyDown += delegate(object o, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) { search.Text = ""; e.Handled = true; e.SuppressKeyPress = true; } };
            Controls.Add(search);
            savedTimer.Tick += delegate { savedTimer.Stop(); saved = null; Invalidate(); };
            Build();
            ApplyScale(W.ScaleAt(Cursor.Position.X, Cursor.Position.Y));
        }

        int P(float v) { return (int)Math.Round(v * s); }

        // ---- werkkopie
        void Reload()
        {
            work = cfg.Clone();
            autostartNow = Autostart.IsOn; autostartWork = autostartNow;
            inboxChecked = false;
        }

        public bool Dirty { get { return work.DiffersFrom(cfg) || autostartWork != autostartNow; } }

        void Edited() { saved = null; ApplyTheme(); Invalidate(); }

        // Opslaan: de werkkopie wordt wat Kapture gebruikt, met een bevestiging onderaan
        public void SaveNow()
        {
            if (!Dirty) return;
            cfg.CopyFrom(work);
            cfg.Save();
            if (autostartWork != autostartNow) { Autostart.Set(autostartWork); autostartNow = Autostart.IsOn; autostartWork = autostartNow; }
            if (app != null) app.SettingsChanged();
            work = cfg.Clone();
            inboxChecked = false;
            saved = "Instellingen opgeslagen";
            savedTimer.Stop(); savedTimer.Start();
            ApplyTheme();
            Invalidate();
        }

        void Revert() { Reload(); saved = null; ApplyTheme(); Invalidate(); }

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

        // Het venster laat het gekozen thema meteen zien (voorbeeld); de rest van Kapture pas na Opslaan
        void ApplyTheme()
        {
            t = Program.ForceTheme ?? (work.ThemeMode == 1 ? Theme.Light : work.ThemeMode == 2 ? Theme.DarkTheme : SystemTheme());
            BackColor = t.Surface;
            search.BackColor = t.Surface;
            search.ForeColor = t.OnSurface;
            if (IsHandleCreated) { try { int v = t.Dark ? 1 : 0; W.DwmSetWindowAttribute(Handle, 20, ref v, 4); } catch { } }
        }

        static Theme SystemTheme() { int keep = Theme.Mode; Theme.Mode = 0; Theme r = Theme.Current; Theme.Mode = keep; return r; }

        public void ShowAndFocus()
        {
            if (!Dirty) Reload();
            ApplyTheme();
            if (!Visible) Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            W.SetForegroundWindow(Handle);
            Activate();
            Invalidate();
        }

        // Waarden opnieuw lezen (bijvoorbeeld na Sneltoetsen pauzeren uit het tray-menu); niet als je zelf aan het wijzigen bent
        public void RefreshValues() { if (!Dirty) Reload(); ApplyTheme(); Invalidate(); }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); ApplyTheme(); }

        // De inbox controleren (map aanmaken, proefbestand schrijven): niet bij elke keer tekenen
        string InboxProblem
        {
            get
            {
                if (!inboxChecked) { inboxProblem = InboxFolder.Check(InboxFolder.Resolve(work)); inboxChecked = true; }
                return inboxProblem;
            }
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
                GetIndex = delegate { return work.ThemeMode; }, SetIndex = delegate(int i) { work.ThemeMode = i; Edited(); }
            });
            algemeen.Cards.Add(thema);
            Card start = new Card { Title = "Opstarten en meldingen", Icon = "settings", Desc = "Wanneer Kapture start en hoe lang de melding blijft staan." };
            start.Rows.Add(new Row
            {
                Id = "autostart", Title = "Starten met Windows", Keywords = "opstarten automatisch aanmelden",
                Sub = delegate { return "Kapture staat dan meteen bij de klok."; },
                Kind = RowKind.Toggle, Get = delegate { return autostartWork; }, Set = delegate(bool v) { autostartWork = v; Edited(); }
            });
            start.Rows.Add(new Row
            {
                Id = "melding", Title = "Melding zichtbaar", Keywords = "seconden tijd toast rechtsonder",
                Sub = delegate { return "Hoe lang de melding rechtsonder blijft staan. Zolang je muis erboven hangt, blijft hij staan."; },
                Kind = RowKind.Stepper, GetNumber = delegate { return work.ToastSeconds; }, SetNumber = delegate(int n) { work.ToastSeconds = n; Edited(); },
                Min = 2, Max = 60, Unit = " s"
            });
            algemeen.Cards.Add(start);
            Sections.Add(algemeen);

            Section toetsen = new Section { Name = "Sneltoetsen", Icon = "keyboard", Desc = "Elke sneltoets kun je los aan of uit zetten." };
            Card knip = new Card { Title = "Print Screen en het Knipprogramma", Icon = "info", Desc = "Windows 11 stuurt Print Screen standaard naar het Knipprogramma. Dan krijgt Kapture hem niet." };
            Row knipRow = new Row
            {
                Id = "knipprogramma", Title = "Print Screen voor Kapture", Keywords = "knipprogramma snipping windows instelling werkt niet prtsc",
                Sub = delegate { return App.SnippingUsesPrintScreen() ? "Windows vangt Print Screen nu af voor het Knipprogramma. Klik op Zet uit, dan regelt Kapture het." : "In orde: Print Screen is vrij voor Kapture."; },
                Warn = delegate { return App.SnippingUsesPrintScreen(); },
                Kind = RowKind.Buttons
            };
            knipRow.Buttons.Add(new Btn { Label = "Zet uit", Icon = "check", Primary = true, Show = delegate { return App.SnippingUsesPrintScreen(); }, Click = delegate { if (app != null) app.FixSnipping(); else App.TurnOffSnippingPrintScreen(); Invalidate(); } });
            knipRow.Buttons.Add(new Btn { Label = "Windows-instelling", Icon = "external-link", Click = delegate { App.OpenSnippingSetting(); } });
            knip.Rows.Add(knipRow);
            toetsen.Cards.Add(knip);
            Card keys = new Card { Title = "Sneltoetsen", Icon = "keyboard", Desc = "Werkt overal, ook als Kapture op de achtergrond staat." };
            keys.Rows.Add(HotkeyRow("toets-screenshot", "Screenshot", "PrtSc", "print screen prtsc", App.HotkeyCapture, delegate { return work.HotkeyCapture; }, delegate(bool v) { work.HotkeyCapture = v; }));
            keys.Rows.Add(HotkeyRow("toets-notitie", "Screenshot met notitie", "Shift+PrtSc", "notitie shift", App.HotkeyNote, delegate { return work.HotkeyNote; }, delegate(bool v) { work.HotkeyNote = v; }));
            keys.Rows.Add(HotkeyRow("toets-laatste", "Notitie bij de laatste screenshot", "Ctrl+PrtSc", "notitie ctrl laatste achteraf", App.HotkeyLastNote, delegate { return work.HotkeyLastNote; }, delegate(bool v) { work.HotkeyLastNote = v; }));
            keys.Rows.Add(new Row
            {
                Id = "pauze", Title = "Alle sneltoetsen pauzeren", Keywords = "pauze uit tijdelijk",
                Sub = delegate { return "Handig als een ander programma Print Screen even nodig heeft. Werkt meteen, ook in het menu bij de klok."; },
                Kind = RowKind.Toggle, Get = delegate { return cfg.HotkeysPaused; },
                Set = delegate(bool v) { if (app != null) app.SetPaused(v); else cfg.HotkeysPaused = v; Invalidate(); }
            });
            toetsen.Cards.Add(keys);
            Sections.Add(toetsen);

            Section opslaan = new Section { Name = "Opslaan en mappen", Icon = "folder", Desc = "Waar je screenshots komen en hoe ze heten." };
            Card inbox = new Card { Title = "Inbox-map", Icon = "folder-open", Desc = "Hier zet Kapture elke screenshot neer. In OneDrive ziet Claude ze ook." };
            Row inboxRow = new Row
            {
                Id = "inbox", Title = "Map voor je screenshots", Keywords = "inbox map onedrive pictures screenshots opslaan pad locatie",
                Sub = delegate
                {
                    string f = InboxFolder.Resolve(work);
                    string problem = InboxProblem;
                    return f + (problem != null ? "\n" + problem : (string.IsNullOrEmpty(work.Inbox) ? "\nStandaardmap" : ""));
                },
                Warn = delegate { return InboxProblem != null; },
                MonoSub = true, Kind = RowKind.Buttons
            };
            inboxRow.Buttons.Add(new Btn { Label = "Wijzig...", Icon = "folder", Click = ChooseInbox });
            inboxRow.Buttons.Add(new Btn { Label = "Openen", Icon = "external-link", Click = delegate { OpenFolder(InboxFolder.Resolve(work)); } });
            inbox.Rows.Add(inboxRow);
            inbox.Rows.Add(new Row
            {
                Id = "maand", Title = "Elke maand een eigen map", Keywords = "maand submap ordenen structuur mappen",
                Sub = delegate { return "Bijvoorbeeld " + Path.Combine(Path.GetFileName(InboxFolder.Resolve(work).TrimEnd('\\')), DateTime.Now.ToString("yyyy-MM")) + ". Handig als de inbox vol raakt."; },
                Kind = RowKind.Toggle, Get = delegate { return work.MonthFolders; }, Set = delegate(bool v) { work.MonthFolders = v; Edited(); }
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
                Kind = RowKind.Toggle, Get = delegate { return work.NoteStrip; }, Set = delegate(bool v) { work.NoteStrip = v; Edited(); }
            });
            notes.Rows.Add(new Row
            {
                Id = "tekstbestand", Title = "Lange notitie ook als tekstbestand", Keywords = "txt tekst lang regels",
                Sub = delegate { return "Past de notitie niet in de naam, of heeft hij meerdere regels, dan staat de hele tekst in een .txt met dezelfde naam."; },
                Kind = RowKind.Toggle, Get = delegate { return work.NoteTextFile; }, Set = delegate(bool v) { work.NoteTextFile = v; Edited(); }
            });
            notes.Rows.Add(new Row
            {
                Id = "notitie-hoe", Title = "Zo typ je een notitie", Keywords = "hoe sneltoets melding shift ctrl",
                Sub = delegate { return "Shift+PrtSc: het kader blijft staan met een notitiebalk eronder.\nCtrl+PrtSc of een klik in de melding: notitie bij de laatste screenshot.\nEnter bewaart, Shift+Enter is een nieuwe regel, Esc sluit."; },
                Kind = RowKind.Info
            });
            notities.Cards.Add(notes);
            Sections.Add(notities);

            Section bewerken = new Section { Name = "Bewerken", Icon = "pencil", Desc = "De editor: pijlen, nummers, tekst, vervagen en uitsnijden." };
            Card ed = new Card { Title = "Editor", Icon = "pencil", Desc = "Open hem met Bewerken in de melding, of met een klik op de miniatuur." };
            ed.Rows.Add(new Row
            {
                Id = "miniatuur", Title = "Klik op de miniatuur opent", Keywords = "editor bewerken foto windows miniatuur openen",
                Sub = delegate { return work.ThumbOpensEditor ? "De editor van Kapture." : "Het programma dat Windows voor afbeeldingen gebruikt, bijvoorbeeld Foto's."; },
                Kind = RowKind.Segments, Segments = new string[] { "Kapture-editor", "Windows" },
                GetIndex = delegate { return work.ThumbOpensEditor ? 0 : 1; }, SetIndex = delegate(int i) { work.ThumbOpensEditor = i == 0; Edited(); }
            });
            ed.Rows.Add(new Row
            {
                Id = "editor-hoe", Title = "Zo werkt de editor", Keywords = "sneltoetsen ongedaan pijl tekst vervagen",
                Sub = delegate { return "Kies bovenin een gereedschap en teken op de screenshot. Ctrl+Z maakt ongedaan, Ctrl+C kopieert.\nKlaar bewaart de screenshot (in hetzelfde bestand) en zet hem op het klembord.\nVervagen vervangt de pixels echt: in het bestand is het daarna niet meer te lezen."; },
                Kind = RowKind.Info
            });
            bewerken.Cards.Add(ed);
            Sections.Add(bewerken);

            Section over = new Section { Name = "Over en bijwerken", Icon = "info", Desc = "Versie, nieuwe versies en privacy." };
            Card about = new Card { Title = "Kapture " + Program.Version, Icon = "camera", Desc = "Je eigen screenshot-tool voor Windows." };
            Row versie = new Row
            {
                Id = "versie", Title = "Nieuwe versie", Keywords = "update versie bijwerken github release downloaden",
                Sub = delegate
                {
                    if (app != null && app.Updating) return "Bezig met bijwerken...";
                    if (app != null && app.PendingUpdate != null) return "Versie " + app.PendingUpdate.Version + " staat klaar. Bijwerken duurt een paar seconden; Kapture start daarna vanzelf weer.";
                    return updateStatus ?? "Je hebt versie " + Program.Version + ". Klik op Nu zoeken om te kijken of er een nieuwere is.";
                },
                Kind = RowKind.Buttons
            };
            versie.Buttons.Add(new Btn
            {
                LabelNow = delegate { return app != null && app.PendingUpdate != null ? "Bijwerken naar " + app.PendingUpdate.Version : ""; },
                Icon = "download", Primary = true,
                Show = delegate { return app != null && app.PendingUpdate != null && !app.Updating; },
                Click = delegate { if (app != null) app.InstallUpdate(); }
            });
            versie.Buttons.Add(new Btn
            {
                Label = "Nu zoeken", Icon = "refresh-cw",
                Show = delegate { return app == null || app.PendingUpdate == null; },
                Click = delegate
                {
                    if (app == null) return;
                    updateStatus = "Even kijken op GitHub...";
                    Invalidate();
                    app.CheckForUpdateNow(delegate(int r)
                    {
                        updateStatus = r == 1 ? null : r == 0 ? "Je hebt de nieuwste versie (" + Program.Version + ")." : "Kon niet kijken. Is er internet?";
                        if (!IsDisposed) Invalidate();
                    });
                }
            });
            about.Rows.Add(versie);
            about.Rows.Add(new Row
            {
                Id = "zoeken-auto", Title = "Zelf zoeken naar nieuwe versies", Keywords = "automatisch update bijwerken internet",
                Sub = delegate { return "Bij het starten kijkt Kapture op GitHub of er een nieuwe versie is. Er gaat niets van jou mee."; },
                Kind = RowKind.Toggle, Get = delegate { return work.UpdateCheck; }, Set = delegate(bool v) { work.UpdateCheck = v; Edited(); }
            });
            Row page = new Row
            {
                Id = "versies", Title = "Alle versies", Keywords = "github downloadpagina release notes",
                Sub = delegate { return "Wat er in elke versie nieuw is, en de installers om zelf te downloaden."; },
                Kind = RowKind.Buttons
            };
            page.Buttons.Add(new Btn { Label = "Downloadpagina", Icon = "external-link", Click = delegate { App.Open(Updates.Page); } });
            about.Rows.Add(page);
            about.Rows.Add(new Row
            {
                Id = "privacy", Title = "Privacy", Keywords = "privacy internet gegevens",
                Sub = delegate { return "Kapture stuurt nooit vanzelf iets naar internet; alleen de vraag aan GitHub of er een nieuwe versie is. Het foutlog bevat nooit bestandsnamen, notities of wat er op een screenshot staat."; },
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
                    if (hotkeyId == App.HotkeyCapture && App.SnippingUsesPrintScreen()) return "Gaat nu naar het Knipprogramma. Zie hierboven: Zet uit.";
                    if (app != null && app.HotkeyTaken.ContainsKey(hotkeyId)) return "Bezet: een ander programma gebruikt deze toets, bijvoorbeeld ShareX.";
                    return "Aan";
                },
                Warn = delegate
                {
                    if (cfg.HotkeysPaused || !get()) return false;
                    if (hotkeyId == App.HotkeyCapture && App.SnippingUsesPrintScreen()) return true;
                    return app != null && app.HotkeyTaken.ContainsKey(hotkeyId);
                },
                Kind = RowKind.Toggle, Get = get, Set = delegate(bool v) { set(v); Edited(); }
            };
        }

        void ChooseInbox()
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.Description = "Kies de map voor je screenshots";
                d.SelectedPath = InboxFolder.Resolve(work);
                d.ShowNewFolderButton = true;
                if (d.ShowDialog(this) != DialogResult.OK) return;
                string problem = InboxFolder.Check(d.SelectedPath);
                if (problem != null) { MessageBox.Show(this, problem, "Kapture", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                work.Inbox = string.Equals(d.SelectedPath.TrimEnd('\\'), InboxFolder.Default().TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) ? "" : d.SelectedPath;
                inboxChecked = false;
                Edited();
            }
        }

        static void OpenFolder(string f)
        {
            try { Directory.CreateDirectory(f); System.Diagnostics.Process.Start("explorer.exe", "\"" + f + "\""); }
            catch (Exception ex) { Log.Error("Map openen mislukt", ex); }
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

        static bool ShowsRow(Row r) { return r != null; }

        public string Query { get { return search.Text; } set { search.Text = value; } }

        // ---- indeling
        RectangleF SearchBox { get { float w = Math.Min(P(SearchW), ClientSize.Width - P(NavW) - P(48)); w = Math.Max(w, P(240)); return new RectangleF((ClientSize.Width - w) / 2f, (P(TopH) - P(SearchH)) / 2f, w, P(SearchH)); } }
        RectangleF ContentOf(Size size) { return new RectangleF(P(NavW), P(TopH), size.Width - P(NavW), size.Height - P(TopH) - P(FootH)); }
        RectangleF Content { get { return ContentOf(ClientSize); } }

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

        // ---- tekst via Windows (ClearType): scherp, zoals in NexusPen en Verkenner
        const TextFormatFlags Base = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;

        static void Txt(Graphics g, string text, Font f, RectangleF r, Color c, bool wrap) { Txt(g, text, f, r, c, wrap, StringAlignment.Near); }

        static void Txt(Graphics g, string text, Font f, RectangleF r, Color c, bool wrap, StringAlignment align)
        {
            if (string.IsNullOrEmpty(text)) return;
            TextFormatFlags fl = Base;
            fl |= align == StringAlignment.Center ? TextFormatFlags.HorizontalCenter : align == StringAlignment.Far ? TextFormatFlags.Right : TextFormatFlags.Left;
            if (wrap) fl |= TextFormatFlags.WordBreak | TextFormatFlags.Top | TextFormatFlags.TextBoxControl;
            else fl |= TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
            Rectangle ri = Rectangle.FromLTRB((int)Math.Round(r.Left), (int)Math.Round(r.Top), (int)Math.Round(r.Right), (int)Math.Round(r.Bottom));
            TextRenderer.DrawText(g, text, f, ri, c, fl);
        }

        static float TextW(string text, Font f) { return TextRenderer.MeasureText(text ?? "", f, new Size(int.MaxValue, int.MaxValue), Base | TextFormatFlags.SingleLine).Width; }

        static float TextH(string text, Font f, float width)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return TextRenderer.MeasureText(text, f, new Size((int)Math.Max(1, width), int.MaxValue), Base | TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl).Height;
        }

        // ---- tekenen
        protected override void OnPaint(PaintEventArgs e) { Draw(e.Graphics, ClientSize, true); }

        // Tekent het hele venster. live = met het echte zoekvak (anders tekenen we de tekst zelf, voor de zelftest)
        public void Draw(Graphics g, Size size, bool live)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            zones.Clear();
            using (SolidBrush b = new SolidBrush(t.Surface)) g.FillRectangle(b, 0, 0, size.Width, size.Height);

            RectangleF content = ContentOf(size);
            GraphicsState st = g.Save();
            g.SetClip(content);
            DrawContent(g, content);
            g.Restore(st);

            DrawFooter(g, size);

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
                bool typed = !live && search.Text.Length > 0;
                Txt(g, typed ? search.Text : "Zoek een instelling...", Fonts.Ui(14 * s, 0), new RectangleF(sb.X + P(46), sb.Y, sb.Width - P(60), sb.Height), typed ? t.OnSurface : t.Outline, false);
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
                Color bg = sel ? t.PrimaryContainer : (hot == id ? t.Container : t.SurfaceLow);
                if (sel || hot == id)
                    using (GraphicsPath gp = Svg.RoundRect(r, P(8)))
                    using (SolidBrush b = new SolidBrush(bg)) g.FillPath(b, gp);
                Icons.Draw(g, Sections[i].Icon, new RectangleF(r.X + P(12), r.Y, P(16), r.Height), 16 * s, sel ? t.Primary : t.Variant, 2);
                Txt(g, Sections[i].Name, Fonts.Ui(13 * s, sel ? 1 : 0), new RectangleF(r.X + P(40), r.Y, r.Width - P(44), r.Height), sel ? t.OnPrimaryContainer : t.OnSurface, false);
                int idx = i;
                zones.Add(new Zone { R = r, Id = id, Click = delegate { GoTo(idx); }, Fixed = true });
                y += P(NavItemH) + P(4);
            }
        }

        // Onderaan: links de stand van zaken (of de bevestiging), rechts Annuleren en Opslaan
        void DrawFooter(Graphics g, Size size)
        {
            RectangleF foot = new RectangleF(P(NavW), size.Height - P(FootH), size.Width - P(NavW), P(FootH));
            using (SolidBrush b = new SolidBrush(t.SurfaceLow)) g.FillRectangle(b, foot);
            using (Pen p = new Pen(t.OutlineVariant)) g.DrawLine(p, foot.X, foot.Y + 0.5f, foot.Right, foot.Y + 0.5f);
            bool dirty = Dirty;
            Font bf = Fonts.Ui(13 * s, 1);
            float bh = P(36), by = foot.Y + (foot.Height - bh) / 2;
            float sw = TextW("Opslaan", bf) + P(16) + P(22) + P(16);
            RectangleF saveR = new RectangleF(foot.Right - P(ContentPadX) - sw, by, sw, bh);
            float cw = TextW("Annuleren", bf) + P(32);
            RectangleF cancelR = new RectangleF(saveR.X - P(8) - cw, by, cw, bh);

            // Opslaan: blauw als er iets te bewaren is, anders grijs
            using (GraphicsPath gp = Svg.RoundRect(saveR, P(8)))
            using (SolidBrush b = new SolidBrush(dirty ? (hot == "opslaan" ? Theme.Mix(t.Primary, t.OnSurface, 0.15f) : t.Primary) : t.Container))
                g.FillPath(b, gp);
            Color sc = dirty ? t.OnPrimary : Color.FromArgb(255, Theme.Mix(t.Container, t.OnSurface, 0.4f));
            Icons.Draw(g, "save", new RectangleF(saveR.X + P(16), saveR.Y, P(16), saveR.Height), 16 * s, sc, 2);
            Txt(g, "Opslaan", bf, new RectangleF(saveR.X + P(38), saveR.Y, saveR.Width - P(46), saveR.Height), sc, false);
            if (dirty) zones.Add(new Zone { R = saveR, Id = "opslaan", Click = SaveNow, Fixed = true });

            if (dirty)
            {
                using (GraphicsPath gp = Svg.RoundRect(new RectangleF(cancelR.X + 0.5f, cancelR.Y + 0.5f, cancelR.Width - 1, cancelR.Height - 1), P(8)))
                using (SolidBrush b = new SolidBrush(hot == "annuleren" ? t.Container : t.Surface))
                using (Pen p = new Pen(t.OutlineVariant)) { g.FillPath(b, gp); g.DrawPath(p, gp); }
                Txt(g, "Annuleren", bf, cancelR, t.OnSurface, false, StringAlignment.Center);
                zones.Add(new Zone { R = cancelR, Id = "annuleren", Click = Revert, Fixed = true });
            }

            // links: bevestiging, of wat er nog niet bewaard is
            float lx = foot.X + P(ContentPadX), lw = cancelR.X - P(16) - lx;
            Font mf = Fonts.Ui(13 * s, 0);
            if (saved != null)
            {
                RectangleF dot = new RectangleF(lx, foot.Y + (foot.Height - P(22)) / 2, P(22), P(22));
                using (SolidBrush db = new SolidBrush(t.PrimaryContainer)) g.FillEllipse(db, dot);
                Icons.Draw(g, "check", dot, 14 * s, t.Primary, 2.5f);
                Txt(g, saved + ". Kapture gebruikt ze nu.", Fonts.Ui(13 * s, 1), new RectangleF(lx + P(30), foot.Y, lw - P(30), foot.Height), t.OnSurface, false);
            }
            else if (dirty)
                Txt(g, "Je hebt wijzigingen die nog niet bewaard zijn.", mf, new RectangleF(lx, foot.Y, lw, foot.Height), t.OnSurface, false);
            else
                Txt(g, "Wijzigingen worden bewaard als je op Opslaan klikt.", mf, new RectangleF(lx, foot.Y, lw, foot.Height), t.Variant, false);
        }

        // Eén pagina (de gekozen sectie), of bij zoeken alle regels die passen
        void DrawContent(Graphics g, RectangleF area)
        {
            string q = search.Text.Trim();
            bool searching = q.Length > 0;
            float x = area.X + P(ContentPadX), w = Math.Min(area.Width - 2 * P(ContentPadX) - P(8), P(820));
            float y = area.Y + P(ContentPadTop) - scrollY;
            float top = y;
            bool any = false;
            for (int si = 0; si < Sections.Count; si++)
            {
                if (!searching && si != navIndex) continue;
                Section sec = Sections[si];
                List<Card> cards = new List<Card>();
                foreach (Card c in sec.Cards) foreach (Row r in c.Rows) if (ShowsRow(r) && Matches(r, q)) { cards.Add(c); break; }
                if (cards.Count == 0) continue;
                any = true;
                Txt(g, sec.Name, Fonts.Ui(22 * s, 1), new RectangleF(x, y, w, P(32)), t.OnSurface, false);
                y += P(34);
                Font df = Fonts.Ui(13 * s, 0);
                Txt(g, sec.Desc, df, new RectangleF(x, y, w, P(60)), t.Variant, true);
                y += TextH(sec.Desc, df, w) + P(16);
                foreach (Card c in cards) { y += DrawCard(g, c, x, y, w, q); y += P(CardGap); }
                y += P(SectionGap) - P(CardGap);
            }
            if (!any)
                Txt(g, "Niets gevonden voor \"" + q + "\". Probeer een ander woord, bijvoorbeeld map, notitie of sneltoets.", Fonts.Ui(14 * s, 0), new RectangleF(x, y, w, P(60)), t.Variant, true);
            contentH = (y - top) + P(ContentPadTop);
            DrawScrollbar(g, area);
        }

        float DrawCard(Graphics g, Card c, float x, float y, float w, string q)
        {
            Font titleF = Fonts.Ui(15 * s, 1), descF = Fonts.Ui(12 * s, 0);
            float pad = P(CardPad), inner = w - 2 * pad;
            float headH = Math.Max(P(40), P(24) + TextH(c.Desc, descF, inner - P(56)));
            List<Row> rows = new List<Row>();
            foreach (Row r in c.Rows) if (ShowsRow(r) && Matches(r, q)) rows.Add(r);
            List<float> rowH = new List<float>();
            float total = pad + headH + P(16);
            foreach (Row r in rows) { float h = RowHeight(r, inner); rowH.Add(h); total += h; }
            total += P(4) + pad - P(RowPadY);

            RectangleF cr = new RectangleF(x, y, w, total);
            using (GraphicsPath gp = Svg.RoundRect(new RectangleF(cr.X + 0.5f, cr.Y + 0.5f, cr.Width - 1, cr.Height - 1), P(12)))
            using (SolidBrush b = new SolidBrush(t.Surface))
            using (Pen p = new Pen(t.OutlineVariant)) { g.FillPath(b, gp); g.DrawPath(p, gp); }
            RectangleF tile = new RectangleF(x + pad, y + pad, P(40), P(40));
            using (GraphicsPath tp = Svg.RoundRect(tile, P(10))) using (SolidBrush b = new SolidBrush(t.Primary)) g.FillPath(b, tp);
            Icons.Draw(g, c.Icon, tile, 20 * s, Color.White, 2);
            float tx = tile.Right + P(16);
            Txt(g, c.Title, titleF, new RectangleF(tx, y + pad, inner - P(56), P(22)), t.OnSurface, false);
            Txt(g, c.Desc, descF, new RectangleF(tx, y + pad + P(24), inner - P(56), P(60)), t.Variant, true);
            float ry = y + pad + headH + P(16);
            for (int i = 0; i < rows.Count; i++)
            {
                using (Pen p = new Pen(t.OutlineVariant)) g.DrawLine(p, x + pad, ry - 0.5f, x + w - pad, ry - 0.5f);
                DrawRow(g, rows[i], new RectangleF(x + pad, ry, inner, rowH[i]));
                ry += rowH[i];
            }
            return total;
        }

        List<Btn> VisibleButtons(Row r)
        {
            List<Btn> list = new List<Btn>();
            foreach (Btn b in r.Buttons) if (b.Show == null || b.Show()) list.Add(b);
            return list;
        }

        static string LabelOf(Btn b) { return b.LabelNow != null ? b.LabelNow() : b.Label; }

        float ControlWidth(Row r)
        {
            switch (r.Kind)
            {
                case RowKind.Toggle:
                    return P(40) + (r.Keycap != null ? KeycapW(r.Keycap) + P(12) : 0);
                case RowKind.Segments:
                    { float w = 0; foreach (string seg in r.Segments) w += SegW(seg); return w + P(8); }
                case RowKind.Stepper: return P(120);
                case RowKind.Buttons:
                    { float w = 0; foreach (Btn b in VisibleButtons(r)) w += BtnW(b) + P(8); return Math.Max(0, w - P(8)); }
            }
            return 0;
        }

        float KeycapW(string k) { return TextW(k, Fonts.Mono(12 * s)) + P(16); }
        float SegW(string seg) { return TextW(seg, Fonts.Ui(13 * s, 1)) + P(28); }
        float BtnW(Btn b) { return TextW(LabelOf(b), Fonts.Ui(13 * s, 1)) + P(24) + (b.Icon != null ? P(24) : 0); }

        Font SubFont(Row r) { return r.MonoSub ? Fonts.Mono(12 * s) : Fonts.Ui(12 * s, 0); }

        float RowHeight(Row r, float inner)
        {
            float cw = ControlWidth(r);
            float tw = inner - (cw > 0 ? cw + P(ControlGap) : 0);
            float h = P(RowPadY) + P(20) + P(4) + TextH(r.Sub != null ? r.Sub() : "", SubFont(r), tw) + P(RowPadY);
            return Math.Max(P(60), h);
        }

        void DrawRow(Graphics g, Row r, RectangleF rr)
        {
            float cw = ControlWidth(r);
            float tw = rr.Width - (cw > 0 ? cw + P(ControlGap) : 0);
            Txt(g, r.Title, Fonts.Ui(14 * s, 0), new RectangleF(rr.X, rr.Y + P(RowPadY), tw, P(20)), t.OnSurface, false);
            string sub = r.Sub != null ? r.Sub() : "";
            bool warn = r.Warn != null && r.Warn();
            Txt(g, sub, SubFont(r), new RectangleF(rr.X, rr.Y + P(RowPadY) + P(24), tw, rr.Height), warn ? t.Error : t.Variant, true);

            float cx = rr.Right - cw, cy = rr.Y + P(RowPadY) - P(2);
            switch (r.Kind)
            {
                case RowKind.Toggle:
                    {
                        if (r.Keycap != null)
                        {
                            float kw = KeycapW(r.Keycap);
                            RectangleF key = new RectangleF(cx, cy, kw, P(26));
                            using (GraphicsPath kp = Svg.RoundRect(key, P(6)))
                            using (SolidBrush kb = new SolidBrush(t.SurfaceLow))
                            using (Pen kpn = new Pen(t.OutlineVariant)) { g.FillPath(kb, kp); g.DrawPath(kpn, kp); }
                            Txt(g, r.Keycap, Fonts.Mono(12 * s), key, t.OnSurface, false, StringAlignment.Center);
                            cx += kw + P(12);
                        }
                        RectangleF tr = new RectangleF(cx, cy + P(2), P(40), P(22));
                        DrawToggle(g, tr, r.Get(), hot == r.Id);
                        RectangleF hit = tr; hit.Inflate(P(6), P(6));
                        Row row = r;
                        zones.Add(new Zone { R = hit, Id = r.Id, Click = delegate { row.Set(!row.Get()); } });
                        break;
                    }
                case RowKind.Segments:
                    {
                        RectangleF box = new RectangleF(cx, cy, ControlWidth(r), P(32));
                        using (GraphicsPath bp = Svg.RoundRect(box, P(8))) using (SolidBrush bb = new SolidBrush(t.Container)) g.FillPath(bb, bp);
                        float sx = box.X + P(4);
                        int cur = r.GetIndex();
                        for (int i = 0; i < r.Segments.Length; i++)
                        {
                            float sw = SegW(r.Segments[i]);
                            RectangleF seg = new RectangleF(sx, box.Y + P(3), sw, box.Height - P(6));
                            string id = r.Id + ":" + i;
                            if (i == cur)
                                using (GraphicsPath sp = Svg.RoundRect(seg, P(6)))
                                using (SolidBrush sb = new SolidBrush(t.Surface))
                                using (Pen spn = new Pen(t.OutlineVariant)) { g.FillPath(sb, sp); g.DrawPath(spn, sp); }
                            else if (hot == id)
                                using (GraphicsPath sp = Svg.RoundRect(seg, P(6))) using (SolidBrush sb = new SolidBrush(t.ContainerHigh)) g.FillPath(sb, sp);
                            Txt(g, r.Segments[i], Fonts.Ui(13 * s, 1), seg, i == cur ? t.OnSurface : t.Variant, false, StringAlignment.Center);
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
                        Txt(g, r.GetNumber() + r.Unit, Fonts.Ui(14 * s, 1), new RectangleF(minus.Right, box.Y, plus.X - minus.Right, box.Height), t.OnSurface, false, StringAlignment.Center);
                        break;
                    }
                case RowKind.Buttons:
                    {
                        float bx = cx;
                        List<Btn> shown = VisibleButtons(r);
                        for (int i = 0; i < shown.Count; i++)
                        {
                            Btn b = shown[i];
                            float bw = BtnW(b);
                            RectangleF br = new RectangleF(bx, cy, bw, P(32));
                            string id = r.Id + ":b" + i;
                            Color bg = b.Primary ? (hot == id ? Theme.Mix(t.Primary, t.OnSurface, 0.15f) : t.Primary) : (hot == id ? t.Container : t.Surface);
                            using (GraphicsPath bp = Svg.RoundRect(new RectangleF(br.X + 0.5f, br.Y + 0.5f, br.Width - 1, br.Height - 1), P(8)))
                            using (SolidBrush bb = new SolidBrush(bg))
                            {
                                g.FillPath(bb, bp);
                                if (!b.Primary) using (Pen pn = new Pen(t.OutlineVariant)) g.DrawPath(pn, bp);
                            }
                            Color fc = b.Primary ? t.OnPrimary : t.OnSurface;
                            float lx = br.X + P(12);
                            if (b.Icon != null) { Icons.Draw(g, b.Icon, new RectangleF(lx, br.Y, P(16), br.Height), 16 * s, b.Primary ? t.OnPrimary : t.Primary, 2); lx += P(24); }
                            Txt(g, LabelOf(b), Fonts.Ui(13 * s, 1), new RectangleF(lx, br.Y, br.Right - lx - P(4), br.Height), fc, false);
                            Action click = b.Click;
                            zones.Add(new Zone { R = br, Id = id, Click = delegate { try { click(); } catch (Exception ex) { Log.Error("Knop in Instellingen mislukt", ex); } } });
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
            using (SolidBrush k = new SolidBrush(on ? Color.White : t.Outline)) g.FillEllipse(k, kx, r.Y + (r.Height - d) / 2, d, d);
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
            scrollY = 0;
            if (search.Text.Length > 0) search.Text = "";
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            scrollY -= e.Delta * P(60) / 120f;
            ClampScroll();
            Invalidate();
        }

        string HitTest(Point p)
        {
            if (!thumbR.IsEmpty && thumbR.Contains(p)) return "thumb";
            bool inContent = Content.Contains(p);
            for (int i = zones.Count - 1; i >= 0; i--)
            {
                if (!zones[i].R.Contains(p)) continue;
                // knoppen van de pagina die onder de bovenbalk of de onderbalk weggescrold zijn, tellen niet
                if (!zones[i].Fixed && !inContent) continue;
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
                ClampScroll(); Invalidate();
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
            if (key == (Keys.Control | Keys.S)) { SaveNow(); return true; }
            if (key == Keys.Escape && !search.Focused) { Close(); return true; }
            if (!search.Focused)
            {
                float page = Content.Height - P(60);
                if (key == Keys.PageDown || key == Keys.Down || key == Keys.PageUp || key == Keys.Up || key == Keys.Home || key == Keys.End)
                {
                    if (key == Keys.PageDown) scrollY += page; else if (key == Keys.PageUp) scrollY -= page;
                    else if (key == Keys.Down) scrollY += P(40); else if (key == Keys.Up) scrollY -= P(40);
                    else if (key == Keys.Home) scrollY = 0; else scrollY = contentH;
                    ClampScroll(); Invalidate();
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, key);
        }

        bool quitting;

        // Kapture stopt: sluiten zonder vragen (de vraag over opslaan is dan al gesteld, of niet nodig)
        public void CloseForQuit() { quitting = true; Close(); }

        // Sluiten met wijzigingen die nog niet bewaard zijn: eerst vragen. Daarna verbergen (zoekterm en pagina blijven).
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && !quitting)
            {
                if (Dirty)
                {
                    DialogResult a = MessageBox.Show(this, "Je hebt wijzigingen die nog niet bewaard zijn. Wil je ze opslaan?", "Kapture",
                        MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                    if (a == DialogResult.Cancel) { e.Cancel = true; return; }
                    if (a == DialogResult.Yes) SaveNow(); else Revert();
                }
                e.Cancel = true; Hide();
                return;
            }
            base.OnFormClosing(e);
        }

        // ---- voor de zelftest: het venster als plaatje, zonder het op het scherm te zetten
        public Bitmap RenderForTest(Theme theme, float scale, int section, string query) { return RenderForTest(theme, scale, section, query, false); }

        public Bitmap RenderForTest(Theme theme, float scale, int section, string query, bool edited)
        {
            Program.ForceTheme = theme;
            try
            {
                s = scale; t = theme;
                search.Font = Fonts.Ui(14 * s, 0);
                Size size = new Size(P(1040), P(720));
                search.Text = query ?? "";
                navIndex = section; scrollY = 0;
                if (edited) { work.MonthFolders = !work.MonthFolders; work.ToastSeconds = 8; }
                Bitmap b = new Bitmap(size.Width, size.Height);
                using (Graphics g = Graphics.FromImage(b)) Draw(g, size, false);
                return b;
            }
            finally { Program.ForceTheme = null; }
        }

        // Voor bijwerken: zijn er wijzigingen, dan eerst vragen. Geeft false als je Annuleren kiest.
        public bool AskSaveBeforeQuit()
        {
            if (!Dirty) return true;
            DialogResult a = MessageBox.Show(Visible ? this : null, "Je hebt wijzigingen in Instellingen die nog niet bewaard zijn. Wil je ze opslaan voor het bijwerken?", "Kapture",
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (a == DialogResult.Cancel) return false;
            if (a == DialogResult.Yes) SaveNow(); else Revert();
            return true;
        }

        public void ShowSavedForTest() { saved = "Instellingen opgeslagen"; }

        public int ZoneCountForTest { get { return zones.Count; } }
    }
}
