namespace PbiRestProxy.Rest;

public sealed record ExecuteDaxResponse(
    string SemanticModel,
    string? Workspace,
    double ElapsedMs,
    int RowCount,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<object?>> Rows);
