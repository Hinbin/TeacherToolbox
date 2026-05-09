using System.Threading.Tasks;

namespace TeacherToolbox.Services
{
    public interface IRegisterSyncService
    {
        bool IsEnabled { get; }
        Task SyncAsync();
    }
}
