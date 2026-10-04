// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>The table's own words for having nothing (nsail#800): a register a screen opened
/// on an install that has never filled it says so in that screen's language, and "No hay
/// registros para mostrar" stays the answer for every table that asks for nothing else. The
/// slot rides the table rather than the page because the table is what knows a read has
/// answered — an empty state drawn while the first page is in flight is a claim about data
/// nobody has yet (intentional-ui.md, "a read that failed is not a read that came back
/// empty").
/// <para>The slot sits inside the vendor's own NoRecordsContent, so "it stays away when rows
/// arrived" is the same condition the generic sentence has always answered to and is not
/// restated here — MudBlazor's ServerData branch never lands a row under bUnit anyway (the
/// host re-renders after the query returns, and the branch reads its page back before
/// that).</para></summary>
public sealed class NsTableEmptyTests : BunitContext, IAsyncLifetime
{
    const string EmptyMarkup = "<p id=\"no-medicos\">Todavía no hay ningún médico registrado.</p>";

    const string Generic = "No hay registros para mostrar";

    public NsTableEmptyTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            ["Common.NoRecords"] = Generic,
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

    [Fact]
    public void ATableWithNoEmptySlotKeepsTheGenericSentence()
    {
        var cut = Render<EmptyTableHost>();

        cut.WaitForAssertion(() => Assert.Contains(Generic, cut.Markup));
    }

    [Fact]
    public void TheEmptySlotReplacesTheGenericSentenceWhenTheReadCameBackWithNothing()
    {
        var cut = Render<EmptyTableHost>(parameters => parameters.Add(host => host.Empty, Fragment()));

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#no-medicos")));

        Assert.DoesNotContain(Generic, cut.Markup);
    }

    // The half the slot exists to keep honest: the read is held open, so the table has no
    // answer about the data yet and may not print one — neither the caller's sentence nor the
    // generic one.
    [Fact]
    public void NeitherSentenceIsPrintedBeforeTheReadAnswered()
    {
        var gate = new TaskCompletionSource();

        var cut = Render<EmptyTableHost>(parameters => parameters
            .Add(host => host.Gate, gate)
            .Add(host => host.Empty, Fragment()));

        Assert.Empty(cut.FindAll("#no-medicos"));
        Assert.DoesNotContain(Generic, cut.Markup);

        // Released so the held read finishes against a live renderer rather than a disposed
        // one — and the sentence the table was withholding arrives the moment it has an
        // answer to say it about.
        gate.SetResult();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#no-medicos")));
    }

    static RenderFragment Fragment()
    {
        return builder => builder.AddMarkupContent(0, EmptyMarkup);
    }
}
