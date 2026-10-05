// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;

namespace NSail.Components.Tests;

/// <summary>nsail#1938: the flat rung with no word says its name through the vendor's tooltip
/// (CollapsedLabelWhisperTests covers the other face, the stylesheet's own bubble), and the
/// vendor opens that bubble on hover and on FOCUS and closes it on neither after a press — the
/// pointer never left, and the click left the control holding focus. So the bubble stayed
/// painted over whatever the press had just put on screen.
/// <para>Reported on one screen — the ARCA settings chevron, whose bubble sat on top of the
/// field the same click revealed — and nsail#1918 answered that screen by giving the control a
/// word, which leaves it carrying no tooltip at all. The rule it was an instance of was never
/// answered: every wordless Inline control is one, and a row's action glyphs are all of them.
/// So the press is what closes it, which makes the whisper the component's to own rather than
/// the vendor's to default.</para>
/// <para>Read on the link beside the button, because the two must not part company at one width
/// (IconOnlyStateTests). What makes a live instance of this narrow is that almost every press
/// REMOUNTS the tooltip and so closes it by accident — a link leaving the page, a row action
/// re-rendering its row, NsHelp's ? pulling the pointer onto its own overlay were all measured
/// doing exactly that. The ARCA chevron was the one that did neither, and nsail#1918 has since
/// given that control a word. So this pins the contract and not a screen: the state the vendor
/// leaves open is removed rather than waited for (principles.md, 4).</para></summary>
public sealed class NsButtonWhisperClosesTests : BunitContext, IAsyncLifetime
{
    public NsButtonWhisperClosesTests()
    {
        Services.AddMudServices();
        // NsLink resolves its address through the root surface even with no cascade, the
        // registrations CollapsedLabelWhisperTests makes for the same reason.
        Services.AddSingleton(new RouteTable(typeof(NsButtonWhisperClosesTests).Assembly, []));
        Services.AddScoped<RootSurface>();
        Services.AddScoped<SurfaceHistory>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ThePressClosesTheBubbleTheHoverOpened(bool navigates)
    {
        var cut = Render<WhisperHost>(p => p.Add(x => x.Navigates, navigates));

        await Hover(cut);

        Assert.NotNull(cut.Find(".mud-popover-open"));

        await Press(cut);

        // The pointer is still over the glyph and the control is still focused — neither of the
        // vendor's own exits has fired, and the bubble is gone anyway.
        Assert.Empty(cut.FindAll(".mud-popover-open"));
    }

    /// <summary>And it comes back: the press takes the bubble down, it does not take the name
    /// away. The pointer leaving and returning is the gesture that asks for it again.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TheNameIsStillThereForTheNextHover(bool navigates)
    {
        var cut = Render<WhisperHost>(p => p.Add(x => x.Navigates, navigates));

        await Hover(cut);
        await Press(cut);
        await cut.InvokeAsync(() => cut.Find(".mud-tooltip-root").PointerLeave());
        await Hover(cut);

        Assert.NotNull(cut.Find(".mud-popover-open"));
    }

    /// <summary>The name the bubble reads is the control's own accessible name, so both wordless
    /// rungs carry their Label as aria-label. Handed none, each renders the vendor's bare glyph
    /// with neither — mute to a pointer and anonymous to a reader, which is what the patient
    /// portal's download links were (nsail#2010; NamedGlyphTests holds the tree).</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheWordlessRungCarriesItsLabelAsItsAccessibleName(bool navigates)
    {
        var cut = Render<WhisperHost>(p => p.Add(x => x.Navigates, navigates));

        Assert.Equal("Editar", cut.Find(".mud-icon-button").GetAttribute("aria-label"));
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    // MudBlazor's popover service is IAsyncDisposable only, so bUnit's synchronous teardown
    // throws on it — the NsExpanderTests idiom.
    public new async Task DisposeAsync()
    {
        await ((IAsyncDisposable)this).DisposeAsync();
    }

    // The vendor hangs the open on the wrapper's own pointer events, not on the control's.
    static Task Hover(IRenderedComponent<WhisperHost> cut)
    {
        return cut.InvokeAsync(() => cut.Find(".mud-tooltip-root").PointerEnter());
    }

    // The link's face is an anchor and the button's is a button, which is the whole difference
    // between them here.
    static Task Press(IRenderedComponent<WhisperHost> cut)
    {
        return cut.InvokeAsync(() => cut.Find(".mud-icon-button").Click());
    }
}
