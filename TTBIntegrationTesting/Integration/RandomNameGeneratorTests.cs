
using FlaUI.UIA3;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using TTBIntegrationTesting.Integration_Tests;
using FlaUI.Core.WindowsAPI;
using System.Diagnostics;
using FlaUI.Core.Conditions;
using FlaUI.Core.Tools;
using FlaUI.Core.Exceptions;

namespace TTBIntegrationTesting
{
    [TestFixture]
    public class RandomNameGeneratorTests : TestBase
    {
        private AutomationElement? _rngPage;

        [SetUp]
        public void RNGSetUp()
        {
            // Navigate to RNG using NavigationView
            var navigationView = MainWindow!.FindFirstDescendant(cf =>
                cf.ByAutomationId("NavView"));

            navigationView?.FindFirstChild(cf =>
                cf.ByName("Open Navigation")).Click();

            var rngNavItem = navigationView?.FindFirstDescendant(cf =>
                cf.ByName("Random Name Generator"));

            // Wait a moment for navigation
            Thread.Sleep(500);

            rngNavItem?.Click();

            // Wait a moment for navigation
            Thread.Sleep(500);

            // Check if RNG page is loaded by looking for its unique elements
            _rngPage = MainWindow!.FindFirstDescendant(cf =>
                cf.ByAutomationId("NameDisplay"))?.Parent;


            Assert.That(_rngPage, Is.Not.Null, "RNG page should be loaded");
        }

        [Test]
        public void AddClass_ButtonExists()
        {
            var addClassButton = _rngPage!.FindFirstDescendant(cf =>
                cf.ByName("Add Class"));

            Assert.That(addClassButton, Is.Not.Null, "Add Class button should exist");
            Assert.That(addClassButton?.Name, Is.EqualTo("Add Class"),
                "Button should be labeled 'Add Class'");
        }

        [Test]
        public void AddClass_MoreClassesAfterFive()
        {

        }

        [Test]
        public void AddClass_AddNewClass()
        {
            // Count initial number of classes
            var initialClassCount = CountClassLists();

            OpenClassFile_NavigatesToFileAndOpens("8xCs2.txt");

            var classCountResult = Retry.WhileTrue(() =>
            {
                var currentCount = CountClassLists();
                // Return true while the condition we don't want is true (count hasn't increased)
                return currentCount != initialClassCount + 2;
            }, TimeSpan.FromSeconds(2), null, true);

            Assert.That(classCountResult.Result, Is.True, "A new class should be added (+2 for remove class)");
        }

        [Test]
        public void NameDisplay_ShowsNameOnClick()
        {
            var nameDisplay = _rngPage!.FindFirstDescendant(cf =>
                cf.ByAutomationId("NameDisplay"));

            // Get the text pattern
            var textPattern = nameDisplay.Patterns.Text.Pattern;

            // Initially should be empty
            Assert.That(textPattern.DocumentRange.GetText(-1), Is.Empty.Or.Null,
                "Name display should start empty");

            OpenClassFile_NavigatesToFileAndOpens("8xCs2.txt");

            // Click to generate name
            nameDisplay?.Click();

            // After click, should not be empty
            Assert.That(textPattern.DocumentRange.GetText(-1), Is.Not.Empty,
                "Name display should show a name after clicking");
        }

        [Test]
        public void NameDisplay_ShowsNameOnLoad()
        {
            var nameDisplay = _rngPage!.FindFirstDescendant(cf =>
                cf.ByAutomationId("NameDisplay"));

            // Get the text pattern
            var textPattern = nameDisplay.Patterns.Text.Pattern;

            // Initially should be empty
            Assert.That(textPattern.DocumentRange.GetText(-1), Is.Empty.Or.Null,
                "Name display should start empty");

            OpenClassFile_NavigatesToFileAndOpens("8xCs2.txt");

            // After loading, should not be empty
            Assert.That(textPattern.DocumentRange.GetText(-1), Is.Not.Empty,
                "Name display should show a name after clicking");
        }

        [Test]
        public void MultipleClasses_SwitchBetweenClasses()
        {
            OpenClassFile_NavigatesToFileAndOpens("8xCs2.txt");
            OpenClassFile_NavigatesToFileAndOpens("7xCs3.txt");

            // Get class list elements
            var classList = GetClassList().ToList(); // Materialize the list

            Assert.That(classList.Count, Is.GreaterThanOrEqualTo(2),
                "Should have at least 2 class buttons");

            // Click each class and verify it becomes active
            foreach (var button in classList)
            {
                try
                {
                    SafeClick(button);
                    Thread.Sleep(1000); // Wait for UI to update
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Failed to click button '{button.Name}': {ex.Message}");
                }
            }
        }

