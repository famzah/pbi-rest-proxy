using System.Diagnostics;
using System.Globalization;
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
            var (columns, rows) = LoadResult(reader);
            stopwatch.Stop();
            var result = new DaxQueryResult(columns, rows, stopwatch.Elapsed);

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

    private static (IReadOnlyList<DaxResultColumn> Columns, IReadOnlyList<IReadOnlyList<object?>> Rows) LoadResult(AdomdDataReader reader)
    {
        var columns = new DaxResultColumn[reader.FieldCount];

        for (var columnIndex = 0; columnIndex < reader.FieldCount; columnIndex++)
        {
            var columnType = reader.GetFieldType(columnIndex);
            columns[columnIndex] = new DaxResultColumn(
                columnIndex,
                reader.GetName(columnIndex),
                reader.GetDataTypeName(columnIndex),
                columnType.Name,
                columnType.FullName);
        }

        var rows = new List<IReadOnlyList<object?>>();

        while (reader.Read())
        {
            var rawValues = new object[reader.FieldCount];
            var values = new object?[reader.FieldCount];
            reader.GetValues(rawValues);

            for (var columnIndex = 0; columnIndex < values.Length; columnIndex++)
            {
                values[columnIndex] = NormalizeValue(rawValues[columnIndex]);
            }

            rows.Add(values);
        }

        return (columns, rows);
    }

    private static object? NormalizeValue(object? value)
    {
        return value switch
        {
            null => null,
            DBNull => null,
            string stringValue => stringValue,
            bool boolValue => boolValue,
            byte byteValue => byteValue,
            sbyte sbyteValue => sbyteValue,
            short shortValue => shortValue,
            ushort ushortValue => ushortValue,
            int intValue => intValue,
            uint uintValue => uintValue,
            long longValue => longValue,
            ulong ulongValue => ulongValue,
            float floatValue => floatValue,
            double doubleValue => doubleValue,
            decimal decimalValue => decimalValue,
            Guid guidValue => guidValue,
            DateTime dateTimeValue => dateTimeValue,
            DateTimeOffset dateTimeOffsetValue => dateTimeOffsetValue,
            DateOnly dateOnlyValue => dateOnlyValue,
            TimeOnly timeOnlyValue => timeOnlyValue,
            TimeSpan timeSpanValue => timeSpanValue,
            byte[] bytes => bytes,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }
}
