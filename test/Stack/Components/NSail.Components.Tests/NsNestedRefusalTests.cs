// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;
using NSail.Problems;

namespace NSail.Components.Tests;

/// <summary>How far a form's own validator reaches. Both ends walk a message's own members and
/// nothing under them, so a bound declared on a nested model was drawn by neither — which is
/// why a field marked or bounded from one would have been promising what nothing enforced
/// (nsail#797, nsail#818). A member that says [Validated] opens its model to the same walk the
/// wire takes (MessageValidator), and only that member: the model beside it, held by a plain
/// member, keeps the row-model scope and is refused by nobody.</summary>
public sealed class NsNestedRefusalTests : BunitContext, IAsyncLifetime
{
    public NsNestedRefusalTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            ["Problems.Bounded"] = "Va de {from} a {to}",
            ["Problems.Required"] = "Obligatorio"
        })]));
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

    IRenderedComponent<NestedRefusalHost> RenderHost(NestedRefusalModel model, Action? submitted = null, Problem? problem = null)
    {
        return Render<NestedRefusalHost>(p => p
            .Add(x => x.Model, model)
            .Add(x => x.Problem, problem)
            .Add(x => x.Submitted, () => submitted?.Invoke()));
    }

    static NestedRefusalModel Valid()
    {
        var model = new NestedRefusalModel();

        model.Opened.Note = "ok";
        model.Loose.Note = "ok";

        return model;
    }

    // What MessageValidator puts on the wire beside the code, so the catalog's tokens are
    // filled by the same bound that refused.
    static readonly Dictionary<string, string> Bound = new() { ["from"] = "-30.00", ["to"] = "30.00" };

    static string FootAlert(IRenderedComponent<NestedRefusalHost> cut)
    {
        var feet = cut.FindAll(".ns-form-problem");

        return feet.Count == 0 ? string.Empty : feet[0].TextContent;
    }

    static string? Refusal(IRenderedComponent<NestedRefusalHost> cut, string label)
    {
        return Control(cut, label).QuerySelector(".mud-input-helper-text")?.TextContent;
    }

    static bool IsMarked(IRenderedComponent<NestedRefusalHost> cut, string label)
    {
        return Control(cut, label).ClassList.Contains("mud-input-required");
    }

    // The element is found and changed inside one dispatch: a handle taken before the previous
    // render is a handler id the tree no longer has.
    static Task Type(IRenderedComponent<NestedRefusalHost> cut, string label, string text)
    {
        return cut.InvokeAsync(() => Control(cut, label)
            .QuerySelector("input")!
            .Change(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = text }));
    }

    static AngleSharp.Dom.IElement Control(IRenderedComponent<NestedRefusalHost> cut, string label)
    {
        return cut.FindAll(".mud-input-control")
            .Single(node => node.QuerySelector("label")?.TextContent == label);
    }

    /// <summary>The submit reaches the opened model, words it from the catalog by the
    /// attribute's own code, and fills the bound the attribute refuses by.</summary>
    [Fact]
    public async Task AnOpenedModelIsRefusedUnderItsOwnField_InTheCatalogsWords()
    {
        var model = new NestedRefusalModel();
        var submitted = false;

        model.Opened.Measure = 45m;
        model.Opened.Note = "ok";
        model.Loose.Note = "ok";

        var cut = RenderHost(model, () => submitted = true);

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.Equal("Va de -30.00 a 30.00", Refusal(cut, "Opened"));
        Assert.False(submitted);
    }

    /// <summary>The scope that was NOT widened: a row model held by a plain member is enforced
    /// by neither end, so nothing is drawn under its field and the submit goes through.</summary>
    [Fact]
    public async Task AModelNothingOpened_RefusesNothingAndStopsNothing()
    {
        var model = new NestedRefusalModel();
        var submitted = false;

        model.Opened.Note = "ok";
        model.Loose.Measure = 45m;
        model.Loose.Note = null;

        var cut = RenderHost(model, () => submitted = true);

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.Null(Refusal(cut, "Loose"));
        Assert.Null(Refusal(cut, "Loose note"));
        Assert.True(submitted);
    }

    /// <summary>The field speaks first: typing the value is enough, with no submit — the
    /// per-field pass takes the same reach the whole-model one does.</summary>
    [Fact]
    public async Task AnOpenedModelsFieldRefusesOnTheChange()
    {
        var cut = RenderHost(Valid());

        await Type(cut, "Opened", "45");

        Assert.Equal("Va de -30.00 a 30.00", Refusal(cut, "Opened"));

        await Type(cut, "Opened", "-2.25");

        Assert.Null(Refusal(cut, "Opened"));
    }

    /// <summary>The mark reads exactly what refuses, on both sides of the scope: an opened
    /// model's [Required] is marked because the submit will refuse it, and the same declaration
    /// on the model beside it is not, because nothing will.</summary>
    [Fact]
    public void TheMarkFollowsTheSameReachAsTheRefusal()
    {
        var cut = RenderHost(new NestedRefusalModel());

        Assert.True(IsMarked(cut, "Opened note"));
        Assert.False(IsMarked(cut, "Loose note"));
    }

    /// <summary>The other end of the same walk: a server refusal names the field by its path
    /// from the message, since that is the only name that tells two nested models apart — and
    /// the form resolves it to the very input the nested field registered.</summary>
    [Fact]
    public async Task AServerRefusalNamingAPath_DrawsUnderTheFieldThatPathLeadsTo()
    {
        var problem = new Problem(
            "InvalidModel",
            "One or more fields contain invalid data",
            [new Issue("Bounded", "out of range", "Opened.Measure", Bound)],
            400);

        var cut = RenderHost(Valid(), problem: problem);

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.Equal("Va de -30.00 a 30.00", Refusal(cut, "Opened"));
        Assert.Equal(string.Empty, FootAlert(cut));
    }

    /// <summary>And the fallback still holds under a path: a refusal naming a model no field
    /// rendered has nothing to stand under, so it stands at form level rather than vanishing.</summary>
    [Fact]
    public async Task AServerRefusalNamingAPathNoFieldRenders_DrawsAtFormLevel()
    {
        var problem = new Problem(
            "InvalidModel",
            "One or more fields contain invalid data",
            [new Issue("Bounded", "out of range", "Opened.Absent", Bound)],
            400);

        var cut = RenderHost(Valid(), problem: problem);

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.Contains("Va de -30.00 a 30.00", FootAlert(cut), StringComparison.Ordinal);
    }

    /// <summary>Opening a member takes nothing away from the message's own members.</summary>
    [Fact]
    public async Task TheMessagesOwnMembersAreStillRefused()
    {
        var model = new NestedRefusalModel { Own = -45m };
        var submitted = false;

        model.Opened.Note = "ok";
        model.Loose.Note = "ok";

        var cut = RenderHost(model, () => submitted = true);

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.Equal("Va de -30.00 a 30.00", Refusal(cut, "Own"));
        Assert.False(submitted);
    }
}
