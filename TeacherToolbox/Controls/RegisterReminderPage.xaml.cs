using Microsoft.Extensions.DependencyInjection;
using TeacherToolbox.Helpers;
using TeacherToolbox.ViewModels;

namespace TeacherToolbox.Controls
{
    public sealed partial class RegisterReminderPage : AutomatedPage
    {
        public RegisterReminderViewModel ViewModel { get; }

        public RegisterReminderPage() : base()
        {
            ViewModel = App.Current.Services.GetRequiredService<RegisterReminderViewModel>();
            this.InitializeComponent();
            WindowHelper.SetWindowForElement(this, App.MainWindow);
        }
    }
}
