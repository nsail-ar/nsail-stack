// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;

namespace NSail.Components.Tests;

/// <summary>A pasted secret is long and a box of dots cannot be checked before it is saved: the
/// eye flips the box between password and text. It reveals what is typed and nothing else, so a
/// stored, empty box has none. The catalog is the assembly's own shipped strings, not a fixture
/// dictionary, for the reason NsPasswordFieldStoredTests gives.</summary>
public sealed class NsPasswordFieldEyeTests : BunitContext, IAsyncLifetime
{
    const string EyeSelector = ".mud-input-adornment button";

    public NsPasswordFieldEyeTests()
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
        string language = "es",
        bool isStored = false,
        string? value = "hunter2",
        bool disabled = false,
        bool readOnly = false)
    {
        Services.GetRequiredService<LanguageProvider>().Current = language;

        return Render<NsPasswordField>(ps => ps
            .Add(p => p.Label, "App Secret")
            .Add(p => p.IsStored, isStored)
            .Add(p => p.Disabled, disabled)
            .Add(p => p.ReadOnly, readOnly)
            .Add(p => p.Value, value));
    }

    [Fact]
    public void TheEyeTogglesTheBoxBetweenPasswordAndText()
    {
        var cut = Render();

        Assert.Equal("password", cut.Find("input").GetAttribute("type"));

        cut.Find(EyeSelector).Click();

        Assert.Equal("text", cut.Find("input").GetAttribute("type"));

        cut.Find(EyeSelector).Click();

        Assert.Equal("password", cut.Find("input").GetAttribute("type"));
    }

    [Theory]
    [InlineData("es", "Mostrar contraseña", "Ocultar contraseña")]
    [InlineData("en", "Show password", "Hide password")]
    public void TheEyeIsNamedForWhatItWillDo(string language, string show, string hide)
    {
        var cut = Render(language);

        Assert.Equal(show, cut.Find(EyeSelector).GetAttribute("aria-label"));

        cut.Find(EyeSelector).Click();

        Assert.Equal(hide, cut.Find(EyeSelector).GetAttribute("aria-label"));
    }

    [Fact]
    public void AStoredEmptyBoxHasNothingToRevealAndKeepsItsCheck()
    {
        var cut = Render(isStored: true, value: null);

        Assert.Empty(cut.FindAll(EyeSelector));
        Assert.NotEmpty(cut.FindAll(".ns-stored-mark"));
    }

    [Fact]
    public void TypingIntoAStoredBoxDrawsTheEye()
    {
        var cut = Render(isStored: true, value: "h");

        Assert.NotEmpty(cut.FindAll(EyeSelector));
        Assert.Empty(cut.FindAll(".ns-stored-mark"));
    }

    [Fact]
    public void ABoxNothingIsStoredInDrawsTheEyeEvenWhenEmpty()
    {
        var cut = Render(value: null);

        Assert.NotEmpty(cut.FindAll(EyeSelector));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ADisabledOrReadOnlyBoxDrawsNoEye(bool disabled, bool readOnly)
    {
        var cut = Render(disabled: disabled, readOnly: readOnly);

        Assert.Empty(cut.FindAll(EyeSelector));
    }

    /// <summary>Known red (keyboard.md, Known reds): the vendor draws its input adornment button
    /// with tabindex=-1 and offers no parameter for it, so Tab skips the eye. This pins what the
    /// vendor does today: the day an upgrade changes it, this fails and the row goes.</summary>
    [Fact]
    public void TheEyeIsAButtonTheVendorKeepsOutOfTheTabOrder()
    {
        var eye = Render().Find(EyeSelector);

        Assert.Equal("button", eye.LocalName);
        Assert.Equal("-1", eye.GetAttribute("tabindex"));
    }
}
