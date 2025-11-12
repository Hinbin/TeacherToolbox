using System.Reflection;

namespace TeacherToolbox.Helpers
{
    public static class VersionHelper
    {
        public static string GetAppVersion()
        {
            var version = Assembly.GetExecutingAssembly()
                .GetName()
                .Version;

            return $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }
}   