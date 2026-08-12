using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

namespace WallpaperParsec
{
    /// <summary>
    /// Tray UI and the lifetime of the hosted window. No main form: the app is the icon.
    /// </summary>
    internal sealed class TrayApp : ApplicationContext
    {
        private readonly NotifyIcon _icon;
        private readonly ContextMenuStrip _menu;
        private readonly Timer _watchdog;
        private readonly ShellWatcher _shellWatcher;
        private readonly Settings _settings;

        private AttachedWindow _attached;

        public TrayApp() : this(null)
        {
        }

        public TrayApp(string attachHint)
        {
            _settings = Settings.Load();

            _menu = new ContextMenuStrip();
            _menu.Opening += OnMenuOpening;

            _icon = new NotifyIcon();
            _icon.Icon = SystemIcons.Application;
            _icon.Text = "Wallpaper Parsec";
            _icon.Visible = true;
            _icon.ContextMenuStrip = _menu;
            _icon.DoubleClick += delegate { ToggleAttach(); };

            _shellWatcher = new ShellWatcher();
            _shellWatcher.ShellRestarted += OnShellRestarted;
            _shellWatcher.CloseRequested += delegate { ExitApp(); };

            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

            // Logging off or shutting down must put the hosted window back first: a window
            // left styled as a child of a shell window has no way to restore itself.
            SystemEvents.SessionEnding += OnSessionEnding;

            _watchdog = new Timer();
            _watchdog.Interval = 2000;
            _watchdog.Tick += OnWatchdogTick;
            _watchdog.Enabled = true;

            if (!string.IsNullOrEmpty(attachHint))
                AttachByHint(attachHint);
            else if (_settings.AutoAttach)
                TryAttachTarget(false);
        }

        /// <summary>Attach the first visible window whose title or process contains the hint.</summary>
        private void AttachByHint(string hint)
        {
            foreach (WindowEntry entry in EnumerateCandidateWindows())
            {
                if (entry.Title.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    entry.ProcessName.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    AttachHandle(entry.Handle, true);
                    return;
                }
            }
            Notify("No visible window matched \"" + hint + "\".");
        }

        // ---- menu ----------------------------------------------------------

        private void OnMenuOpening(object sender, EventArgs e)
        {
            _menu.Items.Clear();

            if (_attached != null && _attached.IsAlive())
            {
                ToolStripMenuItem status = new ToolStripMenuItem("Behind wallpaper: " + Shorten(_attached.Title));
                status.Enabled = false;
                _menu.Items.Add(status);
                _menu.Items.Add(new ToolStripSeparator());
                _menu.Items.Add(Item("Bring back to the desktop", delegate { Detach(true); }));
                _menu.Items.Add(Item("Re-fit to monitor", delegate { _attached.Resize(DesktopLayer.TargetBounds(_settings.Monitor)); }));
            }
            else
            {
                _menu.Items.Add(Item("Put " + _settings.ProcessName + " behind the wallpaper",
                    delegate { TryAttachTarget(true); }));
                _menu.Items.Add(BuildWindowPicker());
            }

            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(BuildMonitorMenu());

            ToolStripMenuItem auto = Item("Attach automatically when it starts", delegate
            {
                _settings.AutoAttach = !_settings.AutoAttach;
                _settings.Save();
            });
            auto.Checked = _settings.AutoAttach;
            _menu.Items.Add(auto);

            ToolStripMenuItem keep = Item("Re-attach if the desktop rebuilds", delegate
            {
                _settings.KeepAttached = !_settings.KeepAttached;
                _settings.Save();
            });
            keep.Checked = _settings.KeepAttached;
            _menu.Items.Add(keep);

            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(Item("Launch Parsec", LaunchTarget));
            _menu.Items.Add(Item("Open settings folder", delegate
            {
                System.IO.Directory.CreateDirectory(Settings.Directory);
                Process.Start("explorer.exe", Settings.Directory);
            }));
            _menu.Items.Add(Item("Exit", delegate { ExitApp(); }));
        }

        private ToolStripMenuItem BuildWindowPicker()
        {
            ToolStripMenuItem picker = new ToolStripMenuItem("Put another window behind the wallpaper");

            foreach (WindowEntry entry in EnumerateCandidateWindows())
            {
                WindowEntry captured = entry;
                picker.DropDownItems.Add(Item(Shorten(captured.Title) + "  [" + captured.ProcessName + "]",
                    delegate { AttachHandle(captured.Handle, true); }));
            }

            if (picker.DropDownItems.Count == 0)
                picker.Enabled = false;

            return picker;
        }

