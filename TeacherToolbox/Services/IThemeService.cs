using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace TeacherToolbox.Services
{
    /// <summary>
    /// Interface for theme-related services
    /// </summary>
    public interface IThemeService
    {
        /// <summary>
        /// Gets whether the current theme is dark
        /// </summary>
        bool IsDarkTheme();

        /// <summary>
        /// Gets the appropriate hand color brush for the current theme
        /// </summary>
        SolidColorBrush GetHandColorBrush();

        /// <summary>
        /// Gets the current theme
        /// </summary>
        ElementTheme CurrentTheme { get; }
    }
}
