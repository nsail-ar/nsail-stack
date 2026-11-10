// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components.Tests;

/// <summary>nsail#2970: a count rides the row's right edge at every depth. The vendor zeroes a
/// nested entry's end padding, so the drawer states one end padding for every row and the
/// count cannot leave the menu.</summary>
public sealed class NsNavMenuNestedCountEdgeTests
{
    [Fact]
    public void EveryDrawerRowKeepsItsEndPaddingAtAnyDepth()
    {
        var css = File.ReadAllText(Path.Combine(Root(), "src/Stack/Components/NSail.Components.Mud/wwwroot/ns-mud.css"));

        Assert.Matches(
            @"\.ns-nav-drawer \.mud-nav-link\s*\{[^}]*padding-inline-end:\s*16px\s*!important;",
            css);
    }

    static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NSail.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"NSail.sln was not found above {AppContext.BaseDirectory}.");
    }
}
