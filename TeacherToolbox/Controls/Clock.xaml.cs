using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

using CommunityToolkit.WinUI.UI.Controls;


using Microsoft.UI.Composition;
using System.Numerics;
using Windows.ApplicationModel.Preview.Notes;
using Windows.ApplicationModel.VoiceCommands;
using System.ComponentModel.Design;
using CommunityToolkit.Common;
using Windows.ApplicationModel.Contacts;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

enum RadialLevel
{
    Inner,
    Outer
}

namespace TeacherToolbox.Controls
{

    public sealed partial class Clock : UserControl
    {
        private Compositor _compositor;
        private ContainerVisual _root;

        private SpriteVisual _hourhand;
        private SpriteVisual _minutehand;
        private SpriteVisual _secondhand;
        private CompositionScopedBatch _batch;

        private DispatcherTimer _timer = new DispatcherTimer();

        private DateTime now;

        private TimeSpan offset = TimeSpan.Zero;

        // Private 2d array to hold the values for the gauges, 60 minutes and 2 radial levels
        private int[,] gaugeArray;
        private int selectedGauge = -1;
        private int gaugeCount = 0;

        // A C# list to hold the radial gauges
        private List<RadialGauge> radialGaugeList = new List<RadialGauge>();

        public Clock()
        {
            this.InitializeComponent();

            this.Loaded += Clock_Loaded;

            _timer.Interval = TimeSpan.FromMilliseconds(200);
            _timer.Tick += Timer_Tick;
            timePickerFlyout.TimePicked += TimePickerFlyout_TimePicked;

            // Make a 2d gauge array to hold the values for the gauges, 60 minutes and 2 radial levels.  Set all values to -1
            gaugeArray = new int[60, 2];
            for (int i = 0; i < 60; i++)
            {
                gaugeArray[i, 0] = -1;
                gaugeArray[i, 1] = -1;
            }


        }

        public bool ShowTicks { get; set; } = true;

        public Brush FaceColor { get; set; } = new SolidColorBrush(Colors.Transparent);
        public ImageSource BackgroundImage { get; set; }

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

        private void Clock_Loaded(object sender, RoutedEventArgs e)
        {
            now = DateTime.Now;

            Face.Fill = FaceColor;

            // Get the containerVisual for the canvas in WinUI3

            _root = ElementCompositionPreview.GetElementVisual(Container) as ContainerVisual;
            _compositor = _root.Compositor;

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
            _hourhand.Brush = _compositor.CreateColorBrush(Colors.Black);
            _hourhand.CenterPoint = new Vector3(2.0f, 50.0f, 0);
            _hourhand.Offset = new Vector3(98.0f, 50.0f, 0);
            _root.Children.InsertAtTop(_hourhand);

            // Minute Hand
            _minutehand = _compositor.CreateSpriteVisual();
            _minutehand.Size = new Vector2(4.0f, 100.0f);
            _minutehand.Brush = _compositor.CreateColorBrush(Colors.Black);
            _minutehand.CenterPoint = new Vector3(2.0f, 80.0f, 0);
            _minutehand.Offset = new Vector3(98.0f, 20.0f, 0);
            _root.Children.InsertAtTop(_minutehand);

            SetHoursAndMinutes();

            // Add XAML element.
            if (BackgroundImage != null)
            {
                var xaml = new Image();
                xaml.Source = BackgroundImage;
                xaml.Height = 200;
                xaml.Width = 200;
                Container.Children.Add(xaml);
            }

            _timer.Start();
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

        private void Clock_Pointer_Pressed(object sender, PointerRoutedEventArgs e)
        {
            // Capture pointer
            var canvas = (Canvas)sender;
            canvas.CapturePointer(e.Pointer);

            // Get the x and y co-ordinate of where the clock was clicked
            var point = e.GetCurrentPoint((UIElement)sender).Position;

            // Get the minutes from the angle
            var timeSelected = GetMinutesFromCoordinate(point);

            // On a left mouse click or touch event
            if (e.GetCurrentPoint((UIElement)sender).Properties.IsLeftButtonPressed)
            {
                // If a gauge doesn't already exist at this position, create one
                if (gaugeArray[timeSelected[0], timeSelected[1]] == -1)
                {
                    // Add a gauge to the canvas
                    addGauge(timeSelected, canvas);


                } // else if a gauge is already there, select it
                else
                {
                    // Set the selectedGauge to the gaugeArray value
                    selectedGauge = gaugeArray[timeSelected[0], timeSelected[1]];
                }
            }

            // On a right mouse click or touch event
            if (e.GetCurrentPoint((UIElement)sender).Properties.IsRightButtonPressed)
            {
                // If a gauge exists at this position, remove it
                if (gaugeArray[timeSelected[0], timeSelected[1]] > -1)
                {
                    removeGauge(timeSelected, canvas);

                }
            }
        }

        private void removeGauge(int[] timeSelected, Canvas canvas)
        {
            int gaugeNumber = gaugeArray[timeSelected[0], timeSelected[1]];
            // Loop through the gaugeArray to find all instances of the selected gauge and set the value to -1
            for (int i = 0; i < 60; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    if (gaugeArray[i, j] == gaugeNumber)
                    {
                        gaugeArray[i, j] = -1;
                    }
                }
            }

            // Find the gauge in the radialGaugeList and remove it
            for (int i = 0; i < radialGaugeList.Count; i++)
            {
                if (radialGaugeList[i].Name == "Gauge" + gaugeNumber)
                {
                    canvas.Children.Remove(radialGaugeList[i]);
                    radialGaugeList.RemoveAt(i);
                    break;
                }
            }
        }

