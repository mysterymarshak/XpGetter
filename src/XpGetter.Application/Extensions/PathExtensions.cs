namespace XpGetter.Application.Extensions;

public static class PathExtensions
{
    extension(Path)
    {
        public static string GetFilePathWithinExecutableDirectory(string fileName)
            => Path.Combine(AppContext.BaseDirectory, fileName);
    }
}