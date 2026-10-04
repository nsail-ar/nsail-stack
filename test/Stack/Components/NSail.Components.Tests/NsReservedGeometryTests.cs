// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;
using NSail.Problems;

namespace NSail.Components.Tests;

/// <summary>Refusal placement, and what pays for it. Nothing reserves: the form-level message
/// SITS IN THE BUTTON ROW when a panel footer claims it (IFormProblems — HostDialogSurfaceTests
/// pins that seating) and a form nobody claimed draws its fallback strip only while there is
/// something to say (Leonardo, 2026-08-07); the FIELD's message arrives in the vendor's own
/// helper container, in normal flow under the field it names, and wraps to as many lines as it
/// needs (nsail#796). What is pinned here is what bUnit can see: the scope hook, the box the
/// message lands in, that the house adds nothing to the node the vendor hands over, and that a
/// field draws exactly one message. Whether the words are readable in full and whether the row
/// growing leaves its neighbours' tops alone is a box model — no assertion in a headless DOM
/// measures it, and NSail.Optical.E2E's FieldRefusalGeometryTests is where it is measured.</summary>
public sealed class NsReservedGeometryTests : BunitContext, IAsyncLifetime
{
    public NsReservedGeometryTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

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

    IRenderedComponent<ReservedGeometryHost> RenderHost(Problem? problem)
    {
        return Render<ReservedGeometryHost>(p => p
            .Add(x => x.Model, new ReservedGeometryModel())
            .Add(x => x.Problem, problem));
    }

    /// <summary>The hook the whole mechanism hangs off. Scoped rather than global on purpose: a
    /// field outside a form has no submit that can refuse it, so it is not given a message
    /// placement it will never use.</summary>
    [Fact]
    public void TheFormMarksItself_soTheMechanismCanBeScopedToFieldsInsideOne()
    {
        var cut = RenderHost(problem: null);

        Assert.Contains("ns-form", cut.Find("form").ClassList);
    }

    /// <summary>Measured, not assumed, and the reason a clean form spends nothing on the
    /// possibility of a refusal: MudBlazor renders no helper container at all while the field is
    /// clean, so there is no element to reserve a height on. The message arrives as a NEW node
    /// and takes its own height when it does — the cost is paid by the refusal, never by the
    /// forms nobody refused.</summary>
    [Fact]
    public async Task AFieldsMessageSlotDoesNotExistUntilThereIsAMessage()
    {
        var cut = RenderHost(Invalid(new Issue("Required", "Falta el nombre.", nameof(ReservedGeometryModel.Name))));

        Assert.Empty(cut.FindAll(".mud-input-control-helper-container"));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        var slot = Assert.Single(cut.FindAll(".mud-input-control-helper-container"));

        Assert.Contains("Falta el nombre.", slot.TextContent, StringComparison.Ordinal);

        // Inside the control box, which is the column the message takes its line at the foot of.
        Assert.NotNull(slot.ParentElement);
        Assert.Contains("mud-input-control", slot.ParentElement!.ClassList);
    }

    /// <summary>One control box per field, so the message lands at the foot of the whole field
    /// rather than in the middle of it: the vendor draws the helper container as the last item
    /// of the box it belongs to, and a field that rendered two nested ones would draw it inside
    /// the inner one, with the rest of the field below the refusal. Every field shape in the
    /// fixture — text, select, picker, and the chrome-only NsField — is one box.</summary>
    [Fact]
    public void EveryFieldShapeIsExactlyOneControlBox_soTheMessageLandsUnderTheWholeField()
    {
        var cut = RenderHost(problem: null);

        var controls = cut.FindAll(".mud-input-control");

        Assert.Equal(4, controls.Count);
        Assert.All(controls, control => Assert.Empty(control.QuerySelectorAll(".mud-input-control")));
    }

