// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;
using NSail.Problems;

namespace NSail.Components.Tests;

/// <summary>Leonardo, twice in one session: "sigo sin ver mensajes de error bajo los campos".
/// The server answered a well-formed, localized Problem and the screen showed a generic toast
/// or nothing at all. The contract NsForm owes, pinned here: an issue naming a field this form
/// actually renders draws under that field; an issue naming anything else — a property no
/// field bound (6062c3fb's silent absorption), a rule name, nothing at all — draws at form
/// level, visibly. Nothing ever vanishes, and the text is the one the sender already
/// localized.</summary>
public sealed class NsFormProblemDisplayTests : BunitContext, IAsyncLifetime
{
    public NsFormProblemDisplayTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // Same MudPopoverProvider teardown rule as NsSelectTests: bunit's synchronous Dispose
    // cannot tear down MudBlazor's popover provider, so xunit's async lifecycle takes over.
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    static Problem Invalid(params Issue[] issues)
    {
        return new("InvalidModel", "One or more fields contain invalid data", issues, 400);
    }

    /// <summary>The foot line only exists while there is something to say (Leonardo 2026-08-07,
    /// superseding the reserved-geometry contract: a panel footer seats the messages in the
    /// button row, and the unclaimed fallback draws only when non-empty) — so "nothing fell to
    /// the foot" is a missing element or an empty one, both read as empty here.</summary>
    static string FootAlert<TComponent>(IRenderedComponent<TComponent> cut) where TComponent : class, IComponent
    {
        var feet = cut.FindAll(".ns-form-problem");

        return feet.Count == 0 ? string.Empty : feet[0].TextContent;
    }

    // The fields render in binding order (Name, then Alias). Asked of the input itself rather
    // than counted in the markup: MudBlazor stamps .mud-input-error on several nested nodes of
    // one errored field, so a count of them says nothing about how many fields were marked.
    static string?[] FieldErrors<TComponent>(IRenderedComponent<TComponent> cut) where TComponent : class, IComponent
    {
        return cut.FindComponents<MudTextField<string>>()
            .Select(field => field.Instance.Error ? field.Instance.ErrorText : null)
            .ToArray();
    }

    [Fact]
    public async Task IssueNamingARenderedField_DrawsUnderThatField_AndNowhereElse()
    {
        var problem = Invalid(new Issue("OutOfRange", "El eje va de 1 a 180 grados.", "Name"));

        var cut = Render<FormProblemHost>(p => p
            .Add(x => x.Model, new FormProblemModel())
            .Add(x => x.Problem, problem));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.Equal(["El eje va de 1 a 180 grados.", null], FieldErrors(cut));
        Assert.Equal(string.Empty, FootAlert(cut));
    }

    /// <summary>6062c3fb's lesson, now binding: the source was a real property of the model, so
    /// the old ApplyProblem called it handled and attached the message to an input that never
    /// rendered — the refusal vanished entirely and the screen just sat there.</summary>
    [Fact]
    public async Task IssueNamingAPropertyNoFieldRenders_DrawsAtFormLevel()
    {
        var problem = Invalid(new Issue("Required", "Falta el apodo.", nameof(FormProblemModel.Nickname)));

        var cut = Render<FormProblemHost>(p => p
            .Add(x => x.Model, new FormProblemModel())
            .Add(x => x.Problem, problem));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.Contains("Falta el apodo.", FootAlert(cut), StringComparison.Ordinal);
    }

