using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TeacherToolbox.Model;

namespace TeacherToolbox.Services
{
    /// <summary>
    /// Monitors the clock and fires ReminderDue events when a configured reminder slot becomes due.
    /// All events are raised on the UI dispatcher thread.
    /// </summary>
    public sealed class RegisterReminderScheduler : IRegisterReminderService, IDisposable
    {
        private readonly ITelemetryService _telemetry;
        private readonly DispatcherQueue _dispatcher;
        private readonly IClock _clock;

        private RegisterReminderSettings _settings = new();

        // Tracks which reminder IDs have already fired today (keyed by slot ID + date)
        private readonly HashSet<string> _firedToday = new();

        // Main polling timer — interval recalculated after each settings update or fire
        private System.Threading.Timer _pollTimer;

        // Per-slot snooze timers (keyed by reminder ID)
        private readonly Dictionary<Guid, System.Threading.Timer> _snoozeTimers = new();

        private readonly object _lock = new();
        private bool _started;

        public event EventHandler<RegisterReminder> ReminderDue;

        public RegisterReminderScheduler(ITelemetryService telemetry, IClock clock = null)
        {
            _telemetry = telemetry;
            _dispatcher = DispatcherQueue.GetForCurrentThread();
            _clock = clock ?? SystemClock.Instance;
        }

        public Task StartAsync()
        {
            lock (_lock)
            {
                if (_started) return Task.CompletedTask;
                _started = true;
            }
            ArmPollTimer();
            _telemetry.LogInfo("RegisterReminderScheduler started.");
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            lock (_lock)
            {
                if (!_started) return Task.CompletedTask;
                _started = false;
                DisposePollTimer();
                DisposeAllSnoozeTimers();
            }
            _telemetry.LogInfo("RegisterReminderScheduler stopped.");
            return Task.CompletedTask;
        }

        public void UpdateSettings(RegisterReminderSettings settings)
        {
            lock (_lock)
            {
                _settings = settings ?? new RegisterReminderSettings();
                // Clear today's fired log so rescheduling is accurate after settings change
                _firedToday.Clear();
                DisposeAllSnoozeTimers();
                if (_started) ArmPollTimer();
            }
        }

        public void Snooze(RegisterReminder reminder)
        {
            lock (_lock)
            {
                CancelSnooze(reminder.Id);

                int snoozeMs = Math.Max(1, _settings.SnoozeMinutes) * 60 * 1000;
                var timer = new System.Threading.Timer(_ => FireReminder(reminder), null, snoozeMs, System.Threading.Timeout.Infinite);
                _snoozeTimers[reminder.Id] = timer;
            }
            _telemetry.LogInfo($"Register reminder snoozed for {_settings.SnoozeMinutes} min: {reminder.Label}");
        }

        public void Acknowledge(RegisterReminder reminder)
        {
            lock (_lock)
            {
                CancelSnooze(reminder.Id);
                MarkFiredToday(reminder);
            }
            _telemetry.LogInfo($"Register reminder acknowledged: {reminder.Label}");
        }

        // ------------------------------------------------------------------ internals

        private void ArmPollTimer()
        {
            DisposePollTimer();

            if (!_settings.MasterEnabled || _settings.Reminders == null || _settings.Reminders.Count == 0)
                return;

            TimeSpan delay = ComputeDelayToNextDue();
            if (delay == TimeSpan.MaxValue) return;

            // Cap at 1 hour to re-check midnight boundary (day rollover clears _firedToday)
            TimeSpan cappedDelay = delay < TimeSpan.FromHours(1) ? delay : TimeSpan.FromHours(1);

            _pollTimer = new System.Threading.Timer(OnPollTimerFired, null, cappedDelay, System.Threading.Timeout.InfiniteTimeSpan);
        }

        private void OnPollTimerFired(object _)
        {
            List<RegisterReminder> due;
            lock (_lock)
            {
                if (!_started) return;
                PurgeStaleFiredEntries();
                due = CollectDueReminders();
                foreach (var r in due) MarkFiredToday(r);
                ArmPollTimer();
            }
            foreach (var r in due) FireReminder(r);
        }

        private void FireReminder(RegisterReminder reminder)
        {
            void Raise() => ReminderDue?.Invoke(this, reminder);
            if (_dispatcher != null)
                _dispatcher.TryEnqueue(Raise);
            else
                Raise();
        }

        private List<RegisterReminder> CollectDueReminders() =>
            RegisterReminderLogic.GetDueReminders(_clock.Now, _settings, _firedToday);

        private TimeSpan ComputeDelayToNextDue() =>
            RegisterReminderLogic.ComputeDelayToNextDue(_clock.Now, _settings, _firedToday);

        private void MarkFiredToday(RegisterReminder r) =>
            _firedToday.Add(RegisterReminderLogic.MakeFiredKey(r.Id, _clock.Now));

        private void PurgeStaleFiredEntries()
        {
            // Entries from previous days are irrelevant — clear them to avoid unbounded growth
            string today = _clock.Now.ToString("yyyy-MM-dd");
            _firedToday.RemoveWhere(k => !k.EndsWith(today));
        }

        private void CancelSnooze(Guid id)
        {
            if (_snoozeTimers.TryGetValue(id, out var t))
            {
                t.Dispose();
                _snoozeTimers.Remove(id);
            }
        }

        private void DisposeAllSnoozeTimers()
        {
            foreach (var t in _snoozeTimers.Values) t.Dispose();
            _snoozeTimers.Clear();
        }

        private void DisposePollTimer()
        {
            _pollTimer?.Dispose();
            _pollTimer = null;
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _started = false;
                DisposePollTimer();
                DisposeAllSnoozeTimers();
            }
        }
    }
}
