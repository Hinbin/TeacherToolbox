using System;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using NUnit.Framework;

namespace TeacherToolbox.IntegrationTests.IntegrationTests
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.SingleInstance)]
    public class TimerTests : TestBase
    {
        [OneTimeSetUp]
        public void ClassSetUp() => EnableSharedLaunch();

        [OneTimeTearDown]
        public void ClassTearDown() => TearDownSharedLaunch();

        private Window? _timerWindow;

        [TearDown]
        public void TimerTearDown()
        {
            if (_timerWindow == null)
            {
                return;
            }

            try
            {
                _timerWindow.Close();
            }
            catch
            {
                return;
            }

            WaitUntilCondition(
                () =>
                {
                    try
                    {
                        return _timerWindow.IsOffscreen || _timerWindow.Properties.IsOffscreen;
                    }
                    catch
                    {
                        return true;
                    }
                },
                "Timer window should close",
                TimeSpan.FromSeconds(3));
        }

        [Test]
        public void TimerWindow_OpensWithFixedTime()
        {
            OpenTimerWindow("Timer30Button");

            var timerText = WaitUntilFound(
                () => _timerWindow!.FindFirstDescendant(cf => cf.ByAutomationId("timerText")),
                "Timer text should be present");
            var timerGauge = WaitUntilFound(
                () => _timerWindow!.FindFirstDescendant(cf => cf.ByAutomationId("timerGauge")),
                "Timer gauge should be present");

            Assert.Multiple((Action)(() =>
            {
                Assert.That(GetDisplayedText(timerText), Is.EqualTo("30").Or.EqualTo("29"));
                Assert.That(timerGauge.IsOffscreen, Is.False);
            }));
        }

        [Test]
        public void TimerWindow_OpensWithIntervalSelector()
        {
            OpenTimerWindow("TimerIntervalButton");

            var intervalsListView = WaitUntilFound(
                () => _timerWindow!.FindFirstDescendant(cf => cf.ByAutomationId("intervalsListView")),
                "Interval timer selector should be present");
            var addIntervalButton = WaitUntilFound(
                () => _timerWindow!.FindFirstDescendant(cf => cf.ByAutomationId("addIntervalButton")),
                "Add interval button should be present");

            Assert.Multiple((Action)(() =>
            {
                Assert.That(intervalsListView.IsOffscreen, Is.False);
                Assert.That(addIntervalButton.IsOffscreen, Is.False);
            }));
        }

        private void OpenTimerWindow(string buttonAutomationId)
        {
            NavigateToPage("Timer");
            var timerPage = VerifyPageLoaded("TimerSelectionPage");
            var timerButton = WaitUntilFound(
                () => timerPage.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByAutomationId(buttonAutomationId))),
                $"Timer button '{buttonAutomationId}' should exist");

            SafeClick(timerButton);

            _timerWindow = WaitUntilFound(
                () => FindTimerWindow(),
                "Timer window should open",
                TimeSpan.FromSeconds(5));
        }
    }
}
