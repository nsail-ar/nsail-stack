// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

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
/// Each test names its own URL: the probe result caches per URL for the process's life (the
/// same trade NsImage makes per AssetId), so a shared URL across tests would let an earlier
/// test's fake response answer a later test that never touched its own factory.</summary>
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
    public void InlinesAnSvgResponseSoItInheritsCurrentColor()
    {
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
