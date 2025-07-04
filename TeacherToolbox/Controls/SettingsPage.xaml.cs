using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using TeacherToolbox.Helpers;
using TeacherToolbox.Services;
using TeacherToolbox.ViewModels;

namespace TeacherToolbox.Controls
{
    public sealed partial class SettingsPage : AutomatedPage
    {
        private readonly IThemeService _themeService;
        public SettingsViewModel ViewModel { get; }

        public SettingsPage() : base()
        {
            // Get services
            var settingsService = LocalSettingsService.GetSharedInstanceSync();
            _themeService = App.Current.Services?.GetService<IThemeService>();

            // Initialize ViewModel
            ViewModel = new SettingsViewModel(settingsService);

            // Initialize component
            this.InitializeComponent();

            // Set window and DataContext
            WindowHelper.SetWindowForElement(this, App.MainWindow);
            this.DataContext = ViewModel;

            // Subscribe to ThemeChanged event
            ViewModel.ThemeChanged += OnThemeChanged;
        }

        private void OnThemeChanged(ElementTheme theme)
        {
            if (_themeService != null)
            {
                // Update the theme service
                _themeService.CurrentTheme = theme;
            }
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new FrameworkElementAutomationPeer(this);
        }
    }
}