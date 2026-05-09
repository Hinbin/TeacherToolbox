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
        private bool _isEnabled;
        private int _hour;
        private int _minute;
        private string _label;

        public ReminderSlotViewModel(RegisterReminder model, Action onChanged)
        {
            Model = model;
            _onChanged = onChanged;
            _isEnabled = model.IsEnabled;
            _hour = model.Hour;
            _minute = model.Minute;
            _label = model.Label ?? "";
        }

        public RegisterReminder Model { get; }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (SetProperty(ref _isEnabled, value))
                {
                    Model.IsEnabled = value;
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

        /// <summary>Hours 0–23 for the ComboBox.</summary>
        public List<int> HourOptions { get; } = Enumerable.Range(0, 24).ToList();

        /// <summary>Minutes 0–59 for the ComboBox.</summary>
        public List<int> MinuteOptions { get; } = Enumerable.Range(0, 60).ToList();
    }

    public sealed class RegisterReminderViewModel : ObservableObject
    {
        private const int SlotCount = 6;

        private readonly ISettingsService _settingsService;
        private readonly IRegisterReminderService _reminderService;
        private readonly ITelemetryService _telemetry;
        private MediaPlayer _testPlayer;

        private bool _masterEnabled;
        private bool _weekdaysOnly;
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
                    Save();
            }
        }

        public bool WeekdaysOnly
        {
            get => _weekdaysOnly;
            set
            {
                if (SetProperty(ref _weekdaysOnly, value))
                    Save();
            }
        }

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
            _weekdaysOnly = settings.WeekdaysOnly;
            _snoozeMinutes = settings.SnoozeMinutes;
            _selectedSoundIndex = settings.SoundIndex;

            // Ensure we always have exactly SlotCount reminders
            var reminders = settings.Reminders ?? new List<RegisterReminder>();
            var defaultTimes = new (int Hour, int Minute)[] { (8, 30), (9, 25), (10, 25), (11, 25), (12, 25), (13, 25) };
            while (reminders.Count < SlotCount)
            {
                var (h, m) = defaultTimes[reminders.Count];
                reminders.Add(new RegisterReminder { Hour = h, Minute = m, IsEnabled = false });
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
                WeekdaysOnly = _weekdaysOnly,
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
