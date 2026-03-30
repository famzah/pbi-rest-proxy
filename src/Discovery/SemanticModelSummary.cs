namespace PbiRestProxy.Discovery;

public sealed record SemanticModelSummary(
    string Id,
    string Name,
    string? ConfiguredBy,
    bool? IsRefreshable)
{
    public string OwnerDisplay => string.IsNullOrWhiteSpace(ConfiguredBy) ? "-" : ConfiguredBy;

    public string RefreshableDisplay => IsRefreshable switch
    {
        true => "Yes",
        false => "No",
        _ => "-"
    };
}
