using System;
using System.Drawing;
using System.Windows.Forms;

namespace WallpaperParsec
{
    /// <summary>
    /// Locates the shell window that paints the wallpaper, i.e. the one layer that sits
    /// under the desktop icons. Everything here is best-effort: the layout differs between
    /// Windows 10, Windows 11 and the "Windows spotlight" desktop, and none of it is
    /// contractual, so every step degrades to a usable fallback instead of throwing.
    /// </summary>
    internal static class DesktopLayer
    {
        /// <summary>Handle of the wallpaper layer, or IntPtr.Zero if it could not be found.</summary>
        public static IntPtr Resolve()
        {
            IntPtr progman = Native.FindWindow("Progman", null);
            if (progman == IntPtr.Zero)
                return IntPtr.Zero;

            RequestWorkerSplit(progman);

            IntPtr worker = FindWallpaperWorker();
            if (worker != IntPtr.Zero)
                return worker;

            // Progman kept SHELLDLL_DefView and spawned the wallpaper WorkerW as its own
            // child instead of a sibling. That child is the wallpaper, so parenting to
            // Progman itself would put us underneath it and show nothing; parenting to the
            // child puts us over the wallpaper and still under the icons, which are the
            // sibling in front of it.
            IntPtr childWorker = Native.FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);
            if (childWorker != IntPtr.Zero)
                return childWorker;

            // No WorkerW anywhere: Progman paints the wallpaper itself and is the layer.
            if (Native.FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
                return progman;

            return IntPtr.Zero;
        }

        private static void RequestWorkerSplit(IntPtr progman)
        {
            IntPtr unused;

            // The zero-argument form is what Windows 10 and most Windows 11 builds answer.
            Native.SendMessageTimeout(progman, Native.WM_SPAWN_WORKER, IntPtr.Zero, IntPtr.Zero,
                Native.SMTO_NORMAL, 1000, out unused);

            // Builds running the slideshow/spotlight desktop ignore the above and only
            // split when asked with the extended arguments, so send both and take
            // whichever produced a WorkerW.
            Native.SendMessageTimeout(progman, Native.WM_SPAWN_WORKER, new IntPtr(0x0D), new IntPtr(0x01),
                Native.SMTO_NORMAL, 1000, out unused);
            Native.SendMessageTimeout(progman, Native.WM_SPAWN_WORKER, new IntPtr(0x0D), IntPtr.Zero,
                Native.SMTO_NORMAL, 1000, out unused);
        }

        /// <summary>
        /// After the split there are two WorkerW windows and only one of them paints the
        /// wallpaper. The wallpaper one is the sibling that follows the WorkerW holding
        /// SHELLDLL_DefView; the icon-owning one must be left alone or the hosted window
        /// ends up in front of the icons.
        /// </summary>
        private static IntPtr FindWallpaperWorker()
        {
            IntPtr found = IntPtr.Zero;

            Native.EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
            {
                if (Native.FindWindowEx(hWnd, IntPtr.Zero, "SHELLDLL_DefView", null) == IntPtr.Zero)
                    return true;

                IntPtr sibling = Native.FindWindowEx(IntPtr.Zero, hWnd, "WorkerW", null);
                if (sibling != IntPtr.Zero)
                {
                    found = sibling;
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            return found;
        }

        /// <summary>
        /// Child coordinates inside the wallpaper layer. It spans the whole virtual screen,
        /// so its client origin is the virtual screen origin, which is not (0,0) whenever a
        /// monitor sits left of or above the primary one.
        /// </summary>
        public static Rectangle ToLayerCoordinates(Rectangle screenRect)
        {
            Rectangle virt = SystemInformation.VirtualScreen;
            return new Rectangle(screenRect.X - virt.X, screenRect.Y - virt.Y, screenRect.Width, screenRect.Height);
        }

        /// <summary>Bounds of the configured target, in screen coordinates.</summary>
        public static Rectangle TargetBounds(string monitorDeviceName)
        {
            if (string.IsNullOrEmpty(monitorDeviceName) || monitorDeviceName == Settings.AllMonitors)
                return SystemInformation.VirtualScreen;

            Screen[] screens = Screen.AllScreens;
            for (int i = 0; i < screens.Length; i++)
            {
                if (screens[i].DeviceName == monitorDeviceName)
                    return screens[i].Bounds;
            }

            // The saved monitor is gone (undocked, unplugged). The primary is the least
            // surprising place to land, and the setting is left alone so reconnecting works.
            return Screen.PrimaryScreen.Bounds;
        }
    }
}
