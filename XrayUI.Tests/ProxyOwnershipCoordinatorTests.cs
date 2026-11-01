using Microsoft.Win32;
using XrayUI.Services;

namespace XrayUI.Tests;

public class ProxyOwnershipCoordinatorTests
{
    [Fact]
    public void ClearWithoutTakeover_DoesNotMutateExternalValues()
    {
        var values = FakeValues.WithExternalProxy();
        var states = new FakeStates();

        Assert.True(new ProxyOwnershipService.OwnershipCoordinator(values, states, () => { }).ClearProxy());

        Assert.Equal("external:9000", values["ProxyServer"].Value);
        Assert.Equal(0, values.Mutations);
    }

    [Fact]
    public void TakeoverAcrossPortChanges_RestoresFirstSnapshot()
    {
        var values = FakeValues.WithExternalProxy();
        var states = new FakeStates();
        var subject = new ProxyOwnershipService.OwnershipCoordinator(values, states, () => { });

        Assert.True(subject.SetProxy("127.0.0.1", 10808));
        Assert.True(subject.SetProxy("127.0.0.1", 20808));
        Assert.Equal("external:9000", states.Value!.Original["ProxyServer"].Value);
        Assert.Equal("127.0.0.1:20808", values["ProxyServer"].Value);

        Assert.True(subject.ClearProxy());
        Assert.Equal("external:9000", values["ProxyServer"].Value);
        Assert.Null(states.Value);
    }

    [Fact]
    public void ClearAfterExternalModification_LeavesAllValuesAndRecoveryState()
    {
        var values = FakeValues.WithExternalProxy();
        var states = new FakeStates();
        var subject = new ProxyOwnershipService.OwnershipCoordinator(values, states, () => { });
        Assert.True(subject.SetProxy("127.0.0.1", 10808));
        values.Set("ProxyServer", new ProxyOwnershipService.ProxyValue("other-app:7777", RegistryValueKind.String));

        Assert.False(subject.ClearProxy());
        Assert.Equal("other-app:7777", values["ProxyServer"].Value);
        Assert.NotNull(states.Value);
    }

    [Fact]
    public void PartialTakeoverFailure_RestoresOnlyValuesWrittenByThisAttempt()
    {
        var values = FakeValues.WithExternalProxy();
        values.ThrowOnceOnSet = "ProxyOverride";
        var states = new FakeStates();
        var subject = new ProxyOwnershipService.OwnershipCoordinator(values, states, () => { });

        Assert.False(subject.SetProxy("127.0.0.1", 10808));
        Assert.Equal("0", values["ProxyEnable"].Value);
        Assert.Equal("external:9000", values["ProxyServer"].Value);
        Assert.Equal("<local>", values["ProxyOverride"].Value);
        Assert.NotNull(states.Value);
    }

    [Fact]
    public void AbnormalExitRecovery_NewCoordinatorRestoresPersistedOriginalSnapshot()
    {
        var values = FakeValues.WithExternalProxy();
        var states = new FakeStates();
        Assert.True(new ProxyOwnershipService.OwnershipCoordinator(values, states, () => { }).SetProxy("127.0.0.1", 10808));

        Assert.True(new ProxyOwnershipService.OwnershipCoordinator(values, states, () => { }).ClearProxy());

        Assert.Equal("0", values["ProxyEnable"].Value);
        Assert.Equal("external:9000", values["ProxyServer"].Value);
        Assert.Null(states.Value);
    }

    [Fact]
    public void PartialRestoreFailure_CanBeRetriedWithoutOverwritingExternalValues()
    {
        var values = FakeValues.WithExternalProxy();
        var states = new FakeStates();
        var subject = new ProxyOwnershipService.OwnershipCoordinator(values, states, () => { });
        Assert.True(subject.SetProxy("127.0.0.1", 10808));

        values.ThrowOnceOnSet = "ProxyServer";
        Assert.False(subject.ClearProxy());
        Assert.NotNull(states.Value);
        Assert.Equal("0", values["ProxyEnable"].Value);

        Assert.True(subject.ClearProxy());
        Assert.Equal("external:9000", values["ProxyServer"].Value);
        Assert.Null(states.Value);
    }

    private sealed class FakeStates : ProxyOwnershipService.IStateStore
    {
        public ProxyOwnershipService.ProxyState? Value { get; private set; }
        public ProxyOwnershipService.ProxyState? Read() => Value;
        public void Write(ProxyOwnershipService.ProxyState state) => Value = state;
        public void Delete() => Value = null;
    }

    private sealed class FakeValues : ProxyOwnershipService.IValueStore
    {
        private readonly Dictionary<string, ProxyOwnershipService.ProxyValue> _values;
        public int Mutations { get; private set; }
        public string? ThrowOnceOnSet { get; set; }
        public ProxyOwnershipService.ProxyValue this[string name] => _values[name];

        private FakeValues(Dictionary<string, ProxyOwnershipService.ProxyValue> values) => _values = values;

        public static FakeValues WithExternalProxy() => new(new(StringComparer.Ordinal)
        {
            ["ProxyEnable"] = new("0", RegistryValueKind.DWord),
            ["ProxyServer"] = new("external:9000", RegistryValueKind.String),
            ["ProxyOverride"] = new("<local>", RegistryValueKind.String),
        });

        public ProxyOwnershipService.ProxyValue Get(string name) => _values.TryGetValue(name, out var value)
            ? value : new ProxyOwnershipService.ProxyValue(null, RegistryValueKind.Unknown);
        public void Set(string name, ProxyOwnershipService.ProxyValue value)
        {
            if (ThrowOnceOnSet == name)
            {
                ThrowOnceOnSet = null;
                throw new IOException("injected write failure");
            }
            _values[name] = value;
            Mutations++;
        }
        public void Delete(string name)
        {
            _values.Remove(name);
            Mutations++;
        }
        public void Flush() { }
    }
}
