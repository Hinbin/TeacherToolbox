using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using TeacherToolbox.Model;
using TeacherToolbox.Helpers;
using TeacherToolbox.Services;
using Windows.Media.Core;
using Windows.Media.Playback;
using System.IO;
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

        // Commands
        public IRelayCommand TestSoundCommand { get; }
        public IRelayCommand SendFeedbackCommand { get; }

        // Events for view interaction
        public event Action<ElementTheme> ThemeChanged;

        /// <summary>
        /// Constructor that uses dependency injection for the settings service
        /// </summary>
        /// <param name="settingsService">The settings service to use</param>
        public SettingsViewModel(ISettingsService settingsService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

            // Initialize sound options from SoundSettings helper
            _soundOptions = new List<Helpers.SoundSettings.SoundOption>();
            foreach (var option in Helpers.SoundSettings.SoundOptions.OrderBy(x => x.Key))
            {
                _soundOptions.Add(option.Value);
            }

            // Initialize commands
            TestSoundCommand = new AsyncRelayCommand(TestSoundAsync);
            SendFeedbackCommand = new AsyncRelayCommand(SendFeedbackAsync);

            // Initialize settings
            InitializeSettings();
        }

        /// <summary>
        /// Default constructor that creates its own settings service instance
        /// </summary>
        public SettingsViewModel() : this(LocalSettingsService.GetSharedInstanceSync())
        {
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
                Debug.WriteLine($"Error initializing settings: {ex.Message}");
            }
        }

        private void UpdateAppTheme()
        {
            ElementTheme theme;
            switch (SelectedThemeIndex)
            {
                case 1: // Light
                    theme = ElementTheme.Light;
                    break;
                case 2: // Dark
                    theme = ElementTheme.Dark;
                    break;
                default: // System
                    theme = ElementTheme.Default;
                    break;
            }

            // Notify the view to update the theme
            ThemeChanged?.Invoke(theme);

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

        private async Task TestSoundAsync()
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
                Debug.WriteLine($"Error playing test sound: {ex.Message}");
            }
        }

        private async Task SendFeedbackAsync()
        {
            try
            {
                var uri = new Uri("https://teachertoolbox.canny.io/feature-requests/");
                await Windows.System.Launcher.LaunchUriAsync(uri);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error launching feedback URL: {ex.Message}");
            }
        }

        // Clean up resources
        public void Dispose()
        {
            _testPlayer?.Dispose();
        }
    }
}