using System;

namespace TeacherToolbox.Services
{
    /// <summary>
    /// Interface for timer operations that can be mocked in tests
    /// </summary>
    public interface ITimerService
    {
        /// <summary>
        /// Event fired when the timer ticks
        /// </summary>
        event EventHandler<object> Tick;

        /// <summary>
        /// Gets or sets the timer interval
        /// </summary>
        TimeSpan Interval { get; set; }

        /// <summary>
        /// Gets whether the timer is enabled/running
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Starts the timer
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the timer
        /// </summary>
        void Stop();
    }
}