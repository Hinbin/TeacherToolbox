using System.Collections.Generic;

namespace TeacherToolbox.Helpers
{
    public static class SoundSettings
    {
        public const string SoundKey = "Sound";

        public static readonly Dictionary<int, (string Name, string FileName)> SoundOptions = new()
        {
            { 0, ("Strings", "strings.wav") },
            { 1, ("Ding", "ring.wav") },
            { 2, ("Double Chime", "doublechime.mp3") },    
            { 3, ("Marimba", "marimba.mp3") },
            { 4, ("Wind Chimes", "windchimes.mp3") },
            { 5, ("Announcement", "announcement.wav") }
        };

        public static string GetSoundFileName(int index)
        {
            return SoundOptions.TryGetValue(index, out var sound)
                ? sound.FileName
                : SoundOptions[0].FileName; // Fallback to default
        }

        public static string GetSoundName(int index)
        {
            return SoundOptions.TryGetValue(index, out var sound)
                ? sound.Name
                : SoundOptions[0].Name; // Fallback to default
        }
    }
}