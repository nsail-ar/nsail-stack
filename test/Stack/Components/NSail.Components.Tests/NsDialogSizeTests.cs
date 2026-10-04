// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;

namespace NSail.Components.Tests;

/// <summary>Doctrine (ui/surfaces.md, the size seam; the ruling and the rejected shapes are
/// intentional-ui-cases-surfaces.md, "The dialog that stopped taking its shape from content"): a
/// dialog's frame comes from its `Size` against the viewport, on both axes, and never from its
/// content — the same contract main and aside already honor. GetStyle used to pair a fixed
/// width with a `min-height` that content past it could still grow; these pin the fixed
/// geometry (both axes named by the same property, no `min-`/`max-height` split) and the
/// stylesheet seam that lets the phone breakpoint override both. The content wrapper's own
/// contract (no `overflow-y-auto` — NsPanel's scroll, not the frame's) is
/// PortaledSurfaceContainerTests'. Reuses SurfaceStackHost/SurfaceStackLayeringTests'
/// composition so the dialog renders behind a real SurfaceContext rather than only its
/// parameter fallback.</summary>
public sealed class NsDialogSizeTests : BunitContext, IAsyncLifetime
{
    public NsDialogSizeTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddComponentServices();
        Services.AddScoped<IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddScoped<IBrowserViewportService>(_ => new FixedBreakpointViewportService(Breakpoint.Lg));
    }

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    IRenderedComponent<SurfaceStackHost> RenderHost()
    {
        return Render<SurfaceStackHost>(p => p
            .Add(x => x.RouteTable, new RouteTable(typeof(NsDialogSizeTests).Assembly, []))
            .Add(x => x.Aside, false)
            .Add(x => x.Modal, true));
    }

    [Theory]
    [InlineData(NsSize.Xs, 400, 240)]
    [InlineData(NsSize.Sm, 500, 300)]
    [InlineData(NsSize.Md, 640, 360)]
    [InlineData(NsSize.Lg, 800, 440)]
    [InlineData(NsSize.Xl, 960, 520)]
    [InlineData(NsSize.Xxl, 1140, 600)]
    public async Task BothAxesComeFromSizeAgainstTheViewport_withNoMinOrMaxHeightSplit(NsSize size, int width, int height)
    {
        var host = RenderHost();

        await host.InvokeAsync(() => host.Instance.ModalSurface!.SetSize(size));
        host.Render();

        var style = host.Find(".ns-dialog").GetAttribute("style") ?? string.Empty;

        Assert.Contains($"--ns-dialog-width: min({width}px, calc(100vw - 32px))", style);
        Assert.Contains($"--ns-dialog-height: min({height}px, calc(100vh - 32px))", style);

        // The bug this story kills: a min-height lets content past it grow the box, so
        // switching tabs moved the frame. Neither survives — height is a property named
        // exactly like width, not a floor content can push past.
        Assert.DoesNotContain("min-height", style);
        Assert.DoesNotContain("max-height", style);
    }

    [Fact]
    public void TheStylesheetTurnsTheCustomPropertiesIntoTheRealWidthAndHeight()
    {
        var css = ReadStylesheet();

        var block = RuleContaining(css, "width: var(--ns-dialog-width);", ".ns-dialog {", out _);

        Assert.Contains("height: var(--ns-dialog-height);", block);
    }

    [Fact]
    public void TheFrameClipsRatherThanScrollingItsOwnContent()
    {
        var css = ReadStylesheet();

        var block = RuleContaining(css, "width: var(--ns-dialog-width);", ".ns-dialog {", out _);

        Assert.Contains("overflow: hidden;", block);
    }

    [Fact]
    public void BelowThePhoneBreakpoint_theDialogCoversTheWholeViewport_squareCornered()
    {
        var css = ReadStylesheet();

        // Anchored on the dvh line, which only this rule writes: "border-radius: 0;" stopped
        // being unique in the file the moment another rule had a corner to square (nsail#1816
        // flattens a table inside a card), and RuleContaining finds the FIRST occurrence.
        var block = RuleContaining(css, "height: 100dvh;", ".ns-dialog {", out var start);

        // Same 599.98px MudBlazor sm threshold the dashboard grid and the whisper's collapse
        // gates already use (CollapsedLabelWhisperTests, NsDashboardCardStretchTests) — a
        // dialog degrades to full screen there like every other surface does below its own
        // mobile breakpoint (surfaces.md). Finding the nearest such query above the block is
        // not enough to prove containment: the file carries three more of them earlier, so a
        // rule moved out of every media query would still find one and this could never fail.
        // The span the query actually closes is what the block has to start inside.
        var media = css.LastIndexOf("@media (max-width: 599.98px)", start, StringComparison.Ordinal);
        Assert.True(
            media >= 0 && start < ClosingBrace(css, media),
            "The full-screen override is not written inside the phone media query.");

        Assert.Contains("width: 100vw;", block);
        Assert.Contains("height: 100vh;", block);

        // Android Chrome sizes 100vh against the URL-bar-retracted viewport, not what is
        // actually on screen; the dvh line after is the fallback pair ns.css's html,body
        // idiom uses, and headless Chromium can't tell the two apart to pin this in E2E.
        Assert.Contains("height: 100dvh;", block);
    }

    /// <summary>Finds a rule by content that is unique inside it, rather than by which
    /// occurrence of its selector it is — <c>.ns-dialog</c> opens more than one block in the
    /// file, and a position keyed on the exact whitespace between two selectors breaks on
    /// reformatting alone.</summary>
    static string RuleContaining(string css, string content, string selector, out int start)
    {
        var index = css.IndexOf(content, StringComparison.Ordinal);
        Assert.True(index >= 0, $"'{content}' was not found in the stylesheet.");

        start = css.LastIndexOf(selector, index, StringComparison.Ordinal);
        Assert.True(start >= 0, $"'{content}' is not written inside a '{selector}' rule.");

        var end = css.IndexOf('}', index);
        Assert.True(end >= 0, $"'{selector}' rule containing '{content}' has no closing brace.");

        return css[start..end];
    }

    /// <summary>The index of the brace closing the block that opens at or after
    /// <paramref name="from"/>, or -1 when the file never closes it.</summary>
    static int ClosingBrace(string css, int from)
    {
        var depth = 0;

        for (var i = css.IndexOf('{', from); i >= 0 && i < css.Length; i++)
        {
            if (css[i] == '{')
            {
                depth++;
            }
            else if (css[i] == '}' && --depth == 0)
            {
                return i;
            }
        }

        return -1;
    }

    static string ReadStylesheet()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NSail.sln")))
            {
                return File.ReadAllText(Path.Combine(
                    directory.FullName,
                    "src/Stack/Components/NSail.Components.Mud/wwwroot/ns-mud.css"));
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"NSail.sln was not found above {AppContext.BaseDirectory}.");
    }
}