        private ToolStripMenuItem BuildMonitorMenu()
        {
            ToolStripMenuItem monitors = new ToolStripMenuItem("Monitor");

            ToolStripMenuItem all = Item("All monitors", delegate { SetMonitor(Settings.AllMonitors); });
            all.Checked = _settings.Monitor == Settings.AllMonitors;
            monitors.DropDownItems.Add(all);

            Screen[] screens = Screen.AllScreens;
            for (int i = 0; i < screens.Length; i++)
            {
                Screen screen = screens[i];
                string label = string.Format("Monitor {0} — {1}×{2}{3}", i + 1,
                    screen.Bounds.Width, screen.Bounds.Height, screen.Primary ? " (primary)" : "");
                ToolStripMenuItem item = Item(label, delegate { SetMonitor(screen.DeviceName); });
                item.Checked = _settings.Monitor == screen.DeviceName;
                monitors.DropDownItems.Add(item);
            }

            return monitors;
        }

        private static ToolStripMenuItem Item(string text, EventHandler onClick)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.Click += onClick;
            return item;
        }

        private static string Shorten(string text)
        {
            if (string.IsNullOrEmpty(text)) return "(untitled)";
            return text.Length <= 48 ? text : text.Substring(0, 45) + "...";
        }

        private void SetMonitor(string deviceName)
        {
            _settings.Monitor = deviceName;
            _settings.Save();
            if (_attached != null && _attached.IsAlive())
                _attached.Resize(DesktopLayer.TargetBounds(_settings.Monitor));
        }

        // ---- attach / detach -----------------------------------------------

        private void ToggleAttach()
        {
            if (_attached != null && _attached.IsAlive())
                Detach(true);
            else
                TryAttachTarget(true);
        }

        private void TryAttachTarget(bool loud)
        {
            IntPtr hWnd = FindTargetWindow();
            if (hWnd == IntPtr.Zero)
            {
                if (loud)
                    Notify("No window found for " + _settings.ProcessName +
                           ". Start Parsec and open the session window first.");
                return;
            }
            AttachHandle(hWnd, loud);
        }

        private void AttachHandle(IntPtr hWnd, bool loud)
        {
            if (_attached != null)
                Detach(false);

            try
            {
                IntPtr layer = DesktopLayer.Resolve();
                if (layer == IntPtr.Zero)
                {
                    Notify("The desktop wallpaper layer could not be found. " +
                           "This needs Explorer to be running as the shell.");
                    return;
                }

                _attached = AttachedWindow.Attach(hWnd, layer, DesktopLayer.TargetBounds(_settings.Monitor));
                if (loud)
                    Notify(Shorten(_attached.Title) + " is behind the wallpaper. " +
                           "It cannot receive clicks or keys there.");
            }
            catch (Exception ex)
            {
                _attached = null;
                Notify(ex.Message);
            }
        }

        private void Detach(bool loud)
        {
            if (_attached == null)
                return;

            AttachedWindow window = _attached;
            _attached = null;
            try
            {
                window.Detach();
                if (loud)
                    Notify(Shorten(window.Title) + " is back on the desktop.");
            }
            catch (Exception ex)
            {
                Notify("Could not restore the window: " + ex.Message);
            }
        }

        // ---- target discovery ----------------------------------------------

        private IntPtr FindTargetWindow()
        {
            string wanted = (_settings.ProcessName ?? "").Trim();
            if (wanted.Length == 0)
                return IntPtr.Zero;

            // Parsec's client is parsecd.exe, but accept the bare name too so a renamed
            // or repackaged build still resolves.
            string[] names = new string[] { wanted, wanted + "d" };
            for (int i = 0; i < names.Length; i++)
            {
                Process[] found;
                try { found = Process.GetProcessesByName(names[i]); }
                catch (InvalidOperationException) { continue; }

                for (int j = 0; j < found.Length; j++)
                {
                    IntPtr h = found[j].MainWindowHandle;
                    if (h != IntPtr.Zero && Native.IsWindowVisible(h))
                        return h;
                }
            }
            return IntPtr.Zero;
        }

        private struct WindowEntry
        {
            public IntPtr Handle;
            public string Title;
            public string ProcessName;
        }

        private List<WindowEntry> EnumerateCandidateWindows()
        {
            List<WindowEntry> entries = new List<WindowEntry>();
            IntPtr ownHandle = _shellWatcher.Handle;

            Native.EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
            {
                if (hWnd == ownHandle || !Native.IsWindowVisible(hWnd))
                    return true;
                if (Native.GetParent(hWnd) != IntPtr.Zero)
                    return true;

                string title = Native.TitleOf(hWnd);
                if (title.Length == 0)
                    return true;

                // The shell's own windows are the layer, not candidates for it.
                string className = Native.ClassNameOf(hWnd);
                if (className == "Progman" || className == "WorkerW" || className == "Shell_TrayWnd")
                    return true;

                WindowEntry entry = new WindowEntry();
                entry.Handle = hWnd;
                entry.Title = title;
                entry.ProcessName = ProcessNameOf(hWnd);
                entries.Add(entry);
                return true;
            }, IntPtr.Zero);

