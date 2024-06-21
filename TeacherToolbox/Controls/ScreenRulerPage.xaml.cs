using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TeacherToolbox.Helpers;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

//TODO: Togggle close/open button based on state
//TODO: Add colour picker

namespace TeacherToolbox.Controls
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ScreenRulerPage : Page
    {

        ScreenRulerWindow screenRulerWindow;
        DisplayManager displayManager;
        private int currentDisplayIndex = 0; // Variable to store the current display

        public ScreenRulerPage()
        {
            this.InitializeComponent();

            // Create a new instance of the ruler

            screenRulerWindow = new ScreenRulerWindow(this);
            screenRulerWindow.Closed += ScreenRulerWindow_Closed;   
            
            displayManager = new DisplayManager();
            if (displayManager.DisplayAreas.Count > 1)
            {
                ChangeDisplayButton.Visibility = Visibility.Visible;
            }
        }

        public void Close_Ruler_Window_Click(object sender, RoutedEventArgs e)
        {
            screenRulerWindow.Close();
            OpenRulerWindowButton.Visibility = Visibility.Visible;
            CloseRulerWindowButton.Visibility = Visibility.Collapsed;
        }

        public void Open_Ruler_Window_Click(object sender, RoutedEventArgs e)
        {
            screenRulerWindow = new ScreenRulerWindow(this);
            OpenRulerWindowButton.Visibility = Visibility.Collapsed;
            CloseRulerWindowButton.Visibility = Visibility.Visible;
        }

        private void ScreenRulerWindow_Closed(object sender, WindowEventArgs e)
        {
            OpenRulerWindowButton.Visibility = Visibility.Visible;
            CloseRulerWindowButton.Visibility = Visibility.Collapsed;
        }

        private void ChangeDisplayButton_Click(object sender, RoutedEventArgs e)
        {
            // Close the current window
            screenRulerWindow.Close();
            OpenRulerWindowButton.Visibility = Visibility.Collapsed;
            CloseRulerWindowButton.Visibility = Visibility.Visible;


            // Create a new window on the next display in the display manager
            // Get the next display index
            currentDisplayIndex++;
            if (currentDisplayIndex >= displayManager.DisplayAreas.Count)
            {
                currentDisplayIndex = 0; // Wrap around to the first display if the index exceeds the number of displays
            }

            // Create a new window on the next display
            screenRulerWindow = new ScreenRulerWindow(this, displayManager.DisplayAreas[currentDisplayIndex].DisplayId.Value);

        }
    }
}
