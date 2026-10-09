using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using ProtonPassCliExtension.PassCli.Models;

namespace ProtonPassCliExtension.PassCli;

/// <summary>
/// Thin async wrapper over <c>pass-cli</c>. Arguments are always passed as separate argv entries.
/// Secret-bearing stdout is parsed and discarded; it is never put into an error or logged.
/// </summary>
internal sealed class PassCliClient
{
    private const int MaxStdErrLength = 500;

    private readonly IProcessRunner _runner;
    private readonly Func<PassCliOptions> _options;

    public PassCliClient(IProcessRunner runner, Func<PassCliOptions> options)
    {
        _runner = runner;
        _options = options;
    }

    public PassCliClient(IProcessRunner runner, PassCliOptions options)
        : this(runner, () => options)
    {
    }

    /// <summary>Verifies the CLI is installed and a session exists (<c>pass-cli info</c>).</summary>
    public async Task<PassCliResult<bool>> CheckSessionAsync(CancellationToken ct = default)
    {
        var run = await RunAsync(["info"], ct).ConfigureAwait(false);
        return run.Error is not null ? PassCliResult<bool>.Fail(run.Error) : PassCliResult<bool>.Ok(true);
    }

    public async Task<PassCliResult<IReadOnlyList<Vault>>> ListVaultsAsync(CancellationToken ct = default)
    {
        var run = await RunAsync(["vault", "list", "--output", "json"], ct).ConfigureAwait(false);
        if (run.Error is not null)
        {
            return PassCliResult<IReadOnlyList<Vault>>.Fail(run.Error);
        }

        var parsed = TryParse(run.StdOut, PassCliJsonContext.Default.VaultListResponse);
        return parsed?.Vaults is null
            ? PassCliResult<IReadOnlyList<Vault>>.Fail(new PassCliError(PassCliErrorKind.InvalidOutput))
            : PassCliResult<IReadOnlyList<Vault>>.Ok(parsed.Vaults);
    }

    /// <summary>Lists active items of one vault (metadata only). The '=' form stops IDs starting with '-' being read as flags.</summary>
    public async Task<PassCliResult<IReadOnlyList<ItemSummary>>> ListItemsAsync(string shareId, CancellationToken ct = default)
    {
        var run = await RunAsync(
            ["item", "list", $"--share-id={shareId}", "--filter-state", "active", "--output", "json"], ct)
            .ConfigureAwait(false);
        if (run.Error is not null)
        {
            return PassCliResult<IReadOnlyList<ItemSummary>>.Fail(run.Error);
        }

        var parsed = TryParse(run.StdOut, PassCliJsonContext.Default.ItemListResponse);
        return parsed?.Items is null
            ? PassCliResult<IReadOnlyList<ItemSummary>>.Fail(new PassCliError(PassCliErrorKind.InvalidOutput))
            : PassCliResult<IReadOnlyList<ItemSummary>>.Ok(parsed.Items);
    }

    /// <summary>Reads one field (password, username, email, ...) via a pass:// reference. The value is a secret: do not log.</summary>
    public async Task<PassCliResult<string>> GetFieldAsync(string shareId, string itemId, string field, CancellationToken ct = default)
    {
        var run = await RunAsync(["item", "view", PassReference.Build(shareId, itemId, field)], ct).ConfigureAwait(false);
        if (run.Error is not null)
        {
            return PassCliResult<string>.Fail(run.Error);
        }

        // The CLI appends exactly one line terminator; anything else belongs to the value.
        var value = TrimOneLineEnding(run.StdOut);
        return value.Length == 0
            ? PassCliResult<string>.Fail(new PassCliError(PassCliErrorKind.FieldNotFound))
            : PassCliResult<string>.Ok(value);
    }

    /// <summary>Username, falling back to the email field when the login has no username.</summary>
    public async Task<PassCliResult<string>> GetUsernameAsync(string shareId, string itemId, CancellationToken ct = default)
    {
        var username = await GetFieldAsync(shareId, itemId, "username", ct).ConfigureAwait(false);
        if (username.IsSuccess || username.Error!.Kind != PassCliErrorKind.FieldNotFound)
        {
            return username;
        }

        return await GetFieldAsync(shareId, itemId, "email", ct).ConfigureAwait(false);
    }

