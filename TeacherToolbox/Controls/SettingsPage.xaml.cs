using Microsoft.UI.Xaml;
using TeacherToolbox.Helpers;
using TeacherToolbox.ViewModels;

namespace TeacherToolbox.Controls
{
    public sealed partial class SettingsPage : AutomatedPage
    {
        public SettingsViewModel ViewModel => (SettingsViewModel)this.DataContext;
        public string AppVersion => $"Version {VersionHelper.GetAppVersion()}";

        public SettingsPage() : base()
        {
            // Initialize component
            this.InitializeComponent();

            // Set window and DataContext
            WindowHelper.SetWindowForElement(this, App.MainWindow);

            // Subscribe to ThemeChanged event
            ViewModel.ThemeChanged += OnThemeChanged;
        }

        private void OnThemeChanged(int themeIndex)
        {
            if (ThemeService != null)
            {
                ThemeService.CurrentTheme = themeIndex switch
                {
                    1 => ElementTheme.Light,
                    2 => ElementTheme.Dark,
                    _ => ElementTheme.Default
                };
            }
        }
    }
}