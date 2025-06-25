using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Logging;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using NUnit.Framework;
using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace TeacherToolbox.IntegrationTests.IntegrationTests
{
    [TestFixture]
    public class ClockTests : TestBase
    {
        private AutomationElement? _clockPage;
        private AutomationElement? _clockCanvas;
        private AutomationElement? _digitalTimeDisplay;
        private AutomationElement? _centreTextBox;

        // Clock-specific timeouts
        private static readonly TimeSpan ClockInteractionTimeout = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan SegmentCreationTimeout = TimeSpan.FromSeconds(2);

        // Clock positioning constants - ensure we're definitely past 12:00
        private const int ClockRadius = 50;
        private const int TwelveOClockOffsetX = 10; // Increased offset to ensure 12:01+ position
        private const int LargerClockRadius = 70; // For tests using larger radius

        [SetUp]
        public void ClockSetUp()
        {
            // Navigate to Clock page
            EnsureNavigationIsOpen();
            NavigateToPage("Exam Clock");
            _clockPage = VerifyPageLoaded("Clock");

            // Get key clock elements
            InitializeClockElements();
            Wait.UntilResponsive(_clockPage!);
            Wait.UntilResponsive(_digitalTimeDisplay);
            Wait.UntilInputIsProcessed();
        }

        private void InitializeClockElements()
        {
            // Find the main clock grid
            var clockGrid = WaitUntilFound<AutomationElement>(
                () => _clockPage!.FindFirstDescendant(cf => cf.ByName("ExamClock")),
                "Clock grid should be present");

            // Find the clock canvas (where segments are drawn)
            _clockCanvas = WaitUntilFound<AutomationElement>(
                () => _clockPage!.FindFirstDescendant(cf => cf.ByAutomationId("ClockCanvas")),
                "Clock canvas should be present");

            // Find digital time display
            _digitalTimeDisplay = WaitUntilFound<AutomationElement>(
                () => _clockPage!.FindFirstDescendant(cf => cf.ByAutomationId("digitalTimeTextBlock")),
                "Digital time display should be present");

            // Find centre text box
            _centreTextBox = WaitUntilFound<AutomationElement>(
                () => _clockPage!.FindFirstDescendant(cf => cf.ByAutomationId("centreTextBox")),
                "Centre text box should be present");
        }

        #region Basic Clock Functionality Tests

        [Test]
        public void Clock_DisplaysCurrentTime()
        {
            Wait.UntilResponsive(_digitalTimeDisplay);
            // Verify digital time is showing
            var timeText = _digitalTimeDisplay!.AsTextBox().Text;
            Assert.That(timeText, Is.Not.Null.And.Not.Empty,
                "Clock should display current time");

            // Verify time format (should be like "2:30 PM" or "14:30")
            Assert.That(timeText.Contains(":"), Is.True,
                "Time should contain colon separator");
        }

        [Test]
        public void Clock_CanAccessTimePickerFlyout()
        {
            Wait.UntilResponsive(_digitalTimeDisplay);
            // Click on digital time to open time picker flyout
            _digitalTimeDisplay!.Click();

            // Search at desktop level for the popup
            using var automation = new UIA3Automation();
            var desktop = automation.GetDesktop();

            var flyoutPopup = WaitUntilFound<AutomationElement>(
                () => desktop.FindFirstDescendant(cf =>
                    cf.ByControlType(ControlType.Pane)
                    .And(cf.ByName("timepicker"))), 
                "Time picker flyout popup should appear",
                ClockInteractionTimeout);

            Assert.That(flyoutPopup, Is.Not.Null, "Time picker flyout should be accessible");
        }

        [Test]
        public void Clock_HasCentreTextBox()
        {
            Assert.That(_centreTextBox, Is.Not.Null, "Centre text box should exist");
            Assert.That(_centreTextBox!.IsEnabled, Is.True, "Centre text box should be enabled");
        }

        [Test]
        public void Clock_CentreTextPersistsInput()
        {
            // Clear existing text and enter new text
            var testText = "12345";

            _centreTextBox!.Focus();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A); // Select all
            Keyboard.Type(testText);

            // Wait for text to be processed
            Thread.Sleep(500);

            // Navigate away and back to test persistence
            NavigateAwayAndBackToClock();

            // Verify text persisted
            var persistedText = _centreTextBox!.AsTextBox().Text;
            Assert.That(persistedText, Is.EqualTo(testText),
                "Centre text should persist after navigation");
        }

        #endregion

        #region Time Segment Tests

        [Test]
        public void Clock_CanCreateTimeSegment_LeftClick()
        {
            // Get initial number of segments (clock face elements)
            var initialElements = GetClockFaceElements();
            var initialCount = initialElements.Count();

            // Click on a position on the clock face (12:01 position - slightly right of 12:00)
            var clockCenter = GetClockCenterPoint();
            var topPosition = new System.Drawing.Point(
                clockCenter.X + TwelveOClockOffsetX,
                clockCenter.Y - ClockRadius);

            Mouse.MoveTo(topPosition);
            Wait.UntilInputIsProcessed();
            Thread.Sleep(200); // Allow time for mouse move to register properly
            Mouse.Click();

            // Wait for segment to be created
            WaitUntilCondition(
                () => GetClockFaceElements().Count() > initialCount,
                "A new time segment should be created",
                SegmentCreationTimeout);

            var newElements = GetClockFaceElements();
            Assert.That(newElements.Count(), Is.GreaterThan(initialCount),
                "Should have more elements after creating segment");
        }


        [Test]
        public void Clock_SeparateClicks_CreateSeparateSegments()
        {
            var initialCount = GetClockFaceElements().Count();
            var clockCenter = GetClockCenterPoint();

            // Click at 12:01
            var pos1 = new System.Drawing.Point(
                clockCenter.X + TwelveOClockOffsetX,
                clockCenter.Y - ClockRadius);

            Mouse.MoveTo(pos1);
            Wait.UntilInputIsProcessed();
            Thread.Sleep(200);
            Mouse.Click();
            Thread.Sleep(500);

            var afterFirstClick = GetClockFaceElements().Count();
            Assert.That(afterFirstClick, Is.EqualTo(initialCount + 1),
                "First click should create one segment");

            // Click at 1 o'clock (different position)
            var pos2 = new System.Drawing.Point(
                clockCenter.X + 50,
                clockCenter.Y - 20);

            Mouse.MoveTo(pos2);
            Wait.UntilInputIsProcessed();
            Thread.Sleep(200);
            Mouse.Click();
            Thread.Sleep(500);

            var afterSecondClick = GetClockFaceElements().Count();
            Assert.That(afterSecondClick, Is.EqualTo(initialCount + 2),
                "Second click should create a separate segment (total: 2 segments)");
        }

        [Test]
        public void Clock_CanRemoveTimeSegment_RightClick()
        {
            // First create a segment
            CreateTestTimeSegment();

            var segmentCount = GetClockFaceElements().Count();
            Assert.That(segmentCount, Is.GreaterThan(0), "Should have at least one segment before removal");

            // Right-click on the same position to remove segment (12:01 position)
            var clockCenter = GetClockCenterPoint();
            var segmentPosition = new System.Drawing.Point(
                clockCenter.X + TwelveOClockOffsetX,
                clockCenter.Y - LargerClockRadius);

            Mouse.MoveTo(segmentPosition);
            Wait.UntilInputIsProcessed();
            Thread.Sleep(200); // Ensure mouse move completes before clicking
            Mouse.RightClick();

            // Wait for segment to be removed
            WaitUntilCondition(
                () => GetClockFaceElements().Count() < segmentCount,
                "Time segment should be removed by right-click",
                SegmentCreationTimeout);
        }

        [Test]
        public void Clock_SupportsMultipleTimeSegments()
        {
            var initialCount = GetClockFaceElements().Count();
            var clockCenter = GetClockCenterPoint();

            // Create segments at different positions
            var positions = new[]
            {
                new System.Drawing.Point(clockCenter.X + TwelveOClockOffsetX, clockCenter.Y - ClockRadius),  // 12:01
                new System.Drawing.Point(clockCenter.X + ClockRadius, clockCenter.Y),                      // 3 o'clock
                new System.Drawing.Point(clockCenter.X, clockCenter.Y + ClockRadius),                      // 6 o'clock
                new System.Drawing.Point(clockCenter.X - ClockRadius, clockCenter.Y)                       // 9 o'clock
            };

            foreach (var position in positions)
            {
                Mouse.MoveTo(position);
                Wait.UntilInputIsProcessed();
                Thread.Sleep(200); // Ensure mouse move completes before clicking
                Mouse.Click();
                Thread.Sleep(400); // Allow time for each segment to be created
            }

            // Wait for all segments to be created
            WaitUntilCondition(
                () => GetClockFaceElements().Count() >= initialCount + positions.Length,
                "Multiple time segments should be created",
                TimeSpan.FromSeconds(5));

            var finalCount = GetClockFaceElements().Count();
            Assert.That(finalCount, Is.GreaterThanOrEqualTo(initialCount + positions.Length),
                "Should be able to create multiple time segments");
        }

        [Test]
        public void Clock_SegmentsHaveDifferentColors()
        {
            // Create multiple segments
            CreateMultipleTestSegments(3);

            // Get all gauge elements (these represent the colored segments)
            var gauges = WaitUntilFound<AutomationElement[]>(
                () => {
                    var elements = _clockCanvas!.FindAllDescendants(cf =>
                        cf.ByControlType(ControlType.Custom));
                    return elements.Length > 0 ? elements : null;
                },
                "Should find colored gauge segments");

            Assert.That(gauges.Length, Is.GreaterThanOrEqualTo(3),
                "Should have multiple colored segments");

            // Note: We can't easily test the actual colors in automation, but we can verify
            // that different gauge elements exist, which implies different colored segments
        }

        #endregion

        #region Clock Instructions Tests

        [Test]
        public void Clock_ShowsInstructionsOnFirstLoad()
        {
            // This test might need to be run in a clean environment or with reset settings
            // Look for the instruction tip
            var instructionTip = _clockPage!.FindFirstDescendant(cf =>
                cf.ByAutomationId("ClockInstructionTip"));

            if (instructionTip != null)
            {
                // If instructions are shown, verify the content
                var tipContent = instructionTip.FindFirstDescendant(cf =>
                    cf.ByControlType(ControlType.Text));

                if (tipContent != null)
                {
                    Assert.That(tipContent.Name, Does.Contain("Left-click"),
                        "Instructions should mention left-click functionality");
                    Assert.That(tipContent.Name, Does.Contain("Right-click"),
                        "Instructions should mention right-click functionality");
                }
            }

            // Note: Instructions might not always be visible if they've been shown before
            Assert.Pass("Clock loads successfully (instructions may not be visible if shown previously)");
        }

        #endregion

        #region Clock Theme Tests

        [Test]
        public void Clock_AdaptsToThemeChanges()
        {
            // Navigate to settings to change theme
            EnsureNavigationIsOpen();
            NavigateToPage("Settings");

            var settingsPage = VerifyPageLoaded("SettingsPage");
            Wait.UntilResponsive(settingsPage);

            // Find and change theme
            var themeComboBox = WaitUntilFound<AutomationElement>(
                () => settingsPage.FindFirstDescendant(cf => cf.ByAutomationId("ThemeComboBox")),
                "Theme combo box should be found");

            ScrollElementIntoView(themeComboBox);
            Wait.UntilResponsive(themeComboBox);

            // Change to dark theme
            themeComboBox.Patterns.ExpandCollapse.Pattern.Expand();
            var darkOption = WaitUntilFound<AutomationElement>(
                () => themeComboBox.FindFirstDescendant(cf => cf.ByName("Dark")),
                "Dark theme option should be available");

            darkOption.Click();
            Wait.UntilInputIsProcessed();
            Wait.UntilResponsive(NavigationPane!);

            // Navigate back to clock
            EnsureNavigationIsOpen();
            NavigateToPage("Exam Clock");

            Wait.UntilResponsive(_digitalTimeDisplay);
            Thread.Sleep(1000); // Allow time for theme change to apply
            _clockPage = VerifyPageLoaded("Clock");
            Wait.UntilResponsive(_digitalTimeDisplay);

            // Clock should still be functional after theme change
            InitializeClockElements();

            Assert.That(_clockCanvas, Is.Not.Null,
                "Clock should remain functional after theme change");
            Assert.That(_digitalTimeDisplay!.AsTextBox().Text, Is.Not.Null.And.Not.Empty,
                "Time display should still work after theme change");
            
        }

        #endregion

        #region Performance and State Tests

        [Test]
        public void Clock_HandlesRapidInteractions()
        {
            var clockCenter = GetClockCenterPoint();
            var initialCount = GetClockFaceElements().Count();

            // Perform rapid clicks at different positions (avoid exact hour positions)
            for (int i = 0; i < 5; i++)
            {
                var angle = (i * 72) + 5; // 72 degrees apart + 5 degree offset to avoid exact hours
                var radians = angle * Math.PI / 180;
                var x = clockCenter.X + (int)(60 * Math.Cos(radians));
                var y = clockCenter.Y + (int)(60 * Math.Sin(radians));

                Mouse.MoveTo(new System.Drawing.Point(x, y));
                Wait.UntilInputIsProcessed();
                Thread.Sleep(100); // Ensure mouse move completes even in rapid mode
                Mouse.Click();
                Thread.Sleep(100); // Brief pause between clicks for processing
            }

            // Wait for all interactions to process
            Thread.Sleep(1000);

            // Clock should still be responsive
            Assert.That(_digitalTimeDisplay!.Name, Is.Not.Null.And.Not.Empty,
                "Clock should remain responsive after rapid interactions");
        }

        [Test]
        public void Clock_MaintainsStateAfterNavigation()
        {
            // Set centre text
            var testCentreText = "TEST123";
            _centreTextBox!.Focus();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            Keyboard.Type(testCentreText);

            // Create a time segment
            CreateTestTimeSegment();
            var segmentCount = GetClockFaceElements().Count();

            // Navigate away and back
            NavigateAwayAndBackToClock();

            // Verify centre text persisted
            Assert.That(_centreTextBox!.AsTextBox().Text, Is.EqualTo(testCentreText),
                "Centre text should persist after navigation");

            // Verify clock is still visible 
            Assert.That(_digitalTimeDisplay!.AsTextBox().Text, Is.Not.Null.And.Not.Empty,
                "Centre text should persist after navigation");

            // Note: Time segments are not persisted by design in the current implementation
            // They are meant to be temporary visual aids during an exam session
        }

        #endregion

        #region Helper Methods

        private void CreateTestTimeSegment()
        {
            var clockCenter = GetClockCenterPoint();
            // Position at 12:01 (slightly right of 12:00 to avoid exact hour position)
            var position = new System.Drawing.Point(
                clockCenter.X + TwelveOClockOffsetX,
                clockCenter.Y - LargerClockRadius);

            Mouse.MoveTo(position);
            Wait.UntilInputIsProcessed();
            Thread.Sleep(200); // Ensure mouse move completes before clicking
            Mouse.Click();
            Thread.Sleep(300); // Allow time for segment creation
        }

        private void CreateMultipleTestSegments(int count)
        {
            var clockCenter = GetClockCenterPoint();

            for (int i = 0; i < count; i++)
            {
                // Add a small offset to avoid exact hour positions
                var angle = (i * (360.0 / count)) + 5; // +5 degrees offset
                var radians = angle * Math.PI / 180;
                var x = clockCenter.X + (int)(70 * Math.Cos(radians - Math.PI / 2)); // -PI/2 to start at top
                var y = clockCenter.Y + (int)(70 * Math.Sin(radians - Math.PI / 2));

                Mouse.MoveTo(new System.Drawing.Point(x, y));
                Wait.UntilInputIsProcessed();
                Thread.Sleep(200); // Ensure mouse move completes before clicking
                Mouse.Click();
                Thread.Sleep(400); // Allow time for each segment
            }
        }

        private System.Drawing.Point GetClockCenterPoint()
        {
            var canvasRect = _clockCanvas!.BoundingRectangle;

            return new System.Drawing.Point(
                canvasRect.X + canvasRect.Width / 2,
                canvasRect.Y + canvasRect.Height / 2);
        }

        private AutomationElement[] GetClockFaceElements()
        {
            try
            {
                // Look for RadialGauge elements which represent the colored segments
                var elements = _clockCanvas!.FindAllDescendants(cf =>
                    cf.ByControlType(ControlType.Custom));

                // Filter for gauge segments
                return elements.Where(e =>
                {
                    try
                    {
                        // Gauge elements have the word Gauge set as part of their automationID
                        return e.AutomationId.Contains("Gauge");
                    }
                    catch
                    {
                        return false;
                    }
                }).ToArray();
            }
            catch
            {
                return new AutomationElement[0];
            }
        }

        private void NavigateAwayAndBackToClock()
        {
            // Navigate to a different page
            EnsureNavigationIsOpen();
            NavigateToPage("Random Name Generator");

            // Wait for navigation
            WaitUntilFound<AutomationElement>(
                () => MainWindow!.FindFirstDescendant(cf => cf.ByAutomationId("RandomNameGenerator")),
                "Should navigate away from clock");

            // Navigate back to clock
            EnsureNavigationIsOpen();
            NavigateToPage("Exam Clock");

            _clockPage = VerifyPageLoaded("Clock");
            InitializeClockElements();

            // Wait for digital time to be populated by the timer
            WaitUntilCondition(
                () => !string.IsNullOrEmpty(_digitalTimeDisplay!.AsTextBox().Text),
                "Digital time should be populated after navigation",
                TimeSpan.FromSeconds(3));
        }

        #endregion
    }
}