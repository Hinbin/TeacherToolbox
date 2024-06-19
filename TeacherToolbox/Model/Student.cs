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

            // Check to see if there is a number or sequence of numbers at the end of the name.  If so, use it as the weighting
            Match match = Regex.Match(value, @"\d+$");
            if (match.Success)
            {
                Weighting = int.Parse(match.Value);
                sanitizedName = value.Substring(0, match.Index);
            } else
                {
                Weighting = 1;
            }

            // Use regex to remove any punctuationapart from - and '
            sanitizedName = Regex.Replace(sanitizedName, @"[^\w\s'-]", "");

            // Remove any single character words
            sanitizedName = Regex.Replace(sanitizedName, @"\b\w\b", "");
            
            // Trim any whitespace
            sanitizedName = sanitizedName.Trim();

            return sanitizedName;

        }
    }
}
