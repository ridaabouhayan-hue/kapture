using System;
using System.Drawing;

namespace Kapture
{
    // Het pictogram voor de vensters: één keer gemaakt (een HICON per venster lekte)
    static class AppIcon
    {
        static Icon icon; static bool tried;
        public static Icon Get()
        {
            if (icon != null || tried) return icon;
            tried = true;
            try
            {
                using (Bitmap b = Logo.Bitmap(32))
                {
                    IntPtr h = b.GetHicon();
                    using (Icon tmp = Icon.FromHandle(h)) icon = (Icon)tmp.Clone();
                    W.DestroyIcon(h);
                }
            }
            catch (Exception ex) { Log.Error("Pictogram maken mislukt", ex); }
            return icon;
        }
    }
}
