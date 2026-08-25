# Shy UI: window title bar auto-hider

[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Shy UI is a lightweight Windows tray application that automatically hides the
title bar of maximized windows, giving you a cleaner fullscreen-style view
while keeping the window fully functional. Move your mouse to the top 2px of
the screen to temporarily reveal the title bar.

## Documentation

- [Changelog](CHANGELOG.md)
- [Contributing](CONTRIBUTING.md)
- [Code of Conduct](CODE_OF_CONDUCT.md)
- [Security](SECURITY.md)
- [License](LICENSE)

## How it works

A hidden form registers the global hotkeys, and a timer watches the foreground
window every 50 ms. When a window is maximized, Shy UI restores it and resizes
it to fill the monitor work area plus one extra strip at the top (40px by
default), which places its title bar just off-screen. The window stays fully
interactive the whole time.

Touch the top 2px of the screen with the mouse to slide the title bar back
down. Move the mouse below the bar to hide it again. Each tick prunes dead
window handles from its tracking lists, so Shy UI never touches a handle that
another window now owns.

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

The tray menu offers Settings, Pause (Ctrl+Alt+S), Run at Startup, and Exit.
Adding or removing an app pops a balloon tip, and Exit asks for confirmation.
The Settings window shows a grid where you edit each managed app's process
name and top bar height, then save with one click.

## Configuration

Configuration is stored in `shyui_config.txt` next to the executable
(format: `processname=height`, one per line). Activity is logged to
`shyui_log.txt`. Example:

```text
notepad=40
chrome=32
```

Process names match lowercase, and a line may contain extra `=` characters;
everything after the first `=` is parsed as the height.

## Prerequisites

- Windows. Shy UI calls the Win32 API directly and uses Windows Forms.
- .NET Framework 4.x, which is included with Windows. No SDK or extra install
  is required.

## Build

Requires .NET Framework 4.x (included with Windows; no SDK needed):

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

or manually:

```
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /out:ShyUI.exe /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ShyUI.cs
```

`build.ps1` prefers the 64-bit compiler and falls back to the 32-bit path,
then writes `ShyUI.exe` to the repository root.

## Project structure

```text
ShyUI.cs                    Entire application in a single source file
build.ps1                   Build script that invokes the bundled csc.exe
docs/index.html             Project homepage
.github/workflows/ci.yml    CI workflow
sonar-project.properties    Sonar analysis settings
shyui_config.txt            Created at runtime next to the executable
shyui_log.txt               Runtime activity log
```

## License

MIT

## 🔒 Security

This repository uses [gitleaks](https://github.com/gitleaks/gitleaks) for automatic secret scanning on every commit.

### Pre-commit hook

A pre-commit hook is configured to scan for secrets before each commit. This helps prevent accidentally committing sensitive information like:
- API keys
- Passwords
- Tokens
- Private keys

### Setup

To enable the pre-commit hook locally:

```bash
# Install pre-commit
pip install pre-commit

# Install hooks
pre-commit install
```

### Bypass (emergency only)

In case of emergency, you can bypass the hook:

```bash
git commit --no-verify -m "emergency commit"
```

> ⚠️ Only use `--no-verify` in emergency situations. Regular commits should always be scanned.
