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

        MediaPlayer player;

        int secondsLeft;
        DispatcherTimer timer;
        DisplayManager displayManager;

        private PointInt32 lastPosition;

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

            if (seconds > 0)
            {
                StartTimer(seconds);
            }
            else
            {
                SetupCustomTimerSelection();
            }

            await Task.Delay(50);
        }

        private void SetupCustomTimerSelection()
        {
            for (int i = 0; i < 60; i++)
            {
                minutes.Items.Add(i);
                seconds.Items.Add(i);
            }

            for (int i = 0; i < 24; i++)
            {
                hours.Items.Add(i);
            }

            timerGauge.Visibility = Visibility.Collapsed;
            timerText.Visibility = Visibility.Collapsed;
            timeSelector.Visibility = Visibility.Visible;
        }

        private void StartTimer(int seconds)
        {
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

                player = new MediaPlayer();
                player.Source = MediaSource.CreateFromUri(new Uri(soundPath));
            }
            catch (UriFormatException uriEx)
            {
                Console.WriteLine($"URI format exception: {uriEx.Message}");
            }
            catch (FileNotFoundException fileEx)
            {
                Console.WriteLine($"File not found: {fileEx.Message}");
                // Fallback to default sound if the selected sound file is not found
                try
                {
                    string defaultSoundPath = Path.Combine(AppContext.BaseDirectory, "Assets", SoundSettings.GetSoundFileName(0));
                    player = new MediaPlayer();
                    player.Source = MediaSource.CreateFromUri(new Uri(defaultSoundPath));
                }
                catch (Exception)
                {
                    Console.WriteLine("Failed to load default sound file");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
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
            // Make the timerGauge text the absolute value of secondsLeft
            SetTimerText();

            if (secondsLeft >= 0)
            {
                timerGauge.Value = secondsLeft;
            }

            if (secondsLeft == 0)
            {
                // Play ring.wav in the Assets directory
                player.Play();
            }
        }

        private void SetTimerText()
        {
            int secondsToShow = Math.Abs(secondsLeft);

            // If seconds left is negative, change to red text
            if (secondsLeft < 0)
            {
                timerText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
            }

            // If more than an hour, show hours, minutes and seconds
            if (secondsToShow > 3599)
            {
                // Break the text into hours, minutes and seconds
                int hours = secondsToShow / 3600;
                int minutes = (secondsToShow % 3600) / 60;
                int seconds = secondsToShow % 60;
                // Seconds and minutes should have a leading zero if less than 10
                timerText.Text = $"{hours}:{minutes.ToString("D2")}:{seconds.ToString("D2")}";
            }
            else if (secondsToShow > 59)
            {
                // Break the text into minutes and seconds
                int minutes = secondsToShow / 60;
                int seconds = secondsToShow % 60;
                // Seconds should have a leading zero if less than 10
                timerText.Text = $"{minutes}:{seconds.ToString("D2")}";
            }
            else
            {
                timerText.Text = secondsToShow.ToString();
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


        private void StartButton_Click(object sender, RoutedEventArgs e)
        {

            // Get the number of seconds from the comboboxes - blank values should be treated as 0
            int hoursSelected = hours.SelectedItem == null ? 0 : (int)hours.SelectedItem;
            int minutesSelected = minutes.SelectedItem == null ? 0 : (int)minutes.SelectedItem;
            int secondsSelected = seconds.SelectedItem == null ? 0 : (int)seconds.SelectedItem;

            // Work out the overall number of seconds
            int totalSeconds = (hoursSelected * 3600) + (minutesSelected * 60) + secondsSelected;

            StartTimer(totalSeconds);
            timerGauge.Visibility = Visibility.Visible;
            timerText.Visibility = Visibility.Visible;
            timeSelector.Visibility = Visibility.Collapsed;


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
}