    /// <summary>The message is the vendor's node and the house writes nothing onto it — no
    /// class, no inline style, no wrapper of its own. That is what lets it wrap: everything a
    /// stylesheet could say here has been said by not saying it (ns-mud.css, the under-field
    /// zone), so a house class arriving on this node is a clamp coming back. The words sit in a
    /// child of the helper text and the text carries the vendor's own error colour class; a
    /// vendor upgrade that flattens either is what this catches.</summary>
    [Fact]
    public async Task TheMessageIsTheVendorsOwnNode_WornByNoHouseClassAndNoStyle()
    {
        var cut = RenderHost(Invalid(new Issue("Required", "Falta el nombre.", nameof(ReservedGeometryModel.Name))));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        var slot = cut.Find(".mud-input-control-helper-container");

        Assert.Null(slot.GetAttribute("style"));
        Assert.DoesNotContain(slot.ClassList, name => name.StartsWith("ns-", StringComparison.Ordinal));

        var text = cut.Find(".mud-input-control-helper-container .mud-input-helper-text");

        Assert.Contains("mud-input-error", text.ClassList);

        var words = Assert.Single(text.Children);

        Assert.Equal("Falta el nombre.", words.TextContent);
        Assert.Null(words.GetAttribute("style"));
        Assert.DoesNotContain(words.ClassList, name => name.StartsWith("ns-", StringComparison.Ordinal));
    }

    /// <summary>One message, and only one. A field handed several issues shows the first and
    /// nothing else (NsFieldBase.GetErrorText takes the first message the EditContext holds):
    /// the strip under a field answers one question at a time, and a field that listed every
    /// rule it broke would grow its row by the length of the list. What the message is allowed
    /// to spend is the lines its own sentence needs — never a second sentence.</summary>
    [Fact]
    public async Task AFieldHandedSeveralIssuesStillDrawsOneMessage()
    {
        var cut = RenderHost(Invalid(
            new Issue("Required", "Falta el nombre.", nameof(ReservedGeometryModel.Name)),
            new Issue("MaxLength", "El nombre es demasiado largo.", nameof(ReservedGeometryModel.Name))));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        var text = cut.Find(".mud-input-control-helper-container .mud-input-helper-text");

        Assert.Equal("Falta el nombre.", text.TextContent);
    }

    /// <summary>What a screen reader is actually handed, measured rather than assumed. The
    /// message the eye reads is drawn by a container nothing NAMES, and naming is the whole of
    /// what an assistive technology goes by. A house node carries the same words where no eye
    /// sees them and the input's `aria-describedby` names that node — so the reference resolves
    /// to an element that exists and holds the message's own text, which is the whole of what
    /// the input announcing itself invalid was missing (ruled nsail#216). The words being in
    /// the DOM twice is not a second REPORT: one node is read, the other is only ever spoken,
    /// and a node no eye sees is plumbing.</summary>
    [Fact]
    public async Task TheInputsDescribedByNamesANodeCarryingTheMessagesWords()
    {
        var cut = RenderHost(Invalid(new Issue("Required", "Falta el nombre.", nameof(ReservedGeometryModel.Name))));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        var control = cut.Find(".mud-input-control-helper-container").ParentElement;

        Assert.NotNull(control);

        var input = control!.QuerySelector("input");

        Assert.NotNull(input);
        Assert.Equal("true", input!.GetAttribute("aria-invalid"));

        var named = input.GetAttribute("aria-describedby");

        Assert.False(string.IsNullOrWhiteSpace(named));

        var described = cut.Find($"#{named}");

        Assert.Equal("Falta el nombre.", described.TextContent);
    }

