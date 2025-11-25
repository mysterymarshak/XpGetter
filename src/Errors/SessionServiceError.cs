namespace XpGetter.Errors;

public class SessionServiceError : BaseError
{
    public required string ClientName { get; init; }
}