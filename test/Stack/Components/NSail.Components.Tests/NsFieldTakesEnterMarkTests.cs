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

/// <summary>A text input inside a form with a submit button saves the whole document on Enter —
/// the browser's implicit submission — and that is the SAME keystroke a box with an
/// <c>OnCommit</c> answers: a counter passing ten boxes over a barcode reader closed the sale on
/// the first one. So a box that answers Enter itself stamps <c>data-ns-commits</c> on its input
/// and the keyboard layer takes the default away for it (ns.js, onDocumentKeyDown — the same
/// listener that answers an open combobox's Enter).
///
/// The mark is DERIVED and never a parameter: it is on wherever something actually hangs on the
/// commit, so a plain field in a one-field form still submits on Enter the way it always did.
/// <para>This is the markup half, the same split NsComboboxExpandedMarkTests keeps: the guard
/// cannot be proved at this layer — bUnit renders through AngleSharp, which performs no implicit
/// form submission — so what is pinned here is the attribute the keystroke-time read keys on, and
/// the submission itself is held where a browser runs a form with a committing box in it.</para>
/// </summary>
public sealed class NsFieldTakesEnterMarkTests : BunitContext, IAsyncLifetime
{
    public NsFieldTakesEnterMarkTests()
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

    [Fact]
    public void ABoxWithSomethingHangingOnItsCommit_TakesTheEnter()
    {
        var cut = Render<NsTextField>(ps => ps
            .Add(p => p.Value, "7791234567890")
            .Add(p => p.OnCommit, EventCallback.Factory.Create<string?>(this, _ => { })));

        Assert.Equal("true", cut.Find("input").GetAttribute("data-ns-commits"));
    }

    /// <summary>The whole reason the mark is derived: a field that only binds has no second
    /// meaning for the key to collide with, and taking Enter from those forms would cost every
    /// one-field screen its own habit.</summary>
    [Fact]
    public void ABoxThatOnlyBinds_LeavesTheEnterToTheForm()
    {
        var cut = Render<NsTextField>(ps => ps.Add(p => p.Value, "hello"));

        Assert.Null(cut.Find("input").GetAttribute("data-ns-commits"));
    }

    /// <summary>Enter is text in a textarea (<c>CommitsOnEnter</c>), so it never ends the entry
    /// there and there is nothing to take: the key writes a newline and the form is not in it.</summary>
    [Fact]
    public void ATextArea_LeavesTheEnterAlone_EvenWithACommitHandler()
    {
        var cut = Render<NsTextArea>(ps => ps
            .Add(p => p.Value, "a note")
            .Add(p => p.OnCommit, EventCallback.Factory.Create<string?>(this, _ => { })));

        Assert.Null(cut.Find("textarea").GetAttribute("data-ns-commits"));
    }

    /// <summary>The whole typed family carries it, not NsTextField alone: a money cell that
    /// saves on Enter is the same collision as a chip list's entry box.</summary>
    [Fact]
    public void AMoneyFieldWithACommitHandler_TakesTheEnter()
    {
        var cut = Render<NsMoneyField<decimal?>>(ps => ps
            .Add(p => p.Value, 0m)
            .Add(p => p.OnCommit, EventCallback.Factory.Create<decimal?>(this, _ => { })));

        Assert.Equal("true", cut.Find("input").GetAttribute("data-ns-commits"));
    }
}
