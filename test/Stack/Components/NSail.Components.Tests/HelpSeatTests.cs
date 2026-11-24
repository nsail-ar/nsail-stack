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
/// rendered rows: they reach the ? in a top-aligned row and miss it in a centred row, a column and
/// beside a boolean control. The centre on the input is measured in a browser by the products'
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

    [Fact]
    public void TheSeatReachesTheHelpBesideALabelledFieldInATopAlignedRow()
    {
        var cut = Render<HelpSeatHost>();

        var seated = Seated(cut);

        Assert.Single(seated);
        Assert.NotNull(seated[0].Closest(".top-row"));
    }

    [Fact]
    public void TheSeatMissesACentredRowAColumnAndABooleanControl()
    {
        var cut = Render<HelpSeatHost>();

        var seated = Seated(cut);

        Assert.Equal(4, cut.FindAll(".ns-help").Count);
        Assert.DoesNotContain(seated, help => help.Closest(".centred-row") is not null);
        Assert.DoesNotContain(seated, help => help.Closest(".column") is not null);
        Assert.DoesNotContain(seated, help => help.Closest(".check-row") is not null);
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
        var lines = ReadStylesheet();
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
        var lines = ReadStylesheet();
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

    static string[] ReadStylesheet()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NSail.sln")))
            {
                var css = File.ReadAllText(Path.Combine(
                    directory.FullName,
                    "src/Stack/Components/NSail.Components.Mud/wwwroot/ns-mud.css"));

                return css.Split('\n').Select(line => line.Trim()).ToArray();
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("NSail.sln not found above the test's output directory.");
    }
}
