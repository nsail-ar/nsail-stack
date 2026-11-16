// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
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
    const string Focus = "Blazor._internal.domWrapper.focus";

    readonly List<string?> _committed = [];
    readonly List<string?> _entered = [];
    readonly List<string?> _written = [];
    readonly List<bool> _stoppedWhileShown = [];
    readonly List<bool> _focusedWhileShown = [];
    Func<bool>? _shown;

    public NsTextFieldCameraTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddStringsFromAssembly(typeof(NsTextField).Assembly);
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid("nsapp.scanStop", _ => Seen(_stoppedWhileShown)).SetVoidResult();
        JSInterop.SetupVoid(Focus, _ => Seen(_focusedWhileShown)).SetVoidResult();
    }

    // Records whether the camera was still drawn when the call left for the browser.
    bool Seen(List<bool> calls)
    {
        if (_shown is not null)
        {
            calls.Add(_shown());
        }

        return true;
    }

    void Watch<TComponent>(IRenderedComponent<TComponent> cut) where TComponent : IComponent
    {
        _shown = () => cut.FindAll(".ns-camera").Count > 0;
    }

    // The stop reached the camera while its video was still in the page, by the id it started
    // with, and the cursor was placed only after the camera had gone.
    void StoppedThenFocused()
    {
        var started = Assert.Single(JSInterop.Invocations, invocation => invocation.Identifier == "nsapp.scanStart");
        var stopped = Assert.Single(JSInterop.Invocations, invocation => invocation.Identifier == "nsapp.scanStop");
        Assert.Equal(started.Arguments[0], stopped.Arguments[0]);
        Assert.Equal([true], _stoppedWhileShown);
        Assert.NotEmpty(_focusedWhileShown);
        Assert.DoesNotContain(true, _focusedWhileShown);
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

        var cut = Render<NsTextField>(ps => ps
            .Add(p => p.Label, "Código")
            .Add(p => p.Camera, camera)
            .Add(p => p.Suffix, suffix)
            .Add(p => p.KeyHint, keyHint)
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<string?>(this, value => _written.Add(value)))
            .Add(p => p.OnCommit, EventCallback.Factory.Create<string?>(this, value => _committed.Add(value)))
            .Add(p => p.OnEnterCommit, EventCallback.Factory.Create<string?>(this, value => _entered.Add(value))));
        Watch(cut);

        return cut;
    }

    void CameraAnswers(string? failure)
    {
        JSInterop.Setup<string?>("nsapp.scanStart", _ => true).SetResult(failure);
    }

    NsTextField.CameraReader Reader()
    {
        var call = Assert.Single(JSInterop.Invocations, invocation => invocation.Identifier == "nsapp.scanStart");

        return Assert.IsType<DotNetObjectReference<NsTextField.CameraReader>>(call.Arguments[2]).Value;
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
        StoppedThenFocused();
    }

    [Fact]
    public async Task TheCloseButtonStopsTheCameraAndCommitsNothing()
    {
        CameraAnswers(null);
        var cut = Render(scans: true);

        cut.Find(ButtonSelector).Click();
        cut.Find(".ns-camera button").Click();

        Assert.Empty(cut.FindAll(".ns-camera"));
        StoppedThenFocused();

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

    [Fact]
    public async Task AFieldThatGoesWhileTheCameraReadsStopsItByItsOwnId()
    {
        CameraAnswers(null);
        var cut = Render(scans: true);
        cut.Find(ButtonSelector).Click();
        _shown = null;

        await cut.InvokeAsync(() => DisposeComponentsAsync());

        var started = Assert.Single(JSInterop.Invocations, invocation => invocation.Identifier == "nsapp.scanStart");
        var stopped = Assert.Single(JSInterop.Invocations, invocation => invocation.Identifier == "nsapp.scanStop");
        Assert.IsType<string>(started.Arguments[0]);
        Assert.Equal(started.Arguments[0], stopped.Arguments[0]);
    }

    [Fact]
    public async Task TheCameraOpensAsAModalThatHoldsTheCursorAndEscape()
    {
        CameraAnswers(null);
        var holds = new EscapeHolds(() => Task.CompletedTask);
        Services.GetRequiredService<LanguageProvider>().Current = "es";
        JSInterop.Setup<bool>("nsapp.canScan").SetResult(true);
        var cut = Render<CascadingValue<EscapeHolds>>(ps => ps
            .Add(p => p.Value, holds)
            .Add(p => p.IsFixed, true)
            .AddChildContent<NsTextField>(field => field.Add(p => p.Label, "Código").Add(p => p.Camera, true)));
        Watch(cut);

        cut.Find(ButtonSelector).Click();

        var camera = cut.Find(".ns-camera");
        Assert.Equal("true", camera.GetAttribute("aria-modal"));
        Assert.Single(cut.FindComponents<MudBlazor.MudFocusTrap>());
        Assert.True(holds.Any);

        await cut.InvokeAsync(() => camera.KeyDown(new KeyboardEventArgs { Key = "Escape" }));

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".ns-camera")));
        StoppedThenFocused();
        Assert.False(holds.Any);
    }

    [Theory]
    [InlineData("AbortError")]
    [InlineData("NotReadableError")]
    public void ACameraTheBrowserCouldNotStartIsSaidTooAndTheCursorIsBackInTheBox(string failure)
    {
        CameraAnswers(failure);
        var cut = Render(scans: true);

        cut.Find(ButtonSelector).Click();

        Assert.Equal("No se pudo abrir la cámara. Escribí el código.", cut.Find(".ns-field-helper").TextContent.Trim());
        Assert.NotEmpty(_focusedWhileShown);
        Assert.DoesNotContain(true, _focusedWhileShown);
    }

    [Fact]
    public void ACameraFailureTakesTheLineOfARefusalAlreadyStanding()
    {
        CameraAnswers("NotAllowedError");
        Services.GetRequiredService<LanguageProvider>().Current = "es";
        JSInterop.Setup<bool>("nsapp.canScan").SetResult(true);
        var model = new Scan();
        var context = new EditContext(model);
        var refusals = new ValidationMessageStore(context);
        refusals.Add(() => model.Code, "Falta el código.");
        var cut = Render<CascadingValue<EditContext>>(ps => ps
            .Add(p => p.Value, context)
            .AddChildContent<NsTextField>(field => field
                .Add(p => p.Label, "Código")
                .Add(p => p.For, () => model.Code)
                .Add(p => p.Camera, true)));
        Assert.Contains("Falta el código.", cut.Find(".mud-input-helper-text").TextContent, StringComparison.Ordinal);

        cut.Find(ButtonSelector).Click();

        Assert.Contains("La cámara no tiene permiso.", cut.Find(".mud-input-helper-text").TextContent, StringComparison.Ordinal);
    }

    /// <summary>Known red (keyboard.md, Known reds): the camera is the vendor's adornment button,
    /// which the vendor keeps out of the tab order; the keyboard's own way to a code is the box
    /// itself. This pins what the vendor does today: the day an upgrade changes it, this fails
    /// and the row goes.</summary>
    [Fact]
    public void TheCameraIsAButtonTheVendorKeepsOutOfTheTabOrder()
    {
        var button = Render(scans: true).Find(ButtonSelector);

        Assert.Equal("button", button.LocalName);
        Assert.Equal("-1", button.GetAttribute("tabindex"));
    }

    [Fact]
    public void TheCameraButtonIsOneTheWhisperReaches()
    {
        var cut = Render(scans: true);

        Assert.NotNull(cut.Find(".ns-camera-field .mud-input-adornment-icon-button[aria-label]"));
    }

    sealed class Scan
    {
        public string? Code { get; set; }
    }
}
