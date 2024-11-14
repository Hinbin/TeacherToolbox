using System;
using System.Runtime.InteropServices;

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

    public void PreventSleep(bool keepDisplayOn = true)
    {
        if (!_preventingSleep)
        {
            EXECUTION_STATE state = EXECUTION_STATE.ES_CONTINUOUS | EXECUTION_STATE.ES_SYSTEM_REQUIRED;
            if (keepDisplayOn)
            {
                state |= EXECUTION_STATE.ES_DISPLAY_REQUIRED;
            }

            SetThreadExecutionState(state);
            _preventingSleep = true;
        }
    }

    public void AllowSleep()
    {
        if (_preventingSleep)
        {
            SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS);
            _preventingSleep = false;
        }
    }

    public void Dispose()
    {
        AllowSleep();
    }
}