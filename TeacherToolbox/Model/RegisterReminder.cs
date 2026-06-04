using System;

namespace TeacherToolbox.Model
{
    public sealed class RegisterReminder
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Label { get; set; } = "";
        public DaySchedule Monday { get; set; } = new();
        public DaySchedule Tuesday { get; set; } = new();
        public DaySchedule Wednesday { get; set; } = new();
        public DaySchedule Thursday { get; set; } = new();
        public DaySchedule Friday { get; set; } = new();
    }
}
