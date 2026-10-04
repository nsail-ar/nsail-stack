// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;

namespace NSail.Components;

/// <summary>Contributed guide chapter: Name is the localization key of its title, the same
/// role it plays on NavMenuItem and OnboardingStep, and File names the markdown beside the
/// contributing module — its address in the guide (/guide/{File}) and its file on disk. The
/// prose is never in an assembly: it is a static web asset of the module's own project, so a
/// chapter costs a fetch when it is opened and nothing before that.</summary>
public sealed class GuideItem
{
    /// <summary>The default the culture chain falls back to: a guide nobody translated is
    /// better read in the language its owner wrote it in than rendered as its own keys.</summary>
    public const string DefaultCulture = "es";

    public required string Name { get; init; }

    /// <summary>The chapter's file, with no culture and no extension — "work-orders" for
    /// wwwroot/guide/es/work-orders.md. Also the segment its address carries.</summary>
    public required string File { get; init; }

    /// <summary>Sort key across all contributors, same bands as NavMenuItem.Weight; ties keep
    /// contribution order.</summary>
    public int Weight { get; init; }

    /// <summary>Assembly whose static web assets carry the file — the contributing module's
    /// own, stamped by Collect, never written by a contributor: the class that contributes a
    /// chapter and the project that ships it are the same project by construction.</summary>
    public string Assembly { get; private set; } = string.Empty;

    /// <summary>Where the chapter's folder is served from, which is also what a relative image
    /// inside it resolves against.</summary>
    public string Folder(string culture)
    {
        return string.Create(CultureInfo.InvariantCulture, $"_content/{Assembly}/guide/{culture}/");
    }

    public string Path(string culture)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{Folder(culture)}{File}.md");
    }

    /// <summary>Every contributor's chapters in one guide, ordered by Weight ascending.</summary>
    public static async Task<IReadOnlyList<GuideItem>> Collect(
        IEnumerable<IGuideContributor> contributors)
    {
        ArgumentNullException.ThrowIfNull(contributors);

        var items = new List<GuideItem>();

        foreach (var contributor in contributors)
        {
            var assembly = contributor.GetType().Assembly.GetName().Name ?? string.Empty;

            foreach (var item in await contributor.GetItems())
            {
                item.Assembly = assembly;
                items.Add(item);
            }
        }

        // OrderBy is stable, so equal weights keep the contribution order above.
        return items.OrderBy(item => item.Weight).ToList();
    }

    /// <summary>The chapter an address names, the first one when it names none, and null when
    /// there is no guide at all — so /guide and /guide/{unknown} both land somewhere.</summary>
    public static GuideItem? Open(IReadOnlyList<GuideItem> chapters, string? file)
    {
        ArgumentNullException.ThrowIfNull(chapters);

        return chapters.FirstOrDefault(chapter =>
            string.Equals(chapter.File, file, StringComparison.OrdinalIgnoreCase))
            ?? chapters.FirstOrDefault();
    }
}
