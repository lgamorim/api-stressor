namespace Stressor.Core;

public sealed class JsonPayloadValidationException : Exception
{
    public JsonPayloadValidationException(string message)
        : base(message)
    {
    }

    public JsonPayloadValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