    /// <summary>CreatePrescription's five rules answer RuleViolation issues whose source is the
    /// rule name, not a member — the case that reached the user as a generic toast.</summary>
    [Fact]
    public async Task IssueNamingARuleRatherThanAField_DrawsAtFormLevel()
    {
        var problem = new Problem(
            "RuleViolation",
            "The operation could not be completed",
            [new Issue("RuleViolation", "El eje va de 1 a 180 grados.", "AxisRange")],
            422);

        var cut = Render<FormProblemHost>(p => p
            .Add(x => x.Model, new FormProblemModel())
            .Add(x => x.Problem, problem));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.Contains("El eje va de 1 a 180 grados.", FootAlert(cut), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProblemWithNoIssueAtAll_DrawsItsTitleAtFormLevel()
    {
        var problem = new Problem("Unknown", "Something went wrong", [], 500);

        var cut = Render<FormProblemHost>(p => p
            .Add(x => x.Model, new FormProblemModel())
            .Add(x => x.Problem, problem));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.Contains("Something went wrong", FootAlert(cut), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TwoIssues_EachLandOnItsOwnField()
    {
        var problem = Invalid(
            new Issue("Required", "Falta el nombre.", nameof(FormProblemModel.Name)),
            new Issue("Required", "Falta el alias.", nameof(FormProblemModel.Alias)));

        var cut = Render<FormProblemHost>(p => p
            .Add(x => x.Model, new FormProblemModel())
            .Add(x => x.Problem, problem));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.Equal(["Falta el nombre.", "Falta el alias."], FieldErrors(cut));
        Assert.Equal(string.Empty, FootAlert(cut));
    }

    /// <summary>One Problem carrying both kinds: the field one goes under its field, the rule
    /// one goes to form level, and neither swallows the other.</summary>
    [Fact]
    public async Task AFieldIssueAndARuleIssueTogether_BothShow()
    {
        var problem = Invalid(
            new Issue("Required", "Falta el nombre.", nameof(FormProblemModel.Name)),
            new Issue("RuleViolation", "Los pagos no cubren la venta.", "TendersMismatch"));

        var cut = Render<FormProblemHost>(p => p
            .Add(x => x.Model, new FormProblemModel())
            .Add(x => x.Problem, problem));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.Equal(["Falta el nombre.", null], FieldErrors(cut));
        Assert.Contains("Los pagos no cubren la venta.", FootAlert(cut), StringComparison.Ordinal);
    }

    /// <summary>The form having shown it is what stops the generic toast: the report is marked
    /// handled before it can travel to the surface's ProblemManager.</summary>
    [Fact]
    public async Task AProblemTheFormShowed_IsReportedAsHandled()
    {
        ProblemEventArgs? seen = null;

        var problem = Invalid(new Issue("RuleViolation", "Los pagos no cubren la venta.", "TendersMismatch"));

        var cut = Render<FormProblemHost>(p => p
            .Add(x => x.Model, new FormProblemModel())
            .Add(x => x.Problem, problem)
            .Add(x => x.Reported, args => seen = args));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.NotNull(seen);
        Assert.True(seen!.Handled);
    }

    /// <summary>A second submit that the server accepted clears what the first one drew — a
    /// stale refusal under a field the user already fixed is its own defect.</summary>
    [Fact]
    public async Task ASubmitWithNoProblem_ClearsWhatThePreviousOneDrew()
    {
        var problem = Invalid(new Issue("RuleViolation", "Los pagos no cubren la venta.", "TendersMismatch"));

        var cut = Render<FormProblemHost>(p => p
            .Add(x => x.Model, new FormProblemModel())
            .Add(x => x.Problem, problem));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.NotEqual(string.Empty, FootAlert(cut));

        cut.Render(p => p.Add(x => x.Problem, (Problem?)null));
        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.Equal(string.Empty, FootAlert(cut));
    }

    /// <summary>nsail#775: a refusal the SCREEN decided, with no send behind it, takes the same
    /// path a server one does — the submit args carry it, the form applies it, and the panel
    /// footer seats it in the row the buttons already occupy. One strip, in the one placement,
    /// so no screen has to reach for an alert of its own and grow that row.</summary>
    [Fact]
    public async Task ARefusalTheScreenDecidedItself_SeatsInThePanelFooterLikeAServerOne()
    {
        var cut = Render<ClientRefusalHost>(p => p
            .Add(x => x.Model, new FormProblemModel())
            .Add(x => x.Refuses, true));

        Assert.Empty(cut.FindAll(".ns-form-problem"));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        var strip = Assert.Single(cut.FindAll(".ns-form-problem"));

        Assert.Contains("La fecha de fin es anterior a la de inicio.", strip.TextContent, StringComparison.Ordinal);
        Assert.Contains("ns-form-problem-inline", strip.ClassList);
        Assert.Equal("alert", strip.GetAttribute("role"));

        // The row the buttons already occupy, not one of its own.
        Assert.NotNull(strip.ParentElement);
        Assert.Contains("ns-panel-footer", strip.ParentElement!.ClassList);

        // And nothing was sent: this is the refusal caught before the round trip.
        Assert.Equal(0, cut.Instance.Sent);
    }

    /// <summary>The same terms the server-side ones clear on: HandleSubmit opens with
    /// ClearProblems, so a screen's own refusal needs no clearing rule of its own.</summary>
    [Fact]
    public async Task AScreensOwnRefusal_ClearsOnTheNextSubmit()
    {
        var cut = Render<ClientRefusalHost>(p => p
            .Add(x => x.Model, new FormProblemModel())
            .Add(x => x.Refuses, true));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.NotEqual(string.Empty, FootAlert(cut));

        cut.Render(p => p.Add(x => x.Refuses, false));
        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.Equal(string.Empty, FootAlert(cut));
        Assert.Equal(1, cut.Instance.Sent);
    }

    /// <summary>nsail#1867: a member a screen edits through composed controls — an attendee list
    /// drawn as a panel of rows — had no way to carry its own refusal, because only a field
    /// announces an identifier and that member has no field. NsFieldRefusal is that field with
    /// no input: it announces the member and draws the sentence where the editor stands, so the
    /// reason reads beside the controls it is about instead of falling to the foot as the
    /// Stack's generic "Invalid value".</summary>
    [Fact]
    public async Task IssueNamingAMemberAnAnchorAnnounces_DrawsAtTheAnchor_AndNotAtTheFoot()
    {
        var problem = Invalid(new Issue("Required", "Falta el apodo.", nameof(FormProblemModel.Nickname)));

        var cut = Render<AnchoredMemberHost>(p => p
            .Add(x => x.Model, new FormProblemModel())
            .Add(x => x.Problem, problem));

        // Nothing is spent on the possibility: the anchor renders no node at all while clean.
        Assert.Empty(cut.FindAll(".nickname-editor .mud-input-helper-text"));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        var refusal = Assert.Single(cut.FindAll(".nickname-editor .mud-input-helper-text"));

        Assert.Equal("Falta el apodo.", refusal.TextContent);
        Assert.Contains("mud-input-error", refusal.ClassList);
        Assert.Equal(string.Empty, FootAlert(cut));

        // Nothing names this node — there is no input to be described by it — so it announces
        // itself rather than being pointed at, and the clipped twin an input's reference would
        // need is not rendered at all.
        Assert.Equal("alert", refusal.GetAttribute("role"));
        Assert.Empty(cut.FindAll(".nickname-editor .ns-field-described"));
    }

    /// <summary>And the anchored refusal clears like any other: the next submit the server took
    /// leaves the editor as clean as it started.</summary>
    [Fact]
    public async Task AnAnchoredRefusal_ClearsOnTheNextSubmitTheServerTook()
    {
        var problem = Invalid(new Issue("Required", "Falta el apodo.", nameof(FormProblemModel.Nickname)));

        var cut = Render<AnchoredMemberHost>(p => p
            .Add(x => x.Model, new FormProblemModel())
            .Add(x => x.Problem, problem));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.NotEmpty(cut.FindAll(".nickname-editor .mud-input-helper-text"));

        cut.Render(p => p.Add(x => x.Problem, (Problem?)null));
        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.Empty(cut.FindAll(".nickname-editor .mud-input-helper-text"));
    }

    /// <summary>A field inside a tab is rendered (NsTabs keeps every panel alive), so its
    /// issue anchors like any other — the tracker chain has to reach past the tab that owns
    /// the nearer cascade.</summary>
    [Fact]
    public async Task IssueNamingAFieldInsideATab_DrawsUnderThatField()
    {
        var problem = Invalid(new Issue("Required", "Falta el alias.", nameof(FormProblemModel.Alias)));

        var cut = Render<FormProblemTabHost>(p => p
            .Add(x => x.Model, new FormProblemModel())
            .Add(x => x.Problem, problem));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.Equal([null, "Falta el alias."], FieldErrors(cut));
        Assert.Equal(string.Empty, FootAlert(cut));
    }

    /// <summary>The second placement of the doctrine (intentional-ui, refusal placement): the
    /// message is anchored on a field the user cannot see, so the TAB HEADER says so —
    /// persistent and spatial, pointing at exactly where to go. This is what replaced the
    /// snackbar, which raced the eye and left nothing to come back to. Derived from the fields
    /// that announced themselves inside the tab, so no page wires it.</summary>
    [Fact]
    public async Task IssueOnAFieldInAnInactiveTab_BadgesThatTabHeaderAndNoOther()
    {
        var problem = Invalid(new Issue("Required", "Falta el alias.", nameof(FormProblemModel.Alias)));

        var cut = Render<FormProblemTabHost>(p => p
            .Add(x => x.Model, new FormProblemModel())
            .Add(x => x.Problem, problem));

        var tabs = cut.FindAll(".mud-tab");

        // Alias lives on the second tab, and the first is the one showing.
        Assert.Contains("mud-tab-active", tabs[0].ClassList);

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        tabs = cut.FindAll(".mud-tab");

        Assert.DoesNotContain("mud-badge-dot", tabs[0].OuterHtml, StringComparison.Ordinal);
        Assert.Contains("mud-badge-dot", tabs[1].OuterHtml, StringComparison.Ordinal);
        Assert.Contains("mud-theme-error", tabs[1].OuterHtml, StringComparison.Ordinal);

        // And it stayed anchored: the badge points at it, the foot line did not absorb it.
        Assert.Equal(string.Empty, FootAlert(cut));
    }
}
