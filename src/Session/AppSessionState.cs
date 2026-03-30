using PbiRestProxy.Discovery;

namespace PbiRestProxy.Session;

public sealed record AppSessionState(
    AccessTokenSource? TokenSource,
    ParsedAccessToken? AccessToken,
    WorkspaceSummary? SelectedWorkspace,
    SemanticModelSummary? SelectedSemanticModel,
    string? XmlaEndpoint)
{
    public static AppSessionState Empty { get; } = new(
        TokenSource: null,
        AccessToken: null,
        SelectedWorkspace: null,
        SelectedSemanticModel: null,
        XmlaEndpoint: null);

    public string? SelectedWorkspaceName => SelectedWorkspace?.Name;

    public string? SelectedSemanticModelName => SelectedSemanticModel?.Name;

    public bool HasAccessToken => AccessToken is not null;

    public bool HasUsableAccessToken => AccessToken is { IsExpired: false };
}
