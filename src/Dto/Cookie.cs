namespace XpGetter.Dto;

public class AuthCookie
{
    public string Value => $"steamLoginSecure={_account.Id}||{_account.AccessToken}"; 
    
    private readonly AccountDto _account;
    
    public AuthCookie(AccountDto account)
    {
        _account = account;
    }
}