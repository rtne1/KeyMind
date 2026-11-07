KeyMind 3.0 — Arabic/English keyboard-layout converter (Windows source package)

INSTALL:
1. Exit any running KeyMind instance from the Windows notification area.
2. Extract to D:\KeyMind-v3.0\KeyMind (do not run from inside the ZIP).
3. Run START-KEYMIND.cmd; it rebuilds KeyMind.exe using the Windows .NET Framework C# compiler.
4. The desktop UI can be closed to the system tray; leave KeyMind running.

EXTERNAL EDITOR TEST (use Notepad first):
English layout: type lpl] followed by Space => should become محمد.
Arabic layout: use physical keys local followed by Space => should become local.
Settings: Engine Enabled and Auto corrections ON.
If it does not work, open the Live diagnostics tab and copy the report. It records
only counters, active app name, layout and status, NOT the words typed.

CHANGES:
- Low-level keyboard hook installed on WinForms UI thread (as in earlier builds).
- Automatically retries hook installation with a null HMODULE if needed.
- Physical Space is delivered normally; corrective key events are injected later.
- Pending rewrite is cancelled when new keys, focus changes or clicks are observed.
- More common English names recognized; still no guarantee for unknown names.

LIMITS:
Windows-only .NET Framework C# source. Compiling or testing on Windows was not
possible here. Protected/elevated apps may reject SendInput; layout recognition
and automatic prediction are not perfect.
Privacy: no typed words saved/uploaded, but low-level system keyboard events are
observed. Pause KeyMind when entering confidential data or passwords; modern
browser password fields may not always be detected automatically.
