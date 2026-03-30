namespace PbiRestProxy.Logging;

public sealed record LogEntry(DateTimeOffset Timestamp, AppLogLevel Level, string Source, string Message)
{
    public string LocalTimestampText => Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz");
}

