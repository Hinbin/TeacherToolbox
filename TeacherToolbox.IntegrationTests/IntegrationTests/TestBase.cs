using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using NUnit.Framework;

namespace TeacherToolbox.IntegrationTests.IntegrationTests;

public abstract class TestBase
{
    protected Application? App { get; private set; }
    protected UIA3Automation? Automation { get; private set; }
    protected Window? MainWindow { get; private set; }
    protected AutomationElement? NavigationPane;
    private int? _appProcessId;
    private bool _sharedLaunchActive;

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DialogTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan NavigationToggleTimeout = TimeSpan.FromSeconds(2);

    private readonly string[] _filesToDelete =
    [
        "settings.json",
        "centreNumber.json",
        "classes.json",
        "testPickFile.txt"
    ];

    protected string SolutionRoot { get; private set; } = string.Empty;
    protected string AppPath { get; private set; } = string.Empty;

    public TestBase()
    {
        Retry.DefaultTimeout = DefaultTimeout;
        Retry.DefaultInterval = TimeSpan.FromMilliseconds(100);
        ResolvePaths();
    }

    // Call from [OneTimeSetUp] in a [FixtureLifeCycle(SingleInstance)] fixture to launch
    // the app once for all tests in the fixture instead of once per test.
    protected void EnableSharedLaunch()
    {
        _sharedLaunchActive = true;
        KillTeacherToolboxProcesses();
        KillShortcutWatcherProcesses();
        DeleteAppDataFiles();
        LaunchApp();
    }

    // Call from [OneTimeTearDown] to close the shared app instance.
    protected void TearDownSharedLaunch()
    {
        _sharedLaunchActive = false;
        CloseApp();
    }

    [SetUp]
    public void BaseSetUp()
    {
        if (_sharedLaunchActive) return;
        KillTeacherToolboxProcesses();
        KillShortcutWatcherProcesses();
        DeleteAppDataFiles();
        LaunchApp();
    }

    [TearDown]
    public void BaseTearDown()
    {
        if (_sharedLaunchActive) return;
        CloseApp();
    }

    protected void LaunchApp()
    {
        if (App?.HasExited == false)
        {
            return;
        }

        if (!File.Exists(AppPath))
        {
            throw new FileNotFoundException($"TeacherToolbox.exe not found at {AppPath}. Make sure the app project is built.");
        }

        try
        {
            Environment.SetEnvironmentVariable("TEACHER_TOOLBOX_TEST_SHORTCUT_PIPE", "1");
            Environment.SetEnvironmentVariable("TEACHER_TOOLBOX_TEST_PICK_FILE_PATH_FILE", TestPickFilePath);
            App = Application.Launch(AppPath);
            Automation = new UIA3Automation();
            MainWindow = App.GetMainWindow(Automation, TimeSpan.FromSeconds(10));
            _appProcessId = GetProcessId(MainWindow!);
            NavigationPane = MainWindow!.FindFirstDescendant(cf => cf.ByAutomationId("NavigationPane"));
            Assert.That(MainWindow, Is.Not.Null, "Main window should be found");
        }
        catch (Exception ex)
        {
            CloseApp();
            throw new Exception("Failed to launch or initialize application. Make sure the application is built correctly.", ex);
        }
    }

    protected void CloseApp()
    {
        try
        {
            if (App?.HasExited == false)
            {
                try
                {
                    App.Close();
                }
                catch
                {
                    KillLaunchedAppProcess();
                }

                try
                {
                    var stopwatch = Stopwatch.StartNew();
                    while (!App.HasExited && stopwatch.Elapsed < TimeSpan.FromSeconds(5))
                    {
                        System.Threading.Thread.Sleep(100);
                    }

                    if (!App.HasExited)
                    {
                        KillLaunchedAppProcess();
                    }
                }
                catch
                {
                    KillLaunchedAppProcess();
                }
            }
        }
        finally
        {
            KillShortcutWatcherProcesses();
            Automation?.Dispose();
            App?.Dispose();
            Automation = null;
            App = null;
            MainWindow = null;
            NavigationPane = null;
            _appProcessId = null;
        }
    }

