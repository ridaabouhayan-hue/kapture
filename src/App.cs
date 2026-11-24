using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Kapture
{
    // ------------------------------------------------------------------ Venster dat de sneltoetsen ontvangt
    class HotkeyWindow : NativeWindow
    {
        public Action<int> OnHotkey;
        public Action OnShowRequest;
        public static readonly int ShowMessage = W.RegisterWindowMessage("Kapture.AlGestart");

        public HotkeyWindow() { CreateHandle(new CreateParams()); }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == W.WM_HOTKEY && OnHotkey != null) { OnHotkey(m.WParam.ToInt32()); return; }
            if (m.Msg == ShowMessage && ShowMessage != 0 && OnShowRequest != null) { OnShowRequest(); return; }
            base.WndProc(ref m);
        }
    }

    // ------------------------------------------------------------------ Kapture zelf: tray-icoon, sneltoetsen, de stappen van een screenshot
    class App : ApplicationContext
    {
        public const int HotkeyCapture = 1, HotkeyNote = 2, HotkeyLastNote = 3, HotkeyCustom = 4;

        // Een eigen sneltoets (Keys met Ctrl, Alt, Shift) naar wat RegisterHotKey wil
        public static int ModsOf(int keys)
        {
            Keys k = (Keys)keys; int m = W.MOD_NOREPEAT;
            if ((k & Keys.Control) != 0) m |= W.MOD_CONTROL;
            if ((k & Keys.Alt) != 0) m |= W.MOD_ALT;
            if ((k & Keys.Shift) != 0) m |= W.MOD_SHIFT;
            return m;
        }

        public static string KeyName(int keys)
        {
            if (keys == 0) return "";
            Keys k = (Keys)keys;
            string s = "";
            if ((k & Keys.Control) != 0) s += "Ctrl+";
            if ((k & Keys.Alt) != 0) s += "Alt+";
            if ((k & Keys.Shift) != 0) s += "Shift+";
            Keys code = k & Keys.KeyCode;
            string n = code.ToString();
            if (code >= Keys.D0 && code <= Keys.D9) n = n.Substring(1);
            else if (code == Keys.Snapshot) n = "PrtSc";
            else if (code == Keys.OemQuestion) n = "/";
            else if (code == Keys.Oemcomma) n = ",";
            else if (code == Keys.OemPeriod) n = ".";
            return s + n;
        }

        // Mag deze toets een eigen sneltoets zijn? Geeft null als het mag, anders de reden.
        // Een sneltoets van Kapture werkt overal en pakt de toets af van elk ander programma. Daarom niet:
        // gewone letters, Ctrl+letter (kopiëren, plakken, opslaan...), Ctrl+Alt+letter (dat is AltGr: é, ë, ß),
        // Alt+F4 en andere vaste toetsen van Windows.
        public static string WhyNotKey(int keys)
        {
            Keys k = (Keys)keys, code = k & Keys.KeyCode;
            bool ctrl = (k & Keys.Control) != 0, alt = (k & Keys.Alt) != 0, shift = (k & Keys.Shift) != 0;
            if (code == Keys.ControlKey || code == Keys.ShiftKey || code == Keys.Menu || code == Keys.None || code == Keys.LWin || code == Keys.RWin) return "";
            if (code == Keys.Snapshot) return "Print Screen is al de gewone toets. Kies een andere.";
            if (code == Keys.Escape || code == Keys.Tab || code == Keys.Delete || code == Keys.Back || code == Keys.Enter || code == Keys.Space)
                return "Die toets heeft Windows zelf nodig. Kies bijvoorbeeld Ctrl+Shift+K.";
            if (code == Keys.F4 && alt) return "Alt+F4 sluit vensters. Kies een andere.";
            if (code >= Keys.F13 && code <= Keys.F24) return null;
            if (code >= Keys.F1 && code <= Keys.F12) return ctrl || alt || shift ? null : "Een F-toets alleen gebruiken programma's zelf. Zet Ctrl, Alt of Shift erbij.";
            if (!ctrl && !alt) return "Gebruik Ctrl+Shift of Alt+Shift erbij, bijvoorbeeld Ctrl+Shift+K. Gewone letters gaan niet: dan kun je niet meer typen.";
            if (ctrl && alt && !shift) return "Ctrl+Alt met een letter is ook AltGr (é, ë, ß). Kies Ctrl+Shift+letter, bijvoorbeeld Ctrl+Shift+K.";
            if (!shift) return "Alleen Ctrl of Alt met een letter gebruiken programma's zelf (kopiëren, plakken, opslaan). Zet Shift erbij, bijvoorbeeld Ctrl+Shift+K.";
            return null;
        }

        public static bool UsableKey(int keys) { return WhyNotKey(keys) == null; }

        // Tijdens het opnemen van een eigen sneltoets: alle sneltoetsen van Kapture even los, zodat de toetsen in Instellingen aankomen
        public void SuspendHotkeys()
        {
            foreach (int id in new int[] { HotkeyCapture, HotkeyNote, HotkeyLastNote, HotkeyCustom }) W.UnregisterHotKey(hot.Handle, id);
            retry.Stop();
        }

        readonly Settings cfg;
        readonly History history;
        readonly HotkeyWindow hot = new HotkeyWindow();
        readonly NotifyIcon tray = new NotifyIcon();
        readonly DimWindow dim = new DimWindow();
        readonly Overlay overlay = new Overlay();
        readonly Toast toast = new Toast();
        readonly TrayMenu menu = new TrayMenu();
        readonly ICapture capture = new GdiCapture();
        SettingsForm settingsForm;
        readonly System.Collections.Generic.Dictionary<Shot, EditorForm> editors = new System.Collections.Generic.Dictionary<Shot, EditorForm>();
        Dib shot;                 // het vastgelegde scherm, alleen zolang de overlay open is
        Rectangle shotOrigin;
        string shotApp = "";
        bool busy;
        Shot noteShot;            // de screenshot waar de notitiebalk bij hoort
        SaveResult noteResult;    // al opgeslagen terwijl de notitiebalk nog openstond
        readonly object noteGate = new object();
        string clipText;          // tekst die op het klembord stond voor een screenshot met notitie (om te plakken in de notitiebalk)
        int running;              // opslaan of hernoemen dat nog bezig is op de achtergrond

        // Welke sneltoetsen niet lukten (bezet door een ander programma); voor Instellingen
        public readonly System.Collections.Generic.Dictionary<int, bool> HotkeyTaken = new System.Collections.Generic.Dictionary<int, bool>();

        public App()
        {
            cfg = Settings.Load();
            Theme.Mode = cfg.ThemeMode;
            history = History.Load(History.DefaultPath);
            hot.OnHotkey = OnHotkey;
            hot.OnShowRequest = delegate { OpenSettings(); };   // Kapture nog eens gestart: open Instellingen
            overlay.Selected += OnSelected;
            overlay.Cancelled += OnCancelled;
            overlay.NoteDone += OnNoteDone;
            BuildTray();
            if (!retryHooked) { retry.Tick += delegate { RetryTaken(); }; retryHooked = true; }
            RegisterHotkeys(true);
            CheckSnippingSetting();
            string inboxProblem = InboxFolder.Check(InboxFolder.Resolve(cfg));
            if (inboxProblem != null)
                Notify("Inbox-map niet bereikbaar", inboxProblem + " Kies een andere map in Instellingen.", "Instellingen", "settings", delegate { OpenSettings(); }, true);

            // bijwerken: is Kapture net bijgewerkt, en staat er een nieuwe versie klaar?
            int flag = Updates.ReadFlag();
            Updates.CleanUp();
            if (flag == 1) Info("Kapture is bijgewerkt naar " + Program.Version, "Je instellingen zijn gebleven.", "download");
            else if (flag == -1) Notify("Bijwerken lukte niet", "Kapture draait nog de vorige versie. Probeer het later nog eens via Instellingen, Over.", null, null, null, false);
            if (cfg.UpdateCheck && !Program.SelfTest)
                Updates.CheckLater(20000, delegate(Updates.Info info) { try { overlay.BeginInvoke((Action)delegate { UpdateFound(info, false); }); } catch { } });
            Log.Info("Gestart " + Program.Version + " op Windows " + Environment.OSVersion.Version);
        }

        // ---- tray
        void BuildTray()
        {
            tray.Text = "Kapture";
            tray.Icon = TrayIcon();
            // links- en rechtsklikken openen hetzelfde menu (zoals in het Stitch-ontwerp)
            tray.MouseUp += delegate(object o, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left && e.Button != MouseButtons.Right) return;
                FillMenu(menu, cfg, history.Last != null, delegate { StartCapture(false); }, delegate { StartCapture(true); }, delegate { NoteForLast(); },
                    delegate { OpenInbox(); }, delegate { OpenSettings(); }, delegate { TogglePause(); }, delegate { Quit(); });
                if (PendingUpdate != null) menu.InsertUpdate("Bijwerken naar " + PendingUpdate.Version, delegate { InstallUpdate(); });
                menu.ShowAt(Cursor.Position);
            };
            tray.Visible = true;
            UpdateTrayText();
        }

        // De regels van het tray-menu (Stitch-scherm 7); ook gebruikt door de zelftest
        public static void FillMenu(TrayMenu m, Settings c, bool hasLast, Action shot, Action shotNote, Action lastNote, Action inbox, Action settings, Action pause, Action quit)
        {
            bool keys = !c.HotkeysPaused;
            m.Clear();
            m.AddHeader("Kapture", "Naar Claude");
            m.Add("Nieuwe screenshot", "camera", keys && c.HotkeyCapture ? "PrtSc" : null, shot);
            m.Add("Screenshot met notitie", "sticky-note", keys && c.HotkeyNote ? "Shift+PrtSc" : null, shotNote);
            m.Add("Notitie bij laatste", "pencil", keys && c.HotkeyLastNote ? "Ctrl+PrtSc" : null, hasLast ? lastNote : null);
            m.AddSeparator();
            m.Add("Inbox-map openen", "folder-open", inbox);
            m.Add("Instellingen", "settings", settings);
            m.AddCheck("Sneltoetsen pauzeren", "pause", c.HotkeysPaused, pause);
            m.AddSeparator();
            m.Add("Afsluiten", "power", quit);
        }

        static Icon TrayIcon()
        {
            try
            {
                Stream st = typeof(App).Assembly.GetManifestResourceStream("Kapture.ico");
                if (st != null) using (st) return new Icon(st, SystemInformation.SmallIconSize);
            }
            catch (Exception ex) { Log.Error("Tray-icoon laden mislukt", ex); }
            using (Bitmap b = Logo.Bitmap(32)) return Icon.FromHandle(b.GetHicon());
        }

        // ---- sneltoetsen
        public void RegisterHotkeys(bool tell)
        {
            foreach (int id in new int[] { HotkeyCapture, HotkeyNote, HotkeyLastNote, HotkeyCustom }) W.UnregisterHotKey(hot.Handle, id);
            HotkeyTaken.Clear();
            if (!cfg.HotkeysPaused)
            {
                System.Collections.Generic.List<string> bad = new System.Collections.Generic.List<string>();
                if (cfg.HotkeyCapture && !W.RegisterHotKey(hot.Handle, HotkeyCapture, W.MOD_NOREPEAT, W.VK_SNAPSHOT)) { HotkeyTaken[HotkeyCapture] = true; bad.Add("Print Screen"); }
                if (cfg.HotkeyNote && !W.RegisterHotKey(hot.Handle, HotkeyNote, W.MOD_NOREPEAT | W.MOD_SHIFT, W.VK_SNAPSHOT)) { HotkeyTaken[HotkeyNote] = true; bad.Add("Shift+Print Screen"); }
                if (cfg.HotkeyLastNote && !W.RegisterHotKey(hot.Handle, HotkeyLastNote, W.MOD_NOREPEAT | W.MOD_CONTROL, W.VK_SNAPSHOT)) { HotkeyTaken[HotkeyLastNote] = true; bad.Add("Ctrl+Print Screen"); }
                if (cfg.CustomKey != 0 && UsableKey(cfg.CustomKey) && !W.RegisterHotKey(hot.Handle, HotkeyCustom, ModsOf(cfg.CustomKey), cfg.CustomKey & (int)Keys.KeyCode)) { HotkeyTaken[HotkeyCustom] = true; bad.Add(KeyName(cfg.CustomKey)); }
                if (bad.Count > 0)
                {
                    Log.Info(bad.Count + " sneltoets(en) konden niet vastgelegd worden");
                    if (tell) Notify(string.Join(", ", bad.ToArray()) + (bad.Count == 1 ? " is bezet" : " zijn bezet"),
                        BusyHelp + " Kapture probeert het zelf elke 15 seconden opnieuw.", "Opnieuw", "rotate-ccw", delegate { RegisterHotkeys(true); }, true);
                }
            }
            UpdateTrayText();
            if (HotkeyTaken.Count > 0) retry.Start(); else retry.Stop();
        }

        public const string BusyHelp = "Een ander programma gebruikt deze toets. Vaak is dat OneDrive (instelling \"Schermafbeeldingen opslaan in OneDrive\") of ShareX.";

        // Bezette sneltoetsen stil opnieuw proberen: komt Print Screen vrij (OneDrive of ShareX dicht), dan werkt hij meteen
        readonly Timer retry = new Timer { Interval = 15000 };
        bool retryHooked;

        void RetryTaken()
        {
            if (cfg.HotkeysPaused || HotkeyTaken.Count == 0) { retry.Stop(); return; }
            bool freed = false;
            if (HotkeyTaken.ContainsKey(HotkeyCapture) && W.RegisterHotKey(hot.Handle, HotkeyCapture, W.MOD_NOREPEAT, W.VK_SNAPSHOT)) { HotkeyTaken.Remove(HotkeyCapture); freed = true; }
            if (HotkeyTaken.ContainsKey(HotkeyNote) && W.RegisterHotKey(hot.Handle, HotkeyNote, W.MOD_NOREPEAT | W.MOD_SHIFT, W.VK_SNAPSHOT)) { HotkeyTaken.Remove(HotkeyNote); freed = true; }
            if (HotkeyTaken.ContainsKey(HotkeyLastNote) && W.RegisterHotKey(hot.Handle, HotkeyLastNote, W.MOD_NOREPEAT | W.MOD_CONTROL, W.VK_SNAPSHOT)) { HotkeyTaken.Remove(HotkeyLastNote); freed = true; }
            if (HotkeyTaken.ContainsKey(HotkeyCustom) && cfg.CustomKey != 0 && W.RegisterHotKey(hot.Handle, HotkeyCustom, ModsOf(cfg.CustomKey), cfg.CustomKey & (int)Keys.KeyCode)) { HotkeyTaken.Remove(HotkeyCustom); freed = true; }
            if (freed)
            {
                Log.Info("Sneltoets weer vrij en vastgelegd");
                Info(HotkeyTaken.Count == 0 ? "Sneltoetsen werken weer" : "Een sneltoets werkt weer", "Het andere programma gebruikt de toets niet meer. Druk op Print Screen om het te proberen.", "check");
                if (settingsForm != null && !settingsForm.IsDisposed) settingsForm.RefreshValues();
            }
            if (HotkeyTaken.Count == 0) retry.Stop();
        }

        void TogglePause() { SetPaused(!cfg.HotkeysPaused); }

        // Pauze werkt meteen (tray-menu en Instellingen); staat los van Opslaan in Instellingen
        public void SetPaused(bool paused)
        {
            cfg.HotkeysPaused = paused;
            cfg.Save();
            RegisterHotkeys(true);
            if (settingsForm != null && !settingsForm.IsDisposed) settingsForm.RefreshValues();
        }

        void UpdateTrayText() { tray.Text = cfg.HotkeysPaused ? "Kapture (sneltoetsen op pauze)" : "Kapture"; }

        // Windows 11 opent standaard het Knipprogramma met Print Screen. Eén keer melden hoe je dat uitzet.
        public static bool SnippingUsesPrintScreen()
        {
            object v = null;
            try { v = Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Keyboard", "PrintScreenKeyForSnippingEnabled", null); } catch { }
            return !(v is int && (int)v == 0);
        }

        public static void OpenSnippingSetting() { Open("ms-settings:easeofaccess-keyboard"); }

        // De Windows-instelling "Print Screen opent het Knipprogramma" uitzetten (alleen voor jou, geen beheerder nodig).
        // Windows leest hem meestal meteen; anders na opnieuw aanmelden. Geeft true als het register nu "uit" zegt.
        public static bool TurnOffSnippingPrintScreen()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Control Panel\Keyboard"))
                    k.SetValue("PrintScreenKeyForSnippingEnabled", 0, RegistryValueKind.DWord);
                // WM_SETTINGCHANGE aan alle vensters, op de achtergrond: een traag venster mag Kapture niet laten hangen
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    try { IntPtr res; W.SendMessageTimeout(new IntPtr(0xFFFF), 0x001A, IntPtr.Zero, "Control Panel\\Keyboard", 2, 1000, out res); } catch { }
                });
                Log.Info("Print Screen losgemaakt van het Knipprogramma");
            }
            catch (Exception ex) { Log.Error("Print Screen-instelling aanpassen mislukt", ex); }
            return !SnippingUsesPrintScreen();
        }

        void CheckSnippingSetting()
        {
            // elke start opnieuw zolang Windows Print Screen nog naar het Knipprogramma stuurt: anders doet PrtSc niets in Kapture
            if (!cfg.HotkeyCapture || !SnippingUsesPrintScreen()) return;
            Notify("Print Screen gaat nu naar het Knipprogramma", "Windows vangt Print Screen af, dus Kapture krijgt hem niet (Ctrl+PrtSc en Shift+PrtSc werken wel). Klik op Zet uit, dan regelt Kapture het.",
                "Zet uit", "keyboard", delegate { FixSnipping(); }, true);
        }

        public void FixSnipping()
        {
            bool off = TurnOffSnippingPrintScreen();
            RegisterHotkeys(false);
            if (off) Info("Print Screen is nu van Kapture", "Druk op Print Screen om het te proberen. Opent het Knipprogramma toch nog? Meld je dan een keer af en weer aan bij Windows.", "check");
            else Notify("Uitzetten lukte niet", "Zet het zelf uit in de Windows-instelling: \"Print Screen gebruiken om het Knipprogramma te openen\".", "Open instelling", "settings-2", delegate { OpenSnippingSetting(); }, true);
            if (settingsForm != null && !settingsForm.IsDisposed) settingsForm.RefreshValues();
        }

        void OnHotkey(int id)
        {
            if (id == HotkeyCapture || id == HotkeyCustom) StartCapture(false);
            else if (id == HotkeyNote) StartCapture(true);
            else if (id == HotkeyLastNote) NoteForLast();
        }

        // ---- een screenshot: donker, vastleggen, kader, opslaan
        public void StartCapture(bool withNote)
        {
            if (busy || overlay.IsActive || overlay.InNoteMode) return;
            busy = true;
            try
            {
                toast.Dismiss();
                menu.HideMenu();
                clipText = null;
                if (withNote) { try { if (Clipboard.ContainsText()) clipText = Clipboard.GetText(); } catch { } }
                shotApp = Naming.ForegroundApp();
                Rectangle v = VirtualScreen.Bounds;
                if (dim.Excluded) dim.ShowOver(v);
                shot = capture.Grab(v);
                if (shot == null)
                {
                    dim.HideNow(); busy = false;
                    Notify("Screenshot lukte niet", "Windows gaf het scherm niet vrij. Probeer het nog eens.", null, null, null, false);
                    return;
                }
                shotOrigin = v;
                Point cur = Cursor.Position;
                Rectangle mon = Screen.FromPoint(cur).Bounds;
                if (!overlay.Prepare(shot, v, Theme.Current, cur, mon, withNote))
                {
                    FreeShot(); dim.HideNow(); busy = false;
                    Notify("Screenshot lukte niet", "Er was niet genoeg geheugen vrij. Sluit een paar programma's en probeer het nog eens.", null, null, null, false);
                    return;
                }
                overlay.ShowNow();
                dim.HideNow();
                Watchdog.Arm(overlay.Handle, dim.Handle);
            }
            catch (Exception ex)
            {
                Log.Error("Screenshot starten mislukt", ex);
                SafeReset();
            }
        }

        void OnSelected(Rectangle r)
        {
            try
            {
                Bitmap piece = shot.Crop(r);
                bool note = overlay.WithNote && piece != null;
                if (!note)
                {
                    Watchdog.Disarm();
                    overlay.End();
                    FreeShot();
                    busy = false;
                }
                if (piece == null) { if (note) SafeReset(); return; }
                // welk programma staat onder het midden van het kader (de vensters van Kapture tellen niet mee)
                string under = Naming.AppAt(shotOrigin.X + r.X + r.Width / 2, shotOrigin.Y + r.Y + r.Height / 2);
                if (under != "" && under != "Verkenner") shotApp = under;
                else if (shotApp == "Verkenner") shotApp = under;
                Shot entry = new Shot { Time = DateTime.Now, App = shotApp ?? "" };
                string folder = InboxFolder.ForShot(cfg, entry.Time);
                if (note)
                {
                    noteShot = entry; noteResult = null;
                    string paste = clipText; clipText = null;
                    overlay.BeginNote(r, delegate(string n) { return Naming.Base(entry.Time, entry.App, n) + ".png"; }, paste);
                }
                System.Threading.Interlocked.Increment(ref running);
                Saver.SaveAndCopy(piece, entry, folder, true, cfg.NoteStrip, cfg.NoteTextFile, overlay, delegate(SaveResult sr)
                {
                    System.Threading.Interlocked.Decrement(ref running);
                    OnSaved(sr);
                });
            }
            catch (Exception ex)
            {
                Log.Error("Kader verwerken mislukt", ex);
                SafeReset();
            }
        }

        void OnSaved(SaveResult r)
        {
            Trim();
            if (!r.Failed) history.Add(r.Shot);
            if (noteShot == r.Shot && overlay.InNoteMode) { noteResult = r; return; }   // de melding komt na de notitie
            string pending = r.Shot.PendingNote; r.Shot.PendingNote = null;
            if (!r.Failed && !string.IsNullOrEmpty(pending)) ApplyNote(r.Shot, pending);
            ShowSaved(r, pending);
        }

        // Notitiebalk klaar: Enter (tekst), Esc (null) of weggeklikt
        void OnNoteDone(string text)
        {
            Watchdog.Disarm();
            overlay.End();
            FreeShot();
            busy = false;
            Shot entry = noteShot; noteShot = null;
            SaveResult r = noteResult; noteResult = null;
            if (entry == null) return;
            if (r == null) { entry.PendingNote = text ?? ""; return; }   // nog aan het opslaan: OnSaved doet de rest
            if (!r.Failed && !string.IsNullOrEmpty(text)) ApplyNote(entry, text);
            ShowSaved(r, text);
        }

        void OnCancelled()
        {
            Watchdog.Disarm();
            overlay.End();
            FreeShot();
            busy = false;
            Trim();
        }

        void SafeReset()
        {
            Watchdog.Disarm();
            try { overlay.End(); } catch { }
            try { dim.HideNow(); } catch { }
            FreeShot();
            noteShot = null; noteResult = null;
            busy = false;
        }

        void FreeShot() { if (shot != null) { shot.Dispose(); shot = null; } }

        // 'note' = de notitie die net getypt is (wordt misschien nog in de naam gezet)
        void ShowSaved(SaveResult r, string note)
        {
            if (r.Failed)
            {
                if (r.Thumb != null) r.Thumb.Dispose();
                string sub = "De inbox-map was niet bereikbaar. Staat OneDrive aan? Je kunt een andere map kiezen in Instellingen.";
                if (!string.IsNullOrEmpty(note)) sub += " Je notitie was: " + note;
                Notify(r.Copied ? "Gekopieerd, niet opgeslagen" : "Opslaan lukte niet", sub, "Instellingen", "settings", delegate { OpenSettings(); }, true);
                return;
            }
            ShowShotToast(r.Shot, r.Thumb, r.Copied, false, note);
        }

        // Melding met notitieveld voor een screenshot. edit = meteen het toetsenbord in het notitieveld (Ctrl+PrtSc).
        void ShowShotToast(Shot entry, Bitmap thumb, bool copied, bool edit) { ShowShotToast(entry, thumb, copied, edit, null); }

        void ShowShotToast(Shot entry, Bitmap thumb, bool copied, bool edit, string typed)
        {
            string note = !string.IsNullOrEmpty(typed) ? typed.Trim() : (entry.Note ?? "");
            ToastContent c = new ToastContent();
            c.Thumb = thumb;
            c.Title = note.Length > 0 ? "Notitie bewaard" : (copied ? "Opgeslagen en gekopieerd" : "Opgeslagen");
            c.Sub = note.Length > 0 ? Naming.Base(entry.Time, entry.App, note) + ".png" : (copied || edit ? "Preset: Nu naar Claude" : "Kopieren lukte niet; het bestand staat wel in je inbox.");
            c.ActionLabel = "Bewerken"; c.ActionIcon = "pencil";
            c.OnAction = delegate { OpenEditor(entry, c.Note); };   // c.Note: net getypt in de melding
            c.Action2Label = "Map openen"; c.Action2Icon = "folder-open";
            c.OnAction2 = delegate { ShowInFolder(entry.Path); };
            c.OnThumb = delegate { if (cfg.ThumbOpensEditor) OpenEditor(entry, c.Note); else Open(entry.Path); };
            c.Seconds = cfg.ToastSeconds;
            c.NoteField = true; c.Note = note; c.Tag = entry;
            c.OnNote = delegate(string n) { ApplyNote(entry, n); };
            toast.ShowToast(c, Theme.Current);
            if (edit) toast.BeginEdit();
        }

        // ---- de editor (Stitch-scherm 5)
        public void OpenEditor(Shot entry) { OpenEditor(entry, null); }

        // 'note': de notitie zoals je hem net zag (kan nog aan het hernoemen zijn)
        public void OpenEditor(Shot entry, string note)
        {
            EditorForm open;
            if (entry != null && editors.TryGetValue(entry, out open) && !open.IsDisposed)
            {
                // één editor per screenshot: anders overschrijft de ene het werk van de andere
                if (open.WindowState == FormWindowState.Minimized) open.WindowState = FormWindowState.Normal;
                W.SetForegroundWindow(open.Handle); open.Activate();
                return;
            }
            string path;
            lock (noteGate) path = entry != null ? entry.Path : null;
            if (path == null || !File.Exists(path))
            {
                Notify("Screenshot niet gevonden", "Het bestand is verplaatst of verwijderd.", null, null, null, false);
                return;
            }
            try
            {
                Bitmap img = Saver.Load(path);
                EditorForm.Host h = new EditorForm.Host();
                h.SaveImage = delegate(Bitmap b)
                {
                    bool ok;
                    // onder hetzelfde slot als hernoemen: nooit opslaan terwijl het bestand van naam wisselt
                    lock (noteGate) ok = Saver.Replace(b, entry.Path);
                    if (ok) Log.Info("Screenshot bewerkt en opgeslagen");
                    return ok;
                };
                h.Copy = delegate(Bitmap b, string n, bool sameAsFile)
                {
                    bool ok;
                    if (cfg.NoteStrip && !string.IsNullOrEmpty(n)) using (Bitmap withNote = Notes.WithStrip(b, n)) ok = Saver.Copy(withNote, null);
                    else ok = Saver.Copy(b, null);
                    // alleen als het klembord precies het bestand is, mag een latere notitie het klembord bijwerken
                    if (ok) { entry.Copied = sameAsFile; entry.ClipboardSeq = W.GetClipboardSequenceNumber(); }
                    return ok;
                };
                h.NoteChanged = delegate(string changed) { ApplyNote(entry, changed); };
                EditorForm f = new EditorForm(img, path, note ?? entry.Note, h);
                editors[entry] = f;
                f.FormClosed += delegate { editors.Remove(entry); };
                f.Show();
                W.SetForegroundWindow(f.Handle);
                f.Activate();
            }
            catch (Exception ex)
            {
                Log.Error("Editor openen mislukt", ex);
                Notify("Bewerken lukte niet", "De screenshot kon niet geopend worden.", null, null, null, false);
            }
        }

        // Ctrl+PrtSc: notitie bij de laatste screenshot
        void NoteForLast()
        {
            if (overlay.IsActive || overlay.InNoteMode) return;
            ToastContent cur = toast.Content;
            if (cur != null && cur.NoteField) { toast.BeginEdit(); return; }
            Shot last = history.Last;
            if (last == null) { Notify("Nog geen screenshot", "Maak eerst een screenshot met Print Screen. Daarna kun je er met Ctrl+PrtSc een notitie bij typen.", null, null, null, false); return; }
            Bitmap thumb = null;
            try { using (Bitmap full = Saver.Load(last.Path)) thumb = Saver.Thumbnail(full, 160, 96); }
            catch (Exception ex) { Log.Error("Miniatuur laden mislukt", ex); }
            ShowShotToast(last, thumb, true, true);
        }

        // De notitie in de naam zetten (op de achtergrond, met nieuwe pogingen), daarna het klembord en de melding bijwerken
        void ApplyNote(Shot entry, string note)
        {
            note = (note ?? "").Trim();
            if (entry.Path == null) { entry.PendingNote = note; return; }
            bool textFile = cfg.NoteTextFile;
            System.Threading.Interlocked.Increment(ref running);
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                Exception fail = null;
                lock (noteGate)
                {
                    try { Notes.Apply(entry, note, textFile); }
                    catch (Exception ex) { fail = ex; }
                }
                System.Threading.Interlocked.Decrement(ref running);
                try { overlay.BeginInvoke((Action)delegate { NoteApplied(entry, note, fail); }); }
                catch (Exception ex) { Log.Error("Terug naar de UI mislukt", ex); }
            });
        }

        void NoteApplied(Shot entry, string note, Exception fail)
        {
            if (fail != null)
            {
                Log.Error("Hernoemen met notitie mislukt", fail);
                Notify("Notitie niet in de naam gezet", "Het bestand was bezet (OneDrive houdt het soms even vast). Je notitie is niet kwijt.",
                    "Opnieuw", "rotate-ccw", delegate { ApplyNote(entry, note); }, true);
                return;
            }
            history.Save();
            // klembord: alleen vervangen als daar nog onze screenshot op staat
            if (entry.Copied && cfg.NoteStrip && W.GetClipboardSequenceNumber() == entry.ClipboardSeq)
            {
                try
                {
                    using (Bitmap img = Saver.Load(entry.Path))
                    {
                        bool ok;
                        if (note.Length > 0) using (Bitmap withNote = Notes.WithStrip(img, note)) ok = Saver.Copy(withNote, null);
                        else ok = Saver.Copy(img, null);
                        if (ok) entry.ClipboardSeq = W.GetClipboardSequenceNumber();
                    }
                }
                catch (Exception ex) { Log.Error("Klembord bijwerken mislukt", ex); }
            }
            ToastContent cur = toast.Content;
            if (cur != null && cur.Tag == entry)
            {
                toast.UpdateNote(note);
                toast.UpdateText(note.Length > 0 ? "Notitie bewaard" : "Notitie weggehaald", Path.GetFileName(entry.Path));
            }
        }

        // Een gewone, goede melding (blauw icoon), bijvoorbeeld "bijgewerkt"
        void Info(string title, string sub, string icon)
        {
            ToastContent c = new ToastContent();
            c.Ok = true; c.Icon = icon; c.Title = title; c.Sub = sub; c.Seconds = 8;
            toast.ShowToast(c, Theme.Current);
        }

        // ---- bijwerken met een klik (zoals in NexusPen)
        public Updates.Info PendingUpdate;
        bool updating;
        public bool Updating { get { return updating; } }

        void UpdateFound(Updates.Info info, bool fromSettings)
        {
            PendingUpdate = info;
            if (settingsForm != null && !settingsForm.IsDisposed) settingsForm.RefreshValues();
            if (fromSettings || overlay.IsActive || overlay.InNoteMode || toast.IsEditing) return;
            ToastContent c = new ToastContent();
            c.Ok = true; c.Icon = "download"; c.Title = "Nieuwe versie: Kapture " + info.Version;
            c.Sub = "Bijwerken duurt een paar seconden. Kapture start daarna vanzelf weer; je instellingen blijven.";
            c.ActionLabel = "Bijwerken"; c.ActionIcon = "download"; c.OnAction = delegate { InstallUpdate(); };
            c.Seconds = 15;
            toast.ShowToast(c, Theme.Current);
        }

        // Nu kijken (vanuit Instellingen). result: 1 = er is iets nieuws, 0 = je hebt de nieuwste, -1 = kon niet kijken
        public void CheckForUpdateNow(Action<int> result)
        {
            Updates.CheckLater(0, delegate(Updates.Info info)
            {
                try { overlay.BeginInvoke((Action)delegate { UpdateFound(info, true); if (result != null) result(1); }); } catch { }
            }, delegate
            {
                try { overlay.BeginInvoke((Action)delegate { if (result != null) result(0); }); } catch { }
            }, delegate
            {
                try { overlay.BeginInvoke((Action)delegate { if (result != null) result(-1); }); } catch { }
            });
        }

        // Downloaden, controleren (sha256 van GitHub), de installer stil laten lopen; die start Kapture daarna weer
        public void InstallUpdate()
        {
            Updates.Info info = PendingUpdate;
            if (info == null || updating) return;
            if (info.SetupUrl == null || info.Sha256 == null)
            {
                // geen controlecode bij deze versie: dan niet zelf installeren, alleen de pagina openen
                if (info.PageUrl != null && info.PageUrl.StartsWith("https://github.com/")) Open(info.PageUrl);
                return;
            }
            updating = true;
            if (settingsForm != null && !settingsForm.IsDisposed && !settingsForm.AskSaveBeforeQuit()) { updating = false; return; }
            IWin32Window owner = settingsForm != null && !settingsForm.IsDisposed && settingsForm.Visible ? (IWin32Window)settingsForm : null;
            if (MessageBox.Show(owner, "Kapture bijwerken naar " + info.Version + "? Kapture sluit even en start daarna vanzelf weer. Je instellingen en screenshots blijven staan.",
                "Kapture", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) { updating = false; return; }
            Info("De nieuwe versie wordt opgehaald...", "Even geduld, dit duurt een paar seconden.", "download");
            Updates.Download(info, delegate(string file)
            {
                try
                {
                    overlay.BeginInvoke((Action)delegate
                    {
                        if (file == null)
                        {
                            updating = false;
                            Notify("Bijwerken lukte niet", "Probeer het later nog eens, of download hem zelf van de downloadpagina.", "Downloadpagina", "external-link", delegate { Open(Updates.Page); }, true);
                            return;
                        }
                        PendingUpdate = null;   // updating blijft aan: geen tweede download of installer
                        // eerst afronden (notitiebalk, opslaan, hernoemen), pas dan de installer: die sluit Kapture hard af
                        Quit(delegate
                        {
                            Updates.WriteFlag(info.Version);   // bij de volgende start melden dat het gelukt is (of niet)
                            // keuzes van de vorige installatie respecteren: starten met Windows en de snelkoppeling niet terugzetten
                            string tasks = "";
                            if (!Autostart.IsOn) tasks += "!autostart";
                            if (!File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Kapture.lnk"))) tasks += (tasks.Length > 0 ? "," : "") + "!desktop";
                            string args = "/SILENT /SUPPRESSMSGBOXES /NORESTART /NOCANCEL /BIJWERKEN" + (tasks.Length > 0 ? " /MERGETASKS=\"" + tasks + "\"" : "");
                            ProcessStartInfo psi = new ProcessStartInfo(file, args);
                            psi.UseShellExecute = true;
                            Process.Start(psi);
                        }, delegate(Exception ex)
                        {
                            updating = false;
                            if (ex == null) { PendingUpdate = info; return; }   // je koos Annuleren in de editor: later bijwerken
                            Log.Error("Installer starten", ex);
                            try { File.Delete(Updates.FlagPath); } catch { }
                            Notify("Bijwerken lukte niet", "Probeer het later nog eens, of download hem zelf van de downloadpagina.", "Downloadpagina", "external-link", delegate { Open(Updates.Page); }, true);
                        });
                    });
                }
                catch (Exception ex) { Log.Error("Bijwerken starten", ex); updating = false; }
            });
        }

        void Notify(string title, string sub, string action, string icon, Action onAction, bool stay)
        {
            ToastContent c = new ToastContent();
            c.Ok = false; c.Icon = "alert"; c.Title = title; c.Sub = sub;
            c.ActionLabel = action; c.ActionIcon = icon; c.OnAction = onAction;
            c.Stay = stay; c.Seconds = 10;
            toast.ShowToast(c, Theme.Current);
        }

        // Na een screenshot het geheugen teruggeven aan Windows (zie docs/SPIKE.md)
        static void Trim()
        {
            try
            {
                GC.Collect(); GC.WaitForPendingFinalizers();
                W.SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, new IntPtr(-1), new IntPtr(-1));
            }
            catch { }
        }

        // ---- instellingen
        void OpenSettings()
        {
            try
            {
                if (settingsForm == null || settingsForm.IsDisposed)
                    settingsForm = new SettingsForm(cfg, this);
                settingsForm.ShowAndFocus();
            }
            catch (Exception ex) { Log.Error("Instellingen openen mislukt", ex); }
        }

        public Settings Config { get { return cfg; } }

        // Komt uit Instellingen na Opslaan (al bewaard)
        public void SettingsChanged()
        {
            Theme.Mode = cfg.ThemeMode;
            RegisterHotkeys(false);
        }

        public bool InboxUsable(out string problem) { problem = InboxFolder.Check(InboxFolder.Resolve(cfg)); return problem == null; }

        // ---- mappen openen
        public void OpenInbox()
        {
            try
            {
                string f = InboxFolder.Resolve(cfg);
                Directory.CreateDirectory(f);
                Process.Start("explorer.exe", "\"" + f + "\"");
            }
            catch (Exception ex) { Log.Error("Inbox openen mislukt", ex); }
        }

        static void ShowInFolder(string path)
        {
            try
            {
                if (path != null && File.Exists(path)) Process.Start("explorer.exe", "/select,\"" + path + "\"");
                else if (path != null) Process.Start("explorer.exe", "\"" + Path.GetDirectoryName(path) + "\"");
            }
            catch (Exception ex) { Log.Error("Map openen mislukt", ex); }
        }

        public static void Open(string target)
        {
            try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
            catch (Exception ex) { Log.Error("Openen mislukt", ex); }
        }

        bool quitting;

        void Quit() { Quit(null, null); }

        // Stoppen. 'last' draait na het afronden maar voor het opruimen (de installer bij bijwerken);
        // mislukt 'last', dan blijft Kapture gewoon draaien en krijgt 'failed' de fout.
        void Quit(Action last, Action<Exception> failed)
        {
            if (quitting) return;   // al bezig met stoppen (bijvoorbeeld twee keer op Afsluiten geklikt)
            quitting = true;
            // open editors: eerst vragen of bewaren (Annuleren = niet stoppen)
            foreach (EditorForm ef in new System.Collections.Generic.List<EditorForm>(editors.Values))
                if (!ef.IsDisposed && !ef.PrepareQuit()) { quitting = false; if (failed != null) failed(null); return; }
            // een open notitiebalk of notitie in de melding afronden, en wachten tot opslaan en hernoemen klaar zijn (hooguit 5 seconden)
            if (overlay.InNoteMode) overlay.FinishNoteNow();
            if (toast.IsEditing) toast.Dismiss();
            Stopwatch wait = Stopwatch.StartNew();
            while (System.Threading.Thread.VolatileRead(ref running) > 0 && wait.ElapsedMilliseconds < 5000)
            {
                Application.DoEvents();
                System.Threading.Thread.Sleep(30);
            }
            if (last != null)
            {
                try { last(); }
                catch (Exception ex) { quitting = false; if (failed != null) failed(ex); return; }
            }
            foreach (int id in new int[] { HotkeyCapture, HotkeyNote, HotkeyLastNote, HotkeyCustom }) W.UnregisterHotKey(hot.Handle, id);
            tray.Visible = false;
            tray.Dispose();
            SafeReset();
            if (settingsForm != null && !settingsForm.IsDisposed) settingsForm.CloseForQuit();
            foreach (EditorForm ef in new System.Collections.Generic.List<EditorForm>(editors.Values)) if (!ef.IsDisposed) ef.Close();
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { try { tray.Dispose(); } catch { } }
            base.Dispose(disposing);
        }
    }

    // ------------------------------------------------------------------ Waakhond: het donkere scherm mag nooit blijven hangen
    // Een tikker op de UI-draad houdt een hartslag bij. Staat de overlay open en blijft de hartslag 3 seconden weg
    // (Kapture zit vast), dan verbergt een aparte draad de overlay en het donkere venster.
    static class Watchdog
    {
        static readonly System.Windows.Forms.Timer beat = new System.Windows.Forms.Timer { Interval = 250 };
        static System.Threading.Timer guard;
        static long last;
        static IntPtr ov, dimH;
        static volatile bool armed;
        static bool hooked;

        public static void Arm(IntPtr overlay, IntPtr dim)
        {
            ov = overlay; dimH = dim;
            System.Threading.Interlocked.Exchange(ref last, DateTime.UtcNow.Ticks);
            if (!hooked) { beat.Tick += delegate { System.Threading.Interlocked.Exchange(ref last, DateTime.UtcNow.Ticks); }; hooked = true; }
            beat.Start();
            if (guard == null) guard = new System.Threading.Timer(Check, null, 500, 500);
            armed = true;
        }

        public static void Disarm() { armed = false; beat.Stop(); }

        static void Check(object state)
        {
            if (!armed) return;
            long ago = DateTime.UtcNow.Ticks - System.Threading.Interlocked.Read(ref last);
            if (ago < TimeSpan.FromSeconds(3).Ticks) return;
            armed = false;
            W.ShowWindowAsync(ov, W.SW_HIDE);
            W.ShowWindowAsync(dimH, W.SW_HIDE);
            Log.Info("Waakhond: overlay verborgen omdat Kapture niet reageerde");
        }
    }
}
