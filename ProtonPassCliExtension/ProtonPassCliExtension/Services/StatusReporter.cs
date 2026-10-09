using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using ProtonPassCliExtension.PassCli;

namespace ProtonPassCliExtension.Services;

internal static class StatusReporter
{
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

    /// <summary>Shows a status message on the page. Never pass secret values here.</summary>
    public static void Show(string message, MessageState state = MessageState.Error)
    {
        ExtensionHost.ShowStatus(new StatusMessage { Message = message, State = state }, StatusContext.Page);
    }

    public static void Show(PassCliError error) => Show(Describe(error));
}
