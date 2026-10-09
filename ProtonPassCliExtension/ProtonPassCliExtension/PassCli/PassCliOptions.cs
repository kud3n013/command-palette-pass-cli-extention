using System;

namespace ProtonPassCliExtension.PassCli;

internal sealed record PassCliOptions
{
    /// <summary>Executable name or full path. A bare name is resolved through PATH.</summary>
    public string ExecutablePath { get; init; } = "pass-cli";

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
}
