namespace XpGetter.Application.Utils;

public interface IQrCode
{
    void Draw(string message, string content);
    void Clear();
    void Reset();
}
