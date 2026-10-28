using System;
using System.IO;
using System.Text.RegularExpressions;
using XrayUI.Helpers;

namespace XrayUI.Services;

/// <summary>Best-effort, size-bounded lifecycle diagnostics. Never logs full configs or URLs.</summary>
public static partial class DiagnosticLogService
{
    private const long MaxBytes = 5 * 1024 * 1024;
    private const int MaxFiles = 5;
    private static readonly object Gate = new();
    private static readonly string DirectoryPath = Path.Combine(AppPaths.LocalAppDataDir, "diagnostics");
    private static readonly string ActivePath = Path.Combine(DirectoryPath, "xrayui.log");
    private static readonly DiagnosticLogWriter Writer = new(new FileDiagnosticLogStore(), DirectoryPath, MaxBytes, MaxFiles, () => DateTimeOffset.UtcNow);

    public static void Write(string category, string message)
    {
        try
        {
            lock (Gate)
            {
                Writer.TryWrite(category, message);
            }
        }
        catch { /* diagnostics must never alter proxy availability */ }
    }

    public static bool Export(string destinationPath)
    {
        try
        {
            lock (Gate)
            {
                if (!Directory.Exists(DirectoryPath)) return false;
                using var output = new StreamWriter(destinationPath, append: false);
                for (var index = MaxFiles - 1; index >= 1; index--)
                {
                    var archived = ActivePath + "." + index;
                    if (File.Exists(archived)) output.Write(File.ReadAllText(archived));
                }
                if (File.Exists(ActivePath)) output.Write(File.ReadAllText(ActivePath));
                return true;
            }
        }
        catch { return false; }
    }

    internal static string RedactForTest(string value) => Redact(value);

    internal static DiagnosticLogWriter CreateWriterForTest(
        IDiagnosticLogStore store, string directory, long maxBytes, int maxFiles, Func<DateTimeOffset>? clock = null) =>
        new(store, directory, maxBytes, maxFiles, clock ?? (() => DateTimeOffset.UtcNow));

    private static string Redact(string value) => Secret().Replace(Url().Replace(value, "[url]"), "$1=[redacted]");
    [GeneratedRegex(@"(?i)(password|privatekey|token|subscription|uuid)\s*[:=]\s*[^\s,;]+")]
    private static partial Regex Secret();
    [GeneratedRegex(@"(?i)\b(?:https?|vless|vmess|trojan|ss|hysteria2)://\S+")]
    private static partial Regex Url();

    internal interface IDiagnosticLogStore
    {
        void CreateDirectory(string path);
        bool Exists(string path);
        long Length(string path);
        void AppendAllText(string path, string contents);
        void Delete(string path);
        void Move(string source, string destination, bool overwrite);
    }

    internal sealed class DiagnosticLogWriter(
        IDiagnosticLogStore store,
        string directory,
        long maxBytes,
        int maxFiles,
        Func<DateTimeOffset> clock)
    {
        private readonly string _activePath = Path.Combine(directory, "xrayui.log");

        internal bool TryWrite(string category, string message)
        {
            try
            {
                store.CreateDirectory(directory);
                RotateIfNeeded();
                store.AppendAllText(_activePath, $"{clock():O} [{category}] {Redact(message)}{Environment.NewLine}");
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void RotateIfNeeded()
        {
            if (!store.Exists(_activePath) || store.Length(_activePath) < maxBytes) return;
            // maxFiles counts the active file too, so keep at most maxFiles - 1 archives.
            var archiveCount = Math.Max(0, maxFiles - 1);
            if (archiveCount == 0)
            {
                store.Delete(_activePath);
                return;
            }
            var oldest = _activePath + "." + archiveCount;
            if (store.Exists(oldest)) store.Delete(oldest);
            for (var index = archiveCount - 1; index >= 1; index--)
            {
                var source = _activePath + "." + index;
                if (store.Exists(source)) store.Move(source, _activePath + "." + (index + 1), true);
            }
            store.Move(_activePath, _activePath + ".1", true);
        }
    }

    private sealed class FileDiagnosticLogStore : IDiagnosticLogStore
    {
        public void CreateDirectory(string path) => Directory.CreateDirectory(path);
        public bool Exists(string path) => File.Exists(path);
        public long Length(string path) => new FileInfo(path).Length;
        public void AppendAllText(string path, string contents) => File.AppendAllText(path, contents);
        public void Delete(string path) => File.Delete(path);
        public void Move(string source, string destination, bool overwrite) => File.Move(source, destination, overwrite);
    }
}
