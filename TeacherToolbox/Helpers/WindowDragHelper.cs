using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;
using System;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TeacherToolbox.Services;
using TeacherToolbox.Model;

namespace TeacherToolbox.Helpers
{
    /// <summary>
    /// Helper class for drag-based window movement
    /// </summary>
    public class WindowDragHelper
    {
        private int nX = 0, nY = 0, nXWindow = 0, nYWindow = 0;
        private bool bMoving = false;
        private Microsoft.UI.Windowing.AppWindow _apw;
        private bool isClicked = false;
        private bool onlyVertical = false;
        private readonly ISettingsService _settingsService; // Updated to use interface
        private readonly bool _useMainWindowPosition; // Whether to use main or timer window position

        [DllImport("User32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool GetCursorPos(out Windows.Graphics.PointInt32 lpPoint);

        /// <summary>
        /// Creates a new window drag helper using the settings service
        /// </summary>
        /// <param name="window">The window to enable dragging on</param>
        /// <param name="settingsService">The settings service for persisting window position</param>
        /// <param name="isTimerWindow">Whether this is a timer window (uses different position storage)</param>
        /// <param name="onlyAllowVertical">Whether to only allow vertical dragging</param>
        public WindowDragHelper(
            Window window,
            ISettingsService settingsService,
            bool isTimerWindow = false,
            bool onlyAllowVertical = false)
        {
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            Microsoft.UI.WindowId myWndId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            _apw = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(myWndId);
            onlyVertical = onlyAllowVertical;
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _useMainWindowPosition = !isTimerWindow;
        }

        /// <summary>
        /// Call when navigating to reset state
        /// </summary>
        public void OnNavigate()
        {
            isClicked = false;
            bMoving = false;
        }

        /// <summary>
        /// Handle pointer released event to save window position
        /// </summary>
        public void PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            ((UIElement)sender).ReleasePointerCaptures();
            bMoving = false;

            // Save the window position to settings
            if (_settingsService != null && isClicked)
            {
                var position = _apw.Position;
                var size = _apw.Size;
                var displayId = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(
                    _apw.Id,
                    Microsoft.UI.Windowing.DisplayAreaFallback.Primary).DisplayId;

                // Create window position
                var windowPosition = new WindowPosition(
                    position.X,
                    position.Y,
                    size.Width,
                    size.Height,
                    displayId.Value
                );

                // Save either to main window or timer window position
                if (_useMainWindowPosition)
                {
                    _settingsService.SetLastWindowPosition(windowPosition);
                }
                else
                {
                    _settingsService.SetLastTimerWindowPosition(windowPosition);
                }
            }

            isClicked = false;
        }

        /// <summary>
        /// Handle pointer pressed event to initiate drag
        /// </summary>
        public void PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            isClicked = true;
            var properties = e.GetCurrentPoint((UIElement)sender).Properties;

            if (properties.IsLeftButtonPressed)
            {
                // Check if the event source or any of its parents is a Button
                if (IsOrIsChildOfButton(e.OriginalSource as DependencyObject))
                {
                    // If true, do not initiate drag
                    return;
                }

                ((UIElement)sender).CapturePointer(e.Pointer);
                nXWindow = _apw.Position.X;
                nYWindow = _apw.Position.Y;
                Windows.Graphics.PointInt32 pt;
                GetCursorPos(out pt);
                nX = pt.X;
                nY = pt.Y;
                bMoving = true;
            }
        }

        /// <summary>
        /// Helper method to check if the source is a Button or is contained within a Button
        /// </summary>
        private bool IsOrIsChildOfButton(DependencyObject source)
        {
            while (source != null && !(source is Button))
            {
                source = VisualTreeHelper.GetParent(source);
            }
            return source is Button;
        }

        /// <summary>
        /// Handle pointer moved event to move window
        /// </summary>
        public void PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!isClicked) return;

            var properties = e.GetCurrentPoint((UIElement)sender).Properties;
            if (properties.IsLeftButtonPressed)
            {
                Windows.Graphics.PointInt32 pt;
                GetCursorPos(out pt);

                if (bMoving)
                {
                    // If vertical only, only move the Y axis
                    if (onlyVertical)
                    {
                        _apw.Move(new Windows.Graphics.PointInt32(nXWindow, nYWindow + (pt.Y - nY)));
                    }
                    else
                    {
                        _apw.Move(new Windows.Graphics.PointInt32(nXWindow + (pt.X - nX), nYWindow + (pt.Y - nY)));
                    }
                }
                e.Handled = true;
            }
        }
    }
}