using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Moq;
using TeacherToolbox.Model;
using TeacherToolbox.ViewModels;
using TeacherToolbox.Services;
using TeacherToolbox.Helpers;
using Windows.Media.Playback;
using Windows.Media.Core;

namespace TeacherToolbox.UnitTests.ViewModels
{
    [TestFixture]
    public class SettingsViewModelTests
    {
        private Mock<ISettingsService> _mockSettingsService;
        private Mock<ITelemetryService> _mockTelemetry;
        private Mock<IFilePickerService> _mockFilePicker;
        private Mock<IWindowService> _mockWindowService;
        private Mock<IUriLauncherService> _mockUriLauncher;
        private SettingsViewModel _viewModel;

        [SetUp]
        public void Setup()
        {
            // Setup mock services
            _mockSettingsService = new Mock<ISettingsService>();
            _mockTelemetry = new Mock<ITelemetryService>();
            _mockFilePicker = new Mock<IFilePickerService>();
            _mockWindowService = new Mock<IWindowService>();
            _mockUriLauncher = new Mock<IUriLauncherService>();

            // Default settings service return values
            _mockSettingsService.Setup(s => s.GetTheme()).Returns(0); // System
            _mockSettingsService.Setup(s => s.GetTimerSound()).Returns(0);
            _mockSettingsService.Setup(s => s.GetTimerFinishBehavior()).Returns(TimerFinishBehavior.CloseTimer);
            _mockUriLauncher.Setup(s => s.LaunchUriAsync(It.IsAny<Uri>())).ReturnsAsync(true);

            // Create the view model with the mock services
            _viewModel = CreateViewModel();
        }

        [TearDown]
        public void Cleanup()
        {
            // Make sure to dispose the ViewModel to clean up resources
            _viewModel.Dispose();
        }

        private SettingsViewModel CreateViewModel()
        {
            return new SettingsViewModel(
                _mockSettingsService.Object,
                _mockTelemetry.Object,
                _mockFilePicker.Object,
                _mockWindowService.Object,
                _mockUriLauncher.Object);
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
            var viewModel = CreateViewModel();

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
            Assert.Throws<ArgumentNullException>((Action)(() => new SettingsViewModel(null, _mockTelemetry.Object, _mockFilePicker.Object, _mockWindowService.Object, _mockUriLauncher.Object)));
            Assert.Throws<ArgumentNullException>((Action)(() => new SettingsViewModel(_mockSettingsService.Object, null, _mockFilePicker.Object, _mockWindowService.Object, _mockUriLauncher.Object)));
            Assert.Throws<ArgumentNullException>((Action)(() => new SettingsViewModel(_mockSettingsService.Object, _mockTelemetry.Object, null, _mockWindowService.Object, _mockUriLauncher.Object)));
            Assert.Throws<ArgumentNullException>((Action)(() => new SettingsViewModel(_mockSettingsService.Object, _mockTelemetry.Object, _mockFilePicker.Object, null, _mockUriLauncher.Object)));
            Assert.Throws<ArgumentNullException>((Action)(() => new SettingsViewModel(_mockSettingsService.Object, _mockTelemetry.Object, _mockFilePicker.Object, _mockWindowService.Object, null)));
        }

        [Test]
        public void Constructor_InitializesSoundOptions()
        {
            // Act & Assert
            Assert.That(_viewModel.SoundOptions, Is.Not.Null);
            Assert.That(_viewModel.SoundOptions.Count, Is.GreaterThan(0));
        }

        #endregion

        #region Theme Tests

        [Test]
        public void SelectedThemeIndex_WhenSet_UpdatesServiceAndRaisesEvent()
        {
            // Arrange
            bool eventRaised = false;
            int capturedThemeIndex = -1;
            _viewModel.ThemeChanged += (index) => {
                eventRaised = true;
                capturedThemeIndex = index;
            };

            // Act
            _viewModel.SelectedThemeIndex = 2; // Dark

            // Assert
            _mockSettingsService.Verify(s => s.SetTheme(2), Times.Once);
            Assert.That(eventRaised, Is.True);
            Assert.That(capturedThemeIndex, Is.EqualTo(2)); // 2 = Dark
        }

