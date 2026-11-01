using XrayUI.Services;

namespace XrayUI.Tests;

public class SerializedFileSaverTests
{
    [Fact]
    public void SettingsCacheEpoch_CompletedSaveInvalidatesOlderRead()
    {
        var epoch = new SettingsCacheEpoch();
        var oldRead = epoch.Capture();

        epoch.BeginSave();
        Assert.False(epoch.IsCurrent(oldRead));

        var readDuringWrite = epoch.Capture();
        epoch.CompleteSave();

        Assert.False(epoch.IsCurrent(readDuringWrite));
        Assert.True(epoch.IsCurrent(epoch.Capture()));
    }

    [Fact]
    public async Task SaveAsync_ConcurrentRequests_AreWrittenInOrderAndPublishAfterEachSuccess()
    {
        var written = new List<string>();
        var published = new List<string>();
        var firstWriteEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstWrite = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var saver = new SerializedFileSaver(async (_, snapshot) =>
        {
            written.Add(snapshot);
            if (snapshot == "first")
            {
                firstWriteEntered.SetResult();
                await releaseFirstWrite.Task;
            }
        });

        var first = saver.SaveAsync("settings.json", "first", () => published.Add("first"));
        await firstWriteEntered.Task;
        var second = saver.SaveAsync("settings.json", "second", () => published.Add("second"));
        Assert.Equal(["first"], written);
        Assert.Equal([], published);

        releaseFirstWrite.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(["first", "second"], written);
        Assert.Equal(["first", "second"], published);
    }

    [Fact]
    public async Task SaveAsync_WriteFailure_DoesNotPublishUnsavedSnapshot()
    {
        var published = false;
        var saver = new SerializedFileSaver((_, _) => Task.FromException(new IOException("injected disk failure")));

        await Assert.ThrowsAsync<IOException>(() => saver.SaveAsync("settings.json", "snapshot", () => published = true));

        Assert.False(published);
    }
}
