using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace ProtonPassCliExtension.Pages;

/// <summary>Dropdown filter on the list page: all vaults, or one vault by name.</summary>
internal sealed partial class VaultFilters : Filters
{
    public const string AllId = "";

    private IFilterItem[] _filters = [new Filter { Id = AllId, Name = "All vaults" }];

    public VaultFilters()
    {
        CurrentFilterId = AllId;
    }

    public IReadOnlyList<string> VaultNames { get; private set; } = [];

    public void SetVaults(IEnumerable<string> names)
    {
        VaultNames = names.Distinct().OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToList();
        _filters = [new Filter { Id = AllId, Name = "All vaults" }, .. VaultNames.Select(n => new Filter { Id = n, Name = n })];
    }

    public override IFilterItem[] GetFilters() => _filters;
}
