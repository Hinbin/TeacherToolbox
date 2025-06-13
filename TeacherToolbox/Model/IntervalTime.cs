using System;

namespace TeacherToolbox.Model
{
    /// <summary>
    /// Represents a time interval for the timer
    /// </summary>
    public class IntervalTime
    {
        /// <summary>
        /// Hours component of the interval
        /// </summary>
        public int Hours { get; set; }

        /// <summary>
        /// Minutes component of the interval
        /// </summary>
        public int Minutes { get; set; }

        /// <summary>
        /// Seconds component of the interval
        /// </summary>
        public int Seconds { get; set; }

        /// <summary>
        /// Gets the total seconds represented by this interval
        /// </summary>
        public int TotalSeconds => (Hours * 3600) + (Minutes * 60) + Seconds;
    }
}