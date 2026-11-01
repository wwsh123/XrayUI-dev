using XrayUI.Services;

namespace XrayUI.Tests;

public class XrayReadySignalTests
{
    [Fact]
    public async Task WaitAsync_SilentProcess_TimesOutWithinBound()
    {
        var signal = XrayReadySignal.CreateForTest();

        var result = await signal.WaitAsync(TimeSpan.FromMilliseconds(15), TestContext.Current.CancellationToken);

        Assert.Equal(XrayReadySignal.Outcome.TimedOut, result);
    }

    [Fact]
    public async Task WaitAsync_EarlyExit_BeatsReadinessTimeout()
    {
        var signal = XrayReadySignal.CreateForTest();
        signal.MarkExitedForTest();

        var result = await signal.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Assert.Equal(XrayReadySignal.Outcome.Exited, result);
    }

    [Fact]
    public async Task WaitAsync_OnlyCoreStartupLineMarksReady()
    {
        var signal = XrayReadySignal.CreateForTest();
        signal.ObserveOutputForTest("ordinary output started");
        signal.ObserveOutputForTest("2026/10/03 [Warning] core: Xray started");

        var result = await signal.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Assert.Equal(XrayReadySignal.Outcome.Ready, result);
    }
}
