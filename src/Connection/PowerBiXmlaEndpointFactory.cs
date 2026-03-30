using PbiRestProxy.Discovery;

namespace PbiRestProxy.Connection;

public static class PowerBiXmlaEndpointFactory
{
    public static string BuildWorkspaceEndpoint(WorkspaceSummary workspace)
    {
        return $"powerbi://api.powerbi.com/v1.0/myorg/{Uri.EscapeDataString(workspace.Name)}";
    }
}
