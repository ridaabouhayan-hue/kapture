using System;
using System.Drawing;
using System.Windows.Forms;

namespace Kapture
{
    // ------------------------------------------------------------------ Vastleggen (variant C uit docs/SPIKE.md)
    // 1. Meteen een zwart venster op 45% over het hele scherm: het scherm wordt binnen ongeveer 12 ms donker.
    //    Dat venster is onzichtbaar voor de opname (WDA_EXCLUDEFROMCAPTURE).
    // 2. Dan het scherm vastleggen (BitBlt).
    // 3. Dan het stilstaande beeld tonen (Overlay) en het zwarte venster weer weg.
    interface ICapture
    {
        // Legt het rechthoek van het virtuele scherm vast; null als het niet lukt
        Dib Grab(Rectangle screen);
    }

    class GdiCapture : ICapture
    {
        public Dib Grab(Rectangle r)
        {
            Dib d = Dib.Create(r.Width, r.Height);
            if (d == null) return null;
            IntPtr scr = W.GetDC(IntPtr.Zero);
            bool ok = W.BitBlt(d.Dc, 0, 0, r.Width, r.Height, scr, r.X, r.Y, W.SRCCOPY);
            W.ReleaseDC(IntPtr.Zero, scr);
            if (!ok) { d.Dispose(); return null; }
            return d;
        }
    }

    static class VirtualScreen
    {
        public static Rectangle Bounds
        {
            get { return new Rectangle(W.GetSystemMetrics(76), W.GetSystemMetrics(77), W.GetSystemMetrics(78), W.GetSystemMetrics(79)); }
        }
    }

    // Het zwarte venster van stap 1
    class DimWindow : Form
    {
        public const byte Alpha = 115;   // 45% zwart; Overlay maakt het stilstaande beeld even donker (factor 141/256)

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= W.WS_EX_LAYERED | W.WS_EX_TOOLWINDOW | W.WS_EX_TOPMOST | W.WS_EX_NOACTIVATE | W.WS_EX_TRANSPARENT;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        public DimWindow()
        {
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
            BackColor = Color.Black; AutoScaleMode = AutoScaleMode.None;
            IntPtr h = Handle;
            W.SetLayeredWindowAttributes(h, 0, Alpha, W.LWA_ALPHA);
            Excluded = W.SetWindowDisplayAffinity(h, W.WDA_EXCLUDEFROMCAPTURE);
            if (!Excluded) Log.Info("Donker venster kan niet onzichtbaar voor de opname: het scherm wordt pas donker met het stilstaande beeld");
        }

        public bool Excluded;

        public void ShowOver(Rectangle r)
        {
            W.SetWindowPos(Handle, W.HWND_TOPMOST, r.X, r.Y, r.Width, r.Height, W.SWP_NOACTIVATE | W.SWP_SHOWWINDOW);
        }

        public void HideNow() { W.ShowWindow(Handle, W.SW_HIDE); }
    }
}
