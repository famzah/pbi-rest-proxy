using System.Globalization;
using System.Text;
using System.Text.Json;

namespace PbiRestProxy.Session;

public static class AccessTokenParser
{
    public static string Normalize(string tokenInput)
    {
        if (string.IsNullOrWhiteSpace(tokenInput))
        {
            throw new InvalidAccessTokenException("Paste an access token first.");
        }

        var normalizedToken = tokenInput.Trim();
        const string bearerPrefix = "Bearer ";

        return normalizedToken.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? normalizedToken[bearerPrefix.Length..].Trim()
            : normalizedToken;
    }

    public static ParsedAccessToken Parse(string normalizedToken)
    {
        var tokenParts = normalizedToken.Split('.');

        if (tokenParts.Length < 2)
        {
            throw new InvalidAccessTokenException("The access token must be a JWT with at least a header and payload section.");
        }

        try
        {
            var payloadJson = Encoding.UTF8.GetString(DecodeBase64Url(tokenParts[1]));
            using var payloadDocument = JsonDocument.Parse(payloadJson);
            var payload = payloadDocument.RootElement;

            var expiration = ReadUnixTimestampClaim(payload, "exp");

            if (expiration is null)
            {
                throw new InvalidAccessTokenException("The access token payload does not contain an 'exp' claim.");
            }

            return new ParsedAccessToken(
                Audience: ReadClaim(payload, "aud") ?? "n/a",
                TenantId: ReadClaim(payload, "tid"),
                Name: ReadClaim(payload, "name"),
                PreferredUsername: ReadClaim(payload, "preferred_username"),
                UserPrincipalName: ReadClaim(payload, "upn"),
                ApplicationId: ReadClaim(payload, "appid") ?? ReadClaim(payload, "azp"),
                IssuedAtUtc: ReadUnixTimestampClaim(payload, "iat"),
                ExpiresAtUtc: expiration.Value);
        }
        catch (InvalidAccessTokenException)
        {
            throw;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            throw new InvalidAccessTokenException("The access token could not be parsed as a valid JWT.", ex);
        }
    }

    private static string? ReadClaim(JsonElement payload, string name)
    {
        if (!payload.TryGetProperty(name, out var claimValue))
        {
            return null;
        }

        return claimValue.ValueKind switch
        {
            JsonValueKind.String => claimValue.GetString(),
            JsonValueKind.Number => claimValue.GetRawText(),
            JsonValueKind.Array => string.Join(", ",
                claimValue.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!)),
            _ => null
        };
    }

    private static DateTimeOffset? ReadUnixTimestampClaim(JsonElement payload, string name)
    {
        if (!payload.TryGetProperty(name, out var claimValue))
        {
            return null;
        }

        long unixSeconds;

        if (claimValue.ValueKind == JsonValueKind.Number)
        {
            unixSeconds = claimValue.GetInt64();
        }
        else if (claimValue.ValueKind == JsonValueKind.String &&
                 long.TryParse(claimValue.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out unixSeconds))
        {
        }
        else
        {
            return null;
        }

        return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
    }

    private static byte[] DecodeBase64Url(string value)
    {
        var paddedValue = value.Replace('-', '+').Replace('_', '/');

        paddedValue = (paddedValue.Length % 4) switch
        {
            0 => paddedValue,
            2 => paddedValue + "==",
            3 => paddedValue + "=",
            _ => throw new FormatException("Invalid base64url payload length.")
        };

        return Convert.FromBase64String(paddedValue);
    }
}
