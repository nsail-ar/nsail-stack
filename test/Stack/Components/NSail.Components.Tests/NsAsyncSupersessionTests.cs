// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Metadata;
using NSail.Settings;

namespace NSail.Components.Tests;

/// <summary>A late answer is about a question nobody is asking any more: the route gate, the
/// drawer's arrangement read and the action outlet each drop it, and a contributor that fails
/// takes only itself out.</summary>
public sealed class NsAsyncSupersessionTests : BunitContext, IAsyncLifetime
{
    const string HandoffKey = "NSail.NavMenu";

    readonly CapturedLog _log = new();

    public NsAsyncSupersessionTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddComponentServices();
        Services.AddMudServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton<ILoggerFactory>(_log);
        Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        Services.AddSingleton<Mediator>(new CapturingMediator());
        Services.AddSingleton<DialogManager>(new CountingDialogManager());
        this.AddAuthorization().SetAuthorized("tester");
    }

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    BunitNavigationManager Navigation
    {
        get { return (BunitNavigationManager)Services.GetRequiredService<NavigationManager>(); }
    }

    /// <summary>AC1: run 1 is still waiting on its gate when the route moves; its redirect
    /// arrives last and does not move the visitor off the page that replaced it.</summary>
    [Fact]
    public void ASupersededRedirectDoesNotNavigate()
    {
        var (slow, cut) = RenderSuperseded();
        var before = Navigation.History.Count;
        var drawn = cut.RenderCount;

        slow.Asks[0].SetResult(typeof(ProbeOtherPage));

        // The superseded run still renders when it ends; waiting on that render is what makes
        // the assertions below about an answer that was applied or dropped, not one still on its way.
        cut.WaitForState(() => cut.RenderCount > drawn);

        Assert.Equal(before, Navigation.History.Count);
        Assert.DoesNotContain("page", cut.Markup);

        slow.Asks[1].SetResult(null);

        cut.WaitForAssertion(() => Assert.Contains("page", cut.Markup));
    }

    /// <summary>AC1: run 1's gate lets the old page through while the new page's gate is still
    /// thinking; that pass is not the new page's, which stays undrawn until its own answer.</summary>
    [Fact]
    public void ASupersededPassDoesNotDrawTheNewPage()
    {
        var (slow, cut) = RenderSuperseded();
        var drawn = cut.RenderCount;

        slow.Asks[0].SetResult(null);

        cut.WaitForState(() => cut.RenderCount > drawn);

        Assert.DoesNotContain("page", cut.Markup);

        slow.Asks[1].SetResult(null);

        cut.WaitForAssertion(() => Assert.Contains("page", cut.Markup));
    }

    /// <summary>K1: the gate that is still current redirects as it always did.</summary>
    [Fact]
    public void AnUnsupersededGateStillRedirects()
    {
        Services.AddSingleton<IRouteGate>(new RedirectingRouteGate(typeof(ProbePage), typeof(ProbeOtherPage)));

        var before = Navigation.History.Count;

        Render<NsRouteGate>(p => p
            .AddCascadingValue(new RouteTable(typeof(ProbePage).Assembly, []))
            .Add(x => x.PageType, typeof(ProbePage)));

        Assert.Equal(before + 1, Navigation.History.Count);
    }

    /// <summary>AC2: the first session's arrangement read finishes after the second session's
    /// and must not become what the prerender persists.</summary>
    [Fact]
    public async Task ASupersededSessionsArrangementIsNotRemembered()
    {
        var handoff = await Handoff.Empty();
        var arranger = new SequencedArrangement();
        var menu = new NavMenu([new FixedNavContributor()], [], arrangements: [arranger], handoff: handoff.State);

        var first = menu.GetItems(Task.FromResult(new AuthenticationState(new ClaimsPrincipal())));
        var second = menu.GetItems(Task.FromResult(new AuthenticationState(new ClaimsPrincipal())));

        arranger.Answers[1].SetResult(Placed("New", 2));
        await second;

        arranger.Answers[0].SetResult(Placed("Old", 1));
        await first;

        await handoff.Manager.PersistStateAsync(handoff, Renderer);

        var persisted = handoff.Read<NavMenuHandoff>(HandoffKey);

        Assert.Equal("New", persisted?.Arrangements.Single().Entries.Single().Name);
    }

    /// <summary>AC2's second half: a count that cannot answer is left out and named.</summary>
    [Fact]
    public async Task ASwallowedCountFailureIsLoggedWithTheEntrysName()
    {
        var menu = new NavMenu([], [new ThrowingCount("Tickets")], log: new Logger<NavMenu>(_log));

        Assert.Empty(await menu.GetCounts());
        Assert.Contains(_log.Messages, message => message.Contains("Tickets", StringComparison.Ordinal));
    }

    /// <summary>AC3 and K1: one contributor throws while another is still asking, the slot
    /// keeps its mark until the slow one answers, then stops resolving, and nobody is asked twice.</summary>
    [Fact]
    public void AThrowingContributorDropsOnlyItsOwnItemsAndIsLogged()
    {
        Services.AddSingleton<IActionContributor<ProbeRowOutlet>>(new ThrowingContributor());
        var slow = new WaitingContributor { Verb = "Sync" };
        Services.AddSingleton<IActionContributor<ProbeRowOutlet>>(slow);

        var cut = Render<ActionOutletHost>(p => p
            .Add(x => x.Mounted, true)
            .Add(x => x.Context, "Ana"));

        cut.WaitForAssertion(() => Assert.Contains("ns-action-mark", cut.Markup));

        slow.Answer();

        cut.WaitForAssertion(() => Assert.DoesNotContain("ns-action-mark", cut.Markup));
        Assert.Contains("Sync-Ana", cut.Markup);
        Assert.Equal(1, slow.Asks);
        Assert.Contains(_log.Messages, message => message.Contains(nameof(ThrowingContributor), StringComparison.Ordinal));
    }

    static NavMenuArrangement Placed(string name, int order)
    {
        return new NavMenuArrangement
        {
            Entries = [new NavMenuArrangementEntry { Name = name, Order = order }]
        };
    }

    (ScriptedRouteGate Slow, IRenderedComponent<NsRouteGate> Cut) RenderSuperseded()
    {
        var slow = new ScriptedRouteGate();

        Services.AddSingleton<IRouteGate>(slow);

        var cut = Render<NsRouteGate>(p => p
            .AddCascadingValue(new RouteTable(typeof(ProbePage).Assembly, []))
            .Add(x => x.PageType, typeof(ProbePage))
            .Add(x => x.ChildContent, "page"));

        cut.Render(p => p.Add(x => x.PageType, typeof(ProbeAdminPage)));

        Assert.Equal(2, slow.Asks.Count);

        return (slow, cut);
    }

    sealed class ScriptedRouteGate : IRouteGate
    {
        public List<TaskCompletionSource<Type?>> Asks { get; } = [];

        public Task<Type?> GetRedirect(Type page, CancellationToken cancellationToken = default)
        {
            var ask = new TaskCompletionSource<Type?>(TaskCreationOptions.RunContinuationsAsynchronously);

            Asks.Add(ask);

            return ask.Task;
        }
    }

    sealed class SequencedArrangement : INavMenuArrangement
    {
        public List<TaskCompletionSource<NavMenuArrangement>> Answers { get; } = [];

        public Task<NavMenuArrangement> GetArrangement(CancellationToken cancellationToken = default)
        {
            var answer = new TaskCompletionSource<NavMenuArrangement>(TaskCreationOptions.RunContinuationsAsynchronously);

            Answers.Add(answer);

            return answer.Task;
        }
    }

    sealed class ThrowingCount(string name) : INavMenuCount
    {
        public string Name { get; } = name;

        public Task<int> GetCount()
        {
            throw new InvalidOperationException("down");
        }
    }

    sealed class ThrowingContributor : IActionContributor<ProbeRowOutlet>
    {
        public Task<IReadOnlyList<ActionItem>> GetActions(ProbeRowOutlet outlet)
        {
            throw new InvalidOperationException("down");
        }
    }

    sealed class CapturedLog : ILoggerFactory, ILoggerProvider
    {
        public List<string> Messages { get; } = [];

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new Sink(Messages);
        }

        public void Dispose()
        {
        }

        sealed class Sink(List<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                messages.Add(formatter(state, exception));
            }
        }
    }
}
