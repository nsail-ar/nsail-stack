// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#1460: the same vendor cascade NsButton shadows was reaching NsLink, and a link
/// declares no Submits and drives no save — Entregar's Cobrar, Facturar and Ver comprobante went
/// dark for the length of MarkDelivered, and Cancelar venta's Ver venta with them.
///
/// What is pinned here is the link's half of the same rule: a form that is merely SAVING leaves
/// a link alone on every face it has, and a form that refuses writes still greys the two faces
/// the vendor draws — the second is what keeps a link and a command at the same emphasis from
/// reading as two things (NsLink's own note on the chromed classes).</summary>
public sealed class NsLinkSavingFormTests : BunitContext, IAsyncLifetime
{
    public NsLinkSavingFormTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddComponentServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(BuildRouteTable());
        Services.AddScoped<SurfaceHistory>();
        Services.AddScoped<RootSurface>();
    }

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    static RouteTable BuildRouteTable()
    {
        return new(typeof(NsLinkSavingFormTests).Assembly, []);
    }

    IRenderedComponent<SubmittingButtonHost> Mount(Task held, bool disabled = false)
    {
        return Render<SubmittingButtonHost>(p => p
            .Add(x => x.RouteTable, BuildRouteTable())
            .Add(x => x.Model, new SubmitTrackingModel())
            .Add(x => x.Disabled, disabled)
            .Add(x => x.Held, held));
    }

    static bool Greyed(IRenderedComponent<SubmittingButtonHost> host, string css)
    {
        return host.Find(css).HasAttribute("disabled");
    }

    // The worded face is a bare anchor and has no disabled attribute to read: what says it is
    // still a way to the destination is that it still carries one.
    static bool Navigates(IRenderedComponent<SubmittingButtonHost> host, string css)
    {
        return host.Find(css).GetAttribute("href") is { Length: > 0 };
    }

    [Fact]
    public void BeforeAnythingRuns_EveryFaceOfALinkIsLive()
    {
        var host = Mount(Task.CompletedTask);

        Assert.False(Greyed(host, ".link-chromed"));
        Assert.False(Greyed(host, ".link-glyph"));
        Assert.True(Navigates(host, ".link-chromed"));
        Assert.True(Navigates(host, ".link-worded"));
    }

    /// <summary>The headline: the hero's save is in flight, the act that drives it is refused —
    /// and the link beside it in the same footer is not, on any face. A link submits nothing, so
    /// a save it has no part in is not its business.</summary>
    [Fact]
    public void WhileTheFormSaves_NoFaceOfALinkIsRefused()
    {
        var running = new TaskCompletionSource();
        var host = Mount(running.Task);

        // Discarded on purpose: the submit's own Task only completes when `running` is released
        // below, so awaiting the dispatch here would deadlock — the WaitForAssertion that
        // follows is the wait that stands in for it (NsLoadTests' note).
        _ = host.InvokeAsync(() => host.Find("form").Submit());

        host.WaitForAssertion(() => Assert.True(Greyed(host, "button.quote")));

        Assert.False(Greyed(host, ".link-chromed"));
        Assert.False(Greyed(host, ".link-glyph"));
        Assert.True(Navigates(host, ".link-chromed"));
        Assert.True(Navigates(host, ".link-worded"));

        running.SetResult();

        host.WaitForAssertion(() => Assert.False(Greyed(host, "button.quote")));
    }

    /// <summary>The other half, unchanged: a form that refuses writes outright greys the link
    /// with the command beside it — the emphasis ladder draws those two as one button and one of
    /// them going quiet alone is a footer disagreeing with itself. Only the merely-saving case
    /// was the defect, so the shadow above must not have loosened this one.</summary>
    [Fact]
    public void AFormThatRefusesWrites_GreysTheLinkWithTheCommandBesideIt()
    {
        var host = Mount(Task.CompletedTask, disabled: true);

        Assert.True(Greyed(host, "button.beside"));
        Assert.True(Greyed(host, ".link-chromed"));
        Assert.True(Greyed(host, ".link-glyph"));

        // And the grey does not lie: MudBlazor drops the href of a button it draws disabled, so
        // the refusal reaches the destination and not only the paint.
        Assert.False(Navigates(host, ".link-chromed"));
    }

    /// <summary>Both halves of the form's word at once — it refuses writes AND a submit is in
    /// flight under it. A refusal a running save is standing on top of is still a refusal: the
    /// link stays grey with the command beside it for the whole of the save, and the destination
    /// stays dropped with it.</summary>
    [Fact]
    public async Task AFormThatRefusesWrites_KeepsGreyingTheLinkWhileASubmitIsHeldInFlight()
    {
        var running = new TaskCompletionSource();
        var host = Mount(running.Task, disabled: true);

        Assert.True(Greyed(host, ".link-chromed"));

        // Straight at the form, because nothing on screen can raise this one: HandleSubmit is
        // what a caller's own Submit() reaches and it does not read Disabled, which is how both
        // halves of the word come to be true at once. Held rather than awaited here — the task
        // only completes when the hold below is released (NsLoadTests' note).
        var submit = host.InvokeAsync(() => host.Find("form").Submit());

        // A count of one is a render that already landed: the form draws the freeze before it
        // invokes the handler, so the screen read below is the screen as the save left it.
        host.WaitForAssertion(() => Assert.Equal(1, host.Instance.Submits));

        Assert.True(Greyed(host, ".link-chromed"));
        Assert.True(Greyed(host, ".link-glyph"));
        Assert.False(Navigates(host, ".link-chromed"));

        running.SetResult();
        await submit;

        Assert.True(Greyed(host, ".link-chromed"));
    }
}
