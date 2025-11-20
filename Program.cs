using Fizzler.Systems.HtmlAgilityPack;
using HtmlAgilityPack;
using Spectre.Console;
using System.Text;
using Autofac;
using OneOf;
using XpGetter;
using XpGetter.Steam;
using XpGetter.Utils;

var containerBuilder = new ContainerBuilder();
containerBuilder.RegisterModule<MainModule>();
var container = containerBuilder.Build();

var authorizationService = container.Resolve<IAuthorizationService>();
var activityService = container.Resolve<IActivityService>();
var settingsProvider = container.Resolve<SettingsProvider>();
var settings = settingsProvider.Import();

if (args.Contains("--add-account"))
{
    await ShowMenu();
}

while (!settings.Accounts.Any())
{
    Console.WriteLine("No accounts found.");
    await ShowMenu();
}

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
    TryExtractAccount(authorizationResult);
}

void TryExtractAccount(OneOf<Account, AuthorizationServiceError> authorizationResult)
{
    if (authorizationResult.TryPickT0(out var account, out _))
    {
        settingsProvider.AddAccount(account, settings);
        settingsProvider.Sync(settings);
    }
    else
    {
        var error = authorizationResult.AsT1;
        Console.WriteLine("An error occured while adding new account.");
        Console.WriteLine(error.Message);
        if (error.Exception is not null)
        {
            Console.WriteLine(error.Exception.ToString());
        }
    }
}

var accounts = settings.Accounts;
var tasks = accounts.Select(activityService.GetActivityInfo);

var results = await Task.WhenAll(tasks);
foreach (var (i, result) in results.Index())
{
    if (result.TryPickT0(out var info, out var error))
    {
        var isDropAvailable = info.IsDropAvailable();
        var isDropAvailableFormatted = isDropAvailable ? "Yes" : "No";
        AnsiConsole.MarkupLine($"[blue]{info.AccountName}[/]");
        AnsiConsole.MarkupLine($"Rank: {info.CsgoProfileRank}");
        AnsiConsole.MarkupLine($"Last drop: {info.LastDropDateTime}");
        AnsiConsole.MarkupLine($"Drop is available: [{(isDropAvailable ? "green" : "red")}]{isDropAvailableFormatted}[/]");
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
