using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;

namespace TeacherToolbox.Services
{
    /// <summary>
    /// Comprehensive theme service implementation
    /// </summary>
    public class ThemeService : IThemeService
    {
        private ElementTheme _currentTheme = ElementTheme.Default;
        private readonly Application _application;

        public ThemeService(Application application = null)
        {
            _application = application ?? Application.Current;
        }

        public ElementTheme CurrentTheme
        {
            get => _currentTheme;
            set
            {
                if (_currentTheme != value)
                {
                    _currentTheme = value;
                    OnThemeChanged(value);
                }
            }
        }

        public bool IsDarkTheme
        {
            get
            {
                try
                {
                    return CurrentTheme == ElementTheme.Dark ||
                           (CurrentTheme == ElementTheme.Default &&
                            _application.RequestedTheme == ApplicationTheme.Dark);
                }
                catch
                {
                    return false; // Default to light theme on error
                }
            }
        }

        public SolidColorBrush HandColorBrush
        {
            get
            {
                try
                {
                    return new SolidColorBrush(IsDarkTheme ? Colors.White : Colors.Black);
                }
                catch
                {
                    // Return null if we can't create a brush (e.g., in unit tests)
                    return null;
                }
            }
        }

        public Color ApplicationBackgroundColor
        {
            get
            {
                try
                {
                    // Try to get the actual theme brush color
                    if (_application.Resources.TryGetValue("ApplicationPageBackgroundThemeBrush", out object brushObj) &&
                        brushObj is SolidColorBrush brush)
                    {
                        return brush.Color;
                    }
                }
                catch
                {
                    // Ignore errors and use fallback
                }

                // Fallback colors
                return IsDarkTheme
                    ? Color.FromArgb(255, 32, 32, 32)
                    : Color.FromArgb(255, 243, 243, 243);
            }
        }

        public event EventHandler<ElementTheme> ThemeChanged;

        public void ApplyThemeToWindow(Window window)
        {
            if (window == null) return;

            try
            {
                // Apply theme to window content
                if (window.Content is FrameworkElement rootElement)
                {
                    rootElement.RequestedTheme = CurrentTheme;
                }

                // Update title bar
                UpdateTitleBarTheme(window);
            }
            catch (Exception ex)
            {
                // Log error but don't crash
                System.Diagnostics.Debug.WriteLine($"Error applying theme to window: {ex.Message}");
            }
        }

        public void UpdateTitleBarTheme(Window window)
        {
            if (window == null || !AppWindowTitleBar.IsCustomizationSupported())
                return;

            try
            {
                var titleBar = window.AppWindow.TitleBar;
                var isDark = IsDarkTheme;

                // Set title bar background to transparent
                titleBar.BackgroundColor = null;
                titleBar.InactiveBackgroundColor = null;

                // Set transparent background for caption buttons
                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                titleBar.ButtonHoverBackgroundColor = Colors.Transparent;
                titleBar.ButtonPressedBackgroundColor = Colors.Transparent;

                // Set appropriate foreground colors based on theme
                if (isDark)
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
            catch (Exception ex)
            {
                // Log error but don't crash
                System.Diagnostics.Debug.WriteLine($"Error updating title bar theme: {ex.Message}");
            }
        }

        private void OnThemeChanged(ElementTheme newTheme)
        {
            ThemeChanged?.Invoke(this, newTheme);

            // Update main window if available
            if (App.MainWindow?.Content is FrameworkElement mainContent)
            {
                mainContent.RequestedTheme = newTheme;
                UpdateTitleBarTheme(App.MainWindow);
            }
        }
    }
}