using System;
using FlaUI.Core.AutomationElements;
using NUnit.Framework;

namespace TeacherToolbox.IntegrationTests.IntegrationTests
{
    [TestFixture]
    public class ClockTests : TestBase
    {
        [Test]
        public void Clock_PageLoadsAndShowsDigitalTime()
        {
            NavigateToPage("Exam Clock");
            var clockPage = VerifyPageLoaded("Clock");

            var timeDisplay = WaitUntilFound(
                () => clockPage.FindFirstDescendant(cf => cf.ByAutomationId("digitalTimeTextBlock")),
                "Digital clock time should be present");

            WaitUntilCondition(
                () => !string.IsNullOrEmpty(timeDisplay.AsTextBox().Text),
                "Digital clock should be populated",
                TimeSpan.FromSeconds(5));

            Assert.That(timeDisplay.AsTextBox().Text, Does.Match(@"\d+:\d+"));
        }
    }
}
