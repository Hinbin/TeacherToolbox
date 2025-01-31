// ThemeHelper.cs
using Microsoft.UI.Xaml;
using Microsoft.UI;
using Windows.UI;
using Microsoft.UI.Xaml.Media;
using System;

namespace TeacherToolbox.Helpers
{
    public static class ThemeHelper
    {
        public static event EventHandler<ElementTheme> ThemeChanged;
        private static ElementTheme _rootTheme = ElementTheme.Default;

        public static ElementTheme RootTheme
        {
            get => _rootTheme;
            set
            {
                if (_rootTheme != value)
                {
                    _rootTheme = value;
                    OnThemeChanged(value);
                }
            }
        }

        private static void OnThemeChanged(ElementTheme newTheme)
        {
            ThemeChanged?.Invoke(null, newTheme);
            if (App.MainWindow?.Content is FrameworkElement mainContent)
            {
                mainContent.RequestedTheme = newTheme;
                if (App.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.UpdateTitleBarTheme();
                }
            }
        }

        public static void ApplyThemeToWindow(Window window)
        {
            if (window == null) return;

            // Apply theme to window content
            if (window.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = RootTheme;
            }

            // Apply theme to title bar
            TitleBarHelper.ApplyThemeToWindow(window);
        }

        public static bool IsDarkTheme()
        {
            return RootTheme == ElementTheme.Dark ||
                   (RootTheme == ElementTheme.Default &&
                    Application.Current.RequestedTheme == ApplicationTheme.Dark);
        }

        public static Color GetApplicationBackgroundColor()
        {
            if (Application.Current.Resources.TryGetValue("ApplicationPageBackgroundThemeBrush", out object brushObj) &&
                brushObj is SolidColorBrush brush)
            {
                return brush.Color;
            }

            // Fallback colors if resource isn't found
            return IsDarkTheme()
                ? Color.FromArgb(255, 32, 32, 32)
                : Color.FromArgb(255, 243, 243, 243);
        }
    }
}