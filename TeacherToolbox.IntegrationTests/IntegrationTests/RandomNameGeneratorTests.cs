using FlaUI.Core.AutomationElements;
using NUnit.Framework;

namespace TeacherToolbox.IntegrationTests.IntegrationTests
{
    [TestFixture]
    public class RandomNameGeneratorTests : TestBase
    {
        [Test]
        public void RandomNameGenerator_CanOpenClassFileAndGenerateName()
        {
            NavigateToPage("Random Name Generator");
            var rngPage = VerifyPageLoaded("RandomNameGeneratorPage");

            OpenClassFile(rngPage, "8xCs2.txt");

            var classButton = WaitUntilFound(
                () => rngPage.FindFirstDescendant(cf => cf.ByName("8xCs2")),
                "Class button should appear after opening a class file");
            SafeClick(classButton);

            var nameDisplay = WaitUntilFound(
                () => rngPage.FindFirstDescendant(cf => cf.ByAutomationId("NameDisplay")),
                "Name display should exist");
            var textPattern = nameDisplay.Patterns.Text.Pattern;

            SafeClick(nameDisplay);

            WaitUntilCondition(
                () => !string.IsNullOrWhiteSpace(textPattern.DocumentRange.GetText(-1)),
                "Name display should show a generated name");
        }
    }
}
