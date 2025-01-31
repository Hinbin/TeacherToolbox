// File: Helpers/WindowHelper.cs
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;

namespace TeacherToolbox.Helpers
{
    public static class WindowHelper
    {
        private static Dictionary<int, WeakReference<Window>> _windowCache
            = new Dictionary<int, WeakReference<Window>>();

        public static void SetWindowForElement(UIElement element, Window window)
        {
            if (element == null) return;
            var elementId = element.GetHashCode();
            _windowCache[elementId] = new WeakReference<Window>(window);
        }

        public static Window GetWindowForElement(UIElement element)
        {
            if (element == null) return null;

            var elementId = element.GetHashCode();
            if (_windowCache.TryGetValue(elementId, out var windowRef) &&
                windowRef.TryGetTarget(out var window))
            {
                return window;
            }

            var parent = VisualTreeHelper.GetParent(element) as UIElement;
            while (parent != null)
            {
                var parentId = parent.GetHashCode();
                if (_windowCache.TryGetValue(parentId, out var parentWindowRef) &&
                    parentWindowRef.TryGetTarget(out var parentWindow))
                {
                    SetWindowForElement(element, parentWindow);
                    return parentWindow;
                }
                parent = VisualTreeHelper.GetParent(parent) as UIElement;
            }

            return null;
        }
    }
}