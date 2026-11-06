// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using System.Text.RegularExpressions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;

namespace NSail.Components.Tests;

/// <summary>Doctrine (ui/fields.md, An autofilled field keeps its own surface and ink): the
/// browser paints its autofill tint and ink on the input alone, so the password's eye beside it
/// would read as a second, darker box. No harness makes the browser autofill, so the promise is read
/// from the stylesheet — the input's background is held where it was and never painted, its text
/// takes the field's ink — and the render pins that every box the rule must reach is an input the
/// one selector matches, before and after the eye flips it.</summary>
public sealed class AutofillSurfaceTests : BunitContext, IAsyncLifetime
{
    const string AutofillInput = ".mud-input > input.mud-input-root:-webkit-autofill";

    public AutofillSurfaceTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddStringsFromAssembly(typeof(NsPasswordField).Assembly);
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
    public void TheAutofilledInputKeepsItsBackgroundOffTheBrowsersTint()
    {
        var body = Body(AutofillInput);

        // The browser's tint is !important in its own sheet, so no author background beats it;
        // a transition whose duration and delay together outlast any session is what keeps the
        // value the input already had, whichever of the two carries the length.
        var transition = Regex.Match(body,
            @"transition:\s*background-color\s+(?<first>\d+(\.\d+)?m?s)(\s+(?<second>\d+(\.\d+)?m?s))?");

        Assert.True(transition.Success, "The autofilled input declares no background-color transition.");

        var held = Seconds(transition.Groups["first"].Value) + Seconds(transition.Groups["second"].Value);

        Assert.True(held >= TimeSpan.FromDays(1).TotalSeconds, $"The transition holds the background for {held}s only.");
    }

    [Fact]
    public void TheAutofilledTextReadsInTheFieldsOwnInk()
    {
        var body = Body(AutofillInput);

        Assert.Contains("-webkit-text-fill-color: var(--mud-palette-text-primary)", body);
        Assert.Contains("caret-color: var(--mud-palette-text-primary)", body);
    }

    // An outlined input is transparent over a card on sign-in and over the page on a settings
    // screen, so a paint of one token — a background, a gradient, or the inset shadow that fakes one — is
    // wrong on one of them and draws the seam again beside the adornment.
    [Fact]
    public void NoAutofillRulePaintsTheInput()
    {
        var painters = Rules(HouseStylesheet.Read())
            .Where(rule => rule.Selectors.Any(selector => selector.Contains(":-webkit-autofill")))
            .Where(rule => Regex.IsMatch(rule.Body, @"(?<![-\w])(background(-color|-image)?|(-webkit-)?box-shadow)\s*:"))
            .Select(rule => string.Join(", ", rule.Selectors))
            .ToArray();

        Assert.Empty(painters);
    }

    // The rule keys on the browser's state and nothing else, so focus does not narrow it; what
    // could is the eye flipping the type, which must leave the input the selector names in place.
    [Fact]
    public async Task ThePasswordInputMatchesTheRuleWhicheverWayTheEyeIsSet()
    {
        var cut = Render<NsPasswordField>(ps => ps
            .Add(p => p.Label, "Contraseña")
            .Add(p => p.Value, "hunter2"));

        AssertReached(cut.Find("input"));

        await cut.InvokeAsync(() => cut.Find(".mud-input-adornment button").Click());

        Assert.Equal("text", cut.Find("input").GetAttribute("type"));
        AssertReached(cut.Find("input"));

        await cut.InvokeAsync(() => cut.Find(".mud-input-adornment button").Click());

        Assert.Equal("password", cut.Find("input").GetAttribute("type"));
        AssertReached(cut.Find("input"));
    }

    [Fact]
    public void AnUnadornedTextFieldMatchesTheSameRule()
    {
        var cut = Render<NsTextField>(ps => ps
            .Add(p => p.Label, "Usuario")
            .Add(p => p.Value, "ana"));

        AssertReached(cut.Find("input"));
    }

    static void AssertReached(AngleSharp.Dom.IElement input)
    {
        Assert.True(input.Matches(".mud-input.mud-input-outlined > input.mud-input-root"),
            $"The autofill rule does not reach <input class=\"{input.ClassName}\">.");
    }

    static string Body(string selector)
    {
        var rule = Rules(HouseStylesheet.Read()).Where(rule => rule.Selectors.Contains(selector)).ToArray();

        Assert.Single(rule);

        return rule[0].Body;
    }

    static double Seconds(string time)
    {
        if (time.Length == 0)
        {
            return 0;
        }

        return time.EndsWith("ms", StringComparison.Ordinal)
            ? double.Parse(time[..^2], CultureInfo.InvariantCulture) / 1000
            : double.Parse(time[..^1], CultureInfo.InvariantCulture);
    }

    // A rule's prelude is everything since the previous brace; a statement ending in ';' before
    // it (an @import) is not part of it, and a group of selectors splits on its commas.
    static IEnumerable<(string[] Selectors, string Body)> Rules(string css)
    {
        var stripped = Regex.Replace(css, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        foreach (var match in Regex.Matches(stripped, @"([^{}]*)\{([^{}]*)\}", RegexOptions.Singleline).Cast<Match>())
        {
            var prelude = match.Groups[1].Value;
            var selectors = prelude[(prelude.LastIndexOf(';') + 1)..]
                .Split(',')
                .Select(selector => Regex.Replace(selector, @"\s+", " ").Trim())
                .Where(selector => selector.Length > 0)
                .ToArray();

            yield return (selectors, match.Groups[2].Value);
        }
    }
}
