// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using ProtonPassCliExtension.Pages;
using ProtonPassCliExtension.Services;

namespace ProtonPassCliExtension;

public partial class ProtonPassCliExtensionCommandsProvider : CommandProvider
{
    private readonly AppServices _services = new();
    private readonly ICommandItem[] _commands;

    public ProtonPassCliExtensionCommandsProvider()
    {
        DisplayName = "Proton Pass (unofficial)";
        Icon = IconHelpers.FromRelativePath("Assets\\ProtonPass.png");
        Settings = _services.Settings.CommandSettings;
        _commands = [
            new CommandItem(new ItemsListPage(_services))
            {
                Title = "Proton Pass",
                Subtitle = "Unofficial: search and copy from your vault",
            },
        ];
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }
}
