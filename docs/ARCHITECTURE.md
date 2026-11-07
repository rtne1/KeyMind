# Architecture overview

KeyMind is a Windows desktop application built with WinForms and C#.

## Main components

- `src/Program.cs` — application startup, single-instance protection and local error logging.
- `src/MainForm.cs` — WinForms interface, settings, diagnostics and tray behavior.
- `src/KeyboardEngine.cs` — keyboard hook, physical-key tracking, listener lifecycle and Windows input rewriting.
- `src/WordConverter.cs` — layout mapping and candidate scoring.
- `src/Lexicon.cs` — dictionary loading, normalization, lookup and compact `.kmd` support.
- `src/SettingsStore.cs` — local settings, excluded apps and optional startup registration.

## Conceptual flow

```text
Physical keyboard input
          |
Windows low-level hook / listener
          |
Word boundary and layout detection
          |
Candidate conversion + dictionary scoring
          |
Threshold and context checks
          |
Optional injected replacement (SendInput)
          |
Target application / editor
```

The Windows hook observes key events globally, while correction uses simulated input. Input can change between detection and replacement; the implementation attempts to reject stale corrections when additional keys or focus changes occur, but real-world applications differ. See `SECURITY.md`.

## Build routes

1. Legacy `Build-KeyMind.cmd` uses the Windows .NET Framework C# compiler and emits `KeyMind.exe` in the root directory.
2. `KeyMind.csproj` targets `net10.0-windows` using WinForms, producing output in `bin/`.

These two paths have different runtime requirements. Build and test the route you intend to distribute.
