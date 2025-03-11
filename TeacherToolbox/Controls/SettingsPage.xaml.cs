using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using TeacherToolbox.Helpers;
using TeacherToolbox.ViewModels;
using TeacherToolbox.Services;
using System.Threading.Tasks;

namespace TeacherToolbox.Controls
{
    public sealed partial class SettingsPage : AutomatedPage
    {
        // ViewModel property
        public SettingsViewModel ViewModel { get; }

        public SettingsPage() : base()
        {
            // Initialize ViewModel with the settings service
            ViewModel = CreateViewModel();

            // Initialize component
            this.InitializeComponent();

            // Set window and DataContext
            WindowHelper.SetWindowForElement(this, App.MainWindow);
            this.DataContext = ViewModel;

            // Subscribe to ThemeChanged event
            ViewModel.ThemeChanged += OnThemeChanged;
        }

        private SettingsViewModel CreateViewModel()
        {
            // Get the settings service (this will be replaced with proper DI in the future)
            var settingsService = LocalSettingsService.GetSharedInstanceSync();

            // Create and return the view model with the injected service
            return new SettingsViewModel(settingsService);
        }

        private void OnThemeChanged(ElementTheme theme)
        {
            // Update the root theme
            ThemeHelper.RootTheme = theme;

            // Update title bar
            var window = WindowHelper.GetWindowForElement(this);
            if (window != null)
            {
                TitleBarHelper.ApplySystemThemeToCaptionButtons(window);
            }
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new FrameworkElementAutomationPeer(this);
        }
    }
}