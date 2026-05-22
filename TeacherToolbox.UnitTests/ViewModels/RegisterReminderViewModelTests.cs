using System.Collections.Generic;
using System.Linq;
using Moq;
using NUnit.Framework;
using TeacherToolbox.Helpers;
using TeacherToolbox.Model;
using TeacherToolbox.Services;
using TeacherToolbox.ViewModels;

namespace TeacherToolbox.UnitTests.ViewModels
{
    [TestFixture]
    public class RegisterReminderViewModelTests
    {
        private Mock<ISettingsService> _settingsService;
        private Mock<IRegisterReminderService> _reminderService;
        private Mock<ITelemetryService> _telemetry;

        [SetUp]
        public void Setup()
        {
            _settingsService = new Mock<ISettingsService>();
            _reminderService = new Mock<IRegisterReminderService>();
            _telemetry = new Mock<ITelemetryService>();

            _settingsService
                .Setup(s => s.GetRegisterReminderSettings())
                .Returns(new RegisterReminderSettings
                {
                    MasterEnabled = true,
                    SnoozeMinutes = 3,
                    SoundIndex = 0,
                    Reminders = new List<RegisterReminder>()
                });
        }

        [Test]
        public void Constructor_UsesRegisterReminderSoundOptionsOnly()
        {
            var viewModel = CreateViewModel();

            Assert.That(
                viewModel.SoundOptions.Select(x => x.Name),
                Is.EqualTo(SoundSettings.RegisterReminderSoundOptions.OrderBy(x => x.Key).Select(x => x.Value.Name)));

            Assert.That(
                viewModel.SoundOptions.Select(x => x.FileName),
                Does.Not.Contain(SoundSettings.GetSoundFileName(0)));
        }

        [Test]
        public void SelectedSoundIndex_WhenChanged_SavesRegisterReminderSettings()
        {
            var viewModel = CreateViewModel();

            viewModel.SelectedSoundIndex = 3;

            _settingsService.Verify(
                s => s.SaveRegisterReminderSettings(It.Is<RegisterReminderSettings>(settings => settings.SoundIndex == 3)),
                Times.Once);
            _reminderService.Verify(
                s => s.UpdateSettings(It.Is<RegisterReminderSettings>(settings => settings.SoundIndex == 3)),
                Times.Once);
        }

        private RegisterReminderViewModel CreateViewModel()
        {
            return new RegisterReminderViewModel(
                _settingsService.Object,
                _reminderService.Object,
                _telemetry.Object);
        }
    }
}
