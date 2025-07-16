using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml;

using System;

namespace TeacherToolbox.Converters;

public class IsSelectedToStyleConverter : IValueConverter
{
    public Style DefaultStyle { get; set; }
    public Style SelectedStyle { get; set; }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isSelected)
        {
            return isSelected ? SelectedStyle : DefaultStyle;
        }
        return DefaultStyle;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}