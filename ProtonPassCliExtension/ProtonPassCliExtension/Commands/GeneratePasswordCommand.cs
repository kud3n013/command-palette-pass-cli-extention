using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using ProtonPassCliExtension.Services;

namespace ProtonPassCliExtension.Commands;

/// <summary>Generates a random password with pass-cli and copies it (auto-cleared like any other secret).</summary>
internal sealed partial class GeneratePasswordCommand : InvokableCommand
{
    private readonly AppServices _services;

    public GeneratePasswordCommand(AppServices services)
    {
        _services = services;
        Name = "Generate password";
        Icon = new IconInfo("");
    }

    public override CommandResult Invoke()
    {
        _ = Task.Run(RunAsync);
        return CommandResult.Dismiss();
    }

    private async Task RunAsync()
    {
        try
        {
            var result = await _services.Client.GeneratePasswordAsync(_services.Settings.GeneratedPasswordLength).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                new ToastStatusMessage(StatusReporter.Describe(result.Error!)).Show();
                return;
            }

            _services.Clipboard.CopySecret(result.Value!, _services.Settings.ClipboardClearDelay);
            new ToastStatusMessage("Generated password copied").Show();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            new ToastStatusMessage($"Generate password failed: {ex.Message}").Show();
        }
    }
}
