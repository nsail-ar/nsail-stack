// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

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
}
