using System.Collections.Generic;
using System.Threading.Tasks;

namespace TeacherToolbox.Services
{
    /// <summary>
    /// Interface for application settings service that handles persisting and retrieving settings
    /// </summary>
    public interface ISettingsService
    {
        #region General Settings

        /// <summary>
        /// Gets a setting value with the specified key, or returns the default value if not found
        /// </summary>
        /// <typeparam name="T">The type of the setting value</typeparam>
        /// <param name="key">The key of the setting</param>
        /// <param name="defaultValue">The default value to return if the setting is not found</param>
        /// <returns>The setting value or default value</returns>
        T GetValueOrDefault<T>(string key, T defaultValue);

        /// <summary>
        /// Sets a setting value with the specified key
        /// </summary>
        /// <typeparam name="T">The type of the setting value</typeparam>
        /// <param name="key">The key of the setting</param>
        /// <param name="value">The value to set</param>
        void SetValue<T>(string key, T value);

        /// <summary>
        /// Saves all settings to persistent storage
        /// </summary>
        void SaveSettings();

        /// <summary>
        /// Loads all settings from persistent storage
        /// </summary>
        /// <returns>Task representing the asynchronous operation</returns>
        Task LoadSettings();

        #endregion

        #region Timer Settings

        /// <summary>
        /// Gets the currently selected timer sound index
        /// </summary>
        /// <returns>The selected sound index</returns>
        int GetTimerSound();

        /// <summary>
        /// Sets the timer sound index
        /// </summary>
        /// <param name="soundIndex">The sound index to set</param>
        void SetTimerSound(int soundIndex);

        /// <summary>
        /// Gets the timer finish behavior
        /// </summary>
        /// <returns>The timer finish behavior enum value</returns>
        Model.TimerFinishBehavior GetTimerFinishBehavior();

        /// <summary>
        /// Sets the timer finish behavior
        /// </summary>
        /// <param name="behavior">The timer finish behavior to set</param>
        void SetTimerFinishBehavior(Model.TimerFinishBehavior behavior);

        #endregion

        #region Theme Settings

        /// <summary>
        /// Gets the application theme setting
        /// </summary>
        /// <returns>The theme index (0=System, 1=Light, 2=Dark)</returns>
        int GetTheme();

        /// <summary>
        /// Sets the application theme
        /// </summary>
        /// <param name="themeIndex">The theme index to set (0=System, 1=Light, 2=Dark)</param>
        void SetTheme(int themeIndex);

        #endregion

        #region Window Position

        /// <summary>
        /// Gets the last saved window position
        /// </summary>
        /// <returns>The window position</returns>
        Model.WindowPosition GetLastWindowPosition();

        /// <summary>
        /// Sets the last window position
        /// </summary>
        /// <param name="position">The window position to save</param>
        void SetLastWindowPosition(Model.WindowPosition position);

        /// <summary>
        /// Gets the last saved timer window position
        /// </summary>
        /// <returns>The timer window position</returns>
        Model.WindowPosition GetLastTimerWindowPosition();

        /// <summary>
        /// Sets the last timer window position
        /// </summary>
        /// <param name="position">The timer window position to save</param>
        void SetLastTimerWindowPosition(Model.WindowPosition position);

        #endregion

        #region Interval Configurations

        /// <summary>
        /// Gets saved interval configurations
        /// </summary>
        /// <returns>List of saved interval configurations</returns>
        List<Model.SavedIntervalConfig> GetSavedIntervalConfigs();

        /// <summary>
        /// Saves interval configurations
        /// </summary>
        /// <param name="configs">The configurations to save</param>
        void SaveIntervalConfigs(List<Model.SavedIntervalConfig> configs);

        /// <summary>
        /// Gets saved custom timer configurations
        /// </summary>
        /// <returns>List of saved custom timer configurations</returns>
        List<Model.SavedIntervalConfig> GetSavedCustomTimerConfigs();

        /// <summary>
        /// Saves custom timer configurations
        /// </summary>
        /// <param name="configs">The configurations to save</param>
        void SaveCustomTimerConfigs(List<Model.SavedIntervalConfig> configs);

        #endregion

        #region Clock Settings

        /// <summary>
        /// Gets whether clock instructions have been shown
        /// </summary>
        /// <returns>True if instructions have been shown, false otherwise</returns>
        bool GetHasShownClockInstructions();

        /// <summary>
        /// Sets whether clock instructions have been shown
        /// </summary>
        /// <param name="shown">True if instructions have been shown, false otherwise</param>
        void SetHasShownClockInstructions(bool shown);

        /// <summary>
        /// Gets the center text setting
        /// </summary>
        /// <returns>The center text</returns>
        string GetCentreText();

        /// <summary>
        /// Sets the center text setting
        /// </summary>
        /// <param name="text">The center text to set</param>
        void SetCentreText(string text);

        // Mock Mode settings
        bool GetMockMode();
        void SetMockMode(bool value);

        bool GetSoundEnabled();
        void SetSoundEnabled(bool value);

        #endregion
    }
}