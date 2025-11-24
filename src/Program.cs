using Spectre.Console;
using System.Text;
using Autofac;
using OneOf;
using OneOf.Types;
using Serilog;
using XpGetter;
using XpGetter.Results;
using XpGetter.Steam;
using XpGetter.Utils;
using XpGetter.Dto;
using XpGetter.Settings;
using XpGetter.Settings.Entities;
using XpGetter.Steam.Services;

var containerBuilder = new ContainerBuilder();
containerBuilder.RegisterModule<MainModule>();
var container = containerBuilder.Build();

// TODO: refactor that mess

var sessions = new Dictionary<Account, SteamSession>();
var sessionService = container.Resolve<ISessionService>();
var authenticationService = container.Resolve<IAuthenticationService>();
var activityService = container.Resolve<IActivityService>();
var settingsProvider = container.Resolve<SettingsProvider>();
var logger = container.Resolve<ILogger>();
var settings = settingsProvider.Import();

if (args.Contains("--add-account"))
{
    await ShowAddAccountMenu();
}

await SyncAccounts();

while (!settings.Accounts.Any())
{
    Console.WriteLine("No accounts found.");
    await ShowAddAccountMenu();
}

await PrintInfo();

async Task ShowAddAccountMenu()
{
    Console.WriteLine("[1] Add account by username/password");
    Console.WriteLine("[2] Add account by qr-code");
    Console.WriteLine("[3] Exit");
    Console.Write(">>>> ");

    var availableKeys = new List<ConsoleKey> { ConsoleKey.D1, ConsoleKey.D2, ConsoleKey.D3 };
    ConsoleKeyInfo keyInfo;
    do
    {
        keyInfo = Console.ReadKey(true);
    } while (!availableKeys.Contains(keyInfo.Key));

    Console.WriteLine(keyInfo.KeyChar);
    switch(keyInfo.Key)
    {
        case ConsoleKey.D1:
            await AddNewAccount();
            break;
        case ConsoleKey.D2:
            await AddNewAccountQr();
            break;
        case ConsoleKey.D3:
            Environment.Exit(0);
            break;
    }

    Console.WriteLine("==========");
}

async Task AddNewAccount()
{
    Console.WriteLine("Adding new account via username/password...");

    Console.Write("Type the username: ");
    var username = Console.ReadLine();

    var stringBuilder = new StringBuilder();
    Console.Write("Type the password: ");

    while (true)
    {
        var key = Console.ReadKey(true);
        if (key.Key == ConsoleKey.Enter)
        {
            break;
        }

        if (key.Key == ConsoleKey.Backspace && stringBuilder.Length > 0)
        {
            stringBuilder.Remove(stringBuilder.Length - 1, 1);
        }
        else
        {
            stringBuilder.Append(key.KeyChar);
        }
    }

    Console.WriteLine();

    var password = stringBuilder.ToString();

    if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
    {
        Console.WriteLine("Some of authentication fields is empty. Aborting..");
        return;
    }

    var createSessionResult = await CreateSessionAsync(username);
    if (createSessionResult.TryPickT1(out var error, out var session))
    {
        OnCreateSessionFailed(error);
        return;
    }

    var authenticationResult = await authenticationService.AuthenticateByUsernameAndPasswordAsync(session, username, password);
    TryExtractAccount(session, authenticationResult);
}

void OnCreateSessionFailed(SessionServiceError error)
{
    Console.WriteLine("Failed to create steam session.");
    Console.WriteLine(error.Message);
    if (error.Exception is not null)
    {
        Console.WriteLine(error.Exception.ToString());
    }
}

async Task AddNewAccountQr()
{
    Console.WriteLine("Adding new account via QR...");

    var createSessionResult = await CreateSessionAsync("<unnamed>");
    if (createSessionResult.TryPickT1(out var error, out var session))
    {
        OnCreateSessionFailed(error);
        return;
    }

    var authenticationResult = await authenticationService.AuthenticateByQrCodeAsync(session);
    TryExtractAccountFromQrAuth(session, authenticationResult);
}

void TryExtractAccount(SteamSession session, OneOf<Success, AuthenticationServiceError> authenticationResult)
{
    if (authenticationResult.TryPickT1(out var error, out _))
    {
        var authServiceError = authenticationResult.AsT1;
        Console.WriteLine("An error occured while adding new account.");
        Console.WriteLine(authServiceError.Message);
        if (authServiceError.Exception is not null)
        {
            Console.WriteLine(authServiceError.Exception.ToString());
        }

        return;
    }

    var account = session.Account!;
    settingsProvider.AddAccount(account, settings);
    settingsProvider.Sync(settings);
}

