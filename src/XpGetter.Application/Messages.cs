namespace XpGetter.Application;

public static class Messages
{
    public static class Configuration
    {
        public const string IsInvalid = "Configuration file is invalid. Replacing it with the default one.";
    }

    public static class Start
    {
        public const string HelloLog = $"{Constants.ProgramName} {Constants.Version}: Started.";
        public const string Hello = $"[deepskyblue3]Hello! XpGetter {Constants.Version}[/]";
        public const string NoAccounts = "No accounts found.";
        public const string SavedAccountFormat = "[steelblue1]{0}[/]";
        public const string SavedAccounts = "[green]Saved accounts: [[{0}]][/]";
        public const string GetActivityInfo = "Get activity info";
        public const string ManageAccounts = "Manage accounts";
        public const string CheckForUpdates = "Check for updates";
        public const string Exited = "Exited.";
    }

    public static class Version
    {
        public const string GetException = "An exception was thrown while retrieving latest version.";
        public const string Error = "[red]Couldn't retrieve the latest version. Check the logs.[/]";
        public const string Update = "There's a new version '[green]{0}[/]'! Go to [link={{Constants.GitHubReleasesPageUrl}}]GitHub Releases[/] to download it.";
        public const string Newer = "It looks like you're on a dev ver! Nothing to do.";
        public const string NoUpdates = $"{Constants.ProgramName} {Constants.Version} is up to date!";
    }

    public static class ManageAccounts
    {
        public const string AddNew = "Add new";
        public const string AccountWasNotFound
            = "[red]Account with username '{0}' was not found in configuration. (This should be technically impossible — please report a bug.)[/]";

        public const string AccountRemoved = "Account '{0}' was removed.";
        public const string Remove = "Remove";
        public const string AccountFormat = "{0}:";
    }

    public static class AddAccount
    {
        public const string LogInWay = "Choice log in way:";
        public const string ViaPassword = "Username/password";
        public const string ViaQrCode = "QR-code";
        public const string AddingAccountViaQr = "Adding new account via QR...";
        public const string SuccessfullyAdded = "[green]Account '{0}' was successfully added.[/]";
        public const string TypeUsername = "Username:";
        public const string TypePassword = "Password:";
        public const string AddingAccountViaPassword = "Adding new account via username/password...";
        public const string AccountAlreadyExists = "[red]Account '{0}' already exists.[/]";
    }

    public static class Statuses
    {
        public const string CreatingSession = "Creating session...";
        public const string SessionCreationError = "Session creation error [red]:([/]";
        public const string Authenticating = "Authenticating...";
        public const string Authenticated = "Authenticated [green]:)[/]";
        public const string RetrievingActivity = "Retrieving activity info...";
        public const string RetrievingLastNewRankDrop = "Retrieving last new rank drop...";
        public const string RetrievingXpAndRank = "Retrieving xp and rank...";
        public const string RetrievingXpAndRankError = "Retrieving xp and rank: error [red]:([/]";
        public const string RetrievingXpAndRankOk = "Retrieving xp and rank: got! [green]:)[/]";
        public const string RetrievingActivityError = "Retrieving activity info: error [red]:([/]";
        public const string RetrievingActivityOk = "Retrieving activity info: got! [green]:)[/]";
        public const string TooLongHistoryStatus = "New rank drop: too long inventory history! [red]:([/]";
        public const string NewRankDropError = "New rank drop: error [red]:([/]";
        public const string NewRankDropOk = "New rank drop: got! [green]:)[/]";
        public const string NewRankDropNoResultsOnPage = "New rank drop: fetching more history...";
        public const string NewRankDropGotOnlyOne = "New rank drop: got with warnings [yellow]-_-;[/]";
        public const string NewRankDropMispaged = "New rank drop: mispaged. You're lucky!";
        public const string NewRankDropNotFound = "New rank drop: not found. Are you new in cs2? [green]:)[/]";
        public const string RetrievingWalletInfo = "Retrieving wallet info...";
        public const string RetrievingWalletInfoError = "Retrieving wallet info: error [red]:([/]";
        public const string RetrievingWalletInfoOk = "Retrieving wallet info: got! [green]:)[/]";
        public const string RetrievingItemsPriceNotStarted = "Retrieving items price: awaiting wallet info & items...";
        public const string RetrievingItemsPrice = "Retrieving items price...";
        public const string RetrievingItemsPriceError = "Retrieving items price: error [red]:([/]";
        public const string RetrievingItemsPriceOk = "Retrieving items price: got! [green]:)[/]";
        public const string RetrievingLatestVersion = "Retrieving lastest version...";
    }

