using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using XrayUI.Helpers;
using XrayUI.Models;

namespace XrayUI.Services
{
    public class SettingsService
    {
        private static readonly string DataDir = AppPaths.LocalAppDataDir;

        private static readonly string SettingsFile = AppPaths.SettingsJsonPath;
        private static readonly string ServersFile  = Path.Combine(DataDir, "servers.json");

        private AppSettings? _cachedSettings;
        private readonly SerializedFileSaver _settingsSaver;
        private readonly SettingsCacheEpoch _settingsEpoch = new();

        public SettingsService()
        {
            Directory.CreateDirectory(DataDir);
            _settingsSaver = new SerializedFileSaver(WriteAtomicAsync);
        }

        /// <summary>Drop the in-memory cache so the next LoadSettingsAsync re-reads the file.
        /// Used when an external process (e.g. the user's text editor) may have modified
        /// settings.json on disk.</summary>
        public void InvalidateCache() => _cachedSettings = null;

        /// <summary>Invalidate the cache and reload from disk in one call.</summary>
        public async Task<AppSettings> ReloadAsync()
        {
            InvalidateCache();
            return await LoadSettingsAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Drop the cache and shell-open settings.json in the user's default .json editor.
        /// Cache is dropped first so subsequent reads pick up whatever the editor writes.
        /// Throws if the OS reports no association for .json.
        /// </summary>
        public void OpenInExternalEditor()
        {
            InvalidateCache();
            Process.Start(new ProcessStartInfo
            {
                FileName = SettingsFile,
                UseShellExecute = true,
            });
        }

        // ── AppSettings ───────────────────────────────────────────────────────

        public async Task<AppSettings> LoadSettingsAsync()
        {
            if (_cachedSettings is not null)
                return _cachedSettings;

            var generation = _settingsEpoch.Capture();

            try
            {
                if (!File.Exists(SettingsFile))
                {
                    var created = new AppSettings { RoutingRegion = InferDefaultRoutingRegion() };
                    if (_settingsEpoch.IsCurrent(generation)) _cachedSettings = created;
                    return _cachedSettings ?? created;
                }

                var json = await File.ReadAllTextAsync(SettingsFile).ConfigureAwait(false);
                AppSettings loaded;
                try
                {
                    loaded = JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.AppSettings) ?? new AppSettings();
                }
                catch (JsonException ex)
                {
                    QuarantineInvalidJson(SettingsFile, ex);
                    var recovered = new AppSettings { RoutingRegion = InferDefaultRoutingRegion() };
                    if (_settingsEpoch.IsCurrent(generation)) _cachedSettings = recovered;
                    return _cachedSettings ?? recovered;
                }

                // One-time migration: settings.json written before XrayLogLevel existed only has
                // the legacy VerboseXrayLog bool. Populate the new field so every other reader
                // can trust it's non-null instead of re-deriving the fallback on every read.
                loaded.XrayLogLevel ??= loaded.VerboseXrayLog ? XrayLogLevel.Info : XrayLogLevel.Warning;
                if (_settingsEpoch.IsCurrent(generation)) _cachedSettings = loaded;
                return _cachedSettings ?? loaded;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsService] Failed to load settings: {ex.Message}");
                return new AppSettings();
            }
        }

        /// <summary>
        /// First-run default for <see cref="AppSettings.RoutingRegion"/>. Deliberately narrow:
        /// only an explicit RU/IR Windows home region deviates from "cn" — geosite ships domestic
        /// lists for cn/ru/ir only, and system locale/region is a weak signal for everyone else
        /// (many zh users run en-US Windows or set region to US on VPS/VMs).
        /// </summary>
        private static string InferDefaultRoutingRegion()
        {
            try
            {
                var region = Windows.System.UserProfile.GlobalizationPreferences.HomeGeographicRegion;
                if (string.Equals(region, "RU", StringComparison.OrdinalIgnoreCase)) return "ru";
                if (string.Equals(region, "IR", StringComparison.OrdinalIgnoreCase)) return "ir";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsService] Region inference failed: {ex.Message}");
            }
            return "cn";
        }

        public async Task SaveSettingsAsync(AppSettings settings)
        {
            // Serialize immediately so this request owns an immutable on-disk snapshot even if
            // the caller continues editing its observable settings object while waiting.
            var json = JsonSerializer.Serialize(settings, AppJsonSerializerContext.Readable<AppSettings>());
            _settingsEpoch.BeginSave();
            await _settingsSaver.SaveAsync(SettingsFile, json, () =>
            {
                // A load that began while this save was queued can only have observed the
                // previous file, so invalidate it at the disk-commit boundary as well.
                _settingsEpoch.CompleteSave();
                _cachedSettings = settings;
            }).ConfigureAwait(false);
        }

        // ── Server list ───────────────────────────────────────────────────────

        public async Task<List<ServerEntry>> LoadServersAsync()
        {
            try
            {
                if (!File.Exists(ServersFile))
                    return new List<ServerEntry>();

                var json = await File.ReadAllTextAsync(ServersFile).ConfigureAwait(false);
                List<ServerEntry> list;
                try
                {
                    list = JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.ListServerEntry) ?? [];
                }
                catch (JsonException ex)
                {
                    QuarantineInvalidJson(ServersFile, ex);
                    return [];
                }

                // Persist once if legacy JSON has no Id keys, so field-initializer-generated
                // Ids don't regenerate on every launch and break LastAutoConnectServerId.
                if (list.Count > 0 && !json.Contains("\"Id\":", StringComparison.Ordinal))
                    await SaveServersAsync(list).ConfigureAwait(false);

                return list;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsService] Failed to load servers: {ex.Message}");
                return [];
            }
        }

        public async Task SaveServersAsync(IEnumerable<ServerEntry> servers)
        {
            var serverList = servers as List<ServerEntry> ?? servers.ToList();
            var json = JsonSerializer.Serialize(serverList, AppJsonSerializerContext.Readable<List<ServerEntry>>());
            await WriteAtomicAsync(ServersFile, json).ConfigureAwait(false);
        }

        // Write-to-temp + atomic swap: a crash or power cut mid-save can never leave a
        // truncated settings/servers file — the previous complete file survives until the
        // replace commits. Temp name is per-call (Guid-suffixed) so concurrent saves of the
        // same file (e.g. two VMs persisting settings.json close together) never write over
        // each other's temp file or race on the replace.
        private static async Task WriteAtomicAsync(string path, string contents)
        {
            var tmp = $"{path}.{Guid.NewGuid():N}.tmp";
            try
            {
                await File.WriteAllTextAsync(tmp, contents).ConfigureAwait(false);

                if (File.Exists(path))
                    File.Replace(tmp, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
                else
                    File.Move(tmp, path);
            }
            catch
            {
                try { File.Delete(tmp); } catch { }
                throw;
            }
        }

        private static void QuarantineInvalidJson(string path, JsonException error)
        {
            try
            {
                var backup = $"{path}.invalid-{DateTime.UtcNow:yyyyMMddHHmmss}.json";
                File.Move(path, backup);
                Debug.WriteLine($"[SettingsService] Invalid configuration moved to '{backup}': {error.Message}");
            }
            catch (Exception moveError)
            {
                Debug.WriteLine($"[SettingsService] Failed to preserve invalid configuration '{path}': {moveError.Message}");
            }
        }
    }
}
