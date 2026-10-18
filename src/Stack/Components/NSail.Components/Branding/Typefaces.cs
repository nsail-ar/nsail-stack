// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>The house's self-hosted OFL catalog a <see cref="Brand"/> picks its
/// <see cref="Brand.Typeface"/> from — the font files and licenses ride beside
/// <c>NSail.Components.Mud</c>'s static web assets (<c>wwwroot/fonts/{Key}.ttf</c>,
/// <c>{Key}-OFL.txt</c>), reachable at <c>_content/NSail.Components.Mud/fonts/{Key}.ttf</c> in
/// any host that references the package — no CDN, no per-tenant URL to store or resolve.
/// Growing it is adding an entry and its two files, never a plumbing change.</summary>
public static class Typefaces
{
    public static readonly IReadOnlyList<Typeface> All =
    [
        new("inter", "Inter", TypefaceCategory.Sans, ["Inter", "sans-serif"]),
        new("work-sans", "Work Sans", TypefaceCategory.Sans, ["Work Sans", "sans-serif"]),
        new("lora", "Lora", TypefaceCategory.Serif, ["Lora", "serif"]),
        new("source-serif-4", "Source Serif 4", TypefaceCategory.Serif, ["Source Serif 4", "serif"]),
        new("zilla-slab", "Zilla Slab", TypefaceCategory.Slab, ["Zilla Slab", "serif"]),
        new("baloo-2", "Baloo 2", TypefaceCategory.Rounded, ["Baloo 2", "sans-serif"]),
        new("comfortaa", "Comfortaa", TypefaceCategory.Rounded, ["Comfortaa", "sans-serif"]),
        new("fraunces", "Fraunces", TypefaceCategory.Display, ["Fraunces", "serif"]),
        new("bricolage-grotesque", "Bricolage Grotesque", TypefaceCategory.Display, ["Bricolage Grotesque", "sans-serif"]),
    ];

    public static Typeface? Find(string? key)
    {
        return key is null ? null : All.FirstOrDefault(t => t.Key == key);
    }

    /// <summary>The web-root-relative path every host that references the Mud package serves
    /// this family's file at — a browser's <c>@font-face</c> reads it as a URL, and a server
    /// that wants the bytes (QuestPDF, embedding a document) reads it the same way, through
    /// <c>IWebHostEnvironment.WebRootFileProvider.GetFileInfo</c>: one convention, two
    /// readers, never a second copy of the file.</summary>
    public static string AssetPath(string key)
    {
        return $"_content/NSail.Components.Mud/fonts/{key}.ttf";
    }
}
