using System.Threading.Tasks;

namespace TeacherToolbox.Services
{
    public sealed class DisabledRegisterSyncService : IRegisterSyncService
    {
        public bool IsEnabled => false;

        public Task SyncAsync()
        {
            return Task.CompletedTask;
        }
    }
}
