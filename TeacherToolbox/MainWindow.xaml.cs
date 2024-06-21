using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Linq;
using TeacherToolbox.Controls;
using System.IO;
using System.IO.Pipes;
using System.Diagnostics;
using WinUIEx;
using Microsoft.UI.Input;



// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.


namespace TeacherToolbox
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow
    {
        private readonly OverlappedPresenter _presenter;
        private NamedPipeServerStream pipeServer;
        private WindowDragHelper dragHelper;

        private void ContentFrame_NavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        public MainWindow()
        {
            this.InitializeComponent();

            _presenter = this.AppWindow.Presenter as OverlappedPresenter;
            Windows.Graphics.SizeInt32 size = new(_Width: 600, _Height: 200);
            this.AppWindow.ResizeClient(size);
                       
            this.ExtendsContentIntoTitleBar = true;
            SetRegionsForCustomTitleBar(); // To allow the nav button to be selectable, but the rest of the title bar to function as normal

            NavView.IsPaneOpen = false;

            pipeServer = new NamedPipeServerStream("ShotcutWatcher", PipeDirection.In);
            ListenForKeyPresses();

            this.SetIsAlwaysOnTop(true);

            dragHelper = new WindowDragHelper(this);

            try
            {
                // Start the KeyInterceptor application
                Process.Start("ShortcutWatcher.exe");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            this.Closed += MainWindow_Closed;
        }

        private void MainWindow_Closed(object sender, WindowEventArgs e)
        {
            foreach (var process in Process.GetProcessesByName("ShortcutWatcher"))
            {
                process.Kill();
            }

        }

        private async void ListenForKeyPresses()
        {
            await pipeServer.WaitForConnectionAsync();

            using (StreamReader reader = new StreamReader(pipeServer))
            {
                string key;
                while ((key = await reader.ReadLineAsync()) != null)
                {
                    // If ALT + number key is pressed, create a timerWindow with the specified time
                    if (key.StartsWith("D"))
                    {
                        string time = key.Substring(1);
                        if (int.TryParse(time, out int timeInt))
                        {
                            TimerWindow timerWindow;

                            // If the number is 0, start a 30 second timer.  Otherwise do it for that number of minutes
                            if (timeInt == 0)
                            {
                                timerWindow = new TimerWindow(30);
                                await timerWindow.InitializeAsync();
                            } else
                            {
                                timerWindow = new TimerWindow(timeInt * 60);
                                await timerWindow.InitializeAsync();
                            }

                            timerWindow.Activate();

                        }
                    }else if (key == "F9")
                    {

                        // Grab focus and unminiize the window
                        this.Activate();
                        
                        // Navigate to the RandomNameGenerator page if needed

                        if (ContentFrame.SourcePageType != typeof(RandomNameGenerator))
                        {
                            NavView.SelectedItem = NavView.MenuItems[1];
                            NavView_Navigate(typeof(RandomNameGenerator), new EntranceNavigationTransitionInfo());
                        }

                        // Call the GenerateName function of the RandomNameGenerator page
                        if (ContentFrame.Content is RandomNameGenerator randomNameGenerator)
                        {
                            randomNameGenerator.GenerateName();
                        }
                    }
                }
            }
        }

        private void NavView_Loaded(object sender, RoutedEventArgs e)
        {
            // Add handler for ContentFrame navigation.
            ContentFrame.Navigated += On_Navigated;

            // NavView doesn't load any page by default, so load home page.
            NavView.SelectedItem = NavView.MenuItems[0];
            // If navigation occurs on SelectionChanged, this isn't needed.
            // Because we use ItemInvoked to navigate, we need to call Navigate
            // here to load the home page.
            NavView_Navigate(typeof(RandomNameGenerator), new EntranceNavigationTransitionInfo());
            NavView.Header = null;
        }

        private void NavView_Navigate( Type navPageType, NavigationTransitionInfo transitionInfo)
        {
            // Get the page type before navigation so you can prevent duplicate
            // entries in the backstack.
            Type preNavPageType = ContentFrame.CurrentSourcePageType;

            // Only navigate if the selected page isn't currently loaded.
            if (navPageType is not null && !Type.Equals(preNavPageType, navPageType))
            {
                ContentFrame.Navigate(navPageType, null, transitionInfo);
            }
        }

        private void On_Navigated(object sender, NavigationEventArgs e)
        {
            NavView.IsBackEnabled = ContentFrame.CanGoBack;

            if (ContentFrame.SourcePageType != null)
            {
                // Select the nav view item that corresponds to the page being navigated to.
                NavView.SelectedItem = NavView.MenuItems
                            .OfType<NavigationViewItem>()
                            .First(i => i.Tag.Equals(ContentFrame.SourcePageType.FullName.ToString()));

                dragHelper.OnNavigate();                
            }

            // Only enable always on top for the RNG page
            if (ContentFrame.SourcePageType == typeof(RandomNameGenerator))
            {
                this.SetIsAlwaysOnTop(true);
            } else
            {
                this.SetIsAlwaysOnTop(false);
            }
        }

        private void NavView_BackRequested(NavigationView sender,
                                   NavigationViewBackRequestedEventArgs args)
        {
            TryGoBack();
        }

        private bool TryGoBack()
        {
            if (!ContentFrame.CanGoBack)
                return false;

            // Don't go back if the nav pane is overlayed.
            if (NavView.IsPaneOpen &&
                (NavView.DisplayMode == NavigationViewDisplayMode.Compact ||
                 NavView.DisplayMode == NavigationViewDisplayMode.Minimal))
                return false;

            ContentFrame.GoBack();
            return true;
        }

        private void NavView_ItemInvoked(NavigationView sender,
                                 NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItemContainer != null)
            {
                Type navPageType = Type.GetType(args.InvokedItemContainer.Tag.ToString());
                NavView_Navigate(navPageType, args.RecommendedNavigationTransitionInfo);
            }
        }

        private void Grid_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            dragHelper.PointerReleased(sender, e);
        }

        private void Grid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            dragHelper.PointerPressed(sender, e);
        }

        private void Grid_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            dragHelper.PointerMoved(sender, e);
        }

        private void NavView_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // If the random name generator is currently open, generate a new name
            // Call the GenerateName function of the RandomNameGenerator page
            if (ContentFrame.Content is RandomNameGenerator randomNameGenerator)
            {
                // Return if a button was the source of the tap
                if (e.OriginalSource is Button)
                {
                    return;
                }

                // If the sender is a text block with the word start, return
                if (e.OriginalSource is TextBlock textBlock)
                {
                    if (textBlock.Text == "Add Class")
                    {
                        return;
                    }
                }


                randomNameGenerator.GenerateName();
            }
        }

        private void SetRegionsForCustomTitleBar()
        {
            InputNonClientPointerSource nonClientInputSrc =
            InputNonClientPointerSource.GetForWindowId(this.AppWindow.Id);
            // Make everything apart from the navigation burger button region non-client
            nonClientInputSrc.SetRegionRects(NonClientRegionKind.Caption,
                new Windows.Graphics.RectInt32[]
                {
                    new Windows.Graphics.RectInt32(48, 0, 552, 48)
                });
        }

    }

}
