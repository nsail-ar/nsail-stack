// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Icons;
using NSail.Components;

namespace NSail.Components.Tests;

public sealed class NavMenuItemMergeTests
{
    [Fact]
    public void KeepsALeafWithNoDuplicateUnchanged()
    {
        var items = new List<NavMenuItem>
        {
            new() { Name = "Accounts", Icon = new Glyph("badge"), PageType = typeof(NavMenuItemMergeTests) }
        };

        var merged = NavMenuItem.Merge(items);

        var item = Assert.Single(merged);
        Assert.Equal("Accounts", item.Name);
        Assert.Equal(new Glyph("badge"), item.Icon);
        Assert.Equal(typeof(NavMenuItemMergeTests), item.PageType);
    }

    [Fact]
    public void MergesTopLevelGroupsSharingAName()
    {
        var items = new List<NavMenuItem>
        {
            new()
            {
                Name = "Accounting",
                Icon = new Glyph("accounting"),
                Items = [new NavMenuItem { Name = "Accounts", PageType = typeof(NavMenuItemMergeTests) }]
            },
            new()
            {
                Name = "Accounting",
                Items = [new NavMenuItem { Name = "Vouchers", PageType = typeof(NavMenuItem) }]
            }
        };

        var merged = NavMenuItem.Merge(items);

        var group = Assert.Single(merged);
        Assert.Equal("Accounting", group.Name);
        Assert.Equal(new Glyph("accounting"), group.Icon);
        Assert.Equal(2, group.Items.Count);
        Assert.Equal("Accounts", group.Items[0].Name);
        Assert.Equal("Vouchers", group.Items[1].Name);
    }

    // Root carries a second child ("Other") so the shape stays a genuine two-level tree —
    // the single-child fold is a different behavior with its own tests below, and this one
    // is only about Sub merging A and B at its own level.
    [Fact]
    public void MergesNestedGroupsAtEveryLevel()
    {
        var items = new List<NavMenuItem>
        {
            new()
            {
                Name = "Root",
                Items =
                [
                    new NavMenuItem
                    {
                        Name = "Sub",
                        Items = [new NavMenuItem { Name = "A", PageType = typeof(NavMenuItemMergeTests) }]
                    },
                    new NavMenuItem { Name = "Other", PageType = typeof(NavMenuItem) }
                ]
            },
            new()
            {
                Name = "Root",
                Items =
                [
                    new NavMenuItem
                    {
                        Name = "Sub",
                        Items = [new NavMenuItem { Name = "B", PageType = typeof(NavMenuItem) }]
                    }
                ]
            }
        };

        var merged = NavMenuItem.Merge(items);

        var root = Assert.Single(merged);
        Assert.Equal("Root", root.Name);
        var sub = root.Items.Single(item => item.Name == "Sub");
        Assert.Equal(2, sub.Items.Count);
        Assert.Equal("A", sub.Items[0].Name);
        Assert.Equal("B", sub.Items[1].Name);
    }

    // The kit plants the glyph and the app changes it, so the LAST one contributed wins and
    // an app registered after the kits is the one holding the pen. A contribution that names
    // no icon is not an opinion about it: it leaves whatever came before standing.
    [Fact]
    public void TheLastIconContributedWins()
    {
        var items = new List<NavMenuItem>
        {
            new() { Name = "Accounting", Icon = new Glyph("kit-icon"), Items = [] },
            new() { Name = "Accounting", Icon = new Glyph("app-icon"), Items = [] }
        };

        var merged = NavMenuItem.Merge(items);

        Assert.Equal(new Glyph("app-icon"), Assert.Single(merged).Icon);
    }

    [Fact]
    public void AnOverrideNamingNoIconLeavesTheKitsStanding()
    {
        var items = new List<NavMenuItem>
        {
            new() { Name = "Accounting", Icon = new Glyph("kit-icon"), Items = [] },
            new() { Name = "Accounting", Weight = 25, Items = [] }
        };

        var merged = NavMenuItem.Merge(items);

        var accounting = Assert.Single(merged);
        Assert.Equal(new Glyph("kit-icon"), accounting.Icon);
        Assert.Equal(25, accounting.Weight);
    }

