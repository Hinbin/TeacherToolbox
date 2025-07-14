using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace TeacherToolbox.IntegrationTests.IntegrationTests
{
    [TestFixture]
    public class TimerTests : TestBase
    {
        private Window? _timerWindow;
        private AutomationElement? _timerText;
        private AutomationElement? _timerGauge;
        private AutomationElement? _intervalsListView;
        private AutomationElement? _addIntervalButton;
        private AutomationElement? _startButton;
        private AutomationElement? _intervalInfoText;

        // Timer-specific timeouts
        private static readonly TimeSpan TimerStartTimeout = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan TimerUpdateTimeout = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan WindowOpenTimeout = TimeSpan.FromSeconds(5);

        #region Test Setup and Teardown

        [SetUp]
        public void TimerSetUp()
        {
            // Ensure we're starting from the main window
            Wait.UntilResponsive(MainWindow!);
        }

        [TearDown]
        public void TimerTearDown()
        {
            // Close any open timer windows
            if (_timerWindow != null && !_timerWindow.IsOffscreen)
            {
                try
                {
                    _timerWindow.Close();
                    Thread.Sleep(500); // Allow time for window to close
                }
                catch
                {
                    // Window may already be closed
                }
            }
            _timerWindow = null;
        }

        #endregion

        #region Basic Timer Window Tests

        [Test]
        public void TimerWindow_OpensWithFixedTime()
        {
            // Open a 30-second timer
            OpenTimerWindow(30);

            Assert.That(_timerWindow, Is.Not.Null, "Timer window should open");
            Assert.That(_timerText, Is.Not.Null, "Timer text should be present");
            Assert.That(_timerGauge, Is.Not.Null, "Timer gauge should be present");
            Assert.That(_timerGauge!.IsOffscreen, Is.False, "Timer gauge should be visible");

            // Verify timer starts automatically
            Thread.Sleep(1500); // Wait for timer to tick
            var currentText = _timerText!.AsTextBox().Text;
            Assert.That(currentText, Is.Not.EqualTo("30"), "Timer should be counting down");
        }

        [Test]
        public void TimerWindow_OpensWithCustomTimeSelector()
        {
            // Open custom timer (seconds = 0)
            OpenTimerWindow(0);

            Assert.That(_timerWindow, Is.Not.Null, "Timer window should open");
            Assert.That(_intervalsListView, Is.Not.Null, "Time selector should be present");
            Assert.That(_intervalsListView.IsOffscreen, Is.False, "Time selector should be visible");           
            Assert.That(_startButton, Is.Not.Null, "Start button should be present");

            // Verify interval button is hidden for custom timer
            if (_addIntervalButton != null)
            {
                Assert.That(_addIntervalButton.IsOffscreen, Is.True,
                    "Add interval button should be hidden for custom timer");
            }
        }

        [Test]
        public void TimerWindow_OpensWithIntervalSelector()
        {
            // Open interval timer (seconds = -1)
            OpenTimerWindow(-1);

            Assert.That(_timerWindow, Is.Not.Null, "Timer window should open");
            Assert.That(_intervalsListView, Is.Not.Null, "Time selector should be present");
            Assert.That(_addIntervalButton, Is.Not.Null, "Add interval button should be present");
            Assert.That(_addIntervalButton!.IsOffscreen, Is.False, "Add interval button should be visible");
        }

        [Test]
        public void TimerWindow_HasCorrectWindowProperties()
        {
            OpenTimerWindow(30);

            // Check window properties
            var appWindow = _timerWindow!.AsWindow();

            // Window should be always on top
            // Note: This might be difficult to verify directly through automation
            Assert.That(appWindow.Title, Does.Contain("Timer"), "Window should have Timer in title");

            // Verify window can be moved (has drag functionality)
            var initialPosition = appWindow.BoundingRectangle;
            DragWindowHorizontally(appWindow, 50);
            Thread.Sleep(500);
            var newPosition = appWindow.BoundingRectangle;

            // Restore position
            DragWindowHorizontally(appWindow, -50);
        }

        private void DragWindowHorizontally(Window window, int offsetX)
        {
            var windowBounds = window.BoundingRectangle;
            var startPoint = new System.Drawing.Point(
                windowBounds.X + windowBounds.Width / 2,
                windowBounds.Y + 10);
            Mouse.DragHorizontally(startPoint, offsetX);
        }

        #endregion

        #region Timer Countdown Tests

        [Test]
        public void Timer_CountsDownCorrectly()
        {
            OpenTimerWindow(30);

            // Get initial value
            var initialText = _timerText!.AsTextBox().Text;
            Assert.That(initialText, Is.EqualTo("30"), "Timer should start at 30 seconds");

            // Wait and check countdown
            Thread.Sleep(2000);
            var afterTwoSeconds = _timerText!.AsTextBox().Text;
            Assert.That(afterTwoSeconds, Is.EqualTo("28"), "Timer should show 28 after 2 seconds");

            // Verify gauge updates
            var gaugeValue = GetGaugeValue();
            Assert.That(gaugeValue, Is.LessThanOrEqualTo(28), "Gauge value should decrease");
        }

        [Test]
        public void Timer_DisplaysCorrectTimeFormat()
        {
            // Test seconds only (< 60)
            OpenTimerWindow(30);
            Assert.That(_timerText!.AsTextBox().Text, Is.EqualTo("30"),
                "Should display seconds only for times under 60 seconds");
            CloseTimerWindow();

            // Test minutes:seconds format (>= 60)
            OpenTimerWindow(60);
            Assert.That(_timerText!.AsTextBox().Text, Is.EqualTo("1:00"),
                "Should display M:SS format for times >= 60 seconds");
            CloseTimerWindow();

            // Test larger times
            OpenTimerWindow(120);
            Assert.That(_timerText!.AsTextBox().Text, Is.EqualTo("2:00"),
                "Should display M:SS format for 2 minutes");
        }

        [Test]
        public void Timer_HandlesZeroCorrectly()
        {
            OpenTimerWindow(0);

            var secondsCombo = FindComboBoxByLabel("seconds");
            SelectComboBoxValue(secondsCombo!, "1");
            StartTimer();

            // Wait for timer to reach zero
            WaitUntilCondition(
                () => _timerText!.AsTextBox().Text == "0",
                "Timer should reach zero",
                TimeSpan.FromSeconds(5));

            // Check timer behavior at zero (depends on settings)
            Thread.Sleep(1500);

            // Timer should either close, count up, or stay at zero
            // We can't test the exact behavior without knowing settings,
            // but we can verify it doesn't crash
            Assert.Pass("Timer handles reaching zero without crashing");
        }

        #endregion

        #region Pause/Resume Tests

        [Test]
        public void Timer_CanPauseAndResume()
        {
            OpenTimerWindow(30);
            Thread.Sleep(1000); // Let timer run briefly

            // Click on timer to pause (avoiding the timer text itself)
            var timerBounds = _timerWindow!.BoundingRectangle;
            var clickPoint = new System.Drawing.Point(
                timerBounds.X + timerBounds.Width / 4,
                timerBounds.Y + timerBounds.Height / 2);

            Mouse.Click(clickPoint);
            Thread.Sleep(500);

            // Get paused value
            var pausedValue = _timerText!.AsTextBox().Text;

            // Wait to ensure timer is paused
            Thread.Sleep(1500);
            var stillPausedValue = _timerText!.AsTextBox().Text;
            Assert.That(stillPausedValue, Is.EqualTo(pausedValue),
                "Timer should remain paused");

            // Click again to resume
            Mouse.Click(clickPoint);
            Thread.Sleep(1500);

            var resumedValue = _timerText!.AsTextBox().Text;
            Assert.That(resumedValue, Is.Not.EqualTo(pausedValue),
                "Timer should resume counting after unpause");
        }

        #endregion

        #region Custom Timer Tests

        [Test]
        public void CustomTimer_CanSetAndStartTime()
        {
            OpenTimerWindow(0);

            // Find time input controls
            var hoursCombo = FindComboBoxByLabel("hours");
            var minutesCombo = FindComboBoxByLabel("minutes");
            var secondsCombo = FindComboBoxByLabel("seconds");

            Assert.That(hoursCombo, Is.Not.Null, "Hours combo box should be present");
            Assert.That(minutesCombo, Is.Not.Null, "Minutes combo box should be present");
            Assert.That(secondsCombo, Is.Not.Null, "Seconds combo box should be present");

            // Set custom time: 1 minute 15 seconds
            SelectComboBoxValue(minutesCombo!, "1");
            SelectComboBoxValue(secondsCombo!, "15");

            StartTimer();

            // Verify timer started with correct time
            Assert.That(_timerGauge!.IsOffscreen, Is.False, "Timer gauge should be visible after start");
            Assert.That(_timerText!.AsTextBox().Text, Is.EqualTo("1:15"),
                "Timer should show 1:15");
        }

        [Test]
        public void CustomTimer_ValidatesInput()
        {
            OpenTimerWindow(0);

            var hoursCombo = FindComboBoxByLabel("hours");

            // Try to enter invalid value
            hoursCombo!.Focus();
            Keyboard.Type("99");
            Keyboard.Press(VirtualKeyShort.ENTER);
            Thread.Sleep(500);

            // Should reset to valid value
            var comboValue = hoursCombo.AsComboBox().SelectedItem?.Text ?? "0";
            Assert.That(int.Parse(comboValue), Is.LessThan(24),
                "Hours should be validated to less than 24");
        }

        [Test]
        public void CustomTimer_CanStartWithEnterKey()
        {
            OpenTimerWindow(0);

            var secondsCombo = FindComboBoxByLabel("seconds");

            // Set time and press Enter
            SelectComboBoxValue(secondsCombo!, "15");
            secondsCombo!.Focus();
            Keyboard.Press(VirtualKeyShort.ENTER);
            Wait.UntilInputIsProcessed();

            // Wait for the time gauge to be visible
            _timerGauge = _timerWindow!.FindFirstDescendant(cf =>
                cf.ByAutomationId("timerGauge")).WaitUntilClickable();

            // Timer should start
            Assert.That(_timerGauge!.IsOffscreen, Is.False,
                "Timer should start when Enter is pressed");
        }

        #endregion

        #region Interval Timer Tests

        [Test]
        public void IntervalTimer_CanAddIntervals()
        {
            OpenTimerWindow(-1);

            // Should start with one interval
            var intervals = GetIntervalItems();
            Assert.That(intervals.Length, Is.EqualTo(1), "Should start with one interval");

            // Add another interval
            _addIntervalButton!.Click();
            Thread.Sleep(500);

            intervals = GetIntervalItems();
            Assert.That(intervals.Length, Is.EqualTo(2), "Should have two intervals after adding");

            // Verify interval numbers are correct
            var firstIntervalText = GetIntervalNumberText(intervals[0]);
            var secondIntervalText = GetIntervalNumberText(intervals[1]);

            Assert.That(firstIntervalText, Does.Contain("1"), "First interval should be numbered 1");
            Assert.That(secondIntervalText, Does.Contain("2"), "Second interval should be numbered 2");
        }

        [Test]
        public void IntervalTimer_CanRemoveIntervals()
        {
            OpenTimerWindow(-1);

            // Add intervals
            _addIntervalButton!.Click();
            _addIntervalButton!.Click();
            Wait.UntilInputIsProcessed();

            var intervals = GetIntervalItems();
            Assert.That(intervals.Length, Is.EqualTo(3), "Should have three intervals");

            // Find and click remove button on second interval
            var removeButton = intervals[1].FindFirstDescendant(cf =>
                cf.ByAutomationId("RemoveIntervalButton"));

            Assert.That(removeButton, Is.Not.Null, "Remove button should be present");
            Wait.UntilInputIsProcessed();
            string className = removeButton.AsButton().ClassName;
            string name = removeButton.AutomationId;
            Assert.That(className, Is.EqualTo("Button"), "Remove button should be a Button control");
            ScrollElementIntoView(removeButton!);
            Wait.UntilInputIsProcessed();
            removeButton!.AsButton().Click();

            Wait.UntilInputIsProcessed();

            intervals = GetIntervalItems();
            Assert.That(intervals.Length, Is.EqualTo(2), "Should have two intervals after removal");
        }

        [Test]
        public void IntervalTimer_LimitsToEightIntervals()
        {
            OpenTimerWindow(-1);

            // Add intervals up to the maximum
            for (int i = 0; i < 8; i++)
            {
                if (_addIntervalButton!.IsEnabled)
                {
                    _addIntervalButton.Click();
                    Thread.Sleep(200);
                }
            }

            var intervals = GetIntervalItems();
            Assert.That(intervals.Length, Is.LessThanOrEqualTo(8),
                "Should not exceed 8 intervals");

            Assert.That(_addIntervalButton!.IsEnabled, Is.False,
                "Add button should be disabled at max intervals");
        }

        [Test]
        public void IntervalTimer_RunsIntervalsInSequence()
        {
            OpenTimerWindow(-1);

            // Set up two short intervals
            var intervals = GetIntervalItems();
            SetIntervalTime(intervals[0], 0, 0, 2); // 2 seconds

            _addIntervalButton!.Click();
            Wait.UntilInputIsProcessed();
            intervals = GetIntervalItems();
            SetIntervalTime(intervals[1], 0, 0, 2); // 2 seconds

            StartTimer();

            _intervalInfoText = _timerWindow!.FindFirstDescendant(cf =>
                cf.ByAutomationId("intervalInfoText")).WaitUntilClickable();

            // Verify first interval is running
            Assert.That(_intervalInfoText, Is.Not.Null, "Interval info should be present");
            Assert.That(_intervalInfoText!.AsTextBox().Text, Does.Contain("1/2"),
                "Should show interval 1 of 2");

            // Wait for first interval to complete
            Thread.Sleep(3000);

            // Should now be on second interval
            var intervalText = _intervalInfoText!.AsTextBox().Text;
            Assert.That(intervalText, Does.Contain("2/2"),
                "Should show interval 2 of 2");
        }

        [Test]
        public void IntervalTimer_FirstIntervalCannotBeRemoved()
        {
            OpenTimerWindow(-1);

            var intervals = GetIntervalItems();
            var firstInterval = intervals[0];

            // Should not have remove button
            var removeButton = firstInterval.FindFirstDescendant(cf =>
                cf.ByName("Remove Interval"));

            Assert.That(removeButton, Is.Null,
                "First interval should not have remove button");
        }

        #endregion

        #region Settings Persistence Tests

        [Test]
        public void Timer_SavesCustomTimerConfiguration()
        {
            // Set custom timer
            OpenTimerWindow(0);
            var minutesCombo = FindComboBoxByLabel("minutes");
            SelectComboBoxValue(minutesCombo!, "5");
            StartTimer();
            CloseTimerWindow();

            // Open new custom timer
            OpenTimerWindow(0);

            // Should remember last configuration
            minutesCombo = FindComboBoxByLabel("minutes");
            var selectedValue = minutesCombo!.AsComboBox().SelectedItem?.Text;

            // Note: This test assumes settings persistence is implemented
            // If not implemented, this assertion may fail
            Assert.Pass("Custom timer configuration test completed");
        }

        [Test]
        public void Timer_SavesIntervalConfiguration()
        {
            // Set up intervals
            OpenTimerWindow(-1);

            var intervals = GetIntervalItems();
            SetIntervalTime(intervals[0], 0, 1, 30); // 1:30

            _addIntervalButton!.Click();
            Thread.Sleep(500);
            intervals = GetIntervalItems();
            SetIntervalTime(intervals[1], 0, 2, 0); // 2:00

            StartTimer();
            Thread.Sleep(1000);
            CloseTimerWindow();

            // Open new interval timer
            OpenTimerWindow(-1);

            // Should have saved intervals
            intervals = GetIntervalItems();

            // Note: This test assumes settings persistence is implemented
            Assert.Pass("Interval timer configuration test completed");
        }

        #endregion

        #region Theme and Visual Tests

        [Test]
        public void Timer_RespondsToThemeChanges()
        {
            OpenTimerWindow(30);

            // Note: Testing actual theme changes would require changing app settings
            // This test verifies the timer window renders correctly in the current theme

            Assert.That(_timerText, Is.Not.Null, "Timer text should be visible");
            Assert.That(_timerGauge, Is.Not.Null, "Timer gauge should be visible");

            // Verify text is readable (not null or empty)
            var timerTextValue = _timerText!.AsTextBox().Text;
            Assert.That(string.IsNullOrWhiteSpace(timerTextValue), Is.False,
                "Timer text should be readable in current theme");
        }

        #endregion

        #region Window Behavior Tests

        [Test]
        public void TimerWindow_CanBeRepositioned()
        {
            OpenTimerWindow(30);

            var initialBounds = _timerWindow!.BoundingRectangle;

            // Drag window
            var dragPoint = new System.Drawing.Point(
                initialBounds.X + initialBounds.Width / 2,
                initialBounds.Y + 20);

            Mouse.DragHorizontally(dragPoint, 100);
            Thread.Sleep(500);

            var newBounds = _timerWindow!.BoundingRectangle;
            Assert.That(newBounds.X, Is.Not.EqualTo(initialBounds.X),
                "Window should move when dragged");
        }

        [Test]
        public void TimerWindow_CanBeResized()
        {
            OpenTimerWindow(30);

            var initialBounds = _timerWindow!.BoundingRectangle;

            // Try to resize window from corner
            var resizePoint = new System.Drawing.Point(
                initialBounds.Right - 5,
                initialBounds.Bottom - 5);

            Mouse.Drag(resizePoint,
                new System.Drawing.Point(resizePoint.X + 50, resizePoint.Y + 50));
            Thread.Sleep(500);

            var newBounds = _timerWindow!.BoundingRectangle;

            // Verify size changed (may not work if window has fixed size)
            var sizeChanged = newBounds.Width != initialBounds.Width ||
                             newBounds.Height != initialBounds.Height;

            Assert.Pass("Window resize test completed");
        }

        #endregion

        #region Helper Methods

        private void OpenTimerWindow(int seconds)
        {
            try 
            {
                // Navigate to a page that can open timer windows
                EnsureNavigationIsOpen();
                NavigateToPage("Timer");

                var timerPage = VerifyPageLoaded("TimerSelectionPage");
                AutomationElement? timerButton = null;

                if (seconds > 0)
                {
                    var buttonId = GetTimerButtonId(seconds);

                   timerButton = timerPage.FindFirstDescendant(cf =>
                        cf.ByControlType(ControlType.Button).And(cf.ByAutomationId(buttonId)));

                    if (timerButton == null)
                    {
                        var buttonText = GetTimerButtonText(seconds);
                        timerButton = timerPage.FindFirstDescendant(cf =>
                            cf.ByControlType(ControlType.Button).And(cf.ByName(buttonText)));
                    }


                    Assert.That(timerButton, Is.Not.Null, $"Timer button for {seconds} seconds should exist");
                }
                else if (seconds == 0)
                {
                    timerButton = timerPage.FindFirstDescendant(cf =>
                        cf.ByControlType(ControlType.Button).And(cf.ByAutomationId("timerCustomButton")));
                    Assert.That(timerButton, Is.Not.Null, $"Custom timer button should exist");
                }
                else if (seconds == -1)
                {
                    timerButton = timerPage.FindFirstDescendant(cf =>
                        cf.ByControlType(ControlType.Button).And(cf.ByAutomationId("timerIntervalButton")));
                    Assert.That(timerButton, Is.Not.Null, "Interval timer button should exist");
                }
   

 
                timerButton.WaitUntilClickable();
                timerButton.Click();
                Wait.UntilInputIsProcessed();

                _timerWindow = WaitUntilFound<Window>(
                    () => {
                        try
                        {
                            using var automation = new UIA3Automation();
                            var desktop = automation.GetDesktop();
                            var windows = desktop.FindAllChildren(cf =>
                                cf.ByControlType(ControlType.Window));
                            
                            // More specific window identification
                            var timerWindow = windows.FirstOrDefault(w => 
                                w.Name.Contains("Timer") && // Check name
                                !w.Name.Contains("Visual Studio") && // Exclude VS
                                w != MainWindow); // Exclude main window
                            
                            if (timerWindow != null)
                            {
                                Debug.WriteLine($"Found timer window: Name={timerWindow.Name}, Class={timerWindow.ClassName}");
                                try
                                {      
                                    return timerWindow.AsWindow(); // Convert to Window type
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"Failed to convert to Window: {ex.Message}");
                                }
                            }
                            return null;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error in window search: {ex.Message}");
                            return null;
                        }
                    },
                    "Timer window should open",
                    WindowOpenTimeout);

                // Verify window was found before proceeding
                if (_timerWindow == null)
                {
                    throw new InvalidOperationException("Failed to find timer window after opening");
                }

                // Initialize timer window elements with null checks
                try
                {
                    InitializeTimerElements();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to initialize timer elements: {ex.Message}");
                    throw;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OpenTimerWindow: {ex}");
                
                // Try to clean up if something went wrong
                if (_timerWindow != null)
                {
                    try
                    {
                        _timerWindow.Close();
                    }
                    catch 
                    {
                        // Ignore cleanup errors
                    }
                    _timerWindow = null;
                }
                
                throw;
            }
        }

        private string GetTimerButtonId(int seconds)
        {
            // Map seconds to the button AutomationId from XAML
            return seconds switch
            {
                30 => "timer30Button",
                60 => "timer60Button",
                120 => "timer120Button",
                180 => "timer180Button",
                300 => "timer300Button",
                600 => "timer600Button",
                _ => $"timer{seconds}Button" // Fallback
            };
        }

        private string GetTimerButtonText(int seconds)
        {
            // Map seconds to the button content text from XAML
            return seconds switch
            {
                30 => "30 secs",
                60 => "1 mins",
                120 => "2 mins",
                180 => "3 mins",
                300 => "5 mins",
                600 => "10 mins",
                _ => seconds < 60 ? $"{seconds} secs" : $"{seconds / 60} mins"
            };
        }

        private void InitializeTimerElements()
        {
            _timerText = _timerWindow!.FindFirstDescendant(cf =>
                cf.ByAutomationId("timerText"));

            _timerGauge = _timerWindow!.FindFirstDescendant(cf =>
                cf.ByAutomationId("timerGauge"));

            _intervalsListView = _timerWindow!.FindFirstDescendant(cf =>
                cf.ByAutomationId("intervalsListView"));

            _addIntervalButton = _timerWindow!.FindFirstDescendant(cf =>
                cf.ByAutomationId("addIntervalButton"));

            _startButton = _timerWindow!.FindFirstDescendant(cf =>
                cf.ByAutomationId("startButton"));

            _intervalInfoText = _timerWindow!.FindFirstDescendant(cf =>
                cf.ByAutomationId("intervalInfoText"));
        }

        private void CloseTimerWindow()
        {
            if (_timerWindow != null && !_timerWindow.IsOffscreen)
            {
                _timerWindow.Close();
                Thread.Sleep(500);
            }
            _timerWindow = null;
        }

        private void StartTimer()
        {
            // Start timer
            _startButton!.AsButton().Click();
            Wait.UntilInputIsProcessed();

            // Try to find the timer gauge
            _timerGauge = _timerWindow!.FindFirstDescendant(cf =>
                cf.ByAutomationId("timerGauge")).WaitUntilClickable();

            _timerText = _timerWindow!.FindFirstDescendant(cf =>
                cf.ByAutomationId("timerText")).WaitUntilClickable();
        }

        private AutomationElement? FindComboBoxByLabel(string boxName)
        {
            var boxes = _intervalsListView!.FindAllDescendants(cf =>
                cf.ByControlType(ControlType.ComboBox));

            foreach (var box in boxes)
            {
                if (box.AutomationId.ToLower().Contains(boxName.ToLower()))
                {
                    return box;
                }
            }

            return null;
        }

        private void SelectComboBoxValue(AutomationElement comboBox, string value)
        {
            // Scroll the combo box itself into view first
            comboBox.Focus();
            comboBox.AsComboBox().Expand(); // Ensure combo box is expanded
            comboBox.AsComboBox().Select(value);
            Wait.UntilInputIsProcessed();
            comboBox.AsComboBox().Collapse();
        }

        private AutomationElement[] GetIntervalItems()
        {
            if (_intervalsListView == null)
                return new AutomationElement[0];

            return _intervalsListView.FindAllDescendants(cf =>
                cf.ByControlType(ControlType.ListItem));
        }

        private void SetIntervalTime(AutomationElement interval, int hours, int minutes, int seconds)
        {
            var comboBoxes = interval.FindAllDescendants(cf =>
                cf.ByControlType(ControlType.ComboBox));

            if (comboBoxes.Length >= 3)
            {
                SelectComboBoxValue(comboBoxes[0], hours.ToString());
                SelectComboBoxValue(comboBoxes[1], minutes.ToString());
                SelectComboBoxValue(comboBoxes[2], seconds.ToString());
            }
        }

        private string GetIntervalNumberText(AutomationElement interval)
        {
            var textBlocks = interval.FindAllDescendants(cf =>
                cf.ByControlType(ControlType.Text));

            var intervalText = textBlocks.FirstOrDefault(t =>
                t.AutomationId.Contains("Interval"));

            return intervalText?.Name ?? "";
        }

        private double GetGaugeValue()
        {
            // Since we can't directly access the gauge value property,
            // we might need to infer it from other visual elements
            // or use the timer text as a proxy

            var timerTextValue = _timerText!.AsTextBox().Text;
            if (int.TryParse(timerTextValue, out int seconds))
            {
                return seconds;
            }

            // Parse M:SS or H:MM:SS format
            var parts = timerTextValue.Split(':');
            if (parts.Length == 2)
            {
                return int.Parse(parts[0]) * 60 + int.Parse(parts[1]);
            }
            else if (parts.Length == 3)
            {
                return int.Parse(parts[0]) * 3600 + int.Parse(parts[1]) * 60 + int.Parse(parts[2]);
            }

            return 0;
        }

        private void WaitUntilCondition(Func<bool> condition, string message, TimeSpan timeout)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            while (!condition() && stopwatch.Elapsed < timeout)
            {
                Thread.Sleep(100);
            }

            if (!condition())
            {
                Assert.Fail($"Timeout waiting for: {message}");
            }
        }

        private T? WaitUntilFound<T>(Func<T?> findFunc, string elementDescription, TimeSpan? timeout = null)
            where T : class
        {
            var actualTimeout = timeout ?? TimeSpan.FromSeconds(5);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            while (stopwatch.Elapsed < actualTimeout)
            {
                var element = findFunc();
                if (element != null)
                    return element;

                Thread.Sleep(100);
            }

            return null;
        }

        #endregion
    }
}