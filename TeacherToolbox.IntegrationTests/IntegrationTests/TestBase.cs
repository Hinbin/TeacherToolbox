using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using Microsoft.UI.Xaml.Controls;
using NUnit.Framework;
using OpenQA.Selenium.BiDi.Modules.Script;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;


namespace TeacherToolbox.IntegrationTests.IntegrationTests;

public class TestBase
{
    protected Application? App { get; private set; }
    protected UIA3Automation? Automation { get; private set; }
    protected Window? MainWindow { get; private set; }
    protected AutomationElement? NavigationPane;
    
    // Maximum number of retries for opening the navigation pane
    private const int MaxOpenRetries = 3;

    // Define global timeout settings
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DialogTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan NavigationToggleTimeout = TimeSpan.FromSeconds(2);


    public TestBase()
    {
        // Configure default retry settings
        Retry.DefaultTimeout = DefaultTimeout;
        Retry.DefaultInterval = TimeSpan.FromMilliseconds(100);
    }
    // Array of files to delete during setup
    private readonly string[] filesToDelete = new[]
    {
        "settings.json",
        "centreNumber.json",
        "classes.json"
    };

    [SetUp]
    public void BaseSetUp()
    {
        // Delete specified files if they exist
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\TeacherToolbox";
        foreach (var fileName in filesToDelete)
        {
            string filePath = Path.Combine(localAppData, fileName);
            if (File.Exists(filePath))
            {
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Warning: Failed to delete {filePath}: {ex.Message}");
                }
            }
        }

        // Find the solution root by traversing up from the test assembly location
        var assemblyLocation = Assembly.GetExecutingAssembly().Location;
        var dir = new DirectoryInfo(Path.GetDirectoryName(assemblyLocation)!);
        DirectoryInfo? solutionRoot = dir;
        while (solutionRoot != null && !File.Exists(Path.Combine(solutionRoot.FullName, "TeacherToolbox.sln")))
            solutionRoot = solutionRoot.Parent;

        if (solutionRoot == null)
            throw new DirectoryNotFoundException("Could not find solution root (TeacherToolbox.sln not found).");

        // Build the path to the TeacherToolbox.exe in the main app's output directory
        var appPath = Path.Combine(
            solutionRoot.FullName,
            "TeacherToolbox",
            "bin",
            "x86",
            "Debug", // or "Release" if you want to run release builds
            "net8.0-windows10.0.19041.0",
            "win-x86",
            "TeacherToolbox.exe"
        );

        if (!File.Exists(appPath))
            throw new FileNotFoundException($"TeacherToolbox.exe not found at {appPath}. Make sure the app project is built.");

