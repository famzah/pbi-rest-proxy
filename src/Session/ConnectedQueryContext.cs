namespace PbiRestProxy.Session;

public sealed record ConnectedQueryContext(
    string AccessToken,
    ParsedAccessToken ParsedAccessToken,
    string XmlaEndpoint,
    string SemanticModelName,
    string? WorkspaceName);
