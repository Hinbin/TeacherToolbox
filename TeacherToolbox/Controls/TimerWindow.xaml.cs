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
using Windows.ApplicationModel.VoiceCommands;
using System.Threading.Tasks;
using Microsoft.UI.Windowing;
using System.Linq;
using TeacherToolbox.Helpers;
using Microsoft.UI;
using static System.Net.WebRequestMethods;
using Microsoft.UI.Xaml.Controls;
using System.Runtime.CompilerServices;

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
            this.InitializeComponent();

            this.ExtendsContentIntoTitleBar = true;
            this.IsMaximizable = false;
            this.IsMinimizable = false;
            this.SizeChanged += TimerWindow_SizeChanged;

            // Set the background
            TrySetAcrylicBackdrop(true);

            // Make this always on top and centered on screen
            this.IsAlwaysOnTop = true;
            LoadSoundFile();

            dragHelper = new WindowDragHelper(this);

            // If the number of seconds is above 0, show the standard timer.  Otherwise, let the user select a time.
            if (seconds > 0)
            {
                StartTimer(seconds);
            }
            else
            {
                SetupCustomTimerSelection();
            }

            displayManager = new DisplayManager();

            // ResizeEndTimer is used to save the window position after resizing
            resizeEndTimer = new DispatcherTimer();
            resizeEndTimer.Interval = TimeSpan.FromMilliseconds(500); // Adjust the delay as needed
            resizeEndTimer.Tick += ResizeEndTimer_Tick;
        }

        public async Task InitializeAsync()
        {
            localSettings = await LocalSettings.CreateAsync();
            PointInt32 lastPosition = new PointInt32(localSettings.LastWindowPosition.X, localSettings.LastWindowPosition.Y);
            ulong lastDisplayIdValue = localSettings.LastWindowPosition.DisplayID;
            // Check to see if the last display are is present - if so , move the window to that position
            var allDisplayAreas = displayManager.DisplayAreas;
            // Check through all the displayIDs of the display areas to see if the last display area is present                
            if (allDisplayAreas.Any(da => da.DisplayId.Value == lastDisplayIdValue))
            {
                this.Move(lastPosition.X, lastPosition.Y);
            }
            else
            {
                // If not, center the window on the screen
                this.CenterOnScreen();
            }

            if (localSettings.LastWindowPosition.Width > 10 && localSettings.LastWindowPosition.Height > 10)
            {
                this.Height = localSettings.LastWindowPosition.Height;
                this.Width = localSettings.LastWindowPosition.Width;
            } 
            else {
                this.Height = 300;
                this.Width = 300;
            }

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

        private void LoadSoundFile()
        {
            try
            {
                string soundPath = Path.Combine(AppContext.BaseDirectory, "Assets", "ring.wav");
                player = new MediaPlayer();
                player.Source = MediaSource.CreateFromUri(new Uri(soundPath));
            }
            catch (UriFormatException uriEx)
            {
                // Handle exception related to URI format
                Console.WriteLine($"URI format exception: {uriEx.Message}");
            }
            catch (FileNotFoundException fileEx)
            {
                // Handle exception related to file not found
                Console.WriteLine($"File not found: {fileEx.Message}");
            }
            catch (Exception ex)
            {
                // Handle any other exception
                Console.WriteLine($"An error occurred: {ex.Message}");
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
            // Make sure any Mica/Acrylic controller is disposed so it doesn't try to
            // use this closed window.
            if (m_acrylicController != null)
            {
                m_acrylicController.Dispose();
                m_acrylicController = null;
            }
            this.Activated -= Window_Activated;
            m_configurationSource = null;

            // Remove the timer
            player.Dispose();

            // Check to see if the timer is running, and if so stop it
            if (timer != null)
            {
                timer.Stop();
            }

            displayManager.Dispose();
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
            resizeEndTimer.Stop();
            resizeEndTimer.Start();
        }

        private void ResizeEndTimer_Tick(object sender, object e)
        {
            resizeEndTimer.Stop();
            // Perform actions here after resizing has stopped
            localSettings.LastWindowPosition = GetCurrentWindowInformation();
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