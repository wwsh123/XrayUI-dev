using XrayUI.Services;

namespace XrayUI.Tests;

public class DiagnosticLogServiceTests
{
    [Fact]
    public void RedactForTest_RemovesShareUrlsAndSecrets()
    {
        var value = DiagnosticLogService.RedactForTest(
            "vless://uuid@example.test:443?token=abc password=secret https://subscription.example/a");

        Assert.DoesNotContain("uuid@example", value);
        Assert.DoesNotContain("secret", value);
        Assert.DoesNotContain("subscription.example", value);
        Assert.Contains("[url]", value);
        Assert.Contains("password=[redacted]", value);
    }

    [Fact]
    public void Writer_RotatesWithinConfiguredFileLimit_AndRedactsBeforeAppend()
    {
        var store = new FakeStore();
        var writer = DiagnosticLogService.CreateWriterForTest(
            store, "diagnostics", maxBytes: 10, maxFiles: 3,
            () => new DateTimeOffset(2026, 10, 4, 0, 0, 0, TimeSpan.Zero));
        var active = Path.Combine("diagnostics", "xrayui.log");
        store.Files[active] = "old-active";
        store.Files[active + ".1"] = "old-1";
        store.Files[active + ".2"] = "oldest";

        Assert.True(writer.TryWrite("core", "token=secret vless://uuid@example.test:443"));

        Assert.DoesNotContain(store.Files.Values, value => value == "oldest");
        Assert.Equal("old-1", store.Files[active + ".2"]);
        Assert.Equal("old-active", store.Files[active + ".1"]);
        Assert.Equal(3, store.Files.Count);
        Assert.Contains("token=[redacted]", store.Files[active]);
        Assert.DoesNotContain("uuid@example", store.Files[active]);
    }

    [Fact]
    public void Writer_WriteFailure_ReturnsFalseWithoutThrowing()
    {
        var writer = DiagnosticLogService.CreateWriterForTest(new FakeStore { FailAppend = true }, "diagnostics", 10, 3);

        Assert.False(writer.TryWrite("core", "password=secret"));
    }

    private sealed class FakeStore : DiagnosticLogService.IDiagnosticLogStore
    {
        public Dictionary<string, string> Files { get; } = new(StringComparer.Ordinal);
        public bool FailAppend { get; init; }

        public void CreateDirectory(string path) { }
        public bool Exists(string path) => Files.ContainsKey(path);
        public long Length(string path) => Files[path].Length;
        public void AppendAllText(string path, string contents)
        {
            if (FailAppend) throw new IOException("injected diagnostics failure");
            Files[path] = Files.GetValueOrDefault(path) + contents;
        }
        public void Delete(string path) => Files.Remove(path);
        public void Move(string source, string destination, bool overwrite)
        {
            if (!overwrite && Files.ContainsKey(destination)) throw new IOException("destination exists");
            Files[destination] = Files[source];
            Files.Remove(source);
        }
    }
}
