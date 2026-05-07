using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using System;
using System.Threading.Tasks;
using TeacherToolbox.Services;
using TeacherToolbox.ViewModels;

namespace TeacherToolbox
{
    public partial class App : Application
    {
        private const string ThemeKey = "Theme";
        private static readonly TimeSpan TelemetryFlushInterval = TimeSpan.FromMinutes(10);

        private System.Threading.Timer _flushTimer;

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

            // Hook AppDomain and TaskScheduler exception handlers as early as possible.
            // App.UnhandledException is wired in OnLaunched once MainWindow exists.
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                {
                    var telemetry = Services.GetRequiredService<ITelemetryService>();
                    telemetry.LogInfo($"AppDomain.UnhandledException caught: {ex.GetType().Name}");
                    telemetry.CaptureException(ex, "AppDomain.UnhandledException");
                }
            };

            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                var telemetry = Services.GetRequiredService<ITelemetryService>();
                telemetry.LogInfo($"TaskScheduler.UnobservedTaskException caught: {args.Exception.GetType().Name}");
                telemetry.CaptureException(args.Exception, "TaskScheduler.UnobservedTaskException");
                args.SetObserved();
            };

            this.UnhandledException += (sender, args) =>
            {
                var telemetry = Services.GetRequiredService<ITelemetryService>();
                telemetry.LogInfo($"App.UnhandledException caught: {args.Exception.GetType().Name}");
                telemetry.CaptureException(args.Exception, "App.UnhandledException");
                args.Handled = true;
            };
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Telemetry must be first so other services can depend on it if needed
            services.AddSingleton<ITelemetryService, TelemetryService>();

            // Window service for HWND access
            services.AddSingleton<IWindowService, WindowService>();

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

            services.AddSingleton<IFilePickerService, FilePickerService>();
            services.AddSingleton<IShortcutWatcherService, ShortcutWatcherManager>();

            // Register other services
            services.AddTransient<ISleepPreventer, SleepPreventer>();
            services.AddTransient<ITimerService, DispatcherTimerService>();

            // Register ViewModels
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<ClockViewModel>();
            services.AddTransient<TimerWindowViewModel>();
            services.AddTransient<RandomNameGeneratorViewModel>();
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            MainWindow = ActivatorUtilities.CreateInstance<MainWindow>(Services);

            // Register main window with window service
            Services.GetRequiredService<IWindowService>().SetWindow(MainWindow);

            // Dispose DI container (and singletons that hold OS resources, like Serilog) on shutdown.
            MainWindow.Closed += (_, _) =>
            {
                _flushTimer?.Dispose();
                _flushTimer = null;
                (Services as IDisposable)?.Dispose();
            };

            await InitializeAppThemeAsync();
            MainWindow.Activate();

            // Flush telemetry reports on startup, then on a periodic timer so reports buffered
            // mid-session don't have to wait for the next launch.
            _ = Task.Run(async () =>
            {
                var telemetry = Services.GetRequiredService<ITelemetryService>();
                await telemetry.FlushAsync();
                telemetry.LogInfo("Application launched and telemetry flushed.");
            });

            _flushTimer = new System.Threading.Timer(
                _ =>
                {
                    // FlushAsync handles its own exceptions; fire-and-forget the Task.
                    try { _ = Services.GetRequiredService<ITelemetryService>().FlushAsync(); }
                    catch { /* defensive against teardown races on Services */ }
                },
                state: null,
                dueTime: TelemetryFlushInterval,
                period: TelemetryFlushInterval);
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
                Services.GetRequiredService<ITelemetryService>()
                    .LogError("Failed to initialize theme", ex);
            }
        }
    }
}
