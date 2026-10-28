using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace XrayUI.Services;

/// <summary>Produces the narrowly-owned TUN cleanup commands without executing them.</summary>
internal static class TunCleanupCommands
{
    internal static List<string> Build(string interfaceName, string? serverAddress)
    {
        var commands = new List<string>
        {
            $"netsh interface ipv4 delete route {XrayConfigConstants.TunAutoRouteV4} \"{interfaceName}\" store=active",
            $"netsh interface ipv4 delete route 0.0.0.0/1 \"{interfaceName}\" store=active",
            $"netsh interface ipv4 delete route 128.0.0.0/1 \"{interfaceName}\" store=active",
            $"netsh interface ipv6 delete route {XrayConfigConstants.TunAutoRouteV6} \"{interfaceName}\" store=active",
        };

        if (IPAddress.TryParse(serverAddress, out var parsed) && parsed.AddressFamily == AddressFamily.InterNetwork)
            commands.Add($"netsh interface ipv4 delete route {parsed}/32 \"{interfaceName}\" store=active");

        commands.AddRange([
            $"netsh interface ipv4 set dnsservers \"{interfaceName}\" source=dhcp",
            $"netsh interface ipv6 set dnsservers \"{interfaceName}\" source=dhcp",
        ]);
        return commands;
    }
}
