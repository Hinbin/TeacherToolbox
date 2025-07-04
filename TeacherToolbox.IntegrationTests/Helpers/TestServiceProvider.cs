using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.IO;
using TeacherToolbox.Services;
using TeacherToolbox.Tests.Services;

namespace TeacherToolbox.Tests.Helpers
{
    public static class TestServiceProvider
    {
        public static IServiceProvider CreateWithMocks()
        {
            var services = new ServiceCollection();

            // Register mock services
            var mockSettingsService = new Mock<ISettingsService>();
            var mockThemeService = new Mock<IThemeService>();
            var mockSleepPreventer = new Mock<ISleepPreventer>();
            var mockTimerService = new Mock<ITimerService>();

            services.AddSingleton(mockSettingsService.Object);
            services.AddSingleton(mockThemeService.Object);
            services.AddSingleton(mockSleepPreventer.Object);
            services.AddSingleton(mockTimerService.Object);

            return services.BuildServiceProvider();
        }

        public static IServiceProvider CreateWithDefaults()
        {
            var services = new ServiceCollection();
            string tempDir = Path.Combine(Path.GetTempPath(), "TeacherToolboxTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            string _testFilePath = Path.Combine(tempDir, "test_settings.json");

            // Register real services for integration tests
            services.AddSingleton<ISettingsService>(new TestLocalSettingsService(_testFilePath));
            services.AddSingleton<IThemeService, ThemeService>();
            services.AddTransient<ISleepPreventer, SleepPreventer>();
            services.AddTransient<ITimerService, DispatcherTimerService>();

            return services.BuildServiceProvider();
        }
    }
}