// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>A contributed action gave no sign between the click and the result — the icon sat
/// still while the call was in flight and jumped straight to whatever came after (nsail#1346,
/// Meta's Sincronizar the case that surfaced it). NsAction.HandleClick is the one seam every
/// contributed action's OnClick renders through, so the loading face lands there — once, for
/// every `NsAction` call site — rather than being copied into a contributor.</summary>
public sealed class NsActionRunningTests : BunitContext, IAsyncLifetime
{
    public NsActionRunningTests()
    {
        Services.AddMudServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // MudPopoverProvider pulls in a MudBlazor service that is IAsyncDisposable-only and
    // internal, so bUnit's synchronous teardown cannot dispose it (NsActionColumnTests' note).
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    IRenderedComponent<ActionRunHost> Mount(ActionItem action)
    {
        var cut = Render<ActionRunHost>();

        cut.Render(p => p.Add(x => x.Action, action));

        return cut;
    }

    [Fact]
    public void ClickingHoldsTheLoadingIconWhileTheCallIsInFlightAndReturnsItAfterward()
    {
        var running = new TaskCompletionSource();
        var action = new ActionItem { Name = "Sync", Icon = NsIcons.Sync, OnClick = () => running.Task };

        var cut = Mount(action);

        Assert.Contains(NsIcons.Sync.Markup, cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(NsIcons.Progress.Markup, cut.Markup, StringComparison.Ordinal);

        // Discarded on purpose: HandleClick's own Task only completes when `running` is
        // released below, so awaiting the dispatch here would deadlock — the WaitForAssertion
        // that follows is the wait that stands in for it (NsLoadTests' note).
        _ = cut.InvokeAsync(() => cut.Find("button").Click());

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(NsIcons.Progress.Markup, cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain(NsIcons.Sync.Markup, cut.Markup, StringComparison.Ordinal);
            Assert.True(cut.Find("button").HasAttribute("disabled"));
        });

        running.SetResult();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(NsIcons.Sync.Markup, cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain(NsIcons.Progress.Markup, cut.Markup, StringComparison.Ordinal);
            Assert.False(cut.Find("button").HasAttribute("disabled"));
        });
    }

    [Fact]
    public async Task ASecondClickWhileTheFirstIsStillInFlightNeverReachesOnClickAgain()
    {
        var running = new TaskCompletionSource();
        var calls = 0;
        var action = new ActionItem
        {
            Name = "Sync",
            Icon = NsIcons.Sync,
            OnClick = () =>
            {
                calls++;

                return running.Task;
            },
        };

        var cut = Mount(action);

        // Discarded on purpose: HandleClick's own Task only completes when `running` is
        // released below, so awaiting the dispatch here would deadlock — the WaitForAssertion
        // that follows is the wait that stands in for it (NsLoadTests' note).
        _ = cut.InvokeAsync(() => cut.Find("button").Click());

        cut.WaitForAssertion(() => Assert.Contains(NsIcons.Progress.Markup, cut.Markup, StringComparison.Ordinal));

        // The guard returns before HandleClick reaches Action.OnClick, so this second click's
        // own Task completes synchronously and is safe to await, unlike the first.
        await cut.InvokeAsync(() => cut.Find("button").Click());

        Assert.Equal(1, calls);

        running.SetResult();

        cut.WaitForAssertion(() => Assert.Contains(NsIcons.Sync.Markup, cut.Markup, StringComparison.Ordinal));
    }
}
