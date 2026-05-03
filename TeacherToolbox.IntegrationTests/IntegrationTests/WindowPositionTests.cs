using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using FlaUI.UIA3;

namespace TeacherToolbox.IntegrationTests.IntegrationTests
{
    [TestFixture]
    public class WindowPositionTests
    {
        private Application? _app;
        private UIA3Automation? _automation;
        private Window? _mainWindow;
        private string _appPath = string.Empty;
        private string _settingsPath = string.Empty;

        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Find the solution root by traversing up from the test assembly location
            var assemblyLocation = Assembly.GetExecutingAssembly().Location;
            var dir = new DirectoryInfo(Path.GetDirectoryName(assemblyLocation)!);
            DirectoryInfo? solutionRoot = dir;
            while (solutionRoot != null && !File.Exists(Path.Combine(solutionRoot.FullName, "TeacherToolbox.sln")))
                solutionRoot = solutionRoot.Parent;

            if (solutionRoot == null)
                throw new DirectoryNotFoundException("Could not find solution root (TeacherToolbox.sln not found).");

            // Build the path to the TeacherToolbox.exe in the main app's output directory
            _appPath = Path.Combine(
                solutionRoot.FullName,
                "TeacherToolbox",
                "bin",
                "x86",
                "Debug",
                "net8.0-windows10.0.19041.0",
                "win-x86",
                "TeacherToolbox.exe"
            );

            if (!File.Exists(_appPath))
                throw new FileNotFoundException($"TeacherToolbox.exe not found at {_appPath}. Make sure the app project is built.");

