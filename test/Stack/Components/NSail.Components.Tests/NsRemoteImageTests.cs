// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net;
using System.Text;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSail.Components.Tests.Fixtures;

namespace NSail.Components.Tests;

/// <summary>Retires the sign-in page's inline-JS onerror hack: an &lt;img&gt; cannot see the
/// text colour of the page hosting it, so a monochrome SVG logo drawn with currentColor must
/// be inlined to inherit it, while a raster keeps the &lt;img&gt; it always had and a broken
/// route stops rendering anything at all rather than relying on the browser's own error event.
/// The probe verdict caches per service scope, and each test has its own.</summary>
public sealed class NsRemoteImageTests : BunitContext
{
    static HttpResponseMessage SvgResponse(string markup)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(markup, Encoding.UTF8, "image/svg+xml")
        };
    }

    static HttpResponseMessage RasterResponse()
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([137, 80, 78, 71])
            {
                Headers = { ContentType = new("image/png") }
            }
        };
    }

    [Fact]
    public void FitsAnInlinedSvgToItsArtworkOnceAfterTheRenderThatInlinesIt()
    {
        JSInterop.SetupVoid("nsapp.fitMark", _ => true);

        Services.AddSingleton<IHttpClientFactory>(new FixedHttpClientFactory(() =>
            SvgResponse("<svg viewBox=\"0 0 10 10\"><path d=\"M0 0h10v5H0z\"/></svg>")));

        var cut = Render<NsRemoteImage>(ps => ps.Add(p => p.Url, "/test/ns-remote-image/fit"));

        cut.WaitForState(() => cut.Markup.Contains("<svg", StringComparison.Ordinal), TimeSpan.FromSeconds(2));
        cut.WaitForAssertion(() => Assert.Single(JSInterop.Invocations["nsapp.fitMark"]));

        cut.Render();

        Assert.Single(JSInterop.Invocations["nsapp.fitMark"]);
    }

    [Fact]
    public void InlinesNothingExecutableFromAHostileSvg()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddSingleton<IHttpClientFactory>(new FixedHttpClientFactory(() =>
            SvgResponse("<svg viewBox=\"0 0 10 10\" onload=\"x()\"><script>y()</script><image href=\"z\" onerror=\"w()\"/><path d=\"M0 0h10v10H0z\"/></svg>")));

        var cut = Render<NsRemoteImage>(ps => ps.Add(p => p.Url, "/test/ns-remote-image/hostile"));

        cut.WaitForState(() => cut.Markup.Contains("<path", StringComparison.Ordinal), TimeSpan.FromSeconds(2));

        Assert.DoesNotContain("onload", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("<image", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("onerror", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void NeverFitsARasterImage()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddSingleton<IHttpClientFactory>(new FixedHttpClientFactory(RasterResponse));

        var cut = Render<NsRemoteImage>(ps => ps.Add(p => p.Url, "/test/ns-remote-image/no-fit"));

        cut.WaitForState(() => cut.FindAll("img").Count > 0, TimeSpan.FromSeconds(2));

        Assert.Empty(JSInterop.Invocations.Where(i => i.Identifier == "nsapp.fitMark"));
    }

    [Fact]
    public void InlinesAnSvgResponseSoItInheritsCurrentColor()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddSingleton<IHttpClientFactory>(new FixedHttpClientFactory(() =>
            SvgResponse("<svg viewBox=\"0 0 10 10\"><path fill=\"currentColor\" d=\"M0 0h10v10H0z\"/></svg>")));

        var cut = Render<NsRemoteImage>(ps => ps.Add(p => p.Url, "/test/ns-remote-image/svg"));

        cut.WaitForState(() => cut.Markup.Contains("<svg", StringComparison.Ordinal), TimeSpan.FromSeconds(2));

        Assert.Contains("fill=\"currentColor\"", cut.Markup);
        Assert.DoesNotContain("<img", cut.Markup);
    }

    [Fact]
    public void RendersAnImgForARasterResponse()
    {
        Services.AddSingleton<IHttpClientFactory>(new FixedHttpClientFactory(RasterResponse));

        var cut = Render<NsRemoteImage>(ps => ps
            .Add(p => p.Url, "/test/ns-remote-image/raster")
            .Add(p => p.Alt, "Brand"));

        cut.WaitForState(() => cut.FindAll("img").Count > 0, TimeSpan.FromSeconds(2));

        var img = cut.Find("img");

        Assert.Equal("/test/ns-remote-image/raster", img.GetAttribute("src"));
        Assert.Equal("Brand", img.GetAttribute("alt"));
        Assert.DoesNotContain("<svg", cut.Markup);
    }

    [Fact]
    public void RendersNothingWhenTheFetchFails()
    {
        Services.AddSingleton<IHttpClientFactory>(new FixedHttpClientFactory(() =>
            throw new HttpRequestException("simulated network failure")));

        var cut = Render<NsRemoteImage>(ps => ps.Add(p => p.Url, "/test/ns-remote-image/failed"));

        cut.WaitForState(() => cut.Markup.Trim().Length == 0, TimeSpan.FromSeconds(2));

        Assert.Empty(cut.Markup.Trim());
    }

    [Fact]
    public void FetchesAgainOnTheNextRenderAfterATransientFailure()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var calls = 0;

        Services.AddSingleton<IHttpClientFactory>(new FixedHttpClientFactory(() =>
        {
            return ++calls == 1
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : RasterResponse();
        }));

        var cut = Render<NsRemoteImage>(ps => ps.Add(p => p.Url, "/test/ns-remote-image/flaky"));

        cut.WaitForState(() => cut.Markup.Trim().Length == 0, TimeSpan.FromSeconds(2));

        cut.Render();

        cut.WaitForState(() => cut.FindAll("img").Count > 0, TimeSpan.FromSeconds(2));

        Assert.Equal(2, calls);
    }

    [Fact]
    public void RemembersA404()
    {
        var calls = 0;

        Services.AddSingleton<IHttpClientFactory>(new FixedHttpClientFactory(() =>
        {
            calls++;

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }));

        var cut = Render<NsRemoteImage>(ps => ps.Add(p => p.Url, "/test/ns-remote-image/missing"));

        cut.WaitForState(() => cut.Markup.Trim().Length == 0, TimeSpan.FromSeconds(2));

        cut.Render();
        cut.Render();

        Assert.Equal(1, calls);
        Assert.Empty(cut.Markup.Trim());
    }

    [Fact]
    public void EndsUnavailableOnATimeoutOrAnUnresolvableUrl()
    {
        Services.AddSingleton<IHttpClientFactory>(new FixedHttpClientFactory(() =>
            throw new TaskCanceledException("simulated timeout")));

        var cut = Render<NsRemoteImage>(ps => ps.Add(p => p.Url, "/test/ns-remote-image/timeout"));

        cut.WaitForState(() => cut.Markup.Trim().Length == 0, TimeSpan.FromSeconds(2));

        Assert.Empty(cut.Markup.Trim());

        using var other = new BunitContext();

        other.Services.AddSingleton<IHttpClientFactory>(new FixedHttpClientFactory(() =>
            throw new InvalidOperationException("relative url")));

        var cut2 = other.Render<NsRemoteImage>(ps => ps.Add(p => p.Url, "relative"));

        cut2.WaitForState(() => cut2.Markup.Trim().Length == 0, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void FetchesOncePerScopeAndNeverSharesAcrossScopes()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var calls = 0;

        Services.AddSingleton<IHttpClientFactory>(new FixedHttpClientFactory(() =>
        {
            calls++;

            return RasterResponse();
        }));

        var cut = Render<NsRemoteImage>(ps => ps.Add(p => p.Url, "/test/ns-remote-image/shared"));

        cut.WaitForState(() => cut.FindAll("img").Count > 0, TimeSpan.FromSeconds(2));

        cut.Render();

        var second = Render<NsRemoteImage>(ps => ps.Add(p => p.Url, "/test/ns-remote-image/shared"));

        second.WaitForState(() => second.FindAll("img").Count > 0, TimeSpan.FromSeconds(2));

        Assert.Equal(1, calls);

        using var tenantB = new BunitContext();

        tenantB.JSInterop.Mode = JSRuntimeMode.Loose;

        var callsB = 0;

        tenantB.Services.AddSingleton<IHttpClientFactory>(new FixedHttpClientFactory(() =>
        {
            callsB++;

            return RasterResponse();
        }));

        var other = tenantB.Render<NsRemoteImage>(ps => ps.Add(p => p.Url, "/test/ns-remote-image/shared"));

        other.WaitForState(() => other.FindAll("img").Count > 0, TimeSpan.FromSeconds(2));

        Assert.Equal(1, callsB);
    }

    /// <summary>Prerendering has no HTTP transport of its own, so the probe never runs and the
    /// component must not throw or hide -- it stays on the optimistic &lt;img&gt; default,
    /// exactly the branch the sign-in page's prerendered HTML is asserted against on the
    /// live slot.</summary>
    [Fact]
    public void KeepsTheImgFallbackWhenNoHttpTransportIsAvailable()
    {
        var cut = Render<NsRemoteImage>(ps => ps.Add(p => p.Url, "/test/ns-remote-image/no-transport"));

        var img = cut.Find("img");

        Assert.Equal("/test/ns-remote-image/no-transport", img.GetAttribute("src"));
    }
}
