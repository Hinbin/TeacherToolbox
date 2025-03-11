using System.Text.Json.Serialization;

namespace TeacherToolbox.Model
{
    /// <summary>
    /// Configuration for a timer interval
    /// </summary>
    public class SavedIntervalConfig
    {
        /// <summary>
        /// Hours component of the interval
        /// </summary>
        [JsonPropertyName("hours")]
        public int Hours { get; set; }

        /// <summary>
        /// Minutes component of the interval
        /// </summary>
        [JsonPropertyName("minutes")]
        public int Minutes { get; set; }

        /// <summary>
        /// Seconds component of the interval
        /// </summary>
        [JsonPropertyName("seconds")]
        public int Seconds { get; set; }

        /// <summary>
        /// Gets the total seconds represented by this interval
        /// </summary>
        [JsonIgnore]
        public int TotalSeconds => (Hours * 3600) + (Minutes * 60) + Seconds;

        /// <summary>
        /// Creates a new empty interval configuration
        /// </summary>
        public SavedIntervalConfig()
        {
            Hours = 0;
            Minutes = 0;
            Seconds = 0;
        }

        /// <summary>
        /// Creates a new interval configuration with the specified values
        /// </summary>
        public SavedIntervalConfig(int hours, int minutes, int seconds)
        {
            Hours = hours;
            Minutes = minutes;
            Seconds = seconds;
        }

        /// <summary>
        /// Creates a deep copy of this interval configuration
        /// </summary>
        public SavedIntervalConfig Clone()
        {
            return new SavedIntervalConfig
            {
                Hours = this.Hours,
                Minutes = this.Minutes,
                Seconds = this.Seconds
            };
        }
    }
}