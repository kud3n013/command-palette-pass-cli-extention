using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ProtonPassCliExtension.PassCli;

/// <summary>Result of running an external process. Stdout may contain secrets: never log it.</summary>
internal sealed record ProcessOutput(int ExitCode, string StdOut, string StdErr, bool TimedOut);

/// <summary>Thrown by a runner when the executable cannot be started (not found, not executable).</summary>
internal sealed class ProcessNotFoundException : Exception
{
    public ProcessNotFoundException(string fileName)
        : base($"Could not start '{fileName}'.")
    {
    }
}

internal interface IProcessRunner
{
    Task<ProcessOutput> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
