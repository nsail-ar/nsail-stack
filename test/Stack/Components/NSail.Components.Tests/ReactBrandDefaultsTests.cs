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
    const string File = "src/Stack/React/NSail.React.Stack/src/brand.json";

    static readonly (bool Dark, string Chrome)[] Samples =
    [
        (false, BrandTheme.DefaultLight().Chrome),
        (true, BrandTheme.DefaultDark().Chrome),
        (false, "#d8e8d1"),
        (true, "#1c2c12"),
        (false, "#2b4a8c"),
        (true, "#c2185b"),
    ];

    [Fact]
    public void The_React_brand_file_is_the_C_sharp_brand()
    {
        var expected = Expected();
        var actual = JsonNode.Parse(System.IO.File.ReadAllText(Path.Combine(Root(), File)));

        Assert.True(
            JsonNode.DeepEquals(expected, actual),
            $"{File} differs from the C# brand; write it as:\n{expected.ToJsonString(new JsonSerializerOptions { WriteIndented = true })}");
    }

    static JsonObject Expected()
    {
        var typefaces = new JsonObject();

        foreach (var typeface in Typefaces.All)
        {
            typefaces[typeface.Key] = new JsonArray(typeface.FontFamily.Select(name => (JsonNode)name).ToArray());
        }

        var samples = new JsonArray();

        foreach (var (dark, chrome) in Samples)
        {
            var theme = dark ? BrandTheme.DefaultDark() : BrandTheme.DefaultLight();

            theme.Chrome = chrome;
            samples.Add(new JsonObject
            {
                ["dark"] = dark,
                ["chrome"] = chrome,
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
            if (System.IO.File.Exists(Path.Combine(directory.FullName, "NSail.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"NSail.sln was not found above {AppContext.BaseDirectory}.");
    }
}
