using System;
using System.ComponentModel;
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
        try
        {
            var result = _services.Client.GeneratePasswordAsync(_services.Settings.GeneratedPasswordLength).GetAwaiter().GetResult();
            if (!result.IsSuccess)
            {
                StatusReporter.Show(result.Error!);
                return CommandResult.KeepOpen();
            }

            _services.Clipboard.CopySecret(result.Value!, _services.Settings.ClipboardClearDelay);
            return CommandResult.ShowToast("Generated password copied");
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            StatusReporter.Show($"Generate password failed: {ex.Message}");
            return CommandResult.KeepOpen();
        }
    }
}
