using System;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using TeacherToolbox.Model;
using TeacherToolbox.Services;
using TeacherToolbox.ViewModels;

namespace TeacherToolbox.UnitTests.ViewModels
{
    [TestFixture]
    public class RandomNameGeneratorViewModelTests
    {
        private Mock<ISettingsService> _mockSettingsService;
        private Mock<IFilePickerService> _mockFilePickerService;
        private RandomNameGeneratorViewModel _viewModel;

        [SetUp]
        public void Setup()
        {
            _mockSettingsService = new Mock<ISettingsService>();
            _mockFilePickerService = new Mock<IFilePickerService>();
            _mockFilePickerService.Setup(s => s.PickTextFileAsync(It.IsAny<IntPtr>())).ReturnsAsync((Windows.Storage.StorageFile)null);

            _viewModel = new RandomNameGeneratorViewModel(_mockSettingsService.Object, _mockFilePickerService.Object);
        }

        [Test]
        public void Constructor_WithNullDependencies_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new RandomNameGeneratorViewModel(null, _mockFilePickerService.Object));
            Assert.Throws<ArgumentNullException>(() => new RandomNameGeneratorViewModel(_mockSettingsService.Object, null));
        }

        [Test]
        public void Constructor_InitializesCollections()
        {
            Assert.That(_viewModel.StudentClasses, Is.Not.Null);
            Assert.That(_viewModel.VisibleClasses, Is.Not.Null);
            Assert.That(_viewModel.OverflowClasses, Is.Not.Null);
        }

        [Test]
        public void Constructor_InitializesCommands()
        {
            Assert.That(_viewModel.AddClassCommand, Is.Not.Null);
            Assert.That(_viewModel.RemoveClassCommand, Is.Not.Null);
            Assert.That(_viewModel.SelectClassCommand, Is.Not.Null);
            Assert.That(_viewModel.GenerateNameCommand, Is.Not.Null);
            Assert.That(_viewModel.ShowInstructionsCommand, Is.Not.Null);
        }

        [Test]
        public void Constructor_SetsInitialVisibilityState()
        {
            Assert.That(_viewModel.IsInstructionVisible, Is.True);
            Assert.That(_viewModel.IsTipVisible, Is.True);
            Assert.That(_viewModel.IsNameVisible, Is.False);
        }

        [Test]
        public void CurrentClass_WhenSet_SelectsOnlyMatchingClass()
        {
            var classA = CreateClass("A", "Alice");
            var classB = CreateClass("B", "Bob");
            _viewModel.StudentClasses.Add(classA);
            _viewModel.StudentClasses.Add(classB);

            _viewModel.CurrentClass = classB;

            Assert.That(classA.IsSelected, Is.False);
            Assert.That(classB.IsSelected, Is.True);
        }

        [Test]
        public void CurrentClass_WhenChanged_ClearsPreviousSelection()
        {
            var classA = CreateClass("A", "Alice");
            var classB = CreateClass("B", "Bob");
            _viewModel.StudentClasses.Add(classA);
            _viewModel.StudentClasses.Add(classB);

            _viewModel.CurrentClass = classA;
            _viewModel.CurrentClass = classB;

            Assert.That(classA.IsSelected, Is.False);
            Assert.That(classB.IsSelected, Is.True);
        }

        [Test]
        public void GenerateName_WithNoCurrentClass_ShowsTip()
        {
            _viewModel.GenerateName();

            Assert.That(_viewModel.IsNameVisible, Is.False);
            Assert.That(_viewModel.IsTipVisible, Is.True);
            Assert.That(_viewModel.TipText, Is.Not.Empty);
        }

        [Test]
        public void GenerateName_WithCurrentClass_ShowsStudentName()
        {
            _viewModel.CurrentClass = CreateClass("A", "Alice");

            _viewModel.GenerateName();

            Assert.That(_viewModel.IsNameVisible, Is.True);
            Assert.That(_viewModel.IsTipVisible, Is.False);
            Assert.That(_viewModel.NameDisplay, Is.EqualTo("Alice"));
        }

        [Test]
        public void GenerateNameCommand_WithCurrentClass_ShowsStudentName()
        {
            _viewModel.CurrentClass = CreateClass("A", "Alice");

            _viewModel.GenerateNameCommand.Execute(null);

            Assert.That(_viewModel.NameDisplay, Is.EqualTo("Alice"));
        }

        [Test]
        public void SelectClassCommand_WithClass_SetsCurrentClassAndGeneratesName()
        {
            var studentClass = CreateClass("A", "Alice");
            _viewModel.StudentClasses.Add(studentClass);

            _viewModel.SelectClassCommand.Execute(studentClass);

            Assert.That(_viewModel.CurrentClass, Is.EqualTo(studentClass));
            Assert.That(_viewModel.NameDisplay, Is.EqualTo("Alice"));
        }

        [Test]
        public void SelectClassCommand_WithNull_DoesNothing()
        {
            _viewModel.SelectClassCommand.Execute(null);

            Assert.That(_viewModel.CurrentClass, Is.Null);
            Assert.That(_viewModel.NameDisplay, Is.Empty);
        }

        [Test]
        public void SelectClassCommand_WithOverflowClass_MovesClassIntoVisibleClasses()
        {
            var first = CreateClass("A", "Alice");
            var overflow = CreateClass("F", "Frank");
            _viewModel.StudentClasses.Add(first);
            _viewModel.StudentClasses.Add(CreateClass("B", "Bob"));
            _viewModel.StudentClasses.Add(CreateClass("C", "Cara"));
            _viewModel.StudentClasses.Add(CreateClass("D", "Dan"));
            _viewModel.StudentClasses.Add(CreateClass("E", "Eve"));
            _viewModel.StudentClasses.Add(overflow);
            _viewModel.VisibleClasses.Add(first);

            _viewModel.SelectClassCommand.Execute(overflow);

            Assert.That(_viewModel.StudentClasses[0], Is.EqualTo(overflow));
            Assert.That(_viewModel.VisibleClasses, Does.Contain(overflow));
        }

        [Test]
        public void ShowInstructionsCommand_RaisesInstructionsRequested()
        {
            var requestedPage = "";
            _viewModel.InstructionsRequested += (_, page) => requestedPage = page;

            _viewModel.ShowInstructionsCommand.Execute(null);

            Assert.That(requestedPage, Is.EqualTo("RandomNameGeneratorPage"));
        }

        [Test]
        public async Task AddClassCommand_WhenPickerReturnsNull_DoesNotAddClass()
        {
            await _viewModel.AddClassCommand.ExecuteAsync(null);

            Assert.That(_viewModel.StudentClasses.Count, Is.EqualTo(0));
            _mockFilePickerService.Verify(s => s.PickTextFileAsync(_viewModel.WindowHandle), Times.Once);
        }

        [Test]
        public async Task InitializeAsync_WithSameDate_LoadsSameDayClasses()
        {
            var firstViewModel = CreateViewModelForDate(new DateTime(2026, 5, 4));
            var secondViewModel = CreateViewModelForDate(new DateTime(2026, 5, 4));

            await firstViewModel.InitializeAsync();
            await secondViewModel.InitializeAsync();

            Assert.That(firstViewModel.StudentClasses.Select(c => c.ClassName), Is.EqualTo(new[] { "Monday" }));
            Assert.That(secondViewModel.StudentClasses.Select(c => c.ClassName), Is.EqualTo(new[] { "Monday" }));
        }

        [Test]
        public void WindowHandle_WhenSet_StoresValue()
        {
            var handle = new IntPtr(1234);

            _viewModel.WindowHandle = handle;

            Assert.That(_viewModel.WindowHandle, Is.EqualTo(handle));
        }

        [Test]
        public void ButtonVisibilityProperties_ReflectStudentClassCount()
        {
            _viewModel.StudentClasses.Add(CreateClass("A", "Alice"));
            _viewModel.StudentClasses.Add(CreateClass("B", "Bob"));
            _viewModel.StudentClasses.Add(CreateClass("C", "Cara"));

            Assert.That(_viewModel.ShowAddRemoveButtonsDirectly, Is.True);
            Assert.That(_viewModel.ShowMoreButton, Is.False);

            _viewModel.StudentClasses.Add(CreateClass("D", "Dan"));

            Assert.That(_viewModel.ShowAddRemoveButtonsDirectly, Is.False);
            Assert.That(_viewModel.ShowMoreButton, Is.True);
        }

        private static StudentClass CreateClass(string className, params string[] studentNames)
        {
            var studentClass = new StudentClass(className, $"{className}.txt");
            foreach (var studentName in studentNames)
            {
                studentClass.AddStudent(new Student(studentName));
            }

            return studentClass;
        }

        private RandomNameGeneratorViewModel CreateViewModelForDate(DateTime currentDate)
        {
            return new RandomNameGeneratorViewModel(
                _mockSettingsService.Object,
                _mockFilePickerService.Object,
                () => currentDate,
                () => Task.FromResult(CreateClassSelector()));
        }

        private static StudentClassSelector CreateClassSelector()
        {
            var selector = new StudentClassSelector
            {
                studentClasses = new List<StudentClass>[7]
            };

            for (int i = 0; i < selector.studentClasses.Length; i++)
            {
                selector.studentClasses[i] = new List<StudentClass>();
            }

            selector.studentClasses[0].Add(CreateClass("Monday", "Alice"));
            selector.studentClasses[1].Add(CreateClass("Tuesday", "Bob"));
            selector.studentClasses[6].Add(CreateClass("Sunday", "Sam"));
            return selector;
        }
    }
}