    [Fact]
    public void PreservesContributionOrderAcrossContributors()
    {
        var items = new List<NavMenuItem>
        {
            new() { Name = "Second" },
            new() { Name = "First" },
            new() { Name = "Second" }
        };

        var merged = NavMenuItem.Merge(items);

        Assert.Equal(["Second", "First"], merged.Select(item => item.Name));
    }

    [Fact]
    public void OrdersGroupsByWeightAscending()
    {
        var items = new List<NavMenuItem>
        {
            new() { Name = "Settings", Weight = 60 },
            new() { Name = "Directory", Weight = 50 },
            new() { Name = "Scheduling", Weight = 5 }
        };

        var merged = NavMenuItem.Merge(items);

        Assert.Equal(["Scheduling", "Directory", "Settings"], merged.Select(item => item.Name));
    }

    [Fact]
    public void KeepsContributionOrderBetweenEqualWeights()
    {
        var items = new List<NavMenuItem>
        {
            new() { Name = "Second", Weight = 10 },
            new() { Name = "Third", Weight = 10 },
            new() { Name = "First", Weight = 5 }
        };

        var merged = NavMenuItem.Merge(items);

        Assert.Equal(["First", "Second", "Third"], merged.Select(item => item.Name));
    }

    // Reordering is the app's word over the kit's, so the weight is the last one contributed
    // and not the smallest: a door the app wants at the tail cannot be pulled back up by the
    // kit that planted it.
    [Fact]
    public void AMergedEntryTakesTheLastWeightContributed()
    {
        var items = new List<NavMenuItem>
        {
            new() { Name = "Directory", Weight = 50 },
            new() { Name = "Accounting", Weight = 30 },
            new() { Name = "Directory", Weight = 90 }
        };

        var merged = NavMenuItem.Merge(items);

        Assert.Equal(["Accounting", "Directory"], merged.Select(item => item.Name));
        Assert.Equal(90, merged.Single(item => item.Name == "Directory").Weight);
    }

    [Fact]
    public void AnEntryNobodyWeighsSortsWhereAZeroWould()
    {
        var items = new List<NavMenuItem>
        {
            new() { Name = "Weighed", Weight = 5 },
            new() { Name = "Unweighed" }
        };

        var merged = NavMenuItem.Merge(items);

        Assert.Equal(["Unweighed", "Weighed"], merged.Select(item => item.Name));
        Assert.Null(merged.Single(item => item.Name == "Unweighed").Weight);
    }

    // Optical grows Directory's flat "Parties" leaf into a group of role-filtered entries by
    // contributing Items under the same Name. The leaf keeps its PageType, but the renderer
    // reads Items first, so the merged node arrives as a group and its siblings do not.
    [Fact]
    public void ALeafGainingItemsFromAnotherContributorBecomesAGroup()
    {
        var items = new List<NavMenuItem>
        {
            new()
            {
                Name = "Directory",
                Items =
                [
                    new NavMenuItem { Name = "Parties", Icon = new Glyph("people"), PageType = typeof(NavMenuItem) },
                    new NavMenuItem { Name = "Roles", PageType = typeof(NavMenuItemMergeTests) }
                ]
            },
            new()
            {
                Name = "Directory",
                Items =
                [
                    new NavMenuItem
                    {
                        Name = "Parties",
                        Items =
                        [
                            new NavMenuItem { Name = "All", PageType = typeof(NavMenuItem) },
                            new NavMenuItem { Name = "Patients", PageType = typeof(NavMenuItem) }
                        ]
                    }
                ]
            }
        };

        var merged = NavMenuItem.Merge(items);

        var directory = Assert.Single(merged);
        var parties = directory.Items.Single(item => item.Name == "Parties");

        Assert.Equal(["All", "Patients"], parties.Items.Select(item => item.Name));
        Assert.Equal(new Glyph("people"), parties.Icon);
        Assert.Equal(typeof(NavMenuItem), parties.PageType);

        var roles = directory.Items.Single(item => item.Name == "Roles");
        Assert.Empty(roles.Items);
    }

