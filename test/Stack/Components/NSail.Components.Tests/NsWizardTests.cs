// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>The wizard's contract (intentional-ui.md, Wizard): it hosts the steps onboarding
/// contributes, ordered by weight and opened on the first pending one; the chrome owns Next
/// and Back; a step participates through an async hook that may work and may veto; Next stays
/// disabled until the step says it can advance, and a step that says nothing is born complete;
/// Back remounts the step, which is what reloads it from the server.</summary>
public sealed class NsWizardTests : BunitContext, IAsyncLifetime
{
    readonly WizardProbe _probe = new();

    public NsWizardTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddScoped<IBrowserViewportService>(_ => new FixedBreakpointViewportService(Breakpoint.Lg));
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddScoped<NavMenu>();
        Services.AddSingleton(new RouteTable(typeof(NsWizardTests).Assembly, []));
        Services.AddSingleton(_probe);
    }

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    static OnboardingStep Step(string name, Type stepType, int weight, bool? complete = null)
    {
        return new OnboardingStep
        {
            Name = name,
            StepType = stepType,
            Weight = weight,
            IsCompleteAsync = complete is null ? null : () => Task.FromResult(complete.Value)
        };
    }

    IRenderedComponent<NsWizard> Mount(params OnboardingStep[] steps)
    {
        Services.AddSingleton<IOnboardingContributor>(new WizardContributor(steps));

        return Render<NsWizard>();
    }

    IRenderedComponent<NsWizard> Mount(IOnboardingContributor[] contributors)
    {
        foreach (var contributor in contributors)
        {
            Services.AddSingleton(contributor);
        }

        return Render<NsWizard>();
    }

    static string[] RailTitles(IRenderedComponent<NsWizard> cut)
    {
        return cut.FindAll(".ns-wizard-rail-title").Select(node => node.TextContent.Trim()).ToArray();
    }

    static bool RailMarked(IRenderedComponent<NsWizard> cut, int index)
    {
        return cut.FindAll(".ns-wizard-rail-step")[index].ClassList.Contains("ns-wizard-rail-step-complete");
    }

    static bool NextIsDisabled(IRenderedComponent<NsWizard> cut)
    {
        return cut.Find("button.ns-wizard-next").HasAttribute("disabled");
    }

    /// <summary>Registration by weight is OnboardingStep's job and is pinned there; what this
    /// pins is that the wizard consumes that order — the rail lists the steps in it, and the
    /// first one is what opens.</summary>
    [Fact]
    public void StepsAreHostedInWeightOrderAcrossContributors()
    {
        var cut = Mount([
            new WizardContributor(Step("Late", typeof(WizardInformationalStepProbe), 90)),
            new WizardContributor(Step("Early", typeof(WizardWorkingStepProbe), 10))]);

        Assert.Equal(["Early", "Late"], RailTitles(cut));
        Assert.NotNull(cut.Find(".ns-probe-working"));
    }

    /// <summary>An informational step registers nothing, so nothing has to enable its button:
    /// it is born complete and the chrome's Next is live on arrival.</summary>
    [Fact]
    public void AnInformationalStepIsBornComplete()
    {
        var cut = Mount(
            Step("Welcome", typeof(WizardInformationalStepProbe), 10),
            Step("Work", typeof(WizardWorkingStepProbe), 20));

        Assert.NotNull(cut.Find(".ns-probe-informational"));
        Assert.False(NextIsDisabled(cut));
    }

    [Fact]
    public async Task NextIsDisabledUntilTheStepReportsItCanAdvance()
    {
        _probe.CanAdvance = false;

        var cut = Mount(
            Step("Work", typeof(WizardWorkingStepProbe), 10),
            Step("Welcome", typeof(WizardInformationalStepProbe), 20));

        Assert.True(NextIsDisabled(cut));

        await cut.InvokeAsync(() => _probe.Report(true));

        Assert.False(NextIsDisabled(cut));
    }

    /// <summary>The veto half of the hook: the step did its work, refused, and stays on screen
    /// with whatever it drew — which is how a form step keeps the refusal under its own field.</summary>
    [Fact]
    public async Task AVetoedOnNextHoldsTheStep()
    {
        _probe.Vetoes = true;

        var cut = Mount(
            Step("Work", typeof(WizardWorkingStepProbe), 10),
            Step("Welcome", typeof(WizardInformationalStepProbe), 20));

        await cut.InvokeAsync(() => cut.Find("button.ns-wizard-next").Click());

        Assert.Equal(1, _probe.Submits);
        Assert.NotNull(cut.Find(".ns-probe-working"));
        Assert.Empty(cut.FindAll(".ns-probe-informational"));
    }

    [Fact]
    public async Task ASuccessfulOnNextAdvances()
    {
        var cut = Mount(
            Step("Work", typeof(WizardWorkingStepProbe), 10),
            Step("Welcome", typeof(WizardInformationalStepProbe), 20));

        await cut.InvokeAsync(() => cut.Find("button.ns-wizard-next").Click());

        Assert.Equal(1, _probe.Submits);
        Assert.NotNull(cut.Find(".ns-probe-informational"));
        Assert.Empty(cut.FindAll(".ns-probe-working"));
    }

    /// <summary>Back reloads the step from the server, and the mechanism is the remount: the
    /// step's own load runs on arrival, so the wizard needs to know nothing about refetching.</summary>
    [Fact]
    public async Task BackReloadsTheStep()
    {
        var cut = Mount(
            Step("Work", typeof(WizardWorkingStepProbe), 10),
            Step("Welcome", typeof(WizardInformationalStepProbe), 20));

        Assert.Equal(1, _probe.Loads);

        await cut.InvokeAsync(() => cut.Find("button.ns-wizard-next").Click());
        await cut.InvokeAsync(() => cut.Find("button.ns-wizard-back").Click());

        Assert.Equal(2, _probe.Loads);
        Assert.NotNull(cut.Find(".ns-probe-working"));
    }

    /// <summary>nsail#1460: a step's hook is the form's Submit() it drew no button for, and the
    /// chrome's Next sits OUTSIDE that form — no cascade of it reaches here, so nothing greyed
    /// the button and a second Next re-entered the step's Runner for its "already running"
    /// throw. The sequence owns Next, so the sequence is what refuses: while one is in flight
    /// neither way out is offered, and both come back when it lands.</summary>
    [Fact]
    public async Task WhileANextIsInFlight_NeitherWayOutOfTheStepIsOffered()
    {
        _probe.Held = new TaskCompletionSource<bool>();

        var cut = Mount(
            Step("First", typeof(WizardInformationalStepProbe), 10),
            Step("Work", typeof(WizardWorkingStepProbe), 20),
            Step("Welcome", typeof(WizardInformationalStepProbe), 30));

        // Onto the working step, whose hook is the one that holds. The informational step this
        // leaves registers none, so this Next answers before it returns.
        await cut.InvokeAsync(() => cut.Find("button.ns-wizard-next").Click());

        Assert.False(NextIsDisabled(cut));
        Assert.False(cut.Find("button.ns-wizard-back").HasAttribute("disabled"));

        // Discarded on purpose: the hook's own Task only completes when the probe is released
        // below, so awaiting the dispatch here would deadlock — the WaitForAssertion that
        // follows is the wait that stands in for it (NsLoadTests' note).
        _ = cut.InvokeAsync(() => cut.Find("button.ns-wizard-next").Click());

        cut.WaitForAssertion(() =>
        {
            Assert.True(NextIsDisabled(cut));
            Assert.True(cut.Find("button.ns-wizard-back").HasAttribute("disabled"));
        });

        Assert.Equal(1, _probe.Submits);

        _probe.Held.SetResult(true);

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".ns-probe-informational")));
    }

    /// <summary>The veto half of the same guard: a step that refused is still a step somebody
    /// has to be able to leave, so the refusal is handed back whichever way the hook ended.</summary>
    [Fact]
    public async Task ANextThatWasVetoedHandsTheButtonsBack()
    {
        _probe.Vetoes = true;

        var cut = Mount(
            Step("Work", typeof(WizardWorkingStepProbe), 10),
            Step("Welcome", typeof(WizardInformationalStepProbe), 20));

        await cut.InvokeAsync(() => cut.Find("button.ns-wizard-next").Click());

        Assert.False(NextIsDisabled(cut));
        Assert.NotNull(cut.Find(".ns-probe-working"));
    }

    [Fact]
    public void BackIsDisabledOnTheFirstStep()
    {
        var cut = Mount(
            Step("Work", typeof(WizardWorkingStepProbe), 10),
            Step("Welcome", typeof(WizardInformationalStepProbe), 20));

        Assert.True(cut.Find("button.ns-wizard-back").HasAttribute("disabled"));
    }

    [Fact]
    public async Task TheRailMarksTheStepTheWizardAdvancedPast()
    {
        var cut = Mount(
            Step("Work", typeof(WizardWorkingStepProbe), 10),
            Step("Welcome", typeof(WizardInformationalStepProbe), 20));

        Assert.False(RailMarked(cut, 0));

        await cut.InvokeAsync(() => cut.Find("button.ns-wizard-next").Click());

        Assert.True(RailMarked(cut, 0));
        Assert.False(RailMarked(cut, 1));
    }

    /// <summary>A step that answered "already done" before the sequence opened is behind the
    /// wizard, and the rail says so — the landing index and the marks come from the same walk.</summary>
    [Fact]
    public void AStepAlreadyCompleteIsMarkedAndTheWizardOpensPastIt()
    {
        var cut = Mount(
            Step("Done", typeof(FirstTestStep), 10, complete: true),
            Step("Work", typeof(WizardWorkingStepProbe), 20),
            Step("Welcome", typeof(WizardInformationalStepProbe), 30));

        Assert.True(RailMarked(cut, 0));
        Assert.NotNull(cut.Find(".ns-probe-working"));
    }

    /// <summary>The closing act names itself: every other step advances with the generic
    /// verb, the last one fires with its own — never the same word for two different acts.</summary>
    [Fact]
    public async Task TheLastStepsButtonNamesItselfFinishRatherThanTheGenericNext()
    {
        var cut = Mount(
            Step("Work", typeof(WizardWorkingStepProbe), 10),
            Step("Welcome", typeof(WizardInformationalStepProbe), 20));

        Assert.Equal("Common.Next", cut.Find("button.ns-wizard-next").TextContent.Trim());

        await cut.InvokeAsync(() => cut.Find("button.ns-wizard-next").Click());

        Assert.Equal("Common.Finish", cut.Find("button.ns-wizard-next").TextContent.Trim());
    }
}
