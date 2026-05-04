using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TeacherToolbox.Controls;
using TeacherToolbox.Helpers;
using TeacherToolbox.Services;
using Windows.ApplicationModel.VoiceCommands;
using Windows.UI;
using WinUIEx;



// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.


namespace TeacherToolbox
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow
    {
        // Services
        private readonly ISleepPreventer _sleepPreventer;
        private readonly ISettingsService _settingsService;
        private readonly IThemeService _themeService;
        private readonly ITelemetryService _telemetry;

        private readonly OverlappedPresenter _presenter;
        private NamedPipeServerStream pipeServer;
        private WindowDragHelper dragHelper;
        private Process shortcutWatcherProcess;
        private Timer watchdogTimer;
        private const int MAX_RESTART_ATTEMPTS = 3;
        private int restartAttempts = 0;
        private bool isShortcutWatcherRunning = false;
        private int pipeFailedChecks = 0;
        private object pipeLock = new object();
        private bool isPipeListenerRunning = false;
        private SemaphoreSlim pipeSemaphore = new SemaphoreSlim(1, 1);        
        private void ContentFrame_NavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        public MainWindow()
        {
            this.InitializeComponent();

            // Get services from the App's service provider
            var services = App.Current.Services;

            _settingsService = services.GetRequiredService<ISettingsService>();
            _sleepPreventer = services.GetRequiredService<ISleepPreventer>();
            _themeService = services.GetRequiredService<IThemeService>();
            _telemetry = services.GetRequiredService<ITelemetryService>();

            _presenter = this.AppWindow.Presenter as OverlappedPresenter;

            // Restore saved window position or use default size
            RestoreWindowPosition();

            this.ExtendsContentIntoTitleBar = true;
            UpdateTitleBarTheme();
            SetRegionsForCustomTitleBar(); // To allow the nav button to be selectable, but the rest of the title bar to function as normal

            NavView.IsPaneOpen = false;
            pipeFailedChecks = 0;

            pipeServer = new NamedPipeServerStream("ShortcutWatcher", PipeDirection.In);
            ListenForKeyPresses();

            this.SetIsAlwaysOnTop(true);

            dragHelper = new WindowDragHelper(this, _settingsService);

            try
            {
                // Start the KeyInterceptor application
                StartShortcutWatcher();
            }
            catch (Exception e)
            {
                _telemetry.LogWarning("Failed to start ShortcutWatcher from MainWindow ctor", e);
            }

            this.Closed += MainWindow_Closed;
            this.SizeChanged += MainWindow_SizeChanged;
            this.AppWindow.Changed += AppWindow_Changed;
        }

        private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            // When the window moves (e.g., dragged to a different monitor),
            // we need to recalculate the title bar regions for the new DPI
            if (args.DidPositionChange)
            {
                SetRegionsForCustomTitleBar();
            }
        }

        private void OnShortcutWatcherExited(object sender, EventArgs e)
        {
            Debug.WriteLine("ShortcutWatcher process exited unexpectedly");
            isShortcutWatcherRunning = false;
            AttemptRestartIfNeeded();
        }

        private async void VerifyShortcutWatcherRunning()
        {
            // Wait briefly to allow ShortcutWatcher to initialize
            await Task.Delay(1000);

            // Verify pipe connection can be established
            await VerifyPipeConnectionAsync();
        }

        private async Task VerifyPipeConnectionAsync()
        {
            try
            {
                Debug.WriteLine("Verifying ShortcutWatcher pipe connection...");

                // Check if process is still running first
                if (shortcutWatcherProcess == null || shortcutWatcherProcess.HasExited)
                {
                    Debug.WriteLine("ShortcutWatcher process has exited before verification");
                    isShortcutWatcherRunning = false;
                    AttemptRestartIfNeeded();
                    return;
                }

                // Try to establish a test pipe connection to verify ShortcutWatcher is responsive
                using (NamedPipeClientStream testPipe = new NamedPipeClientStream(".", "ShortcutWatcherShutdown", PipeDirection.Out))
                {
                    try
                    {
                        // Use a shorter timeout first for better responsiveness
                        var connectTask = testPipe.ConnectAsync(2000);

                        // Wait for the connection attempt to complete
                        await connectTask;

                        if (testPipe.IsConnected)
                        {
                            Debug.WriteLine("ShortcutWatcher shutdown pipe connection verified");
                            isShortcutWatcherRunning = true;
                            restartAttempts = 0; // Reset the counter upon successful connection

                            // Just testing connection, don't send actual shutdown signal

                            // Try to clean up properly
                            testPipe.Close();
                            return;
                        }
                    }
                    catch (TimeoutException)
                    {
                        _telemetry.LogWarning("ShortcutWatcher shutdown pipe connection timed out");
                    }
                    catch (Exception innerEx)
                    {
                        _telemetry.LogWarning("Error during ShortcutWatcher verification", innerEx);
                    }
                }

                // If we reach here, the first method failed - check process again
                if (shortcutWatcherProcess != null && !shortcutWatcherProcess.HasExited)
                {
                    Debug.WriteLine("Process is still running but pipe connection failed - waiting for ALIVE signal");

                    // Don't restart yet - wait for an ALIVE signal or process exit
                    // The WatchdogCallback will eventually restart if needed
                }
                else
                {
                    Debug.WriteLine("ShortcutWatcher pipe connection failed - process not running");
                    isShortcutWatcherRunning = false;
                    AttemptRestartIfNeeded();
                }
            }
            catch (Exception ex)
            {
                _telemetry.LogError("ShortcutWatcher pipe connection test failed", ex);
                isShortcutWatcherRunning = false;
                AttemptRestartIfNeeded();
            }
        }

        private void WatchdogCallback(object state)
        {
            // Run on background thread to avoid UI blocking
            Task.Run(async () => {
                try
                {
                    Debug.WriteLine("Watchdog checking ShortcutWatcher status...");

                    // First check if process is still running
                    bool processRunning = shortcutWatcherProcess != null && !shortcutWatcherProcess.HasExited;

                    if (!processRunning)
                    {
                        Debug.WriteLine("Watchdog detected ShortcutWatcher process is not running");
                        isShortcutWatcherRunning = false;
                        AttemptRestartIfNeeded();
                        return;
                    }

                    // Process is running, now verify pipe connection is working
                    bool isPipeConnected = false;

                    try
                    {
                        // First check if our server pipe is connected
                        if (pipeServer != null && pipeServer.IsConnected)
                        {
                            Debug.WriteLine("Main pipe server is connected");
                            isPipeConnected = true;
                        }
                        else
                        {
                            // Try the shutdown pipe as a backup check
                            using (NamedPipeClientStream testPipe = new NamedPipeClientStream(".", "ShortcutWatcherShutdown", PipeDirection.Out))
                            {
                                var connectTask = testPipe.ConnectAsync(2000);
                                bool connectResult = await Task.WhenAny(connectTask, Task.Delay(3000)) == connectTask;

                                if (connectResult && testPipe.IsConnected)
                                {
                                    Debug.WriteLine("Watchdog: ShortcutWatcher shutdown pipe is responsive");
                                    isPipeConnected = true;

                                    // Don't send a shutdown signal, just close the connection
                                    testPipe.Close();
                                }
                                else
                                {
                                    Debug.WriteLine("Watchdog: ShortcutWatcher shutdown pipe is not responsive");
                                }
                            }
                        }
                    }
                    catch (Exception pipeEx)
                    {
                        _telemetry.LogWarning("Watchdog: Error checking pipe status", pipeEx);
                    }

                    // Make decision based on both process and pipe status
                    if (processRunning && !isPipeConnected)
                    {
                        Debug.WriteLine("Watchdog: Process is running but pipes are not responsive");

                        // If this happens multiple times in a row, kill and restart
                        if (++pipeFailedChecks >= 3) // Add this field to your class
                        {
                            Debug.WriteLine("Watchdog: Multiple pipe check failures, forcing process restart");
                            try
                            {
                                // Kill the process
                                if (!shortcutWatcherProcess.HasExited)
                                {
                                    shortcutWatcherProcess.Kill();
                                }
                            }
                            catch (Exception ex)
                            {
                                _telemetry.LogWarning("Watchdog: failed to kill unresponsive ShortcutWatcher", ex);
                            }

                            isShortcutWatcherRunning = false;
                            AttemptRestartIfNeeded();
                            pipeFailedChecks = 0;
                        }
                    }
                    else if (processRunning && isPipeConnected)
                    {
                        // Everything is working
                        Debug.WriteLine("Watchdog: ShortcutWatcher is running properly");
                        isShortcutWatcherRunning = true;
                        pipeFailedChecks = 0;
                    }
                    else
                    {
                        // Process not running or other issues
                        Debug.WriteLine("Watchdog: ShortcutWatcher needs restart");
                        isShortcutWatcherRunning = false;
                        AttemptRestartIfNeeded();
                        pipeFailedChecks = 0;
                    }
                }
                catch (Exception ex)
                {
                    _telemetry.LogError("Error in watchdog", ex);

                    // If an exception occurs in watchdog, try restarting
                    try
                    {
                        isShortcutWatcherRunning = false;
                        AttemptRestartIfNeeded();
                    }
                    catch (Exception nestEx)
                    {
                        _telemetry.LogWarning("Watchdog: nested error during AttemptRestartIfNeeded", nestEx);
                    }
                }
            });
        }

        private void AttemptRestartIfNeeded()
        {
            // Run on background thread to prevent UI blocking
            Task.Run(() => {
                try
                {
                    // Limit restart attempts to avoid infinite restart loops
                    if (restartAttempts < MAX_RESTART_ATTEMPTS)
                    {
                        restartAttempts++;
                        Debug.WriteLine($"Attempting to restart ShortcutWatcher (attempt {restartAttempts} of {MAX_RESTART_ATTEMPTS})");

                        // Clean up previous process reference if it exists
                        if (shortcutWatcherProcess != null)
                        {
                            // Unsubscribe from events first to avoid race conditions
                            try
                            {
                                shortcutWatcherProcess.Exited -= OnShortcutWatcherExited;
                            }
                            catch (Exception ex)
                            {
                                _telemetry.LogWarning("Failed to unsubscribe ShortcutWatcher Exited handler", ex);
                            }

                            // Then try to terminate if it's still running
                            try
                            {
                                if (!shortcutWatcherProcess.HasExited)
                                {
                                    shortcutWatcherProcess.Kill();
                                    // Wait briefly for it to exit
                                    shortcutWatcherProcess.WaitForExit(1000);
                                }
                            }
                            catch (Exception ex)
                            {
                                _telemetry.LogWarning("Error killing ShortcutWatcher process during restart", ex);
                            }

                            // Clean up
                            try { shortcutWatcherProcess.Dispose(); }
                            catch (Exception ex)
                            {
                                _telemetry.LogWarning("Failed to dispose ShortcutWatcher process", ex);
                            }

                            shortcutWatcherProcess = null;
                        }

                        // Also clean up any other instances that might be running
                        CleanupExistingShortcutWatcherProcesses();

                        // Wait briefly before attempting to restart
                        Task.Delay(2000).Wait();

                        // Find the executable path reliably
                        string exePath = "";
                        string workingDir = "";

                        try
                        {
                            exePath = Path.Combine(AppContext.BaseDirectory, "ShortcutWatcher.exe");
                            workingDir = AppContext.BaseDirectory;

                            if (!File.Exists(exePath))
                            {
                                // Try relative to the executing assembly
                                string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                                string assemblyDir = Path.GetDirectoryName(assemblyPath);
                                exePath = Path.Combine(assemblyDir, "ShortcutWatcher.exe");
                                workingDir = assemblyDir;

                                if (!File.Exists(exePath))
                                {
                                    // As a last resort, try to search in common locations
                                    if (File.Exists("ShortcutWatcher.exe"))
                                    {
                                        exePath = Path.GetFullPath("ShortcutWatcher.exe");
                                        workingDir = Path.GetDirectoryName(exePath);
                                    }
                                    else
                                    {
                                        throw new FileNotFoundException("Could not find ShortcutWatcher.exe");
                                    }
                                }
                            }

                            Debug.WriteLine($"Found ShortcutWatcher.exe at: {exePath}");
                        }
                        catch (Exception ex)
                        {
                            _telemetry.LogError("Error finding ShortcutWatcher.exe", ex);
                            return;
                        }

                        // Set up the process with more control
                        var startInfo = new ProcessStartInfo
                        {
                            FileName = exePath,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            WorkingDirectory = workingDir
                        };

                        Debug.WriteLine($"Restarting ShortcutWatcher.exe from {startInfo.WorkingDirectory}");

                        try
                        {
                            // Start the process
                            Process proc = Process.Start(startInfo);

                            if (proc != null)
                            {
                                shortcutWatcherProcess = proc;
                                shortcutWatcherProcess.EnableRaisingEvents = true;
                                shortcutWatcherProcess.Exited += OnShortcutWatcherExited;

                                Debug.WriteLine($"ShortcutWatcher restarted with PID: {shortcutWatcherProcess.Id}");

                                // The pipe connection will be established by ListenForKeyPresses
                                // which is continually running in the background
                            }
                            else
                            {
                                Debug.WriteLine("Failed to restart ShortcutWatcher - Process.Start returned null");
                            }
                        }
                        catch (Exception ex)
                        {
                            _telemetry.LogError("Error starting ShortcutWatcher process", ex);
                        }
                    }
                    else
                    {
                        Debug.WriteLine("Maximum ShortcutWatcher restart attempts reached");
                        // Consider notifying the user here that ShortcutWatcher failed to start
                    }
                }
                catch (Exception ex)
                {
                    _telemetry.LogError("Critical error in ShortcutWatcher restart", ex);
                }
            });
        }



        private void CleanupExistingShortcutWatcherProcesses()
        {
            try
            {
                // Get all processes named ShortcutWatcher (case-insensitive)
                var existingProcesses = Process.GetProcesses()
                    .Where(p => string.Equals(p.ProcessName, "ShortcutWatcher", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (existingProcesses.Count > 0)
                {
                    Debug.WriteLine($"Found {existingProcesses.Count} existing ShortcutWatcher processes");

                    // First try to shut them down gracefully
                    foreach (var process in existingProcesses)
                    {
                        try
                        {
                            // Try to send a shutdown signal
                            SendShutdownSignalToSpecificProcess(process.Id);

                            // Give it a moment to shut down gracefully
                            if (!process.WaitForExit(1000))
                            {
                                Debug.WriteLine($"Process {process.Id} didn't exit gracefully, forcing termination");
                                process.Kill();
                            }
                            else
                            {
                                Debug.WriteLine($"Process {process.Id} exited gracefully");
                            }
                        }
                        catch (Exception ex)
                        {
                            _telemetry.LogWarning($"Error shutting down process {process.Id}", ex);

                            // If graceful shutdown fails, force termination
                            try
                            {
                                if (!process.HasExited)
                                {
                                    process.Kill();
                                    Debug.WriteLine($"Forcibly terminated process {process.Id}");
                                }
                            }
                            catch (Exception killEx)
                            {
                                _telemetry.LogWarning($"Failed to forcibly terminate process {process.Id}", killEx);
                            }
                        }
                        finally
                        {
                            process.Dispose();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _telemetry.LogError("Error during existing ShortcutWatcher process cleanup", ex);
            }
        }

        private void SendShutdownSignalToSpecificProcess(int processId)
        {
            try
            {
                // Try to create a pipe specifically named for this process ID
                // Try the default pipe first - this is the most likely to work
                using (NamedPipeClientStream pipeClient = new NamedPipeClientStream(".", "ShortcutWatcherShutdown", PipeDirection.Out))
                {
                    // Short timeout for connection attempt
                    pipeClient.Connect(500);

                    if (pipeClient.IsConnected)
                    {
                        using (StreamWriter writer = new StreamWriter(pipeClient))
                        {
                            writer.WriteLine("SHUTDOWN");
                            writer.Flush();
                            Debug.WriteLine($"Sent shutdown signal to process {processId}");
                        }
                    }
                }
            }

            catch (Exception ex)
            {
                _telemetry.LogWarning($"Failed to send shutdown signal to process {processId}", ex);
                // Just log the error - we'll fall back to Kill() if needed
            }
        }
        private void StartShortcutWatcher()
        {
            // Run the potentially blocking operations on a background thread
            Task.Run(() => {
                try
                {
                    // First, check for any existing ShortcutWatcher processes
                    CleanupExistingShortcutWatcherProcesses();

                    // Set up the pipe server first to ensure it's ready
                    Debug.WriteLine("Setting up pipe server...");
                    try
                    {
                        // Always dispose and recreate the pipe server to ensure a clean state
                        pipeServer?.Dispose();
                        pipeServer = new NamedPipeServerStream("ShortcutWatcher", PipeDirection.In, 1);
                        ListenForKeyPresses();
                        Debug.WriteLine("Pipe server setup complete");
                    }
                    catch (Exception pipeEx)
                    {
                        _telemetry.LogWarning("Error setting up pipe server, will retry", pipeEx);

                        // Retry after a short delay
                        Task.Delay(1000).ContinueWith(_ => {
                            try
                            {
                                pipeServer?.Dispose();
                                pipeServer = new NamedPipeServerStream("ShortcutWatcher", PipeDirection.In, 1);
                                ListenForKeyPresses();
                                Debug.WriteLine("Pipe server setup retry successful");
                            }
                            catch (Exception retryEx)
                            {
                                _telemetry.LogError("Pipe server setup retry failed", retryEx);
                            }
                        });
                    }

                    // Find executable path with proper error handling
                    string exePath = Path.Combine(AppContext.BaseDirectory, "ShortcutWatcher.exe");
                    if (!File.Exists(exePath))
                    {
                        // Try relative to the executing assembly
                        string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                        string assemblyDir = Path.GetDirectoryName(assemblyPath);
                        exePath = Path.Combine(assemblyDir, "ShortcutWatcher.exe");

                        if (!File.Exists(exePath))
                        {
                            // As a last resort, try to search in common locations
                            if (File.Exists("ShortcutWatcher.exe"))
                            {
                                exePath = "ShortcutWatcher.exe";
                            }
                            else
                            {
                                throw new FileNotFoundException("Could not find ShortcutWatcher.exe");
                            }
                        }
                    }

                    // Set up the process
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = Path.GetDirectoryName(exePath)
                    };

                    Debug.WriteLine($"Starting ShortcutWatcher.exe from {startInfo.WorkingDirectory}");

                    // Start the process
                    shortcutWatcherProcess = Process.Start(startInfo);

                    if (shortcutWatcherProcess != null)
                    {
                        // Configure the process
                        shortcutWatcherProcess.EnableRaisingEvents = true;
                        shortcutWatcherProcess.Exited += OnShortcutWatcherExited;

                        // Set up a timer to verify pipe connection with a delay
                        Debug.WriteLine("Setting up verification delay");
                        Task.Delay(5000).ContinueWith(_ => VerifyShortcutWatcherRunning());

                        // Start a watchdog timer to periodically check if the process is still running
                        watchdogTimer = new Timer(WatchdogCallback, null, 15000, 30000);

                        Debug.WriteLine($"ShortcutWatcher process started with PID: {shortcutWatcherProcess.Id}");

                        // Mark as running
                        isShortcutWatcherRunning = true;
                    }
                    else
                    {
                        Debug.WriteLine("Failed to start ShortcutWatcher.exe - Process.Start returned null");
                        AttemptRestartIfNeeded();
                    }
                }
                catch (Exception e)
                {
                    _telemetry.LogError("Error starting ShortcutWatcher", e);
                    AttemptRestartIfNeeded();
                }
            });
        }

        private void MainWindow_SizeChanged(object sender, WindowSizeChangedEventArgs e)
        {
            SetRegionsForCustomTitleBar();
        }

        private void RestoreWindowPosition()
        {
            try
            {
                var savedPosition = _settingsService.GetLastWindowPosition();

                if (!savedPosition.IsEmpty)
                {
                    // Verify the saved display still exists and the position is valid
                    bool positionIsValid = false;
                    double targetScaleFactor = 1.0;

                    try
                    {
                        // Get all display areas and check if the saved position is within any of them
                        var displayAreas = Microsoft.UI.Windowing.DisplayArea.FindAll();

                        foreach (var display in displayAreas)
                        {
                            var workArea = display.WorkArea;

                            // Check if the saved position's top-left corner is within this display's work area
                            // Allow some tolerance for windows that might be partially off-screen
                            if (savedPosition.X >= workArea.X - 100 &&
                                savedPosition.X < workArea.X + workArea.Width &&
                                savedPosition.Y >= workArea.Y - 100 &&
                                savedPosition.Y < workArea.Y + workArea.Height)
                            {
                                positionIsValid = true;

                                // Get the DPI scale factor for this display
                                // We calculate it from the ratio of outer bounds to work area
                                // or use a more direct method if available
                                try
                                {
                                    // Get DPI for target monitor using its bounds
                                    uint dpiX = 96, dpiY = 96;
                                    var monitorHandle = MonitorFromPoint(
                                        new POINT { x = savedPosition.X, y = savedPosition.Y },
                                        MONITOR_DEFAULTTONEAREST);

                                    if (monitorHandle != IntPtr.Zero)
                                    {
                                        GetDpiForMonitor(monitorHandle, MDT_EFFECTIVE_DPI, out dpiX, out dpiY);
                                        targetScaleFactor = dpiX / 96.0;
                                        Debug.WriteLine($"Target monitor DPI: {dpiX}, scale factor: {targetScaleFactor}");
                                    }
                                }
                                catch (Exception dpiEx)
                                {
                                    _telemetry.LogWarning("Could not get DPI for target monitor", dpiEx);
                                    // Fall back to current window's scale factor
                                    if (Content?.XamlRoot != null)
                                    {
                                        targetScaleFactor = Content.XamlRoot.RasterizationScale;
                                    }
                                }

                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _telemetry.LogWarning("Error checking display areas during window position restoration", ex);
                        // If we can't check displays, assume position is valid
                        positionIsValid = true;
                    }

                    if (positionIsValid)
                    {
                        // Convert saved DIP size to physical pixels for the target monitor
                        int physicalWidth = (int)(savedPosition.Width * targetScaleFactor);
                        int physicalHeight = (int)(savedPosition.Height * targetScaleFactor);

                        // Ensure minimum size
                        physicalWidth = Math.Max(physicalWidth, (int)(400 * targetScaleFactor));
                        physicalHeight = Math.Max(physicalHeight, (int)(150 * targetScaleFactor));

                        // Restore position and size
                        this.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(
                            savedPosition.X,
                            savedPosition.Y,
                            physicalWidth,
                            physicalHeight
                        ));

                        Debug.WriteLine($"Restored window position: {savedPosition.X}, {savedPosition.Y}, {physicalWidth}x{physicalHeight} physical pixels (from {savedPosition.Width}x{savedPosition.Height} DIPs, scale: {targetScaleFactor})");
                    }
                    else
                    {
                        // Saved position is not valid (display may have been disconnected), use default
                        Debug.WriteLine("Saved window position is not on any current display, using default");
                        Windows.Graphics.SizeInt32 size = new(_Width: 600, _Height: 200);
                        this.AppWindow.ResizeClient(size);
                    }
                }
                else
                {
                    // No saved position, use default size
                    Windows.Graphics.SizeInt32 size = new(_Width: 600, _Height: 200);
                    this.AppWindow.ResizeClient(size);
                }
            }
            catch (Exception ex)
            {
                _telemetry.LogError("Error restoring window position", ex);
                // Fall back to default size
                Windows.Graphics.SizeInt32 size = new(_Width: 600, _Height: 200);
                this.AppWindow.ResizeClient(size);
            }
        }

        #region DPI Helper P/Invoke

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [System.Runtime.InteropServices.DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const int MDT_EFFECTIVE_DPI = 0;

        #endregion

        private void SaveWindowPosition()
        {
            try
            {
                var position = this.AppWindow.Position;
                var size = this.AppWindow.Size;
                var displayId = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(
                    this.AppWindow.Id,
                    Microsoft.UI.Windowing.DisplayAreaFallback.Primary).DisplayId;

                // Get the current DPI scale factor to convert physical pixels to DIPs
                // This ensures the size is stored in a DPI-independent way
                double scaleFactor = 1.0;
                if (Content?.XamlRoot != null)
                {
                    scaleFactor = Content.XamlRoot.RasterizationScale;
                }

                // Store size in DIPs (device-independent pixels) for cross-DPI compatibility
                var windowPosition = new Model.WindowPosition(
                    position.X,
                    position.Y,
                    size.Width / scaleFactor,  // Convert to DIPs
                    size.Height / scaleFactor, // Convert to DIPs
                    displayId.Value
                );

                _settingsService.SetLastWindowPosition(windowPosition);
                Debug.WriteLine($"Saved window position: {position.X}, {position.Y}, {size.Width / scaleFactor}x{size.Height / scaleFactor} DIPs (scale: {scaleFactor})");
            }
            catch (Exception ex)
            {
                _telemetry.LogError("Error saving window position", ex);
            }
        }

        private void MainWindow_Closed(object sender, WindowEventArgs e)
        {
            try
            {
                // Save window position before closing
                SaveWindowPosition();

                // Dispose the watchdog timer
                watchdogTimer?.Dispose();
                watchdogTimer = null;

                _sleepPreventer?.Dispose();

                // Send shutdown signal to ShortcutWatcher
                SendShutdownSignalToShortcutWatcher();

                // Give a chance for the process to exit gracefully
                if (shortcutWatcherProcess != null && !shortcutWatcherProcess.HasExited)
                {
                    // Wait briefly for graceful shutdown
                    if (!shortcutWatcherProcess.WaitForExit(500))
                    {
                        // If it doesn't exit gracefully, force it to close
                        try
                        {
                            shortcutWatcherProcess.Kill();
                        }
                        catch (Exception ex)
                        {
                            _telemetry.LogWarning("Error killing ShortcutWatcher during window close", ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _telemetry.LogError("Error during MainWindow cleanup", ex);
            }
        }

        private void SendShutdownSignalToShortcutWatcher()
        {
            try
            {
                using (NamedPipeClientStream pipeClient = new NamedPipeClientStream(".", "ShortcutWatcherShutdown", PipeDirection.Out))
                {
                    pipeClient.Connect(1000); // 1 second timeout
                    using (StreamWriter writer = new StreamWriter(pipeClient))
                    {
                        writer.WriteLine("SHUTDOWN");
                        writer.Flush();
                    }
                }
            }
            catch (Exception ex)
            {
                _telemetry.LogWarning("Error sending shutdown signal to ShortcutWatcher", ex);
            }
        }
        private async void ListenForKeyPresses()
        {
            // Check if already running - use a simple boolean flag first
            if (isPipeListenerRunning)
            {
                Debug.WriteLine("ListenForKeyPresses already running, ignoring duplicate call");
                return;
            }

            // Try to acquire the semaphore
            bool semaphoreAcquired = false;
            try
            {
                // Set the flag first to prevent other concurrent calls
                isPipeListenerRunning = true;

                // Try to acquire the semaphore with a timeout
                semaphoreAcquired = await pipeSemaphore.WaitAsync(1000);

                if (!semaphoreAcquired)
                {
                    Debug.WriteLine("Failed to acquire pipe semaphore, another operation might be in progress");
                    isPipeListenerRunning = false;
                    return;
                }

                Debug.WriteLine("Starting pipe listener loop");

                // Main loop for pipe communication
                while (true)
                {
                    NamedPipeServerStream localPipeServer = null;
                    StreamReader reader = null;

                    try
                    {
                        // Always properly dispose the old pipe server first
                        try
                        {
                            if (pipeServer != null)
                            {
                                Debug.WriteLine("Disposing old pipe server");
                                pipeServer.Dispose();
                                pipeServer = null;
                            }
                        }
                        catch (Exception disposeEx)
                        {
                            Debug.WriteLine($"Error disposing old pipe: {disposeEx.Message}");
                        }

                        // Create a new pipe server
                        Debug.WriteLine("Creating new pipe server instance");
                        pipeServer = new NamedPipeServerStream("ShortcutWatcher", PipeDirection.In, 1,
                            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                        // Keep a local reference to the pipe
                        localPipeServer = pipeServer;

                        Debug.WriteLine("Waiting for ShortcutWatcher pipe connection...");
                        await localPipeServer.WaitForConnectionAsync();
                        Debug.WriteLine("ShortcutWatcher pipe connected");

                        // Once connected, mark as running
                        isShortcutWatcherRunning = true;
                        restartAttempts = 0;

                        // Stream reader for this pipe
                        reader = new StreamReader(localPipeServer);

                        string message;
                        while ((message = await reader.ReadLineAsync()) != null)
                        {
                            Debug.WriteLine($"Received from ShortcutWatcher: {message}");

                            // Process messages
                            if (message.StartsWith("STARTUP:"))
                            {
                                // Startup message
                                string pidString = message.Substring("STARTUP:".Length);
                                if (int.TryParse(pidString, out int pid))
                                {
                                    Debug.WriteLine($"ShortcutWatcher started successfully with PID: {pid}");
                                    isShortcutWatcherRunning = true;
                                    restartAttempts = 0;
                                }
                            }
                            else if (message == "ALIVE")
                            {
                                // Heartbeat
                                Debug.WriteLine("Received alive signal from ShortcutWatcher");
                                isShortcutWatcherRunning = true;
                            }
                            else
                            {
                                // Process key press
                                ProcessKeyPress(message);
                            }
                        }

                        // If we get here cleanly, the pipe was disconnected normally
                        Debug.WriteLine("Pipe disconnected normally");
                    }
                    catch (Exception ex)
                    {
                        _telemetry.LogError("Error in ListenForKeyPresses", ex);

                        // Check process status and decide what to do
                        bool processRunning = shortcutWatcherProcess != null && !shortcutWatcherProcess.HasExited;

                        if (!processRunning)
                        {
                            Debug.WriteLine("ShortcutWatcher process not running, requesting restart");
                            isShortcutWatcherRunning = false;

                            // Release semaphore before potentially long operation
                            if (semaphoreAcquired)
                            {
                                pipeSemaphore.Release();
                                semaphoreAcquired = false;
                            }

                            await RestartShortcutWatcherWithDelay();

                            // Re-acquire semaphore
                            semaphoreAcquired = await pipeSemaphore.WaitAsync(1000);
                            if (!semaphoreAcquired)
                            {
                                Debug.WriteLine("Failed to re-acquire semaphore after restart");
                                break;  // Exit the loop
                            }
                        }
                        else if (ex is IOException && ex.Message.Contains("busy"))
                        {
                            Debug.WriteLine("Pipe is busy, forcing ShortcutWatcher restart");

                            // Release semaphore before potentially long operation
                            if (semaphoreAcquired)
                            {
                                pipeSemaphore.Release();
                                semaphoreAcquired = false;
                            }

                            await ForceRestartShortcutWatcher();

                            // Re-acquire semaphore
                            semaphoreAcquired = await pipeSemaphore.WaitAsync(1000);
                            if (!semaphoreAcquired)
                            {
                                Debug.WriteLine("Failed to re-acquire semaphore after forced restart");
                                break;  // Exit the loop
                            }
                        }
                        else
                        {
                            Debug.WriteLine("ShortcutWatcher process still running, recreating pipe only");

                            // Clean up resources
                            try
                            {
                                reader?.Dispose();
                                reader = null;

                                if (localPipeServer != null && localPipeServer != pipeServer)
                                {
                                    localPipeServer.Dispose();
                                    localPipeServer = null;
                                }
                            }
                            catch (Exception cleanupEx)
                            {
                                _telemetry.LogWarning("Error during cleanup in ListenForKeyPresses", cleanupEx);
                            }
                        }
                    }
                    finally
                    {
                        // Always clean up resources in finally block
                        try
                        {
                            reader?.Dispose();
                        }
                        catch (Exception ex)
                        {
                            _telemetry.LogWarning("Error disposing pipe reader in finally", ex);
                        }

                        try
                        {
                            if (localPipeServer != null && localPipeServer != pipeServer)
                            {
                                localPipeServer.Dispose();
                            }
                        }
                        catch (Exception ex)
                        {
                            _telemetry.LogWarning("Error disposing local pipe server in finally", ex);
                        }
                    }

                    // Wait a bit before retrying to avoid tight loops
                    await Task.Delay(1000);
                }
            }
            finally
            {
                // Always clean up in the finally block
                isPipeListenerRunning = false;

                // Only release if we acquired it
                if (semaphoreAcquired)
                {
                    try
                    {
                        pipeSemaphore.Release();
                    }
                    catch (SemaphoreFullException)
                    {
                        _telemetry.LogWarning("Tried to release semaphore when it was already released");
                    }
                    catch (Exception ex)
                    {
                        _telemetry.LogError("Error releasing semaphore", ex);
                    }
                }

                Debug.WriteLine("ListenForKeyPresses exited");
            }
        }

        private void NavView_Loaded(object sender, RoutedEventArgs e)
        {
            // Add handler for ContentFrame navigation.
            ContentFrame.Navigated += On_Navigated;

            // NavView doesn't load any page by default, so load home page.
            NavView.SelectedItem = NavView.MenuItems[0];
            // If navigation occurs on SelectionChanged, this isn't needed.
            // Because we use ItemInvoked to navigate, we need to call Navigate
            // here to load the home page.
            NavView_Navigate(typeof(RandomNameGeneratorPage), new EntranceNavigationTransitionInfo());
            NavView.Header = null;
        }

        private void NavView_Navigate(Type navPageType, NavigationTransitionInfo transitionInfo)
        {
            Type preNavPageType = ContentFrame.CurrentSourcePageType;

            if (navPageType is not null && !Type.Equals(preNavPageType, navPageType))
            {
                // Don't handle Clock navigation here as it's handled in NavView_ItemInvoked
                if (navPageType != typeof(Clock))
                {
                    ContentFrame.Navigate(navPageType, null, transitionInfo);
                }
            }
        }

        private void On_Navigated(object sender, NavigationEventArgs e)
        {
            Debug.WriteLine($"On_Navigated called for {e.SourcePageType.Name} with parameter: {e.Parameter}");
            NavView.IsBackEnabled = ContentFrame.CanGoBack;

            if (ContentFrame.Content is AutomatedPage page)
            {
                string className = page.GetType().Name;
                AutomationProperties.SetAutomationId(page, className);
                var peer = FrameworkElementAutomationPeer.FromElement(page);
                if (peer == null)
                {
                    peer = new FrameworkElementAutomationPeer(page);
                }
            }

            if (ContentFrame.SourcePageType == typeof(SettingsPage))
            {
                NavView.SelectedItem = NavView.SettingsItem;
            }
            else if (ContentFrame.SourcePageType != null)
            {
                NavView.SelectedItem = NavView.MenuItems
                            .OfType<NavigationViewItem>()
                            .First(i => i.Tag.Equals(ContentFrame.SourcePageType.FullName.ToString()));
            }

            dragHelper.OnNavigate();

            // Only enable always on top for the RNG page
            if (ContentFrame.SourcePageType == typeof(RandomNameGeneratorPage))
            {
                this.SetIsAlwaysOnTop(true);
            }
            else
            {
                this.SetIsAlwaysOnTop(false);
            }
        }

        private void NavView_BackRequested(NavigationView sender,
                                   NavigationViewBackRequestedEventArgs args)
        {
            TryGoBack();
        }

        private bool TryGoBack()
        {
            if (!ContentFrame.CanGoBack)
                return false;

            // Don't go back if the nav pane is overlayed.
            if (NavView.IsPaneOpen &&
                (NavView.DisplayMode == NavigationViewDisplayMode.Compact ||
                 NavView.DisplayMode == NavigationViewDisplayMode.Minimal))
                return false;

            ContentFrame.GoBack();
            return true;
        }

        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked)
            {
                NavView_Navigate(typeof(SettingsPage), args.RecommendedNavigationTransitionInfo);
            }
            else if (args.InvokedItemContainer != null)
            {
                Type navPageType = Type.GetType(args.InvokedItemContainer.Tag.ToString());

                if (navPageType == typeof(Clock))
                {
                    ContentFrame.Navigate(navPageType, _sleepPreventer, args.RecommendedNavigationTransitionInfo);
                }
                else
                {
                    NavView_Navigate(navPageType, args.RecommendedNavigationTransitionInfo);
                }
            }
        }

        private void Grid_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            dragHelper.PointerReleased(sender, e);
        }

        private void Grid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            dragHelper.PointerPressed(sender, e);
        }

        private void Grid_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            dragHelper.PointerMoved(sender, e);
        }

        private void SetRegionsForCustomTitleBar()
        {
            try
            {
                InputNonClientPointerSource nonClientInputSrc =
                    InputNonClientPointerSource.GetForWindowId(this.AppWindow.Id);

                // Get the scale factor for proper DPI handling
                double scaleFactor = 1.0;
                if (Content?.XamlRoot != null)
                {
                    scaleFactor = Content.XamlRoot.RasterizationScale;
                }

                // Get the title bar height (already in physical pixels)
                int titleBarHeight = this.AppWindow.TitleBar.Height;
                if (titleBarHeight == 0)
                {
                    // Fallback to typical title bar height (32 DIPs) scaled for DPI
                    titleBarHeight = (int)(32 * scaleFactor);
                }

                // Get window dimensions (in physical pixels)
                var windowSize = this.AppWindow.Size;

                // Set empty caption regions to disable title bar dragging
                nonClientInputSrc.SetRegionRects(NonClientRegionKind.Caption,
                    new Windows.Graphics.RectInt32[] { });

                // Calculate the window control buttons area
                // Base width is ~138px at 100% scale (46px per button x 3 buttons), add margin for safety
                int controlButtonsWidth = (int)(150 * scaleFactor);
                int contentAreaWidth = windowSize.Width - controlButtonsWidth;

                // Only set passthrough for the content area, excluding the window control buttons
                if (contentAreaWidth > 0)
                {
                    var passthroughRect = new Windows.Graphics.RectInt32
                    {
                        X = 0,
                        Y = 0,
                        Width = contentAreaWidth,
                        Height = titleBarHeight
                    };

                    nonClientInputSrc.SetRegionRects(NonClientRegionKind.Passthrough,
                        new Windows.Graphics.RectInt32[] { passthroughRect });

                    Debug.WriteLine($"Title bar regions set - Passthrough area: {passthroughRect.Width}x{passthroughRect.Height}, Scale: {scaleFactor}, Control buttons preserved");
                }
                else
                {
                    // Window too narrow, don't set passthrough to avoid breaking control buttons
                    nonClientInputSrc.SetRegionRects(NonClientRegionKind.Passthrough,
                        new Windows.Graphics.RectInt32[] { });

                    Debug.WriteLine("Window too narrow, no passthrough regions set");
                }
            }
            catch (Exception ex)
            {
                _telemetry.LogWarning("Error setting title bar regions", ex);
            }
        }

        public void UpdateTitleBarTheme()
        {
            try
            {                
                _themeService?.UpdateTitleBarTheme(this);
            }
            catch (Exception ex)
            {
                _telemetry.LogWarning("Error updating title bar theme", ex);
            }
        }

        // Process key presses on the UI thread
        private void ProcessKeyPress(string message)
        {
            try
            {
                // Ensure we run UI operations on the UI thread
                this.DispatcherQueue.TryEnqueue(() => {
                    try
                    {
                        // If ALT + number key is pressed, create a timerWindow with the specified time
                        if (message.StartsWith("D"))
                        {
                            string time = message.Substring(1);
                            if (int.TryParse(time, out int timeInt))
                            {
                                TimerWindow timerWindow;

                                // If the number is 0, start a 30 second timer.  Otherwise do it for that number of minutes
                                if (timeInt == 0)
                                {
                                    timerWindow = new TimerWindow(30);
                                }
                                else if (timeInt == 9)
                                {
                                    timerWindow = new TimerWindow(0);
                                }
                                else
                                {
                                    timerWindow = new TimerWindow(timeInt * 60);
                                }

                                timerWindow.Activate();
                            }
                        }
                        else if (message == "F9")
                        {
                            // Grab focus and unminiize the window
                            this.Activate();

                            // Navigate to the RandomNameGeneratorPage page if needed
                            if (ContentFrame.SourcePageType != typeof(RandomNameGeneratorPage))
                            {
                                NavView.SelectedItem = NavView.MenuItems[1];
                                NavView_Navigate(typeof(RandomNameGeneratorPage), new EntranceNavigationTransitionInfo());
                            }

                            // Call the GenerateName function of the RandomNameGeneratorPage page
                            if (ContentFrame.Content is RandomNameGeneratorPage randomNameGenerator)
                            {
                                randomNameGenerator.GenerateName();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _telemetry.LogError("Error processing UI action for key press", ex);
                    }
                });
            }
            catch (Exception ex)
            {
                _telemetry.LogError($"Error dispatching key press {message}", ex);
            }
        }

        // Force restart with better semaphore handling
        private async Task ForceRestartShortcutWatcher()
        {
            Debug.WriteLine("Force restarting ShortcutWatcher");

            try
            {
                // Terminate any existing process
                if (shortcutWatcherProcess != null)
                {
                    try
                    {
                        shortcutWatcherProcess.Exited -= OnShortcutWatcherExited;

                        if (!shortcutWatcherProcess.HasExited)
                        {
                            shortcutWatcherProcess.Kill(true); // Force immediate termination
                            shortcutWatcherProcess.WaitForExit(1000);
                        }

                        shortcutWatcherProcess.Dispose();
                        shortcutWatcherProcess = null;
                    }
                    catch (Exception ex)
                    {
                        _telemetry.LogWarning("Error killing process during force restart", ex);
                    }
                }

                // Clean up any other instances
                await Task.Run(() => CleanupExistingShortcutWatcherProcesses());

                // Reset state
                isShortcutWatcherRunning = false;
                restartAttempts = 0;

                // Request restart with a delay
                await RestartShortcutWatcherWithDelay();
            }
            catch (Exception ex)
            {
                _telemetry.LogError("Error during ShortcutWatcher force restart", ex);
            }
        }

        private async Task RestartWithDelayAsync()
        {
            // Add a delay to allow resources to be cleaned up
            await Task.Delay(2000);
            AttemptRestartIfNeeded();
        }

        // Start restart with a delay to allow cleanup
        private async Task RestartShortcutWatcherWithDelay()
        {
            Debug.WriteLine("Restarting ShortcutWatcher with delay");

            // Add a delay to allow resources to be cleaned up
            await Task.Delay(2000);

            // Call the synchronous method to avoid compatibility issues
            AttemptRestartIfNeeded();
        }
    }
}
