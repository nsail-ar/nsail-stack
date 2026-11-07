// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;

namespace NSail.Components.Tests;

/// <summary>A value shown once — a generated password, a token — is carried elsewhere by hand:
/// the box's copy button puts exactly what it holds on the clipboard, and answers in its own
/// place by turning into a check until the value changes.</summary>
public sealed class NsTextFieldCopyTests : BunitContext, IAsyncLifetime
{
    const string ButtonSelector = ".mud-input-adornment button";

    public NsTextFieldCopyTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddStringsFromAssembly(typeof(NsTextField).Assembly);
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

    IRenderedComponent<NsTextField> Render(string? value, bool copy = true, string language = "es")
    {
        Services.GetRequiredService<LanguageProvider>().Current = language;

        return Render<NsTextField>(ps => ps
            .Add(p => p.Label, "Contraseña")
            .Add(p => p.Copy, copy)
            .Add(p => p.Value, value));
    }

    [Fact]
    public void ThePressWritesTheBoxsValueToTheClipboard()
    {
        var cut = Render("Kq7-mW2p");

        cut.Find(ButtonSelector).Click();

        var call = Assert.Single(JSInterop.Invocations, invocation => invocation.Identifier == "navigator.clipboard.writeText");
        Assert.Equal("Kq7-mW2p", call.Arguments[0]);
    }

    [Theory]
    [InlineData("es", "Copiar", "Copiado")]
    [InlineData("en", "Copy", "Copied")]
    public void TheButtonSaysTheValueWasCopiedUntilItChanges(string language, string copy, string copied)
    {
        var cut = Render("Kq7-mW2p", language: language);

        Assert.Equal(copy, cut.Find(ButtonSelector).GetAttribute("aria-label"));

        cut.Find(ButtonSelector).Click();

        Assert.Equal(copied, cut.Find(ButtonSelector).GetAttribute("aria-label"));

        cut.Render(ps => ps.Add(p => p.Value, "typed over"));

        Assert.Equal(copy, cut.Find(ButtonSelector).GetAttribute("aria-label"));
    }

    [Fact]
    public void AnEmptyBoxDrawsNoButton()
    {
        var cut = Render(null);

        Assert.Empty(cut.FindAll(ButtonSelector));

        cut.Render(ps => ps.Add(p => p.Value, "Kq7-mW2p"));

        Assert.Single(cut.FindAll(ButtonSelector));
    }

    /// <summary>An http origin that is not localhost has no clipboard: the press must leave the
    /// page and the value shown once standing, with the button still offering the copy.</summary>
    [Fact]
    public void AClipboardTheBrowserRefusesLeavesTheBoxAndTheButtonAsTheyWere()
    {
        JSInterop.SetupVoid("navigator.clipboard.writeText", _ => true)
            .SetException(new Microsoft.JSInterop.JSException("Cannot read properties of undefined (reading 'writeText')"));
        var cut = Render("Kq7-mW2p");

        cut.Find(ButtonSelector).Click();

        Assert.Equal("Copiar", cut.Find(ButtonSelector).GetAttribute("aria-label"));
        Assert.Equal("Kq7-mW2p", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public void ASuffixedBoxThatDoesNotAskKeepsItsSuffixAndNoButton()
    {
        var cut = Render<NsTextField>(ps => ps
            .Add(p => p.Label, "Precio")
            .Add(p => p.Suffix, "ARS")
            .Add(p => p.Value, "120"));

        Assert.Contains("ARS", cut.Find(".mud-input-adornment").TextContent, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll(ButtonSelector));
    }

    [Fact]
    public void ABoxThatDoesNotAskDrawsNoButton()
    {
        var cut = Render("Kq7-mW2p", copy: false);

        Assert.Empty(cut.FindAll(ButtonSelector));
    }

    /// <summary>Known red (keyboard.md, Known reds): the vendor keeps its adornment button out of
    /// the tab order, and the box selecting its value on focus is the keyboard's copy. This pins
    /// what the vendor does today: the day an upgrade changes it, this fails and the row goes.</summary>
    [Fact]
    public void TheButtonIsOneTheVendorKeepsOutOfTheTabOrder()
    {
        var button = Render("Kq7-mW2p").Find(ButtonSelector);

        Assert.Equal("button", button.LocalName);
        Assert.Equal("-1", button.GetAttribute("tabindex"));
    }
}
