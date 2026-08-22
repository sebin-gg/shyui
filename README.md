# Shy UI — Window Title Bar Auto-Hider

[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Shy UI is a lightweight Windows tray application that automatically hides the
title bar of maximized windows, giving you a cleaner fullscreen-style view
while keeping the window fully functional. Move your mouse to the top 2px of
the screen to temporarily reveal the title bar.

No SDK required — builds with the .NET Framework compiler bundled with Windows.

## Documentation

- [Changelog](CHANGELOG.md)
- [Contributing](CONTRIBUTING.md)
- [Code of Conduct](CODE_OF_CONDUCT.md)
- [Security](SECURITY.md)
- [License](LICENSE)

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
## 🔒 Security

This repository uses [gitleaks](https://github.com/gitleaks/gitleaks) for automatic secret scanning on every commit.

### Pre-commit Hook

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

### Bypass (Emergency Only)

In case of emergency, you can bypass the hook:

```bash
git commit --no-verify -m "emergency commit"
```

> ⚠️ Only use `--no-verify` in emergency situations. Regular commits should always be scanned.

