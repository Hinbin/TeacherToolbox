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

        private readonly OverlappedPresenter _presenter;
        private NamedPipeServerStream pipeServer;
        private WindowDragHelper dragHelper;
        private Process shortcutWatcherProcess;
        private System.Threading.Timer watchdogTimer;
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

            _presenter = this.AppWindow.Presenter as OverlappedPresenter;
            Windows.Graphics.SizeInt32 size = new(_Width: 600, _Height: 200);
            this.AppWindow.ResizeClient(size);

            this.ExtendsContentIntoTitleBar = true;
            UpdateTitleBarTheme();
            SetRegionsForCustomTitleBar(); // To allow the nav button to be selectable, but the rest of the title bar to function as normal

            NavView.IsPaneOpen = false;
            pipeFailedChecks = 0;

            pipeServer = new NamedPipeServerStream("ShotcutWatcher", PipeDirection.In);
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
                Debug.WriteLine(e.Message);
            }

            this.Closed += MainWindow_Closed;
            this.SizeChanged += MainWindow_SizeChanged;
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
                        Debug.WriteLine("ShortcutWatcher shutdown pipe connection timed out");
                    }
                    catch (Exception innerEx)
                    {
                        Debug.WriteLine($"Error during verification: {innerEx.Message}");
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
                Debug.WriteLine($"ShortcutWatcher pipe connection test failed: {ex.Message}");
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
                        Debug.WriteLine($"Watchdog: Error checking pipe status: {pipeEx.Message}");
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
                            catch { }

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
                    Debug.WriteLine($"Error in watchdog: {ex.Message}");

                    // If an exception occurs in watchdog, try restarting
                    try
                    {
                        isShortcutWatcherRunning = false;
                        AttemptRestartIfNeeded();
                    }
                    catch { }
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
                            catch { }

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
                                Debug.WriteLine($"Error killing process: {ex.Message}");
                            }

                            // Clean up
                            try { shortcutWatcherProcess.Dispose(); }
                            catch { }

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
                            Debug.WriteLine($"Error finding ShortcutWatcher.exe: {ex.Message}");
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
                            Debug.WriteLine($"Error starting ShortcutWatcher process: {ex.Message}");
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
                    Debug.WriteLine($"Critical error in restart: {ex.Message}");
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
                            Debug.WriteLine($"Error shutting down process {process.Id}: {ex.Message}");

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
                                Debug.WriteLine($"Failed to forcibly terminate process {process.Id}: {killEx.Message}");
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
                Debug.WriteLine($"Error during existing process cleanup: {ex.Message}");
            }
        }

        private void SendShutdownSignalToSpecificProcess(int processId)
        {
            try
            {
                // Try to create a pipe specifically named for this process ID
                // Try the default pipe first - this is the most likely to work
                using (NamedPipeClientStream pipeClient = new NamedPipeClientStream(".", "ShotcutWatcherShutdown", PipeDirection.Out))
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
                Debug.WriteLine($"Failed to send shutdown signal to process {processId}: {ex.Message}");
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
                        pipeServer = new NamedPipeServerStream("ShotcutWatcher", PipeDirection.In, 1);
                        ListenForKeyPresses();
                        Debug.WriteLine("Pipe server setup complete");
                    }
                    catch (Exception pipeEx)
                    {
                        Debug.WriteLine($"Error setting up pipe server: {pipeEx.Message}, will retry");

                        // Retry after a short delay
                        Task.Delay(1000).ContinueWith(_ => {
                            try
                            {
                                pipeServer?.Dispose();
                                pipeServer = new NamedPipeServerStream("ShotcutWatcher", PipeDirection.In, 1);
                                ListenForKeyPresses();
                                Debug.WriteLine("Pipe server setup retry successful");
                            }
                            catch (Exception retryEx)
                            {
                                Debug.WriteLine($"Retry failed: {retryEx.Message}");
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
                        watchdogTimer = new System.Threading.Timer(WatchdogCallback, null, 15000, 30000);

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
                    Debug.WriteLine($"Error starting ShortcutWatcher: {e.Message}");
                    AttemptRestartIfNeeded();
                }
            });
        }

        private void MainWindow_SizeChanged(object sender, WindowSizeChangedEventArgs e)
        {
            SetRegionsForCustomTitleBar();
        }

        private void MainWindow_Closed(object sender, WindowEventArgs e)
        {
            try
            {
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
                            Debug.WriteLine($"Error killing ShortcutWatcher: {ex}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during cleanup: {ex}");
            }
        }

        private void SendShutdownSignalToShortcutWatcher()
        {
            try
            {
                using (NamedPipeClientStream pipeClient = new NamedPipeClientStream(".", "ShotcutWatcherShutdown", PipeDirection.Out))
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
                Debug.WriteLine($"Error sending shutdown signal: {ex.Message}");
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
                        pipeServer = new NamedPipeServerStream("ShotcutWatcher", PipeDirection.In, 1,
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
                        Debug.WriteLine($"Error in ListenForKeyPresses: {ex.Message}");

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
                                Debug.WriteLine($"Error during cleanup: {cleanupEx.Message}");
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
                        catch { }

                        try
                        {
                            if (localPipeServer != null && localPipeServer != pipeServer)
                            {
                                localPipeServer.Dispose();
                            }
                        }
                        catch { }
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
                        Debug.WriteLine("Warning: Tried to release semaphore when it was already released");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error releasing semaphore: {ex.Message}");
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
            NavView_Navigate(typeof(RandomNameGenerator), new EntranceNavigationTransitionInfo());
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
            if (ContentFrame.SourcePageType == typeof(RandomNameGenerator))
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

        private void NavView_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // If the random name generator is currently open, generate a new name
            // Call the GenerateName function of the RandomNameGenerator page
            if (ContentFrame.Content is RandomNameGenerator randomNameGenerator)
            {
                // Return if a button was the source of the tap
                if (e.OriginalSource is Button)
                {
                    return;
                }

                // If the sender is a text block with the word start, return
                if (e.OriginalSource is TextBlock textBlock)
                {
                    if (textBlock.Text == "Add Class" || textBlock.Text == "Remove Class" || textBlock.Text == "More")
                    {
                        return;
                    }
                }


                randomNameGenerator.GenerateName();
            }
        }

        private void SetRegionsForCustomTitleBar()
        {
            try
            {
                InputNonClientPointerSource nonClientInputSrc =
                    InputNonClientPointerSource.GetForWindowId(this.AppWindow.Id);

                // Get the title bar height
                int titleBarHeight = this.AppWindow.TitleBar.Height;
                if (titleBarHeight == 0)
                {
                    // Fallback to typical title bar height if not available
                    titleBarHeight = 32;
                }

                // Get window dimensions
                var windowSize = this.AppWindow.Size;

                // Set empty caption regions to disable title bar dragging
                nonClientInputSrc.SetRegionRects(NonClientRegionKind.Caption,
                    new Windows.Graphics.RectInt32[] { });

                // Calculate the window control buttons area (typically ~168px wide for all three buttons)
                int controlButtonsWidth = 168;
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

                    Debug.WriteLine($"Title bar regions set - Passthrough area: {passthroughRect.Width}x{passthroughRect.Height}, Control buttons preserved");
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
                Debug.WriteLine($"Error setting title bar regions: {ex.Message}");
            }
        }

        public void UpdateTitleBarTheme()
        {
            try
            {
                var themeService = App.Current.Services?.GetService<IThemeService>();
                themeService?.UpdateTitleBarTheme(this);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating title bar theme: {ex.Message}");
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

                            // Navigate to the RandomNameGenerator page if needed
                            if (ContentFrame.SourcePageType != typeof(RandomNameGenerator))
                            {
                                NavView.SelectedItem = NavView.MenuItems[1];
                                NavView_Navigate(typeof(RandomNameGenerator), new EntranceNavigationTransitionInfo());
                            }

                            // Call the GenerateName function of the RandomNameGenerator page
                            if (ContentFrame.Content is RandomNameGenerator randomNameGenerator)
                            {
                                randomNameGenerator.GenerateName();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error processing UI action: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error dispatching key press {message}: {ex.Message}");
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
                        Debug.WriteLine($"Error killing process: {ex.Message}");
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
                Debug.WriteLine($"Error during force restart: {ex.Message}");
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
