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
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace TeacherToolbox.Controls
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ScreenRulerPage : Page
    {

        ScreenRulerWindow screenRulerWindow;

        public ScreenRulerPage()
        {
            this.InitializeComponent();
            // Create a new instance of the ruler

            screenRulerWindow = new ScreenRulerWindow();
            
        }

        public void Close_Ruler_Window_Click(object sender, RoutedEventArgs e)
        {
            screenRulerWindow.Close();
        }
    }
}
