// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Messaging.Runtime.Context;
using NSail.Metadata;
using NSail.Problems;

namespace NSail.Components.Tests;

/// <summary>The story (nsail#319): a page loads more than the form it holds, and when any of
/// that failed there was nowhere to say so and nothing to press — the read threw out of
/// OnCreatedAsync, reached the layout's NsErrorBoundary and replaced the whole app with the
/// error splash. Worse, a read that failed and a read that came back empty looked identical:
/// the table drew "no records" either way.
///
/// NsLoad is the region that answers both. Three states and no fourth — not read yet, read,
/// could not be read — so the only thing that can draw the content is a read that worked, and
/// the failure draws in the content's own place with a retry that re-runs that read alone.</summary>
public sealed class NsLoadTests : BunitContext, IAsyncLifetime
{
    public NsLoadTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddComponentServices();
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton<Mediator>(new CapturingMediator());
        Services.AddSingleton<MessageContextAccessor>();
        Services.AddSingleton(new RouteTable(typeof(NsLoadTests).Assembly, []));

        // The surface's own report ends at ProblemManager, which notifies through the dialog
        // manager — counting it is what tells "the region drew the failure" apart from "the
        // failure also went out as a toast the user has to connect back to an empty screen".
        Services.AddScoped<DialogManager>(_ => Dialogs);

