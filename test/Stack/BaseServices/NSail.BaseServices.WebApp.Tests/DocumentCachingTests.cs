// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Security.Claims;
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

        context.User = new ClaimsPrincipal(
            new ClaimsIdentity("test"));
        context.Response.ContentType = "text/html";

        DocumentCaching.Apply(context);

        Assert.Equal("private, no-store", context.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public void Apply_ClearsPragmaAndExpires_OnAnAnonymousDocument()
    {
        var context = new DefaultHttpContext();

        context.Response.ContentType = "text/html";
        context.Response.Headers.CacheControl = "max-age=0";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers.Expires = "0";

        DocumentCaching.Apply(context);

        Assert.Equal("no-cache", context.Response.Headers.CacheControl.ToString());
        Assert.False(context.Response.Headers.ContainsKey("Pragma"));
        Assert.False(context.Response.Headers.ContainsKey("Expires"));
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
