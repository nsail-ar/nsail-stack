// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#2338: AutoFocus is the router's arrival marker (NsArrivalFocus, ns.js) and
/// nothing else, and a box that takes the cursor back for the next entry asks for its own
/// first-render focus (FocusOnRender) — pinned here as the markup half, since there is no JS
/// harness in the tree (NsFocusCandidateMarkTests' own note) to prove the DOM precedence the
/// marker is read by, nor the pointer the arrival's pass reads.</summary>
public sealed class NsArrivalFocusMarkTests : BunitContext, IAsyncLifetime
{
    public NsArrivalFocusMarkTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
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
    public void AnAutoFocusField_CarriesTheArrivalMark()
    {
        var cut = Render<NsTextField>(p => p
            .Add(x => x.Label, "Código")
            .Add(x => x.AutoFocus, true));

        Assert.Equal("true", cut.Find("input").GetAttribute("data-ns-arrival-focus"));
    }

    [Fact]
    public void APlainField_CarriesNoArrivalMark()
    {
        var cut = Render<NsTextField>(p => p.Add(x => x.Label, "Código"));

        Assert.Null(cut.Find("input").GetAttribute("data-ns-arrival-focus"));
    }

    /// <summary>The split the touch rule needs: AutoFocus is the MARKER, so the box keeps no
    /// focus of its own at its first render. The arrival's pass is the one that reads the
    /// pointer (ns.js) — a vendor focus fired on render reads none, and a phone arriving at a
    /// list would raise its keyboard over the very list whatever the marker's pass decided.</summary>
    [Fact]
    public void AnAutoFocusField_HandsTheVendorNoFocusOfItsOwn()
    {
        var cut = Render<NsTextField>(p => p
            .Add(x => x.Label, "Código")
            .Add(x => x.AutoFocus, true));

        Assert.Equal("true", cut.Find("input").GetAttribute("data-ns-arrival-focus"));
        Assert.False(cut.FindComponent<MudBlazor.MudTextField<string>>().Instance.AutoFocus);
    }

    /// <summary>And the other way round for the box of a repeated act: FocusOnRender is the
    /// vendor's own focus and claims no arrival, so the fresh box of the next entry takes the
    /// cursor back on every pointer while the marker stays where the screen put it.</summary>
    [Fact]
    public void AFocusOnRenderBox_TakesTheVendorsOwnFocusAndCarriesNoMark()
    {
        var cut = Render<NsTextField>(p => p
            .Add(x => x.Label, "Código")
            .Add(x => x.FocusOnRender, true));

        Assert.Null(cut.Find("input").GetAttribute("data-ns-arrival-focus"));
        Assert.True(cut.FindComponent<MudBlazor.MudTextField<string>>().Instance.AutoFocus);
    }

    /// <summary>Every field reads the word the same way, not the search box alone: a scope
    /// selector standing where a list's search goes marks its input and takes no first-render
    /// focus either — which is what keeps the rule true on the nine screens that mark a picker
    /// rather than a free-text box.</summary>
    [Fact]
    public void AnAutoFocusSelect_MarksItsInputWithoutTheVendorsFocus()
    {
        var cut = Render<NsSelect<string, string>>(p => p
            .Add(x => x.Label, "Sucursal")
            .Add(x => x.AutoFocus, true));

        Assert.Equal("true", cut.Find("input").GetAttribute("data-ns-arrival-focus"));
        Assert.False(cut.FindComponent<MudBlazor.MudSelect<string>>().Instance.AutoFocus);
    }

    /// <summary>The composer carries both words for the same reason a scan box does: arriving at
    /// a thread marks the box, and keeping the caret after a send is the keyed box's own first
    /// render.</summary>
    [Fact]
    public void TheComposer_TellsTheArrivalFromTheCaretItKeeps()
    {
        var marked = Render<NsSendMessage>(p => p.Add(x => x.AutoFocus, true));

        Assert.Equal("true", marked.Find("textarea").GetAttribute("data-ns-arrival-focus"));
        Assert.False(marked.FindComponent<MudBlazor.MudTextField<string>>().Instance.AutoFocus);

        var rearmed = Render<NsSendMessage>(p => p.Add(x => x.FocusOnRender, true));

        Assert.Null(rearmed.Find("textarea").GetAttribute("data-ns-arrival-focus"));
        Assert.True(rearmed.FindComponent<MudBlazor.MudTextField<string>>().Instance.AutoFocus);
    }
}
