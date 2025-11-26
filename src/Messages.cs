namespace XpGetter;

public static class Messages
{
    public static class Start
    {
        public const string Hello = $"Hello! XpGetter {Constants.Version}";
        public const string NoAccounts = "No accounts found.";
        public const string SavedAccounts = "Saved accounts: [[{0}]]";
    }

    public static class Session
    {
        public const string FailedSessionCreation = "[red]Error while creating session for client '{0}'[/]";
    }

    public static class Authentication
    {
        public const string CannotCreateSteamSession = "Cannot create steam session. Check your internet connection.";
        public const string SessionExpired = "[yellow]Session for '{0}' is expired. You need to log in into account again.[/]";
        public const string AuthenticationError = "[red]An error occurred while logging in into account '{0}'[/]";
        public const string AccountRemoved = "Account '{0}' removed because its session is expired.";
        public const string NoSavedAccounts = "You need to log in again.";
        public const string UnauthenticatedSessions = "There're unauthenticated, not expired sessions for some reason.";
        public const string SuccessfullyAuthenticated = "Successfully authenticated sessions: {@SessionNames}";
        public const string InvalidPassword = "Invalid password. Try again.";
        public const string Cancelled = "[yellow]It looks like you cancelled the authentication in the Steam mobile app. Try again.[/]";
    }

    public static class Activity
    {
        public const string GetActivityError = "[red]An error occurred while retrieving activity info.[/]";
    }

    public static class ManageAccounts
    {
        public const string AccountWasNotFound
            = "[red]Account with username '{0}' was not found in configuration. (This should be technically impossible — please report a bug.)[/]";

        public const string AccountRemoved = "Account '{0}' was removed.";
    }

    public static class AddAccount
    {
        public const string AddingAccountViaQr = "Adding new account via QR...";
        public const string SuccessfullyAdded = "Account '{0}' was successfully added.";
        public const string AddingAccountViaPassword = "Adding new account via username/password...";
        public const string AccountAlreadyExists = "Account '{0}' already exists.";
    }

    public static class Common
    {
        public const string FatalError =
            $"[red]Some errors occurred. Read their descriptions to understand what happened. The program cannot continue execution. Check your internet connection or report a bug to the developer. Also, see the [link={Constants.GitHubPageUrl}]GitHub page[/] for similar issues or the FAQ.[/]";
    }
}