        try
        {
            // Launch the application
            App = Application.Launch(appPath);
            Automation = new UIA3Automation();
            MainWindow = App.GetMainWindow(Automation, TimeSpan.FromSeconds(10));
            NavigationPane = MainWindow!.FindFirstDescendant(cf => cf.ByAutomationId("NavigationPane"));
            Assert.That(MainWindow, Is.Not.Null, "Main window should be found");
        }
        catch (Exception ex)
        {
            // Clean up if we failed to get the main window
            Automation?.Dispose();
            App?.Dispose();
            throw new Exception("Failed to launch or initialize application. Make sure the application is built correctly.", ex);
        }
    }

    [TearDown]
    public void BaseTearDown()
    {
        try
        {
            if (App?.HasExited == false)
            {
                App?.Close();
            }
        }
        finally
        {
            Automation?.Dispose();
            App?.Dispose();
        }
    }

    [Test]
    public void Application_LaunchesSuccessfully()
    {
        Assert.Multiple(() =>
        {
            Assert.That(App, Is.Not.Null, "Application should be launched");
            Assert.That(App!.HasExited, Is.False, "Application should be running");
            Assert.That(MainWindow, Is.Not.Null, "Main window should be available");
            Assert.That(MainWindow!.Title, Is.EqualTo("Teacher Toolbox"), "Window title should match");
        });
    }

    protected void SafeClick(AutomationElement element)
    {
        var clickResult = Retry.WhileException(
            () => element.Click(),
            TimeSpan.FromSeconds(2),
            null,  // no exception handler needed since we're using the result
            true   // throws original exception if retry fails
        );

        Assert.That(clickResult.Success, Is.True,
            $"Failed to click element: {element.Name}");
    }


    protected void EnsureNavigationIsOpen()
    {
        // First ensure that NavigationPane is available
        var navPane = WaitUntilFound<AutomationElement>(
            () => MainWindow!.FindFirstDescendant(cf => cf.ByAutomationId("NavigationPane")),
            "Navigation pane should be available");

        // NavigationPane is now guaranteed to be not null
        NavigationPane = navPane;

        // Try to find the close button first to see if navigation is already open
        var closeButton = navPane.FindFirstChild(cf => cf.ByName("Close Navigation"));

        // If close button exists, navigation is already open
        if (closeButton != null)
        {
            System.Diagnostics.Debug.WriteLine("Navigation is already open");
            return;
        }

        // Navigation is closed, find the open button
        var openButton = WaitUntilFound<AutomationElement>(
            () => navPane.FindFirstChild(cf => cf.ByName("Open Navigation")),
            "Open Navigation button should be available");

        // Try to open the navigation pane with retries
        for (int retry = 0; retry < MaxOpenRetries; retry++)
        {
            System.Diagnostics.Debug.WriteLine($"Attempting to open navigation (attempt {retry + 1} of {MaxOpenRetries})");

            // Click the open button
            openButton.Click();

            try
            {
                // Try to find the close button with a shorter timeout
                WaitUntilFound<AutomationElement>(
                    () => navPane.FindFirstChild(cf => cf.ByName("Close Navigation")),
                    "Close Navigation button after opening",
                    NavigationToggleTimeout);

                // If we get here, navigation is successfully opened
                System.Diagnostics.Debug.WriteLine("Navigation successfully opened");
                return;
            }
            catch (TimeoutException)
            {
                if (retry == MaxOpenRetries - 1)
                {
                    // Last retry, rethrow with more context
                    throw new TimeoutException(
                        $"Failed to open navigation pane after {MaxOpenRetries} attempts");
                }

                System.Diagnostics.Debug.WriteLine("Navigation didn't open, retrying...");

                // Small delay before retrying to give UI time to settle
                System.Threading.Thread.Sleep(200);
            }
        }

        // We shouldn't reach here, but if we do:
        throw new InvalidOperationException("Navigation pane could not be opened");
    }

    // Improved method to click navigation buttons without Thread.Sleep
    protected void ClickNavigationButton(string buttonName)
    {
        // Ensure NavigationPane is available
        if (NavigationPane == null)
        {
            NavigationPane = WaitUntilFound<AutomationElement>(
                () => MainWindow!.FindFirstDescendant(cf => cf.ByAutomationId("NavView")),
                "Navigation view should be available");
        }

        // Find the button with waiting
        var button = WaitUntilFound<AutomationElement>(
            () => NavigationPane.FindFirstChild(cf => cf.ByName(buttonName)),
            $"Navigation button '{buttonName}' should be found");

        // Click the button
        button.Click();

        // Instead of Thread.Sleep, wait for appropriate state change
        if (buttonName == "Open Navigation")
        {
            // If we opened navigation, wait for close button to appear
            WaitUntilFound<AutomationElement>(
                () => NavigationPane.FindFirstChild(cf => cf.ByName("Close Navigation")),
                "Navigation should open after clicking open button");
        }
        else if (buttonName == "Close Navigation")
        {
            // If we closed navigation, wait for open button to appear
            WaitUntilFound<AutomationElement>(
                () => NavigationPane.FindFirstChild(cf => cf.ByName("Open Navigation")),
                "Navigation should close after clicking close button");
        }
        // For other buttons, no specific waiting needed
    }

    // Improved method to navigate to a page with better waiting
    protected void NavigateToPage(string pageName)
    {
        // Ensure navigation is open first
        EnsureNavigationIsOpen();

        // Find the navigation item with waiting
        var navItem = WaitUntilFound<AutomationElement>(
            () => NavigationPane!.FindFirstDescendant(cf => cf.ByName(pageName)),
            $"Navigation item '{pageName}' should be found");

        // Find scrollable parent (if it exists)
        var scrollContainer = NavigationPane!.FindFirstDescendant(cf =>
            cf.ByAutomationId("MenuItemsHost"));

        if (scrollContainer != null)
        {
            // Try to scroll item into view if supported
            try
            {
                var navItemPattern = navItem.Patterns.ScrollItem.Pattern;
                navItemPattern?.ScrollIntoView();
            }
            catch (Exception ex)
            {
                // Log but continue - scrolling might not be needed
                System.Diagnostics.Debug.WriteLine($"Warning: Couldn't scroll to {pageName}: {ex.Message}");
            }
        }

        // Ensure item is on screen before clicking
        WaitUntilCondition(
            () => !navItem.Properties.IsOffscreen,
            $"Navigation item '{pageName}' should be visible on screen");

        // Focus and click the item
        navItem.Focus();
        navItem.Click();
    }


    // Helper for finding navigation items
    protected AutomationElement GetNavigationItem(string pageName)
    {
        var navItem = NavigationPane!.FindFirstDescendant(cf =>
            cf.ByName(pageName));

        if (navItem == null)
            throw new InvalidOperationException($"Navigation item '{pageName}' not found");

        return navItem;
    }

    private void WaitForElementOnScreen(AutomationElement element, int timeoutMs = 2000)
    {
        var stopwatch = Stopwatch.StartNew();
        while (element.Properties.IsOffscreen && stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            Thread.Sleep(100);
        }

        if (element.Properties.IsOffscreen)
        {
            throw new Exception($"Element {element.Name} remained offscreen after {timeoutMs}ms");
        }
    }

    // Helper to verify a page is loaded
    protected AutomationElement VerifyPageLoaded(string pageId)
    {
        var contentFrame = MainWindow!.FindFirstDescendant(cf =>
            cf.ByAutomationId("NavigationPane"));

        Assert.That(contentFrame, Is.Not.Null, "Navigation Pane should exist");

        var pageElement = contentFrame.FindFirstDescendant(cf =>
            cf.ByAutomationId(pageId));

        Assert.That(pageElement, Is.Not.Null, $"{pageId} page should be loaded");

        return pageElement;
    }

    protected T WaitUntilFound<T>(Func<T> findFunc, string errorMessage, TimeSpan? timeout = null) where T : class
    {
        // Start a stopwatch to measure how long we're searching
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Use the Retry.WhileNull method with the timeout
            var result = Retry.WhileNull(findFunc, timeout ?? DefaultTimeout, TimeSpan.FromMilliseconds(100), true);

            // If we get here but result is null, it means the Retry didn't throw but still failed
            if (result.Result == null)
            {
                Assert.Fail($"Timeout occurred: {errorMessage} (timeout: {timeout ?? DefaultTimeout})");
            }

            // Log success with timing information if in debug mode
            System.Diagnostics.Debug.WriteLine($"Found element in {stopwatch.ElapsedMilliseconds}ms: {errorMessage}");

            return result.Result;
        }
        catch (TimeoutException)
        {
            // Create a more descriptive timeout exception
            throw new TimeoutException($"Timeout occurred: {errorMessage} (timeout: {timeout ?? DefaultTimeout})");
        }
        catch (Exception ex)
        {
            // Enhance other exceptions with our context
            throw new Exception($"Error while finding element ({errorMessage}): {ex.Message}", ex);
        }
    }

    // Helper method to scroll an element into view if needed
    protected void ScrollElementIntoView(AutomationElement element)
    {
        try
        {
            // Check if element has scroll item pattern
            if (element.Patterns.ScrollItem.IsSupported)
            {
                element.Patterns.ScrollItem.Pattern.ScrollIntoView();
            }
        }
        catch (Exception ex)
        {
            // Log but continue - scrolling might not be needed or supported
            System.Diagnostics.Debug.WriteLine($"Warning: Couldn't scroll element into view: {ex.Message}");
        }
    }


    // Helper method for waiting until a condition is met
    protected void WaitUntilCondition(Func<bool> conditionFunc, string errorMessage, TimeSpan? timeout = null)
    {
        var result = Retry.WhileTrue(() => !conditionFunc(), timeout ?? DefaultTimeout, null, true);
        Assert.That(result.Result, Is.True, errorMessage);
    }
}