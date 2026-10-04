// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#1460: a plain NsButton wired to a form's Submit() stayed pressable while that
/// form was already saving, and the second press could only fail — HandleSubmit re-enters the
/// form's Runner and earns InvalidOperationException("Runner is already running."). Nueva
/// Venta's Presupuestar was one of them.
///
/// The promise pinned here is the one NsSubmit already keeps and NsButton had no notion of: a
/// button that drives a form's submit is not pressable while that form is saving, and it works
/// again after. Submits is what says so, and it is opt-in for the third criterion's sake — a
/// button standing in the same cascade that submits nothing must be untouched by the save
/// beside it.</summary>
public sealed class NsButtonSubmitsTests : BunitContext, IAsyncLifetime
{
    public NsButtonSubmitsTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddComponentServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
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
        return new(typeof(NsButtonSubmitsTests).Assembly, []);
    }

    IRenderedComponent<SubmittingButtonHost> Mount(Task held, bool disabled = false)
    {
        return Render<SubmittingButtonHost>(p => p
            .Add(x => x.RouteTable, BuildRouteTable())
            .Add(x => x.Model, new SubmitTrackingModel())
            .Add(x => x.Disabled, disabled)
            .Add(x => x.Held, held));
    }

    static bool Refused(IRenderedComponent<SubmittingButtonHost> host, string css)
    {
        return host.Find(css).HasAttribute("disabled");
    }

    [Fact]
    public void BeforeAnythingRuns_EverySubmittingButtonIsLive()
    {
        var host = Mount(Task.CompletedTask);

        Assert.False(Refused(host, "button.quote"));
        Assert.False(Refused(host, "button.beside"));
        Assert.False(Refused(host, "button.hero"));
    }

    /// <summary>The headline: the hero's save is in flight, so the act that drives the same
    /// form is refused — and it comes back the moment the save lands.</summary>
    [Fact]
    public void WhileTheHeroesSubmitIsInFlight_TheButtonThatSubmitsIsRefusedAndComesBack()
    {
        var running = new TaskCompletionSource();
        var host = Mount(running.Task);

        // Discarded on purpose: the submit's own Task only completes when `running` is released
        // below, so awaiting the dispatch here would deadlock — the WaitForAssertion that
        // follows is the wait that stands in for it (NsLoadTests' note).
        _ = host.InvokeAsync(() => host.Find("form").Submit());

        host.WaitForAssertion(() => Assert.True(Refused(host, "button.quote")));

        running.SetResult();

        host.WaitForAssertion(() => Assert.False(Refused(host, "button.quote")));
    }

    /// <summary>The third criterion, and the reason Submits is opt-in rather than a cascade
    /// every button reads: the act beside it submits nothing, stands in the same form's
    /// cascade, and is untouched — refused neither in the markup nor in the handler.</summary>
    [Fact]
    public async Task AButtonThatSubmitsNothingKeepsWorkingWhileTheFormSaves()
    {
        var running = new TaskCompletionSource();
        var host = Mount(running.Task);

        _ = host.InvokeAsync(() => host.Find("form").Submit());

        host.WaitForAssertion(() => Assert.True(Refused(host, "button.quote")));

        Assert.False(Refused(host, "button.beside"));

        await host.InvokeAsync(() => host.Find("button.beside").Click());

        Assert.Equal(1, host.Instance.Besides);

        running.SetResult();
    }

    /// <summary>The way out is not that button, and both of its faces say so together. A close
    /// stays refused for the length of the save — the surface it would close is the one this
    /// save is about to finish — and the header X must not disagree with the Cancelar beside
    /// it, which is what NsClose reading the form's word for itself buys.</summary>
    [Fact]
    public void BothFacesOfTheWayOutAnswerTheSaveTogether()
    {
        var running = new TaskCompletionSource();
        var host = Mount(running.Task);

        Assert.False(Refused(host, "button.cancel"));
        Assert.False(Refused(host, "button.close-x"));

        _ = host.InvokeAsync(() => host.Find("form").Submit());

        host.WaitForAssertion(() =>
        {
            Assert.True(Refused(host, "button.cancel"));
            Assert.True(Refused(host, "button.close-x"));
        });

        running.SetResult();

        host.WaitForAssertion(() =>
        {
            Assert.False(Refused(host, "button.cancel"));
            Assert.False(Refused(host, "button.close-x"));
        });
    }

    /// <summary>Both halves of the form's word at once — it refuses writes AND a submit is in
    /// flight under it. A refusal a running save is standing on top of is still a refusal: the
    /// button that submits nothing is refused with the fields for the whole of it, and does not
    /// come back when the save lands because the form never stopped refusing.</summary>
    [Fact]
    public async Task AFormThatRefusesWrites_KeepsRefusingForTheLengthOfASubmitHeldInFlight()
    {
        var running = new TaskCompletionSource();
        var host = Mount(running.Task, disabled: true);

        Assert.True(Refused(host, "button.beside"));

        // Straight at the form, because nothing on screen can raise this one: HandleSubmit is
        // what a caller's own Submit() reaches and it does not read Disabled, which is how both
        // halves of the word come to be true at once. Held rather than awaited here — the task
        // only completes when the hold below is released (NsLoadTests' note).
        var submit = host.InvokeAsync(() => host.Find("form").Submit());

        // A count of one is a render that already landed: the form draws the freeze before it
        // invokes the handler, so the screen read below is the screen as the save left it.
        host.WaitForAssertion(() => Assert.Equal(1, host.Instance.Submits));

        Assert.True(Refused(host, "button.beside"));
        Assert.True(Refused(host, "button.quote"));

        running.SetResult();
        await submit;

        Assert.True(Refused(host, "button.beside"));
    }

    /// <summary>What the user does: presses again. The press reaches the vendor's button anyway
    /// in a unit test, so the guard behind the attribute is what has to answer — nothing runs,
    /// and no second submit is raised at the form.</summary>
    [Fact]
    public async Task ASecondPressWhileTheFormSaves_RunsNothing()
    {
        var running = new TaskCompletionSource();
        var host = Mount(running.Task);

        _ = host.InvokeAsync(() => host.Find("button.quote").Click());

        host.WaitForAssertion(() =>
        {
            Assert.Equal(1, host.Instance.Submits);
            Assert.True(Refused(host, "button.quote"));
        });

        // The press is refused before it starts anything, so this one's own Task completes and
        // is safe to await, unlike the first (NsActionRunningTests' note).
        await host.InvokeAsync(() => host.Find("button.quote").Click());

        Assert.Equal(1, host.Instance.Quotes);
        Assert.Equal(1, host.Instance.Submits);

        running.SetResult();

        host.WaitForAssertion(() => Assert.False(Refused(host, "button.quote")));

        // And the act is whole again: the same button saves the same form once the first one
        // has landed — the hold is released, so this submit runs to its end inside the dispatch.
        await host.InvokeAsync(() => host.Find("button.quote").Click());

        Assert.Equal(2, host.Instance.Submits);
    }
}
