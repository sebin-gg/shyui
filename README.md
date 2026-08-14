# Shy UI — Window Title Bar Auto-Hider

Shy UI is a lightweight Windows tray application that automatically hides the
title bar of maximized windows, giving you a cleaner fullscreen-style view
while keeping the window fully functional. Move your mouse to the top 2px of
the screen to temporarily reveal the title bar.

## Features

- **Auto-Shy**: any window you maximize is automatically managed (title bar hidden)
- **Per-app control**: press `Ctrl+Alt+T` while a window is focused to add/remove
  that app from the managed list
- **Pause/Resume**: `Ctrl+Alt+S` (or tray menu) pauses management and restores
  windows to their pre-managed state
- **Per-app top bar height**: configure the hidden title-bar height per app in
  Settings (default 40px)
- **Run at startup**: toggle from the tray menu (HKCU `Run` key)

## Usage

| Action | Key |
|---|---|
| Pause / Resume all | `Ctrl+Alt+S` |
| Add / remove focused app | `Ctrl+Alt+T` |
| Reveal hidden title bar | Move mouse to top 2px of screen |

Configuration is stored in `shyui_config.txt` next to the executable
(format: `processname=height`, one per line). Activity is logged to
`shyui_log.txt`.

## Build

Requires .NET Framework 4.x (included with Windows; no SDK needed):

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

or manually:

```
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /out:ShyUI.exe /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ShyUI.cs
```

## License

MIT