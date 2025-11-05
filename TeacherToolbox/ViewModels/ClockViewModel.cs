using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using TeacherToolbox.Controls;
using TeacherToolbox.Helpers;
using TeacherToolbox.Model;
using TeacherToolbox.Services;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Media.Playback;
using System.IO;

namespace TeacherToolbox.ViewModels
{
    public class ClockViewModel : ObservableObject, IDisposable
    {
        // Constants
        private const int ClockSize = 200;
        private const int ClockCenter = 100;
        private const double HourHandLength = 70;
        private const double MinuteHandLength = 100;
        private const double SecondHandLength = 100;

        // Private fields
        private readonly ISettingsService _settingsService;
        private readonly ITimerService _timerService;
        private readonly IThemeService _themeService;

        private DateTime _currentTime;
        private TimeSpan _timeOffset;
        private string _digitalTimeText;
        private string _centreText;
        private double _hourHandAngle;
        private double _minuteHandAngle;
        private double _secondHandAngle;
        private readonly ObservableCollection<TimeSlice> _timeSlices;
        private readonly ISleepPreventer _sleepPreventer;
        private bool _disposed = false;
        private SolidColorBrush _handColorBrush;
        private int _gaugeNameCounter = 0;
        private bool _isSoundAvailable;


        // Mock Mode fields
        private bool _isMockMode;
        private bool _isPaused;
        private bool _isSoundEnabled;
        private MediaPlayer _mediaPlayer;
        private int _lastMinute = -1;
        private DateTime? _pausedTime;

        // Properties
        public DateTime CurrentTime
        {
            get => _currentTime;
            private set => SetProperty(ref _currentTime, value);
        }

        public TimeSpan TimeOffset
        {
            get => _timeOffset;
            set
            {
                if (SetProperty(ref _timeOffset, value))
                {
                    UpdateTime();
                }
            }
        }

        public string DigitalTimeText
        {
            get => _digitalTimeText;
            private set => SetProperty(ref _digitalTimeText, value);
        }

        public string CentreText
        {
            get => _centreText;
            set
            {
                if (SetProperty(ref _centreText, value))
                {
                    _settingsService?.SetCentreText(value);
                }
            }
        }

        public double HourHandAngle
        {
            get => _hourHandAngle;
            private set => SetProperty(ref _hourHandAngle, value);
        }

        public double MinuteHandAngle
        {
            get => _minuteHandAngle;
            private set => SetProperty(ref _minuteHandAngle, value);
        }

        public double SecondHandAngle
        {
            get => _secondHandAngle;
            private set => SetProperty(ref _secondHandAngle, value);
        }

        public ObservableCollection<TimeSlice> TimeSlices => _timeSlices;

        public SolidColorBrush HandColorBrush
        {
            get => _handColorBrush;
            private set => SetProperty(ref _handColorBrush, value);
        }