    // Two children naming one page differ only by their deep-link Parameters, so the merge
    // must keep them apart rather than collapse them onto the page they share.
    [Fact]
    public void KeepsSiblingsNamingOnePageApartByName()
    {
        var items = new List<NavMenuItem>
        {
            new()
            {
                Name = "Parties",
                Items =
                [
                    new NavMenuItem { Name = "All", PageType = typeof(NavMenuItem) },
                    new NavMenuItem
                    {
                        Name = "Patients",
                        PageType = typeof(NavMenuItem),
                        Parameters = new { RoleIds = new[] { Guid.Empty } }
                    }
                ]
            }
        };

        var merged = NavMenuItem.Merge(items);

        var parties = Assert.Single(merged);
        Assert.Equal(2, parties.Items.Count);
        Assert.Null(parties.Items[0].Parameters);
        Assert.NotNull(parties.Items[1].Parameters);
    }

    // Óptica's SETTINGS "Optical" group carries exactly one member, Workshops: the group
    // itself must not render, only Workshops, at the position the group would have held.
    // The settings tree is the caller that asks for the fold — SettingsMenu merges its own
    // subtree with the flag on — and the test right below is the same shape without it.
    [Fact]
    public void ASingleMemberSettingsGroupCollapsesToItsOnlyChild()
    {
        var items = new List<NavMenuItem>
        {
            new()
            {
                Name = "Optical",
                Icon = new Glyph("frame"),
                Weight = 40,
                Items = [new NavMenuItem { Name = "Workshops", Icon = new Glyph("products"), PageType = typeof(NavMenuItemMergeTests) }]
            }
        };

        var merged = NavMenuItem.Merge(items, collapseSingleChildGroups: true);

        var item = Assert.Single(merged);
        Assert.Equal("Workshops", item.Name);
        Assert.Equal(new Glyph("products"), item.Icon);
        Assert.Equal(typeof(NavMenuItemMergeTests), item.PageType);
        Assert.Equal(40, item.Weight);
        Assert.Empty(item.Items);
    }

    // The nav's own merge — the default — keeps the group whatever it holds. Leonardo on
    // Compras, whose single member had flattened Órdenes de Compra onto the top level:
    // "me gustaba el menu compras, que vuelva". The group is the domain's map, and the
    // place the section's next screen (recepciones, proveedores) is going to be born in.
    [Fact]
    public void ASingleMemberNavGroupKeepsItsLevel()
    {
        var items = new List<NavMenuItem>
        {
            new()
            {
                Name = "Purchasing",
                Icon = new Glyph("receipt"),
                Weight = 20,
                Items = [new NavMenuItem { Name = "PurchaseOrders", PageType = typeof(NavMenuItemMergeTests) }]
            }
        };

        var merged = NavMenuItem.Merge(items);

        var group = Assert.Single(merged);
        Assert.Equal("Purchasing", group.Name);
        Assert.Equal(new Glyph("receipt"), group.Icon);
        Assert.Equal(20, group.Weight);
        Assert.Equal(["PurchaseOrders"], group.Items.Select(item => item.Name));
    }

    // The fold is born the moment a second contributor lands under the same group name:
    // once two members exist, the group renders as a group, not as one of its children.
    [Fact]
    public void ASecondMemberStopsTheGroupFromCollapsing()
    {
        var items = new List<NavMenuItem>
        {
            new()
            {
                Name = "Optical",
                Items = [new NavMenuItem { Name = "Workshops", PageType = typeof(NavMenuItemMergeTests) }]
            },
            new()
            {
                Name = "Optical",
                Items = [new NavMenuItem { Name = "Colors", PageType = typeof(NavMenuItem) }]
            }
        };

        var merged = NavMenuItem.Merge(items, collapseSingleChildGroups: true);

        var group = Assert.Single(merged);
        Assert.Equal("Optical", group.Name);
        Assert.Equal(["Workshops", "Colors"], group.Items.Select(item => item.Name));
    }

