using XpGetter.Application.Features.Io;

namespace XpGetter.Application.Features.Configuration.Repositories.FileOperationStrategies;

public class DirectFileOperationStrategy : IFileOperationStrategy
{
    private readonly IFilesAccessor _filesAccessor;

    public DirectFileOperationStrategy(IFilesAccessor filesAccessor)
    {
        _filesAccessor = filesAccessor;
    }

    public string ReadFileContent(string filePath)
    {
        return _filesAccessor.ReadAllText(filePath);
    }

    public void WriteFileContent(string filePath, string content)
    {
        _filesAccessor.WriteAllText(filePath, content);
    }
}
