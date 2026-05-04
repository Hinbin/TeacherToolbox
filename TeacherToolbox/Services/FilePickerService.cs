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

        public async Task<StorageFile> SaveFileAsync(IntPtr windowHandle, string suggestedFileName, string[] fileTypes)
        {
            var savePicker = new FileSavePicker();
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, windowHandle);
            savePicker.SuggestedFileName = suggestedFileName;

            foreach (var type in fileTypes)
            {
                savePicker.FileTypeChoices.Add(type.ToUpper().TrimStart('.') + " files", new[] { type });
            }

            return await savePicker.PickSaveFileAsync();
        }
    }
}