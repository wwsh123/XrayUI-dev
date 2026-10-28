using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;
using XrayUI.Helpers;

namespace XrayUI.Services;

/// <summary>
/// Owns only the three WinInet values written by this application. The prior state is persisted
/// before the first successful takeover and is restored only if no other program changed the
/// values afterwards. Environment proxy variables are intentionally outside this service.
/// </summary>
public static class ProxyOwnershipService
{
    private const string RegPath = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";
    private static readonly string StatePath = Path.Combine(AppPaths.LocalAppDataDir, "system_proxy_ownership.json");
    private const string Override = "localhost;127.*;10.*;172.16.*;172.17.*;172.18.*;172.19.*;172.20.*;172.21.*;172.22.*;172.23.*;172.24.*;172.25.*;172.26.*;172.27.*;172.28.*;172.29.*;172.30.*;172.31.*;192.168.*;<local>";

    [DllImport("wininet.dll", SetLastError = true)]
    private static extern bool InternetSetOption(IntPtr hInternet, int option, IntPtr buffer, int length);

    public static bool SetProxy(string host, int port)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegPath, writable: true)
                ?? throw new InvalidOperationException("无法打开注册表项：" + RegPath);
            return new OwnershipCoordinator(new RegistryValueStore(key), new JsonStateStore(), NotifyWindows)
                .SetProxy(host, port);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SystemProxy] owned SetProxy failed: {ex.Message}");
            return false;
        }
    }

    public static bool ClearProxy()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegPath, writable: true)
                ?? throw new InvalidOperationException("无法打开注册表项：" + RegPath);
            return new OwnershipCoordinator(new RegistryValueStore(key), new JsonStateStore(), NotifyWindows)
                .ClearProxy();
        }
        catch (Exception ex)
        {
            // Keep the state file: a later explicit Clear can retry a partial restore safely.
            Debug.WriteLine($"[SystemProxy] owned ClearProxy failed: {ex.Message}");
            return false;
        }
    }

    private static ProxyState Capture(RegistryKey key) => new(CaptureValues(key), new());
    private static Dictionary<string, ProxyValue> CaptureValues(RegistryKey key)
    {
        var values = new Dictionary<string, ProxyValue>(StringComparer.Ordinal);
        foreach (var name in new[] { "ProxyEnable", "ProxyServer", "ProxyOverride" })
        {
            var value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            values[name] = value is null ? new(null, RegistryValueKind.Unknown) : new(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture), key.GetValueKind(name));
        }
        return values;
    }
    private static bool Matches(RegistryKey key, string name, ProxyValue expected)
    {
        var actual = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return string.Equals(Convert.ToString(actual, System.Globalization.CultureInfo.InvariantCulture), expected.Value, StringComparison.Ordinal)
            && (actual is null || key.GetValueKind(name) == expected.Kind);
    }
    private static void Restore(RegistryKey key, string name, ProxyValue value)
    {
        if (value.Value is null) key.DeleteValue(name, false);
        else key.SetValue(name, value.Kind == RegistryValueKind.DWord ? int.Parse(value.Value!, System.Globalization.CultureInfo.InvariantCulture) : value.Value, value.Kind);
    }
    private static void TryRecoverPartialWrite(ProxyState state, Dictionary<string, ProxyValue> desired)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegPath, writable: true);
            if (key is null) return;
            foreach (var pair in desired)
                if (Matches(key, pair.Key, pair.Value)) Restore(key, pair.Key, state.Original[pair.Key]);
            key.Flush();
            NotifyWindows();
        }
        catch (Exception ex) { Debug.WriteLine($"[SystemProxy] partial-write recovery failed: {ex.Message}"); }
    }
    private static ProxyState? ReadState()
    {
        try
        {
            return File.Exists(StatePath)
                ? JsonSerializer.Deserialize(File.ReadAllText(StatePath), ProxyOwnershipJsonContext.Default.ProxyState)
                : null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SystemProxy] state read failed: {ex.Message}");
            return null;
        }
    }
    private static void WriteState(ProxyState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
        var temporary = StatePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(state, ProxyOwnershipJsonContext.Default.ProxyState));
        File.Move(temporary, StatePath, true);
    }
    private static void NotifyWindows()
    {
        InternetSetOption(IntPtr.Zero, 39, IntPtr.Zero, 0);
        InternetSetOption(IntPtr.Zero, 37, IntPtr.Zero, 0);
    }

    // Kept independent from Win32 and the filesystem so the ownership contract can be tested
    // against in-memory registry/state substitutes. The public static API above remains the
    // sole production entry point.
    internal interface IValueStore
    {
        ProxyValue Get(string name);
        void Set(string name, ProxyValue value);
        void Delete(string name);
        void Flush();
    }

    internal interface IStateStore
    {
        ProxyState? Read();
        void Write(ProxyState state);
        void Delete();
    }

    internal sealed class OwnershipCoordinator(IValueStore values, IStateStore states, Action notify)
    {
        private static readonly string[] ValueNames = ["ProxyEnable", "ProxyServer", "ProxyOverride"];

        public bool SetProxy(string host, int port)
        {
            var original = states.Read()?.Original ?? CaptureValues();
            var desired = new Dictionary<string, ProxyValue>(StringComparer.Ordinal)
            {
                ["ProxyEnable"] = new("1", RegistryValueKind.DWord),
                ["ProxyServer"] = new($"{host}:{port}", RegistryValueKind.String),
                ["ProxyOverride"] = new(Override, RegistryValueKind.String)
            };

            try
            {
                // State persistence precedes every mutation, preserving crash recovery.
                states.Write(new ProxyState(original, desired));
                foreach (var pair in desired) values.Set(pair.Key, pair.Value);
                values.Flush();
                notify();
                return true;
            }
            catch
            {
                TryRecoverPartialWrite(original, desired);
                return false;
            }
        }

        public bool ClearProxy()
        {
            var state = states.Read();
            if (state is null) return true;
            // A failed takeover or a partially completed restore may already have put some
            // values back to their original state. Both are safe recovery states; any third
            // value belongs to somebody else, so leave every value and the state file alone.
            if (ValueNames.Any(name => !Matches(name, state.LastWritten[name])
                                       && !Matches(name, state.Original[name])))
                return false;

            try
            {
                foreach (var pair in state.Original) Restore(pair.Key, pair.Value);
                values.Flush();
                states.Delete();
                notify();
                return true;
            }
            catch
            {
                // Keep recovery state so an explicit retry remains safe.
                return false;
            }
        }

        private Dictionary<string, ProxyValue> CaptureValues() => ValueNames.ToDictionary(name => name, values.Get, StringComparer.Ordinal);
        private bool Matches(string name, ProxyValue expected) => values.Get(name) == expected;
        private void Restore(string name, ProxyValue value)
        {
            if (value.Value is null) values.Delete(name);
            else values.Set(name, value);
        }
        private void TryRecoverPartialWrite(Dictionary<string, ProxyValue> original, Dictionary<string, ProxyValue> desired)
        {
            try
            {
                foreach (var pair in desired)
                    if (Matches(pair.Key, pair.Value)) Restore(pair.Key, original[pair.Key]);
                values.Flush();
                notify();
            }
            catch { }
        }
    }

    private sealed class RegistryValueStore(RegistryKey key) : IValueStore
    {
        public ProxyValue Get(string name)
        {
            var value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            return value is null
                ? new ProxyValue(null, RegistryValueKind.Unknown)
                : new ProxyValue(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture), key.GetValueKind(name));
        }

        public void Set(string name, ProxyValue value) => key.SetValue(
            name,
            value.Kind == RegistryValueKind.DWord ? int.Parse(value.Value!, System.Globalization.CultureInfo.InvariantCulture) : value.Value!,
            value.Kind);
        public void Delete(string name) => key.DeleteValue(name, false);
        public void Flush() => key.Flush();
    }

    private sealed class JsonStateStore : IStateStore
    {
        public ProxyState? Read() => ReadState();
        public void Write(ProxyState state) => WriteState(state);
        public void Delete() => File.Delete(StatePath);
    }

    public sealed record ProxyValue(string? Value, RegistryValueKind Kind);
    public sealed record ProxyState(Dictionary<string, ProxyValue> Original, Dictionary<string, ProxyValue> LastWritten);
}

[System.Text.Json.Serialization.JsonSerializable(typeof(ProxyOwnershipService.ProxyState))]
internal partial class ProxyOwnershipJsonContext : System.Text.Json.Serialization.JsonSerializerContext { }
