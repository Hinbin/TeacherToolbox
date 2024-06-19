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


    public static void Main()
    {
        // Hide the console window
        var handle = GetConsoleWindow();
        ShowWindow(handle, SW_HIDE);

        _hookID = SetHook(_proc);
        pipeClient = new NamedPipeClientStream(".", "ShotcutWatcher", PipeDirection.Out);
        pipeClient.Connect();
        writer = new StreamWriter(pipeClient);
        Application.Run();
        writer.Close();
        pipeClient.Close();
        UnhookWindowsHookEx(_hookID);
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
            else if (wParam == (IntPtr)WM_KEYUP  || wParam == (IntPtr)WM_SYSKEYUP)
            {
                keysBeingPressed.Remove(key);
            }
        }

        return CallNextHookEx(_hookID, nCode, wParam, lParam);
    }

    private static void SendKeyPress(Keys key)
    {
        writer.WriteLine(key);
        writer.Flush();
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