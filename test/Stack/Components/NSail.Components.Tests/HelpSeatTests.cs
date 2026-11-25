// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>A ? beside a labelled field in a top-aligned row sits on the field's INPUT, not on
/// its label gutter (ui/styling.md, the help seat). bUnit lays out no box model, so what is pinned
/// is the stylesheet's own rule — the field's geometry it declares — and its selectors run against
/// rendered rows: they reach the ? in a top-aligned row — past the field's under-field nodes, and
/// through a wrapper — and miss it in a centred row, a column, beside a boolean control and as a
/// row's second ?. The centre on the input is measured in a browser by the products'
/// walks.</summary>
public sealed class HelpSeatTests : BunitContext, IAsyncLifetime
{
    public HelpSeatTests()
    {
        Services.AddComponentServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog(
        [
            new FixedStrings(new Dictionary<string, string>
            {
                ["Common.NoOptions"] = "Todavía no hay {0} para elegir.",
                ["Common.CreateNew"] = "Crear {0}...",
                ["Components.MissingProbe"] = "Depósito",
            }),
        ]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(HelpSeatTests).Assembly, []));
        Services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
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

    // What the house draws in a field's under-field zone is a sibling in the row, between the
    // field and the ?: the hint while clean, the refusal's described twin while refused, a
    // password's strength bar ahead of its hint, an empty picker's door. The seat reaches past
    // each.
    [Fact]
    public void TheFieldsUnderFieldNodesStandBetweenItAndTheHelp()
    {
        var cut = Render<HelpSeatHost>();

        Assert.Equal(["ns-field-helper"], Between(cut, ".hinted-row"));
        Assert.Equal(["ns-field-described"], Between(cut, ".refused-row"));
        Assert.Equal(["ns-password-strength", "ns-field-helper"], Between(cut, ".strength-row"));
        Assert.Equal(["ns-field-missing"], Between(cut, ".door-row"));
    }

    [Fact]
    public void TheSeatReachesEveryHelpBesideALabelledFieldInATopAlignedRow()
    {
        var cut = Render<HelpSeatHost>();

        var seated = Seated(cut);

        Assert.Equal(7, cut.FindAll(".ns-help.seated").Count);
        Assert.All(cut.FindAll(".ns-help.seated"), help => Assert.Contains(help, seated));
    }

    [Fact]
    public void TheSeatMissesACentredRowAColumnABooleanControlAndASecondHelp()
    {
        var cut = Render<HelpSeatHost>();

        var seated = Seated(cut);

        Assert.Equal(11, cut.FindAll(".ns-help").Count);
        Assert.All(seated, help => Assert.Contains("seated", help.ClassList));
    }

    // The house's under-field class of each node between the row's first child and its ?.
    static string[] Between(IRenderedComponent<HelpSeatHost> cut, string row)
    {
        string[] zone = ["ns-field-helper", "ns-field-described", "ns-password-strength", "ns-field-missing"];

        return cut.Find(row).Children
            .Skip(1)
            .TakeWhile(child => !child.ClassList.Contains("ns-help"))
            .Select(child => zone.Single(child.ClassList.Contains))
            .ToArray();
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
