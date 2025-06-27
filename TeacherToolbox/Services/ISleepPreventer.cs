using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeacherToolbox.Services
{
    public interface ISleepPreventer
    {
        void PreventSleep(bool keepDisplayOn = true);
        void AllowSleep();
        void Dispose();
    }
}
