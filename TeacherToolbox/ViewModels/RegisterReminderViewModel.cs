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
    public sealed class ReminderSlotViewModel : ObservableObject
    {
        private readonly Action _onChanged;
        private int _hour;
        private int _minute;
        private string _label;
        private bool _monday;
        private bool _tuesday;
        private bool _wednesday;
        private bool _thursday;
        private bool _friday;
        private bool _saturday;
        private bool _sunday;

        public ReminderSlotViewModel(RegisterReminder model, Action onChanged)
        {
            Model = model;
            _onChanged = onChanged;
            _hour = model.Hour;
            _minute = model.Minute;
            _label = model.Label ?? "";
            var days = model.Days;
            _monday = days.HasFlag(ReminderDays.Monday);
            _tuesday = days.HasFlag(ReminderDays.Tuesday);
            _wednesday = days.HasFlag(ReminderDays.Wednesday);
            _thursday = days.HasFlag(ReminderDays.Thursday);
            _friday = days.HasFlag(ReminderDays.Friday);
            _saturday = days.HasFlag(ReminderDays.Saturday);
            _sunday = days.HasFlag(ReminderDays.Sunday);
            Model.Days = days;
        }

        public RegisterReminder Model { get; }

        public int Hour
        {
            get => _hour;
            set
            {
                if (SetProperty(ref _hour, Math.Clamp(value, 0, 23)))
                {
                    Model.Hour = _hour;
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
                    Model.Minute = _minute;
                    _onChanged();
                }
            }
        }

        public string Label
        {
            get => _label;
            set
            {
                if (SetProperty(ref _label, value ?? ""))
                {
                    Model.Label = _label;
                    _onChanged();
                }
            }
        }

        public bool Monday
        {
            get => _monday;
            set => SetDay(ref _monday, value, ReminderDays.Monday);
        }

        public bool Tuesday
        {
            get => _tuesday;
            set => SetDay(ref _tuesday, value, ReminderDays.Tuesday);
        }

        public bool Wednesday
        {
            get => _wednesday;
            set => SetDay(ref _wednesday, value, ReminderDays.Wednesday);
        }

        public bool Thursday
        {
            get => _thursday;
            set => SetDay(ref _thursday, value, ReminderDays.Thursday);
        }

        public bool Friday
        {
            get => _friday;
            set => SetDay(ref _friday, value, ReminderDays.Friday);
        }

        public bool Saturday
        {
            get => _saturday;
            set => SetDay(ref _saturday, value, ReminderDays.Saturday);
        }

        public bool Sunday
        {
            get => _sunday;
            set => SetDay(ref _sunday, value, ReminderDays.Sunday);
        }

        /// <summary>Hours 0–23 for the ComboBox.</summary>
        public List<int> HourOptions { get; } = Enumerable.Range(0, 24).ToList();

        /// <summary>Minutes 0–59 for the ComboBox.</summary>
        public List<int> MinuteOptions { get; } = Enumerable.Range(0, 60).ToList();

        private void SetDay(ref bool field, bool value, ReminderDays day)
        {
            if (!SetProperty(ref field, value)) return;

            Model.Days = value
                ? Model.Days | day
                : Model.Days & ~day;
            _onChanged();
        }
    }

    public sealed class RegisterReminderViewModel : ObservableObject
    {
        private const int SlotCount = 6;

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

            SoundOptions = SoundSettings.SoundOptions.Values.ToList();
            TestSoundCommand = new RelayCommand(TestSound);

            LoadFromSettings();
        }

        public ObservableCollection<ReminderSlotViewModel> Slots { get; } = new();

        public List<SoundSettings.SoundOption> SoundOptions { get; }

        public IRelayCommand TestSoundCommand { get; }

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

            // Ensure we always have exactly SlotCount reminders
            var reminders = settings.Reminders ?? new List<RegisterReminder>();
            var defaultTimes = new (int Hour, int Minute)[] { (8, 30), (9, 25), (10, 25), (11, 25), (12, 25), (13, 25) };
            while (reminders.Count < SlotCount)
            {
                var (h, m) = defaultTimes[reminders.Count];
                reminders.Add(new RegisterReminder { Hour = h, Minute = m, Days = ReminderDays.None });
            }

            Slots.Clear();
            for (int i = 0; i < SlotCount; i++)
                Slots.Add(new ReminderSlotViewModel(reminders[i], Save));
        }

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
                string soundFile = SoundSettings.GetSoundFileName(_selectedSoundIndex);
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
