using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using OpenQA.Selenium.DevTools.V131.DOM;
using TeacherToolbox.Model;
using TeacherToolbox.Services;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace TeacherToolbox.ViewModels
{
    public class RandomNameGeneratorViewModel : ObservableObject
    {
        private readonly ISettingsService _settingsService;
        private readonly IFilePickerService _filePickerService;
        private readonly Random _random = new();

        private StudentClass _currentClass;
        private StudentClassSelector _studentClassSelector;
        private string _nameDisplay = "";
        private string _tipText = "";
        private bool _isNameVisible;
        private bool _isTipVisible = true;
        private bool _isInstructionVisible = true;
        private int _dayOfWeek;
        private List<string> _tips;

        #region Properties

        public StudentClass CurrentClass
        {
            get => _currentClass;
            set
            {
                if (SetProperty(ref _currentClass, value))
                {
                    // Update selection state for all classes
                    foreach (var studentClass in StudentClasses)
                    {
                        studentClass.IsSelected = studentClass == value;
                    }
                }
            }
        }

        public string NameDisplay
        {
            get => _nameDisplay;
            set => SetProperty(ref _nameDisplay, value);
        }

        public string TipText
        {
            get => _tipText;
            set => SetProperty(ref _tipText, value);
        }

        public bool IsNameVisible
        {
            get => _isNameVisible;
            set => SetProperty(ref _isNameVisible, value);
        }

        public bool IsTipVisible
        {
            get => _isTipVisible;
            set => SetProperty(ref _isTipVisible, value);
        }

        public bool IsInstructionVisible
        {
            get => _isInstructionVisible;
            set => SetProperty(ref _isInstructionVisible, value);
        }

        public ObservableCollection<StudentClass> StudentClasses { get; }

        public ObservableCollection<StudentClass> VisibleClasses { get; }

        public ObservableCollection<StudentClass> OverflowClasses { get; }

        public bool HasOverflowClasses => OverflowClasses.Count > 0;

        // New properties for button visibility logic
        public bool ShowAddRemoveButtonsDirectly => StudentClasses.Count <= 3;
        public bool ShowMoreButton => StudentClasses.Count > 3;

        #endregion

        #region Commands

        public IAsyncRelayCommand AddClassCommand { get; }
        public IRelayCommand RemoveClassCommand { get; }
        public IRelayCommand<StudentClass> SelectClassCommand { get; }
        public IRelayCommand GenerateNameCommand { get; }
        public IRelayCommand ShowInstructionsCommand { get; }

        #endregion

        #region Events

        public event EventHandler<string> InstructionsRequested;

        #endregion

        #region Constructor

        public RandomNameGeneratorViewModel(ISettingsService settingsService, IFilePickerService filePickerService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _filePickerService = filePickerService ?? throw new ArgumentNullException(nameof(filePickerService));

            StudentClasses = new ObservableCollection<StudentClass>();
            VisibleClasses = new ObservableCollection<StudentClass>();
            OverflowClasses = new ObservableCollection<StudentClass>();

            // Initialize commands
            AddClassCommand = new AsyncRelayCommand(AddClassAsync);
            RemoveClassCommand = new RelayCommand(RemoveClass);
            SelectClassCommand = new RelayCommand<StudentClass>(SelectClass);
            GenerateNameCommand = new RelayCommand(GenerateName);
            ShowInstructionsCommand = new RelayCommand(ShowInstructions);

            // Set day of week (Monday = 0, Tuesday = 1, etc.)
            _dayOfWeek = DateTime.Now.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)DateTime.Now.DayOfWeek - 1;

            // Initialize tips
            InitializeTips();
        }

        #endregion

        #region Initialization

        public async Task InitializeAsync()
        {
            _studentClassSelector = await StudentClassSelector.CreateAsync();

            LoadClassesForDay();
            DisplayRandomTip();
        }

        private void InitializeTips()
        {
            _tips = new List<string>
            {
                "Click on a student's name to select another student randomly.",
                "Add a new class by clicking the 'Add Class' button and selecting a text file with student names.",
                "Right-click on a class button to quickly remove that class.",
                "Classes are organized by day of the week. Today's classes are shown automatically.",
                "Use the Random Name Generator to ensure fair participation from all students.",
                "You can tap anywhere in the name display area to generate a new random name.",
                "Click the 'Get Started' button to learn more about using the Random Name Generator.",
                "Try using the exam clock tool to help students understand time management with color-coded segments.",
                "The colour coded segments in the exam clock are colour blind friendly",
                "In the exam clock left click and drag to create a segment. Right clicking deletes a segment.",
                "The interval timer feature is great for managing timed classroom activities and rotations.",
                "Timer sounds can be customized in the settings to alert students when time is up."
            };
        }

        #endregion

        #region Class Management

        private void LoadClassesForDay()
        {
            StudentClasses.Clear();
            VisibleClasses.Clear();
            OverflowClasses.Clear();

            if (_studentClassSelector?.studentClasses == null) return;

            var todaysClasses = _studentClassSelector.studentClasses[_dayOfWeek];

            foreach (var studentClass in todaysClasses)
            {
                StudentClasses.Add(studentClass);
            }

            UpdateVisibleClasses();
        }

        private void UpdateVisibleClasses()
        {
            VisibleClasses.Clear();
            OverflowClasses.Clear();

            // Add up to 5 visible classes
            for (int i = 0; i < Math.Min(StudentClasses.Count, 5); i++)
            {
                VisibleClasses.Add(StudentClasses[i]);
            }

            // Add remaining to overflow
            for (int i = 5; i < StudentClasses.Count; i++)
            {
                OverflowClasses.Add(StudentClasses[i]);
            }

            // If there is at least one class, hide the instructions
            if (StudentClasses.Count > 0)
            {
                IsInstructionVisible = false;
            }
            else
            {
                IsInstructionVisible = true;
            }

            // Notify UI of property changes
            OnPropertyChanged(nameof(HasOverflowClasses));
            OnPropertyChanged(nameof(ShowAddRemoveButtonsDirectly));
            OnPropertyChanged(nameof(ShowMoreButton));
        }

        private async Task AddClassAsync()
        {
            // The view must provide the window handle through a property or method
            var file = await _filePickerService.PickTextFileAsync(WindowHandle);

            if (file != null)
            {
                var studentClass = await StudentClass.CreateAsync(file.DisplayName, file.Path);
                _studentClassSelector.AddClass(studentClass, _dayOfWeek);

                LoadClassesForDay();
                SelectClass(studentClass);
                GenerateName();
            }
        }

        // Property to be set by the view
        public IntPtr WindowHandle { get; set; }

        private void RemoveClass()
        {
            if (CurrentClass != null)
            {
                _studentClassSelector.RemoveClass(CurrentClass, _dayOfWeek);
                CurrentClass = null;
                LoadClassesForDay();
                DisplayRandomTip();
            }
        }

        public void RemoveSpecificClass(StudentClass studentClass)
        {
            if (studentClass != null)
            {
                _studentClassSelector.RemoveClass(studentClass, _dayOfWeek);

                if (CurrentClass == studentClass)
                {
                    CurrentClass = null;
                    DisplayRandomTip();
                }

                LoadClassesForDay();
            }
        }

        private void SelectClass(StudentClass studentClass)
        {
            if (studentClass == null) return;

            CurrentClass = studentClass;

            // If the class is not in the visible classes, move it to the front
            if (!VisibleClasses.Contains(studentClass))
            {
                var classIndex = StudentClasses.IndexOf(studentClass);
                if (classIndex >= 0)
                {
                    StudentClasses.Move(classIndex, 0);
                    UpdateVisibleClasses();
                }
            }

            GenerateName();
        }

        #endregion

        #region Name Generation

        public void GenerateName()
        {
            if (CurrentClass == null)
            {
                DisplayRandomTip();
                return;
            }

            IsNameVisible = true;
            IsTipVisible = false;

            var randomStudent = CurrentClass.GetRandomStudent();
            if (randomStudent != null)
            {
                NameDisplay = randomStudent.Name;
            }
        }

        private void DisplayRandomTip()
        {
            IsNameVisible = false;
            IsTipVisible = true;

            int index = _random.Next(_tips.Count);
            TipText = _tips[index];
        }

        #endregion

        #region Instructions

        private void ShowInstructions()
        {
            InstructionsRequested?.Invoke(this, "RandomNameGeneratorPage");
        }

        #endregion
    }
}