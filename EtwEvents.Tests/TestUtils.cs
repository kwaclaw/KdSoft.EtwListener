using Microsoft.DotNet.PlatformAbstractions;

namespace EtwEvents.Tests
{
    static class TestUtils
    {
        static string? projectDir;
        public static string? ProjectDir {
            get {
                if (projectDir != null)
                    return projectDir;
                var startDir = ApplicationEnvironment.ApplicationBasePath;
                var objDir = FindDirectoryUp(new DirectoryInfo(startDir), "obj");
                return projectDir = objDir?.Parent?.FullName;
            }
        }

        public static DirectoryInfo? FindDirectoryUp(DirectoryInfo curDir, string dirPattern) {
            var result = curDir.EnumerateDirectories(dirPattern).FirstOrDefault();
            if (result == null && curDir.Parent != null) {
                result = FindDirectoryUp(curDir.Parent, dirPattern);
            }
            return result;
        }
    }
}
