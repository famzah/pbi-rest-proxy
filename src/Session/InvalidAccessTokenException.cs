namespace PbiRestProxy.Session;

public sealed class InvalidAccessTokenException : Exception
{
    public InvalidAccessTokenException(string message)
        : base(message)
    {
    }

    public InvalidAccessTokenException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

