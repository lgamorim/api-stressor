namespace Stressor.Core;

/// <summary>Executes stress-test sessions using configured load modes and pacing.</summary>
public sealed class StressTestRunner : IStressTestRunner
{
    private readonly IJsonPayloadReader _payloadReader;
    private readonly IHttpStressTestClient _httpClient;
    private readonly IConsoleSessionReporter _reporter;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates a runner with the given collaborators.</summary>
    public StressTestRunner(
        IJsonPayloadReader payloadReader,
        IHttpStressTestClient httpClient,
        IConsoleSessionReporter reporter,
        TimeProvider timeProvider)
    {
        _payloadReader = payloadReader;
        _httpClient = httpClient;
        _reporter = reporter;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<SessionReport> RunAsync(StressTestOptions options, CancellationToken cancellationToken = default)
    {
        var validationErrors = StressTestOptionsValidator.Validate(options);
        if (validationErrors.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", validationErrors));
        }

        var payloads = await _payloadReader.ReadAsync(options.PayloadFilePath, cancellationToken).ConfigureAwait(false);

        return options.Load switch
        {
            LoadMode.GentlePacing => await RunGentlePacingAsync(options, payloads, cancellationToken).ConfigureAwait(false),
            LoadMode.FixedRate => await RunFixedRateAsync(options, payloads, cancellationToken).ConfigureAwait(false),
            LoadMode.Batch => await RunBatchAsync(options, payloads, cancellationToken).ConfigureAwait(false),
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

        _reporter.WriteSessionStart(options);

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
                        await Task.Delay(waitTime, _timeProvider, cancellationToken).ConfigureAwait(false);
                        elapsed += waitTime;
                    }
                }

                var payload = payloads[(request - 1) % payloads.Count];
                var payloadIndex = (request - 1) % payloads.Count + 1;
                var requestStart = elapsed;
                var sessionRequestIndex = (cycle - 1) * options.RequestsPerInterval + request;

                var outcome = await _httpClient.SendAsync(
                    options,
                    payload,
                    cycle,
                    request,
                    cancellationToken).ConfigureAwait(false);

                outcome = outcome with
                {
                    PayloadIndex = payloadIndex,
                    PayloadCount = payloads.Count
                };

                elapsed += outcome.Latency;
                nextRequestStart = requestStart + options.Interval;

                cycleOutcomes.Add(outcome);
                outcomes.Add(outcome);

                ReportVerboseRequestIfNeeded(
                    options,
                    cycle,
                    request,
                    payload,
                    payloads.Count,
                    sessionRequestIndex,
                    sessionTotalRequests,
                    outcome);

                if (outcome.IsCancelled)
                {
                    wasCancelled = true;
                    break;
                }
            }

            _reporter.WriteCycleSummary(cycle, options.Cycles, cycleOutcomes);

