using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;
using System;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

public class WindowDragHelper
{
    private int nX = 0, nY = 0, nXWindow = 0, nYWindow = 0;
    private bool bMoving = false;
    private Microsoft.UI.Windowing.AppWindow _apw;
    private bool isClicked = false;
    private bool onlyVertical = false;
    private TeacherToolbox.Model.LocalSettings _settings; // Add this field

    [DllImport("User32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern bool GetCursorPos(out Windows.Graphics.PointInt32 lpPoint);

    // Update constructor to receive settings
    public WindowDragHelper(Window window, TeacherToolbox.Model.LocalSettings settings, bool onlyAllowVertical = false)
    {
        IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        Microsoft.UI.WindowId myWndId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
        _apw = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(myWndId);
        onlyVertical = onlyAllowVertical;
        _settings = settings; // Store settings reference
    }

    public void OnNavigate()
    {
        isClicked = false;
        bMoving = false;
    }

    // Update PointerReleased to save position
    public void PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        ((UIElement)sender).ReleasePointerCaptures();
        bMoving = false;

        // Save the window position to settings
        if (_settings != null && isClicked)
        {
            var position = _apw.Position;
            var size = _apw.Size;

            _settings.LastWindowPosition = new TeacherToolbox.Model.WindowPosition(
                position.X,
                position.Y,
                size.Width,
                size.Height,
                0 // DisplayID is likely not needed for this, but set as needed
            );
        }

        isClicked = false;
    }

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

    // Helper method to check if the source is a Button or is contained within a Button
    private bool IsOrIsChildOfButton(DependencyObject source)
    {
        while (source != null && !(source is Button))
        {
            source = VisualTreeHelper.GetParent(source);
        }
        return source is Button;
    }


    public void PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!isClicked) return;

        var properties = e.GetCurrentPoint((UIElement)sender).Properties;
        if (properties.IsLeftButtonPressed)
        {
            Windows.Graphics.PointInt32 pt;
            GetCursorPos(out pt);

            if (bMoving)
                // If vertical only, only move they Y axis
                if (onlyVertical)
                {
                    _apw.Move(new Windows.Graphics.PointInt32(nXWindow, nYWindow + (pt.Y - nY)));
                }
                else
                {
                    _apw.Move(new Windows.Graphics.PointInt32(nXWindow + (pt.X - nX), nYWindow + (pt.Y - nY)));
                }
            e.Handled = true;
        }
    }
}