            // Get the settings file path
            _settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TeacherToolbox",
                "settings.json"
            );
        }

        [SetUp]
        public void SetUp()
        {
            // Delete settings file before each test to start fresh
            if (File.Exists(_settingsPath))
            {
                try
                {
                    File.Delete(_settingsPath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Warning: Failed to delete settings: {ex.Message}");
                }
            }
        }

        [TearDown]
        public void TearDown()
        {
            CloseApp();
        }

        private void LaunchApp()
        {
            _app = Application.Launch(_appPath);
            _automation = new UIA3Automation();
            _mainWindow = _app.GetMainWindow(_automation, TimeSpan.FromSeconds(10));
            Assert.That(_mainWindow, Is.Not.Null, "Main window should be found");
        }

        private void CloseApp()
        {
            try
            {
                if (_app?.HasExited == false)
                {
                    _app?.Close();
                    // Wait for app to fully close
                    Thread.Sleep(500);
                }
            }
            finally
            {
                _automation?.Dispose();
                _app?.Dispose();
                _app = null;
                _automation = null;
                _mainWindow = null;
            }
        }

        [Test]
        public void WindowPosition_PersistsAfterRestart()
        {
            // Launch the app
            LaunchApp();

            // Get initial position
            var initialRect = _mainWindow!.BoundingRectangle;
            System.Diagnostics.Debug.WriteLine($"Initial position: {initialRect.X}, {initialRect.Y}");

            // Move window to a new position (offset by 100 pixels)
            int newX = (int)initialRect.X + 100;
            int newY = (int)initialRect.Y + 100;

            // Use Windows API to move the window
            MoveWindow(newX, newY);

            // Verify window moved
            var movedRect = _mainWindow.BoundingRectangle;
            System.Diagnostics.Debug.WriteLine($"Moved position: {movedRect.X}, {movedRect.Y}");

            // Allow some tolerance for window chrome differences
            Assert.That(Math.Abs(movedRect.X - newX), Is.LessThan(10), "Window should have moved to new X position");
            Assert.That(Math.Abs(movedRect.Y - newY), Is.LessThan(10), "Window should have moved to new Y position");

            // Store the position before closing
            double savedX = movedRect.X;
            double savedY = movedRect.Y;

            // Close the app (this should save the position)
            CloseApp();

            // Wait a moment for settings to be written
            Thread.Sleep(500);

            // Verify settings file was created
            Assert.That(File.Exists(_settingsPath), Is.True, "Settings file should exist after closing app");

            // Relaunch the app
            LaunchApp();

            // Get position after relaunch
            var restoredRect = _mainWindow!.BoundingRectangle;
            System.Diagnostics.Debug.WriteLine($"Restored position: {restoredRect.X}, {restoredRect.Y}");

            // Verify position was restored (with some tolerance for window chrome)
            Assert.That(Math.Abs(restoredRect.X - savedX), Is.LessThan(10),
                $"Window X position should be restored. Expected ~{savedX}, got {restoredRect.X}");
            Assert.That(Math.Abs(restoredRect.Y - savedY), Is.LessThan(10),
                $"Window Y position should be restored. Expected ~{savedY}, got {restoredRect.Y}");
        }

        [Test]
        public void WindowSize_PersistsAfterRestart()
        {
            // Launch the app
            LaunchApp();

            // Get initial size
            var initialRect = _mainWindow!.BoundingRectangle;
            System.Diagnostics.Debug.WriteLine($"Initial size: {initialRect.Width}x{initialRect.Height}");

            // Resize window to a new size
            int newWidth = (int)initialRect.Width + 100;
            int newHeight = (int)initialRect.Height + 50;

            ResizeWindow(newWidth, newHeight);

            // Verify window resized
            var resizedRect = _mainWindow.BoundingRectangle;
            System.Diagnostics.Debug.WriteLine($"Resized: {resizedRect.Width}x{resizedRect.Height}");

            // Store the size before closing
            double savedWidth = resizedRect.Width;
            double savedHeight = resizedRect.Height;

            // Close the app
            CloseApp();

            // Wait for settings to be written
            Thread.Sleep(500);

            // Relaunch the app
            LaunchApp();

            // Get size after relaunch
            var restoredRect = _mainWindow!.BoundingRectangle;
            System.Diagnostics.Debug.WriteLine($"Restored size: {restoredRect.Width}x{restoredRect.Height}");

            // Verify size was restored (with some tolerance)
            Assert.That(Math.Abs(restoredRect.Width - savedWidth), Is.LessThan(20),
                $"Window width should be restored. Expected ~{savedWidth}, got {restoredRect.Width}");
            Assert.That(Math.Abs(restoredRect.Height - savedHeight), Is.LessThan(20),
                $"Window height should be restored. Expected ~{savedHeight}, got {restoredRect.Height}");
        }

        [Test]
        public void WindowPosition_UsesDefaultWhenNoSavedPosition()
        {
            // Ensure no settings file exists
            if (File.Exists(_settingsPath))
            {
                File.Delete(_settingsPath);
            }

            // Launch the app
            LaunchApp();

            // Get the position - should be at default
            var rect = _mainWindow!.BoundingRectangle;
            System.Diagnostics.Debug.WriteLine($"Default position: {rect.X}, {rect.Y}, size: {rect.Width}x{rect.Height}");

            // Default size should be 600x200 (as defined in code)
            // Allow tolerance for window chrome
            Assert.That(rect.Width, Is.GreaterThan(500), "Window width should be reasonable default");
            Assert.That(rect.Height, Is.GreaterThan(150), "Window height should be reasonable default");
        }

        private void MoveWindow(int x, int y)
        {
            // Get window handle
            var hwnd = new IntPtr(_mainWindow!.Properties.NativeWindowHandle.Value);

            // Use SetWindowPos to move the window
            SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER);

            // Wait for the move to complete
            Thread.Sleep(200);
        }

        private void ResizeWindow(int width, int height)
        {
            // Get window handle
            var hwnd = new IntPtr(_mainWindow!.Properties.NativeWindowHandle.Value);

            // Get current position
            var rect = _mainWindow.BoundingRectangle;

            // Use SetWindowPos to resize the window
            SetWindowPos(hwnd, IntPtr.Zero, (int)rect.X, (int)rect.Y, width, height, SWP_NOZORDER);

            // Wait for the resize to complete
            Thread.Sleep(200);
        }

        [Test]
        public void WindowPosition_PersistsOnSecondaryMonitor()
        {
            // Get all monitors
            var monitors = GetAllMonitors();

            if (monitors.Count < 2)
            {
                Assert.Ignore("Test requires multiple monitors. Skipping.");
                return;
            }

            // Find a secondary monitor (not the primary)
            var secondaryMonitor = monitors.FirstOrDefault(m => !m.IsPrimary);
            if (secondaryMonitor == null)
            {
                Assert.Ignore("Could not find secondary monitor. Skipping.");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"Secondary monitor bounds: {secondaryMonitor.Bounds}");

            // Launch the app
            LaunchApp();

            // Move window to the secondary monitor
            int newX = secondaryMonitor.Bounds.Left + 50;
            int newY = secondaryMonitor.Bounds.Top + 50;

            MoveWindow(newX, newY);

            // Verify window moved to secondary monitor
            var movedRect = _mainWindow!.BoundingRectangle;
            System.Diagnostics.Debug.WriteLine($"Moved to secondary monitor: {movedRect.X}, {movedRect.Y}");

            // Store the position before closing
            double savedX = movedRect.X;
            double savedY = movedRect.Y;

            // Close the app
            CloseApp();

            // Wait for settings to be written
            Thread.Sleep(500);

            // Relaunch the app
            LaunchApp();

            // Get position after relaunch
            var restoredRect = _mainWindow!.BoundingRectangle;
            System.Diagnostics.Debug.WriteLine($"Restored position on secondary: {restoredRect.X}, {restoredRect.Y}");

            // Verify position was restored on secondary monitor
            Assert.That(Math.Abs(restoredRect.X - savedX), Is.LessThan(10),
                $"Window X position should be restored on secondary monitor. Expected ~{savedX}, got {restoredRect.X}");
            Assert.That(Math.Abs(restoredRect.Y - savedY), Is.LessThan(10),
                $"Window Y position should be restored on secondary monitor. Expected ~{savedY}, got {restoredRect.Y}");

            // Verify window is actually on the secondary monitor
            Assert.That(restoredRect.X, Is.GreaterThanOrEqualTo(secondaryMonitor.Bounds.Left - 10),
                "Window should be on secondary monitor (X check)");
        }

        [Test]
        public void WindowPosition_FallsBackWhenMonitorDisconnected()
        {
            // This test simulates the scenario by writing invalid coordinates to settings
            // and verifying the app falls back to defaults

            // Create a settings file with coordinates that don't exist on any monitor
            // (e.g., X = -5000 which is unlikely to be a valid position)
            var settingsDir = Path.GetDirectoryName(_settingsPath);
            if (!Directory.Exists(settingsDir))
            {
                Directory.CreateDirectory(settingsDir!);
            }

            // Write settings with an invalid position (simulating a disconnected monitor)
            var invalidSettings = @"{
                ""LastWindowPosition"": {
                    ""X"": -5000,
                    ""Y"": -5000,
                    ""Width"": 600,
                    ""Height"": 200,
                    ""DisplayID"": 99999
                }
            }";
            File.WriteAllText(_settingsPath, invalidSettings);

            // Launch the app
            LaunchApp();

            // Get the position - should fall back to default on primary monitor
            var rect = _mainWindow!.BoundingRectangle;
            System.Diagnostics.Debug.WriteLine($"Position after invalid settings: {rect.X}, {rect.Y}");

            // Get primary monitor bounds
            var monitors = GetAllMonitors();
            var primaryMonitor = monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors.First();

            // Window should be on a valid monitor, not at -5000,-5000
            Assert.That(rect.X, Is.GreaterThanOrEqualTo(primaryMonitor.Bounds.Left - 100),
                "Window should fall back to valid position when saved position is invalid");
            Assert.That(rect.Y, Is.GreaterThanOrEqualTo(primaryMonitor.Bounds.Top - 100),
                "Window should fall back to valid position when saved position is invalid");
        }

        #region Monitor Detection Helpers

        private System.Collections.Generic.List<MonitorInfo> GetAllMonitors()
        {
            var monitors = new System.Collections.Generic.List<MonitorInfo>();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
                {
                    var info = new MONITORINFOEX();
                    info.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(info);
                    if (GetMonitorInfo(hMonitor, ref info))
                    {
                        monitors.Add(new MonitorInfo
                        {
                            Bounds = new System.Drawing.Rectangle(
                                info.rcMonitor.Left,
                                info.rcMonitor.Top,
                                info.rcMonitor.Right - info.rcMonitor.Left,
                                info.rcMonitor.Bottom - info.rcMonitor.Top),
                            WorkArea = new System.Drawing.Rectangle(
                                info.rcWork.Left,
                                info.rcWork.Top,
                                info.rcWork.Right - info.rcWork.Left,
                                info.rcWork.Bottom - info.rcWork.Top),
                            IsPrimary = (info.dwFlags & MONITORINFOF_PRIMARY) != 0
                        });
                    }
                    return true;
                }, IntPtr.Zero);
            return monitors;
        }

        private class MonitorInfo
        {
            public System.Drawing.Rectangle Bounds { get; set; }
            public System.Drawing.Rectangle WorkArea { get; set; }
            public bool IsPrimary { get; set; }
        }

        #endregion

        #region P/Invoke Declarations

        // P/Invoke for SetWindowPos
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;

        // P/Invoke for monitor enumeration
        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        private const uint MONITORINFOF_PRIMARY = 0x00000001;

        #endregion
    }
}
