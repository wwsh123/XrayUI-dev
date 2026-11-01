using System.Text.Json;
using System.Text.Json.Nodes;
using XrayUI.Models;
using XrayUI.Services;

namespace XrayUI.Tests;

public class PresetSettingsTests
{
    [Fact]
    public void Export_IncludesSubscriptionsCustomRulesAndCloudflareAccounts()
    {
        var preset = new PresetSettings
        {
            Subscriptions =
            [
                new SubscriptionPresetEntry
                {
                    Id = "sub-1",
                    Name = "Main Subscription",
                    Group = "Alpha",
                    Url = "https://example.com/sub"
                }
            ],
            CustomRules =
            [
                new CustomRoutingRule
                {
                    Type = "process",
                    Process = ["chrome.exe"],
                    OutboundTag = "proxy"
                }
            ],
            CloudflareAccounts =
            [
                new CloudflareAccount
                {
                    Id = "cf-1",
                    Name = "Ops Account",
                    AccountTag = "abc123",
                    ApiToken = "token-123"
                }
            ],
            AdvancedRouting = new JsonObject
            {
                ["domainStrategy"] = "AsIs"
            }
        };

        var json = JsonSerializer.Serialize(preset, AppJsonSerializerContext.Default.PresetSettings);

        Assert.Contains("\"Subscriptions\"", json);
        Assert.Contains("\"CustomRules\"", json);
        Assert.Contains("\"CloudflareAccounts\"", json);

        using var document = JsonDocument.Parse(json);
        var account = document.RootElement
            .GetProperty("CloudflareAccounts")[0];
        Assert.Equal("abc123", account.GetProperty("AccountTag").GetString());
        Assert.Equal("token-123", account.GetProperty("ApiToken").GetString());
    }
}
