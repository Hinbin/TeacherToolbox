using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.IO;
using System.Runtime.InteropServices;
using TeacherToolbox.Helpers;
using TeacherToolbox.Model;
using TeacherToolbox.Services;
using Windows.Graphics;
using Windows.Media.Core;
using Windows.Media.Playback;
using WinUIEx;

namespace TeacherToolbox.Controls
{
    public sealed partial class RegisterReminderToastWindow : WindowEx
    {
        private const int WindowWidth = 360;
        private const int WindowHeight = 130;
        private const int Margin = 16;
        private const int AutoDismissSeconds = 60;

        private readonly IRegisterReminderService _reminderService;
        private readonly IThemeService _themeService;
        private readonly RegisterReminder _reminder;
        private readonly RegisterReminderSettings _settings;

        private DispatcherTimer _autoDismissTimer;
        private MediaPlayer _player;

        public RegisterReminderToastWindow(
            RegisterReminder reminder,
            RegisterReminderSettings settings,
            IRegisterReminderService reminderService,
            IThemeService themeService)
        {
            _reminder = reminder;
            _settings = settings;
            _reminderService = reminderService;
            _themeService = themeService;

            this.InitializeComponent();

            ConfigureWindow();
            PositionBottomRight();
            ApplyTheme();

            if (!string.IsNullOrWhiteSpace(reminder.Label))
            {
                LabelText.Text = reminder.Label;
                LabelText.Visibility = Visibility.Visible;
            }

            this.Activated += OnActivated;
        }

        private void OnActivated(object sender, WindowActivatedEventArgs args)
        {
            SlideInStoryboard.Begin();
            PlaySound();
            StartAutoDismiss();
            this.Activated -= OnActivated;

            // Update snooze button text with configured snooze duration
            SnoozeButton.Content = $"Snooze {_settings.SnoozeMinutes} min";
        }

        private void ConfigureWindow()
        {
            this.IsResizable = false;
            this.IsMaximizable = false;
            this.IsMinimizable = false;
            this.IsTitleBarVisible = false;
            this.SetIsAlwaysOnTop(true);

            // Prevent the toast from stealing keyboard focus
            this.ExtendsContentIntoTitleBar = true;
        }

        private void PositionBottomRight()
        {
            try
            {
                var workArea = DisplayArea.Primary.WorkArea;
                double scale = this.Content?.XamlRoot?.RasterizationScale ?? 1.0;

                int physW = (int)(WindowWidth * scale);
                int physH = (int)(WindowHeight * scale);
                int physMargin = (int)(Margin * scale);

                int x = workArea.X + workArea.Width - physW - physMargin;
                int y = workArea.Y + workArea.Height - physH - physMargin;

                this.AppWindow.MoveAndResize(new RectInt32(x, y, physW, physH));
            }
            catch
            {
                // Leave at default position if positioning fails
            }
        }

        private void ApplyTheme()
        {
            try
            {
                _themeService?.ApplyThemeToWindow(this);
            }
            catch { }
        }

        private void PlaySound()
        {
            try
            {
                string soundFile = SoundSettings.GetSoundFileName(_settings.SoundIndex);
                string soundPath = Path.Combine(AppContext.BaseDirectory, "Assets", soundFile);
                if (File.Exists(soundPath))
                {
                    _player = new MediaPlayer();
                    _player.Source = MediaSource.CreateFromUri(new Uri(soundPath));
                    _player.Play();
                }
            }
            catch { }
        }

        private void StartAutoDismiss()
        {
            _autoDismissTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(AutoDismissSeconds)
            };
            _autoDismissTimer.Tick += (_, _) => Snooze();
            _autoDismissTimer.Start();
        }

        private void SnoozeButton_Click(object sender, RoutedEventArgs e) => Snooze();

        private void DoneButton_Click(object sender, RoutedEventArgs e) => Done();

        private void Snooze()
        {
            StopAutoDismiss();
            _reminderService.Snooze(_reminder);
            CleanupAndClose();
        }

        private void Done()
        {
            StopAutoDismiss();
            _reminderService.Acknowledge(_reminder);
            CleanupAndClose();
        }

        private void StopAutoDismiss()
        {
            _autoDismissTimer?.Stop();
            _autoDismissTimer = null;
        }

        private void CleanupAndClose()
        {
            try
            {
                _player?.Dispose();
                _player = null;
            }
            catch { }

            this.Close();
        }
    }
}
