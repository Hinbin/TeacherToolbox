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

        public List<StudentClass> studentClasses = new ();
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

        public void RemoveClass(StudentClass studentClass)
        {
            studentClasses.Remove(studentClass);
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
                    studentClasses = JsonSerializer.Deserialize<List<StudentClass>>(json);

                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error loading classes: " + e.Message);
            }

            // Check for duplicate class names and remove them
            var duplicates = studentClasses.GroupBy(x => x.ClassName).Where(g => g.Count() > 1).Select(y => y.Key).ToList();
            foreach (var duplicate in duplicates)
            {
                studentClasses.RemoveAll(x => x.ClassName == duplicate);
            }

            // Check each file path to see if the file still exists, if it doesn't, remove them
            List<StudentClass> classesToRemove = new ();

            foreach (StudentClass studentClass in studentClasses)
            {
                if (!File.Exists(studentClass.ClassPath))
                {
                    classesToRemove.Add(studentClass);
                }
            }

            foreach (StudentClass studentClass in classesToRemove)
            {
                studentClasses.Remove(studentClass);
            }

            // For each StudentClass remaining, reload the class
            await Task.WhenAll(studentClasses.Select(studentClass => studentClass.LoadStudents()));
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

        public void AddClass(StudentClass studentClass)
        {
            studentClasses.Add(studentClass);
            SaveClasses();
        }

    }
}
