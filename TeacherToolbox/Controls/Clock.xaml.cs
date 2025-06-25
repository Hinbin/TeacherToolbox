using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Numerics;
using Windows.Foundation;
using System.IO;
using WinUIEx;
using Microsoft.UI.Xaml.Shapes;
using TeacherToolbox.Model;
using Windows.Globalization;
using Microsoft.UI.Xaml.Navigation;
using System.Diagnostics;
using TeacherToolbox.Helpers;
using TeacherToolbox.Services;
using Microsoft.UI.Xaml.Automation;

namespace TeacherToolbox.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

enum RadialLevel
{
    Inner,
    Outer
}

public class TimeSlice
{
    public int StartMinute { get; set; }
    public int Duration { get; set; }
    public int RadialLevel { get; set; }
    public string Name { get; set; }


    public TimeSlice(int startMinute, int duration, int radialLevel, string name)
    {
        this.StartMinute = startMinute;
        this.Duration = duration;
        this.RadialLevel = radialLevel;
        this.Name = name;
    }

    public TimeSlice()
    {
    }

    public bool IsWithinTimeSlice(int minute, int radialLevel)
    {
        if (this.RadialLevel != radialLevel) return false;

        if (minute >= StartMinute && minute < StartMinute + Duration)
        {
            return true;
        } // check forward accross the hour boundary
        else if (StartMinute + Duration > 60 && minute < StartMinute && minute + 60 < StartMinute + Duration)
        {
            return true;
        }

        return false;
    }

}
public sealed partial class Clock : AutomatedPage
{
    private Compositor _compositor;
    private ContainerVisual _root;

    private SpriteVisual _hourhand;
    private SpriteVisual _minutehand;
    private SpriteVisual _secondhand;
    private CompositionScopedBatch _batch;

    private readonly DispatcherTimer _timer = new();

    private DateTime now;

    private TimeSpan offset = TimeSpan.Zero;

    // Private 2d array to hold the values for the gauges, 60 minutes and 2 radial levels
    private string selectedGauge;
    private int gaugeCount = 0;

    // A C# list to hold the radial gauges
    private readonly List<RadialGauge> radialGaugeList = new();
    private readonly List<TimeSlice> timeSlices = new();

    public ImageSource BackgroundImage { get; set; }

    public ISettingsService localSettingsService;

    private SleepPreventer _sleepPreventer;
    private bool _isPreventingSleep = false;
    CompositionColorBrush handColourBrush;


    public Clock()
    {
        this.InitializeComponent();

        this.Loaded += Clock_Loaded;

        _timer.Interval = TimeSpan.FromMilliseconds(200);
        _timer.Tick += Timer_Tick;
        timePickerFlyout.TimePicked += TimePickerFlyout_TimePicked;
    }    

    public bool ShowTicks { get; set; } = false;

    public Brush FaceColor { get; set; } = new SolidColorBrush(Colors.Transparent);

