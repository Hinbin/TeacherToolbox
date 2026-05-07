using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace TeacherToolbox.Services
{
    public class FilePickerService : IFilePickerService
    {
        public async Task<StorageFile> PickTextFileAsync(IntPtr windowHandle)
        {
#if DEBUG
            var testFilePath = Environment.GetEnvironmentVariable("TEACHER_TOOLBOX_TEST_PICK_FILE");
            var testFilePathFile = Environment.GetEnvironmentVariable("TEACHER_TOOLBOX_TEST_PICK_FILE_PATH_FILE");
            if (string.IsNullOrWhiteSpace(testFilePath) &&
                !string.IsNullOrWhiteSpace(testFilePathFile) &&
                File.Exists(testFilePathFile))
            {
                testFilePath = await File.ReadAllTextAsync(testFilePathFile);
            }

            testFilePath = testFilePath?.Trim();
            if (!string.IsNullOrWhiteSpace(testFilePath) && File.Exists(testFilePath))
            {
                return await StorageFile.GetFileFromPathAsync(testFilePath);
            }
#endif

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
