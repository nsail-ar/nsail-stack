// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>The other half of nsail#1937. Every typed box commits on the keystroke, which is
/// what a BINDING wants and what a handler that takes or saves the value does not: a price
/// typed as 2350 over a stored 1500 sent four saves, writing 2, 23 and 235 before it, and a
/// chip list took one address per letter. `OnCommit` is the seam those hang on instead — the
/// person finished, which is Enter or the leave — and these cases are what says the two events
/// are not the same one.</summary>
public sealed class TypedValueCommitTests : BunitContext, IAsyncLifetime
{
    public TypedValueCommitTests()
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

    /// <summary>The whole point of the seam: thirteen keystrokes are not thirteen entries. A
    /// barcode typed by hand sent one save per digit while the handler hung on ValueChanged.</summary>
    [Fact]
    public async Task TypingKeystrokeByKeystroke_CommitsNothingUntilTheEntryEnds()
    {
        var cut = Render<CommittedValueHost>(p => p.Add(x => x.Model, new TypedValueModel()));

        foreach (var typed in Typing.Prefixes("7791234567890"))
        {
            await cut.InvokeAsync(() => Box<NsTextField>(cut).Input(typed));
        }

        Assert.Empty(cut.Instance.Committed);

        await cut.InvokeAsync(() => Box<NsTextField>(cut).Blur());

        Assert.Equal([(nameof(NsTextField), "7791234567890")], cut.Instance.Committed);
    }

    /// <summary>Both halves of the gesture, on every box in the family at once — the same
    /// reason TypedValueSubmitTests names them all: a field added later is named here by the
    /// entry that never arrived rather than by a walker on one screen.</summary>
    [Fact]
    public async Task LeavingAnyTypedField_CommitsWhatTheBoxHolds()
    {
        var cut = Render<CommittedValueHost>(p => p.Add(x => x.Model, new TypedValueModel()));

        await TypeAndLeave<NsTextField>(cut, "Feriado");
        await TypeAndLeave<NsTextArea>(cut, "Una nota");
        await TypeAndLeave<NsEmailField>(cut, "alguien@acme.example");
        await TypeAndLeave<NsPhoneField>(cut, "1122334455");
        await TypeAndLeave<NsUrlField>(cut, "https://acme.example");
        await TypeAndLeave<NsPasswordField>(cut, "secreto");
        await TypeAndLeave<NsSearchField>(cut, "buscado");
        await TypeAndLeave<NsNumericField<decimal?>>(cut, "7");
        await TypeAndLeave<NsMoneyField<decimal?>>(cut, "1500");
        await TypeAndLeave<NsPercentField<decimal?>>(cut, "21");
        await TypeAndLeave<NsDurationField<TimeSpan?>>(cut, "3");

        Assert.Equal(
        [
            (nameof(NsTextField), (object?)"Feriado"),
            (nameof(NsTextArea), "Una nota"),
            (nameof(NsEmailField), "alguien@acme.example"),
            (nameof(NsPhoneField), "1122334455"),
            (nameof(NsUrlField), "https://acme.example"),
            (nameof(NsPasswordField), "secreto"),
            (nameof(NsSearchField), "buscado"),
            (nameof(NsNumericField<decimal?>), 7m),
            (nameof(NsMoneyField<decimal?>), 1500m),
            (nameof(NsPercentField<decimal?>), 21m),
            (nameof(NsDurationField<TimeSpan?>), TimeSpan.FromHours(3)),
        ], cut.Instance.Committed);
    }

    /// <summary>A masked box is a different vendor control underneath, not a text box wearing a
    /// pattern: the seam has to reach it too, or the first caller that masks a field — a DNI, a
    /// CUIT — finds the commit silently missing. The value committed is the RAW characters, the
    /// same thing the binding holds (ui/fields.md, Mask).</summary>
    [Fact]
    public async Task AMaskedBox_CommitsTheRawValueWhenItIsLeft()
    {
        var cut = Render<CommittedValueHost>(p => p.Add(x => x.Model, new TypedValueModel()));

        var box = cut.FindComponents<NsTextField>()[1].Find("input");

        await cut.InvokeAsync(() => box.Input("30123456"));

        Assert.Empty(cut.Instance.Committed);

        await cut.InvokeAsync(() => box.Blur());

        Assert.Equal([(CommittedValueHost.MaskedField, (object?)"30123456")], cut.Instance.Committed);
    }

