using System;
using System.Threading.Tasks;
using Windows.System;

namespace TeacherToolbox.Services
{
    public class UriLauncherService : IUriLauncherService
    {
        public async Task<bool> LaunchUriAsync(Uri uri)
        {
            return await Launcher.LaunchUriAsync(uri);
        }
    }
}
