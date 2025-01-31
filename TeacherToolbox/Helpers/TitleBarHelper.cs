using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using Windows.UI;
using Microsoft.UI;

namespace TeacherToolbox.Helpers
{
    public static class TitleBarHelper
    {
        public static void ApplyThemeToWindow(Window window)
        {
            if (window == null || !AppWindowTitleBar.IsCustomizationSupported()) return;

            var titleBar = window.AppWindow.TitleBar;
            var isDarkTheme = ThemeHelper.IsDarkTheme();

            if (isDarkTheme)
            {
                // Dark theme colors
                var darkBackground = Color.FromArgb(255, 32, 32, 32);

                // Active window state
                titleBar.BackgroundColor = darkBackground;
                titleBar.ButtonBackgroundColor = darkBackground;
                titleBar.ButtonForegroundColor = Colors.White;
                titleBar.ButtonHoverForegroundColor = Colors.White;
                titleBar.ButtonPressedForegroundColor = Colors.White;
                titleBar.ButtonHoverBackgroundColor = Color.FromArgb(255, 45, 45, 45);
                titleBar.ButtonPressedBackgroundColor = Color.FromArgb(255, 55, 55, 55);

                // Inactive window state
                titleBar.InactiveBackgroundColor = darkBackground;
                titleBar.ButtonInactiveBackgroundColor = darkBackground;
                titleBar.ButtonInactiveForegroundColor = Color.FromArgb(255, 180, 180, 180);
            }
            else
            {
                // Light theme colors
                var lightBackground = Color.FromArgb(255, 243, 243, 243);

                // Active window state
                titleBar.BackgroundColor = lightBackground;
                titleBar.ButtonBackgroundColor = lightBackground;
                titleBar.ButtonForegroundColor = Colors.Black;
                titleBar.ButtonHoverForegroundColor = Colors.Black;
                titleBar.ButtonPressedForegroundColor = Colors.Black;
                titleBar.ButtonHoverBackgroundColor = Color.FromArgb(255, 229, 229, 229);
                titleBar.ButtonPressedBackgroundColor = Color.FromArgb(255, 204, 204, 204);

                // Inactive window state
                titleBar.InactiveBackgroundColor = lightBackground;
                titleBar.ButtonInactiveBackgroundColor = lightBackground;
                titleBar.ButtonInactiveForegroundColor = Color.FromArgb(255, 100, 100, 100);
            }
        }

        public static Color ApplySystemThemeToCaptionButtons(Window window)
        {
            var backgroundColor = ThemeHelper.GetApplicationBackgroundColor();
            var luminance = (0.299 * backgroundColor.R + 0.587 * backgroundColor.G + 0.114 * backgroundColor.B) / 255;
            var foregroundColor = luminance > 0.5 ? Colors.Black : Colors.White;

            ApplyThemeToWindow(window);

            return foregroundColor;
        }
    }
}