using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using XrayUI.Models;

namespace XrayUI.Services
{
    public class PresetExportService
    {
        private readonly SettingsService _settings;

        public PresetExportService(SettingsService settings)
        {
            _settings = settings;
        }

        public async Task<string> ExportJsonAsync()
        {
            var servers = await _settings.LoadServersAsync().ConfigureAwait(false);
            var settings = await _settings.LoadSettingsAsync().ConfigureAwait(false);

            var preset = new PresetSettings
            {
                Servers = servers.Count > 0 ? servers.ToList() : null,
                Subscriptions = settings.Subscriptions is { Count: > 0 }
                    ? settings.Subscriptions
                        .Select(SubscriptionPresetEntry.FromSubscription)
                        .ToList()
                    : null,
                CustomRules = settings.CustomRules is { Count: > 0 }
                    ? settings.CustomRules.ToList()
                    : null,
                AdvancedRouting = settings.AdvancedRouting?.DeepClone() as JsonObject,
                CloudflareAccounts = settings.CloudflareAccounts is { Count: > 0 }
                    ? settings.CloudflareAccounts.ToList()
                    : null,
                Settings = CloneSettings(settings),
            };

            return JsonSerializer.Serialize(
                preset,
                AppJsonSerializerContext.Readable<PresetSettings>());
        }

        public async Task<string> ExportAsync()
        {
            Directory.CreateDirectory(PresetPaths.Dir);

            var servers = await _settings.LoadServersAsync().ConfigureAwait(false);
            var settings = await _settings.LoadSettingsAsync().ConfigureAwait(false);

            var preset = new PresetSettings
            {
                Servers = servers.Count > 0 ? servers.ToList() : null,
                Subscriptions = settings.Subscriptions is { Count: > 0 }
                    ? settings.Subscriptions
                        .Select(SubscriptionPresetEntry.FromSubscription)
                        .ToList()
                    : null,
                CustomRules = settings.CustomRules is { Count: > 0 }
                    ? settings.CustomRules.ToList()
                    : null,
                AdvancedRouting = settings.AdvancedRouting?.DeepClone() as JsonObject,
                CloudflareAccounts = settings.CloudflareAccounts is { Count: > 0 }
                    ? settings.CloudflareAccounts.ToList()
                    : null,
                Settings = CloneSettings(settings),
            };

            var serversJson = JsonSerializer.Serialize(
                servers,
                AppJsonSerializerContext.Readable<List<ServerEntry>>());
            await File.WriteAllTextAsync(PresetPaths.ServersFile, serversJson).ConfigureAwait(false);

            var settingsJson = JsonSerializer.Serialize(
                preset,
                AppJsonSerializerContext.Readable<PresetSettings>());
            await File.WriteAllTextAsync(PresetPaths.SettingsFile, settingsJson).ConfigureAwait(false);

            return PresetPaths.Dir;
        }

        private static AppSettings? CloneSettings(AppSettings settings)
        {
            var json = JsonSerializer.Serialize(settings, AppJsonSerializerContext.Default.AppSettings);
            return JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.AppSettings);
        }
    }
}
