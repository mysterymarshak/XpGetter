namespace XpGetter.Application.Errors;

public abstract class BaseError
{
    public string Message { get; init; } = string.Empty;
    public Exception? Exception { get; init; }
}