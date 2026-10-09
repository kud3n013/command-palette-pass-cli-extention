using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using ProtonPassCliExtension.PassCli;

namespace ProtonPassCliExtension.Services;

internal static class StatusReporter
{
    private static readonly object Gate = new();
    private static StatusMessage? _current;

    public static string Describe(PassCliError error) => error.Kind switch
    {
        PassCliErrorKind.NotInstalled => "pass-cli was not found. Install it with 'winget install --id Proton.ProtonPass.CLI --exact' or set its path in the extension settings.",
        PassCliErrorKind.NotLoggedIn => "Not logged in to Proton Pass. Run 'pass-cli login' in a terminal, then refresh.",
        PassCliErrorKind.Timeout => "pass-cli timed out. Check your connection and try again.",
        PassCliErrorKind.FieldNotFound => "This item doesn't have that field.",
        PassCliErrorKind.NoTotp => "This item has no TOTP configured.",
        PassCliErrorKind.InvalidOutput => "pass-cli returned output this extension couldn't read. The CLI version may be unsupported.",
        _ => string.IsNullOrWhiteSpace(error.StdErr)
            ? $"pass-cli failed (exit code {error.ExitCode})."
            : $"pass-cli failed (exit code {error.ExitCode}): {error.StdErr}",
    };

    /// <summary>Shows one status message at a time, replacing the previous one. Never pass secret values here.</summary>
    public static void Show(string message, MessageState state = MessageState.Error)
    {
        var next = new StatusMessage { Message = message, State = state };
        lock (Gate)
        {
            Hide();
            _current = next;
            ExtensionHost.ShowStatus(next, StatusContext.Page);
        }
    }

    public static void Show(PassCliError error) => Show(Describe(error));

    /// <summary>Hides the current status message, if any (for example after a successful reload).</summary>
    public static void Hide()
    {
        lock (Gate)
        {
            if (_current is not null)
            {
                ExtensionHost.HideStatus(_current);
                _current = null;
            }
        }
    }
}
