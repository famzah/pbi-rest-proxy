using PbiRestProxy.Discovery;

namespace PbiRestProxy.Session;

public sealed record AppSessionState(
    AccessTokenSource? TokenSource,
    ParsedAccessToken? AccessToken,
    WorkspaceSummary? SelectedWorkspace,
    SemanticModelSummary? SelectedSemanticModel,
    WorkspaceSummary? ConnectedWorkspace,
    SemanticModelSummary? ConnectedSemanticModel,
    string? XmlaEndpoint)
{
    public static AppSessionState Empty { get; } = new(
        TokenSource: null,
        AccessToken: null,
        SelectedWorkspace: null,
        SelectedSemanticModel: null,
        ConnectedWorkspace: null,
        ConnectedSemanticModel: null,
        XmlaEndpoint: null);

    public string? SelectedWorkspaceName => SelectedWorkspace?.Name;

    public string? SelectedSemanticModelName => SelectedSemanticModel?.Name;

    public string? ConnectedWorkspaceName => ConnectedWorkspace?.Name;

    public string? ConnectedSemanticModelName => ConnectedSemanticModel?.Name;

    public bool HasAccessToken => AccessToken is not null;

    public bool HasUsableAccessToken => AccessToken is { IsExpired: false };

    public bool HasConnectedTarget => ConnectedWorkspace is not null && ConnectedSemanticModel is not null && !string.IsNullOrWhiteSpace(XmlaEndpoint);
}
