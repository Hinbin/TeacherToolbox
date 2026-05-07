using System.Threading.Tasks;

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
        private readonly System.Func<ISettingsService> _createSettingsService;

        public SettingsServiceFactory()
            : this(CreateLocalSettingsService)
        {
        }

        public SettingsServiceFactory(System.Func<ISettingsService> createSettingsService)
        {
            _createSettingsService = createSettingsService ?? throw new System.ArgumentNullException(nameof(createSettingsService));
        }

        public async Task<ISettingsService> CreateAsync()
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = _createSettingsService();
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
                        _instance = _createSettingsService();
                    }
                }
            }

            return _instance;
        }

        private static ISettingsService CreateLocalSettingsService()
        {
            var service = new LocalSettingsService();
            service.InitializeSync();
            return service;
        }
    }
}