    private static void KillTeacherToolboxProcesses()
    {
        foreach (var process in Process.GetProcessesByName("TeacherToolbox"))
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(3000);
                }
            }
            catch (Exception ex)
            {
                TestContext.Out.WriteLine($"Failed to kill TeacherToolbox process {process.Id}: {ex.Message}");
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private void KillLaunchedAppProcess()
    {
        if (_appProcessId == null)
        {
            return;
        }

        try
        {
            var process = Process.GetProcessById(_appProcessId.Value);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(3000);
            }
        }
        catch (Exception ex)
        {
            TestContext.Out.WriteLine($"Failed to kill TeacherToolbox process {_appProcessId}: {ex.Message}");
        }
    }

    private static void KillShortcutWatcherProcesses()
    {
        foreach (var process in Process.GetProcessesByName("ShortcutWatcher"))
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(3000);
                }
            }
            catch (Exception ex)
            {
                TestContext.Out.WriteLine($"Failed to kill ShortcutWatcher process {process.Id}: {ex.Message}");
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    protected void SafeClick(AutomationElement element)
    {
        var result = Retry.WhileException(
            () => element.Click(),
            TimeSpan.FromSeconds(2),
            null,
            true);

        Assert.That(result.Success, Is.True, $"Failed to click element: {element.Name}");
        Wait.UntilInputIsProcessed();
    }

    protected void EnsureNavigationIsOpen()
    {
        NavigationPane = WaitUntilFound(
            () => MainWindow!.FindFirstDescendant(cf => cf.ByAutomationId("NavigationPane")),
            "Navigation pane should be available");

        if (NavigationPane.FindFirstChild(cf => cf.ByName("Close Navigation")) != null)
        {
            return;
        }

        var openButton = WaitUntilFound(
            () => NavigationPane.FindFirstChild(cf => cf.ByName("Open Navigation")),
            "Open Navigation button should be available");

        for (var retry = 0; retry < 3; retry++)
        {
            openButton.Click();
            Wait.UntilInputIsProcessed();

            try
            {
                WaitUntilFound(
                    () => NavigationPane.FindFirstChild(cf => cf.ByName("Close Navigation")),
                    "Close Navigation button after opening",
                    NavigationToggleTimeout);
                return;
            }
            catch (TimeoutException) when (retry < 2)
            {
            }
        }

        throw new TimeoutException("Failed to open navigation pane after 3 attempts");
    }

    protected void NavigateToPage(string pageName)
    {
        DismissOpenTeachingTip();
        EnsureNavigationIsOpen();

        var navItem = WaitUntilFound(
            () => NavigationPane!.FindFirstDescendant(cf => cf.ByControlType(ControlType.ListItem).And(cf.ByName(pageName)))
                ?? NavigationPane.FindFirstDescendant(cf => cf.ByName(pageName)),
            $"Navigation item '{pageName}' should be found");

        ScrollElementIntoView(navItem);
        WaitUntilCondition(
            () => !navItem.Properties.IsOffscreen,
            $"Navigation item '{pageName}' should be visible on screen");

        navItem.Focus();
        if (navItem.Patterns.Invoke.IsSupported)
        {
            navItem.Patterns.Invoke.Pattern.Invoke();
        }
        else
        {
            navItem.Click();
        }
        Wait.UntilInputIsProcessed();
    }

    private void DismissOpenTeachingTip()
    {
        try
        {
            var dismissButton = MainWindow?.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("Got it!")));
            dismissButton?.Click();
            Keyboard.Press(VirtualKeyShort.ESCAPE);
            Wait.UntilInputIsProcessed();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Warning: couldn't dismiss teaching tip: {ex.Message}");
        }
    }

    protected AutomationElement VerifyPageLoaded(string pageId)
    {
        return WaitUntilFound(
            () => MainWindow!.FindFirstDescendant(cf => cf.ByAutomationId(pageId)),
            $"{pageId} page should be loaded");
    }

    protected T WaitUntilFound<T>(Func<T?> findFunc, string errorMessage, TimeSpan? timeout = null) where T : class
    {
        var result = Retry.WhileNull(findFunc, timeout ?? DefaultTimeout, TimeSpan.FromMilliseconds(100), true);
        if (result.Result == null)
        {
            Assert.Fail($"Timeout occurred: {errorMessage} (timeout: {timeout ?? DefaultTimeout})");
        }

        return result.Result!;
    }

    protected void WaitUntilCondition(Func<bool> conditionFunc, string errorMessage, TimeSpan? timeout = null)
    {
        var result = Retry.WhileFalse(conditionFunc, timeout ?? DefaultTimeout, TimeSpan.FromMilliseconds(100), true);
        Assert.That(result.Result, Is.True, errorMessage);
    }

    protected static string GetDisplayedText(AutomationElement element)
    {
        try
        {
            var textBoxText = element.AsTextBox().Text;
            if (!string.IsNullOrEmpty(textBoxText))
            {
                return textBoxText;
            }
        }
        catch
        {
        }

        return element.Name;
    }

    protected void ScrollElementIntoView(AutomationElement? element)
    {
        if (element == null)
        {
            return;
        }

        try
        {
            if (element.Patterns.ScrollItem.IsSupported)
            {
                element.Patterns.ScrollItem.Pattern.ScrollIntoView();
                Wait.UntilInputIsProcessed();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Warning: couldn't scroll element into view: {ex.Message}");
        }
    }

    protected Window? FindTimerWindow()
    {
        try
        {
            using var automation = new UIA3Automation();
            var desktop = automation.GetDesktop();
            var windows = desktop.FindAllChildren(cf => cf.ByControlType(ControlType.Window));
            return windows
                .FirstOrDefault(w => w.Name.Contains("Timer") && !w.Name.Contains("Visual Studio") && w != MainWindow)
                ?.AsWindow();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in timer window search: {ex.Message}");
            return null;
        }
    }

    protected void OpenClassFile(AutomationElement rngPage, string fileName)
    {
        var path = Path.Combine(SolutionRoot, "TeacherToolbox.IntegrationTests", "Files", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(TestPickFilePath)!);
        File.WriteAllText(TestPickFilePath, path);

        var addClassButton = WaitUntilFound(
            () => rngPage.FindFirstDescendant(cf => cf.ByAutomationId("AddClassButton"))
                ?? rngPage.FindFirstDescendant(cf => cf.ByName("Add Class")),
            "Add Class button should exist");

        try
        {
            addClassButton.Click();
            Wait.UntilInputIsProcessed();

            WaitUntilFound(
                () => rngPage.FindFirstDescendant(cf => cf.ByName(Path.GetFileNameWithoutExtension(fileName))),
                "Class button should appear after opening a class file",
                DialogTimeout);
            return;
        }
        finally
        {
            try
            {
                File.Delete(TestPickFilePath);
            }
            catch
            {
            }
        }
    }

    protected void CleanupProcess(Process? process)
    {
        if (process == null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(3000);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Process cleanup failed: {ex.Message}");
        }
    }

    protected void DeleteAppDataFiles()
    {
        var localAppData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TeacherToolbox");

        foreach (var fileName in _filesToDelete)
        {
            var filePath = Path.Combine(localAppData, fileName);
            if (!File.Exists(filePath))
            {
                continue;
            }

            try
            {
                File.Delete(filePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Warning: failed to delete {filePath}: {ex.Message}");
            }
        }
    }

    private void ResolvePaths()
    {
        var assemblyLocation = Assembly.GetExecutingAssembly().Location;
        var dir = new DirectoryInfo(Path.GetDirectoryName(assemblyLocation)!);
        DirectoryInfo? solutionRoot = dir;

        while (solutionRoot != null && !File.Exists(Path.Combine(solutionRoot.FullName, "TeacherToolbox.sln")))
        {
            solutionRoot = solutionRoot.Parent;
        }

        if (solutionRoot == null)
        {
            throw new DirectoryNotFoundException("Could not find solution root (TeacherToolbox.sln not found).");
        }

        SolutionRoot = solutionRoot.FullName;
        AppPath = Path.Combine(
            SolutionRoot,
            "TeacherToolbox",
            "bin",
            "x86",
            "Debug",
            "net8.0-windows10.0.19041.0",
            "win-x86",
            "TeacherToolbox.exe");
    }

    private static int GetProcessId(Window window)
    {
        var hwnd = new IntPtr(window.Properties.NativeWindowHandle.Value);
        GetWindowThreadProcessId(hwnd, out var processId);
        return (int)processId;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    private static string TestPickFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TeacherToolbox",
        "testPickFile.txt");
}