        private void addGauge(int[] timeSelected, Canvas canvas)
        {
            int radialLevel = timeSelected[1];

            // Work out the 5 minute interval the minute is in, rounded down to the nearest 5 minutes
            int fiveMinuteInterval = timeSelected[0] / 5;

            // Programatically create a WinUI3 Community toolbox radial gauge to radialGaugeList 
            RadialGauge newGauge = new RadialGauge();

            // if radialLevel is 0, set the gauge to 200x200, else set it to 100x100

            newGauge.Name = "Gauge" + gaugeCount;

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
                newGauge.Width = 100;
                newGauge.Height = 100;
                // Set the position of the new gauge
                newGauge.SetValue(Canvas.LeftProperty, 50);
                newGauge.SetValue(Canvas.TopProperty, 50);
                newGauge.SetValue(Canvas.ZIndexProperty, 2);
                newGauge.ScalePadding = 20;
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
            newGauge.TrailBrush = getRandomColourBrush();
            newGauge.TrailBrush.Opacity = 0.65;
            newGauge.Minimum = 0;
            newGauge.Maximum = 1;
            newGauge.Value = 1;

            newGauge.NeedleWidth = 0;
            newGauge.NeedleBrush = new SolidColorBrush(Colors.Pink);

            // Add the new gauge to the canvas
            Container.Children.Add(newGauge);

            // Set the selectedGauge to the last gauge added
            selectedGauge = gaugeCount;
            gaugeCount++;

            // Add the new gauge to the radialGaugeList
            radialGaugeList.Add(newGauge);

            newGauge.Name = "Gauge" + selectedGauge;


            // Set the gaugeArray value to the selectedGauge for all 5 minutes in the interval
            for (int i = 0; i < 5; i++)
            {
                gaugeArray[fiveMinuteInterval * 5 + i, timeSelected[1]] = selectedGauge;
            }

        }

        private void Clock_Pointer_Released(object sender, PointerRoutedEventArgs e)
        {
            // Release pointer
            var canvas = (Canvas)sender;
            canvas.ReleasePointerCapture(e.Pointer);

            // Get the x and y co-ordinate of where the clock was clicked
            var point = e.GetCurrentPoint((UIElement)sender).Position;

            // Get the minutes from the angle
            var minutes = GetMinutesFromCoordinate(point);
            selectedGauge = -1;

        }

        private void Clock_Pointer_Exited(object sender, PointerRoutedEventArgs e)
        {
            // Release pointer
            var canvas = (Canvas)sender;
            canvas.ReleasePointerCapture(e.Pointer);
            selectedGauge = -1;
        }

