using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using TeacherToolbox.Helpers;
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

        public ScreenRulerWindow(ScreenRulerPage fromPage,ulong displayId = 0)
        {
            var services = App.Current.Services;
            _settingsService = services.GetRequiredService<ISettingsService>();
            _themeService = services.GetRequiredService<IThemeService>();

            this.InitializeComponent();

            var appWindow = this.AppWindow;
            DisplayManager displayManager = new DisplayManager();
            DisplayArea displayArea = displayManager.GetDisplayArea(displayId == 0 ? displayManager.PrimaryDisplayId : displayId);

            // Directly use the work area width of the display area
            int windowWidth = displayArea.WorkArea.Width;
            int windowHeight = 100; // Set the height as needed

            // Calculate the window's X and Y position to center it within the display's work area
            int windowX = displayArea.WorkArea.X;
            int windowY = displayArea.WorkArea.Y + (displayArea.WorkArea.Height - windowHeight) / 2;

            // Resize and move the window to fit within the intended display's work area
            appWindow.MoveAndResize(new RectInt32(windowX, windowY, windowWidth, windowHeight));

            this.IsAlwaysOnTop = true;
            this.IsTitleBarVisible = false;
            this.IsResizable = false;
            this.SystemBackdrop = blurredBackdrop;

            dragHelper = new WindowDragHelper(this, _settingsService, true);

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

        private class BlurredBackdrop : CompositionBrushBackdrop
        {
            protected override Windows.UI.Composition.CompositionBrush CreateBrush(Windows.UI.Composition.Compositor compositor)
                => compositor.CreateHostBackdropBrush();
        }
    }
}
