// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Components;
using NSail.Sample.Contacts;
using NSail.Sample.Home;

namespace NSail.Sample;

public sealed class Menu : INavMenuContributor
{
    public Task<IReadOnlyList<NavMenuItem>> GetItems()
    {
        return Task.FromResult<IReadOnlyList<NavMenuItem>>(
        [
            new NavMenuItem
            {
                Name = "Home",
                Icon = NsIcons.Home,
                Weight = 10,
                PageType = typeof(HomePage)
            },
            new NavMenuItem
            {
                Name = "Contacts",
                Icon = NsIcons.People,
                Weight = 20,
                PageType = typeof(ContactsPage)
            }
        ]);
    }
}
