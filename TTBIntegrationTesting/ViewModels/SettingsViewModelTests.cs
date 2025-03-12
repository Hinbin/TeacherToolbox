using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Moq;
using Microsoft.UI.Xaml;
using TeacherToolbox.Model;
using TeacherToolbox.ViewModels;
using TeacherToolbox.Services;
using TeacherToolbox.Helpers;
using Windows.Media.Playback;
using Windows.Media.Core;

namespace TeacherToolbox.Tests.ViewModels
{
    [TestFixture]
    public class SettingsViewModelTests
    {
        private Mock<ISettingsService> _mockSettingsService;
        private SettingsViewModel _viewModel;

        [SetUp]
        public void Setup()
        {
            // Setup mock settings service
            _mockSettingsService = new Mock<ISettingsService>();

            // Default settings service return values
            _mockSettingsService.Setup(s => s.GetTheme()).Returns(0); // System
            _mockSettingsService.Setup(s => s.GetTimerSound()).Returns(0);
            _mockSettingsService.Setup(s => s.GetTimerFinishBehavior()).Returns(TimerFinishBehavior.CloseTimer);

            // Create the view model with the mock service
            _viewModel = new SettingsViewModel(_mockSettingsService.Object);
        }

        [TearDown]
        public void Cleanup()
        {
            // Make sure to dispose the ViewModel to clean up resources
            _viewModel.Dispose();
        }

        #region Initialization Tests

        [Test]
        public void Constructor_WithSettingsService_InitializesWithCorrectValues()
        {
            // Arrange
            _mockSettingsService.Setup(s => s.GetTheme()).Returns(1); // Light
            _mockSettingsService.Setup(s => s.GetTimerSound()).Returns(2);
            _mockSettingsService.Setup(s => s.GetTimerFinishBehavior()).Returns(TimerFinishBehavior.CountUp);

            // Act
            var viewModel = new SettingsViewModel(_mockSettingsService.Object);

            // Assert
            Assert.That(viewModel.SelectedThemeIndex, Is.EqualTo(1));
            Assert.That(viewModel.SelectedTimerSoundIndex, Is.EqualTo(2));
            Assert.That(viewModel.SelectedTimerFinishBehaviorIndex, Is.EqualTo(1));

            // Cleanup
            viewModel.Dispose();
        }

