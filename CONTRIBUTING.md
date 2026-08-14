# Contributing to Shy UI

Thanks for taking the time to contribute! Here's how to help.

## Getting started

1. Fork the repository.
2. Create a feature branch: `git checkout -b feat/my-change`.
3. Make your changes to `ShyUI.cs` or the docs.
4. Build and smoke-test locally (see below).
5. Commit with a clear message and push to your fork.
6. Open a pull request against `main`.

## Development environment

- Windows 10/11, no SDK required.
- Build with the bundled compiler:

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

The output `ShyUI.exe` is a tray application. To test it:

1. Run `ShyUI.exe`.
2. Confirm `shyui_log.txt` appears next to the exe with
   `Registered hotkey Ctrl+Alt+S` and `Registered hotkey Ctrl+Alt+T` lines.
3. Maximize any window; its title bar should hide. Move the mouse to the top
   2px of the screen to reveal it.
4. Test `Ctrl+Alt+S` (pause/resume) and `Ctrl+Alt+T` (add/remove focused app).

> Note: never commit `ShyUI.exe`, `shyui_log.txt` or `shyui_config.txt` —
> they are gitignored.

## Code style

- Keep changes minimal and in the style of the surrounding code.
- Don't add comments unless they explain non-obvious Win32 behavior.
- Test every change against the checklist above.

## Reporting bugs

Open an issue with:

- Windows version
- Steps to reproduce
- The relevant lines from `shyui_log.txt`
- Expected vs. actual behavior

## Licensing

By contributing, you agree that your contributions are licensed under the
[MIT License](LICENSE).