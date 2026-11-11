// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;

namespace NSail.Components.Tests;

/// <summary>A scan box takes a phone's camera without code of its own: where the browser has
/// its own BarcodeDetector the field draws a camera button, and the code the camera reads is
/// entered the way a reader's burst ending on Enter is — through the same commit.</summary>
public sealed class NsTextFieldCameraTests : BunitContext, IAsyncLifetime
{
    const string ButtonSelector = ".mud-input-adornment button";
    const string Code = "7790070410130";

    readonly List<string?> _committed = [];
    readonly List<string?> _entered = [];
    readonly List<string?> _written = [];

    public NsTextFieldCameraTests()
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

    IRenderedComponent<NsTextField> Render(bool scans, bool camera = true, string language = "es", string? suffix = null, string? keyHint = null)
    {
        Services.GetRequiredService<LanguageProvider>().Current = language;
        JSInterop.Setup<bool>("nsapp.canScan").SetResult(scans);

        return Render<NsTextField>(ps => ps
            .Add(p => p.Label, "Código")
            .Add(p => p.Camera, camera)
            .Add(p => p.Suffix, suffix)
            .Add(p => p.KeyHint, keyHint)
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<string?>(this, value => _written.Add(value)))
            .Add(p => p.OnCommit, EventCallback.Factory.Create<string?>(this, value => _committed.Add(value)))
            .Add(p => p.OnEnterCommit, EventCallback.Factory.Create<string?>(this, value => _entered.Add(value))));
    }

    void CameraAnswers(string? failure)
    {
        JSInterop.Setup<string?>("nsapp.scanStart", _ => true).SetResult(failure);
    }

    NsTextField.CameraReader Reader()
    {
        var call = Assert.Single(JSInterop.Invocations, invocation => invocation.Identifier == "nsapp.scanStart");

        return Assert.IsType<DotNetObjectReference<NsTextField.CameraReader>>(call.Arguments[1]).Value;
    }

    [Theory]
    [InlineData("es", "Leer con la cámara")]
    [InlineData("en", "Scan with the camera")]
    public void ABrowserThatReadsCodesDrawsANamedCameraButton(string language, string name)
    {
        var cut = Render(scans: true, language: language);

        Assert.Equal(name, cut.Find(ButtonSelector).GetAttribute("aria-label"));
    }

    [Fact]
    public async Task ACodeTheCameraReadsIsCommittedAsIfTypedAndEndedWithEnter()
    {
        CameraAnswers(null);
        var cut = Render(scans: true);

        cut.Find(ButtonSelector).Click();
        Assert.Single(cut.FindAll(".ns-camera video"));

        await cut.InvokeAsync(() => Reader().Read(Code));

        Assert.Equal([Code], _committed);
        Assert.Equal([Code], _entered);
        Assert.Equal([Code], _written);
        Assert.Empty(cut.FindAll(".ns-camera"));
        Assert.Contains(JSInterop.Invocations, invocation => invocation.Identifier == "nsapp.scanStop");
    }

    [Fact]
    public async Task TheCloseButtonStopsTheCameraAndCommitsNothing()
    {
        CameraAnswers(null);
        var cut = Render(scans: true);

        cut.Find(ButtonSelector).Click();
        cut.Find(".ns-camera button").Click();

        Assert.Empty(cut.FindAll(".ns-camera"));
        Assert.Contains(JSInterop.Invocations, invocation => invocation.Identifier == "nsapp.scanStop");

        await cut.InvokeAsync(() => Reader().Read(Code));

        Assert.Empty(_committed);
    }

    [Fact]
    public void ABrowserWithoutBarcodeDetectorDrawsNoButtonOnAFieldThatOptsIn()
    {
        var cut = Render(scans: false);

        Assert.Empty(cut.FindAll(ButtonSelector));
    }

    [Theory]
    [InlineData("NotAllowedError", "es", "La cámara no tiene permiso. Permitila en el navegador o escribí el código.")]
    [InlineData("NotAllowedError", "en", "The camera has no permission. Allow it in the browser, or type the code.")]
    [InlineData("NotFoundError", "es", "No se pudo abrir la cámara. Escribí el código.")]
    [InlineData("NotFoundError", "en", "The camera could not be opened. Type the code instead.")]
    public async Task ACameraThatCannotOpenIsSaidUnderTheBoxWhichStaysTypeable(string failure, string language, string message)
    {
        CameraAnswers(failure);
        var cut = Render(scans: true, language: language);

        cut.Find(ButtonSelector).Click();

        Assert.Empty(cut.FindAll(".ns-camera"));
        Assert.Equal(message, cut.Find(".ns-field-helper").TextContent.Trim());
        Assert.False(cut.Find("input").HasAttribute("disabled"));

        await cut.InvokeAsync(() => cut.Find("input").Input(Code));
        await cut.InvokeAsync(() => cut.Find("input").KeyDown(new KeyboardEventArgs { Key = "Enter" }));

        Assert.Equal([Code], _committed);
        Assert.Empty(cut.FindAll(".ns-field-helper"));
    }

    [Fact]
    public void AFieldThatDoesNotOptInAsksNothingAndDrawsNoCamera()
    {
        var cut = Render(scans: true, camera: false);

        Assert.Empty(cut.FindAll(ButtonSelector));
        Assert.DoesNotContain(JSInterop.Invocations, invocation => invocation.Identifier == "nsapp.canScan");
    }

    [Fact]
    public async Task OnAFieldThatOptsInAReadersBurstStillCommitsOnceOnEnter()
    {
        var cut = Render(scans: true);

        await cut.InvokeAsync(() => cut.Find("input").Input(Code));
        await cut.InvokeAsync(() => cut.Find("input").KeyDown(new KeyboardEventArgs { Key = "Enter" }));

        Assert.Equal([Code], _committed);
        Assert.Equal([Code], _entered);
    }

    [Fact]
    public void TheCameraTakesTheSlotASuffixWouldPrintIn()
    {
        var cut = Render(scans: true, suffix: "ARS");

        Assert.Single(cut.FindAll(ButtonSelector));
        Assert.DoesNotContain("ARS", cut.Find(".mud-input-adornment").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void AKeyCapBesideTheCameraIsSetOffTheButton()
    {
        var cut = Render(scans: true, keyHint: "Enter");

        Assert.Contains("ns-search-scans", cut.Find(".ns-search-box").ClassList);
        Assert.Single(cut.FindAll(".ns-search-key"));
        Assert.Single(cut.FindAll(ButtonSelector));
    }
}
