using System.Collections.Generic;

namespace TeacherToolbox.Helpers
{
    /// <summary>
    /// Helper class for managing timer sound settings
    /// </summary>
    public static class SoundSettings
    {
        public const string SoundKey = "Sound";

        /// <summary>
        /// Class representing a sound option
        /// </summary>
        public class SoundOption
        {
            /// <summary>
            /// Display name of the sound
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// Filename of the sound file
            /// </summary>
            public string FileName { get; set; }

            /// <summary>
            /// Default constructor
            /// </summary>
            public SoundOption() { }

            /// <summary>
            /// Constructor with name and filename
            /// </summary>
            public SoundOption(string name, string fileName)
            {
                Name = name;
                FileName = fileName;
            }
        }

        /// <summary>
        /// Dictionary of available sound options
        /// </summary>
        public static readonly Dictionary<int, SoundOption> SoundOptions = new Dictionary<int, SoundOption>
        {
            { 0, new SoundOption { Name = "Strings", FileName = "strings.wav" } },
            { 1, new SoundOption { Name = "Ding", FileName = "ring.wav" } },
            { 2, new SoundOption { Name = "Double Chime", FileName = "doublechime.mp3" } },
            { 3, new SoundOption { Name = "Marimba", FileName = "marimba.mp3" } },
            { 4, new SoundOption { Name = "Wind Chimes", FileName = "windchimes.mp3" } },
            { 5, new SoundOption { Name = "Announcement", FileName = "announcement.wav" } }
        };

/// <summary>
/// Gets the sound filename for the specified index
/// </summary>
public static string GetSoundFileName(int index)
        {
            if (SoundOptions.TryGetValue(index, out var option))
            {
                return option.FileName;
            }
            return "strings.wav"; // Default fallback
        }
    }
}