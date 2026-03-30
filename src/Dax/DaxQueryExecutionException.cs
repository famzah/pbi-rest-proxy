namespace PbiRestProxy.Dax;

public sealed class DaxQueryExecutionException : InvalidOperationException
{
    public DaxQueryExecutionException(DaxQueryFailureKind failureKind, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        FailureKind = failureKind;
    }

    public DaxQueryFailureKind FailureKind { get; }
}