        private IEnumerable<AutomationElement> GetClassList()
        {
            if (_rngPage == null)
                return Enumerable.Empty<AutomationElement>();

            var classList = _rngPage.FindFirstDescendant(cf =>
                cf.ByAutomationId("ClassesViewer"));

            if (classList == null)
                return Enumerable.Empty<AutomationElement>();

            var children = classList.FindAllChildren();
            if (children == null)
                return Enumerable.Empty<AutomationElement>();

            return children.Where(child =>
            {
                try
                {
                    // First check if it's a button
                    if (child.ControlType == ControlType.Button)
                    {
                        return child.Name != "Add Class";
                    }
                    return false; // If not a button, exclude it
                }
                catch (PropertyNotSupportedException)
                {
                    return false; // Skip elements that don't support ControlType
                }
            });
        }

        private int CountClassLists()
        {
            return GetClassList().Count();
        }

        public void OpenClassFile_NavigatesToFileAndOpens(string fileName)
        {
            var addClassButton = _rngPage!.FindFirstDescendant(cf =>
                cf.ByName("Add Class"));

            // Click the Add Class button
            addClassButton?.Click();


            // Create a new automation scope specifically for finding the dialog
            using (var dialogAutomation = new UIA3Automation())
            {
                var fileDialogResult = Retry.WhileNull<AutomationElement>(() =>
                {
                    var desktop = dialogAutomation.GetDesktop();

                    // Find all windows and debug their names
                    var allWindows = desktop.FindAll(TreeScope.Children,
                        new PropertyCondition(Automation.PropertyLibrary.Element.ControlType, ControlType.Window));

                    foreach (var window in allWindows)
                    {
                        System.Diagnostics.Debug.WriteLine($"Found window: {window.Name}");
                    }

                    // Find and return the file dialog
                    return desktop.FindFirst(TreeScope.Descendants,
                        new AndCondition(
                            new PropertyCondition(Automation.PropertyLibrary.Element.ControlType, ControlType.Window),
                            new PropertyCondition(Automation.PropertyLibrary.Element.ClassName, "#32770")
                        ));

                }, TimeSpan.FromSeconds(10), null, true);

                var fileDialog = fileDialogResult.Result;

                Assert.That(fileDialog, Is.Not.Null, "File dialog should appear");

                // Find the filename input field - look specifically for the "File name:" edit box
                var filenameInput = fileDialog.FindFirst(TreeScope.Descendants,
                    new AndCondition(
                        new PropertyCondition(Automation.PropertyLibrary.Element.ControlType, ControlType.Edit),
                        new PropertyCondition(Automation.PropertyLibrary.Element.Name, "File name:")
                    ));

                // If that doesn't work, try finding by automation ID which is typically "1148"
                if (filenameInput == null)
                {
                    filenameInput = fileDialog.FindFirst(TreeScope.Descendants,
                        new AndCondition(
                            new PropertyCondition(Automation.PropertyLibrary.Element.ControlType, ControlType.Edit),
                            new PropertyCondition(Automation.PropertyLibrary.Element.AutomationId, "1148")
                        ));
                }

                Assert.That(filenameInput, Is.Not.Null, "Filename input field should exist");


                // Get the path relative to the solution directory
                var solutionDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\..\.."));
                var path = Path.Combine(solutionDir, "TTBIntegrationTesting", "Integration", "Files", fileName);

                filenameInput.Focus();
                Keyboard.Type(path);

                // Press Enter to confirm
                Thread.Sleep(200); // Small delay to ensure filename is entered
                Keyboard.Press(VirtualKeyShort.RETURN);

                var dialogClosedResult = Retry.WhileTrue(() =>
                {
                    var desktop = dialogAutomation.GetDesktop();

                    var dialogWindow = desktop.FindFirst(TreeScope.Descendants,
                        new AndCondition(
                            new PropertyCondition(Automation.PropertyLibrary.Element.ControlType, ControlType.Window),
                            new PropertyCondition(Automation.PropertyLibrary.Element.ClassName, "#32770")
                        ));

                    return dialogWindow != null;  // Keep retrying while dialog exists
                }, TimeSpan.FromSeconds(10), null, true);

                Assert.That(dialogClosedResult.Result, Is.True, "File dialog should be closed");
            }
        }
    }
}