// Kapture spike, ronde 2: los testprogramma, geen onderdeel van de app.
// Meet: sneltoets tot donker scherm en tot stilstaand beeld, geheugen, Print Screen, schaal en pixels, Mica.
// Bewaart nooit een schermafbeelding op schijf en raakt het klembord niet aan.
// Variant B: vastleggen, dan stilstaand beeld plus donker venster tonen.
// Variant C: eerst het donkere venster (onzichtbaar voor de opname), dan vastleggen.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

static class W
{
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(IntPtr h, int id, int mods, int vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h, int id);
    [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr o);
    [DllImport("gdi32.dll")] public static extern bool BitBlt(IntPtr d, int x, int y, int w, int h, IntPtr s, int sx, int sy, int rop);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateDIBSection(IntPtr dc, ref BITMAPINFO bmi, uint usage, out IntPtr bits, IntPtr sec, uint off);
    [DllImport("gdi32.dll")] public static extern int GetDeviceCaps(IntPtr dc, int i);
    [DllImport("gdi32.dll")] public static extern uint GetPixel(IntPtr dc, int x, int y);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool UpdateLayeredWindow(IntPtr h, IntPtr dst, ref POINT pt, ref SIZE sz, IntPtr src, ref POINT srcPt, int key, ref BLENDFUNCTION bf, int flags);
    [DllImport("user32.dll")] public static extern bool SetLayeredWindowAttributes(IntPtr h, uint key, byte alpha, uint flags);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);
    [DllImport("user32.dll")] public static extern bool SetWindowDisplayAffinity(IntPtr h, uint a);
    [DllImport("dwmapi.dll")] public static extern int DwmFlush();
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr h, int a, ref int v, int size);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int i);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(IntPtr mon, int type, out uint x, out uint y);
    [DllImport("user32.dll")] public static extern IntPtr MonitorFromPoint(POINT pt, uint flags);
    [DllImport("kernel32.dll")] public static extern bool SetProcessWorkingSetSize(IntPtr p, IntPtr min, IntPtr max);

    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }
    [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int cx, cy; public SIZE(int a, int b) { cx = a; cy = b; } }
    [StructLayout(LayoutKind.Sequential, Pack = 1)] public struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
    [StructLayout(LayoutKind.Sequential)] public struct BITMAPINFOHEADER { public int biSize, biWidth, biHeight; public short biPlanes, biBitCount; public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant; }
    [StructLayout(LayoutKind.Sequential)] public struct BITMAPINFO { public BITMAPINFOHEADER h; public int c0; }
}

class Dib
{
    public IntPtr Dc, Bmp, Old, Bits; public int Wd, Ht;
    Dib() { }
    // Geeft null als Windows de buffer niet kan maken.
    public static Dib Maak(int w, int h)
    {
        var d = new Dib(); d.Wd = w; d.Ht = h;
        var bi = new W.BITMAPINFO();
        bi.h.biSize = Marshal.SizeOf(typeof(W.BITMAPINFOHEADER)); bi.h.biWidth = w; bi.h.biHeight = -h; bi.h.biPlanes = 1; bi.h.biBitCount = 32;
        IntPtr scr = W.GetDC(IntPtr.Zero);
        d.Dc = W.CreateCompatibleDC(scr);
        d.Bmp = W.CreateDIBSection(scr, ref bi, 0, out d.Bits, IntPtr.Zero, 0);
        W.ReleaseDC(IntPtr.Zero, scr);
        if (d.Bmp == IntPtr.Zero || d.Bits == IntPtr.Zero) { W.DeleteDC(d.Dc); return null; }
        d.Old = W.SelectObject(d.Dc, d.Bmp);
        return d;
    }
    public void Free() { W.SelectObject(Dc, Old); W.DeleteObject(Bmp); W.DeleteDC(Dc); }
}

class Laag : Form
{
    protected override CreateParams CreateParams
    {
        get { var cp = base.CreateParams; cp.ExStyle |= 0x80000 | 0x80 | 0x8 | 0x08000000; return cp; } // layered, toolwindow, topmost, noactivate
    }
    public Laag() { FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; }
}

class HotWin : NativeWindow
{
    public Action<int> OnHot;
    public HotWin() { CreateHandle(new CreateParams()); }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0312 && OnHot != null) OnHot(m.WParam.ToInt32());
        base.WndProc(ref m);
    }
}

