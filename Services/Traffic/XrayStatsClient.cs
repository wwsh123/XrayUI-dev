using System;
using System.Collections.Immutable;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Xray.App.Stats.Command;
using XrayUI.Models.Traffic;

namespace XrayUI.Services.Traffic;

/// <summary>Reads Xray counters over one persistent local HTTP/2 channel per core session.</summary>
public sealed class XrayStatsClient : IXrayStatsClient
{
    private readonly GrpcChannel _channel;
    private readonly StatsService.StatsServiceClient _client;

    public XrayStatsClient(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (!endpoint.IsLoopback || endpoint.Scheme != Uri.UriSchemeHttp)
            throw new ArgumentException("Stats endpoint must be local HTTP.", nameof(endpoint));

        var handler = new SocketsHttpHandler
        {
            UseProxy = false,
            EnableMultipleHttp2Connections = true
        };
        _channel = GrpcChannel.ForAddress(endpoint, new GrpcChannelOptions { HttpHandler = handler });
        _client = new StatsService.StatsServiceClient(_channel);
    }

    public async Task<ImmutableArray<TrafficCounter>> QueryCountersAsync(CancellationToken cancellationToken)
    {
        var response = await _client.QueryStatsAsync(
            new QueryStatsRequest { Pattern = string.Empty, Reset = false },
            cancellationToken: cancellationToken).ResponseAsync.ConfigureAwait(false);

        var counters = ImmutableArray.CreateBuilder<TrafficCounter>(response.Stat.Count);
        foreach (var stat in response.Stat)
        {
            var parts = stat.Name.Split(">>>", StringSplitOptions.None);
            if (parts.Length != 4 || parts[2] != "traffic") continue;
            var scope = parts[0] switch
            {
                "inbound" => TrafficCounterScope.Inbound,
                "outbound" => TrafficCounterScope.Outbound,
                _ => (TrafficCounterScope?)null
            };
            var direction = parts[3] switch
            {
                "uplink" => TrafficDirection.Upload,
                "downlink" => TrafficDirection.Download,
                _ => (TrafficDirection?)null
            };
            if (scope.HasValue && direction.HasValue)
                counters.Add(new(new(scope.Value, parts[1], direction.Value), Math.Max(0, stat.Value)));
        }
        return counters.ToImmutable();
    }

    public ValueTask DisposeAsync()
    {
        _channel.Dispose();
        return ValueTask.CompletedTask;
    }
}