    // A subgroup that collapses can leave its own parent down to one child, and that fold
    // must fire too: the doctrine is bottom-up, not one level deep.
    [Fact]
    public void ACollapseCascadesUpWhenItLeavesTheParentWithOneChildToo()
    {
        var items = new List<NavMenuItem>
        {
            new()
            {
                Name = "Root",
                Weight = 5,
                Items =
                [
                    new NavMenuItem
                    {
                        Name = "Sub",
                        Weight = 10,
                        Items = [new NavMenuItem { Name = "Leaf", PageType = typeof(NavMenuItemMergeTests) }]
                    }
                ]
            }
        };

        var merged = NavMenuItem.Merge(items, collapseSingleChildGroups: true);

        var item = Assert.Single(merged);
        Assert.Equal("Leaf", item.Name);
        Assert.Equal(5, item.Weight);
        Assert.Empty(item.Items);
    }

    // The story in one test: the kit plants Calendario at the first level and an Agenda group
    // under it, and the app moves Calendario inside that group by naming the two and nothing
    // else — the page, the icon and the label stay the kit's.
    [Fact]
    public void AParentOverrideMovesTheKitsDoorUnderThatGroup()
    {
        var items = new List<NavMenuItem>
        {
            new() { Name = "Calendar", Icon = new Glyph("today"), Weight = 2, PageType = typeof(NavMenuItemMergeTests) },
            new()
            {
                Name = "Scheduling",
                Icon = new Glyph("calendar"),
                Weight = 5,
                Items = [new NavMenuItem { Name = "Appointments", Weight = 20, PageType = typeof(NavMenuItem) }]
            },
            new() { Name = "Scheduling", Icon = new Glyph("lens"), Weight = 25 },
            new() { Name = "Calendar", Parent = "Scheduling", Weight = 10 }
        };

        var merged = NavMenuItem.Merge(items);

        var scheduling = Assert.Single(merged);
        Assert.Equal("Scheduling", scheduling.Name);
        Assert.Equal(new Glyph("lens"), scheduling.Icon);
        Assert.Equal(25, scheduling.Weight);

        Assert.Equal(["Calendar", "Appointments"], scheduling.Items.Select(item => item.Name));

        var calendar = scheduling.Items[0];
        Assert.Equal(new Glyph("today"), calendar.Icon);
        Assert.Equal(typeof(NavMenuItemMergeTests), calendar.PageType);
        Assert.Equal(10, calendar.Weight);
        Assert.Null(calendar.Parent);
    }

    // A moved entry lands among the group's children and merges with the one already
    // answering to its Name, which is how an app reaches a leaf the kit nested rather than
    // only the doors it planted at the first level.
    [Fact]
    public void AMovedEntryMergesWithTheChildOfThatNameAlreadyThere()
    {
        var items = new List<NavMenuItem>
        {
            new()
            {
                Name = "Scheduling",
                Items =
                [
                    new NavMenuItem { Name = "Appointments", Icon = new Glyph("schedule"), Weight = 20, PageType = typeof(NavMenuItem) },
                    new NavMenuItem { Name = "PendingOutbounds", Weight = 30, PageType = typeof(NavMenuItemMergeTests) }
                ]
            },
            new() { Name = "Appointments", Parent = "Scheduling", Weight = 90 }
        };

        var merged = NavMenuItem.Merge(items);

        var scheduling = Assert.Single(merged);
        Assert.Equal(["PendingOutbounds", "Appointments"], scheduling.Items.Select(item => item.Name));

        var appointments = scheduling.Items[1];
        Assert.Equal(new Glyph("schedule"), appointments.Icon);
        Assert.Equal(typeof(NavMenuItem), appointments.PageType);
    }

