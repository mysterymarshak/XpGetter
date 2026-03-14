using System.IO.MemoryMappedFiles;
using XpGetter.Application.Extensions;

namespace XpGetter.Application.Features.Io;

public interface IFilesAccessor : IDisposable
{
    bool Exists(string filePath);
    string ReadAllText(string filePath);
    void WriteAllText(string filePath, string content);
    byte[] ReadAllBytes(string filePath);
    Stream OpenStream(string filePath, FileMode mode, FileAccess access);
    MemoryMappedFile CreateReadonlyMemoryMapping(string filePath);
    FileInfo GetInfo(string filePath);
    FilesAccessor AbsolutePaths();
}

public class RestrictedFilesAccessor : FilesAccessor
{
    protected override string GetAbsolutePath(string filePath)
    {
        if (Path.IsPathRooted(filePath))
        {
            throw new InvalidOperationException("Attempt to access rooted path");
        }

        var baseDir = Path.ExecutableDirectory;
        var fullPath = Path.GetFilePathWithinExecutableDirectory(filePath);

        if (!fullPath.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Attempt to access path outside of executable directory");
        }

        return fullPath;
    }
}

public class FilesAccessor : IFilesAccessor
{
    public bool Exists(string filePath)
    {
        var absolutePath = GetAbsolutePath(filePath);
        return File.Exists(absolutePath);
    }

    public string ReadAllText(string filePath)
    {
        var absolutePath = GetAbsolutePath(filePath);
        return File.ReadAllText(absolutePath);
    }

    public void WriteAllText(string filePath, string content)
    {
        var absolutePath = GetAbsolutePath(filePath);
        File.WriteAllText(absolutePath, content);
    }

    public byte[] ReadAllBytes(string filePath)
    {
        var absolutePath = GetAbsolutePath(filePath);
        return File.ReadAllBytes(absolutePath);
    }

    public Stream OpenStream(string filePath, FileMode mode, FileAccess access)
    {
        var absolutePath = GetAbsolutePath(filePath);
        return new FileStream(absolutePath, mode, access);
    }

    public MemoryMappedFile CreateReadonlyMemoryMapping(string filePath)
    {
        var absolutePath = GetAbsolutePath(filePath);
        return MemoryMappedFile.CreateFromFile(absolutePath, FileMode.Open);
    }

    public FileInfo GetInfo(string filePath)
    {
        var absolutePath = GetAbsolutePath(filePath);
        return new FileInfo(absolutePath);
    }

    public FilesAccessor AbsolutePaths()
    {
        return new FilesAccessor();
    }

    public void Dispose()
    {
    }

    protected virtual string GetAbsolutePath(string filePath)
    {
        return filePath;
    }
}
