using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using ProtonPassCliExtension.PassCli;
using ProtonPassCliExtension.Services;

namespace ProtonPassCliExtension.Commands;

/// <summary>
/// Base for actions on one item. The palette is dismissed immediately and the work (a ~0.5 s pass-cli call)
/// finishes in the background, reporting through a toast. Secrets are fetched here, at invocation time,
/// and handed straight to the clipboard service. Nothing is logged and no secret reaches a message.
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

    public sealed override CommandResult Invoke()
    {
        _ = Task.Run(RunSafelyAsync);
        return CommandResult.Dismiss();
    }

    protected abstract Task RunAsync();

    protected static void Toast(string message) => new ToastStatusMessage(message).Show();

    protected static void Toast(PassCliError error) => Toast(StatusReporter.Describe(error));

    protected void CopyAndToast(string secret, string what)
    {
        Services.Clipboard.CopySecret(secret, Services.Settings.ClipboardClearDelay);
        Toast(what + " copied");
    }

    private async Task RunSafelyAsync()
    {
        try
        {
            await RunAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or OperationCanceledException)
        {
            // Messages from these exception types describe the OS failure and never contain item values.
            Toast($"{Name} failed: {ex.Message}");
        }
    }
}

/// <summary>Fetches one secret field and copies it.</summary>
internal abstract partial class CopyFieldCommand : ItemCommand
{
    private readonly string _what;

    protected CopyFieldCommand(AppServices services, CachedItem item, string name, string what, string glyph)
        : base(services, item)
    {
        _what = what;
        Name = name;
        Icon = new IconInfo(glyph);
    }

    protected abstract Task<PassCliResult<string>> FetchAsync();

    protected sealed override async Task RunAsync()
    {
        var result = await FetchAsync().ConfigureAwait(false);
        if (result.IsSuccess)
        {
            CopyAndToast(result.Value!, _what);
        }
        else
        {
            Toast(result.Error!);
        }
    }
}

internal sealed partial class CopyPasswordCommand : CopyFieldCommand
{
    public CopyPasswordCommand(AppServices services, CachedItem item)
        : base(services, item, "Copy password", "Password", "")
    {
    }

    protected override Task<PassCliResult<string>> FetchAsync() =>
        Services.Client.GetFieldAsync(Item.ShareId, Item.ItemId, "password");
}

internal sealed partial class CopyUsernameCommand : CopyFieldCommand
{
    public CopyUsernameCommand(AppServices services, CachedItem item)
        : base(services, item, "Copy username", "Username", "")
    {
    }

    protected override Task<PassCliResult<string>> FetchAsync() =>
        Services.Client.GetUsernameAsync(Item.ShareId, Item.ItemId);
}

internal sealed partial class CopyEmailCommand : CopyFieldCommand
{
    public CopyEmailCommand(AppServices services, CachedItem item)
        : base(services, item, "Copy email", "Email", "")
    {
    }

    protected override Task<PassCliResult<string>> FetchAsync() =>
        Services.Client.GetFieldAsync(Item.ShareId, Item.ItemId, "email");
}

internal sealed partial class CopyTotpCommand : CopyFieldCommand
{
    public CopyTotpCommand(AppServices services, CachedItem item)
        : base(services, item, "Copy TOTP code", "TOTP code", "")
    {
    }

    protected override Task<PassCliResult<string>> FetchAsync() =>
        Services.Client.GetTotpAsync(Item.ShareId, Item.ItemId);
}

internal sealed partial class CopyReferenceCommand : InvokableCommand
{
    private readonly AppServices _services;
    private readonly CachedItem _item;

    public CopyReferenceCommand(AppServices services, CachedItem item)
    {
        _services = services;
        _item = item;
        Name = "Copy pass:// reference";
        Icon = new IconInfo("");
    }

    public override CommandResult Invoke()
    {
        try
        {
            // The reference holds IDs only, no secret, so no auto-clear. Nothing to wait for, so this stays synchronous.
            _services.Clipboard.CopySecret(PassReference.Build(_item.ShareId, _item.ItemId), TimeSpan.Zero);
            return CommandResult.ShowToast("Reference copied");
        }
        catch (Win32Exception ex)
        {
            return CommandResult.ShowToast($"Copy failed: {ex.Message}");
        }
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

    protected override async Task RunAsync()
    {
        var result = await Services.Client.GetUrlsAsync(Item.ShareId, Item.ItemId).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            Toast(result.Error!);
            return;
        }

        foreach (var raw in result.Value!)
        {
            if (TryNormalise(raw, out var uri))
            {
                Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
                return;
            }
        }

        Toast("This item has no web URL.");
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
