using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace TeacherToolbox.Converters
{
    /// <summary>
    /// Converts pause state to appropriate icon glyph for pause/play buttons
    /// </summary>
    public class PauseResumeIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isPaused)
            {
                // Return play icon when paused (to resume), pause icon when playing (to pause)
                return isPaused ? "\uE768" : "\uE769"; // Play: E768, Pause: E769
            }
            return "\uE769"; // Default to pause icon
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}