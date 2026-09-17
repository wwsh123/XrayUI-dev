using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace XrayUI.Models
{
    public class PresetSettings
    {
        public List<ServerEntry>? Servers { get; set; }
        public List<SubscriptionPresetEntry>? Subscriptions { get; set; }
        public List<CustomRoutingRule>? CustomRules { get; set; }
        public JsonObject? AdvancedRouting { get; set; }
        public List<CloudflareAccount>? CloudflareAccounts { get; set; }

        // Full portable snapshot of the current persisted settings, including non-routing
        // and UI preferences that are not covered by the individual preset buckets above.
        public AppSettings? Settings { get; set; }
    }
}
