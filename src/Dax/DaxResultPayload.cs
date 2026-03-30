using PbiRestProxy.Session;

namespace PbiRestProxy.Dax;

public sealed record DaxResultPayload(
    string SemanticModel,
    string? Workspace,
    string XmlaEndpoint,
    double ElapsedMs,
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
            result.Columns,
            result.Rows);
    }
}
