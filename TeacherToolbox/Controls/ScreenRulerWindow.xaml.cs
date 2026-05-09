using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using System;
using TeacherToolbox.Helpers;
using TeacherToolbox.Model;
using TeacherToolbox.Services;
using Windows.Graphics;
using WinUIEx;


// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace TeacherToolbox.Controls
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ScreenRulerWindow : WindowEx
    {

        // Services
        private readonly IThemeService _themeService;
        private readonly ISettingsService _settingsService;


        private WindowDragHelper dragHelper;
        private static BlurredBackdrop blurredBackdrop = new BlurredBackdrop();
        private ScreenRulerPage screenRulerPage;
        private bool isResizing;
        private int resizeStartY;
        private int resizeStartHeight;
        private const int DefaultRulerHeight = 100;
        private const int MinimumRulerHeight = 60;

        public ScreenRulerWindow(ScreenRulerPage fromPage, ISettingsService settingsService, IThemeService themeService, ulong displayId = 0)
        {
            _settingsService = settingsService;
            _themeService = themeService;

            this.InitializeComponent();

            var appWindow = this.AppWindow;
            DisplayManager displayManager = new DisplayManager();
            DisplayArea displayArea = displayManager.GetDisplayArea(displayId == 0 ? displayManager.PrimaryDisplayId : displayId);
            WindowPosition savedPosition = _settingsService.GetLastScreenRulerWindowPosition();

            // Directly use the work area width of the display area
            int windowWidth = displayArea.WorkArea.Width;
            int windowHeight = GetInitialWindowHeight(savedPosition, displayArea.WorkArea.Height);

            // Calculate the window's X and Y position to center it within the display's work area
            int windowX = displayArea.WorkArea.X;
            int windowY = GetInitialWindowY(savedPosition, displayArea.WorkArea, windowHeight);

            // Resize and move the window to fit within the intended display's work area
            appWindow.MoveAndResize(new RectInt32(windowX, windowY, windowWidth, windowHeight));

            this.IsAlwaysOnTop = true;
            this.IsTitleBarVisible = false;
            this.IsResizable = false;
            this.SystemBackdrop = blurredBackdrop;

            dragHelper = new WindowDragHelper(this, _settingsService, WindowType.ScreenRulerWindow, true);

            screenRulerPage = fromPage;

            this.Show();
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

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Check to see if fromPage is still open
            if (screenRulerPage != null)
            {
                screenRulerPage.Close_Ruler_Window_Click(sender, e);
            }

            this.Close();
        }

        private static int GetInitialWindowHeight(WindowPosition savedPosition, int workAreaHeight)
        {
            if (savedPosition.Height > MinimumRulerHeight)
            {
                return Math.Clamp((int)Math.Round(savedPosition.Height), MinimumRulerHeight, workAreaHeight);
            }

            return Math.Clamp(DefaultRulerHeight, MinimumRulerHeight, workAreaHeight);
        }

        private static int GetInitialWindowY(WindowPosition savedPosition, RectInt32 workArea, int windowHeight)
        {
            int centredY = workArea.Y + (workArea.Height - windowHeight) / 2;

            if (savedPosition.DisplayID != 0 && savedPosition.Height > 0)
            {
                return Math.Clamp(savedPosition.Y, workArea.Y, workArea.Y + workArea.Height - windowHeight);
            }

            return centredY;
        }

        private void ResizeHandle_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var properties = e.GetCurrentPoint(resizeHandle).Properties;
            if (!properties.IsLeftButtonPressed)
            {
                return;
            }

            resizeHandle.CapturePointer(e.Pointer);
            WindowDragHelper.GetCursorPos(out PointInt32 cursorPosition);
            resizeStartY = cursorPosition.Y;
            resizeStartHeight = AppWindow.Size.Height;
            isResizing = true;
            e.Handled = true;
        }

        private void ResizeHandle_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!isResizing)
            {
                return;
            }

            var properties = e.GetCurrentPoint(resizeHandle).Properties;
            if (!properties.IsLeftButtonPressed)
            {
                EndResize();
                e.Handled = true;
                return;
            }

            WindowDragHelper.GetCursorPos(out PointInt32 cursorPosition);
            int requestedHeight = resizeStartHeight + (cursorPosition.Y - resizeStartY);
            ResizeToHeight(requestedHeight);
            e.Handled = true;
        }

        private void ResizeHandle_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            EndResize();
            e.Handled = true;
        }

        private void EndResize()
        {
            if (!isResizing)
            {
                return;
            }

            resizeHandle.ReleasePointerCaptures();
            isResizing = false;
            SaveCurrentWindowPosition();
        }

        private void ResizeToHeight(int requestedHeight)
        {
            RectInt32 workArea = DisplayArea.GetFromWindowId(
                AppWindow.Id,
                DisplayAreaFallback.Primary).WorkArea;

            int maxHeight = Math.Max(MinimumRulerHeight, workArea.Y + workArea.Height - AppWindow.Position.Y);
            int height = Math.Clamp(requestedHeight, MinimumRulerHeight, maxHeight);

            AppWindow.Resize(new SizeInt32(AppWindow.Size.Width, height));
        }

        private void SaveCurrentWindowPosition()
        {
            if (_settingsService == null)
            {
                return;
            }

            var displayId = DisplayArea.GetFromWindowId(
                AppWindow.Id,
                DisplayAreaFallback.Primary).DisplayId;

            _settingsService.SetLastScreenRulerWindowPosition(new WindowPosition(
                AppWindow.Position.X,
                AppWindow.Position.Y,
                AppWindow.Size.Width,
                AppWindow.Size.Height,
                displayId.Value));
        }

        private class BlurredBackdrop : CompositionBrushBackdrop
        {
            protected override Windows.UI.Composition.CompositionBrush CreateBrush(Windows.UI.Composition.Compositor compositor)
                => compositor.CreateHostBackdropBrush();
        }
    }
}
