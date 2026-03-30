using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using PbiRestProxy.Logging;

namespace PbiRestProxy.Discovery;

public sealed class PowerBiDiscoveryService
{
    private static readonly Uri PowerBiApiBaseAddress = new("https://api.powerbi.com/v1.0/myorg/");

    private readonly HttpClient httpClient;
    private readonly LogStore logStore;

    public PowerBiDiscoveryService(LogStore logStore)
    {
        this.logStore = logStore;
        httpClient = new HttpClient
        {
            BaseAddress = PowerBiApiBaseAddress
        };
    }

    public async Task<IReadOnlyList<WorkspaceSummary>> LoadWorkspacesAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        logStore.WriteInfo("Discovery", "Loading accessible Power BI workspaces.");

        using var response = await SendGetAsync("groups", accessToken, cancellationToken);
        var payload = await ReadRequiredJsonAsync<WorkspaceListResponse>(response, cancellationToken);

        var workspaces = (payload.Value ?? [])
            .Where(workspace => !string.IsNullOrWhiteSpace(workspace.Id) && !string.IsNullOrWhiteSpace(workspace.Name))
            .Select(workspace => new WorkspaceSummary(
                workspace.Id,
                workspace.Name,
                workspace.IsOnDedicatedCapacity))
            .OrderBy(workspace => workspace.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        logStore.WriteInfo("Discovery", $"Loaded {workspaces.Length} workspace(s).");
        return workspaces;
    }

    public async Task<IReadOnlyList<SemanticModelSummary>> LoadSemanticModelsAsync(string accessToken, WorkspaceSummary workspace, CancellationToken cancellationToken = default)
    {
        logStore.WriteInfo("Discovery", $"Loading semantic models for workspace '{workspace.Name}'.");

        using var response = await SendGetAsync($"groups/{Uri.EscapeDataString(workspace.Id)}/datasets", accessToken, cancellationToken);
        var payload = await ReadRequiredJsonAsync<SemanticModelListResponse>(response, cancellationToken);

        var semanticModels = (payload.Value ?? [])
            .Where(model => !string.IsNullOrWhiteSpace(model.Id) && !string.IsNullOrWhiteSpace(model.Name))
            .Select(model => new SemanticModelSummary(
                model.Id,
                model.Name,
                model.ConfiguredBy,
                model.IsRefreshable))
            .OrderBy(model => model.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        logStore.WriteInfo("Discovery", $"Loaded {semanticModels.Length} semantic model(s) for workspace '{workspace.Name}'.");
        return semanticModels;
    }

    private async Task<HttpResponseMessage> SendGetAsync(string relativePath, string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, relativePath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var failureMessage = BuildFailureMessage(relativePath, response.StatusCode, body);

        logStore.WriteError("Discovery", failureMessage);
        response.Dispose();
        throw new InvalidOperationException(failureMessage);
    }

    private static async Task<T> ReadRequiredJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var payload = await response.Content.ReadFromJsonAsync<T>(cancellationToken);

        return payload ?? throw new InvalidOperationException("The Power BI API returned an empty JSON payload.");
    }

    private static string BuildFailureMessage(string relativePath, HttpStatusCode statusCode, string responseBody)
    {
        var detail = string.IsNullOrWhiteSpace(responseBody)
            ? "No response body was returned."
            : $"Response body: {responseBody.Trim()}";

        return $"Power BI discovery request to '{relativePath}' failed with HTTP {(int)statusCode} ({statusCode}). {detail}";
    }

    private sealed record WorkspaceListResponse(
        [property: JsonPropertyName("value")]
        WorkspaceResponseItem[]? Value);

    private sealed record WorkspaceResponseItem(
        [property: JsonPropertyName("id")]
        string Id,
        [property: JsonPropertyName("name")]
        string Name,
        [property: JsonPropertyName("isOnDedicatedCapacity")]
        bool IsOnDedicatedCapacity);

    private sealed record SemanticModelListResponse(
        [property: JsonPropertyName("value")]
        SemanticModelResponseItem[]? Value);

    private sealed record SemanticModelResponseItem(
        [property: JsonPropertyName("id")]
        string Id,
        [property: JsonPropertyName("name")]
        string Name,
        [property: JsonPropertyName("configuredBy")]
        string? ConfiguredBy,
        [property: JsonPropertyName("isRefreshable")]
        bool? IsRefreshable);
}
