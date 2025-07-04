using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using TeacherToolbox.Services;

namespace TeacherToolbox
{
    public partial class App : Application
    {
        private const string ThemeKey = "Theme";

        public IServiceProvider Services { get; private set; }
        public static new App Current => (App)Application.Current;
        public static Window MainWindow { get; private set; }

        public App()
        {
            this.InitializeComponent();

            // Configure services before anything else
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            Services = serviceCollection.BuildServiceProvider();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Register theme service as singleton
            services.AddSingleton<IThemeService>(provider => new ThemeService(this));

            // Note: LocalSettingsService might need special handling due to async initialization
            // You might need to register it differently or use a factory pattern
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            MainWindow = new MainWindow();
            await InitializeAppThemeAsync();
            MainWindow.Activate();

            this.UnhandledException += (sender, args) =>
            {
                Debug.WriteLine($"UNHANDLED EXCEPTION: {args.Message}");
                Debug.WriteLine($"Exception: {args.Exception}");
                if (args.Exception.InnerException != null)
                {
                    Debug.WriteLine($"Inner Exception: {args.Exception.InnerException}");
                    Debug.WriteLine($"Inner Stack Trace: {args.Exception.InnerException.StackTrace}");
                }
                Debug.WriteLine($"Stack Trace: {args.Exception.StackTrace}");
                args.Handled = true;
            };
        }

        private async Task InitializeAppThemeAsync()
        {
            try
            {
                var themeService = Services.GetService<IThemeService>();
                var localSettings = await LocalSettingsService.GetSharedInstanceAsync();
                var savedThemeIndex = localSettings.GetValueOrDefault(ThemeKey, 0);

                // Convert index to ElementTheme
                ElementTheme theme = savedThemeIndex switch
                {
                    1 => ElementTheme.Light,
                    2 => ElementTheme.Dark,
                    _ => ElementTheme.Default
                };

                // Set the theme
                themeService.CurrentTheme = theme;

                // Apply theme to main window
                themeService.ApplyThemeToWindow(MainWindow);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to initialize theme: {ex}");
                // Fallback handled by theme service
            }
        }
    }
}