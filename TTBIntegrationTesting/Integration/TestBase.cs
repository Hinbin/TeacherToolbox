using FlaUI.UIA3;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using System.Diagnostics;
using FlaUI.Core.Tools;
using Microsoft.UI.Xaml.Controls;

namespace TTBIntegrationTesting.Integration_Tests;

public class TestBase
{
    protected Application? App { get; private set; }
    protected UIA3Automation? Automation { get; private set; }
    protected Window? MainWindow { get; private set; }
    protected AutomationElement? NavigationView;

    // Array of files to delete during setup
    private readonly string[] filesToDelete = new[]
    {
        "centreNumber.json",
        "classes.json"
    };

    [SetUp]
    public void BaseSetUp()
    {
        // Delete specified files if they exist
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
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
                    Debug.WriteLine($"Warning: Failed to delete {filePath}: {ex.Message}");
                }
            }
        }

        var solutionDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\..\.."));
        var appPath = Path.Combine(
            solutionDir,
            @"TeacherToolbox\bin\x86\Debug\net6.0-windows10.0.19041.0\TeacherToolbox.exe"
        );

        if (!File.Exists(appPath))
        {
            throw new FileNotFoundException($"Application not found at {appPath}");
        }

        // Launch the application and wait for it to be ready
        App = Application.Launch(appPath);
        Automation = new UIA3Automation();

        // Wait for the main window with a timeout
        try
        {
            MainWindow = App.GetMainWindow(Automation, TimeSpan.FromSeconds(10));
        }
        catch (Exception ex)
        {
            // Clean up if we failed to get the main window
            Automation?.Dispose();
            App?.Dispose();
            throw new Exception("Failed to get main window. Make sure the application launches correctly.", ex);
        }

        NavigationView = MainWindow!.FindFirstDescendant(cf =>
            cf.ByAutomationId("NavigationPane"));

        Assert.That(MainWindow, Is.Not.Null, "Main window should be found");
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

    protected void NavigateToPage(string pageName)
    {
        EnsureNavigationIsOpen();
        var navItem = GetNavigationItem(pageName);

        // Find scrollable parent
        var scrollContainer = NavigationView!.FindFirstDescendant(cf =>
            cf.ByAutomationId("MenuItemsHost"));

        if (scrollContainer != null)
        {
            var navItemPattern = navItem.Patterns.ScrollItem.Pattern;
            navItemPattern.ScrollIntoView();                
        }

        navItem.Focus();
        WaitForElementOnScreen(navItem);

        navItem.Click();
        Thread.Sleep(500);
    }

    // Helper to ensure navigation is open
    protected void EnsureNavigationIsOpen()
    {
        var openButton = NavigationView!.FindFirstChild(cf =>
            cf.ByName("Open Navigation"));

        if (openButton != null) // If we find the open button, navigation is closed
        {
            ClickNavigationButton("Open Navigation");
        }
    }

    // Helper for finding navigation items
    protected AutomationElement GetNavigationItem(string pageName)
    {
        var navItem = NavigationView!.FindFirstDescendant(cf =>
            cf.ByName(pageName));

        if (navItem == null)
            throw new InvalidOperationException($"Navigation item '{pageName}' not found");

        return navItem;
    }

    // Helper method to find and click navigation buttons
    protected void ClickNavigationButton(string buttonName)
    {
        var button = NavigationView!.FindFirstChild(cf =>
            cf.ByName(buttonName));

        if (button == null)
            throw new InvalidOperationException($"Navigation button '{buttonName}' not found");

        button.Click();
        Thread.Sleep(500); // Keep existing wait logic

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
}