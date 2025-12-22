using SteamKit2;

namespace XpGetter.Application.Results;

public record SteamKitJobFailed(AsyncJobFailedException Exception);