        // Mock Mode Properties
        public bool IsMockMode
        {
            get => _isMockMode;
            set
            {
                if (_isMockMode != value)
                {
                    _isMockMode = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsNotMockMode));
                }
            }
        }

        public bool IsNotMockMode => !IsMockMode;

        public bool IsPaused
        {
            get => _isPaused;
            set
            {
                if (SetProperty(ref _isPaused, value))
                {
                    if (value)
                        _timerService.Stop();
                    else
                        _timerService.Start();
                }
            }
        }

        public bool IsSoundEnabled
        {
            get => _isSoundEnabled;
            set => SetProperty(ref _isSoundEnabled, value);
        }

        // Commands
        public IRelayCommand<TimePickedEventArgs> TimePickedCommand { get; }
        public IRelayCommand<Point> AddGaugeCommand { get; }
        public IRelayCommand<string> RemoveGaugeCommand { get; }
        public IRelayCommand ShowInstructionsCommand { get; }
        public IRelayCommand<string> NudgeTimeCommand { get; }
        public IRelayCommand TogglePauseCommand { get; }

        // Events
        public event EventHandler<TimeSlice> TimeSliceAdded;
        public event EventHandler<TimeSlice> TimeSliceRemoved;
        public event EventHandler RequestShowInstructions;

        // Constructor with dependency injection
        public ClockViewModel(
            IThemeService themeService,
            ISettingsService settingsService,
            ITimerService timerService,
            ISleepPreventer sleepPreventer = null)
        {
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _timerService = timerService ?? throw new ArgumentNullException(nameof(timerService));
            _sleepPreventer = sleepPreventer;

            _timeSlices = new ObservableCollection<TimeSlice>();
            _timerService.Interval = TimeSpan.FromMilliseconds(200);
            _timerService.Tick += Timer_Tick;

            // Subscribe to theme changes if theme service is available
            if (_themeService != null)
            {
                _themeService.ThemeChanged += OnThemeServiceChanged;
            }

            // Initialize commands
            TimePickedCommand = new RelayCommand<TimePickedEventArgs>(OnTimePicked);
            AddGaugeCommand = new RelayCommand<Point>(OnAddGauge);
            RemoveGaugeCommand = new RelayCommand<string>(OnRemoveGauge);
            ShowInstructionsCommand = new RelayCommand(OnShowInstructions);
            NudgeTimeCommand = new RelayCommand<string>(OnNudgeTime);
            TogglePauseCommand = new RelayCommand(OnTogglePause);

            // Initialize properties
            InitializeProperties();

            // Initialize media player for sound
            InitializeMediaPlayer();

            // Start the timer
            _timerService.Start();

            // Prevent sleep if sleep preventer is provided
            _sleepPreventer?.PreventSleep();
        }

        private void InitializeProperties()
        {
            _currentTime = DateTime.Now;
            _centreText = _settingsService?.GetCentreText() ?? string.Empty;
            // Initialize mock mode with defaults since methods might not exist
            _isMockMode = false;
            _isSoundEnabled = true;
            UpdateHandColor();
            UpdateTime();
        }

        private void InitializeMediaPlayer()
        {
            try
            {
                int soundIndex = _settingsService?.GetTimerSound() ?? 0;
                string soundFileName = SoundSettings.GetSoundFileName(soundIndex);
                string soundPath = Path.Combine(AppContext.BaseDirectory, "Assets", soundFileName);

                if (File.Exists(soundPath))
                {
                    _mediaPlayer = new MediaPlayer();
                    _mediaPlayer.Source = MediaSource.CreateFromUri(new Uri(soundPath));
                    _isSoundAvailable = true;
                }
                else
                {
                    // Try loading default sound
                    string defaultSoundPath = Path.Combine(AppContext.BaseDirectory, "Assets", SoundSettings.GetSoundFileName(0));
                    if (File.Exists(defaultSoundPath))
                    {
                        _mediaPlayer = new MediaPlayer();
                        _mediaPlayer.Source = MediaSource.CreateFromUri(new Uri(defaultSoundPath));
                        _isSoundAvailable = true;
                    }
                    else
                    {
                        Debug.WriteLine("No sound files available");
                        _isSoundAvailable = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to initialize media player: {ex.Message}");
                _isSoundAvailable = false;
            }
        }

        private void Timer_Tick(object sender, object e)
        {
            if (!_isPaused)
            {
                UpdateTime();
                CheckForSegmentExpiry();
            }
        }

        private void UpdateTime()
        {
            if (_isPaused && _pausedTime.HasValue)
            {
                // When paused, use the stored paused time
                _currentTime = _pausedTime.Value;
            }
            else
            {
                // Normal operation - current time is real time plus offset
                _currentTime = DateTime.Now.Add(_timeOffset);
            }

            DigitalTimeText = _currentTime.ToString("h:mm tt");
            UpdateClockHands();
        }

        private void UpdateClockHands()
        {
            // Use TotalHours for gradual hour hand movement (includes minutes and seconds as fractions)
            HourHandAngle = _currentTime.TimeOfDay.TotalHours * 30;

            // Add seconds to minute hand for gradual movement (each second = 0.1 degrees)
            MinuteHandAngle = (_currentTime.Minute * 6) + (_currentTime.Second * 0.1);

            SecondHandAngle = _currentTime.Second * 6;
        }

        private void CheckForSegmentExpiry()
        {
            if (!_isMockMode || !_isSoundEnabled) return;

            var currentMinute = _currentTime.Minute;

            // Check if we've crossed into a new minute
            if (currentMinute != _lastMinute)
            {
                _lastMinute = currentMinute;

                // Check if any time slice ends at this minute
                foreach (var slice in _timeSlices)
                {
                    // Calculate the end minute of the slice
                    var sliceEndMinute = (slice.StartMinute + slice.Duration) % 60;

                    if (currentMinute == sliceEndMinute)
                    {
                        PlaySound();
                        break;
                    }
                }
            }
        }

        private void PlaySound()
        {
            if (_isSoundAvailable && _mediaPlayer != null)
            {
                try
                {
                    _mediaPlayer.PlaybackSession.Position = TimeSpan.Zero;
                    _mediaPlayer.Play();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to play sound: {ex.Message}");
                }
            }
        }

        private void UpdateHandColor()
        {
            try
            {
                var isDarkTheme = _themeService?.IsDarkTheme ?? false;
                HandColorBrush = new SolidColorBrush(isDarkTheme ? Colors.White : Colors.Black);
            }
            catch
            {
                // If we can't create brushes (e.g., in unit tests), set to null
                HandColorBrush = null;
            }
        }

        private void OnNudgeTime(string parameter)
        {
            if (!_isMockMode || !int.TryParse(parameter, out int minutes)) return;

            // Store the current time for reference
            var currentDisplayTime = _currentTime;

            // Add the minutes to the current displayed time and reset seconds to 0
            var newTargetTime = currentDisplayTime.AddMinutes(minutes);
            newTargetTime = new DateTime(newTargetTime.Year, newTargetTime.Month, newTargetTime.Day,
                                        newTargetTime.Hour, newTargetTime.Minute, 0, 0);

            // Calculate the offset needed to display this target time
            _timeOffset = newTargetTime.Subtract(DateTime.Now);

            // If we're currently paused, update the paused time too
            if (_isPaused && _pausedTime.HasValue)
            {
                _pausedTime = newTargetTime;
            }

            UpdateTime();
        }

        private void OnTogglePause()
        {
            if (!_isMockMode) return;

            if (!_isPaused)
            {
                // About to pause - store the current displayed time
                _pausedTime = _currentTime;
                IsPaused = true;
            }
            else
            {
                // About to resume - adjust offset so current time matches paused time
                if (_pausedTime.HasValue)
                {
                    _timeOffset = _pausedTime.Value.Subtract(DateTime.Now);
                    _pausedTime = null;
                }
                IsPaused = false;
            }
        }

        // Existing command handlers
        private void OnThemeServiceChanged(object sender, ElementTheme e)
        {
            UpdateHandColor();
        }

        public void OnThemeChanged()
        {
            UpdateHandColor();
        }

        private void OnTimePicked(TimePickedEventArgs args)
        {
            if (args == null) return;

            // Calculate the offset between current time and picked time
            _timeOffset = DateTime.Today.Add(args.NewTime).Subtract(DateTime.Now);

            // Add current seconds to offset
            _timeOffset = _timeOffset.Add(TimeSpan.FromSeconds(_currentTime.Second));

            // Round to nearest minute
            if (_timeOffset.Seconds > 30)
            {
                _timeOffset = _timeOffset.Add(TimeSpan.FromMinutes(1));
            }
            _timeOffset = new TimeSpan(_timeOffset.Hours, _timeOffset.Minutes, 0);

            UpdateTime();
        }

        public void RefreshSound()
        {
            InitializeMediaPlayer();
        }

        private void OnAddGauge(Point point)
        {
            var timeSelected = GetMinutesFromCoordinate(point);
            var existingSlice = FindTimeSliceAtPosition(timeSelected[0], timeSelected[1]);

            if (existingSlice == null)
            {
                var newSlice = CreateTimeSlice(timeSelected);
                _timeSlices.Add(newSlice);
                TimeSliceAdded?.Invoke(this, newSlice);
            }
        }

        private void OnRemoveGauge(string gaugeName)
        {
            var sliceToRemove = _timeSlices.FirstOrDefault(s => s.Name == gaugeName);
            if (sliceToRemove != null)
            {
                _timeSlices.Remove(sliceToRemove);
                TimeSliceRemoved?.Invoke(this, sliceToRemove);

                // Reset counter if no slices remain
                if (_timeSlices.Count == 0)
                {
                    _gaugeNameCounter = 0;
                }
            }
        }

        private void OnShowInstructions()
        {
            RequestShowInstructions?.Invoke(this, EventArgs.Empty);
        }

        public int[] GetMinutesFromCoordinate(Point point)
        {
            var dx = point.X - ClockCenter;
            var dy = point.Y - ClockCenter;
            var distance = Math.Sqrt(dx * dx + dy * dy);

            // Single source of truth for radial level determination
            int radialLevel;
            if (distance < 60)  // Closer to center = Outer level
            {
                radialLevel = 1;  // Outer (smaller circle)
            }
            else
            {
                radialLevel = 0;  // Inner (larger circle)
            }

            // Calculate angle and convert to minutes
            var angle = Math.Atan2(dy, dx) * 180 / Math.PI;
            angle = (angle + 90 + 360) % 360; // Convert to clock coordinates
            var minutes = (int)Math.Round(angle / 6);

            return new[] { minutes % 60, radialLevel };
        }

        // Make this method public so Clock.xaml.cs can access it
        public TimeSlice FindTimeSliceAtPosition(int minutes, int radialLevel)
        {
            return _timeSlices.FirstOrDefault(s => s.IsWithinTimeSlice(minutes, radialLevel));
        }

        private TimeSlice CreateTimeSlice(int[] timeData)
        {
            int radialLevel = timeData[1];
            int fiveMinuteInterval = timeData[0] / 5;

            // Use the counter for unique names instead of count
            var name = $"Gauge{_gaugeNameCounter++}";
            return new TimeSlice(fiveMinuteInterval * 5, 5, radialLevel, name);
        }

        // ExtendTimeSlice method that Clock.xaml.cs is calling
        public void ExtendTimeSlice(string sliceName, int newMinute, int newRadialLevel)
        {
            var slice = _timeSlices.FirstOrDefault(s => s.Name == sliceName);
            if (slice == null || slice.RadialLevel != newRadialLevel) return;

            // Round to 5-minute intervals
            int newFiveMinuteInterval = newMinute / 5;
            int currentStartInterval = slice.StartMinute / 5;
            int currentEndInterval = (slice.StartMinute + slice.Duration) / 5;

            // Don't extend if we're already at this position
            if (newFiveMinuteInterval >= currentStartInterval &&
                newFiveMinuteInterval < currentEndInterval)
            {
                return; // Mouse is within the current slice
            }

            // Check if the new position would overlap with another slice
            var wouldOverlap = _timeSlices.Any(s =>
                s != slice &&
                s.RadialLevel == newRadialLevel &&
                WouldOverlapIfExtended(slice, newFiveMinuteInterval, s));

            if (wouldOverlap) return;

            // Calculate the shortest angular distance to determine direction
            int forwardDistance = CalculateForwardDistance(currentEndInterval, newFiveMinuteInterval);
            int backwardDistance = CalculateBackwardDistance(currentStartInterval, newFiveMinuteInterval);

            if (forwardDistance <= backwardDistance)
            {
                // Extend forward
                int newDuration = (forwardDistance + (currentEndInterval - currentStartInterval)) * 5;
                if (newDuration > 0 && newDuration <= 60 && !WouldCrossHourBoundary(slice.StartMinute, newDuration))
                {
                    slice.Duration = newDuration;
                    OnPropertyChanged(nameof(TimeSlices));
                }
            }
            else
            {
                // Extend backward
                int newStartMinute = newFiveMinuteInterval * 5;
                int newDuration = (backwardDistance + (currentEndInterval - currentStartInterval)) * 5;
                if (newDuration > 0 && newDuration <= 60 && !WouldCrossHourBoundary(newStartMinute, newDuration))
                {
                    slice.StartMinute = newStartMinute;
                    slice.Duration = newDuration;
                    OnPropertyChanged(nameof(TimeSlices));
                }
            }
        }

        // Helper method to calculate forward distance considering hour wrap
        private int CalculateForwardDistance(int fromInterval, int toInterval)
        {
            if (toInterval >= fromInterval)
                return toInterval - fromInterval;
            else
                return (12 - fromInterval) + toInterval; // Wrap around hour
        }

        // Helper method to calculate backward distance considering hour wrap
        private int CalculateBackwardDistance(int fromInterval, int toInterval)
        {
            if (toInterval <= fromInterval)
                return fromInterval - toInterval;
            else
                return fromInterval + (12 - toInterval); // Wrap around hour
        }

        // Check if extension would cross hour boundary in a way that makes the slice invalid
        private bool WouldCrossHourBoundary(int startMinute, int duration)
        {
            // Allow slices up to 60 minutes that may cross hour boundary
            return duration > 60;
        }

        private bool WouldOverlapIfExtended(TimeSlice extendingSlice, int newInterval, TimeSlice otherSlice)
        {
            int newMinute = newInterval * 5;

            // If extending forward
            if (newMinute > extendingSlice.StartMinute + extendingSlice.Duration ||
                (extendingSlice.StartMinute + extendingSlice.Duration > 55 && newMinute < 5))
            {
                int currentEnd = extendingSlice.StartMinute + extendingSlice.Duration;
                int minute = currentEnd;

                // Check INCLUDING the target minute
                do
                {
                    if (otherSlice.IsWithinTimeSlice(minute, otherSlice.RadialLevel))
                        return true;
                    minute = (minute + 5) % 60;
                } while (minute != (newMinute + 5) % 60); // Loop one past target to include it
            }
            // If extending backward
            else
            {
                int minute = newMinute;

                // Check INCLUDING the starting position
                do
                {
                    if (otherSlice.IsWithinTimeSlice(minute, otherSlice.RadialLevel))
                        return true;
                    minute = (minute + 5) % 60;
                } while (minute != extendingSlice.StartMinute);
            }

            return false;
        }

        public bool HasShownClockInstructions()
        {
            return _settingsService?.GetHasShownClockInstructions() ?? false;
        }

        public void SetHasShownClockInstructions(bool value)
        {
            _settingsService?.SetHasShownClockInstructions(value);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _timerService.Tick -= Timer_Tick;
                _timerService.Stop();

                if (_themeService != null)
                {
                    _themeService.ThemeChanged -= OnThemeServiceChanged;
                }

                _sleepPreventer?.AllowSleep();
                _mediaPlayer?.Dispose();
                _disposed = true;
            }
        }
    }
}