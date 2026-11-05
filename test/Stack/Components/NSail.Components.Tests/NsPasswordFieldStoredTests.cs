// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;

namespace NSail.Components.Tests;

/// <summary>A write-only credential never travels back, so the box opens empty on every visit
/// and "nothing is stored" and "the secret is stored and not shown" look identical. IsStored
/// tells them apart inside the box: a short placeholder and an icon carrying the sentence.
///
/// The catalog is the assembly's own shipped strings, not a fixture dictionary: the words are
/// the whole feature, so a test over made-up text would pass while the screen read a key.</summary>
public sealed class NsPasswordFieldStoredTests : BunitContext, IAsyncLifetime
{
    public NsPasswordFieldStoredTests()
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

    IRenderedComponent<NsPasswordField> Render(
        string language, bool isStored, string? helper = null, string? placeholder = null, string? value = null)
    {
        Services.GetRequiredService<LanguageProvider>().Current = language;

        return Render<NsPasswordField>(ps => ps
            .Add(p => p.Label, "App Secret")
            .Add(p => p.IsStored, isStored)
            .Add(p => p.Helper, helper)
            .Add(p => p.Placeholder, placeholder)
            .Add(p => p.Value, value));
    }

    [Theory]
    [InlineData("es", "•••••••• Guardado", "Guardado. Vacío para conservarlo.")]
    [InlineData("en", "•••••••• Saved", "Saved. Leave empty to keep it.")]
    public void AStoredSecret_SaysSoInsideTheBoxAndCarriesTheSentenceOnAnIcon(string language, string placeholder, string sentence)
    {
        var cut = Render(language, isStored: true);

        Assert.Equal(placeholder, cut.Find("input").GetAttribute("placeholder"));
        Assert.Contains(sentence, cut.Markup);
        Assert.Empty(cut.FindAll(".ns-field-helper"));
    }

    /// <summary>Off renders no helper node and no placeholder or icon — the fields that pass
    /// nothing (sign-in, recover, set-password, onboarding, CalDav) draw what they always drew.
    /// </summary>
    [Fact]
    public void NothingStored_DrawsNoHelperPlaceholderOrIcon()
    {
        var cut = Render("es", isStored: false);

        Assert.Empty(cut.FindAll(".ns-field-helper"));
        Assert.Null(cut.Find("input").GetAttribute("placeholder"));
        Assert.DoesNotContain("Guardado", cut.Markup);
    }

    [Fact]
    public void TypingHidesThePlaceholderAndTheIcon()
    {
        var cut = Render("es", isStored: true, value: "hunter2");

        Assert.Null(cut.Find("input").GetAttribute("placeholder"));
        Assert.DoesNotContain("Guardado", cut.Markup);
    }

    [Fact]
    public void AnExplicitHelperWinsOverTheStoredState()
    {
        var cut = Render("es", isStored: true, helper: "Pegá el token del usuario del sistema.");

        Assert.Equal(
            "Pegá el token del usuario del sistema.",
            Assert.Single(cut.FindAll(".ns-field-helper")).TextContent);
    }

    [Fact]
    public void AnExplicitPlaceholderWinsOverTheStoredOne()
    {
        var cut = Render("es", isStored: true, placeholder: "Token");

        Assert.Equal("Token", cut.Find("input").GetAttribute("placeholder"));
    }
}
