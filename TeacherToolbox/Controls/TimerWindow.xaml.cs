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

        // To allow for a draggable window
        private Microsoft.UI.Windowing.AppWindow _apw;
        IntPtr hWnd = IntPtr.Zero;

        MediaPlayer player;

        int secondsLeft;
        DispatcherTimer timer;

        public TimerWindow(int seconds)
        {
            this.InitializeComponent();

            this.ExtendsContentIntoTitleBar = true;
            this.IsMaximizable = false;
            this.IsMinimizable = false;

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
            else if(timerGauge.Value <= 120)
            {
                timerGauge.TickSpacing = 30;
            } // If less than 10 minutes, a tick interval every minute
            else if(timerGauge.Value <= 600)
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

            if (secondsLeft >= 0) {
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
            }else if (secondsToShow > 59)
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
        }

        private void Grid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            dragHelper.PointerPressed(sender, e);
        }

        private void Grid_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            dragHelper.PointerMoved(sender, e);
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