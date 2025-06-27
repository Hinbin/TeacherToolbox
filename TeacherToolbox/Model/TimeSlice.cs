namespace TeacherToolbox.Model
{
    public enum RadialLevel
    {
        Inner,
        Outer
    }

    public class TimeSlice
    {
        public int StartMinute { get; set; }
        public int Duration { get; set; }
        public int RadialLevel { get; set; }
        public string Name { get; set; }

        public TimeSlice(int startMinute, int duration, int radialLevel, string name)
        {
            this.StartMinute = startMinute;
            this.Duration = duration;
            this.RadialLevel = radialLevel;
            this.Name = name;
        }

        public TimeSlice()
        {
        }

        public bool IsWithinTimeSlice(int minute, int radialLevel)
        {
            if (this.RadialLevel != radialLevel) return false;

            // Calculate the end minute of this slice
            int endMinute = (StartMinute + Duration) % 60;

            // Check if the slice crosses the hour boundary
            if (StartMinute + Duration > 60)
            {
                // Slice crosses hour boundary (e.g., 50-10 means from 50 minutes to 10 minutes)
                // The minute is within if it's either >= StartMinute OR < endMinute
                return minute >= StartMinute || minute < endMinute;
            }
            else
            {
                // Normal case: slice doesn't cross hour boundary
                return minute >= StartMinute && minute < StartMinute + Duration;
            }
        }

        public bool WouldOverlapWith(int startMinute, int duration, int radialLevel)
        {
            if (this.RadialLevel != radialLevel) return false;

            // Check every 5-minute interval of the proposed slice
            for (int i = 0; i < duration; i += 5)
            {
                int minuteToCheck = (startMinute + i) % 60;
                if (IsWithinTimeSlice(minuteToCheck, radialLevel))
                {
                    return true;
                }
            }

            // Also check if any minute of this slice would be within the proposed slice
            for (int i = 0; i < Duration; i += 5)
            {
                int minuteToCheck = (StartMinute + i) % 60;
                int proposedEndMinute = (startMinute + duration) % 60;

                // Check if the proposed slice crosses hour boundary
                if (startMinute + duration > 60)
                {
                    if (minuteToCheck >= startMinute || minuteToCheck < proposedEndMinute)
                    {
                        return true;
                    }
                }
                else
                {
                    if (minuteToCheck >= startMinute && minuteToCheck < startMinute + duration)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}