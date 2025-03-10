using FlaUI.UIA3;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Conditions;
using System.Threading;
using TeacherToolbox;
using TTBIntegrationTesting.Integration_Tests;

namespace TTBIntegrationTesting
{
    [TestFixture]
    public class NavigationTests : TestBase
    {

        // Helper for screen ruler specific verification
        private void VerifyScreenRulerWindow()
        {
            var automation = new UIA3Automation();
            var rulerRect = automation.GetDesktop().FindFirstDescendant(cf =>
                cf.ByName("Screen Ruler Rectangle"));

            Assert.That(rulerRect, Is.Not.Null, "Screen Ruler Rectangle should be present");

            var rulerWindow = rulerRect.Parent?.Parent;
            Assert.That(rulerWindow, Is.Not.Null, "Screen Ruler window should be loaded");
        }


        [SetUp]
        public void NavigationSetUp()
        {
            Assert.That(NavigationView, Is.Not.Null, "NavigationPane should be present");
            EnsureNavigationIsOpen();
        }

        [Test]
        public void NavigationMenu_ContainsAllExpectedItems()
        {
            var menuItemsHost = NavigationView!.FindFirstDescendant(cf =>
                cf.ByAutomationId("MenuItemsHost"));

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

            // Additional verification
            var timerControls = timerPage.FindAllDescendants(cf =>
                cf.ByControlType(ControlType.Button));
            Assert.That(timerControls, Is.Not.Empty, "Timer page should contain control buttons");
        }

        [Test]
        public void NavigateToScreenRuler_LoadsRulerPage()
        {
            NavigateToPage("Screen Ruler");
            var screenRulerPage = VerifyPageLoaded("ScreenRulerPage");
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

            // We know from existing tests that Clock is a reliable indicator
            var clockDisplay = clockPage.FindFirstDescendant(cf =>
                cf.ByName("ExamClock"));
            Assert.That(clockDisplay, Is.Not.Null, "Clock display should be present");

        }

        [Test]
        public void NavigateToRNG_LoadsRNGPage()
        {
            NavigateToPage("Random Name Generator");
            var clockPage = VerifyPageLoaded("RandomNameGenerator");
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
                NavigateToPage(pageName);
                VerifyPageLoaded(pageId);
            }
        }

        [Test]
        public void NavigationView_CanTogglePane()
        {
            // Close the navigation pane
            ClickNavigationButton("Close Navigation");

            // Verify pane is closed
            var openNavButton = NavigationView!.FindFirstChild(cf =>
                cf.ByName("Open Navigation"));
            Assert.That(openNavButton, Is.Not.Null, "Navigation pane should be closeable");

            // Open the navigation pane
            ClickNavigationButton("Open Navigation");

            // Verify pane is open
            var closeNavButton = NavigationView!.FindFirstChild(cf =>
                cf.ByName("Close Navigation"));
            Assert.That(closeNavButton, Is.Not.Null, "Navigation pane should be openable");
        }

        [Test]
        public void NavigateToSettings_LoadsSettingsPage()
        {
            NavigateToPage("Settings");
            var screenRulerPage = VerifyPageLoaded("SettingsPage");
        }

    }
}