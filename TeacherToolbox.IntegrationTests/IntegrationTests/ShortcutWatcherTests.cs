using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using NUnit.Framework;

namespace TeacherToolbox.IntegrationTests.IntegrationTests
{
    [TestFixture]
    public class ShortcutWatcherTests : TestBase
    {
        [Test]
        public void ShortcutListener_DoesNotStartExternalProcess()
        {
            var watcherProcess = Process.GetProcessesByName("ShortcutWatcher");

            Assert.That(watcherProcess, Is.Empty);
        }

        [Test]
        public void F9_NavigatesToRandomNameGenerator()
        {
            NavigateToPage("Random Name Generator");
            var rngPage = VerifyPageLoaded("RandomNameGeneratorPage");
            OpenClassFile(rngPage, "8xCs2.txt");

            NavigateToPage("Timer");
            VerifyPageLoaded("TimerSelectionPage");

            Keyboard.Press(VirtualKeyShort.F9);
            Wait.UntilInputIsProcessed();

            var loadedRngPage = WaitUntilFound(
                () => MainWindow!.FindFirstDescendant(cf => cf.ByAutomationId("RandomNameGeneratorPage")),
                "RandomNameGeneratorPage should be loaded after F9",
                TimeSpan.FromSeconds(5));

            Assert.That(loadedRngPage, Is.Not.Null);
        }

        [TestCase(VirtualKeyShort.KEY_0, "30")]
        [TestCase(VirtualKeyShort.KEY_1, "1:00")]
        public void WinPlusNumber_StartsExpectedTimer(VirtualKeyShort numberKey, string expectedText)
        {
            SendShortcutMessage(numberKey);
            Wait.UntilInputIsProcessed();

            var timerWindow = WaitUntilFound(
                () => FindTimerWindow(),
                $"Timer window should appear after Win+{numberKey}",
                TimeSpan.FromSeconds(5));
            Wait.UntilResponsive(timerWindow);

            var timerText = WaitUntilFound(
                () => timerWindow.FindFirstDescendant(cf => cf.ByAutomationId("timerText")),
                "Timer text should be found",
                TimeSpan.FromSeconds(2));

            var actualText = timerText.AsTextBox().Text;
            var expectedConstraint = expectedText == "1:00"
                ? Is.EqualTo("1:00").Or.EqualTo("59")
                : Is.EqualTo(expectedText).Or.EqualTo("29");

            Assert.That(actualText, expectedConstraint);
            timerWindow.Close();
        }

        [Test]
        public void WinPlusNine_OpensManualTimerSelection()
        {
            SendShortcutMessage(VirtualKeyShort.KEY_9);
            Wait.UntilInputIsProcessed();

            var timerWindow = WaitUntilFound(
                () => FindTimerWindow(),
                "Timer window should appear after Win+9",
                TimeSpan.FromSeconds(5));
            Wait.UntilResponsive(timerWindow);

            var intervalsListView = WaitUntilFound(
                () => timerWindow.FindFirstDescendant(cf => cf.ByAutomationId("intervalsListView")),
                "Manual timer selector should be present",
                TimeSpan.FromSeconds(2));

            Assert.That(intervalsListView.IsOffscreen, Is.False);
            timerWindow.Close();
        }

        private static void SendShortcutMessage(VirtualKeyShort numberKey)
        {
            using var pipe = new NamedPipeClientStream(".", "TeacherToolboxShortcutTest", PipeDirection.Out);
            pipe.Connect(3000);
            using var writer = new StreamWriter(pipe) { AutoFlush = true };
            writer.WriteLine($"D{(int)numberKey - (int)VirtualKeyShort.KEY_0}");
        }
    }
}