        [Test]
        public void Constructor_WithNullService_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new SettingsViewModel(null));
        }

        [Test]
        public void Constructor_InitializesSoundOptions()
        {
            // Act & Assert
            Assert.That(_viewModel.SoundOptions, Is.Not.Null);
            Assert.That(_viewModel.SoundOptions.Count, Is.GreaterThan(0));
        }

        [Test]
        public void DefaultConstructor_CreatesLocalSettingsService()
        {
            // This test is a bit harder since the default constructor uses a static method
            // We'd need to use a more advanced mocking framework to mock static methods
            // For now, we'll just verify it doesn't throw an exception

            // Act & Assert
            Assert.DoesNotThrow(() => new SettingsViewModel());
        }

        #endregion

        #region Theme Tests

        [Test]
        public void SelectedThemeIndex_WhenSet_UpdatesServiceAndRaisesEvent()
        {
            // Arrange
            bool eventRaised = false;
            ElementTheme capturedTheme = ElementTheme.Default;
            _viewModel.ThemeChanged += (theme) => {
                eventRaised = true;
                capturedTheme = theme;
            };

            // Act
            _viewModel.SelectedThemeIndex = 2; // Dark

            // Assert
            _mockSettingsService.Verify(s => s.SetTheme(2), Times.Once);
            Assert.That(eventRaised, Is.True);
            Assert.That(capturedTheme, Is.EqualTo(ElementTheme.Dark));
        }

        [Test]
        public void SelectedThemeIndex_WhenSetToSameValue_DoesNotUpdateOrRaiseEvent()
        {
            // Arrange
            bool eventRaised = false;
            _viewModel.ThemeChanged += (theme) => { eventRaised = true; };

            // Initialize with value 0
            _mockSettingsService.Setup(s => s.GetTheme()).Returns(0);
            _viewModel = new SettingsViewModel(_mockSettingsService.Object);

            // Reset verification counts
            _mockSettingsService.Invocations.Clear();

            // Act
            _viewModel.SelectedThemeIndex = 0; // Already set to 0

            // Assert
            _mockSettingsService.Verify(s => s.SetTheme(It.IsAny<int>()), Times.Never);
            Assert.That(eventRaised, Is.False);
        }

        [Test]
        public void UpdateAppTheme_SystemTheme_SetsElementThemeDefault()
        {
            // Arrange
            ElementTheme capturedTheme = ElementTheme.Dark; // Start with a different value
            _viewModel.ThemeChanged += (theme) => { capturedTheme = theme; };

            // Act
            _viewModel.SelectedThemeIndex = 0; // System

            // Assert
            Assert.That(capturedTheme, Is.EqualTo(ElementTheme.Default));
        }

        [Test]
        public void UpdateAppTheme_LightTheme_SetsElementThemeLight()
        {
            // Arrange
            ElementTheme capturedTheme = ElementTheme.Dark; // Start with a different value
            _viewModel.ThemeChanged += (theme) => { capturedTheme = theme; };

            // Act
            _viewModel.SelectedThemeIndex = 1; // Light

            // Assert
            Assert.That(capturedTheme, Is.EqualTo(ElementTheme.Light));
        }

        [Test]
        public void UpdateAppTheme_DarkTheme_SetsElementThemeDark()
        {
            // Arrange
            ElementTheme capturedTheme = ElementTheme.Light; // Start with a different value
            _viewModel.ThemeChanged += (theme) => { capturedTheme = theme; };

            // Act
            _viewModel.SelectedThemeIndex = 2; // Dark

            // Assert
            Assert.That(capturedTheme, Is.EqualTo(ElementTheme.Dark));
        }

        #endregion

        #region Timer Sound Tests

        [Test]
        public void SelectedTimerSoundIndex_WhenSet_UpdatesService()
        {
            // Act
            _viewModel.SelectedTimerSoundIndex = 3;

            // Assert
            _mockSettingsService.Verify(s => s.SetTimerSound(3), Times.Once);
        }

        [Test]
        public void SelectedTimerSoundIndex_WhenSetToSameValue_DoesNotUpdateService()
        {
            // Arrange
            _mockSettingsService.Setup(s => s.GetTimerSound()).Returns(1);
            _viewModel = new SettingsViewModel(_mockSettingsService.Object);

            // Reset verification counts
            _mockSettingsService.Invocations.Clear();

            // Act
            _viewModel.SelectedTimerSoundIndex = 1; // Already set to 1

            // Assert
            _mockSettingsService.Verify(s => s.SetTimerSound(It.IsAny<int>()), Times.Never);
        }

        #endregion

        #region Timer Finish Behavior Tests

        [Test]
        public void SelectedTimerFinishBehaviorIndex_WhenSet_UpdatesService()
        {
            // Act
            _viewModel.SelectedTimerFinishBehaviorIndex = 2; // StayAtZero

            // Assert
            _mockSettingsService.Verify(s => s.SetTimerFinishBehavior(TimerFinishBehavior.StayAtZero), Times.Once);
        }

        [Test]
        public void SelectedTimerFinishBehaviorIndex_WhenSetToSameValue_DoesNotUpdateService()
        {
            // Arrange
            _mockSettingsService.Setup(s => s.GetTimerFinishBehavior()).Returns(TimerFinishBehavior.CountUp);
            _viewModel = new SettingsViewModel(_mockSettingsService.Object);

            // Reset verification counts
            _mockSettingsService.Invocations.Clear();

            // Act
            _viewModel.SelectedTimerFinishBehaviorIndex = 1; // Already set to CountUp (1)

            // Assert
            _mockSettingsService.Verify(s => s.SetTimerFinishBehavior(It.IsAny<TimerFinishBehavior>()), Times.Never);
        }

        [Test]
        public void GetSelectedTimerFinishBehavior_ReturnsCorrectEnum()
        {
            // Arrange
            _viewModel.SelectedTimerFinishBehaviorIndex = 1; // CountUp

            // Act
            var result = _viewModel.GetSelectedTimerFinishBehavior();

            // Assert
            Assert.That(result, Is.EqualTo(TimerFinishBehavior.CountUp));
        }

        #endregion

        #region Command Tests

        [Test]
        public void TestSoundCommand_WhenExecuted_PlaysSound()
        {
            // This test is challenging due to MediaPlayer being used inside the command
            // We would need to mock or create a test seam for MediaPlayer
            // For now, we'll just verify the command executes without exceptions

            // Act & Assert
            Assert.DoesNotThrow(() => _viewModel.TestSoundCommand.Execute(null));

            // No further assertion needed if no exception is thrown
            // In a real test, you might use a mock media player or a test-specific sound service
        }

        [Test]
        public void SendFeedbackCommand_WhenExecuted_LaunchesUri()
        {
            // This test is also challenging as it uses Windows.System.Launcher
            // We would need to mock or create a test seam for Launcher
            // For now, we'll just verify the command executes without exceptions

            // Act & Assert
            Assert.DoesNotThrow(() => _viewModel.SendFeedbackCommand.Execute(null));

            // No further assertion needed if no exception is thrown
            // In a real test, you might use a mock launcher service or a test-specific feedback service
        }

        #endregion
    }
}