using FlaUI.UIA3;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Conditions;
using System.Threading;
using TeacherToolbox;
using TTBIntegrationTesting.Integration_Tests;
using System.Linq;

namespace TTBIntegrationTesting
{
    [TestFixture]
    public class SettingsTests : TestBase
    {
        private AutomationElement? _settingsPage;

        [SetUp]
        public void SettingsSetUp()
        {
            EnsureNavigationIsOpen();
            NavigateToPage("Settings");
            _settingsPage = VerifyPageLoaded("SettingsPage");

            Assert.That(_settingsPage, Is.Not.Null, "Settings page should be loaded");
        }

        [Test]
        public void TimerSound_ComboBoxExists()
        {
            var timerSoundComboBox = _settingsPage!.FindFirstDescendant(cf =>
                cf.ByAutomationId("TimerSoundComboBox"));

            Assert.That(timerSoundComboBox, Is.Not.Null, "Timer sound combo box should exist");
            Assert.That(timerSoundComboBox?.ControlType, Is.EqualTo(ControlType.ComboBox),
                "Timer sound selector should be a combo box");
        }

        [Test]
        public void TimerSound_HasDefaultOption()
        {
            var timerSoundComboBox = _settingsPage!.FindFirstDescendant(cf =>
                cf.ByAutomationId("TimerSoundComboBox"));

            // Get the selection pattern to check the selected item
            var selectionPattern = timerSoundComboBox.Patterns.Selection.Pattern;
            var selectedItem = selectionPattern.Selection.ValueOrDefault;

            Assert.That(selectedItem, Is.Not.Null, "Should have a default selection");
            Assert.That(selectedItem?.FirstOrDefault()?.Name, Is.Not.Empty, "Default selection should have a name");

        }

        [Test]
        public void TimerSound_CanExpandComboBox()
        {
            var timerSoundComboBox = _settingsPage!.FindFirstDescendant(cf =>
                cf.ByAutomationId("TimerSoundComboBox"));

            // Get the expand/collapse pattern
            var expandPattern = timerSoundComboBox.Patterns.ExpandCollapse.Pattern;

            // Initially should be collapsed
            Assert.That(expandPattern.ExpandCollapseState, Is.EqualTo(ExpandCollapseState.Collapsed),
                "Combo box should start collapsed");

            // Expand the combo box
            expandPattern.Expand();
            Thread.Sleep(500); // Wait for animation

            Assert.That(expandPattern.ExpandCollapseState, Is.EqualTo(ExpandCollapseState.Expanded),
                "Combo box should be expanded after clicking");
        }

        [Test]
        public void TimerSound_CanSelectDifferentSound()
        {
            var timerSoundComboBox = _settingsPage!.FindFirstDescendant(cf =>
                cf.ByAutomationId("TimerSoundComboBox"));

            // Get initial selection
            var selectionPattern = timerSoundComboBox.Patterns.Selection.Pattern;
            var initialSelection = selectionPattern.Selection.ValueOrDefault?.FirstOrDefault()?.Name;

            // Expand the combo box
            timerSoundComboBox.Patterns.ExpandCollapse.Pattern.Expand();
            Thread.Sleep(500); // Wait for animation

            // Find all items in the combo box
            var items = timerSoundComboBox.FindAllDescendants(cf =>
                cf.ByControlType(ControlType.ListItem));

            Assert.That(items.Length, Is.GreaterThan(1), "Should have multiple sound options");

            // Find a different item than the currently selected one
            var differentItem = items.FirstOrDefault(item => item.Name != initialSelection);
            Assert.That(differentItem, Is.Not.Null, "Should find a different sound option");

            // Click the different item
            differentItem?.Click();
            Thread.Sleep(500); // Wait for selection to take effect

            // Get new selection
            var newSelection = selectionPattern.Selection.ValueOrDefault?.FirstOrDefault()?.Name;

            Assert.That(newSelection, Is.Not.EqualTo(initialSelection),
                "Selected sound should be different after changing");
        }

