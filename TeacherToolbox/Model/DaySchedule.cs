namespace TeacherToolbox.Model
{
    public sealed class DaySchedule
    {
        public bool Enabled { get; set; }
        public int Hour { get; set; } = 9;
        public int Minute { get; set; } = 0;
    }
}
