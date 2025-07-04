using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace TeacherToolbox.Services
{
    public interface ISettingsServiceFactory
    {
        Task<ISettingsService> CreateAsync();
        ISettingsService CreateSync();
    }

    public class SettingsServiceFactory : ISettingsServiceFactory
    {
        private ISettingsService _instance;
        private readonly object _lock = new object();

        public async Task<ISettingsService> CreateAsync()
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        var service = new LocalSettingsService();
                        service.InitializeSync(); // Use sync in lock to avoid deadlock
                        _instance = service;
                    }
                }

                // Ensure async loading outside of lock
                await _instance.LoadSettings();
            }

            return _instance;
        }

        public ISettingsService CreateSync()
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        var service = new LocalSettingsService();
                        service.InitializeSync();
                        _instance = service;
                    }
                }
            }

            return _instance;
        }
    }
}