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

        return options.Load switch
        {
            LoadMode.GentlePacing => await RunGentlePacingAsync(options, payloads, cancellationToken).ConfigureAwait(false),
            LoadMode.FixedRate => await RunFixedRateAsync(options, payloads, cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(options), options.Load, "Unsupported load mode.")
        };
    }

    private async Task<SessionReport> RunGentlePacingAsync(
        StressTestOptions options,
        IReadOnlyList<string> payloads,
        CancellationToken cancellationToken)
    {
        var outcomes = new List<RequestOutcome>();
        var wasCancelled = false;
        var sessionTotalRequests = options.RequestsPerInterval * options.Cycles;

        reporter.WriteSessionStart(options);

        var elapsed = TimeSpan.Zero;
        TimeSpan? nextRequestStart = null;

        for (var cycle = 1; cycle <= options.Cycles && !cancellationToken.IsCancellationRequested; cycle++)
        {
            var cycleOutcomes = new List<RequestOutcome>();

            for (var request = 1; request <= options.RequestsPerInterval; request++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    wasCancelled = true;
                    break;
                }

                if (nextRequestStart is not null)
                {
                    var waitTime = nextRequestStart.Value - elapsed;
                    if (waitTime > TimeSpan.Zero)
                    {
                        await delayProvider.DelayAsync(waitTime, cancellationToken).ConfigureAwait(false);
                        elapsed += waitTime;
                    }
                }

                var payload = payloads[(request - 1) % payloads.Count];
                var requestStart = elapsed;
                var sessionRequestIndex = (cycle - 1) * options.RequestsPerInterval + request;

                var outcome = await httpClient.SendAsync(
                    options,
                    payload,
                    cycle,
                    request,
                    cancellationToken).ConfigureAwait(false);

                elapsed += outcome.Latency;
                nextRequestStart = requestStart + options.Interval;

                cycleOutcomes.Add(outcome);
                outcomes.Add(outcome);

                ReportVerboseRequestIfNeeded(
                    options,
                    cycle,
                    request,
                    payload,
                    sessionRequestIndex,
                    sessionTotalRequests,
                    outcome);

                if (outcome.IsCancelled)
                {
                    wasCancelled = true;
                    break;
                }
            }

            reporter.WriteCycleSummary(cycle, options.Cycles, cycleOutcomes);

            if (wasCancelled || cancellationToken.IsCancellationRequested)
            {
                wasCancelled = true;
                break;
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

    private async Task<SessionReport> RunFixedRateAsync(
        StressTestOptions options,
        IReadOnlyList<string> payloads,
        CancellationToken cancellationToken)
    {
        var totalRequests = options.RequestsPerInterval * options.Cycles;
        var tasksByCycle = new List<List<Task<RequestOutcome>>>(options.Cycles);
        for (var i = 0; i < options.Cycles; i++)
        {
            tasksByCycle.Add([]);
        }

        var outcomes = new List<RequestOutcome>();
        var wasCancelled = false;
        var elapsed = TimeSpan.Zero;

        reporter.WriteSessionStart(options);

        for (var k = 0; k < totalRequests && !cancellationToken.IsCancellationRequested; k++)
        {
            var scheduledAt = TimeSpan.FromTicks((long)k * options.Interval.Ticks);
            var waitTime = scheduledAt - elapsed;
            if (waitTime > TimeSpan.Zero)
            {
                await delayProvider.DelayAsync(waitTime, cancellationToken).ConfigureAwait(false);
                elapsed += waitTime;
            }
            else
            {
                elapsed = scheduledAt;
            }

            var cycle = k / options.RequestsPerInterval + 1;
            var request = k % options.RequestsPerInterval + 1;
            var payload = payloads[(request - 1) % payloads.Count];
            var sessionRequestIndex = k + 1;

            var task = SendAndReportAsync(
                options,
                payload,
                cycle,
                request,
                sessionRequestIndex,
                totalRequests,
                cancellationToken);

            tasksByCycle[cycle - 1].Add(task);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            wasCancelled = true;
        }

        for (var cycle = 1; cycle <= options.Cycles; cycle++)
        {
            var cycleTasks = tasksByCycle[cycle - 1];
            if (cycleTasks.Count == 0)
            {
                continue;
            }

            var cycleOutcomes = await Task.WhenAll(cycleTasks).ConfigureAwait(false);
            outcomes.AddRange(cycleOutcomes);

            if (cycleOutcomes.Any(o => o.IsCancelled))
            {
                wasCancelled = true;
            }

            reporter.WriteCycleSummary(cycle, options.Cycles, cycleOutcomes);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            wasCancelled = true;
        }

        var report = new SessionReport(options, outcomes, wasCancelled);
        reporter.WriteSessionComplete(report);
        return report;
    }

    private async Task<RequestOutcome> SendAndReportAsync(
        StressTestOptions options,
        string payload,
        int cycle,
        int request,
        int sessionRequestIndex,
        int sessionTotalRequests,
        CancellationToken cancellationToken)
    {
        var outcome = await httpClient.SendAsync(
            options,
            payload,
            cycle,
            request,
            cancellationToken).ConfigureAwait(false);

        ReportVerboseRequestIfNeeded(
            options,
            cycle,
            request,
            payload,
            sessionRequestIndex,
            sessionTotalRequests,
            outcome);

        return outcome;
    }

    private void ReportVerboseRequestIfNeeded(
        StressTestOptions options,
        int cycle,
        int request,
        string payload,
        int sessionRequestIndex,
        int sessionTotalRequests,
        RequestOutcome outcome)
    {
        if (!options.Verbose && !options.PrettyPrint)
        {
            return;
        }

        reporter.WriteVerboseRequest(
            cycle,
            options.Cycles,
            request,
            options.RequestsPerInterval,
            payload,
            options.PrettyPrint,
            options.Load,
            sessionRequestIndex,
            sessionTotalRequests,
            outcome);
    }
}
