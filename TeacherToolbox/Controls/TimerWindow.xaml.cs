using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Input;
using System;
using System.IO;
using WinUIEx;
using WinRT;
using System.Runtime.InteropServices;
using Windows.Media.Playback;
using Windows.Media.Core;
using Windows.Graphics;
using TeacherToolbox.Model;
using System.Threading.Tasks;
using Microsoft.UI.Windowing;
using System.Linq;
using TeacherToolbox.Helpers;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Dispatching;
using Windows.System;
using System.Collections.Generic;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace TeacherToolbox.Controls
{

    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class TimerWindow : WindowEx
    {

        WindowsSystemDispatcherQueueHelper m_wsdqHelper; // See separate sample below for implementation
        Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController m_acrylicController;
        Microsoft.UI.Composition.SystemBackdrops.SystemBackdropConfiguration m_configurationSource;

        private WindowDragHelper dragHelper;
        public LocalSettings localSettings;
        private DispatcherTimer resizeEndTimer;

        // To allow for a draggable window
        IntPtr hWnd = IntPtr.Zero;

        int secondsLeft;
        DispatcherTimer timer;
        DisplayManager displayManager;

        private PointInt32 lastPosition;

        private bool isSoundAvailable;
        private MediaPlayer player;

        private int intervalCount = 1;
        private const int MaxIntervals = 8;        
        private Queue<IntervalTime> intervals;
        private int currentIntervalTotal;
        private int intervalNumber = 0;

        public TimerWindow(int seconds)
        {
            try
            {
                // Set initial size before InitializeComponent
                this.Height = 100;
                this.Width = 100;

                this.InitializeComponent();

                // Only set opacity if content exists
                if (this.Content != null)
                {
                    this.Content.Opacity = 0;
                }

                InitializeWindowAsync(seconds);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during window construction: {ex.Message}");
                // Ensure content is visible if there's an error
                if (this.Content != null)
                {
                    this.Content.Opacity = 1;
                }
            }
        }

        private async void InitializeWindowAsync(int seconds)
        {
            try
            {
                // Basic window setup
                this.ExtendsContentIntoTitleBar = true;

                // Initialize timers with proper error handling
                InitializeTimers();

                // Set up window properties
                ConfigureWindowProperties();

                // Initialize all resources
                await InitializeResourcesAsync();

                // Position and show the window
                await FinalizeWindowSetupAsync(seconds);                

                // Make content visible
                if (this.Content != null)
                {
                    this.Content.Opacity = 1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during initialization: {ex.Message}");
                if (this.Content != null)
                {
                    this.Content.Opacity = 1;
                }
            }
        }

        private void InitializeTimers()
        {
            resizeEndTimer = new DispatcherTimer();
            resizeEndTimer.Interval = TimeSpan.FromMilliseconds(500);
            resizeEndTimer.Tick += ResizeEndTimer_Tick;
            this.SizeChanged += TimerWindow_SizeChanged;
        }

        private void ConfigureWindowProperties()
        {
            TrySetAcrylicBackdrop(true);
            ThemeHelper.ApplyThemeToWindow(this);

            var presenter = AppWindow?.Presenter as OverlappedPresenter;
            if (presenter != null)
            {
                presenter.IsMinimizable = false;
                presenter.IsMaximizable = false;
                presenter.IsAlwaysOnTop = true;
            }
        }

        private async Task InitializeResourcesAsync()
        {
            await LoadSoundFileAsync();
            dragHelper = new WindowDragHelper(this);
            localSettings = await LocalSettings.CreateAsync();
            displayManager = new DisplayManager();
        }

        private async Task FinalizeWindowSetupAsync(int seconds)
        {
            // Handle window positioning first
            if (localSettings?.LastWindowPosition != null)
            {
                if (localSettings.LastWindowPosition.Width > 10 && localSettings.LastWindowPosition.Height > 10)
                {
                    this.Height = localSettings.LastWindowPosition.Height;
                    this.Width = localSettings.LastWindowPosition.Width;
                }

                PointInt32 lastPosition = new PointInt32(localSettings.LastWindowPosition.X, localSettings.LastWindowPosition.Y);
                ulong lastDisplayIdValue = localSettings.LastWindowPosition.DisplayID;

                var allDisplayAreas = displayManager?.DisplayAreas;
                if (allDisplayAreas?.Any(da => da.DisplayId.Value == lastDisplayIdValue) == true)
                {
                    this.Move(lastPosition.X, lastPosition.Y);
                }
                else
                {
                    this.CenterOnScreen();
                }
            }
            else
            {
                this.CenterOnScreen();
            }

            // Wait for window positioning to complete
            await Task.Delay(100);

            if (seconds > 0)
            {
                StartTimer(seconds);
            }
            else if (seconds == 0)
            {
                SetupCustomTimerSelection();

                // Add a slight delay after setup and then focus the minutes control
                await Task.Delay(100);

                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.High, async () =>
                {
                    // Ensure the ComboBox is in edit mode
                    minutes.IsDropDownOpen = false;  // Close dropdown if open
                    minutes.IsEditable = true;       // Ensure it's editable
                    minutes.Focus(FocusState.Programmatic);

                    // Get the TextBox and put it in edit mode
                    var textBox = minutes.Descendants<TextBox>().FirstOrDefault();
                    if (textBox != null)
                    {
                        textBox.Focus(FocusState.Programmatic);
                        textBox.SelectAll();
                    }
                });
            }
            else if (seconds == -1)
            {
                SetupCustomTimerSelection();

                addIntervalButton.Visibility = Visibility.Visible;
            }
        }

        private void SetupCustomTimerSelection()
        {
            // Populate the combo boxes
            for (int i = 0; i < 60; i++)
            {
                minutes.Items.Add(i);
                seconds.Items.Add(i);
            }

            for (int i = 0; i < 24; i++)
            {
                hours.Items.Add(i);
            }

            // Set visibility
            timerGauge.Visibility = Visibility.Collapsed;
            timerText.Visibility = Visibility.Collapsed;
            timeSelector.Visibility = Visibility.Visible;

            // Set initial values
            hours.Text = "0";
            minutes.Text = "0";
            seconds.Text = "0";

            // Remove any existing handlers first to prevent duplicates
            hours.KeyDown -= ComboBox_KeyDown;
            minutes.KeyDown -= ComboBox_KeyDown;
            seconds.KeyDown -= ComboBox_KeyDown;
            hours.TextSubmitted -= ComboBox_TextSubmitted;
            minutes.TextSubmitted -= ComboBox_TextSubmitted;
            seconds.TextSubmitted -= ComboBox_TextSubmitted;

            // Add event handlers to all combo boxes
            hours.KeyDown += ComboBox_KeyDown;
            minutes.KeyDown += ComboBox_KeyDown;
            seconds.KeyDown += ComboBox_KeyDown;
            hours.TextSubmitted += ComboBox_TextSubmitted;
            minutes.TextSubmitted += ComboBox_TextSubmitted;
            seconds.TextSubmitted += ComboBox_TextSubmitted;

            // Set tab order
            hours.TabIndex = 0;
            minutes.TabIndex = 1;
            seconds.TabIndex = 2;
            startButton.TabIndex = 3;
        }

        private void ComboBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                e.Handled = true;
                StartTimer_FromCustomSelection();
            }
        }

        private void ComboBox_TextSubmitted(ComboBox sender, ComboBoxTextSubmittedEventArgs args)
        {
            bool isValid = false;
            if (int.TryParse(args.Text, out int value))
            {
                // Validate range based on which ComboBox it is
                if (sender == hours && value >= 0 && value < 24)
                {
                    isValid = true;
                }
                else if ((sender == minutes || sender == seconds) && value >= 0 && value < 60)
                {
                    isValid = true;
                }
            }

            if (!isValid)
            {
                args.Handled = true;
                sender.Text = "0";
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            StartTimer_FromCustomSelection();
        }


        private void StartTimer(int seconds)
        {
            // Clean up existing timer if it exists
            if (timer != null)
            {
                timer.Stop();
                timer.Tick -= Timer_Tick;
                timer = null;
            }

            secondsLeft = seconds;

            // If less than 60 seconds, use 60 seconds as the timerGauge maximum
            timerGauge.Maximum = seconds < 60 ? 60 : seconds;
            timerGauge.Minimum = 0;
            timerGauge.Value = secondsLeft;
            SetTimerTickInterval();
            SetTimerText();

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private async Task LoadSoundFileAsync()
        {
            try
            {
                var settings = await LocalSettings.CreateAsync();
                int soundIndex = settings.GetValueOrDefault(SoundSettings.SoundKey, 0);
                string soundFileName = SoundSettings.GetSoundFileName(soundIndex);
                string soundPath = Path.Combine(AppContext.BaseDirectory, "Assets", soundFileName);

                if (File.Exists(soundPath))
                {
                    player = new MediaPlayer();
                    player.Source = MediaSource.CreateFromUri(new Uri(soundPath));
                    isSoundAvailable = true;
                }
                else
                {
                    // Try loading default sound
                    string defaultSoundPath = Path.Combine(AppContext.BaseDirectory, "Assets", SoundSettings.GetSoundFileName(0));
                    if (File.Exists(defaultSoundPath))
                    {
                        player = new MediaPlayer();
                        player.Source = MediaSource.CreateFromUri(new Uri(defaultSoundPath));
                        isSoundAvailable = true;
                    }
                    else
                    {
                        Console.WriteLine("No sound files available");
                        isSoundAvailable = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading sound: {ex.Message}");
                isSoundAvailable = false;
            }
        }

        private async Task InitializeWindowPositionAsync()
        {
            PointInt32 lastPosition = new PointInt32(localSettings.LastWindowPosition.X, localSettings.LastWindowPosition.Y);
            ulong lastDisplayIdValue = localSettings.LastWindowPosition.DisplayID;

            var allDisplayAreas = displayManager.DisplayAreas;
            if (allDisplayAreas.Any(da => da.DisplayId.Value == lastDisplayIdValue))
            {
                this.Move(lastPosition.X, lastPosition.Y);
            }
            else
            {
                this.CenterOnScreen();
            }

            if (localSettings.LastWindowPosition.Width > 10 && localSettings.LastWindowPosition.Height > 10)
            {
                this.Height = localSettings.LastWindowPosition.Height;
                this.Width = localSettings.LastWindowPosition.Width;
            }
            else
            {
                this.Height = 300;
                this.Width = 300;
            }
        }

        private void SetTimerTickInterval()
        {
            // If the timer is less than 60 seconds, tick interval to every 5 seconds
            if (timerGauge.Value <= 60)
            {
                timerGauge.TickSpacing = 5;
            } // If less than two minutes, a tick interval every 30 seconds
            else if (timerGauge.Value <= 120)
            {
                timerGauge.TickSpacing = 30;
            } // If less than 10 minutes, a tick interval every minute
            else if (timerGauge.Value <= 600)
            {
                timerGauge.TickSpacing = 60;
            } // Otherwise every 10% 
            else
            {
                timerGauge.TickSpacing = (int)timerGauge.Value / 10;
            }

        }

        private void Timer_Tick(object sender, object e)
        {
            secondsLeft--;
            SetTimerText();

            if (secondsLeft >= 0)
            {
                timerGauge.Value = secondsLeft;
            }

            if (secondsLeft == 0)
            {
                // Play sound at the end of each interval
                if (isSoundAvailable && player != null)
                {
                    try
                    {
                        player.Play();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error playing sound: {ex.Message}");
                    }
                }

                // Only check for more intervals if we're running in interval mode
                if (intervals != null && intervals.Count > 0)
                {
                    StartNextInterval();
                }
                // If no more intervals, let it continue counting up
                else
                {
                    timerText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
                    // Don't stop the timer - let it continue counting
                }
            }
        }


        private void StartNextInterval()
        {
            if (intervals.Count > 0)
            {
                var nextInterval = intervals.Dequeue();
                intervalNumber++;
                currentIntervalTotal = nextInterval.TotalSeconds;
                StartTimer(nextInterval.TotalSeconds);

                // Update gauge to show just this interval
                timerGauge.Maximum = currentIntervalTotal;
                timerGauge.Minimum = 0;
                timerGauge.Value = currentIntervalTotal;
                SetTimerTickInterval();
            }
        }

        private void SetTimerText()
        {
            int secondsToShow = Math.Abs(secondsLeft);

            string timeText;
            // If more than an hour, show hours, minutes and seconds
            if (secondsToShow > 3599)
            {
                int hours = secondsToShow / 3600;
                int minutes = (secondsToShow % 3600) / 60;
                int seconds = secondsToShow % 60;
                timeText = $"{hours}:{minutes.ToString("D2")}:{seconds.ToString("D2")}";
            }
            else if (secondsToShow > 59)
            {
                int minutes = secondsToShow / 60;
                int seconds = secondsToShow % 60;
                timeText = $"{minutes}:{seconds.ToString("D2")}";
            }
            else
            {
                timeText = secondsToShow.ToString();
            }

            // Add interval number if we have multiple intervals and we're not in overtime
            if (intervals != null && intervalCount > 1 && secondsLeft >= 0)
            {
                timerText.Text = $"{intervalNumber}/{intervalCount}: {timeText}";
            }
            else
            {
                timerText.Text = timeText;
            }

            // Set text color to red if we're in overtime
            if (secondsLeft < 0)
            {
                timerText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
            }
        }

        bool TrySetAcrylicBackdrop(bool useAcrylicThin)
        {
            if (Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController.IsSupported())
            {
                m_wsdqHelper = new WindowsSystemDispatcherQueueHelper();
                m_wsdqHelper.EnsureWindowsSystemDispatcherQueueController();

                // Hooking up the policy object
                m_configurationSource = new Microsoft.UI.Composition.SystemBackdrops.SystemBackdropConfiguration();
                this.Activated += Window_Activated;
                this.Closed += Window_Closed;
                ((FrameworkElement)this.Content).ActualThemeChanged += Window_ThemeChanged;

                // Initial configuration state.
                m_configurationSource.IsInputActive = true;
                SetConfigurationSourceTheme();

                m_acrylicController = new Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController();

                m_acrylicController.Kind = useAcrylicThin ? Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicKind.Thin : Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicKind.Base;

                // Enable the system backdrop.
                // Note: Be sure to have "using WinRT;" to support the Window.As<...>() call.
                m_acrylicController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
                m_acrylicController.SetSystemBackdropConfiguration(m_configurationSource);
                return true; // Succeeded.
            }

            return false; // Acrylic is not supported on this system.
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            m_configurationSource.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            try
            {
                // Unregister event handlers
                if (minutes != null)
                {
                    minutes.KeyDown -= ComboBox_KeyDown;
                    minutes.TextSubmitted -= ComboBox_TextSubmitted;
                }

                this.SizeChanged -= TimerWindow_SizeChanged;
                this.Activated -= Window_Activated;
                ((FrameworkElement)this.Content).ActualThemeChanged -= Window_ThemeChanged;

                // Dispose the acrylic controller
                if (m_acrylicController != null)
                {
                    m_acrylicController.Dispose();
                    m_acrylicController = null;
                }
                m_configurationSource = null;

                // Stop and cleanup the timer
                if (timer != null)
                {
                    timer.Stop();
                    timer.Tick -= Timer_Tick;
                    timer = null;
                }

                // Stop and cleanup the resize timer
                if (resizeEndTimer != null)
                {
                    resizeEndTimer.Stop();
                    resizeEndTimer.Tick -= ResizeEndTimer_Tick;
                    resizeEndTimer = null;
                }

                // Cleanup the media player
                if (player != null)
                {
                    player.Dispose();
                    player = null;
                }

                // Cleanup the display manager
                if (displayManager != null)
                {
                    displayManager.Dispose();
                    displayManager = null;
                }

                // Set content opacity back to normal in case window is reused
                if (this.Content != null)
                {
                    this.Content.Opacity = 1;
                }

                // Cleanup drag helper
                dragHelper = null;

                // Clear any remaining references
                m_wsdqHelper = null;
                localSettings = null;
            }
            catch (Exception ex)
            {
                // Log but don't rethrow as we're already closing
                Console.WriteLine($"Error during window cleanup: {ex.Message}");
            }
        }

        private void Window_ThemeChanged(FrameworkElement sender, object args)
        {
            if (m_configurationSource != null)
            {
                SetConfigurationSourceTheme();
            }
        }

        private void SetConfigurationSourceTheme()
        {
            switch (((FrameworkElement)this.Content).ActualTheme)
            {
                case ElementTheme.Dark: m_configurationSource.Theme = Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Dark; break;
                case ElementTheme.Light: m_configurationSource.Theme = Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Light; break;
                case ElementTheme.Default: m_configurationSource.Theme = Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Default; break;
            }
        }

        private void Grid_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            dragHelper.PointerReleased(sender, e);

            localSettings.LastWindowPosition = GetCurrentWindowInformation();
        }

        private void TimerWindow_SizeChanged(object sender, WindowSizeChangedEventArgs e)
        {
            if (resizeEndTimer != null)
            {
                resizeEndTimer.Stop();
                resizeEndTimer.Start();
            }
        }

        private void ResizeEndTimer_Tick(object sender, object e)
        {
            if (resizeEndTimer != null)
            {
                resizeEndTimer.Stop();
                // Perform actions here after resizing has stopped
                if (localSettings != null)
                {
                    localSettings.LastWindowPosition = GetCurrentWindowInformation();
                }
            }
        }


        private WindowPosition GetCurrentWindowInformation()
        {
            DisplayId displayId = DisplayArea.GetFromWindowId(this.AppWindow.Id, DisplayAreaFallback.Primary).DisplayId;
            return new WindowPosition(lastPosition.X, lastPosition.Y, this.Width, this.Height, displayId.Value);
        }

        private void AddIntervalButton_Click(object sender, RoutedEventArgs e)
        {
            if (intervalCount >= MaxIntervals) return;

            // Create a new row in the Grid
            var rowDef = new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) };
            intervalsPanel.RowDefinitions.Add(rowDef);

            // Create and add the new ComboBoxes
            var hoursBox = new ComboBox
            {
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Header = "Hours",
                IsEditable = true,
                Text = "0"
            };

            var minutesBox = new ComboBox
            {
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Header = "Minutes",
                IsEditable = true,
                Text = "0"
            };

            var secondsBox = new ComboBox
            {
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Header = "Seconds",
                IsEditable = true,
                Text = "0"
            };

            // Add the same event handlers as the original ComboBoxes
            hoursBox.KeyDown += ComboBox_KeyDown;
            minutesBox.KeyDown += ComboBox_KeyDown;
            secondsBox.KeyDown += ComboBox_KeyDown;
            hoursBox.TextSubmitted += ComboBox_TextSubmitted;
            minutesBox.TextSubmitted += ComboBox_TextSubmitted;
            secondsBox.TextSubmitted += ComboBox_TextSubmitted;

            // Populate the ComboBoxes
            for (int i = 0; i < 24; i++) hoursBox.Items.Add(i);
            for (int i = 0; i < 60; i++)
            {
                minutesBox.Items.Add(i);
                secondsBox.Items.Add(i);
            }

            // Set Grid positions
            Grid.SetRow(hoursBox, intervalCount);
            Grid.SetColumn(hoursBox, 0);
            Grid.SetRow(minutesBox, intervalCount);
            Grid.SetColumn(minutesBox, 1);
            Grid.SetRow(secondsBox, intervalCount);
            Grid.SetColumn(secondsBox, 2);

            // Add to the Grid
            intervalsPanel.Children.Add(hoursBox);
            intervalsPanel.Children.Add(minutesBox);
            intervalsPanel.Children.Add(secondsBox);

            intervalCount++;

            // Disable Add Interval button if we've reached the maximum
            if (intervalCount >= MaxIntervals)
            {
                addIntervalButton.IsEnabled = false;
            }
        }

        private void StartTimer_FromCustomSelection()
        {
            intervals = new Queue<IntervalTime>();

            // Collect all intervals
            for (int row = 0; row < intervalCount; row++)
            {
                var hourBox = GetComboBoxAt(row, 0);
                var minuteBox = GetComboBoxAt(row, 1);
                var secondBox = GetComboBoxAt(row, 2);

                if (hourBox != null && minuteBox != null && secondBox != null)
                {
                    var interval = new IntervalTime
                    {
                        Hours = int.TryParse(hourBox.Text, out int h) ? h : 0,
                        Minutes = int.TryParse(minuteBox.Text, out int m) ? m : 0,
                        Seconds = int.TryParse(secondBox.Text, out int s) ? s : 0
                    };

                    if (interval.TotalSeconds > 0)
                    {
                        intervals.Enqueue(interval);
                    }
                }
            }

            if (intervals.Count > 0)
            {
                StartNextInterval();
                timerGauge.Visibility = Visibility.Visible;
                timerText.Visibility = Visibility.Visible;
                timeSelector.Visibility = Visibility.Collapsed;
            }
        }
        private ComboBox GetComboBoxAt(int row, int column)
        {
            return intervalsPanel.Children
                .Cast<FrameworkElement>()
                .FirstOrDefault(e => Grid.GetRow(e) == row && Grid.GetColumn(e) == column) as ComboBox;
        }

        private void Grid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            dragHelper.PointerPressed(sender, e);
        }

        private void Grid_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            dragHelper.PointerMoved(sender, e);
        }

        protected override void OnPositionChanged(PointInt32 newPosition)
        {
            lastPosition = newPosition;
        }

        private void Grid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (timer == null) return;

            // If the sender is a text block with the word start, return
            if (e.OriginalSource is TextBlock textBlock)
            {
                if (textBlock.Text == "Start")
                {
                    return;
                }
            }

            // Use the VisualTreeHelper to get the element that was tapped
            DependencyObject tappedElement = VisualTreeHelper.GetParent((DependencyObject)e.OriginalSource);

            // Check to see if the tapped Element is the start button
            if (tappedElement != null)
            {
                if (tappedElement is Button startButton) return;
            }

            // Pause the timer if running.  If timer is paused, unpause it
            if (timer.IsEnabled)
            {
                timer.Stop();
                // Change the font to bold to indicate the timer is paused
                timerText.FontWeight = Microsoft.UI.Text.FontWeights.Thin;
                timerGauge.TrailBrush = new SolidColorBrush(Microsoft.UI.Colors.DarkGray);

            }
            else
            {
                timer.Start();
                // Change the font back to normal
                timerText.FontWeight = Microsoft.UI.Text.FontWeights.Normal;
                // Change the colour of the trailbrush back to #5b3493
                if (Application.Current.Resources.TryGetValue("darkPurpleBrush", out object darkPurpleBrush))
                {
                    timerGauge.TrailBrush = darkPurpleBrush as SolidColorBrush;
                }
            }

        }

    }


    class WindowsSystemDispatcherQueueHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        struct DispatcherQueueOptions
        {
            internal int dwSize;
            internal int threadType;
            internal int apartmentType;
        }

        [DllImport("CoreMessaging.dll")]
        private static extern int CreateDispatcherQueueController([In] DispatcherQueueOptions options, [In, Out, MarshalAs(UnmanagedType.IUnknown)] ref object dispatcherQueueController);

        object m_dispatcherQueueController = null;
        public void EnsureWindowsSystemDispatcherQueueController()
        {
            if (Windows.System.DispatcherQueue.GetForCurrentThread() != null)
            {
                // one already exists, so we'll just use it.
                return;
            }

            if (m_dispatcherQueueController == null)
            {
                DispatcherQueueOptions options;
                options.dwSize = Marshal.SizeOf(typeof(DispatcherQueueOptions));
                options.threadType = 2;    // DQTYPE_THREAD_CURRENT
                options.apartmentType = 2; // DQTAT_COM_STA

                CreateDispatcherQueueController(options, ref m_dispatcherQueueController);
            }
        }
    }

    class IntervalTime
    {
        public int Hours { get; set; }
        public int Minutes { get; set; }
        public int Seconds { get; set; }
        public int TotalSeconds => (Hours * 3600) + (Minutes * 60) + Seconds;
    }

}

