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

        if (options.Cycles <= 0)
        {
            errors.Add("Cycles must be greater than zero.");
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

        return errors;
    }
}
