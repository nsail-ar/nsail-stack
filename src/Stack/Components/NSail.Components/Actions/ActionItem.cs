// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components;
using NSail.Icons;

using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components;

/// <summary>Contributed action: a link (PageType), a command (OnClick) or a menu (Menu) —
/// exactly one of the three. The URL comes from the route table at render, never written.
/// Name drives the localized label ("Actions.{Name}").</summary>
public sealed class ActionItem
{
    public required string Name { get; init; }

    public Glyph? Icon { get; init; }

    /// <summary>Renders instead of the glyph on icon chrome — a contributor whose action IS
    /// its own image (a person's avatar) rather than a fixed catalog entry. A plain Blazor
    /// primitive, so Stack stays domain-free: the contributor's own kit builds the fragment
    /// (an AssetId-based image), never named or typed here. Mutually exclusive with Icon in
    /// spirit, never combined — the glyph is what a contributor with no image falls back to.</summary>
    public RenderFragment? Content { get; init; }

    /// <summary>Destination page of a link action; its own authorize attributes gate it.</summary>
    public Type? PageType { get; init; }

    /// <summary>Route parameters for PageType, as an anonymous object.</summary>
    public object? Parameters { get; init; }

    /// <summary>Where the link opens: one of the app's surfaces (Surfaces.Aside/Modal),
    /// one of the reserved targets (Surfaces.Auto, Surfaces.Main), or the browser
    /// (Surfaces.Blank).</summary>
    public Surface? Target { get; init; }

    public Func<Task>? OnClick { get; init; }

    /// <summary>The body of an anchored menu the action's own face opens — the third kind of
    /// act, beside going somewhere and doing something: offering a choice. The host renders it
    /// in an NsMenu whose trigger is this item's Icon or Content, so a contributor gets the
    /// house's menu without naming a vendor type. A plain Blazor primitive for the same reason
    /// Content is one: the contributor's own kit builds the fragment (its own component,
    /// loading its own data), never named or typed here.</summary>
    public RenderFragment? Menu { get; init; }

    /// <summary>Sort key across all contributors; ties keep contribution order.</summary>
    public int Weight { get; init; }

    /// <summary>The action's affinity: the host renders a separator in its overflow menu
    /// between two consecutive items that do not share one, so a kebab reads as families of
    /// acts and not as a list. Any enum the caller owns — an affinity is the caller's own
    /// vocabulary and the Stack knows no domain, and typing it as an enum is what keeps a page
    /// from writing a number it pulled from air. Unset is "the rest", which is where every
    /// contributed action lands without declaring anything.</summary>
    public Enum? Group { get; init; }

    public bool Disabled { get; init; }

    /// <summary>Whether this act changes what the form around it holds. True is the default and
    /// covers every deed, so an act drawn under an NsForm that refuses writes — ReadOnly,
    /// Disabled, or frozen for the length of a submit — is withheld without the screen that drew
    /// it saying anything (NsActBase). False is the act that must survive there: it probes, it
    /// opens a conversation, it goes somewhere. It says so once, here, because the toolbar
    /// renders a contributed item and forwards no parameter of its own.</summary>
    public bool Writes { get; init; } = true;

    /// <summary>The tone of the STATE this act is about, painted on its own glyph — the status
    /// channel's vocabulary (NsStatusText, NsStatusDot), not a second palette and not an
    /// intention: an act is Danger because it destroys something, never because what it reports
    /// is bad news. It is what lets one contributed verb say which of several states a row is
    /// in without a word in a cell that has none. Null is the ordinary ink, which is what every
    /// act that reports no state carries.</summary>
    public NsSeverity? Severity { get; init; }

    /// <summary>The action's label: "Actions.{Name}" when the action names its own, otherwise
    /// the destination page's own title — the same derivation NsPageLink makes, so a page's
    /// fixed link becomes an ActionItem without paying for a second string to keep in
    /// sync.</summary>
    public string GetLabel(StringManager strings, MetadataProvider metadata)
    {
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentNullException.ThrowIfNull(metadata);

        if (strings.TryTranslate($"Actions.{Name}", out var named))
        {
            return named;
        }

        if (PageType is not null && strings.TryTranslate(metadata.KeyFor(PageType, "Title"), out var title))
        {
            return title;
        }

        return Name;
    }
}
