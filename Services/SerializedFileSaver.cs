using System;
using System.Threading;
using System.Threading.Tasks;

namespace XrayUI.Services;

/// <summary>Serializes writes to one settings file and publishes cache state only after success.</summary>
internal sealed class SerializedFileSaver(Func<string, string, Task> write)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    internal async Task SaveAsync(string path, string snapshot, Action published)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await write(path, snapshot).ConfigureAwait(false);
            published();
        }
        finally
        {
            _gate.Release();
        }
    }
}
