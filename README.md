# entities-parsec

A Windows 11 tray app that moves the Parsec client window into the desktop wallpaper layer, so the remote session plays behind your icons. It works on any top-level window, not just Parsec.

MIT licensed, written against the documented `user32` surface — no code from Lively Wallpaper, ScreenPlay or any other GPL project was read or copied.

## Build

```powershell
.\build.ps1
```

No .NET SDK required. The script prefers Visual Studio's Roslyn compiler and falls back to the C# compiler that ships inside Windows itself (`C:\Windows\Microsoft.NET\Framework64\v4.0.30319`), which is why the sources stay C# 5-compatible. Output lands in `bin\WallpaperParsec.exe`, and both compilers are verified to build it.

## Use

Run `bin\WallpaperParsec.exe`. It has no window — look for the tray icon.

- **Put parsecd behind the wallpaper** — finds the running Parsec client and attaches it.
- **Put another window behind the wallpaper** — pick any visible window instead.
- **Monitor** — one display or the whole virtual desktop.
- **Attach automatically when it starts** — polls every 2s and attaches Parsec when it appears.
- **Re-attach if the desktop rebuilds** — re-parents after the shell recreates the layer.
- **Bring back to the desktop** — restores the window. Double-clicking the tray icon toggles.

`WallpaperParsec.exe --attach parsec` attaches the first window whose title or process name contains the argument, then sits in the tray as usual. Settings live in `%APPDATA%\WallpaperParsec\settings.ini`; set `LaunchPath` there if Parsec is not at `%LOCALAPPDATA%\Parsec\parsecd.exe`.

Try it on a throwaway window first — Notepad, a browser — before pointing it at a live Parsec session.

## The layer

`WALLPAPER_LAYER.md` has the five steps this takes, the step where a naive implementation breaks, and the six things it cannot do — no input, and what happens when Explorer restarts, among them.
