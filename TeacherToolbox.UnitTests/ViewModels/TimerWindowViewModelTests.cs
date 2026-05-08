using System;
using System.Collections.Generic;
using Microsoft.UI;
using Moq;
using NUnit.Framework;
using TeacherToolbox.Model;
using TeacherToolbox.Services;
using TeacherToolbox.ViewModels;
using Windows.UI;

namespace TeacherToolbox.UnitTests.ViewModels
{
    [TestFixture]
    public class TimerWindowViewModelTests
    {
        private Mock<ISettingsService> _mockSettingsService;
        private Mock<IThemeService> _mockThemeService;
        private Mock<ITimerService> _mockTimerService;
        private bool _timerEnabled;
        private TimerWindowViewModel _viewModel;

        [SetUp]
        public void Setup()
        {
            _timerEnabled = false;
            _mockSettingsService = new Mock<ISettingsService>();
            _mockThemeService = new Mock<IThemeService>();
            _mockTimerService = new Mock<ITimerService>();

            _mockSettingsService.Setup(s => s.GetTimerSound()).Returns(0);
            _mockSettingsService.Setup(s => s.GetTimerFinishBehavior()).Returns(TimerFinishBehavior.CloseTimer);
            _mockSettingsService.Setup(s => s.GetSavedIntervalConfigs()).Returns(new List<SavedIntervalConfig>());
            _mockSettingsService.Setup(s => s.GetSavedCustomTimerConfigs()).Returns(new List<SavedIntervalConfig>());
            _mockThemeService.Setup(s => s.IsDarkTheme).Returns(false);
            _mockTimerService.SetupProperty(t => t.Interval);
            _mockTimerService.SetupGet(t => t.IsEnabled).Returns(() => _timerEnabled);
            _mockTimerService.Setup(t => t.Start()).Callback(() => _timerEnabled = true);
            _mockTimerService.Setup(t => t.Stop()).Callback(() => _timerEnabled = false);
        }

        [TearDown]
        public void Cleanup()
        {
            _viewModel?.Dispose();
        }

        [Test]
        public void Constructor_WithNullDependencies_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>((Action)(() => new TimerWindowViewModel(null, 30, _mockThemeService.Object, _mockTimerService.Object)));
            Assert.Throws<ArgumentNullException>((Action)(() => new TimerWindowViewModel(_mockSettingsService.Object, 30, null, _mockTimerService.Object)));
            Assert.Throws<ArgumentNullException>((Action)(() => new TimerWindowViewModel(_mockSettingsService.Object, 30, _mockThemeService.Object, null)));
        }

        [Test]
        public void Constructor_WithDirectTimer_StartsTimerAndFormatsSeconds()
        {
            _viewModel = CreateViewModel(30);

            Assert.That(_viewModel.TimerText, Is.EqualTo("30"));
            Assert.That(_viewModel.TimerGaugeValue, Is.EqualTo(30));
            Assert.That(_viewModel.TimerGaugeMaximum, Is.EqualTo(60));
            Assert.That(_viewModel.IsTimerGaugeVisible, Is.True);
            Assert.That(_viewModel.IsTimerSetupVisible, Is.False);
            _mockTimerService.Verify(t => t.Start(), Times.Once);
        }

        [Test]
        public void Constructor_WithMinuteTimer_FormatsMinutesAndSeconds()
        {
            _viewModel = CreateViewModel(90);

            Assert.That(_viewModel.TimerText, Is.EqualTo("1:30"));
            Assert.That(_viewModel.TimerGaugeMaximum, Is.EqualTo(90));
            Assert.That(_viewModel.TimerGaugeTickSpacing, Is.EqualTo(30));
        }

        [Test]
        public void Constructor_WithHourTimer_FormatsHoursMinutesAndSeconds()
        {
            _viewModel = CreateViewModel(3661);

            Assert.That(_viewModel.TimerText, Is.EqualTo("1:01:01"));
            Assert.That(_viewModel.TimerGaugeTickSpacing, Is.EqualTo(366));
        }

