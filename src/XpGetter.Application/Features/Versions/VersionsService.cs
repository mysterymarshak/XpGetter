using Serilog;

namespace XpGetter.Application.Features.Versions;

public interface IVersionService
{
    Task<Version?> GetLatestVersionAsync();
}

public class VersionService : IVersionService
{
    private const string Url = $"https://cdn.jsdelivr.net/gh/{Constants.Author}/{Constants.ProgramName}@{Constants.MasterBranch}/version";

    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    public VersionService(HttpClient httpClient, ILogger logger)
    {
        httpClient.BaseAddress = new Uri(Url);
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<Version?> GetLatestVersionAsync()
    {
        try
        {
            var stringContent = await _httpClient.GetStringAsync(string.Empty);
            var version = new Version(stringContent);
            return version;
        }
        catch (Exception exception)
        {
            _logger.Error(exception, Messages.Version.GetException);
            return null;
        }
    }
}
