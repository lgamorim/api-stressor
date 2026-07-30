namespace Stressor.Core;

/// <summary>Merges HTTP header dictionaries with case-insensitive names.</summary>
public static class HttpHeadersMerger
{
    /// <summary>Merges header layers; later layers override earlier ones for the same name.</summary>
    public static IReadOnlyDictionary<string, string> Merge(params IEnumerable<KeyValuePair<string, string>>?[] layers)
    {
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var layer in layers)
        {
            if (layer is null)
            {
                continue;
            }

            foreach (var (name, value) in layer)
            {
                merged[name] = value;
            }
        }

        return merged;
    }
}
