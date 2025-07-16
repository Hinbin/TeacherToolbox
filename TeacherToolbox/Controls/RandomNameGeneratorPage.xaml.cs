using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using TeacherToolbox.Model;
using TeacherToolbox.ViewModels;

namespace TeacherToolbox.Controls
{
    public sealed partial class RandomNameGeneratorPage
    {
        public RandomNameGeneratorViewModel ViewModel { get; }

        public RandomNameGeneratorPage()
        {
            this.InitializeComponent();

            // Get the ViewModel from dependency injection
            ViewModel = App.Current.Services.GetRequiredService<RandomNameGeneratorViewModel>();

            // Set the window handle for file picker
            var window = App.MainWindow;
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            ViewModel.WindowHandle = hWnd;

            // Set DataContext for data binding
            this.Loaded += RandomNameGenerator_Loaded;
        }

        private async void RandomNameGenerator_Loaded(object sender, RoutedEventArgs e)
        {
            // Initialize the ViewModel
            await ViewModel.InitializeAsync();
            this.DataContext = ViewModel;

            // Subscribe to events
            ViewModel.InstructionsRequested += OnInstructionsRequested;

            // Subscribe to property changes to update the More button
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Update the More button when relevant properties change
            if (e.PropertyName == nameof(ViewModel.ShowMoreButton) ||
                e.PropertyName == nameof(ViewModel.HasOverflowClasses) ||
                e.PropertyName == nameof(ViewModel.OverflowClasses))
            {
                UpdateMoreButtonFlyout();
            }
        }

        private void OnInstructionsRequested(object sender, string pageName)
        {
            // Handle instructions request - navigate to help or show dialog
            // This is UI-specific logic that stays in the code-behind
        }

        private void SelectClass_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is StudentClass studentClass)
            {
                ViewModel.SelectClassCommand.Execute(studentClass);
            }
        }

        private void Page_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Walk up the visual tree to see if the tap occurred within a Button
            DependencyObject element = e.OriginalSource as DependencyObject;
            while (element != null)
            {
                if (element is Button)
                {
                    // The tap was on a button or its content - let the button handle it
                    return;
                }
                element = VisualTreeHelper.GetParent(element);
            }

            // Also check for other interactive elements that shouldn't trigger name generation
            if (e.OriginalSource is DropDownButton ||
                e.OriginalSource is MenuFlyoutItem)
            {
                return;
            }

            // Generate a new name only if we didn't tap on any button or interactive element
            ViewModel.GenerateNameCommand.Execute(null);
            e.Handled = true; // Prevent event from bubbling up to NavView
        }

        private void Button_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Stop the event from bubbling up to Page_Tapped
            e.Handled = true;
            // The Command binding will still execute, but we prevent the page tap handler from running
        }

        private void Class_Right_Clicked(object sender, RightTappedRoutedEventArgs e)
        {
            // Get the StudentClass from the button's DataContext
            if (sender is Button button && button.DataContext is StudentClass studentClass)
            {
                ViewModel.RemoveSpecificClass(studentClass);
            }
        }

        private void MoreButton_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateMoreButtonFlyout();
        }

        private void UpdateMoreButtonFlyout()
        {
            if (MoreMenuFlyout == null || ViewModel == null) return;

            // Clear existing items
            MoreMenuFlyout.Items.Clear();

            // Add overflow classes
            foreach (var studentClass in ViewModel.OverflowClasses)
            {
                var menuItem = new MenuFlyoutItem
                {
                    Text = studentClass.ClassName,
                    DataContext = studentClass
                };
                menuItem.Click += OverflowClass_Click;
                MoreMenuFlyout.Items.Add(menuItem);
            }

            // Add separator if we have overflow classes
            if (ViewModel.OverflowClasses.Count > 0)
            {
                MoreMenuFlyout.Items.Add(new MenuFlyoutSeparator());
            }

            // Add "Remove Current Class" item
            var removeClassItem = new MenuFlyoutItem
            {
                Text = "Remove Current Class"
            };
            removeClassItem.Click += RemoveCurrentClass_Click;
            MoreMenuFlyout.Items.Add(removeClassItem);

            // Add "Add Class" item
            var addClassItem = new MenuFlyoutItem
            {
                Text = "Add Class"
            };
            addClassItem.Click += AddClass_Click;
            MoreMenuFlyout.Items.Add(addClassItem);

            // Add "Help" item
            var helpItem = new MenuFlyoutItem
            {
                Text = "Help"
            };
            helpItem.Click += Help_Click;
            MoreMenuFlyout.Items.Add(helpItem);
        }

        private void OverflowClass_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is StudentClass studentClass)
            {
                ViewModel.SelectClassCommand.Execute(studentClass);
            }
        }

        private void RemoveCurrentClass_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.RemoveClassCommand.Execute(null);
        }

        private void AddClass_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.AddClassCommand.Execute(null);
        }

        private void Help_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ShowInstructionsCommand.Execute(null);
        }

        // Handle the main area tap to generate names
        public void GenerateName()
        {
            ViewModel.GenerateNameCommand.Execute(null);
        }
    }
}