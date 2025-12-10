namespace XpGetter.Application.Features.Configuration.Repositories.FileOperationStrategies;

public interface IFileOperationStrategy
{
    string ReadFileContent(string filePath);
    void WriteFileContent(string filePath, string content);
}