    /// <summary>The node is hidden from the EYE and present to the accessibility tree, which is
    /// a narrower thing than "hidden": `display:none`, `visibility:hidden` and `aria-hidden`
    /// each take it out of the tree as well, and a described-by pointing at any of those
    /// announces nothing. The house class is what buys the distinction, so it is the class that
    /// is pinned here — no eye reads this node, which is why the words standing in the DOM twice
    /// is plumbing rather than a second placement.</summary>
    [Fact]
    public async Task TheDescribedNodeIsHiddenFromTheEyeAndNotFromTheTree()
    {
        var cut = RenderHost(Invalid(new Issue("Required", "Falta el nombre.", nameof(ReservedGeometryModel.Name))));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        var described = Assert.Single(cut.FindAll(".ns-field-described"));

        Assert.Equal("Falta el nombre.", described.TextContent);
        Assert.Null(described.GetAttribute("aria-hidden"));
        Assert.Null(described.GetAttribute("style"));

        // The hint's own block is a different node with a different job: a field with no Helper
        // still gets described, so the words can never be hung off a block that is not there.
        Assert.Empty(cut.FindAll(".ns-field-helper"));
    }

    /// <summary>Arrives with the refusal and leaves with it, both ends together. A reference
    /// outliving the node it names points at nothing, and a node outliving the reference is read
    /// by nobody — so the id is null while the field is clean and neither end can be left behind
    /// on its own.</summary>
    [Fact]
    public async Task TheDescriptionArrivesWithTheRefusalAndLeavesWithIt()
    {
        var cut = RenderHost(Invalid(new Issue("Required", "Falta el nombre.", nameof(ReservedGeometryModel.Name))));

        Assert.Null(cut.Find("input").GetAttribute("aria-describedby"));
        Assert.Empty(cut.FindAll(".ns-field-described"));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.NotNull(cut.Find("input").GetAttribute("aria-describedby"));

        cut.Render(p => p
            .Add(x => x.Model, new ReservedGeometryModel())
            .Add(x => x.Problem, (Problem?)null));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.Null(cut.Find("input").GetAttribute("aria-describedby"));
        Assert.Empty(cut.FindAll(".ns-field-described"));
    }

    /// <summary>Not just the plain text field: every shape carries the reference, each naming ITS
    /// OWN message and no neighbour's. A field describing the wrong refusal is worse than one
    /// describing none, and one shared id across a form would do exactly that. The picker is in
    /// this list — the shape that used to be unreachable is the point of the sweep.</summary>
    [Fact]
    public async Task EveryFieldShapeNamesItsOwnMessage()
    {
        var cut = RenderHost(Invalid(
            new Issue("Required", "Falta el nombre.", nameof(ReservedGeometryModel.Name)),
            new Issue("Required", "Falta la opción.", nameof(ReservedGeometryModel.Choice)),
            new Issue("Required", "Falta la fecha.", nameof(ReservedGeometryModel.Date))));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        var described = cut.FindAll("input")
            .Select(input => input.GetAttribute("aria-describedby"))
            .Where(named => !string.IsNullOrWhiteSpace(named))
            .Select(named => cut.Find($"#{named}").TextContent)
            .ToArray();

        Assert.Equal(["Falta el nombre.", "Falta la opción.", "Falta la fecha."], described);
    }

    /// <summary>The picker reaches its refusal through the same splat every other shape uses, and
    /// it has to be measured rather than read off the family: a picker's inner input is a MudInput
    /// the picker composes, not one this side hands parameters to, so the only proof the id
    /// travels that far is finding it there.</summary>
    [Fact]
    public async Task APickersInnerInputCarriesTheReferenceTheFieldSplatted()
    {
        var cut = RenderHost(Invalid(new Issue("Required", "Falta la fecha.", nameof(ReservedGeometryModel.Date))));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        var slot = Assert.Single(cut.FindAll(".mud-input-control-helper-container"));

        Assert.Contains("Falta la fecha.", slot.TextContent, StringComparison.Ordinal);

        var described = Assert.Single(cut.FindAll(".ns-field-described"));
        var input = Assert.Single(cut.FindAll("input[aria-describedby]"));

        Assert.Equal(described.GetAttribute("id"), input.GetAttribute("aria-describedby"));
        Assert.Equal("Falta la fecha.", described.TextContent);
    }

