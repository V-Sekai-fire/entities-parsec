# The wallpaper layer, and what it will not do

Moved out of `README.md` when the forty-line rule went in. The README says what this is and how
to use it; this page is the mechanism and the limits that follow from it.

## How it works

1. Send the undocumented message `0x052C` to `Progman`, which asks the shell to split the
   desktop into a wallpaper-painting layer and the icon layer. Three argument forms are sent,
   because Windows 11's spotlight desktop answers only the extended one.
2. Find the layer. Three shapes occur in the wild and all three are handled: a top-level
   `WorkerW` following the window that owns `SHELLDLL_DefView`; a `WorkerW` that is a *child* of
   `Progman`; or `Progman` itself when no `WorkerW` exists.
3. Save the target window's parent, styles and placement, strip its frame, add `WS_CHILD` and
   `WS_EX_NOACTIVATE`, then `SetParent` it into the layer.
4. Size it to the chosen monitor, in coordinates relative to the layer's origin — which is the
   *virtual screen* origin, not (0,0), whenever a monitor sits left of or above the primary.
5. Push it to the bottom of its siblings. Skipping this is the common bug: a freshly reparented
   child goes to the *top*, which covers the desktop icons.

Step 2 is where a naive implementation breaks. On the machine this was developed on, the
sibling `WorkerW` never appears and `Progman` owns a child `WorkerW` that paints the wallpaper —
so the widely-copied "fall back to `Progman`" advice puts your window *behind the wallpaper*,
where it renders nothing at all.

## What it cannot do

- **No input.** A window in the wallpaper layer gets no clicks and no keyboard focus; the icon
  layer is in front of it and takes them. This is a viewer. If you need to control the remote
  machine, bring Parsec back to the desktop first.
- **Explorer restarting takes the window with it.** Children die with their parent, so if
  `explorer.exe` crashes or restarts, the hosted window is destroyed and Parsec has to be
  relaunched. Nothing can prevent this; the app notices and tells you.
- **Do not kill it from Task Manager.** Exit from the tray menu instead. The original styles and
  parent live in this process, so killing it strands the hosted window as a frameless child of a
  shell window. Logging off and shutting down are handled and restore first.
- **Parsec must be windowed.** A client in exclusive fullscreen cannot be reparented.
- **Elevation must match.** A window owned by an elevated process cannot be adopted unless this
  app is elevated too. It says so rather than failing silently.
- **The stream may throttle.** Parsec decides its own decode rate, and a window the compositor
  considers occluded may get fewer frames. That is the remote client's policy, not something
  this app can override.
