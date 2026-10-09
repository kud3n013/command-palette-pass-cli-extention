// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace ProtonPassCliExtension;

internal sealed partial class ProtonPassCliExtensionPage : ListPage
{
    public ProtonPassCliExtensionPage()
    {
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = "Proton Pass";
        Name = "Open";
    }

    public override IListItem[] GetItems()
    {
        return [
            new ListItem(new NoOpCommand()) { Title = "TODO: Implement your extension here" }
        ];
    }
}
