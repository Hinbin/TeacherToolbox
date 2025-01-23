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
        private AutomationElement? _navigationView;

        // Helper method to find and click navigation buttons
        private void ClickNavigationButton(string buttonName)
        {
            var button = _navigationView!.FindFirstChild(cf =>
                cf.ByName(buttonName));

            if (button == null)
                throw new InvalidOperationException($"Navigation button '{buttonName}' not found");

            button.Click();
            Thread.Sleep(500); // Keep existing wait logic
        }

        // Helper to ensure navigation is open
        private void EnsureNavigationIsOpen()
        {
            var openButton = _navigationView!.FindFirstChild(cf =>
                cf.ByName("Open Navigation"));

            if (openButton != null) // If we find the open button, navigation is closed
            {
                ClickNavigationButton("Open Navigation");
            }
        }

        // Helper for finding navigation items
        private AutomationElement GetNavigationItem(string pageName)
        {
            var navItem = _navigationView!.FindFirstDescendant(cf =>
                cf.ByName(pageName));

            if (navItem == null)
                throw new InvalidOperationException($"Navigation item '{pageName}' not found");

            return navItem;
        }


        // Helper for navigating to a specific page
        private void NavigateToPage(string pageName)
        {
            EnsureNavigationIsOpen();
            var navItem = GetNavigationItem(pageName);
            navItem.Click();
            Thread.Sleep(500); // Maintain existing wait logic
        }

        // Helper to verify a page is loaded
        private AutomationElement VerifyPageLoaded(string pageId)
        {
            var contentFrame = MainWindow!.FindFirstDescendant(cf =>
                cf.ByAutomationId("NavigationPane"));

            Assert.That(contentFrame, Is.Not.Null, "Navigation Pane should exist");

            var pageElement = contentFrame.FindFirstDescendant(cf =>
                cf.ByAutomationId(pageId));

            Assert.That(pageElement, Is.Not.Null, $"{pageId} page should be loaded");

            return pageElement;
        }

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
            _navigationView = MainWindow!.FindFirstDescendant(cf =>
                cf.ByAutomationId("NavigationPane"));

            Assert.That(_navigationView, Is.Not.Null, "NavigationPane should be present");
            EnsureNavigationIsOpen();
        }

        [Test]
        public void NavigationMenu_ContainsAllExpectedItems()
        {
            var menuItemsHost = _navigationView!.FindFirstDescendant(cf =>
                cf.ByAutomationId("MenuItemsHost"));

            var menuItems = menuItemsHost.FindAllChildren(cf =>
                cf.ByControlType(ControlType.ListItem));

            var expectedItems = new[]
            {
                "Random Name Generator",
                "Timer",
                "Screen Ruler",
                "Exam Clock"
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
            var navigationOrder = new[]
            {
                ("Timer", "Timer"),
                ("Screen Ruler", "ScreenRulerPage"),
                ("Exam Clock", "Clock"),
                ("Random Name Generator", "RandomNameGenerator")
            };

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
            var openNavButton = _navigationView!.FindFirstChild(cf =>
                cf.ByName("Open Navigation"));
            Assert.That(openNavButton, Is.Not.Null, "Navigation pane should be closeable");

            // Open the navigation pane
            ClickNavigationButton("Open Navigation");

            // Verify pane is open
            var closeNavButton = _navigationView!.FindFirstChild(cf =>
                cf.ByName("Close Navigation"));
            Assert.That(closeNavButton, Is.Not.Null, "Navigation pane should be openable");
        }

    }
}