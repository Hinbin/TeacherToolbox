using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using TeacherToolbox.Model;
using TeacherToolbox.Services;
using TeacherToolbox.ViewModels;
using Windows.Foundation;

namespace TeacherToolbox.Controls
{
    public sealed partial class Clock : AutomatedPage
    {
        // View-specific fields for Composition API
        private Compositor _compositor;
        private ContainerVisual _root;
        private SpriteVisual _hourhand;
        private SpriteVisual _minutehand;
        private SpriteVisual _secondhand;
        private CompositionScopedBatch _batch;

        // ViewModel and visual elements
        public ClockViewModel ViewModel { get; private set; }
        private readonly Dictionary<string, RadialGauge> _gauges = new Dictionary<string, RadialGauge>();
        private string _selectedGaugeName;
        private int _selectedGaugeRadialLevel = -1; // Track the radial level of selected gauge
        private readonly string[] _colorPalette;

        // Services
        private readonly IThemeService _themeService;

        public Clock()
        {
            this.InitializeComponent();
            this.Loaded += Clock_Loaded;

            // Initialize color palette based on theme
            var themeService = App.Current.Services?.GetService<IThemeService>();
            var isDarkTheme = themeService?.IsDarkTheme ?? false;

            _colorPalette = new[]
                {
            "#FF0072B2", "#FFCC79A7", "#FFF0E442", "#FF009E73", "#FF785EF0",
            "#FFD55E00", "#FF56B4E9", isDarkTheme ? "#FFFFFFFF" : "#FF000000",
            "#FFDC267F", "#FF117733"
        };
        }
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Get services from DI
            var settingsService = await LocalSettingsService.GetSharedInstanceAsync();
            var themeService = App.Current.Services?.GetService<IThemeService>();
            var sleepPreventer = e.Parameter as SleepPreventer;

            // Create ViewModel with all services
            ViewModel = new ClockViewModel(settingsService, sleepPreventer, null, themeService);
            this.DataContext = ViewModel;

            // Subscribe to ViewModel events
            ViewModel.TimeSliceAdded += OnTimeSliceAdded;
            ViewModel.TimeSliceRemoved += OnTimeSliceRemoved;
            ViewModel.RequestShowInstructions += OnRequestShowInstructions;
            ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            // Unsubscribe from events
            if (ViewModel != null)
            {
                ViewModel.TimeSliceAdded -= OnTimeSliceAdded;
                ViewModel.TimeSliceRemoved -= OnTimeSliceRemoved;
                ViewModel.RequestShowInstructions -= OnRequestShowInstructions;
                ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
                ViewModel.Dispose();
            }
        }

        private async void Clock_Loaded(object sender, RoutedEventArgs e)
        {
            // Initialize Composition visuals
            InitializeCompositionVisuals();

            // Load clock background image based on theme
            LoadClockBackgroundImage();

            // Show instructions if needed
            if (ViewModel != null && !ViewModel.HasShownClockInstructions())
            {
                ClockInstructionTip.IsOpen = true;
                ViewModel.SetHasShownClockInstructions(true);
            }
        }

        private void InitializeCompositionVisuals()
        {
            _root = ElementCompositionPreview.GetElementVisual(Container) as ContainerVisual;
            _compositor = _root.Compositor;

            // Create clock hands using Composition API
            CreateClockHands();

            // Subscribe to theme changes through the actual theme property
            ((FrameworkElement)this.Content).ActualThemeChanged += OnThemeChanged;
        }

        private void CreateClockHands()
        {
            // Get theme service from DI
            var themeService = App.Current.Services?.GetService<IThemeService>();
            var handColor = themeService?.IsDarkTheme == true ? Colors.White : Colors.Black;

            // Use ViewModel's brush if available, otherwise use theme service color
            if (ViewModel?.HandColorBrush?.Color != null)
            {
                handColor = ViewModel.HandColorBrush.Color;
            }

            var handColorBrush = _compositor.CreateColorBrush(handColor);

            // Second Hand
            _secondhand = _compositor.CreateSpriteVisual();
            _secondhand.Size = new Vector2(2.0f, 100.0f);
            _secondhand.Brush = _compositor.CreateColorBrush(Colors.Red);
            _secondhand.CenterPoint = new Vector3(1.0f, 80.0f, 0);
            _secondhand.Offset = new Vector3(99.0f, 20.0f, 0);
            _secondhand.RotationAngleInDegrees = (float)(ViewModel?.SecondHandAngle ?? 0);
            _root.Children.InsertAtTop(_secondhand);

            // Hour Hand
            _hourhand = _compositor.CreateSpriteVisual();
            _hourhand.Size = new Vector2(4.0f, 70.0f);
            _hourhand.Brush = handColorBrush;
            _hourhand.CenterPoint = new Vector3(2.0f, 50.0f, 0);
            _hourhand.Offset = new Vector3(98.0f, 50.0f, 0);
            _root.Children.InsertAtTop(_hourhand);

            // Minute Hand
            _minutehand = _compositor.CreateSpriteVisual();
            _minutehand.Size = new Vector2(4.0f, 100.0f);
            _minutehand.Brush = handColorBrush;
            _minutehand.CenterPoint = new Vector3(2.0f, 80.0f, 0);
            _minutehand.Offset = new Vector3(98.0f, 20.0f, 0);
            _root.Children.InsertAtTop(_minutehand);

            UpdateClockHandPositions();
        }

