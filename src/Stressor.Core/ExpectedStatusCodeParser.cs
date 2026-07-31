namespace Stressor.Core;

/// <summary>Parses expected HTTP status code values from CLI input.</summary>
public static class ExpectedStatusCodeParser
{
    private const int MinStatusCode = 100;
    private const int MaxStatusCode = 599;

    /// <summary>Parses a single status code in the range 100–599.</summary>
    public static bool TryParse(string input, out int code)
    {
        code = default;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        if (!int.TryParse(input.Trim(), out code))
        {
            return false;
        }

        return IsValidRange(code);
    }

    /// <summary>Parses one or more inputs, each of which may be a single code or comma-separated list.</summary>
    public static bool TryParseMany(
        IEnumerable<string> inputs,
        out IReadOnlySet<int> codes,
        out string? error)
    {
        var parsed = new HashSet<int>();

        foreach (var input in inputs)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                error = "Status code cannot be empty.";
                codes = parsed;
                return false;
            }

            foreach (var token in input.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (!TryParse(token, out var code))
                {
                    error = $"Status code '{token}' is invalid. Use integers from 100 to 599.";
                    codes = parsed;
                    return false;
                }

                parsed.Add(code);
            }
        }

        error = null;
        codes = parsed;
        return true;
    }

    private static bool IsValidRange(int code) => code is >= MinStatusCode and <= MaxStatusCode;
}
