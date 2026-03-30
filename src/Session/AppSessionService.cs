using PbiRestProxy.Connection;
using PbiRestProxy.Discovery;
using PbiRestProxy.Logging;

namespace PbiRestProxy.Session;

public sealed class AppSessionService
{
    private readonly LogStore logStore;
    private readonly object syncRoot = new();
    private string? accessToken;

    public AppSessionService(LogStore logStore)
    {
        this.logStore = logStore;
    }

    public event Action? StateChanged;

    public AppSessionState State
    {
        get
        {
            lock (syncRoot)
            {
                return state;
            }
        }
        private set
        {
            lock (syncRoot)
            {
                state = value;
            }
        }
    }

    private AppSessionState state = AppSessionState.Empty;

    public string? CurrentAccessToken
    {
        get
        {
            lock (syncRoot)
            {
                return accessToken;
            }
        }
    }

    public void SetAccessToken(string tokenInput, AccessTokenSource source)
    {
        var normalizedToken = AccessTokenParser.Normalize(tokenInput);
        var parsedToken = AccessTokenParser.Parse(normalizedToken);

        AppSessionState updatedState;

        lock (syncRoot)
        {
            accessToken = normalizedToken;
            updatedState = state with
            {
                TokenSource = source,
                AccessToken = parsedToken,
                SelectedWorkspace = null,
                SelectedSemanticModel = null,
                ConnectedWorkspace = null,
                ConnectedSemanticModel = null,
                XmlaEndpoint = null
            };
            state = updatedState;
        }

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
        lock (syncRoot)
        {
            if (accessToken is null && state.AccessToken is null)
            {
                return;
            }

            accessToken = null;
            state = state with
            {
                TokenSource = null,
                AccessToken = null,
                SelectedWorkspace = null,
                SelectedSemanticModel = null,
                ConnectedWorkspace = null,
                ConnectedSemanticModel = null,
                XmlaEndpoint = null
            };
        }

        logStore.WriteInfo("Auth", "Access token cleared from the current session.");
        StateChanged?.Invoke();
    }

    public void SetSelectedWorkspace(WorkspaceSummary? workspace)
    {
        lock (syncRoot)
        {
            if (state.SelectedWorkspace?.Id == workspace?.Id)
            {
                return;
            }

            state = state with
            {
                SelectedWorkspace = workspace,
                SelectedSemanticModel = null,
                ConnectedWorkspace = null,
                ConnectedSemanticModel = null,
                XmlaEndpoint = null
            };
        }

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
        lock (syncRoot)
        {
            if (semanticModel is not null && state.SelectedWorkspace is null)
            {
                throw new InvalidOperationException("A workspace must be selected before choosing a semantic model.");
            }

            if (state.SelectedSemanticModel?.Id == semanticModel?.Id)
            {
                return;
            }

            state = state with
            {
                SelectedSemanticModel = semanticModel,
                ConnectedWorkspace = null,
                ConnectedSemanticModel = null,
                XmlaEndpoint = null
            };
        }

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

    public void ConnectSelection()
    {
        WorkspaceSummary selectedWorkspace;
        SemanticModelSummary selectedSemanticModel;
        string xmlaEndpoint;

        lock (syncRoot)
        {
            if (state.SelectedWorkspace is null || state.SelectedSemanticModel is null)
            {
                throw new InvalidOperationException("Select a workspace and semantic model before connecting.");
            }

            selectedWorkspace = state.SelectedWorkspace;
            selectedSemanticModel = state.SelectedSemanticModel;
            xmlaEndpoint = PowerBiXmlaEndpointFactory.BuildWorkspaceEndpoint(selectedWorkspace);

            state = state with
            {
                ConnectedWorkspace = selectedWorkspace,
                ConnectedSemanticModel = selectedSemanticModel,
                XmlaEndpoint = xmlaEndpoint
            };
        }

        logStore.WriteInfo(
            "Connection",
            $"Connected target set to workspace '{selectedWorkspace.Name}', semantic model '{selectedSemanticModel.Name}', XMLA endpoint '{xmlaEndpoint}'.");
        StateChanged?.Invoke();
    }

    public void ClearSelection()
    {
        lock (syncRoot)
        {
            if (state.SelectedWorkspace is null &&
                state.SelectedSemanticModel is null &&
                state.ConnectedWorkspace is null &&
                state.ConnectedSemanticModel is null &&
                state.XmlaEndpoint is null)
            {
                return;
            }

            state = state with
            {
                SelectedWorkspace = null,
                SelectedSemanticModel = null,
                ConnectedWorkspace = null,
                ConnectedSemanticModel = null,
                XmlaEndpoint = null
            };
        }

        logStore.WriteInfo("Discovery", "Workspace selection, semantic model selection, and connected target cleared.");
        StateChanged?.Invoke();
    }

    public void Disconnect()
    {
        lock (syncRoot)
        {
            if (state.ConnectedWorkspace is null && state.ConnectedSemanticModel is null && state.XmlaEndpoint is null)
            {
                return;
            }

            state = state with
            {
                ConnectedWorkspace = null,
                ConnectedSemanticModel = null,
                XmlaEndpoint = null
            };
        }

        logStore.WriteInfo("Connection", "Disconnected the current XMLA target.");
        StateChanged?.Invoke();
    }

    public bool TryGetConnectedQueryContext(out ConnectedQueryContext? context, out string? failureMessage)
    {
        lock (syncRoot)
        {
            if (accessToken is null || state.AccessToken is null)
            {
                context = null;
                failureMessage = "Load an access token before executing DAX.";
                return false;
            }

            if (state.AccessToken.IsExpired)
            {
                context = null;
                failureMessage = "The current access token is expired. Refresh it before executing DAX.";
                return false;
            }

            if (state.XmlaEndpoint is null || state.ConnectedSemanticModelName is null)
            {
                context = null;
                failureMessage = "Connect to a semantic model before executing DAX.";
                return false;
            }

            context = new ConnectedQueryContext(
                accessToken,
                state.AccessToken,
                state.XmlaEndpoint,
                state.ConnectedSemanticModelName,
                state.ConnectedWorkspaceName);
            failureMessage = null;
            return true;
        }
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
