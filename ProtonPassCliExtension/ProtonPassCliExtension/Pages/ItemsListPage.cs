using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using ProtonPassCliExtension.Commands;
using ProtonPassCliExtension.PassCli;
using ProtonPassCliExtension.Services;

namespace ProtonPassCliExtension.Pages;

internal sealed partial class ItemsListPage : ListPage
{
    private readonly AppServices _services;
    private readonly object _gate = new();
    private IListItem[] _items = [];
    private bool _started;

    public ItemsListPage(AppServices services)
    {
        _services = services;
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = "Proton Pass (unofficial)";
        Name = "Open";
        PlaceholderText = "Search your vault...";
        ShowDetails = false;
    }

    // GetItems is called often and must never start a process: it only returns what is already loaded.
    public override IListItem[] GetItems()
    {
        bool shouldLoad;
        lock (_gate)
        {
            shouldLoad = !_started;
            _started = true;
        }

        if (shouldLoad)
        {
            _ = ReloadAsync(force: false);
        }

        return _items;
    }

    /// <summary>Drops the cache and reloads the item list.</summary>
    public void Refresh()
    {
        _services.Cache.Invalidate();
        _ = ReloadAsync(force: true);
    }

    private async Task ReloadAsync(bool force)
    {
        IsLoading = true;
        try
        {
            if (!force && _services.Cache.TryGet(_services.Settings.CacheTtl, out var cached))
            {
                Publish(cached);
                return;
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var result = await _services.Loader.LoadAsync(cts.Token).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                StatusReporter.Show(result.Error!);
                Publish([]);
                return;
            }

            _services.Cache.Set(result.Value!);
            if (result.Value!.Count == 0)
            {
                StatusReporter.Show("No items found in your vaults.", MessageState.Warning);
            }

            Publish(result.Value!);
        }
        catch (OperationCanceledException)
        {
            StatusReporter.Show(new PassCliError(PassCliErrorKind.Timeout));
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void Publish(IReadOnlyList<CachedItem> items)
    {
        _items = items.Select(ToListItem).ToArray();
        RaiseItemsChanged();
    }

    private IListItem ToListItem(CachedItem item)
    {
        var isLogin = item.ItemType == "login";
        var primary = isLogin
            ? new CopyPasswordCommand(_services, item)
            : (ICommand)new CopyReferenceCommand(_services, item);

        var more = new List<IContextItem>();
        if (isLogin)
        {
            more.Add(new CommandContextItem(new CopyUsernameCommand(_services, item)));
            more.Add(new CommandContextItem(new CopyTotpCommand(_services, item)));
            more.Add(new CommandContextItem(new OpenItemUrlCommand(_services, item)));
        }

        if (isLogin)
        {
            more.Add(new CommandContextItem(new CopyReferenceCommand(_services, item)));
        }

        more.Add(new CommandContextItem(new RefreshCommand(this)));

        return new ListItem(primary)
        {
            Title = item.Title,
            Subtitle = item.VaultName,
            Tags = [new Tag(item.ItemType.Replace('_', ' '))],
            MoreCommands = more.ToArray(),
        };
    }
}
