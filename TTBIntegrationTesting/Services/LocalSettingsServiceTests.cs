using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using TeacherToolbox.Model;
using TeacherToolbox.Services;
using TeacherToolbox.Helpers;

namespace TeacherToolbox.Tests.Services
{
    [TestFixture]
    public class LocalSettingsServiceTests
    {
        // Test-specific file path to avoid interfering with the actual application settings
        private string _testFilePath;
        private LocalSettingsService _settingsService;

        [SetUp]
        public void Setup()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "TeacherToolboxTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            _testFilePath = Path.Combine(tempDir, "test_settings.json");
            _settingsService = new TestLocalSettingsService(_testFilePath);
        }

        [TearDown]
        public void Cleanup()
        {
            // Clean up test directory
            string testDir = Path.GetDirectoryName(_testFilePath);
            if (Directory.Exists(testDir))
            {
                try
                {
                    Directory.Delete(testDir, true);
                }
                catch (IOException)
                {
                    // Ignore IO exceptions during cleanup - these might happen if files are still in use
                }
            }
        }

        #region Basic Settings Operations

        [Test]
        public void GetValueOrDefault_WhenKeyDoesNotExist_ReturnsDefaultValue()
        {
            // Arrange
            const string key = "NonExistentKey";
            const string defaultValue = "DefaultValue";

            // Act
            var result = _settingsService.GetValueOrDefault(key, defaultValue);

            // Assert
            Assert.That(result, Is.EqualTo(defaultValue));
        }

        [Test]
        public void SetValue_ThenGetValue_ReturnsSetValue()
        {
            // Arrange
            const string key = "TestKey";
            const string value = "TestValue";

            // Act
            _settingsService.SetValue(key, value);
            var result = _settingsService.GetValueOrDefault(key, "DefaultValue");

            // Assert
            Assert.That(result, Is.EqualTo(value));
        }