    // A kit entry the app never names is left exactly as the kit put it — the whole promise
    // that makes mounting a kit's menu cheaper than copying it.
    [Fact]
    public void AKitEntryNobodyOverridesIsUntouched()
    {
        var items = new List<NavMenuItem>
        {
            new()
            {
                Name = "Scheduling",
                Icon = new Glyph("calendar"),
                Weight = 5,
                Items = [new NavMenuItem { Name = "PendingOutbounds", Icon = new Glyph("call"), Weight = 30, PageType = typeof(NavMenuItem) }]
            },
            new() { Name = "Scheduling", Weight = 25 }
        };

        var pending = Assert.Single(Assert.Single(NavMenuItem.Merge(items)).Items);

        Assert.Equal("PendingOutbounds", pending.Name);
        Assert.Equal(new Glyph("call"), pending.Icon);
        Assert.Equal(30, pending.Weight);
        Assert.Equal(typeof(NavMenuItem), pending.PageType);
    }

    [Fact]
    public void AnEntryHiddenByTheAppCarriesItsBranchOutOfTheDrawer()
    {
        var items = new List<NavMenuItem>
        {
            new()
            {
                Name = "Scheduling",
                Weight = 5,
                Items = [new NavMenuItem { Name = "Appointments", PageType = typeof(NavMenuItem) }]
            },
            new() { Name = "Scheduling", Visible = false }
        };

        var scheduling = Assert.Single(NavMenuItem.Merge(items));

        Assert.False(scheduling.Visible);
        Assert.Equal(["Appointments"], scheduling.Items.Select(item => item.Name));
    }

    // Hidden is a state the tree carries, never a deletion: NsTitleBar derives a page's glyph
    // from its menu entry, so a screen whose door the app took out of the drawer would lose
    // the icon on its own title bar if Find could no longer see it.
    [Fact]
    public void AHiddenEntryStillAnswersFind()
    {
        var items = new List<NavMenuItem>
        {
            new()
            {
                Name = "Scheduling",
                Items = [new NavMenuItem { Name = "Appointments", Icon = new Glyph("schedule"), PageType = typeof(NavMenuItem) }]
            },
            new() { Name = "Scheduling", Visible = false }
        };

        var merged = NavMenuItem.Merge(items);
        var found = NavMenuItem.Find(merged, typeof(NavMenuItem));

        Assert.NotNull(found);
        Assert.Equal(new Glyph("schedule"), found.Icon);
    }

    [Fact]
    public void AParentNobodyContributesThrows()
    {
        var items = new List<NavMenuItem>
        {
            new() { Name = "Calendar", Parent = "Contactologia", PageType = typeof(NavMenuItem) }
        };

        var error = Assert.Throws<InvalidOperationException>(() => NavMenuItem.Merge(items));

        Assert.Contains("Contactologia", error.Message);
    }

    [Fact]
    public void TwoEntriesHangingUnderEachOtherThrow()
    {
        var items = new List<NavMenuItem>
        {
            new() { Name = "Calendar", Parent = "Scheduling" },
            new() { Name = "Scheduling", Parent = "Calendar" }
        };

        Assert.Throws<InvalidOperationException>(() => NavMenuItem.Merge(items));
    }

    // The separator is placed and not named, so nothing about the override rules touches it:
    // it keeps its identity beside entries that moved out of its level entirely.
    [Fact]
    public void ASeparatorKeepsItsPlaceWhileAnEntryMovesAway()
    {
        var separator = NavMenuItem.Separator(2);

        var items = new List<NavMenuItem>
        {
            new() { Name = "Home", Weight = 1, PageType = typeof(NavMenuItem) },
            separator,
            new() { Name = "Scheduling", Weight = 25 },
            new() { Name = "Calendar", Parent = "Scheduling", Weight = 10, PageType = typeof(NavMenuItemMergeTests) }
        };

        var merged = NavMenuItem.Merge(items);

        Assert.Equal(["Home", "", "Scheduling"], merged.Select(item => item.Name));
        Assert.Same(separator, merged[1]);
    }

    [Fact]
    public void OrdersNestedGroupsByWeightToo()
    {
        var items = new List<NavMenuItem>
        {
            new()
            {
                Name = "Root",
                Items =
                [
                    new NavMenuItem { Name = "Late", Weight = 20 },
                    new NavMenuItem { Name = "Early", Weight = 10 }
                ]
            }
        };

        var merged = NavMenuItem.Merge(items);

        var root = Assert.Single(merged);
        Assert.Equal(["Early", "Late"], root.Items.Select(item => item.Name));
    }

