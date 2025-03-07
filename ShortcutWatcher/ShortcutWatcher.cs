using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
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
    private static NamedPipeServerStream shutdownPipeServer;
    private static StreamWriter writer;
    private static HashSet<Keys> keysBeingPressed = new HashSet<Keys>();

    // Added constants for connection retries
    private const int INITIAL_CONNECTION_RETRY_COUNT = 10;
    private const int INITIAL_CONNECTION_RETRY_DELAY_MS = 1000;
    private const int PIPE_CONNECTION_TIMEOUT_MS = 3000; // Increased timeout

    // Adding resource management flags
    private static volatile bool isRunning = true;
    private static volatile bool isShuttingDown = false;
    private static object connectionLock = new object();

    private static DateTime lastWindowsKeyPress = DateTime.MinValue; // Timestamp of the last Windows key press

    // Added process watchdog timer
    private static System.Threading.Timer aliveSignalTimer;

    public static void Main()
    {
        // Create a crash log file to capture any issues
        string logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ShortcutWatcher",
            "watcher.log");

        try
        {
            // Ensure directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(logPath));

            // Log startup
            File.AppendAllText(logPath, $"{DateTime.Now}: ShortcutWatcher starting\r\n");

            // Hide the console window
            var handle = GetConsoleWindow();
            ShowWindow(handle, SW_HIDE);

            _hookID = SetHook(_proc);
            File.AppendAllText(logPath, $"{DateTime.Now}: Keyboard hook set\r\n");

            // Start the shutdown listener first so it's ready
            StartShutdownListener();
            File.AppendAllText(logPath, $"{DateTime.Now}: Shutdown listener started\r\n");

            // Try to establish initial pipe connection with retries
            bool connected = TryConnectWithRetries();
            File.AppendAllText(logPath, $"{DateTime.Now}: Initial connection attempt result: {connected}\r\n");

            if (!connected)
            {
                Debug.WriteLine("Failed to establish pipe connection after multiple attempts");
                File.AppendAllText(logPath, $"{DateTime.Now}: Failed to establish pipe connection after multiple attempts\r\n");
                // Continue running anyway - we'll retry connections when needed
            }

            // Subscribe to session switch events
            SystemEvents.SessionSwitch += new SessionSwitchEventHandler(SystemEvents_SessionSwitch);

            // Call failsafe check periodically
            var failsafeTimer = new System.Windows.Forms.Timer();
            failsafeTimer.Interval = 5000;
            failsafeTimer.Tick += (sender, e) => FailSafeCheck();
            failsafeTimer.Start();

            // Set up alive signal timer
            aliveSignalTimer = new System.Threading.Timer(SendAliveSignal, null,
                3000, // Initial delay - reduced to send signal sooner
                5000); // Periodic interval - increased frequency

            File.AppendAllText(logPath, $"{DateTime.Now}: All timers started\r\n");

            // Create a hidden form to run the message loop
            using (var hiddenForm = new Form { Opacity = 0, ShowInTaskbar = false })
            {
                // Set up another timer to periodically check if we should exit
                var exitCheckTimer = new System.Windows.Forms.Timer();
                exitCheckTimer.Interval = 1000;
                exitCheckTimer.Tick += (sender, e) => {
                    if (isShuttingDown)
                    {
                        File.AppendAllText(logPath, $"{DateTime.Now}: Detected shutdown flag, closing application\r\n");
                        exitCheckTimer.Stop();
                        hiddenForm.Close();
                    }
                };
                exitCheckTimer.Start();

                File.AppendAllText(logPath, $"{DateTime.Now}: Starting message loop\r\n");

                // Run the message loop
                Application.Run(hiddenForm);
            }

            File.AppendAllText(logPath, $"{DateTime.Now}: Message loop ended, cleaning up\r\n");
            // Cleanup
            CleanupResources();
            File.AppendAllText(logPath, $"{DateTime.Now}: ShortcutWatcher exiting normally\r\n");
        }
        catch (Exception ex)
        {
            // Log the error to a file since we're likely hidden
            try
            {
                logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ShortcutWatcher",
                    "error.log");

                // Ensure directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(logPath));

                // Append to log file
                File.AppendAllText(logPath,
                    $"{DateTime.Now}: Critical error: {ex.Message}\r\n{ex.StackTrace}\r\n\r\n");
            }
            catch
            {
                // Nothing else we can do
            }

            // Re-throw to allow Windows to report the crash
            throw;
        }
    }

    private static bool TryConnectWithRetries()
    {
        for (int attempt = 1; attempt <= INITIAL_CONNECTION_RETRY_COUNT; attempt++)
        {
            try
            {
                Debug.WriteLine($"Attempting pipe connection (attempt {attempt} of {INITIAL_CONNECTION_RETRY_COUNT})");

                // Clean up any existing connections
                lock (connectionLock)
                {
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

                    // Use shorter timeout for initial attempts to avoid long delays
                    var timeout = (attempt <= 3) ? 1000 : PIPE_CONNECTION_TIMEOUT_MS;

                    // Use a Task with timeout to avoid blocking
                    var connectionTask = Task.Run(() => {
                        try
                        {
                            pipeClient.Connect(timeout);
                            return true;
                        }
                        catch
                        {
                            return false;
                        }
                    });

                    // Wait with timeout to avoid blocking indefinitely
                    bool connectResult = connectionTask.Wait(timeout + 500);

                    if (!connectResult || !connectionTask.Result)
                    {
                        // Connection timed out or failed
                        throw new TimeoutException("Connection attempt timed out");
                    }

                    if (pipeClient.IsConnected)
                    {
                        writer = new StreamWriter(pipeClient);
                        writer.AutoFlush = true; // Ensure data is sent immediately

                        // Send confirmation that we've started successfully
                        SendStartupConfirmation();

                        Debug.WriteLine("Pipe connection established successfully");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Pipe connection attempt {attempt} failed: {ex.Message}");

                // Log to file asynchronously
                Task.Run(() => {
                    try
                    {
                        string logPath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "ShortcutWatcher", "watcher.log");
                        File.AppendAllText(logPath,
                            $"{DateTime.Now}: Connection attempt {attempt} failed: {ex.Message}\r\n");
                    }
                    catch { }
                });

                // Cleanup after failed attempt - do outside the lock
                try
                {
                    if (writer != null)
                    {
                        writer.Dispose();
                        writer = null;
                    }
                }
                catch { }

                try
                {
                    if (pipeClient != null)
                    {
                        pipeClient.Dispose();
                        pipeClient = null;
                    }
                }
                catch { }

                // Wait before retrying
                if (attempt < INITIAL_CONNECTION_RETRY_COUNT)
                {
                    System.Threading.Thread.Sleep(INITIAL_CONNECTION_RETRY_DELAY_MS);
                }
            }
        }

        return false;
    }

    private static void SendAliveSignal(object state)
    {
        if (isShuttingDown) return;

        lock (connectionLock)
        {
            try
            {
                if (writer == null || pipeClient == null || !pipeClient.IsConnected)
                {
                    // Try to reconnect
                    if (!ReconnectPipe())
                    {
                        string logPath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "ShortcutWatcher",
                            "watcher.log");
                        File.AppendAllText(logPath, $"{DateTime.Now}: Failed to reconnect for alive signal\r\n");
                    }
                    return;
                }

                // Send a heartbeat signal
                writer.WriteLine("ALIVE");
                writer.Flush();
                Debug.WriteLine("Sent alive signal");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error sending alive signal: {ex.Message}");

                string logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ShortcutWatcher",
                    "watcher.log");
                File.AppendAllText(logPath, $"{DateTime.Now}: Error sending alive signal: {ex.Message}\r\n");

                // Attempt to reconnect
                ReconnectPipe();
            }
        }
    }

    private static void SendStartupConfirmation()
    {
        if (writer == null || pipeClient == null || !pipeClient.IsConnected)
        {
            if (!ReconnectPipe())
            {
                Debug.WriteLine("Failed to send startup confirmation - no pipe connection available");
                return;
            }
        }

        try
        {
            if (writer != null)
            {
                // Send a special message with process ID to confirm startup
                writer.WriteLine($"STARTUP:{Process.GetCurrentProcess().Id}");
                writer.Flush();
                Debug.WriteLine($"Sent startup confirmation with PID {Process.GetCurrentProcess().Id}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error sending startup confirmation: {ex.Message}");
        }
    }

    private static void StartShutdownListener()
    {
        try
        {
            shutdownPipeServer = new NamedPipeServerStream("ShortcutWatcherShutdown", PipeDirection.In);

            // Start an async operation to wait for and handle shutdown signal
            Task.Run(async () => {
                try
                {
                    Debug.WriteLine("Shutdown listener started");

                    while (isRunning)
                    {
                        try
                        {
                            // Wait for connection
                            await shutdownPipeServer.WaitForConnectionAsync();
                            Debug.WriteLine("Shutdown connection received");

                            using (StreamReader reader = new StreamReader(shutdownPipeServer))
                            {
                                string message = await reader.ReadLineAsync();
                                Debug.WriteLine($"Shutdown message received: {message}");

                                if (message == "SHUTDOWN")
                                {
                                    Debug.WriteLine("Shutdown signal received, exiting application");
                                    isShuttingDown = true;
                                    isRunning = false;

                                    // Gracefully exit the application
                                    Application.Exit();
                                    break;
                                }
                            }

                            // Disconnect and reset for next connection
                            shutdownPipeServer.Disconnect();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error in shutdown listener connection: {ex.Message}");

                            // Recreate the pipe if there was an error
                            try
                            {
                                if (shutdownPipeServer != null)
                                {
                                    shutdownPipeServer.Dispose();
                                }
                                shutdownPipeServer = new NamedPipeServerStream("ShortcutWatcherShutdown", PipeDirection.In);
                            }
                            catch
                            {
                                // If we can't recreate the pipe, break out of the loop
                                break;
                            }

                            // Wait a bit before trying again
                            await Task.Delay(1000);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Fatal error in shutdown listener: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to create shutdown listener: {ex.Message}");
            // Continue running even if shutdown listener fails
        }
    }

    private static void CleanupResources()
    {
        Debug.WriteLine("Cleaning up resources");

        // Update flags first
        isRunning = false;

        // Dispose timers
        try { aliveSignalTimer?.Dispose(); } catch { }
        aliveSignalTimer = null;

        // Clean up pipes with proper error handling
        try
        {
            if (writer != null)
            {
                writer.Flush();
                writer.Dispose();
            }
        }
        catch { }
        writer = null;

        try { pipeClient?.Dispose(); } catch { }
        pipeClient = null;

        try { shutdownPipeServer?.Dispose(); } catch { }
        shutdownPipeServer = null;

        // Unhook keyboard
        try { UnhookWindowsHookEx(_hookID); } catch { }
        _hookID = IntPtr.Zero;

        // Unsubscribe from events
        SystemEvents.SessionSwitch -= new SessionSwitchEventHandler(SystemEvents_SessionSwitch);

        // Clear state
        keysBeingPressed.Clear();

        // Log cleanup
        string logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ShortcutWatcher",
            "watcher.log");
        File.AppendAllText(logPath, $"{DateTime.Now}: Resources cleaned up\r\n");
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
                Debug.WriteLine($"Sent key press: {key}");
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
                    Debug.WriteLine($"Reconnected and sent key press: {key}");
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
        Debug.WriteLine("Attempting to reconnect pipe");
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
            pipeClient.Connect(PIPE_CONNECTION_TIMEOUT_MS); // Increased timeout

            if (pipeClient.IsConnected)
            {
                writer = new StreamWriter(pipeClient);
                writer.AutoFlush = true; // Ensure data is sent immediately
                SendStartupConfirmation();
                Debug.WriteLine("Pipe reconnection successful");
                return true;
            }

            Debug.WriteLine("Pipe reconnection failed - not connected after Connect() call");
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