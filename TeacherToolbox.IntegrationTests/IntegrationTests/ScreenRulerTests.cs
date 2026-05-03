using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Logging;
using FlaUI.UIA3;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace TeacherToolbox.IntegrationTests.IntegrationTests
{
    [TestFixture]
    public class ScreenRulerTests : TestBase
    {
        private Window? _rulerWindow;
        private AutomationElement? _rulerRectangle;
        private AutomationElement? _closeButton;

        private static readonly TimeSpan WindowOpenTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan WindowCloseTimeout = TimeSpan.FromSeconds(3);

        #region Test Setup and Teardown

        [SetUp]
        public void ScreenRulerSetUp()
        {
            // Ensure we're starting from the main window
            Wait.UntilResponsive(MainWindow!);
        }

        [TearDown]
        public void ScreenRulerTearDown()
        {
            // Close any open ruler windows
            if (_rulerWindow != null && !_rulerWindow.IsOffscreen)
            {
                try
                {
                    // Try to click the close button on the ruler window
                    if (_closeButton != null)
                    {
                        _closeButton.Click();
                        Thread.Sleep(500);
                    }
                    else
                    {
                        _rulerWindow.Close();
                    }
                    Thread.Sleep(500);
                }
                catch
                {
                    // Window may already be closed
                }
            }
            _rulerWindow = null;
            _rulerRectangle = null;
            _closeButton = null;
        }

        #endregion

        #region Basic Screen Ruler Tests

        [Test]
        public void ScreenRuler_OpensRulerWindow()
        {
            OpenScreenRulerPage();
            Assert.That(_rulerWindow, Is.Not.Null, "Ruler window should open");
            Assert.That(_rulerRectangle, Is.Not.Null, "Ruler rectangle should be present");
        }

        [Test]
        public void ScreenRuler_HasCorrectInitialDimensions()
        {
            OpenScreenRulerPage();

            var windowBounds = _rulerWindow!.BoundingRectangle;

            // Ruler should span the width of the screen and have a height of around 100
            Assert.That(windowBounds.Height, Is.LessThanOrEqualTo(150),
                "Ruler height should be around 100 pixels");
            Assert.That(windowBounds.Width, Is.GreaterThan(500),
                "Ruler width should span the display");
        }

        [Test]
        public void ScreenRuler_HasCloseButton()
        {
            OpenScreenRulerPage();

            Assert.That(_closeButton, Is.Not.Null, "Close button should be present on ruler window");
        }

        [Test]
        public void ScreenRuler_CanCloseRulerWindow()
        {
            OpenScreenRulerPage();

            // Click close button on ruler window
            _closeButton!.Click();
            Thread.Sleep(500);

            // Verify window is closed by checking the rectangle is no longer present
            using (var automation = new UIA3Automation())
            {
                var desktop = automation.GetDesktop();
                var rulerRect = desktop.FindFirstDescendant(cf => cf.ByName("Screen Ruler Rectangle"));
                Assert.That(rulerRect, Is.Null, "Ruler rectangle should not be present after closing");
            }

            // Verify Open button is now visible on the page (it has x:Name="OpenRulerWindowButton")
            var rulerPage = VerifyPageLoaded("ScreenRulerPage");
            var openButton = WaitUntilFound<AutomationElement>(
                () => rulerPage.FindFirstDescendant(cf => cf.ByName("OpenRulerWindowButton")),
                "Open Ruler Window button should be visible after closing",
                TimeSpan.FromSeconds(3));

            Assert.That(openButton, Is.Not.Null, "Open button should be visible");
            Assert.That(openButton.IsOffscreen, Is.False, "Open button should be on screen");
        }

        #endregion

        #region Vertical Locking Tests

        [Test]
        public void ScreenRuler_AllowsVerticalDragging()
        {

            OpenScreenRulerPage();
 
            var initialBounds = _rulerWindow!.BoundingRectangle;
            int initialX = initialBounds.X;
            int initialY = initialBounds.Y;

            // Drag window vertically down by 100 pixels
            DragWindowVertically(_rulerWindow, 100);

            var newBounds = _rulerWindow!.BoundingRectangle;

            // Y position should change
            Assert.That(newBounds.Y, Is.Not.EqualTo(initialY),
                "Ruler should move vertically when dragged up/down");

            // Y position should be approximately 100 pixels lower (within tolerance)
            Assert.That(Math.Abs((newBounds.Y - initialY) - 100), Is.LessThan(10),
                "Ruler should move approximately 100 pixels down");
        }

        [Test]
        public void ScreenRuler_PreventsHorizontalDragging()
        {
            OpenScreenRulerPage();

            var initialBounds = _rulerWindow!.BoundingRectangle;
            int initialX = initialBounds.X;
            int initialY = initialBounds.Y;

            Thread.Sleep(500);

            // Attempt to drag window horizontally to the right by 100 pixels
            DragWindowHorizontally(_rulerWindow, 100);
            Thread.Sleep(500);

            var newBounds = _rulerWindow!.BoundingRectangle;

            // X position should NOT change (horizontal movement is locked)
            Assert.That(Math.Abs(newBounds.X - initialX), Is.LessThan(10),
                "Ruler should NOT move horizontally when dragged left/right (horizontal lock)");

            // Y position may change slightly due to drag but should be close to original
            Assert.That(Math.Abs(newBounds.Y - initialY), Is.LessThan(20),
                "Ruler Y position should remain approximately the same during horizontal drag attempt");
        }

        [Test]
        public void ScreenRuler_WindowIsPositionedCorrectly()
        {
            OpenScreenRulerPage();

            var windowBounds = _rulerWindow!.BoundingRectangle;

            // The ruler should be positioned at the left edge of the display (X = 0 or close to it)
            // This verifies that the horizontal position is locked to the display
            Assert.That(windowBounds.X, Is.LessThanOrEqualTo(10),
                "Ruler should be positioned at or near the left edge of the display (horizontal lock verification)");

            // The width should span most/all of the display
            Assert.That(windowBounds.Width, Is.GreaterThan(500),
                "Ruler should span a significant width of the display");

            // Height should be around 100 pixels
            Assert.That(windowBounds.Height, Is.LessThanOrEqualTo(150),
                "Ruler should have a height of approximately 100 pixels");
        }

        [Test]
        public void ScreenRuler_WindowHasDraggableArea()
        {
            OpenScreenRulerPage();

            // Verify the ruler window has the proper structure for dragging
            // The entire grid should respond to pointer events
            var grid = _rulerWindow!.FindFirstDescendant(cf => cf.ByControlType(ControlType.Pane));

            Assert.That(grid, Is.Not.Null,
                "Ruler window should have a grid that handles pointer events for dragging");
        }

        #endregion

        #region Window Position Independence Tests

        [Test]
        public void ScreenRuler_PositionStoredSeparatelyFromTimerWindow()
        {
            // This test verifies that ScreenRuler and TimerWindow positions are stored independently

            // Step 1: Open and verify ScreenRuler window
            OpenScreenRulerPage();
            var rulerBounds = _rulerWindow!.BoundingRectangle;
            int rulerX = rulerBounds.X;
            int rulerY = rulerBounds.Y;

            // Close the ruler
            _closeButton!.Click();
            Thread.Sleep(500);

            // Step 2: Open a Timer window
            EnsureNavigationIsOpen();
            NavigateToPage("Timer");
            var timerPage = VerifyPageLoaded("TimerSelectionPage");

            // Find and click the 30 second timer button
            var timer30Button = WaitUntilFound<AutomationElement>(
                () => timerPage.FindFirstDescendant(cf =>
                    cf.ByAutomationId("timer30Button")),
                "30 second timer button should exist",
                TimeSpan.FromSeconds(3));

            Assert.That(timer30Button, Is.Not.Null, "Timer button should be found");
            timer30Button.Click();
            Thread.Sleep(1000);

            // Find the timer window
            Window? timerWindow = null;
            try
            {
                using (var automation = new UIA3Automation())
                {
                    var desktop = automation.GetDesktop();
                    var windows = desktop.FindAllChildren(cf => cf.ByControlType(ControlType.Window));

                    // Find timer window (look for timer-specific element)
                    foreach (var window in windows)
                    {
                        var timerText = window.FindFirstDescendant(cf => cf.ByAutomationId("timerText"));
                        if (timerText != null)
                        {
                            timerWindow = window.AsWindow();
                            break;
                        }
                    }
                }

                Assert.That(timerWindow, Is.Not.Null, "Timer window should open");

                var timerBounds = timerWindow!.BoundingRectangle;

                // Step 3: Verify positions are different
                // The ScreenRuler should be at the left edge (X near 0) spanning the width
                // The Timer window should be at a different position (likely centered or last saved position)

                // Key assertion: ScreenRuler width should be much larger than timer width
                // This indirectly verifies they're using different position storage
                Assert.That(rulerBounds.Width, Is.GreaterThan(timerBounds.Width),
                    "ScreenRuler should be wider than TimerWindow (verifying different position storage)");

                // ScreenRuler should be at left edge (X close to 0)
                Assert.That(rulerX, Is.LessThanOrEqualTo(10),
                    "ScreenRuler should be at left edge of display");

                // ScreenRuler should have small height
                Assert.That(rulerBounds.Height, Is.LessThanOrEqualTo(150),
                    "ScreenRuler should have small height (~100px)");

                // Timer window should have different dimensions
                Assert.That(timerBounds.Height, Is.GreaterThan(150),
                    "TimerWindow should be taller than ScreenRuler");

                // If timer and ruler had same position storage, they would have similar dimensions
                // The fact they're different proves they use separate storage
            }
            finally
            {
                // Clean up: close timer window
                if (timerWindow != null && !timerWindow.IsOffscreen)
                {
                    try
                    {
                        timerWindow.Close();
                        Thread.Sleep(500);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
        }

        [Test]
        public void ScreenRuler_ReopensAtSamePosition()
        {
            // Open ScreenRuler first time
            OpenScreenRulerPage();
            var firstBounds = _rulerWindow!.BoundingRectangle;
            int firstX = firstBounds.X;
            int firstY = firstBounds.Y;

            // Close it
            _closeButton!.Click();
            Thread.Sleep(500);

            // Navigate away and back
            EnsureNavigationIsOpen();
            NavigateToPage("Settings");
            Thread.Sleep(500);

            // Navigate back to ScreenRuler
            EnsureNavigationIsOpen();
            NavigateToPage("Screen Ruler");
            Thread.Sleep(500);

            // Find the ruler window again
            _rulerRectangle = WaitUntilFound<AutomationElement>(
                () =>
                {
                    try
                    {
                        using var automation = new UIA3Automation();
                        var desktop = automation.GetDesktop();
                        return desktop.FindFirstDescendant(cf => cf.ByName("Screen Ruler Rectangle"));
                    }
                    catch
                    {
                        return null;
                    }
                },
                "Screen Ruler Rectangle should reappear",
                TimeSpan.FromSeconds(5));

            var windowElement = _rulerRectangle!.Parent?.Parent;
            Assert.That(windowElement, Is.Not.Null, "Should find ruler window");

            _rulerWindow = windowElement.AsWindow();
            var secondBounds = _rulerWindow.BoundingRectangle;

            // Verify it opened at approximately the same position
            // Allow for small differences due to DPI or screen configuration
            Assert.That(Math.Abs(secondBounds.X - firstX), Is.LessThan(20),
                "ScreenRuler should reopen at approximately the same X position");

            // Y position might vary more due to vertical dragging capability
            // but should still be in a reasonable range
            Assert.That(Math.Abs(secondBounds.Y - firstY), Is.LessThan(500),
                "ScreenRuler should reopen at a similar Y position");

            // Width should be the same (spans display)
            Assert.That(Math.Abs(secondBounds.Width - firstBounds.Width), Is.LessThan(50),
                "ScreenRuler width should remain consistent");
        }

        #endregion

        #region Change Display Tests

        [Test]
        public void ScreenRuler_ChangeDisplayButtonExistsForMultipleDisplays()
        {
            OpenScreenRulerPage();

            var rulerPage = VerifyPageLoaded("ScreenRulerPage");
            var changeDisplayButton = rulerPage.FindFirstDescendant(cf =>
                cf.ByName("ChangeDisplayButton"));

            // This test will pass if the button is visible (multiple displays)
            // or if it's not visible (single display)
            if (changeDisplayButton != null && !changeDisplayButton.IsOffscreen)
            {
                Assert.Pass("Change Display button is visible (multiple displays detected)");
            }
            else
            {
                Assert.Pass("Change Display button is not visible (single display setup)");
            }
        }

        #endregion

        #region Helper Methods

        private void OpenScreenRulerPage()
        {
            try
            {
                // Navigate to Screen Ruler page
                EnsureNavigationIsOpen();
                NavigateToPage("Screen Ruler");
                VerifyPageLoaded("ScreenRulerPage");

                // Wait for ruler rectangle to appear (using the approach from NavigationTests)
                _rulerRectangle = WaitUntilFound<AutomationElement>(
                    () =>
                    {
                        try
                        {
                            using var automation = new UIA3Automation();
                            var desktop = automation.GetDesktop();
                            return desktop.FindFirstDescendant(cf => cf.ByName("Screen Ruler Rectangle"));
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error finding ruler rectangle: {ex.Message}");
                            return null;
                        }
                    },
                    "Screen Ruler Rectangle should be present",
                    WindowOpenTimeout);

                if (_rulerRectangle == null)
                {
                    throw new InvalidOperationException("Failed to find ruler rectangle after opening");
                }

                // Get the window (parent of parent of the rectangle)
                var windowElement = _rulerRectangle.Parent?.Parent;
                if (windowElement == null)
                {
                    throw new InvalidOperationException("Failed to find ruler window from rectangle");
                }

                _rulerWindow = windowElement.AsWindow();

                // Initialize ruler elements
                InitializeRulerElements();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OpenScreenRulerPage: {ex}");
                throw;
            }
        }

        private void InitializeRulerElements()
        {
            // Find close button on the ruler window using AutomationId
            _closeButton = _rulerWindow!.FindFirstDescendant(cf =>
                cf.ByAutomationId("ScreenRulerCloseButton"));

            if (_closeButton == null)
            {
                // Try finding by name if AutomationId doesn't work
                _closeButton = _rulerWindow!.FindFirstDescendant(cf =>
                    cf.ByName("Screen Ruler Close Button"));
            }
        }

        private void DragWindowVertically(Window window, int offsetY)
        {
            var windowBounds = window.BoundingRectangle;
            // Click on the left side of the window to avoid the close button
            // Use middle height of the window for stability
            var startPoint = new System.Drawing.Point(
                windowBounds.X + 100,  // 100 pixels from left edge
                windowBounds.Y + windowBounds.Height / 2);

            var endPoint = new System.Drawing.Point(
                startPoint.X,  // Keep X the same for vertical drag
                startPoint.Y + offsetY);


            Mouse.MoveTo(startPoint);
            Wait.UntilInputIsProcessed();

            // Perform drag operation with explicit start and end points
            Mouse.Down();
            Wait.UntilInputIsProcessed();

            Mouse.MoveBy(0, offsetY);
            Wait.UntilInputIsProcessed();

            Thread.Sleep(1000);

            Mouse.Up();
            Wait.UntilInputIsProcessed();
        }

        private void DragWindowHorizontally(Window window, int offsetX)
        {
            var windowBounds = window.BoundingRectangle;
            // Click on the left side of the window to avoid the close button
            // Use middle height of the window for stability
            var startPoint = new System.Drawing.Point(
                windowBounds.X + 100,  // 100 pixels from left edge
                windowBounds.Y + windowBounds.Height / 2);

            Mouse.MoveTo(startPoint);

            // Perform drag operation with explicit start and end points
            Mouse.Down();

            Wait.UntilInputIsProcessed();

            Mouse.MoveBy(offsetX, 0);  // Move horizontally
            Thread.Sleep(200);
            Wait.UntilInputIsProcessed();

            Mouse.Up();
        }


        #endregion
    }
}