        private void LoadClockBackgroundImage()
        {
            // Get theme service from DI
            var themeService = App.Current.Services?.GetService<IThemeService>();
            var isDarkTheme = themeService?.IsDarkTheme ?? false;

            string imagePath = System.IO.Path.Combine(
                AppContext.BaseDirectory,
                "Assets",
                isDarkTheme ? "clockfacedark.png" : "clockface.png"
            );

            try
            {
                var bitmapImage = new BitmapImage(new Uri(imagePath));
                ClockBackgroundImage.Source = bitmapImage;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load clock face image: {ex.Message}");
            }
        }

        private void OnViewModelPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ClockViewModel.SecondHandAngle):
                    AnimateSecondHand();
                    break;
                case nameof(ClockViewModel.HourHandAngle):
                case nameof(ClockViewModel.MinuteHandAngle):
                    UpdateClockHandPositions();
                    break;
                case nameof(ClockViewModel.HandColorBrush):
                    UpdateHandColors();
                    break;
            }
        }

        private void AnimateSecondHand()
        {
            if (_secondhand == null || ViewModel == null) return;

            _batch = _compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            var animation = _compositor.CreateScalarKeyFrameAnimation();

            var currentAngle = ViewModel.SecondHandAngle;
            var previousAngle = currentAngle - 6; // Previous second position

            animation.InsertKeyFrame(0.00f, (float)previousAngle);
            animation.InsertKeyFrame(1.00f, (float)currentAngle);
            animation.Duration = TimeSpan.FromMilliseconds(900);

            _secondhand.StartAnimation(nameof(_secondhand.RotationAngleInDegrees), animation);

            _batch.End();
            _batch.Completed += (s, args) => UpdateClockHandPositions();
        }

        private void UpdateClockHandPositions()
        {
            if (ViewModel == null) return;

            _hourhand?.StopAnimation(nameof(_hourhand.RotationAngleInDegrees));
            _minutehand?.StopAnimation(nameof(_minutehand.RotationAngleInDegrees));

            if (_hourhand != null)
                _hourhand.RotationAngleInDegrees = (float)ViewModel.HourHandAngle;
            if (_minutehand != null)
                _minutehand.RotationAngleInDegrees = (float)ViewModel.MinuteHandAngle;

            // Set second hand position only if it's not currently being animated
            // This ensures correct initial positioning while preserving animations
            if (_secondhand != null && _batch == null)
                _secondhand.RotationAngleInDegrees = (float)ViewModel.SecondHandAngle;
        }

        private void UpdateHandColors()
        {
            if (ViewModel?.HandColorBrush == null) return;

            var handColorBrush = _compositor.CreateColorBrush(ViewModel.HandColorBrush.Color);
            if (_hourhand != null)
                _hourhand.Brush = handColorBrush;
            if (_minutehand != null)
                _minutehand.Brush = handColorBrush;
        }

        private void OnThemeChanged(FrameworkElement sender, object args)
        {
            ViewModel?.OnThemeChanged();
            LoadClockBackgroundImage();
            UpdateHandColors();
        }

        #region Event Handlers

        private void TimePickerFlyout_TimePicked(TimePickerFlyout sender, TimePickedEventArgs args)
        {
            ViewModel?.TimePickedCommand.Execute(args);
        }

        private void Digital_Time_Tapped(object sender, TappedRoutedEventArgs e)
        {
            FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);
        }

        private void Clock_Pointer_Pressed(object sender, PointerRoutedEventArgs e)
        {
            var canvas = (Canvas)sender;
            canvas.CapturePointer(e.Pointer);

            var point = e.GetCurrentPoint(canvas).Position;
            var timeSelected = GetTimeFromPoint(point);

            var existingSlice = ViewModel?.FindTimeSliceAtPosition(timeSelected[0], timeSelected[1]);
            _selectedGaugeName = existingSlice?.Name;
            _selectedGaugeRadialLevel = existingSlice?.RadialLevel ?? timeSelected[1];

            if (e.GetCurrentPoint(canvas).Properties.IsLeftButtonPressed)
            {
                if (_selectedGaugeName == null)
                {
                    // Create new gauge and immediately select it for potential extension
                    ViewModel?.AddGaugeCommand.Execute(point);

                    // Find the newly created time slice and set it as selected
                    var newSlice = ViewModel?.FindTimeSliceAtPosition(timeSelected[0], timeSelected[1]);
                    if (newSlice != null)
                    {
                        _selectedGaugeName = newSlice.Name;
                        _selectedGaugeRadialLevel = newSlice.RadialLevel;
                    }
                }
            }
            else if (e.GetCurrentPoint(canvas).Properties.IsRightButtonPressed)
            {
                if (_selectedGaugeName != null)
                {
                    ViewModel?.RemoveGaugeCommand.Execute(_selectedGaugeName);
                    _selectedGaugeName = null;
                    _selectedGaugeRadialLevel = -1;
                }
            }

            e.Handled = true;
        }

        private void Clock_Pointer_Released(object sender, PointerRoutedEventArgs e)
        {
            var canvas = (Canvas)sender;
            canvas.ReleasePointerCapture(e.Pointer);
            _selectedGaugeName = null;
            _selectedGaugeRadialLevel = -1;
        }

        private void Clock_Pointer_Exited(object sender, PointerRoutedEventArgs e)
        {
            if (!e.GetCurrentPoint((UIElement)sender).Properties.IsLeftButtonPressed)
            {
                var canvas = (Canvas)sender;
                canvas.ReleasePointerCapture(e.Pointer);
                _selectedGaugeName = null;
                _selectedGaugeRadialLevel = -1;
            }
        }

        private void Clock_Pointer_Moved(object sender, PointerRoutedEventArgs e)
        {
            e.Handled = true;

            if (!e.Pointer.IsInContact || string.IsNullOrEmpty(_selectedGaugeName))
                return;

            var point = e.GetCurrentPoint((UIElement)sender).Position;
            var timeSelected = GetTimeFromPoint(point);

            // Check if we're still in the same radial level as the selected gauge
            if (timeSelected[1] != _selectedGaugeRadialLevel)
            {
                // Radial level changed during drag - stop extending
                return;
            }

            ViewModel?.ExtendTimeSlice(_selectedGaugeName, timeSelected[0], timeSelected[1]);

            // Update the visual gauge
            if (_gauges.TryGetValue(_selectedGaugeName, out var gauge))
            {
                var slice = ViewModel.TimeSlices.FirstOrDefault(s => s.Name == _selectedGaugeName);
                if (slice != null)
                {
                    UpdateGaugeFromSlice(gauge, slice);
                }
            }
        }

        #endregion

        #region Time Slice Visual Management

        private void OnTimeSliceAdded(object sender, TimeSlice slice)
        {
            CreateGaugeForSlice(slice);
        }

        private void OnTimeSliceRemoved(object sender, TimeSlice slice)
        {
            RemoveGaugeForSlice(slice);
        }

        private void CreateGaugeForSlice(TimeSlice slice)
        {
            var gauge = new RadialGauge
            {
                Name = slice.Name,
                IsInteractive = false,
                NeedleLength = 95,
                ValueStringFormat = "'",
                TickSpacing = 0,
                ScaleWidth = 40,
                ScaleBrush = new SolidColorBrush(Colors.Transparent),
                TrailBrush = GetNextColorBrush(),
                Minimum = 0,
                Maximum = 1,
                Value = 1,
                NeedleWidth = 0
            };

            // Set opacity
            gauge.TrailBrush.Opacity = 0.65;

            // Configure based on radial level
            if (slice.RadialLevel == (int)RadialLevel.Inner)
            {
                gauge.Width = 200;
                gauge.Height = 200;
                gauge.SetValue(Canvas.LeftProperty, 0);
                gauge.SetValue(Canvas.TopProperty, 0);
                gauge.SetValue(Canvas.ZIndexProperty, 1);
                gauge.ScalePadding = 5;
            }
            else
            {
                gauge.Width = 140;
                gauge.Height = 140;
                gauge.SetValue(Canvas.LeftProperty, 30);
                gauge.SetValue(Canvas.TopProperty, 30);
                gauge.SetValue(Canvas.ZIndexProperty, 2);
            }

            UpdateGaugeFromSlice(gauge, slice);

            // Add to canvas and dictionary
            Container.Children.Add(gauge);
            _gauges[slice.Name] = gauge;
        }

        private void RemoveGaugeForSlice(TimeSlice slice)
        {
            if (_gauges.TryGetValue(slice.Name, out var gauge))
            {
                Container.Children.Remove(gauge);
                _gauges.Remove(slice.Name);
            }
        }

        private void UpdateGaugeFromSlice(RadialGauge gauge, TimeSlice slice)
        {
            int startInterval = slice.StartMinute / 5;
            int endInterval = (slice.StartMinute + slice.Duration) / 5;

            gauge.MinAngle = startInterval * 30;
            gauge.MaxAngle = endInterval * 30;
        }

        private SolidColorBrush GetNextColorBrush()
        {
            // Try to find the first unused color from the palette
            foreach (var hexCode in _colorPalette)
            {
                bool colorInUse = false;

                // Check if any existing gauge is using this color
                foreach (var gauge in _gauges.Values)
                {
                    if (gauge.TrailBrush is SolidColorBrush brush)
                    {
                        var brushColor = brush.Color;
                        var paletteColor = ColorHelper.FromArgb(
                            Convert.ToByte(hexCode.Substring(1, 2), 16),
                            Convert.ToByte(hexCode.Substring(3, 2), 16),
                            Convert.ToByte(hexCode.Substring(5, 2), 16),
                            Convert.ToByte(hexCode.Substring(7, 2), 16));

                        if (brushColor.A == paletteColor.A &&
                            brushColor.R == paletteColor.R &&
                            brushColor.G == paletteColor.G &&
                            brushColor.B == paletteColor.B)
                        {
                            colorInUse = true;
                            break;
                        }
                    }
                }

                if (!colorInUse)
                {
                    return new SolidColorBrush(ColorHelper.FromArgb(
                        Convert.ToByte(hexCode.Substring(1, 2), 16),
                        Convert.ToByte(hexCode.Substring(3, 2), 16),
                        Convert.ToByte(hexCode.Substring(5, 2), 16),
                        Convert.ToByte(hexCode.Substring(7, 2), 16)));
                }
            }

            // If all palette colors are used, generate a random color that contrasts with the theme
            Random random = new Random();
            bool isDarkTheme = _themeService?.IsDarkTheme ?? false;

            if (isDarkTheme)
            {
                // For dark theme, generate lighter colors
                int minValue = 100; // Ensure colors are not too dark
                int maxValue = 255;

                // Generate at least one component that's bright
                byte r = (byte)random.Next(minValue, maxValue);
                byte g = (byte)random.Next(minValue, maxValue);
                byte b = (byte)random.Next(minValue, maxValue);

                // Ensure at least one channel is bright enough
                if (r < 150 && g < 150 && b < 150)
                {
                    // Make one channel brighter
                    switch (random.Next(3))
                    {
                        case 0: r = (byte)random.Next(150, maxValue); break;
                        case 1: g = (byte)random.Next(150, maxValue); break;
                        case 2: b = (byte)random.Next(150, maxValue); break;
                    }
                }

                return new SolidColorBrush(ColorHelper.FromArgb(255, r, g, b));
            }
            else
            {
                // For light theme, generate darker colors
                int minValue = 0;
                int maxValue = 156; // Ensure colors are not too light

                byte r = (byte)random.Next(minValue, maxValue);
                byte g = (byte)random.Next(minValue, maxValue);
                byte b = (byte)random.Next(minValue, maxValue);

                // Ensure at least one channel is dark enough
                if (r > 100 && g > 100 && b > 100)
                {
                    // Make one channel darker
                    switch (random.Next(3))
                    {
                        case 0: r = (byte)random.Next(minValue, 100); break;
                        case 1: g = (byte)random.Next(minValue, 100); break;
                        case 2: b = (byte)random.Next(minValue, 100); break;
                    }
                }

                return new SolidColorBrush(ColorHelper.FromArgb(255, r, g, b));
            }
        }

        #endregion

        #region Helper Methods

        private int[] GetTimeFromPoint(Point point)
        {
            // Calculate angle from clock center
            var angle = Math.Atan2(point.Y - 100, point.X - 100) * (180 / Math.PI);
            angle = 180 + angle;

            // Convert to minutes
            var minutes = (int)(angle / 6);
            minutes = (minutes + 45) % 60;

            // Determine radial level
            var distance = Math.Sqrt(Math.Pow(point.X - 100, 2) + Math.Pow(point.Y - 100, 2));
            int radialLevel = distance < 55 ? (int)RadialLevel.Outer : (int)RadialLevel.Inner;

            return new int[] { minutes, radialLevel };
        }

        private void OnRequestShowInstructions(object sender, EventArgs e)
        {
            ClockInstructionTip.IsOpen = true;
        }

        #endregion
    }
}