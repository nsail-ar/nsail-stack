// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
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

    [Theory]
    [InlineData("a/b", "api/x/a%2Fb")]
    [InlineData("../x", "api/x/..%2Fx")]
    [InlineData("?x=1", "api/x/%3Fx%3D1")]
    [InlineData("https://evil.test/", "api/x/https%3A%2F%2Fevil.test%2F")]
    public void Route_value_is_one_escaped_segment(string value, string expected)
    {
        var request = new HttpRequestMessageBuilder()
            .Method(HttpMethod.Get)
            .Path($"api/x/{HttpRequestMessageBuilder.Segment(value)}")
            .Build();

        Assert.Equal(expected, request.RequestUri!.OriginalString);
    }

    [Theory]
    [InlineData("es-AR")]
    [InlineData("tr-TR")]
    public void Values_are_invariant_under_any_culture(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(culture);
        try
        {
            var since = new DateTime(2026, 11, 6, 12, 34, 56, 789, DateTimeKind.Utc);
            var request = new HttpRequestMessageBuilder()
                .Method(HttpMethod.Get)
                .Path($"api/x/{HttpRequestMessageBuilder.Segment(1.5m)}/{HttpRequestMessageBuilder.Segment(since)}")
                .AddQuery("d", 1.5d)
                .AddQuery("i", 1234567)
                .AddQuery("since", since)
                .AddQuery("at", new DateTimeOffset(since))
                .AddQuery("time", new TimeOnly(12, 34, 56, 789))
                .Build();

            Assert.Equal(
                "api/x/1.5/2026-11-06T12%3A34%3A56.7890000Z?d=1.5&i=1234567"
                + "&since=2026-11-06T12%3A34%3A56.7890000Z&at=2026-11-06T12%3A34%3A56.7890000%2B00%3A00"
                + "&time=12%3A34%3A56.7890000",
                request.RequestUri!.OriginalString);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
