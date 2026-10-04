// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
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
/// the prerendering server resolved is the one the WebAssembly client adopts.</summary>
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
    public async Task AClientHandedNothingAnswersNoPhone()
    {
        var client = new DeviceProvider((await Handoff.Empty()).State);

        Assert.False(client.IsPhone);
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
