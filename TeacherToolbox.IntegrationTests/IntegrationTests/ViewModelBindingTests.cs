using System;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using NUnit.Framework;

namespace TeacherToolbox.IntegrationTests.IntegrationTests
{
    [TestFixture]
    public class ViewModelBindingTests : TestBase
    {
        [Test]
        public void Clock_ViewModelBinding_PopulatesTimeDisplay()
        {
            NavigateToPage("Exam Clock");
            var clockPage = VerifyPageLoaded("Clock");

            var timeDisplay = WaitUntilFound(
                () => clockPage.FindFirstDescendant(cf => cf.ByAutomationId("digitalTimeTextBlock")),
                "Digital time TextBlock should be present");

            WaitUntilCondition(
                () => !string.IsNullOrEmpty(timeDisplay.AsTextBox().Text),
                "Clock time should be populated by the ViewModel",
                TimeSpan.FromSeconds(5));

            Assert.That(timeDisplay.AsTextBox().Text, Does.Match(@"\d+:\d+"));
        }

        [Test]
        public void Settings_ViewModelBinding_PopulatesSoundOptions()
        {
            NavigateToPage("Settings");
            var settingsPage = VerifyPageLoaded("SettingsPage");

            var comboBox = WaitUntilFound(
                () => settingsPage.FindFirstDescendant(cf => cf.ByAutomationId("TimerSoundComboBox")),
                "TimerSoundComboBox should be present");

            ScrollElementIntoView(comboBox);
            comboBox.Patterns.ExpandCollapse.Pattern.Expand();

            var items = WaitUntilFound(
                () =>
                {
                    var found = comboBox.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem));
                    return found.Length > 0 ? found : null;
                },
                "TimerSoundComboBox should have ViewModel-backed items");

            Assert.That(items.Length, Is.GreaterThan(0));
            comboBox.Patterns.ExpandCollapse.Pattern.Collapse();
        }
    }
}
