using NUnit.Framework;

namespace TeacherToolbox.IntegrationTests.IntegrationTests
{
    [TestFixture]
    public class NavigationTests : TestBase
    {
        [Test]
        public void NavigateThroughAllPages_SuccessfulNavigation()
        {
            var navigationOrder = new[]
            {
                ("Timer", "TimerSelectionPage"),
                ("Screen Ruler", "ScreenRulerPage"),
                ("Exam Clock", "Clock"),
                ("Random Name Generator", "RandomNameGeneratorPage"),
                ("Settings", "SettingsPage")
            };

            foreach (var (pageName, pageId) in navigationOrder)
            {
                NavigateToPage(pageName);
                VerifyPageLoaded(pageId);
            }
        }
    }
}
