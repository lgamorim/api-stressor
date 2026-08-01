namespace Stressor.Core;

/// <summary>Validates <see cref="StressTestOptions"/> before a session starts.</summary>
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

    /// <summary>Returns validation errors for the given options, or an empty list when valid.</summary>
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

        if (options.Batch <= 0)
        {
            errors.Add("Batch size must be greater than zero.");
        }

        if (options.Batch > options.RequestsPerInterval)
        {
            errors.Add("Batch size cannot be greater than requests per cycle.");
        }

        if (options.Batch > 1 && options.Load != LoadMode.Batch)
        {
            errors.Add("Use --load batch when --batch is greater than 1.");
        }

        if (options.Load == LoadMode.Batch)
        {
            if (options.Interval < TimeSpan.Zero)
            {
                errors.Add("Interval must be greater than or equal to zero.");
            }
        }
        else if (options.Interval <= TimeSpan.Zero)
        {
            errors.Add("Interval must be greater than zero.");
        }

        if (!options.IsDurationLimited && options.Cycles <= 0)
        {
            errors.Add("Cycles must be greater than zero.");
        }

        if (options.IsDurationLimited && options.Duration <= TimeSpan.Zero)
        {
            errors.Add("Duration must be greater than zero.");
        }

        if (options.Auth is not null && string.IsNullOrWhiteSpace(options.Auth))
        {
            errors.Add("Auth value cannot be empty or whitespace.");
        }

        if (options.RequestTimeout <= TimeSpan.Zero)
        {
            errors.Add("Request timeout must be greater than zero.");
        }

        if (options.CycleInterval < TimeSpan.Zero)
        {
            errors.Add("Cycle interval must be greater than or equal to zero.");
        }

        foreach (var header in options.Headers)
        {
            if (string.IsNullOrWhiteSpace(header.Key))
            {
                errors.Add("Header name cannot be empty or whitespace.");
            }
        }

        foreach (var statusCode in options.ExpectedStatusCodes)
        {
            if (statusCode is < 100 or > 599)
            {
                errors.Add($"Status code '{statusCode}' is invalid. Use integers from 100 to 599.");
            }
        }

        return errors;
    }
}
