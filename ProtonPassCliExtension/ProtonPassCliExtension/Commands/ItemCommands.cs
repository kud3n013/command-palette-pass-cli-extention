using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using ProtonPassCliExtension.PassCli;
using ProtonPassCliExtension.Services;

namespace ProtonPassCliExtension.Commands;

/// <summary>
/// Base for actions on one item. Secrets are fetched here, at invocation time, and handed straight
/// to the clipboard service. Nothing is logged and no secret reaches a status message.
/// </summary>
internal abstract partial class ItemCommand : InvokableCommand
{
    protected ItemCommand(AppServices services, CachedItem item)
    {
        Services = services;
        Item = item;
    }

    protected AppServices Services { get; }

    protected CachedItem Item { get; }

    protected static CommandResult Fail(PassCliError error)
    {
        var message = StatusReporter.Describe(error);
        StatusReporter.Show(message);
        return CommandResult.KeepOpen();
    }

    protected CommandResult CopyAndDismiss(string secret, string what)
    {
        Services.Clipboard.CopySecret(secret, Services.Settings.ClipboardClearDelay);
        return CommandResult.ShowToast(what + " copied");
    }

    // Invoke is synchronous; the work is a short-lived child process, so block the invoking thread.
    protected static T Wait<T>(Task<T> task) => task.GetAwaiter().GetResult();
}

internal sealed partial class CopyPasswordCommand : ItemCommand
{
    public CopyPasswordCommand(AppServices services, CachedItem item)
        : base(services, item)
    {
        Name = "Copy password";
        Icon = new IconInfo("");
    }

    public override CommandResult Invoke()
    {
        var result = Wait(Services.Client.GetFieldAsync(Item.ShareId, Item.ItemId, "password"));
        return result.IsSuccess ? CopyAndDismiss(result.Value!, "Password") : Fail(result.Error!);
    }
}

internal sealed partial class CopyUsernameCommand : ItemCommand
{
    public CopyUsernameCommand(AppServices services, CachedItem item)
        : base(services, item)
    {
        Name = "Copy username";
        Icon = new IconInfo("");
    }

    public override CommandResult Invoke()
    {
        var result = Wait(Services.Client.GetUsernameAsync(Item.ShareId, Item.ItemId));
        return result.IsSuccess ? CopyAndDismiss(result.Value!, "Username") : Fail(result.Error!);
    }
}

internal sealed partial class CopyTotpCommand : ItemCommand
{
    public CopyTotpCommand(AppServices services, CachedItem item)
        : base(services, item)
    {
        Name = "Copy TOTP code";
        Icon = new IconInfo("");
    }

    public override CommandResult Invoke()
    {
        var result = Wait(Services.Client.GetTotpAsync(Item.ShareId, Item.ItemId));
        return result.IsSuccess ? CopyAndDismiss(result.Value!, "TOTP code") : Fail(result.Error!);
    }
}

internal sealed partial class CopyReferenceCommand : ItemCommand
{
    public CopyReferenceCommand(AppServices services, CachedItem item)
        : base(services, item)
    {
        Name = "Copy pass:// reference";
        Icon = new IconInfo("");
    }

    public override CommandResult Invoke()
    {
        // The reference holds IDs only, no secret, so no auto-clear.
        Services.Clipboard.CopySecret(PassReference.Build(Item.ShareId, Item.ItemId), TimeSpan.Zero);
        return CommandResult.ShowToast("Reference copied");
    }
}

internal sealed partial class OpenItemUrlCommand : ItemCommand
{
    public OpenItemUrlCommand(AppServices services, CachedItem item)
        : base(services, item)
    {
        Name = "Open URL";
        Icon = new IconInfo("");
    }

    public override CommandResult Invoke()
    {
        var result = Wait(Services.Client.GetUrlsAsync(Item.ShareId, Item.ItemId));
        if (!result.IsSuccess)
        {
            return Fail(result.Error!);
        }

        foreach (var raw in result.Value!)
        {
            if (TryNormalise(raw, out var uri))
            {
                Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
                return CommandResult.Dismiss();
            }
        }

        StatusReporter.Show("This item has no web URL.", MessageState.Warning);
        return CommandResult.KeepOpen();
    }

    // Only http(s) is opened, so an item can't make us launch arbitrary protocol handlers or files.
    internal static bool TryNormalise(string raw, out Uri uri)
    {
        var candidate = raw.Trim();
        if (candidate.Length > 0 && !candidate.Contains("://", StringComparison.Ordinal))
        {
            candidate = "https://" + candidate;
        }

        return Uri.TryCreate(candidate, UriKind.Absolute, out uri!)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
    }
}