    public static class Session
    {
        public const string ClientNameFormat = "Client '{0}'";
        public const string BoundedSessionLogFormat = $"{ClientNameFormat}: {{1}}";
        public const string Connecting = "Connecting to Steam...";
        public const string ConnectingException = "An exception was thrown while connecting to Steam.";
        public const string Reconnect = "Trying to reconnect ({0})";
        public const string TooManyRetryAttempts = "Cannot connect to steam.";
        public const string Connected = "Connected to Steam.";
        public const string Disconnected = "Disconnected from Steam.";
        public const string UnauthenticatedAccountBind = "Attempt to bind unauthenticated account.";
        public const string FailedSessionCreation = "[red]Error while creating session for client '{0}'[/]";
    }

    public static class Http
    {
        public const string Error = "[red]Http client error.[/]";
        public const string HtmlError = "[red]Http client/invalid html error.[/]";
        public const string DeserializationError = "Cannot deserialize json. See details in logs.";
        public const string DeserializationErrorLog = "Cannot deserialize json. Raw: {Json}";
    }

    public static class Authentication
    {
        public const string Authenticating = "Authenticating...";
        public const string Authenticated = "Authenticated [green]:)[/]";
        public const string LoggingIn = "Logging in...";
        public const string LoggingInPassword = "Logging in via password...";
        public const string AuthenticateViaTokenException = "An exception was thrown while trying to authenticate session via token.";
        public const string AuthenticateViaPasswordException = "An exception was thrown while trying to authenticate session via password.";
        public const string AuthenticateViaQrException = "An exception was thrown while trying to authenticate session via qr code.";
        public const string LogOnNotOk = "Unable to logon to Steam: {0} / {1}";
        public const string LogOnNotOkTimeout = "Unable to logon to Steam (possible timeout): {0} / {1}";
        public const string LogOnOk = "Successfully logged on.";
        public const string QrCodeRefreshed = "Steam has refreshed the qr code.";
        public const string RefreshTokenExpirationDate = "Refresh token expiration date: {0}";
        public const string RefreshTokenExpired = "Refresh token is expired.";
        public const string AccessTokenExpirationDate = "Access token expiration date: {0}";
        public const string AccessTokenExpired = "Access token is expired.";
        public const string AccessTokenRenewed = "Access token has successfully renewed.";
        public const string AccessRenewingException = "An exception was thrown while renewing access token.";
        public const string CannotCreateSteamSession = "[red]Cannot create steam session. Check your internet connection.[/]";
        public const string SessionExpired = "[yellow]Session for '{0}' is expired. You need to log in into account again.[/]";
        public const string SessionExpiredStatus = "Session expired [red]:([/]";
        public const string AuthenticationError = "[red]An error occurred while logging in into account '{0}'[/]";
        public const string AuthenticationErrorStatus = "Authentication error [red]:([/]";
        public const string AccountRemoved = "[yellow]Account '{0}' removed because it's session is expired.[/]";
        public const string NoSavedAccounts = "[yellow]You need to log in again.[/]";
        public const string UnauthenticatedSessions = "[red]There're unauthenticated, not expired sessions for some reason.[/]";
        public const string SuccessfullyAuthenticated = "Successfully authenticated sessions: {@SessionNames}";
        public const string InvalidPassword = "[red]Invalid password. Try again.[/]";
        public const string Cancelled = "[red]It looks like you cancelled the authentication in the Steam mobile app. Try again.[/]";
        public const string InvalidJwtToken = "Invalid jwt token is provided.";
    }

