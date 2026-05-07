using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

using TeacherToolbox.Services;
using Microsoft.Extensions.DependencyInjection;

namespace TeacherToolbox.Controls;

public abstract class AutomatedPage : Page
{
    protected ITelemetryService TelemetryService { get; }
    protected IThemeService ThemeService { get; }
    protected ISettingsService SettingsService { get; }

    protected AutomatedPage()
    {
        // Resolve services from DI
        var services = App.Current.Services;
        if (services != null)
        {
            TelemetryService = services.GetRequiredService<ITelemetryService>();
            ThemeService = services.GetRequiredService<IThemeService>();
            SettingsService = services.GetRequiredService<ISettingsService>();
        }

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