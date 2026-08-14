# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-08-14

### Added

- Tray application that hides the title bar of maximized windows
- Auto-Shy: any maximized window is managed automatically
- Per-app management via hotkey `Ctrl+Alt+T` and the Settings grid
- Global pause/resume via `Ctrl+Alt+S` or the tray menu
- Configurable per-app top bar height (`shyui_config.txt`)
- "Run at Startup" toggle (HKCU `Run` key)
- Logging to `shyui_log.txt`

### Fixed

- Unhandled timer exceptions no longer terminate the app
- Dead window handles are pruned to prevent handle-reuse mis-management
- Pause now restores non-maximized windows to their prior state
- Config lines containing extra `=` are parsed correctly
- Removed per-tick log spam; loop interval raised from 20ms to 50ms