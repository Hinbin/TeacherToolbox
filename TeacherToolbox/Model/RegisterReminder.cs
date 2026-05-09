using System;

namespace TeacherToolbox.Model
{
    public sealed class RegisterReminder
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public int Hour { get; set; }
        public int Minute { get; set; }
        public string Label { get; set; } = "";
        public bool IsEnabled { get; set; } = false;
    }
}
