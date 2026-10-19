// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Runtime.CompilerServices;
using NSail.Components;

namespace NSail.Components.Tests;

/// <summary>A real choice across families (nsail#2361's ruling), each one self-hosted (OFL,
/// its license shipped beside the file) rather than named and never delivered.</summary>
public sealed class TypefacesTests
{
    [Fact]
    public void Keys_are_unique()
    {
        Assert.Equal(Typefaces.All.Count, Typefaces.All.Select(t => t.Key).Distinct().Count());
    }

    [Fact]
    public void Every_category_Leonardo_named_is_covered()
    {
        var categories = Typefaces.All.Select(t => t.Category).Distinct().ToHashSet();

        Assert.Contains(TypefaceCategory.Sans, categories);
        Assert.Contains(TypefaceCategory.Serif, categories);
        Assert.Contains(TypefaceCategory.Slab, categories);
        Assert.Contains(TypefaceCategory.Rounded, categories);
        Assert.Contains(TypefaceCategory.Display, categories);
    }

    [Fact]
    public void Find_is_case_sensitive_and_answers_null_for_an_unknown_key()
    {
        Assert.Null(Typefaces.Find(null));
        Assert.Null(Typefaces.Find("Inter"));
        Assert.NotNull(Typefaces.Find("inter"));
    }

    // Every entry's file and its OFL license actually ship -- a key with no font behind it
    // paints nothing and a key with no license is not self-hostable.
    [Fact]
    public void Every_entry_ships_its_font_file_and_its_OFL_license()
    {
        var fonts = Path.Combine(RepoRoot(), "src", "Stack", "Components", "NSail.Components.Mud", "wwwroot", "fonts");

        foreach (var typeface in Typefaces.All)
        {
            Assert.True(File.Exists(Path.Combine(fonts, $"{typeface.Key}.ttf")), $"{typeface.Key}.ttf");
            Assert.True(File.Exists(Path.Combine(fonts, $"{typeface.Key}-OFL.txt")), $"{typeface.Key}-OFL.txt");
        }
    }

    static string RepoRoot([CallerFilePath] string here = "")
    {
        // test/Stack/Components/NSail.Components.Tests/TypefacesTests.cs -> the checkout root.
        return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "..", ".."));
    }
}
