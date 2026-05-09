using System;
using System.Threading.Tasks;
using TeacherToolbox.Model;

namespace TeacherToolbox.Services
{
    public interface IRegisterReminderService
    {
        event EventHandler<RegisterReminder> ReminderDue;

        Task StartAsync();
        Task StopAsync();
        void UpdateSettings(RegisterReminderSettings settings);

        /// <summary>Schedules a one-off snooze re-fire after the configured delay.</summary>
        void Snooze(RegisterReminder reminder);

        /// <summary>Marks the reminder as done for today, cancelling any pending snooze.</summary>
        void Acknowledge(RegisterReminder reminder);
    }
}
