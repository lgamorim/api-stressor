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
        var sessionStart = _timeProvider.GetUtcNow();
        var totalCycles = GetTotalCycles(options);
        var sessionTotalRequests = GetSessionTotalRequests(options);
        var sessionRequestIndex = 0;

        _reporter.WriteSessionStart(options);

        var elapsed = TimeSpan.Zero;
        TimeSpan? nextRequestStart = null;

        for (var cycle = 1; ShouldStartCycle(cycle, sessionStart, options) && !cancellationToken.IsCancellationRequested; cycle++)
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
                sessionRequestIndex++;

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
                    totalCycles,
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

            WriteCycleOrProgress(options, sessionStart, cycle, outcomes, cycleOutcomes, totalCycles, sessionTotalRequests);

            if (!wasCancelled
                && ShouldStartCycle(cycle + 1, sessionStart, options)
                && options.CycleInterval > TimeSpan.Zero)
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
        var outcomes = new List<RequestOutcome>();
        var wasCancelled = false;
        var elapsed = TimeSpan.Zero;
        var sessionStart = _timeProvider.GetUtcNow();
        var totalCycles = GetTotalCycles(options);
        var sessionTotalRequests = GetSessionTotalRequests(options);
        var tasksByCycle = new List<List<Task<RequestOutcome>>>();
        var sessionRequestIndex = 0;

        _reporter.WriteSessionStart(options);

        var k = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var cycle = k / options.RequestsPerInterval + 1;
            var requestInCycle = k % options.RequestsPerInterval;

            if (requestInCycle == 0)
            {
                if (!options.IsDurationLimited && cycle > options.Cycles)
                {
                    break;
                }

                if (options.IsDurationLimited && !ShouldStartCycle(cycle, sessionStart, options))
                {
                    break;
                }
            }

            var cycleIndex = k / options.RequestsPerInterval;
            while (tasksByCycle.Count <= cycleIndex)
            {
                tasksByCycle.Add([]);
            }

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

            var request = requestInCycle + 1;
            var payload = payloads[(request - 1) % payloads.Count];
            sessionRequestIndex++;

            var task = SendAndReportAsync(
                options,
                payloads,
                payload,
                cycle,
                request,
                sessionRequestIndex,
                sessionTotalRequests,
                totalCycles,
                cancellationToken);

            tasksByCycle[cycleIndex].Add(task);
            k++;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            wasCancelled = true;
        }

        for (var cycle = 1; cycle <= tasksByCycle.Count; cycle++)
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

            WriteCycleOrProgress(options, sessionStart, cycle, outcomes, cycleOutcomes, totalCycles, sessionTotalRequests);
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
        var sessionStart = _timeProvider.GetUtcNow();
        var totalCycles = GetTotalCycles(options);
        var sessionTotalRequests = GetSessionTotalRequests(options);
        var sessionRequestIndex = 0;

        _reporter.WriteSessionStart(options);

        var elapsed = TimeSpan.Zero;
        TimeSpan? nextWaveStart = null;

        for (var cycle = 1; ShouldStartCycle(cycle, sessionStart, options) && !cancellationToken.IsCancellationRequested; cycle++)
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
                    sessionRequestIndex++;

                    tasks[i] = SendAndReportAsync(
                        options,
                        payloads,
                        payload,
                        cycle,
                        request,
                        sessionRequestIndex,
                        sessionTotalRequests,
                        totalCycles,
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

            WriteCycleOrProgress(options, sessionStart, cycle, outcomes, cycleOutcomes, totalCycles, sessionTotalRequests);

            if (!wasCancelled
                && ShouldStartCycle(cycle + 1, sessionStart, options)
                && options.CycleInterval > TimeSpan.Zero)
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

    private void WriteCycleOrProgress(
        StressTestOptions options,
        DateTimeOffset sessionStart,
        int cycle,
        IReadOnlyList<RequestOutcome> outcomes,
        IReadOnlyList<RequestOutcome> cycleOutcomes,
        int? totalCycles,
        int? sessionTotalRequests)
    {
        if (options.Progress && options.Verbose == VerboseMode.Off)
        {
            _reporter.WriteProgress(CreateProgressSnapshot(options, sessionStart, cycle, outcomes, totalCycles, sessionTotalRequests));
            return;
        }

        _reporter.WriteCycleSummary(cycle, totalCycles, cycleOutcomes);
    }

    private SessionProgressSnapshot CreateProgressSnapshot(
        StressTestOptions options,
        DateTimeOffset sessionStart,
        int cycleNumber,
        IReadOnlyList<RequestOutcome> outcomes,
        int? totalCycles,
        int? totalRequests)
    {
        var succeeded = outcomes.Count(o => o.IsSuccess);
        var failed = outcomes.Count(o => !o.IsSuccess && !o.IsCancelled);
        var cancelled = outcomes.Count(o => o.IsCancelled);

        if (options.IsDurationLimited)
        {
            return new SessionProgressSnapshot(
                outcomes.Count,
                null,
                succeeded,
                failed,
                cancelled,
                cycleNumber,
                null,
                _timeProvider.GetUtcNow() - sessionStart,
                options.Duration);
        }

        return new SessionProgressSnapshot(
            outcomes.Count,
            totalRequests,
            succeeded,
            failed,
            cancelled,
            cycleNumber,
            totalCycles,
            null,
            null);
    }

    private bool ShouldStartCycle(int cycle, DateTimeOffset sessionStart, StressTestOptions options) =>
        options.IsDurationLimited
            ? _timeProvider.GetUtcNow() - sessionStart < options.Duration
            : cycle <= options.Cycles;

    private static int? GetTotalCycles(StressTestOptions options) =>
        options.IsDurationLimited ? null : options.Cycles;

    private static int? GetSessionTotalRequests(StressTestOptions options) =>
        options.IsDurationLimited ? null : options.RequestsPerInterval * options.Cycles;

    private async Task<RequestOutcome> SendAndReportAsync(
        StressTestOptions options,
        IReadOnlyList<string> payloads,
        string payload,
        int cycle,
        int request,
        int sessionRequestIndex,
        int? sessionTotalRequests,
        int? totalCycles,
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
            totalCycles,
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
        int? totalCycles,
        int request,
        string payload,
        int payloadCount,
        int sessionRequestIndex,
        int? sessionTotalRequests,
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
            totalCycles,
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