        [Test]
        public void TimerTick_DecrementsTextAndGauge()
        {
            _viewModel = CreateViewModel(30);

            RaiseTick();

            Assert.That(_viewModel.TimerText, Is.EqualTo("29"));
            Assert.That(_viewModel.TimerGaugeValue, Is.EqualTo(29));
        }

        [Test]
        public void TimerTick_WhenTimerReachesZero_RaisesFinishedForCloseBehavior()
        {
            _viewModel = CreateViewModel(1);
            var eventRaised = false;
            _viewModel.TimerFinished += (_, _) => eventRaised = true;

            RaiseTick();

            Assert.That(eventRaised, Is.True);
            Assert.That(_viewModel.TimerTextColor, Is.EqualTo(Colors.Red));
            _mockTimerService.Verify(t => t.Stop(), Times.Once);
        }

        [Test]
        public void TimerTick_WithStayAtZeroBehavior_StopsWithoutFinishedEvent()
        {
            _mockSettingsService.Setup(s => s.GetTimerFinishBehavior()).Returns(TimerFinishBehavior.StayAtZero);
            _viewModel = CreateViewModel(1);
            var eventRaised = false;
            _viewModel.TimerFinished += (_, _) => eventRaised = true;

            RaiseTick();

            Assert.That(eventRaised, Is.False);
            Assert.That(_viewModel.TimerText, Is.EqualTo("0"));
            Assert.That(_viewModel.TimerTextColor, Is.EqualTo(Colors.Red));
            _mockTimerService.Verify(t => t.Stop(), Times.Once);
        }

        [Test]
        public void TimerTick_WithCountUpBehavior_AllowsNegativeDisplayAndRedText()
        {
            _mockSettingsService.Setup(s => s.GetTimerFinishBehavior()).Returns(TimerFinishBehavior.CountUp);
            _viewModel = CreateViewModel(1);

            RaiseTick();
            RaiseTick();

            Assert.That(_viewModel.TimerText, Is.EqualTo("1"));
            Assert.That(_viewModel.TimerTextColor, Is.EqualTo(Colors.Red));
            _mockTimerService.Verify(t => t.Stop(), Times.Never);
        }

        [Test]
        public void PauseResumeTimerCommand_WhenRunning_PausesTimer()
        {
            _viewModel = CreateViewModel(30);

            _viewModel.PauseResumeTimerCommand.Execute(null);

            Assert.That(_viewModel.IsPaused, Is.True);
            Assert.That(_viewModel.TrailColor, Is.EqualTo(Colors.DarkGray));
            _mockTimerService.Verify(t => t.Stop(), Times.Once);
        }

        [Test]
        public void PauseResumeTimerCommand_WhenPaused_ResumesTimer()
        {
            _viewModel = CreateViewModel(30);
            _viewModel.PauseResumeTimerCommand.Execute(null);

            _viewModel.PauseResumeTimerCommand.Execute(null);

            Assert.That(_viewModel.IsPaused, Is.False);
            Assert.That(_viewModel.TrailColor, Is.EqualTo(Color.FromArgb(255, 0x5b, 0x34, 0x93)));
            _mockTimerService.Verify(t => t.Start(), Times.Exactly(2));
        }

        [Test]
        public void Constructor_WithCustomTimer_ShowsSetupAndDefaultInterval()
        {
            _viewModel = CreateViewModel(0);

            Assert.That(_viewModel.IsTimerSetupVisible, Is.True);
            Assert.That(_viewModel.IsTimerGaugeVisible, Is.False);
            Assert.That(_viewModel.IsAddIntervalButtonVisible, Is.False);
            Assert.That(_viewModel.IntervalsList.Count, Is.EqualTo(1));
            Assert.That(_viewModel.IntervalsList[0].ShowRemoveButton, Is.False);
        }

        [Test]
        public void Constructor_WithSavedCustomTimer_LoadsSavedConfiguration()
        {
            _mockSettingsService.Setup(s => s.GetSavedCustomTimerConfigs()).Returns(new List<SavedIntervalConfig>
            {
                new(0, 2, 5)
            });

            _viewModel = CreateViewModel(0);

            Assert.That(_viewModel.IntervalsList.Count, Is.EqualTo(1));
            Assert.That(_viewModel.IntervalsList[0].Minutes, Is.EqualTo(2));
            Assert.That(_viewModel.IntervalsList[0].Seconds, Is.EqualTo(5));
        }

