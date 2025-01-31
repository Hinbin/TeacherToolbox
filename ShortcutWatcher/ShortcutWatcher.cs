using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Windows.Forms;

class ShortcutWatcher
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;
    private static LowLevelKeyboardProc _proc = HookCallback;
    private static IntPtr _hookID = IntPtr.Zero;
    private static NamedPipeClientStream pipeClient;
    private static StreamWriter writer;
    private static HashSet<Keys> keysBeingPressed = new HashSet<Keys>();

    private static DateTime lastWindowsKeyPress = DateTime.MinValue; // Timestamp of the last Windows key press
    public static void Main()
    {
        // Hide the console window
        var handle = GetConsoleWindow();
        ShowWindow(handle, SW_HIDE);

        _hookID = SetHook(_proc);

        // Try to establish initial pipe connection
        try
        {
            pipeClient = new NamedPipeClientStream(".", "ShotcutWatcher", PipeDirection.Out);
            pipeClient.Connect(1000); // 1 second timeout
            writer = new StreamWriter(pipeClient);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Initial pipe connection failed: {ex.Message}");
            // Continue running even if initial connection fails
        }

        // Subscribe to session switch events
        SystemEvents.SessionSwitch += new SessionSwitchEventHandler(SystemEvents_SessionSwitch);

        // Call failsafe check periodically
        var timer = new System.Windows.Forms.Timer();
        timer.Interval = 5000;
        timer.Tick += (sender, e) => FailSafeCheck();
        timer.Start();

        Application.Run();

        // Cleanup
        writer?.Dispose();
        pipeClient?.Dispose();
        UnhookWindowsHookEx(_hookID);
        SystemEvents.SessionSwitch -= new SessionSwitchEventHandler(SystemEvents_SessionSwitch);

        Application.ApplicationExit += (sender, e) =>
        {
            keysBeingPressed.Clear();
        };
    }

    private static void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionLock)
        {
            // The session is being locked, clear any pressed keys
            keysBeingPressed.Clear();
        }
        else if (e.Reason == SessionSwitchReason.SessionUnlock)
        {
            // Also clear when the session is unlocked
            keysBeingPressed.Clear();
        }
    }

    private static IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        using (Process curProcess = Process.GetCurrentProcess())
        using (ProcessModule curModule = curProcess.MainModule)
        {
            return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                GetModuleHandle(curModule.ModuleName), 0);
        }
    }

    private delegate IntPtr LowLevelKeyboardProc(
        int nCode, IntPtr wParam, IntPtr lParam);

    private static IntPtr HookCallback(
    int nCode, IntPtr wParam, IntPtr lParam)
    {
        int vkCode = Marshal.ReadInt32(lParam);
        Keys key = (Keys)vkCode;

        if (nCode >= 0)
        {
            if ((wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN) && !keysBeingPressed.Contains(key))
            {
                keysBeingPressed.Add(key);

                // Update the timestamp when a Windows key is pressed
                if (key == Keys.LWin || key == Keys.RWin)
                {
                    lastWindowsKeyPress = DateTime.Now;
                }

                // Check if Windows key is pressed
                if (keysBeingPressed.Contains(Keys.LWin) || keysBeingPressed.Contains(Keys.RWin))
                {
                    // Check if the key is a number key
                    if ((key >= Keys.D0 && key <= Keys.D9) || (key >= Keys.NumPad0 && key <= Keys.NumPad9))
                    {
                        SendKeyPress(key);

                        // Prevent the key from being passed on to the system
                        return (IntPtr)1;
                    }
                }
                else if (key == Keys.F9)
                {
                    SendKeyPress(key);
                }
            }
            else if (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP)
            {
                keysBeingPressed.Remove(key);
            }
        }

        return CallNextHookEx(_hookID, nCode, wParam, lParam);
    }
    private static void SendKeyPress(Keys key)
    {
        if (writer == null || pipeClient == null || !pipeClient.IsConnected)
        {
            // Try to establish connection if we don't have one
            if (!ReconnectPipe())
            {
                Debug.WriteLine("Failed to send key press - no pipe connection available");
                return;
            }
        }

        try
        {
            // Double check writer is not null after potential reconnection
            if (writer != null)
            {
                writer.WriteLine(key);
                writer.Flush();
            }
        }
        catch (IOException)
        {
            // Attempt to reconnect
            try
            {
                if (ReconnectPipe())
                {
                    // Try to send the key press again
                    writer?.WriteLine(key);
                    writer?.Flush();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to reconnect to pipe: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error sending key press: {ex.Message}");
        }
    }

    private static bool ReconnectPipe()
    {
        try
        {
            // Clean up existing resources
            if (writer != null)
            {
                try { writer.Dispose(); } catch { }
                writer = null;
            }
            if (pipeClient != null)
            {
                try { pipeClient.Dispose(); } catch { }
                pipeClient = null;
            }

            // Create new pipe client
            pipeClient = new NamedPipeClientStream(".", "ShotcutWatcher", PipeDirection.Out);

            // Try to connect with a timeout
            pipeClient.Connect(1000); // 1 second timeout

            if (pipeClient.IsConnected)
            {
                writer = new StreamWriter(pipeClient);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to reconnect to pipe: {ex.Message}");
            writer = null;
            pipeClient = null;
            return false;
        }
    }

    private static void FailSafeCheck()
    {
        // Check if more than 5 seconds have passed since the last Windows key press
        if ((DateTime.Now - lastWindowsKeyPress).TotalSeconds > 5)
        {
            keysBeingPressed.Remove(Keys.LWin);
            keysBeingPressed.Remove(Keys.RWin);
        }
    }


    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook,
        LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
        IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("kernel32.dll")]
    static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    const int SW_HIDE = 0;
}