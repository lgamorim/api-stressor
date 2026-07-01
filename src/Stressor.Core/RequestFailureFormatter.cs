namespace Stressor.Core;

using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

internal static partial class RequestFailureFormatter
{
    internal const int MaxBodyLength = 1024;

    internal static async Task<string> FormatHttpErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var statusCode = response.StatusCode;
        var reasonPhrase = response.ReasonPhrase;
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        var body = response.Content is null
            ? null
            : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        var summary = $"HTTP {(int)statusCode} {reasonPhrase}";
        var bodySummary = SummarizeBody(body, mediaType);

        return bodySummary is null ? summary : $"{summary} — {bodySummary}";
    }

    internal static string FormatException(Exception exception)
    {
        if (exception is HttpRequestException httpRequestException && httpRequestException.StatusCode is not null)
        {
            var status = httpRequestException.StatusCode.Value;
            var message = UnwrapMessages(exception);
            return $"HTTP {(int)status} {status} — {message}";
        }

        return UnwrapMessages(exception);
    }

    internal static string FormatTimeout() => "Request timed out.";

    internal static string? SummarizeBody(string? body, string? mediaType)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        if (IsJson(mediaType, body))
        {
            return SummarizeJson(body);
        }

        if (IsHtml(mediaType, body))
        {
            return SummarizeHtml(body);
        }

        return Truncate(body.Trim());
    }

    private static string SummarizeJson(string body)
    {
        var trimmed = body.Trim();

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var propertyName in new[] { "detail", "title", "message", "error" })
                {
                    if (!root.TryGetProperty(propertyName, out var value))
                    {
                        continue;
                    }

                    var extracted = ExtractJsonValue(value);
                    if (extracted is not null)
                    {
                        return Truncate($"{propertyName}: {extracted}");
                    }
                }
            }
        }
        catch (JsonException)
        {
        }

        return Truncate(trimmed);
    }

    private static string? ExtractJsonValue(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Array when value.GetArrayLength() > 0 => ExtractJsonValue(value[0]),
            _ => null
        };

    private static string SummarizeHtml(string body)
    {
        var withoutTags = HtmlTagPattern().Replace(body, " ");
        var normalized = WhitespacePattern().Replace(withoutTags, " ").Trim();
        var summary = string.IsNullOrWhiteSpace(normalized) ? "HTML response" : normalized;

        return Truncate(summary, prefix: "HTML: ");
    }

    private static string UnwrapMessages(Exception exception)
    {
        var messages = new List<string>();

        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (!string.IsNullOrWhiteSpace(current.Message))
            {
                messages.Add(current.Message);
            }
        }

        return messages.Count == 0 ? exception.GetType().Name : string.Join(" -> ", messages);
    }

    private static string Truncate(string value, string? prefix = null)
    {
        if (value.Length <= MaxBodyLength)
        {
            return prefix is null ? value : prefix + value;
        }

        var truncated = value[..MaxBodyLength];
        var suffix = $"... (truncated, {value.Length} chars total)";
        return prefix is null ? truncated + suffix : prefix + truncated + suffix;
    }

    private static bool IsJson(string? mediaType, string body)
    {
        if (mediaType is not null
            && (mediaType.Contains("json", StringComparison.OrdinalIgnoreCase)
                || mediaType.Contains("problem+json", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var trimmed = body.TrimStart();
        return trimmed.StartsWith('{') || trimmed.StartsWith('[');
    }

    private static bool IsHtml(string? mediaType, string body)
    {
        if (mediaType is not null && mediaType.Contains("html", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var trimmed = body.TrimStart();
        return trimmed.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex("<[^>]+>", RegexOptions.Singleline)]
    private static partial Regex HtmlTagPattern();

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespacePattern();
}