    // Override navigated to 
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Debug.WriteLine("Clock page OnNavigatedTo");
        if (e.Parameter is SleepPreventer sleepPreventer)
        {
            _sleepPreventer = sleepPreventer;
            Debug.WriteLine("Got SleepPreventer from navigation parameter");
            _sleepPreventer.PreventSleep();
            _isPreventingSleep = true;
        }
        else
        {
            Debug.WriteLine("No SleepPreventer in navigation parameter!");
        }
    }

    private void TimePickerFlyout_TimePicked(TimePickerFlyout sender, TimePickedEventArgs args)
    {

        // Get the difference between the current time and the new time ignoring seconds
        offset = DateTime.Today.Add(args.NewTime).Subtract(DateTime.Now);

        // Get the current number of seconds of the minute and add it to the offset
        offset = offset.Add(TimeSpan.FromSeconds(now.Second));

        // Round the offset to the nearest minute

        if (offset.Seconds > 30)
        {
            offset = offset.Add(TimeSpan.FromMinutes(1));
        }
        offset = new TimeSpan(offset.Hours, offset.Minutes, 0);

    }

    private async void Clock_Loaded(object sender, RoutedEventArgs e)
    {
        now = DateTime.Now;

        digitalTimeTextBlock.Text = now.ToString("h:mm tt");

        Face.Fill = FaceColor;

        // Get the containerVisual for the canvas in WinUI3

        _root = ElementCompositionPreview.GetElementVisual(Container) as ContainerVisual;
        _compositor = _root.Compositor;

        var isDarkTheme = ThemeHelper.IsDarkTheme();
        string imagePath = "";

        if (isDarkTheme)
        {
            //Load clock settings
            imagePath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Clock_Face_Inverse.png");
            handColourBrush = _compositor.CreateColorBrush(Colors.White);
        }
        else
        {
            imagePath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Clock_Face.png");
            handColourBrush = _compositor.CreateColorBrush(Colors.Black);
        }
        BackgroundImage = new BitmapImage(new Uri(imagePath));

        // Hour Ticks
        if (ShowTicks)
        {
            SpriteVisual tick;
            for (int i = 0; i < 12; i++)
            {
                tick = _compositor.CreateSpriteVisual();
                tick.Size = new Vector2(4.0f, 20.0f);
                tick.Brush = _compositor.CreateColorBrush(Colors.Silver);
                tick.Offset = new Vector3(98.0f, 0.0f, 0);
                tick.CenterPoint = new Vector3(2.0f, 100.0f, 0);
                tick.RotationAngleInDegrees = i * 30;
                _root.Children.InsertAtTop(tick);
            }
        }

        // Second Hand
        _secondhand = _compositor.CreateSpriteVisual();
        _secondhand.Size = new Vector2(2.0f, 100.0f);
        _secondhand.Brush = _compositor.CreateColorBrush(Colors.Red);
        _secondhand.CenterPoint = new Vector3(1.0f, 80.0f, 0);
        _secondhand.Offset = new Vector3(99.0f, 20.0f, 0);
        _root.Children.InsertAtTop(_secondhand);
        _secondhand.RotationAngleInDegrees = (float)(int)DateTime.Now.TimeOfDay.TotalSeconds * 6;

        // Hour Hand
        _hourhand = _compositor.CreateSpriteVisual();
        _hourhand.Size = new Vector2(4.0f, 70.0f);
        _hourhand.Brush = handColourBrush;
        _hourhand.CenterPoint = new Vector3(2.0f, 50.0f, 0);
        _hourhand.Offset = new Vector3(98.0f, 50.0f, 0);
        _root.Children.InsertAtTop(_hourhand);

        // Minute Hand
        _minutehand = _compositor.CreateSpriteVisual();
        _minutehand.Size = new Vector2(4.0f, 100.0f);
        _minutehand.Brush = handColourBrush;
        _minutehand.CenterPoint = new Vector3(2.0f, 80.0f, 0);
        _minutehand.Offset = new Vector3(98.0f, 20.0f, 0);
        _root.Children.InsertAtTop(_minutehand);

        SetHoursAndMinutes();

        // Add XAML element.
        if (BackgroundImage != null)
        {
            var xaml = new Image
            {
                Source = BackgroundImage,
                Height = 200,
                Width = 200
            };
            Container.Children.Add(xaml);
        }

        _timer.Start();

        localSettingsService = await LocalSettingsService.GetSharedInstanceAsync();
        centreTextBox.Text = localSettingsService.GetCentreText();

        // Inside your Clock_Loaded method, after other initialization:
        if (!localSettingsService.GetHasShownClockInstructions())
        {
            ClockInstructionTip.IsOpen = true;
            localSettingsService.SetHasShownClockInstructions(true);
        }

    }

    private void Timer_Tick(object sender, object e)
    {
        DateTime checkTime = DateTime.Now;

        digitalTimeTextBlock.Text = now.ToString("h:mm tt");

        // Check to see if we have a new second
        if (now.Second != checkTime.Second)
        {

            // Add offset on to current time
            now = DateTime.Now.Add(offset);

            _batch = _compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            var animation = _compositor.CreateScalarKeyFrameAnimation();
            var seconds = (float)(int)now.TimeOfDay.TotalSeconds - 1;

            // This works:
            animation.InsertKeyFrame(0.00f, seconds * 6);
            animation.InsertKeyFrame(1.00f, (seconds + 1) * 6);

            animation.Duration = TimeSpan.FromMilliseconds(900);
            _secondhand.StartAnimation(nameof(_secondhand.RotationAngleInDegrees), animation);
            _batch.End();
            _batch.Completed += Batch_Completed;

        }
    }

    /// <summary>
    /// Fired at the end of the secondhand animation. 
    /// </summary>
    private void Batch_Completed(object sender, CompositionBatchCompletedEventArgs args)
    {
        _batch.Completed -= Batch_Completed;

        SetHoursAndMinutes();
    }

    private void SetHoursAndMinutes()
    {
        _hourhand.RotationAngleInDegrees = (float)now.TimeOfDay.TotalHours * 30;
        _minutehand.RotationAngleInDegrees = now.Minute * 6;
        // Add on second intervals to the minute hand
        _minutehand.RotationAngleInDegrees += (float)now.Second * 0.1f;
    }

    private void Digital_Time_Tapped(object sender, TappedRoutedEventArgs e)
    {
        FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);
    }

    private string CheckIfGaugeAtPosition(int minute, int radialLevel)
    {
        // Call the isWithinTimeSlice function to see if a gauge is already there
        // for all gauges in the array.  If a gauge is already there, select it, otherwise create a new gauge
        for (int i = 0; i < timeSlices.Count; i++)
        {
            if (timeSlices[i].IsWithinTimeSlice(minute, radialLevel))
            {
                selectedGauge = timeSlices[i].Name;
                return selectedGauge;
            }
        }
        selectedGauge = null;
        return selectedGauge;
    }

    private void Clock_Pointer_Pressed(object sender, PointerRoutedEventArgs e)
    {
        // Capture pointer
        var canvas = (Canvas)sender;
        canvas.CapturePointer(e.Pointer);

        // Get the x and y co-ordinate of where the clock was clicked
        var point = e.GetCurrentPoint((UIElement)sender).Position;

        // Get the minutes from the angle
        var timeSelected = GetMinutesFromCoordinate(point);
        CheckIfGaugeAtPosition(timeSelected[0], timeSelected[1]);

        // On a left mouse click or touch event
        if (e.GetCurrentPoint((UIElement)sender).Properties.IsLeftButtonPressed)
        {
            if (selectedGauge == null) AddGauge(timeSelected);
            }

        // On a right mouse click or touch event
        if (e.GetCurrentPoint((UIElement)sender).Properties.IsRightButtonPressed)
        {
            // If a gauge exists at this position, remove it
            if (selectedGauge != null) RemoveGauge(canvas);
        }

        // Mark the event as handled so it does not bubble up to the parent
        e.Handled = true;
    }

    private void RemoveGauge(Canvas canvas)
    {
        // Find the gauge with the selectedGauge name and remove it from the canvas
        canvas.Children.Remove(radialGaugeList.Find(x => x.Name == selectedGauge));
        radialGaugeList.RemoveAll(x => x.Name == selectedGauge);
        timeSlices.RemoveAll(x => x.Name == selectedGauge);
        selectedGauge = null;

        if (timeSlices.Count == 0) gaugeCount = 0;  // Reset this to 0 to reset colours
    }

    private void AddGauge(int[] timeSelected)
    {
        int radialLevel = timeSelected[1];

        // Work out the 5 minute interval the minute is in, rounded down to the nearest 5 minutes
        int fiveMinuteInterval = timeSelected[0] / 5;

        // Programatically create a WinUI3 Community toolbox radial gauge to radialGaugeList 
        RadialGauge newGauge = new()
        {
            // if radialLevel is 0, set the gauge to 200x200, else set it to 100x100

            Name = "Gauge" + gaugeCount
        };
        
        string gaugeName = newGauge.Name;
        AutomationProperties.SetAutomationId(newGauge, gaugeName);

        if (radialLevel == (int)RadialLevel.Inner)
        {
            newGauge.Width = 200;
            newGauge.Height = 200;
            // Set the position of the new gauge
            newGauge.SetValue(Canvas.LeftProperty, 0);
            newGauge.SetValue(Canvas.TopProperty, 0);
            newGauge.SetValue(Canvas.ZIndexProperty, 1);
            newGauge.ScalePadding = 5;            

        }
        else
        {
            newGauge.Width = 140;
            newGauge.Height = 140;
            // Set the position of the new gauge
            newGauge.SetValue(Canvas.LeftProperty, 30);
            newGauge.SetValue(Canvas.TopProperty, 30);
            newGauge.SetValue(Canvas.ZIndexProperty, 2);
        }

        newGauge.IsInteractive = false;
        newGauge.NeedleLength = 95;

        // set the minangle to the start of the 5 minute interval
        newGauge.MinAngle = fiveMinuteInterval * 30;
        // set the maxangle to the end of the 5 minute interval
        newGauge.MaxAngle = (fiveMinuteInterval + 1) * 30;

        newGauge.ValueStringFormat = "'";
        newGauge.TickSpacing = 0;

        newGauge.ScaleWidth = 40;
        newGauge.ScaleBrush = new SolidColorBrush(Colors.Transparent);

        // Make the trail a randomised colour with 50% opacity
        newGauge.TrailBrush = GetNextColourBrush();
        newGauge.TrailBrush.Opacity = 0.65;
        newGauge.Minimum = 0;
        newGauge.Maximum = 1;
        newGauge.Value = 1;

        newGauge.NeedleWidth = 0;

        // Add the new gauge to the canvas
        Container.Children.Add(newGauge);

        // Set the selectedGauge to the last gauge added
        selectedGauge = "Gauge" + gaugeCount;
        gaugeCount++;

        // Add the new gauge to the radialGaugeList
        radialGaugeList.Add(newGauge);

        newGauge.Name = selectedGauge;

        // Add the new gauge to the timeSlices list
        timeSlices.Add(new TimeSlice(fiveMinuteInterval * 5, 5, radialLevel, selectedGauge));
    }

    private void Clock_Pointer_Released(object sender, PointerRoutedEventArgs e)
    {
        // Release pointer
        var canvas = (Canvas)sender;
        canvas.ReleasePointerCapture(e.Pointer);

        selectedGauge = null;
    }

    private void Clock_Pointer_Exited(object sender, PointerRoutedEventArgs e)
    {
        // Only release pointer capture if the mouse button is not down
        if (!e.GetCurrentPoint((UIElement)sender).Properties.IsLeftButtonPressed)
        {
            var canvas = (Canvas)sender;
            canvas.ReleasePointerCapture(e.Pointer);
            selectedGauge = null;
        }
    }

    private void Clock_Pointer_Moved(object sender, PointerRoutedEventArgs e)
    {
        e.Handled = true;
        // Check if the pointer is captured
        if (e.Pointer.IsInContact)
        {
            // Get the x and y co-ordinate of where the clock was clicked
            var point = e.GetCurrentPoint((UIElement)sender).Position;

            // Get the minutes from the angle
            var timeSelected = GetMinutesFromCoordinate(point);

            // If a gauge has been selected, call the function to change the range of minutes it is set to
            if (selectedGauge != null)
            {
                // Check the time selected already contains a time slice, if so, return
                if (timeSlices.Find(x => x.IsWithinTimeSlice(timeSelected[0], timeSelected[1])) != null) return;

                // Find the timeSlice in the timeSlices list by reference
                TimeSlice timeSlice = timeSlices.Find(x => x.Name == selectedGauge);

                // Get the startMinute and endMinute in the 5 minute interval
                int startFiveMinuteInterval = timeSlice.StartMinute / 5;
                int endFiveMinuteInterval = (timeSlice.StartMinute + timeSlice.Duration) / 5;
                int newFiveMinuteInterval = timeSelected[0] / 5;

                if (newFiveMinuteInterval >= endFiveMinuteInterval)
                {
                    // If in the next slice, fill the slice
                    if (newFiveMinuteInterval == endFiveMinuteInterval)
                {
                        timeSlice.Duration += 5;
                    } // Check to see if the hour boundary has been crossed backwards 
                    else if (newFiveMinuteInterval == startFiveMinuteInterval + 11)
                    {
                        timeSlice.StartMinute = (timeSlice.StartMinute + 55) % 60;
                        timeSlice.Duration += 5;
                }
            }
                else if (newFiveMinuteInterval < startFiveMinuteInterval)
            {
                    // If in the previous slice, fill the slice
                    if (newFiveMinuteInterval == startFiveMinuteInterval - 1)
                    {
                        timeSlice.StartMinute = newFiveMinuteInterval * 5;
                        timeSlice.Duration += 5;
                    }  // Check to see if the hour boundary has been crossed forwards 
                    else if (newFiveMinuteInterval == endFiveMinuteInterval - 12)
                    {
                        timeSlice.Duration += 5;
            }
        }

                startFiveMinuteInterval = timeSlice.StartMinute / 5;
                endFiveMinuteInterval = (timeSlice.StartMinute + timeSlice.Duration) / 5;

                // Find the gauge in the radialGaugeList and update the minAngle and maxAngle
                RadialGauge radialGauge = radialGaugeList.Find(x => x.Name == selectedGauge);
                radialGauge.MinAngle = startFiveMinuteInterval * 30;
                radialGauge.MaxAngle = (endFiveMinuteInterval) * 30;
            }
        }
    }


    private static int[] GetMinutesFromCoordinate(Point point)
    {
        int radialLevel;
        // Get the x and y co-ordinate of where the clock was clicked from the PointerEventHandler

        // Get the angle in degrees of the point clicked if x =100 and y=100 is the center of the clock.  0 degrees is 12 o'clock
        var angle = Math.Atan2(point.Y - 100, point.X - 100) * (180 / Math.PI);

        // Convert the angle to a positive value
        angle = 180 + angle;

        // Get the minutes from the angle
        var minutes = (int)(angle / 6);

        // Currently the angle 0 is 9 o'clock so we need to convert it to 12 o'clock and use mod 60 to get the minutes
        minutes = (minutes + 45) % 60;

        // The inner circle has a diameter of 140 and the outer circle a diameter of 200 - check to see if the mouse click is within the inner circle
        if (Math.Sqrt(Math.Pow(point.X - 100, 2) + Math.Pow(point.Y - 100, 2)) < 55)
        {
            radialLevel = (int)RadialLevel.Outer;
        }
        else
        {
            radialLevel = (int)RadialLevel.Inner;
        }

        return new int[] { minutes, radialLevel };

    }

    private SolidColorBrush GetNextColourBrush()
    {
        string[] hexCodeArray = { "#FF0072B2", "#FFCC79A7", "#FFF0E442", "#FF009E73", "#FF785EF0",
                                      "#FFD55E00", "#FF56B4E9" , "#FF000000", "#FFDC267F", "#FF117733"};


        // Go through hexCodeArray and see if a colour is already taken in the RadialGauge list
        for (int i = 0; i < hexCodeArray.Length; i++)
        {
            if (radialGaugeList.Find(x => ((SolidColorBrush)x.TrailBrush).Color.ToString() == hexCodeArray[i]) == null)
            {

                return new SolidColorBrush(ColorHelper.FromArgb(
                     Convert.ToByte(hexCodeArray[i].Substring(1, 2), 16),
                    Convert.ToByte(hexCodeArray[i].Substring(3, 2), 16),
                    Convert.ToByte(hexCodeArray[i].Substring(5, 2), 16),
                    Convert.ToByte(hexCodeArray[i].Substring(7, 2), 16)
                    ));
            }
        }


        // Generate a random colour
        Random random = new();
        // Generate random argb values
        byte a = (byte)random.Next(0, 255);
        byte r = (byte)random.Next(0, 255);
        byte g = (byte)random.Next(0, 255);
        byte b = (byte)random.Next(0, 255);

        return new SolidColorBrush(ColorHelper.FromArgb(a, r, g, b));


    }

    private void centreTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        localSettingsService.SetCentreText(centreTextBox.Text);
    }

}
