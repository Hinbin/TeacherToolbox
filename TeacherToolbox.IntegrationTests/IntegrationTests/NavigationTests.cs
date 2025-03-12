using FlaUI.UIA3;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Conditions;
using System.Threading;
using TeacherToolbox;
using NUnit.Framework;
using System.Linq;
using FlaUI.Core.Tools;

namespace TeacherToolbox.IntegrationTests.IntegrationTests
{
    [TestFixture]
    public class NavigationTests : TestBase
    {
        // Helper for screen ruler specific verification with waiting
        private void VerifyScreenRulerWindow()
        {
            using (var automation = new UIA3Automation())
            {
                // Wait for ruler rectangle to appear
                var rulerRect = WaitUntilFound<AutomationElement>(
                    () => automation.GetDesktop().FindFirstDescendant(cf => cf.ByName("Screen Ruler Rectangle")),
                    "Screen Ruler Rectangle should be present");

                // Verify ruler window (parent of parent)
                var rulerWindow = rulerRect.Parent?.Parent;
                Assert.That(rulerWindow, Is.Not.Null, "Screen Ruler window should be loaded");
            }
        }

        [SetUp]
        public void NavigationSetUp()
        {
            // Wait for navigation view to be available
            WaitUntilFound<AutomationElement>(
                () => NavigationPane,
                "NavigationPane should be present");

            EnsureNavigationIsOpen();
        }

        [Test]
        public void NavigationMenu_ContainsAllExpectedItems()
        {
            // Wait for menu items host to be available
            var menuItemsHost = WaitUntilFound<AutomationElement>(
                () => NavigationPane!.FindFirstDescendant(cf => cf.ByAutomationId("MenuItemsHost")),
                "Menu items host should be present");

            // Get all menu items
            var menuItems = menuItemsHost.FindAllChildren(cf =>
                cf.ByControlType(ControlType.ListItem));

            var expectedItems = new[]
            {
                "Random Name Generator",
                "Timer",
                "Screen Ruler",
                "Exam Clock",
                "Settings"
            };

            var menuItemNames = menuItems.Select(item => item.Name).ToList();

            foreach (var expectedItem in expectedItems)
            {
                Assert.That(menuItemNames, Contains.Item(expectedItem),
                    $"Navigation menu should contain {expectedItem}");
            }
        }

        [Test]
        public void NavigateToTimer_LoadsTimerPage()
        {
            NavigateToPage("Timer");
            var timerPage = VerifyPageLoaded("Timer");

            // Wait for timer controls using helper
            var timerControls = WaitUntilFound<AutomationElement[]>(
                () => {
                    var controls = timerPage.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
                    return controls.Length > 0 ? controls : null;
                },
                "Timer page should contain control buttons");
        }

        [Test]
        public void NavigateToScreenRuler_LoadsRulerPage()
        {
            NavigateToPage("Screen Ruler");
            VerifyPageLoaded("ScreenRulerPage");
        }

        [Test]
        public void NavigateToScreenRuler_LoadsRulerWindow()
        {
            NavigateToPage("Screen Ruler");
            VerifyPageLoaded("ScreenRulerPage");
            VerifyScreenRulerWindow();
        }

        [Test]
        public void NavigateToExamClock_LoadsClockPage()
        {
            NavigateToPage("Exam Clock");
            var clockPage = VerifyPageLoaded("Clock");

            // Wait for clock display using helper
            var clockDisplay = WaitUntilFound<AutomationElement>(
                () => clockPage.FindFirstDescendant(cf => cf.ByName("ExamClock")),
                "Clock display should be present");
        }

        [Test]
        public void NavigateToRNG_LoadsRNGPage()
        {
            NavigateToPage("Random Name Generator");
            VerifyPageLoaded("RandomNameGenerator");
        }

        [Test]
        public void NavigateThroughAllPages_SuccessfulNavigation()
        {
            // Define the standard navigation pages
            var navigationOrder = new[]
            {
                ("Timer", "Timer"),
                ("Screen Ruler", "ScreenRulerPage"),
                ("Exam Clock", "Clock"),
                ("Random Name Generator", "RandomNameGenerator"),
                ("Settings", "SettingsPage")
            };

            // Navigate through all standard pages
            foreach (var (pageName, pageId) in navigationOrder)
            {
                // Ensure navigation is visible before each navigation
                EnsureNavigationIsOpen();

                // Navigate to the page
                NavigateToPage(pageName);

                // Verify the page is loaded
                VerifyPageLoaded(pageId);
            }
        }

        [Test]
        public void NavigationPane_CanTogglePane()
        {
            // Close the navigation pane
            ClickNavigationButton("Close Navigation");

            // Wait for the open button to appear using helper
            var openNavButton = WaitUntilFound<AutomationElement>(
                () => NavigationPane!.FindFirstChild(cf => cf.ByName("Open Navigation")),
                "Navigation pane should be closeable");

            // Open the navigation pane
            openNavButton.Click();

            // Wait for the close button to appear using helper
            var closeNavButton = WaitUntilFound<AutomationElement>(
                () => NavigationPane!.FindFirstChild(cf => cf.ByName("Close Navigation")),
                "Navigation pane should be openable");
        }

        [Test]
        public void NavigateToSettings_LoadsSettingsPage()
        {
            NavigateToPage("Settings");
            VerifyPageLoaded("SettingsPage");
        }
    }
}