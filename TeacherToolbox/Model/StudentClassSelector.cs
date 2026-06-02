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
        private const string AppDataFolderName = "TeacherToolbox";
        private const string ClassesFileName = "classes.json";
        private const string MigratedBackupExtension = ".migrated";

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
            await RepairUnconvertedLegacyMigrationIfNeeded();

            // Load classes.json
            string filePath = GetCurrentClassesFilePath();

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

            string filePath = GetCurrentClassesFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));

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
            string oldFilePath = GetLegacyClassesFilePath();
            string newFilePath = GetCurrentClassesFilePath();

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
                    var legacyClasses = JsonSerializer.Deserialize<List<StudentClass>[]>(json);

                    if (legacyClasses != null)
                    {
                        var convertedClasses = ConvertLegacyDayOrder(legacyClasses);

                        // Write to new location
                        await File.WriteAllTextAsync(newFilePath, JsonSerializer.Serialize(convertedClasses));

                        // Rename old file as backup instead of deleting
                        string backupPath = oldFilePath + MigratedBackupExtension;
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

        private async Task RepairUnconvertedLegacyMigrationIfNeeded()
        {
            string backupPath = GetLegacyClassesFilePath() + MigratedBackupExtension;
            string newFilePath = GetCurrentClassesFilePath();

            if (!File.Exists(backupPath) || !File.Exists(newFilePath))
            {
                return;
            }

            try
            {
                var backupJson = await File.ReadAllTextAsync(backupPath);
                var currentJson = await File.ReadAllTextAsync(newFilePath);

                var legacyClasses = JsonSerializer.Deserialize<List<StudentClass>[]>(backupJson);
                var currentClasses = JsonSerializer.Deserialize<List<StudentClass>[]>(currentJson);

                if (legacyClasses == null || currentClasses == null)
                {
                    return;
                }

                var convertedClasses = ConvertLegacyDayOrder(legacyClasses);

                if (HasSameClassLayout(currentClasses, legacyClasses) &&
                    !HasSameClassLayout(currentClasses, convertedClasses))
                {
                    await File.WriteAllTextAsync(newFilePath, JsonSerializer.Serialize(convertedClasses));
                    Debug.WriteLine("Repaired classes.json day order from legacy migration backup.");
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Error repairing legacy classes.json day order: {e.Message}");
            }
        }

        internal static List<StudentClass>[] ConvertLegacyDayOrder(List<StudentClass>[] legacyClasses)
        {
            var convertedClasses = new List<StudentClass>[7];
            for (int i = 0; i < convertedClasses.Length; i++)
            {
                convertedClasses[i] = new List<StudentClass>();
            }

            if (legacyClasses == null)
            {
                return convertedClasses;
            }

            CopyDay(legacyClasses, convertedClasses, (int)DayOfWeek.Monday, 0);
            CopyDay(legacyClasses, convertedClasses, (int)DayOfWeek.Tuesday, 1);
            CopyDay(legacyClasses, convertedClasses, (int)DayOfWeek.Wednesday, 2);
            CopyDay(legacyClasses, convertedClasses, (int)DayOfWeek.Thursday, 3);
            CopyDay(legacyClasses, convertedClasses, (int)DayOfWeek.Friday, 4);
            CopyDay(legacyClasses, convertedClasses, (int)DayOfWeek.Saturday, 5);
            CopyDay(legacyClasses, convertedClasses, (int)DayOfWeek.Sunday, 6);

            return convertedClasses;
        }

        internal static bool HasSameClassLayout(List<StudentClass>[] first, List<StudentClass>[] second)
        {
            if (first == null || second == null || first.Length != second.Length)
            {
                return false;
            }

            for (int day = 0; day < first.Length; day++)
            {
                var firstDay = first[day] ?? new List<StudentClass>();
                var secondDay = second[day] ?? new List<StudentClass>();

                if (firstDay.Count != secondDay.Count)
                {
                    return false;
                }

                for (int classIndex = 0; classIndex < firstDay.Count; classIndex++)
                {
                    if (!ClassesMatch(firstDay[classIndex], secondDay[classIndex]))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static void CopyDay(List<StudentClass>[] source, List<StudentClass>[] destination, int sourceDay, int destinationDay)
        {
            if (sourceDay >= source.Length || source[sourceDay] == null)
            {
                return;
            }

            destination[destinationDay] = new List<StudentClass>(source[sourceDay]);
        }

        private static bool ClassesMatch(StudentClass first, StudentClass second)
        {
            if (first == null || second == null)
            {
                return first == second;
            }

            return first.ClassName == second.ClassName && first.ClassPath == second.ClassPath;
        }

        private static string GetLegacyClassesFilePath() =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ClassesFileName);

        private static string GetCurrentClassesFilePath() =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppDataFolderName, ClassesFileName);

    }
}
