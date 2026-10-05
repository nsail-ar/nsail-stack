// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Messaging.Runtime.Context;
using NSail.Metadata;
using NSail.Problems;

namespace NSail.Components.Tests;

/// <summary>The story (nsail#1898): a paged list inside a region could be left permanently
/// empty. The pager writes the question down and the read spends it, so a re-read landing while
/// that read is in flight — a filter moved, a search typed, a *Saved event off a poll —
/// superseded the read and found no question left to ask. The region was already "read", so the
/// grid was not remounted and its query never fired again: no rows, and every later gesture
/// repeated the same no-op until an F5.
///
/// The grid is on screen from the moment it ASKS, so a re-read reaches it through its own query,
/// which writes the question down again. A browser cannot stage that window reliably; the gate
/// here is what measures it.</summary>
public sealed class NsLoadPagedListTests : BunitContext, IAsyncLifetime
{
    public NsLoadPagedListTests()
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
        Services.AddScoped<DialogManager>(_ => Dialogs);
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

    IRenderedComponent<PagedRegionHost> Host(
        TaskCompletionSource gate,
        Problem? refusal = null,
        NsTableMode mode = NsTableMode.Auto)
    {
        return Render<PagedRegionHost>(p => p
            .Add(x => x.Gate, gate)
            .Add(x => x.Refusal, refusal)
            .Add(x => x.Mode, mode));
    }

    /// <summary>What keeps the arrival to one round trip, and the reason the question exists at
    /// all: a region that read the first page itself would have the grid ask for it again as it
    /// mounted.</summary>
    [Fact]
    public void ArrivingOnAPagedList_CostsOneRead()
    {
        var gate = new TaskCompletionSource();
        gate.SetResult();

        var host = Host(gate);

        host.WaitForAssertion(() => Assert.NotEmpty(host.Instance.Rows.Items));

        Assert.Equal(1, host.Instance.Reads);
        Assert.Equal(1, host.Instance.Queries);
    }

    /// <summary>The window the story is about. Before the fix the re-read took the region's door,
    /// superseded the read in flight and found the question spent: the second read never
    /// happened and the rows never arrived. Both branches of the grid are measured — the pager's
    /// re-read goes through ReloadServerData, the phone's through Fetch — because only the query
    /// they share is what writes the question down again.</summary>
    [Theory]
    [InlineData(NsTableMode.Auto)]
    [InlineData(NsTableMode.Scroll)]
    public async Task AReReadDispatchedWhileTheOpeningReadIsInFlight_EndsWithTheRowsItAskedFor(NsTableMode mode)
    {
        var gate = new TaskCompletionSource();

        var host = Host(gate, mode: mode);

        host.WaitForAssertion(() => Assert.Equal(1, host.Instance.Reads));
        Assert.Empty(host.Instance.Rows.Items);

        // Discarded on purpose: the grid's query runs the re-read to its end here, but the first
        // read is still held, so the WaitForAssertion below is the wait that stands in for it.
        _ = host.InvokeAsync(() => host.Instance.Saved());

        host.WaitForAssertion(() => Assert.Equal(2, host.Instance.Reads));
        host.WaitForAssertion(() => Assert.NotEmpty(host.Instance.Rows.Items));

        Assert.Equal("read 2", host.Instance.Rows.Items[0].Name);

        gate.SetResult();

        // And the screen keeps working: the gesture after it reads too, with nothing repeated.
        host.WaitForAssertion(() => Assert.Empty(host.FindAll(".ns-load-problem")));

        await host.InvokeAsync(() => host.Instance.Saved());

        host.WaitForAssertion(() => Assert.Equal(3, host.Instance.Reads));
    }

    /// <summary>The same window entered from the other end (Vigía, PR #2016): the read that was
    /// superseded refuses — late, and not by its token, so it reports — while the read that
    /// replaced it has already put rows on screen. The region draws nothing, because it threw
    /// that answer away; a host told about it anyway lowers the flag that says its grid is gone,
    /// and from there every gesture takes the region's door and finds the question spent. The
    /// refusal of a replaced read reaches nobody, so the list stays readable.</summary>
    [Fact]
    public async Task ASupersededReadRefusingAfterItsReplacementLanded_LeavesTheListReadable()
    {
        var gate = new TaskCompletionSource();

        var host = Host(gate, Unreachable());

        host.WaitForAssertion(() => Assert.Equal(1, host.Instance.Reads));

        // Discarded on purpose: the first read is still held, so this dispatch only completes
        // once the gate below is released. The WaitForAssertion is the wait that stands in.
        _ = host.InvokeAsync(() => host.Instance.Saved());

        host.WaitForAssertion(() => Assert.Equal(2, host.Instance.Reads));
        host.WaitForAssertion(() => Assert.NotEmpty(host.Instance.Rows.Items));

        // The replacement has landed; now the superseded read refuses.
        gate.SetResult();

        await host.InvokeAsync(() => host.Instance.Saved());

        host.WaitForAssertion(() => Assert.Equal(3, host.Instance.Reads));

        // And what the person sees is the rows, not a reason: the refusal belonged to an answer
        // the region discarded, so it was reported to nobody.
        Assert.Equal("read 3", host.Instance.Rows.Items[0].Name);
        Assert.Equal(0, host.Instance.Refusals);
        Assert.Empty(host.FindAll(".ns-load-problem"));
        Assert.Empty(Dialogs.Notices);
    }

    /// <summary>A read that failed still takes the grid with it, so the Retry is the door back —
    /// and it costs one read, because the fresh grid asks for its page as it mounts.</summary>
    [Fact]
    public async Task TheRetryAfterAFailedRead_CostsOneRead()
    {
        var gate = new TaskCompletionSource();
        gate.SetResult();

        var host = Host(gate, Unreachable());

        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll(".ns-load-retry")));

        Assert.Equal(1, host.Instance.Reads);
        Assert.Empty(host.Instance.Rows.Items);

        // The other half of the rule above: the refusal the region DOES draw is the one the host
        // is told about, because that is the one that took its grid away.
        Assert.Equal(1, host.Instance.Refusals);

        await host.InvokeAsync(() => host.Find(".ns-load-retry").Click());

        host.WaitForAssertion(() => Assert.NotEmpty(host.Instance.Rows.Items));

        Assert.Equal(2, host.Instance.Reads);
        Assert.Empty(host.FindAll(".ns-load-problem"));
    }

    static Problem Unreachable()
    {
        return new(
            code: "Unreachable",
            title: "The list could not be read",
            issues: [new Issue(code: "Unreachable", message: "The list could not be read right now.")],
            status: 503);
    }
}
