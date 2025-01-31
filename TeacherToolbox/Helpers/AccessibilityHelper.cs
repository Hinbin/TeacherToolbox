// File: Helpers/AccessibilityHelper.cs
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;

namespace TeacherToolbox.Helpers
{
    public static class AccessibilityHelper
    {
        public static void AnnounceActionForAccessibility(UIElement element, string announcement, string activityId)
        {
            if (element == null) return;

            var peer = FrameworkElementAutomationPeer.FromElement(element);
            peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }
    }
}