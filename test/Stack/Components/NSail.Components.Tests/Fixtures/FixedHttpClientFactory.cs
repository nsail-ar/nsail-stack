// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components.Tests.Fixtures;

/// <summary>Stands in for the real IHttpClientFactory NsRemoteImage asks the service provider
/// for, so a test can hand back a canned response (or throw) without a live server on the
/// other end of the URL. One delegate per instance rather than a full HttpMessageHandler
/// subclass -- every test needs exactly one response, never a sequence.</summary>
public sealed class FixedHttpClientFactory(Func<HttpResponseMessage> respond) : IHttpClientFactory
{
    sealed class FixedHandler(Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(respond());
        }
    }

    // The real factory hands back a client whose BaseAddress is the app's own origin (how a
    // well-known relative route like "/api/branding/logo" resolves at all); a bare HttpClient
    // has none, so the fake sets one too rather than forcing every test to pass an absolute URL.
    public HttpClient CreateClient(string name)
    {
        return new HttpClient(new FixedHandler(respond)) { BaseAddress = new Uri("http://localhost/") };
    }
}
