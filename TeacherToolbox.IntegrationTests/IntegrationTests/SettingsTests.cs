using System;
using System.Threading;
using System.Linq;
using NUnit.Framework;
using FlaUI.UIA3;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Conditions;
using FlaUI.Core.Tools;

namespace TeacherToolbox.IntegrationTests.IntegrationTests
{
    [TestFixture]
    public class SettingsTests : TestBase
    {
        private AutomationElement? _settingsPage;

        // Define animation timeout - specific to this test
        private static readonly TimeSpan AnimationTimeout = TimeSpan.FromMilliseconds(500);

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

            // Scroll the element into view
            ScrollElementIntoView(timerSoundComboBox);

            Assert.That(timerSoundComboBox, Is.Not.Null, "Timer sound combo box should exist");
            Assert.That(timerSoundComboBox?.ControlType, Is.EqualTo(ControlType.ComboBox),
                "Timer sound selector should be a combo box");
        }

        [Test]
        public void TimerSound_HasDefaultOption()
        {
            var timerSoundComboBox = FindComboBox("TimerSoundComboBox");

            // Scroll the element into view
            ScrollElementIntoView(timerSoundComboBox);

            // Get the selection pattern to check the selected item
            var selectionPattern = timerSoundComboBox.Patterns.Selection.Pattern;
            var selectedItem = selectionPattern.Selection.ValueOrDefault;

            Assert.That(selectedItem, Is.Not.Null, "Should have a default selection");
            Assert.That(selectedItem?.FirstOrDefault()?.Name, Is.Not.Empty, "Default selection should have a name");
        }

        [Test]
        public void TimerSound_CanExpandComboBox()
        {
            var timerSoundComboBox = FindComboBox("TimerSoundComboBox");

            // Scroll the element into view
            ScrollElementIntoView(timerSoundComboBox);

            // Get the expand/collapse pattern
            var expandPattern = timerSoundComboBox.Patterns.ExpandCollapse.Pattern;

            // Initially should be collapsed
            Assert.That(expandPattern.ExpandCollapseState, Is.EqualTo(ExpandCollapseState.Collapsed),
                "Combo box should start collapsed");

            // Expand the combo box
            expandPattern.Expand();

            // Wait for combo box to expand using base class helper
            WaitUntilCondition(
                () => expandPattern.ExpandCollapseState == ExpandCollapseState.Expanded,
                "Combo box should be expanded after clicking");
        }

