using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using XrayUI.Models;

namespace XrayUI.Services
{
    public sealed class PresetImportService
    {
        private readonly SettingsService _settings;

        public PresetImportService(SettingsService settings)
        {
            _settings = settings;
        }

        public static bool PresetExists() =>
            File.Exists(PresetPaths.ServersFile) || File.Exists(PresetPaths.SettingsFile);

        public async Task<PresetImportResult> ApplyAsync()
        {
            var importedServers = await TryReplaceServersAsync().ConfigureAwait(false);
            var settingsResult = await TryReplaceSettingsAsync().ConfigureAwait(false);

            return new PresetImportResult(
                importedServers,
                settingsResult.Subscriptions,
                settingsResult.CustomRules,
                settingsResult.AdvancedRouting,
                settingsResult.CloudflareAccounts);
        }

        public async Task<PresetImportResult> ApplyJsonAsync(string json)
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                var servers = JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.ListServerEntry) ?? new List<ServerEntry>();
                var importedArrayServers = await TryReplaceServersAsync(servers).ConfigureAwait(false);
                return new PresetImportResult(importedArrayServers, 0, 0, false, 0);
            }

            var preset = JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.PresetSettings)
                ?? new PresetSettings();

            var importedPresetServers = await TryReplaceServersAsync(preset.Servers).ConfigureAwait(false);
            var settingsResult = await TryReplaceSettingsAsync(preset).ConfigureAwait(false);

            return new PresetImportResult(
                importedPresetServers,
                settingsResult.Subscriptions,
                settingsResult.CustomRules,
                settingsResult.AdvancedRouting,
                settingsResult.CloudflareAccounts);
        }

        private async Task<int> TryReplaceServersAsync(List<ServerEntry>? servers = null)
        {
            List<ServerEntry>? preset;
            if (servers is not null)
            {
                preset = servers;
            }
            else if (File.Exists(PresetPaths.ServersFile))
            {
                preset = await PresetReader.ReadRequiredJsonAsync(
                    PresetPaths.ServersFile,
                    AppJsonSerializerContext.Default.ListServerEntry,
                    "PresetImport").ConfigureAwait(false);
            }
            else
            {
                return 0;
            }

            await _settings.SaveServersAsync(preset).ConfigureAwait(false);
            return preset.Count;
        }

        // Replace semantics: a missing field in the preset clears the existing value
        // (in contrast to InitialImportService's merge-if-empty behavior).
        private async Task<(int Subscriptions, int CustomRules, bool AdvancedRouting, int CloudflareAccounts)> TryReplaceSettingsAsync(PresetSettings? preset = null)
        {
            if (preset is null)
            {
                if (!File.Exists(PresetPaths.SettingsFile))
                    return (0, 0, false, 0);

                preset = await PresetReader.ReadRequiredJsonAsync(
                    PresetPaths.SettingsFile,
                    AppJsonSerializerContext.Default.PresetSettings,
                    "PresetImport").ConfigureAwait(false);
            }

            var target = await _settings.LoadSettingsAsync().ConfigureAwait(false);
            var incomingSettings = preset.Settings is not null ? CloneSettings(preset.Settings) : null;
            if (incomingSettings is not null)
            {
                target = incomingSettings;
            }

            if (preset.Subscriptions is { Count: > 0 })
            {
                target.Subscriptions = preset.Subscriptions
                    .Select(subscription => subscription.ToSubscription())
                    .ToList();
            }
            else if (incomingSettings is not null && incomingSettings.Subscriptions is { Count: > 0 })
            {
                target.Subscriptions = incomingSettings.Subscriptions;
            }
            else
            {
                target.Subscriptions = null;
            }

            if (preset.CustomRules is { Count: > 0 })
            {
                target.CustomRules = preset.CustomRules.ToList();
            }
            else if (incomingSettings is not null && incomingSettings.CustomRules is { Count: > 0 })
            {
                target.CustomRules = incomingSettings.CustomRules;
            }
            else
            {
                target.CustomRules = null;
            }

            if (preset.AdvancedRouting is not null)
            {
                target.AdvancedRouting = preset.AdvancedRouting.DeepClone() as JsonObject;
            }
            else if (incomingSettings is not null)
            {
                target.AdvancedRouting = incomingSettings.AdvancedRouting?.DeepClone() as JsonObject;
            }
            else
            {
                target.AdvancedRouting = null;
            }

            if (preset.CloudflareAccounts is { Count: > 0 })
            {
                target.CloudflareAccounts = preset.CloudflareAccounts.ToList();
            }
            else if (incomingSettings is not null && incomingSettings.CloudflareAccounts.Count > 0)
            {
                target.CloudflareAccounts = incomingSettings.CloudflareAccounts;
            }
            else
            {
                target.CloudflareAccounts = new List<CloudflareAccount>();
            }

            await _settings.SaveSettingsAsync(target).ConfigureAwait(false);

            return (
                target.Subscriptions?.Count ?? 0,
                target.CustomRules?.Count ?? 0,
                target.AdvancedRouting is not null,
                target.CloudflareAccounts.Count);
        }

        private static AppSettings CloneSettings(AppSettings settings)
        {
            var json = JsonSerializer.Serialize(settings, AppJsonSerializerContext.Default.AppSettings);
            return JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.AppSettings) ?? new AppSettings();
        }
    }

    public sealed record PresetImportResult(
        int ImportedServers,
        int ImportedSubscriptions,
        int ImportedCustomRules,
        bool ImportedAdvancedRouting,
        int ImportedCloudflareAccounts);
}
