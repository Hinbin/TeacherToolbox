using System;

namespace TeacherToolbox.Services
{
    public interface IWindowService
    {
        IntPtr WindowHandle { get; }
        void SetWindow(Microsoft.UI.Xaml.Window window);
    }

    public class WindowService : IWindowService
    {
        private Microsoft.UI.Xaml.Window _window;

        public IntPtr WindowHandle => _window != null ? WinRT.Interop.WindowNative.GetWindowHandle(_window) : IntPtr.Zero;

        public void SetWindow(Microsoft.UI.Xaml.Window window)
        {
            _window = window;
        }
    }
}
