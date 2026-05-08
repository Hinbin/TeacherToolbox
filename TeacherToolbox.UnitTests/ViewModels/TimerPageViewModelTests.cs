using System;
using Moq;
using NUnit.Framework;
using TeacherToolbox.Services;
using TeacherToolbox.ViewModels;

namespace TeacherToolbox.UnitTests.ViewModels
{
    [TestFixture]
    public class TimerPageViewModelTests
    {
        private Mock<ISettingsService> _mockSettingsService;
        private TimerPageViewModel _viewModel;

        [SetUp]
        public void Setup()
        {
            _mockSettingsService = new Mock<ISettingsService>();
            _viewModel = new TimerPageViewModel(_mockSettingsService.Object);
        }

        [Test]
        public void Constructor_WithNullSettingsService_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>((Action)(() => new TimerPageViewModel(null)));
        }

        [Test]
        public void Constructor_InitializesOpenTimerCommand()
        {
            Assert.That(_viewModel.OpenTimerCommand, Is.Not.Null);
        }

        [TestCase("30 secs", 30)]
        [TestCase("1 mins", 60)]
        [TestCase("2 mins", 120)]
        [TestCase("3 mins", 180)]
        [TestCase("5 mins", 300)]
        [TestCase("10 mins", 600)]
        public void OpenTimerCommand_WithPresetParameter_RequestsExpectedSeconds(string parameter, int expectedSeconds)
        {
            var requestedSeconds = int.MinValue;
            _viewModel.TimerWindowRequested += (_, seconds) => requestedSeconds = seconds;

            _viewModel.OpenTimerCommand.Execute(parameter);

            Assert.That(requestedSeconds, Is.EqualTo(expectedSeconds));
        }

        [Test]
        public void OpenTimerCommand_WithIntervalParameter_RequestsIntervalTimer()
        {
            var requestedSeconds = int.MinValue;
            _viewModel.TimerWindowRequested += (_, seconds) => requestedSeconds = seconds;

            _viewModel.OpenTimerCommand.Execute("interval");

            Assert.That(requestedSeconds, Is.EqualTo(-1));
        }

        [Test]
        public void OpenTimerCommand_WithCustomParameter_RequestsCustomTimer()
        {
            var requestedSeconds = int.MinValue;
            _viewModel.TimerWindowRequested += (_, seconds) => requestedSeconds = seconds;

            _viewModel.OpenTimerCommand.Execute("custom");

            Assert.That(requestedSeconds, Is.EqualTo(0));
        }

        [Test]
        public void OpenTimerCommand_WithCustomMinuteParameter_ParsesMinutes()
        {
            var requestedSeconds = int.MinValue;
            _viewModel.TimerWindowRequested += (_, seconds) => requestedSeconds = seconds;

            _viewModel.OpenTimerCommand.Execute("7 mins");

            Assert.That(requestedSeconds, Is.EqualTo(420));
        }

        [Test]
        public void OpenTimerCommand_WithCustomSecondParameter_ParsesSeconds()
        {
            var requestedSeconds = int.MinValue;
            _viewModel.TimerWindowRequested += (_, seconds) => requestedSeconds = seconds;

            _viewModel.OpenTimerCommand.Execute("45 secs");

            Assert.That(requestedSeconds, Is.EqualTo(45));
        }

        [Test]
        public void OpenTimerCommand_WithMixedCaseParameter_ParsesPreset()
        {
            var requestedSeconds = int.MinValue;
            _viewModel.TimerWindowRequested += (_, seconds) => requestedSeconds = seconds;

            _viewModel.OpenTimerCommand.Execute("5 MINS");

            Assert.That(requestedSeconds, Is.EqualTo(300));
        }

        [Test]
        public void OpenTimerCommand_WithUnknownParameter_StillRequestsZeroSeconds()
        {
            var requestedSeconds = int.MinValue;
            _viewModel.TimerWindowRequested += (_, seconds) => requestedSeconds = seconds;

            _viewModel.OpenTimerCommand.Execute("not a timer");

            Assert.That(requestedSeconds, Is.EqualTo(0));
        }

        [Test]
        public void OpenTimerCommand_WithNullParameter_DoesNotRequestTimer()
        {
            var eventRaised = false;
            _viewModel.TimerWindowRequested += (_, _) => eventRaised = true;

            _viewModel.OpenTimerCommand.Execute(null);

            Assert.That(eventRaised, Is.False);
        }

        [Test]
        public void OpenTimerCommand_WithEmptyParameter_DoesNotRequestTimer()
        {
            var eventRaised = false;
            _viewModel.TimerWindowRequested += (_, _) => eventRaised = true;

            _viewModel.OpenTimerCommand.Execute(string.Empty);

            Assert.That(eventRaised, Is.False);
        }
    }
}