void TryExtractAccountFromQrAuth(
    SteamSession session, OneOf<Success, UserCancelledAuthByQr, AuthenticationServiceError> authenticationResult)
{
    if (authenticationResult.IsT1)
    {
        Console.WriteLine("You have probably cancelled auth by QR in steam app. Try again.");
        return;
    }

    var impossible = OneOf<Success, AuthenticationServiceError>.FromT1(new AuthenticationServiceError
        {
            Message = $"Impossible case. {nameof(TryExtractAccountFromQrAuth)}()"
        });
    var loweredResult = authenticationResult.Match(
        OneOf<Success, AuthenticationServiceError>.FromT0,
        _ => impossible,
        OneOf<Success, AuthenticationServiceError>.FromT1);

    TryExtractAccount(session, loweredResult);
}

async Task<OneOf<SteamSession, SessionServiceError>> GetOrCreateSessionAsync(Account account)
{
    if (sessions.TryGetValue(account, out var session))
    {
        return session;
    }

    return await CreateSessionAsync(account.Username, account);
}

async Task<OneOf<SteamSession, SessionServiceError>> CreateSessionAsync(string name, Account? account = null)
{
    var createSessionResult = await sessionService.CreateSessionAsync(name, account);
    if (createSessionResult.TryPickT0(out var session, out _) && account is not null)
    {
        sessions.Add(session.Account!, session);
    }

    return createSessionResult;
}

async Task SyncAccounts()
{
    // TODO: remove .Take(1) (for debug purposes)
    var accounts = settings.Accounts.Take(1).ToList();
    if (accounts.Count == 0)
        return;

    var createSessionTasks = accounts.Select(GetOrCreateSessionAsync);
    var createSessionResults = await Task.WhenAll(createSessionTasks);
    var errorResults = createSessionResults.Where(x => x.IsT1).ToList();

    foreach (var errorResult in errorResults)
    {
        var error = errorResult.AsT1;

        Console.WriteLine($"Error while creating session '{error.ClientName}'.");
        Console.WriteLine(error.Message);
        if (error.Exception is not null)
        {
            Console.WriteLine(error.Exception.ToString());
        }
    }

    if (errorResults.Count > 0)
        return;

    var sessions = createSessionResults.Select(x => x.AsT0).ToList();
    var authenticateSessionTasks = sessions.Select(authenticationService.AuthenticateSessionAsync);
    var authenticateSessionResults = await Task.WhenAll(authenticateSessionTasks);

    foreach (var (i, result) in authenticateSessionResults.Index())
    {
        var session = sessions[i];
        var account = session.Account!;

        if (result.TryPickT1(out _, out _))
        {
            settingsProvider.RemoveAccount(account, settings);
            logger.Warning("Session for '{Username}' is expired. You need to log in into account again.", account.Username);
        }
        else if (result.TryPickT2(out var authServiceError, out _))
        {
            Console.WriteLine($"An error occured while logging in into account '{account.Username}'.");
            Console.WriteLine(authServiceError.Message);
            if (authServiceError.Exception is not null)
            {
                Console.WriteLine(authServiceError.Exception.ToString());
            }
        }
    }

    settingsProvider.Sync(settings);
}

async Task PrintInfo()
{
    // TODO: remove .Take(1) (for debug purposes)
    var accounts = sessions
        .Values
        .Where(x => x.IsAuthenticated)
        .Select(x => x.Account!)
        .Take(1);
    var tasks = accounts.Select(activityService.GetActivityInfoAsync);

    var results = await Task.WhenAll(tasks);
    foreach (var (i, result) in results.Index())
    {
        if (result.TryPickT0(out var info, out var error))
        {
            var isDropAvailable = info.IsDropAvailable();
            var isDropAvailableFormatted = isDropAvailable is null ? "<unknown>" : (isDropAvailable.Value ? "Yes" : "No");
            var dropAvailableColor = isDropAvailable is null ? "white" : (isDropAvailable.Value ? "green" : "red");
            AnsiConsole.MarkupLine($"[blue]{info.Account.PersonalName}[/]");
            AnsiConsole.MarkupLine($"Rank: {info.CsgoProfileRank}");
            AnsiConsole.MarkupLine($"Last drop time: {info.GetLastDropTime()}");
            AnsiConsole.MarkupLine($"Last loot: {info.GetPreviousLoot()}");
            AnsiConsole.MarkupLine($"Drop is available: [{dropAvailableColor}]{isDropAvailableFormatted}[/]");
            if (info.AdditionalMessage is not null)
            {
                AnsiConsole.MarkupLine($"[yellow]{info.AdditionalMessage}[/]");
            }
            ProgressBar.Print(info.ExperiencePointsToNextRank, 5000);
            // TODO: implement formatting config
        }
        else
        {
            Console.WriteLine("An error occured while retrieving activity info.");
            Console.WriteLine(error.Message);
            if (error.Exception is not null)
            {
                Console.WriteLine(error.Exception.ToString());
            }
        }

        if (i != results.Length - 1)
        {
            Console.WriteLine();
        }
    }
}
