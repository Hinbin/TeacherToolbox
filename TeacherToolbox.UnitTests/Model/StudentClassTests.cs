using NUnit.Framework;
using TeacherToolbox.Model;

namespace TeacherToolbox.UnitTests.Model;

[TestFixture]
public class StudentClassTests
{
    [Test]
    public void GetRandomStudent_WithWeightedStudents_SelectsHigherWeightMoreOften()
    {
        var studentClass = CreateClass("A", "Heavy10", "Light One", "Light Two");
        var counts = new Dictionary<string, int>
        {
            ["Heavy"] = 0,
            ["Light One"] = 0,
            ["Light Two"] = 0
        };

        for (int i = 0; i < 2000; i++)
        {
            var selectedStudent = studentClass.GetRandomStudent();

            counts[selectedStudent.Name]++;
        }

        Assert.That(counts["Heavy"], Is.GreaterThan(counts["Light One"]));
        Assert.That(counts["Heavy"], Is.GreaterThan(counts["Light Two"]));
    }

    [Test]
    public void GetRandomStudent_WithMultipleStudents_DoesNotPickSameStudentConsecutively()
    {
        var studentClass = CreateClass("A", "Alice", "Bob", "Cara");
        Student previousStudent = null;

        for (int i = 0; i < 100; i++)
        {
            var selectedStudent = studentClass.GetRandomStudent();

            Assert.That(selectedStudent, Is.Not.Null);
            Assert.That(selectedStudent.Name, Is.Not.EqualTo(previousStudent?.Name));
            previousStudent = selectedStudent;
        }
    }

    [Test]
    public void GetRandomStudent_WithSingleStudent_ReturnsThatStudent()
    {
        var studentClass = CreateClass("A", "Alice");

        for (int i = 0; i < 10; i++)
        {
            var selectedStudent = studentClass.GetRandomStudent();

            Assert.That(selectedStudent, Is.Not.Null);
            Assert.That(selectedStudent.Name, Is.EqualTo("Alice"));
        }
    }

    [Test]
    public void GetRandomStudent_WithNoStudents_ReturnsNull()
    {
        var studentClass = new StudentClass("A", "A.txt");

        var selectedStudent = studentClass.GetRandomStudent();

        Assert.That(selectedStudent, Is.Null);
    }

    private static StudentClass CreateClass(string className, params string[] studentNames)
    {
        var studentClass = new StudentClass(className, $"{className}.txt");
        foreach (var studentName in studentNames)
        {
            studentClass.AddStudent(new Student(studentName));
        }

        return studentClass;
    }
}
