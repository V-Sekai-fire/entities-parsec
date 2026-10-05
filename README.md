# entities-parsec

A Windows tray app that moves any window behind the desktop icons, so a full-screen remote-desktop session plays as the wallpaper.

## What it is for

It reparents the chosen window into the layer the shell draws the wallpaper in and keeps it there when the shell rebuilds that layer. `WALLPAPER_LAYER.md` describes the mechanism and what it cannot do.

## Build and run

```powershell
.\build.ps1
```

The script needs no SDK; it uses the C# compiler that ships with the operating system. Run the program it writes; it has no window and lives in the tray.

## Licence

MIT; see `LICENSE`.
