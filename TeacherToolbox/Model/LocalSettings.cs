using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Diagnostics;

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
        [System.Text.Json.Serialization.JsonPropertyName("hours")]
        public int Hours { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("minutes")]
        public int Minutes { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("seconds")]
        public int Seconds { get; set; }
    }

    public class LocalSettings : ObservableObject
    {
        private string centreText;
        private WindowPosition lastWindowPosition;
        private Dictionary<string, object> settings;
        private readonly string filePath;
        private List<SavedIntervalConfig> savedIntervalConfigs;
        private List<SavedIntervalConfig> savedCustomTimerConfigs;
        private readonly object _settingsLock = new object();
        private static LocalSettings _sharedInstance;
        private static readonly object _initLock = new object();

        // Constants for the settings keys
        public const string IntervalConfigsKey = "SavedIntervalConfigs";
        public const string CustomTimerConfigsKey = "SavedCustomTimerConfigs";

        public LocalSettings()
        {
            centreText = "Centre";
            lastWindowPosition = new WindowPosition(0, 0, 0, 0, 0);
            settings = new Dictionary<string, object>();
            savedIntervalConfigs = new List<SavedIntervalConfig>();
            savedCustomTimerConfigs = new List<SavedIntervalConfig>();
            filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TeacherToolbox", "settings.json");

            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
        }

        public static async Task<LocalSettings> GetSharedInstanceAsync()
        {
            if (_sharedInstance == null)
            {
                lock (_initLock)
                {
                    if (_sharedInstance == null)
                    {
                        Console.WriteLine("Creating new shared LocalSettings instance");
                        _sharedInstance = new LocalSettings();
                    }
                }

                // Load settings outside the lock to prevent deadlocks
                await _sharedInstance.LoadSettings();
                Console.WriteLine("Loaded settings into shared instance");
            }

            return _sharedInstance;
        }

        public static async Task<LocalSettings> CreateAsync()
        {
            Console.WriteLine("CreateAsync called - using shared instance");
            return await GetSharedInstanceAsync();
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

        // Save interval timer configurations
        public void SaveIntervalConfigs(List<SavedIntervalConfig> configs)
        {
            // Create a deep copy to avoid reference issues
            savedIntervalConfigs = configs.Select(c => new SavedIntervalConfig
            {
                Hours = c.Hours,
                Minutes = c.Minutes,
                Seconds = c.Seconds
            }).ToList();

            // Immediately save settings to persist changes
            SaveSettings();
            Debug.WriteLine($"Saved {savedIntervalConfigs.Count} interval configurations");
        }

        // Save custom timer configurations
        public void SaveCustomTimerConfigs(List<SavedIntervalConfig> configs)
        {
            // Create a deep copy to avoid reference issues
            savedCustomTimerConfigs = configs.Select(c => new SavedIntervalConfig
            {
                Hours = c.Hours,
                Minutes = c.Minutes,
                Seconds = c.Seconds
            }).ToList();

            // Immediately save settings to persist changes
            SaveSettings();
            Debug.WriteLine($"Saved {savedCustomTimerConfigs.Count} custom timer configurations");
        }

        // Get interval timer configurations
        public List<SavedIntervalConfig> GetSavedIntervalConfigs()
        {
            return new List<SavedIntervalConfig>(savedIntervalConfigs ?? new List<SavedIntervalConfig>());
        }

        // Get custom timer configurations
        public List<SavedIntervalConfig> GetSavedCustomTimerConfigs()
        {
            return new List<SavedIntervalConfig>(savedCustomTimerConfigs ?? new List<SavedIntervalConfig>());
        }

        public void SaveSettings()
        {
            lock (_settingsLock)
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
                        { IntervalConfigsKey, savedIntervalConfigs ?? new List<SavedIntervalConfig>() },
                        { CustomTimerConfigsKey, savedCustomTimerConfigs ?? new List<SavedIntervalConfig>() }
                    };

                    foreach (var setting in settings)
                    {
                        settingsToSave[setting.Key] = setting.Value;
                    }

                    string json = JsonSerializer.Serialize(settingsToSave, options);

                    // Use a temporary file to prevent corruption
                    string tempPath = filePath + ".tmp";
                    File.WriteAllText(tempPath, json);

                    // If the write was successful, replace the original file
                    if (File.Exists(tempPath))
                    {
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                        }
                        File.Move(tempPath, filePath);
                        Debug.WriteLine("Settings saved successfully");
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"Error saving settings: {e.Message}");
                    Debug.WriteLine($"Stack trace: {e.StackTrace}");
                }
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
                                    try
                                    {
                                        LastWindowPosition = kvp.Value.Deserialize<WindowPosition>(options);
                                    }
                                    catch
                                    {
                                        LastWindowPosition = new WindowPosition(0, 0, 0, 0, 0);
                                    }
                                    break;
                                case IntervalConfigsKey:
                                    try
                                    {
                                        savedIntervalConfigs = kvp.Value.Deserialize<List<SavedIntervalConfig>>(options);
                                        // Log successful loading for debugging
                                        Debug.WriteLine($"Loaded {savedIntervalConfigs?.Count ?? 0} interval configs");
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine($"Error deserializing SavedIntervalConfigs: {ex.Message}");
                                        savedIntervalConfigs = new List<SavedIntervalConfig>();
                                    }
                                    break;
                                case CustomTimerConfigsKey:
                                    try
                                    {
                                        savedCustomTimerConfigs = kvp.Value.Deserialize<List<SavedIntervalConfig>>(options);
                                        // Log successful loading for debugging
                                        Debug.WriteLine($"Loaded {savedCustomTimerConfigs?.Count ?? 0} custom timer configs");
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine($"Error deserializing SavedCustomTimerConfigs: {ex.Message}");
                                        savedCustomTimerConfigs = new List<SavedIntervalConfig>();
                                    }
                                    break;
                                default:
                                    settings[kvp.Key] = kvp.Value;
                                    break;
                            }
                        }
                    }
                }
                else
                {
                    Debug.WriteLine("Settings file does not exist, using defaults");
                    savedIntervalConfigs = new List<SavedIntervalConfig>();
                    savedCustomTimerConfigs = new List<SavedIntervalConfig>();
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Error loading settings: {e.Message}");
                Debug.WriteLine($"Stack trace: {e.StackTrace}");
                // Initialize with defaults if loading fails
                savedIntervalConfigs = new List<SavedIntervalConfig>();
                savedCustomTimerConfigs = new List<SavedIntervalConfig>();
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


        public static LocalSettings GetSharedInstanceSync()
        {
            if (_sharedInstance == null)
            {
                lock (_initLock)
                {
                    if (_sharedInstance == null)
                    {
                        _sharedInstance = new LocalSettings();

                        // Use synchronous file I/O instead of async
                        _sharedInstance.LoadSettingsSync();
                    }
                }
            }

            return _sharedInstance;
        }

        // Add this new synchronous method to LocalSettings
        public void LoadSettingsSync()
        {
            try
            {
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);  // Synchronous read
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
                                    try
                                    {
                                        LastWindowPosition = kvp.Value.Deserialize<WindowPosition>(options);
                                    }
                                    catch
                                    {
                                        LastWindowPosition = new WindowPosition(0, 0, 0, 0, 0);
                                    }
                                    break;
                                // Rest of your switch cases...
                                default:
                                    settings[kvp.Key] = kvp.Value;
                                    break;
                            }
                        }
                    }
                }
                else
                {
                    Debug.WriteLine("Settings file does not exist, using defaults");
                    savedIntervalConfigs = new List<SavedIntervalConfig>();
                    savedCustomTimerConfigs = new List<SavedIntervalConfig>();
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Error loading settings: {e.Message}");
                Debug.WriteLine($"Stack trace: {e.StackTrace}");
                // Initialize with defaults if loading fails
                savedIntervalConfigs = new List<SavedIntervalConfig>();
                savedCustomTimerConfigs = new List<SavedIntervalConfig>();
            }
        }
    }
}