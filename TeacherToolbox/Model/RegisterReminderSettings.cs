using System.Collections.Generic;

namespace TeacherToolbox.Model
{
    public sealed class RegisterReminderSettings
    {
        public bool MasterEnabled { get; set; } = false;
        public int SnoozeMinutes { get; set; } = 3;
        public int SoundIndex { get; set; } = 5;
        public List<RegisterReminder> Reminders { get; set; } = new();
    }
}
