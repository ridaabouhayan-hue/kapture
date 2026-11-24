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
        public const int HotkeyCapture = 1, HotkeyNote = 2, HotkeyLastNote = 3;

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
            RegisterHotkeys(true);
            CheckSnippingSetting();
            string inboxProblem = InboxFolder.Check(InboxFolder.Resolve(cfg));
            if (inboxProblem != null)
                Notify("Inbox-map niet bereikbaar", inboxProblem + " Kies een andere map in Instellingen.", "Instellingen", "settings", delegate { OpenSettings(); }, true);
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
            foreach (int id in new int[] { HotkeyCapture, HotkeyNote, HotkeyLastNote }) W.UnregisterHotKey(hot.Handle, id);
            HotkeyTaken.Clear();
            if (!cfg.HotkeysPaused)
            {
                System.Collections.Generic.List<string> bad = new System.Collections.Generic.List<string>();
                if (cfg.HotkeyCapture && !W.RegisterHotKey(hot.Handle, HotkeyCapture, W.MOD_NOREPEAT, W.VK_SNAPSHOT)) { HotkeyTaken[HotkeyCapture] = true; bad.Add("Print Screen"); }
                if (cfg.HotkeyNote && !W.RegisterHotKey(hot.Handle, HotkeyNote, W.MOD_NOREPEAT | W.MOD_SHIFT, W.VK_SNAPSHOT)) { HotkeyTaken[HotkeyNote] = true; bad.Add("Shift+Print Screen"); }
                if (cfg.HotkeyLastNote && !W.RegisterHotKey(hot.Handle, HotkeyLastNote, W.MOD_NOREPEAT | W.MOD_CONTROL, W.VK_SNAPSHOT)) { HotkeyTaken[HotkeyLastNote] = true; bad.Add("Ctrl+Print Screen"); }
                if (bad.Count > 0)
                {
                    Log.Info(bad.Count + " sneltoets(en) konden niet vastgelegd worden");
                    if (tell) Notify(string.Join(", ", bad.ToArray()) + (bad.Count == 1 ? " is bezet" : " zijn bezet"),
                        "Een ander programma gebruikt deze toets, bijvoorbeeld ShareX. Sluit dat en klik op Opnieuw.", "Opnieuw", "rotate-ccw", delegate { RegisterHotkeys(true); }, true);
                }
            }
            UpdateTrayText();
        }

        void TogglePause()
        {
            cfg.HotkeysPaused = !cfg.HotkeysPaused;
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

        void CheckSnippingSetting()
        {
            if (cfg.SnipHintShown || !SnippingUsesPrintScreen()) return;
            Notify("Zet Print Screen vrij voor Kapture", "Windows opent nu het Knipprogramma met Print Screen. Zet in de instelling \"Print Screen gebruiken om het Knipprogramma te openen\" uit.",
                "Open instelling", "settings-2", delegate
                {
                    OpenSnippingSetting();
                    cfg.SnipHintShown = true;   // pas als gezien markeren als Rida de instelling echt opende
                    cfg.Save();
                }, true);
        }

        void OnHotkey(int id)
        {
            if (id == HotkeyCapture) StartCapture(false);
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
            c.ActionLabel = "Map openen"; c.ActionIcon = "folder-open";
            c.OnAction = delegate { ShowInFolder(entry.Path); };
            c.OnThumb = delegate { Open(entry.Path); };
            c.Seconds = cfg.ToastSeconds;
            c.NoteField = true; c.Note = note; c.Tag = entry;
            c.OnNote = delegate(string n) { ApplyNote(entry, n); };
            toast.ShowToast(c, Theme.Current);
            if (edit) toast.BeginEdit();
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

        // Komt uit Instellingen na elke wijziging (al bewaard)
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

        void Quit()
        {
            // een open notitiebalk afronden, en wachten tot opslaan en hernoemen klaar zijn (hooguit 5 seconden)
            if (overlay.InNoteMode) overlay.FinishNoteNow();
            Stopwatch wait = Stopwatch.StartNew();
            while (System.Threading.Thread.VolatileRead(ref running) > 0 && wait.ElapsedMilliseconds < 5000)
            {
                Application.DoEvents();
                System.Threading.Thread.Sleep(30);
            }
            foreach (int id in new int[] { HotkeyCapture, HotkeyNote, HotkeyLastNote }) W.UnregisterHotKey(hot.Handle, id);
            if (toast.IsEditing) toast.Dismiss();
            tray.Visible = false;
            tray.Dispose();
            SafeReset();
            if (settingsForm != null && !settingsForm.IsDisposed) settingsForm.Close();
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