    /// <summary>Enter is the other end of the entry — a price typed into a cell and confirmed
    /// without the hand leaving the keyboard — and the box stays where it is.</summary>
    [Fact]
    public async Task PressingEnter_CommitsWithoutLeavingTheBox()
    {
        var cut = Render<CommittedValueHost>(p => p.Add(x => x.Model, new TypedValueModel()));

        await cut.InvokeAsync(() => Box<NsMoneyField<decimal?>>(cut).Input("2350"));
        await cut.InvokeAsync(() => Box<NsMoneyField<decimal?>>(cut).KeyDown(Key.Enter));

        Assert.Equal([(nameof(NsMoneyField<decimal?>), (object?)2350m)], cut.Instance.Committed);
    }

    /// <summary>And the one box Enter is not an ending in: it is a newline in a field that has
    /// lines, so a note typed over several of them would commit on each.</summary>
    [Fact]
    public async Task PressingEnterInATextArea_IsANewlineAndNotACommit()
    {
        var cut = Render<CommittedValueHost>(p => p.Add(x => x.Model, new TypedValueModel()));

        await cut.InvokeAsync(() => Box<NsTextArea>(cut).Input("Primera línea"));
        await cut.InvokeAsync(() => Box<NsTextArea>(cut).KeyDown(Key.Enter));

        Assert.Empty(cut.Instance.Committed);

        await cut.InvokeAsync(() => Box<NsTextArea>(cut).Blur());

        Assert.Equal([(nameof(NsTextArea), (object?)"Primera línea")], cut.Instance.Committed);
    }

    /// <summary>The text typed belongs to the entry it was typed in and to nothing after it.
    /// The vendor text boxes, unlike the pickers, say nothing when a value is pushed INTO them,
    /// so a bound member the screen moves itself reaches the box without the field hearing it:
    /// the box draws the new value while the field would still be holding the old text, and the
    /// next ending would commit it — a value nobody typed, written over one somebody did.</summary>
    [Fact]
    public async Task AnEntryAlreadyEnded_IsNotWhatTheNextOneCommits()
    {
        var cut = Render<CommittedValueHost>(p => p.Add(x => x.Model, new TypedValueModel()));

        await cut.InvokeAsync(() => Box<NsTextField>(cut).Input("Feriado"));
        await cut.InvokeAsync(() => Box<NsTextField>(cut).Blur());

        await cut.Instance.Push("Carnaval");

        await cut.InvokeAsync(() => Box<NsTextField>(cut).Blur());

        Assert.Equal(
        [
            (nameof(NsTextField), (object?)"Feriado"),
            (nameof(NsTextField), "Carnaval"),
        ], cut.Instance.Committed);
    }

    /// <summary>A box nobody hung a handler on is untouched by the seam: the leave and the
    /// Enter are the vendor's own business there, which is what keeps the two hundred fields
    /// the house already draws out of this story.</summary>
    [Fact]
    public async Task AFieldWithNoHandler_IsUnmovedByTheLeaveAndTheEnter()
    {
        var model = new TypedValueModel();

        var cut = Render<TypedValueHost>(p => p
            .Add(x => x.Model, model)
            .Add(x => x.Submitted, _ => { }));

        var box = cut.FindComponent<NsTextField>().Find("input");

        await cut.InvokeAsync(() => box.Input("Feriado"));
        await cut.InvokeAsync(() => box.KeyDown(Key.Enter));
        await cut.InvokeAsync(() => box.Blur());

        Assert.Equal("Feriado", model.Name);
    }

    static async Task TypeAndLeave<TField>(IRenderedComponent<CommittedValueHost> cut, string text)
        where TField : IComponent
    {
        await cut.InvokeAsync(() => Box<TField>(cut).Input(text));
        await cut.InvokeAsync(() => Box<TField>(cut).Blur());
    }

    static AngleSharp.Dom.IElement Box<TField>(IRenderedComponent<CommittedValueHost> cut) where TField : IComponent
    {
        return cut.FindComponent<TField>().Find("input, textarea");
    }
}
