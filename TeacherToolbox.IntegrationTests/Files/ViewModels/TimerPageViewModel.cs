using System;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TeacherToolbox.Services;

namespace TeacherToolbox.ViewModels
{
    public class TimerPageViewModel : ObservableObject
    {
        private readonly ISettingsService _settingsService;

        // Command for opening a timer
        public IRelayCommand<string> OpenTimerCommand { get; }

        // Constructor with dependency injection
        public TimerPageViewModel(ISettingsService settingsService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            OpenTimerCommand = new RelayCommand<string>(OpenTimer);
        }

        // Method to handle the OpenTimer command
        private void OpenTimer(string parameter)
        {
            if (string.IsNullOrEmpty(parameter))
                return;

            int seconds = 0;

            // Parse the parameter to determine the timer duration
            switch (parameter.ToLower())
            {
                case "30 secs":
                    seconds = 30;
                    break;
                case "1 mins":
                    seconds = 60;
                    break;
                case "2 mins":
                    seconds = 120;
                    break;
                case "3 mins":
                    seconds = 180;
                    break;
                case "5 mins":
                    seconds = 300;
                    break;
                case "10 mins":
                    seconds = 600;
                    break;
                case "interval":
                    seconds = -1; // Special value for interval timer
                    break;
                case "custom":
                    seconds = 0; // Special value for custom timer
                    break;
                default:
                    // Try to parse "X mins" or "X secs"
                    string[] parts = parameter.Split(' ');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int value))
                    {
                        if (parts[1].StartsWith("min"))
                            seconds = value * 60;
                        else if (parts[1].StartsWith("sec"))
                            seconds = value;
                    }
                    break;
            }

            // Use the event to notify the view to create a timer window
            TimerWindowRequested?.Invoke(this, seconds);
        }

        // Event for requesting a new timer window
        public event EventHandler<int> TimerWindowRequested;
    }
}