static class Spike
{
    const int Rondes = 30;
    static StringBuilder R = new StringBuilder();
    static void L(string s) { Console.WriteLine(s); R.AppendLine(s); }
    static double Ms(long a, long b) { return (b - a) * 1000.0 / Stopwatch.Frequency; }
    static string P(List<double> v)
    {
        if (v.Count == 0) return "geen metingen";
        var s = new List<double>(v); s.Sort();
        Func<double, double> q = f => s[Math.Min(s.Count - 1, (int)Math.Round(f * (s.Count - 1)))];
        return string.Format("p50 {0:0.0} ms, p95 {1:0.0} ms, max {2:0.0} ms ({3} keer)", q(0.5), q(0.95), s[s.Count - 1], s.Count);
    }
    static string Mem()
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var p = Process.GetCurrentProcess(); p.Refresh();
        return string.Format("prive {0:0.0} MB, werkset {1:0.0} MB", p.PrivateMemorySize64 / 1048576.0, p.WorkingSet64 / 1048576.0);
    }

    static bool C;
    static int vx, vy, vw, vh;
    static Laag beeld, dim;
    static Dib cap;
    static readonly object slot = new object();
    static AutoResetEvent klaar = new AutoResetEvent(false);
    static volatile bool verwacht;
    static long tInject;
    static int[,] punten;
    static uint[] referentie = new uint[5];
    static List<double> totaal = new List<double>(), nabericht = new List<double>(), donker = new List<double>(), vastleg = new List<double>(), tonen = new List<double>(), wacht = new List<double>(), png = new List<double>();
    static int pixOk, pixAll, mislukt, timeouts;

    [STAThread]
    static void Main(string[] args)
    {
        long t0 = Stopwatch.GetTimestamp();
        C = args.Length > 0 && args[0] == "C";
        bool pm = W.SetProcessDpiAwarenessContext(new IntPtr(-4));
        L("Kapture spike ronde 2, " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ", .NET " + Environment.Version);
        L("Variant: " + (C ? "C (eerst donker, dan vastleggen)" : "B (vastleggen, dan beeld en donker tonen)"));
        L("PerMonitorV2 aan: " + pm);

        vx = W.GetSystemMetrics(76); vy = W.GetSystemMetrics(77); vw = W.GetSystemMetrics(78); vh = W.GetSystemMetrics(79);
        punten = new int[,] { { vw / 2, vh / 2 }, { 5, 5 }, { vw - 6, 5 }, { 5, vh - 6 }, { vw - 6, vh - 6 } };
        IntPtr sdc = W.GetDC(IntPtr.Zero);
        int fysW = W.GetDeviceCaps(sdc, 118), fysH = W.GetDeviceCaps(sdc, 117);
        W.ReleaseDC(IntPtr.Zero, sdc);
        L(""); L("== Schermen");
        L(string.Format("Virtueel scherm: {0}x{1} op ({2},{3}); hoofdscherm fysiek {4}x{5}", vw, vh, vx, vy, fysW, fysH));
        foreach (var s in Screen.AllScreens)
        {
            uint dx, dy; IntPtr mon = W.MonitorFromPoint(new W.POINT(s.Bounds.X + 1, s.Bounds.Y + 1), 2);
            int hr = W.GetDpiForMonitor(mon, 0, out dx, out dy);
            string schaal = hr == 0 ? (dx * 100 / 96) + "%" : "onbekend (fout 0x" + hr.ToString("X") + ")";
            L(string.Format("  {0}: {1}x{2} op ({3},{4}), schaal {5}{6}", s.DeviceName.Replace("\\\\.\\", ""), s.Bounds.Width, s.Bounds.Height, s.Bounds.X, s.Bounds.Y, schaal, s.Primary ? ", hoofdscherm" : ""));
        }

        var hot = new HotWin();
        beeld = new Laag(); IntPtr hb = beeld.Handle;
        dim = new Laag(); dim.BackColor = Color.Black; dim.Bounds = new Rectangle(vx, vy, vw, vh); IntPtr hd = dim.Handle;
        W.SetLayeredWindowAttributes(hd, 0, 115, 2);
        bool uitgesloten = W.SetWindowDisplayAffinity(hd, 0x11);
        L("Donker venster onzichtbaar voor de opname: " + uitgesloten);
        L("Opstarten tot klaar: " + Ms(t0, Stopwatch.GetTimestamp()).ToString("0") + " ms");

        L(""); L("== Geheugen");
        Thread.Sleep(3000);
        L("In rust: " + Mem());

        L(""); L("== Print Screen");
        object reg = Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Keyboard", "PrintScreenKeyForSnippingEnabled", null);
        L("Windows-instelling 'Print Screen opent Knipprogramma': " + (reg == null ? "niet gezet (standaard: aan op Windows 11)" : (reg.ToString() == "1" ? "aan" : "uit")));
        string[] namen = { "PrtSc", "Shift+PrtSc", "Ctrl+PrtSc", "Ctrl+Shift+PrtSc" }; int[] mods = { 0, 4, 2, 6 };
        for (int i = 0; i < 4; i++)
        {
            bool ok = W.RegisterHotKey(hot.Handle, 100 + i, mods[i] | 0x4000, 0x2C);
            int err = ok ? 0 : Marshal.GetLastWin32Error();
            L(string.Format("  {0}: {1}", namen[i], ok ? "vast te leggen" : "lukt niet (fout " + err + ")"));
            if (ok) W.UnregisterHotKey(hot.Handle, 100 + i);
        }

        L(""); L("== Snelheid (testtoets Ctrl+Alt+Shift+F11, " + Rondes + " keer)");
        if (!W.RegisterHotKey(hot.Handle, 1, 1 | 2 | 4 | 0x4000, 0x7A)) { L("Testtoets niet vrij, stop."); Bewaar(); return; }
        hot.OnHot = id =>
        {
            if (id != 1 || !verwacht) return; // een te laat bericht telt niet mee
            verwacht = false;
            long tHot = Stopwatch.GetTimestamp();
            Ronde(tHot);
            klaar.Set();
        };
        var worker = new Thread(Run); worker.IsBackground = true; worker.Start();
        Application.Run();
    }

    // Eén ronde, op de UI-draad, direct na het sneltoetsbericht. Geen meetwerk binnen de getimede stukken.
    static void Ronde(long tHot)
    {
        long tDonker = 0;
        if (C)
        {
            W.SetWindowPos(dim.Handle, new IntPtr(-1), vx, vy, vw, vh, 0x0010 | 0x0040);
            W.DwmFlush();
            tDonker = Stopwatch.GetTimestamp();
        }
        long a = Stopwatch.GetTimestamp();
        Dib d = Dib.Maak(vw, vh);
        if (d == null) { mislukt++; return; }
        IntPtr scr = W.GetDC(IntPtr.Zero);
        bool bitOk = W.BitBlt(d.Dc, 0, 0, vw, vh, scr, vx, vy, 0x00CC0020);
        W.ReleaseDC(IntPtr.Zero, scr);
        long b = Stopwatch.GetTimestamp();
        var pt = new W.POINT(vx, vy); var sz = new W.SIZE(vw, vh); var src = new W.POINT(0, 0); var bf = new W.BLENDFUNCTION();
        bool ulwOk = W.UpdateLayeredWindow(beeld.Handle, IntPtr.Zero, ref pt, ref sz, d.Dc, ref src, 0, ref bf, 4);
        W.ShowWindow(beeld.Handle, 4);
        W.SetWindowPos(dim.Handle, new IntPtr(-1), vx, vy, vw, vh, 0x0010 | 0x0040); // donker boven het stilstaande beeld
        long c = Stopwatch.GetTimestamp();
        W.DwmFlush();
        long e = Stopwatch.GetTimestamp();
        if (!bitOk || !ulwOk) { mislukt++; d.Free(); return; }

        // pas na de metingen: controle dat het donkere venster niet in de foto zit
        for (int i = 0; i < 5; i++)
        {
            int px = Marshal.ReadInt32(d.Bits, (punten[i, 1] * vw + punten[i, 0]) * 4);
            uint asRef = (uint)(((px >> 16) & 0xFF) | (px & 0xFF00) | ((px & 0xFF) << 16));
            pixAll++; if (asRef == referentie[i]) pixOk++;
        }

        lock (slot) { if (cap != null) cap.Free(); cap = d; }
        if (C) donker.Add(Ms(tHot, tDonker));
        vastleg.Add(Ms(a, b)); tonen.Add(Ms(b, c)); wacht.Add(Ms(c, e));
        nabericht.Add(Ms(tHot, e)); totaal.Add(Ms(tInject, e));
    }

    static void Key(byte vk, bool up) { W.keybd_event(vk, 0, up ? 2u : 0u, UIntPtr.Zero); }

    static void Run()
    {
        Thread.Sleep(500);
        for (int r = 0; r < Rondes; r++)
        {
            // referentiepixels buiten de meting, vlak voor de toets
            IntPtr s0 = W.GetDC(IntPtr.Zero);
            for (int i = 0; i < 5; i++) referentie[i] = W.GetPixel(s0, vx + punten[i, 0], vy + punten[i, 1]);
            W.ReleaseDC(IntPtr.Zero, s0);
            verwacht = true;
            try
            {
                Key(0x11, false); Key(0x12, false); Key(0x10, false);
                tInject = Stopwatch.GetTimestamp();
                Key(0x7A, false); Key(0x7A, true);
            }
            finally { Key(0x10, true); Key(0x12, true); Key(0x11, true); }
            if (!klaar.WaitOne(2000)) { verwacht = false; timeouts++; }
            Thread.Sleep(80);
            beeld.Invoke((Action)(() => { W.ShowWindow(dim.Handle, 0); W.ShowWindow(beeld.Handle, 0); }));
            Thread.Sleep(220);
        }
        if (C) L("Toets tot donker scherm:          " + P(donker));
        L("Toets tot stilstaand beeld:       " + P(totaal));
        L("  vanaf het sneltoetsbericht:     " + P(nabericht));
        L("  waarvan vastleggen:             " + P(vastleg));
        L("  beeld en donker tonen:          " + P(tonen));
        L("  wachten op Windows (DwmFlush):  " + P(wacht));
        L(string.Format("Mislukt: {0}, niet aangekomen: {1}", mislukt, timeouts));
        L(string.Format("Pixelcontrole: {0} van {1} punten gelijk aan het scherm van vlak voor de toets", pixOk, pixAll));

        Dib laatste; lock (slot) { laatste = cap; }
        if (laatste != null)
        {
            for (int i = 0; i < 10; i++)
            {
                long a = Stopwatch.GetTimestamp();
                using (var bmp = new Bitmap(vw, vh, vw * 4, PixelFormat.Format32bppRgb, laatste.Bits))
                using (var ms = new MemoryStream()) { bmp.Save(ms, ImageFormat.Png); }
                png.Add(Ms(a, Stopwatch.GetTimestamp()));
            }
            L("PNG van het hele scherm (in geheugen): " + P(png));
        }

        L(""); L("== Geheugen na " + Rondes + " keer");
        L("Met de laatste buffer nog vast: " + Mem());
        lock (slot) { if (cap != null) { cap.Free(); cap = null; } }
        W.SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, new IntPtr(-1), new IntPtr(-1));
        L("Buffer weg, werkset ingekrompen: " + Mem());
        Thread.Sleep(5000);
        L("5 seconden later: " + Mem());

        beeld.Invoke((Action)Mica);
        Bewaar();
        beeld.Invoke((Action)(() => Application.Exit()));
    }

    static void Mica()
    {
        L(""); L("== Mica (Windows 11-achtergrond)");
        var f = new Form(); f.Width = 400; f.Height = 250; f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-2000, -2000); f.ShowInTaskbar = false;
        f.Show();
        int dark = 1, mica = 2;
        int h1 = W.DwmSetWindowAttribute(f.Handle, 20, ref dark, 4);
        int h2 = W.DwmSetWindowAttribute(f.Handle, 38, ref mica, 4);
        L(string.Format("Donkere titelbalk: {0}; Mica: {1} (alleen of Windows het accepteert, niet bekeken)", h1 == 0 ? "aan te zetten" : "fout 0x" + h1.ToString("X"), h2 == 0 ? "aan te zetten" : "fout 0x" + h2.ToString("X")));
        f.Close();
    }

    static void Bewaar()
    {
        try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resultaat.txt"), R.ToString(), Encoding.UTF8); } catch { }
    }
}
