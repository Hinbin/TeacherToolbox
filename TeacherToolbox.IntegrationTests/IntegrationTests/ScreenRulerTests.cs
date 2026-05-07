using System;
using System.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using NUnit.Framework;

namespace TeacherToolbox.IntegrationTests.IntegrationTests
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.SingleInstance)]
    public class ScreenRulerTests : TestBase
    {
        [OneTimeSetUp]
        public void ClassSetUp() => EnableSharedLaunch();

        [OneTimeTearDown]
        public void ClassTearDown() => TearDownSharedLaunch();

        private Window? _rulerWindow;
        private AutomationElement? _closeButton;

        [TearDown]
        public void ScreenRulerTearDown()
        {
            if (_rulerWindow == null)
            {
                return;
            }

            try
            {
                (_closeButton ?? _rulerWindow.FindFirstDescendant(cf => cf.ByAutomationId("ScreenRulerCloseButton")))?.Click();
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
                        return _rulerWindow.IsOffscreen || _rulerWindow.Properties.IsOffscreen;
                    }
                    catch
                    {
                        return true;
                    }
                },
                "Screen ruler window should close",
                TimeSpan.FromSeconds(3));
        }

        [Test]
        public void ScreenRuler_AllowsVerticalDragging()
        {
            OpenScreenRulerPage();
            var initialBounds = _rulerWindow!.BoundingRectangle;

            for (var attempt = 0; attempt < 3 && Math.Abs(_rulerWindow!.BoundingRectangle.Y - initialBounds.Y) <= 20; attempt++)
            {
                DragWindow(_rulerWindow, 0, 160);
            }

            WaitUntilCondition(
                () => Math.Abs(_rulerWindow!.BoundingRectangle.Y - initialBounds.Y) > 20,
                "Ruler should move vertically when dragged",
                TimeSpan.FromSeconds(8));
        }

        [Test]
        public void ScreenRuler_PreventsHorizontalDragging()
        {
            OpenScreenRulerPage();
            var initialBounds = _rulerWindow!.BoundingRectangle;

            DragWindow(_rulerWindow, 100, 0);
            Wait.UntilInputIsProcessed();

            var newBounds = _rulerWindow!.BoundingRectangle;
            Assert.That(Math.Abs(newBounds.X - initialBounds.X), Is.LessThan(10),
                "Ruler should keep its horizontal position locked");
        }

        private void OpenScreenRulerPage()
        {
            NavigateToPage("Screen Ruler");
            VerifyPageLoaded("ScreenRulerPage");

            using var automation = new UIA3Automation();
            var rulerRectangle = WaitUntilFound(
                () => automation.GetDesktop().FindFirstDescendant(cf => cf.ByName("Screen Ruler Rectangle")),
                "Screen Ruler Rectangle should be present",
                TimeSpan.FromSeconds(5));

            _rulerWindow = rulerRectangle.Parent?.Parent?.AsWindow();
            Assert.That(_rulerWindow, Is.Not.Null, "Screen ruler window should be found");

            _closeButton = _rulerWindow!.FindFirstDescendant(cf => cf.ByAutomationId("ScreenRulerCloseButton"))
                ?? _rulerWindow.FindFirstDescendant(cf => cf.ByName("Screen Ruler Close Button"));
        }

        private static void DragWindow(Window window, int offsetX, int offsetY)
        {
            var bounds = window.BoundingRectangle;
            var startPoint = new System.Drawing.Point(bounds.X + 100, bounds.Y + bounds.Height / 2);

            Mouse.MoveTo(startPoint);
            Wait.UntilInputIsProcessed();
            Mouse.Down();
            Wait.UntilInputIsProcessed();
            const int steps = 8;
            var stepX = offsetX / steps;
            var stepY = offsetY / steps;
            for (var i = 0; i < steps; i++)
            {
                Mouse.MoveBy(stepX, stepY);
                Thread.Sleep(30);
                Wait.UntilInputIsProcessed();
            }

            Mouse.Up();
            Wait.UntilInputIsProcessed();
        }
    }
}