    /// <summary>Current TOTP code. Only 'totp' is read; the seed-bearing 'totp_uri' is ignored.</summary>
    public async Task<PassCliResult<string>> GetTotpAsync(string shareId, string itemId, CancellationToken ct = default)
    {
        var run = await RunAsync(["item", "totp", PassReference.Build(shareId, itemId), "--output", "json"], ct).ConfigureAwait(false);
        if (run.Error is not null)
        {
            return PassCliResult<string>.Fail(run.Error);
        }

        var parsed = TryParse(run.StdOut, PassCliJsonContext.Default.TotpResponse);
        return string.IsNullOrEmpty(parsed?.Totp)
            ? PassCliResult<string>.Fail(new PassCliError(PassCliErrorKind.InvalidOutput))
            : PassCliResult<string>.Ok(parsed.Totp);
    }

    /// <summary>Login URLs. The view output includes the password, but only 'urls' is deserialised.</summary>
    public async Task<PassCliResult<IReadOnlyList<string>>> GetUrlsAsync(string shareId, string itemId, CancellationToken ct = default)
    {
        var run = await RunAsync(
            ["item", "view", $"--share-id={shareId}", $"--item-id={itemId}", "--output", "json"], ct)
            .ConfigureAwait(false);
        if (run.Error is not null)
        {
            return PassCliResult<IReadOnlyList<string>>.Fail(run.Error);
        }

        var parsed = TryParse(run.StdOut, PassCliJsonContext.Default.ItemViewResponse);
        return parsed?.Item is null
            ? PassCliResult<IReadOnlyList<string>>.Fail(new PassCliError(PassCliErrorKind.InvalidOutput))
            : PassCliResult<IReadOnlyList<string>>.Ok(parsed.Item.Content?.Content?.Login?.Urls ?? []);
    }

    internal static PassCliError MapFailure(int exitCode, string stdErr)
    {
        var lower = stdErr.ToLowerInvariant();

        // Real output with no session (pass-cli 2.4.2, exit 1): "...there is no session Error: This operation requires an authenticated client".
        // The wording is undocumented, so match loosely.
        if (lower.Contains("not logged in")
            || lower.Contains("pass-cli login")
            || lower.Contains("log in")
            || lower.Contains("login required")
            || lower.Contains("unauthorized")
            || lower.Contains("not authenticated")
            || lower.Contains("authentication")
            || lower.Contains("authenticated client")
            || lower.Contains("no session")
            || (lower.Contains("session") && (lower.Contains("expired") || lower.Contains("invalid") || lower.Contains("no active"))))
        {
            return new PassCliError(PassCliErrorKind.NotLoggedIn, exitCode);
        }

        if (lower.Contains("field does not exist"))
        {
            return new PassCliError(PassCliErrorKind.FieldNotFound, exitCode);
        }

        if (lower.Contains("no totp fields found"))
        {
            return new PassCliError(PassCliErrorKind.NoTotp, exitCode);
        }

        var trimmed = stdErr.Trim();
        if (trimmed.Length > MaxStdErrLength)
        {
            trimmed = trimmed[..MaxStdErrLength];
        }

        return new PassCliError(PassCliErrorKind.Failed, exitCode, trimmed);
    }

    private static string TrimOneLineEnding(string s)
    {
        if (s.EndsWith("\r\n", StringComparison.Ordinal))
        {
            return s[..^2];
        }

        return s.EndsWith('\n') ? s[..^1] : s;
    }

    private static T? TryParse<T>(string json, JsonTypeInfo<T> info)
        where T : class
    {
        try
        {
            return JsonSerializer.Deserialize(json, info);
        }
        catch (JsonException)
        {
            // Not rethrown: JsonException messages can echo parts of the input.
            return null;
        }
    }

    private async Task<RunOutcome> RunAsync(string[] args, CancellationToken ct)
    {
        var options = _options();
        ProcessOutput output;
        try
        {
            output = await _runner.RunAsync(options.ExecutablePath, args, options.Timeout, ct).ConfigureAwait(false);
        }
        catch (ProcessNotFoundException)
        {
            return new RunOutcome(new PassCliError(PassCliErrorKind.NotInstalled), string.Empty);
        }

        if (output.TimedOut)
        {
            return new RunOutcome(new PassCliError(PassCliErrorKind.Timeout), string.Empty);
        }

        return output.ExitCode == 0
            ? new RunOutcome(null, output.StdOut)
            : new RunOutcome(MapFailure(output.ExitCode, output.StdErr), string.Empty);
    }

    private sealed record RunOutcome(PassCliError? Error, string StdOut);
}
