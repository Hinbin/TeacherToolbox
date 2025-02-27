using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

public class IntervalTimeViewModel : INotifyPropertyChanged
{
    private int hours;
    private int minutes;
    private int seconds;
    private int intervalNumber;
    private bool showRemoveButton;

    public event PropertyChangedEventHandler PropertyChanged;

    public IntervalTimeViewModel(int number)
    {
        IntervalNumber = number;
        // Only hide remove button for the first interval (when number is 1)
        ShowRemoveButton = number > 1;
        // Initialize lists for ComboBoxes
        HoursList = Enumerable.Range(0, 24).ToList();
        MinutesList = Enumerable.Range(0, 60).ToList();
        SecondsList = Enumerable.Range(0, 60).ToList();
    }

    public int Hours
    {
        get => hours;
        set
        {
            if (hours != value)
            {
                hours = value;
                OnPropertyChanged(nameof(Hours));
            }
        }
    }

    public int Minutes
    {
        get => minutes;
        set
        {
            if (minutes != value)
            {
                minutes = value;
                OnPropertyChanged(nameof(Minutes));
            }
        }
    }

    public int Seconds
    {
        get => seconds;
        set
        {
            if (seconds != value)
            {
                seconds = value;
                OnPropertyChanged(nameof(Seconds));
            }
        }
    }

    public int IntervalNumber
    {
        get => intervalNumber;
        set
        {
            if (intervalNumber != value)
            {
                intervalNumber = value;
                OnPropertyChanged(nameof(IntervalNumber));
            }
        }
    }

    public bool ShowRemoveButton
    {
        get => showRemoveButton;
        set
        {
            if (showRemoveButton != value)
            {
                showRemoveButton = value;
                OnPropertyChanged(nameof(ShowRemoveButton));
            }
        }
    }

    public List<int> HoursList { get; }
    public List<int> MinutesList { get; }
    public List<int> SecondsList { get; }

    public int TotalSeconds => (Hours * 3600) + (Minutes * 60) + Seconds;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}