using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Threading.Tasks;
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
        // Choose a random name from the list, giving more weight to students who haven't been picked
        StudentClass currentClass;
        StudentClassSelector studentClassSelector;

        public RandomNameGenerator()
        {
            this.InitializeComponent();
            this.Loaded += RandomNameGenerator_Loaded;            
        }

        public async void RandomNameGenerator_Loaded(object sender, RoutedEventArgs e)
        {         
             
            studentClassSelector = await StudentClassSelector.CreateAsync();
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
            openPicker.FileTypeFilter.Add(".txt");

            // See the sample code below for how to make the window accessible from the App class.
            var window = App.MainWindow;

            // Retrieve the window handle (HWND) of the current WinUI 3 window.
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);

            // Initialize the file picker with the window handle (HWND).
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hWnd);

            var file = await openPicker.PickSingleFileAsync();

            if (file != null)
            {
                // Create a new student class object
                StudentClass studentClass = await StudentClass.CreateAsync(file.DisplayName, file.Path);

                SelectNewClass(studentClass);
                // Add the class to the list of classes
                studentClassSelector.AddClass(studentClass);
            }
            UpdateButtons();
            GenerateName();

        }

        private void UpdateButtons()
        {
            // Clear the classlist stackpanel
            ClassList.Children.Clear();

            // Add up to 5 student class buttons
            for (int i = 0; i < Math.Min(studentClassSelector.studentClasses.Count, 5); i++)
            {
                Button classButton = new()
                {
                    Content = studentClassSelector.studentClasses[i].ClassName
                };
                classButton.Click += Class_Clicked;
                // If it is the current class - add an accent style to the button
                if (studentClassSelector.studentClasses[i] == currentClass)
                {
                    classButton.Style = (Style)Application.Current.Resources["AccentButtonStyle"];                    
                }
                ClassList.Children.Add(classButton);
            }

            // Add "More" dropdown button to display the rest of the classes not shown in buttons
            if (studentClassSelector.studentClasses.Count > 3)
            {
                DropDownButton moreButton = new()
                {
                    Content = "More"
                };
                MenuFlyout menuFlyOut = new ();
                moreButton.Flyout = menuFlyOut;
                {
                    if (studentClassSelector.studentClasses.Count > 5) { 

                    for (int i = 5; i < studentClassSelector.studentClasses.Count; i++)
                    {
                            MenuFlyoutItem classItem = new()
                            {
                                Text = studentClassSelector.studentClasses[i].ClassName
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

 
                }
                ClassList.Children.Add(moreButton);

            }

            // If there are 3 or less classes, add an "Add Class" and "Remove Class button
            if (studentClassSelector.studentClasses.Count <= 3)
            {
                Button addClassButton = new()
                {
                    Content = "Add Class"
                };
                addClassButton.Click += Add_Class_Clicked;
                ClassList.Children.Add(addClassButton);

                if (studentClassSelector.studentClasses.Count > 0)
                {
                    Button removeClassButton = new()
                    {
                        Content = "Remove Class"
                    };
                    removeClassButton.Click += Remove_Class_Clicked;
                    ClassList.Children.Add(removeClassButton);
                }
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
            GenerateName();
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
                studentClassSelector.RemoveClass(currentClass);
                currentClass = null;
                UpdateButtons();
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
            } else if (sender.GetType() == typeof(MenuFlyoutItem))
            {
                // cast the sender object into a MenuFlyoutItem
                MenuFlyoutItem button = (MenuFlyoutItem)sender;
                className = button.Text;
            }else
            {
                return;
            }           

            if (className == null) return;

            // Check to see if we just need a new name from the current class
            if (currentClass != null && currentClass.ClassName == className) {
                GenerateName();
                return;
            }

            // Search through the classes in selectedclasses, looking for a match for the button name
            foreach (StudentClass studentClass in studentClassSelector.studentClasses)
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
        private void SelectNewClass(StudentClass studentClass)
        {
            currentClass = studentClass;
            // If the class button is not in the first 5 buttons, move it to the first position
            if (!studentClassSelector.studentClasses.GetRange(0, Math.Min(5, studentClassSelector.studentClasses.Count)).Contains(studentClass))
            {
                studentClassSelector.studentClasses.Remove(studentClass);
                studentClassSelector.studentClasses.Insert(0, studentClass);
            }
            UpdateButtons();
        }

        private void Page_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            // If F9 is pressed, generate a new name
            if (e.Key == Windows.System.VirtualKey.F9)
            {
                GenerateName();
            }
        }
    }

}
