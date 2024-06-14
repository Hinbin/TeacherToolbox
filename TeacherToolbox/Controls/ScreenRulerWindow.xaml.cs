using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
using WinUIEx;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace TeacherToolbox.Controls
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ScreenRulerWindow : WindowEx
    {
        private WindowDragHelper dragHelper;
        private static BlurredBackdrop blurredBackdrop = new BlurredBackdrop();

        public ScreenRulerWindow()
        {
            this.InitializeComponent();

            // Get the AppWindow for the current window
            var appWindow = this.AppWindow;

            // Get the primary display area
            var primaryDisplayArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);

            // Get the work area size of the primary display area
            var workArea = primaryDisplayArea.WorkArea;

            // Set the window size to match the work area width of the primary display
            appWindow.Resize(new Windows.Graphics.SizeInt32((int)workArea.Width, appWindow.Size.Height));

            // Show the window, always on top
            this.IsAlwaysOnTop = true;
            this.CenterOnScreen();
            this.Height = 100;
            this.IsTitleBarVisible = false;
            this.IsResizable = false;
            this.SystemBackdrop = blurredBackdrop;

            dragHelper = new WindowDragHelper(this, true);

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
            this.Close();
        }

        private class BlurredBackdrop : CompositionBrushBackdrop
        {
            protected override Windows.UI.Composition.CompositionBrush CreateBrush(Windows.UI.Composition.Compositor compositor)
                => compositor.CreateHostBackdropBrush();
        }
    }
}
