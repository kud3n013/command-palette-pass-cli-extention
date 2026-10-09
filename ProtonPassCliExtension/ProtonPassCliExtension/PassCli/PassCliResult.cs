namespace ProtonPassCliExtension.PassCli;

internal enum PassCliErrorKind
{
    NotInstalled,
    NotLoggedIn,
    Timeout,
    FieldNotFound,
    NoTotp,
    InvalidOutput,
    Failed,
}

/// <summary>
/// A typed failure. <see cref="StdErr"/> is only populated for <see cref="PassCliErrorKind.Failed"/>
/// and is truncated; stdout is never stored here because it may hold secrets.
/// </summary>
internal sealed record PassCliError(PassCliErrorKind Kind, int? ExitCode = null, string? StdErr = null);

internal sealed class PassCliResult<T>
{
    private PassCliResult(T? value, PassCliError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public PassCliError? Error { get; }

    public bool IsSuccess => Error is null;

    public static PassCliResult<T> Ok(T value) => new(value, null);

    public static PassCliResult<T> Fail(PassCliError error) => new(default, error);
}
