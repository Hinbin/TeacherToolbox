
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace TeacherToolbox.Converters
{
    /// <summary>
    /// Converts pause state to appropriate button text
    /// </summary>
    public class PauseResumeTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isPaused)
            {
                return isPaused ? "Resume" : "Pause";
            }
            return "Pause";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}