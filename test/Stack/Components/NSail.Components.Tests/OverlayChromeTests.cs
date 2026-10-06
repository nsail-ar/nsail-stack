// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#2053: an overlay's chrome is the SHELL's. A page's title row is drawn inside
/// its NsForm, which renders neither of its branches while the first read is in flight, so a
/// deep link into an aside used to be a blank box with no way out for the length of that read —
/// and a routed modal the same with no backdrop to click either. The row the host draws stands
/// from the first frame: the page's own name off its route, the spinner the read raises, the X;
/// the page's bar announces and draws nothing.</summary>
public sealed class OverlayChromeTests : BunitContext, IAsyncLifetime
{
    // The X is a MudTooltip, which pulls MudBlazor's popover service into the container, and that
    // one only implements IAsyncDisposable — a synchronous teardown throws on it by design.
    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public new async Task DisposeAsync()
    {
        await ((IAsyncDisposable)this).DisposeAsync();
    }

    sealed class FakeJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            return default;
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            return default;
        }
    }

    RouteTable Setup()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            // The name drawn before the page renders: the key the ROUTED TYPE resolves, which is
            // the one its own title bar would have passed (NsPartial.GetTitle).
            [new MetadataProvider().KeyFor(typeof(OverlayFormPage), "Title")] = "Nuevo Turno",
            ["Common.Loading"] = "Cargando",
            ["Common.Close"] = "Cerrar",
            ["Common.Retry"] = "Reintentar",
            ["Common.Save"] = "Guardar"
        })]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddScoped<NavMenu>();
        Services.AddSingleton(new RouteTable(typeof(OverlayChromeTests).Assembly, []));
        Services.AddSingleton<SurfaceHistory>();
        Services.AddSingleton<DialogManager>(new CountingDialogManager());
        Services.AddSingleton<IJSRuntime>(new FakeJs());
        JSInterop.Mode = JSRuntimeMode.Loose;

        return Services.GetRequiredService<RouteTable>();
    }

    static IRenderedComponent<OverlayChromeHost> Held(
        BunitContext context,
        RouteTable routes,
        TaskCompletionSource gate,
        bool modal = false,
        bool fails = false,
        TaskCompletionSource? saving = null)
    {
        return context.Render<OverlayChromeHost>(p => p
            .Add(x => x.RouteTable, routes)
            .Add(x => x.Modal, modal)
            .Add(x => x.Title, "Nuevo Turno")
            .Add(x => x.Gate, gate)
            .Add(x => x.Fails, fails)
            .Add(x => x.Saving, saving));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheOverlaysOwnRowNamesItAndOffersTheWayOutBeforeTheReadAnswers(bool modal)
    {
        var routes = Setup();
        var gate = new TaskCompletionSource();

        var cut = Held(this, routes, gate, modal);

        // The screen's name, drawn at once and derived from the page the address matched — the
        // page itself has rendered nothing at all yet.
        Assert.Contains("Nuevo Turno", cut.Markup, StringComparison.Ordinal);

        // The loading mark is the title bar's own slot, and the slot holds one thing at a time.
        var slot = Assert.Single(cut.FindAll(".ns-title-icon-slot"));

        Assert.Contains("Cargando", slot.InnerHtml, StringComparison.Ordinal);

        // The way out, which a routed modal has no backdrop to stand in for.
        Assert.Single(cut.FindComponents<NsClose>());

        // And nothing in the content area: a form draws neither of its branches until its read
        // answers, so there is no field, no form and no submit to press.
        Assert.Empty(cut.FindAll("input"));
        Assert.Empty(cut.FindAll("form"));
        Assert.Empty(cut.FindAll("button[type=submit]"));

        gate.SetResult();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheReadAnsweringLeavesOneTitleRowCarryingThePagesUtilities(bool modal)
    {
        var routes = Setup();
        var gate = new TaskCompletionSource();

        var cut = Held(this, routes, gate, modal);

        gate.SetResult();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("input")));

        // Exactly one row and exactly one X: the page's own bar announced instead of drawing a
        // second one under the first.
        Assert.Single(cut.FindAll(".mud-typography-h6"));
        Assert.Single(cut.FindComponents<NsClose>());

        // The page's utility actions ride the shell's row — the fragment is the page's own, so
        // what it renders keeps answering to the page.
        var row = cut.FindComponents<NsTitleBar>()[0];

        Assert.Contains("probe-utility", row.Markup, StringComparison.Ordinal);

        // And the row the page declared drew nothing.
        var own = cut.FindComponent<OverlayFormPage>().FindComponent<NsTitleBar>();

        Assert.Equal(string.Empty, own.Markup.Trim());
    }

    [Fact]
    public void ThePagesOwnRowHandsItsNameAndItsUtilitiesToTheSurface()
    {
        var routes = Setup();
        var gate = new TaskCompletionSource();

        var cut = Held(this, routes, gate);

        gate.SetResult();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("input")));

        var surface = cut.Instance.Surface;

        Assert.NotNull(surface);
        Assert.Equal("Nuevo Turno", surface.Title);
        Assert.NotNull(surface.Utilities);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AReadThatFailedKeepsTheChromeAndTheXAndSaysWhy(bool modal)
    {
        var routes = Setup();
        var gate = new TaskCompletionSource();

        var cut = Held(this, routes, gate, modal, fails: true);

        gate.SetResult();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".ns-form-problem")));

        // The sender's reason and the one act that answers it, where the fields would have been.
        Assert.Contains("La lectura no llegó.", cut.Markup, StringComparison.Ordinal);
        Assert.Single(cut.FindAll(".ns-form-problem-act"));
        Assert.Empty(cut.FindAll("input"));

        // Standing over it: the name and the way out.
        Assert.Contains("Nuevo Turno", cut.Markup, StringComparison.Ordinal);
        Assert.Single(cut.FindComponents<NsClose>());
    }

    /// <summary>The row is drawn outside the page's form, so the form's own word has to reach it:
    /// an X that stayed pressable over a save that greyed the footer's Cancelar is one way out of
    /// a surface disagreeing with the other. The word travels up through the surface and comes
    /// back down as the name NsForm cascades (SurfaceContext.FormRefuses, NsDialogExit's seam).</summary>
    [Fact]
    public void TheHoistedXGreysWithTheFooterWhileTheSaveIsInFlight()
    {
        var routes = Setup();
        var gate = new TaskCompletionSource();
        var saving = new TaskCompletionSource();

        var cut = Held(this, routes, gate, saving: saving);

        gate.SetResult();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("input")));

        var exit = cut.FindComponent<NsClose>().Find("button");

        Assert.False(exit.HasAttribute("disabled"));

        // Discarded on purpose: the submit's own Task only completes when `saving` is released
        // below, so awaiting the dispatch here would deadlock — the WaitForAssertion is the wait
        // that stands in for it.
        _ = cut.InvokeAsync(() => cut.Find("form").Submit());

        cut.WaitForAssertion(() => Assert.True(cut.FindComponent<NsClose>().Find("button").HasAttribute("disabled")));

        saving.SetResult();

        cut.WaitForAssertion(() => Assert.False(cut.FindComponent<NsClose>().Find("button").HasAttribute("disabled")));
    }

    /// <summary>The page keeps its panel inside its form and its title row inside that panel — no
    /// screen's markup moved. What the hoisted row leaves behind is an EMPTY header slot, which
    /// pays the panel's gap for nothing; .ns-panel-header:empty (ns-mud.css) is what collapses
    /// it, and the hook has to be on the box for that rule to have anything to match.</summary>
    [Fact]
    public void TheHeaderTheRowLeftBehindCarriesTheCollapseHookAndNothingElse()
    {
        var routes = Setup();
        var gate = new TaskCompletionSource();

        var cut = Held(this, routes, gate);

        gate.SetResult();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("input")));

        var header = Assert.Single(cut.FindAll(".ns-panel-header"));

        Assert.Equal(string.Empty, header.InnerHtml);
    }
}
