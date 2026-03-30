namespace PbiRestProxy.Session;

public sealed record ParsedAccessToken(
    string Audience,
    string? TenantId,
    string? Name,
    string? PreferredUsername,
    string? UserPrincipalName,
    string? ApplicationId,
    DateTimeOffset? IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc)
{
    public string DisplayUser
    {
        get
        {
            var login = FirstNonEmpty(PreferredUsername, UserPrincipalName);
            var displayName = FirstNonEmpty(Name, login);

            if (!string.IsNullOrWhiteSpace(displayName) &&
                !string.IsNullOrWhiteSpace(login) &&
                !string.Equals(displayName, login, StringComparison.Ordinal))
            {
                return $"{displayName} ({login})";
            }

            return displayName ?? "Unknown user";
        }
    }

    public DateTimeOffset ExpiresAtLocal => ExpiresAtUtc.ToLocalTime();

    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAtUtc;

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }
}

