using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace TeacherToolbox.Services
{
    public sealed class ShortcutWatcherManager : IShortcutWatcherService
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;
        private const int WM_QUIT = 0x0012;

        private const int VK_F9 = 0x78;
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;
        private const int VK_D0 = 0x30;
        private const int VK_D9 = 0x39;
        private const int VK_NUMPAD0 = 0x60;
        private const int VK_NUMPAD9 = 0x69;

        private static readonly LowLevelKeyboardProc HookProc = HookCallback;
        private static readonly object InstanceLock = new();
        private static ShortcutWatcherManager _activeInstance;

        private readonly ITelemetryService _telemetry;
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly object _stateLock = new();
        private readonly object _pressedKeysLock = new();
        private readonly HashSet<int> _pressedKeys = new();

        private IntPtr _hookId = IntPtr.Zero;
        private Thread _hookThread;
        private uint _hookThreadId;
        private Timer _failsafeTimer;
#if DEBUG
        private CancellationTokenSource _testListenerCts;
        private Task _testListenerTask;
#endif
        private bool _isRunning;
        private volatile bool _stopRequested;
        private Exception _hookStartException;
        private DateTime _lastWindowsKeyPress = DateTime.MinValue;

        public ShortcutWatcherManager(ITelemetryService telemetry)
        {
            _telemetry = telemetry;
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        }

        public bool IsRunning
        {
            get
            {
                lock (_stateLock)
                {
                    return _isRunning;
                }
            }
        }

        public event EventHandler<ShortcutPressedEventArgs> ShortcutPressed;
        public event EventHandler<WatcherHealthChangedEventArgs> HealthChanged;

        public Task StartAsync(CancellationToken ct = default)
        {
            if (ct.IsCancellationRequested)
            {
                return Task.FromCanceled(ct);
            }

            lock (_stateLock)
            {
                if (_isRunning)
                {
                    return Task.CompletedTask;
                }

                try
                {
                    StartHookThread();

                    _failsafeTimer = new Timer(FailsafeTimerCallback, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
#if DEBUG
                    if (Environment.GetEnvironmentVariable("TEACHER_TOOLBOX_TEST_SHORTCUT_PIPE") == "1")
                    {
                        StartTestListener();
                    }
#endif
                    _isRunning = true;
                    _telemetry.LogInfo($"In-process shortcut listener started. HookId={_hookId}");
                    RaiseHealthChanged(true, "In-process shortcut listener started");
                }
                catch (Exception ex)
                {
                    _isRunning = false;
                    _hookId = IntPtr.Zero;
                    _stopRequested = true;

                    lock (InstanceLock)
                    {
                        if (ReferenceEquals(_activeInstance, this))
                        {
                            _activeInstance = null;
                        }
                    }

                    _telemetry.LogError("Error starting in-process shortcut listener", ex);
                    RaiseHealthChanged(false, "Failed to start in-process shortcut listener");
                }
            }

            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            lock (_stateLock)
            {
                if (!_isRunning && _hookId == IntPtr.Zero)
                {
                    return Task.CompletedTask;
                }

                _isRunning = false;
                _failsafeTimer?.Dispose();
                _failsafeTimer = null;
                StopHookThread();

#if DEBUG
                StopTestListenerAsync().GetAwaiter().GetResult();
#endif

                lock (_pressedKeysLock)
                {
                    _pressedKeys.Clear();
                }

                if (_hookId != IntPtr.Zero)
                {
                    try
                    {
                        if (!UnhookWindowsHookEx(_hookId))
                        {
                            _telemetry.LogWarning($"Failed to unhook keyboard listener. Win32 error: {Marshal.GetLastWin32Error()}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _telemetry.LogWarning("Error unhooking keyboard listener", ex);
                    }
                    finally
                    {
                        _hookId = IntPtr.Zero;
                    }
                }

                lock (InstanceLock)
                {
                    if (ReferenceEquals(_activeInstance, this))
                    {
                        _activeInstance = null;
                    }
                }
            }

            RaiseHealthChanged(false, "In-process shortcut listener stopped");
            return Task.CompletedTask;
        }

#if DEBUG
        private void StartTestListener()
        {
            if (_testListenerTask != null && !_testListenerTask.IsCompleted)
            {
                return;
            }

            _testListenerCts = new CancellationTokenSource();
            _testListenerTask = ListenForTestShortcutMessagesAsync(_testListenerCts.Token);
        }

        private async Task StopTestListenerAsync()
        {
            if (_testListenerCts == null)
            {
                return;
            }

            await _testListenerCts.CancelAsync();
            if (_testListenerTask != null)
            {
                await Task.WhenAny(_testListenerTask, Task.Delay(1000));
            }

            _testListenerCts.Dispose();
            _testListenerCts = null;
            _testListenerTask = null;
        }

        private async Task ListenForTestShortcutMessagesAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using var testPipe = new NamedPipeServerStream(
                        "TeacherToolboxShortcutTest",
                        PipeDirection.In,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await testPipe.WaitForConnectionAsync(cancellationToken);
                    using var reader = new StreamReader(testPipe);
                    var message = await reader.ReadLineAsync();
                    if (TryParseShortcutMessage(message, out var args))
                    {
                        RaiseShortcutPressed(args);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _telemetry.LogWarning("Test shortcut pipe listener stopped", ex);
                    break;
                }
            }
        }
#endif

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
        }

        private void StartHookThread()
        {
            _stopRequested = false;
            _hookStartException = null;

            using var hookStarted = new ManualResetEventSlim(false);

            _hookThread = new Thread(() => HookThreadLoop(hookStarted))
            {
                IsBackground = true,
                Name = "TeacherToolbox shortcut hook"
            };
            _hookThread.SetApartmentState(ApartmentState.STA);
            _hookThread.Start();

            if (!hookStarted.Wait(TimeSpan.FromSeconds(5)))
            {
                _stopRequested = true;
                throw new TimeoutException("Timed out starting the shortcut hook thread.");
            }

            if (_hookStartException != null)
            {
                throw _hookStartException;
            }
        }

        private void StopHookThread()
        {
            _stopRequested = true;

            if (_hookThreadId != 0)
            {
                PostThreadMessage(_hookThreadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            }

            if (_hookThread != null && _hookThread.IsAlive && !_hookThread.Join(TimeSpan.FromSeconds(2)))
            {
                _telemetry.LogWarning("Shortcut hook thread did not stop within the expected timeout.");
            }

            _hookThread = null;
            _hookThreadId = 0;
        }

        private void HookThreadLoop(ManualResetEventSlim hookStarted)
        {
            try
            {
                _hookThreadId = GetCurrentThreadId();

                lock (InstanceLock)
                {
                    _activeInstance = this;
                }

                _hookId = SetHook(HookProc);
                if (_hookId == IntPtr.Zero)
                {
                    throw new InvalidOperationException($"Unable to set keyboard hook. Win32 error: {Marshal.GetLastWin32Error()}");
                }

                hookStarted.Set();

                while (!_stopRequested && GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
                {
                    TranslateMessage(ref message);
                    DispatchMessage(ref message);
                }
            }
            catch (Exception ex)
            {
                _hookStartException = ex;
                hookStarted.Set();
                _telemetry.LogError("Shortcut hook thread failed", ex);
            }
            finally
            {
                if (_hookId != IntPtr.Zero)
                {
                    try
                    {
                        if (!UnhookWindowsHookEx(_hookId))
                        {
                            _telemetry.LogWarning($"Failed to unhook keyboard listener. Win32 error: {Marshal.GetLastWin32Error()}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _telemetry.LogWarning("Error unhooking keyboard listener", ex);
                    }
                    finally
                    {
                        _hookId = IntPtr.Zero;
                    }
                }

                lock (InstanceLock)
                {
                    if (ReferenceEquals(_activeInstance, this))
                    {
                        _activeInstance = null;
                    }
                }
            }
        }

        internal static bool TryParseShortcutMessage(string message, out ShortcutPressedEventArgs args)
        {
            args = null;

            if (string.IsNullOrWhiteSpace(message) ||
                message.StartsWith("STARTUP:", StringComparison.Ordinal) ||
                string.Equals(message, "ALIVE", StringComparison.Ordinal))
            {
                return false;
            }

            if (message.StartsWith("D", StringComparison.Ordinal) &&
                int.TryParse(message.Substring(1), out int number) &&
                number >= 0 &&
                number <= 9)
            {
                args = CreateTimerShortcutArgs(number, message);
                return true;
            }

            if (string.Equals(message, "F9", StringComparison.Ordinal))
            {
                args = CreateRandomNameShortcutArgs(message);
                return true;
            }

            return false;
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            ShortcutWatcherManager instance;
            lock (InstanceLock)
            {
                instance = _activeInstance;
            }

            if (instance == null || nCode < 0)
            {
                return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
            }

            return instance.HandleKeyboardEvent(nCode, wParam, lParam);
        }

        private IntPtr HandleKeyboardEvent(int nCode, IntPtr wParam, IntPtr lParam)
        {
            var vkCode = Marshal.ReadInt32(lParam);
            var isKeyDown = wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN;
            var isKeyUp = wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP;

            if (!isKeyDown && !isKeyUp)
            {
                return CallNextHookEx(_hookId, nCode, wParam, lParam);
            }

            lock (_pressedKeysLock)
            {
                if (isKeyUp)
                {
                    _pressedKeys.Remove(vkCode);

                    return CallNextHookEx(_hookId, nCode, wParam, lParam);
                }

                if (TryGetShortcutNumber(vkCode, out var numberFromPressedKey) && IsWindowsKeyPhysicallyDown())
                {
                    if (_pressedKeys.Add(vkCode))
                    {
                        RaiseShortcutPressed(CreateTimerShortcutArgs(numberFromPressedKey, $"D{numberFromPressedKey}"));
                    }

                    return (IntPtr)1;
                }

                if (!_pressedKeys.Add(vkCode))
                {
                    return CallNextHookEx(_hookId, nCode, wParam, lParam);
                }

                if (IsWindowsKey(vkCode))
                {
                    _lastWindowsKeyPress = DateTime.Now;

                    if (TryGetPhysicallyPressedShortcutNumber(out var numberFromHeldKey))
                    {
                        RaiseShortcutPressed(CreateTimerShortcutArgs(numberFromHeldKey, $"D{numberFromHeldKey}"));
                        return (IntPtr)1;
                    }
                }

                if (IsWindowsKeyPressed())
                {
                    if (TryGetShortcutNumber(vkCode, out var number))
                    {
                        SendTimerShortcut(number);
                        return (IntPtr)1;
                    }
                }
                else if (vkCode == VK_F9)
                {
                    RaiseShortcutPressed(CreateRandomNameShortcutArgs("F9"));
                }
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private void SendTimerShortcut(int vkCode)
        {
            if (TryGetShortcutNumber(vkCode, out var number))
            {
                RaiseShortcutPressed(CreateTimerShortcutArgs(number, $"D{number}"));
            }
        }

        private int? FindPressedNumberKey()
        {
            foreach (var key in _pressedKeys)
            {
                if (TryGetShortcutNumber(key, out _))
                {
                    return key;
                }
            }

            return null;
        }

        private bool IsWindowsKeyPressed()
        {
            return _pressedKeys.Contains(VK_LWIN) || _pressedKeys.Contains(VK_RWIN);
        }

        private static bool IsWindowsKey(int vkCode)
        {
            return vkCode == VK_LWIN || vkCode == VK_RWIN;
        }

        private static bool IsWindowsKeyPhysicallyDown()
        {
            return IsKeyPhysicallyDown(VK_LWIN) || IsKeyPhysicallyDown(VK_RWIN);
        }

        private static bool TryGetPhysicallyPressedShortcutNumber(out int number)
        {
            for (var vkCode = VK_D0; vkCode <= VK_D9; vkCode++)
            {
                if (IsKeyPhysicallyDown(vkCode))
                {
                    number = vkCode - VK_D0;
                    return true;
                }
            }

            for (var vkCode = VK_NUMPAD0; vkCode <= VK_NUMPAD9; vkCode++)
            {
                if (IsKeyPhysicallyDown(vkCode))
                {
                    number = vkCode - VK_NUMPAD0;
                    return true;
                }
            }

            number = -1;
            return false;
        }

        private static bool IsKeyPhysicallyDown(int vkCode)
        {
            return (GetAsyncKeyState(vkCode) & 0x8000) != 0;
        }

        private static bool TryGetShortcutNumber(int vkCode, out int number)
        {
            if (vkCode >= VK_D0 && vkCode <= VK_D9)
            {
                number = vkCode - VK_D0;
                return true;
            }

            if (vkCode >= VK_NUMPAD0 && vkCode <= VK_NUMPAD9)
            {
                number = vkCode - VK_NUMPAD0;
                return true;
            }

            number = -1;
            return false;
        }

        private void FailsafeTimerCallback(object state)
        {
            lock (_pressedKeysLock)
            {
                if ((DateTime.Now - _lastWindowsKeyPress).TotalSeconds <= 5)
                {
                    return;
                }

                _pressedKeys.Remove(VK_LWIN);
                _pressedKeys.Remove(VK_RWIN);
            }
        }

        private void RaiseShortcutPressed(ShortcutPressedEventArgs args)
        {
            void Raise() => ShortcutPressed?.Invoke(this, args);

            if (_dispatcherQueue != null)
            {
                _dispatcherQueue.TryEnqueue(Raise);
            }
            else
            {
                Raise();
            }
        }

        private void RaiseHealthChanged(bool isRunning, string message)
        {
            var args = new WatcherHealthChangedEventArgs
            {
                IsRunning = isRunning,
                Message = message,
                ChangedAt = DateTimeOffset.UtcNow
            };

            void Raise() => HealthChanged?.Invoke(this, args);

            if (_dispatcherQueue != null)
            {
                _dispatcherQueue.TryEnqueue(Raise);
            }
            else
            {
                Raise();
            }
        }

        private static ShortcutPressedEventArgs CreateTimerShortcutArgs(int number, string rawMessage)
        {
            return new ShortcutPressedEventArgs
            {
                Kind = ShortcutKind.Timer,
                Number = number,
                PressedAt = DateTimeOffset.UtcNow,
                RawMessage = rawMessage
            };
        }

        private static ShortcutPressedEventArgs CreateRandomNameShortcutArgs(string rawMessage)
        {
            return new ShortcutPressedEventArgs
            {
                Kind = ShortcutKind.RandomName,
                Number = -1,
                PressedAt = DateTimeOffset.UtcNow,
                RawMessage = rawMessage
            };
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using var curProcess = Process.GetCurrentProcess();
            var curModule = curProcess.MainModule
                ?? throw new InvalidOperationException("Unable to access the current process module for the keyboard hook.");

            return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeMessage
        {
            public IntPtr Hwnd;
            public uint Message;
            public UIntPtr WParam;
            public IntPtr LParam;
            public uint Time;
            public int PointX;
            public int PointY;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetMessage(out NativeMessage lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TranslateMessage(ref NativeMessage lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage(ref NativeMessage lpMsg);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostThreadMessage(uint idThread, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
    }
}
