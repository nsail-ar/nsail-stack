// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor;
using MudBlazor.Services;
using NSail.BaseServices.WebApp;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#640, the pager a phone was shown while the browser was still being measured.
/// What is pinned is the FIRST render: a request that says it comes from a phone renders the
/// scroll branch with no pager in it, without an interop answer ever arriving — and the answer
/// the prerendering server resolved is the one the WebAssembly client adopts. nsail#4449: a
/// client the hand-off never reached reads the browser and answers the same.</summary>
public sealed class NsTableDeviceModeTests : BunitContext, IAsyncLifetime
{
    public NsTableDeviceModeTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
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
    public void APhoneClientHintScrollsFromTheFirstRender()
    {
        Serving(headers => headers["Sec-CH-UA-Mobile"] = "?1");

        var cut = Render<ScrollTableHost>(parameters => parameters.Add(p => p.Mode, NsTableMode.Auto));

        // Read with no wait on purpose: the answer came in on the request, so the branch that
        // renders first is already the final one. A pager here is the defect.
        Assert.Empty(cut.FindAll(".ns-table .mud-table-pagination"));
        Assert.NotEmpty(cut.FindAll(".ns-table"));
    }

    [Fact]
    public void APhoneUserAgentScrollsWhenNoHintIsSent()
    {
        Serving(headers => headers.UserAgent =
            "Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1");

        var cut = Render<ScrollTableHost>(parameters => parameters.Add(p => p.Mode, NsTableMode.Auto));

        Assert.Empty(cut.FindAll(".ns-table .mud-table-pagination"));
    }

    [Fact]
    public void ADesktopHintKeepsThePager()
    {
        Serving(headers =>
        {
            headers["Sec-CH-UA-Mobile"] = "?0";
            headers.UserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0 Safari/537.36";
        });

        var cut = Render<ScrollTableHost>(parameters => parameters.Add(p => p.Mode, NsTableMode.Auto));

        Assert.NotEmpty(cut.FindAll(".ns-table .mud-table-pagination"));
    }

    [Fact]
    public void AnIPadKeepsThePager()
    {
        // iPadOS reports a desktop user agent and answers the hint with ?0: a tablet has the
        // room to page, which is the answer both readings give.
        Serving(headers => headers.UserAgent =
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Safari/605.1.15");

        var cut = Render<ScrollTableHost>(parameters => parameters.Add(p => p.Mode, NsTableMode.Auto));

        Assert.NotEmpty(cut.FindAll(".ns-table .mud-table-pagination"));
    }

    [Fact]
    public async Task TheClientAdoptsTheAnswerTheServerResolved()
    {
        // The client has no request to read: what it answers is what the prerendering server
        // wrote into the document, under the key both sides agree on.
        var client = new DeviceProvider((await Handoff.Carrying("NSail.Device", true)).State);

        Assert.True(client.IsPhone);
    }

    [Fact]
    public async Task AClientHandedNothingAnswersNoPhoneWithNoBrowserToRead()
    {
        var client = new DeviceProvider((await Handoff.Empty()).State);

        Assert.False(client.IsPhone);
    }

    [Theory]
    [InlineData("", IPhone)]
    [InlineData("?1", ChromeOnAndroid)]
    [InlineData("", "Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0 Mobile Safari/537.36")]
    public async Task AClientHandedNothingOnAPhoneReadsTheBrowser(string hint, string agent)
    {
        var client = new DeviceProvider((await Handoff.Empty()).State, new Browser(hint, agent));

        Assert.True(client.IsPhone);
    }

    [Theory]
    [InlineData("?0", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0 Safari/537.36")]
    [InlineData("", IPad)]
    [InlineData("", "Mozilla/5.0 (Linux; Android 14; SM-X710) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0 Safari/537.36")]
    public async Task AClientHandedNothingOnADesktopOrATabletAnswersNo(string hint, string agent)
    {
        var client = new DeviceProvider((await Handoff.Empty()).State, new Browser(hint, agent));

        Assert.False(client.IsPhone);
    }

    [Fact]
    public async Task AClientReadLateAnswersAsOneHandedTheAnswer()
    {
        // One phone, two clients: the hand-off reached one first read and not the other.
        var handed = new DeviceProvider((await Handoff.Carrying("NSail.Device", true)).State, new Browser("", IPhone));
        var late = new DeviceProvider((await Handoff.Empty()).State, new Browser("", IPhone));
        var never = new DeviceProvider(null, new Browser("", IPhone));

        Assert.True(handed.IsPhone);
        Assert.Equal(handed.IsPhone, late.IsPhone);
        Assert.Equal(handed.IsPhone, never.IsPhone);
    }

    [Fact]
    public async Task AClientHandedNothingOnAPhoneScrollsFromTheFirstRender()
    {
        var handoff = (await Handoff.Empty()).State;

        Services.AddScoped<IDeviceProvider>(_ => new DeviceProvider(handoff, new Browser("", IPhone)));

        var cut = Render<ScrollTableHost>(parameters => parameters.Add(p => p.Mode, NsTableMode.Auto));

        Assert.Empty(cut.FindAll(".ns-table .mud-table-pagination"));
        Assert.NotEmpty(cut.FindAll(".ns-table"));
    }

    [Fact]
    public async Task ABrowserThatCannotAnswerLeavesTheClientOnNo()
    {
        var client = new DeviceProvider((await Handoff.Empty()).State, new Browser(null, null));

        Assert.False(client.IsPhone);
    }

    const string IPhone =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    const string IPad =
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Safari/605.1.15";

    const string ChromeOnAndroid =
        "Mozilla/5.0 (Linux; Android 10; K) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0 Safari/537.36";

    // The WebAssembly runtime's synchronous interop, answering what ns.js's nsapp.device reads
    // off a browser; a null hint and agent is a document whose script never answered.
    sealed class Browser(string? hint, string? agent) : IJSInProcessRuntime
    {
        public TValue Invoke<TValue>(string identifier, params object?[]? args)
        {
            Assert.Equal("nsapp.device", identifier);

            if (hint is null && agent is null)
            {
                throw new JSException("nsapp.device is not a function");
            }

            return (TValue)(object)new[] { hint, agent };
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            throw new NotSupportedException();
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            throw new NotSupportedException();
        }
    }

    void Serving(Action<IHeaderDictionary> request)
    {
        Services.AddScoped<IDeviceProvider>(_ => new RequestDeviceProvider(Accessor(request)));
    }

    static IHttpContextAccessor Accessor(Action<IHeaderDictionary> request)
    {
        var context = new DefaultHttpContext();

        request(context.Request.Headers);

        return new HttpContextAccessor { HttpContext = context };
    }
}
