using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace ProtonPassCliExtension.Pages;

/// <summary>
/// Dropdown filter on the list page: all vaults, or one vault by name. An instance is immutable once built;
/// the page publishes a new one after each load, because the host does not re-read an existing one.
/// </summary>
internal sealed partial class VaultFilters : Filters
{
    public const string AllId = "";

    private readonly IFilterItem[] _filters;

    public VaultFilters(IEnumerable<string> vaultNames, string currentId)
    {
        VaultNames = vaultNames.Distinct().OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToList();
        _filters = [new Filter { Id = AllId, Name = "All vaults" }, .. VaultNames.Select(n => new Filter { Id = n, Name = n })];

        // Fall back to "All vaults" if the remembered vault no longer exists.
        var known = currentId.Length > 0 && VaultNames.Contains(currentId, StringComparer.OrdinalIgnoreCase);
        CurrentFilterId = known ? VaultNames.First(n => n.Equals(currentId, StringComparison.OrdinalIgnoreCase)) : AllId;
    }

    public IReadOnlyList<string> VaultNames { get; }

    public override IFilterItem[] GetFilters() => _filters;
}
