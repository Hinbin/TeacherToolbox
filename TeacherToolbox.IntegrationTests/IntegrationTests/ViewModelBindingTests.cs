using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using NUnit.Framework;
using System;

namespace TeacherToolbox.IntegrationTests.IntegrationTests
{
    /// <summary>
    /// Verifies that the ViewModelLocator DataContext bindings introduced in Plan 04 are
    /// correctly wired on each page.  A failure here means the Locator resource wasn't
    /// resolved, causing ViewModel to be null and ViewModel-driven UI to be empty or events
    /// to throw a NullReferenceException on navigation.
    /// </summary>
    [TestFixture]
    public class ViewModelBindingTests : TestBase
    {
        [SetUp]
        public void SetUp()
        {
            WaitUntilFound<AutomationElement>(
                () => NavigationPane,
                "NavigationPane should be present");
            EnsureNavigationIsOpen();
        }

        [Test]
        public void Clock_ViewModelBinding_PopulatesTimeDisplay()
        {
            NavigateToPage("Exam Clock");
            var clockPage = VerifyPageLoaded("Clock");

            // digitalTimeTextBlock.Text is bound to ViewModel.DigitalTimeText.
            // If ViewModel is null the binding silently produces an empty string.
            var timeDisplay = WaitUntilFound<AutomationElement>(
                () => clockPage.FindFirstDescendant(cf => cf.ByAutomationId("digitalTimeTextBlock")),
                "Digital time TextBlock should be present");

            WaitUntilCondition(
                () => !string.IsNullOrEmpty(timeDisplay.AsTextBox().Text),
                "Clock time should be populated by the ViewModel — empty means DataContext binding failed",
                TimeSpan.FromSeconds(5));

            Assert.That(timeDisplay.AsTextBox().Text, Does.Match(@"\d+:\d+"),
                "Time should be in HH:MM format, confirming ViewModel is bound");
        }

        [Test]
        public void Settings_ViewModelBinding_PopulatesSoundOptions()
        {
            NavigateToPage("Settings");
            var settingsPage = VerifyPageLoaded("SettingsPage");

            // TimerSoundComboBox.ItemsSource is bound to {Binding SoundOptions} — a ViewModel
            // collection.  If ViewModel is null the combo box will have no items.
            var comboBox = WaitUntilFound<AutomationElement>(
                () => settingsPage.FindFirstDescendant(cf => cf.ByAutomationId("TimerSoundComboBox")),
                "TimerSoundComboBox should be present");

            ScrollElementIntoView(comboBox);
            comboBox.Patterns.ExpandCollapse.Pattern.Expand();

            var items = WaitUntilFound<AutomationElement[]>(
                () =>
                {
                    var found = comboBox.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem));
                    return found.Length > 0 ? found : null;
                },
                "TimerSoundComboBox should have items — empty means {Binding SoundOptions} failed to resolve ViewModel");

            Assert.That(items.Length, Is.GreaterThan(0),
                "SoundOptions should be populated by the SettingsViewModel DataContext binding");

            comboBox.Patterns.ExpandCollapse.Pattern.Collapse();
        }

        [Test]
        public void RandomNameGenerator_ViewModelBinding_PageLoadsWithoutCrash()
        {
            NavigateToPage("Random Name Generator");
            var rngPage = VerifyPageLoaded("RandomNameGeneratorPage");

            // ClassesViewer exists and the app is still running.
            // A ViewModel NRE in the Loaded handler would have been caught by the global
            // UnhandledException handler — the page would appear but event subscriptions
            // (e.g. ViewModel.InstructionsRequested) would be missing.
            var classesViewer = WaitUntilFound<AutomationElement>(
                () => rngPage.FindFirstDescendant(cf => cf.ByAutomationId("ClassesViewer")),
                "ClassesViewer should be present");

            Assert.That(classesViewer, Is.Not.Null);
            Assert.That(App!.HasExited, Is.False, "App should still be running after RNG navigation");
        }

        [Test]
        public void TimerSelection_ViewModelBinding_PageLoadsWithButtons()
        {
            NavigateToPage("Timer");
            var timerPage = VerifyPageLoaded("TimerSelectionPage");

            // Buttons are declared in XAML but rely on the page being properly instantiated
            // (services resolved via AutomatedPage base).  If SettingsService or ThemeService
            // are null, opening a TimerWindow would NRE.
            var buttons = WaitUntilFound<AutomationElement[]>(
                () =>
                {
                    var found = timerPage.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
                    return found.Length > 0 ? found : null;
                },
                "Timer selection page should contain timer buttons");

            Assert.That(buttons.Length, Is.GreaterThan(0),
                "Timer page should display its timer buttons");
        }

        [Test]
        public void ScreenRuler_ViewModelBinding_PageLoadsWithoutCrash()
        {
            NavigateToPage("Screen Ruler");
            VerifyPageLoaded("ScreenRulerPage");

            // ScreenRulerPage passes SettingsService and ThemeService (resolved from
            // AutomatedPage base) into ScreenRulerWindow.  If they are null the window
            // constructor throws.  Verify the app is still alive.
            Assert.That(App!.HasExited, Is.False,
                "App should still be running — a null service would have crashed ScreenRulerWindow");
        }
    }
}
