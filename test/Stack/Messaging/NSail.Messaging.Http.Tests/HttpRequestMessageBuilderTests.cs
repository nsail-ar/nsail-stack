// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging.Http;

namespace NSail.Messaging.Http.Tests;

public class HttpRequestMessageBuilderTests
{
    enum Kind
    {
        Individual = 0,
        LegalEntity = 1
    }

    sealed class Payload
    {
        public Kind Kind { get; set; }
    }

    [Fact]
    public async Task Json_content_writes_enums_as_names()
    {
        var request = new HttpRequestMessageBuilder()
            .Method(HttpMethod.Post)
            .Path("api/parties")
            .AddJsonContent(new Payload { Kind = Kind.LegalEntity })
            .Build();

        var body = await request.Content!.ReadAsStringAsync();

        Assert.Equal("{\"kind\":\"LegalEntity\"}", body);
    }

    [Fact]
    public void Query_writes_enums_as_names()
    {
        var request = new HttpRequestMessageBuilder()
            .Method(HttpMethod.Get)
            .Path("api/parties")
            .AddQuery("kind", Kind.LegalEntity)
            .Build();

        Assert.Equal("api/parties?kind=LegalEntity", request.RequestUri!.ToString());
    }

    [Fact]
    public void Header_skips_null_and_writes_values_invariantly()
    {
        var request = new HttpRequestMessageBuilder()
            .Method(HttpMethod.Get)
            .Path("api/parties")
            .AddHeader("x-trace", (string?)null)
            .AddHeader("x-attempt", (int?)null)
            .AddHeader("x-count", 1234.5)
            .AddHeader("x-tenant", "acme")
            .AddHeader("x-many", (IEnumerable<int>?)new[] { 1, 2 })
            .AddHeader("x-none", (IEnumerable<int>?)null)
            .Build();

        Assert.False(request.Headers.Contains("x-trace"));
        Assert.False(request.Headers.Contains("x-attempt"));
        Assert.False(request.Headers.Contains("x-none"));
        Assert.Equal("1234.5", Assert.Single(request.Headers.GetValues("x-count")));
        Assert.Equal("acme", Assert.Single(request.Headers.GetValues("x-tenant")));
        Assert.Equal(new[] { "1", "2" }, request.Headers.GetValues("x-many"));
    }
}
