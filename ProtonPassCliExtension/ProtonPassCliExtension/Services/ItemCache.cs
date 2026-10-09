using System;
using System.Collections.Generic;

namespace ProtonPassCliExtension.Services;

/// <summary>Metadata only. There is deliberately no field for any secret value.</summary>
internal sealed record CachedItem(string ShareId, string ItemId, string Title, string VaultName, string ItemType);

internal sealed class ItemCache
{
    private readonly TimeProvider _time;
    private readonly object _gate = new();
    private IReadOnlyList<CachedItem>? _items;
    private DateTimeOffset _loadedAt;

    public ItemCache(TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
    }

    public bool TryGet(TimeSpan ttl, out IReadOnlyList<CachedItem> items)
    {
        lock (_gate)
        {
            if (_items is not null && _time.GetUtcNow() - _loadedAt < ttl)
            {
                items = _items;
                return true;
            }
        }

        items = [];
        return false;
    }

    /// <summary>Whatever is cached, even if past its lifetime. Metadata only, so stale data is harmless here.</summary>
    public IReadOnlyList<CachedItem> Peek()
    {
        lock (_gate)
        {
            return _items ?? [];
        }
    }

    public void Set(IReadOnlyList<CachedItem> items)
    {
        lock (_gate)
        {
            _items = items;
            _loadedAt = _time.GetUtcNow();
        }
    }

    public void Invalidate()
    {
        lock (_gate)
        {
            _items = null;
        }
    }
}
