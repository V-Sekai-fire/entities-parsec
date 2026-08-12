using System;
using System.Runtime.InteropServices;
using System.Text;

namespace WallpaperParsec
{
    /// <summary>
    /// user32 surface used to move a foreign top-level window into the desktop
    /// wallpaper layer. Signatures only; policy lives in DesktopLayer/AttachedWindow.
    /// </summary>
    internal static class Native
    {
        public const int GWL_STYLE = -16;
        public const int GWL_EXSTYLE = -20;

        public const int WS_CHILD = 0x40000000;
        public const int WS_VISIBLE = 0x10000000;
        public const int WS_POPUP = unchecked((int)0x80000000);
        public const int WS_CAPTION = 0x00C00000;
        public const int WS_BORDER = 0x00800000;
        public const int WS_DLGFRAME = 0x00400000;
        public const int WS_THICKFRAME = 0x00040000;
        public const int WS_SYSMENU = 0x00080000;
        public const int WS_MINIMIZEBOX = 0x00020000;
        public const int WS_MAXIMIZEBOX = 0x00010000;
        public const int WS_CLIPSIBLINGS = 0x04000000;
        public const int WS_CLIPCHILDREN = 0x02000000;

        public const int WS_EX_APPWINDOW = 0x00040000;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_NOACTIVATE = 0x08000000;
        public const int WS_EX_TOPMOST = 0x00000008;

        public const int SWP_NOZORDER = 0x0004;
        public const int SWP_NOACTIVATE = 0x0010;
        public const int SWP_FRAMECHANGED = 0x0020;
        public const int SWP_SHOWWINDOW = 0x0040;
        public const int SWP_NOCOPYBITS = 0x0100;

        public const int SW_HIDE = 0;
        public const int SW_SHOWNORMAL = 1;
        public const int SW_SHOWNOACTIVATE = 4;
        public const int SW_RESTORE = 9;

        public const int SMTO_NORMAL = 0x0000;

        /// <summary>
        /// Undocumented Progman message that asks the shell to split the desktop into a
        /// wallpaper-painting WorkerW and the icon layer. Repeating it is a no-op.
        /// </summary>
        public const int WM_SPAWN_WORKER = 0x052C;

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindow(string className, string windowName);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string className, string windowName);

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder buffer, int maxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder buffer, int maxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetParent(IntPtr child, IntPtr newParent);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SendMessageTimeout(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam,
            int flags, int timeoutMs, out IntPtr result);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int cx, int cy, int flags);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int cmd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT placement);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT placement);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int RegisterWindowMessage(string name);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
        private static extern int GetWindowLong32(IntPtr hWnd, int index);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr GetWindowLong64(IntPtr hWnd, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
        private static extern int SetWindowLong32(IntPtr hWnd, int index, int value);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr SetWindowLong64(IntPtr hWnd, int index, IntPtr value);

        // The *Ptr entry points do not exist in 32-bit user32, so the width has to be
        // chosen at run time rather than by an AnyCPU build guess.
        public static int GetWindowLong(IntPtr hWnd, int index)
        {
            if (IntPtr.Size == 8)
                return (int)GetWindowLong64(hWnd, index).ToInt64();
            return GetWindowLong32(hWnd, index);
        }

        public static int SetWindowLong(IntPtr hWnd, int index, int value)
        {
            if (IntPtr.Size == 8)
                return (int)SetWindowLong64(hWnd, index, new IntPtr(value)).ToInt64();
            return SetWindowLong32(hWnd, index, value);
        }

        public static string ClassNameOf(IntPtr hWnd)
        {
            StringBuilder sb = new StringBuilder(256);
            int n = GetClassName(hWnd, sb, sb.Capacity);
            if (n <= 0) return string.Empty;
            return sb.ToString();
        }

        public static string TitleOf(IntPtr hWnd)
        {
            int len = GetWindowTextLength(hWnd);
            if (len <= 0) return string.Empty;
            StringBuilder sb = new StringBuilder(len + 1);
            GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public POINT ptMinPosition;
            public POINT ptMaxPosition;
            public RECT rcNormalPosition;

            public static WINDOWPLACEMENT Create()
            {
                WINDOWPLACEMENT wp = new WINDOWPLACEMENT();
                wp.length = Marshal.SizeOf(typeof(WINDOWPLACEMENT));
                return wp;
            }
        }
    }
}
