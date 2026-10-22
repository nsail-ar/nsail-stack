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
using NSail.Testing.Models;

namespace NSail.Components.Tests;

/// <summary>nsail#2275: a [Validated] collection's issue names its element by index
/// ("Addresses[1].Street"), which `RefusalPlacement`'s field resolver reads by indexing into the
/// collection — the same resolver an un-indexed nested-model path already took, so a collection
/// earns no second placement rule. Whether that path has a field to draw under when the answer
/// arrives decides where it draws: a screen rendering a field per element has one, while a row
/// editor's open row has none by then — both row hosts settle the row they hold open before the
/// document is sent (nsail#2245), so the row the server answers about is already closed and the
/// refusal falls to the form's foot like any other unplaced issue.</summary>
public sealed class NsFormCollectionProblemDisplayTests : BunitContext, IAsyncLifetime
{
    public NsFormCollectionProblemDisplayTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
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

    static string FootAlert<TComponent>(IRenderedComponent<TComponent> cut) where TComponent : class, IComponent
    {
        var feet = cut.FindAll(".ns-form-problem, .ns-list-editor-problem");

        return feet.Count == 0 ? string.Empty : string.Join(' ', feet.Select(foot => foot.TextContent));
    }

    // No catalog is loaded in this fixture, so the button's label is the untranslated key — the
    // same fallback every other empty-catalog fixture in this file reads by.
    static Task Edit<TComponent>(IRenderedComponent<TComponent> cut, int index) where TComponent : class, IComponent
    {
        return cut.InvokeAsync(() => cut.FindAll("button[aria-label='Actions.Edit']")[index].Click());
    }

    [Fact]
    public async Task AnIssueNamingACollectionsElement_DrawsUnderThatElementsField()
    {
        var model = new CollectionProblemModel
        {
            Addresses = [new AddressRow { Street = "Siempreviva 742" }, new AddressRow { Street = "Falsa 123" }],
        };

        var problem = Invalid(new Issue("Required", "Falta la calle.", "Addresses[1].Street"));

        var cut = Render<CollectionFieldsHost>(p => p
            .Add(x => x.Model, model)
            .Add(x => x.Problem, problem));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        var fields = cut.FindComponents<MudTextField<string>>();

        Assert.True(fields[1].Instance.Error);
        Assert.Equal("Falta la calle.", fields[1].Instance.ErrorText);
        Assert.Equal(string.Empty, FootAlert(cut));
    }

    /// <summary>The index picks the element, not the value: two elements holding different text
    /// are still resolved by POSITION, so the second one's refusal never bleeds onto the
    /// first.</summary>
    [Fact]
    public async Task TheIndexPicksTheRow_NotWhicheverRowLooksTheSame()
    {
        var model = new CollectionProblemModel
        {
            Addresses = [new AddressRow { Street = "Siempreviva 742" }, new AddressRow { Street = "Falsa 123" }],
        };

        var problem = Invalid(new Issue("Required", "Falta la calle.", "Addresses[1].Street"));

        var cut = Render<CollectionFieldsHost>(p => p
            .Add(x => x.Model, model)
            .Add(x => x.Problem, problem));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        var fields = cut.FindComponents<MudTextField<string>>();

        Assert.False(fields[0].Instance.Error);
    }

    /// <summary>The table's open row, which by the time an answer arrives is not open: the
    /// submit settles it first (nsail#2245) — the row's own rule commits it and the editor
    /// closes — so the server's issue about that row names no field on screen and lands at the
    /// form's foot, exactly like a row nobody ever opened.</summary>
    [Fact]
    public async Task AnOpenTableRowIsSettledBeforeTheSend_SoTheServersAnswerLandsAtTheFoot()
    {
        var model = new CollectionProblemModel
        {
            Addresses = [new AddressRow { Street = "Siempreviva 742" }, new AddressRow { Street = "Falsa 123" }],
        };

        var problem = Invalid(new Issue("Required", "Falta la calle.", "Addresses[1].Street"));

        var cut = Render<TableProblemHost>(p => p
            .Add(x => x.Model, model)
            .Add(x => x.Problem, problem));

        await Edit(cut, 1);

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        // Nothing refuses the row, so the settle closed it and the save went out in the same
        // press: no editor left on screen, and the answer where an unplaced issue goes.
        Assert.Empty(cut.FindComponents<MudTextField<string>>());
        Assert.Contains("Falta la calle.", FootAlert(cut), StringComparison.Ordinal);
    }

    /// <summary>A row the screen never opened renders no field to claim the path — exactly the
    /// case an un-indexed, unbound member already falls to the foot for.</summary>
    [Fact]
    public async Task AnIssueNamingARowNoFieldRenders_DrawsAtTheFormsFoot()
    {
        var model = new CollectionProblemModel
        {
            Addresses = [new AddressRow { Street = "Siempreviva 742" }, new AddressRow { Street = "Falsa 123" }],
        };

        var problem = Invalid(new Issue("Required", "Falta la calle.", "Addresses[1].Street"));

        var cut = Render<TableProblemHost>(p => p
            .Add(x => x.Model, model)
            .Add(x => x.Problem, problem));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.Contains("Falta la calle.", FootAlert(cut), StringComparison.Ordinal);
    }

    /// <summary>The client half: an NsListEditor row's own [Required] Street is asked the
    /// moment the DOCUMENT is submitted — Settle() runs before anything is sent — rather than
    /// left to a round trip, so the row stays open, draws its reason, and the send never
    /// happens: the same contract a refused OnCommit already carries, now extended to a row a
    /// plain declaration refuses.</summary>
    [Fact]
    public async Task AnNsListEditorRowsOwnRequiredStreet_RefusesTheSubmitWithNoSend()
    {
        var model = new CollectionProblemModel { Addresses = [new AddressRow { Street = "Siempreviva 742" }] };

        var cut = Render<ListEditorProblemHost>(p => p
            .Add(x => x.Model, model)
            .Add(x => x.Problem, (Problem?)null));

        await Edit(cut, 0);

        await cut.InvokeAsync(() => cut.Find("input").Input(string.Empty));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        // Still open: the row's own field, carrying its own reason, and no send went out.
        var field = Assert.Single(cut.FindComponents<MudTextField<string>>());

        Assert.True(field.Instance.Error);
        Assert.Equal(0, cut.Instance.Submits);
    }

    /// <summary>A row no member opened with [Validated] takes none of this — the collection
    /// member's own opt-in, same as a single nested model.</summary>
    [Fact]
    public async Task ARowOfAPlainCollection_IsNotRefusedByItsOwnDeclaredMember()
    {
        var model = new LooseCollectionHost.Model
        {
            Addresses = [new AddressRow { Street = "Siempreviva 742" }],
        };

        var cut = Render<LooseCollectionHost>(p => p.Add(x => x.TheModel, model));

        await Edit(cut, 0);

        await cut.InvokeAsync(() => cut.Find("input").Input(string.Empty));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        // Closed, and the save went through: nothing here is anybody's rule to enforce.
        Assert.Empty(cut.FindComponents<MudTextField<string>>());
        Assert.Equal(1, cut.Instance.Submits);
    }
}
