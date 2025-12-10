namespace XpGetter.Configuration.Repositories.FileOperationStrategies;

public interface IFileOperationStrategy
{
    string ReadFileContent(string filePath);
    void WriteFileContent(string filePath, string content);
}
