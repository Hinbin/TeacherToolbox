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

    public class LocalSettings : ObservableObject
    {
        private string centreText;
        private WindowPosition lastWindowPosition;
        private Dictionary<string, object> settings;
        private readonly string filePath;

        public LocalSettings()
        {
            centreText = "Centre";
            lastWindowPosition = new WindowPosition(0, 0, 0, 0, 0);
            settings = new Dictionary<string, object>();
            filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TeacherToolbox", "settings.json");

            // Ensure directory exists
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
        }

        public void SaveSettings()
        {
            try
            {
                var settingsToSave = new Dictionary<string, object>(settings)
                {
                    { "CentreText", CentreText },
                    { "LastWindowPosition", LastWindowPosition }
                };

                string json = JsonSerializer.Serialize(settingsToSave);
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
                    var loadedSettings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

                    if (loadedSettings != null)
                    {
                        settings = new Dictionary<string, object>();
                        foreach (var kvp in loadedSettings)
                        {
                            if (kvp.Key == "CentreText")
                            {
                                CentreText = kvp.Value.GetString() ?? "Centre";
                            }
                            else if (kvp.Key == "LastWindowPosition")
                            {
                                LastWindowPosition = kvp.Value.Deserialize<WindowPosition>();
                            }
                            else
                            {
                                settings[kvp.Key] = kvp.Value;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error loading settings: {e.Message}");
            }
        }
    }
}