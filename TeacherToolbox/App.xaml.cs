using Microsoft.UI.Xaml;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using TeacherToolbox.Helpers;
using TeacherToolbox.Model;
using System.Diagnostics;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace TeacherToolbox
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public sealed partial class App : Application
    {
        public static Window MainWindow { get; set; }
        public static bool HandleClosedEvents { get; set; } = true;
        private const string ThemeKey = "Theme";

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            MainWindow = new MainWindow();
            await InitializeAppThemeAsync();

            MainWindow.Activate();

            this.UnhandledException += (sender, args) =>
            {
                System.Diagnostics.Debug.WriteLine($"UNHANDLED EXCEPTION: {args.Message}");
                System.Diagnostics.Debug.WriteLine($"Exception: {args.Exception}");
                if (args.Exception.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Inner Exception: {args.Exception.InnerException}");
                    System.Diagnostics.Debug.WriteLine($"Inner Stack Trace: {args.Exception.InnerException.StackTrace}");
                }
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {args.Exception.StackTrace}");
                args.Handled = true;
            };
        }

        private async Task InitializeAppThemeAsync()
        {
            try
            {
                // Load saved settings
                var localSettings = await LocalSettings.GetSharedInstanceAsync();
                var savedThemeIndex = localSettings.GetValueOrDefault(ThemeKey, 0);

                // Convert index to ElementTheme
                ElementTheme theme = savedThemeIndex switch
                {
                    1 => ElementTheme.Light,
                    2 => ElementTheme.Dark,
                    _ => ElementTheme.Default
                };

                // Set the theme
                ThemeHelper.RootTheme = theme;

                // Apply theme to main window
                ThemeHelper.ApplyThemeToWindow(MainWindow);
            }
            catch (Exception ex)
            {
                // Log the error if you have logging set up
                Debug.WriteLine($"Failed to initialize theme: {ex}");

                // Fallback to default theme
                ThemeHelper.RootTheme = ElementTheme.Default;
                ThemeHelper.ApplyThemeToWindow(MainWindow);
            }
        }

    }
}
