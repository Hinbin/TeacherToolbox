using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.Advertisement;
using System.Text.RegularExpressions;

namespace TeacherToolbox.Model
{
    public class Student
    {
        private string _name;

        public string Name
        {
            get => _name;
            set => _name = SanitizeStudentName(value);
        }

        public int Weighting { get; set; }

        public Student(string name)
        {
            Name = name;
        }


        public string SanitizeStudentName(string value)
        {
            // Return if null or blank
            if (string.IsNullOrWhiteSpace(value)) return value;

            string sanitizedName = value;

            // Check to see if there is a number or sequence of numbers at the end of the name. If so, use it as the weighting
            Match match = Regex.Match(value, @"\d+$");
            if (match.Success)
            {
                Weighting = int.Parse(match.Value);
                sanitizedName = value.Substring(0, match.Index);
            }
            else
            {
                Weighting = 1;
            }

            // Use regex to remove any punctuation apart from - and ', but keep apostrophes that are between letters
            sanitizedName = Regex.Replace(sanitizedName, @"(?<![a-zA-Z])'|'(?![a-zA-Z])|[^\w\s'-]", "");

            // Remove trailing repeated single letters (like "I I I I") but keep single letters (like "W")
            sanitizedName = Regex.Replace(sanitizedName, @"\s+([A-Z])(?:\s+\1)+$", "");

            // Modified regex to preserve single letters followed by space and another word, but only at the start
            // Also preserve single capital letters at the end (like "John S")
            sanitizedName = Regex.Replace(sanitizedName, @"\b(?!([A-Z](?=\s+[A-Za-z]{2,}))|[a-zA-Z]'\b|[A-Z]$)\w\b", "");

            // Trim any whitespace and remove multiple spaces
            sanitizedName = Regex.Replace(sanitizedName.Trim(), @"\s+", " ");

            return sanitizedName;
        }

    }
}
