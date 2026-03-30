namespace PbiRestProxy.Dax;

public sealed record DaxExecutionOptions
{
    public DaxExecutionOptions(int commandTimeoutSeconds, int rowLimit)
    {
        if (commandTimeoutSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(commandTimeoutSeconds), "The DAX command timeout must be greater than zero.");
        }

        if (rowLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rowLimit), "The DAX row limit must be greater than zero.");
        }

        CommandTimeoutSeconds = commandTimeoutSeconds;
        RowLimit = rowLimit;
    }

    public static DaxExecutionOptions Default { get; } = new(30, 1000);

    public int CommandTimeoutSeconds { get; }

    public int RowLimit { get; }
}
