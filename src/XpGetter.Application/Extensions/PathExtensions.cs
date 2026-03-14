namespace XpGetter.Application.Extensions;

public static class PathExtensions
{
    extension(Path)
    {
        public static string ExecutableDirectory => AppContext.BaseDirectory;

        public static string GetFilePathWithinExecutableDirectory(string filePath)
            => Path.GetFullPath(Path.Combine(Path.ExecutableDirectory, filePath));
    }
}
