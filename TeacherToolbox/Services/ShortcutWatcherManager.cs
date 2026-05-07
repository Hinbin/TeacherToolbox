using Microsoft.UI.Dispatching;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TeacherToolbox.Services
{
    public sealed class ShortcutWatcherManager : IShortcutWatcherService
    {
        private const int MaxRestartAttempts = 3;
        private const string ShortcutPipeName = "ShortcutWatcher";
        private const string ShutdownPipeName = "ShortcutWatcherShutdown";

        private readonly ITelemetryService _telemetry;
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly SemaphoreSlim _pipeSemaphore = new(1, 1);
        private readonly object _stateLock = new();

        private NamedPipeServerStream _pipeServer;
        private Process _shortcutWatcherProcess;
        private Timer _watchdogTimer;
        private CancellationTokenSource _shutdownCts;
        private Task _listenerTask;
        private Task _testListenerTask;
        private int _restartAttempts;
        private bool _isRunning;
        private int _pipeFailedChecks;
        private bool _isPipeListenerRunning;
        private bool _isStopping;

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

        public async Task StartAsync(CancellationToken ct = default)
        {
            lock (_stateLock)
            {
                if (_shutdownCts != null && !_shutdownCts.IsCancellationRequested)
                {
                    return;
                }

                _shutdownCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                _isStopping = false;
                _pipeFailedChecks = 0;
            }

            await Task.Run(() =>
            {
                try
                {
                    CleanupExistingShortcutWatcherProcesses();
                    EnsureListenerStarted();

#if DEBUG
                    if (Environment.GetEnvironmentVariable("TEACHER_TOOLBOX_TEST_SHORTCUT_PIPE") == "1")
                    {
                        EnsureTestListenerStarted();
                    }
#endif

                    StartProcess();
                }
                catch (Exception ex)
                {
                    _telemetry.LogError("Error starting ShortcutWatcher", ex);
                    AttemptRestartIfNeeded();
                }
            }, ct);
        }

        public async Task StopAsync()
        {
            CancellationTokenSource cts;
            Process process;

            lock (_stateLock)
            {
                _isStopping = true;
                cts = _shutdownCts;
                process = _shortcutWatcherProcess;
                _shutdownCts = null;
                _isRunning = false;
            }

            try
            {
                if (cts != null)
                {
                    await cts.CancelAsync();
                }
                _watchdogTimer?.Dispose();
                _watchdogTimer = null;

                try
                {
                    _pipeServer?.Dispose();
                    _pipeServer = null;
                }
                catch (Exception ex)
                {
                    _telemetry.LogWarning("Error disposing ShortcutWatcher pipe server", ex);
                }

                SendShutdownSignalToShortcutWatcher();

                if (process != null)
                {
                    try
                    {
                        process.Exited -= OnShortcutWatcherExited;

                        if (!process.HasExited && !process.WaitForExit(500))
                        {
                            process.Kill();
                        }
                    }
                    catch (Exception ex)
                    {
                        _telemetry.LogWarning("Error killing ShortcutWatcher during shutdown", ex);
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }

                if (_listenerTask != null)
                {
                    await Task.WhenAny(_listenerTask, Task.Delay(1000));
                }

                if (_testListenerTask != null)
                {
                    await Task.WhenAny(_testListenerTask, Task.Delay(1000));
                }
            }
            catch (Exception ex)
            {
                _telemetry.LogError("Error during ShortcutWatcher cleanup", ex);
            }
            finally
            {
                cts?.Dispose();
                lock (_stateLock)
                {
                    _shortcutWatcherProcess = null;
                    _listenerTask = null;
                    _testListenerTask = null;
                    _isPipeListenerRunning = false;
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            _pipeSemaphore.Dispose();
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
                args = new ShortcutPressedEventArgs
                {
                    Kind = ShortcutKind.Timer,
                    Number = number,
                    PressedAt = DateTimeOffset.UtcNow,
                    RawMessage = message
                };
                return true;
            }

            if (string.Equals(message, "F9", StringComparison.Ordinal))
            {
                args = new ShortcutPressedEventArgs
                {
                    Kind = ShortcutKind.RandomName,
                    Number = -1,
                    PressedAt = DateTimeOffset.UtcNow,
                    RawMessage = message
                };
                return true;
            }

            return false;
        }

        private void EnsureListenerStarted()
        {
            lock (_stateLock)
            {
                if (_listenerTask != null && !_listenerTask.IsCompleted)
                {
                    return;
                }

                _listenerTask = ListenForKeyPressesAsync(_shutdownCts.Token);
            }
        }

#if DEBUG
        private void EnsureTestListenerStarted()
        {
            lock (_stateLock)
            {
                if (_testListenerTask != null && !_testListenerTask.IsCompleted)
                {
                    return;
                }

                _testListenerTask = ListenForTestShortcutMessagesAsync(_shutdownCts.Token);
            }
        }
#endif

        private void StartProcess()
        {
            string exePath = ResolveShortcutWatcherPath();

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(exePath)
            };

            Debug.WriteLine($"Starting ShortcutWatcher.exe from {startInfo.WorkingDirectory}");

            var process = Process.Start(startInfo);
            if (process == null)
            {
                Debug.WriteLine("Failed to start ShortcutWatcher.exe - Process.Start returned null");
                AttemptRestartIfNeeded();
                return;
            }

            lock (_stateLock)
            {
                _shortcutWatcherProcess = process;
                _isRunning = true;
            }

            process.EnableRaisingEvents = true;
            process.Exited += OnShortcutWatcherExited;

            _ = VerifyShortcutWatcherRunningAsync();
            _watchdogTimer = new Timer(WatchdogCallback, null, 15000, 30000);

            Debug.WriteLine($"ShortcutWatcher process started with PID: {process.Id}");
            RaiseHealthChanged(true, $"ShortcutWatcher process started with PID {process.Id}");
        }

        private static string ResolveShortcutWatcherPath()
        {
            string exePath = Path.Combine(AppContext.BaseDirectory, "ShortcutWatcher.exe");
            if (File.Exists(exePath))
            {
                return exePath;
            }

            string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string assemblyDir = Path.GetDirectoryName(assemblyPath);
            exePath = Path.Combine(assemblyDir, "ShortcutWatcher.exe");
            if (File.Exists(exePath))
            {
                return exePath;
            }

            if (File.Exists("ShortcutWatcher.exe"))
            {
                return Path.GetFullPath("ShortcutWatcher.exe");
            }

            throw new FileNotFoundException("Could not find ShortcutWatcher.exe");
        }

        private void OnShortcutWatcherExited(object sender, EventArgs e)
        {
            if (_isStopping)
            {
                return;
            }

            Debug.WriteLine("ShortcutWatcher process exited unexpectedly");
            SetRunning(false, "ShortcutWatcher process exited unexpectedly");
            AttemptRestartIfNeeded();
        }

        private async Task VerifyShortcutWatcherRunningAsync()
        {
            try
            {
                await Task.Delay(6000);
                await VerifyPipeConnectionAsync();
            }
            catch (Exception ex)
            {
                _telemetry.LogError("ShortcutWatcher verification task failed", ex);
            }
        }

        private async Task VerifyPipeConnectionAsync()
        {
            try
            {
                Debug.WriteLine("Verifying ShortcutWatcher pipe connection...");

                if (_shortcutWatcherProcess == null || _shortcutWatcherProcess.HasExited)
                {
                    Debug.WriteLine("ShortcutWatcher process has exited before verification");
                    SetRunning(false, "ShortcutWatcher process exited before verification");
                    AttemptRestartIfNeeded();
                    return;
                }

                using (var testPipe = new NamedPipeClientStream(".", ShutdownPipeName, PipeDirection.Out))
                {
                    try
                    {
                        await testPipe.ConnectAsync(2000);

                        if (testPipe.IsConnected)
                        {
                            Debug.WriteLine("ShortcutWatcher shutdown pipe connection verified");
                            lock (_stateLock)
                            {
                                _isRunning = true;
                                _restartAttempts = 0;
                            }

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

                if (_shortcutWatcherProcess != null && !_shortcutWatcherProcess.HasExited)
                {
                    Debug.WriteLine("Process is still running but pipe connection failed - waiting for ALIVE signal");
                }
                else
                {
                    Debug.WriteLine("ShortcutWatcher pipe connection failed - process not running");
                    SetRunning(false, "ShortcutWatcher pipe connection failed; process not running");
                    AttemptRestartIfNeeded();
                }
            }
            catch (Exception ex)
            {
                _telemetry.LogError("ShortcutWatcher pipe connection test failed", ex);
                SetRunning(false, "ShortcutWatcher pipe connection test failed");
                AttemptRestartIfNeeded();
            }
        }

        private void WatchdogCallback(object state)
        {
            Task.Run(async () =>
            {
                try
                {
                    Debug.WriteLine("Watchdog checking ShortcutWatcher status...");

                    bool processRunning = _shortcutWatcherProcess != null && !_shortcutWatcherProcess.HasExited;

                    if (!processRunning)
                    {
                        Debug.WriteLine("Watchdog detected ShortcutWatcher process is not running");
                        SetRunning(false, "Watchdog detected ShortcutWatcher process is not running");
                        AttemptRestartIfNeeded();
                        return;
                    }

                    bool isPipeConnected = false;

                    try
                    {
                        if (_pipeServer != null && _pipeServer.IsConnected)
                        {
                            Debug.WriteLine("Main pipe server is connected");
                            isPipeConnected = true;
                        }
                        else
                        {
                            using (var testPipe = new NamedPipeClientStream(".", ShutdownPipeName, PipeDirection.Out))
                            {
                                await testPipe.ConnectAsync(2000);

                                if (testPipe.IsConnected)
                                {
                                    Debug.WriteLine("Watchdog: ShortcutWatcher shutdown pipe is responsive");
                                    isPipeConnected = true;
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

                    if (processRunning && !isPipeConnected)
                    {
                        Debug.WriteLine("Watchdog: Process is running but pipes are not responsive");

                        if (++_pipeFailedChecks >= 3)
                        {
                            Debug.WriteLine("Watchdog: Multiple pipe check failures, forcing process restart");
                            try
                            {
                                if (!_shortcutWatcherProcess.HasExited)
                                {
                                    _shortcutWatcherProcess.Kill();
                                }
                            }
                            catch (Exception ex)
                            {
                                _telemetry.LogWarning("Watchdog: failed to kill unresponsive ShortcutWatcher", ex);
                            }

                            SetRunning(false, "Watchdog restarting unresponsive ShortcutWatcher");
                            AttemptRestartIfNeeded();
                            _pipeFailedChecks = 0;
                        }
                    }
                    else if (processRunning && isPipeConnected)
                    {
                        Debug.WriteLine("Watchdog: ShortcutWatcher is running properly");
                        SetRunning(true, "ShortcutWatcher is running properly");
                        _pipeFailedChecks = 0;
                    }
                    else
                    {
                        Debug.WriteLine("Watchdog: ShortcutWatcher needs restart");
                        SetRunning(false, "Watchdog says ShortcutWatcher needs restart");
                        AttemptRestartIfNeeded();
                        _pipeFailedChecks = 0;
                    }
                }
                catch (Exception ex)
                {
                    _telemetry.LogError("Error in watchdog", ex);

                    try
                    {
                        SetRunning(false, "Watchdog error");
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
            if (_isStopping)
            {
                return;
            }

            Task.Run(() =>
            {
                try
                {
                    if (_restartAttempts < MaxRestartAttempts)
                    {
                        _restartAttempts++;
                        Debug.WriteLine($"Attempting to restart ShortcutWatcher (attempt {_restartAttempts} of {MaxRestartAttempts})");

                        DisposeCurrentProcessForRestart();
                        CleanupExistingShortcutWatcherProcesses();

                        Task.Delay(2000).Wait();

                        try
                        {
                            string exePath = ResolveShortcutWatcherPath();
                            var startInfo = new ProcessStartInfo
                            {
                                FileName = exePath,
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                WorkingDirectory = Path.GetDirectoryName(exePath)
                            };

                            Debug.WriteLine($"Restarting ShortcutWatcher.exe from {startInfo.WorkingDirectory}");

                            Process proc = Process.Start(startInfo);

                            if (proc != null)
                            {
                                lock (_stateLock)
                                {
                                    _shortcutWatcherProcess = proc;
                                    _isRunning = true;
                                }

                                proc.EnableRaisingEvents = true;
                                proc.Exited += OnShortcutWatcherExited;

                                Debug.WriteLine($"ShortcutWatcher restarted with PID: {proc.Id}");
                                RaiseHealthChanged(true, $"ShortcutWatcher restarted with PID {proc.Id}");
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
                        _telemetry.LogWarning("Maximum ShortcutWatcher restart attempts reached");
                        RaiseHealthChanged(false, "Maximum ShortcutWatcher restart attempts reached");
                    }
                }
                catch (Exception ex)
                {
                    _telemetry.LogError("Critical error in ShortcutWatcher restart", ex);
                }
            });
        }

        private void DisposeCurrentProcessForRestart()
        {
            if (_shortcutWatcherProcess == null)
            {
                return;
            }

            try
            {
                _shortcutWatcherProcess.Exited -= OnShortcutWatcherExited;
            }
            catch (Exception ex)
            {
                _telemetry.LogWarning("Failed to unsubscribe ShortcutWatcher Exited handler", ex);
            }

            try
            {
                if (!_shortcutWatcherProcess.HasExited)
                {
                    _shortcutWatcherProcess.Kill();
                    _shortcutWatcherProcess.WaitForExit(1000);
                }
            }
            catch (Exception ex)
            {
                _telemetry.LogWarning("Error killing ShortcutWatcher process during restart", ex);
            }

            try
            {
                _shortcutWatcherProcess.Dispose();
            }
            catch (Exception ex)
            {
                _telemetry.LogWarning("Failed to dispose ShortcutWatcher process", ex);
            }

            _shortcutWatcherProcess = null;
        }

        private void CleanupExistingShortcutWatcherProcesses()
        {
            try
            {
                var existingProcesses = Process.GetProcesses()
                    .Where(p => string.Equals(p.ProcessName, "ShortcutWatcher", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (existingProcesses.Count > 0)
                {
                    Debug.WriteLine($"Found {existingProcesses.Count} existing ShortcutWatcher processes");

                    foreach (var process in existingProcesses)
                    {
                        try
                        {
                            SendShutdownSignalToSpecificProcess(process.Id);

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
                using (var pipeClient = new NamedPipeClientStream(".", ShutdownPipeName, PipeDirection.Out))
                {
                    pipeClient.Connect(500);

                    if (pipeClient.IsConnected)
                    {
                        using (var writer = new StreamWriter(pipeClient))
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
            }
        }

        private void SendShutdownSignalToShortcutWatcher()
        {
            try
            {
                using (var pipeClient = new NamedPipeClientStream(".", ShutdownPipeName, PipeDirection.Out))
                {
                    pipeClient.Connect(1000);
                    using (var writer = new StreamWriter(pipeClient))
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

        private async Task ListenForKeyPressesAsync(CancellationToken cancellationToken)
        {
            if (_isPipeListenerRunning)
            {
                Debug.WriteLine("ListenForKeyPresses already running, ignoring duplicate call");
                return;
            }

            bool semaphoreAcquired = false;
            try
            {
                _isPipeListenerRunning = true;
                semaphoreAcquired = await _pipeSemaphore.WaitAsync(1000, cancellationToken);

                if (!semaphoreAcquired)
                {
                    Debug.WriteLine("Failed to acquire pipe semaphore, another operation might be in progress");
                    _isPipeListenerRunning = false;
                    return;
                }

                Debug.WriteLine("Starting pipe listener loop");

                while (!cancellationToken.IsCancellationRequested)
                {
                    NamedPipeServerStream localPipeServer = null;
                    StreamReader reader = null;

                    try
                    {
                        try
                        {
                            if (_pipeServer != null)
                            {
                                Debug.WriteLine("Disposing old pipe server");
                                _pipeServer.Dispose();
                                _pipeServer = null;
                            }
                        }
                        catch (Exception disposeEx)
                        {
                            Debug.WriteLine($"Error disposing old pipe: {disposeEx.Message}");
                        }

                        Debug.WriteLine("Creating new pipe server instance");
                        _pipeServer = new NamedPipeServerStream(
                            ShortcutPipeName,
                            PipeDirection.In,
                            1,
                            PipeTransmissionMode.Byte,
                            PipeOptions.Asynchronous);

                        localPipeServer = _pipeServer;

                        Debug.WriteLine("Waiting for ShortcutWatcher pipe connection...");
                        await localPipeServer.WaitForConnectionAsync(cancellationToken);
                        Debug.WriteLine("ShortcutWatcher pipe connected");

                        lock (_stateLock)
                        {
                            _isRunning = true;
                            _restartAttempts = 0;
                        }

                        reader = new StreamReader(localPipeServer);

                        string message;
                        while (!cancellationToken.IsCancellationRequested &&
                               (message = await reader.ReadLineAsync()) != null)
                        {
                            Debug.WriteLine($"Received from ShortcutWatcher: {message}");
                            ProcessWatcherMessage(message);
                        }

                        Debug.WriteLine("Pipe disconnected normally");
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _telemetry.LogError("Error in ListenForKeyPresses", ex);

                        bool processRunning = _shortcutWatcherProcess != null && !_shortcutWatcherProcess.HasExited;

                        if (!processRunning)
                        {
                            Debug.WriteLine("ShortcutWatcher process not running, requesting restart");
                            SetRunning(false, "ShortcutWatcher process not running, requesting restart");

                            if (semaphoreAcquired)
                            {
                                _pipeSemaphore.Release();
                                semaphoreAcquired = false;
                            }

                            await RestartShortcutWatcherWithDelay();

                            semaphoreAcquired = await _pipeSemaphore.WaitAsync(1000, cancellationToken);
                            if (!semaphoreAcquired)
                            {
                                Debug.WriteLine("Failed to re-acquire semaphore after restart");
                                break;
                            }
                        }
                        else if (ex is IOException && ex.Message.Contains("busy"))
                        {
                            Debug.WriteLine("Pipe is busy, forcing ShortcutWatcher restart");

                            if (semaphoreAcquired)
                            {
                                _pipeSemaphore.Release();
                                semaphoreAcquired = false;
                            }

                            await ForceRestartShortcutWatcher();

                            semaphoreAcquired = await _pipeSemaphore.WaitAsync(1000, cancellationToken);
                            if (!semaphoreAcquired)
                            {
                                Debug.WriteLine("Failed to re-acquire semaphore after forced restart");
                                break;
                            }
                        }
                        else
                        {
                            Debug.WriteLine("ShortcutWatcher process still running, recreating pipe only");

                            try
                            {
                                reader?.Dispose();
                                reader = null;

                                if (localPipeServer != null && localPipeServer != _pipeServer)
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
                            if (localPipeServer != null && localPipeServer != _pipeServer)
                            {
                                localPipeServer.Dispose();
                            }
                        }
                        catch (Exception ex)
                        {
                            _telemetry.LogWarning("Error disposing local pipe server in finally", ex);
                        }
                    }

                    await Task.Delay(1000, cancellationToken);
                }
            }
            finally
            {
                _isPipeListenerRunning = false;

                if (semaphoreAcquired)
                {
                    try
                    {
                        _pipeSemaphore.Release();
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

        private void ProcessWatcherMessage(string message)
        {
            if (message.StartsWith("STARTUP:", StringComparison.Ordinal))
            {
                string pidString = message.Substring("STARTUP:".Length);
                if (int.TryParse(pidString, out int pid))
                {
                    Debug.WriteLine($"ShortcutWatcher started successfully with PID: {pid}");
                    lock (_stateLock)
                    {
                        _isRunning = true;
                        _restartAttempts = 0;
                    }
                    RaiseHealthChanged(true, $"ShortcutWatcher startup message received from PID {pid}");
                }
            }
            else if (string.Equals(message, "ALIVE", StringComparison.Ordinal))
            {
                Debug.WriteLine("Received alive signal from ShortcutWatcher");
                SetRunning(true, "ShortcutWatcher alive signal received");
            }
            else if (TryParseShortcutMessage(message, out var args))
            {
                RaiseShortcutPressed(args);
            }
        }

#if DEBUG
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
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        ProcessWatcherMessage(message);
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

        private async Task ForceRestartShortcutWatcher()
        {
            Debug.WriteLine("Force restarting ShortcutWatcher");

            try
            {
                DisposeCurrentProcessForRestart();
                await Task.Run(() => CleanupExistingShortcutWatcherProcesses());

                lock (_stateLock)
                {
                    _isRunning = false;
                    _restartAttempts = 0;
                }

                await RestartShortcutWatcherWithDelay();
            }
            catch (Exception ex)
            {
                _telemetry.LogError("Error during ShortcutWatcher force restart", ex);
            }
        }

        private async Task RestartShortcutWatcherWithDelay()
        {
            Debug.WriteLine("Restarting ShortcutWatcher with delay");
            await Task.Delay(2000);
            AttemptRestartIfNeeded();
        }

        private void SetRunning(bool isRunning, string message)
        {
            bool changed;
            lock (_stateLock)
            {
                changed = _isRunning != isRunning;
                _isRunning = isRunning;
            }

            if (changed)
            {
                RaiseHealthChanged(isRunning, message);
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
    }
}
