using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation.Peers;
using TeacherToolbox.Model;
using TeacherToolbox.Helpers;
using System;
using Windows.Media.Core;
using Windows.Media.Playback;
using System.IO;
using System.Linq;

namespace TeacherToolbox.Controls;

public sealed partial class SettingsPage : AutomatedPage
{
    private const string ThemeKey = "Theme";
    private const string SoundKey = "Sound";
    private LocalSettings _localSettings;
    private MediaPlayer _testPlayer;

    public SettingsPage() : base()
    {
        this.InitializeComponent();
        WindowHelper.SetWindowForElement(this, App.MainWindow);
        InitializeSettingsAsync();
    }

    private async void InitializeSettingsAsync()
    {
        _localSettings = await LocalSettings.CreateAsync();

        // Clear and populate the sound combo box
        TimerSoundComboBox.Items.Clear();
        foreach (var option in SoundSettings.SoundOptions.OrderBy(x => x.Key))
        {
            TimerSoundComboBox.Items.Add(new ComboBoxItem { Content = option.Value.Name });
        }

        // Load settings from LocalSettings
        ThemeComboBox.SelectedIndex = _localSettings.GetValueOrDefault(ThemeKey, 0);
        TimerSoundComboBox.SelectedIndex = _localSettings.GetValueOrDefault(SoundSettings.SoundKey, 0);
    }

    private void TimerSound_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TimerSoundComboBox.SelectedItem is ComboBoxItem selectedItem)
        {
            UpdateTimerSound();
        }
    }

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeComboBox.SelectedItem is ComboBoxItem selectedItem)
        {
            var theme = selectedItem.Content.ToString();
            UpdateAppTheme(theme);
        }
    }

    private void UpdateAppTheme(string theme)
    {
        var window = WindowHelper.GetWindowForElement(this);

        switch (theme)
        {
            case "Light":
                ThemeHelper.RootTheme = ElementTheme.Light;
                break;
            case "Dark":
                ThemeHelper.RootTheme = ElementTheme.Dark;
                break;
            default: // System
                ThemeHelper.RootTheme = ElementTheme.Default;
                break;
        }

        // Apply the theme and update title bar
        TitleBarHelper.ApplySystemThemeToCaptionButtons(window);

        _localSettings.SetValue(ThemeKey, ThemeComboBox.SelectedIndex);
        _localSettings.SaveSettings();
    }

    private void UpdateTimerSound()
    {
        _localSettings.SetValue(SoundSettings.SoundKey, TimerSoundComboBox.SelectedIndex);
        _localSettings.SaveSettings();
    }

    private void TimerSoundButton_Clicked(object sender, RoutedEventArgs e)
    {
        try
        {
            string soundFileName = SoundSettings.GetSoundFileName(TimerSoundComboBox.SelectedIndex);
            string soundPath = Path.Combine(AppContext.BaseDirectory, "Assets", soundFileName);

            if (_testPlayer != null)
            {
                _testPlayer.Dispose();
            }

            _testPlayer = new MediaPlayer();
            _testPlayer.Source = MediaSource.CreateFromUri(new Uri(soundPath));
            _testPlayer.Play();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error playing test sound: {ex.Message}");
        }
    }



    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new FrameworkElementAutomationPeer(this);
    }


}