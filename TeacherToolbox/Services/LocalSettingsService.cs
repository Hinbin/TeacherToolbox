using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Diagnostics;
using TeacherToolbox.Model;
using TeacherToolbox.Services;
using TeacherToolbox.Helpers;

namespace TeacherToolbox.Services
{
    /// <summary>
    /// Concrete implementation of ISettingsService that uses local storage
    /// </summary>
    public class LocalSettingsService : ISettingsService
    {
        // Constants for the settings keys
        private const string ThemeKey = "Theme";
        private const string SoundKey = "Sound";
        private const string TimerFinishBehaviorKey = "TimerFinishBehavior";
        private const string IntervalConfigsKey = "SavedIntervalConfigs";
        private const string CustomTimerConfigsKey = "SavedCustomTimerConfigs";
        private const string CentreTextKey = "CentreText";
        private const string LastWindowPositionKey = "LastWindowPosition";
        private const string LastTimerWindowPositionKey = "LastTimerWindowPosition";
        private const string HasShownClockInstructionsKey = "HasShownClockInstructions";

        // Private fields
        private string centreText;
        private WindowPosition lastWindowPosition;
        private WindowPosition lastTimerWindowPosition;
        private Dictionary<string, object> settings;
        private readonly string filePath;
        private List<SavedIntervalConfig> savedIntervalConfigs;
        private List<SavedIntervalConfig> savedCustomTimerConfigs;
        private readonly object _settingsLock = new object();
        private bool hasShownClockInstructions;

        protected virtual string FilePath => filePath;

        public LocalSettingsService()
        {
            centreText = "Centre";
            lastWindowPosition = new WindowPosition(0, 0, 0, 0, 0);
            lastTimerWindowPosition = new WindowPosition(0, 0, 350, 300, 0);
            settings = new Dictionary<string, object>();
            savedIntervalConfigs = new List<SavedIntervalConfig>();
            savedCustomTimerConfigs = new List<SavedIntervalConfig>();
            filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TeacherToolbox", "settings.json");

            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
        }

        public async Task InitializeAsync()
        {
            await LoadSettings();
        }

        public void InitializeSync()
        {
            LoadSettingsSync();
        }

        #region Helper Methods

        private void SetAndSave<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (!EqualityComparer<T>.Default.Equals(field, value))
            {
                field = value;
                SaveSettings();
            }
        }

        #endregion

        #region ISettingsService Implementation

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public void SetValue<T>(string key, T value)
        {
            settings[key] = value;
            SaveSettings();
        }

