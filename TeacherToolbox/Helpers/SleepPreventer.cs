using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading;

public class SleepPreventer : IDisposable
{
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

    [FlagsAttribute]
    private enum EXECUTION_STATE : uint
    {
        ES_AWAYMODE_REQUIRED = 0x00000040,
        ES_CONTINUOUS = 0x80000000,
        ES_DISPLAY_REQUIRED = 0x00000002,
        ES_SYSTEM_REQUIRED = 0x00000001
    }

    private bool _preventingSleep = false;
    private bool _disposed = false;
    private Timer _timer;
    private readonly object _lock = new object();

    public void PreventSleep(bool keepDisplayOn = true)
    {
        lock (_lock)
        {
            if (_disposed) return;

            try
            {
                Debug.WriteLine($"Setting thread execution state at {DateTime.Now}");

                EXECUTION_STATE state = EXECUTION_STATE.ES_CONTINUOUS | EXECUTION_STATE.ES_SYSTEM_REQUIRED;
                if (keepDisplayOn)
                {
                    state |= EXECUTION_STATE.ES_DISPLAY_REQUIRED;
                }

                EXECUTION_STATE result = SetThreadExecutionState(state);

                if (result == 0)
                {
                    int error = Marshal.GetLastWin32Error();
                    Debug.WriteLine($"SetThreadExecutionState failed with error code: {error}");
                    return;
                }

                _preventingSleep = true;

                // Create a timer that refreshes the state every 30 seconds
                if (_timer == null)
                {
                    _timer = new Timer(RefreshThreadExecutionState, keepDisplayOn,
                        TimeSpan.Zero, TimeSpan.FromSeconds(30));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in PreventSleep: {ex}");
                // Continue running even if we fail
            }
        }
    }

    private void RefreshThreadExecutionState(object state)
    {
        lock (_lock)
        {
            if (_disposed || !_preventingSleep) return;

            try
            {
                bool keepDisplayOn = (bool)state;
                EXECUTION_STATE flags = EXECUTION_STATE.ES_CONTINUOUS | EXECUTION_STATE.ES_SYSTEM_REQUIRED;
                if (keepDisplayOn)
                {
                    flags |= EXECUTION_STATE.ES_DISPLAY_REQUIRED;
                }

                Debug.WriteLine($"Refreshing thread execution state at {DateTime.Now}");
                SetThreadExecutionState(flags);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in RefreshThreadExecutionState: {ex}");
                // Continue running even if we fail
            }
        }
    }

    public void AllowSleep()
    {
        lock (_lock)
        {
            if (_disposed) return;

            try
            {
                if (_preventingSleep)
                {
                    _timer?.Dispose();
                    _timer = null;

                    SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS);
                    _preventingSleep = false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in AllowSleep: {ex}");
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            AllowSleep();
            _timer?.Dispose();
            _timer = null;
        }
    }
}