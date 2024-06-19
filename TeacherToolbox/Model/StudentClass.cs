using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Storage;
using System.Text;
using System.Threading.Tasks;

namespace TeacherToolbox.Model
{
    public class StudentClass
    {
        public string ClassName { get; set; }
        public string ClassPath { get; set; }
        public List<Student> Students { get; set; }
        public DateTime LastSelected { get; set; }

        // Define a list to keep track of previously selected students
        private readonly List<Student> previouslySelected = new();
        private Student lastStudentSelected;

        public StudentClass(string className, string classPath)
        {
            ClassName = className;
            ClassPath = classPath;
            Students = new List<Student>();
        }

        public static async Task<StudentClass> CreateAsync(string className, string classPath)
        {
            var studentClass = new StudentClass(className, classPath);
            await studentClass.LoadStudents();
            return studentClass;
        }

        public void AddStudent(Student student)
        {
            Students.Add(student);
        }

        public void RemoveStudent(Student student)
        {
            Students.Remove(student);
        }

        public void ClearStudents()
        {
            Students.Clear();
        }

        public async Task LoadStudents()
        {
            // Try to open the file - return if it doesn't exist
            StorageFile file = await StorageFile.GetFileFromPathAsync(ClassPath);
            if (file == null) return;

            // Clear the students list
            Students.Clear();

            // Read the file and split the names by line
            string text = await FileIO.ReadTextAsync(file);
            string[] names = text.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string name in names)
            {
                Student student = new Student(name);
                // if the student name is not blank, add it to the list
                if (!string.IsNullOrWhiteSpace(student.Name))
                {
                    Students.Add(student);
                }
            }
        }

        public Student GetRandomStudent()
        {

            Student[] availableStudents;

            // Filter out the last student selected from the array, unless has only one name in it
            if (Students.Count > 1 && lastStudentSelected != null)
            {
                availableStudents = Students.Where(s => s.Name != lastStudentSelected.Name).ToArray();
            }
            else
            {
                availableStudents = Students.ToArray();
            }

            // Check to see if any of the students have weights > 1, if so, call the weighted random selection method
            if (Students.Any(s => s.Weighting > 1))
            {
                return GetWeightedRandomStudent(availableStudents);
            }
            else
            {
                return GetStandardRandomStudent(availableStudents);
            }

        }

        private Student GetWeightedRandomStudent(Student[] availableStudents)
        {
            Random random = new ();

            // Calculate the weight for each available name
            int[] weights = new int[availableStudents.Length];
            for (int i = 0; i < availableStudents.Length; i++)
            {
                weights[i] = availableStudents[i].Weighting;
            }

            // Calculate the total weight
            int totalWeight = weights.Sum();
            int sum = 0;

            // Generate a random number within the total weight range
            int randomNumber = random.Next(1, totalWeight + 1);

            // Select the name based on the random number and weights
            for (int i = 0; i < availableStudents.Length; i++)
            {
                sum += weights[i];
                if (randomNumber <= sum)
                {
                    Student selectedStudent = availableStudents[i];
                    // Add the selected student to the previously selected list
                    previouslySelected.Add(selectedStudent);

                    // If all students have been picked, reset the previously selected list
                    if (previouslySelected.Count == Students.Count)
                    {
                        previouslySelected.Clear();
                    }

                    lastStudentSelected = selectedStudent;
                    return selectedStudent;
                }
            }

            return null;
        }

        private Student GetStandardRandomStudent(Student[] availableStudents)
        {
            Random random = new ();

            // Calculate the weight for each available name
            int[] weights = new int[availableStudents.Length];
            for (int i = 0; i < availableStudents.Length; i++)
            {
                // Assign higher weight to students who haven't been picked.  The more times they have been picked, the lower the weight.
                weights[i] = Students.Count - previouslySelected.Count(s => s == availableStudents[i]);
            }

            // Calculate the total weight
            int totalWeight = weights.Sum();

            // Generate a random number within the total weight range
            int randomNumber = random.Next(1, totalWeight + 1);

            // Select the name based on the random number and weights
            int cumulativeWeight = 0;
            Student selectedStudent;
            for (int i = 0; i < availableStudents.Length; i++)
            {
                cumulativeWeight += weights[i];
                if (randomNumber <= cumulativeWeight)
                {
                    selectedStudent = availableStudents[i];
                    // Add the selected student to the previously selected list
                    previouslySelected.Add(selectedStudent);

                    // If all students have been picked, reset the previously selected list
                    if (previouslySelected.Count == Students.Count)
                    {
                        previouslySelected.Clear();
                    }

                    lastStudentSelected = selectedStudent;
                    return selectedStudent;
                }
            }

            return null;
        }

        // Override == operator to compare StudentClass objects
        public static bool operator == (StudentClass a, StudentClass b)
        {
            if (a is null && b is null)
            {
                return true;
            }
            if (a is null || b is null)
            {
                return false;
            }
            return a.ClassName == b.ClassName && a.ClassPath == b.ClassPath;
        }

        // Override != operator to compare StudentClass objects
        public static bool operator != (StudentClass a, StudentClass b)
        {
            return !(a == b);
        }

        // Override Equals method to compare StudentClass objects
        public override bool Equals(object obj)
        {
            if (obj is StudentClass studentClass)
            {
                return this == studentClass;
            }
            return false;
        }

        // Override GetHashCode method to compare StudentClass objects
        public override int GetHashCode()
        {
            return ClassName.GetHashCode() ^ ClassPath.GetHashCode();
        }

    }
}
