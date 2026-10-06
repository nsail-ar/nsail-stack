// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components.Tests;

/// <summary>nsail#1941 — where the pointer is coarse every action face that draws a BOX is at
/// least 44px high, so a worded act and the glyph beside it measure the same under a finger.
/// Proven by reading the stylesheet: the rule is geometry under a media query, and bUnit lays
/// out no box model at all (CollapsedLabelWhisperTests settles the whisper the same way). What
/// the rule then does to a real screen is the browser walks' (Optical E2E,
/// TicketHeaderOneRowTests).</summary>
public sealed class TouchTargetFloorTests
{
    // The industry's floor, not a pick: Apple HIG 44pt, WCAG 2.5.5 Target Size. Written here so
    // a stylesheet that quietly moved to another number comes back red with the reason.
    const string Floor = "min-height: 44px;";

    [Fact]
    public void The_floor_is_declared_once_on_the_boxed_face_and_only_where_the_pointer_is_coarse()
    {
        var lines = ReadStylesheet();

        Assert.Equal(1, lines.Count(line => line == Floor));

        // The selector it is written against, and the query it is written inside. .mud-button is
        // the chromed face and nothing else wears it — NsButton, NsLink, NsSubmit, NsClose and
        // NsMenu's labelled trigger all render one — so one declaration reaches every container
        // a finger meets it in, rather than the per-caller branch principles.md refuses.
        Assert.Equal(".mud-button {", lines[Preceding(lines, Floor, line => line.EndsWith('{'))]);
        Assert.Equal("@media (pointer: coarse) {", lines[Query(lines, Floor)]);
    }

    // min-height and not height: the icon-only square already measures 44 by construction (12px
    // around the Md glyph) and a box this GREW would be the vendor's rectangle again, the very
    // shape ns-square exists to refuse. So the floor leaves it exactly as it is, which is the
    // AC's "an icon-only action keeps its own box under either pointer". The flat worded anchor
    // is out of the rule for the other reason: it draws no box, so there is nothing to grow.
    [Fact]
    public void The_floor_sets_no_height_and_reaches_no_bare_anchor()
    {
        var lines = ReadStylesheet();

        var block = Block(lines, Query(lines, Floor));

        Assert.Contains(block, line => line == Floor);
        Assert.DoesNotContain(block, line => line.StartsWith("height:", StringComparison.Ordinal));
        Assert.DoesNotContain(block, line => line.Contains("ns-link-iconed", StringComparison.Ordinal));
    }

    /// <summary>The nearest line above a declaration that satisfies <paramref name="opens"/> —
    /// its own selector, or the query it is nested in. Fails loudly on a stylesheet that no
    /// longer carries the declaration rather than passing on its absence.</summary>
    static int Preceding(string[] lines, string declaration, Func<string, bool> opens)
    {
        var at = Array.IndexOf(lines, declaration);

        Assert.True(at >= 0, $"ns-mud.css no longer carries '{declaration}'.");

        for (var walk = at - 1; walk >= 0; walk--)
        {
            if (opens(lines[walk]))
            {
                return walk;
            }
        }

        throw new InvalidOperationException($"nothing above '{declaration}' opens a block it belongs to.");
    }

    static int Query(string[] lines, string declaration)
    {
        return Preceding(lines, declaration, line => line.StartsWith("@media ", StringComparison.Ordinal));
    }

    /// <summary>Every line of the block a line opens, counted by braces — the whole query and
    /// nothing past it, so what the floor's own block does NOT say can be asserted.</summary>
    static string[] Block(string[] lines, int opens)
    {
        var depth = 0;
        var block = new List<string>();

        for (var walk = opens; walk < lines.Length; walk++)
        {
            block.Add(lines[walk]);

            depth += lines[walk].Count(c => c == '{') - lines[walk].Count(c => c == '}');

            if (walk > opens && depth == 0)
            {
                return [.. block];
            }
        }

        throw new InvalidOperationException($"the block opened at line {opens + 1} is never closed.");
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

        throw new DirectoryNotFoundException($"NSail.sln was not found above {AppContext.BaseDirectory}.");
    }
}
