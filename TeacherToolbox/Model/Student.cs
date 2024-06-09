using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeacherToolbox.Model
{
    public class Student
    {
        public string Name { get; set; }
        public int Weighting { get; set; }

        public Student(string name, int weighting)
        {
            Name = name;
            Weighting = weighting;
        }
    }
}
