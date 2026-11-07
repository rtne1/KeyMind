using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace KeyMind
{
    internal static class WinApi
    {
        public const int WH_KEYBOARD_LL = 13, WH_MOUSE_LL = 14;
        public const int WM_KEYDOWN = 0x100, WM_KEYUP = 0x101, WM_SYSKEYDOWN = 0x104, WM_SYSKEYUP = 0x105;
        public const uint KEYEVENTF_KEYUP = 0x0002, KEYEVENTF_UNICODE = 0x0004;
        public const int VK_SPACE = 0x20, VK_BACK = 0x08, VK_TAB = 0x09, VK_RETURN = 0x0D;
        public const int VK_LCONTROL = 0xA2, VK_RCONTROL = 0xA3, VK_CONTROL = 0x11;
        public const int VK_LMENU = 0xA4, VK_RMENU = 0xA5, VK_MENU = 0x12;
        public const int VK_LSHIFT = 0xA0, VK_RSHIFT = 0xA1, VK_SHIFT = 0x10;
        public const int VK_LWIN = 0x5B, VK_RWIN = 0x5C, VK_ESCAPE = 0x1B;
        public const int VK_CAPITAL = 0x14, VK_DELETE = 0x2E;
        public const int VK_LEFT = 0x25, VK_UP = 0x26, VK_RIGHT = 0x27, VK_DOWN = 0x28;
        public const int VK_HOME = 0x24, VK_END = 0x23, VK_INSERT = 0x2D;
        public const int VK_PRIOR = 0x21, VK_NEXT = 0x22;
        public const int GWL_STYLE = -16;
        public const uint WM_QUIT = 0x0012, WM_KEYMIND_REHOOK = 0x8374;
        public const long INPUT_TAG = 0x4B45594D;

        public delegate IntPtr HookProcedure(int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, HookProcedure lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool PostThreadMessage(uint idThread, uint msg, UIntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint min, uint max);
        [DllImport("user32.dll")]
        public static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint min, uint max, uint remove);
        [DllImport("user32.dll")]
        public static extern bool TranslateMessage(ref MSG msg);
        [DllImport("user32.dll")]
        public static extern IntPtr DispatchMessage(ref MSG msg);
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        [DllImport("user32.dll")]
        public static extern IntPtr GetKeyboardLayout(uint idThread);
        [DllImport("user32.dll")]
        public static extern short GetKeyState(int nVirtKey);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW", ExactSpelling = true)]
        public static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO info);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int x, y; }
        [StructLayout(LayoutKind.Sequential)]
        public struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public UIntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
            public uint lPrivate;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT
        {
            public int x, y;
            public uint mouseData, flags, time;
            public UIntPtr dwExtraInfo;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct GUITHREADINFO
        {
            public uint cbSize;
            public uint flags;
            public IntPtr hwndActive, hwndFocus, hwndCapture, hwndMenuOwner, hwndMoveSize, hwndCaret;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public uint type;
            public INPUTUNION data;
        }
        [StructLayout(LayoutKind.Explicit)]
        public struct INPUTUNION
        {
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public MOUSEINPUT mi; // Native INPUT union size/alignment: 40 bytes on x64.
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx, dy;
            public uint mouseData, dwFlags, time;
            public UIntPtr dwExtraInfo;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }
        public static INPUT MakeKey(ushort vk, ushort scan, uint flags)
        {
            INPUT input = new INPUT();
            input.type = 1;
            input.data = new INPUTUNION();
            input.data.ki = new KEYBDINPUT { wVk = vk, wScan = scan, dwFlags = flags, time = 0, dwExtraInfo = new UIntPtr((uint)INPUT_TAG) };
            return input;
        }
    }

    public sealed class KeyboardEngine : IDisposable
    {
        private readonly Control _owner;
        private readonly WordConverter _converter;
        private readonly KeyMindSettings _settings;
        private readonly WinApi.HookProcedure _keyboardProc;
        private readonly WinApi.HookProcedure _mouseProc;
        private IntPtr _keyboardHook;
        private IntPtr _mouseHook;
        // Keep Windows hooks on a separate pumped thread, never the UI thread.
        private readonly Thread _hookThread;
        private readonly ManualResetEvent _hookReady = new ManualResetEvent(false);
        private uint _hookThreadId;
        private System.Threading.Timer _recoveryTimer;
        private int _hookRecoveries;
        private long _lastKeyTick;
        private int _inputGeneration;
        private volatile bool _disposed;
        private int _seenKeys, _seenSpaces, _proposals, _applied, _failures, _skipped;
        private volatile string _lastStatus = "لم تبدأ المراقبة بعد";
        private volatile string _lastLanguage = "غير معروف";
        private volatile string _lastApp = "غير معروف";
        private readonly StringBuilder _token = new StringBuilder();
        private readonly StringBuilder _physicalKeys = new StringBuilder();
        private bool _physicalReliable = true;
        private readonly uint _selfPid = (uint)Process.GetCurrentProcess().Id;
        private bool _arabic;
        private IntPtr _foreground = IntPtr.Zero, _focused = IntPtr.Zero;
        private bool _ctrl, _alt, _shift;
        private uint _cachedPid = 0;
        private string _cachedProcessName = "";
        private string _pending = "";
        private UndoRecord _undo;
        public bool IsHookActive { get { return _keyboardHook != IntPtr.Zero; } }
        public event Action<string, string> Converted;
        public event Action<string> HookError;
        // Diagnostic information contains counters and app name ONLY. Never raw input.
        public string DiagnosticSnapshot()
        {
            return "Hook (background thread): " + (IsHookActive ? "registered" : "NOT INSTALLED") +
                "   |   Input: " + (_settings.Enabled ? "enabled" : "paused") +
                "   |   Auto: " + (_settings.AutoMode ? "on" : "off") +
                "   |   Confidence: " + _settings.Threshold + "%" + Environment.NewLine +
                "Physical keys: " + Interlocked.CompareExchange(ref _seenKeys, 0, 0) +
                "   |   Spaces: " + Interlocked.CompareExchange(ref _seenSpaces, 0, 0) +
                "   |   Candidates: " + Interlocked.CompareExchange(ref _proposals, 0, 0) +
                "   |   Replacements: " + Interlocked.CompareExchange(ref _applied, 0, 0) +
                "   |   Skipped: " + Interlocked.CompareExchange(ref _skipped, 0, 0) +
                "   |   Failures: " + Interlocked.CompareExchange(ref _failures, 0, 0) + Environment.NewLine +
                "Listener refreshes: " + Interlocked.CompareExchange(ref _hookRecoveries, 0, 0) +
                "   |   Last key: " + LastKeyAge() + Environment.NewLine +
                "Active app: " + _lastApp + "   |   Layout: " + _lastLanguage + Environment.NewLine +
                "Last action: " + _lastStatus;
        }

        private sealed class UndoRecord
        {
            public string Original, Inserted;
            public IntPtr Window, Focus;
        }

        public KeyboardEngine(Control owner, WordConverter converter, KeyMindSettings settings)
        {
            _owner = owner;
            _converter = converter;
            _settings = settings;
            _keyboardProc = KeyboardCallback;
            _mouseProc = MouseCallback;
            _hookThread = new Thread(HookThreadMain);
            _hookThread.Name = "KeyMind Windows keyboard listener";
            _hookThread.IsBackground = true;
            _hookThread.SetApartmentState(ApartmentState.STA);
            _hookThread.Start();
            if (!_hookReady.WaitOne(5000)) _lastStatus = "Listener thread did not initialize";
        }

        private string LastKeyAge()
        {
            long last = Interlocked.Read(ref _lastKeyTick);
            if (last == 0) return "none observed";
            return Math.Max(0, (Stopwatch.GetTimestamp() - last) / Stopwatch.Frequency) + "s ago";
        }

        // A Windows low-level hook may be silently removed after a timeout.
        // The dedicated thread handles callbacks and periodically refreshes registration.
        private void HookThreadMain()
        {
            try
            {
                _hookThreadId = WinApi.GetCurrentThreadId();
                WinApi.MSG message;
                WinApi.PeekMessage(out message, IntPtr.Zero, 0, 0, 0); // establish message queue
                InstallHooks();
                _hookReady.Set();
                _recoveryTimer = new System.Threading.Timer(delegate(object state)
                {
                    uint id = _hookThreadId;
                    if (!_disposed && id != 0)
                        WinApi.PostThreadMessage(id, WinApi.WM_KEYMIND_REHOOK, UIntPtr.Zero, IntPtr.Zero);
                }, null, 20000, 20000);
                int status = 0;
                while (!_disposed && (status = WinApi.GetMessage(out message, IntPtr.Zero, 0, 0)) > 0)
                {
                    if (message.message == WinApi.WM_KEYMIND_REHOOK)
                    {
                        InstallHooks();
                        Interlocked.Increment(ref _hookRecoveries);
                        continue;
                    }
                    WinApi.TranslateMessage(ref message);
                    WinApi.DispatchMessage(ref message);
                }
                if (!_disposed && status == -1) _lastStatus = "Listener message loop stopped unexpectedly";
            }
            catch (Exception ex)
            {
                _lastStatus = "Listener thread error: " + ex.GetType().Name;
                Program.LogError(ex);
            }
            finally
            {
                _hookReady.Set();
                if (_recoveryTimer != null) { _recoveryTimer.Dispose(); _recoveryTimer = null; }
                RemoveHooks();
                _hookThreadId = 0;
            }
        }

        public bool RestartListener()
        {
            uint id = _hookThreadId;
            if (_disposed || id == 0) return false;
            bool posted = WinApi.PostThreadMessage(id, WinApi.WM_KEYMIND_REHOOK, UIntPtr.Zero, IntPtr.Zero);
            if (posted) _lastStatus = "Listener restart requested";
            return posted;
        }

        private void RemoveHooks()
        {
            if (_keyboardHook != IntPtr.Zero)
            {
                WinApi.UnhookWindowsHookEx(_keyboardHook);
                _keyboardHook = IntPtr.Zero;
            }
            if (_mouseHook != IntPtr.Zero)
            {
                WinApi.UnhookWindowsHookEx(_mouseHook);
                _mouseHook = IntPtr.Zero;
            }
        }

        private void InstallHooks()
        {
            // Install / refresh the Windows low-level hooks on the dedicated message thread.
            // This permits UI repaint, settings IO and dictionary evaluation without timing out hooks.
            try
            {
                RemoveHooks();
                IntPtr module = IntPtr.Zero;
                try
                {
                    using (var process = Process.GetCurrentProcess())
                    using (var mod = process.MainModule)
                        module = WinApi.GetModuleHandle(mod.ModuleName);
                }
                catch { module = IntPtr.Zero; }
                _keyboardHook = WinApi.SetWindowsHookEx(WinApi.WH_KEYBOARD_LL, _keyboardProc, module, 0);
                // WH_KEYBOARD_LL does not inject a DLL into other processes.
                // Retry without a module handle when strict Windows policies reject it.
                if (_keyboardHook == IntPtr.Zero && module != IntPtr.Zero)
                    _keyboardHook = WinApi.SetWindowsHookEx(WinApi.WH_KEYBOARD_LL, _keyboardProc, IntPtr.Zero, 0);
                if (_keyboardHook == IntPtr.Zero)
                {
                    _lastStatus = "Keyboard hook installation error: " + Marshal.GetLastWin32Error();
                    if (HookError != null) HookError(_lastStatus);
                    return;
                }
                _lastStatus = "Listener active on dedicated background message loop";
                _mouseHook = WinApi.SetWindowsHookEx(WinApi.WH_MOUSE_LL, _mouseProc, module, 0);
            }
            catch (Exception ex)
            {
                _lastStatus = "Keyboard listener failed: " + ex.GetType().Name;
                Program.LogError(ex);
            }
        }

        private IntPtr MouseCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = unchecked((int)wParam.ToInt64());
                if (msg == 0x201 || msg == 0x204 || msg == 0x207 || msg == 0x20A || msg == 0x20E)
                {
                    Interlocked.Increment(ref _inputGeneration);
                    Reset();
                }
            }
            return WinApi.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        private IntPtr KeyboardCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0) return WinApi.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
            try
            {
                int message = unchecked((int)wParam.ToInt64());
                bool down = message == WinApi.WM_KEYDOWN || message == WinApi.WM_SYSKEYDOWN;
                bool up = message == WinApi.WM_KEYUP || message == WinApi.WM_SYSKEYUP;
                if (!down && !up) return WinApi.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
                var key = (WinApi.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(WinApi.KBDLLHOOKSTRUCT));
                // Ignore ONLY events we injected ourselves. Some third-party keyboard
                // drivers/remappers forward real user input as LLKHF_INJECTED events.
                if (key.dwExtraInfo.ToUInt64() == (ulong)WinApi.INPUT_TAG)
                    return WinApi.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
                int vk = (int)key.vkCode;
                if (down)
                {
                    Interlocked.Increment(ref _seenKeys);
                    Interlocked.Exchange(ref _lastKeyTick, Stopwatch.GetTimestamp());
                    Interlocked.Increment(ref _inputGeneration);
                }
                if (IsModifier(vk))
                {
                    TrackModifier(vk, down);
                    // Shift is allowed in shortcuts; Arabic shifted symbols are handled below.
                    if (up && !_ctrl && !_alt && _pending.Length > 0)
                    {
                        string action = _pending;
                        _pending = "";
                        // Snapshot hook-owned state BEFORE crossing threads.
                        string original = _token.ToString();
                        string pendingPhysical = _physicalReliable ? _physicalKeys.ToString() : null;
                        bool sourceArabic = _arabic;
                        IntPtr window = _foreground, pendingFocus = _focused;
                        _token.Clear();
                        _physicalKeys.Clear();
                        if (!_owner.IsDisposed && _owner.IsHandleCreated)
                            _owner.BeginInvoke((Action)delegate { PerformPending(action, original, pendingPhysical, sourceArabic, window, pendingFocus); });
                    }
                    return WinApi.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
                }
                if (!down) return WinApi.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
                // Global pause shortcut must work even while paused.
                if (_ctrl && _alt && !_shift && vk == 0x50)
                {
                    _settings.Enabled = !_settings.Enabled;
                    if (!_owner.IsDisposed && _owner.IsHandleCreated)
                        _owner.BeginInvoke((Action)delegate { SettingsStore.Save(_settings); });
                    Reset();
                    return new IntPtr(1);
                }
                if (!_settings.Enabled) { Reset(); return WinApi.CallNextHookEx(_keyboardHook, nCode, wParam, lParam); }
                // Explicit shortcuts. Replacement is performed AFTER modifiers have been released.

                if (_ctrl && _alt && vk == WinApi.VK_SPACE)
                {
                    _pending = _shift ? "force" : "manual";
                    return new IntPtr(1);
                }
                if (_ctrl && _alt && !_shift && vk == 0x5A)
                {
                    _pending = "undo";
                    return new IntPtr(1);
                }
                if (_ctrl || _alt || (WinApi.GetKeyState(WinApi.VK_LWIN) & 0x8000) != 0 || (WinApi.GetKeyState(WinApi.VK_RWIN) & 0x8000) != 0)
                {
                    // Shift is not interpreted: this prevents transforming code syntax and diacritic input.
                    Reset();
                    return WinApi.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
                }

                IntPtr foreground = WinApi.GetForegroundWindow();
                if (foreground == IntPtr.Zero) { Reset(); return WinApi.CallNextHookEx(_keyboardHook, nCode, wParam, lParam); }
                uint pid;
                uint thread = WinApi.GetWindowThreadProcessId(foreground, out pid);
                if (pid == _selfPid) { Reset(); return WinApi.CallNextHookEx(_keyboardHook, nCode, wParam, lParam); }
                IntPtr focus = FocusedHandle(thread);
                // Chromium can momentarily report a null focus handle. Do not
                // clear a partially typed word unless a REAL focus change occurs.
                if (_foreground != foreground || (focus != IntPtr.Zero && _focused != IntPtr.Zero && _focused != focus))
                {
                    _foreground = foreground;
                    _focused = focus;
                    Reset();
                }
                if (focus != IntPtr.Zero) _focused = focus;
                if (SensitiveField(focus, pid))
                {
                    _lastStatus = "Input field excluded or app identification failed";
                    Reset();
                    return WinApi.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
                }
                // Some multi-process editors host their focused control on a
                // different GUI thread from the outer window.
                if (focus != IntPtr.Zero)
                {
                    uint focusPid;
                    uint focusThread = WinApi.GetWindowThreadProcessId(focus, out focusPid);
                    if (focusThread != 0 && focusPid == pid) thread = focusThread;
                }
                long hkl = WinApi.GetKeyboardLayout(thread).ToInt64();
                int primaryLang = (int)(hkl & 0x3ff);
                _lastLanguage = primaryLang == 0x01 ? "Arabic" : primaryLang == 0x09 ? "English" : "Other (0x" + primaryLang.ToString("X") + ")";
                if (primaryLang != 0x01 && primaryLang != 0x09)
                {
                    _lastStatus = "Unsupported keyboard layout (Arabic/English only)";
                    Reset();
                    return WinApi.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
                }
                bool isArabic = primaryLang == 0x01;
                if (_token.Length > 0 && _arabic != isArabic) Reset();
                _arabic = isArabic;

                if (vk == WinApi.VK_SPACE)
                {
                    Interlocked.Increment(ref _seenSpaces);
                    if (_settings.AutoMode && _token.Length >= 3)
                    {
                        string before = _token.ToString();
                        string physicalAtSpace = _physicalReliable ? _physicalKeys.ToString() : null;
                        bool sourceArabic = _arabic;
                        IntPtr capturedWindow = foreground, capturedFocus = focus;
                        int generation = Interlocked.CompareExchange(ref _inputGeneration, 0, 0);
                        // The global hook must NEVER calculate dictionary confidence;
                        // queue it to the UI thread without holding the Windows input pipeline.
                        try
                        {
                            if (!_owner.IsDisposed && _owner.IsHandleCreated)
                                _owner.BeginInvoke((Action)delegate
                                {
                                    if (_disposed || !_settings.Enabled || !_settings.AutoMode) return;
                                    if (generation != Interlocked.CompareExchange(ref _inputGeneration, 0, 0)) return;
                                    if (WinApi.GetForegroundWindow() != capturedWindow) return;
                                    try
                                    {
                                        var proposal = _converter.EvaluatePhysical(before, sourceArabic, _settings.Threshold, false, physicalAtSpace);
                                        if (proposal.Accepted)
                                        {
                                            Interlocked.Increment(ref _proposals);
                                            _lastStatus = "Correction scheduled after space";
                                            QueueCorrection(before, proposal.Converted, capturedWindow, capturedFocus, generation);
                                        }
                                        else
                                        {
                                            Interlocked.Increment(ref _skipped);
                                            _lastStatus = "Space checked: " + proposal.Reason + " (confidence " + proposal.Confidence + "%)";
                                        }
                                    }
                                    catch (Exception ex) { Program.LogError(ex); Interlocked.Increment(ref _failures); }
                                });
                        }
                        catch (Exception ex)
                        {
                            _lastStatus = "Could not schedule correction";
                            Interlocked.Increment(ref _failures);
                            Program.LogError(ex);
                        }
                    }
                    else
                    {
                        _lastStatus = !_settings.AutoMode ? "Auto mode disabled" : "Space detected; word shorter than 3 keys";
                    }
                    _token.Clear();
                    _physicalKeys.Clear();
                    _physicalReliable = true;
                    _undo = null;
                    return WinApi.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
                }
                if (vk == WinApi.VK_BACK)
                {
                    if (_token.Length > 0)
                    {
                        bool ligature = _arabic && _physicalKeys.Length > 0 &&
                            _physicalKeys[_physicalKeys.Length - 1] == 'b' && _token.ToString().EndsWith("لا");
                        _token.Length--;
                        if (_physicalKeys.Length > 0) _physicalKeys.Length--;
                        if (ligature && _token.Length > 0) _token.Length--;
                    }
                    _undo = null;
                    return WinApi.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
                }
                if (vk == WinApi.VK_RETURN || vk == WinApi.VK_TAB || vk == WinApi.VK_ESCAPE || vk == WinApi.VK_DELETE ||
                    vk == WinApi.VK_LEFT || vk == WinApi.VK_RIGHT || vk == WinApi.VK_UP || vk == WinApi.VK_DOWN ||
                    vk == WinApi.VK_HOME || vk == WinApi.VK_END || vk == WinApi.VK_PRIOR || vk == WinApi.VK_NEXT || vk == WinApi.VK_INSERT)
                {
                    Reset();
                    return WinApi.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
                }
                if (_shift && _arabic) { Reset(); return WinApi.CallNextHookEx(_keyboardHook, nCode, wParam, lParam); }
                // English layout: Shift + ; ' , . / [ ] ` types : " < > ? { } ~ - a symbol, not the physical key.
                // Treat it as a word boundary so it can never be mistaken for an Arabic letter key.
                if (_shift && !_arabic && WordConverter.IsPunctuationKey((uint)vk)) { Reset(); return WinApi.CallNextHookEx(_keyboardHook, nCode, wParam, lParam); }
                string ch = WordConverter.CharacterForKey((uint)vk, _arabic);
                if (ch == null)
                {
                    Reset();
                    return WinApi.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
                }
                _undo = null;
                // Each input element is transient, never written to a file or log.
                if (_token.Length + ch.Length > 40) { _token.Clear(); _physicalKeys.Clear(); _physicalReliable = true; }
                _token.Append(ch);
                string physical = WordConverter.CharacterForKey((uint)vk, false);
                if (physical != null) _physicalKeys.Append(physical);
            }
            catch (Exception ex)
            {
                Reset();
                Program.LogError(new Exception("Keyboard hook callback error (no typed content logged)", ex));
            }
            return WinApi.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        }

        private static bool IsModifier(int vk)
        {
            return vk == WinApi.VK_CONTROL || vk == WinApi.VK_LCONTROL || vk == WinApi.VK_RCONTROL ||
                vk == WinApi.VK_MENU || vk == WinApi.VK_LMENU || vk == WinApi.VK_RMENU ||
                vk == WinApi.VK_SHIFT || vk == WinApi.VK_LSHIFT || vk == WinApi.VK_RSHIFT;
        }
        private void TrackModifier(int vk, bool down)
        {
            if (vk == WinApi.VK_CONTROL || vk == WinApi.VK_LCONTROL || vk == WinApi.VK_RCONTROL) _ctrl = down;
            if (vk == WinApi.VK_MENU || vk == WinApi.VK_LMENU || vk == WinApi.VK_RMENU) _alt = down;
            if (vk == WinApi.VK_SHIFT || vk == WinApi.VK_LSHIFT || vk == WinApi.VK_RSHIFT) _shift = down;
        }
        private void PerformPending(string action, string before, string physical, bool sourceArabic, IntPtr window, IntPtr focus)
        {
            if (!_settings.Enabled || _owner.IsDisposed) return;
            IntPtr foreground = WinApi.GetForegroundWindow();
            if (foreground != window) return;
            if (action == "manual" || action == "force")
            {
                if (before.Length < (action == "force" ? 1 : 3)) return;
                var p = _converter.EvaluatePhysical(before, sourceArabic, _settings.Threshold, action == "force", physical);
                if (!p.Accepted) return;
                if (Replace(before, p.Converted, ""))
                {
                    _undo = new UndoRecord { Original = before, Inserted = p.Converted, Window = window, Focus = focus };
                    if (Converted != null) Converted(before, p.Converted);
                    _lastStatus = "Manual correction applied";
                }
            }
            else if (action == "undo" && _undo != null)
            {
                if (_undo.Window == foreground)
                    Replace(_undo.Inserted, _undo.Original, "");
                _undo = null;
            }
        }

        private void QueueCorrection(string original, string corrected, IntPtr window, IntPtr focus, int generation)
        {
            try
            {
                if (_owner.IsDisposed || !_owner.IsHandleCreated)
                {
                    Interlocked.Increment(ref _failures);
                    _lastStatus = "Correction UI message loop unavailable";
                    return;
                }
                // BeginInvoke alone can run before Chrome renders the pending space;
                // a short UI timer gives the target input queue time to settle.
                _owner.BeginInvoke((Action)delegate
                {
                    if (_disposed || _owner.IsDisposed) return;
                    System.Windows.Forms.Timer deferred = new System.Windows.Forms.Timer();
                    deferred.Interval = 45;
                    deferred.Tick += delegate(object sender, EventArgs args)
                    {
                        deferred.Stop();
                        deferred.Dispose();
                        if (_disposed || !_settings.Enabled ||
                            generation != Interlocked.CompareExchange(ref _inputGeneration, 0, 0) ||
                            WinApi.GetForegroundWindow() != window)
                        {
                            Interlocked.Increment(ref _skipped);
                            _lastStatus = "Correction cancelled: focus or text changed";
                            return;
                        }
                        uint targetPid;
                        uint targetThread = WinApi.GetWindowThreadProcessId(window, out targetPid);
                        IntPtr currentFocus = FocusedHandle(targetThread);
                        if (focus != IntPtr.Zero && currentFocus != IntPtr.Zero && currentFocus != focus)
                        {
                            Interlocked.Increment(ref _skipped);
                            _lastStatus = "Correction cancelled: typing field changed";
                            return;
                        }
                        // Original space is already inserted. Delete the full word
                        // plus that space, then inject the corrected word + space.
                        if (Replace(original + " ", corrected + " ", ""))
                        {
                            _undo = new UndoRecord {
                                Original = original + " ", Inserted = corrected + " ",
                                Window = window, Focus = focus
                            };
                            Interlocked.Increment(ref _applied);
                            _lastStatus = "External correction applied";
                            if (Converted != null) Converted(original, corrected);
                        }
                        else
                        {
                            // Never erase a separator on an injection failure.
                            Interlocked.Increment(ref _failures);
                            _lastStatus = "SendInput failed (Win32 " + Marshal.GetLastWin32Error() + "); check app privilege";
                        }
                    };
                    deferred.Start();
                });
            }
            catch (Exception ex)
            {
                Program.LogError(ex);
                Interlocked.Increment(ref _failures);
                _lastStatus = "Could not schedule external correction";
            }
        }

        private static bool InsertText(string value)
        {
            var items = new System.Collections.Generic.List<WinApi.INPUT>();
            foreach (char c in value)
            {
                items.Add(WinApi.MakeKey(0, c, WinApi.KEYEVENTF_UNICODE));
                items.Add(WinApi.MakeKey(0, c, WinApi.KEYEVENTF_UNICODE | WinApi.KEYEVENTF_KEYUP));
            }
            WinApi.INPUT[] events = items.ToArray();
            return events.Length > 0 && WinApi.SendInput((uint)events.Length, events, Marshal.SizeOf(typeof(WinApi.INPUT))) == events.Length;
        }

        private static bool Replace(string original, string converted, string suffix)
        {
            if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(converted)) return false;
            var keys = new System.Collections.Generic.List<WinApi.INPUT>();
            // Backspace one UTF-16 code unit per basic Arabic/English character.
            foreach (char ignored in original)
            {
                keys.Add(WinApi.MakeKey(WinApi.VK_BACK, 0, 0));
                keys.Add(WinApi.MakeKey(WinApi.VK_BACK, 0, WinApi.KEYEVENTF_KEYUP));
            }
            foreach (char c in converted + suffix)
            {
                keys.Add(WinApi.MakeKey(0, c, WinApi.KEYEVENTF_UNICODE));
                keys.Add(WinApi.MakeKey(0, c, WinApi.KEYEVENTF_UNICODE | WinApi.KEYEVENTF_KEYUP));
            }
            WinApi.INPUT[] arr = keys.ToArray();
            return WinApi.SendInput((uint)arr.Length, arr, Marshal.SizeOf(typeof(WinApi.INPUT))) == arr.Length;
        }

        private static IntPtr FocusedHandle(uint thread)
        {
            WinApi.GUITHREADINFO info = new WinApi.GUITHREADINFO();
            info.cbSize = (uint)Marshal.SizeOf(typeof(WinApi.GUITHREADINFO));
            return WinApi.GetGUIThreadInfo(thread, ref info) ? info.hwndFocus : IntPtr.Zero;
        }

        private bool SensitiveField(IntPtr focus, uint pid)
        {
            try
            {
                if (focus != IntPtr.Zero)
                {
                    // Win32 ES_PASSWORD; modern Chromium password fields may require additional protections.
                    var b = new StringBuilder(128);
                    WinApi.GetClassName(focus, b, b.Capacity);
                    string className = b.ToString().ToLowerInvariant();
                    if (className.Contains("edit") && (WinApi.GetWindowLong(focus, WinApi.GWL_STYLE) & 0x20) != 0) return true;
                }
                // Always identify the active app for non-sensitive diagnostics.
                if (_cachedPid != pid)
                {
                    using (var p = Process.GetProcessById((int)pid))
                        _cachedProcessName = p.ProcessName.ToLowerInvariant();
                    _cachedPid = pid;
                }
                _lastApp = _cachedProcessName;
                string excluded = _settings.ExcludedApps ?? "";
                foreach (var item in excluded.Split(','))
                    if (item.Trim().ToLowerInvariant() == _cachedProcessName) return true;
            }
            catch { return true; }
            return false;
        }

        private void Reset()
        {
            _token.Clear();
            _physicalKeys.Clear();
            _physicalReliable = true;
            _undo = null;
        }

        public void Dispose()
        {
            _disposed = true;
            uint threadId = _hookThreadId;
            if (threadId != 0)
                WinApi.PostThreadMessage(threadId, WinApi.WM_QUIT, UIntPtr.Zero, IntPtr.Zero);
            bool joined = _hookThread == null || !_hookThread.IsAlive || _hookThread.Join(1200);
            // Do not dispose the signal while the listener thread may still call Set().
            if (joined) _hookReady.Dispose();
            // Normal unhooking occurs on the hook owner thread (finally in HookThreadMain).
        }
    }
}
