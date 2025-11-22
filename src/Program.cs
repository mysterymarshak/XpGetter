using Spectre.Console;
using System.Text;
using Autofac;
using OneOf;
using Serilog;
using XpGetter;
using XpGetter.Results;
using XpGetter.Steam;
using XpGetter.Utils;

var containerBuilder = new ContainerBuilder();
containerBuilder.RegisterModule<MainModule>();
var container = containerBuilder.Build();

// TODO: refactor that mess

var authorizationService = container.Resolve<IAuthorizationService>();
var activityService = container.Resolve<IActivityService>();
var settingsProvider = container.Resolve<SettingsProvider>();
var logger = container.Resolve<ILogger>();
var settings = settingsProvider.Import();

if (args.Contains("--add-account"))
{
    await ShowMenu();
}

await SyncAccounts();

while (!settings.Accounts.Any())
{
    Console.WriteLine("No accounts found.");
    await ShowMenu();
}

await PrintInfo();

async Task ShowMenu()
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
        Console.WriteLine("Some of authorization fields is empty. Aborting..");
        return;
    }

    var authorizationResult = await authorizationService.AuthorizeByUsernameAndPasswordAsync(username, password);
    TryExtractAccount(authorizationResult);
}

async Task AddNewAccountQr()
{
    Console.WriteLine("Adding new account via QR...");
    var authorizationResult = await authorizationService.AuthorizeByQrCodeAsync();
    TryExtractAccountFromQrAuth(authorizationResult);
}

void TryExtractAccount(OneOf<Account, SessionServiceError, AuthorizationServiceError> authorizationResult)
{
    if (authorizationResult.TryPickT0(out var account, out _))
    {
        settingsProvider.AddAccount(account, settings);
        settingsProvider.Sync(settings);
    } 
    else if (authorizationResult.TryPickT1(out var sessionServiceError, out _))
    {
        Console.WriteLine("An error occured while adding new account.");
        Console.WriteLine(sessionServiceError.Message);
        if (sessionServiceError.Exception is not null)
        {
            Console.WriteLine(sessionServiceError.Exception.ToString());
        }
    }
    else
    {
        var authServiceError = authorizationResult.AsT2;
        Console.WriteLine("An error occured while adding new account.");
        Console.WriteLine(authServiceError.Message);
        if (authServiceError.Exception is not null)
        {
            Console.WriteLine(authServiceError.Exception.ToString());
        }
    }

    // TODO: improve
}

void TryExtractAccountFromQrAuth(
    OneOf<Account, UserCancelledAuthByQr, SessionServiceError, AuthorizationServiceError> authorizationResult)
{
    if (authorizationResult.IsT1)
    {
        Console.WriteLine("You have probably cancelled auth by QR in steam app. Try again.");
        return;
    }

    var loweredResult = authorizationResult.Match(
        OneOf<Account, SessionServiceError, AuthorizationServiceError>.FromT0,
        _ => OneOf<Account, SessionServiceError, AuthorizationServiceError>.FromT2(
            new AuthorizationServiceError
            {
                Message = $"Impossible case. {nameof(TryExtractAccountFromQrAuth)}()"
            }),
        OneOf<Account, SessionServiceError, AuthorizationServiceError>.FromT1,
        OneOf<Account, SessionServiceError, AuthorizationServiceError>.FromT2);
    
    TryExtractAccount(loweredResult);
}

async Task SyncAccounts()
{
    var accounts = settings.Accounts.ToList();
    if (accounts.Count == 0)
        return;

    var tasks = accounts.Select(authorizationService.AuthorizeAccountAsync);

    var results = await Task.WhenAll(tasks);
    foreach (var (i, result) in results.Index())
    {
        var account = accounts[i];

        if (result.TryPickT2(out _, out _))
        {
            settingsProvider.RemoveAccount(account, settings);
            logger.Warning("Session for '{Username}' is expired. You need to log in into account again.", account.Username);
        }
        else if (result.TryPickT3(out var sessionServiceError, out _))
        {
            Console.WriteLine($"An error occured while logging in into account '{account.Username}'.");
            Console.WriteLine(sessionServiceError.Message);
            if (sessionServiceError.Exception is not null)
            {
                Console.WriteLine(sessionServiceError.Exception.ToString());
            }
        }
        else if (result.TryPickT4(out var authServiceError, out _))
        {
            Console.WriteLine($"An error occured while logging in into account '{account.Username}'.");
            Console.WriteLine(authServiceError.Message);
            if (authServiceError.Exception is not null)
            {
                Console.WriteLine(authServiceError.Exception.ToString());
            }
        }

        // TODO: improve
    }

    // TODO: properly handle state when error happend; execution shouldnt go further
    // if there're some internet troubles/ServiceUnavailable retcode it should
    // try fetch info a few times again
    settingsProvider.Sync(settings);
}

async Task PrintInfo()
{
    var accounts = settings.Accounts;
    var tasks = accounts.Select(activityService.GetActivityInfoAsync);

    var results = await Task.WhenAll(tasks);
    foreach (var (i, result) in results.Index())
    {
        if (result.TryPickT0(out var info, out var error))
        {
            var isDropAvailable = info.IsDropAvailable();
            var isDropAvailableFormatted = isDropAvailable is null ? "<unknown>" : (isDropAvailable.Value ? "Yes" : "No");
            var dropAvailableColor = isDropAvailable is null ? "yellow" : (isDropAvailable.Value ? "green" : "red");
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
        }
        else
        {
            Console.WriteLine("An error occured while retrieving activity info.");
            Console.WriteLine(error.Message);
        }

        if (i != results.Length - 1)
        {
            Console.WriteLine();
        }
    }
}
