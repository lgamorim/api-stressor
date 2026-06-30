namespace Stressor.Core;

public sealed class StressTestRunner : IStressTestRunner
{
    private readonly IJsonPayloadReader payloadReader;
    private readonly IHttpStressTestClient httpClient;
    private readonly IConsoleSessionReporter reporter;
    private readonly IDelayProvider delayProvider;

    public StressTestRunner(
        IJsonPayloadReader payloadReader,
        IHttpStressTestClient httpClient,
        IConsoleSessionReporter reporter,
        IDelayProvider delayProvider)
    {
        this.payloadReader = payloadReader;
        this.httpClient = httpClient;
        this.reporter = reporter;
        this.delayProvider = delayProvider;
    }

    public async Task<SessionReport> RunAsync(StressTestOptions options, CancellationToken cancellationToken = default)
    {
        var validationErrors = StressTestOptionsValidator.Validate(options);
        if (validationErrors.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", validationErrors));
        }

        var payloads = await payloadReader.ReadAsync(options.PayloadFilePath, cancellationToken).ConfigureAwait(false);
        var outcomes = new List<RequestOutcome>();
        var wasCancelled = false;

        reporter.WriteSessionStart(options);

        var perRequestDelay = TimeSpan.FromTicks(options.Interval.Ticks / options.RequestsPerInterval);

        for (var cycle = 1; cycle <= options.Cycles && !cancellationToken.IsCancellationRequested; cycle++)
        {
            var cycleElapsed = TimeSpan.Zero;
            var cycleOutcomes = new List<RequestOutcome>();

            for (var request = 1; request <= options.RequestsPerInterval; request++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    wasCancelled = true;
                    break;
                }

                var payload = payloads[(request - 1) % payloads.Count];

                var outcome = await httpClient.SendAsync(
                    options,
                    payload,
                    cycle,
                    request,
                    cancellationToken).ConfigureAwait(false);

                cycleOutcomes.Add(outcome);
                outcomes.Add(outcome);

                if (outcome.IsCancelled)
                {
                    wasCancelled = true;
                    break;
                }

                if (request < options.RequestsPerInterval && !cancellationToken.IsCancellationRequested)
                {
                    var targetElapsed = perRequestDelay * request;
                    var waitTime = targetElapsed - cycleElapsed;
                    if (waitTime > TimeSpan.Zero)
                    {
                        await delayProvider.DelayAsync(waitTime, cancellationToken).ConfigureAwait(false);
                        cycleElapsed += waitTime;
                    }
                }
            }

            reporter.WriteCycleSummary(cycle, options.Cycles, cycleOutcomes);

            if (wasCancelled || cancellationToken.IsCancellationRequested)
            {
                wasCancelled = true;
                break;
            }

            if (cycle < options.Cycles)
            {
                var cycleRemainder = options.Interval - cycleElapsed;
                if (cycleRemainder > TimeSpan.Zero)
                {
                    await delayProvider.DelayAsync(cycleRemainder, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        if (cancellationToken.IsCancellationRequested)
        {
            wasCancelled = true;
        }

        var report = new SessionReport(options, outcomes, wasCancelled);
        reporter.WriteSessionComplete(report);
        return report;
    }
}
