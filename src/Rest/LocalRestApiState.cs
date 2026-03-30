namespace PbiRestProxy.Rest;

public sealed record LocalRestApiState(
    bool IsRunning,
    string BaseUrl,
    string StatusText,
    string? StartupError)
{
    public static LocalRestApiState Stopped(string baseUrl) => new(false, baseUrl, "Stopped", null);

    public static LocalRestApiState Running(string baseUrl) => new(true, baseUrl, "Running", null);

    public static LocalRestApiState Failed(string baseUrl, string startupError) => new(false, baseUrl, "Failed", startupError);
}
