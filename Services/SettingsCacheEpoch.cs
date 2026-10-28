using System.Threading;

namespace XrayUI.Services;

/// <summary>Keeps an older asynchronous disk read from publishing over a newer save.</summary>
internal sealed class SettingsCacheEpoch
{
    private int _value;

    internal int Capture() => Volatile.Read(ref _value);
    internal void BeginSave() => Interlocked.Increment(ref _value);
    internal void CompleteSave() => Interlocked.Increment(ref _value);
    internal bool IsCurrent(int captured) => captured == Volatile.Read(ref _value);
}
