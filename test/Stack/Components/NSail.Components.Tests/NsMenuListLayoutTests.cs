// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components.Tests;

/// <summary>A browser alone can lay a list out; this reads the shipped stylesheet for the two
/// rules that keep a menu's rows one per line however deep under the list they stand.</summary>
public sealed class NsMenuListLayoutTests
{
    [Fact]
    public void TheListIsAColumn()
    {
        Assert.Contains(
            ".ns-menu-list {\n    display: flex;\n    flex-direction: column;\n}",
            Stylesheet().Replace("\r\n", "\n"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void ANestedMenuStandsOnItsOwnLineAtAnyDepthUnderTheList()
    {
        Assert.Contains(
            ".ns-menu-list .ns-menu-keys > .mud-menu {\n    width: 100%;\n    display: inline;\n}",
            Stylesheet().Replace("\r\n", "\n"),
            StringComparison.Ordinal);
    }

    static string Stylesheet()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "NSail.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        return File.ReadAllText(Path.Combine(
            directory.FullName,
            "src/Stack/Components/NSail.Components.Mud/wwwroot/ns-mud.css"));
    }
}
