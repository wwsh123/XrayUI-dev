using System.Text.Json;
using System.Text.Json.Nodes;
using XrayUI.Models;
using XrayUI.Services;
using Xunit;

namespace XrayUI.Tests;

public class MultiNodeRoutingTests
{
    [Fact]
    public void DedicatedPortSlotDisplay_ShowsPortBeforeServerName()
    {
        var server = new ServerEntry
        {
            Name = "Japan Dedicated",
            DedicatedPort = 10809
        };

        Assert.Equal("端口 10809 · Japan Dedicated", server.DedicatedPortSlotDisplay);
    }

    [Fact]
    public void Build_EnablesInboundTrafficStatsApi()
    {
        var server = new ServerEntry
        {
            Host = "example.com",
            Port = 443,
            Protocol = "vless",
            Uuid = "11111111-1111-1111-1111-111111111111"
        };
        var settings = new AppSettings { LocalMixedPort = 10808 };

        var doc = JsonNode.Parse(XrayConfigBuilder.Build(server, settings))!.AsObject();

        Assert.Equal("127.0.0.1:10809", doc["api"]!["listen"]!.GetValue<string>());
        Assert.Contains("StatsService", doc["api"]!["services"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.NotNull(doc["stats"]);
        Assert.True(doc["policy"]!["system"]!["statsInboundUplink"]!.GetValue<bool>());
        Assert.True(doc["policy"]!["system"]!["statsInboundDownlink"]!.GetValue<bool>());
    }

    [Fact]
    public void Build_Chain_UsesDialerProxyAndRetainsSockopt()
    {
        var entry = new ServerEntry { Id = "entry", Protocol = "vless", Host = "entry.example", Port = 443, Uuid = "e", Network = "ws", Path = "/ws" };
        var exit = new ServerEntry { Id = "exit", Protocol = "trojan", Host = "exit.example", Port = 443, Password = "p", Network = "tcp" };
        var chain = new ServerEntry { Protocol = "chain", ChainEntryServerId = entry.Id, ChainExitServerId = exit.Id };

        var doc = JsonNode.Parse(XrayConfigBuilder.Build(chain, new AppSettings(), [chain, entry, exit]))!.AsObject();
        var proxy = doc["outbounds"]!.AsArray().Single(node => node!["tag"]!.GetValue<string>() == "proxy")!.AsObject();

        Assert.Null(proxy["proxySettings"]);
        Assert.Equal("chain-entry", proxy["streamSettings"]!["sockopt"]!["dialerProxy"]!.GetValue<string>());
        Assert.Equal(15, proxy["streamSettings"]!["sockopt"]!["tcpKeepAliveIdle"]!.GetValue<int>());
    }

    [Fact]
    public void Build_ChainWithSameEndpoint_IsRejected()
    {
        var endpoint = new ServerEntry { Id = "same", Protocol = "vless", Host = "node", Port = 443, Uuid = "u" };
        var chain = new ServerEntry { Protocol = "chain", ChainEntryServerId = "same", ChainExitServerId = "same" };
        Assert.Throws<InvalidOperationException>(() => XrayConfigBuilder.Build(chain, new AppSettings(), [chain, endpoint]));
    }

    [Fact]
    public void Build_ChainWithMissingOrNestedEndpoint_IsRejected()
    {
        var endpoint = new ServerEntry { Id = "endpoint", Protocol = "vless", Host = "node", Port = 443, Uuid = "u" };
        var chain = new ServerEntry { Protocol = "chain", ChainEntryServerId = "missing", ChainExitServerId = endpoint.Id };
        Assert.Throws<InvalidOperationException>(() => XrayConfigBuilder.Build(chain, new AppSettings(), [chain, endpoint]));

        var nested = new ServerEntry { Id = "nested", Protocol = "chain", ChainEntryServerId = endpoint.Id, ChainExitServerId = endpoint.Id };
        chain.ChainEntryServerId = nested.Id;
        Assert.Throws<InvalidOperationException>(() => XrayConfigBuilder.Build(chain, new AppSettings(), [chain, nested, endpoint]));
    }

    [Theory]
    [InlineData("hysteria2", "udp")]
    [InlineData("wireguard", "tcp")]
    [InlineData("vless", "quic")]
    public void Build_ChainWithUnsupportedDialerTransport_IsRejected(string protocol, string network)
    {
        var entry = new ServerEntry { Id = "entry", Protocol = protocol, Network = network, Host = "entry", Port = 443, Uuid = "u" };
        var exit = new ServerEntry { Id = "exit", Protocol = "vless", Network = "tcp", Host = "exit", Port = 443, Uuid = "u" };
        var chain = new ServerEntry { Protocol = "chain", ChainEntryServerId = entry.Id, ChainExitServerId = exit.Id };

        Assert.Throws<InvalidOperationException>(() => XrayConfigBuilder.Build(chain, new AppSettings(), [chain, entry, exit]));
    }

    [Fact]
    public void Build_Alpn_DefaultIsOmitted_AndExplicitGrpcRequiresH2()
    {
        var server = new ServerEntry { Protocol = "vless", Host = "node", Port = 443, Uuid = "u", Security = "tls", Network = "tcp" };
        var defaultDoc = JsonNode.Parse(XrayConfigBuilder.Build(server, new AppSettings()))!.AsObject();
        Assert.Null(defaultDoc["outbounds"]![0]!["streamSettings"]!["tlsSettings"]!["alpn"]);

        server.Network = "grpc";
        server.Alpn = ["http/1.1"];
        Assert.Throws<InvalidOperationException>(() => XrayConfigBuilder.Build(server, new AppSettings()));
        server.Alpn = ["h2"];
        Assert.Equal("h2", JsonNode.Parse(XrayConfigBuilder.Build(server, new AppSettings()))!["outbounds"]![0]!["streamSettings"]!["tlsSettings"]!["alpn"]![0]!.GetValue<string>());
    }

    [Theory]
    [InlineData("raw", "tcp")]
    [InlineData("websocket", "ws")]
    [InlineData("splithttp", "xhttp")]
    public void Build_LegacyTransportAliases_AreNormalized(string input, string expected)
    {
        var server = new ServerEntry { Protocol = "vless", Host = "node", Port = 443, Uuid = "u", Security = "tls", Network = input };
        var stream = JsonNode.Parse(XrayConfigBuilder.Build(server, new AppSettings()))!["outbounds"]![0]!["streamSettings"]!;
        Assert.Equal(expected, stream!["network"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("h2")]
    [InlineData("http")]
    [InlineData("quic")]
    public void Build_RemovedTransport_IsRejected(string network)
    {
        var server = new ServerEntry { Protocol = "vless", Host = "node", Port = 443, Uuid = "u", Network = network };
        Assert.Throws<InvalidOperationException>(() => XrayConfigBuilder.Build(server, new AppSettings()));
    }

    [Fact]
    public void Build_HttpUpgradeAndXhttp_PreserveMappedFieldsAndUnknownExtra()
    {
        var upgrade = new ServerEntry { Protocol = "vless", Host = "node", Port = 443, Uuid = "u", Security = "tls", Network = "httpupgrade", Path = "/up", WsHost = "cdn.example" };
        var upgradeSettings = JsonNode.Parse(XrayConfigBuilder.Build(upgrade, new AppSettings()))!["outbounds"]![0]!["streamSettings"]!["httpupgradeSettings"]!;
        Assert.Equal("/up", upgradeSettings!["path"]!.GetValue<string>());
        Assert.Equal("cdn.example", upgradeSettings["host"]!.GetValue<string>());

        var xhttp = new ServerEntry { Protocol = "vless", Host = "node", Port = 443, Uuid = "u", Security = "tls", Network = "xhttp", Path = "/xh", WsHost = "cdn.example", XhttpMode = "stream-up", XhttpExtra = "{\"xPaddingBytes\":\"100-200\",\"futureField\":true}" };
        var xhttpSettings = JsonNode.Parse(XrayConfigBuilder.Build(xhttp, new AppSettings()))!["outbounds"]![0]!["streamSettings"]!["xhttpSettings"]!;
        Assert.Equal("stream-up", xhttpSettings!["mode"]!.GetValue<string>());
        Assert.True(xhttpSettings["extra"]!["futureField"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData("{bad", "")]
    [InlineData("", "{bad")]
    public void Build_InvalidXhttpExtraOrFinalmask_IsRejected(string xhttpExtra, string finalmask)
    {
        var server = new ServerEntry { Protocol = "vless", Host = "node", Port = 443, Uuid = "u", Security = "tls", Network = "xhttp", XhttpExtra = xhttpExtra, Finalmask = finalmask };
        Assert.Throws<InvalidOperationException>(() => XrayConfigBuilder.Build(server, new AppSettings()));
    }

    [Fact]
    public void ServerEntry_LegacyJsonWithoutAlpn_UsesEmptyDefault()
    {
        var server = JsonSerializer.Deserialize<ServerEntry>("{\"host\":\"node\",\"network\":\"raw\"}");
        Assert.NotNull(server);
        Assert.Empty(server.Alpn);
        Assert.Equal("tcp", XrayConfigBuilder.NormalizeNetwork(server.Network));
    }

    [Fact]
    public void Build_WhenMultiNodeDisabled_OnlyBuildsPrimaryInboundAndOutbound()
    {
        var mainServer = new ServerEntry
        {
            Name = "Hong Kong Main",
            Host = "hk.example.com",
            Port = 443,
            Protocol = "vless",
            Uuid = "11111111-1111-1111-1111-111111111111"
        };

        var auxServer = new ServerEntry
        {
            Name = "Japan Aux",
            Host = "jp.example.com",
            Port = 443,
            Protocol = "trojan",
            Password = "pass",
            DedicatedPort = 10809,
            IsDedicatedPortActive = true
        };

        var settings = new AppSettings
        {
            LocalMixedPort = 10808,
            EnableMultiNodeRouting = false,
            RoutingMode = "smart"
        };

        var json = XrayConfigBuilder.Build(mainServer, settings, new[] { mainServer, auxServer });
        var doc = JsonNode.Parse(json)!.AsObject();

        var inbounds = doc["inbounds"]!.AsArray();
        var outbounds = doc["outbounds"]!.AsArray();
        var routing = doc["routing"]!.AsObject();
        var rules = routing["rules"]!.AsArray();

        Assert.Single(inbounds);
        Assert.Equal(10808, inbounds[0]!["port"]!.GetValue<int>());
        Assert.Contains(outbounds, o => o!["tag"]!.GetValue<string>() == "proxy");
        Assert.DoesNotContain(outbounds, o => o!["tag"]!.GetValue<string>().StartsWith("outbound_dedicated_"));
        Assert.DoesNotContain(rules, r => r!["inboundTag"] != null && r["inboundTag"]!.ToString().Contains("inbound_dedicated_"));
    }

    [Fact]
    public void Build_WhenMultiNodeEnabled_BuildsDedicatedInboundsOutboundsAndRoutingRules()
    {
        var mainServer = new ServerEntry
        {
            Name = "Hong Kong Main",
            Host = "hk.example.com",
            Port = 443,
            Protocol = "vless",
            Uuid = "11111111-1111-1111-1111-111111111111"
        };

        var jpServer = new ServerEntry
        {
            Name = "Japan Dedicated",
            Host = "jp.example.com",
            Port = 443,
            Protocol = "trojan",
            Password = "pass",
            DedicatedPort = 10809,
            IsDedicatedPortActive = true
        };

        var sgServer = new ServerEntry
        {
            Name = "Singapore Dedicated",
            Host = "sg.example.com",
            Port = 443,
            Protocol = "shadowsocks",
            Password = "pass",
            DedicatedPort = 10810,
            IsDedicatedPortActive = true,
            AllowDedicatedLan = true
        };

        var inactiveServer = new ServerEntry
        {
            Name = "US Inactive",
            Host = "us.example.com",
            Port = 443,
            Protocol = "vless",
            DedicatedPort = 10811,
            IsDedicatedPortActive = false
        };

        var settings = new AppSettings
        {
            LocalMixedPort = 10808,
            EnableMultiNodeRouting = true,
            RoutingMode = "smart"
        };

        var json = XrayConfigBuilder.Build(mainServer, settings, new[] { mainServer, jpServer, sgServer, inactiveServer });
        var doc = JsonNode.Parse(json)!.AsObject();

        var inbounds = doc["inbounds"]!.AsArray();
        var outbounds = doc["outbounds"]!.AsArray();
        var rules = doc["routing"]!["rules"]!.AsArray();

        Assert.Equal(3, inbounds.Count);
        Assert.Contains(inbounds, i => i!["port"]!.GetValue<int>() == 10808 && i["listen"]!.GetValue<string>() == "127.0.0.1");
        Assert.Contains(inbounds, i => i!["port"]!.GetValue<int>() == 10809 && i["listen"]!.GetValue<string>() == "127.0.0.1");
        Assert.Contains(inbounds, i => i!["port"]!.GetValue<int>() == 10810 && i["listen"]!.GetValue<string>() == "0.0.0.0");
        Assert.Contains(outbounds, o => o!["tag"]!.GetValue<string>() == "proxy");
        Assert.Contains(outbounds, o => o!["tag"]!.GetValue<string>() == $"outbound_dedicated_{jpServer.Id}");
        Assert.Contains(outbounds, o => o!["tag"]!.GetValue<string>() == $"outbound_dedicated_{sgServer.Id}");
        Assert.DoesNotContain(outbounds, o => o!["tag"]!.GetValue<string>() == $"outbound_dedicated_{inactiveServer.Id}");

        var rule0 = rules[0]!.AsObject();
        var rule1 = rules[1]!.AsObject();

        Assert.Equal($"outbound_dedicated_{jpServer.Id}", rule0["outboundTag"]!.GetValue<string>());
        Assert.Equal("inbound_dedicated_10809", rule0["inboundTag"]![0]!.GetValue<string>());
        Assert.Equal($"outbound_dedicated_{sgServer.Id}", rule1["outboundTag"]!.GetValue<string>());
        Assert.Equal("inbound_dedicated_10810", rule1["inboundTag"]![0]!.GetValue<string>());
    }

    [Fact]
    public void Build_TunMode_BindsPrimaryAndAuxiliaryProxyToDifferentConfiguredInterfaces()
    {
        var mainServer = new ServerEntry
        {
            Host = "main.example.com",
            Port = 443,
            Protocol = "vless",
            Uuid = "11111111-1111-1111-1111-111111111111"
        };
        var auxServer = new ServerEntry
        {
            Host = "aux.example.com",
            Port = 443,
            Protocol = "trojan",
            Password = "pass",
            DedicatedPort = 10809,
            IsDedicatedPortActive = true,
            AuxiliaryOutboundInterface = "Ethernet 2"
        };
        var settings = new AppSettings
        {
            IsTunMode = true,
            EnableMultiNodeRouting = true,
            TunOutboundInterface = "Ethernet 1"
        };

        var doc = JsonNode.Parse(XrayConfigBuilder.Build(mainServer, settings, new[] { mainServer, auxServer }))!.AsObject();

        var primary = doc["outbounds"]!.AsArray()
            .Single(outbound => outbound!["tag"]!.GetValue<string>() == "proxy")!;
        var auxiliary = doc["outbounds"]!.AsArray()
            .Single(outbound => outbound!["tag"]!.GetValue<string>() == $"outbound_dedicated_{auxServer.Id}")!;

        Assert.Equal("Ethernet 1", primary["streamSettings"]!["sockopt"]!["interface"]!.GetValue<string>());
        Assert.Equal("Ethernet 2", auxiliary["streamSettings"]!["sockopt"]!["interface"]!.GetValue<string>());
    }

    [Fact]
    public void Build_TunMode_BindsAuxiliaryProxyToPrimaryInterfaceByDefault()
    {
        var mainServer = new ServerEntry
        {
            Host = "main.example.com",
            Port = 443,
            Protocol = "vless",
            Uuid = "11111111-1111-1111-1111-111111111111"
        };
        var auxServer = new ServerEntry
        {
            Host = "aux.example.com",
            Port = 443,
            Protocol = "trojan",
            Password = "pass",
            DedicatedPort = 10809,
            IsDedicatedPortActive = true
        };
        var settings = new AppSettings
        {
            IsTunMode = true,
            EnableMultiNodeRouting = true,
            TunOutboundInterface = "Ethernet 1"
        };

        var doc = JsonNode.Parse(XrayConfigBuilder.Build(mainServer, settings, new[] { mainServer, auxServer }))!.AsObject();

        var primary = doc["outbounds"]!.AsArray()
            .Single(outbound => outbound!["tag"]!.GetValue<string>() == "proxy")!;
        var auxiliary = doc["outbounds"]!.AsArray()
            .Single(outbound => outbound!["tag"]!.GetValue<string>() == $"outbound_dedicated_{auxServer.Id}")!;

        Assert.Equal("Ethernet 1", primary["streamSettings"]!["sockopt"]!["interface"]!.GetValue<string>());
        Assert.Equal("Ethernet 1", auxiliary["streamSettings"]!["sockopt"]!["interface"]!.GetValue<string>());
    }
}