            return entries;
        }

        private static string ProcessNameOf(IntPtr hWnd)
        {
            try
            {
                uint pid;
                Native.GetWindowThreadProcessId(hWnd, out pid);
                return Process.GetProcessById((int)pid).ProcessName;
            }
            catch (Exception)
            {
                return "?";
            }
        }

        private void LaunchTarget(object sender, EventArgs e)
        {
            string path = _settings.LaunchPath;
            if (string.IsNullOrEmpty(path))
            {
                path = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    @"AppData\Local\Parsec\parsecd.exe");
            }

            try
            {
                if (System.IO.File.Exists(path))
                    Process.Start(path);
                else
                    Notify("Parsec was not found at " + path +
                           ". Set LaunchPath in settings.ini to its full path.");
            }
            catch (Exception ex)
            {
                Notify("Could not launch Parsec: " + ex.Message);
            }
        }

        // ---- upkeep ---------------------------------------------------------

        private void OnWatchdogTick(object sender, EventArgs e)
        {
            if (_attached == null)
            {
                if (_settings.AutoAttach)
                    TryAttachTarget(false);
                return;
            }

            if (!_attached.IsAlive())
            {
                // The hosted window died — usually because Explorer restarted and took its
                // child with it. Nothing to restore, so just forget it.
                _attached = null;
                Notify("The hosted window closed. Launch Parsec again to put it back.");
                return;
            }

            if (_settings.KeepAttached && !_attached.IsStillParented())
            {
                IntPtr layer = DesktopLayer.Resolve();
                if (layer != IntPtr.Zero)
                    _attached.Reparent(layer, DesktopLayer.TargetBounds(_settings.Monitor));
            }
        }

        private void OnShellRestarted(object sender, EventArgs e)
        {
            // The wallpaper layer from before the restart is gone; whatever was in it went
            // with it. Re-resolving now means the next attach lands in the new layer.
            if (_attached != null && !_attached.IsAlive())
                _attached = null;

            if (_settings.KeepAttached && _attached != null)
            {
                IntPtr layer = DesktopLayer.Resolve();
                if (layer != IntPtr.Zero)
                    _attached.Reparent(layer, DesktopLayer.TargetBounds(_settings.Monitor));
            }
        }

        private void OnSessionEnding(object sender, SessionEndingEventArgs e)
        {
            Detach(false);
        }

        private void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            if (_attached != null && _attached.IsAlive())
                _attached.Resize(DesktopLayer.TargetBounds(_settings.Monitor));
        }

        private void Notify(string message)
        {
            _icon.BalloonTipTitle = "Wallpaper Parsec";
            _icon.BalloonTipText = message;
            _icon.ShowBalloonTip(5000);
        }

        private void ExitApp()
        {
            Detach(false);
            _icon.Visible = false;
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Leaving a foreign window styled as a child of a shell window would strand
                // it invisibly, so restore before anything else is torn down.
                Detach(false);
                SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
                SystemEvents.SessionEnding -= OnSessionEnding;
                _watchdog.Dispose();
                _icon.Dispose();
                _menu.Dispose();
                _shellWatcher.DestroyHandle();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Hidden top-level window whose only job is to hear the shell's TaskbarCreated
        /// broadcast. It has to be top-level: message-only windows do not get broadcasts.
        /// </summary>
        private sealed class ShellWatcher : NativeWindow
        {
            private readonly int _taskbarCreated;

            public event EventHandler ShellRestarted;

            /// <summary>Raised on WM_CLOSE, which is how another process asks this app to quit.</summary>
            public event EventHandler CloseRequested;

            public ShellWatcher()
            {
                _taskbarCreated = Native.RegisterWindowMessage("TaskbarCreated");

                CreateParams cp = new CreateParams();
                cp.Caption = "WallpaperParsec.ShellWatcher";
                cp.Style = Native.WS_POPUP;
                cp.ExStyle = Native.WS_EX_TOOLWINDOW;
                cp.X = -32000;
                cp.Y = -32000;
                cp.Width = 0;
                cp.Height = 0;
                CreateHandle(cp);
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == _taskbarCreated && ShellRestarted != null)
                {
                    ShellRestarted(this, EventArgs.Empty);
                }
                else if (m.Msg == 0x0010 /* WM_CLOSE */ && CloseRequested != null)
                {
                    CloseRequested(this, EventArgs.Empty);
                    return;
                }
                base.WndProc(ref m);
            }
        }
    }
}