        [Test]
        public void TimerSound_SelectionPersistsAfterReload()
        {
            var timerSoundComboBox = _settingsPage!.FindFirstDescendant(cf =>
                cf.ByAutomationId("TimerSoundComboBox"));

            // Get initial selection
            var selectionPattern = timerSoundComboBox.Patterns.Selection.Pattern;
            var initialSelection = selectionPattern.Selection.ValueOrDefault?.FirstOrDefault()?.Name;

            // Change the selection
            timerSoundComboBox.Patterns.ExpandCollapse.Pattern.Expand();
            Thread.Sleep(500);

            var items = timerSoundComboBox.FindAllDescendants(cf =>
                cf.ByControlType(ControlType.ListItem));
            var differentItem = items.FirstOrDefault(item => item.Name != initialSelection);
            differentItem?.Click();
            Thread.Sleep(500);

            var selectedSound = selectionPattern.Selection.ValueOrDefault?.FirstOrDefault()?.Name;

            // Navigate away and back
            var navigationView = MainWindow!.FindFirstDescendant(cf =>
                cf.ByAutomationId("NavView"));
            navigationView?.FindFirstChild(cf =>
                cf.ByName("Open Navigation")).Click();

            // Navigate to a different page and back
            var otherNavItem = navigationView?.FindFirstDescendant(cf =>
                cf.ByName("Random Name Generator"));
            otherNavItem?.Click();
            Thread.Sleep(500);

            navigationView?.FindFirstChild(cf =>
                cf.ByName("Open Navigation")).Click();
            var settingsNavItem = navigationView?.FindFirstDescendant(cf =>
                cf.ByName("Settings"));
            settingsNavItem?.Click();
            Thread.Sleep(500);

            // Check if selection persisted
            timerSoundComboBox = MainWindow!.FindFirstDescendant(cf =>
                cf.ByAutomationId("TimerSoundComboBox"));
            selectionPattern = timerSoundComboBox.Patterns.Selection.Pattern;
            var persistedSelection = selectionPattern.Selection.ValueOrDefault?.FirstOrDefault()?.Name;

            Assert.That(persistedSelection, Is.EqualTo(selectedSound),
                "Selected sound should persist after navigating away and back");
        }

        [Test]
        public void ThemeComboBox_ComboBoxExists()
        {
            var themeComboBox = _settingsPage!.FindFirstDescendant(cf =>
                cf.ByAutomationId("ThemeComboBox"));

            Assert.That(themeComboBox, Is.Not.Null, "Theme combo box should exist");
            Assert.That(themeComboBox?.ControlType, Is.EqualTo(ControlType.ComboBox),
                "Theme selector should be a combo box");
        }

        [Test]
        public void ThemeComboBox_HasDefaultOption()
        {
            var themeComboBox = _settingsPage!.FindFirstDescendant(cf =>
                cf.ByAutomationId("ThemeComboBox"));

            var selectionPattern = themeComboBox.Patterns.Selection.Pattern;
            var selectedItem = selectionPattern.Selection.ValueOrDefault;

            Assert.That(selectedItem, Is.Not.Null, "Should have a default theme selection");
            Assert.That(selectedItem?.FirstOrDefault()?.Name, Is.EqualTo("Use system setting"),
                "Default theme should be 'Use system setting'");
        }

        [Test]
        public void ThemeComboBox_SelectionPersistsAfterReload()
        {
            var themeComboBox = _settingsPage!.FindFirstDescendant(cf =>
                cf.ByAutomationId("ThemeComboBox"));

            // Change the selection to "Dark"
            themeComboBox.Patterns.ExpandCollapse.Pattern.Expand();
            Thread.Sleep(500);

            var darkThemeOption = themeComboBox.FindFirstDescendant(cf =>
                cf.ByName("Dark"));
            darkThemeOption?.Click();
            Thread.Sleep(500);

            // Navigate away and back
            NavigateAwayAndBack();

            // Verify the selection persisted
            themeComboBox = MainWindow!.FindFirstDescendant(cf =>
                cf.ByAutomationId("ThemeComboBox"));
            var selectionPattern = themeComboBox.Patterns.Selection.Pattern;
            var persistedSelection = selectionPattern.Selection.ValueOrDefault?.FirstOrDefault()?.Name;

            Assert.That(persistedSelection, Is.EqualTo("Dark"),
                "Selected theme should persist after navigating away and back");
        }

        [Test]
        public void TimerSoundTestButton_Exists()
        {
            var testButton = _settingsPage!.FindFirstDescendant(cf =>
                cf.ByAutomationId("TimerSoundButton"));

            Assert.That(testButton, Is.Not.Null, "Timer sound test button should exist");
            Assert.That(testButton?.ControlType, Is.EqualTo(ControlType.Button),
                "Timer sound test control should be a button");
        }

        [Test]
        public void TimerSoundTestButton_CanClick()
        {
            var testButton = _settingsPage!.FindFirstDescendant(cf =>
                cf.ByAutomationId("TimerSoundButton"));

            Assert.That(testButton?.IsEnabled, Is.True, "Timer sound test button should be enabled");

            testButton?.Click();
            Thread.Sleep(1000); // Wait for potential sound playback

            // Note: We can't verify the sound played, but we can verify the button is clickable
            Assert.That(testButton?.IsEnabled, Is.True, "Button should remain enabled after click");
        }

        private void NavigateAwayAndBack()
        {
            var navigationView = MainWindow!.FindFirstDescendant(cf =>
                cf.ByAutomationId("NavView"));
            navigationView?.FindFirstChild(cf =>
                cf.ByName("Open Navigation")).Click();

            var otherNavItem = navigationView?.FindFirstDescendant(cf =>
                cf.ByName("Random Name Generator"));
            otherNavItem?.Click();
            Thread.Sleep(500);

            navigationView?.FindFirstChild(cf =>
                cf.ByName("Open Navigation")).Click();
            var settingsNavItem = navigationView?.FindFirstDescendant(cf =>
                cf.ByName("Settings"));
            settingsNavItem?.Click();
            Thread.Sleep(500);
        }
    }

}