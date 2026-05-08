using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TeacherToolbox.Model;
using TeacherToolbox.Helpers;
using TeacherToolbox.Services;
using Windows.Media.Core;
using Windows.Media.Playback;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;

namespace TeacherToolbox.ViewModels
{
    public class SettingsViewModel : ObservableObject
    {
        // Constants
        private const string ThemeKey = "Theme";
        private const string SoundKey = "Sound";
        private const string TimerFinishBehaviorKey = "TimerFinishBehavior";

        // Private fields
        private readonly ISettingsService _settingsService;
        private readonly ITelemetryService _telemetry;
        private readonly IFilePickerService _filePicker;
        private readonly IWindowService _windowService;
        private readonly IUriLauncherService _uriLauncher;
        private MediaPlayer _testPlayer;
        private int _selectedThemeIndex;
        private int _selectedTimerSoundIndex;
        private int _selectedTimerFinishBehaviorIndex;
        private List<Helpers.SoundSettings.SoundOption> _soundOptions;

        // Properties
        public int SelectedThemeIndex
        {
            get => _selectedThemeIndex;
            set
            {
                if (SetProperty(ref _selectedThemeIndex, value))
                {
                    UpdateAppTheme();
                }
            }
        }

        public int SelectedTimerSoundIndex
        {
            get => _selectedTimerSoundIndex;
            set
            {
                if (SetProperty(ref _selectedTimerSoundIndex, value))
                {
                    UpdateTimerSound();
                }
            }
        }

        public int SelectedTimerFinishBehaviorIndex
        {
            get => _selectedTimerFinishBehaviorIndex;
            set
            {
                if (SetProperty(ref _selectedTimerFinishBehaviorIndex, value))
                {
                    UpdateTimerFinishBehavior();
                }
            }
        }

        public List<Helpers.SoundSettings.SoundOption> SoundOptions => _soundOptions;

        public string AppVersion => VersionHelper.GetAppVersion();

        // Commands
        public IRelayCommand TestSoundCommand { get; }
        public IAsyncRelayCommand SendFeedbackCommand { get; }
        public IAsyncRelayCommand ViewFeedbackCommand { get; }
        public IAsyncRelayCommand SaveLogsCommand { get; }
        public IRelayCommand TestCrashCommand { get; }

        public bool IsDebugVisible =>
#if DEBUG
            true;
#else
            false;
#endif

        // Events for view interaction — passes the theme index (0=Default, 1=Light, 2=Dark)
        public event Action<int> ThemeChanged;

        /// <summary>
        /// Constructor that uses dependency injection for the settings service
        /// </summary>
        public SettingsViewModel(
            ISettingsService settingsService,
            ITelemetryService telemetry,
            IFilePickerService filePicker,
            IWindowService windowService,
            IUriLauncherService uriLauncher)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
            _filePicker = filePicker ?? throw new ArgumentNullException(nameof(filePicker));
            _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
            _uriLauncher = uriLauncher ?? throw new ArgumentNullException(nameof(uriLauncher));

            // Initialize sound options from SoundSettings helper
            _soundOptions = new List<Helpers.SoundSettings.SoundOption>();
            foreach (var option in Helpers.SoundSettings.SoundOptions.OrderBy(x => x.Key))
            {
                _soundOptions.Add(option.Value);
            }

            // Initialize commands
            TestSoundCommand = new RelayCommand(TestSound);
            SendFeedbackCommand = new AsyncRelayCommand(SendFeedbackAsync);
            ViewFeedbackCommand = new AsyncRelayCommand(ViewFeedbackAsync);
            SaveLogsCommand = new AsyncRelayCommand(SaveLogsAsync);
            TestCrashCommand = new RelayCommand(ExecuteTestCrash);

            // Initialize settings
            InitializeSettings();
        }

        private void ExecuteTestCrash()
        {
            throw new InvalidOperationException("Test crash to verify telemetry pipeline.");
        }

        private void InitializeSettings()
        {
            try
            {
                // Load settings from LocalSettings
                _selectedThemeIndex = _settingsService.GetTheme();
                _selectedTimerSoundIndex = _settingsService.GetTimerSound();
                _selectedTimerFinishBehaviorIndex = (int)_settingsService.GetTimerFinishBehavior();

                // Notify UI of property changes
                OnPropertyChanged(nameof(SelectedThemeIndex));
                OnPropertyChanged(nameof(SelectedTimerSoundIndex));
                OnPropertyChanged(nameof(SelectedTimerFinishBehaviorIndex));
            }
            catch (Exception ex)
            {
                _telemetry.LogError("Error initializing settings", ex);
            }
        }

