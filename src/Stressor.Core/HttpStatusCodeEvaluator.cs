namespace Stressor.Core;

/// <summary>Evaluates whether an HTTP status code counts as success for a stress test.</summary>
public static class HttpStatusCodeEvaluator
{
    /// <summary>
    /// Returns whether the status code is successful.
    /// When <paramref name="expectedStatusCodes"/> is empty, any 2xx code succeeds.
    /// </summary>
    public static bool IsSuccess(int statusCode, IReadOnlySet<int> expectedStatusCodes)
    {
        if (expectedStatusCodes.Count == 0)
        {
            return statusCode is >= 200 and <= 299;
        }

        return expectedStatusCodes.Contains(statusCode);
    }
}
