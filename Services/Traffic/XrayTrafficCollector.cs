using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using XrayUI.Models.Traffic;

namespace XrayUI.Services.Traffic;

/// <summary>Polls Xray's local StatsService through one persistent gRPC client per session.</summary>
public sealed class XrayTrafficCollector : IAsyncDisposable
{
    private int _apiPort;
    private readonly TrafficMonitorStore _store;
    private readonly IXrayAccessEventParser _accessParser = new XrayAccessLogParser();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly object _gate = new();
    private CancellationTokenSource? _sessionCancellation;
    private Task? _pollTask;
    private TrafficCoreSession? _session;

    public XrayTrafficCollector(TrafficMonitorStore store, int apiPort)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        if (apiPort is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(apiPort));
        _apiPort = apiPort;
    }

    public void StartSession(int apiPort = 0)
    {
        lock (_gate)
        {
            if (apiPort is > 0)
            {
                if (apiPort > 65535) throw new ArgumentOutOfRangeException(nameof(apiPort));
                _apiPort = apiPort;
            }
            StopSessionLocked();
            var session = new TrafficCoreSession(Guid.NewGuid(),
                new Uri($"http://127.0.0.1:{_apiPort}"), DateTimeOffset.UtcNow);
            _session = session;
            _store.BeginSession(session);
            _sessionCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            _pollTask = PollAsync(session, new XrayStatsClient(session.StatsEndpoint), _sessionCancellation.Token);
        }
    }

    public void StopSession()
    {
        lock (_gate) StopSessionLocked();
    }

    public void RecordLogLine(string line)
    {
        TrafficCoreSession? session;
        lock (_gate) session = _session;
        if (session is null) return;
        var access = _accessParser.Parse(line, DateTimeOffset.UtcNow);
        if (access is not null) _store.RecordAccess(session.Id, access);
    }

    private void StopSessionLocked()
    {
        var session = _session;
        _session = null;
        _sessionCancellation?.Cancel();
        _sessionCancellation?.Dispose();
        _sessionCancellation = null;
        _pollTask = null;
        if (session is not null) _store.EndSession(session.Id);
    }

    private async Task PollAsync(TrafficCoreSession session, IXrayStatsClient statsClient,
        CancellationToken cancellationToken)
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            while (!cancellationToken.IsCancellationRequested)
            {
                var elapsed = stopwatch.Elapsed;
                var observedAt = DateTimeOffset.UtcNow;
                try
                {
                    var counters = await statsClient.QueryCountersAsync(cancellationToken).ConfigureAwait(false);
                    if (counters.IsDefaultOrEmpty)
                        _store.RecordUnavailable(session.Id, elapsed, observedAt);
                    else
                        _store.RecordCounters(new(session.Id, elapsed, observedAt, counters));
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
                catch (Exception)
                {
                    _store.RecordUnavailable(session.Id, elapsed, observedAt);
                }
                try { await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            }
        }
        finally
        {
            try { await statsClient.DisposeAsync().ConfigureAwait(false); }
            catch { }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _lifetime.Cancel();
        lock (_gate) StopSessionLocked();
        try { if (_pollTask is not null) await _pollTask.ConfigureAwait(false); } catch { }
        _lifetime.Dispose();
    }
}
