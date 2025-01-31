
using FlaUI.Core.AutomationElements;

namespace TTBIntegrationTesting.Integration_Tests;

[TestFixture]
public class TimerTests : TestBase
{
    private Window? _timerWindow;

    [SetUp]
    public void TimerSetUp()
    {
        // Find and open timer window
        var timerButton = MainWindow!.FindFirstDescendant("TimerButton")?.AsButton();
        timerButton?.Click();
        _timerWindow = App!.GetAllTopLevelWindows(Automation).FirstOrDefault(w => w.Name == "Timer");
    }

    [TearDown]
    public void TimerTearDown()
    {
        // Close timer window if open
        _timerWindow?.Close();
    }

    [Test]
    public void Timer_StartsAndStops()
    {
        // Timer test implementation
    }

    [Test]
    public void Timer_SetsDuration()
    {
        // Duration test implementation
    }

    [Test]
    public void Timer_WindowPositionPersists()
    {
        //  Move window and check that timer has same position as old window
    }
}