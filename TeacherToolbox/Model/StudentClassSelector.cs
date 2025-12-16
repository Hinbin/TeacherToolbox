using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json.Serialization;
using System.Diagnostics;

namespace TeacherToolbox.Model
{
    public class StudentClassSelector : ObservableObject
    {

        public List<StudentClass>[] studentClasses;
        public StudentClass currentClass;

        public StudentClassSelector()
        {
        }

        public static async Task<StudentClassSelector> CreateAsync()
        {
            var studentClassSelector = new StudentClassSelector();
            await studentClassSelector.LoadClasses();
            return studentClassSelector;
        }

        public void RemoveClass(StudentClass studentClass, int day)
        {
            studentClasses[day].Remove(studentClass);
            SaveClasses();
        }

        public async Task LoadClasses()
        {
            // Check for and migrate old classes.json if it exists
            await MigrateOldClassesFile();

            // Load classes.json
            string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TeacherToolbox", "classes.json");

            // Handle any exceptions - don't load the file if there is an exception

            try
            {
                if (File.Exists(filePath))
                {
                    string json = await File.ReadAllTextAsync(filePath);
                    studentClasses = JsonSerializer.Deserialize<List<StudentClass>[]>(json);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("Error loading classes: " + e.Message);
            }

            // Check to see if the studentClasses object is null, if it is, create a new one
            if (studentClasses == null)
            {
                studentClasses = new List<StudentClass>[7];
                for (int i = 0; i < 7; i++)
                {
                    studentClasses[i] = new List<StudentClass>();
                }
            }

            // Check for duplicate class names and remove them for each day
            foreach (List<StudentClass> day in studentClasses)
            {
                var duplicates = day.GroupBy(x => x.ClassName).Where(g => g.Count() > 1).Select(y => y.Key).ToList();
                foreach (var duplicate in duplicates)
                {
                    // Remove the last duplicate
                    day.Remove(day.Last(x => x.ClassName == duplicate));                    
                }
            }

            // Check each file path to see if the file still exists, if it doesn't, remove them
            List<StudentClass> classesToRemove = new();

            foreach (List<StudentClass> day in studentClasses)
            {
                foreach (StudentClass studentClass in day)
                {
                    if (!File.Exists(studentClass.ClassPath))
                    {
                        classesToRemove.Add(studentClass);
                    }
                }


                foreach (StudentClass studentClass in classesToRemove)
                {
                    day.Remove(studentClass);
                }

                // For each StudentClass remaining, reload the class
                await Task.WhenAll(day.Select(studentClass => studentClass.LoadStudents()));
            }

        }

        public void SaveClasses()
        {
            // Save the classes to the JSON file classes.json in the Environment.SpecialFolder.LocalApplicationData path
            // Serialize the student classes object
            string json = JsonSerializer.Serialize(studentClasses);

            // Specify the file path where you want to save the JSON file

            string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TeacherToolbox", "classes.json");

            // Write the serialized JSON string to the specified file path
            File.WriteAllText(filePath, json);

        }

        public void AddClass(StudentClass studentClass, int day)
        {
            studentClasses[day].Add(studentClass);
            SaveClasses();
        }

        /// <summary>
        /// Migrates classes.json from the old location (LocalApplicationData root) to the new location (LocalApplicationData/TeacherToolbox).
        /// This ensures backward compatibility when upgrading from older versions.
        /// </summary>
        private async Task MigrateOldClassesFile()
        {
            string oldFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "classes.json");
            string newFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TeacherToolbox", "classes.json");

            // Only migrate if old file exists and new file doesn't exist
            if (File.Exists(oldFilePath) && !File.Exists(newFilePath))
            {
                try
                {
                    Debug.WriteLine($"Migrating classes.json from {oldFilePath} to {newFilePath}");

                    // Ensure the TeacherToolbox directory exists
                    string newDirectory = Path.GetDirectoryName(newFilePath);
                    if (!Directory.Exists(newDirectory))
                    {
                        Directory.CreateDirectory(newDirectory);
                    }

                    // Read the old file
                    string json = await File.ReadAllTextAsync(oldFilePath);

                    // Validate it can be deserialized (ensures it's valid data)
                    var testDeserialize = JsonSerializer.Deserialize<List<StudentClass>[]>(json);

                    if (testDeserialize != null)
                    {
                        // Write to new location
                        await File.WriteAllTextAsync(newFilePath, json);

                        // Rename old file as backup instead of deleting
                        string backupPath = oldFilePath + ".migrated";
                        File.Move(oldFilePath, backupPath);

                        Debug.WriteLine($"Migration successful. Old file backed up to {backupPath}");
                    }
                    else
                    {
                        Debug.WriteLine("Migration failed: Could not deserialize old classes.json");
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"Error migrating classes.json: {e.Message}");
                }
            }
        }

    }
}
