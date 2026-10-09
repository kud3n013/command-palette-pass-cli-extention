using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using ProtonPassCliExtension.Pages;

namespace ProtonPassCliExtension.Commands;

internal sealed partial class RefreshCommand : InvokableCommand
{
    private readonly ItemsListPage _page;

    public RefreshCommand(ItemsListPage page)
    {
        _page = page;
        Name = "Refresh items";
        Icon = new IconInfo("");
    }

    public override CommandResult Invoke()
    {
        _page.Refresh();
        return CommandResult.KeepOpen();
    }
}
