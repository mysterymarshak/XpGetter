using XpGetter.Application.Features.Io;
using OneOf;
using XpGetter.Application.Errors;
using XpGetter.Application.Utils.Progress;

namespace XpGetter.Application.Features.Cs2;

public interface IKvsProvider
{
    Task<OneOf<string, ReadOnlyMemory<byte>, KvsProviderError>>
        GetFileAsync(KvFileType fileType, IProgressContext ctx);
}

public class KvsProvider : IKvsProvider
{
    private const string ItemsGameUrl =
        "https://raw.githubusercontent.com/SteamDatabase/GameTracking-CS2/master/game/csgo/pak01_dir/scripts/items/items_game.txt";
    private const string CsgoEnglishUrl =
        "https://raw.githubusercontent.com/SteamDatabase/GameTracking-CS2/master/game/csgo/pak01_dir/resource/csgo_english.txt";

    private const string ItemsGamePath = "items_game.txt";
    private const string CsgoEnglishPath = "csgo_english.txt";

    private readonly HttpClient _httpClient;
    private readonly IFilesAccessor _filesAccessor;

    public KvsProvider(HttpClient httpClient, IFilesAccessor filesAccessor)
    {
        _httpClient = httpClient;
        _filesAccessor = filesAccessor;
    }

    public Task<OneOf<string, ReadOnlyMemory<byte>, KvsProviderError>>
        GetFileAsync(KvFileType fileType, IProgressContext ctx)
    {
        return fileType switch
        {
            KvFileType.ItemsGame => GetFileInternalAsync(ItemsGameUrl, ItemsGamePath, fileType, ctx),
            KvFileType.Localization => GetFileInternalAsync(CsgoEnglishUrl, CsgoEnglishPath, fileType, ctx),
            _ => throw new ArgumentOutOfRangeException(nameof(fileType))
        };
    }

    private async Task<OneOf<string, ReadOnlyMemory<byte>, KvsProviderError>>
        GetFileInternalAsync(string url, string filePath, KvFileType fileType, IProgressContext ctx)
    {
        KvsProviderError? error;

        var task = ctx.AddTask(Messages.Common.Dummy);

        var itemsGameFile = _filesAccessor.GetInfo(filePath);
        if (itemsGameFile.Exists)
        {
            task.Description(string.Format(Messages.Statuses.ValidatingCache, fileType));

            var size = itemsGameFile.Length;
            var getSizeResult = await GetFileSizeAsync(url);
            if (getSizeResult.TryPickT1(out error, out var remoteSize))
            {
                task.SetResult(string.Format(Messages.Statuses.DownloadingError, fileType));
                return error;
            }

            if (size == remoteSize)
            {
                task.SetResult(string.Format(Messages.Statuses.CacheIsUpToDate, fileType));
                return filePath;
            }

            task.Description(string.Format(Messages.Statuses.CacheIsOutdated, filePath));
        }
        else
        {
            task.Description(string.Format(Messages.Statuses.Downloading, fileType));
        }

        var getItemsGameResult = await GetFileAsync(url);
        if (getItemsGameResult.TryPickT1(out error, out var bytes))
        {
            task.SetResult(string.Format(Messages.Statuses.DownloadingError, fileType));
            return error;
        }

        using var stream = _filesAccessor.OpenStream(filePath, FileMode.Create, FileAccess.Write);
        stream.Write(bytes.Span);

        task.SetResult(string.Format(Messages.Statuses.DownloadingOk, fileType));
        return bytes;
    }

    private async Task<OneOf<long, KvsProviderError>> GetFileSizeAsync(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Head, url);
        var response = await _httpClient.SendAsync(request);
        var length = response.Content.Headers.ContentLength;

        if (FailIfNull(length, Messages.Http.CannotGetContentLength, out KvsProviderError? error))
        {
            return error;
        }

        return length.Value;
    }

    private async Task<OneOf<ReadOnlyMemory<byte>, KvsProviderError>> GetFileAsync(string url)
    {
        try
        {
            var response = await _httpClient.GetAsync(url);
            var bytes = await response.Content.ReadAsByteArrayAsync();
            return new ReadOnlyMemory<byte>(bytes);
        }
        catch (Exception exception)
        {
            return new KvsProviderError
            {
                Message = Messages.Http.Error,
                Exception = exception
            };
        }
    }
}
