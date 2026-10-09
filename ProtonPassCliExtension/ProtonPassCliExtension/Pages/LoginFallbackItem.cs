using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using ProtonPassCliExtension.Commands;
using ProtonPassCliExtension.Services;

namespace ProtonPassCliExtension.Pages;

/// <summary>
/// When the typed text looks like a domain (github.com, https://www.example.co.uk/...), offers the best
/// matching login by title. Uses only cached metadata, so it never starts a process while you type.
/// </summary>
internal sealed partial class LoginFallbackItem : FallbackCommandItem
{
    private readonly AppServices _services;

    public LoginFallbackItem(AppServices services)
        : base(new NoOpCommand(), "Proton Pass login", "protonpass.fallback")
    {
        _services = services;
        Icon = IconHelpers.FromRelativePath("Assets\\Icon.png");
    }

    public override void UpdateQuery(string query)
    {
        var token = QueryMatcher.ExtractToken(query);
        var match = token is null ? null : QueryMatcher.BestLogin(_services.Cache.Peek(), token);

        if (match is null)
        {
            Title = string.Empty;
            Subtitle = string.Empty;
            Command = new NoOpCommand();
            return;
        }

        Title = $"Copy password for {match.Title}";
        Subtitle = $"Proton Pass (unofficial) · {match.VaultName}";
        Command = new CopyPasswordCommand(_services, match);
    }
}
