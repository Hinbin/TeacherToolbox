namespace TeacherToolbox.Model
{
    /// <summary>
    /// Defines the behavior of a timer when it finishes counting down
    /// </summary>
    public enum TimerFinishBehavior
    {
        /// <summary>
        /// Close the timer window when timer finishes
        /// </summary>
        CloseTimer = 0,

        /// <summary>
        /// Start counting up when timer finishes
        /// </summary>
        CountUp = 1,

        /// <summary>
        /// Stay at zero when timer finishes
        /// </summary>
        StayAtZero = 2
    }
}