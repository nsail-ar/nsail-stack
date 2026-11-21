// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.Json;
using System.Text.Json.Nodes;
using NSail.Serialization;

namespace NSail.Components.Tests;

/// <summary>@nsail/stack's <c>brand.json</c> is these classes as the wire writes them: each
/// scheme's defaults, the typeface catalog, and the ladder sampled over a few chromes, which
/// <c>brand.test.ts</c> holds the TypeScript derivation to.</summary>
public sealed class ReactBrandDefaultsTests
{
    const string Relative = "src/Stack/React/NSail.React.Stack/src/brand.json";

    // A null accent keeps the scheme's own; the picks are pale and deep on either ground, which
    // is where AccentInk leans furthest or not at all.
    static readonly (bool Dark, string Chrome, string? Accent)[] Samples =
    [
        (false, BrandTheme.DefaultLight().Chrome, null),
        (true, BrandTheme.DefaultDark().Chrome, null),
        (false, BrandTheme.DefaultLight().Chrome, "#f9b062"),
        (false, "#d8e8d1", "#0a7d4f"),
        (true, "#1c2c12", "#0a7d4f"),
        (false, "#2b4a8c", "#ffd54f"),
        (true, "#c2185b", "#7e57c2"),
    ];

    [Fact]
    public void The_React_brand_file_is_the_C_sharp_brand()
    {
        var expected = Expected();
        var actual = JsonNode.Parse(File.ReadAllText(Path.Combine(Root(), Relative)));

        Assert.True(
            JsonNode.DeepEquals(expected, actual),
            $"{Relative} differs from the C# brand; write it as:\n{expected.ToJsonString(new JsonSerializerOptions { WriteIndented = true })}");
    }

    static JsonObject Expected()
    {
        var typefaces = new JsonObject();

        foreach (var typeface in Typefaces.All)
        {
            typefaces[typeface.Key] = new JsonArray(typeface.FontFamily.Select(name => (JsonNode)name).ToArray());
        }

        var samples = new JsonArray();

        foreach (var (dark, chrome, accent) in Samples)
        {
            var theme = dark ? BrandTheme.DefaultDark() : BrandTheme.DefaultLight();

            theme.Chrome = chrome;
            theme.Accent = accent ?? theme.Accent;
            samples.Add(new JsonObject
            {
                ["dark"] = dark,
                ["chrome"] = chrome,
                ["accent"] = theme.Accent,
                ["accentInk"] = theme.AccentInk,
                ["accentOnChrome"] = theme.AccentOnChrome,
                ["ladder"] = new JsonObject
                {
                    ["background"] = theme.Background,
                    ["surface"] = theme.Surface,
                    ["surfaceRaised"] = theme.SurfaceRaised,
                    ["textPrimary"] = theme.TextPrimary,
                    ["textSecondary"] = theme.TextSecondary,
                    ["textDisabled"] = theme.TextDisabled,
                    ["lines"] = theme.Lines,
                    ["linesInputs"] = theme.LinesInputs,
                },
            });
        }

        return new JsonObject
        {
            ["light"] = JsonSerializer.SerializeToNode(BrandTheme.DefaultLight(), JsonOptions.Wire),
            ["dark"] = JsonSerializer.SerializeToNode(BrandTheme.DefaultDark(), JsonOptions.Wire),
            ["typefaces"] = typefaces,
            ["samples"] = samples,
        };
    }

    static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NSail.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"NSail.sln was not found above {AppContext.BaseDirectory}.");
    }
}
