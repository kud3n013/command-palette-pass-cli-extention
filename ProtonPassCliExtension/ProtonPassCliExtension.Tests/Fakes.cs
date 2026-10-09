using ProtonPassCliExtension.PassCli;
using ProtonPassCliExtension.Services;

namespace ProtonPassCliExtension.Tests;

internal static class Fixture
{
    public static string Read(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", name));
}

/// <summary>Records calls and replies from a queue-less lookup keyed on the arguments.</summary>
internal sealed class FakeRunner : IProcessRunner
{
    private readonly Func<IReadOnlyList<string>, ProcessOutput> _reply;

    public FakeRunner(Func<IReadOnlyList<string>, ProcessOutput> reply)
    {
        _reply = reply;
    }

    public List<IReadOnlyList<string>> Calls { get; } = [];

    public string? LastFileName { get; private set; }

    public Task<ProcessOutput> RunAsync(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken)
    {
        LastFileName = fileName;
        Calls.Add(arguments);
        return Task.FromResult(_reply(arguments));
    }

    public static ProcessOutput Ok(string stdout) => new(0, stdout, string.Empty, false);

    public static ProcessOutput Err(int code, string stderr) => new(code, string.Empty, stderr, false);

    public static FakeRunner Always(ProcessOutput output) => new(_ => output);

    public static PassCliClient ClientFor(FakeRunner runner) => new(runner, new PassCliOptions());
}

internal sealed class ThrowingRunner : IProcessRunner
{
    public Task<ProcessOutput> RunAsync(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken) =>
        throw new ProcessNotFoundException(fileName);
}

internal sealed class FakeClipboard : IClipboard
{
    public long SequenceNumber { get; private set; } = 1;

    public string? Text { get; private set; }

    public int ClearCount { get; private set; }

    public void SetSensitiveText(string text)
    {
        Text = text;
        SequenceNumber++;
    }

    public string? GetText() => Text;

    public void Clear()
    {
        Text = null;
        ClearCount++;
        SequenceNumber++;
    }

    /// <summary>Simulates another app writing to the clipboard.</summary>
    public void ExternalWrite(string text)
    {
        Text = text;
        SequenceNumber++;
    }

    /// <summary>Simulates something bumping the sequence number without changing the text.</summary>
    public void Touch() => SequenceNumber++;
}
