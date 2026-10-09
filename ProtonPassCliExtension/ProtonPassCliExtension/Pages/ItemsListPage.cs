using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;
using ProtonPassCliExtension.Commands;
using ProtonPassCliExtension.PassCli;
using ProtonPassCliExtension.Services;

namespace ProtonPassCliExtension.Pages;

internal sealed partial class ItemsListPage : ListPage
{
    private readonly AppServices _services;
    private readonly object _gate = new();
    private IListItem[] _items = [];
    private bool _loading;
    private bool _everLoaded;
    private bool _lastLoadFailed;
    private bool _defaultVaultApplied;
    private IReadOnlyList<CachedItem> _all = [];
    private readonly VaultFilters _filters = new();

    public ItemsListPage(AppServices services)
    {
        _services = services;
        Icon = IconHelpers.FromRelativePath("Assets\\ProtonPass.png");
        Title = "Proton Pass (unofficial)";
        Name = "Open";
        PlaceholderText = "Search your vault...";
        ShowDetails = false;
        Filters = _filters;
        _filters.PropChanged += (_, _) => Rebuild();

        // New path or other settings: forget any failure and try again.
        _services.Settings.Changed += (_, _) => Refresh();
    }

    // GetItems is called often and must never start a process itself. It returns what is already loaded
    // and, at most, kicks off a background reload (first open, or the cache lifetime has passed).
    // After a failed load it does not retry on its own; the placeholder row's Enter does.
    public override IListItem[] GetItems()
    {
        var start = false;
        lock (_gate)
        {
            if (!_loading && !_lastLoadFailed
                && (!_everLoaded || !_services.Cache.TryGet(_services.Settings.CacheTtl, out _)))
            {
                _loading = true;
                start = true;
            }
        }

        if (start)
        {
            _ = ReloadAsync(force: false);
        }

        return _items;
    }

    /// <summary>Drops the cache and reloads the item list. Also clears a previous failure.</summary>
    public void Refresh()
    {
        lock (_gate)
        {
            _services.Cache.Invalidate();
            _lastLoadFailed = false;
            if (_loading)
            {
                return;
            }

            _loading = true;
        }

        _ = ReloadAsync(force: true);
    }

    private async Task ReloadAsync(bool force)
    {
        IsLoading = true;
        try
        {
            IReadOnlyList<CachedItem> items;
            if (!force && _services.Cache.TryGet(_services.Settings.CacheTtl, out var cached))
            {
                items = cached;
            }
            else
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                var result = await _services.Loader.LoadAsync(cts.Token).ConfigureAwait(false);
                if (!result.IsSuccess)
                {
                    Fail(result.Error!);
                    return;
                }

                items = result.Value!;
                _services.Cache.Set(items);
            }

            lock (_gate)
            {
                _everLoaded = true;
                _lastLoadFailed = false;
            }

            _all = items;
            _filters.SetVaults(items.Select(i => i.VaultName));
            ApplyDefaultVault();

            if (items.Count == 0)
            {
                StatusReporter.Show("No items found in your vaults.", MessageState.Warning);
            }
            else
            {
                StatusReporter.Hide();
            }

            Rebuild();
        }
        catch (OperationCanceledException)
        {
            Fail(new PassCliError(PassCliErrorKind.Timeout));
        }
        finally
        {
            lock (_gate)
            {
                _loading = false;
            }

            IsLoading = false;
        }
    }

    // Preselects the "Default vault" setting once; later filter changes by the user are left alone.
    private void ApplyDefaultVault()
    {
        if (_defaultVaultApplied)
        {
            return;
        }

        _defaultVaultApplied = true;
        var wanted = _services.Settings.DefaultVault;
        var match = _filters.VaultNames.FirstOrDefault(n => n.Equals(wanted, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(wanted) && match is not null)
        {
            _filters.CurrentFilterId = match;
        }
    }

    private void Rebuild()
    {
        var filter = _filters.CurrentFilterId;
        var visible = string.IsNullOrEmpty(filter)
            ? _all
            : _all.Where(i => i.VaultName.Equals(filter, StringComparison.OrdinalIgnoreCase)).ToList();

        _items = visible.Count == 0
            ? [Placeholder(
                _all.Count == 0 ? "No items found" : "No items in this vault",
                _all.Count == 0 ? "Your vaults are empty. Add items in Proton Pass, then press Enter to refresh." : "Choose another vault in the filter, or press Enter to refresh.")]
            : visible.Select(ToListItem).ToArray();
        RaiseItemsChanged();
    }

    private void Fail(PassCliError error)
    {
        lock (_gate)
        {
            _lastLoadFailed = true;
        }

        var title = error.Kind switch
        {
            PassCliErrorKind.NotInstalled => "pass-cli not found",
            PassCliErrorKind.NotLoggedIn => "Not logged in to Proton Pass",
            PassCliErrorKind.Timeout => "pass-cli timed out",
            _ => "Couldn't load Proton Pass items",
        };

        _items = [Placeholder(title, StatusReporter.Describe(error) + " Press Enter to retry.")];
        StatusReporter.Show(error);
        RaiseItemsChanged();
    }

    private static KeyChord Shortcut(VirtualKey key) => KeyChordHelpers.FromModifiers(ctrl: true, alt: false, shift: false, vkey: (int)key, scanCode: 0);

    private ListItem Placeholder(string title, string subtitle) =>
        new(new RefreshCommand(this))
        {
            Title = title,
            Subtitle = subtitle,
            Icon = new IconInfo(""),
        };

    private IListItem ToListItem(CachedItem item)
    {
        var isLogin = item.ItemType == "login";
        ICommand primary = isLogin
            ? new CopyPasswordCommand(_services, item)
            : new CopyReferenceCommand(_services, item);

        var more = new List<IContextItem>();
        if (isLogin)
        {
            more.Add(new CommandContextItem(new CopyUsernameCommand(_services, item)) { RequestedShortcut = Shortcut(VirtualKey.U) });
            more.Add(new CommandContextItem(new CopyEmailCommand(_services, item)) { RequestedShortcut = Shortcut(VirtualKey.E) });
            more.Add(new CommandContextItem(new CopyTotpCommand(_services, item)) { RequestedShortcut = Shortcut(VirtualKey.T) });
            more.Add(new CommandContextItem(new OpenItemUrlCommand(_services, item)));
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
