using System.Data;
using System.Diagnostics;
using Microsoft.AnalysisServices;
using Microsoft.AnalysisServices.AdomdClient;
using PbiRestProxy.Logging;
using PbiRestProxy.Session;

namespace PbiRestProxy.Dax;

public sealed class AdomdDaxQueryService
{
    private readonly LogStore logStore;

    public AdomdDaxQueryService(LogStore logStore)
    {
        this.logStore = logStore;
    }

    public DaxQueryResult Execute(
        string accessToken,
        ParsedAccessToken parsedAccessToken,
        string xmlaEndpoint,
        string initialCatalog,
        string query)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("A Power BI access token is required before executing DAX.");
        }

        if (string.IsNullOrWhiteSpace(xmlaEndpoint))
        {
            throw new InvalidOperationException("An XMLA endpoint is required before executing DAX.");
        }

        if (string.IsNullOrWhiteSpace(initialCatalog))
        {
            throw new InvalidOperationException("A semantic model name is required before executing DAX.");
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            throw new InvalidOperationException("Enter a DAX query before executing.");
        }

        var queryPreview = BuildQueryPreview(query);
        logStore.WriteInfo("DAX", $"Executing DAX query against semantic model '{initialCatalog}'. Preview: {queryPreview}");

        try
        {
            var stopwatch = Stopwatch.StartNew();

            using var connection = new AdomdConnection();
            connection.AccessToken = new AccessToken(accessToken, parsedAccessToken.ExpiresAtUtc, null);
            connection.ConnectionString = $"Data Source={xmlaEndpoint};Initial Catalog={initialCatalog};";
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = query;

            using var reader = command.ExecuteReader();
            var resultTable = LoadResultTable(reader);

            stopwatch.Stop();

            var result = new DaxQueryResult(resultTable, stopwatch.Elapsed);
            logStore.WriteInfo(
                "DAX",
                $"DAX query completed in {result.Elapsed.TotalMilliseconds:N0} ms with {result.RowCount} row(s).");

            return result;
        }
        catch (Exception ex)
        {
            var message = $"DAX execution failed for semantic model '{initialCatalog}' at '{xmlaEndpoint}'. {ex.Message}";
            logStore.WriteError("DAX", message);
            throw new InvalidOperationException(message, ex);
        }
    }

    private static string BuildQueryPreview(string query)
    {
        var flattened = string.Join(" ", query.Split((string[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return flattened.Length <= 160 ? flattened : $"{flattened[..157]}...";
    }

    private static DataTable LoadResultTable(AdomdDataReader reader)
    {
        var resultTable = new DataTable();
        var usedColumnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var columnIndex = 0; columnIndex < reader.FieldCount; columnIndex++)
        {
            var columnName = BuildUniqueColumnName(reader.GetName(columnIndex), columnIndex, usedColumnNames);
            var columnType = reader.GetFieldType(columnIndex);

            resultTable.Columns.Add(new DataColumn(columnName, columnType));
        }

        while (reader.Read())
        {
            var values = new object[reader.FieldCount];
            reader.GetValues(values);

            for (var columnIndex = 0; columnIndex < values.Length; columnIndex++)
            {
                if (values[columnIndex] is null)
                {
                    values[columnIndex] = DBNull.Value;
                }
            }

            resultTable.Rows.Add(values);
        }

        return resultTable;
    }

    private static string BuildUniqueColumnName(string? rawColumnName, int columnIndex, ISet<string> usedColumnNames)
    {
        var baseName = string.IsNullOrWhiteSpace(rawColumnName)
            ? $"Column{columnIndex + 1}"
            : rawColumnName;

        var candidate = baseName;
        var suffix = 2;

        while (!usedColumnNames.Add(candidate))
        {
            candidate = $"{baseName}_{suffix}";
            suffix++;
        }

        return candidate;
    }
}