    /// <summary>Why the house has to render the node itself rather than let the vendor carry the
    /// whole thing. A splatted `aria-describedby` reaches the vendor's input and stands — that is
    /// the seam the reference travels on — but the words the vendor draws for an error live in an
    /// element it names with nothing, so the vendor alone offers a description and no way to point
    /// at it. The house supplies the element and the id; the vendor carries the id to the input.
    /// Rendered bare here on purpose: this pins the VENDOR's shape, not ours, and it is what tells
    /// us the day an upgrade changes it.</summary>
    [Fact]
    public void TheVendorNamesAnErrorsWordsWithNothing_andLeavesASplatStanding()
    {
        var error = Render<MudBlazor.MudTextField<string>>(p => p
            .Add(x => x.Error, true)
            .Add(x => x.ErrorText, "Falta el nombre.")
            .Add(x => x.UserAttributes, new Dictionary<string, object?>
            {
                ["aria-describedby"] = "splatted",
                ["data-probe"] = "arrived",
            }));

        Assert.Equal("arrived", error.Find("input").GetAttribute("data-probe"));
        Assert.Equal("splatted", error.Find("input").GetAttribute("aria-describedby"));

        // The words themselves stay unnamed, which is the half the house cannot delegate: nothing
        // in the vendor's own error container carries an id to be described by.
        Assert.Null(error.Find(".mud-input-helper-text > *").GetAttribute("id"));

        // And the same splat on a picker, which composes its inner input rather than being one:
        // the reference travels through both layers untouched.
        var picker = Render<MudBlazor.MudDatePicker>(p => p
            .Add(x => x.Error, true)
            .Add(x => x.ErrorText, "Falta la fecha.")
            .Add(x => x.UserAttributes, new Dictionary<string, object?>
            {
                ["aria-describedby"] = "splatted",
            }));

        Assert.Equal("splatted", picker.Find("input").GetAttribute("aria-describedby"));
    }

    /// <summary>A clean form reserves nothing: no foot line exists until a refusal fills it —
    /// the 2026-08-07 ruling ("consume mucho al pedo") that replaced the reserved line.</summary>
    [Fact]
    public void NoFootLineExistsWhileTheFormIsClean()
    {
        var cut = RenderHost(problem: null);

        Assert.Empty(cut.FindAll(".ns-form-problem"));
    }

    /// <summary>Refused, the fallback strip appears whole: the box, its colour and its alert
    /// role arrive together — an unclaimed form still answers visibly.</summary>
    [Fact]
    public async Task ARefusalDrawsTheFootLine_ColouredAndAnnounced()
    {
        var problem = Invalid(new Issue("Required", "Falta el apodo.", nameof(ReservedGeometryModel.Nickname)));

        var cut = RenderHost(problem);

        Assert.Empty(cut.FindAll(".ns-form-problem"));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        var foot = cut.Find(".ns-form-problem");

        Assert.Contains("Falta el apodo.", foot.TextContent, StringComparison.Ordinal);
        Assert.Contains("ns-form-problem-shown", foot.ClassList);
        Assert.Equal("alert", foot.GetAttribute("role"));
    }

    /// <summary>Cleared again on a submit the server accepted: the strip leaves entirely — a
    /// stale refusal hanging under a form the server already accepted is its own defect.</summary>
    [Fact]
    public async Task AnAcceptedSubmitRemovesTheFootLine()
    {
        var problem = Invalid(new Issue("Required", "Falta el apodo.", nameof(ReservedGeometryModel.Nickname)));

        var cut = RenderHost(problem);

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.Contains("ns-form-problem-shown", cut.Find(".ns-form-problem").ClassList);

        cut.Render(p => p
            .Add(x => x.Model, new ReservedGeometryModel())
            .Add(x => x.Problem, (Problem?)null));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.Empty(cut.FindAll(".ns-form-problem"));
    }
}
