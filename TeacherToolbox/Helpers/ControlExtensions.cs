using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using System.Linq;

namespace TeacherToolbox.Helpers
{
    public static class ControlExtensions
    {
        public static IEnumerable<T> Descendants<T>(this DependencyObject element) where T : DependencyObject
        {
            if (element == null)
                yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                var child = VisualTreeHelper.GetChild(element, i);

                if (child is T childAsT)
                    yield return childAsT;

                foreach (var descendant in Descendants<T>(child))
                    yield return descendant;
            }
        }
    }
}