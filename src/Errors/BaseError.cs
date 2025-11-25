namespace XpGetter.Errors;

public abstract class BaseError
{
    public required string Message { get; init; }
    public Exception? Exception { get; init; }
}