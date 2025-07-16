using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace TeacherToolbox.Services
{
    public class FilePickerService : IFilePickerService
    {
        public async Task<StorageFile> PickTextFileAsync(IntPtr windowHandle)
        {
            var openPicker = new FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, windowHandle);
            openPicker.FileTypeFilter.Add(".txt");

            return await openPicker.PickSingleFileAsync();
        }
    }
}