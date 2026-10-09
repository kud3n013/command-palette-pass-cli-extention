using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProtonPassCliExtension.Services;

/// <summary>
/// Copies sensitive text and clears it again after a delay, but only if the clipboard still holds
/// what we put there. The plaintext is not retained: only its SHA-256 hash and the clipboard
/// sequence number are kept while the timer runs.
/// </summary>
internal sealed class ClipboardService
{
    private readonly IClipboard _clipboard;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly object _gate = new();
    private CancellationTokenSource? _pending;

    public ClipboardService(IClipboard clipboard)
        : this(clipboard, Task.Delay)
    {
    }

    public ClipboardService(IClipboard clipboard, Func<TimeSpan, CancellationToken, Task> delay)
    {
        _clipboard = clipboard;
        _delay = delay;
    }

    /// <summary>The scheduled clear, exposed so tests can await it. Null when auto-clear is disabled.</summary>
    public Task? PendingClear { get; private set; }

    public void CopySecret(string text, TimeSpan clearAfter)
    {
        _clipboard.SetSensitiveText(text);
        var sequence = _clipboard.SequenceNumber;
        var hash = Hash(text);

        CancellationTokenSource? previous;
        lock (_gate)
        {
            previous = _pending;
            if (clearAfter > TimeSpan.Zero)
            {
                var next = new CancellationTokenSource();
                _pending = next;
                PendingClear = ClearLaterAsync(sequence, hash, clearAfter, next);
            }
            else
            {
                _pending = null;
                PendingClear = null;
            }
        }

        // A newer copy supersedes the older timer, so it cannot clear the newer value early.
        previous?.Cancel();
    }

    private async Task ClearLaterAsync(long sequence, byte[] hash, TimeSpan delay, CancellationTokenSource cts)
    {
        try
        {
            await _delay(delay, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (cts.IsCancellationRequested)
        {
            return;
        }

        if (_clipboard.SequenceNumber == sequence)
        {
            _clipboard.Clear();
            return;
        }

        // Something touched the clipboard (a clipboard manager, for example). Still clear if the text is ours.
        var current = _clipboard.GetText();
        if (current is not null && CryptographicOperations.FixedTimeEquals(Hash(current), hash))
        {
            _clipboard.Clear();
        }
    }

    private static byte[] Hash(string text) => SHA256.HashData(Encoding.UTF8.GetBytes(text));
}
