namespace Stressor.Core;

/// <summary>Thrown when an HTTP headers file is present but invalid.</summary>
public sealed class HttpHeadersValidationException : Exception
{
    /// <summary>Creates an exception describing the validation failure.</summary>
    public HttpHeadersValidationException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception describing the validation failure and its cause.</summary>
    public HttpHeadersValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
