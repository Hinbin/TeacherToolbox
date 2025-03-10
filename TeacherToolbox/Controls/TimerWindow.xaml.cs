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
using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.UI.Xaml.Data;

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
        private ObservableCollection<IntervalTimeViewModel> intervalsList;

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

            // First initialize the intervalsList to prevent null reference
            intervalsList = new ObservableCollection<IntervalTimeViewModel>();

            this.InitializeComponent();
            InitializeUIElements();
            InitializeWindowAsync(seconds);


        }

        // Update the InitializeUIElements method to properly handle the containers
        private void InitializeUIElements()
        {
            // Set default text for the timer
            if (timerText != null)
            {
                timerText.Text = "00:00";
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
                Debug.WriteLine($"Error during initialization: {ex.Message}");
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
            try
            {
                // Use a local variable for sound loading to avoid creating multiple LocalSettings instances
                var soundSettings = await LocalSettings.GetSharedInstanceAsync();
                int soundIndex = soundSettings.GetValueOrDefault(SoundSettings.SoundKey, 0);
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

                // Set up drag helper
                var settings = TeacherToolbox.Model.LocalSettings.GetSharedInstanceSync();
                dragHelper = new WindowDragHelper(this, settings);

                // Get the shared instance
                localSettings = await LocalSettings.GetSharedInstanceAsync();
                Console.WriteLine("Using shared LocalSettings instance in TimerWindow");

                // Initialize display manager
                displayManager = new DisplayManager();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in InitializeResourcesAsync: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");

                // Create minimal setup to avoid null references
                if (localSettings == null)
                {
                    localSettings = await LocalSettings.GetSharedInstanceAsync();
                }

                if (displayManager == null)
                {
                    displayManager = new DisplayManager();
                }

                isSoundAvailable = false;
            }
        }

        private async Task FinalizeWindowSetupAsync(int seconds)
        {
            // Handle window positioning first
            if (localSettings?.LastTimerWindowPosition != null)
            {
                if (localSettings.LastTimerWindowPosition.Width > 10 && localSettings.LastTimerWindowPosition.Height > 10)
                {
                    this.Height = localSettings.LastTimerWindowPosition.Height;
                    this.Width = localSettings.LastTimerWindowPosition.Width;
                }

                PointInt32 lastPosition = new PointInt32(localSettings.LastTimerWindowPosition.X, localSettings.LastTimerWindowPosition.Y);
                ulong lastDisplayIdValue = localSettings.LastTimerWindowPosition.DisplayID;

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
            else if (seconds == 0) // Custom timer
            {
                SetupCustomTimerSelection(seconds);
            }
            else if (seconds == -1) // Interval timer
            {
                SetupCustomTimerSelection(seconds);
            }
        }
        private async void SetupCustomTimerSelection(int timerType)
        {
            // Ensure intervalsList is initialized
            if (intervalsList == null)
            {
                intervalsList = new ObservableCollection<IntervalTimeViewModel>();
            }

            Debug.WriteLine($"Setting up custom timer selection. Type: {timerType}");
            Debug.WriteLine($"Is localSettings null? {localSettings == null}");

            if (localSettings == null)
            {
                Debug.WriteLine("LocalSettings is null, creating it now");
                localSettings = await LocalSettings.GetSharedInstanceAsync();
            }

            // Determine whether this is an interval timer or custom timer
            bool isIntervalTimer = timerType == -1;

            // Try to load saved configurations based on timer type
            List<SavedIntervalConfig> savedConfigs;

            if (isIntervalTimer)
            {
                // Load interval timer configurations
                savedConfigs = localSettings.GetSavedIntervalConfigs();
                Debug.WriteLine($"Loaded {savedConfigs?.Count ?? 0} saved interval configurations");
            }
            else
            {
                // Load custom timer configurations
                savedConfigs = localSettings.GetSavedCustomTimerConfigs();
                Debug.WriteLine($"Loaded {savedConfigs?.Count ?? 0} saved custom timer configurations");
            }

            if (savedConfigs != null && savedConfigs.Any())
            {
                // Clear existing intervals and load saved configurations
                intervalsList.Clear();
                foreach (var config in savedConfigs)
                {
                    Debug.WriteLine($"Loading interval: H:{config.Hours} M:{config.Minutes} S:{config.Seconds}");
                    intervalsList.Add(new IntervalTimeViewModel(intervalsList.Count + 1)
                    {
                        Hours = config.Hours,
                        Minutes = config.Minutes,
                        Seconds = config.Seconds
                    });
                }
            }
            else if (!intervalsList.Any())
            {
                Debug.WriteLine("No saved configs found, adding default interval");
                // If no saved configurations, add a default first interval
                intervalsList.Add(new IntervalTimeViewModel(1));
            }

            // Set UI element properties
            if (intervalsListView != null)
            {
                intervalsListView.ItemsSource = intervalsList;
            }

            if (timerGauge != null)
            {
                timerGauge.Visibility = Visibility.Collapsed;
            }

            if (timerText != null)
            {
                timerText.Visibility = Visibility.Collapsed;
            }

            if (timeSelector != null)
            {
                timeSelector.Visibility = Visibility.Visible;
            }

            // Show or hide the add interval button based on the timer type
            if (addIntervalButton != null)
            {
                addIntervalButton.Visibility = isIntervalTimer ? Visibility.Visible : Visibility.Collapsed;
            }

            // Update interval numbers and remove button visibility
            UpdateIntervalNumbers();
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
                // Get the binding property name from the ComboBox's parent StackPanel
                var stackPanel = VisualTreeHelper.GetParent(sender) as StackPanel;
                var label = stackPanel?.Children.OfType<TextBlock>().FirstOrDefault();
                string propertyName = label?.Text.ToLower();

                if (propertyName == "hours" && value >= 0 && value < 24)
                {
                    isValid = true;
                }
                else if ((propertyName == "minutes" || propertyName == "seconds") && value >= 0 && value < 60)
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
                var settings = await LocalSettings.GetSharedInstanceAsync();
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
                        Debug.WriteLine("No sound files available");
                        isSoundAvailable = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading sound: {ex.Message}");
                isSoundAvailable = false;
            }
        }

        private async Task InitializeWindowPositionAsync()
        {
            PointInt32 lastPosition = new PointInt32(localSettings.LastTimerWindowPosition.X, localSettings.LastTimerWindowPosition.Y);
            ulong lastDisplayIdValue = localSettings.LastTimerWindowPosition.DisplayID;

            var allDisplayAreas = displayManager.DisplayAreas;
            if (allDisplayAreas.Any(da => da.DisplayId.Value == lastDisplayIdValue))
            {
                this.Move(lastPosition.X, lastPosition.Y);
            }
            else
            {
                this.CenterOnScreen();
            }

            if (localSettings.LastTimerWindowPosition.Width > 10 && localSettings.LastTimerWindowPosition.Height > 10)
            {
                this.Height = localSettings.LastTimerWindowPosition.Height;
                this.Width = localSettings.LastTimerWindowPosition.Width;
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
                        Debug.WriteLine($"Error playing sound: {ex.Message}");
                    }
                }

                // Only check for more intervals if we're running in interval mode
                if (intervals != null && intervals.Count > 0)
                {
                    StartNextInterval();
                }
                else
                {
                    // Get the configured behavior for when timer finishes
                    TimerFinishBehavior behavior = GetTimerFinishBehavior();

                    // Handle timer finish behavior
                    HandleTimerFinish(behavior);
                }
            }
        }

        private void HandleTimerFinish(TimerFinishBehavior behavior)
        {
            switch (behavior)
            {
                case TimerFinishBehavior.CloseTimer:
                    // Stop the timer to prevent further ticks
                    timer.Stop();

                    // Set text color to red to indicate it's finished
                    timerText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);

                    // Play the sound and wait for it to complete before closing
                    if (isSoundAvailable && player != null)
                    {
                        try
                        {
                            // Subscribe to MediaEnded event to close window after sound completes
                            player.MediaEnded += (s, args) =>
                            {
                                // Use dispatcher to ensure we run on UI thread
                                DispatcherQueue.TryEnqueue(() =>
                                {
                                    this.Close();
                                });
                            };

                            // Play the sound
                            player.Play();

                            // Set a fallback timer in case MediaEnded doesn't fire
                            DispatcherTimer fallbackTimer = new DispatcherTimer();
                            fallbackTimer.Interval = TimeSpan.FromSeconds(5); // Longer timeout to be safe
                            fallbackTimer.Tick += (s, args) =>
                            {
                                fallbackTimer.Stop();
                                this.Close();
                            };
                            fallbackTimer.Start();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error playing sound: {ex.Message}");
                            // Close window immediately if there was an error playing sound
                            this.Close();
                        }
                    }
                    else
                    {
                        // No sound to play, close after a short delay
                        DispatcherTimer closeTimer = new DispatcherTimer();
                        closeTimer.Interval = TimeSpan.FromSeconds(1);
                        closeTimer.Tick += (s, args) =>
                        {
                            closeTimer.Stop();
                            this.Close();
                        };
                        closeTimer.Start();
                    }
                    break;

                case TimerFinishBehavior.CountUp:
                    // Timer continues counting (negative values)
                    timerText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
                    break;

                case TimerFinishBehavior.StayAtZero:
                    // Stop the timer
                    timer.Stop();

                    // Set text color to red to indicate it's finished
                    timerText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);

                    // Reset secondsLeft to 0 to ensure it displays as 0
                    secondsLeft = 0;
                    SetTimerText();
                    break;
            }
        }

        private TimerFinishBehavior GetTimerFinishBehavior()
        {
            // Default to CountUp if setting isn't found
            // Using the same key as defined in SettingsPage
            int behaviorValue = localSettings.GetValueOrDefault("TimerFinishBehavior", (int)TimerFinishBehavior.CountUp);

            // Ensure the value is within valid range
            if (!Enum.IsDefined(typeof(TimerFinishBehavior), behaviorValue))
            {
                behaviorValue = (int)TimerFinishBehavior.CountUp;
            }

            return (TimerFinishBehavior)behaviorValue;
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

            // Format based on duration
            if (secondsToShow >= 3600)
            {
                // Hours format (HH:MM:SS)
                int hours = secondsToShow / 3600;
                int minutes = (secondsToShow % 3600) / 60;
                int seconds = secondsToShow % 60;
                timeText = $"{hours:D1}:{minutes:D2}:{seconds:D2}";
            }
            else if (secondsToShow >= 60)
            {
                // Minutes format (MM:SS)
                int minutes = secondsToShow / 60;
                int seconds = secondsToShow % 60;
                timeText = $"{minutes:D1}:{seconds:D2}";
            }
            else
            {
                // Seconds only format (SS)
                timeText = $"{secondsToShow:D1}";
            }

            // Update the timer text
            timerText.Text = timeText;

            // Set text color to red if we're in overtime
            if (secondsLeft < 0)
            {
                timerText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
                intervalInfoText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
            }
            else
            {
                // Use a resource that properly adapts to theme changes
                var currentTheme = ((FrameworkElement)this.Content).ActualTheme;
                if (currentTheme == ElementTheme.Dark)
                {
                    // Use white text in dark theme
                    timerText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
                    intervalInfoText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
                }
                else
                {
                    // Use dark text in light theme
                    timerText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black);
                    intervalInfoText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black);
                }
            }

            // Update interval info if applicable
            if (intervals != null && intervalCount > 1 && secondsLeft >= 0)
            {
                intervalInfoText.Text = $"Interval {intervalNumber}/{intervalCount}";
                intervalInfoText.Visibility = Visibility.Visible;
            }
            else
            {
                intervalInfoText.Text = "";
                intervalInfoText.Visibility = Visibility.Collapsed;
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
            }
            catch (Exception ex)
            {
                // Log but don't rethrow as we're already closing
                Debug.WriteLine($"Error during window cleanup: {ex.Message}");
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

            localSettings.LastTimerWindowPosition = GetCurrentWindowInformation();
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
                    localSettings.LastTimerWindowPosition = GetCurrentWindowInformation();
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
            if (intervalsList.Count < MaxIntervals)
            {
                // Add new interval at the end of the list
                intervalsList.Add(new IntervalTimeViewModel(intervalsList.Count + 1));
                UpdateIntervalNumbers();
            }

            if (intervalsList.Count >= MaxIntervals)
            {
                addIntervalButton.IsEnabled = false;
            }
        }


        private void RemoveInterval_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var interval = button.DataContext as IntervalTimeViewModel;

            int removedIndex = intervalsList.IndexOf(interval);
            intervalsList.RemoveAt(removedIndex);

            // Update numbers for all remaining intervals
            UpdateIntervalNumbers();

            if (intervalsList.Count < MaxIntervals)
            {
                addIntervalButton.IsEnabled = true;
            }
        }

        private void UpdateIntervalNumbers()
        {
            // Intervals will run in the order they appear, so the numbers should match their position
            for (int i = 0; i < intervalsList.Count; i++)
            {
                intervalsList[i].IntervalNumber = i + 1;
                // Show remove button for all intervals except the first one
                intervalsList[i].ShowRemoveButton = i > 0;
            }
        }

        private async void StartTimer_FromCustomSelection()
        {
            intervals = new Queue<IntervalTime>();
            var savedConfigs = new List<SavedIntervalConfig>();

            Debug.WriteLine($"Starting timer from custom selection. Intervals count: {intervalsList.Count}");

            foreach (var intervalVM in intervalsList)
            {
                if (intervalVM.TotalSeconds > 0)
                {
                    Debug.WriteLine($"Adding interval: H:{intervalVM.Hours} M:{intervalVM.Minutes} S:{intervalVM.Seconds}");

                    // Add to intervals queue for timer functionality
                    intervals.Enqueue(new IntervalTime
                    {
                        Hours = intervalVM.Hours,
                        Minutes = intervalVM.Minutes,
                        Seconds = intervalVM.Seconds
                    });

                    // Save configuration for persisting across sessions
                    savedConfigs.Add(new SavedIntervalConfig
                    {
                        Hours = intervalVM.Hours,
                        Minutes = intervalVM.Minutes,
                        Seconds = intervalVM.Seconds
                    });
                }
            }

            // Save the interval configurations using the existing localSettings instance
            if (savedConfigs.Any())
            {
                Debug.WriteLine($"Saving {savedConfigs.Count} interval configurations");

                if (localSettings == null)
                {
                    Debug.WriteLine("LocalSettings is null, creating it now");
                    localSettings = await LocalSettings.GetSharedInstanceAsync();
                }

                // Determine whether this is an interval timer or custom timer
                // We can identify interval timers by checking if the addIntervalButton is visible
                bool isIntervalTimer = addIntervalButton != null && addIntervalButton.Visibility == Visibility.Visible;

                // Save to the appropriate configuration
                if (isIntervalTimer)
                {
                    localSettings.SaveIntervalConfigs(savedConfigs);
                    Debug.WriteLine("Saved interval timer configurations");
                }
                else
                {
                    localSettings.SaveCustomTimerConfigs(savedConfigs);
                    Debug.WriteLine("Saved custom timer configurations");
                }

                // Force save to ensure it's written to disk
                localSettings.SaveSettings();
                Debug.WriteLine("Saved configurations to settings");
            }

            intervalCount = intervals.Count;

            if (intervals.Count > 0)
            {
                StartNextInterval();
                timerGauge.Visibility = Visibility.Visible;
                timerText.Visibility = Visibility.Visible;
                timeSelector.Visibility = Visibility.Collapsed;
            }
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
        // Update the Grid_Tapped method for pause/resume with the simplified structure
        private void Grid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (timer == null) return;

            // If the sender is a text block with the word start, return
            if (e.OriginalSource is TextBlock textBlock && textBlock.Text == "Start")
            {
                return;
            }

            // Use the VisualTreeHelper to get the element that was tapped
            DependencyObject tappedElement = VisualTreeHelper.GetParent((DependencyObject)e.OriginalSource);

            // Check to see if the tapped Element is the start button
            if (tappedElement != null && tappedElement is Button)
            {
                return;
            }

            // Prevent pausing immediately after starting
            if (secondsLeft == currentIntervalTotal) return;

            // Pause the timer if running. If timer is paused, unpause it
            if (timer.IsEnabled)
            {
                timer.Stop();

                // Update to indicate pause state
                timerText.FontWeight = Microsoft.UI.Text.FontWeights.Thin;
                timerGauge.TrailBrush = new SolidColorBrush(Microsoft.UI.Colors.DarkGray);
            }
            else
            {
                timer.Start();

                // Restore normal font weight
                timerText.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;

                // Change the colour of the trailbrush back
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

    public class ScaleFactorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is double size && parameter is string factorString)
            {
                if (double.TryParse(factorString, out double factor))
                {
                    return size * factor;
                }
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}

