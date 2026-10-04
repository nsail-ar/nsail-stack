// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;

namespace NSail.Components.Tests;

/// <summary>Leonardo, 2026-08-06 with a screenshot of the OT page cramped at phone width:
/// "los botones de abajo deberían hacerse íconos en este tamaño". SECONDARY footer buttons
/// collapse to icon-only under the phone breakpoint (ns-mud.css, scoped to NsPanel's own
/// "ns-panel-footer" hook); the HERO keeps its pinned text. bUnit lays nothing out and runs
/// no container query — the CSS itself is not something a render can prove — so what is
/// pinned here is the DOM shape that rule depends on: the class hooks it selects by, and the
/// accessible name that has to survive the label going display:none (a RowEditor-label
/// precedent, not a new mechanism).</summary>
public sealed class FooterButtonCollapseTests : BunitContext, IAsyncLifetime
{
    public FooterButtonCollapseTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    [Fact]
    public void TheFootersOwnContainerCarriesTheCollapseHook()
    {
        var cut = Render<FooterButtonsHost>();

        // NsPanel's Footer wrapper is the one place the ladder's collapse rule scopes to,
        // shared by every document page — never a page-local class.
        Assert.Contains("ns-panel-footer", cut.Find(".ns-panel-footer").ClassList);
    }

    [Fact]
    public void ASecondaryButtonWithAnIconCarriesBothHooksTheRuleNeeds()
    {
        var cut = Render<FooterButtonsHost>();

        var collect = cut.FindAll("button").Single(b => b.TextContent.Contains("Collect"));

        // :has(.mud-button-icon-start) is what the CSS rule tests before it hides anything —
        // without an icon there would be nothing left to click.
        Assert.NotEmpty(collect.QuerySelectorAll(".mud-button-icon-start"));

        // ns-button-label is NsResponsive's stable hook: present whether or not this
        // particular button's own Breakpoint would have collapsed it on its own.
        var label = collect.QuerySelector(".ns-button-label");
        Assert.NotNull(label);
        Assert.Equal("Collect", label!.TextContent);

        // Neither colour a hero wears — the rule's two :not() clauses spare Save and SignOut,
        // and this button is what is left to collapse.
        Assert.DoesNotContain("mud-button-filled-primary", collect.ClassList);
        Assert.DoesNotContain("mud-button-filled-error", collect.ClassList);
    }

    [Fact]
    public void ASecondaryButtonWithNoIconCarriesNoIconHook_soTheRuleLeavesItAlone()
    {
        var cut = Render<FooterButtonsHost>();

        // A secondary act with nothing to collapse to (an icon-less footer button — any page
        // may render one). :has(.mud-button-icon-start) in the CSS rule fails to match here
        // on purpose, which is what keeps this button from going blank on a phone.
        var cancel = cut.FindAll("button").Single(b => b.TextContent.Contains("Cancel"));

        Assert.Empty(cancel.QuerySelectorAll(".mud-button-icon-start"));
    }

    [Fact]
    public void TheHeroCarriesThePrimaryColourTheRuleExcludes()
    {
        var cut = Render<FooterButtonsHost>();

        var save = cut.FindAll("button").Single(b => b.TextContent.Contains("Save"));

        Assert.Contains("mud-button-filled-primary", save.ClassList);
    }

    /// <summary>Leonardo, 2026-08-10 ("botón cerrar sesión sigue sin texto"): a footer's hero
    /// is not always the primary one. A sign-out is the one act of its surface and it is
    /// destructive, so it wears Danger — and the dialog holding it measures ~430px, under the
    /// phone breakpoint on ANY screen, which is what stripped its word. The hero is spared by
    /// emphasis, and emphasis comes in two colours.</summary>
    [Fact]
    public void TheDangerHeroCarriesTheColourTheRuleExcludesToo()
    {
        var cut = Render<FooterButtonsHost>();

        var signOut = cut.FindAll("button").Single(b => b.TextContent.Contains("SignOut"));

        Assert.Contains("mud-button-filled-error", signOut.ClassList);

        // Both hooks the rule tests for are present, which is exactly why the exclusion has to
        // be explicit: an icon to collapse to, and a label span to hide.
        Assert.NotEmpty(signOut.QuerySelectorAll(".mud-button-icon-start"));
        Assert.Equal("SignOut", signOut.QuerySelector(".ns-button-label")?.TextContent);
    }

    /// <summary>The only assertion here that can actually go red for this defect: bUnit runs no
    /// container query, so the DOM above proves the hooks and nothing about which of them the
    /// stylesheet spares. The rule's own selector is read from the shipped ns-mud.css instead —
    /// EVERY line of the ladder (the label, the icon margin, and the whisper the collapsed
    /// button now says on hover) must exclude BOTH hero colours, or the sign-out loses its word
    /// again the next time somebody edits one line and not the other. The count is the same
    /// tripwire it always was, one line per rule: it goes red when a line joins or leaves the
    /// block, which is when somebody has to re-read this list.</summary>
    [Fact]
    public void TheEmphasisLadderSparesBothHeroColours()
    {
        var css = File.ReadAllText(Path.Combine(
            RepoRoot(),
            "src/Stack/Components/NSail.Components.Mud/wwwroot/ns-mud.css"));

        var rules = css
            .Split('\n')
            .Where(line => line.Contains(".ns-panel-footer .mud-button-filled"))
            .ToList();

        // Two for the collapse itself (label, icon margin), one for the square a collapsed
        // filled rank becomes (nsail#925), one for the bubble's own definition in the shared
        // whisper rule, one for the side it is drawn on here — a footer IS the bottom edge of
        // its surface, so its bubble is the only one that rises (nsail#1417) — and four for its
        // gate (overflow, the ripple it trades for it, and the hover/focus pair).
        Assert.Equal(9, rules.Count);

        foreach (var rule in rules)
        {
            Assert.Contains(":not(.mud-button-filled-primary)", rule);
            Assert.Contains(":not(.mud-button-filled-error)", rule);
        }
    }

    static string RepoRoot()
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

    /// <summary>The accessible name that has to survive: aria-label restates the same string
    /// the visible span carries, so the control is still named the instant the container rule
    /// (ns-mud.css, run by the browser, not by bUnit) turns that span's display to none.</summary>
    [Fact]
    public void ASecondaryButtonsAccessibleNameSurvivesEvenWhileTheLabelIsVisible()
    {
        var cut = Render<FooterButtonsHost>();

        var collect = cut.FindAll("button").Single(b => b.TextContent.Contains("Collect"));

        Assert.Equal("Collect", collect.GetAttribute("aria-label"));
    }

    [Fact]
    public void TheHerosAccessibleNameIsCarriedTheSameWay()
    {
        var cut = Render<FooterButtonsHost>();

        var save = cut.FindAll("button").Single(b => b.TextContent.Contains("Save"));

        Assert.Equal("Save", save.GetAttribute("aria-label"));
    }
}
