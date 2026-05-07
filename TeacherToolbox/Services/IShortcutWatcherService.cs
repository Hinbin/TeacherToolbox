using System;
using System.Threading;
using System.Threading.Tasks;

namespace TeacherToolbox.Services
{
    public interface IShortcutWatcherService : IAsyncDisposable
    {
        Task StartAsync(CancellationToken ct = default);
        Task StopAsync();
        bool IsRunning { get; }
        event EventHandler<ShortcutPressedEventArgs> ShortcutPressed;
        event EventHandler<WatcherHealthChangedEventArgs> HealthChanged;
    }

    public enum ShortcutKind
    {
        Timer,
        RandomName
    }

    public class ShortcutPressedEventArgs : EventArgs
    {
        public ShortcutKind Kind { get; init; }
        public int Number { get; init; }
        public DateTimeOffset PressedAt { get; init; }
        public string RawMessage { get; init; }
    }

    public class WatcherHealthChangedEventArgs : EventArgs
    {
        public bool IsRunning { get; init; }
        public string Message { get; init; }
        public DateTimeOffset ChangedAt { get; init; }
    }
}
