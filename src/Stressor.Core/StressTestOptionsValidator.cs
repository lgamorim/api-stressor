namespace Stressor.Core;

public static class StressTestOptionsValidator
{
    private static readonly HashSet<string> AllowedMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethod.Get.Method,
        HttpMethod.Post.Method,
        HttpMethod.Put.Method,
        HttpMethod.Patch.Method,
        HttpMethod.Delete.Method,
        HttpMethod.Head.Method,
        HttpMethod.Options.Method
    };

    public static IReadOnlyList<string> Validate(StressTestOptions options)
    {
        var errors = new List<string>();

        if (!options.Url.IsAbsoluteUri)
        {
            errors.Add("URL must be absolute.");
        }
        else if (options.Url.Scheme is not "http" and not "https")
        {
            errors.Add("URL must use http or https.");
        }

        if (string.IsNullOrWhiteSpace(options.PayloadFilePath))
        {
            errors.Add("Payload file path is required.");
        }

        if (!AllowedMethods.Contains(options.Method.Method))
        {
            errors.Add($"HTTP method '{options.Method.Method}' is not supported. Allowed: GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS.");
        }

        if (options.RequestsPerInterval <= 0)
        {
            errors.Add("Requests per interval must be greater than zero.");
        }

        if (options.Interval <= TimeSpan.Zero)
        {
            errors.Add("Interval must be greater than zero.");
        }

        if (options.Cycles <= 0)
        {
            errors.Add("Cycles must be greater than zero.");
        }

        if (options.Auth is not null && string.IsNullOrWhiteSpace(options.Auth))
        {
            errors.Add("Auth value cannot be empty or whitespace.");
        }

        return errors;
    }
}
