using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace TeacherToolbox.Controls;

public abstract class AutomatedPage : Page
{
    protected AutomatedPage()
    {
        // Get the class name without namespace to use as automation ID
        string className = GetType().Name;
        AutomationProperties.SetAutomationId(this, className);

        // Create automation peer for the page
        var peer = FrameworkElementAutomationPeer.FromElement(this);
        if (peer == null)
        {
            peer = new FrameworkElementAutomationPeer(this);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new FrameworkElementAutomationPeer(this);
    }
}