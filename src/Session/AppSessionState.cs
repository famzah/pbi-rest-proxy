namespace PbiRestProxy.Session;

public sealed record AppSessionState(
    AccessTokenSource? TokenSource,
    ParsedAccessToken? AccessToken,
    string? SelectedWorkspaceName,
    string? SelectedSemanticModelName,
    string? XmlaEndpoint)
{
    public static AppSessionState Empty { get; } = new(
        TokenSource: null,
        AccessToken: null,
        SelectedWorkspaceName: null,
        SelectedSemanticModelName: null,
        XmlaEndpoint: null);

    public bool HasAccessToken => AccessToken is not null;

    public bool HasUsableAccessToken => AccessToken is { IsExpired: false };
}