        private void UpdateAppTheme()
        {
            // Notify the view to update the theme (0=Default/System, 1=Light, 2=Dark)
            ThemeChanged?.Invoke(SelectedThemeIndex);

            // Save the setting
            _settingsService.SetTheme(SelectedThemeIndex);
        }

        private void UpdateTimerSound()
        {
            _settingsService.SetTimerSound(SelectedTimerSoundIndex);
        }

        private void UpdateTimerFinishBehavior()
        {
            // Save the selected index which corresponds to the TimerFinishBehavior enum values
            _settingsService.SetTimerFinishBehavior((TimerFinishBehavior)SelectedTimerFinishBehaviorIndex);
        }

        // Helper method to convert selected index to enum value
        public TimerFinishBehavior GetSelectedTimerFinishBehavior()
        {
            return (TimerFinishBehavior)SelectedTimerFinishBehaviorIndex;
        }

        private void TestSound()
        {
            try
            {
                string soundFileName = Helpers.SoundSettings.GetSoundFileName(SelectedTimerSoundIndex);
                string soundPath = Path.Combine(AppContext.BaseDirectory, "Assets", soundFileName);

                if (_testPlayer != null)
                {
                    _testPlayer.Dispose();
                }

                _testPlayer = new MediaPlayer();
                _testPlayer.Source = MediaSource.CreateFromUri(new Uri(soundPath));
                _testPlayer.Play();
            }
            catch (Exception ex)
            {
                _telemetry.LogWarning("Error playing test sound", ex);
            }
        }

        private async Task SendFeedbackAsync()
        {
            try
            {
                var uri = new Uri("https://docs.google.com/forms/d/e/1FAIpQLScAKZmB6CN7jBhIiZ7E25Vn_80yPTEUWBTNV4ZMJQEeXrF42g/viewform");
                await _uriLauncher.LaunchUriAsync(uri);
            }
            catch (Exception ex)
            {
                _telemetry.LogWarning("Error launching feedback URL", ex);
            }
        }

        private async Task ViewFeedbackAsync()
        {
            try
            {
                var uri = new Uri("https://docs.google.com/spreadsheets/d/1fdZeVxytN2yPmk5jKqhIFK6_U_6s66v6A4w2uh_SBxA/edit?gid=0#gid=0");
                await _uriLauncher.LaunchUriAsync(uri);
            }
            catch (Exception ex)
            {
                _telemetry.LogWarning("Error launching feedback URL", ex);
            }
        }

        public IntPtr WindowHandle => _windowService.WindowHandle;

        private async Task SaveLogsAsync()
        {
            try
            {
                string logsDir = _telemetry.LogsDirectory;
                if (!Directory.Exists(logsDir))
                {
                    _telemetry.LogWarning("Logs directory does not exist: " + logsDir);
                    return;
                }

                // Create a temporary zip file
                string tempZipPath = Path.Combine(Path.GetTempPath(), $"TeacherToolbox_Logs_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
                
                if (File.Exists(tempZipPath))
                {
                    File.Delete(tempZipPath);
                }

                ZipFile.CreateFromDirectory(logsDir, tempZipPath);

                // Prompt user to save the file
                string fileName = $"TeacherToolbox_Logs_{DateTime.Now:yyyyMMdd}.zip";
                var file = await _filePicker.SaveFileAsync(WindowHandle, fileName, new[] { ".zip" });

                if (file != null)
                {
                    using (var stream = await file.OpenStreamForWriteAsync())
                    using (var tempStream = File.OpenRead(tempZipPath))
                    {
                        await tempStream.CopyToAsync(stream);
                    }
                }

                // Clean up temp file
                if (File.Exists(tempZipPath))
                {
                    File.Delete(tempZipPath);
                }
            }
            catch (Exception ex)
            {
                _telemetry.LogError("Failed to save diagnostic logs", ex);
            }
        }

        // Clean up resources
        public void Dispose()
        {
            _testPlayer?.Dispose();
        }
    }
}
