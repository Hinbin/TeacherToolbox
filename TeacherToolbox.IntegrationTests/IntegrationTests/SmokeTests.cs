using NUnit.Framework;

namespace TeacherToolbox.IntegrationTests.IntegrationTests
{
    [TestFixture]
    public class SmokeTests : TestBase
    {
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
    }
}
