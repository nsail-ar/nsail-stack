// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;

namespace NSail.Components.Tests;

/// <summary>Leonardo, Nueva Receta dialog: "a veces no se nota que hay más cosas abajo" — ruled
/// a scroll SHADOW, not a chevron (intentional-ui.md). The gradient itself is CSS a bUnit render
/// cannot run (no layout, no scroll, no @container), so what this pins is the one thing a render
/// can prove: NsPanel's Content wrapper — the box that already carries overflow-y-auto — also
/// carries the class ns-mud.css hangs the technique on, so every panel and dialog inherits it
/// from this one place rather than a page wiring it per screen.</summary>
public sealed class NsPanelScrollShadowTests : BunitContext, IAsyncLifetime
{
    public NsPanelScrollShadowTests()
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
    public void TheContentWrapperCarriesTheScrollShadowHook()
    {
        var cut = Render<DialogPanelProbeBody>();

        var content = cut.Find(".probe-content").ParentElement;

        Assert.NotNull(content);
        Assert.Contains("overflow-y-auto", content!.ClassList);
        Assert.Contains("ns-scroll-shadow", content.ClassList);
    }

    [Fact]
    public void APanelWithNoContent_rendersNoScrollShadowBox()
    {
        var cut = Render<FooterButtonsHost>();

        Assert.Empty(cut.FindAll(".ns-scroll-shadow"));
    }

    /// <summary>The computed colour is a live probe (NsPanelHostSurfaceTests' own note), but the
    /// source of the regression — tinting off text-primary, which flips near-white in dark mode
    /// (Leonardo, nsail#691) — is text a bUnit render can read even though it cannot run the
    /// gradient. Pinning the rule's own source keeps the fix from drifting back to that idiom.</summary>
    [Fact]
    public void TheStylesheetTintsTheShadowBlack_neverOffTextPrimary()
    {
        var css = File.ReadAllText(Path.Combine(
            RepoRoot(),
            "src/Stack/Components/NSail.Components.Mud/wwwroot/ns-mud.css"));

        var rule = css[css.IndexOf(".ns-scroll-shadow {", StringComparison.Ordinal)..];
        rule = rule[..rule.IndexOf("}", StringComparison.Ordinal)];

        Assert.Contains("rgba(0, 0, 0,", rule, StringComparison.Ordinal);
        Assert.DoesNotContain("--mud-palette-text-primary-rgb", rule, StringComparison.Ordinal);
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
}
