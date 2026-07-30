namespace Stressor.Core;

/// <summary>Thrown when a scenario config file is present but invalid.</summary>
public sealed class StressTestScenarioValidationException : Exception
{
    /// <summary>Creates an exception describing the validation failure.</summary>
    public StressTestScenarioValidationException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception describing the validation failure and its cause.</summary>
    public StressTestScenarioValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
