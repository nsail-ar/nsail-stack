// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Http;
using NSail.BaseServices.WebApp;

namespace NSail.BaseServices.WebApp.Tests;

public class DocumentCachingTests
{
    [Fact]
    public void Apply_LeavesNoStoreAndPragma_OnHtmlDocument()
    {
        var context = new DefaultHttpContext();

        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.Headers.CacheControl = "no-cache,no-store";
        context.Response.Headers.Pragma = "no-cache";

        DocumentCaching.Apply(context);

        Assert.Equal("no-cache,no-store", context.Response.Headers.CacheControl.ToString());
        Assert.Equal("no-cache", context.Response.Headers.Pragma.ToString());
    }

    [Fact]
    public void Apply_AnswersPrivateNoStore_OnAnAuthenticatedDocument()
    {
        var context = new DefaultHttpContext();

        context.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity("test"));
        context.Response.ContentType = "text/html";

        DocumentCaching.Apply(context);

        Assert.Equal("private, no-store", context.Response.Headers.CacheControl.ToString());
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
