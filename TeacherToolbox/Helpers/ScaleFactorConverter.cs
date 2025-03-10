using Microsoft.UI.Xaml.Data;
using System;

namespace TeacherToolbox.Helpers
{
    public class ScaleFactorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is double size && parameter is string factorString)
            {
                if (double.TryParse(factorString, out double factor))
                {
                    return size * factor;
                }
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}