using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace TeacherToolbox.Controls
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Timer : Page
    {
        public Timer()
        {
            this.InitializeComponent();
            // Focus one of the buttons so that the user can use the keyboard to navigate
            thirtySecondButton.Focus(FocusState.Programmatic);
        }

        private void OpenTimer_Click(object sender, RoutedEventArgs e)
        {
            // Work out the number of seconds from the button content
            string buttonContent = (sender as Button).Content.ToString();
            // If it is a custom timer, open the timerwindow with a value of 0
            if (buttonContent.Contains("Custom") )
            {
                TimerWindow timerWindow = new(0);
                timerWindow.Activate();
                return;
            }

            // Check for format exception and log it
            try
            {
                // Parse "30 seconds" to 30, "1 minute" to 60, "2 minutes" to 120, etc.    
                int seconds = int.Parse(buttonContent.Split(' ')[0]) * (buttonContent.Contains("minute") ? 60 : 1);

                //Open the timer window, send the name of the button that was clicked
                TimerWindow timerWindow = new(seconds);
                timerWindow.Activate();
            }
            catch (FormatException ex)
            {
                // Log the exception
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }

        }    
    }
}
