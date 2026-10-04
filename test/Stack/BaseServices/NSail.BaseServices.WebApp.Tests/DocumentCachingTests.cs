// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Http;
using NSail.BaseServices.WebApp;

namespace NSail.BaseServices.WebApp.Tests;

public class DocumentCachingTests
{
    [Fact]
    public void Apply_OverridesNoStore_OnHtmlDocument()
    {
        var context = new DefaultHttpContext();

        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.Headers.CacheControl = "no-cache,no-store";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers.Expires = "Thu, 01 Jan 1970 00:00:00 GMT";

        DocumentCaching.Apply(context);

        Assert.Equal("no-cache", context.Response.Headers.CacheControl.ToString());
        Assert.True(string.IsNullOrEmpty(context.Response.Headers.Pragma.ToString()));
        Assert.True(string.IsNullOrEmpty(context.Response.Headers.Expires.ToString()));
    }

    [Fact]
    public void Apply_SetsNoCache_WhenTheDocumentCarriedNoHeaderAtAll()
    {
        var context = new DefaultHttpContext();

        context.Response.ContentType = "text/html; charset=utf-8";

        DocumentCaching.Apply(context);

        Assert.Equal("no-cache", context.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public void Apply_LeavesAFingerprintedAssetResponseUntouched()
    {
        var context = new DefaultHttpContext();

        context.Response.ContentType = "text/javascript";
        context.Response.Headers.CacheControl = "max-age=31536000, immutable";

        DocumentCaching.Apply(context);

        Assert.Equal("max-age=31536000, immutable", context.Response.Headers.CacheControl.ToString());
    }
}
