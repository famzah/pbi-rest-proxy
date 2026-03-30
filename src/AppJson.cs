using System.Text.Encodings.Web;
using System.Text.Json;

namespace PbiRestProxy;

internal static class AppJson
{
    internal static JsonSerializerOptions CreateSerializerOptions(bool writeIndented)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Configure(options, writeIndented);
        return options;
    }

    internal static void Configure(JsonSerializerOptions options, bool writeIndented)
    {
        options.WriteIndented = writeIndented;
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
    }
}
