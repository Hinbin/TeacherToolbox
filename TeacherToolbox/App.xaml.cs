using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using TeacherToolbox.Services;
using TeacherToolbox.ViewModels;

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
            // Register the settings service factory as singleton
            services.AddSingleton<ISettingsServiceFactory, SettingsServiceFactory>();

            // Register ISettingsService as singleton using factory
            services.AddSingleton<ISettingsService>(provider =>
            {
                var factory = provider.GetRequiredService<ISettingsServiceFactory>();
                // Use sync version for initial registration
                return factory.CreateSync();
            });

            // Register theme service as singleton
            services.AddSingleton<IThemeService>(provider => new ThemeService(this));

            // Register other services
            services.AddTransient<ISleepPreventer, SleepPreventer>();
            services.AddTransient<ITimerService, DispatcherTimerService>();

            // Register ViewModels
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<ClockViewModel>();
            services.AddTransient<TimerWindowViewModel>();
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
                var themeService = Services.GetRequiredService<IThemeService>();
                var settingsService = Services.GetRequiredService<ISettingsService>();

                // Ensure settings are loaded
                await settingsService.LoadSettings();

                var savedThemeIndex = settingsService.GetValueOrDefault(ThemeKey, 0);

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
            }
        }
    }
}