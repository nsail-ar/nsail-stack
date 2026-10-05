// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Messaging.Runtime.Context;
using NSail.Metadata;
using NSail.Problems;

namespace NSail.Components.Tests;

/// <summary>nsail#2006: Probar on Almacenamiento was refused for a reason that named a field the
/// screen renders (Proveedor) and the person read it in a toast that faded, with nothing marked.
/// The cause was structural and not that screen's: an act a page hosts runs on the PAGE's Runner,
/// so its refusal reached ProblemManager with no way back to the form the button was pressed in,
/// however well the issue was anchored.
///
/// What is held here is the seam that closes it. Standing inside a form is a cascade, so the
/// button is what knows it; the surface carries that for the length of the call; the form draws
/// what comes back by the one rule it draws its own refusals by. A probe is not an edit, so none
/// of it touches the document — and an act with no form around it keeps the toast, which is the
/// only place left for a refusal nothing on screen can carry.</summary>
public sealed class NsHostedActRefusalTests : BunitContext, IAsyncLifetime
{
    public NsHostedActRefusalTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddComponentServices();
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            ["Actions.Probe"] = "Probar",
            ["Actions.ProbeOutside"] = "Probar afuera",
        })]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton<Mediator>(new CapturingMediator());
        Services.AddSingleton<MessageContextAccessor>();
        Services.AddSingleton(new RouteTable(typeof(NsHostedActRefusalTests).Assembly, []));

        // The toast is where every one of these refusals used to end, so counting it is what tells
        // "the form drew it" apart from "the form drew it and it also faded past the person".
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

    // The shape StorageManager raises: a rule with its own code, naming the field the screen
    // renders for it (Provider there, Name here).
    static Problem Anchored(string source)
    {
        return new(
            code: "InvalidModel",
            title: "One or more fields contain invalid data",
            issues: [new Issue("NoStorageConfigured", "This install stores nowhere yet.", source)],
            status: 400);
    }

    static Problem Unanchored()
    {
        return new(
            code: "RuleViolation",
            title: "The operation could not be completed",
            issues: [new Issue("StorageRefused", "The storage service answered 403.")],
            status: 422);
    }

    IRenderedComponent<HostedActHost> Host(Problem? refusal, Problem? saveRefusal = null)
    {
        return Render<HostedActHost>(p => p
            .Add(x => x.RouteTable, Services.GetRequiredService<RouteTable>())
            .Add(x => x.Refusal, refusal)
            .Add(x => x.SaveRefusal, saveRefusal));
    }

    static Task Press(IRenderedComponent<HostedActHost> host, string act)
    {
        return host.InvokeAsync(() => host.Find($".{act}").Click());
    }

    // The ? beside the act: an NsButton like every other face, pressed to read a paragraph.
    static Task PressHelp(IRenderedComponent<HostedActHost> host)
    {
        return host.InvokeAsync(() => host.Find(".ns-help button").Click());
    }

    static Task Save(IRenderedComponent<HostedActHost> host)
    {
        return host.InvokeAsync(() => host.Find("form").Submit());
    }

    // Asked of the input itself and not counted in the markup: MudBlazor stamps its error class on
    // several nested nodes of one field (NsFormProblemDisplayTests' own note).
    static string?[] FieldErrors(IRenderedComponent<HostedActHost> host)
    {
        return host.FindComponents<MudBlazor.MudTextField<string>>()
            .Select(field => field.Instance.Error ? field.Instance.ErrorText : null)
            .ToArray();
    }

    static string Foot(IRenderedComponent<HostedActHost> host)
    {
        var feet = host.FindAll(".ns-form-problem");

        return feet.Count == 0 ? string.Empty : feet[0].TextContent;
    }

    [Fact]
    public async Task ARefusedActInsideAForm_MarksTheFieldItsReasonNames_AndDrawsNoToast()
    {
        var host = Host(Anchored(nameof(FormProblemModel.Name)));

        await Press(host, "act-inside");

        Assert.Equal(["This install stores nowhere yet.", null], FieldErrors(host));
        Assert.Empty(Dialogs.Notices);
        Assert.Equal(string.Empty, Foot(host));
    }

    [Fact]
    public async Task ARefusedActWhoseReasonNamesNoRenderedField_StandsAtTheFormsFoot_AndDrawsNoToast()
    {
        var host = Host(Unanchored());

        await Press(host, "act-inside");

        Assert.Contains("The storage service answered 403.", Foot(host), StringComparison.Ordinal);
        Assert.Equal([null, null], FieldErrors(host));
        Assert.Empty(Dialogs.Notices);
    }

    /// <summary>The refusal stays where it was drawn instead of fading on a timer: nothing but the
    /// next press takes it down. A toast is the opposite contract, which is why this one is held
    /// by the renders that follow rather than by the one that drew it.</summary>
    [Fact]
    public async Task TheRefusalStandsAcrossLaterRenders()
    {
        var host = Host(Anchored(nameof(FormProblemModel.Name)));

        await Press(host, "act-inside");

        host.Render();
        await host.InvokeAsync(() => host.Find("input").Input("typed after the refusal"));

        Assert.Equal("This install stores nowhere yet.", FieldErrors(host)[0]);
    }

    /// <summary>The act that WORKS reports nothing at all, so its silence can only be read at the
    /// next press — which is where the last "no" comes down. Without this, a target fixed between
    /// two presses keeps the refusal of the first one on screen forever.</summary>
    [Fact]
    public async Task AnActThatWorksTakesDownWhatTheLastRefusedOneDrew()
    {
        var host = Host(Anchored(nameof(FormProblemModel.Name)));

        await Press(host, "act-inside");

        Assert.Equal("This install stores nowhere yet.", FieldErrors(host)[0]);

        host.Render(p => p.Add(x => x.Refusal, null));

        await Press(host, "act-inside");

        Assert.Equal(2, host.Instance.Page!.Probes);
        Assert.Equal([null, null], FieldErrors(host));
        Assert.Equal(string.Empty, Foot(host));
    }

    /// <summary>A probe is not an edit: it reaches the stored target and writes nothing back, so
    /// its refusal leaves the document exactly as clean as it found it. A submit's refusal does the
    /// opposite and hands every report back (NsForm, ApplyProblem) — this one has none to hand.</summary>
    [Fact]
    public async Task ARefusedActLeavesTheDocumentUnchanged()
    {
        var host = Host(Anchored(nameof(FormProblemModel.Name)));

        await Press(host, "act-inside");

        Assert.False(host.Instance.Surface!.HasChanges);
    }

    /// <summary>The bound, and it is deliberate: a page act with no form around it has nothing on
    /// screen that can carry a refusal, so it keeps the toast every unhandled Problem falls to.</summary>
    [Fact]
    public async Task ARefusedActOutsideEveryForm_KeepsTheToast()
    {
        var host = Host(Anchored(nameof(FormProblemModel.Name)));

        await Press(host, "act-outside");

        Assert.Equal([null, null], FieldErrors(host));
        Assert.Equal(string.Empty, Foot(host));
        Assert.Single(Dialogs.Notices);
    }

    /// <summary>The second bound, written in three documents and now held: a menu's rows render
    /// where the vendor's popover renders — a sibling of the router — so nothing the form cascaded
    /// reaches them, and an act pressed there keeps the toast. The row has to be opened first,
    /// because a menu nobody opened renders no body at all.</summary>
    [Fact]
    public async Task ARefusedActOnTheFarSideOfAPortal_KeepsTheToast()
    {
        var host = Host(Anchored(nameof(FormProblemModel.Name)));

        await host.Find(".ns-menu button").ClickAsync(new MouseEventArgs());
        await host.Find(".ns-menu-item").ClickAsync(new MouseEventArgs());

        Assert.Equal(1, host.Instance.Page!.Probes);
        Assert.Equal([null, null], FieldErrors(host));
        Assert.Equal(string.Empty, Foot(host));
        Assert.Single(Dialogs.Notices);
    }

    /// <summary>The refusal of the form's OWN submit is not a hosted act's to take down: the two
    /// share one seat, and whoever drew last owns it (NsForm, Draw). Without that the probe's
    /// refusal left a claim standing on a seat the save had taken over, so the next act spent it
    /// on the save's reason — the person read why Guardar was refused and watched it go on a press
    /// that resolved nothing of the save's. It comes down at the next SUBMIT and nowhere else.</summary>
    [Fact]
    public async Task ASavesOwnRefusalIsNotTakenDownByALaterAct()
    {
        var host = Host(Anchored(nameof(FormProblemModel.Name)), saveRefusal: Unanchored());

        await Press(host, "act-inside");

        Assert.Equal("This install stores nowhere yet.", FieldErrors(host)[0]);

        await Save(host);

        Assert.Contains("The storage service answered 403.", Foot(host), StringComparison.Ordinal);
        Assert.Equal([null, null], FieldErrors(host));

        // The target was fixed between the two presses, so the act that follows the save works and
        // reports nothing at all — the exact press whose silence used to clear the seat.
        host.Render(p => p.Add(x => x.Refusal, null).Add(x => x.SaveRefusal, Unanchored()));

        await Press(host, "act-inside");

        Assert.Equal(2, host.Instance.Page!.Probes);
        Assert.Contains("The storage service answered 403.", Foot(host), StringComparison.Ordinal);
    }

    /// <summary>A press that resolves nothing takes nothing down either, hosted refusal included:
    /// the bound is the next press of an ACT, and the ? beside the act is an NsButton like every
    /// other face in the house but sends nothing anywhere (NsHelp).</summary>
    [Fact]
    public async Task AHostedRefusalSurvivesAPressThatIsNotAnAct()
    {
        var host = Host(Anchored(nameof(FormProblemModel.Name)));

        await Press(host, "act-inside");

        await PressHelp(host);

        Assert.Equal("This install stores nowhere yet.", FieldErrors(host)[0]);
        Assert.Equal(1, host.Instance.Page!.Probes);
    }
}
