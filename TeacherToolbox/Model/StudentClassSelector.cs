using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json.Serialization;

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
            // Load classes.json
            string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "classes.json");

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
                Console.WriteLine("Error loading classes: " + e.Message);
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
            string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "classes.json");

            // Write the serialized JSON string to the specified file path
            File.WriteAllText(filePath, json);

        }

        public void AddClass(StudentClass studentClass, int day)
        {
            studentClasses[day].Add(studentClass);
            SaveClasses();
        }

    }
}
