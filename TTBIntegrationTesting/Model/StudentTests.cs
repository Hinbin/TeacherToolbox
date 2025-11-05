using NUnit.Framework;
using TeacherToolbox.Model;

namespace TeacherToolbox.Tests.Model;

[TestFixture]
public class StudentTests
{
    [Test]
    public void Constructor_SetsNameAndDefaultWeighting()
    {
        // Arrange & Act
        var student = new Student("John Smith");

        // Assert
        Assert.That(student.Name, Is.EqualTo("John Smith"));
        Assert.That(student.Weighting, Is.EqualTo(1));
    }

    [Test]
    public void SanitizeName_WithNumberSuffix_ExtractsWeighting()
    {
        // Arrange & Act
        var student = new Student("John Smith3");

        // Assert
        Assert.That(student.Name, Is.EqualTo("John Smith"));
        Assert.That(student.Weighting, Is.EqualTo(3));
    }

    [Test]
    public void SanitizeName_WithInvalidPunctuation_RemovesPunctuation()
    {
        // Arrange & Act
        var student = new Student("John! @Smith#$%");

        // Assert
        Assert.That(student.Name, Is.EqualTo("John Smith"));
    }

    [Test]
    public void SanitizeName_WithValidApostrophe_KeepsApostrophe()
    {
        // Arrange & Act
        var student = new Student("O'Connor");

        // Assert
        Assert.That(student.Name, Is.EqualTo("O'Connor"));
    }


    [Test]
    public void SanitizeName_WithSingleCharacterWord_RemovesSingleCharAtEnd()
    {
        // Arrange & Act
        var student = new Student("John Smith I I I I I I I I I I");

        // Assert
        Assert.That(student.Name, Is.EqualTo("John Smith"));
    }

    [Test]
    public void SanitizeName_WithSingleNonRepeatingCharacterWord_KeepCharAtEnd()
    {
        // Arrange & Act
        var student = new Student("John S");

        // Assert
        Assert.That(student.Name, Is.EqualTo("John S"));
    }

    [Test]
    public void SanitizeName_WithSingleCharacterWord_RemovesPunctuationCharsAtEnd()
    {
        // Arrange & Act
        var student = new Student("John Smith / / / / / / / / / /");

        // Assert
        Assert.That(student.Name, Is.EqualTo("John Smith"));
    }

    [Test]
    public void SanitizeName_WithSingleCharacterWord_KeepsInitials()
    {
        // Arrange & Act
        var student = new Student("J Smith");

        // Assert
        Assert.That(student.Name, Is.EqualTo("J Smith"));
    }


    [Test]
    public void SanitizeName_WithSingleCharacterAndApostrophe_KeepsSingleChar()
    {
        // Arrange & Act
        var student = new Student("O'Brien");

        // Assert
        Assert.That(student.Name, Is.EqualTo("O'Brien"));
    }

    [Test]
    public void SanitizeName_WithNullOrWhitespace_ReturnsOriginalValue()
    {
        // Arrange & Act
        var student1 = new Student(null);
        var student2 = new Student("   ");

        // Assert
        Assert.That(student1.Name, Is.Null);
        Assert.That(student2.Name, Is.EqualTo("   "));
    }

    [Test]
    public void SanitizeName_WithHyphen_KeepsHyphen()
    {
        // Arrange & Act
        var student = new Student("Mary-Jane Wilson");

        // Assert
        Assert.That(student.Name, Is.EqualTo("Mary-Jane Wilson"));
    }

}