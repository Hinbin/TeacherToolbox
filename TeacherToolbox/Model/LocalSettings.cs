using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Runtime.CompilerServices;

namespace TeacherToolbox.Model
{
    public struct WindowPosition
    {
        public int X { get; set; }
        public int Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public ulong DisplayID { get; set; }

        public WindowPosition(int x, int y, double width, double height, ulong displayId)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            DisplayID = displayId;
        }
    }

    public class SavedIntervalConfig
    {
        public int Hours { get; set; }
        public int Minutes { get; set; }
        public int Seconds { get; set; }
    }

    public class LocalSettings : ObservableObject
    {
        private string centreText;
        private WindowPosition lastWindowPosition;
        private Dictionary<string, object> settings;
        private readonly string filePath;
        private List<SavedIntervalConfig> savedIntervalConfigs;

        public LocalSettings()
        {
            centreText = "Centre";
            lastWindowPosition = new WindowPosition(0, 0, 0, 0, 0);
            settings = new Dictionary<string, object>();
            savedIntervalConfigs = new List<SavedIntervalConfig>();
            filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TeacherToolbox", "settings.json");

            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
        }

        public static async Task<LocalSettings> CreateAsync()
        {
            var localSettings = new LocalSettings();
            await localSettings.LoadSettings();
            return localSettings;
        }

        private void SetAndSave<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (SetProperty(ref field, value, propertyName))
            {
                SaveSettings();
            }
        }

        public WindowPosition LastWindowPosition
        {
            get => lastWindowPosition;
            set => SetAndSave(ref lastWindowPosition, value);
        }

        public string CentreText
        {
            get => centreText;
            set => SetAndSave(ref centreText, value);
        }

        public void SaveIntervalConfigs(List<SavedIntervalConfig> configs)
        {
            savedIntervalConfigs = new List<SavedIntervalConfig>(configs);
            SaveSettings();
        }

        public List<SavedIntervalConfig> GetSavedIntervalConfigs()
        {
            return new List<SavedIntervalConfig>(savedIntervalConfigs ?? new List<SavedIntervalConfig>());
        }

        public void SaveSettings()
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var settingsToSave = new Dictionary<string, object>
                {
                    { "CentreText", CentreText },
                    { "LastWindowPosition", LastWindowPosition },
                    { "SavedIntervalConfigs", savedIntervalConfigs }
                };

                foreach (var setting in settings)
                {
                    settingsToSave[setting.Key] = setting.Value;
                }

                string json = JsonSerializer.Serialize(settingsToSave, options);
                File.WriteAllText(filePath, json);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error saving settings: {e.Message}");
            }
        }

        public async Task LoadSettings()
        {
            try
            {
                if (File.Exists(filePath))
                {
                    string json = await File.ReadAllTextAsync(filePath);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var loadedSettings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, options);

                    if (loadedSettings != null)
                    {
                        settings = new Dictionary<string, object>();
                        foreach (var kvp in loadedSettings)
                        {
                            switch (kvp.Key)
                            {
                                case "CentreText":
                                    CentreText = kvp.Value.GetString() ?? "Centre";
                                    break;
                                case "LastWindowPosition":
                                    LastWindowPosition = kvp.Value.Deserialize<WindowPosition>(options);
                                    break;
                                case "SavedIntervalConfigs":
                                    savedIntervalConfigs = kvp.Value.Deserialize<List<SavedIntervalConfig>>(options) ?? new List<SavedIntervalConfig>();
                                    break;
                                default:
                                    settings[kvp.Key] = kvp.Value;
                                    break;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error loading settings: {e.Message}");
                // Initialize with defaults if loading fails
                savedIntervalConfigs = new List<SavedIntervalConfig>();
            }
        }

        public T GetValueOrDefault<T>(string key, T defaultValue)
        {
            if (settings.TryGetValue(key, out object value))
            {
                if (value is JsonElement jsonElement)
                {
                    try
                    {
                        return jsonElement.Deserialize<T>();
                    }
                    catch
                    {
                        return defaultValue;
                    }
                }
                return (T)value;
            }
            return defaultValue;
        }

        public void SetValue<T>(string key, T value)
        {
            settings[key] = value;
            SaveSettings();
        }
    }
}