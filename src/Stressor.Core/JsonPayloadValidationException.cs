namespace Stressor.Core;

/// <summary>Thrown when a payload file is present but not valid JSON for stress testing.</summary>
public sealed class JsonPayloadValidationException : Exception
{
    /// <summary>Creates an exception describing the validation failure.</summary>
    public JsonPayloadValidationException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception describing the validation failure and its cause.</summary>
    public JsonPayloadValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