        [Test]
        public void AddIntervalCommand_AddsIntervalsUpToEight()
        {
            _viewModel = CreateViewModel(-1);

            for (int i = 0; i < 10; i++)
            {
                _viewModel.AddIntervalCommand.Execute(null);
            }

            Assert.That(_viewModel.IntervalsList.Count, Is.EqualTo(8));
            Assert.That(_viewModel.CanAddInterval, Is.False);
            Assert.That(_viewModel.AddIntervalCommand.CanExecute(null), Is.False);
        }

        [Test]
        public void RemoveIntervalCommand_RemovesIntervalAndRenumbersRemainingItems()
        {
            _viewModel = CreateViewModel(-1);
            _viewModel.AddIntervalCommand.Execute(null);
            _viewModel.AddIntervalCommand.Execute(null);

            _viewModel.RemoveIntervalCommand.Execute(_viewModel.IntervalsList[1]);

            Assert.That(_viewModel.IntervalsList.Count, Is.EqualTo(2));
            Assert.That(_viewModel.IntervalsList[0].IntervalNumber, Is.EqualTo(1));
            Assert.That(_viewModel.IntervalsList[0].ShowRemoveButton, Is.False);
            Assert.That(_viewModel.IntervalsList[1].IntervalNumber, Is.EqualTo(2));
            Assert.That(_viewModel.IntervalsList[1].ShowRemoveButton, Is.True);
        }

        [Test]
        public void StartTimerCommand_ForCustomTimer_SavesConfigurationAndStartsTimer()
        {
            _viewModel = CreateViewModel(0);
            _viewModel.IntervalsList[0].Minutes = 1;
            _viewModel.IntervalsList[0].Seconds = 15;

            _viewModel.StartTimerCommand.Execute(null);

            Assert.That(_viewModel.TimerText, Is.EqualTo("1:15"));
            Assert.That(_viewModel.IsTimerSetupVisible, Is.False);
            Assert.That(_viewModel.IsTimerGaugeVisible, Is.True);
            _mockSettingsService.Verify(s => s.SaveCustomTimerConfigs(It.Is<List<SavedIntervalConfig>>(configs =>
                configs.Count == 1 && configs[0].Minutes == 1 && configs[0].Seconds == 15)), Times.Once);
        }

        [Test]
        public void StartTimerCommand_ForIntervalTimer_RunsIntervalsInSequence()
        {
            _viewModel = CreateViewModel(-1);
            _viewModel.IntervalsList[0].Seconds = 1;
            _viewModel.AddIntervalCommand.Execute(null);
            _viewModel.IntervalsList[1].Seconds = 2;

            _viewModel.StartTimerCommand.Execute(null);
            RaiseTick();

            Assert.That(_viewModel.TimerText, Is.EqualTo("2"));
            Assert.That(_viewModel.IntervalInfoText, Is.EqualTo("Interval 2/2"));
            Assert.That(_viewModel.IsIntervalInfoVisible, Is.True);
            _mockSettingsService.Verify(s => s.SaveIntervalConfigs(It.Is<List<SavedIntervalConfig>>(configs => configs.Count == 2)), Times.Once);
        }

        [Test]
        public void Dispose_UnsubscribesAndStopsTimer()
        {
            _viewModel = CreateViewModel(30);

            _viewModel.Dispose();

            _mockTimerService.VerifyRemove(t => t.Tick -= It.IsAny<EventHandler<object>>(), Times.AtLeastOnce);
            _mockTimerService.Verify(t => t.Stop(), Times.Once);
        }

        private TimerWindowViewModel CreateViewModel(int seconds)
        {
            return new TimerWindowViewModel(
                _mockSettingsService.Object,
                seconds,
                _mockThemeService.Object,
                _mockTimerService.Object);
        }

        private void RaiseTick()
        {
            _mockTimerService.Raise(t => t.Tick += null, _mockTimerService.Object, EventArgs.Empty);
        }
    }
}
