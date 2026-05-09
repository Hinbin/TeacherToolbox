using System.Collections.Generic;

namespace TeacherToolbox.Model
{
    public sealed class RegisterReminderSettings
    {
        public bool MasterEnabled { get; set; } = false;
        public bool WeekdaysOnly { get; set; } = true;
        public int SnoozeMinutes { get; set; } = 3;
        public int SoundIndex { get; set; } = 1;
        public List<RegisterReminder> Reminders { get; set; } = new();
    }
}
