using FlaUI.UIA3;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using System.Diagnostics;
using FlaUI.Core.Tools;

namespace TTBIntegrationTesting.Integration_Tests;

public class TestBase
{
    protected Application? App { get; private set; }
    protected UIA3Automation? Automation { get; private set; }
    protected Window? MainWindow { get; private set; }

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
                    Console.WriteLine($"Warning: Failed to delete {filePath}: {ex.Message}");
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
}