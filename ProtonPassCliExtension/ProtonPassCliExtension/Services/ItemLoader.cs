using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ProtonPassCliExtension.PassCli;

namespace ProtonPassCliExtension.Services;

/// <summary>Loads item metadata from every vault. pass-cli has no "all vaults" listing, so this fans out per vault.</summary>
internal sealed class ItemLoader
{
    private readonly PassCliClient _client;

    public ItemLoader(PassCliClient client)
    {
        _client = client;
    }

    public async Task<PassCliResult<IReadOnlyList<CachedItem>>> LoadAsync(CancellationToken ct = default)
    {
        var vaults = await _client.ListVaultsAsync(ct).ConfigureAwait(false);
        if (!vaults.IsSuccess)
        {
            return PassCliResult<IReadOnlyList<CachedItem>>.Fail(vaults.Error!);
        }

        var perVault = await Task.WhenAll(vaults.Value!.Select(async vault =>
            (vault, result: await _client.ListItemsAsync(vault.ShareId, ct).ConfigureAwait(false)))).ConfigureAwait(false);

        var failed = perVault.FirstOrDefault(r => !r.result.IsSuccess);
        if (failed.result is not null)
        {
            return PassCliResult<IReadOnlyList<CachedItem>>.Fail(failed.result.Error!);
        }

        var items = perVault
            .SelectMany(r => r.result.Value!.Select(i => new CachedItem(i.ShareId, i.Id, i.Title, r.vault.Name, i.ItemType)))
            .OrderBy(i => i.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return PassCliResult<IReadOnlyList<CachedItem>>.Ok(items);
    }
}
