using Microsoft.Extensions.DependencyInjection;
using TeacherToolbox.ViewModels;

namespace TeacherToolbox
{
    public class ViewModelLocator
    {
        public ClockViewModel Clock => App.Current.Services.GetRequiredService<ClockViewModel>();
        public SettingsViewModel Settings => App.Current.Services.GetRequiredService<SettingsViewModel>();
        public TimerWindowViewModel TimerWindow => App.Current.Services.GetRequiredService<TimerWindowViewModel>();
        public RandomNameGeneratorViewModel RandomNameGenerator => App.Current.Services.GetRequiredService<RandomNameGeneratorViewModel>();
    }
}
