using PbiRestProxy.Session;

namespace PbiRestProxy.Dax;

public sealed record DaxResultPayload(
    string SemanticModel,
    string? Workspace,
    string XmlaEndpoint,
    double ElapsedMs,
    bool IsTruncated,
    int RowLimit,
    IReadOnlyList<DaxResultColumn> Columns,
    IReadOnlyList<IReadOnlyList<object?>> Rows)
{
    public int RowCount => Rows.Count;

    public int ColumnCount => Columns.Count;

    public static DaxResultPayload Create(ConnectedQueryContext queryContext, DaxQueryResult result)
    {
        return new DaxResultPayload(
            queryContext.SemanticModelName,
            queryContext.WorkspaceName,
            queryContext.XmlaEndpoint,
            result.Elapsed.TotalMilliseconds,
            result.IsTruncated,
            result.RowLimit,
            result.Columns,
            result.Rows);
    }
}
