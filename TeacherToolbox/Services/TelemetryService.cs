using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Serilog.Core;

namespace TeacherToolbox.Services
{
    public class LocalTelemetryService : ITelemetryService, IDisposable
    {
        private static readonly Regex UserPathRegex = new Regex(
            @"[A-Za-z]:\\Users\\[^\\\/""'<>|*?:]+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly Logger _logger;

        protected string AppDataDirectory { get; }

        public string LogsDirectory { get; }

        public LocalTelemetryService()
        {
            AppDataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TeacherToolbox");

            LogsDirectory = Path.Combine(AppDataDirectory, "logs");

            Directory.CreateDirectory(LogsDirectory);

            _logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(
                    path: Path.Combine(LogsDirectory, "teachertoolbox-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            _logger.Information("{TelemetryService} initialized. App version {Version}, OS {OS}, Arch {Arch}",
                GetType().Name,
                GetAppVersion(),
                Environment.OSVersion,
                RuntimeInformation.ProcessArchitecture);
        }

        public virtual void CaptureException(Exception ex, string context = null)
        {
            if (ex == null) return;

            _logger.Error(ex, "Unhandled exception. Context={Context}", context ?? "<none>");
        }

        public virtual Task FlushAsync()
        {
            return Task.CompletedTask;
        }

        public void LogInfo(string message)
        {
            _logger.Information(message);
        }

        public void LogWarning(string message, Exception ex = null)
        {
            if (ex == null)
                _logger.Warning(message);
            else
                _logger.Warning(ex, message);
        }

        public void LogError(string message, Exception ex = null)
        {
            if (ex == null)
                _logger.Error(message);
            else
                _logger.Error(ex, message);
        }

        public virtual void Dispose()
        {
            _logger?.Dispose();
        }

        protected static string ScrubPii(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return UserPathRegex.Replace(input, m =>
            {
                int slashIdx = m.Value.IndexOf('\\', 3); // skip "C:\"
                string drive = m.Value.Substring(0, slashIdx + 1);
                return drive + "Users\\<user>";
            });
        }

        protected static string GetAppVersion()
        {
            return typeof(LocalTelemetryService).Assembly.GetName().Version?.ToString() ?? "unknown";
        }
    }

    public sealed class OutwoodTelemetryService : LocalTelemetryService
    {
        private static readonly System.Net.Http.HttpClient HttpClient = new System.Net.Http.HttpClient();
        private readonly SemaphoreSlim _flushLock = new SemaphoreSlim(1, 1);
        private readonly string _pendingTelemetryDirectory;

        // Google Apps Script Web App URL for the private Outwood crash-report sheet.
        private const string TelemetryEndpoint = "https://script.google.com/macros/s/AKfycby0ZNXL5WnGPB_rVWoPN73Gxi8BadMAlgbyY4HQe5077W0z7QZsTPqX1MR9z73VrJlK/exec";

        // Retention bounds for the on-disk pending queue.
        private const int MaxPendingReports = 100;
        private static readonly TimeSpan MaxPendingAge = TimeSpan.FromDays(30);

        public OutwoodTelemetryService()
        {
            _pendingTelemetryDirectory = Path.Combine(AppDataDirectory, "telemetry", "pending");
            Directory.CreateDirectory(_pendingTelemetryDirectory);
        }

        public override void CaptureException(Exception ex, string context = null)
        {
            if (ex == null) return;

            base.CaptureException(ex, context);

            try
            {
                var report = new TelemetryReport
                {
                    Version = GetAppVersion(),
                    Context = context ?? "unknown",
                    ExceptionType = ex.GetType().Name,
                    Message = ScrubPii(ex.Message),
                    StackTrace = ScrubPii(ex.StackTrace ?? "no stack trace"),
                    OS = Environment.OSVersion.ToString(),
                    CPU = RuntimeInformation.ProcessArchitecture.ToString(),
                    MemoryBytes = Process.GetCurrentProcess().WorkingSet64,
                    Timestamp = DateTime.UtcNow
                };

                string fileName = $"crash_{DateTime.UtcNow.Ticks}.json";
                string filePath = Path.Combine(_pendingTelemetryDirectory, fileName);

                string json = JsonSerializer.Serialize(report);

                // Sync write is intentional: this method is called from unhandled-exception
                // handlers where the runtime may tear down before an async continuation runs.
                File.WriteAllText(filePath, json);

                LogInfo("Buffered crash report to disk: " + fileName);
            }
            catch (Exception writeEx)
            {
                LogWarning("Failed to buffer crash report to disk", writeEx);
            }
        }

        public override async Task FlushAsync()
        {
            if (!await _flushLock.WaitAsync(0)) return;

            try
            {
                PrunePendingReports();

                var pendingFiles = Directory.GetFiles(_pendingTelemetryDirectory, "*.json");
                if (pendingFiles.Length == 0) return;

                LogInfo($"Found {pendingFiles.Length} pending telemetry reports. Attempting to upload...");

                foreach (var file in pendingFiles)
                {
                    try
                    {
                        string json = await File.ReadAllTextAsync(file);
                        var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");

                        var response = await HttpClient.PostAsync(TelemetryEndpoint, content);

                        if (response.IsSuccessStatusCode)
                        {
                            LogInfo("Successfully uploaded telemetry report: " + Path.GetFileName(file));
                            File.Delete(file);
                        }
                        else if (IsPermanentFailure(response.StatusCode))
                        {
                            LogWarning($"Dropping telemetry report {Path.GetFileName(file)} after permanent failure. Status: {response.StatusCode}");
                            File.Delete(file);
                        }
                        else
                        {
                            LogWarning($"Failed to upload telemetry report {Path.GetFileName(file)}. Status: {response.StatusCode}. Will retry later.");
                        }
                    }
                    catch (Exception uploadEx)
                    {
                        LogWarning("Error uploading telemetry report " + Path.GetFileName(file), uploadEx);
                    }
                }
            }
            catch (Exception ex)
            {
                LogWarning("Error during telemetry flush", ex);
            }
            finally
            {
                _flushLock.Release();
            }
        }

        private void PrunePendingReports()
        {
            try
            {
                var files = Directory.GetFiles(_pendingTelemetryDirectory, "*.json")
                    .Select(f => new FileInfo(f))
                    .ToList();

                DateTime cutoff = DateTime.UtcNow - MaxPendingAge;
                foreach (var fi in files.Where(f => f.LastWriteTimeUtc < cutoff).ToList())
                {
                    try { fi.Delete(); files.Remove(fi); }
                    catch (Exception ex) { LogWarning("Failed to delete aged telemetry report " + fi.Name, ex); }
                }

                if (files.Count > MaxPendingReports)
                {
                    foreach (var fi in files.OrderBy(f => f.LastWriteTimeUtc).Take(files.Count - MaxPendingReports))
                    {
                        try { fi.Delete(); }
                        catch (Exception ex) { LogWarning("Failed to delete excess telemetry report " + fi.Name, ex); }
                    }
                }
            }
            catch (Exception ex)
            {
                LogWarning("Error pruning pending telemetry reports", ex);
            }
        }

        private static bool IsPermanentFailure(HttpStatusCode status)
        {
            // 4xx are client errors and won't succeed on retry, except for transient throttling/timeouts.
            int code = (int)status;
            if (code < 400 || code >= 500) return false;
            return status != HttpStatusCode.RequestTimeout       // 408
                && (int)status != 429;                           // Too Many Requests
        }

        public override void Dispose()
        {
            _flushLock?.Dispose();
            base.Dispose();
        }

        public class TelemetryReport
        {
            public string Version { get; set; }
            public string Context { get; set; }
            public string ExceptionType { get; set; }
            public string Message { get; set; }
            public string StackTrace { get; set; }
            public string OS { get; set; }
            public string CPU { get; set; }
            public long MemoryBytes { get; set; }
            public DateTime Timestamp { get; set; }
        }
    }
}
