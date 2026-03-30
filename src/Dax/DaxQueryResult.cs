using System.Data;

namespace PbiRestProxy.Dax;

public sealed record DaxQueryResult(
    DataTable Table,
    TimeSpan Elapsed)
{
    public int RowCount => Table.Rows.Count;
}
