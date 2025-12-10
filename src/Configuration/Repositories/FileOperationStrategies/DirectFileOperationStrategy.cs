namespace XpGetter.Configuration.Repositories.FileOperationStrategies;

public class DirectFileOperationStrategy : IFileOperationStrategy
{
    public string ReadFileContent(string filePath)
    {
        return File.ReadAllText(filePath);
    }

    public void WriteFileContent(string filePath, string content)
    {
        File.WriteAllText(filePath, content);
    }
}
