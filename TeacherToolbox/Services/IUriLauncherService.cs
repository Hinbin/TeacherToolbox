using System;
using System.Threading.Tasks;

namespace TeacherToolbox.Services
{
    public interface IUriLauncherService
    {
        Task<bool> LaunchUriAsync(Uri uri);
    }
}
