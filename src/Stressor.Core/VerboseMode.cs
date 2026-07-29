namespace Stressor.Core;

/// <summary>Controls per-request console output during a stress session.</summary>
public enum VerboseMode
{
    /// <summary>Suppress per-request output.</summary>
    Off,

    /// <summary>Print detail only for failed or cancelled requests.</summary>
    Failures,

    /// <summary>Print detail for every request.</summary>
    Full,
}
