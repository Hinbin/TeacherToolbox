using System;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace TeacherToolbox.Services
{
    /// <summary>
    /// Production implementation of ITimerService using DispatcherTimer
    /// </summary>
    public class DispatcherTimerService : ITimerService, IDisposable
    {
        private DispatcherTimer _timer;
        private bool _disposed = false;

        public event EventHandler<object> Tick;

        public TimeSpan Interval
        {
            get => _timer?.Interval ?? TimeSpan.Zero;
            set
            {
                EnsureTimerCreated();
                _timer.Interval = value;
            }
        }

        public bool IsEnabled => _timer?.IsEnabled ?? false;

        private void EnsureTimerCreated()
        {
            if (_timer == null)
            {
                _timer = new DispatcherTimer();
                _timer.Tick += OnTimerTick;
            }
        }

        private void OnTimerTick(object sender, object e)
        {
            Tick?.Invoke(this, e);
        }

        public void Start()
        {
            EnsureTimerCreated();
            _timer.Start();
        }

        public void Stop()
        {
            _timer?.Stop();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_timer != null)
                {
                    _timer.Stop();
                    _timer.Tick -= OnTimerTick;
                    _timer = null;
                }
                _disposed = true;
            }
        }
    }
}