// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>A ? beside a labelled field in a top-aligned row sits on the field's INPUT, not on
/// its label gutter (ui/styling.md, the help seat). bUnit lays out no box model, so what is pinned
/// is the stylesheet's own rule — the field's geometry it declares — and its selectors run against
/// rendered rows: they reach the ? in a top-aligned row — past the field's hint or refusal, and
/// through a wrapper — and miss it in a centred row, a column, beside a boolean control and as a
/// row's second ?. The centre on the input is measured in a browser by the products'
/// walks.</summary>
public sealed class HelpSeatTests : BunitContext, IAsyncLifetime
{
    public HelpSeatTests()
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

    // The field's geometry: the vendor's 8px outlined-with-label gutter above, and the 40px input
    // box the glyph is centred in.
    [Fact]
    public void TheSeatTakesTheFieldsGutterAndInputBox()
    {
        var block = SeatBlock();

        Assert.Contains("margin-top: 0.5rem;", block);
        Assert.Contains("height: 2.5rem;", block);
    }

    // The seat's selectors reach a field's root as a DIRECT child of the row (or of a wrapper the
    // row parents), so they hold only while the vendor's root — not the .mud-input a level or two
    // in — wears the labelled class. This pins that structural assumption.
    [Fact]
    public void TheLabelledClassRidesTheFieldsRootTheRowParents()
    {
        var cut = Render<HelpSeatHost>();

        var row = cut.Find(".top-row");

        Assert.Contains(row.Children, child =>
            child.ClassList.Contains("mud-input-control") &&
            child.ClassList.Contains("mud-input-outlined-with-label"));
    }

    // The field's under-field node is a sibling in the row, between the field and the ?: the hint
    // while clean, the refusal's described twin while refused. The seat reaches past either.
    [Fact]
    public void TheFieldsHintOrRefusalStandsBetweenItAndTheHelp()
    {
        var cut = Render<HelpSeatHost>();

        Assert.Contains("ns-field-helper", Before(cut, ".hinted-row"));
        Assert.Contains("ns-field-described", Before(cut, ".refused-row"));
    }

    [Fact]
    public void TheSeatReachesEveryHelpBesideALabelledFieldInATopAlignedRow()
    {
        var cut = Render<HelpSeatHost>();

        var seated = Seated(cut);

        Assert.Equal(5, cut.FindAll(".ns-help.seated").Count);
        Assert.All(cut.FindAll(".ns-help.seated"), help => Assert.Contains(help, seated));
    }

    [Fact]
    public void TheSeatMissesACentredRowAColumnABooleanControlAndASecondHelp()
    {
        var cut = Render<HelpSeatHost>();

        var seated = Seated(cut);

        Assert.Equal(9, cut.FindAll(".ns-help").Count);
        Assert.All(seated, help => Assert.Contains("seated", help.ClassList));
    }

    // The classes of the node standing right before the row's ?.
    static string[] Before(IRenderedComponent<HelpSeatHost> cut, string row)
    {
        var before = cut.Find($"{row} > .ns-help").PreviousElementSibling;

        return before is null ? [] : [.. before.ClassList];
    }

    // Every .ns-help the rule's selectors match in the rendered page.
    static List<AngleSharp.Dom.IElement> Seated(IRenderedComponent<HelpSeatHost> cut)
    {
        return Selectors()
            .SelectMany(selector => cut.FindAll(selector))
            .Distinct()
            .ToList();
    }

    static string[] Selectors()
    {
        var lines = Lines();
        var at = Opening(lines);
        var selectors = new List<string>();

        for (var line = at; line >= 0 && (line == at || lines[line].EndsWith(',')); line--)
        {
            selectors.Add(lines[line].TrimEnd('{', ',', ' '));
        }

        Assert.NotEmpty(selectors);

        return [.. selectors];
    }

    static string[] SeatBlock()
    {
        var lines = Lines();
        var at = Opening(lines);

        return lines.Skip(at + 1).TakeWhile(line => line != "}").ToArray();
    }

    // The rule's last selector line, the one that opens its block.
    static int Opening(string[] lines)
    {
        var at = Array.FindIndex(lines, line =>
            line.StartsWith(".align-start:not(.flex-column)", StringComparison.Ordinal) && line.EndsWith('{'));

        Assert.True(at >= 0, "ns-mud.css no longer carries the help seat.");

        return at;
    }

    static string[] Lines()
    {
        return HouseStylesheet.Read().Split('\n').Select(line => line.Trim()).ToArray();
    }
}
