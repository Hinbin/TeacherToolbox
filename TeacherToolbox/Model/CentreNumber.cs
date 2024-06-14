using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm;
using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;

namespace TeacherToolbox.Model
{
    public class CentreNumber : ObservableObject
    {
        public string centreText;
        public string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "centreNumber.json");

        public CentreNumber()
        {
        }

        public static async Task<CentreNumber> CreateAsync()
        {
            var centreNumber = new CentreNumber();
            await centreNumber.LoadSettings();
            return centreNumber;
        }

        public string CentreText
        {
            get
            {
                return centreText;
            }
            // For the setter, automatically save the settings to the JSON file
            
            set
            {
                SetProperty(ref centreText, value);
                SaveSettings();
            }
        }

        public void SaveSettings()
        {
            // Save settings to centreNumber.json
            // Save the classes to the JSON file StudentClasses.json in the Environment.SpecialFolder.LocalApplicationData path
            string json = JsonSerializer.Serialize(this);            

            // Write the serialized JSON string to the specified file path
            File.WriteAllText(filePath, json);

        }

        public async Task LoadSettings()
        {
            // Load settings from centreNumber.json
            // Load the classes from the JSON file StudentClasses.json in the Environment.SpecialFolder.LocalApplicationData path
            try
            {
                if (File.Exists(filePath))
                {
                    string json = await File.ReadAllTextAsync(filePath);
                    this.centreText = JsonSerializer.Deserialize<CentreNumber>(json).centreText;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error loading classes: " + e.Message);
            }

        }

    }
}
