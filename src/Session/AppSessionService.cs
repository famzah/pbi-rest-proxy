using PbiRestProxy.Discovery;
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
            AccessToken = parsedToken,
            SelectedWorkspace = null,
            SelectedSemanticModel = null,
            XmlaEndpoint = null
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
            AccessToken = null,
            SelectedWorkspace = null,
            SelectedSemanticModel = null,
            XmlaEndpoint = null
        };

        logStore.WriteInfo("Auth", "Access token cleared from the current session.");
        StateChanged?.Invoke();
    }

    public void SetSelectedWorkspace(WorkspaceSummary? workspace)
    {
        if (State.SelectedWorkspace?.Id == workspace?.Id)
        {
            return;
        }

        State = State with
        {
            SelectedWorkspace = workspace,
            SelectedSemanticModel = null,
            XmlaEndpoint = null
        };

        if (workspace is null)
        {
            logStore.WriteInfo("Discovery", "Workspace selection cleared.");
        }
        else
        {
            logStore.WriteInfo("Discovery", $"Selected workspace '{workspace.Name}'.");
        }

        StateChanged?.Invoke();
    }

    public void SetSelectedSemanticModel(SemanticModelSummary? semanticModel)
    {
        if (semanticModel is not null && State.SelectedWorkspace is null)
        {
            throw new InvalidOperationException("A workspace must be selected before choosing a semantic model.");
        }

        if (State.SelectedSemanticModel?.Id == semanticModel?.Id)
        {
            return;
        }

        State = State with
        {
            SelectedSemanticModel = semanticModel,
            XmlaEndpoint = null
        };

        if (semanticModel is null)
        {
            logStore.WriteInfo("Discovery", "Semantic model selection cleared.");
        }
        else
        {
            logStore.WriteInfo("Discovery", $"Selected semantic model '{semanticModel.Name}'.");
        }

        StateChanged?.Invoke();
    }

    public void ClearSelection()
    {
        if (State.SelectedWorkspace is null && State.SelectedSemanticModel is null && State.XmlaEndpoint is null)
        {
            return;
        }

        State = State with
        {
            SelectedWorkspace = null,
            SelectedSemanticModel = null,
            XmlaEndpoint = null
        };

        logStore.WriteInfo("Discovery", "Workspace and semantic model selections cleared.");
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
