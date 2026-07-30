namespace Stressor.Core;

/// <summary>Parses HTTP header strings in <c>Name: Value</c> format.</summary>
public static class HttpHeaderParser
{
    /// <summary>Parses a header string, splitting on the first colon.</summary>
    public static bool TryParse(string input, out string name, out string value)
    {
        name = string.Empty;
        value = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var separatorIndex = input.IndexOf(':');
        if (separatorIndex <= 0)
        {
            return false;
        }

        name = input[..separatorIndex].Trim();
        if (name.Length == 0)
        {
            return false;
        }

        value = input[(separatorIndex + 1)..].Trim();
        return true;
    }
}