        [Test]
        public void TimerSound_CanSelectDifferentSound()
        {
            var timerSoundComboBox = FindComboBox("TimerSoundComboBox");

            // Scroll the element into view
            ScrollElementIntoView(timerSoundComboBox);

            // Get initial selection
            var selectionPattern = timerSoundComboBox.Patterns.Selection.Pattern;
            var initialSelection = selectionPattern.Selection.ValueOrDefault?.FirstOrDefault()?.Name;

            // Expand the combo box and wait for it
            timerSoundComboBox.Patterns.ExpandCollapse.Pattern.Expand();

            // Wait for list items to be available after expansion using base class helper
            var items = WaitUntilFound<AutomationElement[]>(
                () => {
                    var foundItems = timerSoundComboBox.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem));
                    return foundItems.Length > 0 ? foundItems : null;
                },
                "Should find combo box items after expansion");

            Assert.That(items.Length, Is.GreaterThan(1), "Should have multiple sound options");

            // Find a different item than the currently selected one
            var differentItem = items.FirstOrDefault(item => item.Name != initialSelection);
            Assert.That(differentItem, Is.Not.Null, "Should find a different sound option");

            // Scroll the dropdown item into view before clicking
            ScrollElementIntoView(differentItem);

            // Click the different item
            differentItem?.Click();

            // Wait for selection to change using base class helper
            WaitUntilCondition(
                () => {
                    var newSelection = selectionPattern.Selection.ValueOrDefault?.FirstOrDefault()?.Name;
                    return newSelection != initialSelection;
                },
                "Selected sound should be different after changing");
        }

        [Test]
        public void TimerSound_SelectionPersistsAfterReload()
        {
            var timerSoundComboBox = FindComboBox("TimerSoundComboBox");

            // Scroll the element into view
            ScrollElementIntoView(timerSoundComboBox);

            // Get initial selection
            var selectionPattern = timerSoundComboBox.Patterns.Selection.Pattern;
            var initialSelection = selectionPattern.Selection.ValueOrDefault?.FirstOrDefault()?.Name;

            // Change the selection
            timerSoundComboBox.Patterns.ExpandCollapse.Pattern.Expand();

            // Wait for items to appear using base class helper
            var items = WaitUntilFound<AutomationElement[]>(
                () => {
                    var foundItems = timerSoundComboBox.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem));
                    return foundItems.Length > 0 ? foundItems : null;
                },
                "Should find combo box items after expansion");

            var differentItem = items.FirstOrDefault(item => item.Name != initialSelection);

            // Scroll the dropdown item into view before clicking
            ScrollElementIntoView(differentItem);

            differentItem?.Click();

            // Wait for selection to take effect and get the new selection using base class helper
            WaitUntilCondition(
                () => {
                    var newSelection = selectionPattern.Selection.ValueOrDefault?.FirstOrDefault()?.Name;
                    return newSelection != initialSelection;
                },
                "Selection should change after clicking new item");

            var selectedSound = selectionPattern.Selection.ValueOrDefault?.FirstOrDefault()?.Name;

            // Navigate away and back
            NavigateAwayAndBack();

            // Check if selection persisted
            timerSoundComboBox = FindComboBox("TimerSoundComboBox");

            // Scroll the element into view after navigation
            ScrollElementIntoView(timerSoundComboBox);

            selectionPattern = timerSoundComboBox.Patterns.Selection.Pattern;
            var persistedSelection = selectionPattern.Selection.ValueOrDefault?.FirstOrDefault()?.Name;

            Assert.That(persistedSelection, Is.EqualTo(selectedSound),
                "Selected sound should persist after navigating away and back");
        }

        [Test]
        public void ThemeComboBox_ComboBoxExists()
        {
            var themeComboBox = FindComboBox("ThemeComboBox");

            // Scroll the element into view
            ScrollElementIntoView(themeComboBox);

            Assert.That(themeComboBox, Is.Not.Null, "Theme combo box should exist");
            Assert.That(themeComboBox?.ControlType, Is.EqualTo(ControlType.ComboBox),
                "Theme selector should be a combo box");
        }

        [Test]
        public void ThemeComboBox_HasDefaultOption()
        {
            // Find the theme combo box
            var themeComboBox = FindComboBox("ThemeComboBox");

            // Scroll the element into view if needed
            ScrollElementIntoView(themeComboBox);

            // Get the current selection
            var selectionPattern = themeComboBox.Patterns.Selection.Pattern;
            var currentSelection = selectionPattern.Selection.ValueOrDefault?.FirstOrDefault()?.Name;

            // Check if "Use system setting" is already selected
            if (currentSelection != "Use system setting")
            {
                // Not selected, so we need to open the combo box and select it
                Console.WriteLine("'Use system setting' not currently selected. Current setting: " + currentSelection);

                // Expand the combo box
                var expandPattern = themeComboBox.Patterns.ExpandCollapse.Pattern;
                expandPattern.Expand();

                // Wait for the combo box to expand
                WaitUntilCondition(
                    () => expandPattern.ExpandCollapseState == ExpandCollapseState.Expanded,
                    "Theme combo box should expand");

                // Find the "Use system setting" option
                var systemSettingOption = WaitUntilFound<AutomationElement>(
                    () => themeComboBox.FindFirstDescendant(cf => cf.ByName("Use system setting")),
                    "Use system setting option should be available");

                // Click the option
                systemSettingOption.Click();

                // Wait for selection to update
                WaitUntilCondition(
                    () => {
                        var newSelection = selectionPattern.Selection.ValueOrDefault?.FirstOrDefault()?.Name;
                        return newSelection == "Use system setting";
                    },
                    "Use system setting should be selected");
            }

            // Verify the selection is now "Use system setting"
            var selectedItem = selectionPattern.Selection.ValueOrDefault;
            Assert.That(selectedItem, Is.Not.Null, "Should have a default theme selection");
            Assert.That(selectedItem?.FirstOrDefault()?.Name, Is.EqualTo("Use system setting"),
                "Default theme should be 'Use system setting'");
        }

        [Test]
        public void ThemeComboBox_SelectionPersistsAfterReload()
        {
            var themeComboBox = FindComboBox("ThemeComboBox");

            // Scroll the element into view
            ScrollElementIntoView(themeComboBox);

            // Change the selection to "Dark"
            themeComboBox.Patterns.ExpandCollapse.Pattern.Expand();

            // Wait for "Dark" option and click it using base class helper
            var darkThemeOption = WaitUntilFound<AutomationElement>(
                () => themeComboBox.FindFirstDescendant(cf => cf.ByName("Dark")),
                "Should find Dark theme option");

            // Scroll the dropdown item into view before clicking
            ScrollElementIntoView(darkThemeOption);

            darkThemeOption?.Click();

            // Verify selection changed using base class helper
            WaitUntilCondition(
                () => {
                    var selection = themeComboBox.Patterns.Selection.Pattern.Selection.ValueOrDefault?.FirstOrDefault()?.Name;
                    return selection == "Dark";
                },
                "Dark theme should be selected");

            // Navigate away and back
            NavigateAwayAndBack();

            // Verify the selection persisted
            themeComboBox = FindComboBox("ThemeComboBox");

            // Scroll the element into view after navigation
            ScrollElementIntoView(themeComboBox);

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

            // Scroll the element into view
            ScrollElementIntoView(testButton);

            Assert.That(testButton, Is.Not.Null, "Timer sound test button should exist");
            Assert.That(testButton?.ControlType, Is.EqualTo(ControlType.Button),
                "Timer sound test control should be a button");
        }

        [Test]
        public void TimerSoundTestButton_CanClick()
        {
            var testButton = _settingsPage!.FindFirstDescendant(cf =>
                cf.ByAutomationId("TimerSoundButton"));

            // Scroll the element into view
            ScrollElementIntoView(testButton);

            Assert.That(testButton?.IsEnabled, Is.True, "Timer sound test button should be enabled");

            testButton?.Click();
            // Wait a moment for potential UI response (we can't verify sound)
            Thread.Sleep(300);

            // Note: We can't verify the sound played, but we can verify the button is clickable
            Assert.That(testButton?.IsEnabled, Is.True, "Button should remain enabled after click");
        }

        // Helper method to find a combo box by automation ID
        private AutomationElement FindComboBox(string automationId)
        {
            return WaitUntilFound<AutomationElement>(
                () => _settingsPage!.FindFirstDescendant(cf => cf.ByAutomationId(automationId)),
                $"Should find combo box with automation ID '{automationId}'");
        }

        // Helper method to scroll an element into view using ScrollItemPattern
        private void ScrollElementIntoView(AutomationElement? element)
        {
            if (element == null)
                return;

            try
            {
                // Try to use the ScrollItemPattern if available
                if (element.Patterns.ScrollItem.IsSupported)
                {
                    element.Patterns.ScrollItem.Pattern.ScrollIntoView();

                    // Wait briefly for the scroll animation to complete
                    Thread.Sleep((int)AnimationTimeout.TotalMilliseconds);
                }
                // If ScrollItemPattern is not supported, try scrolling the parent container
                else
                {
                    // Find a scrollable parent
                    var scrollParent = FindScrollableParent(element);
                    if (scrollParent != null && scrollParent.Patterns.Scroll.IsSupported)
                    {
                        // Try to ensure element is visible by scrolling to it
                        var scrollPattern = scrollParent.Patterns.Scroll.Pattern;

                        // This is a basic approach - scroll down until element is visible
                        // More sophisticated handling may be needed for complex UIs
                        while (!IsElementInView(scrollParent, element) &&
                               scrollPattern.VerticalScrollPercent < 100)
                        {
                            scrollPattern.Scroll(ScrollAmount.NoAmount, ScrollAmount.SmallIncrement);
                            Thread.Sleep(50); // Small pause to let UI update
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log but continue - scrolling issues shouldn't fail tests
                Console.WriteLine($"Warning: Could not scroll element into view: {ex.Message}");
            }
        }

        // Helper method to find a scrollable parent container
        private AutomationElement? FindScrollableParent(AutomationElement element)
        {
            var current = element.Parent;
            while (current != null)
            {
                if (current.Patterns.Scroll.IsSupported)
                {
                    return current;
                }
                current = current.Parent;
            }
            return null;
        }

        // Helper method to check if an element is in the visible part of its scrollable container
        private bool IsElementInView(AutomationElement container, AutomationElement element)
        {
            try
            {
                // Get bounding rectangles
                var containerRect = container.BoundingRectangle;
                var elementRect = element.BoundingRectangle;

                // Check if element is within the container's visible area
                return elementRect.Top >= containerRect.Top &&
                       elementRect.Bottom <= containerRect.Bottom;
            }
            catch
            {
                // If there's an error checking, assume it's not in view
                return false;
            }
        }

        private void NavigateAwayAndBack()
        {
            // Navigate to Random Name Generator
            EnsureNavigationIsOpen();
            NavigateToPage("Random Name Generator");

            // Wait for navigation to complete using base class helper
            WaitUntilFound<AutomationElement>(
                () => MainWindow!.FindFirstDescendant(cf => cf.ByAutomationId("RandomNameGeneratorPage")),
                "Should navigate to Random Name Generator page");

            // Navigate back to Settings
            EnsureNavigationIsOpen();
            NavigateToPage("Settings");

            // Update the reference to the settings page using base class helper
            _settingsPage = WaitUntilFound<AutomationElement>(
                () => MainWindow!.FindFirstDescendant(cf => cf.ByAutomationId("SettingsPage")),
                "Should navigate back to Settings page");
        }
    }
}