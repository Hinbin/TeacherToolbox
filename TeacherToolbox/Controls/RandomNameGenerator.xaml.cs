using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Windows.Storage.Pickers;
using Microsoft.UI.Xaml.Media;
using TeacherToolbox.Model;
using Windows.UI.Core;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace TeacherToolbox.Controls
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class RandomNameGenerator : Page
    {
        StudentClass currentClass;
        StudentClassSelector studentClassSelector;
        List<StudentClass> studentClasses;
        int dayOfWeek;
        Button classRightClicked;
        IntPtr hWnd;

        public RandomNameGenerator()
        {
            this.InitializeComponent();
            this.Loaded += RandomNameGenerator_Loaded;
            // Set dayOfWeek as a number, Monday = 0, Tuesday = 1, etc.
            dayOfWeek = (int)DateTime.Now.DayOfWeek;

            // See the sample code below for how to make the window accessible from the App class.
            var window = App.MainWindow;

            // Retrieve the window handle (HWND) of the current WinUI 3 window.
            hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);

        }

        public async void RandomNameGenerator_Loaded(object sender, RoutedEventArgs e)
        {

            studentClassSelector = await StudentClassSelector.CreateAsync();
            studentClasses = studentClassSelector.studentClasses[dayOfWeek];
            UpdateButtons();
        }

        public async Task AddClass()
        {
            // Show the file explorer to select a file
            FileOpenPicker openPicker = new()
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };

            // Initialize the file picker with the window handle (HWND).
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hWnd);
            openPicker.FileTypeFilter.Add(".txt");

            var file = await openPicker.PickSingleFileAsync();

            if (file != null)
            {
                // Create a new student class object
                StudentClass studentClass = await StudentClass.CreateAsync(file.DisplayName, file.Path);
                // Add the class to the list of classes
                studentClassSelector.AddClass(studentClass, dayOfWeek);

                // Wait for the class to be created, then run SelectNewClass
                SelectNewClass(studentClass);
            }

            UpdateButtons();
            GenerateName();

        }

        private void UpdateButtons()
        {
            // Clear the classlist stackpanel
            ClassList.Children.Clear();
            // Get the list of student classes for the current day

            // Add up to 5 student class buttons
            for (int i = 0; i < Math.Min(studentClasses.Count, 5); i++)
            {
                Button classButton = new()
                {
                    Content = studentClasses[i].ClassName
                };
                classButton.Click += Class_Clicked;
                classButton.RightTapped += Class_Right_Clicked;
                // If it is the current class - add an accent style to the button
                if (studentClasses[i] == currentClass)
                {
                    classButton.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
                }
                ClassList.Children.Add(classButton);
            }

            // Add "More" dropdown button to display the rest of the classes not shown in buttons
            if (studentClasses.Count > 3)
            {
                DropDownButton moreButton = new()
                {
                    Content = "More"
                };
                MenuFlyout menuFlyOut = new();
                moreButton.Flyout = menuFlyOut;
                {
                    if (studentClasses.Count > 5)
                    {

                        for (int i = 5; i < studentClasses.Count; i++)
                        {
                            MenuFlyoutItem classItem = new()
                            {
                                Text = studentClasses[i].ClassName
                            };
                            classItem.Click += Class_Clicked;
                            menuFlyOut.Items.Add(classItem);
                        }
                    }
                    // Add a remove class and add class button to the menu

                    MenuFlyoutItem removeClassItem = new()
                    {
                        Text = "Remove Current Class"
                    };
                    removeClassItem.Click += Remove_Class_Clicked;
                    menuFlyOut.Items.Add(removeClassItem);

                    MenuFlyoutItem addClassItem = new()
                    {
                        Text = "Add Class"
                    };
                    addClassItem.Click += Add_Class_Clicked;
                    menuFlyOut.Items.Add(addClassItem);

                    MenuFlyoutItem howToUseItem = new()
                    {
                        Text = "Help"
                    };
                    howToUseItem.Click += How_To_Use_Clicked;
                    menuFlyOut.Items.Add(howToUseItem);

                }
                ClassList.Children.Add(moreButton);

            }

            // If there are 3 or less classes, add an "Add Class" and "Remove Class button
            if (studentClasses.Count <= 3)
            {
                Button addClassButton = new()
                {
                    Content = "Add Class"
                };
                addClassButton.Click += Add_Class_Clicked;
                ClassList.Children.Add(addClassButton);

                if (studentClasses.Count > 0)
                {
                    Button removeClassButton = new()
                    {
                        Content = "Remove Class"
                    };
                    removeClassButton.Click += Remove_Class_Clicked;
                    ClassList.Children.Add(removeClassButton);
                }

                Button howToUseButton = new()
                {
                    Content = "Get Started"
                };
                howToUseButton.Click += How_To_Use_Clicked;
                ClassList.Children.Add(howToUseButton);
            }
        }

        public void GenerateName()
        {
            if (currentClass == null) return;

            Student randomStudent = currentClass.GetRandomStudent();
            if (randomStudent == null) return;

            NameDisplay.Text = randomStudent.Name;
        }

        private void NameDisplay_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Check to see if the Add Class or Remove Class button have been clicked, if so return
            if (e.OriginalSource is Button)
            {
                return;
            }

            GenerateName();
            e.Handled = true;
        }

        private async void Add_Class_Clicked(object sender, RoutedEventArgs e)
        {
            await AddClass();
            GenerateName();
        }

        private void Remove_Class_Clicked(object sender, RoutedEventArgs e)
        {
            if (currentClass != null)
            {
                studentClassSelector.RemoveClass(currentClass, dayOfWeek);
                currentClass = null;
                UpdateButtons();
            }
            else
            {
                if (classRightClicked != null)
                {
                    // Get the text in the classRightClickedButton
                    string className = classRightClicked.Content.ToString();

                    // Find the class with the same name as the button
                    foreach (StudentClass studentClass in studentClasses)
                    {
                        if (studentClass.ClassName == className)
                        {
                            studentClassSelector.RemoveClass(studentClass, dayOfWeek);
                            UpdateButtons();
                            break;
                        }
                    }
                }
            }
        }

        private void Class_Clicked(object sender, RoutedEventArgs e)
        {

            string className;

            // Check we have a button object as a sender - if not return
            if (sender.GetType() == typeof(Button))
            {
                // cast the sender object into a button
                Button button = (Button)sender;
                className = button.Content.ToString();
            }
            else if (sender.GetType() == typeof(MenuFlyoutItem))
            {
                // cast the sender object into a MenuFlyoutItem
                MenuFlyoutItem button = (MenuFlyoutItem)sender;
                className = button.Text;
            }
            else
            {
                return;
            }

            if (className == null) return;

            // Check to see if we just need a new name from the current class
            if (currentClass != null && currentClass.ClassName == className)
            {
                // The name will be generated from the tapped method
                return;
            }

            // Search through the classes in selectedclasses, looking for a match for the button name
            foreach (StudentClass studentClass in studentClasses)
            {
                // Check to see if studentClass has same name as the button content
                if (studentClass.ClassName == className)
                {
                    SelectNewClass(studentClass);
                    break;
                }
            }

            GenerateName();
            UpdateButtons();

        }

        private void Class_Right_Clicked(object sender, RoutedEventArgs e)
        {
            classRightClicked = (Button)sender;

            // Create the flyout menu
            MenuFlyout menuFlyout = new MenuFlyout();

            // Create the "Remove Class" menu item
            MenuFlyoutItem removeClassItem = new MenuFlyoutItem()
            {
                Text = "Remove Class"
            };
            removeClassItem.Click += Remove_Class_Clicked;

            // Add the "Remove Class" menu item to the flyout menu
            menuFlyout.Items.Add(removeClassItem);

            // Set the flyout menu as the flyout for the button or control
            Button button = (Button)sender;
            button.Flyout = menuFlyout;
            button.Flyout.ShowAt(button);

        }

        private void How_To_Use_Clicked(object sender, RoutedEventArgs e)
        {
            RNGHowToUseWindow howToUseWindow= new();
            howToUseWindow.Activate();
        }

        private void SelectNewClass(StudentClass studentClass)
        {
            currentClass = studentClass;

            var classes = studentClasses.GetRange(0, Math.Min(5, studentClasses.Count));

            // If the class button is not in the first 5 buttons, move it to the first position
            if (!studentClasses.GetRange(0, Math.Min(5, studentClasses.Count)).Contains(studentClass))
            {
                studentClasses.Remove(studentClass);
                studentClasses.Insert(0, studentClass);
                UpdateButtons();
            }

        }
    }

}
