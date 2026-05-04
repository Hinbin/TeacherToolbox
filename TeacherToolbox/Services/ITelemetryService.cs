using System;
using System.Threading.Tasks;

namespace TeacherToolbox.Services
{
    public interface ITelemetryService
    {
        void CaptureException(Exception ex, string context = null);
        void LogInfo(string message);
        void LogWarning(string message, Exception ex = null);
        void LogError(string message, Exception ex = null);

        Task FlushAsync();

        string LogsDirectory { get; }
    }
}
