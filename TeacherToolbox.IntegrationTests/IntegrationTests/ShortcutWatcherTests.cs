using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace TeacherToolbox.IntegrationTests.IntegrationTests
{
    [TestFixture]
    public class ShortcutWatcherTests : TestBase
    {
        private AutomationElement? _rngPage;

        [SetUp]
        public void ShortcutWatcherSetUp()
        {
            // Wait for ShortcutWatcher process to start
            WaitUntilCondition(
                () => Process.GetProcessesByName("ShortcutWatcher").Length > 0,
                "ShortcutWatcher.exe should be running",
                TimeSpan.FromSeconds(5));

            // Give the ShortcutWatcher additional time to fully initialize
            // (register global hooks, connect named pipes, etc.)
            WaitUntilCondition(
                () => {
                    var watcherProcess = Process.GetProcessesByName("ShortcutWatcher");
                    if (watcherProcess.Length == 0) return false;

                    // Ensure process has been running for at least a short time
                    // This gives time for hooks to register and pipes to connect
                    try
                    {
                        return (DateTime.Now - watcherProcess[0].StartTime).TotalMilliseconds > 500;
                    }
                    catch
                    {
                        return false;
                    }
                },
                "ShortcutWatcher should be fully initialized",
                TimeSpan.FromSeconds(5));
        }

        [Test]
        public void ShortcutWatcher_ProcessIsRunning()
        {
            // Verify that ShortcutWatcher.exe is running
            var watcherProcess = Process.GetProcessesByName("ShortcutWatcher");

            Assert.Multiple(() =>
            {
                Assert.That(watcherProcess.Length, Is.GreaterThan(0),
                    "ShortcutWatcher.exe should be running");
                Assert.That(watcherProcess[0].HasExited, Is.False,
                    "ShortcutWatcher.exe should not have exited");
            });
        }

        [Test]
        public void F9_NavigatesToRandomNameGenerator()
        {

            // First, set up a class with students
            NavigateToPage("Random Name Generator");

            _rngPage = VerifyPageLoaded("RandomNameGeneratorPage");

            // Add a class file
            OpenClassFile("8xCs2.txt");

            // Navigate away from RandomNameGenerator to ensure F9 brings us back
            NavigateToPage("Timer");

            // Verify we're on Timer page
            var timerPage = VerifyPageLoaded("TimerSelectionPage");

            // Press F9
            Keyboard.Press(VirtualKeyShort.F9);
            Wait.UntilInputIsProcessed();

            // Verify we're now on RandomNameGenerator page
            var rngPage = WaitUntilFound<AutomationElement>(
                () => MainWindow!.FindFirstDescendant(cf =>
                    cf.ByAutomationId("RandomNameGeneratorPage")),
                "RandomNameGeneratorPage should be loaded after F9",
                TimeSpan.FromSeconds(5));

            Assert.That(rngPage, Is.Not.Null,
                "F9 should navigate to RandomNameGeneratorPage");
        }

        [Test]
        public void F9_GeneratesRandomName()
        {
            // First, set up a class with students
            NavigateToPage("Random Name Generator");

            _rngPage = VerifyPageLoaded("RandomNameGeneratorPage");

            // Add a class file
            OpenClassFile("8xCs2.txt");

            // Get the name display element
            var nameDisplay = WaitUntilFound<AutomationElement>(
                () => _rngPage!.FindFirstDescendant(cf =>
                    cf.ByAutomationId("NameDisplay")),
                "NameDisplay should exist");

            var textPattern = nameDisplay.Patterns.Text.Pattern;
            var initialName = textPattern.DocumentRange.GetText(-1);

            // Press F9 to generate a new name
            Keyboard.Press(VirtualKeyShort.F9);
            Wait.UntilInputIsProcessed();

            // Wait for name to change
            WaitUntilCondition(
                () => textPattern.DocumentRange.GetText(-1) != initialName,
                "Name should change after F9",
                TimeSpan.FromSeconds(2));

            // Get the new name
            var newName = textPattern.DocumentRange.GetText(-1);

            Assert.Multiple(() =>
            {
                Assert.That(newName, Is.Not.Null.Or.Empty,
                    "F9 should generate a name");
                Assert.That(newName, Is.Not.EqualTo(initialName),
                    "F9 should generate a different name");
            });
        }

        [Test]
        public void F9_WorksWhenAppNotFocused()
        {
            // Set up the random name generator with a class
            NavigateToPage("Random Name Generator");

            _rngPage = VerifyPageLoaded("RandomNameGeneratorPage");

            OpenClassFile("8xCs2.txt");

            // Start notepad to take focus away from the app
            var notepadProcess = Process.Start("notepad.exe");

            try
            {
                // Wait for notepad to be ready
                var notepadWindow = WaitUntilFound<AutomationElement>(
                    () => {
                        var window = Automation!.GetDesktop()
                            .FindFirstChild(cf => cf.ByName("Untitled - Notepad")
                                .Or(cf.ByName("*Untitled - Notepad")));

                        if (window == null)
                        {
                            window = Automation!.GetDesktop()
                                .FindFirstChild(cf => cf.ByClassName("Notepad"));
                        }

                        return window;
                    },
                    "Notepad window should be open",
                    TimeSpan.FromSeconds(5));

                Wait.UntilResponsive(notepadWindow);

                // Get the name display before pressing F9
                var nameDisplay = WaitUntilFound<AutomationElement>(
                    () => _rngPage!.FindFirstDescendant(cf =>
                        cf.ByAutomationId("NameDisplay")),
                    "NameDisplay should exist");

                var textPattern = nameDisplay.Patterns.Text.Pattern;
                var initialName = textPattern.DocumentRange.GetText(-1);

                // Press F9 while notepad has focus
                Keyboard.Press(VirtualKeyShort.F9);
                Wait.UntilInputIsProcessed();

                // Wait for our app to be activated
                WaitUntilCondition(
                    () => !MainWindow!.Properties.IsOffscreen,
                    "F9 should activate the main window",
                    TimeSpan.FromSeconds(3));

                // Wait for name to change
                WaitUntilCondition(
                    () => textPattern.DocumentRange.GetText(-1) != initialName,
                    "Name should change after F9",
                    TimeSpan.FromSeconds(2));

                // Verify a name was generated
                var newName = textPattern.DocumentRange.GetText(-1);
                Assert.That(newName, Is.Not.Null.Or.Empty,
                    "F9 should generate a name even when app was not focused");
            }
            finally
            {
                // Clean up notepad
                if (notepadProcess != null && !notepadProcess.HasExited)
                {
                    notepadProcess.Kill();
                    notepadProcess.WaitForExit(1000);
                }
            }
        }

        [Test]
        public void WinPlusZero_StartsThirtySecondTimer()
        {
            // Press Win+0 to start 30 second timer
            Keyboard.Press(VirtualKeyShort.LWIN);
            Keyboard.Press(VirtualKeyShort.KEY_0);
            Keyboard.Release(VirtualKeyShort.KEY_0);
            Keyboard.Release(VirtualKeyShort.LWIN);
            Wait.UntilInputIsProcessed();

            // Find the timer window using the same method as TimerTests
            var timerWindow = WaitUntilFound<Window>(
                () => FindTimerWindow(),
                "Timer window should appear after Win+0",
                TimeSpan.FromSeconds(5));

            Assert.That(timerWindow, Is.Not.Null, "Win+0 should open a timer window");
            Wait.UntilResponsive(timerWindow!);

            // Verify the timer is set to 30 seconds (using same element ID as TimerTests)
            var timerText = WaitUntilFound<AutomationElement>(
                () => timerWindow.FindFirstDescendant(cf =>
                    cf.ByAutomationId("timerText")),
                "Timer text should be found",
                TimeSpan.FromSeconds(2));

            var displayedTime = timerText.AsTextBox().Text;

            // Should show "30" for 30 seconds
            Assert.That(displayedTime, Is.EqualTo("30"),
                "Timer should be set to 30 seconds");

            // Close the timer window
            timerWindow.Close();
        }

        [Test]
        public void WinPlusOne_StartsOneMinuteTimer()
        {
            // Press Win+1 to start 1 minute timer
            Keyboard.Press(VirtualKeyShort.LWIN);
            Keyboard.Press(VirtualKeyShort.KEY_1);
            Keyboard.Release(VirtualKeyShort.KEY_1);
            Keyboard.Release(VirtualKeyShort.LWIN);
            Wait.UntilInputIsProcessed();

            // Find the timer window using the same method as TimerTests
            var timerWindow = WaitUntilFound<Window>(
                () => FindTimerWindow(),
                "Timer window should appear after Win+1",
                TimeSpan.FromSeconds(5));

            Assert.That(timerWindow, Is.Not.Null, "Win+1 should open a timer window");
            Wait.UntilResponsive(timerWindow!);

            // Verify the timer is set to 1 minute (60 seconds) (using same element ID as TimerTests)
            var timerText = WaitUntilFound<AutomationElement>(
                () => timerWindow.FindFirstDescendant(cf =>
                    cf.ByAutomationId("timerText")),
                "Timer text should be found",
                TimeSpan.FromSeconds(2));

            var displayedTime = timerText.AsTextBox().Text;

            // Should show "1:00" for 1 minute
            Assert.That(displayedTime, Is.EqualTo("1:00"),
                "Timer should be set to 1 minute");

            // Close the timer window
            timerWindow.Close();
        }

        [Test]
        public void WinPlusNine_OpensManualTimerSelection()
        {
            // Press Win+9 to open manual timer selection
            Keyboard.Press(VirtualKeyShort.LWIN);
            Keyboard.Press(VirtualKeyShort.KEY_9);
            Keyboard.Release(VirtualKeyShort.KEY_9);
            Keyboard.Release(VirtualKeyShort.LWIN);
            Wait.UntilInputIsProcessed();

            // Find the timer window using the same method as TimerTests
            var timerWindow = WaitUntilFound<Window>(
                () => FindTimerWindow(),
                "Timer window should appear after Win+9",
                TimeSpan.FromSeconds(5));

            Assert.That(timerWindow, Is.Not.Null, "Win+9 should open a timer window");
            Wait.UntilResponsive(timerWindow!);

            // Verify manual timer mode - should have time selector controls
            var intervalsListView = WaitUntilFound<AutomationElement>(
                () => timerWindow.FindFirstDescendant(cf =>
                    cf.ByAutomationId("intervalsListView")),
                "Time selector should be present for manual timer",
                TimeSpan.FromSeconds(2));

            var startButton = timerWindow.FindFirstDescendant(cf =>
                cf.ByAutomationId("startButton"));

            Assert.Multiple(() =>
            {
                Assert.That(intervalsListView, Is.Not.Null,
                    "Manual timer should have time selector");
                Assert.That(intervalsListView!.IsOffscreen, Is.False,
                    "Time selector should be visible");
                Assert.That(startButton, Is.Not.Null,
                    "Manual timer should have start button");
            });

            // Close the timer window
            timerWindow.Close();
        }

        [Test]
        public void WinPlusNumber_WorksWhenAppNotFocused()
        {
            // Start notepad to take focus away from the app
            var notepadProcess = Process.Start("notepad.exe");

            try
            {
                // Wait for notepad to be ready
                var notepadWindow = WaitUntilFound<AutomationElement>(
                    () => {
                        var window = Automation!.GetDesktop()
                            .FindFirstChild(cf => cf.ByName("Untitled - Notepad")
                                .Or(cf.ByName("*Untitled - Notepad")));

                        if (window == null)
                        {
                            window = Automation!.GetDesktop()
                                .FindFirstChild(cf => cf.ByClassName("Notepad"));
                        }

                        return window;
                    },
                    "Notepad window should be open",
                    TimeSpan.FromSeconds(5));

                Wait.UntilResponsive(notepadWindow);

                // Press Win+1 while notepad has focus
                Keyboard.Press(VirtualKeyShort.LWIN);
                Keyboard.Press(VirtualKeyShort.KEY_1);
                Keyboard.Release(VirtualKeyShort.KEY_1);
                Keyboard.Release(VirtualKeyShort.LWIN);
                Wait.UntilInputIsProcessed();

                // Find the timer window using the same method as TimerTests
                var timerWindow = WaitUntilFound<Window>(
                    () => FindTimerWindow(),
                    "Timer window should appear after Win+1 even when app not focused",
                    TimeSpan.FromSeconds(5));

                Assert.That(timerWindow, Is.Not.Null,
                    "Win+1 should open a timer window even when app is not focused");
                Wait.UntilResponsive(timerWindow!);

                // Close the timer window
                timerWindow.Close();
            }
            finally
            {
                // Clean up notepad
                if (notepadProcess != null && !notepadProcess.HasExited)
                {
                    notepadProcess.Kill();
                    notepadProcess.WaitForExit(1000);
                }
            }
        }

        // Helper method to open a class file
        private void OpenClassFile(string fileName)
        {
            var addClassButton = _rngPage!.FindFirstDescendant(cf =>
                cf.ByName("Add Class"));

            addClassButton?.Click();
            Wait.UntilInputIsProcessed();

            // Find the file dialog
            var fileDialog = WaitUntilFound<AutomationElement>(
                () => {
                    var desktop = Automation!.GetDesktop();
                    return desktop.FindFirst(FlaUI.Core.Definitions.TreeScope.Descendants,
                        new FlaUI.Core.Conditions.AndCondition(
                            new FlaUI.Core.Conditions.PropertyCondition(
                                Automation.PropertyLibrary.Element.ControlType,
                                FlaUI.Core.Definitions.ControlType.Window),
                            new FlaUI.Core.Conditions.PropertyCondition(
                                Automation.PropertyLibrary.Element.ClassName, "#32770")
                        ));
                },
                "File dialog should appear",
                TimeSpan.FromSeconds(10));

            Wait.UntilResponsive(fileDialog);

            // Find the filename input field
            var filenameInput = fileDialog.FindFirst(FlaUI.Core.Definitions.TreeScope.Descendants,
                new FlaUI.Core.Conditions.AndCondition(
                    new FlaUI.Core.Conditions.PropertyCondition(
                        Automation.PropertyLibrary.Element.ControlType,
                        FlaUI.Core.Definitions.ControlType.Edit),
                    new FlaUI.Core.Conditions.PropertyCondition(
                        Automation.PropertyLibrary.Element.AutomationId, "1148")
                ));

            Assert.That(filenameInput, Is.Not.Null, "Filename input field should exist");

            // Get the path to the test file
            var solutionDir = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                @"..\..\..\..\.."));
            var path = Path.Combine(solutionDir, "TeacherToolbox.IntegrationTests", "Files", fileName);

            filenameInput.Focus();
            Keyboard.Type(path);
            Wait.UntilInputIsProcessed();

            Keyboard.Press(VirtualKeyShort.RETURN);
            Wait.UntilInputIsProcessed();

            // Wait for dialog to close
            WaitUntilCondition(
                () => {
                    var desktop = Automation!.GetDesktop();
                    var dialogWindow = desktop.FindFirst(FlaUI.Core.Definitions.TreeScope.Descendants,
                        new FlaUI.Core.Conditions.AndCondition(
                            new FlaUI.Core.Conditions.PropertyCondition(
                                Automation.PropertyLibrary.Element.ControlType,
                                FlaUI.Core.Definitions.ControlType.Window),
                            new FlaUI.Core.Conditions.PropertyCondition(
                                Automation.PropertyLibrary.Element.ClassName, "#32770")
                        ));
                    return dialogWindow == null;
                },
                "File dialog should be closed",
                TimeSpan.FromSeconds(10));
        }

        // Helper method to find timer window using the same approach as TimerTests
        private Window? FindTimerWindow()
        {
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
        }
    }
}
