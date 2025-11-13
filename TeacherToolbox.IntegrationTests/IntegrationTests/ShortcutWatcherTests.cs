using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.Core.Tools;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace TeacherToolbox.IntegrationTests.IntegrationTests
{
    [TestFixture]
    public class ShortcutWatcherTests : TestBase
    {
        private AutomationElement? _rngPage;

        [SetUp]
        public void ShortcutWatcherSetUp()
        {
            // Give the ShortcutWatcher process time to start and connect
            Thread.Sleep(2000);

            // Verify ShortcutWatcher is running
            var watcherProcess = Process.GetProcessesByName("ShortcutWatcher");
            Assert.That(watcherProcess.Length, Is.GreaterThan(0),
                "ShortcutWatcher.exe should be running");
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
            // First, navigate away from RandomNameGenerator to ensure F9 brings us back
            NavigateToPage("Timer");
            Thread.Sleep(500);

            // Verify we're not on RandomNameGenerator page
            var timerPage = MainWindow!.FindFirstDescendant(cf =>
                cf.ByAutomationId("TimerPage"));
            Assert.That(timerPage, Is.Not.Null,
                "Should be on Timer page before F9 is pressed");

            // Press F9
            Keyboard.Press(VirtualKeyShort.F9);

            // Wait for navigation to complete
            Thread.Sleep(1000);

            // Verify we're now on RandomNameGenerator page
            var rngPage = WaitUntilFound<AutomationElement>(
                () => MainWindow.FindFirstDescendant(cf =>
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
            Thread.Sleep(500);

            _rngPage = WaitUntilFound<AutomationElement>(
                () => MainWindow!.FindFirstDescendant(cf =>
                    cf.ByAutomationId("RandomNameGeneratorPage")),
                "RandomNameGeneratorPage should be loaded");

            // Add a class file
            OpenClassFile("8xCs2.txt");
            Thread.Sleep(1000);

            // Get the name display element
            var nameDisplay = _rngPage!.FindFirstDescendant(cf =>
                cf.ByAutomationId("NameDisplay"));
            Assert.That(nameDisplay, Is.Not.Null, "NameDisplay should exist");

            var textPattern = nameDisplay.Patterns.Text.Pattern;
            var initialName = textPattern.DocumentRange.GetText(-1);

            // Press F9 to generate a new name
            Keyboard.Press(VirtualKeyShort.F9);
            Thread.Sleep(500);

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
            Thread.Sleep(500);

            _rngPage = WaitUntilFound<AutomationElement>(
                () => MainWindow!.FindFirstDescendant(cf =>
                    cf.ByAutomationId("RandomNameGeneratorPage")),
                "RandomNameGeneratorPage should be loaded");

            OpenClassFile("8xCs2.txt");
            Thread.Sleep(1000);

            // Start notepad to take focus away from the app
            var notepadProcess = Process.Start("notepad.exe");
            Thread.Sleep(1000);

            try
            {
                // Verify notepad has focus
                var notepadWindow = Automation!.GetDesktop()
                    .FindFirstChild(cf => cf.ByName("Untitled - Notepad")
                        .Or(cf.ByName("*Untitled - Notepad")));

                if (notepadWindow == null)
                {
                    // Try alternative notepad window names
                    notepadWindow = Automation!.GetDesktop()
                        .FindFirstChild(cf => cf.ByClassName("Notepad"));
                }

                Assert.That(notepadWindow, Is.Not.Null,
                    "Notepad window should be open");

                // Get the name display before pressing F9
                var nameDisplay = _rngPage!.FindFirstDescendant(cf =>
                    cf.ByAutomationId("NameDisplay"));
                var textPattern = nameDisplay.Patterns.Text.Pattern;
                var initialName = textPattern.DocumentRange.GetText(-1);

                // Press F9 while notepad has focus
                Keyboard.Press(VirtualKeyShort.F9);
                Thread.Sleep(1000);

                // Verify that our app now has focus (F9 should activate it)
                var isAppFocused = !MainWindow!.Properties.IsOffscreen;
                Assert.That(isAppFocused, Is.True,
                    "F9 should activate the main window");

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

            // Wait for timer window to appear
            Thread.Sleep(1500);

            // Find the timer window
            var timerWindow = WaitUntilFound<Window>(
                () => Automation!.GetDesktop().FindFirstChild(cf =>
                    cf.ByName("Timer").And(cf.ByClassName("WinUIDesktopWin32WindowClass"))),
                "Timer window should appear after Win+0",
                TimeSpan.FromSeconds(5));

            Assert.That(timerWindow, Is.Not.Null,
                "Win+0 should open a timer window");

            // Verify the timer is set to 30 seconds
            var timeDisplay = timerWindow.FindFirstDescendant(cf =>
                cf.ByAutomationId("TimeDisplay"));

            if (timeDisplay != null)
            {
                var textPattern = timeDisplay.Patterns.Text.Pattern;
                var timeText = textPattern.DocumentRange.GetText(-1);

                // Should show something like "00:30" or "0:30"
                Assert.That(timeText, Does.Contain("30"),
                    "Timer should be set to 30 seconds");
            }

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

            // Wait for timer window to appear
            Thread.Sleep(1500);

            // Find the timer window
            var timerWindow = WaitUntilFound<Window>(
                () => Automation!.GetDesktop().FindFirstChild(cf =>
                    cf.ByName("Timer").And(cf.ByClassName("WinUIDesktopWin32WindowClass"))),
                "Timer window should appear after Win+1",
                TimeSpan.FromSeconds(5));

            Assert.That(timerWindow, Is.Not.Null,
                "Win+1 should open a timer window");

            // Verify the timer is set to 1 minute (60 seconds)
            var timeDisplay = timerWindow.FindFirstDescendant(cf =>
                cf.ByAutomationId("TimeDisplay"));

            if (timeDisplay != null)
            {
                var textPattern = timeDisplay.Patterns.Text.Pattern;
                var timeText = textPattern.DocumentRange.GetText(-1);

                // Should show something like "01:00" or "1:00"
                Assert.That(timeText, Does.Match("0?1:00"),
                    "Timer should be set to 1 minute");
            }

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

            // Wait for timer window to appear
            Thread.Sleep(1500);

            // Find the timer window
            var timerWindow = WaitUntilFound<Window>(
                () => Automation!.GetDesktop().FindFirstChild(cf =>
                    cf.ByName("Timer").And(cf.ByClassName("WinUIDesktopWin32WindowClass"))),
                "Timer window should appear after Win+9",
                TimeSpan.FromSeconds(5));

            Assert.That(timerWindow, Is.Not.Null,
                "Win+9 should open a timer window");

            // Verify we're in manual mode (timer should be at 00:00)
            var timeDisplay = timerWindow.FindFirstDescendant(cf =>
                cf.ByAutomationId("TimeDisplay"));

            if (timeDisplay != null)
            {
                var textPattern = timeDisplay.Patterns.Text.Pattern;
                var timeText = textPattern.DocumentRange.GetText(-1);

                // Should show "00:00" for manual timer
                Assert.That(timeText, Does.Contain("00:00"),
                    "Manual timer should start at 00:00");
            }

            // Close the timer window
            timerWindow.Close();
        }

        [Test]
        public void WinPlusNumber_WorksWhenAppNotFocused()
        {
            // Start notepad to take focus away from the app
            var notepadProcess = Process.Start("notepad.exe");
            Thread.Sleep(1000);

            try
            {
                // Press Win+1 while notepad has focus
                Keyboard.Press(VirtualKeyShort.LWIN);
                Keyboard.Press(VirtualKeyShort.KEY_1);
                Keyboard.Release(VirtualKeyShort.KEY_1);
                Keyboard.Release(VirtualKeyShort.LWIN);

                // Wait for timer window to appear
                Thread.Sleep(1500);

                // Find the timer window - it should appear even though app didn't have focus
                var timerWindow = WaitUntilFound<Window>(
                    () => Automation!.GetDesktop().FindFirstChild(cf =>
                        cf.ByName("Timer").And(cf.ByClassName("WinUIDesktopWin32WindowClass"))),
                    "Timer window should appear after Win+1 even when app not focused",
                    TimeSpan.FromSeconds(5));

                Assert.That(timerWindow, Is.Not.Null,
                    "Win+1 should open a timer window even when app is not focused");

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

            Thread.Sleep(200);
            Keyboard.Press(VirtualKeyShort.RETURN);

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
    }
}
