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

        // Commands
        public IRelayCommand<TimePickedEventArgs> TimePickedCommand { get; }
        public IRelayCommand<Point> AddGaugeCommand { get; }
        public IRelayCommand<string> RemoveGaugeCommand { get; }
        public IRelayCommand ShowInstructionsCommand { get; }

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

            // Initialize properties
            InitializeProperties();

            // Start the timer
            _timerService.Start();

            // Prevent sleep if sleep preventer is provided
            _sleepPreventer?.PreventSleep();
        }

        private void InitializeProperties()
        {
            _currentTime = DateTime.Now;
            _timeOffset = TimeSpan.Zero;

            // Load saved settings
            if (_settingsService != null)
            {
                CentreText = _settingsService.GetCentreText();
            }

            // Set hand color based on theme
            UpdateHandColor();

            // Update initial time display
            UpdateTime();
        }

        private void Timer_Tick(object sender, object e)
        {
            var checkTime = DateTime.Now;

            // Update digital time display
            DigitalTimeText = _currentTime.ToString("h:mm tt");

            // Check if we have a new second
            if (_currentTime.Second != checkTime.Second)
            {
                _currentTime = DateTime.Now.Add(_timeOffset);
                UpdateClockHands();
            }
        }

        private void UpdateTime()
        {
            _currentTime = DateTime.Now.Add(_timeOffset);
            UpdateClockHands();
            DigitalTimeText = _currentTime.ToString("h:mm tt");
        }

        private void UpdateClockHands()
        {
            // Calculate angles for clock hands
            HourHandAngle = (float)_currentTime.TimeOfDay.TotalHours * 30;
            MinuteHandAngle = _currentTime.Minute * 6 + _currentTime.Second * 0.1f;
            SecondHandAngle = _currentTime.Second * 6;
        }
        private void UpdateHandColor()
        {
            try
            {
                // Use property, not method
                HandColorBrush = _themeService?.HandColorBrush;

                // If theme service is null or returns null, create fallback
                if (HandColorBrush == null)
                {
                    HandColorBrush = CreateFallbackBrush();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating hand color: {ex.Message}");
                // In case of error, try to create fallback brush
                HandColorBrush = CreateFallbackBrush();
            }
        }

        private SolidColorBrush CreateFallbackBrush()
        {
            try
            {
                // Try to determine theme and create appropriate brush
                var isDarkTheme = _themeService?.IsDarkTheme ?? false;
                return new SolidColorBrush(isDarkTheme ? Colors.White : Colors.Black);
            }
            catch
            {
                // If we can't create brushes (e.g., in unit tests), return null
                // The UI should handle null brushes gracefully
                return null;
            }
        }

        // Handler for theme service's ThemeChanged event
        private void OnThemeServiceChanged(object sender, ElementTheme e)
        {
            UpdateHandColor();
        }

        // Keep this method for backward compatibility if needed
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
            var slice = _timeSlices.FirstOrDefault(s => s.Name == gaugeName);
            if (slice != null)
            {
                _timeSlices.Remove(slice);
                TimeSliceRemoved?.Invoke(this, slice);

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

        public TimeSlice FindTimeSliceAtPosition(int minute, int radialLevel)
        {
            return _timeSlices.FirstOrDefault(slice => slice.IsWithinTimeSlice(minute, radialLevel));
        }

        private TimeSlice CreateTimeSlice(int[] timeSelected)
        {
            int radialLevel = timeSelected[1];
            int fiveMinuteInterval = timeSelected[0] / 5;

            // Use the counter for unique names instead of count
            var name = $"Gauge{_gaugeNameCounter++}";
            return new TimeSlice(fiveMinuteInterval * 5, 5, radialLevel, name);
        }

        public void ExtendTimeSlice(string sliceName, int newMinute, int newRadialLevel)
        {
            var slice = _timeSlices.FirstOrDefault(s => s.Name == sliceName);
            if (slice == null || slice.RadialLevel != newRadialLevel) return;

            // Check if the new position is already occupied by another slice
            var existingSlice = _timeSlices.FirstOrDefault(s => s != slice && s.IsWithinTimeSlice(newMinute, newRadialLevel));
            if (existingSlice != null) return;

            int newFiveMinuteInterval = newMinute / 5;
            int startInterval = slice.StartMinute / 5;
            int endInterval = (slice.StartMinute + slice.Duration) / 5;

            // Update the slice duration based on the new position
            if (newFiveMinuteInterval >= endInterval)
            {
                if (newFiveMinuteInterval == endInterval)
                {
                    slice.Duration += 5;
                }
                else if (newFiveMinuteInterval == startInterval + 11)
                {
                    slice.StartMinute = (slice.StartMinute + 55) % 60;
                    slice.Duration += 5;
                }
            }
            else if (newFiveMinuteInterval < startInterval)
            {
                if (newFiveMinuteInterval == startInterval - 1)
                {
                    slice.StartMinute = newFiveMinuteInterval * 5;
                    slice.Duration += 5;
                }
                else if (newFiveMinuteInterval == endInterval - 12)
                {
                    slice.Duration += 5;
                }
            }

            // Notify that the collection has changed
            OnPropertyChanged(nameof(TimeSlices));
        }

        private static int[] GetMinutesFromCoordinate(Point point)
        {
            // Calculate angle from clock center
            var angle = Math.Atan2(point.Y - ClockCenter, point.X - ClockCenter) * (180 / Math.PI);
            angle = 180 + angle;

            // Convert to minutes
            var minutes = (int)(angle / 6);
            minutes = (minutes + 45) % 60;

            // Determine radial level (inner or outer)
            var distance = Math.Sqrt(Math.Pow(point.X - ClockCenter, 2) + Math.Pow(point.Y - ClockCenter, 2));
            int radialLevel = distance < 55 ? (int)RadialLevel.Outer : (int)RadialLevel.Inner;

            return new int[] { minutes, radialLevel };
        }

        public bool HasShownClockInstructions()
        {
            return _settingsService?.GetHasShownClockInstructions() ?? false;
        }

        public void SetHasShownClockInstructions(bool shown)
        {
            _settingsService?.SetHasShownClockInstructions(shown);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // Unsubscribe from theme changes
                if (_themeService != null)
                {
                    _themeService.ThemeChanged -= OnThemeServiceChanged;
                }

                _timerService?.Stop();
                if (_timerService is IDisposable disposableTimer)
                {
                    disposableTimer.Dispose();
                }
                _sleepPreventer?.AllowSleep();
                _disposed = true;
            }
        }
    }
}