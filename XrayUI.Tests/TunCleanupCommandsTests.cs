using XrayUI.Services;

namespace XrayUI.Tests;

public class TunCleanupCommandsTests
{
    [Fact]
    public void Build_OnlyTargetsOwnedInterfaceAndLiteralIpv4ServerRoute()
    {
        var commands = TunCleanupCommands.Build("xray-tun", "203.0.113.7");

        Assert.All(commands, command => Assert.Contains("\"xray-tun\"", command));
        Assert.Contains(commands, command => command.Contains("203.0.113.7/32", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.StartsWith("route delete", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("proxy.example.com")]
    [InlineData("2001:db8::1")]
    [InlineData("")]
    public void Build_DoesNotInventServerRouteForUnconfirmedAddress(string serverAddress)
    {
        var commands = TunCleanupCommands.Build("xray-tun", serverAddress);

        Assert.DoesNotContain(commands, command => command.Contains("/32", StringComparison.Ordinal));
    }
}
