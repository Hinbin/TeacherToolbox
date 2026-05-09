using System;

namespace TeacherToolbox.Services
{
    public interface IClock
    {
        DateTime Now { get; }
    }

    public sealed class SystemClock : IClock
    {
        public static readonly SystemClock Instance = new();
        private SystemClock() { }
        public DateTime Now => DateTime.Now;
    }
}
