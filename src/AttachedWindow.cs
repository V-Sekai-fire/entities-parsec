using System;
using System.Drawing;

namespace WallpaperParsec
{
    /// <summary>
    /// One foreign window hosted in the wallpaper layer, plus everything needed to put it
    /// back the way it was. The restore state is the point: reparenting and restyling
    /// another process's window is destructive, and the owning app will not undo it.
    /// </summary>
    internal sealed class AttachedWindow
    {
        private const int StripStyles = Native.WS_CAPTION | Native.WS_THICKFRAME | Native.WS_SYSMENU |
                                        Native.WS_MINIMIZEBOX | Native.WS_MAXIMIZEBOX | Native.WS_BORDER |
                                        Native.WS_DLGFRAME | Native.WS_POPUP;

        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);

        private readonly IntPtr _hWnd;
        private readonly int _originalStyle;
        private readonly int _originalExStyle;
        private readonly IntPtr _originalParent;
        private Native.WINDOWPLACEMENT _originalPlacement;

        public IntPtr Handle { get { return _hWnd; } }
        public string Title { get; private set; }
        public IntPtr Layer { get; private set; }

        private AttachedWindow(IntPtr hWnd, IntPtr layer, string title,
            int style, int exStyle, IntPtr parent, Native.WINDOWPLACEMENT placement)
        {
            _hWnd = hWnd;
            Layer = layer;
            Title = title;
            _originalStyle = style;
            _originalExStyle = exStyle;
            _originalParent = parent;
            _originalPlacement = placement;
        }

        public static AttachedWindow Attach(IntPtr hWnd, IntPtr layer, Rectangle screenBounds)
        {
            if (!Native.IsWindow(hWnd)) throw new InvalidOperationException("That window no longer exists.");
            if (layer == IntPtr.Zero) throw new InvalidOperationException("The desktop wallpaper layer could not be found.");

            int style = Native.GetWindowLong(hWnd, Native.GWL_STYLE);
            int exStyle = Native.GetWindowLong(hWnd, Native.GWL_EXSTYLE);
            IntPtr parent = Native.GetParent(hWnd);
            string title = Native.TitleOf(hWnd);

            Native.WINDOWPLACEMENT placement = Native.WINDOWPLACEMENT.Create();
            Native.GetWindowPlacement(hWnd, ref placement);

            // A maximized window keeps its maximized geometry after reparenting and ignores
            // the size we ask for, so drop it to restored state before touching anything.
            if (placement.showCmd != Native.SW_SHOWNORMAL)
                Native.ShowWindow(hWnd, Native.SW_RESTORE);

            AttachedWindow attached = new AttachedWindow(hWnd, layer, title, style, exStyle, parent, placement);

            int childStyle = (style & ~StripStyles) | Native.WS_CHILD | Native.WS_VISIBLE | Native.WS_CLIPSIBLINGS;
            Native.SetWindowLong(hWnd, Native.GWL_STYLE, childStyle);

            // NOACTIVATE keeps the hosted app from stealing focus when it repaints or
            // reconnects; APPWINDOW/TOPMOST would fight the layer it now lives in.
            int childExStyle = (exStyle & ~(Native.WS_EX_APPWINDOW | Native.WS_EX_TOPMOST)) | Native.WS_EX_NOACTIVATE;
            Native.SetWindowLong(hWnd, Native.GWL_EXSTYLE, childExStyle);

            if (Native.SetParent(hWnd, layer) == IntPtr.Zero)
            {
                int err = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                attached.RestoreStyles();
                throw new InvalidOperationException(string.Format(
                    "SetParent failed (error {0}). A window owned by an elevated process cannot be adopted " +
                    "unless this app runs elevated too.", err));
            }

            attached.Resize(screenBounds);
            return attached;
        }

        public void Resize(Rectangle screenBounds)
        {
            Rectangle r = DesktopLayer.ToLayerCoordinates(screenBounds);

            // HWND_BOTTOM rather than "leave the z-order alone": a freshly reparented child
            // goes to the top of its siblings, which on the Progman fallback path means
            // sitting in front of SHELLDLL_DefView and hiding every desktop icon.
            Native.SetWindowPos(_hWnd, HWND_BOTTOM, r.X, r.Y, r.Width, r.Height,
                Native.SWP_NOACTIVATE | Native.SWP_FRAMECHANGED | Native.SWP_SHOWWINDOW);
        }

        public bool IsAlive()
        {
            return Native.IsWindow(_hWnd);
        }

        /// <summary>True when the shell rebuilt the desktop and dropped us out of the layer.</summary>
        public bool IsStillParented()
        {
            return Native.IsWindow(_hWnd) && Native.GetParent(_hWnd) == Layer;
        }

        public void Reparent(IntPtr layer, Rectangle screenBounds)
        {
            Layer = layer;
            Native.SetParent(_hWnd, layer);
            Resize(screenBounds);
        }

        public void Detach()
        {
            if (!Native.IsWindow(_hWnd))
                return;

            // Order matters: clear WS_CHILD only after the window is a top-level window
            // again, otherwise the shell briefly owns a parentless child window.
            Native.SetParent(_hWnd, _originalParent);
            RestoreStyles();

            Native.WINDOWPLACEMENT placement = _originalPlacement;
            Native.SetWindowPlacement(_hWnd, ref placement);
            Native.SetWindowPos(_hWnd, IntPtr.Zero, 0, 0, 0, 0,
                Native.SWP_NOZORDER | Native.SWP_NOACTIVATE | Native.SWP_FRAMECHANGED |
                0x0001 /* SWP_NOSIZE */ | 0x0002 /* SWP_NOMOVE */);
            Native.ShowWindow(_hWnd, Native.SW_SHOWNORMAL);
        }

        private void RestoreStyles()
        {
            Native.SetWindowLong(_hWnd, Native.GWL_STYLE, _originalStyle);
            Native.SetWindowLong(_hWnd, Native.GWL_EXSTYLE, _originalExStyle);
        }
    }
}
