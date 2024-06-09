using Microsoft.UI.Xaml.Controls;

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
        }

        private void OpenTimer_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            //Open the timer window, send the name of the button that was clicked
            TimerWindow timerWindow = new();
            timerWindow.Activate();
        }
    }
}
