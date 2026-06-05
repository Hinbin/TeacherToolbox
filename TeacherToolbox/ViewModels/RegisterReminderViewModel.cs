using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using TeacherToolbox.Helpers;
using TeacherToolbox.Model;
using TeacherToolbox.Services;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace TeacherToolbox.ViewModels
{
    public sealed class DaySlotViewModel : ObservableObject
    {
        private readonly DaySchedule _model;
        private readonly Action _onChanged;
        private bool _enabled;
        private int _hour;
        private int _minute;

        public DaySlotViewModel(DaySchedule model, Action onChanged)
        {
            _model = model;
            _onChanged = onChanged;
            _enabled = model.Enabled;
            _hour = model.Hour;
            _minute = model.Minute;
        }

        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (SetProperty(ref _enabled, value))
                {
                    _model.Enabled = value;
                    _onChanged();
                }
            }
        }

        public int Hour
        {
            get => _hour;
            set
            {
                if (SetProperty(ref _hour, Math.Clamp(value, 0, 23)))
                {
                    _model.Hour = _hour;
                    _onChanged();
                }
            }
        }

        public int Minute
        {
            get => _minute;
            set
            {
                if (SetProperty(ref _minute, Math.Clamp(value, 0, 59)))
                {
                    _model.Minute = _minute;
                    OnPropertyChanged(nameof(MinuteString));
                    _onChanged();
                }
            }
        }

        public List<int> HourOptions { get; } = Enumerable.Range(0, 24).ToList();
        public List<string> MinuteOptions { get; } = Enumerable.Range(0, 60).Select(m => m.ToString("D2")).ToList();

        public string MinuteString
        {
            get => _minute.ToString("D2");
            set
            {
                if (int.TryParse(value, out int parsed))
                    Minute = parsed;
            }
        }
    }

    public sealed class ReminderSlotViewModel : ObservableObject
    {
        private readonly Action _onChanged;
        private string _label;

        public ReminderSlotViewModel(RegisterReminder model, Action onChanged, Action<ReminderSlotViewModel> onRemove)
        {
            Model = model;
            _onChanged = onChanged;
            _label = model.Label ?? "";
            Monday = new DaySlotViewModel(model.Monday, onChanged);
            Tuesday = new DaySlotViewModel(model.Tuesday, onChanged);
            Wednesday = new DaySlotViewModel(model.Wednesday, onChanged);
            Thursday = new DaySlotViewModel(model.Thursday, onChanged);
            Friday = new DaySlotViewModel(model.Friday, onChanged);
            RemoveCommand = new RelayCommand(() => onRemove(this));
        }

        public RegisterReminder Model { get; }

        public IRelayCommand RemoveCommand { get; }

        public string Label
        {
            get => _label;
            set
            {
                if (SetProperty(ref _label, value ?? ""))
                {
                    Model.Label = _label;
                    OnPropertyChanged(nameof(DisplayLabel));
                    _onChanged();
                }
            }
        }

        public string DisplayLabel => string.IsNullOrWhiteSpace(_label) ? "Untitled" : _label;

        public DaySlotViewModel Monday { get; }
        public DaySlotViewModel Tuesday { get; }
        public DaySlotViewModel Wednesday { get; }
        public DaySlotViewModel Thursday { get; }
        public DaySlotViewModel Friday { get; }
    }

    public sealed class RegisterReminderViewModel : ObservableObject
    {
        private static readonly string[] DefaultLabels =
            ["Registration", "Period 1", "Period 2", "Period 3", "Period 4", "Period 5"];

        private static readonly (int Hour, int Minute)[] DefaultTimes =
            [(8, 30), (9, 0), (10, 0), (11, 25), (12, 25), (13, 55)];

        private readonly ISettingsService _settingsService;
        private readonly IRegisterReminderService _reminderService;
        private readonly ITelemetryService _telemetry;
        private MediaPlayer _testPlayer;

        private bool _masterEnabled;
        private int _snoozeMinutes;
        private int _selectedSoundIndex;

        public RegisterReminderViewModel(
            ISettingsService settingsService,
            IRegisterReminderService reminderService,
            ITelemetryService telemetry)
        {
            _settingsService = settingsService;
            _reminderService = reminderService;
            _telemetry = telemetry;

            SoundOptions = SoundSettings.RegisterReminderSoundOptions
                .OrderBy(x => x.Key)
                .Select(x => x.Value)
                .ToList();
            TestSoundCommand = new RelayCommand(TestSound);
            AddSlotCommand = new RelayCommand(AddSlot);

            LoadFromSettings();
        }

        public ObservableCollection<ReminderSlotViewModel> Slots { get; } = new();

        public List<SoundSettings.SoundOption> SoundOptions { get; }

        public IRelayCommand TestSoundCommand { get; }
        public IRelayCommand AddSlotCommand { get; }

        public bool MasterEnabled
        {
            get => _masterEnabled;
            set
            {
                if (SetProperty(ref _masterEnabled, value))
                {
                    OnPropertyChanged(nameof(ShowSetupInfo));
                    Save();
                }
            }
        }

        public bool ShowSetupInfo => !_masterEnabled;

        public int SnoozeMinutes
        {
            get => _snoozeMinutes;
            set
            {
                int clamped = Math.Clamp(value, 1, 30);
                if (SetProperty(ref _snoozeMinutes, clamped))
                    Save();
            }
        }

        public int SelectedSoundIndex
        {
            get => _selectedSoundIndex;
            set
            {
                if (SetProperty(ref _selectedSoundIndex, value))
                    Save();
            }
        }

        private void LoadFromSettings()
        {
            var settings = _settingsService.GetRegisterReminderSettings();
            _masterEnabled = settings.MasterEnabled;
            _snoozeMinutes = settings.SnoozeMinutes;
            _selectedSoundIndex = settings.SoundIndex;

            var reminders = settings.Reminders ?? new List<RegisterReminder>();
            if (reminders.Count == 0)
            {
                for (int i = 0; i < DefaultLabels.Length; i++)
                {
                    var (h, m) = DefaultTimes[i];
                    reminders.Add(new RegisterReminder
                    {
                        Label = DefaultLabels[i],
                        Monday = new DaySchedule { Hour = h, Minute = m },
                        Tuesday = new DaySchedule { Hour = h, Minute = m },
                        Wednesday = new DaySchedule { Hour = h, Minute = m },
                        Thursday = new DaySchedule { Hour = h, Minute = m },
                        Friday = new DaySchedule { Hour = h, Minute = m }
                    });
                }
            }

            Slots.Clear();
            foreach (var r in reminders)
                Slots.Add(CreateSlotViewModel(r));
        }

        private void AddSlot()
        {
            var reminder = new RegisterReminder
            {
                Monday = new DaySchedule { Hour = 9 },
                Tuesday = new DaySchedule { Hour = 9 },
                Wednesday = new DaySchedule { Hour = 9 },
                Thursday = new DaySchedule { Hour = 9 },
                Friday = new DaySchedule { Hour = 9 }
            };
            Slots.Add(CreateSlotViewModel(reminder));
            Save();
        }

        private ReminderSlotViewModel CreateSlotViewModel(RegisterReminder reminder) =>
            new(reminder, Save, slot => { Slots.Remove(slot); Save(); });

        private void Save()
        {
            var settings = new RegisterReminderSettings
            {
                MasterEnabled = _masterEnabled,
                SnoozeMinutes = _snoozeMinutes,
                SoundIndex = _selectedSoundIndex,
                Reminders = Slots.Select(s => s.Model).ToList()
            };

            _settingsService.SaveRegisterReminderSettings(settings);
            _reminderService.UpdateSettings(settings);
        }

        private void TestSound()
        {
            try
            {
                _testPlayer?.Dispose();
                string soundFile = SoundSettings.GetRegisterReminderSoundFileName(_selectedSoundIndex);
                string soundPath = Path.Combine(AppContext.BaseDirectory, "Assets", soundFile);
                if (File.Exists(soundPath))
                {
                    _testPlayer = new MediaPlayer();
                    _testPlayer.Source = MediaSource.CreateFromUri(new Uri(soundPath));
                    _testPlayer.Play();
                }
            }
            catch (Exception ex)
            {
                _telemetry.LogWarning("Error testing reminder sound", ex);
            }
        }
    }
}
