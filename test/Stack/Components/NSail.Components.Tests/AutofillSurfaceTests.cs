// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.RegularExpressions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;

namespace NSail.Components.Tests;

/// <summary>Doctrine (ui/fields.md, An autofilled field keeps its own surface and ink): the
/// browser paints its autofill tint and ink on the input alone, so the password's eye beside it
/// read as a second, darker box. No harness makes the browser autofill, so the promise is read
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
        // a transition delayed past any session is what keeps the value the input already had.
        Assert.Matches(@"transition:\s*background-color\s+0s\s+\d{5,}s", body);
    }

    [Fact]
    public void TheAutofilledTextReadsInTheFieldsOwnInk()
    {
        var body = Body(AutofillInput);

        Assert.Contains("-webkit-text-fill-color: var(--mud-palette-text-primary)", body);
        Assert.Contains("caret-color: var(--mud-palette-text-primary)", body);
    }

    // An outlined input is transparent over a card on sign-in and over the page on a settings
    // screen, so a paint of one token — a background, or the inset shadow that fakes one — is
    // wrong on one of them and draws the seam again beside the adornment.
    [Fact]
    public void NoAutofillRulePaintsTheInput()
    {
        var painters = Rules(HouseStylesheet.Read())
            .Where(rule => rule.Selector.Contains(":-webkit-autofill"))
            .Where(rule => Regex.IsMatch(rule.Body, @"(?<![-\w])(background(-color)?|(-webkit-)?box-shadow)\s*:"))
            .Select(rule => rule.Selector)
            .ToArray();

        Assert.Empty(painters);
    }

    // The rule keys on the browser's state and nothing else, so focus does not narrow it; what
    // could is the eye flipping the type, which must leave the input the selector names in place.
    [Fact]
    public void ThePasswordInputMatchesTheRuleWhicheverWayTheEyeIsSet()
    {
        var cut = Render<NsPasswordField>(ps => ps
            .Add(p => p.Label, "Contraseña")
            .Add(p => p.Value, "hunter2"));

        AssertReached(cut.Find("input"));

        cut.Find(".mud-input-adornment button").Click();

        Assert.Equal("text", cut.Find("input").GetAttribute("type"));
        AssertReached(cut.Find("input"));

        cut.Find(".mud-input-adornment button").Click();

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
        var rule = Rules(HouseStylesheet.Read()).Where(rule => rule.Selector == selector).ToArray();

        Assert.Single(rule);

        return rule[0].Body;
    }

    static IEnumerable<(string Selector, string Body)> Rules(string css)
    {
        var stripped = Regex.Replace(css, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        foreach (var match in Regex.Matches(stripped, @"([^{}]*)\{([^{}]*)\}", RegexOptions.Singleline).Cast<Match>())
        {
            var selector = match.Groups[1].Value
                .Split('\n')
                .Last(line => !string.IsNullOrWhiteSpace(line))
                .Trim();

            yield return (selector, match.Groups[2].Value);
        }
    }
}
