using System.Linq;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using NUnit.Framework;

namespace TeacherToolbox.IntegrationTests.IntegrationTests
{
    [TestFixture]
    public class SettingsTests : TestBase
    {
        [Test]
        public void TimerSound_SelectionPersistsAfterNavigationReload()
        {
            NavigateToPage("Settings");
            var settingsPage = VerifyPageLoaded("SettingsPage");
            var timerSoundComboBox = FindComboBox(settingsPage, "TimerSoundComboBox");

            var selectionPattern = timerSoundComboBox.Patterns.Selection.Pattern;
            var initialSelection = selectionPattern.Selection.ValueOrDefault?.FirstOrDefault()?.Name;

            timerSoundComboBox.Patterns.ExpandCollapse.Pattern.Expand();
            var items = WaitUntilFound(
                () =>
                {
                    var foundItems = timerSoundComboBox.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem));
                    return foundItems.Length > 1 ? foundItems : null;
                },
                "Timer sound combo box should have multiple options");

            var differentItem = items.First(item => item.Name != initialSelection);
            ScrollElementIntoView(differentItem);
            differentItem.Click();
            WaitUntilCondition(
                () => selectionPattern.Selection.ValueOrDefault?.FirstOrDefault()?.Name != initialSelection,
                "Timer sound selection should change");
            var selectedSound = selectionPattern.Selection.ValueOrDefault?.FirstOrDefault()?.Name;

            NavigateToPage("Random Name Generator");
            VerifyPageLoaded("RandomNameGeneratorPage");
            NavigateToPage("Settings");
            settingsPage = VerifyPageLoaded("SettingsPage");
            timerSoundComboBox = FindComboBox(settingsPage, "TimerSoundComboBox");
            selectionPattern = timerSoundComboBox.Patterns.Selection.Pattern;

            Assert.That(selectionPattern.Selection.ValueOrDefault?.FirstOrDefault()?.Name, Is.EqualTo(selectedSound));
        }

        private AutomationElement FindComboBox(AutomationElement settingsPage, string automationId)
        {
            var comboBox = WaitUntilFound(
                () => settingsPage.FindFirstDescendant(cf => cf.ByAutomationId(automationId)),
                $"Should find combo box '{automationId}'");
            ScrollElementIntoView(comboBox);
            return comboBox;
        }
    }
}