        /// <inheritdoc/>
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
                        { CentreTextKey, centreText },
                        { LastWindowPositionKey, lastWindowPosition },
                        { LastTimerWindowPositionKey, lastTimerWindowPosition },
                        { IntervalConfigsKey, savedIntervalConfigs ?? new List<SavedIntervalConfig>() },
                        { HasShownClockInstructionsKey, hasShownClockInstructions },
                        { CustomTimerConfigsKey, savedCustomTimerConfigs ?? new List<SavedIntervalConfig>() }
                    };

                    foreach (var setting in settings)
                    {
                        settingsToSave[setting.Key] = setting.Value;
                    }

                    string json = JsonSerializer.Serialize(settingsToSave, options);

                    // Use a temporary file to prevent corruption
                    string tempPath = FilePath + ".tmp";
                    File.WriteAllText(tempPath, json);

                    // If the write was successful, replace the original file
                    if (File.Exists(tempPath))
                    {
                        if (File.Exists(FilePath))
                        {
                            File.Delete(FilePath);
                        }
                        File.Move(tempPath, FilePath);
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

        /// <inheritdoc/>
        public async Task LoadSettings()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    string json = await File.ReadAllTextAsync(FilePath);
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
                                case CentreTextKey:
                                    centreText = kvp.Value.GetString() ?? "Centre";
                                    break;
                                case LastWindowPositionKey:
                                    try
                                    {
                                        lastWindowPosition = kvp.Value.Deserialize<WindowPosition>(options);
                                    }
                                    catch
                                    {
                                        lastWindowPosition = new WindowPosition(0, 0, 0, 0, 0);
                                    }
                                    break;
                                case LastTimerWindowPositionKey:
                                    try
                                    {
                                        lastTimerWindowPosition = kvp.Value.Deserialize<WindowPosition>(options);
                                    }
                                    catch
                                    {
                                        lastTimerWindowPosition = new WindowPosition(0, 0, 200, 200, 0);
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
                                case HasShownClockInstructionsKey:
                                    hasShownClockInstructions = kvp.Value.GetBoolean();
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

        public void LoadSettingsSync()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath);
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
                                case CentreTextKey:
                                    centreText = kvp.Value.GetString() ?? "Centre";
                                    break;
                                case LastWindowPositionKey:
                                    try
                                    {
                                        lastWindowPosition = kvp.Value.Deserialize<WindowPosition>(options);
                                    }
                                    catch
                                    {
                                        lastWindowPosition = new WindowPosition(0, 0, 0, 0, 0);
                                    }
                                    break;
                                case LastTimerWindowPositionKey:
                                    try
                                    {
                                        lastTimerWindowPosition = kvp.Value.Deserialize<WindowPosition>(options);
                                    }
                                    catch
                                    {
                                        lastTimerWindowPosition = new WindowPosition(0, 0, 200, 200, 0);
                                    }
                                    break;
                                case IntervalConfigsKey:
                                    try
                                    {
                                        savedIntervalConfigs = kvp.Value.Deserialize<List<SavedIntervalConfig>>(options);
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
                                        Debug.WriteLine($"Loaded {savedCustomTimerConfigs?.Count ?? 0} custom timer configs");
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine($"Error deserializing SavedCustomTimerConfigs: {ex.Message}");
                                        savedCustomTimerConfigs = new List<SavedIntervalConfig>();
                                    }
                                    break;
                                case HasShownClockInstructionsKey:
                                    hasShownClockInstructions = kvp.Value.GetBoolean();
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

        #region Timer Settings

        /// <inheritdoc/>
        public int GetTimerSound()
        {
            return GetValueOrDefault(SoundKey, 0);
        }

        /// <inheritdoc/>
        public void SetTimerSound(int soundIndex)
        {
            SetValue(SoundKey, soundIndex);
        }

        /// <inheritdoc/>
        public TimerFinishBehavior GetTimerFinishBehavior()
        {
            int value = GetValueOrDefault(TimerFinishBehaviorKey, 0);
            return (TimerFinishBehavior)value;
        }

        /// <inheritdoc/>
        public void SetTimerFinishBehavior(TimerFinishBehavior behavior)
        {
            SetValue(TimerFinishBehaviorKey, (int)behavior);
        }

        #endregion

        #region Theme Settings

        /// <inheritdoc/>
        public int GetTheme()
        {
            return GetValueOrDefault(ThemeKey, 0);
        }

        /// <inheritdoc/>
        public void SetTheme(int themeIndex)
        {
            SetValue(ThemeKey, themeIndex);
        }

        #endregion

        #region Window Position

        /// <inheritdoc/>
        public WindowPosition GetLastWindowPosition()
        {
            return lastWindowPosition;
        }

        /// <inheritdoc/>
        public void SetLastWindowPosition(WindowPosition position)
        {
            SetAndSave(ref lastWindowPosition, position);
        }

        /// <inheritdoc/>
        public WindowPosition GetLastTimerWindowPosition()
        {
            return lastTimerWindowPosition;
        }

        /// <inheritdoc/>
        public void SetLastTimerWindowPosition(WindowPosition position)
        {
            SetAndSave(ref lastTimerWindowPosition, position);
        }

        #endregion

        #region Interval Configurations

        /// <inheritdoc/>
        public List<SavedIntervalConfig> GetSavedIntervalConfigs()
        {
            return new List<SavedIntervalConfig>(savedIntervalConfigs ?? new List<SavedIntervalConfig>());
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public List<SavedIntervalConfig> GetSavedCustomTimerConfigs()
        {
            return new List<SavedIntervalConfig>(savedCustomTimerConfigs ?? new List<SavedIntervalConfig>());
        }

        /// <inheritdoc/>
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

        #endregion

        #region Misc Settings

        /// <inheritdoc/>
        public bool GetHasShownClockInstructions()
        {
            return hasShownClockInstructions;
        }

        /// <inheritdoc/>
        public void SetHasShownClockInstructions(bool shown)
        {
            SetAndSave(ref hasShownClockInstructions, shown);
        }

        /// <inheritdoc/>
        public string GetCentreText()
        {
            return centreText;
        }

        /// <inheritdoc/>
        public void SetCentreText(string text)
        {
            SetAndSave(ref centreText, text);
        }

        #endregion

        #endregion
    }
}