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

            // Set the title bar background to null (default)
            titleBar.BackgroundColor = null;
            titleBar.InactiveBackgroundColor = null;

            // Set transparent background for the caption buttons
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonHoverBackgroundColor = Colors.Transparent;
            titleBar.ButtonPressedBackgroundColor = Colors.Transparent;

            // Set appropriate foreground colors based on theme
            if (isDarkTheme)
            {
                titleBar.ButtonForegroundColor = Colors.White;
                titleBar.ButtonHoverForegroundColor = Colors.White;
                titleBar.ButtonPressedForegroundColor = Colors.LightGray;
                titleBar.ButtonInactiveForegroundColor = Color.FromArgb(255, 180, 180, 180);
            }
            else
            {
                titleBar.ButtonForegroundColor = Colors.Black;
                titleBar.ButtonHoverForegroundColor = Colors.Black;
                titleBar.ButtonPressedForegroundColor = Color.FromArgb(255, 80, 80, 80);
                titleBar.ButtonInactiveForegroundColor = Color.FromArgb(255, 100, 100, 100);
            }
        }

        public static Color ApplySystemThemeToCaptionButtons(Window window)
        {
            ApplyThemeToWindow(window);
            var isDarkTheme = ThemeHelper.IsDarkTheme();
            return isDarkTheme ? Colors.White : Colors.Black;
        }
    }
}