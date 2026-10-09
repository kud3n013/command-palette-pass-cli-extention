using ProtonPassCliExtension.Services;
using Xunit;

namespace ProtonPassCliExtension.Tests;

public class ClipboardServiceTests
{
    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(20);

    /// <summary>A delay that completes only when the test says so (or is cancelled).</summary>
    private sealed class ManualDelay
    {
        private readonly List<TaskCompletionSource> _waiters = [];

        public Task Wait(TimeSpan _, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            ct.Register(() => tcs.TrySetCanceled(ct));
            _waiters.Add(tcs);
            return tcs.Task;
        }

        public void Elapse() => _waiters.ForEach(w => w.TrySetResult());
    }

    private static (ClipboardService Service, FakeClipboard Clipboard, ManualDelay Delay) Create()
    {
        var clipboard = new FakeClipboard();
        var delay = new ManualDelay();
        return (new ClipboardService(clipboard, delay.Wait), clipboard, delay);
    }

    [Fact]
    public async Task ClearsClipboardWhenUnchanged()
    {
        var (service, clipboard, delay) = Create();

        service.CopySecret("s3cret", Delay);
        Assert.Equal("s3cret", clipboard.Text);
        delay.Elapse();
        await service.PendingClear!;

        Assert.Null(clipboard.Text);
        Assert.Equal(1, clipboard.ClearCount);
    }

    [Fact]
    public async Task DoesNotClearWhenSomethingElseWasCopied()
    {
        var (service, clipboard, delay) = Create();

        service.CopySecret("s3cret", Delay);
        clipboard.ExternalWrite("user copied this");
        delay.Elapse();
        await service.PendingClear!;

        Assert.Equal("user copied this", clipboard.Text);
        Assert.Equal(0, clipboard.ClearCount);
    }

    [Fact]
    public async Task ClearsWhenSequenceChangedButTextIsStillOurs()
    {
        var (service, clipboard, delay) = Create();

        service.CopySecret("s3cret", Delay);
        clipboard.Touch();
        delay.Elapse();
        await service.PendingClear!;

        Assert.Null(clipboard.Text);
    }

    [Fact]
    public async Task SecondCopySupersedesFirstTimer()
    {
        var (service, clipboard, delay) = Create();

        service.CopySecret("first", Delay);
        var firstTimer = service.PendingClear!;
        service.CopySecret("second", Delay);
        var secondTimer = service.PendingClear!;

        await firstTimer;
        Assert.Equal("second", clipboard.Text);
        Assert.Equal(0, clipboard.ClearCount);

        delay.Elapse();
        await secondTimer;
        Assert.Null(clipboard.Text);
        Assert.Equal(1, clipboard.ClearCount);
    }

    [Fact]
    public void ZeroDelayDisablesAutoClear()
    {
        var (service, clipboard, _) = Create();

        service.CopySecret("keep", TimeSpan.Zero);

        Assert.Null(service.PendingClear);
        Assert.Equal("keep", clipboard.Text);
    }

    [Fact]
    public async Task ZeroDelayCancelsEarlierTimer()
    {
        var (service, clipboard, delay) = Create();

        service.CopySecret("first", Delay);
        var firstTimer = service.PendingClear!;
        service.CopySecret("reference-no-clear", TimeSpan.Zero);
        delay.Elapse();
        await firstTimer;

        Assert.Equal("reference-no-clear", clipboard.Text);
        Assert.Equal(0, clipboard.ClearCount);
    }
}
