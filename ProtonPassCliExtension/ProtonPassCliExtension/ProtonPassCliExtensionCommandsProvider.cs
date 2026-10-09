// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using ProtonPassCliExtension.Commands;
using ProtonPassCliExtension.Pages;
using ProtonPassCliExtension.Services;

namespace ProtonPassCliExtension;

public partial class ProtonPassCliExtensionCommandsProvider : CommandProvider
{
    private readonly AppServices _services = new();
    private readonly ICommandItem[] _commands;
    private readonly IFallbackCommandItem[] _fallbacks;

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
            new CommandItem(new CreateLoginPage(_services))
            {
                Title = "Proton Pass: create login",
                Subtitle = "Adds a login with a generated password",
            },
            new CommandItem(new GeneratePasswordCommand(_services))
            {
                Title = "Proton Pass: generate password",
                Subtitle = "Copies a random password",
            },
        ];
        _fallbacks = [new LoginFallbackItem(_services)];
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }

    public override IFallbackCommandItem[] FallbackCommands()
    {
        return _fallbacks;
    }
}
