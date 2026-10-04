// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;

namespace NSail.Components.Tests;

/// <summary>nsail#1468: a write-only credential never travels back, so the box opens empty on
/// every visit and "nothing is stored" and "the secret is stored and not shown" look identical.
/// IsStored is the one line that tells them apart, and it says in the same breath that leaving
/// the box alone keeps what is there. It lives on the field rather than in each screen's own
/// string because five screens were copying the sentence by hand.
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

    IRenderedComponent<NsPasswordField> Render(string language, bool isStored, string? helper = null)
    {
        Services.GetRequiredService<LanguageProvider>().Current = language;

        return Render<NsPasswordField>(ps => ps
            .Add(p => p.Label, "App Secret")
            .Add(p => p.IsStored, isStored)
            .Add(p => p.Helper, helper));
    }

    [Theory]
    [InlineData("es", "Guardado. Vacío para conservarlo.")]
    [InlineData("en", "Stored. Leave this empty to keep it.")]
    public void AStoredSecret_SaysSoUnderTheBoxAndSaysHowToKeepIt(string language, string words)
    {
        var cut = Render(language, isStored: true);

        Assert.Equal(words, Assert.Single(cut.FindAll(".ns-field-helper")).TextContent);
    }

    /// <summary>Off renders no helper node at all, never an empty one — the fourteen fields that
    /// pass nothing (sign-in, recover, set-password, onboarding, CalDav) have to draw exactly
    /// what they drew before, and a reserved empty box under every password box is not that.
    /// </summary>
    [Fact]
    public void NothingStored_DrawsNoHelperNodeAtAll()
    {
        var cut = Render("es", isStored: false);

        Assert.Empty(cut.FindAll(".ns-field-helper"));
    }

    /// <summary>A screen with something more specific to say still says it: the flag only fills
    /// a hint the caller wrote none of.</summary>
    [Fact]
    public void AnExplicitHelperWinsOverTheStoredLine()
    {
        var cut = Render("es", isStored: true, helper: "Pegá el token del usuario del sistema.");

        Assert.Equal(
            "Pegá el token del usuario del sistema.",
            Assert.Single(cut.FindAll(".ns-field-helper")).TextContent);
    }
}
