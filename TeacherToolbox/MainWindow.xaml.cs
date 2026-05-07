using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Diagnostics;
using System.Linq;
using TeacherToolbox.Controls;
using TeacherToolbox.Helpers;
using TeacherToolbox.Services;
using Windows.UI;
using WinUIEx;



// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.


namespace TeacherToolbox
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow
    {
        // Services
        private readonly ISleepPreventer _sleepPreventer;
        private readonly ISettingsService _settingsService;
        private readonly IThemeService _themeService;
        private readonly ITelemetryService _telemetry;
        private readonly IShortcutWatcherService _shortcutWatcher;

        private readonly OverlappedPresenter _presenter;
        private WindowDragHelper dragHelper;
        private void ContentFrame_NavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        public MainWindow(
            ISettingsService settingsService,
            ISleepPreventer sleepPreventer,
            IThemeService themeService,
            ITelemetryService telemetry,
            IShortcutWatcherService shortcutWatcher)
        {
            this.InitializeComponent();

            _settingsService = settingsService;
            _sleepPreventer = sleepPreventer;
            _themeService = themeService;
            _telemetry = telemetry;
            _shortcutWatcher = shortcutWatcher;

            _presenter = this.AppWindow.Presenter as OverlappedPresenter;

            // Restore saved window position or use default size
            RestoreWindowPosition();

            this.ExtendsContentIntoTitleBar = true;
            UpdateTitleBarTheme();
            SetRegionsForCustomTitleBar(); // To allow the nav button to be selectable, but the rest of the title bar to function as normal

            NavView.IsPaneOpen = false;

            this.SetIsAlwaysOnTop(true);

            dragHelper = new WindowDragHelper(this, _settingsService);

            _shortcutWatcher.ShortcutPressed += OnShortcutPressed;

            try
            {
                _ = _shortcutWatcher.StartAsync();
            }
            catch (Exception e)
            {
                _telemetry.LogWarning("Failed to start ShortcutWatcher from MainWindow ctor", e);
            }

            this.Closed += MainWindow_Closed;
            this.SizeChanged += MainWindow_SizeChanged;
            this.AppWindow.Changed += AppWindow_Changed;
        }

        private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            // When the window moves (e.g., dragged to a different monitor),
            // we need to recalculate the title bar regions for the new DPI
            if (args.DidPositionChange)
            {
                SetRegionsForCustomTitleBar();
            }
        }

        private void MainWindow_SizeChanged(object sender, WindowSizeChangedEventArgs e)
        {
            SetRegionsForCustomTitleBar();
        }

        private void RestoreWindowPosition()
        {
            try
            {
                var savedPosition = _settingsService.GetLastWindowPosition();

                if (!savedPosition.IsEmpty)
                {
                    // Verify the saved display still exists and the position is valid
                    bool positionIsValid = false;
                    double targetScaleFactor = 1.0;

                    try
                    {
                        // Get all display areas and check if the saved position is within any of them
                        var displayAreas = Microsoft.UI.Windowing.DisplayArea.FindAll();

                        foreach (var display in displayAreas)
                        {
                            if (savedPosition.DisplayID != 0 &&
                                display.DisplayId.Value != savedPosition.DisplayID)
                            {
                                continue;
                            }

                            var workArea = display.WorkArea;

                            // Check if the saved position's top-left corner is within this display's work area
                            // Allow some tolerance for windows that might be partially off-screen
                            if (savedPosition.X >= workArea.X - 100 &&
                                savedPosition.X < workArea.X + workArea.Width &&
                                savedPosition.Y >= workArea.Y - 100 &&
                                savedPosition.Y < workArea.Y + workArea.Height)
                            {
                                positionIsValid = true;

                                // Get the DPI scale factor for this display
                                // We calculate it from the ratio of outer bounds to work area
                                // or use a more direct method if available
                                try
                                {
                                    // Get DPI for target monitor using its bounds
                                    uint dpiX = 96, dpiY = 96;
                                    var monitorHandle = MonitorFromPoint(
                                        new POINT { x = savedPosition.X, y = savedPosition.Y },
                                        MONITOR_DEFAULTTONEAREST);

                                    if (monitorHandle != IntPtr.Zero)
                                    {
                                        GetDpiForMonitor(monitorHandle, MDT_EFFECTIVE_DPI, out dpiX, out dpiY);
                                        targetScaleFactor = dpiX / 96.0;
                                        Debug.WriteLine($"Target monitor DPI: {dpiX}, scale factor: {targetScaleFactor}");
                                    }
                                }
                                catch (Exception dpiEx)
                                {
                                    _telemetry.LogWarning("Could not get DPI for target monitor", dpiEx);
                                    // Fall back to current window's scale factor
                                    if (Content?.XamlRoot != null)
                                    {
                                        targetScaleFactor = Content.XamlRoot.RasterizationScale;
                                    }
                                }

                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _telemetry.LogWarning("Error checking display areas during window position restoration", ex);
                        positionIsValid = IsPositionOnMonitor(savedPosition.X, savedPosition.Y);
                    }

                    if (positionIsValid)
                    {
                        // Convert saved DIP size to physical pixels for the target monitor
                        int physicalWidth = (int)(savedPosition.Width * targetScaleFactor);
                        int physicalHeight = (int)(savedPosition.Height * targetScaleFactor);

                        // Ensure minimum size
                        physicalWidth = Math.Max(physicalWidth, (int)(400 * targetScaleFactor));
                        physicalHeight = Math.Max(physicalHeight, (int)(150 * targetScaleFactor));

                        // Restore position and size
                        this.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(
                            savedPosition.X,
                            savedPosition.Y,
                            physicalWidth,
                            physicalHeight
                        ));

                        Debug.WriteLine($"Restored window position: {savedPosition.X}, {savedPosition.Y}, {physicalWidth}x{physicalHeight} physical pixels (from {savedPosition.Width}x{savedPosition.Height} DIPs, scale: {targetScaleFactor})");
                    }
                    else
                    {
                        // Saved position is not valid (display may have been disconnected), use default
                        Debug.WriteLine("Saved window position is not on any current display, using default");
                        ApplyDefaultWindowBounds();
                    }
                }
                else
                {
                    // No saved position, use default size
                    ApplyDefaultWindowBounds();
                }
            }
            catch (Exception ex)
            {
                _telemetry.LogError("Error restoring window position", ex);
                // Fall back to default size
                ApplyDefaultWindowBounds();
            }
        }

        private void ApplyDefaultWindowBounds()
        {
            var workArea = DisplayArea.Primary.WorkArea;
            const int defaultWidth = 700;
            const int defaultHeight = 220;

            var x = workArea.X + Math.Max(0, (workArea.Width - defaultWidth) / 2);
            var y = workArea.Y + Math.Max(0, (workArea.Height - defaultHeight) / 2);

            this.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(
                x,
                y,
                defaultWidth,
                defaultHeight));
        }

        #region DPI Helper P/Invoke

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [System.Runtime.InteropServices.DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const uint MONITOR_DEFAULTTONULL = 0;
        private const int MDT_EFFECTIVE_DPI = 0;

        private static bool IsPositionOnMonitor(int x, int y)
        {
            return MonitorFromPoint(new POINT { x = x, y = y }, MONITOR_DEFAULTTONULL) != IntPtr.Zero;
        }

        #endregion

        private void SaveWindowPosition()
        {
            try
            {
                var position = this.AppWindow.Position;
                var size = this.AppWindow.Size;
                var displayId = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(
                    this.AppWindow.Id,
                    Microsoft.UI.Windowing.DisplayAreaFallback.Primary).DisplayId;

                // Get the current DPI scale factor to convert physical pixels to DIPs
                // This ensures the size is stored in a DPI-independent way
                double scaleFactor = 1.0;
                if (Content?.XamlRoot != null)
                {
                    scaleFactor = Content.XamlRoot.RasterizationScale;
                }

                // Store size in DIPs (device-independent pixels) for cross-DPI compatibility
                var windowPosition = new Model.WindowPosition(
                    position.X,
                    position.Y,
                    size.Width / scaleFactor,  // Convert to DIPs
                    size.Height / scaleFactor, // Convert to DIPs
                    displayId.Value
                );

                _settingsService.SetLastWindowPosition(windowPosition);
                Debug.WriteLine($"Saved window position: {position.X}, {position.Y}, {size.Width / scaleFactor}x{size.Height / scaleFactor} DIPs (scale: {scaleFactor})");
            }
            catch (Exception ex)
            {
                _telemetry.LogError("Error saving window position", ex);
            }
        }

        private void MainWindow_Closed(object sender, WindowEventArgs e)
        {
            try
            {
                // Save window position before closing
                SaveWindowPosition();

                _shortcutWatcher.ShortcutPressed -= OnShortcutPressed;
                _shortcutWatcher.StopAsync().GetAwaiter().GetResult();
                _sleepPreventer?.Dispose();
            }
            catch (Exception ex)
            {
                _telemetry.LogError("Error during MainWindow cleanup", ex);
            }
        }

        private void NavView_Loaded(object sender, RoutedEventArgs e)
        {
            // Add handler for ContentFrame navigation.
            ContentFrame.Navigated += On_Navigated;

            // NavView doesn't load any page by default, so load home page.
            NavView.SelectedItem = NavView.MenuItems[0];
            // If navigation occurs on SelectionChanged, this isn't needed.
            // Because we use ItemInvoked to navigate, we need to call Navigate
            // here to load the home page.
            NavView_Navigate(typeof(RandomNameGeneratorPage), new EntranceNavigationTransitionInfo());
            NavView.Header = null;
        }

        private void NavView_Navigate(Type navPageType, NavigationTransitionInfo transitionInfo)
        {
            Type preNavPageType = ContentFrame.CurrentSourcePageType;

            if (navPageType is not null && !Type.Equals(preNavPageType, navPageType))
            {
                // Don't handle Clock navigation here as it's handled in NavView_ItemInvoked
                if (navPageType != typeof(Clock))
                {
                    ContentFrame.Navigate(navPageType, null, transitionInfo);
                }
            }
            else if (navPageType == typeof(ScreenRulerPage) && ContentFrame.Content is ScreenRulerPage screenRulerPage)
            {
                screenRulerPage.EnsureRulerWindowOpen();
            }
        }

        private void On_Navigated(object sender, NavigationEventArgs e)
        {
            Debug.WriteLine($"On_Navigated called for {e.SourcePageType.Name} with parameter: {e.Parameter}");
            NavView.IsBackEnabled = ContentFrame.CanGoBack;

            if (ContentFrame.Content is AutomatedPage page)
            {
                string className = page.GetType().Name;
                AutomationProperties.SetAutomationId(page, className);
                var peer = FrameworkElementAutomationPeer.FromElement(page);
                if (peer == null)
                {
                    peer = new FrameworkElementAutomationPeer(page);
                }
            }

            if (ContentFrame.SourcePageType == typeof(SettingsPage))
            {
                NavView.SelectedItem = NavView.SettingsItem;
            }
            else if (ContentFrame.SourcePageType != null)
            {
                NavView.SelectedItem = NavView.MenuItems
                            .OfType<NavigationViewItem>()
                            .First(i => i.Tag.Equals(ContentFrame.SourcePageType.FullName.ToString()));
            }

            dragHelper.OnNavigate();

            // Only enable always on top for the RNG page
            if (ContentFrame.SourcePageType == typeof(RandomNameGeneratorPage))
            {
                this.SetIsAlwaysOnTop(true);
            }
            else
            {
                this.SetIsAlwaysOnTop(false);
            }
        }

        private void NavView_BackRequested(NavigationView sender,
                                   NavigationViewBackRequestedEventArgs args)
        {
            TryGoBack();
        }

        private bool TryGoBack()
        {
            if (!ContentFrame.CanGoBack)
                return false;

            // Don't go back if the nav pane is overlayed.
            if (NavView.IsPaneOpen &&
                (NavView.DisplayMode == NavigationViewDisplayMode.Compact ||
                 NavView.DisplayMode == NavigationViewDisplayMode.Minimal))
                return false;

            ContentFrame.GoBack();
            return true;
        }

        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked)
            {
                NavView_Navigate(typeof(SettingsPage), args.RecommendedNavigationTransitionInfo);
            }
            else if (args.InvokedItemContainer != null)
            {
                Type navPageType = Type.GetType(args.InvokedItemContainer.Tag.ToString());

                if (navPageType == typeof(Clock))
                {
                    ContentFrame.Navigate(navPageType, _sleepPreventer, args.RecommendedNavigationTransitionInfo);
                }
                else
                {
                    NavView_Navigate(navPageType, args.RecommendedNavigationTransitionInfo);
                }
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

        private void SetRegionsForCustomTitleBar()
        {
            try
            {
                InputNonClientPointerSource nonClientInputSrc =
                    InputNonClientPointerSource.GetForWindowId(this.AppWindow.Id);

                // Get the scale factor for proper DPI handling
                double scaleFactor = 1.0;
                if (Content?.XamlRoot != null)
                {
                    scaleFactor = Content.XamlRoot.RasterizationScale;
                }

                // Get the title bar height (already in physical pixels)
                int titleBarHeight = this.AppWindow.TitleBar.Height;
                if (titleBarHeight == 0)
                {
                    // Fallback to typical title bar height (32 DIPs) scaled for DPI
                    titleBarHeight = (int)(32 * scaleFactor);
                }

                // Get window dimensions (in physical pixels)
                var windowSize = this.AppWindow.Size;

                // Set empty caption regions to disable title bar dragging
                nonClientInputSrc.SetRegionRects(NonClientRegionKind.Caption,
                    new Windows.Graphics.RectInt32[] { });

                // Calculate the window control buttons area
                // Base width is ~138px at 100% scale (46px per button x 3 buttons), add margin for safety
                int controlButtonsWidth = (int)(150 * scaleFactor);
                int contentAreaWidth = windowSize.Width - controlButtonsWidth;

                // Only set passthrough for the content area, excluding the window control buttons
                if (contentAreaWidth > 0)
                {
                    var passthroughRect = new Windows.Graphics.RectInt32
                    {
                        X = 0,
                        Y = 0,
                        Width = contentAreaWidth,
                        Height = titleBarHeight
                    };

                    nonClientInputSrc.SetRegionRects(NonClientRegionKind.Passthrough,
                        new Windows.Graphics.RectInt32[] { passthroughRect });

                    Debug.WriteLine($"Title bar regions set - Passthrough area: {passthroughRect.Width}x{passthroughRect.Height}, Scale: {scaleFactor}, Control buttons preserved");
                }
                else
                {
                    // Window too narrow, don't set passthrough to avoid breaking control buttons
                    nonClientInputSrc.SetRegionRects(NonClientRegionKind.Passthrough,
                        new Windows.Graphics.RectInt32[] { });

                    Debug.WriteLine("Window too narrow, no passthrough regions set");
                }
            }
            catch (Exception ex)
            {
                _telemetry.LogWarning("Error setting title bar regions", ex);
            }
        }

        public void UpdateTitleBarTheme()
        {
            try
            {                
                _themeService?.UpdateTitleBarTheme(this);
            }
            catch (Exception ex)
            {
                _telemetry.LogWarning("Error updating title bar theme", ex);
            }
        }

        private void OnShortcutPressed(object sender, ShortcutPressedEventArgs e)
        {
            try
            {
                if (e.Kind == ShortcutKind.Timer)
                {
                    TimerWindow timerWindow;

                    if (e.Number == 0)
                    {
                        timerWindow = new TimerWindow(30, _settingsService, _themeService);
                    }
                    else if (e.Number == 9)
                    {
                        timerWindow = new TimerWindow(0, _settingsService, _themeService);
                    }
                    else
                    {
                        timerWindow = new TimerWindow(e.Number * 60, _settingsService, _themeService);
                    }

                    timerWindow.Activate();
                }
                else if (e.Kind == ShortcutKind.RandomName)
                {
                    this.Activate();

                    if (ContentFrame.SourcePageType != typeof(RandomNameGeneratorPage))
                    {
                        NavView.SelectedItem = NavView.MenuItems[1];
                        NavView_Navigate(typeof(RandomNameGeneratorPage), new EntranceNavigationTransitionInfo());
                    }

                    if (ContentFrame.Content is RandomNameGeneratorPage randomNameGenerator)
                    {
                        randomNameGenerator.GenerateName();
                    }
                }
            }
            catch (Exception ex)
            {
                _telemetry.LogError($"Error processing shortcut {e.RawMessage}", ex);
            }
        }
    }
}
