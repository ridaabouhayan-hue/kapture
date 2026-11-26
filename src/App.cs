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
        public Action OnClipboard;
        public static readonly int ShowMessage = W.RegisterWindowMessage("Kapture.AlGestart");

        public HotkeyWindow() { CreateHandle(new CreateParams()); }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == W.WM_HOTKEY && OnHotkey != null) { OnHotkey(m.WParam.ToInt32()); return; }
            if (m.Msg == W.WM_CLIPBOARDUPDATE && OnClipboard != null) { OnClipboard(); return; }
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
            hot.OnClipboard = OnClipboardChanged;
            if (!W.AddClipboardFormatListener(hot.Handle)) Log.Info("Klembord volgen lukte niet");
            overlay.Selected += OnSelected;
            overlay.Cancelled += OnCancelled;
            overlay.NoteDone += OnNoteDone;
            overlay.PresetChanged += delegate(int p) { cfg.Preset = p; cfg.Save(); };   // het menu leest hem bij het openen
            overlay.StepsFor = delegate(int p) { return Presets.StepsOf(cfg, p); };
            Presets.CleanTemp();
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
            m.AddHeader("Kapture", Presets.Chips[Presets.Clamp(c.Preset)]);
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
            Log.Trace("Sneltoetsen: " + (cfg.HotkeysPaused ? "op pauze" : "PrtSc " + State(cfg.HotkeyCapture, HotkeyCapture) + ", Shift+PrtSc " + State(cfg.HotkeyNote, HotkeyNote)
                + ", Ctrl+PrtSc " + State(cfg.HotkeyLastNote, HotkeyLastNote) + ", eigen " + (cfg.CustomKey == 0 ? "geen" : State(true, HotkeyCustom)))
                + "; Knipprogramma met PrtSc " + (SnippingUsesPrintScreen() ? "aan" : "uit"));
            UpdateTrayText();
            if (HotkeyTaken.Count > 0) retry.Start(); else retry.Stop();
        }

        string State(bool on, int id) { return !on ? "uit" : HotkeyTaken.ContainsKey(id) ? "bezet" : "ok"; }

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

        // Sommige Windows-versies geven Print Screen niet door aan Kapture (ook al is hij vastgelegd), maar kopiëren dan
        // zelf het hele scherm naar het klembord. Dat merken we hier: een afbeelding precies zo groot als het scherm,
        // van Windows zelf (geen eigenaar) of terwijl Print Screen net ingedrukt was. We kijken alleen naar de maat,
        // nooit naar wat erop staat. Geen toetsenbordhook: Windows meldt alleen dat het klembord veranderde.
        DateTime lastPrtSc = DateTime.MinValue;

        static readonly uint ownPid = (uint)Process.GetCurrentProcess().Id;

        void OnClipboardChanged()
        {
            try
            {
                // Toetsen bij elke wijziging uitlezen: zo blijft het "net ingedrukt" van een oudere toetsdruk niet hangen
                bool prt = (W.GetAsyncKeyState(W.VK_SNAPSHOT) & 0x8001) != 0;
                bool win = (W.GetAsyncKeyState(0x5B) & 0x8001) != 0 || (W.GetAsyncKeyState(0x5C) & 0x8001) != 0;
                bool alt = (W.GetAsyncKeyState(0x12) & 0x8001) != 0;
                bool shift = (W.GetAsyncKeyState(0x10) & 0x8001) != 0;
                if (!cfg.HotkeyCapture || cfg.HotkeysPaused) return;
                if ((DateTime.UtcNow - lastPrtSc).TotalSeconds < 3) return;   // de sneltoets kwam zelf al binnen
                IntPtr owner = W.GetClipboardOwner();
                uint pid = 0;
                if (owner != IntPtr.Zero) W.GetWindowThreadProcessId(owner, out pid);
                if (pid != 0 && pid == ownPid) return;   // Kapture zette het er zelf op
                bool fromWindows = owner == IntPtr.Zero;
                // Eerst de goedkope controles: de afbeelding opvragen laat het andere programma hem soms pas maken (traag)
                if (!(fromWindows || prt)) return;
                Size size;
                if (!ClipboardBitmapSize(out size)) return;
                bool screenSize = size == VirtualScreen.Bounds.Size;
                foreach (Screen sc in Screen.AllScreens) if (size == sc.Bounds.Size) screenSize = true;
                Log.Trace("Klembord: afbeelding " + (screenSize ? "zo groot als het scherm" : "met een andere maat") + ", van "
                    + (fromWindows ? "Windows zelf" : "een programma") + ", PrtSc " + (prt ? "net ingedrukt" : "niet ingedrukt")
                    + (win ? ", met Windows-toets" : "") + (alt ? ", met Alt" : ""));
                if (!screenSize) return;
                if (win || alt) return;   // Windows-toets + PrtSc of Alt + PrtSc: dat is van Windows zelf, niet voor Kapture
                lastPrtSc = DateTime.UtcNow;
                if (shift && cfg.HotkeyNote) StartCapture(true); else StartCapture(false);
            }
            catch (Exception ex) { Log.Error("Klembord bekijken mislukt", ex); }
        }

        // Alleen de maat van de afbeelding op het klembord (als die er is)
        bool ClipboardBitmapSize(out Size size)
        {
            size = Size.Empty;
            if (!W.IsClipboardFormatAvailable(W.CF_BITMAP)) return false;
            bool open = false;
            for (int i = 0; i < 5 && !open; i++) { open = W.OpenClipboard(hot.Handle); if (!open) System.Threading.Thread.Sleep(15); }
            if (!open) return false;
            try
            {
                IntPtr h = W.GetClipboardData(W.CF_BITMAP);
                W.BITMAP bm;
                if (h == IntPtr.Zero || W.GetObject(h, System.Runtime.InteropServices.Marshal.SizeOf(typeof(W.BITMAP)), out bm) == 0) return false;
                size = new Size(bm.bmWidth, Math.Abs(bm.bmHeight));
                return true;
            }
            finally { W.CloseClipboard(); }
        }

        void OnHotkey(int id)
        {
            if (id == HotkeyCapture || id == HotkeyNote) lastPrtSc = DateTime.UtcNow;
            Log.Trace("Sneltoets binnen: " + (id == HotkeyCapture ? "PrtSc" : id == HotkeyNote ? "Shift+PrtSc" : id == HotkeyLastNote ? "Ctrl+PrtSc" : id == HotkeyCustom ? "eigen sneltoets" : "onbekend"));
            if (id == HotkeyCapture || id == HotkeyCustom) StartCapture(false);
            else if (id == HotkeyNote) StartCapture(true);
            else if (id == HotkeyLastNote) NoteForLast();
        }

        // ---- een screenshot: donker, vastleggen, kader, opslaan
        public void StartCapture(bool withNote)
        {
            if (busy || overlay.IsActive || overlay.InNoteMode)
            {
                Log.Trace("Screenshot niet gestart: " + (overlay.InNoteMode ? "notitiebalk staat nog open" : overlay.IsActive ? "donker scherm staat al open" : "Kapture is nog bezig met de vorige"));
                return;
            }
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
                overlay.Preset = cfg.Preset;
                if (!overlay.Prepare(shot, v, Theme.Current, cur, mon, withNote))
                {
                    FreeShot(); dim.HideNow(); busy = false;
                    Notify("Screenshot lukte niet", "Er was niet genoeg geheugen vrij. Sluit een paar programma's en probeer het nog eens.", null, null, null, false);
                    return;
                }
                overlay.ShowNow();
                dim.HideNow();
                Log.Trace("Donker scherm open; vooraan staat " + (W.GetForegroundWindow() == overlay.Handle ? "Kapture" : Overlay.ForegroundName()));
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
                Point[] lasso = overlay.TakeLasso();
                if (lasso != null && piece != null) piece = OnlyInside(piece, lasso, r);
                int preset = overlay.Preset;
                int steps = Presets.StepsOf(cfg, preset);
                bool allowNote = (steps & Presets.StepNote) != 0;
                // notitiebalk alleen als de preset een notitie toestaat; bij Opslaan in... komt eerst de mapkeuze
                bool note = overlay.WithNote && piece != null && allowNote && preset != Presets.SaveIn;
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
                Shot entry = new Shot { Time = DateTime.Now, App = shotApp ?? "", Preset = Presets.Names[preset], NoNote = !allowNote };
                if (preset == Presets.SaveIn)
                {
                    ChooseFolder(delegate(string chosen) { SaveShot(piece, entry, chosen, steps); }, delegate { piece.Dispose(); });
                    return;
                }
                string folder = preset == Presets.ChooseLater ? Presets.TempFolder : InboxFolder.ForShot(cfg, entry.Time);
                if (note)
                {
                    noteShot = entry; noteResult = null;
                    string paste = clipText; clipText = null;
                    overlay.BeginNote(r, delegate(string n) { return Naming.Base(entry.Time, entry.App, n) + ".png"; }, paste);
                }
                SaveShot(piece, entry, folder, steps);
            }
            catch (Exception ex)
            {
                Log.Error("Kader verwerken mislukt", ex);
                SafeReset();
            }
        }

        // Opslaan en (volgens de stappen van de preset) kopiëren; daarna OnSaved
        void SaveShot(Bitmap piece, Shot entry, string folder, int steps)
        {
            System.Threading.Interlocked.Increment(ref running);
            Saver.SaveAndCopy(piece, entry, folder, (steps & Presets.StepCopyImage) != 0, (steps & Presets.StepCopyFile) != 0,
                cfg.NoteStrip, cfg.NoteTextFile, overlay, delegate(SaveResult sr)
            {
                System.Threading.Interlocked.Decrement(ref running);
                OnSaved(sr);
            });
        }

        // ---- Opslaan in...: een keuzemenu bij de muis met de inbox, de laatste mappen en "Andere map kiezen"
        readonly TrayMenu folderMenu = new TrayMenu();

        void ChooseFolder(Action<string> chosen, Action cancelled)
        {
            bool done = false;
            Action<string> pick = delegate(string f)
            {
                if (done) return;
                done = true;
                RememberFolder(f);
                chosen(f);
            };
            folderMenu.Clear();
            folderMenu.AddHeader("Opslaan in", null);
            string inbox = InboxFolder.ForShot(cfg, DateTime.Now);
            folderMenu.Add("Inbox", "folder", delegate { pick(inbox); });
            foreach (string f in RecentFolders())
            {
                string path = f;
                if (string.Equals(path.TrimEnd('\\'), inbox.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) continue;
                folderMenu.Add(FolderLabel(path), "folder", delegate { pick(path); });
            }
            folderMenu.AddSeparator();
            folderMenu.Add("Andere map kiezen...", "folder-open", delegate
            {
                done = true;   // het menu is dicht, maar de keuze komt nog
                string f = null;
                using (FolderBrowserDialog d = new FolderBrowserDialog())
                {
                    d.Description = "Waar wil je deze screenshot bewaren?";
                    d.ShowNewFolderButton = true;
                    if (d.ShowDialog() == DialogResult.OK) f = d.SelectedPath;
                }
                if (f != null) { RememberFolder(f); chosen(f); } else cancelled();
            });
            folderMenu.Dismissed = delegate { if (!done) { done = true; cancelled(); } };
            folderMenu.ShowAt(Cursor.Position);
        }

        System.Collections.Generic.List<string> RecentFolders()
        {
            System.Collections.Generic.List<string> list = new System.Collections.Generic.List<string>();
            foreach (string f in (cfg.RecentFolders ?? "").Split('|'))
                if (f.Length > 0 && Directory.Exists(f) && list.Count < 5) list.Add(f);
            return list;
        }

        void RememberFolder(string folder)
        {
            System.Collections.Generic.List<string> list = new System.Collections.Generic.List<string>();
            list.Add(folder);
            foreach (string f in RecentFolders()) if (!string.Equals(f, folder, StringComparison.OrdinalIgnoreCase) && list.Count < 5) list.Add(f);
            cfg.RecentFolders = string.Join("|", list.ToArray());
            cfg.Save();
        }

        // "Documenten  /  Cursus" in plaats van het hele pad
        static string FolderLabel(string path)
        {
            string name = Path.GetFileName(path.TrimEnd('\\'));
            string parent = Path.GetFileName(Path.GetDirectoryName(path.TrimEnd('\\')) ?? "");
            if (string.IsNullOrEmpty(name)) return path;
            return string.IsNullOrEmpty(parent) ? name : parent + "  /  " + name;
        }

        // Vrij: alleen wat binnen de getekende vorm valt; daarbuiten wit (wit plakt overal netjes, ook waar
        // doorzichtig zwart zou worden)
        static Bitmap OnlyInside(Bitmap piece, Point[] lasso, Rectangle r)
        {
            Bitmap result = new Bitmap(piece.Width, piece.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            try
            {
                using (Graphics g = Graphics.FromImage(result))
                using (GraphicsPath path = new GraphicsPath(FillMode.Winding))   // dubbel omcirkeld telt ook als binnen
                {
                    g.Clear(Color.White);
                    Point[] pts = new Point[lasso.Length];
                    for (int i = 0; i < lasso.Length; i++) pts[i] = new Point(lasso[i].X - r.X, lasso[i].Y - r.Y);
                    path.AddPolygon(pts);
                    g.SetClip(path);
                    g.DrawImage(piece, new Rectangle(0, 0, piece.Width, piece.Height), 0, 0, piece.Width, piece.Height, GraphicsUnit.Pixel);
                }
            }
            catch { result.Dispose(); return piece; }   // lukt het niet, dan het hele kader: beter dan niets
            piece.Dispose();
            return result;
        }

        void OnSaved(SaveResult r)
        {
            Trim();
            if (!r.Failed) history.Add(r.Shot);
            if (noteShot == r.Shot && overlay.InNoteMode) { noteResult = r; return; }   // de melding komt na de notitie
            int p = Presets.IndexOf(r.Shot.Preset);
            if (!r.Failed && Presets.Has(cfg, p, Presets.StepEditor)) OpenEditor(r.Shot);   // bijvoorbeeld Doorsturen
            if (!r.Failed && !Presets.Has(cfg, p, Presets.StepToast))
            {
                if (r.Thumb != null) r.Thumb.Dispose();
                return;
            }
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
            int p = Presets.IndexOf(entry.Preset);
            bool wantCopy = Presets.Has(cfg, p, Presets.StepCopyImage) || Presets.Has(cfg, p, Presets.StepCopyFile);
            c.Sub = note.Length > 0 ? Naming.Base(entry.Time, entry.App, note) + ".png"
                : (copied || edit || !wantCopy ? "Preset: " + entry.Preset : "Kopieren lukte niet; het bestand staat wel in je inbox.");
            c.ActionLabel = "Bewerken"; c.ActionIcon = "pencil";
            c.OnAction = delegate { OpenEditor(entry, c.Note); };   // c.Note: net getypt in de melding
            c.Action2Label = "Map openen"; c.Action2Icon = "folder-open";
            c.OnAction2 = delegate { ShowInFolder(entry.Path); };
            c.OnThumb = delegate { if (cfg.ThumbOpensEditor) OpenEditor(entry, c.Note); else Open(entry.Path); };
            c.Seconds = cfg.ToastSeconds;
            c.NoteField = !entry.NoNote; c.Note = note; c.Tag = entry;
            if (Presets.IsTemp(entry.Path))
            {
                // Kies zelf: nog niet bewaard; jij kiest
                c.Title = "Nog niet bewaard";
                c.Sub = "Kies Bewaren of Kopiëren. Doe je niets, dan ruimt Kapture hem later op.";
                c.ActionLabel = "Bewaren"; c.ActionIcon = "save";
                c.OnAction = delegate { KeepShot(entry, c.Note); };
                c.Action2Label = "Kopiëren"; c.Action2Icon = "copy";
                c.OnAction2 = delegate { CopyShot(entry, c.Note); };
                c.Seconds = Math.Max(cfg.ToastSeconds, 12);   // even langer: je moet kunnen kiezen
            }
            c.OnNote = delegate(string n) { ApplyNote(entry, n); };
            c.OnDelete = delegate { DeleteShot(entry); };
            toast.ShowToast(c, Theme.Current);
            if (edit) toast.BeginEdit();
        }

        // Kies zelf: Bewaren zet de screenshot (en zijn notitiebestand) van de tijdelijke map in de inbox
        void KeepShot(Shot entry, string typed)
        {
            bool ok = true;
            lock (noteGate)
            {
                try
                {
                    string png = entry.Path;
                    if (png == null || !File.Exists(png)) ok = false;
                    else
                    {
                        string folder = InboxFolder.ForShot(cfg, entry.Time);
                        Directory.CreateDirectory(folder);
                        string target = Naming.Unique(folder, Path.GetFileNameWithoutExtension(png), ".png");
                        File.Move(png, target);
                        string txt = Notes.TextPath(png);
                        if (File.Exists(txt)) File.Move(txt, Notes.TextPath(target));
                        entry.Path = target;
                    }
                }
                catch (Exception ex) { ok = false; Log.Error("Bewaren mislukt", ex); }
            }
            if (!ok) { Notify("Bewaren lukte niet", "De inbox-map was niet bereikbaar. Staat OneDrive aan?", null, null, null, false); return; }
            history.Save();   // een net getypte notitie zet de melding zelf al in de naam (onder hetzelfde slot)
            Info("Bewaard in je inbox", Path.GetFileName(entry.Path), "check");
        }

        // Kies zelf: Kopiëren zet de afbeelding op het klembord (met de notitiestrook als er een notitie is)
        void CopyShot(Shot entry, string typed)
        {
            try
            {
                string path;
                lock (noteGate) path = entry.Path;
                using (Bitmap img = Saver.Load(path))
                {
                    string n = entry.NoNote ? "" : (!string.IsNullOrEmpty(typed) ? typed.Trim() : entry.Note ?? "");
                    bool ok;
                    if (cfg.NoteStrip && n.Length > 0) using (Bitmap withNote = Notes.WithStrip(img, n)) ok = Saver.Copy(withNote, null);
                    else ok = Saver.Copy(img, null);
                    if (ok) { entry.Copied = true; entry.CopiedFile = false; entry.ClipboardSeq = W.GetClipboardSequenceNumber(); }
                    if (!ok) Notify("Kopiëren lukte niet", "Een ander programma hield het klembord vast. Probeer het nog eens.", null, null, null, false);
                }
            }
            catch (Exception ex) { Log.Error("Kopiëren mislukt", ex); }
        }

        // Prullenbak-knop in de melding: de screenshot (en zijn notitiebestand) naar de prullenbak. Terug te halen
        // via de Prullenbak van Windows. Het klembord blijft zoals het is.
        void DeleteShot(Shot entry)
        {
            EditorForm open;
            if (editors.TryGetValue(entry, out open) && !open.IsDisposed)
            {
                Notify("Eerst de editor sluiten", "Deze screenshot staat nog open in de editor.", null, null, null, false);
                return;
            }
            bool ok = true;
            lock (noteGate)   // nooit tijdens hernoemen met een notitie
            {
                try
                {
                    string png = entry.Path;
                    if (png != null && File.Exists(png)) ToRecycleBin(png);
                    string txt = png != null ? Notes.TextPath(png) : null;
                    if (txt != null && File.Exists(txt)) ToRecycleBin(txt);
                }
                catch (Exception ex) { ok = false; Log.Error("Verwijderen mislukt", ex); }
            }
            if (!ok) { Notify("Verwijderen lukte niet", "Het bestand is misschien open in een ander programma.", null, null, null, false); return; }
            history.Remove(entry);
            Info("Screenshot verwijderd", "Hij staat in de Prullenbak, als je hem toch nog nodig hebt.", "trash-2");
        }

        static void ToRecycleBin(string path)
        {
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin, Microsoft.VisualBasic.FileIO.UICancelOption.ThrowException);
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
                    if (entry.NoNote) n = null;   // preset zonder notitie (Doorsturen): geen notitiestrook
                    if (cfg.NoteStrip && !string.IsNullOrEmpty(n)) using (Bitmap withNote = Notes.WithStrip(b, n)) ok = Saver.Copy(withNote, null);
                    else ok = Saver.Copy(b, null);
                    // alleen als het klembord precies het bestand is, mag een latere notitie het klembord bijwerken
                    if (ok) { entry.Copied = sameAsFile; entry.ClipboardSeq = W.GetClipboardSequenceNumber(); }
                    return ok;
                };
                h.NoteChanged = delegate(string changed) { ApplyNote(entry, changed); };
                h.NoNote = entry.NoNote;
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
            if (entry.NoNote) return;   // preset zonder notitie (Doorsturen): nooit een notitie
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
            // (de afbeelding krijgt de notitiestrook; het bestand op het klembord moet naar de nieuwe naam wijzen)
            if (((entry.Copied && cfg.NoteStrip) || entry.CopiedFile) && W.GetClipboardSequenceNumber() == entry.ClipboardSeq)
            {
                try
                {
                    using (Bitmap img = entry.Copied ? Saver.Load(entry.Path) : null)
                    {
                        bool ok;
                        string file = entry.CopiedFile ? entry.Path : null;
                        if (img != null && cfg.NoteStrip && note.Length > 0) using (Bitmap withNote = Notes.WithStrip(img, note)) ok = Saver.Copy(withNote, null, file);
                        else ok = Saver.Copy(img, null, file);
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
