namespace PbiRestProxy.Logging;

public sealed class LogStore
{
    private readonly object syncRoot = new();
    private readonly List<LogEntry> entries = [];

    public event Action<LogEntry>? EntryAdded;
    public event Action? Cleared;

    public IReadOnlyList<LogEntry> Snapshot()
    {
        lock (syncRoot)
        {
            return entries.ToArray();
        }
    }

    public void Clear()
    {
        lock (syncRoot)
        {
            entries.Clear();
        }

        Cleared?.Invoke();
    }

    public void WriteInfo(string source, string message)
    {
        Add(new LogEntry(DateTimeOffset.Now, AppLogLevel.Info, source, message));
    }

    public void WriteWarning(string source, string message)
    {
        Add(new LogEntry(DateTimeOffset.Now, AppLogLevel.Warning, source, message));
    }

    public void WriteError(string source, string message)
    {
        Add(new LogEntry(DateTimeOffset.Now, AppLogLevel.Error, source, message));
    }

    private void Add(LogEntry entry)
    {
        lock (syncRoot)
        {
            entries.Add(entry);
        }

        EntryAdded?.Invoke(entry);
    }
}

