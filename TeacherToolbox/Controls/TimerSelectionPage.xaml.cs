using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System;
using System.Threading.Tasks;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace TeacherToolbox.Controls
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class TimerSelectionPage : AutomatedPage
    {
        public TimerSelectionPage()
        {
            this.InitializeComponent();
            timer30Button.Focus(FocusState.Programmatic);
            this.Loaded += TimerSelectionPage_Loaded;
        }

        private void TimerSelectionPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (!SettingsService.GetHasShownTimerOnboarding())
            {
                TimerOnboardingTip.IsOpen = true;
                SettingsService.SetHasShownTimerOnboarding(true);
            }
        }

        private void OpenTimer_Click(object sender, RoutedEventArgs e)
        {
            _ = OpenTimerAsync(sender);
        }

        private async Task OpenTimerAsync(object sender)
        {
            try
            {
                string tag = (sender as Button).Tag?.ToString();
                TimerWindow timerWindow = null;

                if (tag == "Custom")
                {
                    timerWindow = new TimerWindow(0, SettingsService, ThemeService);
                }
                else if (tag == "Interval")
                {
                    timerWindow = new TimerWindow(-1, SettingsService, ThemeService);
                }
                else
                {
                    int seconds = int.Parse(tag);
                    timerWindow = new TimerWindow(seconds, SettingsService, ThemeService);
                }

                // Ensure window is created before activating
                if (timerWindow != null)
                {
                    // Give the window time to initialize before activation
                    await Task.Delay(50);

                    try
                    {
                        timerWindow.Activate();
                    }
                    catch (Exception activateEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error activating window: {activateEx.Message}");
                        throw; // Rethrow to see the full error
                    }
                }
            }
            catch (FormatException ex)
            {
                // Log the exception
                System.Diagnostics.Debug.WriteLine($"Format error: {ex.Message}");
            }
            catch (Exception ex)
            {
                // Log any other exceptions
                System.Diagnostics.Debug.WriteLine($"General error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}