        [Test]
        public void SetValue_WithNullKey_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => _settingsService.SetValue(null, "value"));
        }

        #endregion

        #region Theme Settings

        [Test]
        public void GetTheme_DefaultValue_ReturnsZero()
        {
            // Act
            var result = _settingsService.GetTheme();

            // Assert
            Assert.That(result, Is.EqualTo(0)); // 0 corresponds to "System" theme
        }

        [Test]
        public void SetTheme_ThenGetTheme_ReturnsSetValue()
        {
            // Arrange
            const int themeIndex = 2; // Dark theme

            // Act
            _settingsService.SetTheme(themeIndex);
            var result = _settingsService.GetTheme();

            // Assert
            Assert.That(result, Is.EqualTo(themeIndex));
        }

        #endregion

        #region Timer Sound Settings

        [Test]
        public void GetTimerSound_DefaultValue_ReturnsZero()
        {
            // Act
            var result = _settingsService.GetTimerSound();

            // Assert
            Assert.That(result, Is.EqualTo(0)); // Default sound index
        }

        [Test]
        public void SetTimerSound_ThenGetTimerSound_ReturnsSetValue()
        {
            // Arrange
            const int soundIndex = 3;

            // Act
            _settingsService.SetTimerSound(soundIndex);
            var result = _settingsService.GetTimerSound();

            // Assert
            Assert.That(result, Is.EqualTo(soundIndex));
        }

        #endregion

        #region Timer Finish Behavior Settings

        [Test]
        public void GetTimerFinishBehavior_DefaultValue_ReturnsCloseTimer()
        {
            // Act
            var result = _settingsService.GetTimerFinishBehavior();

            // Assert
            Assert.That(result, Is.EqualTo(TimerFinishBehavior.CloseTimer));
        }

        [Test]
        public void SetTimerFinishBehavior_ThenGetTimerFinishBehavior_ReturnsSetValue()
        {
            // Arrange
            const TimerFinishBehavior behavior = TimerFinishBehavior.CountUp;

            // Act
            _settingsService.SetTimerFinishBehavior(behavior);
            var result = _settingsService.GetTimerFinishBehavior();

            // Assert
            Assert.That(result, Is.EqualTo(behavior));
        }

        #endregion

        #region Window Position Settings

        [Test]
        public void GetLastWindowPosition_DefaultValue_ReturnsDefaultPosition()
        {
            // Act
            var result = _settingsService.GetLastWindowPosition();

            // Assert
            Assert.That(result.X, Is.EqualTo(0));
            Assert.That(result.Y, Is.EqualTo(0));
            Assert.That(result.Width, Is.EqualTo(0));
            Assert.That(result.Height, Is.EqualTo(0));
        }


        [Test]
        public void SetLastWindowPosition_ThenGetLastWindowPosition_ReturnsSetValue()
        {
            // Arrange
            var position = new WindowPosition(100, 200, 800, 600, 1);

            // Act
            _settingsService.SetLastWindowPosition(position);
            var result = _settingsService.GetLastWindowPosition();

            // Assert
            Assert.That(result.IsEmpty, Is.False);
            Assert.That(result.X, Is.EqualTo(position.X));
            Assert.That(result.Y, Is.EqualTo(position.Y));
            Assert.That(result.Width, Is.EqualTo(position.Width));
            Assert.That(result.Height, Is.EqualTo(position.Height));
            Assert.That(result.DisplayID, Is.EqualTo(position.DisplayID));
        }

        [Test]
        public void GetLastTimerWindowPosition_DefaultValue_ReturnsDefaultPosition()
        {
            // Act
            var result = _settingsService.GetLastWindowPosition();

            // Assert
            Assert.That(result.X, Is.EqualTo(0));
            Assert.That(result.Y, Is.EqualTo(0));
            Assert.That(result.Width, Is.EqualTo(0));
            Assert.That(result.Height, Is.EqualTo(0));
        }

        [Test]
        public void SetLastTimerWindowPosition_ThenGetLastTimerWindowPosition_ReturnsSetValue()
        {
            // Arrange
            var position = new WindowPosition(150, 250, 400, 300, 2);

            // Act
            _settingsService.SetLastTimerWindowPosition(position);
            var result = _settingsService.GetLastTimerWindowPosition();

            // Assert
            Assert.That(result.IsEmpty, Is.False);
            Assert.That(result.X, Is.EqualTo(position.X));
            Assert.That(result.Y, Is.EqualTo(position.Y));
            Assert.That(result.Width, Is.EqualTo(position.Width));
            Assert.That(result.Height, Is.EqualTo(position.Height));
            Assert.That(result.DisplayID, Is.EqualTo(position.DisplayID));
        }

        #endregion

        #region Interval Configurations

        [Test]
        public void GetSavedIntervalConfigs_DefaultValue_ReturnsEmptyList()
        {
            // Act
            var result = _settingsService.GetSavedIntervalConfigs();

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void SaveIntervalConfigs_ThenGetSavedIntervalConfigs_ReturnsSetValue()
        {
            // Arrange
            var configs = new List<SavedIntervalConfig>
            {
                new SavedIntervalConfig { Hours = 1, Minutes = 30, Seconds = 0 },
                new SavedIntervalConfig { Hours = 0, Minutes = 45, Seconds = 30 }
            };

            // Act
            _settingsService.SaveIntervalConfigs(configs);
            var result = _settingsService.GetSavedIntervalConfigs();

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(configs.Count));
            Assert.That(result[0].Hours, Is.EqualTo(configs[0].Hours));
            Assert.That(result[0].Minutes, Is.EqualTo(configs[0].Minutes));
            Assert.That(result[0].Seconds, Is.EqualTo(configs[0].Seconds));
            Assert.That(result[1].Hours, Is.EqualTo(configs[1].Hours));
            Assert.That(result[1].Minutes, Is.EqualTo(configs[1].Minutes));
            Assert.That(result[1].Seconds, Is.EqualTo(configs[1].Seconds));
        }

        [Test]
        public void GetSavedCustomTimerConfigs_DefaultValue_ReturnsEmptyList()
        {
            // Act
            var result = _settingsService.GetSavedCustomTimerConfigs();

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void SaveCustomTimerConfigs_ThenGetSavedCustomTimerConfigs_ReturnsSetValue()
        {
            // Arrange
            var configs = new List<SavedIntervalConfig>
            {
                new SavedIntervalConfig { Hours = 2, Minutes = 15, Seconds = 30 },
                new SavedIntervalConfig { Hours = 0, Minutes = 5, Seconds = 0 }
            };

            // Act
            _settingsService.SaveCustomTimerConfigs(configs);
            var result = _settingsService.GetSavedCustomTimerConfigs();

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(configs.Count));
            Assert.That(result[0].Hours, Is.EqualTo(configs[0].Hours));
            Assert.That(result[0].Minutes, Is.EqualTo(configs[0].Minutes));
            Assert.That(result[0].Seconds, Is.EqualTo(configs[0].Seconds));
            Assert.That(result[1].Hours, Is.EqualTo(configs[1].Hours));
            Assert.That(result[1].Minutes, Is.EqualTo(configs[1].Minutes));
            Assert.That(result[1].Seconds, Is.EqualTo(configs[1].Seconds));
        }

        #endregion

        #region Misc Settings

        [Test]
        public void GetHasShownClockInstructions_DefaultValue_ReturnsFalse()
        {
            // Act
            var result = _settingsService.GetHasShownClockInstructions();

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void SetHasShownClockInstructions_ThenGetHasShownClockInstructions_ReturnsSetValue()
        {
            // Act
            _settingsService.SetHasShownClockInstructions(true);
            var result = _settingsService.GetHasShownClockInstructions();

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void GetCentreText_DefaultValue_ReturnsCentre()
        {
            // Act
            var result = _settingsService.GetCentreText();

            // Assert
            Assert.That(result, Is.EqualTo("Centre"));
        }

        [Test]
        public void SetCentreText_ThenGetCentreText_ReturnsSetValue()
        {
            // Arrange
            const string centreText = "Custom Centre Text";

            // Act
            _settingsService.SetCentreText(centreText);
            var result = _settingsService.GetCentreText();

            // Assert
            Assert.That(result, Is.EqualTo(centreText));
        }

        #endregion

        #region File Operations

        [Test]
        public async Task LoadSettings_AfterSaveSettings_LoadsCorrectValues()
        {
            // Arrange
            const int testThemeIndex = 1;
            _settingsService.SetTheme(testThemeIndex);

            // Create a new service instance to force loading from file
            var newService = new TestLocalSettingsService(_testFilePath);

            // Act
            await newService.LoadSettings();
            var loadedTheme = newService.GetTheme();

            // Assert
            Assert.That(loadedTheme, Is.EqualTo(testThemeIndex));
        }

        [Test]
        public void LoadSettingsSync_AfterSaveSettings_LoadsCorrectValues()
        {
            // Arrange
            const int testSoundIndex = 2;
            _settingsService.SetTimerSound(testSoundIndex);

            // Create a new service instance to force loading from file
            var newService = new TestLocalSettingsService(_testFilePath);

            // Act
            newService.LoadSettingsSync();
            var loadedSound = newService.GetTimerSound();

            // Assert
            Assert.That(loadedSound, Is.EqualTo(testSoundIndex));
        }

        [Test]
        public void SaveSettings_WithMultipleValues_SavesAllValues()
        {
            // Arrange
            const int themeIndex = 2;
            const int soundIndex = 3;
            const TimerFinishBehavior behavior = TimerFinishBehavior.StayAtZero;
            var position = new WindowPosition(100, 200, 800, 600, 1);

            // Act
            _settingsService.SetTheme(themeIndex);
            _settingsService.SetTimerSound(soundIndex);
            _settingsService.SetTimerFinishBehavior(behavior);
            _settingsService.SetLastWindowPosition(position);

            // Create a new service instance to force loading from file
            var newService = new TestLocalSettingsService(_testFilePath);
            newService.LoadSettingsSync();

            // Assert
            Assert.That(newService.GetTheme(), Is.EqualTo(themeIndex));
            Assert.That(newService.GetTimerSound(), Is.EqualTo(soundIndex));
            Assert.That(newService.GetTimerFinishBehavior(), Is.EqualTo(behavior));

            var loadedPosition = newService.GetLastWindowPosition();
            Assert.That(loadedPosition.X, Is.EqualTo(position.X));
            Assert.That(loadedPosition.Y, Is.EqualTo(position.Y));
            Assert.That(loadedPosition.Width, Is.EqualTo(position.Width));
            Assert.That(loadedPosition.Height, Is.EqualTo(position.Height));
        }

        #endregion

        #region Error Handling

        [Test]
        public async Task LoadSettings_WithCorruptJsonFile_RecoversGracefully()
        {
            // Arrange - create an invalid JSON file
            await File.WriteAllTextAsync(_testFilePath, "This is not valid JSON");

            // Create a new service instance
            var newService = new TestLocalSettingsService(_testFilePath);

            // Act & Assert - should not throw an exception
            Assert.DoesNotThrowAsync(async () => await newService.LoadSettings());

            // Verify defaults are used
            Assert.That(newService.GetTheme(), Is.EqualTo(0));
            Assert.That(newService.GetSavedIntervalConfigs(), Is.Empty);
        }

        [Test]
        public void LoadSettingsSync_WithCorruptJsonFile_RecoversGracefully()
        {
            // Arrange - create an invalid JSON file
            File.WriteAllText(_testFilePath, "This is not valid JSON");

            // Create a new service instance
            var newService = new TestLocalSettingsService(_testFilePath);

            // Act & Assert - should not throw an exception
            Assert.DoesNotThrow(() => newService.LoadSettingsSync());

            // Verify defaults are used
            Assert.That(newService.GetTimerSound(), Is.EqualTo(0));
            Assert.That(newService.GetSavedCustomTimerConfigs(), Is.Empty);
        }

        [Test]
        public void SaveSettings_WithReadOnlyFile_HandlesExceptionGracefully()
        {
            // Skip this test if we can't make the file read-only
            // This test may not work on all systems, especially if running as admin
            File.WriteAllText(_testFilePath, "{}");

            try
            {
                // Try to make the file read-only
                File.SetAttributes(_testFilePath, FileAttributes.ReadOnly);

                // Act & Assert - should not throw an exception
                Assert.DoesNotThrow(() => _settingsService.SetTheme(1));
            }
            catch (UnauthorizedAccessException)
            {
                // Skip the test if we can't set the file to read-only
                Assert.Ignore("Cannot set file to read-only for this test");
            }
            finally
            {
                // Clean up
                try
                {
                    File.SetAttributes(_testFilePath, FileAttributes.Normal);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// A testable version of LocalSettingsService that allows us to specify the file path
    /// </summary>
    public class TestLocalSettingsService : LocalSettingsService
    {
        private readonly string _testFilePath;

        public TestLocalSettingsService(string testFilePath)
        {
            _testFilePath = testFilePath;
        }

        // Override the file path property to use our test path
        protected override string FilePath => _testFilePath;
    }
}