        [Test]
        public void SelectedThemeIndex_WhenSetToSameValue_DoesNotUpdateOrRaiseEvent()
        {
            // Arrange
            bool eventRaised = false;
            _viewModel.ThemeChanged += (index) => { eventRaised = true; };

            // Initialize with value 0
            _mockSettingsService.Setup(s => s.GetTheme()).Returns(0);
            _viewModel = CreateViewModel();

            // Reset verification counts
            _mockSettingsService.Invocations.Clear();

            // Act
            _viewModel.SelectedThemeIndex = 0; // Already set to 0

            // Assert
            _mockSettingsService.Verify(s => s.SetTheme(It.IsAny<int>()), Times.Never);
            Assert.That(eventRaised, Is.False);
        }

        [Test]
        public void UpdateAppTheme_SystemTheme_RaisesIndexZero()
        {
            // Arrange - start with a different theme value
            _mockSettingsService.Setup(s => s.GetTheme()).Returns(1); // Light theme
            var viewModel = CreateViewModel();

            int capturedIndex = -1;
            viewModel.ThemeChanged += (index) => { capturedIndex = index; };

            // Act
            viewModel.SelectedThemeIndex = 0; // Change to System (Default)

            // Assert
            Assert.That(capturedIndex, Is.EqualTo(0)); // 0 = Default/System
        }

        [Test]
        public void UpdateAppTheme_LightTheme_RaisesIndexOne()
        {
            // Arrange
            int capturedIndex = -1;
            _viewModel.ThemeChanged += (index) => { capturedIndex = index; };

            // Act
            _viewModel.SelectedThemeIndex = 1; // Light

            // Assert
            Assert.That(capturedIndex, Is.EqualTo(1)); // 1 = Light
        }

        [Test]
        public void UpdateAppTheme_DarkTheme_RaisesIndexTwo()
        {
            // Arrange
            int capturedIndex = -1;
            _viewModel.ThemeChanged += (index) => { capturedIndex = index; };

            // Act
            _viewModel.SelectedThemeIndex = 2; // Dark

            // Assert
            Assert.That(capturedIndex, Is.EqualTo(2)); // 2 = Dark
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
            _viewModel = CreateViewModel();

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
            _viewModel = CreateViewModel();

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
            Assert.DoesNotThrow((Action)(() => _viewModel.TestSoundCommand.Execute(null)));

            // No further assertion needed if no exception is thrown
            // In a real test, you might use a mock media player or a test-specific sound service
        }

        [Test]
        public async Task SendFeedbackCommand_WhenExecuted_LaunchesUri()
        {
            // Act
            await _viewModel.SendFeedbackCommand.ExecuteAsync(null);

            // Assert
            _mockUriLauncher.Verify(
                s => s.LaunchUriAsync(It.Is<Uri>(uri =>
                    uri.ToString() == "https://docs.google.com/forms/d/e/1FAIpQLScAKZmB6CN7jBhIiZ7E25Vn_80yPTEUWBTNV4ZMJQEeXrF42g/viewform")),
                Times.Once);
        }

        [Test]
        public async Task ViewFeedbackCommand_WhenExecuted_LaunchesUri()
        {
            // Act
            await _viewModel.ViewFeedbackCommand.ExecuteAsync(null);

            // Assert
            _mockUriLauncher.Verify(
                s => s.LaunchUriAsync(It.Is<Uri>(uri =>
                    uri.ToString() == "https://docs.google.com/spreadsheets/d/1fdZeVxytN2yPmk5jKqhIFK6_U_6s66v6A4w2uh_SBxA/edit?gid=0#gid=0")),
                Times.Once);
        }

        #endregion
    }
}
