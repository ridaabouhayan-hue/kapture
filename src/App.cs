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

    // ------------------------------------------------------------------ Kapture zelf: tray-icoon, sneltoets, de stappen van een screenshot
    class App : ApplicationContext
    {
        const int HotkeyCapture = 1;

        readonly Settings cfg;
        readonly HotkeyWindow hot = new HotkeyWindow();
        readonly NotifyIcon tray = new NotifyIcon();
        readonly DimWindow dim = new DimWindow();
        readonly Overlay overlay = new Overlay();
        readonly Toast toast = new Toast();
        readonly ICapture capture = new GdiCapture();
        ToolStripMenuItem pauseItem;
        Dib shot;                 // het vastgelegde scherm, alleen zolang de overlay open is
        Rectangle shotOrigin;
        string shotApp = "";
        bool busy;
        string lastPath;

        public App()
        {
            cfg = Settings.Load();
            hot.OnHotkey = OnHotkey;
            hot.OnShowRequest = delegate { Notify("Kapture draait al", "Je vindt hem bij de klok. Druk op Print Screen voor een screenshot.", null, null, null, false); };
            overlay.Selected += OnSelected;
            overlay.Cancelled += OnCancelled;
            BuildTray();
            if (!cfg.HotkeysPaused) RegisterHotkeys(true);
            CheckSnippingSetting();
            Log.Info("Gestart " + Program.Version + " op Windows " + Environment.OSVersion.Version);
        }

        // ---- tray
        void BuildTray()
        {
            tray.Text = "Kapture";
            tray.Icon = TrayIcon();
            ContextMenuStrip m = new ContextMenuStrip();
            m.Renderer = new MenuRenderer();
            m.ShowImageMargin = true;
            m.Font = Fonts.Ui(12 * W.ScaleAt(Cursor.Position.X, Cursor.Position.Y), 0);
            ToolStripMenuItem shotItem = Item(m, "Nieuwe screenshot", "camera", delegate { StartCapture(); });
            shotItem.ShortcutKeyDisplayString = "PrtSc";
            m.Items.Add(new ToolStripSeparator());
            Item(m, "Inbox-map openen", "folder-open", delegate { OpenInbox(); });
            pauseItem = Item(m, "Sneltoetsen pauzeren", "pause", delegate { TogglePause(); });
            pauseItem.Checked = cfg.HotkeysPaused;
            m.Items.Add(new ToolStripSeparator());
            Item(m, "Kapture afsluiten", "power", delegate { Quit(); });
            m.Opening += delegate { W.RoundCorners(m.Handle); };
            tray.ContextMenuStrip = m;
            tray.MouseUp += delegate(object o, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                // linksklikken opent hetzelfde menu (zoals in het Stitch-ontwerp)
                System.Reflection.MethodInfo mi = typeof(NotifyIcon).GetMethod("ShowContextMenu", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (mi != null) mi.Invoke(tray, null);
            };
            tray.Visible = true;
        }

        ToolStripMenuItem Item(ContextMenuStrip m, string text, string icon, EventHandler click)
        {
            ToolStripMenuItem it = new ToolStripMenuItem(text);
            it.Click += click;
            it.Tag = icon;
            float sc = W.ScaleAt(Cursor.Position.X, Cursor.Position.Y);
            it.Padding = new Padding((int)(4 * sc), (int)(7 * sc), (int)(8 * sc), (int)(7 * sc));
            m.Items.Add(it);
            return it;
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
        bool RegisterHotkeys(bool tell)
        {
            W.UnregisterHotKey(hot.Handle, HotkeyCapture);
            bool ok = W.RegisterHotKey(hot.Handle, HotkeyCapture, W.MOD_NOREPEAT, W.VK_SNAPSHOT);
            if (!ok)
            {
                Log.Info("Print Screen kon niet vastgelegd worden");
                if (tell) Notify("Print Screen is bezet", "Een ander programma gebruikt Print Screen, bijvoorbeeld ShareX. Sluit dat en klik op Opnieuw.", "Opnieuw", "camera", delegate { RegisterHotkeys(true); }, true);
            }
            UpdateTrayText();
            return ok;
        }

        void TogglePause()
        {
            cfg.HotkeysPaused = !cfg.HotkeysPaused;
            if (cfg.HotkeysPaused) W.UnregisterHotKey(hot.Handle, HotkeyCapture); else RegisterHotkeys(true);
            pauseItem.Checked = cfg.HotkeysPaused;
            cfg.Save();
            UpdateTrayText();
        }

        void UpdateTrayText() { tray.Text = cfg.HotkeysPaused ? "Kapture (sneltoetsen op pauze)" : "Kapture"; }

        // Windows 11 opent standaard het Knipprogramma met Print Screen. Eén keer melden hoe je dat uitzet.
        void CheckSnippingSetting()
        {
            if (cfg.SnipHintShown) return;
            object v = null;
            try { v = Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Keyboard", "PrintScreenKeyForSnippingEnabled", null); } catch { }
            if (v is int && (int)v == 0) return;
            Notify("Zet Print Screen vrij voor Kapture", "Windows opent nu het Knipprogramma met Print Screen. Zet in de instelling \"Print Screen gebruiken om het Knipprogramma te openen\" uit.",
                "Open instelling", "settings-2", delegate { Open("ms-settings:easeofaccess-keyboard"); }, true);
            cfg.SnipHintShown = true;
            cfg.Save();
        }

        void OnHotkey(int id)
        {
            if (id == HotkeyCapture) StartCapture();
        }

        // ---- een screenshot: donker, vastleggen, kader, opslaan
        public void StartCapture()
        {
            if (busy || overlay.IsActive) return;
            busy = true;
            try
            {
                toast.Dismiss();
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
                if (!overlay.Prepare(shot, v, Theme.Current, cur, mon))
                {
                    FreeShot(); dim.HideNow(); busy = false;
                    Notify("Screenshot lukte niet", "Er was niet genoeg geheugen vrij. Sluit een paar programma's en probeer het nog eens.", null, null, null, false);
                    return;
                }
                overlay.ShowNow();
                dim.HideNow();
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
                overlay.End();
                FreeShot();
                busy = false;
                if (piece == null) return;
                string folder = InboxFolder.Resolve(cfg);
                Saver.SaveAndCopy(piece, folder, shotApp, overlay, ShowSaved);
            }
            catch (Exception ex)
            {
                Log.Error("Kader verwerken mislukt", ex);
                SafeReset();
            }
        }

        void OnCancelled()
        {
            overlay.End();
            FreeShot();
            busy = false;
            Trim();
        }

        void SafeReset()
        {
            try { overlay.End(); } catch { }
            try { dim.HideNow(); } catch { }
            FreeShot();
            busy = false;
        }

        void FreeShot() { if (shot != null) { shot.Dispose(); shot = null; } }

        void ShowSaved(SaveResult r)
        {
            Trim();
            if (r.Failed)
            {
                Notify(r.Copied ? "Gekopieerd, niet opgeslagen" : "Opslaan lukte niet", "De inbox-map was niet bereikbaar. Staat OneDrive aan?", "Map openen", "folder-open", delegate { OpenInbox(); }, true);
                return;
            }
            lastPath = r.Path;
            string path = r.Path;
            ToastContent c = new ToastContent();
            c.Thumb = r.Thumb;
            c.Title = r.Copied ? "Opgeslagen en gekopieerd" : "Opgeslagen";
            c.Sub = r.Copied ? "Preset: Naar Claude" : "Kopieren lukte niet; het bestand staat wel in je inbox.";
            c.ActionLabel = "Map openen"; c.ActionIcon = "folder-open";
            c.OnAction = delegate { ShowInFolder(path); };
            c.OnThumb = delegate { Open(path); };
            c.Seconds = cfg.ToastSeconds;
            toast.ShowToast(c, Theme.Current);
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

        // ---- mappen openen
        void OpenInbox()
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
            try { Process.Start("explorer.exe", "/select,\"" + path + "\""); }
            catch (Exception ex) { Log.Error("Map openen mislukt", ex); }
        }

        static void Open(string target)
        {
            try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
            catch (Exception ex) { Log.Error("Openen mislukt", ex); }
        }

        void Quit()
        {
            W.UnregisterHotKey(hot.Handle, HotkeyCapture);
            tray.Visible = false;
            tray.Dispose();
            SafeReset();
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { try { tray.Dispose(); } catch { } }
            base.Dispose(disposing);
        }
    }

    // ------------------------------------------------------------------ Het tray-menu in de Kapture-kleuren (Stitch-scherm 7)
    class MenuRenderer : ToolStripProfessionalRenderer
    {
        Theme T { get { return Theme.Current; } }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (SolidBrush b = new SolidBrush(T.Surface)) e.Graphics.FillRectangle(b, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using (Pen p = new Pen(T.OutlineVariant)) e.Graphics.DrawRectangle(p, 0, 0, e.AffectedBounds.Width - 1, e.AffectedBounds.Height - 1);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected) return;
            Rectangle r = new Rectangle(4, 1, e.Item.Width - 8, e.Item.Height - 2);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath gp = Svg.RoundRect(r, 6))
            using (SolidBrush b = new SolidBrush(T.PrimaryContainer)) e.Graphics.FillPath(b, gp);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            DrawIcon(e.Graphics, e.Item, T);
            e.TextColor = e.Item.Selected ? T.OnPrimaryContainer : (e.Item is ToolStripMenuItem && ((ToolStripMenuItem)e.Item).ShortcutKeyDisplayString == e.Text ? T.Outline : T.OnSurface);
            base.OnRenderItemText(e);
        }

        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e) { }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e) { }

        protected override void OnRenderToolStripPanelBackground(ToolStripPanelRenderEventArgs e) { }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using (Pen p = new Pen(T.OutlineVariant)) e.Graphics.DrawLine(p, 8, y, e.Item.Width - 8, y);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e) { }

        protected override void InitializeItem(ToolStripItem item)
        {
            base.InitializeItem(item);
        }

        // Icoontjes zelf tekenen in de marge (Lucide), het vinkje bij Pauze in blauw
        protected override void OnRenderItemBackground(ToolStripItemRenderEventArgs e) { base.OnRenderItemBackground(e); }

        public static void DrawIcon(Graphics g, ToolStripItem item, Theme t)
        {
            string icon = item.Tag as string;
            if (icon == null) return;
            float s = item.Height / 32f;
            RectangleF r = new RectangleF(8, 0, 20 * Math.Max(1, s), item.Height);
            ToolStripMenuItem mi = item as ToolStripMenuItem;
            Color c = item.Selected ? t.OnPrimaryContainer : t.Variant;
            if (mi != null && mi.Checked) { c = t.Primary; icon = "check"; }
            Icons.Draw(g, icon, r, 16 * Math.Max(1, s), c, 2);
        }
    }
}
