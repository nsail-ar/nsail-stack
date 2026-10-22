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

/// <summary>What a document's submit does with a grid row left open (nsail#2245). The row's
/// values are already the document's — every NewRow factory appends before returning — so the
/// save was going to write that row either way, and the row's own rule is what answers for it:
/// the submit settles the table before it validates (IFormRows), which commits a row nothing
/// refuses, keeps a refused one open wearing its own words, and takes back out a new row the
/// screen says nobody typed into. The same contract NsListEditor already carries, now the
/// table's.</summary>
public sealed class NsTableSettleTests : BunitContext, IAsyncLifetime
{
    public NsTableSettleTests()
    {
        Services.AddMudServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            ["Actions.Add"] = "Agregar",
            ["Actions.Edit"] = "Editar",
            ["Actions.Confirm"] = "Confirmar",
            ["Actions.Cancel"] = "Cancelar",
            ["Problems.Required"] = "Obligatorio",
        })]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // MudPopoverProvider resolves a MudBlazor service that is internal and IAsyncDisposable
    // only, so bUnit's synchronous teardown cannot dispose it (NsSelectTests' own note).
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    static Task Press(IRenderedComponent<TableFormHost> cut, string name)
    {
        return cut.InvokeAsync(() => cut.FindAll($"button[aria-label='{name}']")[0].Click());
    }

    // The row's own box, typed into and never left: the keystroke is what writes the binding, so
    // what the box holds is already the row's value when the submit is raised (ui/fields.md).
    static Task Type(IRenderedComponent<TableFormHost> cut, string text)
    {
        return cut.InvokeAsync(() => cut.Find("tr.mud-table-row input").Input(text));
    }

    static Task Submit(IRenderedComponent<TableFormHost> cut)
    {
        return cut.InvokeAsync(() => cut.Find("form").Submit());
    }

    static string? RowError(IRenderedComponent<TableFormHost> cut)
    {
        var fields = cut.FindComponents<MudTextField<string>>();

        if (fields.Count == 0)
        {
            return null;
        }

        return fields[0].Instance.Error ? fields[0].Instance.ErrorText : null;
    }

    /// <summary>The row's refusal is posted on the FORM's context, so the document's own submit
    /// lifts it — and the submit is what has to re-answer it, because nobody re-raises the
    /// page's OnRowCommit. So the submit settles the open row first: the row's own rule speaks
    /// again on the values on screen, and the refusal is drawn under the field it names rather
    /// than the bad row reaching the server and its answer landing at the form's foot.</summary>
    [Fact]
    public async Task ADocumentSubmitAsksAnOpenRowsRuleAgainAndIsRefusedByItsOwnWords()
    {
        var cut = Render<TableFormHost>(ps => ps.Add(p => p.Refuse, true));

        await Press(cut, "Agregar");
        await Type(cut, "Armazón");
        await Press(cut, "Confirmar");

        Assert.Equal("Obligatorio", RowError(cut));

        await Submit(cut);

        Assert.Equal(0, cut.Instance.Submits);
        Assert.Equal("Obligatorio", RowError(cut));

        // Under the field, and nowhere else: the row's answer is not a strip at the foot of the
        // form, which is the placement the server's own refusal used to land in.
        Assert.Empty(cut.FindAll(".ns-form-problem"));
    }

    /// <summary>A refusal naming no member the row renders — a rule naming itself — is the
    /// row's all the same: one strip on a row of its own directly under the row that caused it,
    /// never the form's foot and never a toast.</summary>
    [Fact]
    public async Task ARefusalNamingNoFieldDrawsOnTheRowsOwnStrip()
    {
        var cut = Render<TableFormHost>(ps => ps.Add(p => p.RefuseByRule, true));

        await Press(cut, "Agregar");
        await Type(cut, "Armazón");
        await Submit(cut);

        var strip = Assert.Single(cut.FindAll("tr.ns-table-problem"));

        Assert.Contains("La caja del turno ya fue cerrada.", strip.TextContent, StringComparison.Ordinal);
        Assert.Equal(0, cut.Instance.Submits);

        // Still open, and the strip goes with the row: the next answer is the one on screen.
        Assert.Single(cut.FindAll("button[aria-label='Confirmar']"));
    }

    /// <summary>The other half of the ruling: Guardar commits an open row nothing refuses — the
    /// editor closes and the document saves in one tap. The row is in the collection before any
    /// Confirmar, so the save was writing it either way; what the handler is asked about is the
    /// value on screen, not a verdict from an earlier press.</summary>
    [Fact]
    public async Task ADocumentSubmitCommitsAnOpenRowNothingRefuses()
    {
        var cut = Render<TableFormHost>();

        await Press(cut, "Agregar");
        await Type(cut, "Armazón");
        await Submit(cut);

        Assert.Equal("Armazón", cut.Instance.Committed);
        Assert.Equal(1, cut.Instance.Submits);
        Assert.Equal(1, cut.Instance.Rows);

        // The editor is gone: no commit pair on screen, and the row reads as its own cell again.
        Assert.Empty(cut.FindAll("button[aria-label='Confirmar']"));
    }

    /// <summary>The counter sale's keyboard sequence, which is what the story is about: the last
    /// Enter adds a row nobody types into, and the chord that follows saves the lines that WERE
    /// typed and nothing else — the submit takes that row back out through the same door Cancelar
    /// uses, and the save goes through in the same press.</summary>
    [Fact]
    public async Task ANewRowNobodyTypedIntoIsTakenBackOutAndTheSaveGoesThrough()
    {
        var cut = Render<TableFormHost>();

        await Press(cut, "Agregar");
        await Type(cut, "Armazón");
        await Press(cut, "Confirmar");

        await Press(cut, "Agregar");
        await Submit(cut);

        Assert.Equal(1, cut.Instance.Submits);
        Assert.Equal(1, cut.Instance.Rows);

        // Never handed to the handler: a row nobody typed into is not a row the screen's own
        // rule has to refuse, it is a row that was never part of the document.
        Assert.Equal(1, cut.Instance.Commits);
        Assert.Empty(cut.FindAll("button[aria-label='Confirmar']"));
    }

    /// <summary>A screen that answers nothing keeps the Stack's default — the twelve other
    /// editable grids in the house: the open row is committed or refused like any other, so no
    /// grid quietly sends a row its own rule would have thrown back.</summary>
    [Fact]
    public async Task AGridThatAnswersNothingStillCommitsOrRefusesItsNewRow()
    {
        var cut = Render<TableFormHost>(ps => ps
            .Add(p => p.AnswersEmpty, false)
            .Add(p => p.Refuse, true));

        await Press(cut, "Agregar");
        await Submit(cut);

        Assert.Equal(0, cut.Instance.Submits);
        Assert.Equal(1, cut.Instance.Commits);
        Assert.Equal("Obligatorio", RowError(cut));
    }

    /// <summary>A new row somebody DID type into is never dropped in silence, whatever the
    /// screen's own answer about an empty one: it is committed, or refused by name on its own
    /// row.</summary>
    [Fact]
    public async Task ANewRowSomebodyTypedIntoIsCommittedAndNotDropped()
    {
        var cut = Render<TableFormHost>();

        await Press(cut, "Agregar");
        await Type(cut, "Cristal");
        await Submit(cut);

        Assert.Equal(1, cut.Instance.Submits);
        Assert.Equal(1, cut.Instance.Rows);
        Assert.Equal("Cristal", cut.Instance.Committed);
    }

    /// <summary>The row whose own field declares itself required, which is the regime where the
    /// field re-posts a "no" of its own on every validation pass: the submit still either runs
    /// or draws its reason where the person is looking — the settle's, under the same field —
    /// and never leaves the form refused with nothing on screen to explain it. Mute is the one
    /// answer it cannot give.</summary>
    [Fact]
    public async Task ARowStillRefusedByItsOwnFieldRefusesTheSubmitOutLoud()
    {
        var cut = Render<TableFormHost>(ps => ps
            .Add(p => p.Refuse, true)
            .Add(p => p.RowFieldRequired, true));

        await Press(cut, "Agregar");
        await Type(cut, "Armazón");
        await Press(cut, "Confirmar");

        await Submit(cut);

        Assert.Equal(0, cut.Instance.Submits);
        Assert.Equal("Obligatorio", RowError(cut));
    }

    /// <summary>A store's messages outlive the component that posted them: a grid that leaves
    /// the screen with a row refused left the form holding a validation failure with no input on
    /// screen to show it and nobody able to lift it — the document could never be saved again,
    /// with nothing explaining why. The table's own end is where it lets go.</summary>
    [Fact]
    public async Task ATableUnmountedWithARowRefusedLeavesNothingOnTheFormsContext()
    {
        var cut = Render<TableFormHost>(ps => ps.Add(p => p.Refuse, true));

        await Press(cut, "Agregar");
        await Type(cut, "Armazón");
        await Press(cut, "Confirmar");

        Assert.Equal("Obligatorio", RowError(cut));

        cut.Render(ps => ps.Add(p => p.TableMounted, false));

        await Submit(cut);

        Assert.Equal(1, cut.Instance.Submits);
    }
}