    // The app tightens a kit's door by naming it and nothing else: Permission is an
    // arrangement field, so it takes the LAST non-null contribution the way Visible does, and a
    // row that says nothing about it leaves the kit's own word standing.
    [Fact]
    public void TakesTheLastPermissionContributedForAName()
    {
        var items = new List<NavMenuItem>
        {
            new() { Name = "WorkOrders", PageType = typeof(NavMenuItem) },
            new() { Name = "WorkOrders", Permission = typeof(NavMenuItemMergeTests) }
        };

        var merged = NavMenuItem.Merge(items);

        Assert.Equal(typeof(NavMenuItemMergeTests), Assert.Single(merged).Permission);
    }

    [Fact]
    public void AnOverrideSayingNothingAboutThePermissionKeepsTheOneContributed()
    {
        var items = new List<NavMenuItem>
        {
            new() { Name = "WorkOrders", PageType = typeof(NavMenuItem), Permission = typeof(NavMenuItemMergeTests) },
            new() { Name = "WorkOrders", Weight = 9 }
        };

        var merged = NavMenuItem.Merge(items);

        var item = Assert.Single(merged);
        Assert.Equal(typeof(NavMenuItemMergeTests), item.Permission);
        Assert.Equal(9, item.Weight);
    }

    // A gate on a group has to survive the move, since that is how an app reaches a door a kit
    // nested: Parent spends itself getting the entry there and the Permission does not.
    [Fact]
    public void AMovedEntryCarriesItsPermissionIntoTheGroup()
    {
        var items = new List<NavMenuItem>
        {
            new() { Name = "MyAccount" },
            new()
            {
                Name = "MyBalance",
                Parent = "MyAccount",
                PageType = typeof(NavMenuItem),
                Permission = typeof(NavMenuItemMergeTests)
            }
        };

        var merged = NavMenuItem.Merge(items);

        var child = Assert.Single(Assert.Single(merged).Items);
        Assert.Equal(typeof(NavMenuItemMergeTests), child.Permission);
    }

    // The fold hands a group's place to its only child, and there is one slot for a gate: a
    // group that declared one keeps its level rather than letting the fold drop it.
    [Fact]
    public void ASettingsGroupCarryingAPermissionIsNotFoldedOntoItsOnlyChild()
    {
        var items = new List<NavMenuItem>
        {
            new()
            {
                Name = "Optical",
                Weight = 40,
                Permission = typeof(NavMenuItemMergeTests),
                Items = [new NavMenuItem { Name = "Workshops", PageType = typeof(NavMenuItem) }]
            }
        };

        var merged = NavMenuItem.Merge(items, collapseSingleChildGroups: true);

        var group = Assert.Single(merged);
        Assert.Equal("Optical", group.Name);
        Assert.Equal(typeof(NavMenuItemMergeTests), group.Permission);
        Assert.Equal("Workshops", Assert.Single(group.Items).Name);
    }

    // The same rule from the other side: the fold does happen, so the child's own gate has to
    // reach the slot it is handed — dropped there, the fold opens a door somebody closed.
    [Fact]
    public void AFoldedChildTakesItsOwnPermissionIntoTheGroupsPlace()
    {
        var items = new List<NavMenuItem>
        {
            new()
            {
                Name = "Optical",
                Weight = 40,
                Items =
                [
                    new NavMenuItem
                    {
                        Name = "Workshops",
                        PageType = typeof(NavMenuItem),
                        Permission = typeof(NavMenuItemMergeTests)
                    }
                ]
            }
        };

        var merged = NavMenuItem.Merge(items, collapseSingleChildGroups: true);

        var item = Assert.Single(merged);
        Assert.Equal("Workshops", item.Name);
        Assert.Equal(typeof(NavMenuItemMergeTests), item.Permission);
    }
}
