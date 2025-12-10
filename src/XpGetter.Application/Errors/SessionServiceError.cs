namespace XpGetter.Application.Errors;

public class SessionServiceError : BaseError
{
    public required string ClientName { get; init; }
}