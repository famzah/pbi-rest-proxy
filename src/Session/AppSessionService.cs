using PbiRestProxy.Logging;

namespace PbiRestProxy.Session;

public sealed class AppSessionService
{
    private readonly LogStore logStore;
    private string? accessToken;

    public AppSessionService(LogStore logStore)
    {
        this.logStore = logStore;
    }

    public event Action? StateChanged;

    public AppSessionState State { get; private set; } = AppSessionState.Empty;

    public string? CurrentAccessToken => accessToken;

    public void SetAccessToken(string tokenInput, AccessTokenSource source)
    {
        var normalizedToken = AccessTokenParser.Normalize(tokenInput);
        var parsedToken = AccessTokenParser.Parse(normalizedToken);

        accessToken = normalizedToken;
        State = State with
        {
            TokenSource = source,
            AccessToken = parsedToken
        };

        logStore.WriteInfo(
            "Auth",
            $"{FormatTokenSource(source)} access token loaded for {parsedToken.DisplayUser}. Expires {parsedToken.ExpiresAtLocal:yyyy-MM-dd HH:mm:ss zzz}.");

        if (parsedToken.IsExpired)
        {
            logStore.WriteWarning("Auth", "The loaded access token is already expired.");
        }

        StateChanged?.Invoke();
    }

    public void ClearAccessToken()
    {
        if (accessToken is null && State.AccessToken is null)
        {
            return;
        }

        accessToken = null;
        State = State with
        {
            TokenSource = null,
            AccessToken = null
        };

        logStore.WriteInfo("Auth", "Access token cleared from the current session.");
        StateChanged?.Invoke();
    }

    private static string FormatTokenSource(AccessTokenSource source)
    {
        return source switch
        {
            AccessTokenSource.AzureCli => "Azure CLI",
            AccessTokenSource.Manual => "Manual",
            _ => source.ToString()
        };
    }
}
