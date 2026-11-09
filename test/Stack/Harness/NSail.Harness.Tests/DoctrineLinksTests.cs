// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.RegularExpressions;

namespace NSail.Harness.Tests;

// A product reads the doctrine through the plugin alone: a link that leaves it is a dead end.
public partial class DoctrineLinksTests
{
    [GeneratedRegex(@"\]\((?<target>[^)\s]+)\)")]
    private static partial Regex Link();

    [GeneratedRegex(@"(?<![\w/.\-])(?<name>[\w\-]+\.md)\b")]
    private static partial Regex PageName();

    // Files the doctrine names that belong to the codebase it describes, never to the plugin.
    static readonly HashSet<string> ProductPages = ["CLAUDE.md"];

    [GeneratedRegex("`[^`]*`")]
    private static partial Regex CodeSpan();

    static string Plugin()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "NSail.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);

        return Path.Combine(dir.FullName, "plugins", "nsail-stack");
    }

    [Fact]
    public void EveryRelativeLinkInTheDoctrineResolvesInsideThePlugin()
    {
        var plugin = Path.GetFullPath(Plugin());
        var broken = new List<string>();

        foreach (var file in Directory.EnumerateFiles(plugin, "*.md", SearchOption.AllDirectories))
        {
            foreach (Match match in Link().Matches(CodeSpan().Replace(File.ReadAllText(file), "")))
            {
                var target = match.Groups["target"].Value;
                var path = target.Split('#')[0];

                if (path.Length == 0 || path.Contains("://") || path.StartsWith("mailto:"))
                {
                    continue;
                }

                var full = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, path));

                if (!full.StartsWith(plugin + Path.DirectorySeparatorChar) || !(File.Exists(full) || Directory.Exists(full)))
                {
                    broken.Add($"{Path.GetRelativePath(plugin, file)}: {target}");
                }
            }
        }

        Assert.Empty(broken);
    }

    [Fact]
    public void EveryBarePageNameInTheDoctrineIsAPageOfThePluginOrSaysItIsAProductsOwn()
    {
        var plugin = Path.GetFullPath(Plugin());
        var pages = Directory.EnumerateFiles(plugin, "*.md", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .ToHashSet();
        var stray = new List<string>();

        foreach (var file in Directory.EnumerateFiles(plugin, "*.md", SearchOption.AllDirectories))
        {
            foreach (var line in File.ReadAllLines(file))
            {
                foreach (Match match in PageName().Matches(line))
                {
                    var name = match.Groups["name"].Value;

                    if (!pages.Contains(name) && !ProductPages.Contains(name) && !line.Contains("/" + name))
                    {
                        stray.Add($"{Path.GetRelativePath(plugin, file)}: {name}");
                    }
                }
            }
        }

        Assert.Empty(stray);
    }
}
