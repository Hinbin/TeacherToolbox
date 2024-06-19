using Microsoft.UI.Windowing;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeacherToolbox.Helpers
// Needed due to https://github.com/microsoft/microsoft-ui-xaml/issues/6454
{
    public class DisplayManager : IDisposable
    {

        public ObservableCollection<DisplayArea> DisplayAreas;
        private DisplayAreaWatcher watcher;

        public DisplayManager()
        {
            DisplayAreas = new ObservableCollection<DisplayArea>();
            watcher = DisplayArea.CreateWatcher();
            watcher.Added += Display_Added;
            watcher.Removed += Display_Removed;
            watcher.Start();
        }

        private void Display_Added(DisplayAreaWatcher sender, DisplayArea args)
        {
            DisplayAreas.Add(args);
        }

        private void Display_Removed(DisplayAreaWatcher sender, DisplayArea args)
        {
            DisplayAreas.Remove(args);
        }

        public void Dispose()
        {
            watcher.Stop();
            watcher = null;
        }

        public ulong PrimaryDisplayId
        {
            // Iterate through DisplayAreas looking for DisplayArea.IsPrimary == true
            get
            {
                foreach (var displayArea in DisplayAreas)
                {
                    if (displayArea.IsPrimary)
                    {
                        return displayArea.DisplayId.Value;
                    }
                }
                return 0;
            }
        }

        public DisplayArea GetDisplayArea(ulong displayId)
        {
            foreach (var displayArea in DisplayAreas)
            {
                if (displayArea.DisplayId.Value == displayId)
                {
                    return displayArea;
                }
            }
            return null;
        }

    }
}
