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
            // Focus one of the buttons so that the user can use the keyboard to navigate
            timer30Button.Focus(FocusState.Programmatic);
        }

        private async void OpenTimer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Work out the number of seconds from the button content
                string buttonContent = (sender as Button).Content.ToString();
                TimerWindow timerWindow = null;

                // Create window based on button type
                if (buttonContent.Contains("Custom"))
                {
                    timerWindow = new TimerWindow(0);
                }
                else if (buttonContent.Contains("Interval"))
                {
                    timerWindow = new TimerWindow(-1);
                }
                else
                {
                    // Parse "30 seconds" to 30, "1 minute" to 60, "2 minutes" to 120, etc.    
                    int seconds = int.Parse(buttonContent.Split(' ')[0]) * (buttonContent.Contains("min") ? 60 : 1);
                    timerWindow = new TimerWindow(seconds);
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
