using System.Collections.Generic;

namespace ProtonPassCliExtension.PassCli.Models;

internal sealed record Vault(string Name, string VaultId, string ShareId);

internal sealed record VaultListResponse(List<Vault> Vaults);

/// <summary>Metadata-only row from <c>item list</c>. Contains no secrets and no username/URL.</summary>
internal sealed record ItemSummary(
    string Id,
    string ShareId,
    string VaultId,
    string State,
    string Title,
    string ItemType);

internal sealed record ItemListResponse(List<ItemSummary> Items);

// 'item view --output json' contains the password. These records deliberately declare only
// the URL list so the password and TOTP URI are never materialised as typed values.
internal sealed record ItemViewResponse(ItemViewItem? Item);

internal sealed record ItemViewItem(ItemViewContent? Content);

internal sealed record ItemViewContent(ItemViewInner? Content);

internal sealed record ItemViewInner(ItemViewLogin? Login);

internal sealed record ItemViewLogin(List<string>? Urls);

// 'item totp --output json' also returns 'totp_uri' (contains the seed); it is not declared here.
internal sealed record TotpResponse(string? Totp);
