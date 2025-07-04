using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using TeacherToolbox.Helpers;
using TeacherToolbox.Model;
using TeacherToolbox.Services;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace TeacherToolbox.ViewModels
{
    public class TimerWindowViewModel : ObservableObject, IDisposable
    {
        #region Private Fields

        // Services
        private readonly ISettingsService _settingsService;
        private readonly IThemeService _themeService;

        // Timer state
        private DispatcherTimer _timer;
        private int _secondsLeft;
        private int _initialSeconds;
        private bool _isPaused;

        // Interval state
        private Queue<IntervalTime> _intervals;
        private int _intervalCount;
        private int _intervalNumber;
        private int _currentIntervalTotal;

        // Media player
        private MediaPlayer _player;
        private bool _isSoundAvailable;

        // UI state
        private string _timerText = "00:00";
        private string _intervalInfoText = "";
        private bool _isTimerSetupVisible;
        private bool _isTimerGaugeVisible = true;
        private double _timerGaugeValue;
        private double _timerGaugeMaximum;
        private double _timerGaugeMinimum;
        private int _timerGaugeTickSpacing = 5;
        private SolidColorBrush _timerTextColor = new SolidColorBrush(Microsoft.UI.Colors.Black);
        private SolidColorBrush _trailBrush;

        // Interval setup
        private ObservableCollection<IntervalTimeViewModel> _intervalsList = new ObservableCollection<IntervalTimeViewModel>();
        private bool _isAddIntervalButtonVisible;

        #endregion

        #region Properties

        public string TimerText
        {
            get => _timerText;
            private set => SetProperty(ref _timerText, value);
        }

        public string IntervalInfoText
        {
            get => _intervalInfoText;
            private set => SetProperty(ref _intervalInfoText, value);
        }

        public bool IsIntervalInfoVisible => !string.IsNullOrEmpty(_intervalInfoText);

        public bool IsTimerSetupVisible
        {
            get => _isTimerSetupVisible;
            private set => SetProperty(ref _isTimerSetupVisible, value);
        }

        public bool IsTimerGaugeVisible
        {
            get => _isTimerGaugeVisible;
            private set => SetProperty(ref _isTimerGaugeVisible, value);
        }

        public double TimerGaugeValue
        {
            get => _timerGaugeValue;
            private set => SetProperty(ref _timerGaugeValue, value);
        }

        public double TimerGaugeMaximum
        {
            get => _timerGaugeMaximum;
            private set => SetProperty(ref _timerGaugeMaximum, value);
        }

        public double TimerGaugeMinimum
        {
            get => _timerGaugeMinimum;
            private set => SetProperty(ref _timerGaugeMinimum, value);
        }

        public int TimerGaugeTickSpacing
        {
            get => _timerGaugeTickSpacing;
            private set => SetProperty(ref _timerGaugeTickSpacing, value);
        }

        public SolidColorBrush TimerTextColor
        {
            get => _timerTextColor;
            private set => SetProperty(ref _timerTextColor, value);
        }

        public SolidColorBrush TrailBrush
        {
            get => _trailBrush;
            private set => SetProperty(ref _trailBrush, value);
        }

        public ObservableCollection<IntervalTimeViewModel> IntervalsList
        {
            get => _intervalsList;
            private set => SetProperty(ref _intervalsList, value);
        }

        public bool IsAddIntervalButtonVisible
        {
            get => _isAddIntervalButtonVisible;
            private set => SetProperty(ref _isAddIntervalButtonVisible, value);
        }

        public bool CanAddInterval => _intervalsList.Count < 8;

        public bool IsPaused
        {
            get => _isPaused;
            private set => SetProperty(ref _isPaused, value);
        }

        #endregion

        #region Commands

        public IRelayCommand StartTimerCommand { get; }
        public IRelayCommand AddIntervalCommand { get; }
        public IRelayCommand<object> RemoveIntervalCommand { get; }
        public IRelayCommand PauseResumeTimerCommand { get; }

        #endregion

        #region Events

        public event EventHandler TimerFinished;

        #endregion

        #region Constructor

        // Constructor with dependency injection
        public TimerWindowViewModel(ISettingsService settingsService, int seconds, IThemeService themeService = null)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _initialSeconds = seconds;
            _themeService = themeService ?? App.Current.Services?.GetService<IThemeService>();

            // Initialize commands
            StartTimerCommand = new RelayCommand(StartTimerFromSelection);
            AddIntervalCommand = new RelayCommand(AddInterval, () => _intervalsList.Count < 8);
            RemoveIntervalCommand = new RelayCommand<object>(param => RemoveInterval(param as IntervalTimeViewModel));
            PauseResumeTimerCommand = new RelayCommand(PauseResumeTimer);

            // Initialize UI
            _trailBrush = new SolidColorBrush(Microsoft.UI.Colors.Purple); // Will be updated by theme

            // Load sound
            InitializeSound();

            // Initialize timer based on type
            InitializeTimer(seconds);
        }

        #endregion

        #region Timer Initialization

        private async void InitializeSound()
        {
            try
            {
                int soundIndex = _settingsService.GetTimerSound();
                string soundFileName = SoundSettings.GetSoundFileName(soundIndex);
                string soundPath = Path.Combine(AppContext.BaseDirectory, "Assets", soundFileName);

                if (File.Exists(soundPath))
                {
                    _player = new MediaPlayer();
                    _player.Source = MediaSource.CreateFromUri(new Uri(soundPath));
                    _isSoundAvailable = true;
                }
                else
                {
                    // Try loading default sound
                    string defaultSoundPath = Path.Combine(AppContext.BaseDirectory, "Assets", SoundSettings.GetSoundFileName(0));
                    if (File.Exists(defaultSoundPath))
                    {
                        _player = new MediaPlayer();
                        _player.Source = MediaSource.CreateFromUri(new Uri(defaultSoundPath));
                        _isSoundAvailable = true;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("No sound files available");
                        _isSoundAvailable = false;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading sound: {ex.Message}");
                _isSoundAvailable = false;
            }
        }

        private void InitializeTimer(int seconds)
        {
            if (seconds > 0)
            {
                // Direct timer setup
                SetupDirectTimer(seconds);
            }
            else if (seconds == 0)
            {
                // Custom timer setup
                SetupCustomTimerSelection(false);
            }
            else if (seconds == -1)
            {
                // Interval timer setup
                SetupCustomTimerSelection(true);
            }
        }

        private void SetupDirectTimer(int seconds)
        {
            _secondsLeft = seconds;
            TimerGaugeMaximum = seconds < 60 ? 60 : seconds;
            TimerGaugeMinimum = 0;
            TimerGaugeValue = _secondsLeft;
            IsTimerGaugeVisible = true;
            IsTimerSetupVisible = false;

            UpdateTimerText();
            SetTimerTickInterval();

            StartTimer();
        }

        private void SetupCustomTimerSelection(bool isIntervalTimer)
        {
            IsTimerSetupVisible = true;
            IsTimerGaugeVisible = false;
            IsAddIntervalButtonVisible = isIntervalTimer;

            // Load saved configurations
            LoadSavedConfigurations(isIntervalTimer);

            // If no saved configurations, add a default first interval
            if (!_intervalsList.Any())
            {
                _intervalsList.Add(new IntervalTimeViewModel(1));
            }

            UpdateIntervalNumbers();
        }

        private void LoadSavedConfigurations(bool isIntervalTimer)
        {
            List<SavedIntervalConfig> savedConfigs;

            if (isIntervalTimer)
            {
                savedConfigs = _settingsService.GetSavedIntervalConfigs();
            }
            else
            {
                savedConfigs = _settingsService.GetSavedCustomTimerConfigs();
            }

            if (savedConfigs?.Any() == true)
            {
                _intervalsList.Clear();
                foreach (var config in savedConfigs)
                {
                    _intervalsList.Add(new IntervalTimeViewModel(_intervalsList.Count + 1)
                    {
                        Hours = config.Hours,
                        Minutes = config.Minutes,
                        Seconds = config.Seconds
                    });
                }
            }
        }

        #endregion

        #region Timer Operations

        private void StartTimerFromSelection()
        {
            _intervals = new Queue<IntervalTime>();
            var savedConfigs = new List<SavedIntervalConfig>();

            foreach (var intervalVM in _intervalsList)
            {
                if (intervalVM.TotalSeconds > 0)
                {
                    // Add to intervals queue
                    _intervals.Enqueue(new IntervalTime
                    {
                        Hours = intervalVM.Hours,
                        Minutes = intervalVM.Minutes,
                        Seconds = intervalVM.Seconds
                    });

                    // Save configuration
                    savedConfigs.Add(new SavedIntervalConfig
                    {
                        Hours = intervalVM.Hours,
                        Minutes = intervalVM.Minutes,
                        Seconds = intervalVM.Seconds
                    });
                }
            }

            // Save configurations
            if (savedConfigs.Any())
            {
                bool isIntervalTimer = _isAddIntervalButtonVisible;

                if (isIntervalTimer)
                {
                    _settingsService.SaveIntervalConfigs(savedConfigs);
                }
                else
                {
                    _settingsService.SaveCustomTimerConfigs(savedConfigs);
                }
            }

            _intervalCount = _intervals.Count;

            if (_intervals.Count > 0)
            {
                StartNextInterval();
                IsTimerSetupVisible = false;
                IsTimerGaugeVisible = true;
            }
        }

        private void StartNextInterval()
        {
            if (_intervals.Count > 0)
            {
                var nextInterval = _intervals.Dequeue();
                _intervalNumber++;
                _currentIntervalTotal = nextInterval.TotalSeconds;

                _secondsLeft = nextInterval.TotalSeconds;
                TimerGaugeMaximum = _currentIntervalTotal;
                TimerGaugeMinimum = 0;
                TimerGaugeValue = _currentIntervalTotal;

                SetTimerTickInterval();
                UpdateTimerText();
                UpdateIntervalInfo();

                StartTimer();
            }
        }

        private void StartTimer()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Tick -= TimerTick;
            }

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += TimerTick;
            _timer.Start();

            IsPaused = false;
        }

        private void TimerTick(object sender, object e)
        {
            _secondsLeft--;
            UpdateTimerText();

            if (_secondsLeft >= 0)
            {
                TimerGaugeValue = _secondsLeft;
            }

            if (_secondsLeft == 0)
            {
                // Play sound
                PlayTimerEndSound();

                // Check for more intervals
                if (_intervals?.Count > 0)
                {
                    StartNextInterval();
                }
                else
                {
                    HandleTimerFinish();
                }
            }
        }

        private void PlayTimerEndSound()
        {
            if (_isSoundAvailable && _player != null)
            {
                try
                {
                    _player.PlaybackSession.Position = TimeSpan.Zero;
                    _player.Play();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error playing sound: {ex.Message}");
                }
            }
        }

        private void HandleTimerFinish()
        {
            var behavior = _settingsService.GetTimerFinishBehavior();

            switch (behavior)
            {
                case TimerFinishBehavior.CloseTimer:
                    _timer.Stop();
                    TimerTextColor = new SolidColorBrush(Microsoft.UI.Colors.Red);

                    // Signal that the timer is finished, view should close the window
                    TimerFinished?.Invoke(this, EventArgs.Empty);
                    break;

                case TimerFinishBehavior.CountUp:
                    // Timer continues counting (negative values)
                    TimerTextColor = new SolidColorBrush(Microsoft.UI.Colors.Red);
                    break;

                case TimerFinishBehavior.StayAtZero:
                    _timer.Stop();
                    TimerTextColor = new SolidColorBrush(Microsoft.UI.Colors.Red);
                    _secondsLeft = 0;
                    UpdateTimerText();
                    break;
            }
        }

        private void UpdateTimerText()
        {
            int secondsToShow = Math.Abs(_secondsLeft);

            // Format based on duration
            if (secondsToShow >= 3600)
            {
                // Hours format (HH:MM:SS)
                int hours = secondsToShow / 3600;
                int minutes = (secondsToShow % 3600) / 60;
                int seconds = secondsToShow % 60;
                TimerText = $"{hours:D1}:{minutes:D2}:{seconds:D2}";
            }
            else if (secondsToShow >= 60)
            {
                // Minutes format (MM:SS)
                int minutes = secondsToShow / 60;
                int seconds = secondsToShow % 60;
                TimerText = $"{minutes:D1}:{seconds:D2}";
            }
            else
            {
                // Seconds only format (SS)
                TimerText = $"{secondsToShow:D1}";
            }

            // Set text color based on state
            if (_secondsLeft < 0)
            {
                TimerTextColor = new SolidColorBrush(Microsoft.UI.Colors.Red);
            }
            else
            {
                // Use appropriate theme color
                bool isDarkTheme = _themeService.IsDarkTheme;
                TimerTextColor = new SolidColorBrush(isDarkTheme ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Black);
            }
        }

        private void UpdateIntervalInfo()
        {
            if (_intervalCount > 1 && _secondsLeft >= 0)
            {
                IntervalInfoText = $"Interval {_intervalNumber}/{_intervalCount}";
                OnPropertyChanged(nameof(IsIntervalInfoVisible));
            }
            else
            {
                IntervalInfoText = "";
                OnPropertyChanged(nameof(IsIntervalInfoVisible));
            }
        }

        private void SetTimerTickInterval()
        {
            // This controls the tick marks on the RadialGauge
            if (TimerGaugeValue <= 60)
            {
                TimerGaugeTickSpacing = 5;
            }
            else if (TimerGaugeValue <= 120)
            {
                TimerGaugeTickSpacing = 30;
            }
            else if (TimerGaugeValue <= 600)
            {
                TimerGaugeTickSpacing = 60;
            }
            else
            {
                TimerGaugeTickSpacing = (int)TimerGaugeValue / 10;
            }
        }

        private void PauseResumeTimer()
        {
            if (_timer == null) return;

            // Prevent pausing immediately after starting
            if (_secondsLeft == _currentIntervalTotal) return;

            if (_timer.IsEnabled)
            {
                _timer.Stop();
                IsPaused = true;

                // Set visual indication of pause
                TrailBrush = new SolidColorBrush(Microsoft.UI.Colors.DarkGray);
            }
            else
            {
                _timer.Start();
                IsPaused = false;

                // Use theme color for trail
                bool isDarkTheme = _themeService.IsDarkTheme;
                TrailBrush = Application.Current.Resources.TryGetValue("darkPurpleBrush", out object purpleBrush)
                    ? purpleBrush as SolidColorBrush
                    : new SolidColorBrush(Microsoft.UI.Colors.Purple);
            }
        }

        #endregion

        #region Interval Management

        private void AddInterval()
        {
            if (_intervalsList.Count < 8)
            {
                _intervalsList.Add(new IntervalTimeViewModel(_intervalsList.Count + 1));
                UpdateIntervalNumbers();
                OnPropertyChanged(nameof(CanAddInterval));
            }
        }

        private void RemoveInterval(IntervalTimeViewModel interval)
        {
            if (interval != null)
            {
                int removedIndex = _intervalsList.IndexOf(interval);
                if (removedIndex >= 0)
                {
                    _intervalsList.RemoveAt(removedIndex);
                    UpdateIntervalNumbers();
                    OnPropertyChanged(nameof(CanAddInterval));
                }
            }
        }

        private void UpdateIntervalNumbers()
        {
            for (int i = 0; i < _intervalsList.Count; i++)
            {
                _intervalsList[i].IntervalNumber = i + 1;
                _intervalsList[i].ShowRemoveButton = i > 0;
            }
        }

        #endregion

        #region Cleanup

        public void Dispose()
        {
            _timer?.Stop();
            _timer = null;

            _player?.Dispose();
            _player = null;
        }

        #endregion
    }
}