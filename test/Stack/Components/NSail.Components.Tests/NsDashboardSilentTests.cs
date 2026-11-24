// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSail.Components.Tests.Fixtures;
using NSail.Icons;
using NSail.Localization;
using NSail.Messaging;
using NSail.Messaging.Runtime;
using NSail.Messaging.Runtime.Context;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>A card that reads through Handoff and draws only when its read found something —
/// silent while the read is in flight, and silent for good when it found nothing.</summary>
public sealed class SilentUntilFoundTestCard : NsPartial
{
    ProbeAnswer? _answer;

    [Parameter]
    public Glyph? Icon { get; set; }

    protected override async Task OnInitializedAsync()
    {
        _answer = await Handoff(new ProbeRead());
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (_answer is not { Count: > 0 })
        {
            return;
        }

        builder.OpenElement(0, "p");
        builder.AddAttribute(1, "class", "found");
        builder.CloseElement();
    }
}

/// <summary>Answers every read when the test says so, with the figure the test names.</summary>
public sealed class HeldMediator : Mediator
{
    readonly TaskCompletionSource<int> _answer = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public HeldMediator()
        : base(new EmptyServices())
    {
    }

    public void Answer(int count)
    {
        _answer.SetResult(count);
    }

    public override async Task<TResult> Send<TResult>(IMessage<TResult> message, IReadOnlyDictionary<string, string>? headers, CancellationToken cancellationToken = default)
    {
        return (TResult)(object)new ProbeAnswer { Count = await _answer.Task };
    }

    sealed class EmptyServices : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            return null;
        }
    }
}

/// <summary>The dashboard's Silent slot: the host's answer when the gate allowed cards and
/// every one of them answered with nothing. What this pins is that it appears exactly then —
/// never in front of a card whose read has not answered, never beside a card that drew, never
/// in Empty's place — and that a host that gives none sees nothing new.</summary>
public sealed class NsDashboardSilentTests : BunitContext
{
    const string SilentMarkup = "<p id=\"nothing-to-show\">silent</p>";

    const string EmptyMarkup = "<p id=\"nothing-here\">portal</p>";

    // The selector the stylesheet hides the slot with, restated so the DOM and the rule are
    // read against the same question.
    const string Hidden = ".ns-dashboard:has(> .ns-dashboard-item > .ns-dashboard-card > *) + .ns-dashboard-silent";

    readonly HeldMediator _mediator = new();

    sealed class TestContributor(params DashboardItem[] items) : IDashboardContributor
    {
        public Task<IReadOnlyList<DashboardItem>> GetItems()
        {
            return Task.FromResult<IReadOnlyList<DashboardItem>>(items);
        }
    }

    public NsDashboardSilentTests()
    {
        AddAuthorization().SetNotAuthorized();
        Services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        Services.AddScoped<PageGate>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsDashboardSilentTests).Assembly, []));
        Services.AddSingleton<DialogManager>(new CountingDialogManager());
        Services.AddSingleton<Mediator>(_mediator);
        Services.AddSingleton<MessageContextAccessor>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    IRenderedComponent<NsDashboard> Show(bool silent, params DashboardItem[] items)
    {
        Services.AddScoped<IDashboardContributor>(_ => new TestContributor(items));

        return Render<NsDashboard>(parameters =>
        {
            parameters.Add(
                dashboard => dashboard.Empty,
                (RenderFragment)(builder => builder.AddMarkupContent(0, EmptyMarkup)));

            if (silent)
            {
                parameters.Add(
                    dashboard => dashboard.Silent,
                    (RenderFragment)(builder => builder.AddMarkupContent(0, SilentMarkup)));
            }
        });
    }

    static DashboardItem Mute()
    {
        return new DashboardItem { Name = "Mute", CardType = typeof(IconStampedTestCard) };
    }

    static DashboardItem Speaking()
    {
        return new DashboardItem
        {
            Name = "Speaking",
            CardType = typeof(IconStampedTestCard),
            Icon = new Glyph("<path />")
        };
    }

    static DashboardItem Reading()
    {
        return new DashboardItem { Name = "Reading", CardType = typeof(SilentUntilFoundTestCard) };
    }

    [Fact]
    public void TheSilentSlotRendersWhenTheOnlyAllowedCardDrewNothing()
    {
        var cut = Show(silent: true, Mute());

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".ns-dashboard-silent #nothing-to-show")));

        Assert.Empty(cut.FindAll(Hidden));
        Assert.Empty(cut.FindAll("#nothing-here"));
    }

    [Fact]
    public void TheSilentSlotIsHiddenBesideACardThatDrew()
    {
        var cut = Show(silent: true, Mute(), Speaking());

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(Hidden)));
    }

    [Fact]
    public void TheSilentSlotWaitsForACardWhoseReadHasNotAnswered()
    {
        var cut = Show(silent: true, Reading());

        Assert.NotNull(cut.Find(".ns-dashboard"));
        Assert.Empty(cut.FindAll(".ns-dashboard-silent"));

        _mediator.Answer(0);

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".ns-dashboard-silent #nothing-to-show")));
        Assert.Empty(cut.FindAll(Hidden));
    }

    [Fact]
    public void ACardWhoseReadFoundSomethingHidesTheSilentSlot()
    {
        var cut = Show(silent: true, Reading());

        Assert.Empty(cut.FindAll(".ns-dashboard-silent"));

        _mediator.Answer(3);

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".found")));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(Hidden)));
    }

    [Fact]
    public void TheSilentSlotNeverStandsInEmptysPlace()
    {
        var cut = Show(silent: true, new DashboardItem { Name = "Denied", CardType = typeof(AdminTestCard) });

        Assert.NotNull(cut.Find("#nothing-here"));
        Assert.Empty(cut.FindAll(".ns-dashboard-silent"));
    }

    [Fact]
    public void AHostThatGivesNoSilentContentSeesNoSlot()
    {
        var cut = Show(silent: false, Mute());

        Assert.Empty(cut.FindAll(".ns-dashboard-silent"));
        Assert.Empty(cut.FindAll("#nothing-here"));
    }

    [Fact]
    public void TheStylesheetHidesTheSlotBesideACardThatDrew()
    {
        var css = File.ReadAllText(Path.Combine(
            RepoRoot(),
            "src/Stack/Components/NSail.Components.Mud/wwwroot/ns-mud.css"));

        Assert.Contains($"{Hidden} {{", css, StringComparison.Ordinal);
    }

    static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NSail.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"NSail.sln was not found above {AppContext.BaseDirectory}.");
    }
}