    public static class ActivityParsers
    {
        public static class Activity
        {
            public const string NoDataTables = "No 'generic_kv_table' tables was found.";
        }

        public static class Drop
        {
            public const string EmptyHtml = "Empty html document is provided.";
            public const string NoHistoryRows = "Invalid html document: no 'tradehistoryrow' elements. See details in logs.";
            public const string NoHistoryRowsLogger = "Invalid html document: no 'tradehistoryrow' elements. Raw: {Html}";
            public const string CannotParseDateTimeEntry = "Cannot parse datetime. Html: {Html}";
            public const string CannotParseSecondItem = "Cannot parse second drop item. Html: {Html}";
            public const string EmptyMispagedDropHtml = "Cannot parse second mispaged drop item. Empty html.";
            public const string NoHistoryRowsForMispagedDrop = "Invalid mispaged drop html document: no 'tradehistoryrow' elements. See details in logs.";
            public const string NoHistoryRowsForMispagedDropLogger = "Invalid mispaged drop html document: no 'tradehistoryrow' elements. Raw: {Html}";
            public const string CannotParseMispagedDrop = "Cannot parse mispaged drop. See details in logs.";
            public const string CannotParseMispagedDropLogger = "Cannot parse mispaged drop. Html: {Html}";
        }
    }

    public static class Activity
    {
        public const string HttpError = "Http client error: {0}";
        public const string ActivityParserError = "Activity parser error: {0}";
        public const string NoNewRankDropInfo = "No new rank drop info were found. Are you new in cs2?";
        public const string SessionWithNoAccount = "Before requesting activity info session should be authorized and account should be bounded.";
        public const string TooLongHistory = "Too long inventory history! Scanned '{0}' items and nothing about new rank drops were found.";
        public const string GetActivityError = "[red]An error occurred while retrieving activity info.[/]";
        public const string NullCursorForMispagedDrop = "Cannot retrieve mispaged drop if cursor is null.";
        public const string NotSuccessfulResultInLoadInventoryHistory = "Success: false while retrieving inventory history. See details in logs.";
        public const string NotSuccessfulResultInLoadInventoryHistoryLogger = "Success: false while retrieving inventory history. Raw response: {Response}";
        public const string EnterToReturn = "Press enter to return to main menu...";
    }

    public static class Wallet
    {
        public const string GetWalletInfoException = "An exception was thrown while retrieving wallet info.";
    }

    public static class Market
    {
        public const string DeserializationError = "Cannot deserialize item price response json. Raw: {0}";
        public const string GetPriceException = "An exception was thrown while retrieving the items price. Item names: [{0}]";
        public const string CannotFindItemForPrice = "Cannot find original item from the name provided in price response. Item to find: '{MarketName}'. All items: '{@MarketNames}'";
        public const string InvalidPriceRetrieved = "Invalid price for '{MarketName}' is retrieved. Used provider: {PriceProvider}. If there's no warning about using steam market instead of csgo market, then the second one service was used (as default).";
        public const string FallbackServiceUsedSteam = "Couldn't get items [{@ItemNames}] price via csgo market service. Trying to use steam one.";
        public const string GotPricesLog = "Got items' prices: {@Prices}";
    }

    public static class Common
    {
        public const string Dummy = "dummy";
        public const string ChoiceOption = "Choice option:";
        public const string Back = "Back";
        public const string Exit = "Exit";
        public const string ImpossibleMethodCase = "Impossible case. {0}()";
        public const string FatalError =
            $"[red]Some errors occurred. Read their descriptions to understand what happened. The program cannot continue execution. Check your internet connection or report a bug to the developer. Also, see the [link={Constants.GitHubPageUrl}]GitHub page[/] for similar issues or the FAQ.[/]";
        public const string Gap = "[slateblue3_1]===============[/]";
    }
}
