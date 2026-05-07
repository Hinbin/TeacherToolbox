using System;
using System.IO;
using NUnit.Framework;

namespace TeacherToolbox.IntegrationTests.IntegrationTests
{
    [TestFixture]
    public class WindowPositionTests : TestBase
    {
        private string SettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TeacherToolbox",
            "settings.json");

        [Test]
        public void WindowPosition_PersistsAfterRestart()
        {
            var initialRect = MainWindow!.BoundingRectangle;
            var newX = initialRect.X + 100;
            var newY = initialRect.Y + 100;

            MoveWindow((int)newX, (int)newY);
            WaitUntilCondition(
                () => Math.Abs(MainWindow!.BoundingRectangle.X - newX) < 10 &&
                      Math.Abs(MainWindow.BoundingRectangle.Y - newY) < 10,
                "Window should move to the requested position");

            var savedX = MainWindow.BoundingRectangle.X;
            var savedY = MainWindow.BoundingRectangle.Y;

            CloseApp();
            WaitUntilCondition(() => File.Exists(SettingsPath), "Settings file should be written after app close");
            LaunchApp();

            var restoredRect = MainWindow!.BoundingRectangle;
            Assert.Multiple(() =>
            {
                Assert.That(Math.Abs(restoredRect.X - savedX), Is.LessThan(10));
                Assert.That(Math.Abs(restoredRect.Y - savedY), Is.LessThan(10));
            });
        }

        [Test]
        public void WindowPosition_FallsBackWhenSavedPositionIsInvalid()
        {
            CloseApp();

            var settingsDir = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(settingsDir);
            File.WriteAllText(SettingsPath, @"{
                ""LastWindowPosition"": {
                    ""X"": -5000,
                    ""Y"": -5000,
                    ""Width"": 600,
                    ""Height"": 200,
                    ""DisplayID"": 99999
                }
            }");

            LaunchApp();
            var rect = MainWindow!.BoundingRectangle;

            Assert.Multiple(() =>
            {
                Assert.That(rect.X, Is.GreaterThan(-1000));
                Assert.That(rect.Y, Is.GreaterThan(-1000));
                Assert.That(rect.Width, Is.GreaterThan(500));
                Assert.That(rect.Height, Is.GreaterThan(150));
            });
        }

        private void MoveWindow(int x, int y)
        {
            var hwnd = new IntPtr(MainWindow!.Properties.NativeWindowHandle.Value);
            SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
    }
}
