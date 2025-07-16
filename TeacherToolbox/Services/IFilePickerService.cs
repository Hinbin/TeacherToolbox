using System;
using System.Threading.Tasks;
using Windows.Storage;

namespace TeacherToolbox.Services
{
    public interface IFilePickerService
    {
        Task<StorageFile> PickTextFileAsync(IntPtr windowHandle);
    }
}