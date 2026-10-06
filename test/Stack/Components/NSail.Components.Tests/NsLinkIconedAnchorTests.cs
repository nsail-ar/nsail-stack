// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>The flat rung's word-and-icon face: an Icon handed to an Inline link alongside a
/// Label draws both, the glyph beside the word. This is the face itself, not the icon-only or
/// word-only ones NsLinkDomProbeTests already covers. It is a primitive's contract and no
/// screen owes it a caller, so the Stack is where it is proven.</summary>
public sealed class NsLinkIconedAnchorTests : BunitContext
{
    public NsLinkIconedAnchorTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddComponentServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();

        ((BunitNavigationManager)Services.GetRequiredService<NavigationManager>()).NavigateTo("/probe");
    }

    [Fact]
    public void AnIconedInlineLinkKeepsBothTheGlyphAndTheWord()
    {
        // Breakpoint=Always is not a claim that this face reads it: it never does (actions.md,
        // "the one exception"). It stands for the only way a caller reaches the face at all —
        // anything but Never, with a Label — so the test asks for it the way a screen would.
        var anchor = Render<NsLink>(p => p
            .Add(x => x.Href, "directory/parties/1")
            .Add(x => x.Icon, NsIcons.Badge)
            .Add(x => x.Breakpoint, NsBreakpoint.Always)
            .Add(x => x.Label, "Perfil")).Find("a");

        Assert.NotEmpty(anchor.QuerySelectorAll(".mud-icon-root"));
        Assert.Contains("Perfil", anchor.TextContent);
    }

    // Guards the word-only neighbor face, not the fix above — it holds unchanged on main too.
    // The two faces share one branch in NsLink.razor, and a change that iconed this one by
    // accident would pass the fact above without this one catching it.
    [Fact]
    public void AWordOnlyInlineLinkCarriesNoIcon()
    {
        var anchor = Render<NsLink>(p => p
            .Add(x => x.Href, "directory/parties/1")
            .Add(x => x.Label, "Perfil")).Find("a");

        Assert.Empty(anchor.QuerySelectorAll(".mud-icon-root"));
    }
}
