using System;

namespace TeacherToolbox.Model
{
    [Flags]
    public enum ReminderDays
    {
        None = 0,
        Monday = 1,
        Tuesday = 2,
        Wednesday = 4,
        Thursday = 8,
        Friday = 16,
        Saturday = 32,
        Sunday = 64,
        Weekdays = Monday | Tuesday | Wednesday | Thursday | Friday,
        EveryDay = Weekdays | Saturday | Sunday
    }

    public sealed class RegisterReminder
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public int Hour { get; set; }
        public int Minute { get; set; }
        public string Label { get; set; } = "";
        public ReminderDays Days { get; set; } = ReminderDays.None;
    }
}
