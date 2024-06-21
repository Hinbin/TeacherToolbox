using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm;
using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;
using Windows.Graphics;
using System.Text.Json.Serialization;
using System.Runtime.CompilerServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;

namespace TeacherToolbox.Model
{

    public struct WindowPosition
    {
        public int X { get; set; }
        public int Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public ulong DisplayID { get; set; } 

        public WindowPosition(int x, int y, double width, double height, ulong displayId)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            DisplayID = displayId;
        }
    }

public class LocalSettings : ObservableObject
    {
        private string centreText;
        private WindowPosition lastWindowPosition;
        private string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "centreNumber.json");

        public LocalSettings()
        {
            centreText = "Centre";
            lastWindowPosition = new WindowPosition(0, 0, 0, 0, 0);
        }

        public static async Task<LocalSettings> CreateAsync()
        {
            var localSettings = new LocalSettings();
            await localSettings.LoadSettings();
            return localSettings;
        }

        private void SetAndSave<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (SetProperty(ref field, value, propertyName))
            {
                SaveSettings();
            }
        }

        public WindowPosition LastWindowPosition
        {
            get => lastWindowPosition;
            set => SetAndSave(ref lastWindowPosition, value);
        }

        public string CentreText
        {
            get => centreText;
            set => SetAndSave(ref centreText, value);
        }

    public void SaveSettings()
        {
            // Save settings to centreNumber.json
            // Save the classes to the JSON file StudentClasses.json in the Environment.SpecialFolder.LocalApplicationData path
            string json = JsonSerializer.Serialize<LocalSettings>(this);            

            // Write the serialized JSON string to the specified file path
            File.WriteAllText(filePath, json);

        }

        public async Task LoadSettings()
        {
            try
            {
                if (File.Exists(filePath))
                {
                    string json = await File.ReadAllTextAsync(filePath);
                    var localSettings = JsonSerializer.Deserialize<LocalSettings>(json);
                    if (localSettings != null)
                    {
                        this.CentreText = localSettings.CentreText;
                        // Assume GetDisplayAreaFromID is a method that retrieves a DisplayArea object based on a DisplayID
                        this.LastWindowPosition = localSettings.LastWindowPosition;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error loading settings: " + e.Message);
            }
        }

    }
}
