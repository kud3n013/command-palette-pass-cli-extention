using System;
using System.Collections.Generic;
using System.Linq;

namespace ProtonPassCliExtension.Services;

/// <summary>Turns a typed domain/URL into a search token and finds the best matching login by title.</summary>
internal static class QueryMatcher
{
    private static readonly HashSet<string> SecondLevelLabels =
        new(StringComparer.OrdinalIgnoreCase) { "co", "com", "org", "net", "ac", "gov", "edu" };

    /// <summary>
    /// "https://www.github.com/login" -> "github". Returns null unless the text looks like a domain,
    /// so ordinary searches don't trigger the fallback.
    /// </summary>
    public static string? ExtractToken(string query)
    {
        var text = query.Trim();
        if (text.Length == 0 || text.Contains(' '))
        {
            return null;
        }

        var schemeEnd = text.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd >= 0)
        {
            text = text[(schemeEnd + 3)..];
        }

        var cut = text.IndexOfAny(['/', '?', '#', ':']);
        if (cut >= 0)
        {
            text = text[..cut];
        }

        var labels = text.Split('.', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (labels.Count > 0 && labels[0].Equals("www", StringComparison.OrdinalIgnoreCase))
        {
            labels.RemoveAt(0);
        }

        if (labels.Count < 2)
        {
            return null;
        }

        labels.RemoveAt(labels.Count - 1); // TLD
        if (labels.Count > 1 && SecondLevelLabels.Contains(labels[^1]))
        {
            labels.RemoveAt(labels.Count - 1); // co in example.co.uk
        }

        var token = labels[^1];
        return token.Length >= 3 ? token.ToLowerInvariant() : null;
    }

    /// <summary>Best login whose title matches: exact, then prefix, then substring.</summary>
    public static CachedItem? BestLogin(IEnumerable<CachedItem> items, string token)
    {
        return items
            .Where(i => i.ItemType == "login" && i.Title.Contains(token, StringComparison.OrdinalIgnoreCase))
            .OrderBy(i => i.Title.Equals(token, StringComparison.OrdinalIgnoreCase) ? 0
                : i.Title.StartsWith(token, StringComparison.OrdinalIgnoreCase) ? 1 : 2)
            .ThenBy(i => i.Title.Length)
            .FirstOrDefault();
    }
}