            if (!wasCancelled && cycle < options.Cycles && options.CycleInterval > TimeSpan.Zero)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    wasCancelled = true;
                    break;
                }

                await Task.Delay(options.CycleInterval, _timeProvider, cancellationToken).ConfigureAwait(false);
                elapsed += options.CycleInterval;
                nextRequestStart = null;
            }

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
        _reporter.WriteSessionComplete(report);
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

        _reporter.WriteSessionStart(options);

        for (var k = 0; k < totalRequests && !cancellationToken.IsCancellationRequested; k++)
        {
            var cycleIndex = k / options.RequestsPerInterval;
            var requestInCycle = k % options.RequestsPerInterval;
            var scheduledAt = TimeSpan.FromTicks(
                (long)cycleIndex * (options.RequestsPerInterval * options.Interval.Ticks + options.CycleInterval.Ticks)
                + (long)requestInCycle * options.Interval.Ticks);
            var waitTime = scheduledAt - elapsed;
            if (waitTime > TimeSpan.Zero)
            {
                await Task.Delay(waitTime, _timeProvider, cancellationToken).ConfigureAwait(false);
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
                payloads,
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

            _reporter.WriteCycleSummary(cycle, options.Cycles, cycleOutcomes);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            wasCancelled = true;
        }

        var report = new SessionReport(options, outcomes, wasCancelled);
        _reporter.WriteSessionComplete(report);
        return report;
    }

    private async Task<SessionReport> RunBatchAsync(
        StressTestOptions options,
        IReadOnlyList<string> payloads,
        CancellationToken cancellationToken)
    {
        var outcomes = new List<RequestOutcome>();
        var wasCancelled = false;
        var sessionTotalRequests = options.RequestsPerInterval * options.Cycles;

        _reporter.WriteSessionStart(options);

        var elapsed = TimeSpan.Zero;
        TimeSpan? nextWaveStart = null;

        for (var cycle = 1; cycle <= options.Cycles && !cancellationToken.IsCancellationRequested; cycle++)
        {
            var cycleOutcomes = new List<RequestOutcome>();

            for (var waveStart = 0; waveStart < options.RequestsPerInterval; waveStart += options.Batch)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    wasCancelled = true;
                    break;
                }

                if (nextWaveStart is not null && options.Interval > TimeSpan.Zero)
                {
                    var waitTime = nextWaveStart.Value - elapsed;
                    if (waitTime > TimeSpan.Zero)
                    {
                        await Task.Delay(waitTime, _timeProvider, cancellationToken).ConfigureAwait(false);
                        elapsed += waitTime;
                    }
                }

                var waveStartTime = elapsed;
                var waveSize = Math.Min(options.Batch, options.RequestsPerInterval - waveStart);
                var tasks = new Task<RequestOutcome>[waveSize];

                for (var i = 0; i < waveSize; i++)
                {
                    var request = waveStart + i + 1;
                    var payload = payloads[(request - 1) % payloads.Count];
                    var sessionRequestIndex = (cycle - 1) * options.RequestsPerInterval + request;

                    tasks[i] = SendAndReportAsync(
                        options,
                        payloads,
                        payload,
                        cycle,
                        request,
                        sessionRequestIndex,
                        sessionTotalRequests,
                        cancellationToken);
                }

                var waveOutcomes = await Task.WhenAll(tasks).ConfigureAwait(false);

                if (waveOutcomes.Length > 0)
                {
                    var waveDuration = waveOutcomes.Max(o => o.Latency);
                    elapsed += waveDuration;
                }

                nextWaveStart = waveStartTime + options.Interval;

                cycleOutcomes.AddRange(waveOutcomes);
                outcomes.AddRange(waveOutcomes);

                if (waveOutcomes.Any(o => o.IsCancelled))
                {
                    wasCancelled = true;
                    break;
                }
            }

            _reporter.WriteCycleSummary(cycle, options.Cycles, cycleOutcomes);

            if (!wasCancelled && cycle < options.Cycles && options.CycleInterval > TimeSpan.Zero)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    wasCancelled = true;
                    break;
                }

                await Task.Delay(options.CycleInterval, _timeProvider, cancellationToken).ConfigureAwait(false);
                elapsed += options.CycleInterval;
                nextWaveStart = null;
            }

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
        _reporter.WriteSessionComplete(report);
        return report;
    }

    private async Task<RequestOutcome> SendAndReportAsync(
        StressTestOptions options,
        IReadOnlyList<string> payloads,
        string payload,
        int cycle,
        int request,
        int sessionRequestIndex,
        int sessionTotalRequests,
        CancellationToken cancellationToken)
    {
        var payloadIndex = (request - 1) % payloads.Count + 1;
        var outcome = await _httpClient.SendAsync(
            options,
            payload,
            cycle,
            request,
            cancellationToken).ConfigureAwait(false);

        outcome = outcome with
        {
            PayloadIndex = payloadIndex,
            PayloadCount = payloads.Count
        };

        ReportVerboseRequestIfNeeded(
            options,
            cycle,
            request,
            payload,
            payloads.Count,
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
        int payloadCount,
        int sessionRequestIndex,
        int sessionTotalRequests,
        RequestOutcome outcome)
    {
        if (!ShouldReportVerbose(options.Verbose, outcome))
        {
            return;
        }

        var requestPayload = HttpStressTestClient.HttpMethodSupportsBody(options.Method)
            ? payload
            : null;

        _reporter.WriteVerboseRequest(
            cycle,
            options.Cycles,
            request,
            options.RequestsPerInterval,
            requestPayload,
            sessionRequestIndex,
            sessionTotalRequests,
            outcome.PayloadIndex,
            payloadCount,
            outcome);
    }

    internal static bool ShouldReportVerbose(VerboseMode mode, RequestOutcome outcome) =>
        mode switch
        {
            VerboseMode.Off => false,
            VerboseMode.Full => true,
            VerboseMode.Failures => !outcome.IsSuccess,
            _ => false
        };
}
