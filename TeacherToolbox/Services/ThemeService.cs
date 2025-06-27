
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace TeacherToolbox.Services
{
    /// <summary>
    /// Implementation of IThemeService using ThemeHelper
    /// </summary>
    public class ThemeService : IThemeService
    {
        public bool IsDarkTheme()
        {
            try
            {
                return TeacherToolbox.Helpers.ThemeHelper.IsDarkTheme();
            }
            catch
            {
                // Fallback to light theme if there's an error
                return false;
            }
        }

        public SolidColorBrush GetHandColorBrush()
        {
            try
            {
                return new SolidColorBrush(IsDarkTheme() ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Black);
            }
            catch
            {
                // Fallback to black brush
                return new SolidColorBrush(Microsoft.UI.Colors.Black);
            }
        }

        public ElementTheme CurrentTheme
        {
            get
            {
                try
                {
                    return TeacherToolbox.Helpers.ThemeHelper.RootTheme;
                }
                catch
                {
                    return ElementTheme.Default;
                }
            }
        }
    }
}