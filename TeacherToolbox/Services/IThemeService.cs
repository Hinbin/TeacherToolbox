using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;

namespace TeacherToolbox.Services
{
    /// <summary>
    /// Interface for comprehensive theme management
    /// </summary>
    public interface IThemeService
    {
        /// <summary>
        /// Gets or sets the current application theme
        /// </summary>
        ElementTheme CurrentTheme { get; set; }

        /// <summary>
        /// Gets whether the current theme is dark
        /// </summary>
        bool IsDarkTheme { get; }

        /// <summary>
        /// Gets the appropriate hand color brush for the current theme
        /// </summary>
        SolidColorBrush HandColorBrush { get; }

        /// <summary>
        /// Gets the application background color for the current theme
        /// </summary>
        Color ApplicationBackgroundColor { get; }

        /// <summary>
        /// Event raised when the theme changes
        /// </summary>
        event EventHandler<ElementTheme> ThemeChanged;

        /// <summary>
        /// Applies the current theme to a window (including title bar)
        /// </summary>
        void ApplyThemeToWindow(Window window);

        /// <summary>
        /// Updates the title bar theme for a window
        /// </summary>
        void UpdateTitleBarTheme(Window window);
    }
}