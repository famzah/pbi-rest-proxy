namespace PbiRestProxy.Discovery;

public sealed record WorkspaceSummary(
    string Id,
    string Name,
    bool IsOnDedicatedCapacity)
{
    public string CapacityDisplay => IsOnDedicatedCapacity ? "Dedicated" : "Shared";
}
