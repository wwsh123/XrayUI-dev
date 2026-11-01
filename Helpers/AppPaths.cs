using System;
using System.IO;

namespace XrayUI.Helpers
{
    public static class AppPaths
    {
        public static string DataDir { get; } = Path.Combine(AppContext.BaseDirectory, "Data");

        // Keep LocalAppDataDir pointing to DataDir for full backward compatibility across the codebase
        public static string LocalAppDataDir => DataDir;

        public static string UpdatesDir { get; } = Path.Combine(DataDir, "Updates");
        public static string XrayConfigPreviewPath { get; } = Path.Combine(DataDir, "xray-config-preview.json");

        public static string SettingsJsonPath { get; } = Path.Combine(DataDir, "settings.json");
        public static string ServersJsonPath { get; } = Path.Combine(DataDir, "servers.json");
        public static string ServersJson => ServersJsonPath;
        public static string ProfilesDir { get; } = Path.Combine(DataDir, "profiles");
        public static string ProxyProfilePath { get; } = Path.Combine(ProfilesDir, "proxy.json");
        public static string TunProfilePath { get; } = Path.Combine(ProfilesDir, "tun.json");
        public static string RecentConnectionsPath { get; } = Path.Combine(DataDir, "recent-connections.json");
        public static string XrayConfigPath { get; } = Path.Combine(DataDir, "xray_config.json");
        public static string XraySpeedtestConfigPath { get; } = Path.Combine(DataDir, "xray_speedtest.json");

        static AppPaths()
        {
            try
            {
                if (!Directory.Exists(DataDir))
                {
                    Directory.CreateDirectory(DataDir);
                }
            }
            catch
            {
                // Best effort directory creation
            }
        }
    }
}
