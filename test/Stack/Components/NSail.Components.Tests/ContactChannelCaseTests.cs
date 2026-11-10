// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components.Tests;

/// <summary>nsail#3221 — a contact channel's chip reads its value in the case it was saved in,
/// while every other button keeps the vendor's uppercase. Proven by reading the stylesheet:
/// bUnit computes no style, and the rendered text on a real card is the browser walk's.</summary>
public sealed class ContactChannelCaseTests
{
    const string Selector = ".ns-contact-channel .ns-button-label {";

    [Fact]
    public void The_channel_label_keeps_its_own_case_and_still_truncates()
    {
        var block = Block(ReadStylesheet(), Selector);

        Assert.Contains("text-transform: none;", block);
        Assert.Contains("text-overflow: ellipsis;", block);
        Assert.Contains("min-width: 0;", block);
    }

    // The uppercase is lifted off a button's face only inside a container that says why: a bare
    // label or button root carrying it would lower Guardar and Facturar with the e-mail.
    [Fact]
    public void No_rule_lifts_the_uppercase_off_every_button()
    {
        var lines = ReadStylesheet();

        var selectors = lines
            .Select((line, at) => (line, at))
            .Where(entry => entry.line == "text-transform: none;")
            .Select(entry => lines[Array.FindLastIndex(lines, entry.at, line => line.EndsWith('{'))])
            .Where(selector => selector.Contains(".ns-button-label") || selector.Contains(".mud-button"))
            .ToArray();

        Assert.Contains(Selector, selectors);
        Assert.All(selectors, selector => Assert.Contains(' ', selector.TrimEnd('{', ' ')));
    }

    static string[] Block(string[] lines, string selector)
    {
        var at = Array.IndexOf(lines, selector);

        Assert.True(at >= 0, $"ns-mud.css no longer carries '{selector}'.");

        var end = Array.IndexOf(lines, "}", at);

        Assert.True(end > at, $"the block '{selector}' is never closed.");

        return lines[at..end];
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