        private void Clock_Pointer_Moved(object sender, PointerRoutedEventArgs e)
        {
            // Check if the pointer is captured
            if (e.Pointer.IsInContact)
            {
                // Get the x and y co-ordinate of where the clock was clicked
                var point = e.GetCurrentPoint((UIElement)sender).Position;

                // Get the minutes from the angle
                var timeSelected = GetMinutesFromCoordinate(point);

                // If a gauge has been selected, call the function to change the range of minutes it is set to
                if (selectedGauge > -1)
                {
                    // Loop through the gaugeArray to find the start minute and end minute of the selected gauge for the current radialLevel
                    int startMinute = -1;
                    int endMinute = -1;
                    for (int i = 0; i < 60; i++)
                    {
                        if (gaugeArray[i, timeSelected[1]] == selectedGauge)
                        {
                            if (startMinute == -1)
                            {
                                startMinute = i;
                            }
                            endMinute = i;
                        }
                    }

                    // if startMinute is 0 and end minute is 59 then as 



                    // Get the startMinute and endMinute in the 5 minute interval
                    int startFiveMinuteInterval = startMinute / 5;
                    int endFiveMinuteInterval = endMinute / 5;

                    // Get the 5 minute interval the minute is in, rounded down to the nearest 5 minutes
                    int newFiveMinuteInterval = timeSelected[0] / 5;

                    // If going bakwards, swap the start and end intervals, but don't allow this if the newFiveMinuteInterval already has a gauge in it, or in any of the 5 minute intervals between the start and newFiveMinuteInterval
                    if (newFiveMinuteInterval < startFiveMinuteInterval)
                    {
                        for (int i = newFiveMinuteInterval; i < startFiveMinuteInterval; i++)
                        {
                            if (gaugeArray[i * 5, timeSelected[1]] != -1)
                            {
                                return;
                            }
                        }
                        startFiveMinuteInterval = newFiveMinuteInterval;
                        newFiveMinuteInterval = endFiveMinuteInterval;
                    }


                    // Make all 5 minute intervals between the start of the selected gauge and 5 minute interval the pointer is in the same as the selected gauge
                    // but do not overwrite any other gauges
                    for (int i = startFiveMinuteInterval; i <= newFiveMinuteInterval; i++)
                    {
                        if (gaugeArray[i * 5, timeSelected[1]] == -1)
                        {
                            for (int j = 0; j < 5; j++)
                            {
                                gaugeArray[i * 5 + j, timeSelected[1]] = selectedGauge;
                            }
                            endFiveMinuteInterval = newFiveMinuteInterval;
                        } else if (gaugeArray[i * 5, timeSelected[1]] != selectedGauge)
                        {
                            break;
                        }
                    }
                    // Find the gauge in the radialGaugeList and update the minAngle and maxAngle
                    for (int j = 0; j < radialGaugeList.Count; j++)
                    {
                        if (radialGaugeList[j].Name == "Gauge" + selectedGauge)
                        {
                            radialGaugeList[j].MinAngle = startFiveMinuteInterval * 30;
                            radialGaugeList[j].MaxAngle = (endFiveMinuteInterval + 1) * 30;
                            
                            break;
                        }
                    }

                }
            }
        }




        private void Clock_Pointer_Canceled(object sender, PointerRoutedEventArgs e)
        {
            // Release pointer
            var canvas = (Canvas)sender;
            canvas.ReleasePointerCapture(e.Pointer);
            selectedGauge = -1;
        }


        private int[] GetMinutesFromCoordinate(Point point)
        {
            int radialLevel = 0;
            // Get the x and y co-ordinate of where the clock was clicked from the PointerEventHandler

            // Get the angle in degrees of the point clicked if x =100 and y=100 is the center of the clock.  0 degrees is 12 o'clock
            var angle = Math.Atan2(point.Y - 100, point.X - 100) * (180 / Math.PI);

            // Convert the angle to a positive value
            angle = 180 + angle;

            // Get the minutes from the angle
            var minutes = (int)(angle / 6);

            // Currently the angle 0 is 9 o'clock so we need to convert it to 12 o'clock and use mod 60 to get the minutes
            minutes = (minutes + 45) % 60;

            // Check if the position was for the inner circle of the clock - half the size of the clock
            if (point.X > 60 && point.X < 140 && point.Y > 60 && point.Y < 140)
            {
                radialLevel = (int)RadialLevel.Outer;
            }
            else
            {
                radialLevel = (int)RadialLevel.Inner;
            }

            //write line to debug window
            //System.Diagnostics.Debug.WriteLine("Minutes: " + minutes + " Radial Level: " + radialLevel + " X: " + point.X + "Y: " + point.Y);

            return new int[] { minutes, radialLevel };

        }

        private SolidColorBrush getRandomColourBrush()
        {
            // Create a random number generator
            Random random = new Random();

            // Create a random colour
            byte[] colour = new byte[3];
            random.NextBytes(colour);

            // Create a new SolidColorBrush with the random colour
            return new SolidColorBrush(Windows.UI.Color.FromArgb(255, colour[0], colour[1], colour[2]));
        }


    }
}