        AddAuthorization().SetAuthorized("probe");
    }

    CountingDialogManager Dialogs { get; } = new();

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    static Problem Unreachable()
    {
        return new(
            code: "Unreachable",
            title: "The till could not be read",
            issues: [new Issue(code: "Unreachable", message: "The till could not be read right now.")],
            status: 503);
    }

    IRenderedComponent<LoadProbeHost> Host(Problem? refusal = null, TaskCompletionSource? gate = null, bool bare = false)
    {
        return Render<LoadProbeHost>(p => p
            .Add(x => x.RouteTable, Services.GetRequiredService<RouteTable>())
            .Add(x => x.Refusal, refusal)
            .Add(x => x.Gate, gate)
            .Add(x => x.Bare, bare));
    }

    [Fact]
    public void AReadThatWorked_DrawsTheContentAndNoFailure()
    {
        var host = Host();

        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll("#ns-probe-loaded")));

        Assert.Empty(host.FindAll(".ns-load-problem"));
        Assert.Empty(host.FindAll("#ns-page-error"));
        Assert.Empty(Dialogs.Notices);
    }

    [Fact]
    public void AReadThatFailed_DrawsTheReasonAndARetryWhereTheContentWouldHaveBeen()
    {
        var host = Host(Unreachable());

        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll(".ns-load-problem")));

        // The sender's own words, not the Stack's: the catalog is empty here, so what survives
        // is the message the Problem carried, which is exactly what a translated key replaces.
        Assert.Contains("The till could not be read right now.", host.Find(".ns-load-problem").TextContent);
        Assert.NotEmpty(host.FindAll(".ns-load-retry"));

        // The whole point of the last acceptance criterion: nothing that could not be read is
        // allowed to render as though it had been.
        Assert.Empty(host.FindAll("#ns-probe-loaded"));
    }

    [Fact]
    public void AReadThatFailed_NeitherSplashesTheAppNorFadesIntoAToast()
    {
        var host = Host(Unreachable());

        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll(".ns-load-problem")));

        Assert.Empty(host.FindAll("#ns-page-error"));
        Assert.Empty(Dialogs.Notices);
    }

    [Fact]
    public void AReadStillInFlight_DrawsNeitherTheContentNorAnEmptyStandingInForIt()
    {
        var gate = new TaskCompletionSource();

        var host = Host(gate: gate);

        Assert.Empty(host.FindAll("#ns-probe-loaded"));
        Assert.Empty(host.FindAll(".ns-load-problem"));

        gate.SetResult();

        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll("#ns-probe-loaded")));
    }

    [Fact]
    public async Task TheRetry_ReRunsThatReadAloneAndNotThePageAroundIt()
    {
        var host = Host(Unreachable());

        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll(".ns-load-retry")));

        var page = host.Instance.Page!;

        Assert.Equal(1, page.Reads);
        Assert.Equal(1, page.Creates);

        host.Render(p => p.Add(x => x.Refusal, (Problem?)null));

        await host.InvokeAsync(() => host.Find(".ns-load-retry").Click());

        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll("#ns-probe-loaded")));

        Assert.Empty(host.FindAll(".ns-load-problem"));
        Assert.Equal(2, page.Reads);

        // The page initialized once. A retry that re-created the page would have been the
        // "reload the whole thing" the story rules out.
        Assert.Equal(1, page.Creates);
    }

    [Fact]
    public async Task AReadThatFailedAndThenWorked_ShowsTheContentItHadBeenStandingInFor()
    {
        var host = Host(Unreachable());

        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll(".ns-load-problem")));

        host.Render(p => p.Add(x => x.Refusal, (Problem?)null));
        await host.InvokeAsync(() => host.Find(".ns-load-retry").Click());

        host.WaitForAssertion(() => Assert.Contains("read 2", host.Find("#ns-probe-loaded").TextContent));
    }

    /// <summary>The everyday way in is a filter moved mid-read: the read it supersedes runs on
    /// to its own end, and what it ends with must not land on the answer of the read that
    /// replaced it — a refusal drawn over rows the region just brought back is the same lie
    /// the region exists to stop, pointing the other way.</summary>
    [Fact]
    public void AReadSupersededMidFlight_CannotLandItsRefusalOnTheReadThatReplacedIt()
    {
        var superseded = new TaskCompletionSource();
        var replacement = new TaskCompletionSource();

        var host = Host(Unreachable(), gate: superseded);
        var page = host.Instance.Page!;

        host.WaitForAssertion(() => Assert.Equal(1, page.Reads));

        // The filter moves: the next read succeeds, and the one in flight is left to refuse.
        host.Render(p => p
            .Add(x => x.Refusal, (Problem?)null)
            .Add(x => x.Gate, replacement));

        // Discarded on purpose: Reload's Task only completes when `replacement` is released,
        // which this test does further down, so awaiting the dispatch here would deadlock.
        // The WaitForAssertion below is the wait that stands in for it.
        _ = host.InvokeAsync(() => page.Reload());

        host.WaitForAssertion(() => Assert.Equal(2, page.Reads));

        // The region's own token reached the read, so a read that honours it stops there.
        Assert.True(page.Tokens[0].IsCancellationRequested);

        // The order that bites: the superseded read refuses while the read that replaced it is
        // still in flight, so its refusal is the last one written before the newer read lands.
        superseded.SetResult();

        host.WaitForAssertion(() => Assert.Equal(1, page.Refusals));

        replacement.SetResult();

        host.WaitForAssertion(() => Assert.Contains("read 2", host.Find("#ns-probe-loaded").TextContent));

        Assert.Empty(host.FindAll(".ns-load-problem"));
        Assert.Empty(Dialogs.Notices);
    }

    /// <summary>The floor under every screen that has not declared a region yet: NsPage runs
    /// OnCreatedAsync through the Runner, so a read that throws there is a reported Problem
    /// and not the app-wide splash.</summary>
    [Fact]
    public void APageWhoseOwnLoadThrew_KeepsTheAppInsteadOfSplashingIt()
    {
        var host = Host(Unreachable(), bare: true);

        host.WaitForAssertion(() => Assert.NotEmpty(Dialogs.Notices));

        Assert.Empty(host.FindAll("#ns-page-error"));
        Assert.NotEmpty(host.FindAll("#ns-probe-page"));
        Assert.Contains("The till could not be read right now.", Dialogs.Notices[0]);
    }

    /// <summary>The floor may not cost what it saves: the page's load runs on a Runner of its
    /// own, so an action clicked while the read is still in flight runs instead of throwing
    /// "already running" out of the click — which is the same splash, on the same slow
    /// backend.</summary>
    [Fact]
    public async Task AClickWhileThePagesOwnLoadIsInFlight_RunsInsteadOfFaultingIntoTheSplash()
    {
        var gate = new TaskCompletionSource();

        var host = Host(gate: gate, bare: true);

        await host.InvokeAsync(() => host.Find(".ns-probe-act").Click());

        Assert.Equal(1, host.Instance.BarePage!.Acts);
        Assert.Empty(host.FindAll("#ns-page-error"));
        Assert.Empty(Dialogs.Notices);

        gate.SetResult();

        host.WaitForAssertion(() => Assert.Empty(host.FindAll("#ns-page-error")));
    }
}
