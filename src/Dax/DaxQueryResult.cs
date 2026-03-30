namespace PbiRestProxy.Dax;

public sealed record DaxQueryResult(
    IReadOnlyList<DaxResultColumn> Columns,
    IReadOnlyList<IReadOnlyList<object?>> Rows,
    TimeSpan Elapsed)
{
    public int RowCount => Rows.Count;
}
