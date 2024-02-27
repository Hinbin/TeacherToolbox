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


using Microsoft.UI.Composition;
using System.Numerics;
using Windows.ApplicationModel.Preview.Notes;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

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

            public Clock()
            {
                this.InitializeComponent();

                this.Loaded += Clock_Loaded;

                _timer.Interval = TimeSpan.FromMilliseconds(200);
                _timer.Tick += Timer_Tick;
                timePickerFlyout.TimePicked += TimePickerFlyout_TimePicked;

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
            // Check to see if we have a new second
            if (now.Second != DateTime.Now.Second)
            { 
                digitalTimeTextBlock.Text = now.ToString("h:mm tt");
                // Add offset on to current time
                now = DateTime.Now.Add(offset);

                _batch = _compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
                var animation = _compositor.CreateScalarKeyFrameAnimation();
                var seconds = (float)(int)now.TimeOfDay.TotalSeconds - 2;

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

            private void Clock_Tapped(object sender, TappedRoutedEventArgs e)
            {
                FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);
            }
    }
    }

